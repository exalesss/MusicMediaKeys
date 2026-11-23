using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

namespace MusicMediaKeys
{
    internal static class Program
    {
        internal static (bool IsInstall, bool IsUninstall) ResolveLaunchMode(string[] args, string? processPath)
        {
            string exeName = Path.GetFileName(processPath ?? "") ?? "";
            bool isUninstall = exeName.Equals("uninstall.exe", StringComparison.OrdinalIgnoreCase) ||
                               args.Any(a =>
                                   a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) ||
                                   a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase));
            bool isInstall = !isUninstall && (
                exeName.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase) ||
                exeName.Equals("Setup.exe", StringComparison.OrdinalIgnoreCase) ||
                args.Any(a =>
                    a.Equals("--install", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("/install", StringComparison.OrdinalIgnoreCase)));
            return (isInstall, isUninstall);
        }

        private static Application CreateConfiguredApp(ShutdownMode shutdownMode)
        {
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

            var app = new Application { ShutdownMode = shutdownMode };

            EventManager.RegisterClassHandler(
                typeof(FrameworkElement),
                System.Windows.Input.Keyboard.PreviewGotKeyboardFocusEvent,
                new System.Windows.Input.KeyboardFocusChangedEventHandler((s, e) =>
                {
                    if (s is FrameworkElement fe && fe.FocusVisualStyle != null)
                        fe.FocusVisualStyle = null;
                }),
                handledEventsToo: true);

            EventManager.RegisterClassHandler(
                typeof(FrameworkElement),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler((s, e) =>
                {
                    if (s is FrameworkElement fe && fe.FocusVisualStyle != null)
                        fe.FocusVisualStyle = null;
                }),
                handledEventsToo: true);

            app.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            return app;
        }

        [STAThread]
        static void Main()
        {
            string[] args = Environment.GetCommandLineArgs();
            var (isInstall, isUninstall) = ResolveLaunchMode(args, Environment.ProcessPath);
            if (isInstall || isUninstall)
            {
                Application setupApp = CreateConfiguredApp(ShutdownMode.OnMainWindowClose);
                ThemeService.ApplyTheme(setupApp, Theme.Dark);
                Window setupWindow = isUninstall
                    ? new MusicMediaKeys.Installer.UninstallerWindow()
                    : new MusicMediaKeys.Installer.InstallerWindow();
                setupApp.MainWindow = setupWindow;
                setupApp.Run(setupWindow);
                return;
            }

            EventLogService.Log("Startup", "Application starting, version " + AppInfo.Version);
            UpdateService.FinishPreviousUpdate(args);

            AppSettings settingsObj;
            try
            {
                settingsObj = AppSettings.Load();
            }
            catch (Exception ex)
            {
                EventLogService.Log("Startup", "Settings load failed: " + ex.Message);
                settingsObj = new AppSettings();
            }

            var app = CreateConfiguredApp(ShutdownMode.OnExplicitShutdown);

            ThemeService.ApplyTheme(app, settingsObj.Theme);
            Localization.CurrentLanguage = settingsObj.Language;

            Mutex? mutex = TryAcquireSingleInstanceMutex(out bool createdNew);
            if (!createdNew)
            {
                EventLogService.Log("Startup", "Second instance prevented");
                CustomDialogWindow.Show(null, Localization.GetString("AlreadyRunning"), Localization.GetString("AppName"));
                Environment.Exit(0);
                return;
            }

            while (!ElevationManager.IsRunningAsAdministrator())
            {
                if (!AppPaths.IsPortableMode && settingsObj.SkipUacPrompt && ElevationSchedulerService.IsTaskRegistered())
                {
                    EventLogService.Log("Startup", "Attempting silent elevated launch via Task Scheduler");
                    ReleaseSingleInstanceMutex(ref mutex, ref createdNew);
                    if (ElevationSchedulerService.TryRunElevatedTask())
                    {
                        EventLogService.Log("Startup", "Silent elevated process spawned via Task Scheduler. Exiting standard process.");
                        Environment.Exit(0);
                        return;
                    }
                    EventLogService.Log("Startup", "Silent launch via Task Scheduler failed. Falling back to UAC prompt.");
                }

                EventLogService.Log("Startup", "Requesting administrator elevation");
                ReleaseSingleInstanceMutex(ref mutex, ref createdNew);
                if (ElevationManager.TryRestartAsAdministrator())
                {
                    EventLogService.Log("Startup", "Restarted as administrator successfully. Terminating standard process.");
                    Environment.Exit(0);
                    return;
                }

                mutex = TryAcquireSingleInstanceMutex(out createdNew);
                if (!createdNew)
                {
                    EventLogService.Log("Startup", "Another instance took over while elevation was cancelled.");
                    Environment.Exit(0);
                    return;
                }

                EventLogService.Log("Startup", "Elevation prompt cancelled or failed. Showing CustomDialogWindow confirmation.");
                string title = Localization.GetString("AdminRequiredTitle");
                string message = Localization.GetString("AdminRequiredMessage");
                string retryText = Localization.GetString("Retry");
                string exitText = Localization.GetString("Exit");

                bool retry = CustomDialogWindow.ShowConfirmation(null, message, title, retryText, exitText);
                if (!retry)
                {
                    EventLogService.Log("Startup", "User selected exit instead of granting administrator elevation.");
                    ReleaseSingleInstanceMutex(ref mutex, ref createdNew);
                    Environment.Exit(0);
                    return;
                }
            }

            if (!AppPaths.IsPortableMode && settingsObj.SkipUacPrompt)
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        if (!ElevationSchedulerService.IsTaskRegisteredForCurrentExecutable())
                        {
                            ElevationSchedulerService.EnsureElevatedTaskRegistered();
                        }
                    }
                    catch (Exception ex)
                    {
                        EventLogService.Log("Startup", "Failed to register the launch-without-UAC task: " + ex.Message);
                    }
                });
            }

            if (!settingsObj.IsFirstRunCompleted)
            {
                EventLogService.Log("Startup", "Opening WelcomeWizardWindow for first run setup");
                var welcomeWizard = new WelcomeWizardWindow(settingsObj);
                SettingsWindowCoordinator.BringToForeground(welcomeWizard, showIfHidden: false);
                bool? wizardResult = welcomeWizard.ShowDialog();
                if (wizardResult != true)
                {
                    EventLogService.Log("Startup", "Welcome wizard was cancelled or closed");
                    ReleaseSingleInstanceMutex(ref mutex, ref createdNew);
                    Environment.Exit(0);
                    return;
                }
            }

            try
            {
                using var coordinator = new AppCoordinator();
                app.Run();
            }
            catch (Exception ex)
            {
                EventLogService.Log("Startup", "Startup failure: " + ex.Message);
                CustomDialogWindow.Show(
                    null,
                    string.Format(Localization.GetString("CriticalErrorStartupFormat"), ex.Message, ex.StackTrace),
                    Localization.GetString("Error"));
            }
            finally
            {
                ReleaseSingleInstanceMutex(ref mutex, ref createdNew);
                Environment.Exit(0);
            }
        }

        private static Mutex? TryAcquireSingleInstanceMutex(out bool acquired)
        {
            var mutex = new Mutex(true, "MusicMediaKeys_SingleInstance", out acquired);
            if (!acquired)
            {
                try
                {
                    if (mutex.WaitOne(1000))
                    {
                        acquired = true;
                    }
                }
                catch (AbandonedMutexException)
                {
                    acquired = true;
                }
            }

            if (!acquired)
            {
                mutex.Dispose();
                return null;
            }

            return mutex;
        }

        private static void ReleaseSingleInstanceMutex(ref Mutex? mutex, ref bool acquired)
        {
            if (mutex == null)
            {
                acquired = false;
                return;
            }

            if (acquired)
            {
                try { mutex.ReleaseMutex(); } catch { }
                acquired = false;
            }

            mutex.Dispose();
            mutex = null;
        }

        private static void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                ErrorLogger.Log("DispatcherUnhandledException", e.Exception);
                EventLogService.Log("Crash", "DispatcherUnhandledException: " + e.Exception.Message);
            }
            catch { }

            CustomDialogWindow.Show(
                null,
                string.Format(Localization.GetString("ErrorOccurredFormat"), e.Exception.Message, e.Exception.StackTrace),
                Localization.GetString("Error"));

            e.Handled = true;
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                try
                {
                    ErrorLogger.Log("CurrentDomainUnhandledException", ex);
                    EventLogService.Log("Crash", "CurrentDomainUnhandledException: " + ex.Message);
                }
                catch { }

                string message = string.Format(Localization.GetString("ErrorOccurredFormat"), ex.Message, ex.StackTrace);
                string title = Localization.GetString("CriticalError");

                try
                {
                    var dispatcher = Application.Current?.Dispatcher;
                    if (dispatcher != null && !dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
                    {
                        if (dispatcher.CheckAccess())
                        {
                            CustomDialogWindow.Show(null, message, title);
                        }
                        else
                        {
                            dispatcher.Invoke(() => CustomDialogWindow.Show(null, message, title));
                        }
                    }
                }
                catch
                {
                }
            }
        }
    }
}
