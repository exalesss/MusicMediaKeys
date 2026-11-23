using System;

namespace MusicMediaKeys
{
    public static class AutoStartManager
    {
        public const string TaskName = "MusicMediaKeys";

        public static bool IsEnabled()
        {
            return WindowsTaskSchedulerService.IsTaskRegistered(TaskName);
        }

        public static bool IsEnabledForCurrentExecutable()
        {
            string? appPath = AppInfo.ExecutablePath;
            if (string.IsNullOrWhiteSpace(appPath))
                return false;

            return WindowsTaskSchedulerService.IsTaskRegistered(TaskName, appPath);
        }

        public static void Enable()
        {
            if (!ElevationManager.IsRunningAsAdministrator())
                throw new InvalidOperationException(Localization.GetString("AutoStartEnableNeedAdmin"));

            string? appPath = AppInfo.ExecutablePath;
            if (string.IsNullOrEmpty(appPath))
                throw new InvalidOperationException(Localization.GetString("AutoStartPathUnknown"));

            if (WindowsTaskSchedulerService.IsRunningFromUntrustedPath(appPath))
                throw new InvalidOperationException(Localization.GetString("AutoStartUntrustedPath"));

            if (!WindowsTaskSchedulerService.IsValidTaskExecutable(appPath))
                throw new InvalidOperationException(Localization.GetString("AutoStartPathInvalid"));

            bool success = WindowsTaskSchedulerService.RegisterLogonTask(TaskName, appPath, runElevated: true, "MusicMediaKeys Autostart Task");
            if (!success)
            {
                throw new InvalidOperationException(Localization.GetString("AutoStartSchedulerFailed"));
            }
        }

        public static bool Disable()
        {
            try
            {
                if (!IsEnabled())
                    return true;
                return WindowsTaskSchedulerService.DeleteTask(TaskName);
            }
            catch (Exception ex)
            {
                ErrorLogger.Log("AutoStartManager.Disable", ex);
                return false;
            }
        }
    }
}
