using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kalendarz1.CentrumNagranAI.Services;

namespace Kalendarz1.CentrumNagranAI.Views
{
    /// <summary>
    /// View-model dla pojedynczego wyniku w UI. Trzyma miniaturkę jako BitmapImage
    /// żeby DataTemplate mógł ją wyświetlić bez konwerterów.
    /// </summary>
    public class SearchHitVm
    {
        public int Rank { get; set; }
        public long FrameId { get; set; }
        public string CameraId { get; set; } = string.Empty;
        public DateTime TsUtc { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public int Score { get; set; }
        public string Reason { get; set; } = string.Empty;
        public BitmapImage? Thumbnail { get; set; }
        public Brush ScoreColor =>
            Score >= 80 ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)) :
            Score >= 50 ? new SolidColorBrush(Color.FromRgb(0xCA, 0x8A, 0x04)) :
                          new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
        public string TimeLabel => TsUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        // Pokazujemy nazwę kamery z ustawień (np. "Hala uboju") z fallbackiem do CameraId.
        public string CameraDisplayName => Kalendarz1.CentrumNagranAI.Services.CnaConfig.DisplayName(CameraId);
    }

    /// <summary>
    /// Chip kamery — toggle button z label i flagą czy zaznaczony.
    /// </summary>
    public class CameraChipVm : System.ComponentModel.INotifyPropertyChanged
    {
        public string CameraId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected))); }
        }
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    public partial class CentrumNagranAIWindow : Window
    {
        private readonly ObservableCollection<SearchHitVm> _hits = new();
        private readonly ObservableCollection<CameraChipVm> _cameraChips = new();

        public CentrumNagranAIWindow()
        {
            InitializeComponent();
            ResultsList.ItemsSource = _hits;
            CameraChipsList.ItemsSource = _cameraChips;

            try
            {
                CnaConfig.ZaladujJesliTrzeba();
                FrameIndex.Init();
                long count = FrameIndex.CountFrames();
                LeftStatus.Text = $"Klatek w indeksie: {count}  •  Kamer: {CnaConfig.Kamery.Count}  •  Klucz Anthropic: " +
                                  (string.IsNullOrEmpty(CnaConfig.AnthropicApiKey) ? "BRAK" : "OK");
                RightStatus.Text = $"DB: {CnaConfig.DbPath}";

                ZaladujChipyKamer();
            }
            catch (Exception ex)
            {
                LeftStatus.Text = $"Błąd inicjalizacji: {ex.Message}";
            }
        }

        private void ZaladujChipyKamer()
        {
            _cameraChips.Clear();
            foreach (var k in CnaConfig.Kamery)
            {
                _cameraChips.Add(new CameraChipVm
                {
                    CameraId = k.Id,
                    Label = CnaConfig.DisplayName(k),
                    IsSelected = true
                });
            }
        }

        private void ClearDateBtn_Click(object sender, RoutedEventArgs e)
        {
            FromDatePicker.SelectedDate = null;
            ToDatePicker.SelectedDate = null;
            FromTimeBox.Text = "00:00";
            ToTimeBox.Text = "23:59";
        }

        private DateTime? ParseDateTime(DatePicker picker, TextBox timeBox)
        {
            if (!picker.SelectedDate.HasValue) return null;
            var d = picker.SelectedDate.Value.Date;
            if (TimeSpan.TryParse(timeBox.Text, out var t)) d = d.Add(t);
            return d.ToUniversalTime();
        }

        private void QueryBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !e.IsRepeat)
            {
                e.Handled = true;
                _ = WykonajWyszukiwanieAsync();
            }
        }

        private async void SearchBtn_Click(object sender, RoutedEventArgs e)
        {
            await WykonajWyszukiwanieAsync();
        }

        private async System.Threading.Tasks.Task WykonajWyszukiwanieAsync()
        {
            string query = (QueryBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                EmptyText.Text = "Wpisz zapytanie.";
                return;
            }

            SearchBtn.IsEnabled = false;
            QueryBox.IsEnabled = false;
            _hits.Clear();
            EmptyText.Visibility = Visibility.Collapsed;
            ResultsScroller.Visibility = Visibility.Collapsed;
            LoaderOverlay.Visibility = Visibility.Visible;
            LoaderText.Text = $"Wyszukuję: \"{query}\" ...";

            try
            {
                DateTime? fromUtc = ParseDateTime(FromDatePicker, FromTimeBox);
                DateTime? toUtc = ParseDateTime(ToDatePicker, ToTimeBox);
                var selectedCams = _cameraChips.Where(c => c.IsSelected).Select(c => c.CameraId).ToList();
                // Jeśli wszystkie zaznaczone albo żadne — przekaż null (=wszystkie kamery).
                var camFilter = (selectedCams.Count == 0 || selectedCams.Count == _cameraChips.Count)
                    ? null : selectedCams;

                int topN = Math.Max(1, CnaConfig.TopNFinalnych);
                var summary = await SearchService.SearchAsync(
                    query, topN: topN,
                    fromUtc: fromUtc, toUtc: toUtc,
                    cameraIds: camFilter);

                int rank = 1;
                foreach (var h in summary.Hits)
                {
                    _hits.Add(new SearchHitVm
                    {
                        Rank = rank++,
                        FrameId = h.FrameId,
                        CameraId = h.CameraId,
                        TsUtc = h.TsUtc,
                        FilePath = h.FilePath,
                        Score = h.Score,
                        Reason = string.IsNullOrEmpty(h.Reason) ? "(brak opisu)" : h.Reason,
                        Thumbnail = LoadThumbnail(h.FilePath)
                    });
                }

                if (_hits.Count == 0)
                {
                    EmptyText.Text = "Brak wyników. Dorzuć więcej klatek (--cna-test) lub zmień zapytanie.";
                    EmptyText.Visibility = Visibility.Visible;
                }
                else
                {
                    ResultsScroller.Visibility = Visibility.Visible;
                }

                RightStatus.Text =
                    $"VLM calls: {summary.VlmCalls}  •  koszt: ${summary.TotalCostUsd:F4}  •  czas: {summary.DurationMs}ms  •  audit: {summary.AuditId}";
            }
            catch (Exception ex)
            {
                EmptyText.Text = $"Błąd: {ex.Message}";
                EmptyText.Visibility = Visibility.Visible;
            }
            finally
            {
                LoaderOverlay.Visibility = Visibility.Collapsed;
                SearchBtn.IsEnabled = true;
                QueryBox.IsEnabled = true;
            }
        }

        private static BitmapImage? LoadThumbnail(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 480;
                bmp.UriSource = new Uri(filePath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var w = new CnaSettingsWindow { Owner = this };
            w.ShowDialog();
            if (w.ChangesSaved)
            {
                CnaConfig.Przeladuj();
                // Odśwież nazwy w bieżących wynikach (PropertyChanged: po prostu re-bind)
                var snapshot = _hits.ToList();
                _hits.Clear();
                foreach (var h in snapshot) _hits.Add(h);
                ZaladujChipyKamer();
                LeftStatus.Text = $"Klatek w indeksie: {FrameIndex.CountFrames()}  •  Kamer: {CnaConfig.Kamery.Count}  •  Klucz Anthropic: " +
                                  (string.IsNullOrEmpty(CnaConfig.AnthropicApiKey) ? "BRAK" : "OK");
            }
        }

        private void AuditBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CnaConfig.ZaladujJesliTrzeba();
                System.IO.Directory.CreateDirectory(CnaConfig.AuditDir);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = CnaConfig.AuditDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie mogę otworzyć folderu audit: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void PlayBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is SearchHitVm vm)
            {
                var player = new ClipPlayerWindow(vm)
                {
                    Owner = this
                };
                player.ShowDialog();
            }
        }
    }
}
