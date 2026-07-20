using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace Monitoring
{
    public static class GraphicsCaptureHelper
    {
        private static readonly Guid GraphicsCaptureItemGuid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

        [ComImport]
        [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [ComVisible(true)]
        private interface IGraphicsCaptureItemInterop
        {
            IntPtr CreateForWindow([In] IntPtr window, [In] ref Guid iid);
            IntPtr CreateForMonitor([In] IntPtr monitor, [In] ref Guid iid);
        }

        [DllImport("combase.dll", EntryPoint = "RoGetActivationFactory", CallingConvention = CallingConvention.StdCall)]
        private static extern int RoGetActivationFactory(
            IntPtr activatableClassId,
            ref Guid iid,
            out IntPtr factory);

        [DllImport("combase.dll", EntryPoint = "WindowsCreateString", CallingConvention = CallingConvention.StdCall)]
        private static extern int WindowsCreateString(
            [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
            uint length,
            out IntPtr hString);

        [DllImport("combase.dll", EntryPoint = "WindowsDeleteString", CallingConvention = CallingConvention.StdCall)]
        private static extern int WindowsDeleteString(IntPtr hString);

        private static readonly Guid IActivationFactoryGuid = new("00000035-0000-0000-C000-000000000046");

        [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", SetLastError = true)]
        private static extern uint CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        private const uint MONITOR_DEFAULTTOPRIMARY = 1;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFOEX
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        private delegate bool EnumMonitorsProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, EnumMonitorsProc lpfnEnum, IntPtr dwData);

        public class MonitorInfo
        {
            public IntPtr Handle { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }

        public static List<MonitorInfo> GetAllMonitors()
        {
            var monitors = new List<MonitorInfo>();

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref RECT rect, IntPtr data) =>
            {
                var info = new MONITORINFOEX();
                info.cbSize = (uint)Marshal.SizeOf<MONITORINFOEX>();
                if (GetMonitorInfo(hMon, ref info))
                {
                    monitors.Add(new MonitorInfo
                    {
                        Handle = hMon,
                        X = info.rcMonitor.Left,
                        Y = info.rcMonitor.Top,
                        Width = info.rcMonitor.Right - info.rcMonitor.Left,
                        Height = info.rcMonitor.Bottom - info.rcMonitor.Top
                    });
                }
                return true;
            }, IntPtr.Zero);

            return monitors;
        }

        public static GraphicsCaptureItem CreateItemForMonitor(IntPtr hwnd)
        {
            var hmon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTOPRIMARY);
            System.Diagnostics.Debug.WriteLine($"[Capture] hmon=0x{hmon.ToInt64():X}, hwnd=0x{hwnd.ToInt64():X}");
            return CreateItemForMonitorHandle(hmon);
        }

        public static GraphicsCaptureItem CreateItemForMonitorHandle(IntPtr hmon)
        {
            var className = "Windows.Graphics.Capture.GraphicsCaptureItem";
            WindowsCreateString(className, (uint)className.Length, out var hString);

            var iid = IActivationFactoryGuid;
            var hr = RoGetActivationFactory(hString, ref iid, out var factoryPtr);
            WindowsDeleteString(hString);

            if (hr != 0)
            {
                System.Diagnostics.Debug.WriteLine($"[Capture] RoGetActivationFactory failed: 0x{hr:X8}");
                return null!;
            }

            try
            {
                var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPtr);
                var captureIid = GraphicsCaptureItemGuid;
                var itemPointer = interop.CreateForMonitor(hmon, ref captureIid);

                if (itemPointer == IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine("[Capture] CreateForMonitor returned null pointer");
                    return null!;
                }

                try
                {
                    var item = MarshalInspectable<GraphicsCaptureItem>.FromAbi(itemPointer);
                    System.Diagnostics.Debug.WriteLine($"[Capture] GraphicsCaptureItem created, size={item.Size.Width}x{item.Size.Height}");
                    return item;
                }
                finally
                {
                    Marshal.Release(itemPointer);
                }
            }
            finally
            {
                Marshal.Release(factoryPtr);
            }
        }

        public static IDirect3DDevice CreateD3DDevice()
        {
            using var d3d11 = new SharpDX.Direct3D11.Device(
                SharpDX.Direct3D.DriverType.Hardware,
                SharpDX.Direct3D11.DeviceCreationFlags.BgraSupport);

            using var dxgiDevice = d3d11.QueryInterface<SharpDX.DXGI.Device3>();

            CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out var pUnknown);
            try
            {
                return MarshalInterface<IDirect3DDevice>.FromAbi(pUnknown);
            }
            finally
            {
                Marshal.Release(pUnknown);
            }
        }
    }

    public static class Direct3D11Helper
    {
        private static readonly Guid IID_ID3D11Texture2D = new("6F15AAF2-D208-4E89-9AB4-4EAD5C733242");

        public static SharpDX.DXGI.Surface GetDXGISurface(object surface)
        {
            var unkPtr = Marshal.GetIUnknownForObject(surface);
            try
            {
                var textureIid = IID_ID3D11Texture2D;
                var hr = Marshal.QueryInterface(unkPtr, ref textureIid, out var texturePtr);
                if (hr != 0 || texturePtr == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"QueryInterface for ID3D11Texture2D failed: 0x{hr:X8}");
                }

                try
                {
                    var texture = Marshal.GetObjectForIUnknown(texturePtr) as SharpDX.Direct3D11.Texture2D;
                    var dxgiSurface = texture!.QueryInterface<SharpDX.DXGI.Surface>();
                    texture.Dispose();
                    return dxgiSurface;
                }
                finally
                {
                    Marshal.Release(texturePtr);
                }
            }
            finally
            {
                Marshal.Release(unkPtr);
            }
        }
    }
}
