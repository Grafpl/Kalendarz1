using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.Sprawozdania.Services;

namespace Kalendarz1.Sprawozdania.Views
{
    public partial class ZapasyDialog : Window
    {
        private readonly P02ZapasService _svc = new();
        private readonly int _rok;
        private readonly int _miesiac;

        public Dictionary<string, decimal>? Zaakceptowane { get; private set; }

        private static readonly CultureInfo Pl = new("pl-PL");

        public ObservableCollection<ZapasyVm> Wszystkie { get; } = new();
        public ICollectionView Widok { get; private set; }

        private string _aktywnyFiltrPkwiu = "";   // "" = wszystkie

        public ZapasyDialog(int rok, int miesiac)
        {
            InitializeComponent();
            _rok = rok;
            _miesiac = miesiac;

            DateTime ostatni = new DateTime(rok, miesiac, 1).AddMonths(1).AddDays(-1);
            DateTime dzis = DateTime.Today;
            bool histRequired = ostatni < dzis;

            lblOkres.Text = histRequired
                ? $"Okres: {NazwaMc(miesiac)} {rok}  ·  Stan na: {ostatni:dd.MM.yyyy}  ·  Formula: SM + zmiany od {ostatni.AddDays(1):dd.MM} do {dzis:dd.MM.yyyy}"
                : $"Okres: {NazwaMc(miesiac)} {rok}  ·  Stan z SM (bieżący — bez przeliczania historycznego)";

            colStanHist.Header = histRequired
                ? $"Stan na {ostatni:dd.MM.yyyy} (kg)"
                : "Stan bieżący (kg)";

            Widok = CollectionViewSource.GetDefaultView(Wszystkie);
            Widok.Filter = FiltrPredicate;
            dg.ItemsSource = Widok;

            Loaded += async (s, e) => await PrzelaczMetodeAsync();
        }

        private P02ZapasService.MetodaLiczenia AktualnaMetoda()
        {
            if (cbMetoda.SelectedItem is ComboBoxItem cbi && int.TryParse(cbi.Tag?.ToString(), out int i))
                return (P02ZapasService.MetodaLiczenia)i;
            return P02ZapasService.MetodaLiczenia.SM_MroznDyst;
        }

        private async void Cb_Metoda_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            await PrzelaczMetodeAsync();
        }

        private async Task PrzelaczMetodeAsync()
        {
            loadingMini.Visibility = Visibility.Visible;
            try
            {
                var metoda = AktualnaMetoda();
                var lista = await _svc.PobierzPelneZapasyAsync(_rok, _miesiac, metoda);

                Wszystkie.Clear();
                foreach (var r in lista)
                    Wszystkie.Add(new ZapasyVm(r));

                Widok.Refresh();
                AktualizujKafelki();
                AktualizujFooter();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd pobierania zapasów:\n" + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                loadingMini.Visibility = Visibility.Collapsed;
            }
        }

        // ════════════════════════════════════════════════════════════════
        // KAFELKI per PKWiU — sumy do agregacji
        // ════════════════════════════════════════════════════════════════
        private void AktualizujKafelki()
        {
            decimal all = Wszystkie.Where(x => x.StanHistoryczny > 0).Sum(x => x.StanHistoryczny) / 1000m;
            decimal p1010 = Wszystkie.Where(x => x.Pkwiu == "10.12.10-10" && x.StanHistoryczny > 0).Sum(x => x.StanHistoryczny) / 1000m;
            decimal p1050 = Wszystkie.Where(x => x.Pkwiu == "10.12.10-50" && x.StanHistoryczny > 0).Sum(x => x.StanHistoryczny) / 1000m;
            decimal p2013 = Wszystkie.Where(x => x.Pkwiu == "10.12.20-13" && x.StanHistoryczny > 0).Sum(x => x.StanHistoryczny) / 1000m;
            decimal p2053 = Wszystkie.Where(x => x.Pkwiu == "10.12.20-53" && x.StanHistoryczny > 0).Sum(x => x.StanHistoryczny) / 1000m;

            tileAll.Text = $"{all.ToString("N1", Pl)} t";
            tile1010.Text = $"{p1010.ToString("N1", Pl)} t";
            tile1050.Text = $"{p1050.ToString("N1", Pl)} t";
            tile2013.Text = $"{p2013.ToString("N1", Pl)} t";
            tile2053.Text = $"{p2053.ToString("N1", Pl)} t";

            lblTotalAll.Text = $"·  Razem dodatnich: {Wszystkie.Count(x => x.StanHistoryczny > 0)} towarów";
        }

        // ════════════════════════════════════════════════════════════════
        // KLIK KAFELKA → filtr per PKWiU
        // ════════════════════════════════════════════════════════════════
        private void PkwiuTile_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Tag is string tag)
            {
                _aktywnyFiltrPkwiu = tag == "ALL" ? "" : tag;
                lblAktywnyFiltr.Text = tag == "ALL"
                    ? "🔍 Filtr: Wszystkie"
                    : $"🔍 Filtr: {tag}";
                Widok.Refresh();
                AktualizujFooter();
            }
        }

        private void BtnClearFilter_Click(object sender, RoutedEventArgs e)
        {
            _aktywnyFiltrPkwiu = "";
            txtSearch.Clear();
            lblAktywnyFiltr.Text = "🔍 Filtr: Wszystkie";
            Widok.Refresh();
            AktualizujFooter();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            Widok.Refresh();
            AktualizujFooter();
        }

        private bool FiltrPredicate(object obj)
        {
            if (obj is not ZapasyVm v) return false;

            // PKWiU filter
            if (!string.IsNullOrEmpty(_aktywnyFiltrPkwiu) && !string.Equals(v.Pkwiu, _aktywnyFiltrPkwiu, StringComparison.OrdinalIgnoreCase))
                return false;

            // Search text
            string q = (txtSearch?.Text ?? "").Trim();
            if (q.Length > 0)
            {
                if ((v.Kod?.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                    && (v.Nazwa?.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                    && (v.MagazynNazwa?.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0))
                    return false;
            }

            // Ukrywamy zerowe stany (mniej szumu)
            if (v.StanHistoryczny <= 0 && v.StanDzis <= 0) return false;

            return true;
        }

        private void AktualizujFooter()
        {
            int widoczne = Widok.Cast<object>().Count();
            decimal sumaKg = Widok.Cast<ZapasyVm>().Where(x => x.StanHistoryczny > 0).Sum(x => x.StanHistoryczny);
            lblFooterStats.Text = $"Widoczne: {widoczne} towarów  ·  Suma stanu: {sumaKg.ToString("N0", Pl)} kg ({(sumaKg / 1000m).ToString("N1", Pl)} t)";
        }

        // ════════════════════════════════════════════════════════════════
        // KOPIUJ / ZASTOSUJ / ANULUJ
        // ════════════════════════════════════════════════════════════════
        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("PKWiU\tKod\tNazwa\tMagazyn\tKatalog\tStanDzis\tZmianaPo\tStanKwiec");
            foreach (var v in Widok.Cast<ZapasyVm>())
                sb.AppendLine($"{v.Pkwiu}\t{v.Kod}\t{v.Nazwa}\t{v.MagazynNazwa}\t{v.Katalog}\t{v.StanDzis:F0}\t{v.ZmianaPoOkresie:F0}\t{v.StanHistoryczny:F0}");

            try
            {
                Clipboard.SetText(sb.ToString());
                lblFooterStats.Text = $"✓ Skopiowano {Widok.Cast<object>().Count()} wierszy do schowka (TSV)";
            }
            catch (Exception ex) { MessageBox.Show("Błąd kopiowania: " + ex.Message); }
        }

        private async void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var metoda = AktualnaMetoda();
                Zaakceptowane = await _svc.PobierzZapasyNaKoniecOkresuAsync(_rok, _miesiac, metoda);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd zastosowania:\n" + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

        private static string NazwaMc(int m)
        {
            string[] n = { "", "styczeń","luty","marzec","kwiecień","maj","czerwiec",
                "lipiec","sierpień","wrzesień","październik","listopad","grudzień" };
            return (m >= 1 && m <= 12) ? n[m] : $"mc{m}";
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // ViewModel ze szczegółami stanu — wszystko co user potrzebuje zweryfikować
    // ════════════════════════════════════════════════════════════════════
    public class ZapasyVm
    {
        public string Pkwiu { get; }
        public string Kod { get; }
        public string Nazwa { get; }
        public int? Katalog { get; }
        public int Magazyn { get; }
        public string MagazynNazwa { get; }
        public decimal StanDzis { get; }
        public decimal ZmianaPoOkresie { get; }
        public decimal StanHistoryczny { get; }
        public Brush KolorPkwiu { get; }

        public ZapasyVm(P02ZapasService.ZapasyRichRow r)
        {
            Pkwiu = r.PkwiuKlasyfikacja;
            Kod = r.Kod;
            Nazwa = r.Nazwa;
            Katalog = r.Katalog;
            Magazyn = r.Magazyn;
            StanDzis = r.StanDzis;
            ZmianaPoOkresie = r.ZmianaPoOkresie;
            StanHistoryczny = r.StanHistoryczny;

            MagazynNazwa = Magazyn switch
            {
                65555 => "M.UBOJ",
                65554 => "M.PROD",
                65552 => "M.MROŹN",
                65556 => "M.DYST",
                65562 => "M.MASAR",
                _ => Magazyn.ToString()
            };

            string hex = Pkwiu switch
            {
                "10.12.10-10" => "#2563EB",
                "10.12.10-50" => "#16A34A",
                "10.12.20-13" => "#06B6D4",
                "10.12.20-53" => "#7C3AED",
                _ => "#9CA3AF"
            };
            KolorPkwiu = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
    }
}
