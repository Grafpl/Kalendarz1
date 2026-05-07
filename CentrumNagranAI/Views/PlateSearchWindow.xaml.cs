using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Kalendarz1.CentrumNagranAI.Services;

namespace Kalendarz1.CentrumNagranAI.Views
{
    public partial class PlateSearchWindow : Window
    {
        public class PlateHitVm
        {
            public long FrameId { get; set; }
            public string Plate { get; set; } = string.Empty;
            public DateTime TsUtc { get; set; }
            public string CameraId { get; set; } = string.Empty;
            public string FilePath { get; set; } = string.Empty;
            public string TimeLabel => TsUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            public string CameraDisplayName => CnaConfig.DisplayName(CameraId);
        }

        private readonly ObservableCollection<PlateHitVm> _hits = new();

        public PlateSearchWindow()
        {
            InitializeComponent();
            ResultsGrid.ItemsSource = _hits;
            CnaConfig.ZaladujJesliTrzeba();
            FrameIndex.Init();
        }

        private void QueryBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !e.IsRepeat) { e.Handled = true; DoSearch(); }
        }

        private void Search_Click(object sender, RoutedEventArgs e) => DoSearch();

        private void DoSearch()
        {
            var q = (QueryBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(q)) return;

            _hits.Clear();
            try
            {
                var results = PlateDetectionService.SearchByPlate(q, 200);
                foreach (var r in results)
                {
                    _hits.Add(new PlateHitVm
                    {
                        FrameId = r.FrameId,
                        Plate = r.Plate,
                        TsUtc = r.Ts,
                        CameraId = r.CameraId,
                        FilePath = r.FilePath
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
