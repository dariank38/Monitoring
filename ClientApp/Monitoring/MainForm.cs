using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Monitoring
{
    public partial class MainForm : Form
    {
        private static readonly string LogFolder = Path.Combine(AppContext.BaseDirectory, "Logs");
        private const int DefaultIntervalMs = 90_000;
        private const int IndicatorSize = 10;
        private static readonly Random Rng = new();

        private static readonly Color[] PulseColors =
        {
            Color.LimeGreen, Color.Cyan, Color.Magenta, Color.Yellow,
            Color.HotPink, Color.Orange, Color.MediumPurple, Color.Turquoise,
            Color.DodgerBlue
        };
        private const int BlinkTicks = 6;

        private readonly System.Windows.Forms.Timer _captureTimer;
        private readonly System.Windows.Forms.Timer _pulseTimer;
        private readonly ActivityTracker _activityTracker;
        private readonly ServerClient _serverClient;
        private bool _isCapturing;
        private bool _pulseOn;
        private int _colorIndex;
        private int _blinkRemaining;
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
                Interval = DefaultIntervalMs
            };
            _captureTimer.Tick += CaptureTimer_Tick;

            _pulseTimer = new System.Windows.Forms.Timer
            {
                Interval = 1000
            };
            _pulseTimer.Tick += PulseTimer_Tick;
            _pulseTimer.Start();

            Load += MainForm_Load;
            FormClosed += MainForm_FormClosed;
            DoubleClick += MainForm_DoubleClick;
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
            _pulseOn = !_pulseOn;

            Color color;
            if (_blinkRemaining > 0)
            {
                color = _pulseOn ? Color.White : Color.Black;
                if (!_pulseOn)
                    _blinkRemaining--;
            }
            else
            {
                var baseColor = PulseColors[_colorIndex];
                color = _pulseOn ? baseColor : ControlPaint.Dark(baseColor);

                if (!_pulseOn)
                {
                    _colorIndex++;
                    if (_colorIndex >= PulseColors.Length)
                    {
                        _colorIndex = 0;
                        _blinkRemaining = BlinkTicks;
                    }
                }
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
            _pulseTimer.Stop();
            _activityTracker.Dispose();
            _serverClient.Dispose();
            foreach (var indicator in _indicators)
                indicator.Close();
        }

        private async void MainForm_Load(object? sender, EventArgs e)
        {
            var primary = Screen.PrimaryScreen!;
            SetBounds(primary.Bounds.Left, primary.Bounds.Bottom - IndicatorSize, IndicatorSize, IndicatorSize);

            _activityTracker.Start();
            _serverClient.Start();

            await Task.Delay(100);
            await CaptureScreenAsync();
            _captureTimer.Start();
        }

        private void OnCaptureIntervalChanged(int intervalSec)
        {
            if (intervalSec < 10) return;
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

                var fileName = $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                var filePath = Path.Combine(LogFolder, fileName);

                await Task.Run(() => bitmap.Save(filePath, ImageFormat.Png));

                _ = _serverClient.UploadScreenshotAsync(filePath, DateTime.Now);

                var logs = ActivityTracker.LoadLogEntries();
                if (logs.Count > 0)
                    _ = _serverClient.UploadWorkLogsAsync(logs);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Screen capture failed:\n{ex.Message}",
                    "Monitoring Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _isCapturing = false;
            }
        }
    }
}
