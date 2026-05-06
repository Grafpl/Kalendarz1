using System;
using System.Windows;
using Kalendarz1.CentrumNagranAI.Services;

namespace Kalendarz1.CentrumNagranAI.Views
{
    /// <summary>
    /// Główne okno modułu Centrum nagrań AI. Checkpoint 6 = placeholder
    /// (pole + button + status). Pełne UI z miniaturkami i playerem
    /// powstaje w checkpointach 7-8.
    /// </summary>
    public partial class CentrumNagranAIWindow : Window
    {
        public CentrumNagranAIWindow()
        {
            InitializeComponent();

            // Ładuj statystyki z bazy do paska statusu — szybki sygnał że moduł żyje.
            try
            {
                CnaConfig.ZaladujJesliTrzeba();
                FrameIndex.Init();
                long count = FrameIndex.CountFrames();
                LeftStatus.Text = $"Klatek w indeksie: {count}  •  Kamer: {CnaConfig.Kamery.Count}  •  Klucz Anthropic: " +
                                  (string.IsNullOrEmpty(CnaConfig.AnthropicApiKey) ? "BRAK" : "OK");
                RightStatus.Text = $"DB: {CnaConfig.DbPath}";
            }
            catch (Exception ex)
            {
                LeftStatus.Text = $"Błąd inicjalizacji: {ex.Message}";
            }
        }

        private async void SearchBtn_Click(object sender, RoutedEventArgs e)
        {
            string query = (QueryBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                StatusText.Text = "Wpisz zapytanie.";
                return;
            }

            SearchBtn.IsEnabled = false;
            StatusText.Text = $"Wyszukiwanie: \"{query}\" — proszę czekać...";

            try
            {
                var summary = await SearchService.SearchAsync(query, topN: 5);
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Znaleziono {summary.Hits.Count} wyników (z {summary.Candidates} kandydatów).");
                sb.AppendLine($"Czas: {summary.DurationMs}ms, koszt: ${summary.TotalCostUsd:F4}");
                sb.AppendLine();
                int rank = 1;
                foreach (var h in summary.Hits)
                {
                    sb.AppendLine($"#{rank} score={h.Score} | {h.CameraId} | {h.TsUtc:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine($"   {h.Reason}");
                    rank++;
                }
                StatusText.Text = sb.ToString();
                StatusText.TextAlignment = TextAlignment.Left;
                StatusText.Margin = new Thickness(20);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Błąd wyszukiwania: {ex.Message}";
            }
            finally
            {
                SearchBtn.IsEnabled = true;
            }
        }
    }
}
