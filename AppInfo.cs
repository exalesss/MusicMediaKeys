using System;
using System.Reflection;

namespace MusicMediaKeys
{
    internal static class AppInfo
    {
        public static string Version { get; } = ReadVersion();

        public static string? ExecutablePath => Environment.ProcessPath;

        private static string ReadVersion()
        {
            Assembly assembly = typeof(AppInfo).Assembly;
            string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrEmpty(informational))
            {
                int metadataStart = informational.IndexOf('+');
                return metadataStart >= 0 ? informational[..metadataStart] : informational;
            }

            return assembly.GetName().Version?.ToString(3) ?? "";
        }
    }
}
