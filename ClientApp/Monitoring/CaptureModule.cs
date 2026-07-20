using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using WinRT;

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
                    var fileName = $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    var resultPath = Path.Combine(_logFolder, fileName);

                    await ConvertFrameToFileAsync(capturedFrame, resultPath);

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

        private static async Task ConvertFrameToFileAsync(Direct3D11CaptureFrame frame, string filePath)
        {
            var softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface, BitmapAlphaMode.Premultiplied);

            using var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create);
            var winrtStream = WindowsRuntimeStreamExtensions.AsRandomAccessStream(fileStream);

            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, winrtStream);
            encoder.SetSoftwareBitmap(softwareBitmap);
            await encoder.FlushAsync();

            softwareBitmap.Dispose();
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
