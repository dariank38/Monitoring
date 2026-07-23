using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace Monitoring
{
    public partial class MainForm : Form
    {
        private static readonly string LogFolder = Path.Combine(AppContext.BaseDirectory, "Logs");
        private const int DefaultIntervalMs = 90_000;
        private const int IndicatorSize = 10;
        private const int PulseIntervalMs = 2000;

        private readonly System.Windows.Forms.Timer _captureTimer;
        private readonly System.Windows.Forms.Timer _pulseTimer;
        private readonly ActivityTracker _activityTracker;
        private readonly ServerClient _serverClient;
        private bool _isCapturing;
        private int _colorIndex;
        private readonly List<IndicatorForm> _indicators = new();
        private bool _serverOnline;
        private bool _dragging;
        private Point _dragOffset;
        private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "Logs", "config.json");

        public MainForm()
        {
            InitializeComponent();

            Size = new Size(IndicatorSize, IndicatorSize);
            ClientSize = new Size(IndicatorSize, IndicatorSize);
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Red;

            _activityTracker = new ActivityTracker();
            _serverClient = new ServerClient();
            _serverClient.CaptureIntervalChanged += OnCaptureIntervalChanged;
            _serverClient.ServerOnlineChanged += OnServerOnlineChanged;

            foreach (var screen in Screen.AllScreens)
            {
                if (screen.Primary)
                    continue;

                var indicator = new IndicatorForm(screen);
                _indicators.Add(indicator);
                indicator.Show();
            }

            _captureTimer = new System.Windows.Forms.Timer
            {
                Interval = DefaultIntervalMs
            };
            _captureTimer.Tick += CaptureTimer_Tick;

            _pulseTimer = new System.Windows.Forms.Timer
            {
                Interval = PulseIntervalMs
            };
            _pulseTimer.Tick += PulseTimer_Tick;
            _pulseTimer.Start();

            Load += MainForm_Load;
            FormClosed += MainForm_FormClosed;
            DoubleClick += MainForm_DoubleClick;
            MouseDown += MainForm_MouseDown;
            MouseMove += MainForm_MouseMove;
            MouseUp += MainForm_MouseUp;

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("View Work Logs", null, (_, _) => MainForm_DoubleClick(null, EventArgs.Empty));
            contextMenu.Items.Add("Server Settings...", null, (_, _) =>
            {
                _pulseTimer.Stop();
                using var settingsForm = new SettingsForm(_serverClient);
                settingsForm.ShowDialog();
                _pulseTimer.Start();
            });
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Exit", null, (_, _) => Close());
            ContextMenuStrip = contextMenu;
        }

        private void MainForm_DoubleClick(object? sender, EventArgs e)
        {
            _pulseTimer.Stop();
            using var logForm = new WorkLogForm(_activityTracker);
            logForm.ShowDialog();
            _pulseTimer.Start();
        }

        private void PulseTimer_Tick(object? sender, EventArgs e)
        {
            var baseColors = _serverOnline
                ? new[] { Color.Green, Color.LimeGreen }
                : new[] { Color.Red, Color.DarkRed };
            var color = baseColors[_colorIndex % baseColors.Length];
            _colorIndex++;

            BackColor = color;

            var flags = (uint)(NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
            NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, flags);

            foreach (var indicator in _indicators)
            {
                indicator.BackColor = color;
                NativeMethods.SetWindowPos(indicator.Handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, flags);
            }
        }

        private void MainForm_FormClosed(object? sender, FormClosedEventArgs e)
        {
            _pulseTimer.Stop();
            _activityTracker.Dispose();
            _serverClient.Dispose();
            foreach (var indicator in _indicators)
                indicator.Close();
            SaveIndicatorPosition(Location);
        }

        private void MainForm_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _dragOffset = e.Location;
            }
        }

        private void MainForm_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_dragging)
            {
                var newLoc = PointToScreen(e.Location);
                newLoc.Offset(-_dragOffset.X, -_dragOffset.Y);
                Location = newLoc;
            }
        }

        private void MainForm_MouseUp(object? sender, MouseEventArgs e)
        {
            if (_dragging)
            {
                _dragging = false;
                SaveIndicatorPosition(Location);
            }
        }

        private static Point? LoadIndicatorPosition()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("indicator_x", out var xEl) &&
                        doc.RootElement.TryGetProperty("indicator_y", out var yEl))
                    {
                        return new Point(xEl.GetInt32(), yEl.GetInt32());
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("LoadIndicatorPosition", ex);
            }
            return null;
        }

        private static void SaveIndicatorPosition(Point pos)
        {
            try
            {
                var dir = Path.GetDirectoryName(ConfigPath)!;
                Directory.CreateDirectory(dir);
                string json;
                if (File.Exists(ConfigPath))
                {
                    var existing = File.ReadAllText(ConfigPath);
                    using var doc = JsonDocument.Parse(existing);
                    var writer = new StringBuilder("{");
                    var first = true;
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (prop.Name == "indicator_x" || prop.Name == "indicator_y") continue;
                        if (!first) writer.Append(',');
                        first = false;
                        writer.Append($"\"{prop.Name}\":{prop.Value.GetRawText()}");
                    }
                    if (!first) writer.Append(',');
                    writer.Append($"\"indicator_x\":{pos.X},\"indicator_y\":{pos.Y}}}");
                    json = writer.ToString();
                }
                else
                {
                    json = $"{{\"indicator_x\":{pos.X},\"indicator_y\":{pos.Y}}}";
                }
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                Logger.Log("SaveIndicatorPosition", ex);
            }
        }

        private async void MainForm_Load(object? sender, EventArgs e)
        {
            var savedPos = LoadIndicatorPosition();
            if (savedPos.HasValue)
                SetBounds(savedPos.Value.X, savedPos.Value.Y, IndicatorSize, IndicatorSize);
            else
            {
                var primary = Screen.PrimaryScreen!;
                SetBounds(primary.Bounds.Left, primary.Bounds.Bottom - IndicatorSize, IndicatorSize, IndicatorSize);
            }

            _activityTracker.Start();
            _serverClient.Start();

            await Task.Delay(100);
            await CaptureScreenAsync();
            _captureTimer.Start();
        }

        private void OnServerOnlineChanged(bool online)
        {
            _serverOnline = online;
        }

        private void OnCaptureIntervalChanged(int intervalSec)
        {
            if (intervalSec < 30) return;
            var newInterval = intervalSec * 1000;
            if (_captureTimer.Interval != newInterval)
            {
                _captureTimer.Interval = newInterval;
                System.Diagnostics.Debug.WriteLine($"[Capture] Interval updated to {intervalSec}s");
            }
        }

        private async void CaptureTimer_Tick(object? sender, EventArgs e)
        {
            _captureTimer.Stop();
            await CaptureScreenAsync();
            _captureTimer.Start();
        }

        private async Task CaptureScreenAsync()
        {
            if (_isCapturing)
                return;

            _isCapturing = true;

            try
            {
                Directory.CreateDirectory(LogFolder);

                var bounds = SystemInformation.VirtualScreen;

                using var bitmap = new Bitmap(bounds.Width, bounds.Height);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bitmap.Size);
                }

                var fileName = $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                var filePath = Path.Combine(LogFolder, fileName);

                await Task.Run(() =>
                {
                    var jpegEncoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    var encoderParams = new EncoderParameters(1);
                    encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 80L);
                    bitmap.Save(filePath, jpegEncoder, encoderParams);
                });

                _ = _serverClient.UploadScreenshotAsync(filePath, DateTime.Now);

                var logs = ActivityTracker.LoadLogEntries();
                if (logs.Count > 0)
                    _ = _serverClient.UploadWorkLogsAsync(logs);
            }
            catch (Exception ex)
            {
                Logger.Log("CaptureScreen", ex);
            }
            finally
            {
                _isCapturing = false;
            }
        }

    }
}
