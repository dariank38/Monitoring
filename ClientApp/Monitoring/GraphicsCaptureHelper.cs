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
                return MarshalInspectable<IDirect3DDevice>.FromAbi(pUnknown);
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

        [ComImport]
        [Guid("A9B3D012-3DF2-4EE3-BEF7-5C25D9D5A1D6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [ComVisible(true)]
        private interface IDirect3DDxgiInterfaceAccess
        {
            IntPtr GetInterface([In] ref Guid iid);
        }

        public static SharpDX.DXGI.Surface GetDXGISurface(object surface)
        {
            var access = (IDirect3DDxgiInterfaceAccess)surface;
            var iid = IID_ID3D11Texture2D;
            var ptr = access.GetInterface(ref iid);
            var texture = Marshal.GetObjectForIUnknown(ptr) as SharpDX.Direct3D11.Texture2D;
            var dxgiSurface = texture!.QueryInterface<SharpDX.DXGI.Surface>();
            texture.Dispose();
            return dxgiSurface;
        }
    }
}
