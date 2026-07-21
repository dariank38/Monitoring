using System.Runtime.InteropServices;
using System.Text;

namespace Monitoring
{
    public sealed class ActivityTracker : IDisposable
    {
        private static readonly string LogFolder = Path.Combine(AppContext.BaseDirectory, "Logs");
        private const string ActivityLogFile = "ActivityLog.csv";
        private const int IdleThresholdSec = 300; // 5 minutes of no activity = idle
        private const int AwayThresholdSec = 900; // 15 minutes of no activity = away
        private const int SaveIntervalSec = 60; // save work time every 60 seconds

        private readonly System.Windows.Forms.Timer _checkTimer;
        private DateTime _lastActivityTime = DateTime.Now;
        private DateTime _sessionStart = DateTime.Now;
        private DateTime _lastSaveTime = DateTime.Now;
        private bool _isActive = true;
        private bool _isAway = false;
        private int _keyCount;
        private int _mouseCount;

        private LowLevelKeyboardHook? _keyboardHook;
        private LowLevelMouseHook? _mouseHook;

        public bool IsRunning { get; private set; }

        public event EventHandler? ActivityChanged;

        public ActivityTracker()
        {
            _checkTimer = new System.Windows.Forms.Timer
            {
                Interval = 1000
            };
            _checkTimer.Tick += CheckTimer_Tick;
        }

        public void Start()
        {
            if (IsRunning)
                return;

            _keyboardHook = new LowLevelKeyboardHook(OnKeyboardActivity);
            _mouseHook = new LowLevelMouseHook(OnMouseActivity);

            _keyboardHook.Install();
            _mouseHook.Install();

            _sessionStart = DateTime.Now;
            _lastActivityTime = DateTime.Now;
            _lastSaveTime = DateTime.Now;
            _isActive = true;
            _isAway = false;
            _keyCount = 0;
            _mouseCount = 0;

            _checkTimer.Start();
            IsRunning = true;
        }

        public void Stop()
        {
            if (!IsRunning)
                return;

            _checkTimer.Stop();
            _keyboardHook?.Uninstall();
            _mouseHook?.Uninstall();
            IsRunning = false;

            SaveSession();
        }

        private void OnKeyboardActivity()
        {
            _keyCount++;
            _lastActivityTime = DateTime.Now;
            if (!_isActive)
            {
                _isActive = true;
                _isAway = false;
                _sessionStart = DateTime.Now;
                ActivityChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnMouseActivity()
        {
            _mouseCount++;
            _lastActivityTime = DateTime.Now;
            if (!_isActive)
            {
                _isActive = true;
                _isAway = false;
                _sessionStart = DateTime.Now;
                ActivityChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void CheckTimer_Tick(object? sender, EventArgs e)
        {
            var now = DateTime.Now;
            var idleSeconds = (now - _lastActivityTime).TotalSeconds;

            if (_isActive && idleSeconds > AwayThresholdSec)
            {
                _isActive = false;
                _isAway = true;
                SaveSession();
                _sessionStart = now;
                _lastSaveTime = now;
                ActivityChanged?.Invoke(this, EventArgs.Empty);
            }
            else if (_isActive && idleSeconds > IdleThresholdSec)
            {
                _isActive = false;
                _isAway = false;
                SaveSession();
                _sessionStart = now;
                _lastSaveTime = now;
                ActivityChanged?.Invoke(this, EventArgs.Empty);
            }
            else if (!_isActive && !_isAway && idleSeconds > AwayThresholdSec)
            {
                // Transition from Idle to Away
                _isAway = true;
                SaveSession();
                _sessionStart = now;
                _lastSaveTime = now;
                ActivityChanged?.Invoke(this, EventArgs.Empty);
            }
            else if (_isActive && (now - _lastSaveTime).TotalSeconds >= SaveIntervalSec)
            {
                SaveSession();
                _sessionStart = now;
                _lastSaveTime = now;
            }
        }

        private void SaveSession()
        {
            try
            {
                Directory.CreateDirectory(LogFolder);
                var filePath = Path.Combine(LogFolder, ActivityLogFile);

                var sb = new StringBuilder();
                if (!File.Exists(filePath))
                {
                    sb.AppendLine("Date,Start,End,DurationSec,Status,KeyCount,MouseCount");
                }

                var end = DateTime.Now;
                var duration = (int)(end - _sessionStart).TotalSeconds;

                sb.AppendLine(
                    $"{_sessionStart:yyyy-MM-dd}," +
                    $"{_sessionStart:HH:mm:ss}," +
                    $"{end:HH:mm:ss}," +
                    $"{duration}," +
                    $"{(_isActive ? "Active" : _isAway ? "Away" : "Idle")}," +
                    $"{_keyCount}," +
                    $"{_mouseCount}");

                File.AppendAllText(filePath, sb.ToString());

                _keyCount = 0;
                _mouseCount = 0;
            }
            catch
            {
            }
        }

        public ActivitySummary GetSummary()
        {
            var now = DateTime.Now;
            var idleSeconds = (now - _lastActivityTime).TotalSeconds;
            var activeDuration = _isActive ? (int)(now - _sessionStart).TotalSeconds : 0;

            return new ActivitySummary
            {
                IsActive = _isActive,
                IsIdle = !_isActive || idleSeconds > IdleThresholdSec,
                SessionStart = _sessionStart,
                ActiveDurationSec = activeDuration,
                KeyCount = _keyCount,
                MouseCount = _mouseCount,
                LastActivity = _lastActivityTime
            };
        }

        public static List<ActivityLogEntry> LoadLogEntries()
        {
            var entries = new List<ActivityLogEntry>();
            var filePath = Path.Combine(LogFolder, ActivityLogFile);

            if (!File.Exists(filePath))
                return entries;

            try
            {
                var lines = File.ReadAllLines(filePath);
                for (var i = 1; i < lines.Length; i++)
                {
                    var parts = lines[i].Split(',');
                    if (parts.Length < 7)
                        continue;

                    entries.Add(new ActivityLogEntry
                    {
                        Date = parts[0],
                        Start = parts[1],
                        End = parts[2],
                        DurationSec = int.Parse(parts[3]),
                        Status = parts[4],
                        KeyCount = int.Parse(parts[5]),
                        MouseCount = int.Parse(parts[6])
                    });
                }
            }
            catch
            {
            }

            return entries;
        }

        public void Dispose()
        {
            Stop();
            _checkTimer.Dispose();
        }
    }

    public class ActivitySummary
    {
        public bool IsActive { get; set; }
        public bool IsIdle { get; set; }
        public DateTime SessionStart { get; set; }
        public int ActiveDurationSec { get; set; }
        public int KeyCount { get; set; }
        public int MouseCount { get; set; }
        public DateTime LastActivity { get; set; }
    }

    public class ActivityLogEntry
    {
        public string Date { get; set; } = "";
        public string Start { get; set; } = "";
        public string End { get; set; } = "";
        public int DurationSec { get; set; }
        public string Status { get; set; } = "";
        public int KeyCount { get; set; }
        public int MouseCount { get; set; }
    }

    internal sealed class LowLevelKeyboardHook
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private readonly Action _callback;
        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelHookProc? _hookProc;

        public LowLevelKeyboardHook(Action callback)
        {
            _callback = callback;
        }

        public void Install()
        {
            _hookProc = HookCallback;
            using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule!;
            _hookId = NativeMethods.SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, NativeMethods.GetModuleHandle(curModule.ModuleName!), 0);
        }

        public void Uninstall()
        {
            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                _callback();
            }
            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
    }

    internal sealed class LowLevelMouseHook
    {
        private const int WH_MOUSE_LL = 14;
        private readonly Action _callback;
        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelHookProc? _hookProc;

        public LowLevelMouseHook(Action callback)
        {
            _callback = callback;
        }

        public void Install()
        {
            _hookProc = HookCallback;
            using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule!;
            _hookId = NativeMethods.SetWindowsHookEx(WH_MOUSE_LL, _hookProc, NativeMethods.GetModuleHandle(curModule.ModuleName!), 0);
        }

        public void Uninstall()
        {
            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                _callback();
            }
            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
    }

    internal delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

    internal static partial class NativeMethods
    {
        [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW")]
        internal static partial IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn, IntPtr hMod, uint dwThreadId);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool UnhookWindowsHookEx(IntPtr hhk);

        [LibraryImport("user32.dll")]
        internal static partial IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, EntryPoint = "GetModuleHandleW")]
        internal static partial IntPtr GetModuleHandle(string lpModuleName);

        internal const int SWP_NOMOVE = 0x0002;
        internal const int SWP_NOSIZE = 0x0001;
        internal const int SWP_NOACTIVATE = 0x0010;
        internal const int SWP_SHOWWINDOW = 0x0040;
        internal static readonly IntPtr HWND_TOPMOST = new(-1);
        internal static readonly IntPtr HWND_NOTOPMOST = new(-2);

        [LibraryImport("user32.dll", EntryPoint = "SetWindowPos")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool UnregisterHotKey(IntPtr hWnd, int id);

        internal const uint MOD_ALT = 0x0001;
        internal const uint MOD_CONTROL = 0x0002;
        internal const uint MOD_SHIFT = 0x0004;
        internal const int WM_HOTKEY = 0x0312;
    }
}
