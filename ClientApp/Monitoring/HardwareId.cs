using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace Monitoring
{
    public static class HardwareId
    {
        public static string GetHardwareId()
        {
            var cpu = GetWmiProperty("Win32_Processor", "ProcessorId");
            var motherboard = GetWmiProperty("Win32_BaseBoard", "SerialNumber");
            var disk = GetWmiProperty("Win32_DiskDrive", "SerialNumber");

            var raw = $"{cpu}|{motherboard}|{disk}";
            return HashString(raw);
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
