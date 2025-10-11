// Plik: WidokZamowienia.xaml.cs
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Kalendarz1
{
    public partial class WidokZamowienia : Window, INotifyPropertyChanged
    {
        #region Właściwości dla Bindowania Danych

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private decimal _sumaPalet;
        public decimal SumaPalet { get => _sumaPalet; set { _sumaPalet = value; OnPropertyChanged(); } }

        private decimal _sumaPojemnikow;
        public decimal SumaPojemnikow { get => _sumaPojemnikow; set { _sumaPojemnikow = value; OnPropertyChanged(); } }

        private decimal _sumaKg;
        public decimal SumaKg { get => _sumaKg; set { _sumaKg = value; OnPropertyChanged(); } }

        public ObservableCollection<ZamowieniePozycja> PozycjeZamowienia { get; set; }
        private readonly List<KontrahentInfo> _kontrahenci = new();
        private Collection<ZamowieniePozycja> _wszystkieProdukty = new Collection<ZamowieniePozycja>();

        #endregion

        #region Stałe i Pola Wewnętrzne

        public string UserID { get; set; } = "DefaultUser";
        private int? _idZamowieniaDoEdycji;
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True;Connect Timeout=5";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True;Connect Timeout=5";

        private const decimal POJEMNIKOW_NA_PALECIE = 36m;
        private const decimal POJEMNIKOW_NA_PALECIE_E2 = 40m;
        private const decimal KG_NA_POJEMNIKU = 15m;
        private const decimal KG_NA_POJEMNIKU_SPECJALNY = 10m;
        private string? _selectedKlientId;
        private bool _isDataLoaded = false;

        #endregion

        // Pusty konstruktor wymagany przez WPF do uruchomienia
        public WidokZamowienia()
        {
            InitializeComponent();
            this.DataContext = this;
            PozycjeZamowienia = new ObservableCollection<ZamowieniePozycja>();
            _wszystkieProdukty = new Collection<ZamowieniePozycja>();
            dataGridViewZamowienie.ItemsSource = PozycjeZamowienia;
            this.Loaded += WidokZamowienia_Loaded;
        }

        // NOWY KONSTRUKTOR - potrzebny dla starego kodu
        public WidokZamowienia(string userId, int? idZamowienia) : this()
        {
            UserID = userId;
            _idZamowieniaDoEdycji = idZamowienia;
        }


        private async void WidokZamowienia_Loaded(object sender, RoutedEventArgs e)
        {
            InitDefaults();
            try
            {
                btnZapisz.IsEnabled = false;
                await LoadInitialDataInBackground();
                btnZapisz.IsEnabled = true;

                // Włączanie dynamicznego sortowania
                ICollectionView view = CollectionViewSource.GetDefaultView(PozycjeZamowienia);
                if (view != null && view.SortDescriptions.Count == 0)
                {
                    // Sortuj malejąco po HasValue (wiersze z wartością=true będą pierwsze)
                    view.SortDescriptions.Add(new SortDescription("HasValue", ListSortDirection.Descending));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się połączyć z bazą danych lub wystąpił błąd podczas ładowania danych: {ex.Message}", "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadInitialDataInBackground()
        {
            await LoadTowaryAsync();
            await LoadKontrahenciAsync();

            var hands = _kontrahenci.Select(k => k.Handlowiec).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList();
            hands.Insert(0, "— Wszyscy —");
            cbHandlowiecFilter.ItemsSource = hands;
            cbHandlowiecFilter.SelectedIndex = 0;

            _isDataLoaded = true;
            FilterProductsView();
        }

        private async Task LoadTowaryAsync()
        {
            var allProducts = new List<ZamowieniePozycja>();
            var excludedProducts = new HashSet<string> { "KURCZAK B", "FILET C" };
            var priorityOrder = new Dictionary<string, int> {
                { "KURCZAK A", 1 }, { "FILET A", 2 }, { "ĆWIARTKA", 3 }, { "SKRZYDŁO I", 4 },
                { "NOGA", 5 }, { "PAŁKA", 6 }, { "KORPUS", 7 }, { "POLĘDWICZKI", 8 },
                { "SERCE", 9 }, { "WĄTROBA", 10 }, { "ŻOŁĄDKI", 11 }
            };

            using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();
            var katalogi = new[] { "67095", "67153" };

            foreach (var katalog in katalogi)
            {
                using var cmd = new SqlCommand("SELECT Id, Kod FROM [HANDEL].[HM].[TW] WHERE katalog = @katalog", cn);
                cmd.Parameters.AddWithValue("@katalog", katalog);

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    string kod = rd.GetString(1);
                    if (excludedProducts.Any(ex => kod.ToUpper().Contains(ex))) continue;

                    var pozycja = new ZamowieniePozycja(this)
                    {
                        Id = rd.GetInt32(0),
                        Kod = kod,
                        Katalog = katalog
                    };
                    allProducts.Add(pozycja);
                }
            }

            var sortedProducts = allProducts
                .OrderBy(p => priorityOrder.TryGetValue(p.Kod.ToUpper(), out int priority) ? priority : int.MaxValue)
                .ThenBy(p => p.Kod);

            _wszystkieProdukty.Clear();
            foreach (var p in sortedProducts)
            {
                _wszystkieProdukty.Add(p);
            }
        }

        private async Task LoadKontrahenciAsync()
        {
            const string sql = @"
                SELECT c.Id, c.Shortcut AS Nazwa, c.NIP,
                    poa.Postcode AS KodPocztowy, poa.Street AS Miejscowosc, 
                    wym.CDim_Handlowiec_Val AS Handlowiec
                FROM [HANDEL].[SSCommon].[STContractors] c
                LEFT JOIN [HANDEL].[SSCommon].[STPostOfficeAddresses] poa ON poa.ContactGuid = c.ContactGuid AND poa.AddressName = N'adres domyślny'
                LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] wym ON c.Id = wym.ElementId
                ORDER BY c.Shortcut;";

            _kontrahenci.Clear();
            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            await using var rd = await cmd.ExecuteReaderAsync();

            while (await rd.ReadAsync())
            {
                _kontrahenci.Add(new KontrahentInfo
                {
                    Id = rd["Id"]?.ToString() ?? "",
                    Nazwa = rd["Nazwa"]?.ToString() ?? "",
                    NIP = rd["NIP"]?.ToString() ?? "",
                    KodPocztowy = rd["KodPocztowy"]?.ToString() ?? "",
                    Miejscowosc = rd["Miejscowosc"]?.ToString() ?? "",
                    Handlowiec = rd["Handlowiec"]?.ToString() ?? ""
                });
            }
        }

        private void InitDefaults()
        {
            datePickerProdukcji.SelectedDate = DateTime.Today;
            var dzis = DateTime.Now.Date;
            datePickerSprzedaz.SelectedDate = (dzis.DayOfWeek == DayOfWeek.Friday) ? dzis.AddDays(3) : dzis.AddDays(1);
            timePickerGodzinaPrzyjazdu.Text = "08:00";
            RecalcSum();
        }

        public void RecalcSum()
        {
            SumaPalet = PozycjeZamowienia.Sum(p => p.Palety);
            SumaPojemnikow = PozycjeZamowienia.Sum(p => p.Pojemniki);
            SumaKg = PozycjeZamowienia.Sum(p => p.Ilosc);
        }

        public void PrzeliczPozycje(ZamowieniePozycja poz, string zrodloZmiany)
        {
            bool useE2 = poz.E2;
            decimal pojemnikNaPalete = useE2 ? POJEMNIKOW_NA_PALECIE_E2 : POJEMNIKOW_NA_PALECIE;
            decimal kgNaPojemnik = IsSpecialProduct(poz.Kod) ? KG_NA_POJEMNIKU_SPECJALNY : KG_NA_POJEMNIKU;
            decimal kgNaPalete = pojemnikNaPalete * kgNaPojemnik;

            switch (zrodloZmiany)
            {
                case "Ilosc":
                    poz.Pojemniki = (poz.Ilosc > 0 && kgNaPojemnik > 0) ? Math.Round(poz.Ilosc / kgNaPojemnik, 0) : 0m;
                    poz.Palety = (poz.Ilosc > 0 && kgNaPalete > 0) ? poz.Ilosc / kgNaPalete : 0m;
                    break;
                case "Pojemniki":
                    poz.Ilosc = poz.Pojemniki * kgNaPojemnik;
                    poz.Palety = (poz.Pojemniki > 0 && pojemnikNaPalete > 0) ? poz.Pojemniki / pojemnikNaPalete : 0m;
                    break;
                case "Palety":
                    poz.Pojemniki = poz.Palety * pojemnikNaPalete;
                    poz.Ilosc = poz.Palety * kgNaPalete;
                    break;
                case "E2":
                    if (poz.Palety > 0) PrzeliczPozycje(poz, "Palety");
                    else if (poz.Pojemniki > 0) PrzeliczPozycje(poz, "Pojemniki");
                    else if (poz.Ilosc > 0) PrzeliczPozycje(poz, "Ilosc");
                    break;
            }
        }

        private bool IsSpecialProduct(string kod)
        {
            if (string.IsNullOrEmpty(kod)) return false;
            var kodUpper = kod.ToUpper();
            return kodUpper.Contains("WĄTROBA") || kodUpper.Contains("ŻOŁĄDKI") || kodUpper.Contains("SERCE");
        }

        private void FilterProductsView()
        {
            if (!_isDataLoaded) return;

            string aktywnyKatalog = (rbSwiezy.IsChecked == true) ? "67095" : "67153";

            PozycjeZamowienia.Clear();
            var filtered = _wszystkieProdukty.Where(p => p.Katalog == aktywnyKatalog);
            foreach (var item in filtered)
            {
                PozycjeZamowienia.Add(item);
            }
        }

        #region UI Event Handlers

        private void DragWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void TxtSzukajOdbiorcy_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = txtSzukajOdbiorcy.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(query)) { PopupWyniki.IsOpen = false; return; }

            var wyniki = _kontrahenci
                .Where(k => k.Nazwa.ToLower().Contains(query) || k.Miejscowosc.ToLower().Contains(query) || k.NIP.Contains(query))
                .Take(10).ToList();

            listaWynikowOdbiorcy.ItemsSource = wyniki;
            listaWynikowOdbiorcy.DisplayMemberPath = "Nazwa";
            PopupWyniki.IsOpen = wyniki.Any();
        }

        private void WybierzOdbiorceZListy()
        {
            if (listaWynikowOdbiorcy.SelectedItem is KontrahentInfo wybrany)
            {
                _selectedKlientId = wybrany.Id;
                txtSzukajOdbiorcy.Text = wybrany.Nazwa;
                PopupWyniki.IsOpen = false;
            }
        }

        private void CbHandlowiecFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbHandlowiecFilter.SelectedItem is string handlowiec && handlowiec != "— Wszyscy —")
            {
                var odbiorcy = _kontrahenci.Where(k => k.Handlowiec == handlowiec).Select(k => k.Nazwa).Take(12);
                gridOstatniOdbiorcy.ItemsSource = odbiorcy;
                lblOstatniOdbiorcy.Text = $"Ostatni odbiorcy ({handlowiec})";
            }
            else
            {
                gridOstatniOdbiorcy.ItemsSource = null;
                lblOstatniOdbiorcy.Text = "Wybierz handlowca";
            }
        }

        private void RbTypProduktu_CheckedChanged(object sender, RoutedEventArgs e) => FilterProductsView();
        private void DataGridViewZamowienie_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e) => RecalcSum();
        private void TxtSzukajOdbiorcy_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) WybierzOdbiorceZListy(); }
        private void TxtSzukajOdbiorcy_LostFocus(object sender, RoutedEventArgs e) { if (!PopupWyniki.IsMouseOver && !listaWynikowOdbiorcy.IsMouseOver) PopupWyniki.IsOpen = false; }
        private void ListaWynikowOdbiorcy_MouseUp(object sender, MouseButtonEventArgs e) => WybierzOdbiorceZListy();
        private void ListaWynikowOdbiorcy_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) WybierzOdbiorceZListy(); }
        private void OdbiorcaButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Content is string nazwa)
            {
                var odbiorca = _kontrahenci.FirstOrDefault(k => k.Nazwa == nazwa);
                if (odbiorca != null)
                {
                    txtSzukajOdbiorcy.Text = nazwa;
                }
            }
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        #endregion
    }
}