using System;
using System.Threading;
using System.Windows;

namespace AgyPet
{
    static class Program
    {
        static Mutex singleInstance;

        [STAThread]
        static void Main(string[] args)
        {
            if (args != null && args.Length >= 2 && args[0] == "--update")
            {
                Updater.RunSelfUpdate(args[1]);
                return;
            }

            bool created;
            singleInstance = new Mutex(true, "Local\\AgyPet.SingleInstance", out created);
            if (!created) return;

            var settings = PetSettings.Load();
            var monitor = new ActivityMonitor();
            monitor.Start();

            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var updater = new Updater(settings);
            var window = new PetWindow(settings, monitor, updater);
            var tray = new TrayMenu(window, settings, updater);

            updater.Start();

            app.Exit += (s, e) =>
            {
                tray.Dispose();
                try { singleInstance.ReleaseMutex(); } catch { }
            };

            window.Show();
            app.Run();
        }
    }
}
