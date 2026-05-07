using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using Kalendarz1.CentrumNagranAI.Services;

namespace Kalendarz1.CentrumNagranAI.Views
{
    public partial class CnaSettingsWindow : Window
    {
        public class CameraNameRow : INotifyPropertyChanged
        {
            public string CameraId { get; set; } = string.Empty;
            private string _displayName = string.Empty;
            public string DisplayName
            {
                get => _displayName;
                set { _displayName = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName))); }
            }
            public event PropertyChangedEventHandler? PropertyChanged;
        }

        public class OcrCameraRow : INotifyPropertyChanged
        {
            public string CameraId { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;
            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
            }
            public event PropertyChangedEventHandler? PropertyChanged;
        }

        public bool ChangesSaved { get; private set; }

        private readonly ObservableCollection<CameraNameRow> _rows = new();
        private readonly ObservableCollection<OcrCameraRow> _ocrRows = new();

        public CnaSettingsWindow()
        {
            InitializeComponent();
            CnaConfig.ZaladujJesliTrzeba();

            foreach (var k in CnaConfig.Kamery)
            {
                _rows.Add(new CameraNameRow
                {
                    CameraId = k.Id,
                    DisplayName = CnaConfig.NazwyKamer.TryGetValue(k.Id, out var n) ? n : string.Empty
                });
                _ocrRows.Add(new OcrCameraRow
                {
                    CameraId = k.Id,
                    Label = CnaConfig.DisplayName(k),
                    IsSelected = CnaConfig.KameryDoOcr.Contains(k.Id)
                });
            }
            CameraNamesList.ItemsSource = _rows;
            OcrCamerasList.ItemsSource = _ocrRows;

            RetencjaBox.Text = CnaConfig.RetencjaDni.ToString();
            InterwalBox.Text = CnaConfig.InterwalKlatkiSekund.ToString();
            TopNBox.Text = CnaConfig.TopNFinalnych.ToString();
            AnomalyThresholdSlider.Value = CnaConfig.AnomalyThreshold;
            AnomalyThresholdLabel.Text = CnaConfig.AnomalyThreshold.ToString("F2");

            ZaladujStorageInfo();
        }

        private void AnomalyThresholdSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (AnomalyThresholdLabel != null)
                AnomalyThresholdLabel.Text = e.NewValue.ToString("F2");
        }

        private void ZaladujStorageInfo()
        {
            try
            {
                long total = 0;
                int files = 0;
                if (Directory.Exists(CnaConfig.FramesDir))
                {
                    foreach (var f in Directory.EnumerateFiles(CnaConfig.FramesDir, "*.jpg", SearchOption.AllDirectories))
                    {
                        total += new FileInfo(f).Length;
                        files++;
                    }
                }
                long inDb = FrameIndex.CountFrames();
                StorageInfoText.Text =
                    $"Aktualnie: {files:N0} plików JPEG ({total / 1024.0 / 1024.0:F1} MB) na dysku, {inDb:N0} rekordów w bazie.";
            }
            catch (Exception ex)
            {
                StorageInfoText.Text = $"Nie mogę odczytać statystyk: {ex.Message}";
            }
        }

        private void CleanupNowBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(RetencjaBox.Text, out int dni) || dni < 1)
            {
                MessageBox.Show("Retencja musi być liczbą >= 1", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Skasować wszystkie klatki starsze niż {dni} dni? To nieodwracalne.",
                "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            CleanupNowBtn.IsEnabled = false;
            try
            {
                var (filesDel, mb, rows) = IndexerBackgroundService.CleanupOldFrames(dni);
                StatusText.Text = $"✓ Skasowano {filesDel} plików ({mb:F1} MB) + {rows} rekordów DB.";
                ZaladujStorageInfo();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Błąd: {ex.Message}";
            }
            finally
            {
                CleanupNowBtn.IsEnabled = true;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(RetencjaBox.Text, out int dni) || dni < 1 || dni > 365)
            { MessageBox.Show("Retencja musi być liczbą 1-365"); return; }
            if (!int.TryParse(InterwalBox.Text, out int sek) || sek < 2 || sek > 600)
            { MessageBox.Show("Interwał musi być liczbą 2-600 (sekundy)"); return; }
            if (!int.TryParse(TopNBox.Text, out int topN) || topN < 1 || topN > 100)
            { MessageBox.Show("Top N musi być liczbą 1-100"); return; }

            CnaConfig.RetencjaDni = dni;
            CnaConfig.InterwalKlatkiSekund = sek;
            CnaConfig.TopNFinalnych = topN;
            CnaConfig.AnomalyThreshold = Math.Round(AnomalyThresholdSlider.Value, 2);
            CnaConfig.NazwyKamer = _rows
                .Where(r => !string.IsNullOrWhiteSpace(r.DisplayName))
                .ToDictionary(r => r.CameraId, r => r.DisplayName.Trim());
            CnaConfig.KameryDoOcr = _ocrRows.Where(o => o.IsSelected).Select(o => o.CameraId).ToList();

            try
            {
                CnaConfig.ZapiszUstawienia();
                ChangesSaved = true;
                StatusText.Text = "✓ Zapisano.";
                Close();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Błąd zapisu: {ex.Message}";
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
