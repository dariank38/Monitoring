using System.Runtime.InteropServices;
using System.Text;

namespace Monitoring
{
    public class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public string ProcessName { get; set; } = "";
        public string Title { get; set; } = "";
        public Rectangle Bounds { get; set; }
    }

    public static class WindowHelper
    {
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;

        public const uint WDA_NONE = 0x00000000;
        public const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public static List<WindowInfo> GetVisibleWindows()
        {
            var windows = new List<WindowInfo>();

            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd))
                    return true;

                var sb = new StringBuilder(512);
                GetWindowText(hWnd, sb, sb.Capacity);
                var title = sb.ToString();

                if (string.IsNullOrEmpty(title))
                    return true;

                GetWindowRect(hWnd, out var rect);

                if (rect.Right <= rect.Left || rect.Bottom <= rect.Top)
                    return true;

                GetWindowThreadProcessId(hWnd, out var pid);
                string processName = "";
                try
                {
                    using var proc = System.Diagnostics.Process.GetProcessById((int)pid);
                    processName = proc.ProcessName.ToLowerInvariant() + ".exe";
                }
                catch { }

                windows.Add(new WindowInfo
                {
                    Handle = hWnd,
                    ProcessName = processName,
                    Title = title,
                    Bounds = new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top)
                });

                return true;
            }, IntPtr.Zero);

            return windows;
        }

        public static void HideWindow(IntPtr hWnd)
        {
            ShowWindow(hWnd, SW_HIDE);
        }

        public static void ShowWindow(IntPtr hWnd)
        {
            ShowWindow(hWnd, SW_SHOW);
        }

        public static bool IsMinimized(IntPtr hWnd)
        {
            return IsIconic(hWnd);
        }

        public static bool ExcludeFromCapture(IntPtr hWnd)
        {
            return SetWindowDisplayAffinity(hWnd, WDA_EXCLUDEFROMCAPTURE);
        }

        public static bool RestoreCapture(IntPtr hWnd)
        {
            return SetWindowDisplayAffinity(hWnd, WDA_NONE);
        }
    }
}
