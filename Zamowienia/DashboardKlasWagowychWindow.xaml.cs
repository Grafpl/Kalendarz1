// Plik: DashboardKlasWagowychWindow.xaml.cs
// Dashboard do zarządzania rezerwacjami klas wagowych - WPF
#nullable enable
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Kalendarz1
{
    public partial class DashboardKlasWagowychWindow : Window
    {
        #region Stałe i kolory
        private static readonly string CONN_LIBRA = "Server=192.168.0.109;Database=LibraNet;User Id=sa;Password=Libra;TrustServerCertificate=True;";

        private static readonly Color COLOR_SUCCESS = Color.FromArgb(255, 22, 163, 74);
        private static readonly Color COLOR_WARNING = Color.FromArgb(255, 234, 88, 12);
        private static readonly Color COLOR_DANGER = Color.FromArgb(255, 220, 38, 38);

        private static readonly Dictionary<int, Color> KLASY_KOLORY = new() {
            { 5, Color.FromArgb(255, 220, 38, 38) },
            { 6, Color.FromArgb(255, 234, 88, 12) },
            { 7, Color.FromArgb(255, 202, 138, 4) },
            { 8, Color.FromArgb(255, 101, 163, 13) },
            { 9, Color.FromArgb(255, 22, 163, 74) },
            { 10, Color.FromArgb(255, 8, 145, 178) },
            { 11, Color.FromArgb(255, 37, 99, 235) },
            { 12, Color.FromArgb(255, 124, 58, 237) }
        };

        private const decimal KG_NA_POJEMNIK = 15m;
        #endregion

        #region Dane
        private DateTime _dataProdukcji = DateTime.Today;
        private Dictionary<int, int> _prognoza = new();
        private Dictionary<int, int> _sumaZajete = new();
        private List<ZamowienieKlasy> _zamowienia = new();
        private bool _blokujZmiany = false;
        
        // Słownik TextBoxów dla szybkiego dostępu
        private Dictionary<(int zamId, int klasa), TextBox> _textBoxy = new();
        private Dictionary<int, TextBlock> _sumyLabels = new();
        #endregion

        public string UserID { get; set; } = "";

        public DashboardKlasWagowychWindow()
        {
            InitializeComponent();

            for (int i = 5; i <= 12; i++)
            {
                _prognoza[i] = 0;
                _sumaZajete[i] = 0;
            }

            dpData.SelectedDate = DateTime.Today;
            
            Loaded += async (s, e) => await LoadDataAsync();
        }

        #region Event Handlers
        private async void DpData_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpData.SelectedDate.HasValue)
            {
                _dataProdukcji = dpData.SelectedDate.Value;
                await LoadDataAsync();
            }
        }

        private void BtnPoprzedni_Click(object sender, RoutedEventArgs e)
        {
            dpData.SelectedDate = (dpData.SelectedDate ?? DateTime.Today).AddDays(-1);
        }

        private void BtnNastepny_Click(object sender, RoutedEventArgs e)
        {
            dpData.SelectedDate = (dpData.SelectedDate ?? DateTime.Today).AddDays(1);
        }

        private void BtnDzisiaj_Click(object sender, RoutedEventArgs e)
        {
            dpData.SelectedDate = DateTime.Today;
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async void BtnZapiszWszystko_Click(object sender, RoutedEventArgs e)
        {
            await ZapiszWszystkieAsync();
        }
        #endregion

        #region Ładowanie danych
        private async Task LoadDataAsync()
        {
            txtStatus.Text = "⏳ Ładowanie danych...";
            borderStatus.Background = new SolidColorBrush(Color.FromArgb(255, 55, 79, 40));

            try
            {
                _dataProdukcji = dpData.SelectedDate ?? DateTime.Today;

                _prognoza = await PobierzPrognozePojemnikowAsync();
                _zamowienia = await PobierzZamowieniaNaDzienAsync();
                await PobierzIstniejaceRezerwacjeAsync();

                PrzeliczSumyZajete();

                BuildKlasyPodsumowanie();
                BuildListeZamowien();
                UpdateStatusBar();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"❌ Błąd: {ex.Message}";
                borderStatus.Background = new SolidColorBrush(COLOR_DANGER);
            }
        }

        private async Task<Dictionary<int, int>> PobierzPrognozePojemnikowAsync()
        {
            var prognoza = new Dictionary<int, int>();
            for (int i = 5; i <= 12; i++) prognoza[i] = 0;

            try
            {
                await using var cn = new SqlConnection(CONN_LIBRA);
                await cn.OpenAsync();

                var checkSql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'In0E'";
                await using var checkCmd = new SqlCommand(checkSql, cn);
                if ((int)await checkCmd.ExecuteScalarAsync() == 0)
                {
                    return GetDefaultPrognoza();
                }

                int dayOfWeek = (int)_dataProdukcji.DayOfWeek;

                var sql = @"
                    SELECT Klasa, AVG(Ilosc) as SredniaIlosc
                    FROM (
                        SELECT 
                            CASE 
                                WHEN ArticleID = 40 THEN 7
                                WHEN ArticleID BETWEEN 41 AND 48 THEN ArticleID - 34
                                ELSE 0
                            END as Klasa,
                            Quantity as Ilosc
                        FROM In0E
                        WHERE ArticleID BETWEEN 40 AND 48
                        AND Data >= DATEADD(day, -21, @Data)
                        AND Data < @Data
                        AND DATEPART(dw, Data) = @DayOfWeek
                    ) sub
                    WHERE Klasa BETWEEN 5 AND 12
                    GROUP BY Klasa";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Data", _dataProdukcji.Date);
                cmd.Parameters.AddWithValue("@DayOfWeek", dayOfWeek == 0 ? 7 : dayOfWeek);

                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int klasa = rd.GetInt32(0);
                    int ilosc = rd.IsDBNull(1) ? 0 : Convert.ToInt32(rd.GetDouble(1));
                    if (klasa >= 5 && klasa <= 12)
                        prognoza[klasa] = ilosc;
                }

                if (prognoza.Values.Sum() == 0)
                    return GetDefaultPrognoza();
            }
            catch
            {
                return GetDefaultPrognoza();
            }

            return prognoza;
        }

        private Dictionary<int, int> GetDefaultPrognoza() => new() {
            { 5, 158 }, { 6, 3413 }, { 7, 1254 }, { 8, 733 },
            { 9, 1025 }, { 10, 298 }, { 11, 14 }, { 12, 0 }
        };

        private async Task<List<ZamowienieKlasy>> PobierzZamowieniaNaDzienAsync()
        {
            var lista = new List<ZamowienieKlasy>();

            try
            {
                await using var cn = new SqlConnection(CONN_LIBRA);
                await cn.OpenAsync();

                var sql = @"
                    SELECT DISTINCT 
                        z.Id as ZamowienieId,
                        z.Odbiorca,
                        z.Handlowiec,
                        ISNULL(SUM(t.Ilosc), 0) as IloscKg
                    FROM ZamowieniaMieso z
                    LEFT JOIN ZamowieniaMiesoTowar t ON z.Id = t.ZamowienieId
                    WHERE z.DataOdbioru = @Data
                    AND z.Status != 'Anulowane'
                    AND t.IdKatalog = 67095
                    GROUP BY z.Id, z.Odbiorca, z.Handlowiec
                    HAVING SUM(t.Ilosc) > 0
                    ORDER BY z.Odbiorca";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Data", _dataProdukcji.Date);

                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    decimal iloscKg = rd.IsDBNull(3) ? 0 : rd.GetDecimal(3);
                    int pojemniki = (int)Math.Ceiling(iloscKg / KG_NA_POJEMNIK);

                    lista.Add(new ZamowienieKlasy {
                        ZamowienieId = rd.GetInt32(0),
                        Odbiorca = rd.IsDBNull(1) ? "Nieznany" : rd.GetString(1),
                        Handlowiec = rd.IsDBNull(2) ? "" : rd.GetString(2),
                        IloscKg = iloscKg,
                        IloscPojemnikow = pojemniki,
                        RozkladKlas = new Dictionary<int, int> {
                            { 5, 0 }, { 6, 0 }, { 7, 0 }, { 8, 0 },
                            { 9, 0 }, { 10, 0 }, { 11, 0 }, { 12, 0 }
                        }
                    });
                }

                // Dane testowe jeśli pusta lista
                if (lista.Count == 0)
                {
                    lista = GetTestData();
                }
            }
            catch
            {
                lista = GetTestData();
            }

            return lista;
        }

        private List<ZamowienieKlasy> GetTestData() => new() {
            new ZamowienieKlasy {
                ZamowienieId = 1, Odbiorca = "Damak", Handlowiec = "MK",
                IloscKg = 19800, IloscPojemnikow = 1320,
                RozkladKlas = new() { { 5, 0 }, { 6, 1320 }, { 7, 0 }, { 8, 0 }, { 9, 0 }, { 10, 0 }, { 11, 0 }, { 12, 0 } }
            },
            new ZamowienieKlasy {
                ZamowienieId = 2, Odbiorca = "Destan", Handlowiec = "PW",
                IloscKg = 4800, IloscPojemnikow = 320,
                RozkladKlas = new() { { 5, 0 }, { 6, 0 }, { 7, 22 }, { 8, 0 }, { 9, 0 }, { 10, 298 }, { 11, 0 }, { 12, 0 } }
            },
            new ZamowienieKlasy {
                ZamowienieId = 3, Odbiorca = "EUREKA S.C. HURTOWNIA DROBIU", Handlowiec = "KN",
                IloscKg = 2100, IloscPojemnikow = 140,
                RozkladKlas = new() { { 5, 0 }, { 6, 70 }, { 7, 70 }, { 8, 0 }, { 9, 0 }, { 10, 0 }, { 11, 0 }, { 12, 0 } }
            }
        };

        private async Task PobierzIstniejaceRezerwacjeAsync()
        {
            try
            {
                await using var cn = new SqlConnection(CONN_LIBRA);
                await cn.OpenAsync();

                var checkSql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RezerwacjeKlasWagowych'";
                await using var checkCmd = new SqlCommand(checkSql, cn);
                if ((int)await checkCmd.ExecuteScalarAsync() == 0) return;

                var sql = @"SELECT ZamowienieId, Klasa, IloscPojemnikow
                    FROM [dbo].[RezerwacjeKlasWagowych]
                    WHERE DataProdukcji = @Data AND Status = 'Aktywna'";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Data", _dataProdukcji.Date);

                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int zamId = rd.GetInt32(0);
                    int klasa = rd.GetInt32(1);
                    int ilosc = rd.GetInt32(2);

                    var zam = _zamowienia.FirstOrDefault(z => z.ZamowienieId == zamId);
                    if (zam != null && klasa >= 5 && klasa <= 12)
                    {
                        zam.RozkladKlas[klasa] = ilosc;
                    }
                }
            }
            catch { }
        }
        #endregion

        #region Budowanie UI
        private void BuildKlasyPodsumowanie()
        {
            pnlKlasyPodsumowanie.Children.Clear();

            foreach (int klasa in new[] { 5, 6, 7, 8, 9, 10, 11, 12 })
            {
                var card = CreateKlasaCard(klasa);
                pnlKlasyPodsumowanie.Children.Add(card);
            }

            // Karta RAZEM
            var cardRazem = CreateRazemCard();
            pnlKlasyPodsumowanie.Children.Add(cardRazem);
        }

        private Border CreateKlasaCard(int klasa)
        {
            int prognoza = _prognoza.GetValueOrDefault(klasa, 0);
            int zajete = _sumaZajete.GetValueOrDefault(klasa, 0);
            int wolne = prognoza - zajete;
            double procentZajete = prognoza > 0 ? zajete * 100.0 / prognoza : 0;

            var border = new Border {
                Style = (Style)FindResource("KlasaCardStyle")
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });

            // Kolorowy pasek na górze
            var topBar = new Border {
                Background = new SolidColorBrush(KLASY_KOLORY[klasa]),
                CornerRadius = new CornerRadius(6, 6, 0, 0)
            };
            Grid.SetRow(topBar, 0);
            grid.Children.Add(topBar);

            // Nagłówek klasy
            var lblKlasa = new TextBlock {
                Text = $"Klasa {klasa}",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(KLASY_KOLORY[klasa]),
                Margin = new Thickness(8, 4, 0, 0)
            };
            Grid.SetRow(lblKlasa, 1);
            grid.Children.Add(lblKlasa);

            // Zajęte / Prognoza
            Color statusColor = prognoza == 0 ? Colors.Gray :
                               wolne <= 0 ? COLOR_DANGER :
                               wolne < prognoza * 0.2 ? COLOR_WARNING : Color.FromArgb(255, 50, 60, 45);

            var lblStatus = new TextBlock {
                Text = prognoza > 0 ? $"{zajete} / {prognoza}" : "—",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(statusColor),
                Margin = new Thickness(8, 2, 0, 0)
            };
            Grid.SetRow(lblStatus, 2);
            grid.Children.Add(lblStatus);

            // Wolne
            if (prognoza > 0)
            {
                var lblWolne = new TextBlock {
                    Text = wolne <= 0 ? "⚠️ BRAK!" : $"wolne: {wolne}",
                    FontSize = 9,
                    FontWeight = wolne <= 0 ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = new SolidColorBrush(wolne <= 0 ? COLOR_DANGER : COLOR_SUCCESS),
                    Margin = new Thickness(8, 2, 0, 0)
                };
                Grid.SetRow(lblWolne, 3);
                grid.Children.Add(lblWolne);
            }

            // Pasek postępu na dole
            var progressBg = new Border {
                Background = new SolidColorBrush(Color.FromArgb(255, 235, 240, 230)),
                Height = 6,
                Margin = new Thickness(8, 0, 8, 2),
                CornerRadius = new CornerRadius(3),
                VerticalAlignment = VerticalAlignment.Bottom
            };

            var progressGrid = new Grid();
            progressBg.Child = progressGrid;

            if (prognoza > 0)
            {
                double fillPercent = Math.Min(procentZajete, 100);
                Color fillColor = procentZajete >= 100 ? COLOR_DANGER :
                                 procentZajete >= 80 ? COLOR_WARNING : COLOR_SUCCESS;

                var progressFill = new Border {
                    Background = new SolidColorBrush(fillColor),
                    CornerRadius = new CornerRadius(3),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = 114 * fillPercent / 100
                };
                progressGrid.Children.Add(progressFill);
            }

            Grid.SetRow(progressBg, 5);
            grid.Children.Add(progressBg);

            border.Child = grid;
            return border;
        }

        private Border CreateRazemCard()
        {
            int sumaPrognoza = _prognoza.Values.Sum();
            int sumaZajete = _sumaZajete.Values.Sum();
            int sumaWolne = sumaPrognoza - sumaZajete;

            var border = new Border {
                Background = new SolidColorBrush(Color.FromArgb(255, 55, 79, 40)),
                CornerRadius = new CornerRadius(6),
                Width = 145,
                Height = 90,
                Margin = new Thickness(10, 4, 4, 4)
            };

            var stack = new StackPanel { Margin = new Thickness(12, 10, 12, 10) };

            stack.Children.Add(new TextBlock {
                Text = "RAZEM",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 180, 200, 170))
            });

            stack.Children.Add(new TextBlock {
                Text = $"{sumaZajete} poj.",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 4, 0, 0)
            });

            stack.Children.Add(new TextBlock {
                Text = sumaWolne >= 0 ? $"wolne: {sumaWolne}" : $"BRAK: {-sumaWolne}",
                FontSize = 10,
                FontWeight = sumaWolne < 0 ? FontWeights.Bold : FontWeights.Normal,
                Foreground = new SolidColorBrush(sumaWolne < 0 ? 
                    Color.FromArgb(255, 255, 150, 150) : Color.FromArgb(255, 180, 220, 170)),
                Margin = new Thickness(0, 4, 0, 0)
            });

            border.Child = stack;
            return border;
        }

        private void BuildListeZamowien()
        {
            // Usuń stare wiersze (zostaw nagłówek)
            while (pnlZamowienia.Children.Count > 1)
            {
                pnlZamowienia.Children.RemoveAt(1);
            }

            _textBoxy.Clear();
            _sumyLabels.Clear();

            if (_zamowienia.Count == 0)
            {
                var lblEmpty = new TextBlock {
                    Text = "Brak zamówień kurczaka świeżego na ten dzień",
                    FontSize = 14,
                    FontStyle = FontStyles.Italic,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(20)
                };
                pnlZamowienia.Children.Add(lblEmpty);
                return;
            }

            foreach (var zam in _zamowienia)
            {
                var row = CreateZamowienieRow(zam);
                pnlZamowienia.Children.Add(row);
            }
        }

        private Border CreateZamowienieRow(ZamowienieKlasy zam)
        {
            var border = new Border {
                Style = (Style)FindResource("ZamowienieRowStyle"),
                Tag = zam
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            for (int i = 0; i < 8; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Kolumna klienta
            var stackKlient = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stackKlient.Children.Add(new TextBlock {
                Text = zam.Odbiorca,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("TextDark"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 200
            });
            stackKlient.Children.Add(new TextBlock {
                Text = $"{zam.IloscPojemnikow} poj. ({zam.IloscPojemnikow / 36m:N2} pal.)",
                FontSize = 10,
                Foreground = (Brush)FindResource("TextLight")
            });
            Grid.SetColumn(stackKlient, 0);
            grid.Children.Add(stackKlient);

            // Kolumny klas 5-12
            int colIndex = 1;
            foreach (int klasa in new[] { 5, 6, 7, 8, 9, 10, 11, 12 })
            {
                var txt = new TextBox {
                    Text = zam.RozkladKlas.GetValueOrDefault(klasa, 0).ToString(),
                    Style = (Style)FindResource("KlasaTextBoxStyle"),
                    Tag = new KlasaTagWpf { Zamowienie = zam, Klasa = klasa }
                };

                // Kolor tła
                UpdateTextBoxColor(txt, zam, klasa);

                txt.TextChanged += Txt_TextChanged;
                txt.PreviewTextInput += Txt_PreviewTextInput;
                txt.GotFocus += (s, e) => ((TextBox)s).SelectAll();

                Grid.SetColumn(txt, colIndex);
                grid.Children.Add(txt);

                _textBoxy[(zam.ZamowienieId, klasa)] = txt;
                colIndex++;
            }

            // Kolumna SUMA
            int suma = zam.RozkladKlas.Values.Sum();
            var lblSuma = new TextBlock {
                Text = suma.ToString(),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(
                    suma == zam.IloscPojemnikow ? COLOR_SUCCESS :
                    suma > zam.IloscPojemnikow ? COLOR_WARNING : Color.FromArgb(255, 100, 110, 95))
            };
            Grid.SetColumn(lblSuma, 9);
            grid.Children.Add(lblSuma);
            _sumyLabels[zam.ZamowienieId] = lblSuma;

            // Kolumna akcji
            var stackAkcje = new StackPanel { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var btnRowno = new Button { 
                Content = "⚖️", 
                Style = (Style)FindResource("SmallButtonStyle"),
                ToolTip = "Rozdziel równo",
                Tag = zam
            };
            btnRowno.Click += BtnRowno_Click;
            stackAkcje.Children.Add(btnRowno);

            var btnWgDost = new Button { 
                Content = "📊", 
                Style = (Style)FindResource("SmallButtonStyle"),
                ToolTip = "Wg dostępności",
                Tag = zam,
                Margin = new Thickness(4, 0, 0, 0)
            };
            btnWgDost.Click += BtnWgDostepnosci_Click;
            stackAkcje.Children.Add(btnWgDost);

            var btnWyczysc = new Button { 
                Content = "🗑️", 
                Style = (Style)FindResource("SmallButtonStyle"),
                ToolTip = "Wyczyść",
                Tag = zam,
                Margin = new Thickness(4, 0, 0, 0)
            };
            btnWyczysc.Click += BtnWyczysc_Click;
            stackAkcje.Children.Add(btnWyczysc);

            Grid.SetColumn(stackAkcje, 10);
            grid.Children.Add(stackAkcje);

            border.Child = grid;
            return border;
        }

        private void UpdateTextBoxColor(TextBox txt, ZamowienieKlasy zam, int klasa)
        {
            int wartosc = zam.RozkladKlas.GetValueOrDefault(klasa, 0);
            int wolne = _prognoza.GetValueOrDefault(klasa, 0) - _sumaZajete.GetValueOrDefault(klasa, 0);

            if (wolne < 0 && wartosc > 0)
            {
                txt.Background = new SolidColorBrush(Color.FromArgb(255, 255, 200, 200)); // Przekroczono
                txt.Foreground = new SolidColorBrush(COLOR_DANGER);
            }
            else if (wolne <= 0 && wartosc == 0 && _prognoza.GetValueOrDefault(klasa, 0) > 0)
            {
                txt.Background = new SolidColorBrush(Color.FromArgb(255, 255, 235, 235)); // Wyczerpane
                txt.Foreground = new SolidColorBrush(Colors.Gray);
            }
            else if (wartosc > 0)
            {
                txt.Background = new SolidColorBrush(Color.FromArgb(255, 230, 255, 230)); // OK
                txt.Foreground = new SolidColorBrush(KLASY_KOLORY[klasa]);
            }
            else
            {
                txt.Background = Brushes.White;
                txt.Foreground = new SolidColorBrush(Colors.Black);
            }
        }
        #endregion

        #region Obsługa zdarzeń TextBox
        private void Txt_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        }

        private void Txt_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_blokujZmiany) return;
            if (sender is not TextBox txt) return;
            if (txt.Tag is not KlasaTagWpf tag) return;

            int wartosc = 0;
            if (!string.IsNullOrWhiteSpace(txt.Text))
                int.TryParse(txt.Text, out wartosc);

            tag.Zamowienie.RozkladKlas[tag.Klasa] = wartosc;

            PrzeliczSumyZajete();
            BuildKlasyPodsumowanie();
            UpdateAllTextBoxColors();
            UpdateSumaLabel(tag.Zamowienie);
            UpdateStatusBar();
        }
        #endregion

        #region Przyciski akcji
        private void BtnRowno_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (btn.Tag is not ZamowienieKlasy zam) return;

            _blokujZmiany = true;

            int naKlase = zam.IloscPojemnikow / 8;
            int reszta = zam.IloscPojemnikow % 8;

            int idx = 0;
            foreach (int klasa in new[] { 5, 6, 7, 8, 9, 10, 11, 12 })
            {
                int wartosc = naKlase + (idx < reszta ? 1 : 0);
                zam.RozkladKlas[klasa] = wartosc;

                if (_textBoxy.TryGetValue((zam.ZamowienieId, klasa), out var txt))
                    txt.Text = wartosc.ToString();

                idx++;
            }

            _blokujZmiany = false;

            PrzeliczSumyZajete();
            BuildKlasyPodsumowanie();
            UpdateAllTextBoxColors();
            UpdateSumaLabel(zam);
            UpdateStatusBar();
        }

        private void BtnWgDostepnosci_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (btn.Tag is not ZamowienieKlasy zam) return;

            _blokujZmiany = true;

            // Oblicz wolne dla każdej klasy (bez tego zamówienia)
            var wolneBezemnie = new Dictionary<int, int>();
            for (int i = 5; i <= 12; i++)
            {
                int zajeteInnych = _sumaZajete.GetValueOrDefault(i, 0) - zam.RozkladKlas.GetValueOrDefault(i, 0);
                wolneBezemnie[i] = _prognoza.GetValueOrDefault(i, 0) - zajeteInnych;
            }

            int sumaWolnych = wolneBezemnie.Values.Where(v => v > 0).Sum();
            if (sumaWolnych > 0)
            {
                int doRozdzielenia = zam.IloscPojemnikow;
                foreach (int klasa in new[] { 5, 6, 7, 8, 9, 10, 11, 12 })
                {
                    int wartosc = 0;
                    if (wolneBezemnie[klasa] > 0)
                    {
                        wartosc = (int)Math.Round(zam.IloscPojemnikow * (double)wolneBezemnie[klasa] / sumaWolnych);
                        wartosc = Math.Min(wartosc, wolneBezemnie[klasa]);
                        wartosc = Math.Min(wartosc, doRozdzielenia);
                        doRozdzielenia -= wartosc;
                    }

                    zam.RozkladKlas[klasa] = wartosc;
                    if (_textBoxy.TryGetValue((zam.ZamowienieId, klasa), out var txt))
                        txt.Text = wartosc.ToString();
                }
            }

            _blokujZmiany = false;

            PrzeliczSumyZajete();
            BuildKlasyPodsumowanie();
            UpdateAllTextBoxColors();
            UpdateSumaLabel(zam);
            UpdateStatusBar();
        }

        private void BtnWyczysc_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (btn.Tag is not ZamowienieKlasy zam) return;

            _blokujZmiany = true;

            foreach (int klasa in new[] { 5, 6, 7, 8, 9, 10, 11, 12 })
            {
                zam.RozkladKlas[klasa] = 0;
                if (_textBoxy.TryGetValue((zam.ZamowienieId, klasa), out var txt))
                    txt.Text = "0";
            }

            _blokujZmiany = false;

            PrzeliczSumyZajete();
            BuildKlasyPodsumowanie();
            UpdateAllTextBoxColors();
            UpdateSumaLabel(zam);
            UpdateStatusBar();
        }
        #endregion

        #region Helpers
        private void PrzeliczSumyZajete()
        {
            for (int i = 5; i <= 12; i++)
            {
                _sumaZajete[i] = _zamowienia.Sum(z => z.RozkladKlas.GetValueOrDefault(i, 0));
            }
        }

        private void UpdateAllTextBoxColors()
        {
            foreach (var kvp in _textBoxy)
            {
                var zam = _zamowienia.FirstOrDefault(z => z.ZamowienieId == kvp.Key.zamId);
                if (zam != null)
                    UpdateTextBoxColor(kvp.Value, zam, kvp.Key.klasa);
            }
        }

        private void UpdateSumaLabel(ZamowienieKlasy zam)
        {
            if (_sumyLabels.TryGetValue(zam.ZamowienieId, out var lbl))
            {
                int suma = zam.RozkladKlas.Values.Sum();
                lbl.Text = suma.ToString();
                lbl.Foreground = new SolidColorBrush(
                    suma == zam.IloscPojemnikow ? COLOR_SUCCESS :
                    suma > zam.IloscPojemnikow ? COLOR_WARNING : Color.FromArgb(255, 100, 110, 95));
            }
        }

        private void UpdateStatusBar()
        {
            int sumaPrognoza = _prognoza.Values.Sum();
            int sumaZajete = _sumaZajete.Values.Sum();
            int sumaWolne = sumaPrognoza - sumaZajete;
            double procent = sumaPrognoza > 0 ? sumaZajete * 100.0 / sumaPrognoza : 0;

            var wyczerpane = new List<int>();
            for (int i = 5; i <= 12; i++)
            {
                int prog = _prognoza.GetValueOrDefault(i, 0);
                if (prog > 0 && _sumaZajete[i] >= prog)
                    wyczerpane.Add(i);
            }

            int nierozdzielone = _zamowienia.Count(z => z.RozkladKlas.Values.Sum() != z.IloscPojemnikow);

            string wyczerpaneText = wyczerpane.Count > 0
                ? $"  ⚠️ WYCZERPANE: Kl.{string.Join(", Kl.", wyczerpane)}"
                : "";

            string nierozdzieloneText = nierozdzielone > 0
                ? $"  📦 Nierozdzielone: {nierozdzielone}"
                : "";

            txtStatus.Text = $"📊 Zamówień: {_zamowienia.Count}  │  " +
                $"Zajęte: {sumaZajete} poj. ({procent:N1}%)  │  " +
                $"Wolne: {sumaWolne} poj.  │  " +
                $"Prognoza: {sumaPrognoza} poj." +
                wyczerpaneText + nierozdzieloneText;

            borderStatus.Background = new SolidColorBrush(
                wyczerpane.Count > 0 || nierozdzielone > 0 ? COLOR_WARNING : Color.FromArgb(255, 55, 79, 40));
        }
        #endregion

        #region Zapis
        private async Task ZapiszWszystkieAsync()
        {
            try
            {
                await using var cn = new SqlConnection(CONN_LIBRA);
                await cn.OpenAsync();

                await UtworzTabeleJesliNieIstniejeAsync(cn);

                foreach (var zam in _zamowienia)
                {
                    var delSql = @"DELETE FROM [dbo].[RezerwacjeKlasWagowych] 
                                   WHERE ZamowienieId = @ZamId AND DataProdukcji = @Data";
                    await using var delCmd = new SqlCommand(delSql, cn);
                    delCmd.Parameters.AddWithValue("@ZamId", zam.ZamowienieId);
                    delCmd.Parameters.AddWithValue("@Data", _dataProdukcji.Date);
                    await delCmd.ExecuteNonQueryAsync();

                    foreach (var kv in zam.RozkladKlas.Where(kv => kv.Value > 0))
                    {
                        var insSql = @"INSERT INTO [dbo].[RezerwacjeKlasWagowych] 
                            (ZamowienieId, DataProdukcji, Klasa, IloscPojemnikow, Odbiorca, Handlowiec, Status, DataUtworzenia, UtworzylUzytkownik)
                            VALUES (@ZamId, @Data, @Klasa, @Ilosc, @Odbiorca, @Handlowiec, 'Aktywna', GETDATE(), @User)";

                        await using var insCmd = new SqlCommand(insSql, cn);
                        insCmd.Parameters.AddWithValue("@ZamId", zam.ZamowienieId);
                        insCmd.Parameters.AddWithValue("@Data", _dataProdukcji.Date);
                        insCmd.Parameters.AddWithValue("@Klasa", kv.Key);
                        insCmd.Parameters.AddWithValue("@Ilosc", kv.Value);
                        insCmd.Parameters.AddWithValue("@Odbiorca", zam.Odbiorca);
                        insCmd.Parameters.AddWithValue("@Handlowiec", zam.Handlowiec ?? "");
                        insCmd.Parameters.AddWithValue("@User", UserID ?? "system");
                        await insCmd.ExecuteNonQueryAsync();
                    }
                }

                MessageBox.Show($"Zapisano rezerwacje dla {_zamowienia.Count} zamówień!",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task UtworzTabeleJesliNieIstniejeAsync(SqlConnection cn)
        {
            var sql = @"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RezerwacjeKlasWagowych')
                BEGIN
                    CREATE TABLE [dbo].[RezerwacjeKlasWagowych] (
                        [Id] INT IDENTITY(1,1) PRIMARY KEY,
                        [ZamowienieId] INT NOT NULL,
                        [DataProdukcji] DATE NOT NULL,
                        [Klasa] INT NOT NULL,
                        [IloscPojemnikow] INT NOT NULL,
                        [Odbiorca] NVARCHAR(200),
                        [Handlowiec] NVARCHAR(100),
                        [Status] NVARCHAR(50) DEFAULT 'Aktywna',
                        [DataUtworzenia] DATETIME DEFAULT GETDATE(),
                        [UtworzylUzytkownik] NVARCHAR(100)
                    )
                    CREATE INDEX IX_RezerwacjeKlasWagowych_Data ON [dbo].[RezerwacjeKlasWagowych](DataProdukcji)
                    CREATE INDEX IX_RezerwacjeKlasWagowych_Zamowienie ON [dbo].[RezerwacjeKlasWagowych](ZamowienieId)
                END";

            await using var cmd = new SqlCommand(sql, cn);
            await cmd.ExecuteNonQueryAsync();
        }
        #endregion
    }

    #region Helper classes
    public class ZamowienieKlasy
    {
        public int ZamowienieId { get; set; }
        public string Odbiorca { get; set; } = "";
        public string Handlowiec { get; set; } = "";
        public decimal IloscKg { get; set; }
        public int IloscPojemnikow { get; set; }
        public Dictionary<int, int> RozkladKlas { get; set; } = new();
    }

    public class KlasaTagWpf
    {
        public ZamowienieKlasy Zamowienie { get; set; } = null!;
        public int Klasa { get; set; }
    }
    #endregion
}
