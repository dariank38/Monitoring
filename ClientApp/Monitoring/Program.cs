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
            using var mutex = new Mutex(true, MutexName, out var createdNew);
            if (!createdNew)
            {
                System.Diagnostics.Debug.WriteLine("[Program] Another instance is already running. Exiting.");
                return;
            }

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}