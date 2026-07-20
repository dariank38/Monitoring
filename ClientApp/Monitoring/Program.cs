namespace Monitoring
{
    internal static class Program
    {
        private const string MutexName = "Global\\Monitoring_ClientApp_SingleInstance";

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using var mutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                var current = System.Diagnostics.Process.GetCurrentProcess();
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName(current.ProcessName))
                {
                    if (proc.Id != current.Id)
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"[Program] Killing existing instance (PID {proc.Id})");
                            proc.Kill();
                            proc.WaitForExit(5000);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Program] Failed to kill existing instance: {ex.Message}");
                        }
                    }
                }

                mutex.Dispose();
                using var mutex2 = new Mutex(true, MutexName, out createdNew);
                if (!createdNew)
                {
                    System.Diagnostics.Debug.WriteLine("[Program] Could not acquire mutex after killing existing instance. Exiting.");
                    return;
                }
            }

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}