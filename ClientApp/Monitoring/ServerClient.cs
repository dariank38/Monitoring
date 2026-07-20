using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Monitoring
{
    public sealed class ServerClient : IDisposable
    {
        private static readonly string LogFolder = Path.Combine(AppContext.BaseDirectory, "Logs");
        private const string ConfigFile = "config.json";
        private const string DefaultServerUrl = "http://localhost:3000";
        private const int HeartbeatIntervalSec = 30;
        private const string QueueFile = "sync_queue.json";
        private const string SentLogMarker = "last_sent_log.txt";

        private readonly HttpClient _http;
        private readonly string _hardwareId;
        private readonly string _computerName;
        private readonly string _timezone;
        private readonly string _serverUrl;
        private readonly System.Windows.Forms.Timer _heartbeatTimer;
        private bool _serverOnline;
        private bool _heartbeatRunning;
        private bool _flushRunning;
        private readonly object _queueLock = new();
        private static readonly string ErrorLogFile = Path.Combine(LogFolder, "error.log");
        private const int MaxQueuedScreenshots = 50;

        public event Action<int>? CaptureIntervalChanged;

        private static void LogError(string context, Exception ex)
        {
            var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{context}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n";
            System.Diagnostics.Debug.WriteLine(msg);
            try
            {
                Directory.CreateDirectory(LogFolder);
                File.AppendAllText(ErrorLogFile, msg + "\n");
            }
            catch { }
        }

        private static void LogError(string context, string message)
        {
            var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{context}] {message}";
            System.Diagnostics.Debug.WriteLine(msg);
            try
            {
                Directory.CreateDirectory(LogFolder);
                File.AppendAllText(ErrorLogFile, msg + "\n");
            }
            catch { }
        }

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

            _serverUrl = LoadServerUrl();

            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            _http.DefaultRequestHeaders.Add("X-Hardware-Id", _hardwareId);
            _http.DefaultRequestHeaders.Add("X-Computer-Name", _computerName);
            _http.DefaultRequestHeaders.Add("X-Timezone", _timezone);

            _heartbeatTimer = new System.Windows.Forms.Timer
            {
                Interval = HeartbeatIntervalSec * 1000
            };
            _heartbeatTimer.Tick += (_, _) => _ = RunSafeAsync(HeartbeatTickAsync, "HeartbeatTimer");

            LoadQueue();
        }

        private static string LoadServerUrl()
        {
            try
            {
                Directory.CreateDirectory(LogFolder);
                var configPath = Path.Combine(LogFolder, ConfigFile);
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("server_url", out var urlEl) &&
                        urlEl.ValueKind == JsonValueKind.String)
                    {
                        var url = urlEl.GetString()!.TrimEnd('/');
                        System.Diagnostics.Debug.WriteLine($"[ServerClient] Server URL from config: {url}");
                        return url;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ServerClient] Failed to read config: {ex.Message}");
            }

            System.Diagnostics.Debug.WriteLine($"[ServerClient] Using default server URL: {DefaultServerUrl}");
            return DefaultServerUrl;
        }

        public void Start()
        {
            _ = RunSafeAsync(HeartbeatTickAsync, "Start");
            _heartbeatTimer.Start();
        }

        public void Stop()
        {
            _heartbeatTimer.Stop();
        }

        private async Task HeartbeatTickAsync()
        {
            if (_heartbeatRunning)
                return;
            _heartbeatRunning = true;

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
                var resp = await _http.PostAsync($"{_serverUrl}/api/heartbeat", content);
                _serverOnline = resp.IsSuccessStatusCode;
                System.Diagnostics.Debug.WriteLine($"[Heartbeat] Status: {resp.StatusCode}, Online: {_serverOnline}");

                if (_serverOnline)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("capture_interval_sec", out var intervalEl))
                    {
                        var interval = intervalEl.GetInt32();
                        CaptureIntervalChanged?.Invoke(interval);
                        System.Diagnostics.Debug.WriteLine($"[Heartbeat] Capture interval: {interval}s");
                    }
                }
            }
            catch (Exception ex)
            {
                _serverOnline = false;
                LogError("Heartbeat", ex);
            }

            try
            {
                if (_serverOnline && (!wasOnline || _queue.Count > 0))
                {
                    _ = RunSafeAsync(FlushQueueAsync, "FlushQueue");
                }
            }
            finally
            {
                _heartbeatRunning = false;
            }
        }

        public async Task UploadScreenshotAsync(string filePath, DateTime capturedAt)
        {
            if (_serverOnline)
            {
                try
                {
                    await PostScreenshotAsync(filePath, capturedAt);
                    TryDeleteFile(filePath);
                    return;
                }
                catch (Exception ex)
                {
                    LogError("UploadScreenshot", ex);
                }
            }

            lock (_queueLock)
            {
                var queuedScreenshots = _queue.Count(q => q.Type == "screenshot");
                if (queuedScreenshots >= MaxQueuedScreenshots)
                {
                    var oldest = _queue.FirstOrDefault(q => q.Type == "screenshot");
                    if (oldest != null && oldest.FilePath != null)
                    {
                        TryDeleteFile(oldest.FilePath);
                        _queue.Remove(oldest);
                    }
                    LogError("UploadScreenshot", $"Queue full ({MaxQueuedScreenshots} screenshots), dropped oldest");
                }

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

            try
            {
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
                    catch (Exception ex)
                    {
                        LogError("UploadWorkLogs", ex);
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
            catch (Exception ex)
            {
                LogError("UploadWorkLogs", ex);
            }
        }

        private async Task FlushQueueAsync()
        {
            if (_flushRunning)
                return;
            _flushRunning = true;

            try
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
                        {
                            await PostScreenshotAsync(item.FilePath, item.CapturedAt);
                            TryDeleteFile(item.FilePath);
                        }
                    }
                    else if (item.Type == "worklogs" && item.LogsJson != null)
                    {
                        await PostWorkLogsAsync(item.LogsJson);
                        var currentLine = GetLastSentLogLine();
                        SetLastSentLogLine(currentLine + item.LogCount);
                    }
                }
                catch (Exception ex)
                {
                    _serverOnline = false;
                    failed = true;
                    LogError("FlushQueue", ex);
                    remaining.Add(item);
                }
            }

            lock (_queueLock)
            {
                _queue = remaining;
                SaveQueue();
            }
            }
            finally
            {
                _flushRunning = false;
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

            var resp = await _http.PostAsync($"{_serverUrl}/api/screenshots", form);
            resp.EnsureSuccessStatusCode();
        }

        private async Task PostWorkLogsAsync(string logsJson)
        {
            var wrapped = $"{{\"logs\":{logsJson}}}";
            var content = new StringContent(wrapped, Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync($"{_serverUrl}/api/worklogs", content);
            resp.EnsureSuccessStatusCode();
        }

        private static int GetLastSentLogLine()
        {
            try
            {
                var markerPath = Path.Combine(LogFolder, SentLogMarker);
                if (File.Exists(markerPath))
                    return int.Parse(File.ReadAllText(markerPath).Trim());
            }
            catch { }
            return 0;
        }

        private static void SetLastSentLogLine(int line)
        {
            try
            {
                Directory.CreateDirectory(LogFolder);
                File.WriteAllText(Path.Combine(LogFolder, SentLogMarker), line.ToString());
            }
            catch { }
        }

        private void LoadQueue()
        {
            try
            {
                var queuePath = Path.Combine(LogFolder, QueueFile);
                if (File.Exists(queuePath))
                {
                    var json = File.ReadAllText(queuePath);
                    _queue = JsonSerializer.Deserialize<List<PendingItem>>(json) ?? new();
                }
            }
            catch { }
        }

        private void SaveQueue()
        {
            try
            {
                Directory.CreateDirectory(LogFolder);
                var json = JsonSerializer.Serialize(_queue);
                File.WriteAllText(Path.Combine(LogFolder, QueueFile), json);
            }
            catch { }
        }

        public void Dispose()
        {
            Stop();
            _http.Dispose();
            _heartbeatTimer.Dispose();
        }

        private static async Task RunSafeAsync(Func<Task> action, string context)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                LogError(context, ex);
            }
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { LogError("FileCleanup", ex); }
        }
    }
}
