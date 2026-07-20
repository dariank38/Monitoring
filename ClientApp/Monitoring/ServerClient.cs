using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Monitoring
{
    public sealed class ServerClient : IDisposable
    {
        private const string ServerUrl = "http://localhost:3000";
        private const int HeartbeatIntervalSec = 30;
        private const string QueueFile = @"D:\ScreenLogs\sync_queue.json";
        private const string SentLogMarker = @"D:\ScreenLogs\last_sent_log.txt";

        private readonly HttpClient _http;
        private readonly string _hardwareId;
        private readonly string _computerName;
        private readonly string _timezone;
        private readonly System.Windows.Forms.Timer _heartbeatTimer;
        private bool _serverOnline;
        private readonly object _queueLock = new();

        private List<PendingItem> _queue = new();

        private class PendingItem
        {
            public string Type { get; set; } = "";
            public string? FilePath { get; set; }
            public DateTime CapturedAt { get; set; }
            public string? LogsJson { get; set; }
            public int LogCount { get; set; }
        }

        public ServerClient()
        {
            _hardwareId = HardwareId.GetHardwareId();
            _computerName = HardwareId.GetComputerName();

            var tz = TimeZoneInfo.Local;
            _timezone = tz.Id;

            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            _http.DefaultRequestHeaders.Add("X-Hardware-Id", _hardwareId);
            _http.DefaultRequestHeaders.Add("X-Computer-Name", _computerName);
            _http.DefaultRequestHeaders.Add("X-Timezone", _timezone);

            _heartbeatTimer = new System.Windows.Forms.Timer
            {
                Interval = HeartbeatIntervalSec * 1000
            };
            _heartbeatTimer.Tick += async (_, _) => await HeartbeatTickAsync();

            LoadQueue();
        }

        public void Start()
        {
            _ = HeartbeatTickAsync();
            _heartbeatTimer.Start();
        }

        public void Stop()
        {
            _heartbeatTimer.Stop();
        }

        private async Task HeartbeatTickAsync()
        {
            var wasOnline = _serverOnline;

            try
            {
                var json = JsonSerializer.Serialize(new
                {
                    hardware_id = _hardwareId,
                    computer_name = _computerName,
                    timezone = _timezone
                });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var resp = await _http.PostAsync($"{ServerUrl}/api/heartbeat", content);
                _serverOnline = resp.IsSuccessStatusCode;
                System.Diagnostics.Debug.WriteLine($"[Heartbeat] Status: {resp.StatusCode}, Online: {_serverOnline}");
            }
            catch (Exception ex)
            {
                _serverOnline = false;
                System.Diagnostics.Debug.WriteLine($"[Heartbeat] Failed: {ex.Message}");
            }

            if (_serverOnline && (!wasOnline || _queue.Count > 0))
            {
                _ = FlushQueueAsync();
            }
        }

        public async Task UploadScreenshotAsync(string filePath, DateTime capturedAt)
        {
            if (_serverOnline)
            {
                try
                {
                    await PostScreenshotAsync(filePath, capturedAt);
                    return;
                }
                catch
                {
                }
            }

            lock (_queueLock)
            {
                _queue.Add(new PendingItem
                {
                    Type = "screenshot",
                    FilePath = filePath,
                    CapturedAt = capturedAt
                });
                SaveQueue();
            }
        }

        public async Task UploadWorkLogsAsync(List<ActivityLogEntry> logs)
        {
            if (logs.Count == 0)
                return;

            var lastSentLine = GetLastSentLogLine();
            var newLogs = logs.Skip(lastSentLine).ToList();

            if (newLogs.Count == 0)
                return;

            var logsJson = JsonSerializer.Serialize(newLogs.Select(l => new
            {
                date = l.Date,
                start = l.Start,
                end = l.End,
                duration_sec = l.DurationSec,
                status = l.Status,
                key_count = l.KeyCount,
                mouse_count = l.MouseCount
            }));

            if (_serverOnline)
            {
                try
                {
                    await PostWorkLogsAsync(logsJson);
                    SetLastSentLogLine(lastSentLine + newLogs.Count);
                    return;
                }
                catch
                {
                }
            }

            lock (_queueLock)
            {
                _queue.Add(new PendingItem
                {
                    Type = "worklogs",
                    LogsJson = logsJson,
                    LogCount = newLogs.Count
                });
                SaveQueue();
            }
        }

        private async Task FlushQueueAsync()
        {
            List<PendingItem> toFlush;

            lock (_queueLock)
            {
                if (_queue.Count == 0)
                    return;
                toFlush = new List<PendingItem>(_queue);
            }

            var remaining = new List<PendingItem>();
            var failed = false;

            foreach (var item in toFlush)
            {
                if (failed)
                {
                    remaining.Add(item);
                    continue;
                }

                try
                {
                    if (item.Type == "screenshot" && item.FilePath != null)
                    {
                        if (File.Exists(item.FilePath))
                            await PostScreenshotAsync(item.FilePath, item.CapturedAt);
                    }
                    else if (item.Type == "worklogs" && item.LogsJson != null)
                    {
                        await PostWorkLogsAsync(item.LogsJson);
                        var currentLine = GetLastSentLogLine();
                        SetLastSentLogLine(currentLine + item.LogCount);
                    }
                }
                catch
                {
                    _serverOnline = false;
                    failed = true;
                    remaining.Add(item);
                }
            }

            lock (_queueLock)
            {
                _queue = remaining;
                SaveQueue();
            }
        }

        private async Task PostScreenshotAsync(string filePath, DateTime capturedAt)
        {
            using var form = new MultipartFormDataContent();
            using var fileStream = File.OpenRead(filePath);
            using var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");

            form.Add(fileContent, "screenshot", Path.GetFileName(filePath));
            form.Add(new StringContent(capturedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")), "captured_at");

            var resp = await _http.PostAsync($"{ServerUrl}/api/screenshots", form);
            resp.EnsureSuccessStatusCode();
        }

        private async Task PostWorkLogsAsync(string logsJson)
        {
            var wrapped = $"{{\"logs\":{logsJson}}}";
            var content = new StringContent(wrapped, Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync($"{ServerUrl}/api/worklogs", content);
            resp.EnsureSuccessStatusCode();
        }

        private static int GetLastSentLogLine()
        {
            try
            {
                if (File.Exists(SentLogMarker))
                    return int.Parse(File.ReadAllText(SentLogMarker).Trim());
            }
            catch { }
            return 0;
        }

        private static void SetLastSentLogLine(int line)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SentLogMarker)!);
                File.WriteAllText(SentLogMarker, line.ToString());
            }
            catch { }
        }

        private void LoadQueue()
        {
            try
            {
                if (File.Exists(QueueFile))
                {
                    var json = File.ReadAllText(QueueFile);
                    _queue = JsonSerializer.Deserialize<List<PendingItem>>(json) ?? new();
                }
            }
            catch { }
        }

        private void SaveQueue()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(QueueFile)!);
                var json = JsonSerializer.Serialize(_queue);
                File.WriteAllText(QueueFile, json);
            }
            catch { }
        }

        public void Dispose()
        {
            Stop();
            _http.Dispose();
            _heartbeatTimer.Dispose();
        }
    }
}
