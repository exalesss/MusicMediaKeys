using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace MusicMediaKeys
{
    public enum Language
    {
        English = 0,
        Russian = 1,
        German = 2,
        French = 3,
        Spanish = 4,
        SpanishLatAm = 5,
        Portuguese = 6,
        PortugueseBrazil = 7,
        Italian = 8,
        Polish = 9,
        Ukrainian = 10,
        Turkish = 11,
        Czech = 12,
        Dutch = 13,
        Swedish = 14,
        Danish = 15,
        Finnish = 16,
        Norwegian = 17,
        Hungarian = 18,
        Romanian = 19,
        Bulgarian = 20,
        Greek = 21,
        ChineseSimplified = 22,
        ChineseTraditional = 23,
        Japanese = 24,
        Korean = 25,
        Thai = 26,
        Vietnamese = 27,
        Indonesian = 28
    }

    public static class Localization
    {
        private static Language _currentLanguage = Language.English;
        private static readonly ConcurrentDictionary<Language, IReadOnlyDictionary<string, string>> Cache = new();

        public static Language CurrentLanguage
        {
            get => _currentLanguage;
            set => SetLanguage(value);
        }

        public static event EventHandler? LanguageChanged;

        public static string GetString(string key)
        {
            return GetString(key, _currentLanguage);
        }

        public static string GetString(string key, Language lang)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            IReadOnlyDictionary<string, string> target = GetStrings(lang);
            if (target.TryGetValue(key, out string? value) && !string.IsNullOrEmpty(value))
                return value;

            if (lang != Language.English)
            {
                IReadOnlyDictionary<string, string> fallback = GetStrings(Language.English);
                if (fallback.TryGetValue(key, out string? englishValue) && !string.IsNullOrEmpty(englishValue))
                    return englishValue;
            }

            return key;
        }

        public static void SetLanguage(Language language)
        {
            Language resolved = Enum.IsDefined(typeof(Language), language) ? language : Language.English;
            if (_currentLanguage == resolved)
                return;

            _currentLanguage = resolved;
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }

        internal static IReadOnlyDictionary<string, string> GetStrings(Language lang)
        {
            Language resolved = Enum.IsDefined(typeof(Language), lang) ? lang : Language.English;
            return Cache.GetOrAdd(resolved, LoadLanguageStrings);
        }

        private static IReadOnlyDictionary<string, string> LoadLanguageStrings(Language lang)
        {
            string code = LanguageCatalog.GetInfo(lang).Code;
            string resourceName = $"MusicMediaKeys.Locales.{code}.json";
            Assembly asm = typeof(Localization).Assembly;

            using Stream? stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null)
                return new Dictionary<string, string>(StringComparer.Ordinal);

            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
            return parsed != null
                ? new Dictionary<string, string>(parsed, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
