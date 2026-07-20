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
            Direct3D11CaptureFramePool? framePool = null;
            Windows.Graphics.Capture.GraphicsCaptureSession? session = null;

            try
            {
                if (_d3dDevice == null)
                {
                    System.Diagnostics.Debug.WriteLine("[Capture] Creating D3D device...");
                    _d3dDevice = GraphicsCaptureHelper.CreateD3DDevice();
                    System.Diagnostics.Debug.WriteLine("[Capture] D3D device created");
                }

                var item = GraphicsCaptureHelper.CreateItemForMonitor(_hwnd);
                if (item == null)
                {
                    System.Diagnostics.Debug.WriteLine("[Capture] Failed to create GraphicsCaptureItem");
                    return null;
                }

                var size = item.Size;
                System.Diagnostics.Debug.WriteLine($"[Capture] Item size: {size.Width}x{size.Height}");

                framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                    _d3dDevice,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    1,
                    size);

                session = framePool.CreateCaptureSession(item);

                var tcs = new TaskCompletionSource<Direct3D11CaptureFrame?>();

                void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
                {
                    try
                    {
                        var frame = sender.TryGetNextFrame();
                        tcs.TrySetResult(frame);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Capture] FrameArrived error: {ex.Message}");
                        tcs.TrySetResult(null);
                    }
                }

                framePool.FrameArrived += OnFrameArrived;
                session.StartCapture();
                System.Diagnostics.Debug.WriteLine("[Capture] Session started, waiting for frame...");

                var captureTask = tcs.Task;
                var timeoutTask = Task.Delay(5000);
                var completed = await Task.WhenAny(captureTask, timeoutTask);

                framePool.FrameArrived -= OnFrameArrived;

                if (completed != captureTask)
                {
                    System.Diagnostics.Debug.WriteLine("[Capture] Timed out waiting for frame");
                    return null;
                }

                var capturedFrame = await captureTask;
                if (capturedFrame == null)
                {
                    System.Diagnostics.Debug.WriteLine("[Capture] No frame received");
                    return null;
                }

                using (capturedFrame)
                {
                    System.Diagnostics.Debug.WriteLine("[Capture] Frame received, converting to bitmap...");
                    var bitmap = ConvertFrameToBitmap(capturedFrame, size, _d3dDevice);
                    System.Diagnostics.Debug.WriteLine($"[Capture] Bitmap created: {bitmap.Width}x{bitmap.Height}");

                    var fileName = $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    var resultPath = Path.Combine(_logFolder, fileName);

                    await Task.Run(() => bitmap.Save(resultPath, ImageFormat.Png));
                    bitmap.Dispose();

                    System.Diagnostics.Debug.WriteLine($"[Capture] Saved: {resultPath}");
                    return resultPath;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Capture] Error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
            finally
            {
                session?.Dispose();
                framePool?.Dispose();
            }
        }

        private static Bitmap ConvertFrameToBitmap(Direct3D11CaptureFrame frame, Windows.Graphics.SizeInt32 size, IDirect3DDevice winrtDevice)
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

            using var texture = d3dSurface.QueryInterface<SharpDX.Direct3D11.Texture2D>();

            var d3d11Device = texture.Device;

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
