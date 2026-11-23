using System;
using System.Diagnostics;
using System.Security.Principal;

namespace MusicMediaKeys
{
    public static class ElevationManager
    {
        public static bool IsRunningAsAdministrator()
        {
            try
            {
                using WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public static bool TryRestartAsAdministrator()
        {
            string? exePath = AppInfo.ExecutablePath;
            if (string.IsNullOrWhiteSpace(exePath))
                return false;

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                };
                using Process? started = Process.Start(startInfo);
                return started != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
