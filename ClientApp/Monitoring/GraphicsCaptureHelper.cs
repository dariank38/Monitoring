using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

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

        [DllImport("combase.dll", PreserveSig = false)]
        private static extern IntPtr RoGetActivationFactory(
            [In][MarshalAs(UnmanagedType.HString)] string activatableClassId,
            [In] ref Guid iid,
            out IntPtr factory);

        private static readonly Guid IActivationFactoryGuid = new("00000035-0000-0000-C000-000000000046");

        [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", SetLastError = true)]
        private static extern uint CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        private const uint MONITOR_DEFAULTTOPRIMARY = 1;

        public static GraphicsCaptureItem CreateItemForMonitor(IntPtr hwnd)
        {
            var hmon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTOPRIMARY);
            return CreateItemForMonitorHandle(hmon);
        }

        public static GraphicsCaptureItem CreateItemForMonitorHandle(IntPtr hmon)
        {
            var iid = IActivationFactoryGuid;
            RoGetActivationFactory(
                "Windows.Graphics.Capture.GraphicsCaptureItem",
                ref iid,
                out var factoryPtr);
            var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPtr);
            var captureIid = GraphicsCaptureItemGuid;
            var itemPointer = interop.CreateForMonitor(hmon, ref captureIid);
            var item = Marshal.GetObjectForIUnknown(itemPointer) as GraphicsCaptureItem;
            Marshal.Release(itemPointer);
            Marshal.Release(factoryPtr);
            return item!;
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
                return Marshal.GetObjectForIUnknown(pUnknown) as IDirect3DDevice;
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
