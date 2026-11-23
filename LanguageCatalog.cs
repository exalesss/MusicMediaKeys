using System;
using System.Collections.Generic;
using System.Globalization;

namespace MusicMediaKeys
{
    public sealed record LanguageInfo(
        Language Language,
        string Code,
        string Badge,
        string NativeName,
        string EnglishName);

    public static class LanguageCatalog
    {
        public static IReadOnlyList<LanguageInfo> All { get; } = new LanguageInfo[]
        {
            new(Language.English, "en", "EN", "English", "English"),
            new(Language.Russian, "ru", "RU", "Русский", "Russian"),
            new(Language.German, "de", "DE", "Deutsch", "German"),
            new(Language.French, "fr", "FR", "Français", "French"),
            new(Language.Spanish, "es", "ES", "Español", "Spanish (Spain)"),
            new(Language.SpanishLatAm, "es-419", "ES-LA", "Español (Latinoamérica)", "Spanish (Latin America)"),
            new(Language.Portuguese, "pt", "PT", "Português", "Portuguese (Portugal)"),
            new(Language.PortugueseBrazil, "pt-BR", "PT-BR", "Português (Brasil)", "Portuguese (Brazil)"),
            new(Language.Italian, "it", "IT", "Italiano", "Italian"),
            new(Language.Polish, "pl", "PL", "Polski", "Polish"),
            new(Language.Ukrainian, "uk", "UK", "Українська", "Ukrainian"),
            new(Language.Turkish, "tr", "TR", "Türkçe", "Turkish"),
            new(Language.Czech, "cs", "CS", "Čeština", "Czech"),
            new(Language.Dutch, "nl", "NL", "Nederlands", "Dutch"),
            new(Language.Swedish, "sv", "SV", "Svenska", "Swedish"),
            new(Language.Danish, "da", "DA", "Dansk", "Danish"),
            new(Language.Finnish, "fi", "FI", "Suomi", "Finnish"),
            new(Language.Norwegian, "nb", "NO", "Norsk (Bokmål)", "Norwegian"),
            new(Language.Hungarian, "hu", "HU", "Magyar", "Hungarian"),
            new(Language.Romanian, "ro", "RO", "Română", "Romanian"),
            new(Language.Bulgarian, "bg", "BG", "Български", "Bulgarian"),
            new(Language.Greek, "el", "EL", "Ελληνικά", "Greek"),
            new(Language.ChineseSimplified, "zh-CN", "ZH-CN", "简体中文", "Chinese (Simplified)"),
            new(Language.ChineseTraditional, "zh-TW", "ZH-TW", "繁體中文", "Chinese (Traditional)"),
            new(Language.Japanese, "ja", "JA", "日本語", "Japanese"),
            new(Language.Korean, "ko", "KO", "한국어", "Korean"),
            new(Language.Thai, "th", "TH", "ไทย", "Thai"),
            new(Language.Vietnamese, "vi", "VI", "Tiếng Việt", "Vietnamese"),
            new(Language.Indonesian, "id", "ID", "Bahasa Indonesia", "Indonesian")
        };

        private static readonly Dictionary<Language, LanguageInfo> ByLanguage = BuildByLanguage();

        private static Dictionary<Language, LanguageInfo> BuildByLanguage()
        {
            var dict = new Dictionary<Language, LanguageInfo>(All.Count);
            foreach (var info in All)
                dict[info.Language] = info;
            return dict;
        }

        public static LanguageInfo GetInfo(Language language)
        {
            return ByLanguage.TryGetValue(language, out var info) ? info : All[0];
        }

        public static Language DetectSystemLanguage(CultureInfo? culture = null)
        {
            CultureInfo ci = culture ?? CultureInfo.CurrentUICulture;
            string name = ci.Name ?? "";
            string twoLetter = ci.TwoLetterISOLanguageName ?? "";

            if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                if (name.Contains("Hant", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("-TW", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("-HK", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("-MO", StringComparison.OrdinalIgnoreCase))
                {
                    return Language.ChineseTraditional;
                }
                return Language.ChineseSimplified;
            }

            if (name.Equals("pt-BR", StringComparison.OrdinalIgnoreCase))
                return Language.PortugueseBrazil;

            if (twoLetter.Equals("es", StringComparison.OrdinalIgnoreCase))
            {
                if (name.Equals("es-419", StringComparison.OrdinalIgnoreCase) ||
                    (!name.Equals("es-ES", StringComparison.OrdinalIgnoreCase) && name.Contains('-')))
                {
                    return Language.SpanishLatAm;
                }
                return Language.Spanish;
            }

            return twoLetter.ToLowerInvariant() switch
            {
                "ru" => Language.Russian,
                "de" => Language.German,
                "fr" => Language.French,
                "pt" => Language.Portuguese,
                "it" => Language.Italian,
                "pl" => Language.Polish,
                "uk" => Language.Ukrainian,
                "tr" => Language.Turkish,
                "cs" => Language.Czech,
                "nl" => Language.Dutch,
                "sv" => Language.Swedish,
                "da" => Language.Danish,
                "fi" => Language.Finnish,
                "nb" or "nn" or "no" => Language.Norwegian,
                "hu" => Language.Hungarian,
                "ro" => Language.Romanian,
                "bg" => Language.Bulgarian,
                "el" => Language.Greek,
                "ja" => Language.Japanese,
                "ko" => Language.Korean,
                "th" => Language.Thai,
                "vi" => Language.Vietnamese,
                "id" => Language.Indonesian,
                _ => Language.English
            };
        }
    }
}
