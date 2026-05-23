using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.MarketIntelligence.Services;

namespace Kalendarz1.MarketIntelligence.Views
{
    /// <summary>
    /// Okno zarządzania własnymi źródłami wiadomości (intel_Sources).
    /// User wpisuje URL strony, AI auto-detect RSS feed, fetch + AI streszczenie wciągane do briefingu.
    /// </summary>
    public partial class SourcesManagementWindow : Window
    {
        private readonly SourcesService _service = new();
        private List<UserSource> _sources = new();
        private UserSource _editing;

        public SourcesManagementWindow()
        {
            InitializeComponent();
            Loaded += async (_, __) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                _sources = await _service.GetAllAsync();
                dgSources.ItemsSource = null;
                dgSources.ItemsSource = _sources;
                txtStats.Text = $"Łącznie: {_sources.Count}  ·  Aktywne: {_sources.Count(s => s.IsActive)}";
                ClearEditor();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd ładowania: " + ex.Message);
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            _editing = new UserSource
            {
                IsActive = true,
                Priority = 2,
                Category = "Drób",
                Language = "pl",
                SourceType = "RSS",
                FetchIntervalMinutes = 1440
            };
            BindToEditor(_editing);
            samplePanel.Visibility = Visibility.Collapsed;
            txtDetectStatus.Text = "Wpisz URL i kliknij '🔍 Wykryj RSS'";
            txtName.Focus();
        }

        private void DgSources_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgSources.SelectedItem is UserSource s)
            {
                _editing = s;
                BindToEditor(s);
                samplePanel.Visibility = Visibility.Collapsed;
                txtDetectStatus.Text = $"Wybrane źródło. Pobrano artykułów: {s.TotalArticlesFetched}";
            }
        }

        private void BindToEditor(UserSource s)
        {
            txtName.Text = s.Name;
            txtUrl.Text = s.Url;
            txtTopics.Text = s.Topics;
            cbPriority.SelectedIndex = Math.Max(0, Math.Min(2, s.Priority - 1));
            cbCategory.Text = s.Category;
            SelectLanguage(s.Language ?? "pl");
            chkActive.IsChecked = s.IsActive;
        }

        private void ClearEditor()
        {
            _editing = null;
            txtName.Text = txtUrl.Text = txtTopics.Text = "";
            cbPriority.SelectedIndex = 1;
            cbCategory.Text = "Drób";
            SelectLanguage("pl");
            chkActive.IsChecked = true;
            txtDetectStatus.Text = "Wybierz źródło z listy lub kliknij ➕";
            samplePanel.Visibility = Visibility.Collapsed;
        }

        private void SelectLanguage(string langCode)
        {
            for (int i = 0; i < cbLanguage.Items.Count; i++)
            {
                if (cbLanguage.Items[i] is ComboBoxItem item && item.Tag?.ToString() == langCode)
                {
                    cbLanguage.SelectedIndex = i;
                    return;
                }
            }
            cbLanguage.SelectedIndex = 0;
        }

        private async void BtnDetect_Click(object sender, RoutedEventArgs e)
        {
            var url = (txtUrl.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                txtDetectStatus.Text = "⚠ Wpisz URL strony";
                return;
            }

            btnDetect.IsEnabled = false;
            txtDetectStatus.Text = $"⏳ Sprawdzam {url} ...";
            samplePanel.Visibility = Visibility.Collapsed;

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                var result = await _service.DetectRssFeedAsync(url, cts.Token);

                if (result.Success)
                {
                    txtDetectStatus.Text = $"✅ Znaleziono RSS: {result.FeedUrl}";

                    // Aktualizuj formularz: tytuł, URL feed
                    if (string.IsNullOrWhiteSpace(txtName.Text))
                        txtName.Text = result.FeedTitle;
                    if (result.FeedUrl != url)
                        txtUrl.Text = result.FeedUrl;

                    if (result.SampleArticles?.Any() == true)
                    {
                        lstSamples.ItemsSource = result.SampleArticles;
                        samplePanel.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    txtDetectStatus.Text = $"⚠ {result.Error}.\nUstawi się typ 'WebScraping' (HTML parsing).";
                    // Wymuś WebScraping mode dla URL bez RSS
                    // (zapis automatyczny w BtnSave)
                }
            }
            catch (Exception ex)
            {
                txtDetectStatus.Text = "❌ Błąd: " + ex.Message;
            }
            finally
            {
                btnDetect.IsEnabled = true;
            }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_editing == null) _editing = new UserSource();

            // Walidacja
            var name = (txtName.Text ?? "").Trim();
            var url = (txtUrl.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Podaj nazwę źródła.");
                txtName.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("Podaj URL.");
                txtUrl.Focus();
                return;
            }
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                url = "https://" + url;

            // Mapowanie z formularza
            _editing.Name = name;
            _editing.Url = url;
            _editing.Topics = (txtTopics.Text ?? "").Trim();
            _editing.Category = (cbCategory.Text ?? "Drób").Trim();
            _editing.Language = (cbLanguage.SelectedItem is ComboBoxItem langItem && langItem.Tag != null)
                ? langItem.Tag.ToString()
                : "pl";
            _editing.IsActive = chkActive.IsChecked == true;
            if (cbPriority.SelectedItem is ComboBoxItem priItem && int.TryParse(priItem.Tag?.ToString(), out var pri))
                _editing.Priority = pri;

            // SourceType — auto-detect: jeśli URL zawiera /feed/, /rss/, .xml — to RSS, inaczej WebScraping
            var lower = url.ToLowerInvariant();
            _editing.SourceType = (lower.Contains("/feed") || lower.Contains("/rss") || lower.EndsWith(".xml") || lower.EndsWith(".atom"))
                ? "RSS" : "WebScraping";

            try
            {
                if (string.IsNullOrEmpty(_editing.Id))
                {
                    await _service.InsertAsync(_editing);
                }
                else
                {
                    await _service.UpdateAsync(_editing);
                }
                await LoadAsync();
                MessageBox.Show($"✅ Zapisano: {_editing.Name}\n\nŹródło będzie używane przy następnym pełnym pobraniu (ręczne lub auto 06:00).");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd zapisu: " + ex.Message);
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_editing == null || string.IsNullOrEmpty(_editing.Id))
            {
                MessageBox.Show("Brak źródła do usunięcia.");
                return;
            }
            var result = MessageBox.Show(
                $"Usunąć źródło '{_editing.Name}'?\n\nTo nie usunie już pobranych artykułów z intel_Articles.",
                "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _service.DeleteAsync(_editing.Id);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd usunięcia: " + ex.Message);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => ClearEditor();
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnToggleHelp_Click(object sender, RoutedEventArgs e)
        {
            helpBanner.Visibility = helpBanner.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }
}
