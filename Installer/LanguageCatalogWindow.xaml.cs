using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AppLanguage = MusicMediaKeys.Language;

namespace MusicMediaKeys.Installer
{
    public partial class LanguageCatalogWindow : Window
    {
        private readonly int _uiLanguage;
        private readonly HashSet<int> _selectedIds;
        private readonly Dictionary<int, CheckBox> _checkBoxes = new();
        private bool _isUpdatingChecks;

        public IReadOnlyList<int>? SelectedLanguages { get; private set; }

        public LanguageCatalogWindow(int uiLanguage, IEnumerable<int> initialSelected)
        {
            InitializeComponent();
            _uiLanguage = uiLanguage;
            _selectedIds = new HashSet<int>(initialSelected.Where(id => Enum.IsDefined(typeof(AppLanguage), id)));
            if (_selectedIds.Count == 0)
            {
                _selectedIds.Add(uiLanguage);
            }

            Title = InstallerLocalization.Get("LanguageCatalogWindowTitle", _uiLanguage);
            TxtTitle.Text = Title;
            TxtSearchPlaceholder.Text = InstallerLocalization.Get("SearchLanguagesPlaceholder", _uiLanguage);
            BtnSelectAll.Content = InstallerLocalization.Get("SelectAll", _uiLanguage);
            BtnCurrentAndEnglish.Content = InstallerLocalization.Get("OnlyCurrentAndEnglish", _uiLanguage);
            BtnCancel.Content = InstallerLocalization.Get("Cancel", _uiLanguage);
            BtnDone.Content = InstallerLocalization.Get("Done", _uiLanguage);

            RenderLanguages("");
            UpdateSummary();
        }

        private void RenderLanguages(string filter)
        {
            GridLanguages.Children.Clear();
            _checkBoxes.Clear();
            string q = filter.Trim();

            _isUpdatingChecks = true;
            try
            {
                foreach (LanguageInfo info in LanguageCatalog.All)
                {
                    if (!string.IsNullOrEmpty(q) &&
                        !info.NativeName.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                        !info.EnglishName.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                        !info.Code.Contains(q, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    int id = (int)info.Language;
                    var contentPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var badgeBorder = new Border
                    {
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(4, 1, 4, 1),
                        Margin = new Thickness(0, 0, 7, 0),
                        MinWidth = 34,
                        VerticalAlignment = VerticalAlignment.Center,
                        BorderThickness = new Thickness(1)
                    };
                    badgeBorder.SetResourceReference(Border.BackgroundProperty, "BgHover");
                    badgeBorder.SetResourceReference(Border.BorderBrushProperty, "BorderDark");

                    var badgeText = new TextBlock
                    {
                        Text = info.Badge,
                        FontSize = 9,
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    badgeText.SetResourceReference(TextBlock.ForegroundProperty, "TealAccent");
                    badgeBorder.Child = badgeText;
                    contentPanel.Children.Add(badgeBorder);

                    var nameText = new TextBlock
                    {
                        Text = info.NativeName,
                        FontSize = 11.5,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    contentPanel.Children.Add(nameText);

                    var cb = new CheckBox
                    {
                        Content = contentPanel,
                        IsChecked = _selectedIds.Contains(id),
                        Margin = new Thickness(4, 4, 4, 4),
                        Tag = id
                    };
                    cb.Checked += LanguageCheck_Changed;
                    cb.Unchecked += LanguageCheck_Changed;

                    _checkBoxes[id] = cb;
                    GridLanguages.Children.Add(cb);
                }
            }
            finally
            {
                _isUpdatingChecks = false;
            }
        }

        private void LanguageCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingChecks || sender is not CheckBox cb || cb.Tag is not int id)
                return;

            if (cb.IsChecked == true)
            {
                _selectedIds.Add(id);
            }
            else
            {
                if (_selectedIds.Count <= 1)
                {
                    _isUpdatingChecks = true;
                    cb.IsChecked = true;
                    _isUpdatingChecks = false;
                    return;
                }
                _selectedIds.Remove(id);
            }

            UpdateSummary();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
            RenderLanguages(TxtSearch.Text);
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            _isUpdatingChecks = true;
            try
            {
                foreach (LanguageInfo info in LanguageCatalog.All)
                {
                    int id = (int)info.Language;
                    _selectedIds.Add(id);
                    if (_checkBoxes.TryGetValue(id, out var cb))
                        cb.IsChecked = true;
                }
            }
            finally
            {
                _isUpdatingChecks = false;
            }
            UpdateSummary();
        }

        private void BtnCurrentAndEnglish_Click(object sender, RoutedEventArgs e)
        {
            _isUpdatingChecks = true;
            try
            {
                _selectedIds.Clear();
                _selectedIds.Add((int)AppLanguage.English);
                _selectedIds.Add(_uiLanguage);

                foreach (var kvp in _checkBoxes)
                {
                    kvp.Value.IsChecked = _selectedIds.Contains(kvp.Key);
                }
            }
            finally
            {
                _isUpdatingChecks = false;
            }
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            TxtCountSummary.Text = string.Format(
                InstallerLocalization.Get("LanguagesSelectedFormat", _uiLanguage),
                _selectedIds.Count,
                LanguageCatalog.All.Count);
        }

        private void BtnDone_Click(object sender, RoutedEventArgs e)
        {
            SelectedLanguages = LanguageCatalog.All
                .Select(i => (int)i.Language)
                .Where(id => _selectedIds.Contains(id))
                .ToList();
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
