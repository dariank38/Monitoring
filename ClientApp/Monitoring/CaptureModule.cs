using System.Drawing;
using System.Drawing.Imaging;

namespace Monitoring
{
    public sealed class CaptureModule
    {
        private readonly string _logFolder;
        private readonly string _configPath;

        public CaptureModule(string logFolder, string? configPath = null)
        {
            _logFolder = logFolder;
            _configPath = configPath ?? ExclusionConfig.DefaultConfigPath;
        }

        public async Task<string?> CaptureAsync()
        {
            Directory.CreateDirectory(_logFolder);

            var config = ExclusionConfig.Load(_configPath);
            var bounds = SystemInformation.VirtualScreen;

            var excludedWindows = GetExcludedWindows(config);
            var markedWindows = new List<IntPtr>();

            foreach (var window in excludedWindows)
            {
                if (WindowHelper.ExcludeFromCapture(window.Handle))
                    markedWindows.Add(window.Handle);
            }

            if (markedWindows.Count > 0)
                await Task.Delay(50);

            string? filePath = null;

            try
            {
                using var bitmap = new Bitmap(bounds.Width, bounds.Height);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bitmap.Size);
                }

                var fileName = $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                filePath = Path.Combine(_logFolder, fileName);

                await Task.Run(() => bitmap.Save(filePath, ImageFormat.Png));
            }
            finally
            {
                foreach (var hWnd in markedWindows)
                {
                    WindowHelper.RestoreCapture(hWnd);
                }
            }

            return filePath;
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
    }
}
