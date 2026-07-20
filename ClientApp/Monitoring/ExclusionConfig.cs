using System.Text.Json;

namespace Monitoring
{
    public class ExclusionConfig
    {
        public List<string> ExcludedProcesses { get; set; } = new();
        public List<string> ExcludedSites { get; set; } = new();
        public List<string> BrowserProcessNames { get; set; } = new();

        public static readonly string DefaultConfigPath = Path.Combine(AppContext.BaseDirectory, "Logs", "exclusions.json");

        public static ExclusionConfig Load(string? path = null)
        {
            var configPath = path ?? DefaultConfigPath;

            try
            {
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<ExclusionConfig>(json);
                    if (config != null)
                        return config;
                }
            }
            catch { }

            var defaultConfig = new ExclusionConfig();
            Save(defaultConfig, configPath);
            return defaultConfig;
        }

        public static void Save(ExclusionConfig config, string? path = null)
        {
            var configPath = path ?? DefaultConfigPath;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
            }
            catch { }
        }

        public bool IsProcessExcluded(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return false;

            return ExcludedProcesses.Any(p =>
                processName.Contains(p, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsSiteExcluded(string windowTitle)
        {
            if (string.IsNullOrEmpty(windowTitle) || ExcludedSites.Count == 0)
                return false;

            return ExcludedSites.Any(site =>
                windowTitle.Contains(site, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsBrowserProcess(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return false;

            return BrowserProcessNames.Any(p =>
                processName.Contains(p, StringComparison.OrdinalIgnoreCase));
        }
    }
}
