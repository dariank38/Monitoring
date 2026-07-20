using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Monitoring
{
    public partial class MainForm : Form
    {
        private const string LogFolder = @"D:\ScreenLogs";
        private const int MinIntervalMs = 60_000;
        private const int MaxIntervalMs = 120_000;
        private static readonly Random Rng = new();

        private readonly System.Windows.Forms.Timer _captureTimer;
        private readonly System.Windows.Forms.Timer _pulseTimer;
        private readonly ActivityTracker _activityTracker;
        private readonly ServerClient _serverClient;
        private bool _isCapturing;
        private bool _pulseOn;
        private readonly List<IndicatorForm> _indicators = new();

        public MainForm()
        {
            InitializeComponent();

            Size = new Size(8, 8);
            ClientSize = new Size(8, 8);
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.LimeGreen;

            _activityTracker = new ActivityTracker();
            _serverClient = new ServerClient();

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
                Interval = Rng.Next(MinIntervalMs, MaxIntervalMs + 1)
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
            var color = _pulseOn ? Color.LimeGreen : Color.DarkGreen;
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
            SetBounds(primary.Bounds.Left, primary.Bounds.Bottom - 8, 8, 8);

            _activityTracker.Start();
            _serverClient.Start();

            await Task.Delay(100);
            await CaptureScreenAsync();
            _captureTimer.Start();
        }

        private async void CaptureTimer_Tick(object? sender, EventArgs e)
        {
            _captureTimer.Stop();
            await CaptureScreenAsync();
            _captureTimer.Interval = Rng.Next(MinIntervalMs, MaxIntervalMs + 1);
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
