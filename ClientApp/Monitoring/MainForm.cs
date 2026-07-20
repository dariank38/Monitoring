using System.Drawing;
using System.Windows.Forms;

namespace Monitoring
{
    public partial class MainForm : Form
    {
        private static readonly string LogFolder = Path.Combine(AppContext.BaseDirectory, "Logs");
        private const int DefaultIntervalMs = 90_000;
        private const int WarningMs = 10_000;
        private const int IndicatorSize = 10;
        private static readonly Random Rng = new();

        private static readonly Color[] PulseColors =
        {
            Color.Red, Color.Green, Color.Blue, Color.Black, Color.White
        };
        private const int PulseIntervalMs = 2000;

        private readonly System.Windows.Forms.Timer _captureTimer;
        private readonly System.Windows.Forms.Timer _pulseTimer;
        private readonly ActivityTracker _activityTracker;
        private readonly ServerClient _serverClient;
        private CaptureModule _captureModule;
        private bool _isCapturing;
        private bool _pulseOn;
        private bool _captureImminent;
        private bool _warningEnabled = true;
        private const int HotkeyId = 9001;
        private Color _toggleFlashColorA = Color.Empty;
        private Color _toggleFlashColorB = Color.Empty;
        private int _toggleFlashRemaining;
        private int _colorIndex;
        private readonly List<IndicatorForm> _indicators = new();

        public MainForm()
        {
            InitializeComponent();

            Size = new Size(IndicatorSize, IndicatorSize);
            ClientSize = new Size(IndicatorSize, IndicatorSize);
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.LimeGreen;

            _activityTracker = new ActivityTracker();
            _serverClient = new ServerClient();
            _serverClient.CaptureIntervalChanged += OnCaptureIntervalChanged;

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
                Interval = Math.Max(1000, DefaultIntervalMs - WarningMs)
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

            NativeMethods.RegisterHotKey(Handle, HotkeyId,
                NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_ALT,
                (uint)Keys.Oem2);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                _warningEnabled = !_warningEnabled;
                System.Diagnostics.Debug.WriteLine($"[Hotkey] Pre-capture warning {(_warningEnabled ? "enabled" : "disabled")}");

                _captureImminent = false;
                _colorIndex = 0;

                if (_warningEnabled)
                {
                    _toggleFlashColorA = Color.LimeGreen;
                    _toggleFlashColorB = Color.Black;
                    _toggleFlashRemaining = 8;
                    _pulseTimer.Interval = 200;
                }
                else
                {
                    _toggleFlashColorA = Color.Red;
                    _toggleFlashColorB = Color.Green;
                    _toggleFlashRemaining = 10;
                    _pulseTimer.Interval = 100;
                }
            }
            base.WndProc(ref m);
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
            Color color;

            if (_toggleFlashRemaining > 0)
            {
                _pulseOn = !_pulseOn;
                color = _pulseOn ? _toggleFlashColorA : _toggleFlashColorB;
                if (!_pulseOn)
                    _toggleFlashRemaining--;
                if (_toggleFlashRemaining == 0)
                    _pulseTimer.Interval = PulseIntervalMs;
            }
            else if (_captureImminent)
            {
                _pulseOn = !_pulseOn;
                color = _pulseOn ? Color.White : Color.Black;
            }
            else
            {
                color = PulseColors[_colorIndex];
                _colorIndex++;
                if (_colorIndex >= PulseColors.Length)
                    _colorIndex = 0;
            }

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
            NativeMethods.UnregisterHotKey(Handle, HotkeyId);
            _pulseTimer.Stop();
            _activityTracker.Dispose();
            _serverClient.Dispose();
            _captureModule?.Dispose();
            foreach (var indicator in _indicators)
                indicator.Close();
        }

        private async void MainForm_Load(object? sender, EventArgs e)
        {
            var primary = Screen.PrimaryScreen!;
            SetBounds(primary.Bounds.Left, primary.Bounds.Bottom - IndicatorSize, IndicatorSize, IndicatorSize);

            _activityTracker.Start();
            _serverClient.Start();

            _captureModule = new CaptureModule(LogFolder, Handle);

            await Task.Delay(100);
            await CaptureScreenAsync();
            _captureTimer.Start();
        }

        private void OnCaptureIntervalChanged(int intervalSec)
        {
            if (intervalSec < 10) return;
            var newInterval = Math.Max(1000, intervalSec * 1000 - WarningMs);
            if (_captureTimer.Interval != newInterval)
            {
                _captureTimer.Interval = newInterval;
                System.Diagnostics.Debug.WriteLine($"[Capture] Timer interval updated to {newInterval}ms (capture in {intervalSec}s)");
            }
        }

        private async void CaptureTimer_Tick(object? sender, EventArgs e)
        {
            _captureTimer.Stop();

            if (_warningEnabled)
            {
                _captureImminent = true;
                _pulseTimer.Interval = 200;
                System.Diagnostics.Debug.WriteLine($"[Capture] Warning: capture in {WarningMs / 1000}s");
                await Task.Delay(WarningMs);
                _captureImminent = false;
                _pulseTimer.Interval = PulseIntervalMs;
            }

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
                if (_captureModule == null)
                {
                    LogError("CaptureScreen", "_captureModule is null, skipping capture");
                    return;
                }

                var filePath = await _captureModule.CaptureAsync(_warningEnabled);

                if (filePath != null)
                {
                    _ = _serverClient.UploadScreenshotAsync(filePath, DateTime.Now);

                    var logs = ActivityTracker.LoadLogEntries();
                    if (logs.Count > 0)
                        _ = _serverClient.UploadWorkLogsAsync(logs);
                }
            }
            catch (Exception ex)
            {
                LogError("CaptureScreen", ex);
            }
            finally
            {
                _isCapturing = false;
            }
        }

        private static void LogError(string context, Exception ex)
        {
            var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{context}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n";
            System.Diagnostics.Debug.WriteLine(msg);
            try
            {
                Directory.CreateDirectory(LogFolder);
                File.AppendAllText(Path.Combine(LogFolder, "error.log"), msg + "\n");
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
                File.AppendAllText(Path.Combine(LogFolder, "error.log"), msg + "\n");
            }
            catch { }
        }
    }
}
