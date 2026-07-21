using System.Text;

namespace Monitoring
{
    public static class Logger
    {
        private static readonly string LogFolder = Path.Combine(AppContext.BaseDirectory, "Logs");
        private static readonly string ErrorLogFile = Path.Combine(LogFolder, "error.log");
        private const long MaxErrorLogBytes = 1024 * 1024; // 1MB

        public static void Log(string context, Exception ex)
        {
            var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{context}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n";
            System.Diagnostics.Debug.WriteLine(msg);
            WriteError(msg + "\n");
        }

        public static void Log(string context, string message)
        {
            var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{context}] {message}";
            System.Diagnostics.Debug.WriteLine(msg);
            WriteError(msg + "\n");
        }

        private static void WriteError(string text)
        {
            try
            {
                Directory.CreateDirectory(LogFolder);

                // Cap error.log size — keep last 1MB
                if (File.Exists(ErrorLogFile))
                {
                    var fi = new FileInfo(ErrorLogFile);
                    if (fi.Length > MaxErrorLogBytes)
                    {
                        var bytes = File.ReadAllBytes(ErrorLogFile);
                        var keep = new byte[bytes.Length - (int)MaxErrorLogBytes];
                        Array.Copy(bytes, bytes.Length - keep.Length, keep, 0, keep.Length);
                        File.WriteAllBytes(ErrorLogFile, keep);
                    }
                }

                File.AppendAllText(ErrorLogFile, text);
            }
            catch { }
        }
    }
}
