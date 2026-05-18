using Kalendarz1.Flota.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;

namespace Kalendarz1.Flota.Views
{
    /// <summary>
    /// Faza 6-C — Dialog mapowania TransportPL ↔ Flota (LibraNet).
    /// Wymaga uruchomienia <c>Transport/SQL/alter_link_to_flota.sql</c> (Faza 6-A).
    /// </summary>
    public partial class MapowanieFlotaWindow : Window
    {
        private readonly FlotaTransportBridgeService _svc = new();

        public ObservableCollection<PojazdRow> PojazdyRows { get; } = new();
        public ObservableCollection<KierowcaRow> KierowcyRows { get; } = new();

        // Combo sources (z opcją "brak mapowania" na początku)
        public ObservableCollection<FlotaTransportBridgeService.FlotaPojazd> FlotaPojazdyCombo { get; } = new();
        public ObservableCollection<FlotaTransportBridgeService.FlotaKierowca> FlotaKierowcyCombo { get; } = new();

        public MapowanieFlotaWindow()
        {
            InitializeComponent();
            DataContext = this;
            try { WindowIconHelper.SetIcon(this); } catch { }
            Loaded += async (_, _) => await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                StatusText.Text = "Ładowanie...";

                var transportPojazdy = await _svc.GetTransportPojazdyAsync();
                var transportKierowcy = await _svc.GetTransportKierowcyAsync();
                var flotaPojazdy = await _svc.GetFlotaPojazdyAsync();
                var flotaKierowcy = await _svc.GetFlotaKierowcyAsync();

                // Combo: "(brak)" na poczatku + lista
                FlotaPojazdyCombo.Clear();
                FlotaPojazdyCombo.Add(new FlotaTransportBridgeService.FlotaPojazd { ID = "", Brand = "— brak mapowania —" });
                foreach (var f in flotaPojazdy) FlotaPojazdyCombo.Add(f);

                FlotaKierowcyCombo.Clear();
                FlotaKierowcyCombo.Add(new FlotaTransportBridgeService.FlotaKierowca { GID = 0, Name = "— brak mapowania —" });
                foreach (var f in flotaKierowcy) FlotaKierowcyCombo.Add(f);

                PojazdyRows.Clear();
                foreach (var t in transportPojazdy)
                    PojazdyRows.Add(new PojazdRow(t));

                KierowcyRows.Clear();
                foreach (var t in transportKierowcy)
                    KierowcyRows.Add(new KierowcaRow(t));

                PojazdyGrid.ItemsSource = PojazdyRows;
                KierowcyGrid.ItemsSource = KierowcyRows;

                UpdateStats();
                StatusText.Text = "Gotowy";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Błąd: {ex.Message}";
                MessageBox.Show($"Błąd ładowania danych:\n{ex.Message}\n\n" +
                    "Upewnij się, że uruchomiłeś SQL: Transport/SQL/alter_link_to_flota.sql",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateStats()
        {
            PojazdyTotalCount.Text = PojazdyRows.Count.ToString();
            PojazdyMappedCount.Text = PojazdyRows.Count(p => !string.IsNullOrEmpty(p.LibraNetCarTrailerID)).ToString();
            KierowcyTotalCount.Text = KierowcyRows.Count.ToString();
            KierowcyMappedCount.Text = KierowcyRows.Count(k => k.LibraNetDriverGID.HasValue && k.LibraNetDriverGID.Value != 0).ToString();
        }

        private async void BtnAutoMapPojazdy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Auto-mapowanie pojazdów...";
                var mapped = await _svc.AutoMapPojazdyByRegistrationAsync();
                StatusText.Text = $"Auto-mapowanie: {mapped} pojazdów";
                await LoadDataAsync();
                MessageBox.Show($"Zmapowano {mapped} pojazdów po rejestracji.", "Auto-mapowanie",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Błąd: {ex.Message}";
            }
        }

        private async void BtnAutoMapKierowcy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Auto-mapowanie kierowców...";
                var mapped = await _svc.AutoMapKierowcyByNameAsync();
                StatusText.Text = $"Auto-mapowanie: {mapped} kierowców";
                await LoadDataAsync();
                MessageBox.Show($"Zmapowano {mapped} kierowców po Imię + Nazwisko.", "Auto-mapowanie",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Błąd: {ex.Message}";
            }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Zapisywanie...";
                int savedP = 0, savedK = 0;

                // Pojazdy — tylko zmiany
                foreach (var p in PojazdyRows.Where(p => p.IsDirty))
                {
                    string? ctId = string.IsNullOrEmpty(p.LibraNetCarTrailerID) ? null : p.LibraNetCarTrailerID;
                    await _svc.SaveMappingPojazdAsync(p.PojazdID, ctId);
                    p.MarkClean();
                    savedP++;
                }

                foreach (var k in KierowcyRows.Where(k => k.IsDirty))
                {
                    int? gid = (k.LibraNetDriverGID.HasValue && k.LibraNetDriverGID.Value != 0) ? k.LibraNetDriverGID : null;
                    await _svc.SaveMappingKierowcaAsync(k.KierowcaID, gid);
                    k.MarkClean();
                    savedK++;
                }

                UpdateStats();
                StatusText.Text = $"Zapisano: {savedP} pojazdów, {savedK} kierowców";
                MessageBox.Show($"Zapisano:\n  • {savedP} pojazdów\n  • {savedK} kierowców",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Błąd zapisu: {ex.Message}";
                MessageBox.Show($"Błąd zapisu:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // ════════════════════════════════════════════════════════════════
        // Row models with dirty tracking
        // ════════════════════════════════════════════════════════════════

        public class PojazdRow : INotifyPropertyChanged
        {
            private string? _libraNetCarTrailerID;
            private readonly string? _originalCarTrailerID;

            public int PojazdID { get; }
            public string Rejestracja { get; }
            public string MarkaModelDisplay { get; }
            public bool Aktywny { get; }

            public string? LibraNetCarTrailerID
            {
                get => _libraNetCarTrailerID;
                set
                {
                    if (_libraNetCarTrailerID == value) return;
                    _libraNetCarTrailerID = value;
                    OnPropertyChanged();
                }
            }

            public bool IsDirty => _libraNetCarTrailerID != _originalCarTrailerID;
            public void MarkClean() { /* po Save: nowy original = current */ }

            public PojazdRow(FlotaTransportBridgeService.TransportPojazd t)
            {
                PojazdID = t.PojazdID;
                Rejestracja = t.Rejestracja;
                MarkaModelDisplay = string.IsNullOrEmpty(t.Marka) ? "—" : $"{t.Marka} {t.Model}";
                Aktywny = t.Aktywny;
                _libraNetCarTrailerID = t.LibraNetCarTrailerID;
                _originalCarTrailerID = t.LibraNetCarTrailerID;
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            void OnPropertyChanged([CallerMemberName] string? n = null) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        }

        public class KierowcaRow : INotifyPropertyChanged
        {
            private int? _libraNetDriverGID;
            private readonly int? _originalDriverGID;

            public int KierowcaID { get; }
            public string Display { get; }
            public string? Telefon { get; }
            public bool Aktywny { get; }

            public int? LibraNetDriverGID
            {
                get => _libraNetDriverGID;
                set
                {
                    if (_libraNetDriverGID == value) return;
                    _libraNetDriverGID = value;
                    OnPropertyChanged();
                }
            }

            public bool IsDirty => _libraNetDriverGID != _originalDriverGID;
            public void MarkClean() { }

            public KierowcaRow(FlotaTransportBridgeService.TransportKierowca t)
            {
                KierowcaID = t.KierowcaID;
                Display = t.Display;
                Telefon = t.Telefon;
                Aktywny = t.Aktywny;
                _libraNetDriverGID = t.LibraNetDriverGID;
                _originalDriverGID = t.LibraNetDriverGID;
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            void OnPropertyChanged([CallerMemberName] string? n = null) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        }
    }
}
