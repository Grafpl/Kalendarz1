using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace Kalendarz1.OfertaCenowa
{
    /// <summary>
    /// Model odbiorcy oferty (z obu baz danych)
    /// </summary>
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

        // Źródło danych
        public string Zrodlo { get; set; } = "HANDEL"; // "HANDEL" lub "CRM"

        // Właściwości do wyświetlania
        public string ZrodloSkrot => Zrodlo switch
        {
            "HANDEL" => "HANDEL",
            "CRM" => "CRM",
            "RECZNY" => "RĘCZNY",
            _ => Zrodlo
        };

        public SolidColorBrush ZrodloBadgeBackground => Zrodlo switch
        {
            "HANDEL" => new SolidColorBrush(Color.FromRgb(235, 245, 255)),  // niebieski
            "CRM" => new SolidColorBrush(Color.FromRgb(255, 247, 237)),     // pomarańczowy
            "RECZNY" => new SolidColorBrush(Color.FromRgb(243, 232, 255)),  // fioletowy
            _ => new SolidColorBrush(Color.FromRgb(243, 244, 246))          // szary
        };

        public SolidColorBrush ZrodloBadgeForeground => Zrodlo switch
        {
            "HANDEL" => new SolidColorBrush(Color.FromRgb(59, 130, 246)),   // niebieski
            "CRM" => new SolidColorBrush(Color.FromRgb(249, 115, 22)),      // pomarańczowy
            "RECZNY" => new SolidColorBrush(Color.FromRgb(147, 51, 234)),   // fioletowy
            _ => new SolidColorBrush(Color.FromRgb(107, 114, 128))          // szary
        };

        public string AdresPelny => string.IsNullOrEmpty(Adres)
            ? $"{KodPocztowy} {Miejscowosc}".Trim()
            : $"{Adres}, {KodPocztowy} {Miejscowosc}".Trim().TrimEnd(',');

        public Visibility TelefonVisibility => string.IsNullOrEmpty(Telefon) ? Visibility.Collapsed : Visibility.Visible;
        public Visibility KontaktVisibility => string.IsNullOrEmpty(OsobaKontaktowa) ? Visibility.Collapsed : Visibility.Visible;

        // Konwersja do KlientOferta (dla generatora PDF)
        public KlientOferta ToKlientOferta()
        {
            return new KlientOferta
            {
                Id = Id,
                Nazwa = Nazwa,
                NIP = NIP,
                Adres = Adres,
                KodPocztowy = KodPocztowy,
                Miejscowosc = Miejscowosc,
                CzyReczny = false
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Rozszerzony model towaru z właściwościami do wyświetlania
    /// </summary>
    public class TowarOfertaWiersz : INotifyPropertyChanged
    {
        private int _lp;
        private int _id;
        private string _kod = "";
        private string _nazwa = "";
        private string _katalog = "";
        private decimal _ilosc;
        private decimal _cenaJednostkowa;
        private string _opakowanie = "E2";

        public int Lp { get => _lp; set { _lp = value; OnPropertyChanged(); } }
        public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }
        public string Kod { get => _kod; set { _kod = value; OnPropertyChanged(); } }
        public string Nazwa { get => _nazwa; set { _nazwa = value; OnPropertyChanged(); } }
        public string Katalog { get => _katalog; set { _katalog = value; OnPropertyChanged(); } }

        public decimal Ilosc
        {
            get => _ilosc;
            set { _ilosc = value; OnPropertyChanged(); OnPropertyChanged(nameof(Wartosc)); OnPropertyChanged(nameof(WartoscStr)); OnPropertyChanged(nameof(IloscStr)); }
        }

        public decimal CenaJednostkowa
        {
            get => _cenaJednostkowa;
            set { _cenaJednostkowa = value; OnPropertyChanged(); OnPropertyChanged(nameof(Wartosc)); OnPropertyChanged(nameof(WartoscStr)); OnPropertyChanged(nameof(CenaJednostkowaStr)); }
        }

        public string CenaJednostkowaStr
        {
            get => _cenaJednostkowa.ToString("N2");
            set
            {
                if (decimal.TryParse(value.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
                {
                    CenaJednostkowa = result;
                }
            }
        }

        public string Opakowanie { get => _opakowanie; set { _opakowanie = value; OnPropertyChanged(); } }

        public decimal Wartosc => Ilosc * CenaJednostkowa;
        public string WartoscStr => $"{Wartosc:N2} zł";
        public string IloscStr => $"{Ilosc:N0} kg";

        // Konwersja do TowarOferta (dla generatora PDF)
        public TowarOferta ToTowarOferta()
        {
            return new TowarOferta
            {
                Id = Id,
                Kod = Kod,
                Nazwa = Nazwa,
                Katalog = Katalog,
                Ilosc = Ilosc,
                CenaJednostkowa = CenaJednostkowa,
                Opakowanie = Opakowanie
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class OfertaHandlowaWindow : Window, INotifyPropertyChanged
    {
        // Connection strings
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private readonly string _connLibraNet = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // Kolekcje
        public ObservableCollection<OdbiorcaOferta> WynikiWyszukiwania { get; set; } = new();
        public ObservableCollection<OdbiorcaOferta> WybraniOdbiorcy { get; set; } = new();
        public ObservableCollection<TowarOferta> DostepneTowary { get; set; } = new();
        public ObservableCollection<TowarOferta> FiltrowaneTowary { get; set; } = new();
        public ObservableCollection<TowarOfertaWiersz> TowaryWOfercie { get; set; } = new();
        public List<string> OpakowanieLista { get; set; } = new() { "E2", "Karton", "Poliblok" };

        // Stan
        private int _aktualnyKrok = 1;
        private string _aktywnyKatalog = "67095"; // Świeże
        private readonly SzablonyManager _szablonyManager = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public OfertaHandlowaWindow() : this(null)
        {
        }

        public OfertaHandlowaWindow(KlientOferta? klient)
        {
            InitializeComponent();
            DataContext = this;

            // Inicjalizacja
            lstWynikiWyszukiwania.ItemsSource = WynikiWyszukiwania;
            dgTowary.ItemsSource = TowaryWOfercie;

            // Konfiguracja kolumny opakowania
            var opakownieColumn = dgTowary.Columns.FirstOrDefault(c => c.Header?.ToString() == "Opakowanie") as DataGridComboBoxColumn;
            if (opakownieColumn != null)
            {
                opakownieColumn.ItemsSource = OpakowanieLista;
            }

            // Event na zmianę kolekcji towarów
            TowaryWOfercie.CollectionChanged += (s, e) => OdswiezPodsumowanie();

            // Załaduj dane
            LoadDataAsync();

            // Szablony
            _szablonyManager.UtworzSzablonyPrzykladowe();

            // Jeśli przekazano klienta - dodaj go do wybranych
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

            AktualizujWidokKroku();
        }

        #region Ładowanie danych

        private async void LoadDataAsync()
        {
            try
            {
                await LoadTowaryAsync();
                FiltrujTowary();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
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
                await using var cmd = new SqlCommand(
                    "SELECT Id, Kod, Nazwa, katalog FROM [HANDEL].[HM].[TW] WHERE katalog IN ('67095', '67153') ORDER BY Kod ASC", cn);
                await using var rd = await cmd.ExecuteReaderAsync();

                while (await rd.ReadAsync())
                {
                    string kod = rd["Kod"]?.ToString() ?? "";
                    if (excludedProducts.Any(excluded => kod.ToUpper().Contains(excluded)))
                        continue;

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
                MessageBox.Show($"Błąd ładowania towarów: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FiltrujTowary()
        {
            FiltrowaneTowary.Clear();
            foreach (var towar in DostepneTowary.Where(t => t.Katalog == _aktywnyKatalog))
            {
                FiltrowaneTowary.Add(towar);
            }
            if (cboNowyProdukt != null)
                cboNowyProdukt.ItemsSource = FiltrowaneTowary;
        }

        #endregion

        #region Wyszukiwanie odbiorców

        private void TxtSzukajOdbiorcy_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                WyszukajOdbiorcow();
            }
        }

        private void TxtSzukajOdbiorcy_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Opcjonalnie: auto-search po 3 znakach
        }

        private void BtnSzukaj_Click(object sender, RoutedEventArgs e)
        {
            WyszukajOdbiorcow();
        }

        private void RbZrodlo_Changed(object sender, RoutedEventArgs e)
        {
            // Przy zmianie źródła można odświeżyć wyniki
        }

        private async void WyszukajOdbiorcow()
        {
            string fraza = txtSzukajOdbiorcy.Text.Trim();
            if (string.IsNullOrEmpty(fraza) || fraza.Length < 2)
            {
                MessageBox.Show("Wpisz co najmniej 2 znaki", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            WynikiWyszukiwania.Clear();
            placeholderBrakWynikow.Visibility = Visibility.Collapsed;

            try
            {
                bool szukajHandel = rbZrodloWszystkie.IsChecked == true || rbZrodloHandel.IsChecked == true;
                bool szukajCRM = rbZrodloWszystkie.IsChecked == true || rbZrodloCRM.IsChecked == true;

                // Szukaj w Handel (STContractors)
                if (szukajHandel)
                {
                    await SzukajWHandelAsync(fraza);
                }

                // Szukaj w CRM (OdbiorcyCRM)
                if (szukajCRM)
                {
                    await SzukajWCRMAsync(fraza);
                }

                if (WynikiWyszukiwania.Count == 0)
                {
                    placeholderBrakWynikow.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wyszukiwania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SzukajWHandelAsync(string fraza)
        {
            const string sql = @"
                SELECT TOP 50 
                    c.Id, c.Name AS Nazwa, ISNULL(c.NIP, '') AS NIP, 
                    ISNULL(poa.Street, '') AS Adres,
                    ISNULL(poa.PostCode, '') AS KodPocztowy, 
                    ISNULL(poa.Place, '') AS Miejscowosc
                FROM [HANDEL].[SSCommon].[STContractors] c
                LEFT JOIN [HANDEL].[SSCommon].[STPostOfficeAddresses] poa 
                    ON poa.ContactGuid = c.ContactGuid AND poa.AddressName = N'adres domyślny'
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
                Debug.WriteLine($"Błąd szukania w Handel: {ex.Message}");
            }
        }

        private async Task SzukajWCRMAsync(string fraza)
        {
            const string sql = @"
                SELECT TOP 50 
                    ID, NAZWA, KOD, MIASTO, ULICA, NUMER, NR_LOK, TELEFON_K,
                    Imie, Nazwisko, Stanowisko, Wojewodztwo, Status
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
                    string osobaKontaktowa = $"{imie} {nazwisko}".Trim();

                    WynikiWyszukiwania.Add(new OdbiorcaOferta
                    {
                        Id = rd["ID"]?.ToString() ?? "",
                        Nazwa = rd["NAZWA"]?.ToString() ?? "",
                        NIP = "", // CRM nie ma NIP
                        Adres = adres.Trim(),
                        KodPocztowy = rd["KOD"]?.ToString() ?? "",
                        Miejscowosc = rd["MIASTO"]?.ToString() ?? "",
                        Telefon = rd["TELEFON_K"]?.ToString() ?? "",
                        OsobaKontaktowa = osobaKontaktowa,
                        Stanowisko = rd["Stanowisko"]?.ToString() ?? "",
                        Wojewodztwo = rd["Wojewodztwo"]?.ToString() ?? "",
                        Status = rd["Status"]?.ToString() ?? "",
                        Zrodlo = "CRM"
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd szukania w CRM: {ex.Message}");
            }
        }

        private void LstWynikiWyszukiwania_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstWynikiWyszukiwania.SelectedItem is OdbiorcaOferta odbiorca)
            {
                DodajOdbiorce(odbiorca);
            }
        }

        private void BtnDodajOdbiorce_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is OdbiorcaOferta odbiorca)
            {
                DodajOdbiorce(odbiorca);
            }
        }

        private void DodajOdbiorce(OdbiorcaOferta odbiorca)
        {
            // Sprawdź czy już nie dodany
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
            // Ukryj/pokaż placeholder
            placeholderBrakWybranych.Visibility = WybraniOdbiorcy.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            // Aktualizuj licznik
            txtLiczbaOdbiorcow.Text = WybraniOdbiorcy.Count.ToString();

            // Aktualizuj tryb
            txtTrybOdbiorcow.Text = WybraniOdbiorcy.Count switch
            {
                0 => "Tryb: brak odbiorcy",
                1 => "Tryb: pojedynczy odbiorca",
                _ => $"Tryb: wielu odbiorców ({WybraniOdbiorcy.Count})"
            };

            // Odbuduj listę wizualną
            // Usuń wszystkie poza placeholderem
            var doUsuniecia = panelWybraniOdbiorcy.Children.Cast<UIElement>()
                .Where(c => c != placeholderBrakWybranych).ToList();
            foreach (var el in doUsuniecia)
                panelWybraniOdbiorcy.Children.Remove(el);

            // Dodaj karty odbiorców
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
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Lewa strona - dane
            var stackDane = new StackPanel();

            // Badge źródła
            var badgePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            var badge = new Border
            {
                Background = odbiorca.ZrodloBadgeBackground,
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 2, 5, 2),
                Margin = new Thickness(0, 0, 8, 0)
            };
            badge.Child = new TextBlock
            {
                Text = odbiorca.ZrodloSkrot,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = odbiorca.ZrodloBadgeForeground
            };
            badgePanel.Children.Add(badge);

            if (!string.IsNullOrEmpty(odbiorca.Status))
            {
                badgePanel.Children.Add(new TextBlock
                {
                    Text = odbiorca.Status,
                    FontSize = 10,
                    Foreground = (SolidColorBrush)FindResource("LightTextBrush"),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
            stackDane.Children.Add(badgePanel);

            // Nazwa
            stackDane.Children.Add(new TextBlock
            {
                Text = odbiorca.Nazwa,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            // NIP (jeśli jest)
            if (!string.IsNullOrEmpty(odbiorca.NIP))
            {
                stackDane.Children.Add(new TextBlock
                {
                    Text = $"NIP: {odbiorca.NIP}",
                    FontSize = 11,
                    Foreground = (SolidColorBrush)FindResource("LightTextBrush")
                });
            }

            // Adres
            stackDane.Children.Add(new TextBlock
            {
                Text = odbiorca.AdresPelny,
                FontSize = 11,
                Foreground = (SolidColorBrush)FindResource("LightTextBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            // Telefon i osoba kontaktowa
            if (!string.IsNullOrEmpty(odbiorca.Telefon) || !string.IsNullOrEmpty(odbiorca.OsobaKontaktowa))
            {
                var kontaktPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
                if (!string.IsNullOrEmpty(odbiorca.Telefon))
                {
                    kontaktPanel.Children.Add(new TextBlock
                    {
                        Text = $"📞 {odbiorca.Telefon}",
                        FontSize = 10,
                        Foreground = (SolidColorBrush)FindResource("PrimaryGreenBrush"),
                        Margin = new Thickness(0, 0, 10, 0)
                    });
                }
                if (!string.IsNullOrEmpty(odbiorca.OsobaKontaktowa))
                {
                    kontaktPanel.Children.Add(new TextBlock
                    {
                        Text = $"👤 {odbiorca.OsobaKontaktowa}",
                        FontSize = 10,
                        Foreground = (SolidColorBrush)FindResource("LightTextBrush")
                    });
                }
                stackDane.Children.Add(kontaktPanel);
            }

            Grid.SetColumn(stackDane, 0);
            grid.Children.Add(stackDane);

            // Prawa strona - przycisk usuń
            var btnUsun = new Button
            {
                Content = "❌",
                Style = (Style)FindResource("SmallButtonStyle"),
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Top,
                Tag = odbiorca
            };
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
            // Otwórz okno ręcznego wprowadzania
            var okno = new WprowadzOdbiorceRecznieWindow();
            okno.Owner = this;
            if (okno.ShowDialog() == true && okno.Odbiorca != null)
            {
                DodajOdbiorce(okno.Odbiorca);
            }
        }

        #endregion

        #region Produkty

        private void RbTypProduktu_Checked(object sender, RoutedEventArgs e)
        {
            _aktywnyKatalog = rbSwiezy?.IsChecked == true ? "67095" : "67153";
            FiltrujTowary();
        }

        private void CboNowyProdukt_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Nic nie robimy - dodajemy przez przycisk
        }

        private void BtnDodajProdukt_Click(object sender, RoutedEventArgs e)
        {
            if (cboNowyProdukt.SelectedItem is TowarOferta wybrany)
            {
                // Sprawdź czy już nie dodany
                if (TowaryWOfercie.Any(t => t.Id == wybrany.Id))
                {
                    MessageBox.Show("Ten produkt jest już na liście.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var nowyWiersz = new TowarOfertaWiersz
                {
                    Lp = TowaryWOfercie.Count + 1,
                    Id = wybrany.Id,
                    Kod = wybrany.Kod,
                    Nazwa = wybrany.Nazwa,
                    Katalog = wybrany.Katalog,
                    Ilosc = 0,
                    CenaJednostkowa = 0,
                    Opakowanie = "E2"
                };

                // Subskrybuj zmiany
                nowyWiersz.PropertyChanged += TowarWiersz_PropertyChanged;

                TowaryWOfercie.Add(nowyWiersz);
                cboNowyProdukt.SelectedItem = null;
                cboNowyProdukt.Text = "";

                OdswiezPodsumowanie();
            }
        }

        private void TowarWiersz_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TowarOfertaWiersz.Wartosc) ||
                e.PropertyName == nameof(TowarOfertaWiersz.Ilosc) ||
                e.PropertyName == nameof(TowarOfertaWiersz.CenaJednostkowa))
            {
                OdswiezPodsumowanie();
            }
        }

        private void BtnUsunProdukt_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TowarOfertaWiersz towar)
            {
                towar.PropertyChanged -= TowarWiersz_PropertyChanged;
                TowaryWOfercie.Remove(towar);

                // Przenumeruj
                int lp = 1;
                foreach (var t in TowaryWOfercie)
                {
                    t.Lp = lp++;
                }

                OdswiezPodsumowanie();
            }
        }

        private void OdswiezPodsumowanie()
        {
            decimal suma = TowaryWOfercie.Sum(t => t.Wartosc);
            int liczba = TowaryWOfercie.Count;

            txtLiczbaPozycji.Text = liczba.ToString();
            txtSumaTowary.Text = $"{suma:N2} zł";
            txtWartoscCalkowita.Text = $"{suma:N2} zł";
        }

        #endregion

        #region Szablony

        private void BtnWczytajSzablonTowarow_Click(object sender, RoutedEventArgs e)
        {
            var szablony = _szablonyManager.WczytajSzablonyTowarow();
            if (!szablony.Any())
            {
                MessageBox.Show("Brak zapisanych szablonów towarów.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var okno = new WyborSzablonuWindow(szablony.Cast<object>().ToList(), "Wybierz szablon towarów");
            if (okno.ShowDialog() == true && okno.WybranyIndex >= 0)
            {
                var szablon = szablony[okno.WybranyIndex];

                // Wyczyść obecne
                foreach (var t in TowaryWOfercie)
                    t.PropertyChanged -= TowarWiersz_PropertyChanged;
                TowaryWOfercie.Clear();

                // Dodaj z szablonu
                int lp = 1;
                foreach (var towarSzablonu in szablon.Towary)
                {
                    var towarBaza = DostepneTowary.FirstOrDefault(t => t.Id == towarSzablonu.TowarId);
                    if (towarBaza != null)
                    {
                        var wiersz = new TowarOfertaWiersz
                        {
                            Lp = lp++,
                            Id = towarBaza.Id,
                            Kod = towarBaza.Kod,
                            Nazwa = towarBaza.Nazwa,
                            Katalog = towarBaza.Katalog,
                            Ilosc = towarSzablonu.DomyslnaIlosc,
                            CenaJednostkowa = towarSzablonu.DomyslnaCena,
                            Opakowanie = towarSzablonu.Opakowanie
                        };
                        wiersz.PropertyChanged += TowarWiersz_PropertyChanged;
                        TowaryWOfercie.Add(wiersz);
                    }
                }

                OdswiezPodsumowanie();
            }
        }

        private void BtnZarzadzajSzablonamiTowarow_Click(object sender, RoutedEventArgs e)
        {
            var okno = new SzablonTowarowWindow(DostepneTowary.ToList());
            okno.Owner = this;
            okno.ShowDialog();
        }

        private void BtnWczytajSzablonParametrow_Click(object sender, RoutedEventArgs e)
        {
            var szablony = _szablonyManager.WczytajSzablonyParametrow();
            if (!szablony.Any())
            {
                MessageBox.Show("Brak zapisanych szablonów parametrów.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var okno = new WyborSzablonuWindow(szablony.Cast<object>().ToList(), "Wybierz szablon parametrów");
            if (okno.ShowDialog() == true && okno.WybranyIndex >= 0)
            {
                var szablon = szablony[okno.WybranyIndex];
                ZastosujSzablonParametrow(szablon);
            }
        }

        private void ZastosujSzablonParametrow(SzablonParametrow szablon)
        {
            // Termin płatności
            foreach (ComboBoxItem item in cboTerminPlatnosci.Items)
            {
                if (item.Tag?.ToString() == szablon.DniPlatnosci.ToString())
                {
                    cboTerminPlatnosci.SelectedItem = item;
                    break;
                }
            }

            // Konto
            foreach (ComboBoxItem item in cboKontoBankowe.Items)
            {
                if (item.Tag?.ToString() == szablon.WalutaKonta)
                {
                    cboKontoBankowe.SelectedItem = item;
                    break;
                }
            }

            // Transport
            rbTransportWlasny.IsChecked = szablon.TransportTyp == "wlasny";
            rbTransportKlienta.IsChecked = szablon.TransportTyp == "klienta";

            // Język
            foreach (ComboBoxItem item in cboJezykPDF.Items)
            {
                if (item.Tag?.ToString() == szablon.Jezyk.ToString())
                {
                    cboJezykPDF.SelectedItem = item;
                    break;
                }
            }

            // Logo
            foreach (ComboBoxItem item in cboTypLogo.Items)
            {
                if (item.Tag?.ToString() == szablon.TypLogo.ToString())
                {
                    cboTypLogo.SelectedItem = item;
                    break;
                }
            }

            // Widoczność
            chkPdfPokazOpakowanie.IsChecked = szablon.PokazOpakowanie;
            chkPdfPokazCene.IsChecked = szablon.PokazCene;
            chkPdfPokazIlosc.IsChecked = szablon.PokazIlosc;
            chkPdfPokazTermin.IsChecked = szablon.PokazTerminPlatnosci;

            // Notatka
            txtNotatki.Text = szablon.NotatkaCustom;
        }

        #endregion

        #region Nawigacja

        private void AktualizujWidokKroku()
        {
            panelKrok1.Visibility = _aktualnyKrok == 1 ? Visibility.Visible : Visibility.Collapsed;
            panelKrok2.Visibility = _aktualnyKrok == 2 ? Visibility.Visible : Visibility.Collapsed;
            panelKrok3.Visibility = _aktualnyKrok == 3 ? Visibility.Visible : Visibility.Collapsed;
            panelKrok4.Visibility = _aktualnyKrok == 4 ? Visibility.Visible : Visibility.Collapsed;

            if (_aktualnyKrok == 4)
            {
                AktualizujPodsumowanie();
            }

            AktualizujWskaznikiKrokow();
            btnWstecz.IsEnabled = _aktualnyKrok > 1;
            btnDalej.Visibility = _aktualnyKrok < 4 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AktualizujWskaznikiKrokow()
        {
            var zielony = (SolidColorBrush)FindResource("PrimaryGreenBrush");
            var szary = new SolidColorBrush(Color.FromRgb(229, 231, 235));

            borderKrok1.Background = _aktualnyKrok >= 1 ? zielony : szary;
            txtKrok1.Foreground = _aktualnyKrok >= 1 ? zielony : (SolidColorBrush)FindResource("LightTextBrush");
            lineKrok1.Background = _aktualnyKrok > 1 ? zielony : szary;

            borderKrok2.Background = _aktualnyKrok >= 2 ? zielony : szary;
            txtKrok2.Foreground = _aktualnyKrok >= 2 ? zielony : (SolidColorBrush)FindResource("LightTextBrush");
            lineKrok2.Background = _aktualnyKrok > 2 ? zielony : szary;

            borderKrok3.Background = _aktualnyKrok >= 3 ? zielony : szary;
            txtKrok3.Foreground = _aktualnyKrok >= 3 ? zielony : (SolidColorBrush)FindResource("LightTextBrush");
            lineKrok3.Background = _aktualnyKrok > 3 ? zielony : szary;

            borderKrok4.Background = _aktualnyKrok >= 4 ? zielony : szary;
            txtKrok4.Foreground = _aktualnyKrok >= 4 ? zielony : (SolidColorBrush)FindResource("LightTextBrush");
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
                    if (TowaryWOfercie.Count == 0)
                    {
                        MessageBox.Show("Dodaj przynajmniej jeden produkt do oferty.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                    return true;

                default:
                    return true;
            }
        }

        #endregion

        #region Podsumowanie i generowanie

        private void AktualizujPodsumowanie()
        {
            // Odbiorcy
            panelPodsumowanieOdbiorcy.Children.Clear();
            foreach (var odbiorca in WybraniOdbiorcy)
            {
                var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                sp.Children.Add(new TextBlock
                {
                    Text = odbiorca.Nazwa,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold
                });
                sp.Children.Add(new TextBlock
                {
                    Text = $"{odbiorca.AdresPelny}",
                    FontSize = 11,
                    Foreground = (SolidColorBrush)FindResource("LightTextBrush")
                });
                if (!string.IsNullOrEmpty(odbiorca.NIP))
                {
                    sp.Children.Add(new TextBlock
                    {
                        Text = $"NIP: {odbiorca.NIP}",
                        FontSize = 11,
                        Foreground = (SolidColorBrush)FindResource("LightTextBrush")
                    });
                }
                panelPodsumowanieOdbiorcy.Children.Add(sp);
            }

            // Parametry
            txtPodTermin.Text = (cboTerminPlatnosci.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "-";
            txtPodKonto.Text = (cboKontoBankowe.SelectedItem as ComboBoxItem)?.Tag.ToString() ?? "-";
            txtPodTransport.Text = rbTransportWlasny.IsChecked == true ? "Własny" : "Klienta";
            txtPodJezyk.Text = (cboJezykPDF.SelectedItem as ComboBoxItem)?.Tag.ToString() ?? "Polski";

            // Produkty
            dgPodsumowanieTowary.ItemsSource = TowaryWOfercie;
            txtPodLiczbaPozycji.Text = TowaryWOfercie.Count.ToString();

            decimal suma = TowaryWOfercie.Sum(t => t.Wartosc);
            txtPodSuma.Text = $"{suma:N2} zł";

            // Email
            int dniTermin = int.Parse((cboTerminPlatnosci.SelectedItem as ComboBoxItem)?.Tag.ToString() ?? "1");
            string dniSlowo = dniTermin == 1 ? "dzień" : "dni";

            txtEmailTresc.Text = $@"Szanowni Państwo,

W załączeniu przesyłamy ofertę cenową na produkty drobiowe.
Oferta ważna jest przez {dniTermin} {dniSlowo}.

W razie pytań pozostajemy do dyspozycji.

Z poważaniem,
Zespół Handlowy
Ubojnia Drobiu ""Piórkowscy""";
        }

        private void BtnTylkoPDF_Click(object sender, RoutedEventArgs e)
        {
            GenerujPDF(false);
        }

        private void BtnGenerujIWyslij_Click(object sender, RoutedEventArgs e)
        {
            GenerujPDF(true);
        }

        private void GenerujPDF(bool otworzEmail)
        {
            try
            {
                // Weź pierwszego odbiorcę (dla wielu generuj osobne PDFy)
                var odbiorca = WybraniOdbiorcy.First();
                var klient = odbiorca.ToKlientOferta();

                // Przygotuj parametry
                var parametry = new ParametryOferty
                {
                    TerminPlatnosci = (cboTerminPlatnosci.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "1 dzień",
                    DniPlatnosci = int.Parse((cboTerminPlatnosci.SelectedItem as ComboBoxItem)?.Tag.ToString() ?? "1"),
                    WalutaKonta = (cboKontoBankowe.SelectedItem as ComboBoxItem)?.Tag.ToString() ?? "PLN",
                    Jezyk = (cboJezykPDF.SelectedItem as ComboBoxItem)?.Tag.ToString() == "English" ? JezykOferty.English : JezykOferty.Polski,
                    TypLogo = (cboTypLogo.SelectedItem as ComboBoxItem)?.Tag.ToString() == "Dlugie" ? TypLogo.Dlugie : TypLogo.Okragle,
                    PokazOpakowanie = chkPdfPokazOpakowanie.IsChecked == true,
                    PokazCene = chkPdfPokazCene.IsChecked == true,
                    PokazIlosc = chkPdfPokazIlosc.IsChecked == true,
                    PokazTerminPlatnosci = chkPdfPokazTermin.IsChecked == true
                };

                // Produkty
                var produkty = TowaryWOfercie.Select(t => t.ToTowarOferta()).ToList();

                // Transport
                string transport = rbTransportWlasny.IsChecked == true ? "Transport własny" : "Transport klienta";

                // Ścieżka
                string nazwaKlienta = klient.Nazwa.Replace(" ", "_").Replace("\"", "").Replace("/", "_").Replace("\\", "_");
                if (nazwaKlienta.Length > 30) nazwaKlienta = nazwaKlienta.Substring(0, 30);
                string nazwaPliku = $"Oferta_{nazwaKlienta}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Oferty");
                Directory.CreateDirectory(folder);
                string sciezka = Path.Combine(folder, nazwaPliku);

                // Generuj
                var generator = new OfertaPDFGenerator();
                generator.GenerujPDF(sciezka, klient, produkty, txtNotatki.Text, transport, parametry);

                if (otworzEmail)
                {
                    OtworzEmailZZalacznikiem(sciezka, klient, parametry.DniPlatnosci);
                }
                else
                {
                    Process.Start(new ProcessStartInfo(sciezka) { UseShellExecute = true });
                    MessageBox.Show($"PDF został wygenerowany:\n{sciezka}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // Jeśli wielu odbiorców - generuj dla pozostałych
                if (WybraniOdbiorcy.Count > 1)
                {
                    MessageBox.Show($"Wygenerowano PDF dla pierwszego odbiorcy.\nDla pozostałych {WybraniOdbiorcy.Count - 1} odbiorców PDFy zostaną wygenerowane oddzielnie.",
                        "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);

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

                    // Otwórz folder
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
                string temat = Uri.EscapeDataString($"Oferta cenowa - Ubojnia Drobiu \"Piórkowscy\"");

                string dniSlowo = dniWaznosci == 1 ? "dzień" : "dni";
                string tresc = Uri.EscapeDataString(
$@"Szanowni Państwo,

W załączeniu przesyłamy ofertę cenową na produkty drobiowe.
Oferta ważna jest przez {dniWaznosci} {dniSlowo}.

W razie pytań pozostajemy do dyspozycji.

Z poważaniem,
Zespół Handlowy
Ubojnia Drobiu ""Piórkowscy""
Koziółki, Polska
Tel: +48 24 254 00 00");

                string mailtoUrl = $"mailto:?subject={temat}&body={tresc}";

                Process.Start(new ProcessStartInfo(mailtoUrl) { UseShellExecute = true });
                Process.Start(new ProcessStartInfo(sciezkaPDF) { UseShellExecute = true });

                MessageBox.Show(
                    $"Otwarto klienta poczty i PDF.\n\nDołącz plik PDF jako załącznik:\n{sciezkaPDF}",
                    "Email gotowy",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się otworzyć klienta poczty: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion

        #region Obsługa okna

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion
    }
}