using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kalendarz1.MarketIntelligence.Config;

namespace Kalendarz1.MarketIntelligence.Views
{
    /// <summary>
    /// Panel Administracyjny dla MarketIntelligence
    /// Pozwala na konfiguracje fraz wyszukiwania, promptow AI, kontekstu biznesowego i ustawien systemowych
    /// </summary>
    public partial class AdminPanelWindow : Window
    {
        private ObservableCollection<SearchQueryDefinition> _queries;
        private readonly List<string> _categories = new List<string>
        {
            "HPAI", "Ceny", "Konkurencja", "Regulacje", "Eksport", "Import",
            "Klienci", "Koszty", "Pogoda", "Logistyka", "Inwestycje", "Info"
        };

        public AdminPanelWindow()
        {
            InitializeComponent();
            LoadConfiguration();
        }

        /// <summary>
        /// Laduje konfiguracje do kontrolek UI
        /// </summary>
        private void LoadConfiguration()
        {
            var config = ConfigService.Instance.Current;

            // Sciezka do pliku konfiguracji
            ConfigPathText.Text = ConfigService.Instance.ConfigFilePath;

            // === ZWIAD (Queries) ===
            _queries = new ObservableCollection<SearchQueryDefinition>(config.Queries);
            QueriesDataGrid.ItemsSource = _queries;

            // Ustaw ComboBox dla kategorii
            var categoryColumn = QueriesDataGrid.Columns[2] as DataGridComboBoxColumn;
            if (categoryColumn != null)
            {
                categoryColumn.ItemsSource = _categories;
            }

            // === MOZG AI (Prompts) ===
            // Model
            foreach (ComboBoxItem item in ModelComboBox.Items)
            {
                if (item.Content.ToString() == config.Prompts.OpenAiModel)
                {
                    ModelComboBox.SelectedItem = item;
                    break;
                }
            }
            MaxTokensTextBox.Text = config.Prompts.MaxTokens.ToString();
            TemperatureTextBox.Text = config.Prompts.Temperature.ToString("0.0");
            SystemPromptTextBox.Text = config.Prompts.SystemPrompt;
            AnalysisPromptTextBox.Text = config.Prompts.AnalysisPromptTemplate;

            // === KONTEKST BIZNESOWY ===
            CompanyNameTextBox.Text = config.Business.CompanyName;
            DirectorTextBox.Text = config.Business.Director;
            LocationTextBox.Text = config.Business.Location;
            RegionTextBox.Text = config.Business.Region;
            SpecializationTextBox.Text = config.Business.Specialization;
            FinancialContextTextBox.Text = config.Business.FinancialContext;
            CompetitorsTextBox.Text = string.Join(Environment.NewLine, config.Business.Competitors ?? new List<string>());
            MainClientsTextBox.Text = string.Join(Environment.NewLine, config.Business.MainClients ?? new List<string>());

            // === SYSTEM ===
            OpenAiKeyTextBox.Text = config.System.OpenAiApiKey;
            BraveKeyTextBox.Text = config.System.BraveApiKey;

            OpenAiTimeoutTextBox.Text = config.System.OpenAiTimeoutSeconds.ToString();
            BraveTimeoutTextBox.Text = config.System.BraveTimeoutSeconds.ToString();
            ContentTimeoutTextBox.Text = config.System.ContentFetchTimeoutSeconds.ToString();
            PuppeteerTimeoutTextBox.Text = config.System.PuppeteerTimeoutSeconds.ToString();

            DelayTextBox.Text = config.System.MinDelayBetweenRequestsMs.ToString();
            MaxRetriesTextBox.Text = config.System.MaxRetries.ToString();
            RetryDelayTextBox.Text = config.System.RetryBaseDelaySeconds.ToString();

            MaxArticlesTextBox.Text = config.System.MaxArticles.ToString();
            QuickModeTextBox.Text = config.System.QuickModeArticles.ToString();
            ResultsPerQueryTextBox.Text = config.System.MaxResultsPerQuery.ToString();

            // Zapytanie testowe "1 artykuł"
            TestSearchQueryTextBox.Text = config.System.TestSearchQuery ?? "ceny drobiu Polska 2026";

            PuppeteerEnabledCheckBox.IsChecked = config.System.PuppeteerEnabled;
            CostTrackingCheckBox.IsChecked = config.System.CostTrackingEnabled;
            FileLoggingCheckBox.IsChecked = config.System.FileLoggingEnabled;
            OpenLogsFolderCheckBox.IsChecked = config.System.OpenLogsFolderAfterSession;
        }

        /// <summary>
        /// Zapisuje konfiguracje z kontrolek UI
        /// </summary>
        private void SaveConfiguration()
        {
            var config = ConfigService.Instance.Current;

            // === ZWIAD (Queries) ===
            config.Queries = _queries.ToList();

            // === MOZG AI (Prompts) ===
            if (ModelComboBox.SelectedItem is ComboBoxItem selectedModel)
            {
                config.Prompts.OpenAiModel = selectedModel.Content.ToString();
            }
            if (int.TryParse(MaxTokensTextBox.Text, out var maxTokens))
            {
                config.Prompts.MaxTokens = maxTokens;
            }
            if (double.TryParse(TemperatureTextBox.Text.Replace(',', '.'), out var temp))
            {
                config.Prompts.Temperature = Math.Max(0, Math.Min(1, temp));
            }
            config.Prompts.SystemPrompt = SystemPromptTextBox.Text;
            config.Prompts.AnalysisPromptTemplate = AnalysisPromptTextBox.Text;

            // === KONTEKST BIZNESOWY ===
            config.Business.CompanyName = CompanyNameTextBox.Text;
            config.Business.Director = DirectorTextBox.Text;
            config.Business.Location = LocationTextBox.Text;
            config.Business.Region = RegionTextBox.Text;
            config.Business.Specialization = SpecializationTextBox.Text;
            config.Business.FinancialContext = FinancialContextTextBox.Text;
            config.Business.Competitors = CompetitorsTextBox.Text
                .Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            config.Business.MainClients = MainClientsTextBox.Text
                .Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            // === SYSTEM ===
            config.System.OpenAiApiKey = OpenAiKeyTextBox.Text;
            config.System.BraveApiKey = BraveKeyTextBox.Text;

            if (int.TryParse(OpenAiTimeoutTextBox.Text, out var openAiTimeout))
                config.System.OpenAiTimeoutSeconds = openAiTimeout;
            if (int.TryParse(BraveTimeoutTextBox.Text, out var braveTimeout))
                config.System.BraveTimeoutSeconds = braveTimeout;
            if (int.TryParse(ContentTimeoutTextBox.Text, out var contentTimeout))
                config.System.ContentFetchTimeoutSeconds = contentTimeout;
            if (int.TryParse(PuppeteerTimeoutTextBox.Text, out var puppeteerTimeout))
                config.System.PuppeteerTimeoutSeconds = puppeteerTimeout;

            if (int.TryParse(DelayTextBox.Text, out var delay))
                config.System.MinDelayBetweenRequestsMs = delay;
            if (int.TryParse(MaxRetriesTextBox.Text, out var maxRetries))
                config.System.MaxRetries = maxRetries;
            if (int.TryParse(RetryDelayTextBox.Text, out var retryDelay))
                config.System.RetryBaseDelaySeconds = retryDelay;

            if (int.TryParse(MaxArticlesTextBox.Text, out var maxArticles))
                config.System.MaxArticles = maxArticles;
            if (int.TryParse(QuickModeTextBox.Text, out var quickMode))
                config.System.QuickModeArticles = quickMode;
            if (int.TryParse(ResultsPerQueryTextBox.Text, out var resultsPerQuery))
                config.System.MaxResultsPerQuery = resultsPerQuery;

            // Zapytanie testowe "1 artykuł"
            if (!string.IsNullOrWhiteSpace(TestSearchQueryTextBox.Text))
                config.System.TestSearchQuery = TestSearchQueryTextBox.Text.Trim();

            config.System.PuppeteerEnabled = PuppeteerEnabledCheckBox.IsChecked ?? true;
            config.System.CostTrackingEnabled = CostTrackingCheckBox.IsChecked ?? true;
            config.System.FileLoggingEnabled = FileLoggingCheckBox.IsChecked ?? true;
            config.System.OpenLogsFolderAfterSession = OpenLogsFolderCheckBox.IsChecked ?? false;

            // Zapisz do pliku
            ConfigService.Instance.Save();
        }

        #region Event Handlers

        private void AddQuery_Click(object sender, RoutedEventArgs e)
        {
            var newQuery = new SearchQueryDefinition
            {
                Phrase = "nowa fraza wyszukiwania",
                Category = "Info",
                IsActive = true,
                IsQuickMode = false,
                Priority = _queries.Count > 0 ? _queries.Max(q => q.Priority) + 10 : 100
            };
            _queries.Add(newQuery);

            // Przewin do nowego elementu
            QueriesDataGrid.ScrollIntoView(newQuery);
            QueriesDataGrid.SelectedItem = newQuery;
        }

        private void RemoveQuery_Click(object sender, RoutedEventArgs e)
        {
            if (QueriesDataGrid.SelectedItem is SearchQueryDefinition selected)
            {
                var result = MessageBox.Show(
                    $"Czy na pewno chcesz usunac fraze:\n\n\"{selected.Phrase}\"?",
                    "Potwierdzenie usuniecia",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _queries.Remove(selected);
                }
            }
            else
            {
                MessageBox.Show("Zaznacz fraze do usuniecia.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void TestOpenAiKey_Click(object sender, RoutedEventArgs e)
        {
            var key = OpenAiKeyTextBox.Text;
            if (string.IsNullOrWhiteSpace(key))
            {
                MessageBox.Show("Wprowadz klucz API OpenAI.", "Brak klucza",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Tymczasowo zapisz klucz i przetestuj
                var originalKey = ConfigService.Instance.Current.System.OpenAiApiKey;
                ConfigService.Instance.Current.System.OpenAiApiKey = key;

                // Test polaczenia
                var service = new Services.AI.OpenAIAnalysisService();
                var (success, message) = await service.TestConnectionAsync();

                // Przywroc oryginalny klucz
                ConfigService.Instance.Current.System.OpenAiApiKey = originalKey;

                if (success)
                {
                    MessageBox.Show($"Polaczenie OK!\n\n{message}", "Test OpenAI API",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Blad polaczenia:\n\n{message}", "Test OpenAI API",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad testu:\n\n{ex.Message}", "Test OpenAI API",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void TestBraveKey_Click(object sender, RoutedEventArgs e)
        {
            var key = BraveKeyTextBox.Text;
            if (string.IsNullOrWhiteSpace(key))
            {
                MessageBox.Show("Wprowadz klucz API Brave.", "Brak klucza",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Tymczasowo zapisz klucz i przetestuj
                var originalKey = ConfigService.Instance.Current.System.BraveApiKey;
                ConfigService.Instance.Current.System.BraveApiKey = key;

                // Test polaczenia
                var service = new Services.DataSources.BraveSearchService();
                var (success, message) = await service.TestConnectionAsync();

                // Przywroc oryginalny klucz
                ConfigService.Instance.Current.System.BraveApiKey = originalKey;

                if (success)
                {
                    MessageBox.Show($"Polaczenie OK!\n\n{message}", "Test Brave API",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Blad polaczenia:\n\n{message}", "Test Brave API",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad testu:\n\n{ex.Message}", "Test Brave API",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetToDefaults_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Czy na pewno chcesz przywrocic wszystkie ustawienia do wartosci domyslnych?\n\n" +
                "UWAGA: Klucze API zostana zachowane.",
                "Przywracanie domyslnych",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                ConfigService.Instance.ResetToDefaults();
                LoadConfiguration();
                MessageBox.Show("Przywrocono ustawienia domyslne.", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OpenConfigFolder_Click(object sender, RoutedEventArgs e)
        {
            ConfigService.Instance.OpenConfigFolder();
        }

        private void ConfigPathText_Click(object sender, MouseButtonEventArgs e)
        {
            ConfigService.Instance.OpenConfigFolder();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Przywroc konfiguracje z pliku (anuluj niezapisane zmiany)
            ConfigService.Instance.Load();
            DialogResult = false;
            Close();
        }

        private void SaveAndApply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveConfiguration();
                MessageBox.Show(
                    "Konfiguracja zostala zapisana.\n\n" +
                    $"Plik: {ConfigService.Instance.ConfigFilePath}",
                    "Zapisano",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Blad zapisu konfiguracji:\n\n{ex.Message}",
                    "Blad",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
