using Kalendarz1.Transport.Formularze;
using Kalendarz1.Transport.Repozytorium;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;

namespace Kalendarz1.Transport.Views
{
    /// <summary>
    /// Faza 7+ — WPF widok planowania kursów (zastępuje launcher w TransportHubWindow Tab Planowanie).
    /// Edytor kursu (EdytorKursuWithPalety) wciąż otwierany jako WinForms modal —
    /// pełna konwersja edytora (drag&drop palet, ~1900 linii) pozostawiona na osobną fazę.
    /// </summary>
    public partial class TransportPlanowanieView : UserControl
    {
        private static readonly string _connTransport =
            "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private static readonly string _connHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        private readonly TransportRepozytorium _repo;
        private List<KursRow> _rows = new();

        public TransportPlanowanieView()
        {
            InitializeComponent();
            _repo = new TransportRepozytorium(_connTransport, _connHandel);
            DataKursu.SelectedDate = DateTime.Today;
            Loaded += async (_, _) => await LoadKursyAsync();
            KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.F5) { _ = LoadKursyAsync(); e.Handled = true; }
            };
        }

        // ═══════════════════════════════════════════════════════════════════
        // Nawigacja datą
        // ═══════════════════════════════════════════════════════════════════
        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            DataKursu.SelectedDate = (DataKursu.SelectedDate ?? DateTime.Today).AddDays(-1);
        }
        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            DataKursu.SelectedDate = (DataKursu.SelectedDate ?? DateTime.Today).AddDays(1);
        }
        private void BtnDzis_Click(object sender, RoutedEventArgs e)
        {
            DataKursu.SelectedDate = DateTime.Today;
        }
        private async void DataKursu_Changed(object sender, SelectionChangedEventArgs e)
            => await LoadKursyAsync();
        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
            => await LoadKursyAsync();

        // ═══════════════════════════════════════════════════════════════════
        // Load
        // ═══════════════════════════════════════════════════════════════════
        private async Task LoadKursyAsync()
        {
            try
            {
                var data = DataKursu.SelectedDate ?? DateTime.Today;
                StatusText.Text = $"Ładowanie kursów {data:dd.MM.yyyy}...";
                DayNameText.Text = data.ToString("dddd", new CultureInfo("pl-PL"));

                var kursy = await _repo.PobierzKursyPoDacieAsync(data);
                _rows = kursy.Select(k => new KursRow(k)).ToList();
                KursyGrid.ItemsSource = _rows;

                UpdateKpi();
                StatusText.Text = $"Załadowano {_rows.Count} kursów na {data:dd.MM.yyyy}";

                UpdateButtonsEnabled();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Błąd: {ex.Message}";
                MessageBox.Show($"Błąd ładowania kursów:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateKpi()
        {
            KpiKursy.Text = _rows.Count.ToString();
            KpiZKierowca.Text = _rows.Count(r => !string.IsNullOrEmpty(r.KierowcaNazwa)).ToString();
            KpiBezZasobow.Text = _rows.Count(r => string.IsNullOrEmpty(r.KierowcaNazwa) || string.IsNullOrEmpty(r.PojazdRejestracja)).ToString();
            KpiPalety.Text = _rows.Sum(r => r.PaletyNominal).ToString();
        }

        // ═══════════════════════════════════════════════════════════════════
        // Selekcja
        // ═══════════════════════════════════════════════════════════════════
        private void KursyGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => UpdateButtonsEnabled();

        private void UpdateButtonsEnabled()
        {
            bool hasSel = KursyGrid?.SelectedItem != null;
            if (BtnEdytuj != null) BtnEdytuj.IsEnabled = hasSel;
            if (BtnUsun != null) BtnUsun.IsEnabled = hasSel;
        }

        private void KursyGrid_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (KursyGrid.SelectedItem is KursRow) OtworzEdytor(false);
        }

        // ═══════════════════════════════════════════════════════════════════
        // Akcje
        // ═══════════════════════════════════════════════════════════════════
        private void BtnNowy_Click(object sender, RoutedEventArgs e) => OtworzEdytor(true);
        private void BtnEdytuj_Click(object sender, RoutedEventArgs e) => OtworzEdytor(false);

        private void OtworzEdytor(bool nowy)
        {
            try
            {
                var data = DataKursu.SelectedDate ?? DateTime.Today;
                var user = App.UserID ?? "system";

                System.Windows.Forms.DialogResult result;
                if (nowy)
                {
                    using var f = new EdytorKursuWithPalety(_repo, data, user);
                    result = f.ShowDialog();
                }
                else
                {
                    if (KursyGrid.SelectedItem is not KursRow row) return;
                    using var f = new EdytorKursuWithPalety(_repo, row.Source, user);
                    result = f.ShowDialog();
                }

                if (result == System.Windows.Forms.DialogResult.OK)
                    _ = LoadKursyAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwierania edytora:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnUsun_Click(object sender, RoutedEventArgs e)
        {
            if (KursyGrid.SelectedItem is not KursRow row) return;

            var msg = $"Usunąć kurs #{row.KursID}?\n" +
                      $"Trasa: {row.Trasa ?? "—"}\n" +
                      $"Kierowca: {row.KierowcaNazwa ?? "—"} · Pojazd: {row.PojazdRejestracja ?? "—"}\n\n" +
                      "Zamówienia w kursie wrócą do statusu wolnych.";
            var res = MessageBox.Show(msg, "Potwierdź usunięcie",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            try
            {
                await _repo.UsunKursAsync(row.KursID);
                StatusText.Text = $"Usunięto kurs #{row.KursID}";
                await LoadKursyAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd usuwania:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Row wrapper
        // ═══════════════════════════════════════════════════════════════════
        public class KursRow
        {
            public Kurs Source { get; }
            public long KursID => Source.KursID;
            public string? Trasa => Source.Trasa;
            public string? KierowcaNazwa => Source.KierowcaNazwa;
            public string? PojazdRejestracja => Source.PojazdRejestracja;
            public string Status => Source.Status ?? "Planowany";
            public int PaletyNominal => Source.PaletyNominal;

            // Polish++ — kolorowy pill dla statusu kursu (matching enum z Shared/Domain/KursStatus.cs)
            public System.Windows.Media.Brush StatusBgColor => Status switch
            {
                "WTrasie" or "W trasie"     => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 245, 233)),
                "Zakonczony" or "Zakończony" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 234, 246)),
                "Anulowany"                  => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 235, 238)),
                "Akceptowany"                => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(225, 245, 254)),
                _                            => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 243, 224))  // Planowany
            };
            public System.Windows.Media.Brush StatusFgColor => Status switch
            {
                "WTrasie" or "W trasie"     => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 125, 50)),
                "Zakonczony" or "Zakończony" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(57, 73, 171)),
                "Anulowany"                  => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(198, 40, 40)),
                "Akceptowany"                => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(2, 119, 189)),
                _                            => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 81, 0))   // Planowany pomarańcz
            };

            public string GodzinyDisplay
            {
                get
                {
                    var w = Source.GodzWyjazdu?.ToString(@"hh\:mm") ?? "—";
                    var p = Source.GodzPowrotu?.ToString(@"hh\:mm") ?? "—";
                    return $"{w} → {p}";
                }
            }

            public string WypelnienieDisplay
            {
                get
                {
                    if (Source.PaletyPojazdu <= 0) return "—";
                    return $"{Source.PaletyNominal}/{Source.PaletyPojazdu}  ({Source.ProcNominal:F0}%)";
                }
            }

            public KursRow(Kurs k) { Source = k; }
        }
    }
}
