using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Kalendarz1.OfertaCenowa
{
    /// <summary>
    /// Dashboard ze statystykami ofert handlowych
    /// </summary>
    public partial class OfertyDashboardWindow : Window
    {
        private readonly OfertaRepository _repository = new();
        private DateTime _dataOd;
        private DateTime _dataDo;

        public OfertyDashboardWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            // Domyślnie: ten miesiąc
            UstawOkres("miesiac");

            Loaded += OfertyDashboardWindow_Loaded;
        }

        private async void OfertyDashboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDaneAsync();
        }

        #region Ustawianie okresu

        private void UstawOkres(string okres)
        {
            var dzisiaj = DateTime.Today;

            switch (okres)
            {
                case "tydzien":
                    _dataOd = dzisiaj.AddDays(-7);
                    _dataDo = dzisiaj;
                    txtOkres.Text = $"Okres: Ostatnie 7 dni ({_dataOd:dd.MM} - {_dataDo:dd.MM.yyyy})";
                    break;

                case "30dni":
                    _dataOd = dzisiaj.AddDays(-30);
                    _dataDo = dzisiaj;
                    txtOkres.Text = $"Okres: Ostatnie 30 dni ({_dataOd:dd.MM} - {_dataDo:dd.MM.yyyy})";
                    break;

                case "miesiac":
                    _dataOd = new DateTime(dzisiaj.Year, dzisiaj.Month, 1);
                    _dataDo = dzisiaj;
                    txtOkres.Text = $"Okres: {dzisiaj:MMMM yyyy}";
                    break;

                case "kwartal":
                    int kwartal = (dzisiaj.Month - 1) / 3;
                    _dataOd = new DateTime(dzisiaj.Year, kwartal * 3 + 1, 1);
                    _dataDo = dzisiaj;
                    txtOkres.Text = $"Okres: Q{kwartal + 1} {dzisiaj.Year}";
                    break;

                case "rok":
                    _dataOd = new DateTime(dzisiaj.Year, 1, 1);
                    _dataDo = dzisiaj;
                    txtOkres.Text = $"Okres: Rok {dzisiaj.Year}";
                    break;

                case "wszystko":
                default:
                    _dataOd = new DateTime(2020, 1, 1);
                    _dataDo = dzisiaj;
                    txtOkres.Text = "Okres: Wszystkie oferty";
                    break;
            }
        }

        #endregion

        #region Ładowanie danych

        private async Task LoadDaneAsync()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                // Pobierz wszystkie oferty w okresie
                var oferty = await _repository.PobierzListeOfertAsync(
                    dataOd: _dataOd,
                    dataDo: _dataDo.AddDays(1).AddSeconds(-1),
                    limit: 10000
                );

                // Oblicz KPI
                ObliczKPI(oferty);

                // Statusy
                ObliczStatusy(oferty);

                // Top handlowcy
                ObliczTopHandlowcy(oferty);

                // Top klienci
                await LoadTopKlienciAsync();

                // Ostatnie oferty
                LoadOstatnieOferty(oferty);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void ObliczKPI(List<OfertaListaItem> oferty)
        {
            int liczbaOfert = oferty.Count;
            decimal wartoscOfert = oferty.Sum(o => o.WartoscNetto);
            decimal sredniaWartosc = liczbaOfert > 0 ? wartoscOfert / liczbaOfert : 0;

            int zaakceptowane = oferty.Count(o => o.Status == "Zaakceptowana");
            int wyslaneLubZamkniete = oferty.Count(o => o.Status == "Wyslana" || o.Status == "Zaakceptowana" || o.Status == "Odrzucona");
            decimal konwersja = wyslaneLubZamkniete > 0 ? (decimal)zaakceptowane / wyslaneLubZamkniete * 100 : 0;

            decimal wartoscZaakceptowanych = oferty.Where(o => o.Status == "Zaakceptowana").Sum(o => o.WartoscNetto);

            // Wyświetl KPI
            txtLiczbaOfert.Text = liczbaOfert.ToString("N0");
            txtWartoscOfert.Text = FormatujWartosc(wartoscOfert);
            txtSredniaWartosc.Text = FormatujWartosc(sredniaWartosc);
            txtKonwersja.Text = $"{konwersja:N1}%";
            txtZaakceptowane.Text = zaakceptowane.ToString();
            txtZaakceptowaneWartosc.Text = FormatujWartosc(wartoscZaakceptowanych);

            // Porównanie z poprzednim okresem (uproszczone)
            txtLiczbaOfertZmiana.Text = $"w wybranym okresie";
            txtWartoscOfertZmiana.Text = $"{oferty.Count(o => o.Status == "Nowa")} oczekujących";
        }

        private void ObliczStatusy(List<OfertaListaItem> oferty)
        {
            int wszystkie = oferty.Count;
            if (wszystkie == 0) wszystkie = 1; // Unikaj dzielenia przez 0

            int nowe = oferty.Count(o => o.Status == "Nowa");
            int wyslane = oferty.Count(o => o.Status == "Wyslana");
            int zaakceptowane = oferty.Count(o => o.Status == "Zaakceptowana");
            int odrzucone = oferty.Count(o => o.Status == "Odrzucona");
            int anulowane = oferty.Count(o => o.Status == "Anulowana");

            // Ustaw wartości
            txtNowe.Text = nowe.ToString();
            txtWyslane.Text = wyslane.ToString();
            txtZaakceptowaneStatus.Text = zaakceptowane.ToString();
            txtOdrzucone.Text = odrzucone.ToString();
            txtAnulowane.Text = anulowane.ToString();

            // Ustaw paski postępu
            pbNowe.Value = (double)nowe / wszystkie * 100;
            pbWyslane.Value = (double)wyslane / wszystkie * 100;
            pbZaakceptowane.Value = (double)zaakceptowane / wszystkie * 100;
            pbOdrzucone.Value = (double)odrzucone / wszystkie * 100;
            pbAnulowane.Value = (double)anulowane / wszystkie * 100;
        }

        private void ObliczTopHandlowcy(List<OfertaListaItem> oferty)
        {
            var topHandlowcy = oferty
                .GroupBy(o => new { o.HandlowiecID, o.HandlowiecNazwa })
                .Select(g => new TopHandlowiecViewModel
                {
                    ID = g.Key.HandlowiecID,
                    Nazwa = g.Key.HandlowiecNazwa,
                    LiczbaOfert = g.Count(),
                    Wartosc = g.Sum(o => o.WartoscNetto),
                    Zaakceptowane = g.Count(o => o.Status == "Zaakceptowana"),
                    Wyslane = g.Count(o => o.Status == "Wyslana" || o.Status == "Zaakceptowana" || o.Status == "Odrzucona")
                })
                .OrderByDescending(h => h.Wartosc)
                .Take(10)
                .ToList();

            // Dodaj pozycję
            for (int i = 0; i < topHandlowcy.Count; i++)
            {
                topHandlowcy[i].Pozycja = i + 1;
            }

            dgTopHandlowcy.ItemsSource = topHandlowcy;
        }

        private async Task LoadTopKlienciAsync()
        {
            try
            {
                var topKlienci = await _repository.PobierzTopKlientowAsync(10);

                var viewModels = topKlienci.Select((k, i) => new TopKlientViewModel
                {
                    Pozycja = i + 1,
                    KlientNazwa = k.KlientNazwa,
                    LiczbaOfert = k.LiczbaOfert,
                    SumaWartosci = k.SumaWartosci,
                    Zaakceptowane = k.Zaakceptowane,
                    OstatniaOferta = k.OstatniaOferta
                }).ToList();

                icTopKlienci.ItemsSource = viewModels;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd ładowania top klientów: {ex.Message}");
            }
        }

        private void LoadOstatnieOferty(List<OfertaListaItem> oferty)
        {
            var ostatnie = oferty
                .OrderByDescending(o => o.DataWystawienia)
                .Take(10)
                .ToList();

            icOstatnieOferty.ItemsSource = ostatnie;
        }

        #endregion

        #region Pomocnicze

        private string FormatujWartosc(decimal wartosc)
        {
            if (wartosc >= 1000000)
                return $"{wartosc / 1000000:N2} mln zł";
            else if (wartosc >= 1000)
                return $"{wartosc / 1000:N1} tys. zł";
            else
                return $"{wartosc:N2} zł";
        }

        #endregion

        #region Event handlery

        private async void CboOkres_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            var selectedItem = cboOkres.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                string okres = selectedItem.Tag?.ToString() ?? "miesiac";
                UstawOkres(okres);
                await LoadDaneAsync();
            }
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            await LoadDaneAsync();
        }

        private void BtnZobaczWszystkie_Click(object sender, RoutedEventArgs e)
        {
            var okno = new OfertyListaWindow();
            okno.ShowDialog();
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion
    }

    #region ViewModels

    /// <summary>
    /// ViewModel dla top handlowca
    /// </summary>
    public class TopHandlowiecViewModel
    {
        public int Pozycja { get; set; }
        public string ID { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public int LiczbaOfert { get; set; }
        public decimal Wartosc { get; set; }
        public int Zaakceptowane { get; set; }
        public int Wyslane { get; set; }

        public string WartoscFormatowana => $"{Wartosc:N0} zł";
        public decimal Konwersja => Wyslane > 0 ? (decimal)Zaakceptowane / Wyslane * 100 : 0;
        public string KonwersjaFormatowana => $"{Konwersja:N0}%";
    }

    /// <summary>
    /// ViewModel dla top klienta
    /// </summary>
    public class TopKlientViewModel
    {
        public int Pozycja { get; set; }
        public string KlientNazwa { get; set; } = "";
        public int LiczbaOfert { get; set; }
        public decimal SumaWartosci { get; set; }
        public int Zaakceptowane { get; set; }
        public DateTime OstatniaOferta { get; set; }

        public string SumaWartosciFormatowana => $"{SumaWartosci:N0} zł";
        public string OstatniaOfertaFormatowana => $"Ostatnia: {OstatniaOferta:dd.MM.yyyy}";
    }

    #endregion
}
