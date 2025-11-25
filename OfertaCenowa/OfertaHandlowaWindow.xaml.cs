using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Kalendarz1.OfertaCenowa
{
    // =====================================================
    // MODEL ODBIORCY
    // =====================================================
    public class OdbiorcaOferta : INotifyPropertyChanged
    {
        public string Id { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public string NIP { get; set; } = "";
        public string Adres { get; set; } = "";
        public string KodPocztowy { get; set; } = "";
        public string Miejscowosc { get; set; } = "";
        public string Telefon { get; set; } = "";
        public string Email { get; set; } = "";
        public string OsobaKontaktowa { get; set; } = "";
        public string Stanowisko { get; set; } = "";
        public string Wojewodztwo { get; set; } = "";
        public string Status { get; set; } = "";
        public string Zrodlo { get; set; } = "HANDEL";

        public string ZrodloSkrot => Zrodlo switch { "HANDEL" => "HANDEL", "CRM" => "CRM", "RECZNY" => "RĘCZNY", _ => Zrodlo };
        public SolidColorBrush ZrodloBadgeBackground => Zrodlo switch { "HANDEL" => new SolidColorBrush(Color.FromRgb(235, 245, 255)), "CRM" => new SolidColorBrush(Color.FromRgb(255, 247, 237)), "RECZNY" => new SolidColorBrush(Color.FromRgb(243, 232, 255)), _ => new SolidColorBrush(Color.FromRgb(243, 244, 246)) };
        public SolidColorBrush ZrodloBadgeForeground => Zrodlo switch { "HANDEL" => new SolidColorBrush(Color.FromRgb(59, 130, 246)), "CRM" => new SolidColorBrush(Color.FromRgb(249, 115, 22)), "RECZNY" => new SolidColorBrush(Color.FromRgb(147, 51, 234)), _ => new SolidColorBrush(Color.FromRgb(107, 114, 128)) };
        public string AdresPelny => string.IsNullOrEmpty(Adres) ? $"{KodPocztowy} {Miejscowosc}".Trim() : $"{Adres}, {KodPocztowy} {Miejscowosc}".Trim().TrimEnd(',');

        public KlientOferta ToKlientOferta() => new KlientOferta { Id = Id, Nazwa = Nazwa, NIP = NIP, Adres = Adres, KodPocztowy = KodPocztowy, Miejscowosc = Miejscowosc, CzyReczny = Zrodlo == "RECZNY" };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // =====================================================
    // MODEL WIERSZA TOWARU - PROSTY I BEZAWARYJNY
    // =====================================================
    public class TowarWiersz : INotifyPropertyChanged
    {
        private int _lp;
        private int _towarId;
        private string _kod = "";
        private string _nazwa = "";
        private string _katalog = "";
        private decimal _ilosc;
        private decimal _cena;
        private string _opakowanie = "E2";

        public int Lp
        {
            get => _lp;
            set { _lp = value; OnPropertyChanged(); }
        }

        public int TowarId
        {
            get => _towarId;
            set
            {
                if (_towarId != value)
                {
                    _towarId = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CzyWypelniony));
                    OnPropertyChanged(nameof(UsunWidocznosc));
                    OnPropertyChanged(nameof(WyswietlanaNazwa));
                }
            }
        }

        public string Kod
        {
            get => _kod;
            set { _kod = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(WyswietlanaNazwa)); }
        }

        public string Nazwa
        {
            get => _nazwa;
            set { _nazwa = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(WyswietlanaNazwa)); }
        }

        public string Katalog
        {
            get => _katalog;
            set { _katalog = value ?? ""; OnPropertyChanged(); }
        }

        public decimal Ilosc
        {
            get => _ilosc;
            set
            {
                _ilosc = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IloscTekst));
                OnPropertyChanged(nameof(Wartosc));
                OnPropertyChanged(nameof(WartoscTekst));
            }
        }

        public decimal Cena
        {
            get => _cena;
            set
            {
                _cena = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CenaTekst));
                OnPropertyChanged(nameof(Wartosc));
                OnPropertyChanged(nameof(WartoscTekst));
            }
        }

        public string Opakowanie
        {
            get => _opakowanie;
            set { _opakowanie = value ?? "E2"; OnPropertyChanged(); }
        }

        // Właściwości tekstowe z automatyczną konwersją przecinka na kropkę
        public string IloscTekst
        {
            get => _ilosc == 0 ? "" : _ilosc.ToString("G29");
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    Ilosc = 0;
                else if (decimal.TryParse(value.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
                    Ilosc = result;
            }
        }

        public string CenaTekst
        {
            get => _cena == 0 ? "" : _cena.ToString("0.00");
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    Cena = 0;
                else if (decimal.TryParse(value.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
                    Cena = result;
            }
        }

        // Właściwości obliczane
        public decimal Wartosc => Ilosc * Cena;
        public string WartoscTekst => Wartosc == 0 ? "" : $"{Wartosc:N2} zł";
        public string WyswietlanaNazwa => string.IsNullOrEmpty(Kod) ? "" : $"{Kod} - {Nazwa}";
        public bool CzyWypelniony => TowarId > 0 && !string.IsNullOrEmpty(Kod);
        public Visibility UsunWidocznosc => CzyWypelniony ? Visibility.Visible : Visibility.Hidden;
        public string IloscStr => Ilosc == 0 ? "" : $"{Ilosc:N0} kg";
        public string CenaJednostkowaStr => Cena == 0 ? "" : $"{Cena:N2}";

        public TowarOferta ToTowarOferta() => new TowarOferta
        {
            Id = TowarId,
            Kod = Kod,
            Nazwa = Nazwa,
            Katalog = Katalog,
            Ilosc = Ilosc,
            CenaJednostkowa = Cena,
            Opakowanie = Opakowanie
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // =====================================================
    // GŁÓWNE OKNO OFERTY
    // =====================================================
    public partial class OfertaHandlowaWindow : Window, INotifyPropertyChanged
    {
        // === POŁĄCZENIA Z BAZĄ ===
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private readonly string _connLibraNet = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // === KOLEKCJE DANYCH ===
        public ObservableCollection<OdbiorcaOferta> WynikiWyszukiwania { get; set; } = new();
        public ObservableCollection<OdbiorcaOferta> WybraniOdbiorcy { get; set; } = new();
        public ObservableCollection<TowarOferta> DostepneTowary { get; set; } = new();
        public ObservableCollection<TowarOferta> FiltrowaneTowary { get; set; } = new();
        public ObservableCollection<TowarWiersz> TowaryWOfercie { get; set; } = new();
        public List<string> OpakowanieLista { get; set; } = new() { "E2", "Karton", "Poliblok" };

        // === POLA PRYWATNE ===
        private int _aktualnyKrok = 1;
        private string _aktywnyKatalog = "67095";
        private readonly SzablonyManager _szablonyManager = new();
        private string _nazwaOperatora = "";
        private string _userId = "";
        private DispatcherTimer? _searchTimer;

        public event PropertyChangedEventHandler? PropertyChanged;

        // === KONSTRUKTORY ===
        public OfertaHandlowaWindow() : this(null, "") { }
        public OfertaHandlowaWindow(KlientOferta? klient) : this(klient, "") { }

        public OfertaHandlowaWindow(KlientOferta? klient, string userId)
        {
            InitializeComponent();
            DataContext = this;
            _userId = userId;

            // Inicjalizacja wyszukiwania odbiorców
            lstWynikiWyszukiwania.ItemsSource = WynikiWyszukiwania;
            _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _searchTimer.Tick += (s, e) => { _searchTimer.Stop(); WyszukajOdbiorcow(); };

            // Inicjalizacja tabeli produktów
            TowaryWOfercie.CollectionChanged += TowaryWOfercie_CollectionChanged;

            // Ładowanie danych
            LoadDataAsync();
            _szablonyManager.UtworzSzablonyPrzykladowe();

            // Dodanie klienta jeśli podany
            if (klient != null)
            {
                var odbiorca = new OdbiorcaOferta
                {
                    Id = klient.Id,
                    Nazwa = klient.Nazwa,
                    NIP = klient.NIP,
                    Adres = klient.Adres,
                    KodPocztowy = klient.KodPocztowy,
                    Miejscowosc = klient.Miejscowosc,
                    Zrodlo = klient.CzyReczny ? "RECZNY" : "HANDEL"
                };
                WybraniOdbiorcy.Add(odbiorca);
                OdswiezListeWybranychOdbiorcow();
            }

            // Dodaj pierwszy pusty wiersz
            DodajNowyPustyWiersz();

            AktualizujWidokKroku();
        }

        // =====================================================
        // ŁADOWANIE DANYCH
        // =====================================================

        private async void LoadDataAsync()
        {
            try
            {
                await LoadOperatorAsync();
                await LoadTowaryAsync();
                FiltrujTowary();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd ładowania: {ex.Message}");
            }
        }

        private async Task LoadOperatorAsync()
        {
            if (string.IsNullOrEmpty(_userId))
            {
                _nazwaOperatora = "Nieznany";
                txtWystawiajacy.Text = _nazwaOperatora;
                return;
            }

            try
            {
                await using var cn = new SqlConnection(_connLibraNet);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand("SELECT Name FROM [LibraNet].[dbo].[operators] WHERE ID = @id", cn);
                cmd.Parameters.AddWithValue("@id", _userId);
                var result = await cmd.ExecuteScalarAsync();
                _nazwaOperatora = result?.ToString() ?? "Nieznany";
                txtWystawiajacy.Text = _nazwaOperatora;
            }
            catch
            {
                _nazwaOperatora = "Nieznany";
                txtWystawiajacy.Text = _nazwaOperatora;
            }
        }

        private async Task LoadTowaryAsync()
        {
            DostepneTowary.Clear();
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                var excludedProducts = new[] { "KURCZAK B", "FILET C" };
                await using var cmd = new SqlCommand("SELECT Id, Kod, Nazwa, katalog FROM [HANDEL].[HM].[TW] WHERE katalog IN ('67095', '67153') ORDER BY Kod ASC", cn);
                await using var rd = await cmd.ExecuteReaderAsync();

                while (await rd.ReadAsync())
                {
                    string kod = rd["Kod"]?.ToString() ?? "";
                    if (excludedProducts.Any(excluded => kod.ToUpper().Contains(excluded))) continue;

                    DostepneTowary.Add(new TowarOferta
                    {
                        Id = rd.GetInt32(0),
                        Kod = kod,
                        Nazwa = rd["Nazwa"]?.ToString() ?? "",
                        Katalog = rd["katalog"]?.ToString() ?? "",
                        Opakowanie = "E2"
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd ładowania towarów: {ex.Message}");
            }
        }

        private void FiltrujTowary()
        {
            FiltrowaneTowary.Clear();
            foreach (var towar in DostepneTowary.Where(t => t.Katalog == _aktywnyKatalog))
            {
                FiltrowaneTowary.Add(towar);
            }
        }

        // =====================================================
        // WYSZUKIWANIE ODBIORCÓW
        // =====================================================

        private void TxtSzukajOdbiorcy_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchTimer?.Stop();
            string fraza = txtSzukajOdbiorcy.Text.Trim();
            if (fraza.Length >= 3)
                _searchTimer?.Start();
            else
            {
                WynikiWyszukiwania.Clear();
                placeholderBrakWynikow.Visibility = Visibility.Visible;
            }
        }

        private void RbZrodlo_Changed(object sender, RoutedEventArgs e)
        {
            if (txtSzukajOdbiorcy != null && txtSzukajOdbiorcy.Text.Length >= 3)
                WyszukajOdbiorcow();
        }

        private async void WyszukajOdbiorcow()
        {
            string fraza = txtSzukajOdbiorcy.Text.Trim();
            if (string.IsNullOrEmpty(fraza) || fraza.Length < 3) return;

            WynikiWyszukiwania.Clear();
            placeholderBrakWynikow.Visibility = Visibility.Collapsed;

            try
            {
                bool szukajHandel = rbZrodloWszystkie.IsChecked == true || rbZrodloHandel.IsChecked == true;
                bool szukajCRM = rbZrodloWszystkie.IsChecked == true || rbZrodloCRM.IsChecked == true;

                if (szukajHandel) await SzukajWHandelAsync(fraza);
                if (szukajCRM) await SzukajWCRMAsync(fraza);

                if (WynikiWyszukiwania.Count == 0)
                    placeholderBrakWynikow.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd wyszukiwania: {ex.Message}");
            }
        }

        private async Task SzukajWHandelAsync(string fraza)
        {
            const string sql = @"SELECT TOP 30 c.Id, c.Name AS Nazwa, ISNULL(c.NIP, '') AS NIP, 
                ISNULL(poa.Street, '') AS Adres, ISNULL(poa.PostCode, '') AS KodPocztowy, ISNULL(poa.Place, '') AS Miejscowosc 
                FROM [HANDEL].[SSCommon].[STContractors] c 
                LEFT JOIN [HANDEL].[SSCommon].[STPostOfficeAddresses] poa ON poa.ContactGuid = c.ContactGuid AND poa.AddressName = N'adres domyślny' 
                WHERE c.Name LIKE @fraza OR c.NIP LIKE @fraza OR poa.Place LIKE @fraza OR poa.PostCode LIKE @fraza 
                ORDER BY c.Name";

            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@fraza", $"%{fraza}%");
                await using var rd = await cmd.ExecuteReaderAsync();

                while (await rd.ReadAsync())
                {
                    WynikiWyszukiwania.Add(new OdbiorcaOferta
                    {
                        Id = rd["Id"]?.ToString() ?? "",
                        Nazwa = rd["Nazwa"]?.ToString() ?? "",
                        NIP = rd["NIP"]?.ToString() ?? "",
                        Adres = rd["Adres"]?.ToString() ?? "",
                        KodPocztowy = rd["KodPocztowy"]?.ToString() ?? "",
                        Miejscowosc = rd["Miejscowosc"]?.ToString() ?? "",
                        Zrodlo = "HANDEL"
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd HANDEL: {ex.Message}");
            }
        }

        private async Task SzukajWCRMAsync(string fraza)
        {
            const string sql = @"SELECT TOP 30 ID, NAZWA, KOD, MIASTO, ULICA, NUMER, NR_LOK, TELEFON_K, Imie, Nazwisko, Stanowisko, Wojewodztwo, Status 
                FROM [LibraNet].[dbo].[OdbiorcyCRM] 
                WHERE NAZWA LIKE @fraza OR KOD LIKE @fraza OR MIASTO LIKE @fraza OR TELEFON_K LIKE @fraza 
                ORDER BY NAZWA";

            try
            {
                await using var cn = new SqlConnection(_connLibraNet);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@fraza", $"%{fraza}%");
                await using var rd = await cmd.ExecuteReaderAsync();

                while (await rd.ReadAsync())
                {
                    string ulica = rd["ULICA"]?.ToString() ?? "";
                    string numer = rd["NUMER"]?.ToString() ?? "";
                    string nrLok = rd["NR_LOK"]?.ToString() ?? "";
                    string adres = ulica;
                    if (!string.IsNullOrEmpty(numer)) adres += " " + numer;
                    if (!string.IsNullOrEmpty(nrLok)) adres += "/" + nrLok;

                    string imie = rd["Imie"]?.ToString() ?? "";
                    string nazwisko = rd["Nazwisko"]?.ToString() ?? "";

                    WynikiWyszukiwania.Add(new OdbiorcaOferta
                    {
                        Id = rd["ID"]?.ToString() ?? "",
                        Nazwa = rd["NAZWA"]?.ToString() ?? "",
                        NIP = "",
                        Adres = adres.Trim(),
                        KodPocztowy = rd["KOD"]?.ToString() ?? "",
                        Miejscowosc = rd["MIASTO"]?.ToString() ?? "",
                        Telefon = rd["TELEFON_K"]?.ToString() ?? "",
                        OsobaKontaktowa = $"{imie} {nazwisko}".Trim(),
                        Stanowisko = rd["Stanowisko"]?.ToString() ?? "",
                        Wojewodztwo = rd["Wojewodztwo"]?.ToString() ?? "",
                        Status = rd["Status"]?.ToString() ?? "",
                        Zrodlo = "CRM"
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd CRM: {ex.Message}");
            }
        }

        // =====================================================
        // ZARZĄDZANIE ODBIORCAMI
        // =====================================================

        private void LstWynikiWyszukiwania_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstWynikiWyszukiwania.SelectedItem is OdbiorcaOferta odbiorca)
                DodajOdbiorce(odbiorca);
        }

        private void BtnDodajOdbiorce_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is OdbiorcaOferta odbiorca)
                DodajOdbiorce(odbiorca);
        }

        private void DodajOdbiorce(OdbiorcaOferta odbiorca)
        {
            if (WybraniOdbiorcy.Any(o => o.Id == odbiorca.Id && o.Zrodlo == odbiorca.Zrodlo))
            {
                MessageBox.Show("Ten odbiorca jest już dodany.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            WybraniOdbiorcy.Add(odbiorca);
            OdswiezListeWybranychOdbiorcow();
        }

        private void OdswiezListeWybranychOdbiorcow()
        {
            placeholderBrakWybranych.Visibility = WybraniOdbiorcy.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            txtLiczbaOdbiorcow.Text = WybraniOdbiorcy.Count.ToString();
            txtTrybOdbiorcow.Text = WybraniOdbiorcy.Count switch { 0 => "Brak", 1 => "Pojedynczy", _ => $"Wielu ({WybraniOdbiorcy.Count})" };

            var doUsuniecia = panelWybraniOdbiorcy.Children.Cast<UIElement>().Where(c => c != placeholderBrakWybranych).ToList();
            foreach (var el in doUsuniecia)
                panelWybraniOdbiorcy.Children.Remove(el);

            foreach (var odbiorca in WybraniOdbiorcy)
            {
                var karta = UtworzKarteOdbiorcy(odbiorca);
                panelWybraniOdbiorcy.Children.Add(karta);
            }
        }

        private Border UtworzKarteOdbiorcy(OdbiorcaOferta odbiorca)
        {
            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = (SolidColorBrush)FindResource("PrimaryGreenBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var stackDane = new StackPanel();

            var badgePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            var badge = new Border
            {
                Background = odbiorca.ZrodloBadgeBackground,
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 8, 0)
            };
            badge.Child = new TextBlock { Text = odbiorca.ZrodloSkrot, FontSize = 9, FontWeight = FontWeights.Bold, Foreground = odbiorca.ZrodloBadgeForeground };
            badgePanel.Children.Add(badge);

            if (!string.IsNullOrEmpty(odbiorca.Status))
                badgePanel.Children.Add(new TextBlock { Text = odbiorca.Status, FontSize = 10, Foreground = (SolidColorBrush)FindResource("LightTextBrush"), VerticalAlignment = VerticalAlignment.Center });

            stackDane.Children.Add(badgePanel);
            stackDane.Children.Add(new TextBlock { Text = odbiorca.Nazwa, FontSize = 14, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });

            if (!string.IsNullOrEmpty(odbiorca.NIP))
                stackDane.Children.Add(new TextBlock { Text = $"NIP: {odbiorca.NIP}", FontSize = 11, Foreground = (SolidColorBrush)FindResource("LightTextBrush") });

            stackDane.Children.Add(new TextBlock { Text = odbiorca.AdresPelny, FontSize = 11, Foreground = (SolidColorBrush)FindResource("LightTextBrush"), TextTrimming = TextTrimming.CharacterEllipsis });

            if (!string.IsNullOrEmpty(odbiorca.Telefon))
                stackDane.Children.Add(new TextBlock { Text = $"📞 {odbiorca.Telefon}", FontSize = 10, Foreground = (SolidColorBrush)FindResource("PrimaryGreenBrush"), Margin = new Thickness(0, 3, 0, 0) });

            Grid.SetColumn(stackDane, 0);
            grid.Children.Add(stackDane);

            var btnUsun = new Button { Content = "❌", Style = (Style)FindResource("SmallButtonStyle"), FontSize = 14, VerticalAlignment = VerticalAlignment.Top, Tag = odbiorca };
            btnUsun.Click += BtnUsunOdbiorce_Click;
            Grid.SetColumn(btnUsun, 1);
            grid.Children.Add(btnUsun);

            border.Child = grid;
            return border;
        }

        private void BtnUsunOdbiorce_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is OdbiorcaOferta odbiorca)
            {
                WybraniOdbiorcy.Remove(odbiorca);
                OdswiezListeWybranychOdbiorcow();
            }
        }

        private void BtnWprowadzRecznie_Click(object sender, RoutedEventArgs e)
        {
            var okno = new WprowadzOdbiorceRecznieWindow();
            okno.Owner = this;
            if (okno.ShowDialog() == true && okno.Odbiorca != null)
                DodajOdbiorce(okno.Odbiorca);
        }

        // =====================================================
        // ZARZĄDZANIE PRODUKTAMI
        // =====================================================

        private void RbTypProduktu_Checked(object sender, RoutedEventArgs e)
        {
            _aktywnyKatalog = rbSwiezy?.IsChecked == true ? "67095" : "67153";
            FiltrujTowary();
        }

        private void DodajNowyPustyWiersz()
        {
            var nowyWiersz = new TowarWiersz
            {
                Lp = TowaryWOfercie.Count + 1,
                Opakowanie = "E2"
            };

            nowyWiersz.PropertyChanged += TowarWiersz_PropertyChanged;
            TowaryWOfercie.Add(nowyWiersz);
        }

        private void TowaryWOfercie_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            PrzenumerujWiersze();
            AktualizujPodsumowanieTowary();
        }

        private void TowarWiersz_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is TowarWiersz wiersz)
            {
                // Gdy wybrano towar w ostatnim wierszu - dodaj nowy pusty
                if (e.PropertyName == nameof(TowarWiersz.TowarId) && wiersz.CzyWypelniony)
                {
                    if (TowaryWOfercie.LastOrDefault() == wiersz)
                    {
                        Dispatcher.BeginInvoke(new Action(() => DodajNowyPustyWiersz()), DispatcherPriority.Background);
                    }
                }

                // Aktualizuj podsumowanie przy zmianie wartości
                if (e.PropertyName == nameof(TowarWiersz.Wartosc) ||
                    e.PropertyName == nameof(TowarWiersz.Ilosc) ||
                    e.PropertyName == nameof(TowarWiersz.Cena))
                {
                    AktualizujPodsumowanieTowary();
                }
            }
        }

        private void CboTowar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cbo && cbo.SelectedItem is TowarOferta wybranyTowar)
            {
                var wiersz = cbo.DataContext as TowarWiersz;
                if (wiersz != null && wybranyTowar.Id > 0)
                {
                    wiersz.TowarId = wybranyTowar.Id;
                    wiersz.Kod = wybranyTowar.Kod;
                    wiersz.Nazwa = wybranyTowar.Nazwa;
                    wiersz.Katalog = wybranyTowar.Katalog;

                    if (string.IsNullOrEmpty(wiersz.Opakowanie))
                        wiersz.Opakowanie = "E2";
                }
            }
        }

        private void TxtNumeric_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsValidNumericInput(e.Text, (sender as TextBox)?.Text ?? "");
        }

        private bool IsValidNumericInput(string input, string currentText)
        {
            foreach (char c in input)
            {
                if (!char.IsDigit(c) && c != ',' && c != '.')
                    return false;
            }

            if ((input.Contains(',') || input.Contains('.')) &&
                (currentText.Contains(',') || currentText.Contains('.')))
                return false;

            return true;
        }

        private void BtnUsunWiersz_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is TowarWiersz wiersz)
            {
                wiersz.PropertyChanged -= TowarWiersz_PropertyChanged;
                TowaryWOfercie.Remove(wiersz);

                if (!TowaryWOfercie.Any() || TowaryWOfercie.All(w => w.CzyWypelniony))
                    DodajNowyPustyWiersz();
            }
        }

        private void PrzenumerujWiersze()
        {
            int lp = 1;
            foreach (var wiersz in TowaryWOfercie)
                wiersz.Lp = lp++;
        }

        private void AktualizujPodsumowanieTowary()
        {
            var wypelnioneWiersze = TowaryWOfercie.Where(w => w.CzyWypelniony).ToList();
            int liczba = wypelnioneWiersze.Count;
            decimal suma = wypelnioneWiersze.Sum(w => w.Wartosc);

            txtLiczbaPozycji.Text = liczba.ToString();
            txtSumaTowarow.Text = $"{suma:N2} zł";
            txtWartoscCalkowita.Text = $"{suma:N2} zł";
        }

        private void BtnWczytajSzablonTowarow_Click(object sender, RoutedEventArgs e)
        {
            var szablony = _szablonyManager.WczytajSzablonyTowarow();
            if (!szablony.Any())
            {
                MessageBox.Show("Brak zapisanych szablonów towarów.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var okno = new WyborSzablonuWindow(szablony.Cast<object>().ToList(), "Wybierz szablon towarów");
            okno.Owner = this;

            if (okno.ShowDialog() == true && okno.WybranyIndex >= 0)
            {
                var szablon = szablony[okno.WybranyIndex];

                foreach (var w in TowaryWOfercie)
                    w.PropertyChanged -= TowarWiersz_PropertyChanged;
                TowaryWOfercie.Clear();

                foreach (var towarSzablonu in szablon.Towary)
                {
                    var towarBaza = DostepneTowary.FirstOrDefault(t => t.Id == towarSzablonu.TowarId);
                    if (towarBaza != null)
                    {
                        var wiersz = new TowarWiersz
                        {
                            Lp = TowaryWOfercie.Count + 1,
                            TowarId = towarBaza.Id,
                            Kod = towarBaza.Kod,
                            Nazwa = towarBaza.Nazwa,
                            Katalog = towarBaza.Katalog,
                            Ilosc = towarSzablonu.DomyslnaIlosc,
                            Cena = towarSzablonu.DomyslnaCena,
                            Opakowanie = towarSzablonu.Opakowanie
                        };
                        wiersz.PropertyChanged += TowarWiersz_PropertyChanged;
                        TowaryWOfercie.Add(wiersz);
                    }
                }

                DodajNowyPustyWiersz();
                AktualizujPodsumowanieTowary();

                MessageBox.Show($"Wczytano szablon: {szablon.Nazwa}\nLiczba produktów: {szablon.Towary.Count}",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnMarze_Click(object sender, RoutedEventArgs e)
        {
            var towaryDoMarzy = TowaryWOfercie
                .Where(w => w.CzyWypelniony)
                .Select(w => new TowarOfertaWiersz
                {
                    Id = w.TowarId,
                    Kod = w.Kod,
                    Nazwa = w.Nazwa,
                    Ilosc = w.Ilosc,
                    CenaJednostkowa = w.Cena
                })
                .ToList();

            if (!towaryDoMarzy.Any())
            {
                MessageBox.Show("Dodaj przynajmniej jeden produkt.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var okno = new MarzeWindow(towaryDoMarzy);
            okno.Owner = this;
            if (okno.ShowDialog() == true)
            {
                for (int i = 0; i < towaryDoMarzy.Count && i < TowaryWOfercie.Count; i++)
                {
                    var wiersz = TowaryWOfercie.FirstOrDefault(w => w.TowarId == towaryDoMarzy[i].Id);
                    if (wiersz != null)
                        wiersz.Cena = towaryDoMarzy[i].CenaJednostkowa;
                }
                AktualizujPodsumowanieTowary();
            }
        }

        private void BtnTlumaczenia_Click(object sender, RoutedEventArgs e)
        {
            var okno = new TlumaczeniaProduktowWindow();
            okno.Owner = this;
            okno.ShowDialog();
        }

        // =====================================================
        // NAWIGACJA MIĘDZY KROKAMI
        // =====================================================

        private void AktualizujWidokKroku()
        {
            panelKrok1.Visibility = _aktualnyKrok == 1 ? Visibility.Visible : Visibility.Collapsed;
            panelKrok2.Visibility = _aktualnyKrok == 2 ? Visibility.Visible : Visibility.Collapsed;
            panelKrok3.Visibility = _aktualnyKrok == 3 ? Visibility.Visible : Visibility.Collapsed;
            panelKrok4.Visibility = _aktualnyKrok == 4 ? Visibility.Visible : Visibility.Collapsed;

            if (_aktualnyKrok == 4)
                AktualizujPodsumowanie();

            AktualizujWskaznikiKrokow();
            btnWstecz.IsEnabled = _aktualnyKrok > 1;
            btnDalej.Visibility = _aktualnyKrok < 4 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AktualizujWskaznikiKrokow()
        {
            var zielony = (SolidColorBrush)FindResource("PrimaryGreenBrush");
            var szary = new SolidColorBrush(Color.FromRgb(229, 231, 235));
            var szaryTekst = (SolidColorBrush)FindResource("LightTextBrush");

            borderKrok1.Background = _aktualnyKrok >= 1 ? zielony : szary;
            txtKrok1.Foreground = _aktualnyKrok >= 1 ? zielony : szaryTekst;
            lineKrok1.Background = _aktualnyKrok > 1 ? zielony : szary;

            borderKrok2.Background = _aktualnyKrok >= 2 ? zielony : szary;
            txtKrok2.Foreground = _aktualnyKrok >= 2 ? zielony : szaryTekst;
            lineKrok2.Background = _aktualnyKrok > 2 ? zielony : szary;

            borderKrok3.Background = _aktualnyKrok >= 3 ? zielony : szary;
            txtKrok3.Foreground = _aktualnyKrok >= 3 ? zielony : szaryTekst;
            lineKrok3.Background = _aktualnyKrok > 3 ? zielony : szary;

            borderKrok4.Background = _aktualnyKrok >= 4 ? zielony : szary;
            txtKrok4.Foreground = _aktualnyKrok >= 4 ? zielony : szaryTekst;
        }

        private void Krok_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is StackPanel panel && panel.Tag is string tagStr && int.TryParse(tagStr, out int krok))
            {
                if (krok < _aktualnyKrok || WalidujKrok(_aktualnyKrok))
                {
                    _aktualnyKrok = krok;
                    AktualizujWidokKroku();
                }
            }
        }

        private void BtnDalej_Click(object sender, RoutedEventArgs e)
        {
            if (!WalidujKrok(_aktualnyKrok)) return;
            if (_aktualnyKrok < 4)
            {
                _aktualnyKrok++;
                AktualizujWidokKroku();
            }
        }

        private void BtnWstecz_Click(object sender, RoutedEventArgs e)
        {
            if (_aktualnyKrok > 1)
            {
                _aktualnyKrok--;
                AktualizujWidokKroku();
            }
        }

        private bool WalidujKrok(int krok)
        {
            switch (krok)
            {
                case 1:
                    if (WybraniOdbiorcy.Count == 0)
                    {
                        MessageBox.Show("Wybierz przynajmniej jednego odbiorcę.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                    return true;

                case 2:
                    if (!TowaryWOfercie.Any(w => w.CzyWypelniony))
                    {
                        MessageBox.Show("Dodaj przynajmniej jeden produkt.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                    return true;

                default:
                    return true;
            }
        }

        // =====================================================
        // PODSUMOWANIE I GENEROWANIE PDF
        // =====================================================

        private void BtnWczytajSzablonParametrow_Click(object sender, RoutedEventArgs e)
        {
            var szablony = _szablonyManager.WczytajSzablonyParametrow();
            if (!szablony.Any())
            {
                MessageBox.Show("Brak szablonów parametrów.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var okno = new WyborSzablonuWindow(szablony.Cast<object>().ToList(), "Wybierz szablon parametrów");
            okno.Owner = this;

            if (okno.ShowDialog() == true && okno.WybranyIndex >= 0)
            {
                var szablon = szablony[okno.WybranyIndex];

                foreach (ComboBoxItem item in cboTerminPlatnosci.Items)
                    if (item.Tag?.ToString() == szablon.DniPlatnosci.ToString()) { cboTerminPlatnosci.SelectedItem = item; break; }

                foreach (ComboBoxItem item in cboKontoBankowe.Items)
                    if (item.Tag?.ToString() == szablon.WalutaKonta) { cboKontoBankowe.SelectedItem = item; break; }

                rbTransportWlasny.IsChecked = szablon.TransportTyp == "wlasny";
                rbTransportKlienta.IsChecked = szablon.TransportTyp == "klienta";

                foreach (ComboBoxItem item in cboJezykPDF.Items)
                    if (item.Tag?.ToString() == szablon.Jezyk.ToString()) { cboJezykPDF.SelectedItem = item; break; }

                foreach (ComboBoxItem item in cboTypLogo.Items)
                    if (item.Tag?.ToString() == szablon.TypLogo.ToString()) { cboTypLogo.SelectedItem = item; break; }

                chkPdfPokazOpakowanie.IsChecked = szablon.PokazOpakowanie;
                chkPdfPokazCene.IsChecked = szablon.PokazCene;
                chkPdfPokazIlosc.IsChecked = szablon.PokazIlosc;
                chkPdfPokazTermin.IsChecked = szablon.PokazTerminPlatnosci;

                if (!string.IsNullOrEmpty(szablon.NotatkaCustom))
                    txtNotatki.Text = szablon.NotatkaCustom;

                MessageBox.Show($"Wczytano szablon: {szablon.Nazwa}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void AktualizujPodsumowanie()
        {
            txtPodWystawiajacy.Text = _nazwaOperatora;

            panelPodsumowanieOdbiorcy.Children.Clear();
            foreach (var odbiorca in WybraniOdbiorcy)
            {
                var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
                sp.Children.Add(new TextBlock { Text = odbiorca.Nazwa, FontSize = 14, FontWeight = FontWeights.SemiBold });
                sp.Children.Add(new TextBlock { Text = odbiorca.AdresPelny, FontSize = 11, Foreground = (SolidColorBrush)FindResource("LightTextBrush") });
                if (!string.IsNullOrEmpty(odbiorca.NIP))
                    sp.Children.Add(new TextBlock { Text = $"NIP: {odbiorca.NIP}", FontSize = 11, Foreground = (SolidColorBrush)FindResource("LightTextBrush") });
                panelPodsumowanieOdbiorcy.Children.Add(sp);
            }

            txtPodTermin.Text = (cboTerminPlatnosci.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "-";
            txtPodWaznosc.Text = (cboWaznoscOferty.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "-";

            var produkty = TowaryWOfercie.Where(w => w.CzyWypelniony).ToList();
            dgPodsumowanieTowary.ItemsSource = produkty;
            txtPodLiczbaPozycji.Text = produkty.Count.ToString();
            decimal suma = produkty.Sum(w => w.Wartosc);
            txtPodSuma.Text = $"{suma:N2} zł";

            int dniWaznosci = int.Parse((cboWaznoscOferty.SelectedItem as ComboBoxItem)?.Tag.ToString() ?? "1");
            string dniSlowo = dniWaznosci == 1 ? "dzień" : "dni";

            txtEmailTresc.Text = $@"Szanowni Państwo,

W załączeniu oferta cenowa.
Ważna {dniWaznosci} {dniSlowo}.

Z poważaniem,
{_nazwaOperatora}
Ubojnia Drobiu ""Piórkowscy""";
        }

        private void BtnTylkoPDF_Click(object sender, RoutedEventArgs e) => GenerujPDF(false);
        private void BtnGenerujIWyslij_Click(object sender, RoutedEventArgs e) => GenerujPDF(true);

        private void GenerujPDF(bool otworzEmail)
        {
            try
            {
                var odbiorca = WybraniOdbiorcy.First();
                var klient = odbiorca.ToKlientOferta();
                int dniWaznosci = int.Parse((cboWaznoscOferty.SelectedItem as ComboBoxItem)?.Tag.ToString() ?? "1");

                var parametry = new ParametryOferty
                {
                    TerminPlatnosci = (cboTerminPlatnosci.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "1 dzień",
                    DniPlatnosci = int.Parse((cboTerminPlatnosci.SelectedItem as ComboBoxItem)?.Tag.ToString() ?? "1"),
                    DniWaznosci = dniWaznosci,
                    WalutaKonta = (cboKontoBankowe.SelectedItem as ComboBoxItem)?.Tag.ToString() ?? "PLN",
                    Jezyk = (cboJezykPDF.SelectedItem as ComboBoxItem)?.Tag.ToString() == "English" ? JezykOferty.English : JezykOferty.Polski,
                    TypLogo = (cboTypLogo.SelectedItem as ComboBoxItem)?.Tag.ToString() == "Dlugie" ? TypLogo.Dlugie : TypLogo.Okragle,
                    PokazOpakowanie = chkPdfPokazOpakowanie.IsChecked == true,
                    PokazCene = chkPdfPokazCene.IsChecked == true,
                    PokazIlosc = chkPdfPokazIlosc.IsChecked == true,
                    PokazTerminPlatnosci = chkPdfPokazTermin.IsChecked == true,
                    WystawiajacyNazwa = _nazwaOperatora
                };

                var produkty = TowaryWOfercie
                    .Where(w => w.CzyWypelniony)
                    .Select(w => w.ToTowarOferta())
                    .ToList();

                string transport = rbTransportWlasny.IsChecked == true ? "Transport własny" : "Transport klienta";
                string nazwaKlienta = klient.Nazwa.Replace(" ", "_").Replace("\"", "").Replace("/", "_").Replace("\\", "_");
                if (nazwaKlienta.Length > 30) nazwaKlienta = nazwaKlienta.Substring(0, 30);

                string nazwaPliku = $"Oferta_{nazwaKlienta}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Oferty");
                Directory.CreateDirectory(folder);
                string sciezka = Path.Combine(folder, nazwaPliku);

                var generator = new OfertaPDFGenerator();
                generator.GenerujPDF(sciezka, klient, produkty, txtNotatki.Text, transport, parametry);

                if (otworzEmail)
                    OtworzEmailZZalacznikiem(sciezka, klient, dniWaznosci);
                else
                {
                    Process.Start(new ProcessStartInfo(sciezka) { UseShellExecute = true });
                    MessageBox.Show($"PDF wygenerowany:\n{sciezka}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                if (WybraniOdbiorcy.Count > 1)
                {
                    MessageBox.Show($"Wygenerowano dla pierwszego odbiorcy.\nGeneruję dla pozostałych {WybraniOdbiorcy.Count - 1}...", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);

                    for (int i = 1; i < WybraniOdbiorcy.Count; i++)
                    {
                        var kolejnyOdbiorca = WybraniOdbiorcy[i];
                        var kolejnyKlient = kolejnyOdbiorca.ToKlientOferta();
                        nazwaKlienta = kolejnyKlient.Nazwa.Replace(" ", "_").Replace("\"", "").Replace("/", "_").Replace("\\", "_");
                        if (nazwaKlienta.Length > 30) nazwaKlienta = nazwaKlienta.Substring(0, 30);
                        nazwaPliku = $"Oferta_{nazwaKlienta}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                        sciezka = Path.Combine(folder, nazwaPliku);
                        generator.GenerujPDF(sciezka, kolejnyKlient, produkty, txtNotatki.Text, transport, parametry);
                    }

                    Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd generowania PDF: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OtworzEmailZZalacznikiem(string sciezkaPDF, KlientOferta klient, int dniWaznosci)
        {
            try
            {
                string temat = Uri.EscapeDataString("Oferta cenowa - Piórkowscy");
                string dniSlowo = dniWaznosci == 1 ? "dzień" : "dni";
                string tresc = Uri.EscapeDataString($@"Szanowni Państwo,

W załączeniu oferta cenowa.
Ważna {dniWaznosci} {dniSlowo}.

Z poważaniem,
{_nazwaOperatora}
Ubojnia Drobiu ""Piórkowscy""
Tel: +48 24 254 00 00");

                string mailtoUrl = $"mailto:?subject={temat}&body={tresc}";
                Process.Start(new ProcessStartInfo(mailtoUrl) { UseShellExecute = true });
                Process.Start(new ProcessStartInfo(sciezkaPDF) { UseShellExecute = true });

                MessageBox.Show($"Otwarto klienta email i PDF.\nDołącz PDF ręcznie:\n{sciezkaPDF}", "Email", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwierania email: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // =====================================================
        // OBSŁUGA OKNA
        // =====================================================

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            else
                DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void MaximizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

    // =====================================================
    // KLASA POMOCNICZA DLA KOMPATYBILNOŚCI Z MarzeWindow
    // =====================================================
    public class TowarOfertaWiersz : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Kod { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public decimal Ilosc { get; set; }
        public decimal CenaJednostkowa { get; set; }
        public string IloscStr => Ilosc == 0 ? "" : $"{Ilosc:N0} kg";

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
