using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace AgyPet
{
    class CrossApp : Application
    {
        PetSettings settings;
        ActivityMonitor monitor;
        Updater updater;
        PetWindow mainWin;
        TrayMenu trayMenu;

        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
            RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                settings = PetSettings.Load();
                monitor = new ActivityMonitor();
                monitor.Start();
                updater = new Updater(
                    settings,
                    action => Dispatcher.UIThread.Post(action),
                    Program.ShutdownApplication);

                mainWin = new PetWindow(settings, monitor, updater);
                trayMenu = new TrayMenu(mainWin, settings, updater);
                updater.Start();

                desktop.Exit += (s, e) =>
                {
                    try { trayMenu.Dispose(); } catch { }
                    Program.ReleaseSingleInstance();
                };

                desktop.MainWindow = mainWin;
                mainWin.Show();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }

    static class Program
    {
        static Mutex singleInstance;

        [STAThread]
        public static void Main(string[] args)
        {
            if (args != null && args.Length >= 2 && args[0] == "--update")
            {
                Updater.RunSelfUpdate(args[1]);
                return;
            }

            bool created;
            singleInstance = new Mutex(true, "AgyPet.Cross.SingleInstance", out created);
            if (!created) return;

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<CrossApp>()
                .UsePlatformDetect()
                .With(new MacOSPlatformOptions { ShowInDock = false });
        }

        public static void ReleaseSingleInstance()
        {
            try { if (singleInstance != null) singleInstance.ReleaseMutex(); } catch { }
        }

        public static void ShutdownApplication()
        {
            var lt = Application.Current != null ? Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime : null;
            if (lt != null) lt.Shutdown();
        }
    }
}
