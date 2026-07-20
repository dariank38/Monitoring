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

            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bitmap.Size);
            }

            var excludedRects = GetExcludedWindowRects(bounds, config);

            if (excludedRects.Count > 0)
            {
                using var g = Graphics.FromImage(bitmap);
                using var brush = new SolidBrush(Color.Black);
                foreach (var rect in excludedRects)
                {
                    g.FillRectangle(brush, rect);
                }
            }

            var fileName = $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var filePath = Path.Combine(_logFolder, fileName);

            await Task.Run(() => bitmap.Save(filePath, ImageFormat.Png));

            return filePath;
        }

        private static List<Rectangle> GetExcludedWindowRects(Rectangle screenBounds, ExclusionConfig config)
        {
            var rects = new List<Rectangle>();

            if (config.ExcludedProcesses.Count == 0 && config.ExcludedSites.Count == 0)
                return rects;

            var windows = WindowHelper.GetVisibleWindows();

            foreach (var window in windows)
            {
                var excluded = false;

                if (config.IsProcessExcluded(window.ProcessName))
                {
                    excluded = true;
                }

                if (!excluded && config.IsBrowserProcess(window.ProcessName))
                {
                    if (config.IsSiteExcluded(window.Title))
                        excluded = true;
                }

                if (!excluded)
                    continue;

                var rect = window.Bounds;
                rect.Intersect(screenBounds);
                if (rect.Width > 0 && rect.Height > 0)
                    rects.Add(rect);
            }

            return rects;
        }
    }
}
