using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace Monitoring
{
    public sealed class CaptureModule : IDisposable
    {
        private readonly string _logFolder;
        private readonly string _configPath;
        private readonly IntPtr _hwnd;
        private IDirect3DDevice? _d3dDevice;

        public CaptureModule(string logFolder, IntPtr ownerHandle, string? configPath = null)
        {
            _logFolder = logFolder;
            _hwnd = ownerHandle;
            _configPath = configPath ?? ExclusionConfig.DefaultConfigPath;
        }

        public async Task<string?> CaptureAsync()
        {
            Directory.CreateDirectory(_logFolder);

            var config = ExclusionConfig.Load(_configPath);

            var excludedWindows = GetExcludedWindows(config);
            var affinityWindows = new List<IntPtr>();
            var hiddenWindows = new List<IntPtr>();

            foreach (var window in excludedWindows)
            {
                if (WindowHelper.ExcludeFromCapture(window.Handle))
                {
                    affinityWindows.Add(window.Handle);
                }
                else
                {
                    WindowHelper.HideWindow(window.Handle);
                    hiddenWindows.Add(window.Handle);
                }
            }

            if (affinityWindows.Count > 0 || hiddenWindows.Count > 0)
                await Task.Delay(50);

            string? filePath = null;

            try
            {
                filePath = await CaptureMonitorAsync();
            }
            finally
            {
                foreach (var hWnd in affinityWindows)
                {
                    WindowHelper.RestoreCapture(hWnd);
                }
                foreach (var hWnd in hiddenWindows)
                {
                    WindowHelper.ShowWindow(hWnd);
                }
            }

            return filePath;
        }

        private async Task<string?> CaptureMonitorAsync()
        {
            _d3dDevice ??= GraphicsCaptureHelper.CreateD3DDevice();

            var item = GraphicsCaptureHelper.CreateItemForMonitor(_hwnd);
            if (item == null)
                return null;

            var size = item.Size;

            using var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                _d3dDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                1,
                size);

            using var session = framePool.CreateCaptureSession(item);

            var tcs = new TaskCompletionSource<Direct3D11CaptureFrame?>();

            void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
            {
                using var frame = sender.TryGetNextFrame();
                tcs.TrySetResult(frame);
            }

            framePool.FrameArrived += OnFrameArrived;
            session.StartCapture();

            var captureTask = tcs.Task;
            var timeoutTask = Task.Delay(3000);
            var completed = await Task.WhenAny(captureTask, timeoutTask);

            framePool.FrameArrived -= OnFrameArrived;
            session.Dispose();
            framePool.Dispose();

            if (completed != captureTask)
                return null;

            var capturedFrame = await captureTask;
            if (capturedFrame == null)
                return null;

            using (capturedFrame)
            {
                var bitmap = ConvertFrameToBitmap(capturedFrame, size);

                var fileName = $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                var resultPath = Path.Combine(_logFolder, fileName);

                await Task.Run(() => bitmap.Save(resultPath, ImageFormat.Png));
                bitmap.Dispose();

                return resultPath;
            }
        }

        private static Bitmap ConvertFrameToBitmap(Direct3D11CaptureFrame frame, Windows.Graphics.SizeInt32 size)
        {
            var width = size.Width;
            var height = size.Height;
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            var data = bitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            var surface = frame.Surface;
            var d3dSurface = Direct3D11Helper.GetDXGISurface(surface);

            using var d3d11Device = new SharpDX.Direct3D11.Device(
                SharpDX.Direct3D.DriverType.Hardware,
                SharpDX.Direct3D11.DeviceCreationFlags.BgraSupport);
            using var texture = d3dSurface.QueryInterface<SharpDX.Direct3D11.Texture2D>();

            var stagingDesc = texture.Description;
            stagingDesc.Usage = SharpDX.Direct3D11.ResourceUsage.Staging;
            stagingDesc.BindFlags = SharpDX.Direct3D11.BindFlags.None;
            stagingDesc.CpuAccessFlags = SharpDX.Direct3D11.CpuAccessFlags.Read;
            stagingDesc.OptionFlags = SharpDX.Direct3D11.ResourceOptionFlags.None;

            using var stagingTexture = new SharpDX.Direct3D11.Texture2D(d3d11Device, stagingDesc);
            d3d11Device.ImmediateContext.CopyResource(texture, stagingTexture);

            var mapped = d3d11Device.ImmediateContext.MapSubresource(
                stagingTexture, 0,
                SharpDX.Direct3D11.MapMode.Read,
                SharpDX.Direct3D11.MapFlags.None);

            var sourcePtr = mapped.DataPointer;
            var destPtr = data.Scan0;

            for (int y = 0; y < height; y++)
            {
                unsafe
                {
                    Buffer.MemoryCopy(
                        (void*)(sourcePtr + y * mapped.RowPitch),
                        (void*)(destPtr + y * data.Stride),
                        data.Stride,
                        width * 4);
                }
            }

            d3d11Device.ImmediateContext.UnmapSubresource(stagingTexture, 0);
            bitmap.UnlockBits(data);

            d3dSurface.Dispose();

            return bitmap;
        }

        private static List<WindowInfo> GetExcludedWindows(ExclusionConfig config)
        {
            var result = new List<WindowInfo>();

            if (config.ExcludedProcesses.Count == 0 && config.ExcludedSites.Count == 0)
                return result;

            var windows = WindowHelper.GetVisibleWindows();

            foreach (var window in windows)
            {
                var excluded = false;

                if (config.IsProcessExcluded(window.ProcessName))
                {
                    excluded = true;
                }

                if (!excluded && config.ExcludedSites.Count > 0)
                {
                    var isBrowser = config.BrowserProcessNames.Count == 0 || config.IsBrowserProcess(window.ProcessName);
                    if (isBrowser && config.IsSiteExcluded(window.Title))
                        excluded = true;
                }

                if (excluded)
                    result.Add(window);
            }

            return result;
        }

        public void Dispose()
        {
            _d3dDevice?.Dispose();
        }
    }
}
