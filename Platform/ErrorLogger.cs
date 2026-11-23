using System;
using System.IO;

namespace MusicMediaKeys
{
    public static class ErrorLogger
    {
        private static readonly object _writeLock = new();
        private static DateTime _lastCleanupDate = DateTime.MinValue;

        public static string Log(string context, Exception ex)
        {
            lock (_writeLock)
            {
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}{Environment.NewLine}";
                string path;
                try
                {
                    DateTime today = DateTime.Today;
                    if (_lastCleanupDate != today)
                    {
                        _lastCleanupDate = today;
                        CleanOldLogs(keepDays: 7);
                    }

                    path = AppPaths.GetErrorLogFilePath();
                    File.AppendAllText(path, line);
                }
                catch
                {
                    try
                    {
                        path = AppPaths.LogFilePath;
                        File.AppendAllText(path, line);
                    }
                    catch
                    {
                        path = string.Empty;
                    }
                }
                return path;
            }
        }

        private static void CleanOldLogs(int keepDays = 7)
        {
            try
            {
                var dir = new DirectoryInfo(AppPaths.ErrorLogsDirectory);
                if (!dir.Exists) return;

                var threshold = DateTime.Now.AddDays(-keepDays);
                foreach (var file in dir.GetFiles("error_*.log"))
                {
                    if (file.LastWriteTime < threshold)
                    {
                        try { file.Delete(); } catch { }
                    }
                }
            }
            catch
            {
            }
        }
    }
}
