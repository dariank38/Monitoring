using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace Monitoring
{
    public static class HardwareId
    {
        private static readonly string CacheFile = Path.Combine(AppContext.BaseDirectory, "Logs", "hardware_id.txt");

        public static string GetHardwareId()
        {
            try
            {
                if (File.Exists(CacheFile))
                {
                    var cached = File.ReadAllText(CacheFile).Trim();
                    if (!string.IsNullOrEmpty(cached))
                        return cached;
                }
            }
            catch { }

            var cpu = GetWmiProperty("Win32_Processor", "ProcessorId");
            var motherboard = GetWmiProperty("Win32_BaseBoard", "SerialNumber");
            var disk = GetWmiProperty("Win32_DiskDrive", "SerialNumber");

            var raw = $"{cpu}|{motherboard}|{disk}";
            var hash = HashString(raw);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CacheFile)!);
                File.WriteAllText(CacheFile, hash);
            }
            catch { }

            return hash;
        }

        public static string GetComputerName()
        {
            return Environment.MachineName;
        }

        private static string GetWmiProperty(string wmiClass, string property)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}");
                foreach (var obj in searcher.Get())
                {
                    var value = obj[property]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }
            }
            catch
            {
            }

            return "unknown";
        }

        private static string HashString(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
