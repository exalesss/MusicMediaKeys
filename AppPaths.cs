using System;
using System.IO;
using System.Threading;

namespace MusicMediaKeys
{
    internal static class AppPaths
    {
        private static string RoamingDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MusicMediaKeys");

        private static string LocalDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MusicMediaKeys");

        private static bool _isPortableMode;

        static AppPaths()
        {
            try
            {
                string portableMarker = Path.Combine(AppContext.BaseDirectory, "portable.dat");
                if (File.Exists(portableMarker))
                {
                    _isPortableMode = true;
                    string dataRoot = Path.Combine(AppContext.BaseDirectory, "Data");
                    RoamingDir = dataRoot;
                    LocalDir = dataRoot;
                }
            }
            catch
            {
                _isPortableMode = false;
            }
        }

        public static bool IsPortableMode => _isPortableMode;

        internal static void UseRootDirectory(string root)
        {
            if (DataDirLazy.IsValueCreated || LogsDirLazy.IsValueCreated)
                throw new InvalidOperationException("The app folders are already in use.");

            _isPortableMode = false;
            RoamingDir = Path.Combine(root, "Roaming");
            LocalDir = Path.Combine(root, "Local");
        }

        private static readonly Lazy<string> DataDirLazy = new(() =>
        {
            Directory.CreateDirectory(RoamingDir);
            return RoamingDir;
        }, LazyThreadSafetyMode.PublicationOnly);

        private static readonly Lazy<string> LogsDirLazy = new(() =>
        {
            string dir = Path.Combine(LocalDir, "Logs");
            Directory.CreateDirectory(dir);
            return dir;
        }, LazyThreadSafetyMode.PublicationOnly);

        private static readonly Lazy<string> ErrorLogsDirLazy = new(() =>
        {
            string dir = Path.Combine(LogsDirLazy.Value, "ErrorLogs");
            Directory.CreateDirectory(dir);
            return dir;
        }, LazyThreadSafetyMode.PublicationOnly);

        public static string DataDirectory => DataDirLazy.Value;

        public static string SettingsPath => Path.Combine(DataDirectory, "settings.json");

        public static string LogsDirectory => LogsDirLazy.Value;

        public static string LogFilePath => Path.Combine(LogsDirectory, "error.log");
        public static string EventLogFilePath => Path.Combine(LogsDirectory, "events.log");

        public static string ErrorLogsDirectory => ErrorLogsDirLazy.Value;

        public static string GetErrorLogFilePath()
        {
            return Path.Combine(ErrorLogsDirectory, $"error_{DateTime.Now:yyyy-MM-dd}.log");
        }
    }
}
