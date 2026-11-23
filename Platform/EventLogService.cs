using System;
using System.IO;

namespace MusicMediaKeys
{
    internal static class EventLogService
    {
        private static readonly object Sync = new object();
        private const long MaxLogFileSizeBytes = 1024 * 1024;

        public static void Log(string category, string message)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{category}] {message}";
            lock (Sync)
            {
                try
                {
                    string logPath = AppPaths.EventLogFilePath;
                    if (File.Exists(logPath))
                    {
                        var fi = new FileInfo(logPath);
                        if (fi.Length > MaxLogFileSizeBytes)
                        {
                            string oldLogPath = Path.Combine(AppPaths.LogsDirectory, "events.old.log");
                            File.Move(logPath, oldLogPath, overwrite: true);
                        }
                    }
                    File.AppendAllText(logPath, line + Environment.NewLine);
                }
                catch
                {
                }
            }
        }
    }
}
