using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;

namespace MusicMediaKeys
{
    public sealed record IconChoice(ImageSource? Icon, string Name, Language? Language = null, string? Badge = null);

    internal static class LanguageThemeChoices
    {
        public static void FillLanguages(ComboBox combo, Language selected, IReadOnlyList<Language>? availableLanguages = null)
        {
            IReadOnlyList<Language> langs;
            if (availableLanguages != null && availableLanguages.Count > 0)
            {
                langs = availableLanguages;
            }
            else
            {
                var all = new List<Language>(LanguageCatalog.All.Count);
                foreach (LanguageInfo info in LanguageCatalog.All)
                    all.Add(info.Language);
                langs = all;
            }

            combo.Items.Clear();
            int selectedIndex = 0;
            for (int i = 0; i < langs.Count; i++)
            {
                Language lang = langs[i];
                LanguageInfo info = LanguageCatalog.GetInfo(lang);
                combo.Items.Add(new IconChoice(
                    null,
                    info.NativeName,
                    lang,
                    info.Badge));

                if (lang == selected)
                    selectedIndex = i;
            }

            if (combo.Items.Count > 0)
                combo.SelectedIndex = selectedIndex;
        }

        public static Language LanguageAt(int index) => index == 1 ? Language.Russian : Language.English;

        public static Language LanguageAt(ComboBox combo, int index)
        {
            if (index >= 0 && index < combo.Items.Count && combo.Items[index] is IconChoice { Language: Language lang })
                return lang;

            return LanguageAt(index);
        }

        public static void FillThemes(ComboBox combo, Theme selected)
        {
            var moonBrush = new SolidColorBrush(selected == Theme.Dark ? Color.FromRgb(180, 185, 195) : Color.FromRgb(100, 100, 100));

            combo.Items.Clear();
            combo.Items.Add(new IconChoice(ThemeService.CreateMoonDrawing(moonBrush), Localization.GetString("ThemeDark")));
            combo.Items.Add(new IconChoice(ThemeService.CreateSunDrawing(), Localization.GetString("ThemeLight")));
            combo.SelectedIndex = selected == Theme.Light ? 1 : 0;
        }

        public static Theme ThemeAt(int index) => index == 1 ? Theme.Light : Theme.Dark;
    }
}
