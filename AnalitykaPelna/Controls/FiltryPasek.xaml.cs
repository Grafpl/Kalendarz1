using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.AnalitykaPelna.Models;
using Kalendarz1.AnalitykaPelna.Services;

namespace Kalendarz1.AnalitykaPelna.Controls
{
    public enum TrybZakladki { Plan, Realizacja, Bilans, Wydajnosc }

    public partial class FiltryPasek : UserControl
    {
        public event EventHandler<FiltryAnaliz>? FiltryZastosowane;
        public event EventHandler? LiveKlik;
        public event EventHandler? EksportKlik;
        public event EventHandler? ZamknijKlik;

        private TrybZakladki _tryb = TrybZakladki.Bilans;
        private bool _ladujKombo;
        private bool _zaladowano;

        public FiltryPasek()
        {
            InitializeComponent();

            // Domyślny zakres: ostatnie 7 dni
            dpDataOd.SelectedDate = DateTime.Today.AddDays(-AnalitykaConfig.DomyslnyZakresDni);
            dpDataDo.SelectedDate = DateTime.Today;

            UstawTryb(TrybZakladki.Bilans);
        }

        /// <summary>
        /// Przywraca ostatnie zapisane daty z AnalitykaSettings (jeśli są sensowne).
        /// </summary>
        public void PrzywrocOstatnieDaty()
        {
            var od = AnalitykaSettings.OstatniaDataOd;
            var doData = AnalitykaSettings.OstatniaDataDo;
            if (od.HasValue && doData.HasValue && od.Value <= doData.Value
                && (DateTime.Today - od.Value).TotalDays < 365)
            {
                _ladujKombo = true;
                try
                {
                    dpDataOd.SelectedDate = od.Value;
                    dpDataDo.SelectedDate = doData.Value;
                }
                finally { _ladujKombo = false; }
            }
            // Liczba tygodni prognozy
            int tyg = AnalitykaSettings.OstatniLiczbaTygodniPrognozy;
            for (int i = 0; i < cbTygodnie.Items.Count; i++)
            {
                if (cbTygodnie.Items[i] is ComboBoxItem ci && ci.Tag is string tagStr
                    && int.TryParse(tagStr, out int t) && t == tyg)
                {
                    cbTygodnie.SelectedIndex = i;
                    break;
                }
            }
        }

        public TrybZakladki Tryb
        {
            get => _tryb;
            set => UstawTryb(value);
        }

        private void UstawTryb(TrybZakladki tryb)
        {
            _tryb = tryb;
            // Widoczność pól per tryb
            panelOperator.Visibility = tryb == TrybZakladki.Realizacja ? Visibility.Visible : Visibility.Collapsed;
            panelKlasa.Visibility = tryb == TrybZakladki.Realizacja ? Visibility.Visible : Visibility.Collapsed;
            panelTygodnie.Visibility = tryb == TrybZakladki.Plan ? Visibility.Visible : Visibility.Collapsed;
            miPresetPrognoza.Visibility = tryb == TrybZakladki.Plan ? Visibility.Visible : Visibility.Collapsed;
        }

        public async Task ZaladujKomboboxyAsync()
        {
            if (_zaladowano) return;
            _ladujKombo = true;
            try
            {
                var prognoza = new PrognozaService();
                var realizacja = new RealizacjaService();

                var towary = await prognoza.LoadTowaryAsync();
                cbTowar.ItemsSource = towary;
                if (towary.Count > 0) cbTowar.SelectedIndex = 0;

                var hodowcy = await realizacja.LoadHodowcyAsync();
                cbHodowca.ItemsSource = hodowcy;
                if (hodowcy.Count > 0) cbHodowca.SelectedIndex = 0;

                var operatorzy = await realizacja.LoadOperatorzyAsync();
                cbOperator.ItemsSource = operatorzy;
                if (operatorzy.Count > 0) cbOperator.SelectedIndex = 0;

                cbKlasa.SelectedIndex = 0;
                _zaladowano = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się załadować list filtrów:\n{ex.Message}",
                    "Filtry", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                _ladujKombo = false;
            }
        }

        public FiltryAnaliz ZbierzFiltry()
        {
            var f = new FiltryAnaliz
            {
                DataOd = dpDataOd.SelectedDate ?? DateTime.Today.AddDays(-7),
                DataDo = dpDataDo.SelectedDate ?? DateTime.Today
            };

            if (cbTowar.SelectedItem is TowarComboItem t && t.IdHandel > 0)
                f.TowarIdHandel = t.IdHandel;

            if (cbHodowca.SelectedItem is HodowcaComboItem h && !string.IsNullOrEmpty(h.CustomerID))
                f.Dostawca = h.CustomerName;  // RealizacjaService porównuje po CustomerID lub CustomerName

            if (cbOperator.SelectedItem is OperatorComboItem o && !string.IsNullOrEmpty(o.OperatorID))
                f.OperatorID = o.OperatorID;

            if (cbKlasa.SelectedItem is ComboBoxItem ck && ck.Tag is string klasaStr
                && int.TryParse(klasaStr, out int klasa))
                f.KlasaKurczaka = klasa;

            if (cbTygodnie.SelectedItem is ComboBoxItem ctg && ctg.Tag is string tygStr
                && int.TryParse(tygStr, out int tyg))
                f.LiczbaTygodniPrognozy = tyg;

            return f;
        }

        // ═══ Eventy ═══

        private void BtnPresety_Click(object sender, RoutedEventArgs e)
        {
            if (btnPresety.ContextMenu == null) return;
            btnPresety.ContextMenu.PlacementTarget = btnPresety;
            btnPresety.ContextMenu.IsOpen = true;
        }

        private void Preset_Click(object sender, RoutedEventArgs e)
        {
            string? tag = sender switch
            {
                Button b => b.Tag as string,
                MenuItem m => m.Tag as string,
                _ => null
            };
            if (string.IsNullOrEmpty(tag)) return;

            DateTime today = DateTime.Today;
            switch (tag)
            {
                case "dzis":
                    dpDataOd.SelectedDate = today;
                    dpDataDo.SelectedDate = today;
                    break;
                case "wczoraj":
                    dpDataOd.SelectedDate = today.AddDays(-1);
                    dpDataDo.SelectedDate = today.AddDays(-1);
                    break;
                case "7d":
                    dpDataOd.SelectedDate = today.AddDays(-7);
                    dpDataDo.SelectedDate = today;
                    break;
                case "30d":
                    dpDataOd.SelectedDate = today.AddDays(-30);
                    dpDataDo.SelectedDate = today;
                    break;
                case "tydzien":
                    var poniedzialek = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
                    dpDataOd.SelectedDate = poniedzialek;
                    dpDataDo.SelectedDate = poniedzialek.AddDays(6);
                    break;
                case "miesiac":
                    dpDataOd.SelectedDate = new DateTime(today.Year, today.Month, 1);
                    dpDataDo.SelectedDate = new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);
                    break;
                case "poprzMiesiac":
                    var ppm = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
                    dpDataOd.SelectedDate = ppm;
                    dpDataDo.SelectedDate = new DateTime(ppm.Year, ppm.Month, 1).AddMonths(1).AddDays(-1);
                    break;
                case "8tyg":
                    dpDataOd.SelectedDate = today.AddDays(-7 * AnalitykaConfig.PrognozaTygodni);
                    dpDataDo.SelectedDate = today;
                    break;
            }
            BtnZastosuj_Click(sender, e);
        }

        private void DpDataOd_SelectedDateChanged(object sender, SelectionChangedEventArgs e) { /* wymaga klika Zastosuj */ }
        private void DpDataDo_SelectedDateChanged(object sender, SelectionChangedEventArgs e) { /* wymaga klika Zastosuj */ }
        private void Filtr_Changed(object sender, SelectionChangedEventArgs e) { if (_ladujKombo) return; /* wymaga klika Zastosuj */ }

        private void BtnZastosuj_Click(object sender, RoutedEventArgs e)
            => FiltryZastosowane?.Invoke(this, ZbierzFiltry());

        private void BtnLive_Click(object sender, RoutedEventArgs e)
            => LiveKlik?.Invoke(this, EventArgs.Empty);

        private void BtnEksport_Click(object sender, RoutedEventArgs e)
            => EksportKlik?.Invoke(this, EventArgs.Empty);

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
            => ZamknijKlik?.Invoke(this, EventArgs.Empty);

        private void BtnWiecej_Click(object sender, RoutedEventArgs e)
        {
            bool teraz = panelZaawansowany.Visibility == Visibility.Visible;
            panelZaawansowany.Visibility = teraz ? Visibility.Collapsed : Visibility.Visible;
            btnWiecej.Content = teraz ? "▼ Więcej filtrów" : "▲ Mniej";
        }

        public void UstawWygladLive(bool aktywne)
        {
            if (aktywne)
            {
                btnLive.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xDC, 0x26, 0x26));
                btnLive.Foreground = System.Windows.Media.Brushes.White;
            }
            else
            {
                btnLive.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x37, 0x41, 0x51));
                btnLive.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8));
            }
        }

        private void BtnWyczysc_Click(object sender, RoutedEventArgs e)
        {
            _ladujKombo = true;
            try
            {
                dpDataOd.SelectedDate = DateTime.Today.AddDays(-AnalitykaConfig.DomyslnyZakresDni);
                dpDataDo.SelectedDate = DateTime.Today;
                if (cbTowar.Items.Count > 0) cbTowar.SelectedIndex = 0;
                if (cbHodowca.Items.Count > 0) cbHodowca.SelectedIndex = 0;
                if (cbOperator.Items.Count > 0) cbOperator.SelectedIndex = 0;
                cbKlasa.SelectedIndex = 0;
                cbTygodnie.SelectedIndex = 1;  // 8 tygodni
            }
            finally
            {
                _ladujKombo = false;
            }
            BtnZastosuj_Click(sender, e);
        }
    }
}
