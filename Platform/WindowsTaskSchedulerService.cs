using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace MusicMediaKeys
{
    public static class WindowsTaskSchedulerService
    {
        private const string RootFolder = @"\";

        public static bool IsValidTaskExecutable([NotNullWhen(true)] string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length > 260)
                return false;
            if (path.IndexOfAny(new[] { '"', '&', '|', '<', '>', '%', '^' }) >= 0)
                return false;
            return File.Exists(path);
        }

        public static bool IsRunningFromUntrustedPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return true;

            try
            {
                string fullPath = Path.GetFullPath(path);

                string tempPath = Path.GetFullPath(Path.GetTempPath());
                if (fullPath.StartsWith(tempPath, StringComparison.OrdinalIgnoreCase))
                    return true;

                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(userProfile))
                {
                    string downloadsPath = Path.Combine(userProfile, "Downloads") + Path.DirectorySeparatorChar;
                    if (fullPath.StartsWith(downloadsPath, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                return false;
            }
            catch
            {
                return true;
            }
        }

        public static bool IsTaskRegistered(string taskName)
        {
            return IsTaskRegistered(taskName, expectedExecutablePath: null);
        }

        public static bool IsTaskRegistered(string taskName, string? expectedExecutablePath)
        {
            if (string.IsNullOrWhiteSpace(taskName))
                return false;

            object? service = null;
            object? folder = null;
            object? task = null;
            object? definition = null;
            object? actions = null;
            object? action = null;

            try
            {
                Type? serviceType = Type.GetTypeFromProgID("Schedule.Service");
                if (serviceType == null)
                    return false;

                service = Activator.CreateInstance(serviceType);
                if (service == null)
                    return false;

                dynamic dynService = service;
                dynService.Connect();

                folder = dynService.GetFolder(RootFolder);
                if (folder == null)
                    return false;

                dynamic dynFolder = folder;
                try
                {
                    task = dynFolder.GetTask(taskName);
                    if (task == null)
                        return false;

                    if (string.IsNullOrWhiteSpace(expectedExecutablePath))
                        return true;

                    definition = ((dynamic)task).Definition;
                    if (definition == null)
                        return false;

                    actions = ((dynamic)definition).Actions;
                    if (actions == null || (int)((dynamic)actions).Count < 1)
                        return false;

                    action = ((dynamic)actions)[1];
                    string? registeredPath = (string?)((dynamic)action).Path;
                    if (string.IsNullOrWhiteSpace(registeredPath))
                        return false;

                    string normalizedRegistered = Path.GetFullPath(registeredPath.Trim().Trim('"'));
                    string normalizedExpected = Path.GetFullPath(expectedExecutablePath.Trim().Trim('"'));
                    return string.Equals(normalizedRegistered, normalizedExpected, StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception ex) when (ex is FileNotFoundException || ex.HResult == unchecked((int)0x80070002))
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                EventLogService.Log("TaskScheduler", $"Error checking task '{taskName}': {ex.Message}");
                return false;
            }
            finally
            {
                ReleaseCom(action);
                ReleaseCom(actions);
                ReleaseCom(definition);
                ReleaseCom(task);
                ReleaseCom(folder);
                ReleaseCom(service);
            }
        }

        public static bool RegisterLogonTask(string taskName, string executablePath, bool runElevated, string? description = null)
        {
            return RegisterTaskInternal(
                taskName,
                executablePath,
                addLogonTrigger: true,
                runElevated: runElevated,
                description: description ?? "MusicMediaKeys Autostart Task",
                logContext: "RegisterLogonTask",
                kindLabel: "Logon task");
        }

        public static bool RegisterOnDemandElevatedTask(string taskName, string executablePath, string? description = null)
        {
            return RegisterTaskInternal(
                taskName,
                executablePath,
                addLogonTrigger: false,
                runElevated: true,
                description: description ?? "MusicMediaKeys Elevated Launch Task",
                logContext: "RegisterOnDemandElevatedTask",
                kindLabel: "On-demand elevated task");
        }

        private static bool RegisterTaskInternal(
            string taskName,
            string executablePath,
            bool addLogonTrigger,
            bool runElevated,
            string description,
            string logContext,
            string kindLabel)
        {
            if (string.IsNullOrWhiteSpace(taskName) || !IsValidTaskExecutable(executablePath))
                return false;

            if (IsRunningFromUntrustedPath(executablePath))
            {
                EventLogService.Log("TaskScheduler", $"Refused to register {kindLabel.ToLowerInvariant()} for untrusted path: {executablePath}");
                return false;
            }

            object? service = null;
            object? folder = null;
            object? taskDef = null;
            object? regInfo = null;
            object? principal = null;
            object? settings = null;
            object? triggers = null;
            object? trigger = null;
            object? actions = null;
            object? action = null;
            object? registeredTask = null;

            try
            {
                Type? serviceType = Type.GetTypeFromProgID("Schedule.Service");
                if (serviceType == null)
                    return false;

                service = Activator.CreateInstance(serviceType);
                if (service == null)
                    return false;

                dynamic dynService = service;
                dynService.Connect();

                folder = dynService.GetFolder(RootFolder);
                if (folder == null)
                    return false;

                taskDef = dynService.NewTask(0);
                dynamic dynTaskDef = taskDef;

                regInfo = dynTaskDef.RegistrationInfo;
                ((dynamic)regInfo).Description = description;

                string currentUser;
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    currentUser = identity.Name;
                }

                principal = dynTaskDef.Principal;
                dynamic dynPrincipal = principal;
                dynPrincipal.UserId = currentUser;
                dynPrincipal.LogonType = 3;
                dynPrincipal.RunLevel = runElevated ? 1 : 0;

                settings = dynTaskDef.Settings;
                dynamic dynSettings = settings;
                dynSettings.DisallowStartIfOnBatteries = false;
                dynSettings.StopIfGoingOnBatteries = false;
                dynSettings.ExecutionTimeLimit = "PT0S";

                if (addLogonTrigger)
                {
                    triggers = dynTaskDef.Triggers;
                    trigger = ((dynamic)triggers).Create(9);
                    ((dynamic)trigger).UserId = currentUser;
                }

                actions = dynTaskDef.Actions;
                action = ((dynamic)actions).Create(0);
                dynamic dynAction = action;
                dynAction.Path = executablePath;
                string? workDir = Path.GetDirectoryName(executablePath);
                if (!string.IsNullOrEmpty(workDir))
                {
                    dynAction.WorkingDirectory = workDir;
                }

                dynamic dynFolder = folder;
                registeredTask = dynFolder.RegisterTaskDefinition(taskName, dynTaskDef, 6, currentUser, null, 3, null);

                EventLogService.Log("TaskScheduler", $"{kindLabel} '{taskName}' registered successfully via COM API");
                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.Log("TaskScheduler." + logContext, ex);
                return false;
            }
            finally
            {
                ReleaseCom(registeredTask);
                ReleaseCom(action);
                ReleaseCom(actions);
                ReleaseCom(trigger);
                ReleaseCom(triggers);
                ReleaseCom(settings);
                ReleaseCom(principal);
                ReleaseCom(regInfo);
                ReleaseCom(taskDef);
                ReleaseCom(folder);
                ReleaseCom(service);
            }
        }

        public static bool RunTask(string taskName)
        {
            if (string.IsNullOrWhiteSpace(taskName))
                return false;

            object? service = null;
            object? folder = null;
            object? task = null;
            object? runningTask = null;

            try
            {
                Type? serviceType = Type.GetTypeFromProgID("Schedule.Service");
                if (serviceType == null)
                    return false;

                service = Activator.CreateInstance(serviceType);
                if (service == null)
                    return false;

                dynamic dynService = service;
                dynService.Connect();

                folder = dynService.GetFolder(RootFolder);
                if (folder == null)
                    return false;

                dynamic dynFolder = folder;
                task = dynFolder.GetTask(taskName);
                if (task == null)
                    return false;

                dynamic dynTask = task;
                runningTask = dynTask.Run(null);
                EventLogService.Log("TaskScheduler", $"Task '{taskName}' triggered via COM API");
                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.Log("TaskScheduler.RunTask", ex);
                return false;
            }
            finally
            {
                ReleaseCom(runningTask);
                ReleaseCom(task);
                ReleaseCom(folder);
                ReleaseCom(service);
            }
        }

        public static bool DeleteTask(string taskName)
        {
            if (string.IsNullOrWhiteSpace(taskName))
                return true;

            object? service = null;
            object? folder = null;

            try
            {
                Type? serviceType = Type.GetTypeFromProgID("Schedule.Service");
                if (serviceType == null)
                    return false;

                service = Activator.CreateInstance(serviceType);
                if (service == null)
                    return false;

                dynamic dynService = service;
                dynService.Connect();

                folder = dynService.GetFolder(RootFolder);
                if (folder == null)
                    return false;

                dynamic dynFolder = folder;
                try
                {
                    dynFolder.DeleteTask(taskName, 0);
                    EventLogService.Log("TaskScheduler", $"Task '{taskName}' deleted via COM API");
                    return true;
                }
                catch (Exception ex) when (ex is FileNotFoundException || ex.HResult == unchecked((int)0x80070002))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.Log("TaskScheduler.DeleteTask", ex);
                return false;
            }
            finally
            {
                ReleaseCom(folder);
                ReleaseCom(service);
            }
        }

        private static void ReleaseCom(object? comObj)
        {
            if (comObj != null && Marshal.IsComObject(comObj))
            {
                try
                {
                    Marshal.ReleaseComObject(comObj);
                }
                catch { }
            }
        }
    }
}
