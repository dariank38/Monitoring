using System.Drawing;
using System.Drawing.Imaging;
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
        private IDirect3DDevice? _d3dDevice;

        public CaptureModule(string logFolder, IntPtr ownerHandle, string? configPath = null)
        {
            _logFolder = logFolder;
            _configPath = configPath ?? ExclusionConfig.DefaultConfigPath;
        }

        public async Task<string?> CaptureAsync(bool applyExclusions = true)
        {
            Directory.CreateDirectory(_logFolder);

            var excludedWindows = applyExclusions ? GetExcludedWindows(ExclusionConfig.Load(_configPath)) : new List<WindowInfo>();
            var affinityWindows = new List<IntPtr>();
            var hiddenWindows = new List<IntPtr>();

            foreach (var window in excludedWindows)
            {
                try
                {
                    if (window.IsSiteExclusion)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Capture] Site exclusion: hiding {window.ProcessName} - \"{window.Title}\"");
                        WindowHelper.HideWindow(window.Handle);
                        hiddenWindows.Add(window.Handle);
                    }
                    else if (WindowHelper.ExcludeFromCapture(window.Handle))
                    {
                        System.Diagnostics.Debug.WriteLine($"[Capture] Process exclusion: affinity {window.ProcessName} - \"{window.Title}\"");
                        affinityWindows.Add(window.Handle);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[Capture] Process exclusion: hiding {window.ProcessName} - \"{window.Title}\"");
                        WindowHelper.HideWindow(window.Handle);
                        hiddenWindows.Add(window.Handle);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Capture] Failed to exclude {window.ProcessName} - \"{window.Title}\": {ex.Message}");
                }
            }

            if (affinityWindows.Count > 0 || hiddenWindows.Count > 0)
                await Task.Delay(200);

            string? filePath = null;

            try
            {
                filePath = await CaptureMonitorAsync();
                if (filePath == null)
                {
                    System.Diagnostics.Debug.WriteLine("[Capture] Graphics Capture failed, falling back to CopyFromScreen");
                    filePath = await CaptureFallbackAsync();
                }
            }
            finally
            {
                foreach (var hWnd in affinityWindows)
                {
                    try { WindowHelper.RestoreCapture(hWnd); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Capture] Failed to restore affinity: {ex.Message}"); }
                }
                foreach (var hWnd in hiddenWindows)
                {
                    try { WindowHelper.ShowWindow(hWnd); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Capture] Failed to restore window: {ex.Message}"); }
                }
            }

            return filePath;
        }

        private async Task<string?> CaptureMonitorAsync()
        {
            try
            {
                if (_d3dDevice == null)
                {
                    System.Diagnostics.Debug.WriteLine("[Capture] Creating D3D device...");
                    _d3dDevice = GraphicsCaptureHelper.CreateD3DDevice();
                    System.Diagnostics.Debug.WriteLine("[Capture] D3D device created");
                }

                var monitors = GraphicsCaptureHelper.GetAllMonitors();
                System.Diagnostics.Debug.WriteLine($"[Capture] Found {monitors.Count} monitor(s)");

                if (monitors.Count == 0)
                    return null;

                var virtualScreen = SystemInformation.VirtualScreen;
                var combinedBitmap = new Bitmap(virtualScreen.Width, virtualScreen.Height, PixelFormat.Format32bppArgb);
                using var g = Graphics.FromImage(combinedBitmap);
                g.Clear(Color.Black);

                foreach (var mon in monitors)
                {
                    System.Diagnostics.Debug.WriteLine($"[Capture] Capturing monitor at ({mon.X},{mon.Y}) {mon.Width}x{mon.Height}");

                    var frame = await CaptureSingleMonitorAsync(mon.Handle);
                    if (frame == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Capture] Failed to capture monitor 0x{mon.Handle.ToInt64():X}");
                        continue;
                    }

                    using (frame)
                    {
                        var tempFile = Path.Combine(_logFolder, $"_temp_{Guid.NewGuid():N}.png");
                        await ConvertFrameToFileAsync(frame, tempFile);

                        using var monBitmap = new Bitmap(tempFile);
                        var offsetX = mon.X - virtualScreen.X;
                        var offsetY = mon.Y - virtualScreen.Y;
                        g.DrawImage(monBitmap, offsetX, offsetY);

                        try { File.Delete(tempFile); } catch { }
                    }
                }

                var fileName = $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                var resultPath = Path.Combine(_logFolder, fileName);

                await Task.Run(() => SaveJpeg(combinedBitmap, resultPath));
                combinedBitmap.Dispose();

                System.Diagnostics.Debug.WriteLine($"[Capture] Saved: {resultPath}");
                return resultPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Capture] Error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        private async Task<string?> CaptureFallbackAsync()
        {
            try
            {
                var bounds = SystemInformation.VirtualScreen;
                using var bitmap = new Bitmap(bounds.Width, bounds.Height);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bitmap.Size);
                }
                var fileName = $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                var filePath = Path.Combine(_logFolder, fileName);
                await Task.Run(() => SaveJpeg(bitmap, filePath));
                System.Diagnostics.Debug.WriteLine($"[Capture] Fallback saved: {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Capture] Fallback error: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        private static void SaveJpeg(Bitmap bitmap, string filePath)
        {
            var jpegEncoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 80L);
            bitmap.Save(filePath, jpegEncoder, encoderParams);
        }

        private async Task<Direct3D11CaptureFrame?> CaptureSingleMonitorAsync(IntPtr hmon)
        {
            Direct3D11CaptureFramePool? framePool = null;
            Windows.Graphics.Capture.GraphicsCaptureSession? session = null;

            try
            {
                var item = GraphicsCaptureHelper.CreateItemForMonitorHandle(hmon);
                if (item == null)
                    return null;

                var size = item.Size;

                framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                    _d3dDevice!,
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

                var captureTask = tcs.Task;
                var timeoutTask = Task.Delay(5000);
                var completed = await Task.WhenAny(captureTask, timeoutTask);

                framePool.FrameArrived -= OnFrameArrived;

                if (completed != captureTask)
                {
                    System.Diagnostics.Debug.WriteLine("[Capture] Timed out waiting for frame");
                    return null;
                }

                return await captureTask;
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
                var isSite = false;

                if (config.IsProcessExcluded(window.ProcessName))
                {
                    excluded = true;
                }

                if (!excluded && config.ExcludedSites.Count > 0 && config.BrowserProcessNames.Count > 0)
                {
                    if (config.IsBrowserProcess(window.ProcessName) && config.IsSiteExcluded(window.Title))
                    {
                        excluded = true;
                        isSite = true;
                    }
                }

                if (excluded)
                {
                    window.IsSiteExclusion = isSite;
                    result.Add(window);
                }
            }

            return result;
        }

        public void Dispose()
        {
            _d3dDevice?.Dispose();
        }
    }
}
