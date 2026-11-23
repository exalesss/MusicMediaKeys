using System;

namespace MusicMediaKeys
{
    public static class MediaCommandDispatchService
    {
        public static (bool Success, string Details) Send(MediaController controller, AppSettings settings, MediaCommand command, IntPtr foregroundAtKeyPress = default)
        {
            try
            {
                controller.ApplySettings(settings);

                if (settings.UseSystemMediaKeysOnly && foregroundAtKeyPress != IntPtr.Zero)
                    controller.SetForegroundAtKeyPress(foregroundAtKeyPress);

                bool success = controller.SendCommand(command);
                string details = string.IsNullOrWhiteSpace(controller.LastCommandRouteDetails)
                    ? (success ? "OK" : "SendCommand returned false")
                    : controller.LastCommandRouteDetails;
                return (success, details);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static string BuildStatusText(MediaCommand command, bool success, string details)
        {
            return $"{DateTime.Now:HH:mm:ss} | {command} | {(success ? "OK" : "FAIL")} | {details}";
        }
    }
}
