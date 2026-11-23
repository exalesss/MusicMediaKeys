using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace MusicMediaKeys.Installer
{
    internal static class InstallerEngine
    {
        private const string UninstallRegSubKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\MusicMediaKeys";
        private const string AutostartTaskName = "MusicMediaKeys";
        private const string ElevatedTaskName = "MusicMediaKeys_Elevated";

        public static bool IsSafeInstallDirectory(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string trimmed = path.Trim();
            if (trimmed.StartsWith(@"\\", StringComparison.Ordinal))
                return false;

            try
            {
                if (!Path.IsPathFullyQualified(trimmed))
                    return false;

                string full = Path.GetFullPath(trimmed).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.IsNullOrEmpty(full) || full.StartsWith(@"\\", StringComparison.Ordinal))
                    return false;

                string? root = Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.IsNullOrEmpty(root) || string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
                    return false;

                string[] forbiddenRoots =
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86),
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
                    Path.GetTempPath()
                };

                foreach (string forbidden in forbiddenRoots)
                {
                    if (string.IsNullOrWhiteSpace(forbidden))
                        continue;

                    string normalizedForbidden = Path.GetFullPath(forbidden).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (string.Equals(full, normalizedForbidden, StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                if (!string.IsNullOrWhiteSpace(winDir))
                {
                    string normalizedWin = Path.GetFullPath(winDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    if (full.StartsWith(normalizedWin, StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public static async Task InstallAsync(InstallOptions options, IProgress<(int Percent, string Status)> progress)
        {
            await Task.Run(() =>
            {
                int lang = options.Language;
                if (!IsSafeInstallDirectory(options.InstallDirectory))
                    throw new InvalidOperationException(InstallerLocalization.Get("InvalidInstallDirectory", lang));

                progress.Report((10, InstallerLocalization.Get("StatusStopping", lang)));
                StopRunningApp();

                string installDir = Path.GetFullPath(options.InstallDirectory);
                Directory.CreateDirectory(installDir);

                progress.Report((35, InstallerLocalization.Get("StatusExtracting", lang)));
                string exePath = DeployApplicationBinary(installDir);

                progress.Report((60, InstallerLocalization.Get("StatusConfig", lang)));
                string settingsJson = InstallerModel.BuildSettingsJson(options);
                if (options.IsPortable)
                {
                    File.WriteAllText(Path.Combine(installDir, "portable.dat"), "portable=1\r\n");
                    string dataDir = Path.Combine(installDir, "Data");
                    Directory.CreateDirectory(dataDir);
                    WriteSettingsFile(Path.Combine(dataDir, "settings.json"), settingsJson);
                }
                else
                {
                    TryDeleteFile(Path.Combine(installDir, "portable.dat"));

                    string appDataDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "MusicMediaKeys");
                    Directory.CreateDirectory(appDataDir);
                    WriteSettingsFile(Path.Combine(appDataDir, "settings.json"), settingsJson);

                    progress.Report((80, InstallerLocalization.Get("StatusShortcuts", lang)));
                    if (options.CreateDesktopShortcut)
                    {
                        string desktopLnk = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                            "Music Media Keys.lnk");
                        ShortcutHelper.CreateShortcut(desktopLnk, exePath, installDir, "Music Media Keys", exePath);
                    }

                    if (options.CreateStartMenuShortcut)
                    {
                        string startDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                            "Music Media Keys");
                        Directory.CreateDirectory(startDir);
                        string startLnk = Path.Combine(startDir, "Music Media Keys.lnk");
                        ShortcutHelper.CreateShortcut(startLnk, exePath, installDir, "Music Media Keys", exePath);
                    }

                    TryDeleteFile(Path.Combine(installDir, "uninstall.exe"));
                    RegisterUninstallEntry(installDir, exePath);

                    if (IsAdministrator())
                    {
                        progress.Report((92, InstallerLocalization.Get("StatusTasks", lang)));
                        if (options.AutoStart)
                            WindowsTaskSchedulerService.RegisterLogonTask(AutostartTaskName, exePath, runElevated: true);
                        else
                            WindowsTaskSchedulerService.DeleteTask(AutostartTaskName);

                        if (options.SkipUacPrompt)
                            WindowsTaskSchedulerService.RegisterOnDemandElevatedTask(ElevatedTaskName, exePath);
                        else
                            WindowsTaskSchedulerService.DeleteTask(ElevatedTaskName);
                    }
                }

                progress.Report((100, InstallerLocalization.Get("StatusDone", lang)));
            });
        }

        public static async Task UninstallAsync(string installDir, bool removeUserData, int language, IProgress<(int Percent, string Status)> progress)
        {
            await Task.Run(() =>
            {
                if (!IsSafeInstallDirectory(installDir))
                    throw new InvalidOperationException(InstallerLocalization.Get("InvalidInstallDirectory", language));

                string safeInstallDir = Path.GetFullPath(installDir);

                progress.Report((15, InstallerLocalization.Get("UninstallStatusStopping", language)));
                StopRunningApp();

                progress.Report((40, InstallerLocalization.Get("UninstallStatusTasks", language)));
                WindowsTaskSchedulerService.DeleteTask(AutostartTaskName);
                WindowsTaskSchedulerService.DeleteTask(ElevatedTaskName);

                progress.Report((70, InstallerLocalization.Get("UninstallStatusFiles", language)));
                TryDeleteFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Music Media Keys.lnk"));
                TryDeleteDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Music Media Keys"));

                TryDeleteRegistryKey(Registry.CurrentUser, UninstallRegSubKey);
                TryDeleteRegistryKey(Registry.LocalMachine, UninstallRegSubKey);

                if (removeUserData)
                {
                    TryDeleteDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MusicMediaKeys"));
                    TryDeleteDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MusicMediaKeys"));
                    TryDeleteDirectory(Path.Combine(safeInstallDir, "Data"));
                }

                string exePath = Path.Combine(safeInstallDir, "MusicMediaKeys.exe");
                string? selfExe = Environment.ProcessPath;
                bool isSelfExe = !string.IsNullOrEmpty(selfExe) &&
                    string.Equals(Path.GetFullPath(selfExe), Path.GetFullPath(exePath), StringComparison.OrdinalIgnoreCase);
                if (!isSelfExe)
                {
                    TryDeleteFile(exePath);
                }

                TryDeleteFile(Path.Combine(safeInstallDir, "uninstall.exe"));
                TryDeleteFile(Path.Combine(safeInstallDir, "MusicMediaKeys.exe.old"));
                TryDeleteFile(Path.Combine(safeInstallDir, "MusicMediaKeys.exe.new"));
                TryDeleteFile(Path.Combine(safeInstallDir, "icon.ico"));
                TryDeleteFile(Path.Combine(safeInstallDir, "icon error.ico"));
                TryDeleteFile(Path.Combine(safeInstallDir, "portable.dat"));

                progress.Report((100, InstallerLocalization.Get("UninstallComplete", language)));
            });
        }

        public static void ScheduleSelfDeleteOnExit(string installDir, string? selfExePath)
        {
            if (!IsSafeInstallDirectory(installDir))
                return;

            try
            {
                string fullInstallDir = Path.GetFullPath(installDir);
                bool hasSelf = !string.IsNullOrWhiteSpace(selfExePath) && File.Exists(selfExePath);

                string deleteSelfCmd = hasSelf ? "del /f /q \"%MMK_SELF%\" >nul 2>&1 & " : "";
                string args = $"/c for /L %i in (1,1,10) do (ping -n 2 127.0.0.1 >nul & {deleteSelfCmd}rmdir \"%MMK_DIR%\" >nul 2>&1 & if not exist \"%MMK_DIR%\" exit /b 0)";

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = args,
                    WorkingDirectory = Path.GetTempPath(),
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.Environment["MMK_DIR"] = fullInstallDir;
                if (hasSelf)
                    psi.Environment["MMK_SELF"] = Path.GetFullPath(selfExePath!);

                Process.Start(psi);
            }
            catch { }
        }

        public static (int Theme, int Language)? TryReadInstalledThemeAndLanguage(string installDir)
        {
            string[] candidates =
            {
                Path.Combine(installDir, "Data", "settings.json"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MusicMediaKeys", "settings.json")
            };

            foreach (string path in candidates)
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
                    int theme = doc.RootElement.TryGetProperty("Theme", out var tProp) && tProp.TryGetInt32(out int t) ? t : 1;
                    int lang = doc.RootElement.TryGetProperty("Language", out var lProp) && lProp.TryGetInt32(out int l) ? l : 1;
                    return (theme, lang);
                }
                catch { }
            }

            return null;
        }

        public static string ResolveInstalledDirectory()
        {
            RegistryKey[] roots = IsAdministrator()
                ? new[] { Registry.LocalMachine, Registry.CurrentUser }
                : new[] { Registry.CurrentUser, Registry.LocalMachine };

            foreach (RegistryKey root in roots)
            {
                try
                {
                    using RegistryKey? key = root.OpenSubKey(UninstallRegSubKey);
                    string? loc = key?.GetValue("InstallLocation") as string;
                    if (IsSafeInstallDirectory(loc) && Directory.Exists(loc))
                        return Path.GetFullPath(loc);
                }
                catch { }
            }

            string? currentDir = Path.GetDirectoryName(Environment.ProcessPath);
            if (IsSafeInstallDirectory(currentDir))
                return Path.GetFullPath(currentDir!);

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "MusicMediaKeys");
        }

        private static string DeployApplicationBinary(string installDir)
        {
            string targetExe = Path.Combine(installDir, "MusicMediaKeys.exe");
            string? selfExe = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(selfExe) && File.Exists(selfExe))
            {
                string fullSelf = Path.GetFullPath(selfExe);
                string fullTarget = Path.GetFullPath(targetExe);
                if (!string.Equals(fullSelf, fullTarget, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(fullSelf, fullTarget, overwrite: true);
                }
                return fullTarget;
            }

            throw new FileNotFoundException("Current executable path could not be resolved for installation.");
        }

        private static void WriteSettingsFile(string path, string json)
        {
            if (File.Exists(path))
            {
                try { File.Copy(path, path + ".bak", overwrite: true); } catch { }
            }
            File.WriteAllText(path, json);
        }

        private static void RegisterUninstallEntry(string installDir, string exePath)
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            bool useMachine = IsAdministrator() &&
                              installDir.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase);

            RegistryKey root = useMachine ? Registry.LocalMachine : Registry.CurrentUser;
            using RegistryKey key = root.CreateSubKey(UninstallRegSubKey, writable: true);
            long sizeKb = File.Exists(exePath) ? Math.Max(1024, new FileInfo(exePath).Length / 1024) : 15000;

            key.SetValue("DisplayName", "Music Media Keys");
            key.SetValue("DisplayVersion", AppInfo.Version);
            key.SetValue("Publisher", "exalesss");
            key.SetValue("DisplayIcon", $"\"{exePath}\",0");
            key.SetValue("UninstallString", $"\"{exePath}\" --uninstall");
            key.SetValue("InstallLocation", installDir);
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            key.SetValue("EstimatedSize", (int)Math.Min(int.MaxValue, sizeKb), RegistryValueKind.DWord);
        }

        private static void StopRunningApp()
        {
            int currentPid = Environment.ProcessId;
            foreach (Process p in Process.GetProcessesByName("MusicMediaKeys"))
            {
                try
                {
                    if (p.Id == currentPid)
                        continue;

                    if (!p.HasExited)
                    {
                        p.Kill(entireProcessTree: true);
                        p.WaitForExit(3000);
                    }
                }
                catch { }
                finally { p.Dispose(); }
            }
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static void TryDeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { }
        }

        private static void TryDeleteRegistryKey(RegistryKey root, string subKey)
        {
            try { root.DeleteSubKeyTree(subKey, throwOnMissingSubKey: false); } catch { }
        }
    }
}
