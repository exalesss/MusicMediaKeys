namespace MusicMediaKeys
{
    public static class ElevationSchedulerService
    {
        public const string TaskName = "MusicMediaKeys_Elevated";


        public static bool IsTaskRegistered()
        {
            return WindowsTaskSchedulerService.IsTaskRegistered(TaskName);
        }

        public static bool IsTaskRegisteredForCurrentExecutable()
        {
            string? appPath = AppInfo.ExecutablePath;
            if (string.IsNullOrWhiteSpace(appPath))
                return false;

            return WindowsTaskSchedulerService.IsTaskRegistered(TaskName, appPath);
        }

        public static bool TryRunElevatedTask()
        {
            return WindowsTaskSchedulerService.RunTask(TaskName);
        }

        public static bool EnsureElevatedTaskRegistered()
        {
            if (!ElevationManager.IsRunningAsAdministrator())
                return false;

            string? appPath = AppInfo.ExecutablePath;
            if (!WindowsTaskSchedulerService.IsValidTaskExecutable(appPath))
                return false;

            if (WindowsTaskSchedulerService.IsRunningFromUntrustedPath(appPath))
            {
                EventLogService.Log("ElevationScheduler", "Refused to register elevated task for untrusted path: " + appPath);
                return false;
            }

            return WindowsTaskSchedulerService.RegisterOnDemandElevatedTask(TaskName, appPath, "MusicMediaKeys Elevated Launch Task");
        }

        public static bool DeleteElevatedTask()
        {
            return WindowsTaskSchedulerService.DeleteTask(TaskName);
        }
    }
}
