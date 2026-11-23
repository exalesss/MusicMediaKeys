using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace MusicMediaKeys.Tests
{
    public class LocalizationTests
    {
        private static readonly Regex PlaceholderRegex = new(@"\{(\d+)\}", RegexOptions.Compiled);

        [Fact]
        public void GetString_ReturnsRussianTranslation()
        {
            string value = Localization.GetString("Error", Language.Russian);
            Assert.Equal("Ошибка", value);
        }

        [Fact]
        public void AllEnumLanguages_ExistInCatalog_AndHaveCompleteValidJsonWithMatchingPlaceholders()
        {
            Language[] enumValues = Enum.GetValues<Language>();
            Assert.Equal(enumValues.Length, LanguageCatalog.All.Count);

            IReadOnlyDictionary<string, string> enStrings = Localization.GetStrings(Language.English);
            Assert.True(enStrings.Count >= 280, "Expected full en.json resource dictionary.");

            foreach (Language lang in enumValues)
            {
                LanguageInfo info = LanguageCatalog.GetInfo(lang);
                Assert.Equal(lang, info.Language);
                Assert.False(string.IsNullOrWhiteSpace(info.Badge));
                Assert.False(string.IsNullOrWhiteSpace(info.NativeName));

                IReadOnlyDictionary<string, string> dict = Localization.GetStrings(lang);
                Assert.Equal(enStrings.Count, dict.Count);

                foreach (var kvp in enStrings)
                {
                    Assert.True(dict.TryGetValue(kvp.Key, out string? translated), $"Missing key '{kvp.Key}' in {info.Code}.json");
                    Assert.False(string.IsNullOrWhiteSpace(translated), $"Empty translation for '{kvp.Key}' in {info.Code}.json");

                    string[] expectedPlaceholders = PlaceholderRegex.Matches(kvp.Value).Select(m => m.Value).OrderBy(x => x).ToArray();
                    string[] actualPlaceholders = PlaceholderRegex.Matches(translated!).Select(m => m.Value).OrderBy(x => x).ToArray();
                    Assert.Equal(expectedPlaceholders, actualPlaceholders);
                }
            }
        }

        [Theory]
        [InlineData("ru-RU", Language.Russian)]
        [InlineData("de-DE", Language.German)]
        [InlineData("es-ES", Language.Spanish)]
        [InlineData("es-MX", Language.SpanishLatAm)]
        [InlineData("pt-PT", Language.Portuguese)]
        [InlineData("pt-BR", Language.PortugueseBrazil)]
        [InlineData("zh-CN", Language.ChineseSimplified)]
        [InlineData("zh-TW", Language.ChineseTraditional)]
        [InlineData("ja-JP", Language.Japanese)]
        [InlineData("nb-NO", Language.Norwegian)]
        [InlineData("en-US", Language.English)]
        public void DetectSystemLanguage_MapsCulturesCorrectly(string cultureName, Language expected)
        {
            Language actual = LanguageCatalog.DetectSystemLanguage(CultureInfo.GetCultureInfo(cultureName));
            Assert.Equal(expected, actual);
        }
    }
}
