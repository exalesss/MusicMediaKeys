using System;

namespace MusicMediaKeys.Installer
{
    internal static class InstallerLocalization
    {
        public static string Get(string key, int language)
        {
            Language lang = Enum.IsDefined(typeof(Language), language)
                ? (Language)language
                : Language.English;
            return Localization.GetString("Inst_" + key, lang);
        }

        public static string Get(string key, Language language)
        {
            return Localization.GetString("Inst_" + key, language);
        }
    }
}
