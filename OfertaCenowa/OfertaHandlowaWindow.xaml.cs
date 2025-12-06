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
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
    // MODEL WIERSZA TOWARU - Z TYPEM ŚWIEŻY/MROŻONY
    // =====================================================
    public class TowarWiersz : INotifyPropertyChanged
    {
        private int _lp;
        private TowarOferta? _wybranyTowar;
        private decimal _ilosc;
        private decimal _cena;
        private string _opakowanie = "E2";
        private int _typProduktuIndex = 0; // 0 = świeży, 1 = mrożony

        public int Lp
        {
            get => _lp;
            set { _lp = value; OnPropertyChanged(); }
        }

        public TowarOferta? WybranyTowar
        {
            get => _wybranyTowar;
            set
            {
                if (_wybranyTowar != value)
                {
                    _wybranyTowar = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TowarId));
                    OnPropertyChanged(nameof(Kod));
                    OnPropertyChanged(nameof(Nazwa));
                    OnPropertyChanged(nameof(Katalog));
                    OnPropertyChanged(nameof(CzyWypelniony));
                    OnPropertyChanged(nameof(UsunWidocznosc));
                    OnPropertyChanged(nameof(WyswietlanaNazwa));
                    OnPropertyChanged(nameof(TloWiersza));
                    OnPropertyChanged(nameof(CboBackground));
                    OnPropertyChanged(nameof(TxtBackground));
                    
                    TowarZmieniony?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler? TowarZmieniony;
        public event EventHandler? TypZmieniony;

        public int TowarId => _wybranyTowar?.Id ?? 0;
        public string Kod => _wybranyTowar?.Kod ?? "";
        public string Nazwa => _wybranyTowar?.Nazwa ?? "";
        public string Katalog => _wybranyTowar?.Katalog ?? "";

        // TYP PRODUKTU: 0 = świeży, 1 = mrożony
        public int TypProduktuIndex
        {
            get => _typProduktuIndex;
            set
            {
                if (_typProduktuIndex != value)
                {
                    int staryTyp = _typProduktuIndex;
                    _typProduktuIndex = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CzyMrozony));
                    OnPropertyChanged(nameof(TypProduktuEmoji));
                    
                    // Wywołaj event tylko jeśli produkt jest wybrany
                    if (_wybranyTowar != null)
                    {
                        TypZmieniony?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        public bool CzyMrozony => _typProduktuIndex == 1;
        public string TypProduktuEmoji => CzyMrozony ? "❄️" : "🥩";

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

        public decimal Wartosc => Ilosc * Cena;
        public string WartoscTekst => Wartosc == 0 ? "" : $"{Wartosc:N2} zł";
        public string WyswietlanaNazwa => string.IsNullOrEmpty(Kod) ? "" : $"{Kod} - {Nazwa}";
        public bool CzyWypelniony => _wybranyTowar != null && TowarId > 0;
        public Visibility UsunWidocznosc => CzyWypelniony ? Visibility.Visible : Visibility.Hidden;
        public string IloscStr => Ilosc == 0 ? "" : $"{Ilosc:N0} kg";
        public string CenaJednostkowaStr => Cena == 0 ? "" : $"{Cena:N2}";

        public SolidColorBrush TloWiersza => CzyWypelniony 
            ? new SolidColorBrush(Color.FromRgb(255, 255, 255)) 
            : new SolidColorBrush(Color.FromRgb(250, 251, 252));
        
        public SolidColorBrush CboBackground => CzyWypelniony 
            ? new SolidColorBrush(Colors.White) 
            : new SolidColorBrush(Color.FromRgb(249, 250, 251));
        
        public SolidColorBrush TxtBackground => CzyWypelniony 
            ? new SolidColorBrush(Colors.White) 
            : new SolidColorBrush(Color.FromRgb(243, 244, 246));

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
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private readonly string _connLibraNet = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public ObservableCollection<OdbiorcaOferta> WynikiWyszukiwania { get; set; } = new();
        public ObservableCollection<OdbiorcaOferta> WybraniOdbiorcy { get; set; } = new();
        public ObservableCollection<TowarOferta> DostepneTowary { get; set; } = new();
        public ObservableCollection<TowarOferta> FiltrowaneTowary { get; set; } = new();
        public ObservableCollection<TowarOferta> TowarySwiezy { get; set; } = new();
        public ObservableCollection<TowarOferta> TowaryMrozone { get; set; } = new();
        public ObservableCollection<TowarWiersz> TowaryWOfercie { get; set; } = new();
        public List<string> OpakowanieLista { get; set; } = new() { "E2", "Karton", "Poliblok" };

        private int _aktualnyKrok = 1;
        private readonly SzablonyManager _szablonyManager = new();
        private SzablonOdbiorcow? _ostatnioWczytanySzablon = null;
        private readonly OfertaRepository _ofertaRepository = new();
        private string _nazwaOperatora = "";
        private string _emailOperatora = "";
        private string _telefonOperatora = "";
        private string _userId = "";
        private DispatcherTimer? _searchTimer;

        // Publiczna właściwość UserID (ustawiana z MENU)
        public string UserID
        {
            get => _userId;
            set
            {
                _userId = value;
                // Załaduj nazwę operatora po ustawieniu UserID
                _ = LoadOperatorAsync();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public OfertaHandlowaWindow() : this(null, "") { }
        public OfertaHandlowaWindow(KlientOferta? klient) : this(klient, "") { }

        public OfertaHandlowaWindow(KlientOferta? klient, string userId)
        {
            InitializeComponent();
            DataContext = this;
            _userId = userId;

            lstWynikiWyszukiwania.ItemsSource = WynikiWyszukiwania;
            _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _searchTimer.Tick += (s, e) => { _searchTimer.Stop(); WyszukajOdbiorcow(); };

            TowaryWOfercie.CollectionChanged += TowaryWOfercie_CollectionChanged;

            // Wczytaj logo do nagłówka
            WczytajLogoDoNaglowka();

            LoadDataAsync();
            _szablonyManager.UtworzSzablonyPrzykladowe();

            // ✅ ULEPSZONE: Obsługa klienta przekazanego z CRM lub innego źródła
            if (klient != null)
            {
                var odbiorca = new OdbiorcaOferta
                {
                    Id = klient.Id,
                    Nazwa = klient.Nazwa,
                    NIP = klient.NIP ?? "",
                    Adres = klient.Adres ?? "",
                    KodPocztowy = klient.KodPocztowy ?? "",
                    Miejscowosc = klient.Miejscowosc ?? "",
                    Telefon = klient.Telefon ?? "",
                    OsobaKontaktowa = klient.OsobaKontaktowa ?? "",
                    Zrodlo = klient.CzyReczny ? "CRM" : "HANDEL" // CRM jeśli ręczny (z CRM)
                };
                WybraniOdbiorcy.Add(odbiorca);
                OdswiezListeWybranychOdbiorcow();
                
                // Automatycznie przejdź do kroku 2 jeśli klient przekazany
                _aktualnyKrok = 2;
            }

            DodajNowyPustyWiersz();
            AktualizujWidokKroku();
        }

        // =====================================================
        // LOGO W NAGŁÓWKU
        // =====================================================

        private void WczytajLogoDoNaglowka()
        {
            try
            {
                // Próbuj wczytać logo z embedded resources
                var assemblies = new[]
                {
                    Assembly.GetExecutingAssembly(),
                    Assembly.GetEntryAssembly(),
                    Assembly.GetCallingAssembly()
                }.Where(a => a != null).Distinct().ToList();

                foreach (var assembly in assemblies)
                {
                    if (assembly == null) continue;

                    var allResources = assembly.GetManifestResourceNames();

                    // Szukaj białego logo (logo2white) - idealne na zielone tło
                    string? resourceName = allResources
                        .FirstOrDefault(name => name.ToLower().Contains("logo2white") || name.ToLower().Contains("logo-2-white"));

                    if (resourceName != null)
                    {
                        using var stream = assembly.GetManifestResourceStream(resourceName);
                        if (stream != null)
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = stream;
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            bitmap.Freeze();

                            imgLogoHeader.Source = bitmap;
                            return;
                        }
                    }
                }

                // Fallback: spróbuj wczytać z folderu aplikacji
                var exeAssembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                string appFolder = Path.GetDirectoryName(exeAssembly.Location) ?? AppDomain.CurrentDomain.BaseDirectory;

                var possiblePaths = new[]
                {
                    Path.Combine(appFolder, "logo2white.png"),
                    Path.Combine(appFolder, "logo-2-white.png"),
                    Path.Combine(appFolder, "logo-2-green.png"),
                    Path.Combine(appFolder, "Logo.png")
                };

                foreach (var logoPath in possiblePaths)
                {
                    if (File.Exists(logoPath))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(logoPath);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();

                        imgLogoHeader.Source = bitmap;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd wczytywania logo do nagłówka: {ex.Message}");
            }
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
                _emailOperatora = "";
                _telefonOperatora = "";
                txtWystawiajacy.Text = _nazwaOperatora;
                return;
            }

            try
            {
                await using var cn = new SqlConnection(_connLibraNet);
                await cn.OpenAsync();

                // Pobierz nazwę operatora
                await using var cmd = new SqlCommand("SELECT Name FROM [LibraNet].[dbo].[operators] WHERE ID = @id", cn);
                cmd.Parameters.AddWithValue("@id", _userId);
                var result = await cmd.ExecuteScalarAsync();
                _nazwaOperatora = result?.ToString() ?? "Nieznany";
                txtWystawiajacy.Text = _nazwaOperatora;

                // Pobierz dane kontaktowe (email, telefon)
                try
                {
                    await using var cmdKontakt = new SqlCommand(
                        "SELECT Email, Telefon FROM [LibraNet].[dbo].[OperatorzyKontakt] WHERE OperatorID = @id", cn);
                    cmdKontakt.Parameters.AddWithValue("@id", _userId);

                    await using var reader = await cmdKontakt.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        _emailOperatora = reader["Email"]?.ToString() ?? "";
                        _telefonOperatora = reader["Telefon"]?.ToString() ?? "";
                    }
                }
                catch
                {
                    // Tabela może nie istnieć - ignoruj
                    _emailOperatora = "";
                    _telefonOperatora = "";
                }
            }
            catch
            {
                _nazwaOperatora = "Nieznany";
                _emailOperatora = "";
                _telefonOperatora = "";
                txtWystawiajacy.Text = _nazwaOperatora;
            }
        }

        private async Task LoadTowaryAsync()
        {
            DostepneTowary.Clear();
            TowarySwiezy.Clear();
            TowaryMrozone.Clear();
            
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

                    var towar = new TowarOferta
                    {
                        Id = rd.GetInt32(0),
                        Kod = kod,
                        Nazwa = rd["Nazwa"]?.ToString() ?? "",
                        Katalog = rd["katalog"]?.ToString() ?? "",
                        Opakowanie = "E2"
                    };
                    
                    DostepneTowary.Add(towar);
                    
                    if (towar.Katalog == "67095")
                        TowarySwiezy.Add(towar);
                    else if (towar.Katalog == "67153")
                        TowaryMrozone.Add(towar);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd ładowania towarów: {ex.Message}");
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
        // ✅ NOWA FUNKCJA: Checkbox "Do wysłania oferta"
        // =====================================================

        /// <summary>
        /// Obsługa checkboxa - filtruj tylko klientów CRM ze statusem "Do wysłania oferta"
        /// </summary>
        private async void ChkTylkoDoWyslaniaOferty_Changed(object sender, RoutedEventArgs e)
        {
            if (chkTylkoDoWyslaniaOferty.IsChecked == true)
            {
                // Zaznaczony - wczytaj klientów CRM ze statusem "Do wysłania oferta"
                txtSzukajOdbiorcy.Text = "";
                rbZrodloCRM.IsChecked = true;
                await WczytajKlientowDoWyslaniaOfertaAsync();
            }
            else
            {
                // Odznaczony - wyczyść wyniki
                WynikiWyszukiwania.Clear();
                placeholderBrakWynikow.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Obsługa checkboxa "Bez odbiorcy" - oferta ogólna bez danych klienta
        /// </summary>
        private void ChkBezOdbiorcy_Changed(object sender, RoutedEventArgs e)
        {
            if (chkBezOdbiorcy.IsChecked == true)
            {
                // Zaznaczony - wyczyść wybranych odbiorców i pokaż komunikat
                WybraniOdbiorcy.Clear();
                OdswiezListeWybranychOdbiorcow();
                txtTrybOdbiorcow.Text = "Bez odbiorcy";
                placeholderBrakWybranych.Visibility = Visibility.Collapsed;

                // Pokaż informację w panelu wybranych
                var infoPanel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
                var infoIcon = new TextBlock { Text = "📋", FontSize = 32, HorizontalAlignment = HorizontalAlignment.Center, Opacity = 0.7 };
                var infoText = new TextBlock { Text = "Oferta ogólna", FontSize = 14, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0), Foreground = new SolidColorBrush(Color.FromRgb(147, 51, 234)) };
                var infoDesc = new TextBlock { Text = "bez danych odbiorcy", FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Foreground = (SolidColorBrush)FindResource("LightTextBrush") };
                infoPanel.Children.Add(infoIcon);
                infoPanel.Children.Add(infoText);
                infoPanel.Children.Add(infoDesc);
                panelWybraniOdbiorcy.Children.Add(infoPanel);
            }
            else
            {
                // Odznaczony - przywróć normalny widok
                OdswiezListeWybranychOdbiorcow();
            }
        }

        /// <summary>
        /// Wczytuje wszystkich klientów CRM ze statusem "Do wysłania oferta"
        /// </summary>
        private async Task WczytajKlientowDoWyslaniaOfertaAsync()
        {
            const string sql = @"SELECT ID, NAZWA, KOD, MIASTO, ULICA, NUMER, NR_LOK, TELEFON_K, Imie, Nazwisko, Stanowisko, Wojewodztwo, Status 
                FROM [LibraNet].[dbo].[OdbiorcyCRM] 
                WHERE Status = 'Do wysłania oferta'
                ORDER BY NAZWA";

            try
            {
                WynikiWyszukiwania.Clear();
                placeholderBrakWynikow.Visibility = Visibility.Collapsed;

                await using var cn = new SqlConnection(_connLibraNet);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                await using var rd = await cmd.ExecuteReaderAsync();

                int count = 0;
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
                    count++;
                }

                if (count == 0)
                {
                    placeholderBrakWynikow.Visibility = Visibility.Visible;
                    MessageBox.Show("Brak klientów CRM ze statusem 'Do wysłania oferta'.", 
                        "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Znaleziono {count} klientów CRM oczekujących na ofertę.", 
                        "📧 Do wysłania oferta", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd wczytywania klientów CRM: {ex.Message}");
                MessageBox.Show($"Błąd wczytywania:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
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

            // Panel kontaktowy (telefon i email)
            var kontaktPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };

            if (!string.IsNullOrEmpty(odbiorca.Telefon))
                kontaktPanel.Children.Add(new TextBlock { Text = $"📞 {odbiorca.Telefon}", FontSize = 10, Foreground = (SolidColorBrush)FindResource("PrimaryGreenBrush"), Margin = new Thickness(0, 0, 10, 0) });

            // Wskaźnik emaila
            if (!string.IsNullOrEmpty(odbiorca.Email))
            {
                kontaktPanel.Children.Add(new TextBlock { Text = $"✉️ {odbiorca.Email}", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(59, 130, 246)) });
            }
            else
            {
                kontaktPanel.Children.Add(new TextBlock { Text = "⚠️ Brak emaila", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)) });
            }

            if (kontaktPanel.Children.Count > 0)
                stackDane.Children.Add(kontaktPanel);

            Grid.SetColumn(stackDane, 0);
            grid.Children.Add(stackDane);

            // Panel z przyciskami (edytuj i usuń)
            var btnPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Top };

            // Przycisk edycji kontaktu - tylko dla CRM
            if (odbiorca.Zrodlo == "CRM" && int.TryParse(odbiorca.Id, out _))
            {
                var btnEdytuj = new Button
                {
                    Content = "✏️",
                    Style = (Style)FindResource("SmallButtonStyle"),
                    FontSize = 14,
                    Tag = odbiorca,
                    ToolTip = "Edytuj dane kontaktowe",
                    Margin = new Thickness(0, 0, 0, 4)
                };
                btnEdytuj.Click += BtnEdytujKontaktOdbiorcy_Click;
                btnPanel.Children.Add(btnEdytuj);
            }

            var btnUsun = new Button { Content = "❌", Style = (Style)FindResource("SmallButtonStyle"), FontSize = 14, Tag = odbiorca, ToolTip = "Usuń z listy" };
            btnUsun.Click += BtnUsunOdbiorce_Click;
            btnPanel.Children.Add(btnUsun);

            Grid.SetColumn(btnPanel, 1);
            grid.Children.Add(btnPanel);

            border.Child = grid;
            return border;
        }

        private void BtnEdytujKontaktOdbiorcy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is OdbiorcaOferta odbiorca)
            {
                if (!int.TryParse(odbiorca.Id, out int klientId))
                {
                    MessageBox.Show("Nie można edytować tego odbiorcy.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var okno = new EdycjaKontaktuWindow
                {
                    KlientID = klientId,
                    KlientNazwa = odbiorca.Nazwa,
                    OperatorID = _userId
                };
                okno.Owner = this;

                if (okno.ShowDialog() == true && okno.ZapisanoZmiany)
                {
                    // Zaktualizuj dane odbiorcy w liście
                    odbiorca.Email = okno.NowyEmail ?? "";
                    odbiorca.Telefon = okno.NowyTelefon ?? "";
                    odbiorca.OsobaKontaktowa = $"{okno.NoweImie} {okno.NoweNazwisko}".Trim();

                    // Odśwież widok
                    OdswiezListeWybranychOdbiorcow();

                    MessageBox.Show("Dane kontaktowe zostały zaktualizowane.", "Zapisano", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
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
        // SZABLONY ODBIORCÓW
        // =====================================================

        private void BtnWczytajSzablonOdbiorcow_Click(object sender, RoutedEventArgs e)
        {
            var okno = new SzablonOdbiorcowWindow(_userId);
            okno.Owner = this;

            if (okno.ShowDialog() == true && okno.WybranySzablon != null)
            {
                // Zapamiętaj wczytany szablon
                _ostatnioWczytanySzablon = okno.WybranySzablon;
                btnNadpiszSzablon.IsEnabled = true;

                // Wyczyść aktualnych odbiorców
                WybraniOdbiorcy.Clear();

                // Wczytaj odbiorców z szablonu
                foreach (var odbiorcaSzablonu in okno.WybranySzablon.Odbiorcy)
                {
                    var odbiorca = new OdbiorcaOferta
                    {
                        Id = odbiorcaSzablonu.Id,
                        Nazwa = odbiorcaSzablonu.Nazwa,
                        NIP = odbiorcaSzablonu.NIP,
                        Adres = odbiorcaSzablonu.Adres,
                        KodPocztowy = odbiorcaSzablonu.KodPocztowy,
                        Miejscowosc = odbiorcaSzablonu.Miejscowosc,
                        Telefon = odbiorcaSzablonu.Telefon,
                        Email = odbiorcaSzablonu.Email,
                        OsobaKontaktowa = odbiorcaSzablonu.OsobaKontaktowa,
                        Zrodlo = odbiorcaSzablonu.Zrodlo
                    };
                    WybraniOdbiorcy.Add(odbiorca);
                }

                OdswiezListeWybranychOdbiorcow();

                // Odznacz checkbox "Bez odbiorcy" jeśli był zaznaczony
                if (chkBezOdbiorcy.IsChecked == true)
                    chkBezOdbiorcy.IsChecked = false;

                MessageBox.Show($"Wczytano szablon \"{okno.WybranySzablon.Nazwa}\"\nLiczba odbiorców: {okno.WybranySzablon.LiczbaOdbiorcow}",
                    "Szablon wczytany", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnZapiszSzablonOdbiorcow_Click(object sender, RoutedEventArgs e)
        {
            if (WybraniOdbiorcy.Count == 0)
            {
                MessageBox.Show("Najpierw dodaj odbiorców do listy.", "Brak odbiorców", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Dialog do wprowadzenia nazwy szablonu
            var inputDialog = new InputDialog("Zapisz szablon odbiorców", "Nazwa szablonu:", $"Szablon {DateTime.Now:dd.MM.yyyy}");
            inputDialog.Owner = this;

            if (inputDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(inputDialog.InputText))
            {
                var szablon = new SzablonOdbiorcow
                {
                    Nazwa = inputDialog.InputText,
                    Opis = $"{WybraniOdbiorcy.Count} odbiorców",
                    OperatorId = _userId,
                    Odbiorcy = WybraniOdbiorcy.Select(o => new OdbiorcaSzablonu
                    {
                        Id = o.Id,
                        Nazwa = o.Nazwa,
                        NIP = o.NIP,
                        Adres = o.Adres,
                        KodPocztowy = o.KodPocztowy,
                        Miejscowosc = o.Miejscowosc,
                        Telefon = o.Telefon,
                        Email = o.Email,
                        OsobaKontaktowa = o.OsobaKontaktowa,
                        Zrodlo = o.Zrodlo
                    }).ToList()
                };

                _szablonyManager.DodajSzablonOdbiorcow(_userId, szablon);

                MessageBox.Show($"Zapisano szablon \"{szablon.Nazwa}\"\nLiczba odbiorców: {szablon.LiczbaOdbiorcow}",
                    "Szablon zapisany", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnNadpiszSzablon_Click(object sender, RoutedEventArgs e)
        {
            if (_ostatnioWczytanySzablon == null)
            {
                MessageBox.Show("Najpierw wczytaj szablon.", "Brak szablonu", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (WybraniOdbiorcy.Count == 0)
            {
                MessageBox.Show("Lista odbiorców jest pusta.", "Brak odbiorców", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Czy na pewno chcesz nadpisać szablon \"{_ostatnioWczytanySzablon.Nazwa}\"?\n\nAktualna liczba odbiorców: {WybraniOdbiorcy.Count}",
                "Potwierdź nadpisanie",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Aktualizuj odbiorców w szablonie
                _ostatnioWczytanySzablon.Odbiorcy = WybraniOdbiorcy.Select(o => new OdbiorcaSzablonu
                {
                    Id = o.Id,
                    Nazwa = o.Nazwa,
                    NIP = o.NIP,
                    Adres = o.Adres,
                    KodPocztowy = o.KodPocztowy,
                    Miejscowosc = o.Miejscowosc,
                    Telefon = o.Telefon,
                    Email = o.Email,
                    OsobaKontaktowa = o.OsobaKontaktowa,
                    Zrodlo = o.Zrodlo
                }).ToList();

                _ostatnioWczytanySzablon.Opis = $"{WybraniOdbiorcy.Count} odbiorców";

                _szablonyManager.AktualizujSzablonOdbiorcow(_userId, _ostatnioWczytanySzablon);

                MessageBox.Show($"Szablon \"{_ostatnioWczytanySzablon.Nazwa}\" został nadpisany.\nLiczba odbiorców: {WybraniOdbiorcy.Count}",
                    "Szablon zaktualizowany", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // =====================================================
        // ZARZĄDZANIE PRODUKTAMI
        // =====================================================

        private void ChkZamienWszystkieNaMrozone_Changed(object sender, RoutedEventArgs e)
        {
            bool naMrozone = chkZamienWszystkieNaMrozone.IsChecked == true;
            int zamienione = 0;
            int brakMapowania = 0;
            
            foreach (var wiersz in TowaryWOfercie.Where(w => w.CzyWypelniony).ToList())
            {
                // Pomijaj jeśli już jest w odpowiednim stanie
                if (wiersz.CzyMrozony == naMrozone) continue;
                
                int? noweId = naMrozone 
                    ? MapowanieSwiezyMrozonyWindow.PobierzIdMrozonego(wiersz.TowarId)
                    : MapowanieSwiezyMrozonyWindow.PobierzIdSwiezego(wiersz.TowarId);
                
                if (noweId.HasValue)
                {
                    var nowyTowar = DostepneTowary.FirstOrDefault(t => t.Id == noweId.Value);
                    if (nowyTowar != null)
                    {
                        // Zachowaj wartości
                        decimal ilosc = wiersz.Ilosc;
                        decimal cena = wiersz.Cena;
                        string opakowanie = wiersz.Opakowanie;

                        // Odsubskrybuj
                        wiersz.TypZmieniony -= TowarWiersz_TypZmieniony;
                        
                        // Zmień produkt i typ
                        wiersz.WybranyTowar = nowyTowar;
                        wiersz.TypProduktuIndex = naMrozone ? 1 : 0;
                        
                        // Przywróć wartości
                        wiersz.Ilosc = ilosc;
                        wiersz.Cena = cena;
                        wiersz.Opakowanie = opakowanie;

                        // Ponownie subskrybuj
                        wiersz.TypZmieniony += TowarWiersz_TypZmieniony;
                        
                        zamienione++;
                    }
                }
                else
                {
                    brakMapowania++;
                }
            }
            
            AktualizujPodsumowanieTowary();
            
            if (brakMapowania > 0)
            {
                string typ = naMrozone ? "mrożonych" : "świeżych";
                MessageBox.Show($"Zamieniono {zamienione} produktów.\n\n{brakMapowania} produktów nie ma zmapowanych odpowiedników {typ}.\nUżyj '🥩↔️❄️ Mapowanie' aby je zmapować.",
                    "Zamiana produktów", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DodajNowyPustyWiersz()
        {
            var nowyWiersz = new TowarWiersz
            {
                Lp = TowaryWOfercie.Count + 1,
                Opakowanie = "E2",
                TypProduktuIndex = rbDomyslnyMrozony?.IsChecked == true ? 1 : 0
            };

            nowyWiersz.PropertyChanged += TowarWiersz_PropertyChanged;
            nowyWiersz.TowarZmieniony += TowarWiersz_TowarZmieniony;
            nowyWiersz.TypZmieniony += TowarWiersz_TypZmieniony;
            
            TowaryWOfercie.Add(nowyWiersz);
        }

        private void TowarWiersz_TypZmieniony(object? sender, EventArgs e)
        {
            if (sender is TowarWiersz wiersz && wiersz.CzyWypelniony)
            {
                ZamienProduktNaOdpowiednik(wiersz);
            }
        }

        private void ZamienProduktNaOdpowiednik(TowarWiersz wiersz)
        {
            if (wiersz.WybranyTowar == null) return;

            int? noweId = null;
            
            if (wiersz.CzyMrozony)
            {
                // Świeży → Mrożony: pobierz ID mrożonego odpowiednika
                noweId = MapowanieSwiezyMrozonyWindow.PobierzIdMrozonego(wiersz.TowarId);
            }
            else
            {
                // Mrożony → Świeży: pobierz ID świeżego odpowiednika
                noweId = MapowanieSwiezyMrozonyWindow.PobierzIdSwiezego(wiersz.TowarId);
            }

            if (noweId.HasValue)
            {
                var nowyTowar = DostepneTowary.FirstOrDefault(t => t.Id == noweId.Value);
                if (nowyTowar != null)
                {
                    // Zachowaj ilość, cenę i opakowanie
                    decimal ilosc = wiersz.Ilosc;
                    decimal cena = wiersz.Cena;
                    string opakowanie = wiersz.Opakowanie;

                    // Tymczasowo odsubskrybuj żeby uniknąć pętli
                    wiersz.TypZmieniony -= TowarWiersz_TypZmieniony;
                    
                    // Zmień produkt (to nie wywoła TypZmieniony bo odsubskrybowaliśmy)
                    wiersz.WybranyTowar = nowyTowar;
                    
                    // Przywróć wartości
                    wiersz.Ilosc = ilosc;
                    wiersz.Cena = cena;
                    wiersz.Opakowanie = opakowanie;

                    // Ponownie subskrybuj
                    wiersz.TypZmieniony += TowarWiersz_TypZmieniony;

                    AktualizujPodsumowanieTowary();
                }
                else
                {
                    MessageBox.Show($"Nie znaleziono odpowiednika dla: {wiersz.Nazwa}", 
                        "Brak mapowania", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                string typ = wiersz.CzyMrozony ? "mrożonego" : "świeżego";
                MessageBox.Show($"Brak zmapowanego odpowiednika {typ} dla:\n{wiersz.Nazwa}\n\nUżyj przycisku '🥩↔️❄️ Mapowanie' aby zmapować produkty.", 
                    "Brak mapowania", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Przywróć poprzedni typ
                wiersz.TypZmieniony -= TowarWiersz_TypZmieniony;
                wiersz.TypProduktuIndex = wiersz.CzyMrozony ? 0 : 1;
                wiersz.TypZmieniony += TowarWiersz_TypZmieniony;
            }
        }

        private void TowarWiersz_TowarZmieniony(object? sender, EventArgs e)
        {
            if (sender is TowarWiersz wiersz && wiersz.CzyWypelniony)
            {
                // Ustaw domyślny typ produktu
                wiersz.TypProduktuIndex = rbDomyslnyMrozony?.IsChecked == true ? 1 : 0;
                
                if (TowaryWOfercie.LastOrDefault() == wiersz)
                {
                    Dispatcher.BeginInvoke(new Action(() => 
                    {
                        DodajNowyPustyWiersz();
                        AktualizujPodsumowanieTowary();
                    }), DispatcherPriority.Background);
                }
                else
                {
                    AktualizujPodsumowanieTowary();
                }
            }
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
                if (e.PropertyName == nameof(TowarWiersz.Wartosc) ||
                    e.PropertyName == nameof(TowarWiersz.Ilosc) ||
                    e.PropertyName == nameof(TowarWiersz.Cena))
                {
                    AktualizujPodsumowanieTowary();
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

        // NAWIGACJA: Enter w polu Ilość -> przejdź do Ilości następnego wiersza
        private void TxtIlosc_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox txt && txt.Tag is TowarWiersz wiersz)
            {
                int index = TowaryWOfercie.IndexOf(wiersz);
                if (index >= 0 && index < TowaryWOfercie.Count - 1)
                {
                    // Znajdź TextBox ilości w następnym wierszu
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var nextWiersz = TowaryWOfercie[index + 1];
                        // Znajdź wszystkie TextBoxy i ustaw focus na odpowiedni
                        FocusujPoleWWierszu(index + 1, "Ilosc");
                    }), DispatcherPriority.Background);
                }
                e.Handled = true;
            }
        }

        // NAWIGACJA: Enter w polu Cena -> przejdź do Ceny następnego wiersza
        private void TxtCena_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox txt && txt.Tag is TowarWiersz wiersz)
            {
                int index = TowaryWOfercie.IndexOf(wiersz);
                if (index >= 0 && index < TowaryWOfercie.Count - 1)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        FocusujPoleWWierszu(index + 1, "Cena");
                    }), DispatcherPriority.Background);
                }
                e.Handled = true;
            }
        }

        private void FocusujPoleWWierszu(int indeksWiersza, string typPola)
        {
            // Znajdź ItemsControl i odpowiedni wiersz
            var container = icTowary.ItemContainerGenerator.ContainerFromIndex(indeksWiersza) as ContentPresenter;
            if (container != null)
            {
                container.ApplyTemplate();
                var border = VisualTreeHelper.GetChild(container, 0) as Border;
                if (border != null)
                {
                    var grid = border.Child as Grid;
                    if (grid != null)
                    {
                        // Kolumna 3 = Ilość, Kolumna 4 = Cena
                        int kolumna = typPola == "Ilosc" ? 3 : 4;
                        foreach (var child in grid.Children)
                        {
                            if (child is TextBox tb && Grid.GetColumn(tb) == kolumna)
                            {
                                tb.Focus();
                                tb.SelectAll();
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void BtnUsunWiersz_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is TowarWiersz wiersz)
            {
                wiersz.PropertyChanged -= TowarWiersz_PropertyChanged;
                wiersz.TowarZmieniony -= TowarWiersz_TowarZmieniony;
                wiersz.TypZmieniony -= TowarWiersz_TypZmieniony;
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

            // Aktualizuj średnią cenę
            AktualizujSredniaCena();
        }

        private void AktualizujSredniaCena()
        {
            // Sprawdź czy kontrolki są zainicjalizowane
            if (txtSredniaCena == null || txtSredniaCenaPoMarzy == null || txtGlobalnaMarza == null)
                return;

            var wypelnioneWiersze = TowaryWOfercie.Where(w => w.CzyWypelniony && w.Cena > 0).ToList();
            
            if (wypelnioneWiersze.Count == 0)
            {
                txtSredniaCena.Text = "0,00 zł";
                txtSredniaCenaPoMarzy.Text = "";
                return;
            }

            decimal sredniaCena = wypelnioneWiersze.Average(w => w.Cena);
            txtSredniaCena.Text = $"{sredniaCena:N2} zł";

            // Oblicz cenę po marży
            if (decimal.TryParse(txtGlobalnaMarza.Text.Replace(",", "."), 
                System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, 
                out decimal marza) && marza != 0)
            {
                decimal sredniaPoMarzy = sredniaCena * (1 + marza / 100);
                string znak = marza > 0 ? "+" : "";
                txtSredniaCenaPoMarzy.Text = $"→ {sredniaPoMarzy:N2} zł ({znak}{marza:N1}%)";
            }
            else
            {
                txtSredniaCenaPoMarzy.Text = "";
            }
        }

        private void TxtGlobalnaMarza_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            // Pozwól tylko na cyfry, przecinek, kropkę i minus
            string tekst = e.Text;
            bool dozwolone = tekst.All(c => char.IsDigit(c) || c == ',' || c == '.' || c == '-');
            e.Handled = !dozwolone;
        }

        private void TxtGlobalnaMarza_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Aktualizuj podgląd średniej ceny po marży (jeśli kontrolki zainicjalizowane)
            if (txtSredniaCena != null)
                AktualizujSredniaCena();
        }

        private void BtnZastosujMarze_Click(object sender, RoutedEventArgs e)
        {
            string marzaTekst = txtGlobalnaMarza.Text.Replace(",", ".").Trim();
            
            if (!decimal.TryParse(marzaTekst, 
                System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, 
                out decimal marzaProcent))
            {
                MessageBox.Show("Podaj prawidłową wartość marży (np. 10 dla 10%).", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (marzaProcent == 0)
            {
                MessageBox.Show("Marża wynosi 0% - ceny pozostaną bez zmian.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var wypelnioneWiersze = TowaryWOfercie.Where(w => w.CzyWypelniony && w.Cena > 0).ToList();

            if (wypelnioneWiersze.Count == 0)
            {
                MessageBox.Show("Brak produktów z cenami do zmiany.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            decimal mnoznik = 1 + marzaProcent / 100;
            int zmieniono = 0;

            foreach (var wiersz in wypelnioneWiersze)
            {
                decimal nowaCena = Math.Round(wiersz.Cena * mnoznik, 2);
                wiersz.Cena = nowaCena;
                wiersz.CenaTekst = nowaCena.ToString("N2", System.Globalization.CultureInfo.InvariantCulture).Replace(",", ".");
                zmieniono++;
            }

            // Reset marży po zastosowaniu
            txtGlobalnaMarza.Text = "0";
            AktualizujPodsumowanieTowary();

            string kierunek = marzaProcent > 0 ? "podniesione" : "obniżone";
            MessageBox.Show($"Ceny zostały {kierunek} o {Math.Abs(marzaProcent):N1}% dla {zmieniono} produktów.", 
                "Marża zastosowana", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // NOWY: Otwórz okno wyboru szablonu (nie od razu edytor)
        private void BtnWczytajSzablonTowarow_Click(object sender, RoutedEventArgs e)
        {
            var okno = new WyborSzablonuTowarowWindow(DostepneTowary);
            okno.Owner = this;

            if (okno.ShowDialog() == true && okno.WybranySzablon != null)
            {
                WczytajSzablonTowarow(okno.WybranySzablon);
            }
        }

        private void WczytajSzablonTowarow(SzablonTowarow szablon)
        {
            foreach (var w in TowaryWOfercie)
            {
                w.PropertyChanged -= TowarWiersz_PropertyChanged;
                w.TowarZmieniony -= TowarWiersz_TowarZmieniony;
                w.TypZmieniony -= TowarWiersz_TypZmieniony;
            }
            TowaryWOfercie.Clear();

            foreach (var towarSzablonu in szablon.Towary)
            {
                var towarBaza = DostepneTowary.FirstOrDefault(t => t.Id == towarSzablonu.TowarId);
                if (towarBaza != null)
                {
                    var wiersz = new TowarWiersz
                    {
                        Lp = TowaryWOfercie.Count + 1,
                        WybranyTowar = towarBaza,
                        Ilosc = towarSzablonu.DomyslnaIlosc,
                        Cena = towarSzablonu.DomyslnaCena,
                        Opakowanie = towarSzablonu.Opakowanie,
                        TypProduktuIndex = rbDomyslnyMrozony?.IsChecked == true ? 1 : 0
                    };
                    wiersz.PropertyChanged += TowarWiersz_PropertyChanged;
                    wiersz.TowarZmieniony += TowarWiersz_TowarZmieniony;
                    wiersz.TypZmieniony += TowarWiersz_TypZmieniony;
                    TowaryWOfercie.Add(wiersz);
                }
            }

            DodajNowyPustyWiersz();
            AktualizujPodsumowanieTowary();

            MessageBox.Show($"Wczytano szablon: {szablon.Nazwa}\nLiczba produktów: {szablon.Towary.Count}",
                "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // NOWY: Otwórz okno mapowania świeży-mrożony
        private void BtnMapowanie_Click(object sender, RoutedEventArgs e)
        {
            var okno = new MapowanieSwiezyMrozonyWindow(TowarySwiezy, TowaryMrozone);
            okno.Owner = this;
            okno.ShowDialog();
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
                // Pobierz zaktualizowane ceny z okna marż
                var zaktualizowaneTowary = okno.PobierzTowaryZCenami();
                
                foreach (var zaktualizowany in zaktualizowaneTowary)
                {
                    var wiersz = TowaryWOfercie.FirstOrDefault(w => w.TowarId == zaktualizowany.Id);
                    if (wiersz != null && zaktualizowany.CenaJednostkowa > 0)
                    {
                        wiersz.Cena = zaktualizowany.CenaJednostkowa;
                    }
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
                    // Pozwól przejść jeśli zaznaczono "Bez odbiorcy" lub wybrano co najmniej jednego odbiorcę
                    if (WybraniOdbiorcy.Count == 0 && chkBezOdbiorcy.IsChecked != true)
                    {
                        MessageBox.Show("Wybierz przynajmniej jednego odbiorcę lub zaznacz opcję 'Bez odbiorcy'.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        // PODSUMOWANIE I GENEROWANIE
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

            // Sprawdź czy to oferta bez odbiorcy
            if (chkBezOdbiorcy.IsChecked == true)
            {
                var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
                sp.Children.Add(new TextBlock { Text = "📋 Oferta ogólna", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(147, 51, 234)) });
                sp.Children.Add(new TextBlock { Text = "bez danych odbiorcy", FontSize = 11, Foreground = (SolidColorBrush)FindResource("LightTextBrush") });
                panelPodsumowanieOdbiorcy.Children.Add(sp);
            }
            else
            {
                foreach (var odbiorca in WybraniOdbiorcy)
                {
                    var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
                    sp.Children.Add(new TextBlock { Text = odbiorca.Nazwa, FontSize = 14, FontWeight = FontWeights.SemiBold });
                    sp.Children.Add(new TextBlock { Text = odbiorca.AdresPelny, FontSize = 11, Foreground = (SolidColorBrush)FindResource("LightTextBrush") });
                    if (!string.IsNullOrEmpty(odbiorca.NIP))
                        sp.Children.Add(new TextBlock { Text = $"NIP: {odbiorca.NIP}", FontSize = 11, Foreground = (SolidColorBrush)FindResource("LightTextBrush") });
                    panelPodsumowanieOdbiorcy.Children.Add(sp);
                }
            }

            txtPodTermin.Text = (cboTerminPlatnosci.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "-";
            txtPodWaznosc.Text = (cboWaznoscOferty.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "-";

            var produkty = TowaryWOfercie.Where(w => w.CzyWypelniony).ToList();
            dgPodsumowanieTowary.ItemsSource = produkty;
            txtPodLiczbaPozycji.Text = produkty.Count.ToString();
            decimal suma = produkty.Sum(w => w.Wartosc);
            txtPodSuma.Text = $"{suma:N2} zł";

            int dniWaznosci = int.Parse((cboWaznoscOferty.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "1");

            bool pokazCene = chkPdfPokazCene.IsChecked == true;
            bool pokazIlosc = chkPdfPokazIlosc.IsChecked == true;
            bool pokazOpakowanie = chkPdfPokazOpakowanie.IsChecked == true;

            txtEmailTresc.Text = GenerujTrescEmaila(produkty, dniWaznosci, suma, pokazCene, pokazIlosc, pokazOpakowanie);
        }

        private string GenerujTrescEmaila(List<TowarWiersz> produkty, int dniWaznosci, decimal suma, 
            bool pokazCene, bool pokazIlosc, bool pokazOpakowanie)
        {
            string dniSlowo = dniWaznosci == 1 ? "dzień" : "dni";
            bool pokazWartosc = pokazIlosc && pokazCene;
            
            var sb = new StringBuilder();
            sb.AppendLine("Szanowni Państwo,");
            sb.AppendLine();
            sb.AppendLine("W załączeniu przesyłam ofertę cenową.");
            sb.AppendLine($"Oferta ważna {dniWaznosci} {dniSlowo}.");
            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("              CENNIK PRODUKTÓW");
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine();

            int lp = 1;
            foreach (var p in produkty)
            {
                sb.AppendLine($"{lp}. {p.Nazwa} {p.TypProduktuEmoji}");
                
                if (pokazCene && p.Cena > 0)
                    sb.AppendLine($"   Cena: {p.Cena:N2} zł/kg");
                
                if (pokazIlosc && p.Ilosc > 0)
                    sb.AppendLine($"   Ilość: {p.Ilosc:N0} kg");
                
                if (pokazWartosc && p.Wartosc > 0)
                    sb.AppendLine($"   Wartość: {p.Wartosc:N2} zł");
                
                if (pokazOpakowanie)
                    sb.AppendLine($"   Opakowanie: {p.Opakowanie}");
                
                sb.AppendLine();
                lp++;
            }

            if (pokazWartosc && suma > 0)
            {
                sb.AppendLine("═══════════════════════════════════════");
                sb.AppendLine($"SUMA: {suma:N2} zł");
            }
            
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine("Ceny nie zawierają podatku VAT.");
            sb.AppendLine();
            sb.AppendLine("Z poważaniem,");
            sb.AppendLine(_nazwaOperatora);
            sb.AppendLine("Ubojnia Drobiu \"Piórkowscy\"");
            sb.AppendLine("Koziołki 40, 95-061 Dmosin");
            sb.AppendLine("Tel: +48 46 874 71 70");

            return sb.ToString();
        }

        private void BtnTylkoPDF_Click(object sender, RoutedEventArgs e) => GenerujPDF(false);
        private void BtnGenerujIWyslij_Click(object sender, RoutedEventArgs e) => GenerujPDF(true);

        private async void GenerujPDF(bool otworzEmail)
        {
            try
            {
                bool bezOdbiorcy = chkBezOdbiorcy.IsChecked == true;

                // Utwórz klienta - pustego jeśli bez odbiorcy
                KlientOferta klient;
                if (bezOdbiorcy)
                {
                    klient = new KlientOferta { Nazwa = "", NIP = "", Adres = "", KodPocztowy = "", Miejscowosc = "" };
                }
                else
                {
                    var odbiorca = WybraniOdbiorcy.First();
                    klient = odbiorca.ToKlientOferta();
                }

                int dniWaznosci = int.Parse((cboWaznoscOferty.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "1");

                var parametry = new ParametryOferty
                {
                    TerminPlatnosci = (cboTerminPlatnosci.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "1 dzień",
                    DniPlatnosci = int.Parse((cboTerminPlatnosci.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "1"),
                    DniWaznosci = dniWaznosci,
                    WalutaKonta = (cboKontoBankowe.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "PLN",
                    Jezyk = (cboJezykPDF.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "English" ? JezykOferty.English : JezykOferty.Polski,
                    TypLogo = (cboTypLogo.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "Dlugie" ? TypLogo.Dlugie : TypLogo.Okragle,
                    PokazOpakowanie = chkPdfPokazOpakowanie.IsChecked == true,
                    PokazCene = chkPdfPokazCene.IsChecked == true,
                    PokazIlosc = chkPdfPokazIlosc.IsChecked == true,
                    PokazTerminPlatnosci = chkPdfPokazTermin.IsChecked == true,
                    WystawiajacyNazwa = _nazwaOperatora,
                    WystawiajacyEmail = _emailOperatora,
                    WystawiajacyTelefon = _telefonOperatora,
                    BezOdbiorcy = bezOdbiorcy
                };

                var produkty = TowaryWOfercie
                    .Where(w => w.CzyWypelniony)
                    .Select(w => w.ToTowarOferta())
                    .ToList();

                string transport = rbTransportWlasny.IsChecked == true ? "Transport własny" : "Transport klienta";

                string nazwaPliku;
                if (bezOdbiorcy)
                {
                    nazwaPliku = $"Oferta_Ogolna_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                }
                else
                {
                    string nazwaKlienta = klient.Nazwa.Replace(" ", "_").Replace("\"", "").Replace("/", "_").Replace("\\", "_");
                    if (nazwaKlienta.Length > 30) nazwaKlienta = nazwaKlienta.Substring(0, 30);
                    nazwaPliku = $"Oferta_{nazwaKlienta}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                }
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Oferty");
                Directory.CreateDirectory(folder);
                string sciezka = Path.Combine(folder, nazwaPliku);

                var generator = new OfertaPDFGenerator();
                generator.GenerujPDF(sciezka, klient, produkty, txtNotatki.Text, transport, parametry);

                // Zapisz ofertę do bazy danych
                await ZapiszOferteDoBazyAsync(klient, produkty, parametry, txtNotatki.Text, transport, sciezka, nazwaPliku);

                if (otworzEmail)
                    OtworzEmailZZalacznikiem(sciezka, klient, dniWaznosci, produkty);
                else
                {
                    Process.Start(new ProcessStartInfo(sciezka) { UseShellExecute = true });
                    MessageBox.Show($"PDF wygenerowany:\n{sciezka}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // Generuj dla pozostałych odbiorców (tylko jeśli nie jest to oferta bez odbiorcy)
                if (!bezOdbiorcy && WybraniOdbiorcy.Count > 1)
                {
                    MessageBox.Show($"Wygenerowano dla pierwszego odbiorcy.\nGeneruję dla pozostałych {WybraniOdbiorcy.Count - 1}...", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);

                    for (int i = 1; i < WybraniOdbiorcy.Count; i++)
                    {
                        var kolejnyOdbiorca = WybraniOdbiorcy[i];
                        var kolejnyKlient = kolejnyOdbiorca.ToKlientOferta();
                        string nazwaKlienta = kolejnyKlient.Nazwa.Replace(" ", "_").Replace("\"", "").Replace("/", "_").Replace("\\", "_");
                        if (nazwaKlienta.Length > 30) nazwaKlienta = nazwaKlienta.Substring(0, 30);
                        nazwaPliku = $"Oferta_{nazwaKlienta}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                        sciezka = Path.Combine(folder, nazwaPliku);
                        generator.GenerujPDF(sciezka, kolejnyKlient, produkty, txtNotatki.Text, transport, parametry);

                        // Zapisz też kolejną ofertę do bazy
                        await ZapiszOferteDoBazyAsync(kolejnyKlient, produkty, parametry, txtNotatki.Text, transport, sciezka, nazwaPliku);
                    }

                    Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd generowania PDF: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OtworzEmailZZalacznikiem(string sciezkaPDF, KlientOferta klient, int dniWaznosci, List<TowarOferta> produkty)
        {
            try
            {
                bool pokazCene = chkPdfPokazCene.IsChecked == true;
                bool pokazIlosc = chkPdfPokazIlosc.IsChecked == true;
                bool pokazOpakowanie = chkPdfPokazOpakowanie.IsChecked == true;
                bool pokazWartosc = pokazIlosc && pokazCene;

                string temat = "Oferta cenowa - Piórkowscy";
                string dniSlowo = dniWaznosci == 1 ? "dzień" : "dni";

                var sb = new StringBuilder();
                sb.AppendLine("Szanowni Państwo,");
                sb.AppendLine();
                sb.AppendLine("W załączeniu przesyłam ofertę cenową.");
                sb.AppendLine($"Oferta ważna {dniWaznosci} {dniSlowo}.");
                sb.AppendLine();
                sb.AppendLine("═══════════════════════════════════════");
                sb.AppendLine("              CENNIK PRODUKTÓW");
                sb.AppendLine("═══════════════════════════════════════");
                sb.AppendLine();

                decimal suma = 0;
                int lp = 1;
                foreach (var p in produkty)
                {
                    sb.AppendLine($"{lp}. {p.Nazwa}");

                    if (pokazCene && p.CenaJednostkowa > 0)
                        sb.AppendLine($"   Cena: {p.CenaJednostkowa:N2} zł/kg");

                    if (pokazIlosc && p.Ilosc > 0)
                        sb.AppendLine($"   Ilość: {p.Ilosc:N0} kg");

                    decimal wartosc = p.Ilosc * p.CenaJednostkowa;

                    if (pokazWartosc && wartosc > 0)
                        sb.AppendLine($"   Wartość: {wartosc:N2} zł");

                    if (pokazOpakowanie)
                        sb.AppendLine($"   Opakowanie: {p.Opakowanie}");

                    sb.AppendLine();
                    suma += wartosc;
                    lp++;
                }

                if (pokazWartosc && suma > 0)
                {
                    sb.AppendLine("═══════════════════════════════════════");
                    sb.AppendLine($"SUMA: {suma:N2} zł");
                }

                sb.AppendLine("═══════════════════════════════════════");
                sb.AppendLine();
                sb.AppendLine("Ceny nie zawierają podatku VAT.");
                sb.AppendLine();
                sb.AppendLine("Z poważaniem,");
                sb.AppendLine(_nazwaOperatora);
                sb.AppendLine("Ubojnia Drobiu \"Piórkowscy\"");
                sb.AppendLine("Koziołki 40, 95-061 Dmosin");
                sb.AppendLine("Tel: +48 46 874 71 70");

                string tresc = sb.ToString();

                // Próbuj otworzyć Outlook z załącznikiem
                bool outlookOtworzony = SprobujOtworzycOutlookZZalacznikiem(temat, tresc, sciezkaPDF);

                if (!outlookOtworzony)
                {
                    // Fallback - użyj mailto i skopiuj ścieżkę do schowka
                    string mailtoUrl = $"mailto:?subject={Uri.EscapeDataString(temat)}&body={Uri.EscapeDataString(tresc)}";
                    Process.Start(new ProcessStartInfo(mailtoUrl) { UseShellExecute = true });

                    // Skopiuj ścieżkę PDF do schowka
                    Clipboard.SetText(sciezkaPDF);

                    // Otwórz PDF
                    Process.Start(new ProcessStartInfo(sciezkaPDF) { UseShellExecute = true });

                    MessageBox.Show($"Otwarto klienta email i PDF.\n\n📎 Ścieżka do PDF została skopiowana do schowka.\nWklej załącznik (Ctrl+V) w oknie email:\n{sciezkaPDF}",
                        "Email", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwierania email: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Próbuje otworzyć Microsoft Outlook z załącznikiem PDF
        /// </summary>
        private bool SprobujOtworzycOutlookZZalacznikiem(string temat, string tresc, string sciezkaPDF)
        {
            try
            {
                // Użyj dynamic aby uniknąć referencji do Outlook Interop
                Type outlookType = Type.GetTypeFromProgID("Outlook.Application");
                if (outlookType == null)
                    return false;

                dynamic outlookApp = Activator.CreateInstance(outlookType);
                dynamic mailItem = outlookApp.CreateItem(0); // olMailItem = 0

                mailItem.Subject = temat;
                mailItem.Body = tresc;

                // Dodaj załącznik PDF
                if (File.Exists(sciezkaPDF))
                {
                    mailItem.Attachments.Add(sciezkaPDF);
                }

                mailItem.Display(); // Otwórz okno nowej wiadomości

                MessageBox.Show("Otwarto Microsoft Outlook z załączonym PDF.\nWypełnij adres odbiorcy i wyślij.",
                    "📧 Email gotowy", MessageBoxButton.OK, MessageBoxImage.Information);

                return true;
            }
            catch
            {
                // Outlook nie jest zainstalowany lub wystąpił błąd
                return false;
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

        // =====================================================
        // ZAPIS OFERTY DO BAZY DANYCH
        // =====================================================

        private async Task ZapiszOferteDoBazyAsync(
            KlientOferta klient,
            List<TowarOferta> produkty,
            ParametryOferty parametry,
            string notatki,
            string transport,
            string sciezkaPliku,
            string nazwaPliku)
        {
            try
            {
                var (ofertaId, numerOferty) = await _ofertaRepository.ZapiszOferteAsync(
                    klient: klient,
                    produkty: produkty,
                    parametry: parametry,
                    notatki: notatki,
                    transport: transport,
                    handlowiecId: _userId,
                    handlowiecNazwa: _nazwaOperatora,
                    handlowiecEmail: _emailOperatora,
                    handlowiecTelefon: _telefonOperatora,
                    sciezkaPliku: sciezkaPliku,
                    nazwaPliku: nazwaPliku
                );

                System.Diagnostics.Debug.WriteLine($"✅ Oferta zapisana: {numerOferty} (ID: {ofertaId})");
            }
            catch (Exception ex)
            {
                // Loguj błąd ale nie przerywaj - PDF już wygenerowany
                System.Diagnostics.Debug.WriteLine($"❌ Błąd zapisu oferty do bazy: {ex.Message}");
                
                // Opcjonalnie: pokaż ostrzeżenie użytkownikowi
                // MessageBox.Show($"Uwaga: Oferta została wygenerowana, ale nie została zapisana w bazie danych.\n\nBłąd: {ex.Message}", 
                //     "Ostrzeżenie", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
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
