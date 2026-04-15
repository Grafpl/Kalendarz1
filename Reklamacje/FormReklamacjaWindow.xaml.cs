using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kalendarz1.Reklamacje
{
    public partial class FormReklamacjaWindow : Window
    {
        private string connectionStringHandel;
        private string connectionStringLibraNet;

        private int idDokumentu;
        private int idKontrahenta;
        private string numerDokumentu;
        private string nazwaKontrahenta;
        private string userId;

        // Gdy ustawione — formularz jest "przypisany" do tej korekty, nie ma wyboru
        private int? przypisanaKorektaId;
        private string przypisanaKorektaNumer;
        private DateTime? przypisanaKorektaData;
        private decimal? przypisanaKorektaWartosc;
        private decimal? przypisanaKorektaKg;

        // ID nowo utworzonej reklamacji - do automatycznego linku
        public int IdUtworzonejReklamacji { get; private set; }

        private ObservableCollection<TowarReklamacji> towary = new ObservableCollection<TowarReklamacji>();
        private ObservableCollection<PartiaDostawcy> partie = new ObservableCollection<PartiaDostawcy>();
        private ObservableCollection<KorektaItem> korekty = new ObservableCollection<KorektaItem>();
        private List<string> sciezkiZdjec = new List<string>();

        public bool ReklamacjaZapisana { get; private set; } = false;

        private const string DefaultLibraNetConnString = ReklamacjeConnectionStrings.LibraNet;

        public FormReklamacjaWindow(string connStringHandel, int dokId, int kontrId, string nrDok, string nazwaKontr, string user, string connStringLibraNet = null)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            connectionStringHandel = connStringHandel;
            connectionStringLibraNet = connStringLibraNet ?? DefaultLibraNetConnString;
            idDokumentu = dokId;
            idKontrahenta = kontrId;
            numerDokumentu = nrDok;
            nazwaKontrahenta = nazwaKontr;
            userId = user;

            txtKontrahent.Text = nazwaKontrahenta;
            txtFaktura.Text = numerDokumentu;

            dgTowary.ItemsSource = towary;
            dgPartie.ItemsSource = partie;
            dgKorekty.ItemsSource = korekty;
        }

        // Konstruktor "przypiety do korekty" — handlowiec uzupelnia info dla istniejacej korekty
        // Otwiera sie formularz na fakturze bazowej, z ukryta sekcja wyboru korekty,
        // i przy zapisie automatycznie powiazuje sie z przekazana korekta
        public FormReklamacjaWindow(string connStringHandel, int idFakturyBazowej, int kontrId,
            string nrFakturyBazowej, string nazwaKontr, string user, string connStringLibraNet,
            int idKorekty, string nrKorekty, DateTime? dataKorekty, decimal? wartoscKorekty, decimal? kgKorekty)
            : this(connStringHandel, idFakturyBazowej, kontrId, nrFakturyBazowej, nazwaKontr, user, connStringLibraNet)
        {
            przypisanaKorektaId = idKorekty;
            przypisanaKorektaNumer = nrKorekty;
            przypisanaKorektaData = dataKorekty;
            przypisanaKorektaWartosc = wartoscKorekty;
            przypisanaKorektaKg = kgKorekty;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WczytajTowary();
            WczytajPartie();

            // Gdy formularz jest "przypiety do korekty" — NIE laduje listy korekt,
            // pokazuje widok informacyjny (zero wyboru)
            if (przypisanaKorektaId.HasValue && przypisanaKorektaId.Value > 0)
            {
                PokazPrzypisanaKorekte();
            }
            else
            {
                WczytajKorekty();
            }

            AktualizujLiczniki();
            UstawPodkategorieISzablony();
        }

        // Zastap widok listy korekt — widokiem info "POWIAZANA Z KOREKTA: xyz"
        private void PokazPrzypisanaKorekte()
        {
            // Tytul okna
            Title = $"Zgloszenie reklamacji do korekty {przypisanaKorektaNumer}";

            // Ukryj DataGrid korekt + etykiete listy
            if (dgKorekty != null) dgKorekty.Visibility = Visibility.Collapsed;
            if (txtKorektyInfo != null) txtKorektyInfo.Visibility = Visibility.Collapsed;

            // Naglowek prawej kolumny zmien na info o przypisanej korekcie
            var parent = dgKorekty?.Parent as Border;
            var grid = parent?.Parent as Grid;
            if (grid == null) return;

            // Zastap DataGrid informacyjnym panelem
            var infoCard = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3E0")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12")),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 6, 0, 0)
            };
            var sp = new StackPanel();

            sp.Children.Add(new TextBlock
            {
                Text = "✓ POWIAZANA Z KOREKTA",
                FontSize = 11, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E67E22")),
                Margin = new Thickness(0, 0, 0, 10)
            });

            sp.Children.Add(new TextBlock
            {
                Text = "Numer korekty",
                FontSize = 9, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6")),
                FontWeight = FontWeights.SemiBold
            });
            sp.Children.Add(new TextBlock
            {
                Text = przypisanaKorektaNumer ?? "-",
                FontSize = 15, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C3E50")),
                Margin = new Thickness(0, 2, 0, 10)
            });

            if (przypisanaKorektaData.HasValue)
            {
                sp.Children.Add(new TextBlock
                {
                    Text = "Data wystawienia",
                    FontSize = 9, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6")),
                    FontWeight = FontWeights.SemiBold
                });
                sp.Children.Add(new TextBlock
                {
                    Text = przypisanaKorektaData.Value.ToString("dd.MM.yyyy"),
                    FontSize = 13, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C3E50")),
                    Margin = new Thickness(0, 2, 0, 10)
                });
            }

            if (przypisanaKorektaKg.HasValue && przypisanaKorektaKg.Value != 0)
            {
                sp.Children.Add(new TextBlock
                {
                    Text = "Kg z korekty",
                    FontSize = 9, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6")),
                    FontWeight = FontWeights.SemiBold
                });
                sp.Children.Add(new TextBlock
                {
                    Text = $"{przypisanaKorektaKg.Value:#,##0.00} kg",
                    FontSize = 13, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C3E50")),
                    Margin = new Thickness(0, 2, 0, 10)
                });
            }

            if (przypisanaKorektaWartosc.HasValue)
            {
                sp.Children.Add(new TextBlock
                {
                    Text = "Wartosc netto korekty",
                    FontSize = 9, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6")),
                    FontWeight = FontWeights.SemiBold
                });
                sp.Children.Add(new TextBlock
                {
                    Text = $"{przypisanaKorektaWartosc.Value:#,##0.00} zl",
                    FontSize = 13, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")),
                    Margin = new Thickness(0, 2, 0, 14)
                });
            }

            // Separator
            sp.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5CBA7")),
                Margin = new Thickness(0, 0, 0, 12)
            });

            sp.Children.Add(new TextBlock
            {
                Text = "Po zapisaniu reklamacja zostanie automatycznie powiazana z ta korekta — nie trzeba nic wybierac.",
                FontSize = 10.5, FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D")),
                TextWrapping = TextWrapping.Wrap
            });

            infoCard.Child = sp;
            Grid.SetRow(infoCard, 2);
            grid.Children.Add(infoCard);

            // Ukryj panel "wybrana korekta" jesli istnieje
            if (panelWybranaKorekta != null) panelWybranaKorekta.Visibility = Visibility.Collapsed;
        }

        // ============================================================
        // Korekty kontrahenta z HANDEL
        // ============================================================
        private void WczytajKorekty()
        {
            korekty.Clear();
            try
            {
                using (var connH = new SqlConnection(connectionStringHandel))
                {
                    connH.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT DK.id, DK.kod, DK.data,
                               ABS(ISNULL(DK.walNetto, 0)) AS Wartosc,
                               ABS(ISNULL((SELECT SUM(DP.ilosc) FROM [HANDEL].[HM].[DP] DP WHERE DP.super = DK.id), 0)) AS SumaKg
                        FROM [HANDEL].[HM].[DK] DK
                        WHERE DK.khid = @Khid
                          AND DK.seria IN ('sFKS', 'sFKSB', 'sFWK')
                          AND DK.anulowany = 0
                          AND DK.data >= DATEADD(DAY, -90, GETDATE())
                        ORDER BY DK.data DESC", connH))
                    {
                        cmd.Parameters.AddWithValue("@Khid", idKontrahenta);
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                // Sprawdz czy ta korekta juz jest powiazana w LibraNet
                                int idDok = r.GetInt32(0);
                                bool juzPowiazana = false;
                                try
                                {
                                    using (var connL = new SqlConnection(connectionStringLibraNet))
                                    {
                                        connL.Open();
                                        using (var cmdL = new SqlCommand(
                                            "SELECT COUNT(*) FROM [dbo].[Reklamacje] WHERE IdDokumentu = @Id AND TypReklamacji = 'Faktura korygujaca' AND PowiazanaReklamacjaId IS NOT NULL AND PowiazanaReklamacjaId > 0", connL))
                                        {
                                            cmdL.Parameters.AddWithValue("@Id", idDok);
                                            juzPowiazana = Convert.ToInt32(cmdL.ExecuteScalar()) > 0;
                                        }
                                    }
                                }
                                catch { }

                                if (!juzPowiazana)
                                {
                                    korekty.Add(new KorektaItem
                                    {
                                        IdDokumentu = idDok,
                                        NumerDokumentu = r.IsDBNull(1) ? "" : r.GetString(1),
                                        Data = r.GetDateTime(2),
                                        Wartosc = r.IsDBNull(3) ? 0m : Convert.ToDecimal(r.GetValue(3)),
                                        SumaKg = r.IsDBNull(4) ? 0m : Convert.ToDecimal(r.GetValue(4))
                                    });
                                }
                            }
                        }
                    }
                }

                txtKorektyInfo.Text = korekty.Count > 0
                    ? $"{korekty.Count} niepowiazanych korekt (ostatnie 90 dni)"
                    : "Brak korekt dla tego kontrahenta";
            }
            catch
            {
                txtKorektyInfo.Text = "Nie udalo sie pobrac korekt";
            }
        }

        private void RadioKorekta_Click(object sender, RoutedEventArgs e)
        {
            var wybrana = korekty.FirstOrDefault(k => k.IsSelected);
            if (wybrana != null)
            {
                panelWybranaKorekta.Visibility = Visibility.Visible;
                txtWybranaKorekta.Text = $"{wybrana.NumerDokumentu}  |  {wybrana.Data:dd.MM.yyyy}  |  {wybrana.SumaKg:#,##0} kg  |  {wybrana.Wartosc:#,##0} zl";
            }
            else
            {
                panelWybranaKorekta.Visibility = Visibility.Collapsed;
            }
        }

        // ============================================================
        // Podkategorie + szablony opisow wg typu reklamacji
        // ============================================================
        private static readonly Dictionary<string, string[]> PodkategorieWgTypu = new Dictionary<string, string[]>
        {
            ["Jakosc produktu"] = new[]
            {
                "Zly zapach",
                "Niewlasciwy wyglad",
                "Zepsuty produkt",
                "Zla temperatura / rozmrozony",
                "Niewlasciwa konsystencja",
                "Obcy posmak",
                "Zanieczyszczenie"
            },
            ["Ilosc / Brak towaru"] = new[]
            {
                "Mniejsza waga niz na fakturze",
                "Brak sztuk w dostawie",
                "Wiecej niz zamowiono",
                "Niepelna ilosc opakowan"
            },
            ["Uszkodzenie w transporcie"] = new[]
            {
                "Uszkodzony karton",
                "Uszkodzone opakowanie jednostkowe",
                "Zalanie / zawilgocenie",
                "Rozlane / wyciek",
                "Uszkodzenie mechaniczne produktu"
            },
            ["Termin waznosci"] = new[]
            {
                "Produkt po terminie",
                "Krotki termin waznosci",
                "Brak oznaczenia terminu"
            },
            ["Niezgodnosc z zamowieniem"] = new[]
            {
                "Inny produkt niz zamowiony",
                "Inne opakowanie / gramatura",
                "Niewlasciwa marka / rodzaj",
                "Pomylka dokumentowa"
            },
            ["Inne"] = new[] { "Inne - opisz w polu ponizej" }
        };

        private void CmbTypReklamacji_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UstawPodkategorieISzablony();
        }

        private void UstawPodkategorieISzablony()
        {
            if (cmbPodkategoria == null || wrapSzablony == null) return;

            string typ = (cmbTypReklamacji.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Inne";
            if (!PodkategorieWgTypu.TryGetValue(typ, out string[] podkategorie))
                podkategorie = new[] { "Inne" };

            // Uzupelnij ComboBox podkategorii
            cmbPodkategoria.Items.Clear();
            foreach (var p in podkategorie)
                cmbPodkategoria.Items.Add(new ComboBoxItem { Content = p });
            if (cmbPodkategoria.Items.Count > 0) cmbPodkategoria.SelectedIndex = 0;

            // Zbuduj chipy szablonow
            wrapSzablony.Children.Clear();
            foreach (var tekst in podkategorie)
            {
                var chipBorder = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F4FD")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B3D7F2")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(10, 4, 10, 4),
                    Margin = new Thickness(0, 0, 6, 6),
                    Cursor = Cursors.Hand
                };
                var chipText = new TextBlock
                {
                    Text = tekst,
                    FontSize = 10.5,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1565C0"))
                };
                chipBorder.Child = chipText;

                string captured = tekst;
                chipBorder.MouseLeftButtonUp += (s, ev) =>
                {
                    // Dopisz szablon do opisu (nowa linia jesli juz cos jest)
                    if (string.IsNullOrWhiteSpace(txtOpis.Text))
                        txtOpis.Text = captured;
                    else
                        txtOpis.Text = txtOpis.Text.TrimEnd() + Environment.NewLine + "- " + captured;
                    txtOpis.Focus();
                    txtOpis.CaretIndex = txtOpis.Text.Length;
                };
                chipBorder.MouseEnter += (s, ev) =>
                {
                    chipBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D0E7F7"));
                };
                chipBorder.MouseLeave += (s, ev) =>
                {
                    chipBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F4FD"));
                };

                wrapSzablony.Children.Add(chipBorder);
            }
        }

        private void WczytajTowary()
        {
            try
            {
                bool tryPrzypiety = przypisanaKorektaId.HasValue && przypisanaKorektaId.Value > 0;

                // TRYB PRZYPIETY: najpierw sprobuj ReklamacjeTowary (LibraNet) — tam sa dane z sync
                if (tryPrzypiety)
                {
                    bool udaloSieZLibraNet = false;
                    try
                    {
                        using (var conn = new SqlConnection(connectionStringLibraNet))
                        {
                            conn.Open();
                            // Znajdz Id rekordu korekty w LibraNet po IdDokumentu z Symfonii
                            int idRekKorekty = 0;
                            using (var cmdR = new SqlCommand(
                                "SELECT TOP 1 Id FROM [dbo].[Reklamacje] WHERE IdDokumentu = @IdDok AND TypReklamacji = 'Faktura korygujaca'", conn))
                            {
                                cmdR.Parameters.AddWithValue("@IdDok", przypisanaKorektaId.Value);
                                var r = cmdR.ExecuteScalar();
                                if (r != null && r != DBNull.Value) idRekKorekty = Convert.ToInt32(r);
                            }

                            if (idRekKorekty > 0)
                            {
                                using (var cmd = new SqlCommand(@"
                                    SELECT IdTowaru AS ID, Symbol, Nazwa,
                                           ABS(ISNULL(Waga, 0)) AS Waga,
                                           ABS(ISNULL(Cena, 0)) AS Cena,
                                           ABS(ISNULL(Wartosc, 0)) AS Wartosc
                                    FROM [dbo].[ReklamacjeTowary]
                                    WHERE IdReklamacji = @IdR
                                    ORDER BY Id", conn))
                                {
                                    cmd.Parameters.AddWithValue("@IdR", idRekKorekty);
                                    using (var reader = cmd.ExecuteReader())
                                    {
                                        towary.Clear();
                                        while (reader.Read())
                                        {
                                            towary.Add(new TowarReklamacji
                                            {
                                                ID = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                                                Symbol = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                                Nazwa = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                                Waga = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                                                Cena = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                                                IsSelected = true // wszystkie zaznaczone
                                            });
                                        }
                                    }
                                }
                                udaloSieZLibraNet = towary.Count > 0;
                            }
                        }
                    }
                    catch { /* fallback do HANDEL */ }

                    if (udaloSieZLibraNet)
                    {
                        AktualizujLiczniki();
                        return;
                    }
                }

                // FALLBACK / tryb normalny: towary z HANDEL
                using (SqlConnection conn = new SqlConnection(connectionStringHandel))
                {
                    conn.Open();
                    int zrodloDok = tryPrzypiety ? przypisanaKorektaId.Value : idDokumentu;

                    // W trybie przypietym bierzemy ABS (korekta ma ujemne), w normalnym zwykle
                    string query = tryPrzypiety
                        ? @"SELECT DP.id, DP.kod,
                                ISNULL(TW.nazwa, TW.kod) AS Nazwa,
                                CAST(ABS(ISNULL(DP.ilosc, 0)) AS DECIMAL(10,2)) AS Waga,
                                CAST(ABS(ISNULL(DP.cena, 0)) AS DECIMAL(10,2)) AS Cena,
                                CAST(ABS(ISNULL(DP.ilosc, 0) * ISNULL(DP.cena, 0)) AS DECIMAL(10,2)) AS Wartosc
                            FROM [HM].[DP] DP
                            LEFT JOIN [HM].[TW] TW ON DP.idtw = TW.ID
                            WHERE DP.super = @IdDokumentu
                            ORDER BY DP.lp"
                        : @"SELECT DP.id, DP.kod,
                                ISNULL(TW.nazwa, TW.kod) AS Nazwa,
                                CAST(ISNULL(DP.ilosc, 0) AS DECIMAL(10,2)) AS Waga,
                                CAST(ISNULL(DP.cena, 0) AS DECIMAL(10,2)) AS Cena,
                                CAST(ISNULL(DP.ilosc, 0) * ISNULL(DP.cena, 0) AS DECIMAL(10,2)) AS Wartosc
                            FROM [HM].[DP] DP
                            LEFT JOIN [HM].[TW] TW ON DP.idtw = TW.ID
                            WHERE DP.super = @IdDokumentu
                            ORDER BY DP.lp";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@IdDokumentu", zrodloDok);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            towary.Clear();
                            while (reader.Read())
                            {
                                towary.Add(new TowarReklamacji
                                {
                                    ID = reader.GetInt32(0),
                                    Symbol = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                    Nazwa = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    Waga = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                                    Cena = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                                    IsSelected = tryPrzypiety // auto-zaznaczone dla korekt
                                });
                            }
                        }
                    }
                }

                AktualizujLiczniki();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania towarów:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WczytajPartie()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionStringLibraNet))
                {
                    conn.Open();

                    // Sprawdź czy tabela istnieje
                    string checkTable = @"
                        SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                        WHERE TABLE_NAME = 'PartiaDostawca'";

                    using (SqlCommand cmdCheck = new SqlCommand(checkTable, conn))
                    {
                        int tableExists = Convert.ToInt32(cmdCheck.ExecuteScalar());
                        if (tableExists == 0)
                        {
                            txtPartieInfo.Text = "Tabela partii nie istnieje";
                            return;
                        }
                    }

                    string query = @"
                        SELECT
                            CAST([guid] AS NVARCHAR(100)) AS GuidStr,
                            [Partia],
                            [CustomerID],
                            [CustomerName],
                            CONVERT(VARCHAR, [CreateData], 104) + ' ' + LEFT(CAST([CreateGodzina] AS VARCHAR), 8) AS DataUtw
                        FROM [dbo].[PartiaDostawca]
                        WHERE [CreateData] >= DATEADD(DAY, -14, GETDATE())
                        ORDER BY [CreateData] DESC, [CreateGodzina] DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            partie.Clear();
                            while (reader.Read())
                            {
                                string guidStr = reader.IsDBNull(0) ? "" : reader.GetString(0);
                                Guid parsedGuid = Guid.Empty;
                                Guid.TryParse(guidStr, out parsedGuid);

                                partie.Add(new PartiaDostawcy
                                {
                                    GuidPartii = parsedGuid,
                                    GuidPartiiStr = guidStr,
                                    NrPartii = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                    IdDostawcy = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    NazwaDostawcy = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    DataUtworzenia = reader.IsDBNull(4) ? "" : reader.GetString(4)
                                });
                            }
                        }
                    }

                    if (partie.Count == 0)
                    {
                        txtPartieInfo.Text = "Brak partii z ostatnich 14 dni";
                    }
                }
            }
            catch (Exception ex)
            {
                txtPartieInfo.Text = $"Błąd: {ex.Message}";
            }
        }

        // Obsługa checkboxów dla towarów
        private void ChkTowar_Click(object sender, RoutedEventArgs e)
        {
            AktualizujLiczniki();
        }

        private void DgTowary_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Po edycji kg odswiezamy sumy
            Dispatcher.BeginInvoke(new Action(() =>
            {
                dgTowary.Items.Refresh();
                AktualizujLiczniki();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ChkWszystkieTowary_Click(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            bool isChecked = checkBox?.IsChecked ?? false;
            foreach (var towar in towary)
            {
                towar.IsSelected = isChecked;
            }
            AktualizujLiczniki();
        }

        // Obsługa checkboxów dla partii
        private void ChkPartia_Click(object sender, RoutedEventArgs e)
        {
            AktualizujLiczniki();
        }

        private void ChkWszystkiePartie_Click(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            bool isChecked = checkBox?.IsChecked ?? false;
            foreach (var partia in partie)
            {
                partia.IsSelected = isChecked;
            }
            AktualizujLiczniki();
        }

        private void AktualizujLiczniki()
        {
            int liczbaTowary = towary.Count(t => t.IsSelected);
            int liczbaPartii = partie.Count(p => p.IsSelected);
            int liczbaZdjec = sciezkiZdjec.Count;

            txtLicznikTowary.Text = $"{liczbaTowary} towar(ów)";
            txtLicznikPartie.Text = $"{liczbaPartii} parti(i)";
            txtLicznikZdjecia.Text = $"{liczbaZdjec} zdjęć";

            // Suma kg i wartości
            decimal sumaKg = towary.Where(t => t.IsSelected).Sum(t => t.Waga);
            decimal sumaWartosc = towary.Where(t => t.IsSelected).Sum(t => t.Wartosc);
            txtSumaKg.Text = $"{sumaKg:N2} kg";
            txtSumaWartosc.Text = $"{sumaWartosc:N2} zł";
        }

        private void ListZdjecia_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listZdjecia.SelectedIndex >= 0 && listZdjecia.SelectedIndex < sciezkiZdjec.Count)
            {
                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(sciezkiZdjec[listZdjecia.SelectedIndex]);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    imgPodglad.Source = bitmap;
                    txtBrakZdjecia.Visibility = Visibility.Collapsed;
                    btnUsunZdjecie.IsEnabled = true;
                }
                catch
                {
                    imgPodglad.Source = null;
                    txtBrakZdjecia.Visibility = Visibility.Visible;
                }
            }
            else
            {
                imgPodglad.Source = null;
                txtBrakZdjecia.Visibility = Visibility.Visible;
                btnUsunZdjecie.IsEnabled = false;
            }
        }

        private void ImgPodglad_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (listZdjecia.SelectedIndex >= 0 && listZdjecia.SelectedIndex < sciezkiZdjec.Count)
            {
                // Otwórz okno z powiększonym podglądem
                var previewWindow = new Window
                {
                    Title = "Podgląd zdjęcia - kliknij aby zamknąć",
                    WindowState = WindowState.Maximized,
                    WindowStyle = WindowStyle.None,
                    Background = System.Windows.Media.Brushes.Black,
                    Cursor = Cursors.Hand
                };

                var image = new Image
                {
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    Margin = new Thickness(20)
                };

                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(sciezkiZdjec[listZdjecia.SelectedIndex]);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    image.Source = bitmap;
                }
                catch
                {
                    return;
                }

                previewWindow.Content = image;
                previewWindow.MouseLeftButtonDown += (s, args) => previewWindow.Close();
                previewWindow.KeyDown += (s, args) =>
                {
                    if (args.Key == Key.Escape) previewWindow.Close();
                };

                previewWindow.ShowDialog();
            }
        }

        private void BtnDodajZdjecia_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "Pliki graficzne|*.jpg;*.jpeg;*.png;*.bmp;*.gif|Wszystkie pliki|*.*",
                Multiselect = true,
                Title = "Wybierz zdjęcia do reklamacji"
            };

            if (ofd.ShowDialog() == true)
            {
                foreach (string plik in ofd.FileNames)
                {
                    if (!sciezkiZdjec.Contains(plik))
                    {
                        sciezkiZdjec.Add(plik);
                        listZdjecia.Items.Add(Path.GetFileName(plik));
                    }
                }
                AktualizujLiczniki();

                // Automatycznie zaznacz pierwsze zdjęcie
                if (listZdjecia.Items.Count > 0 && listZdjecia.SelectedIndex < 0)
                {
                    listZdjecia.SelectedIndex = 0;
                }
            }
        }

        private void BtnUsunZdjecie_Click(object sender, RoutedEventArgs e)
        {
            if (listZdjecia.SelectedIndex >= 0)
            {
                int index = listZdjecia.SelectedIndex;
                sciezkiZdjec.RemoveAt(index);
                listZdjecia.Items.RemoveAt(index);
                imgPodglad.Source = null;
                txtBrakZdjecia.Visibility = Visibility.Visible;
                btnUsunZdjecie.IsEnabled = false;
                AktualizujLiczniki();
            }
        }

        // ========================================
        // DRAG & DROP ZDJEC
        // ========================================

        private static readonly HashSet<string> dozwoloneRozszerzenia = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif"
        };

        private void ZdjeciaPanel_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var pliki = (string[])e.Data.GetData(DataFormats.FileDrop);
                bool maObrazki = pliki.Any(f => dozwoloneRozszerzenia.Contains(Path.GetExtension(f)));
                e.Effects = maObrazki ? DragDropEffects.Copy : DragDropEffects.None;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void ZdjeciaPanel_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var pliki = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (pliki.Any(f => dozwoloneRozszerzenia.Contains(Path.GetExtension(f))))
                    dropOverlay.Visibility = Visibility.Visible;
            }
        }

        private void ZdjeciaPanel_DragLeave(object sender, DragEventArgs e)
        {
            dropOverlay.Visibility = Visibility.Collapsed;
        }

        private void ZdjeciaPanel_Drop(object sender, DragEventArgs e)
        {
            dropOverlay.Visibility = Visibility.Collapsed;

            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var pliki = (string[])e.Data.GetData(DataFormats.FileDrop);
            int dodano = 0;

            foreach (string plik in pliki)
            {
                if (!dozwoloneRozszerzenia.Contains(Path.GetExtension(plik))) continue;
                if (sciezkiZdjec.Contains(plik)) continue;

                sciezkiZdjec.Add(plik);
                listZdjecia.Items.Add(Path.GetFileName(plik));
                dodano++;
            }

            if (dodano > 0)
            {
                AktualizujLiczniki();
                if (listZdjecia.SelectedIndex < 0)
                    listZdjecia.SelectedIndex = 0;
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void BtnZgloszReklamacje_Click(object sender, RoutedEventArgs e)
        {
            // Walidacja
            var zaznaczoneTowary = towary.Where(t => t.IsSelected).ToList();
            if (zaznaczoneTowary.Count == 0)
            {
                MessageBox.Show("Zaznacz przynajmniej jeden towar do reklamacji!",
                    "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtOpis.Text))
            {
                MessageBox.Show("Wprowadź opis problemu!",
                    "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtOpis.Focus();
                return;
            }

            // Oblicz sumy
            decimal sumaKg = zaznaczoneTowary.Sum(t => t.Waga);
            decimal sumaWartosc = zaznaczoneTowary.Sum(t => t.Wartosc);

            // Pobierz typ, podkategorie i priorytet
            string typReklamacji = (cmbTypReklamacji.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Inne";
            string podkategoria = (cmbPodkategoria.SelectedItem as ComboBoxItem)?.Content?.ToString();
            string priorytet = "Normalny";
            if (cmbPriorytet.SelectedItem is ComboBoxItem item)
            {
                var panel = item.Content as StackPanel;
                if (panel != null && panel.Children.Count > 1)
                {
                    var textBlock = panel.Children[1] as TextBlock;
                    priorytet = textBlock?.Text ?? "Normalny";
                }
            }

            // Sprawdzenie duplikatow - czy dla tego dokumentu juz istnieje otwarta reklamacja
            try
            {
                using (var connCheck = new SqlConnection(connectionStringLibraNet))
                {
                    connCheck.Open();
                    using (var cmdCheck = new SqlCommand(@"
                        SELECT COUNT(*) FROM [dbo].[Reklamacje]
                        WHERE IdDokumentu = @IdDok
                          AND TypReklamacji <> 'Faktura korygujaca'
                          AND StatusV2 NOT IN ('ZAMKNIETA','ODRZUCONA')", connCheck))
                    {
                        cmdCheck.Parameters.AddWithValue("@IdDok", idDokumentu);
                        int istniejace = Convert.ToInt32(cmdCheck.ExecuteScalar());
                        if (istniejace > 0)
                        {
                            var wynik = MessageBox.Show(
                                $"Dla dokumentu {numerDokumentu} juz istnieje {istniejace} otwarta reklamacja.\n\nCzy na pewno chcesz utworzyc kolejna?",
                                "Mozliwy duplikat", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                            if (wynik != MessageBoxResult.Yes) return;
                        }
                    }
                }
            }
            catch { }

            int idReklamacji = 0;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionStringLibraNet))
                {
                    conn.Open();
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // Pobierz handlowca z Symfonii
                            string handlowiecNazwa = "-";
                            try
                            {
                                using (var connH = new SqlConnection("Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True"))
                                {
                                    connH.Open();
                                    using (var cmdH = new SqlCommand(
                                        "SELECT CDim_Handlowiec_Val FROM [HANDEL].[SSCommon].[ContractorClassification] WHERE ElementId = @Khid", connH))
                                    {
                                        cmdH.Parameters.AddWithValue("@Khid", idKontrahenta);
                                        var hResult = cmdH.ExecuteScalar();
                                        if (hResult != null && hResult != DBNull.Value)
                                            handlowiecNazwa = hResult.ToString();
                                    }
                                }
                            }
                            catch { }

                            // 1. Zapisz główny rekord reklamacji (z kategoryzacja + Workflow V2)
                            string queryReklamacja = @"
                                INSERT INTO [dbo].[Reklamacje]
                                (DataZgloszenia, UserID, IdDokumentu, NumerDokumentu, IdKontrahenta, NazwaKontrahenta,
                                 Opis, SumaKg, SumaWartosc, Status, TypReklamacji, Priorytet,
                                 StatusV2, ZrodloZgloszenia, KategoriaPrzyczyny, PodkategoriaPrzyczyny, WymagaUzupelnienia, Handlowiec)
                                VALUES
                                (GETDATE(), @UserID, @IdDokumentu, @NumerDokumentu, @IdKontrahenta, @NazwaKontrahenta,
                                 @Opis, @SumaKg, @SumaWartosc, 'Nowa', @TypReklamacji, @Priorytet,
                                 'ZGLOSZONA', 'Handlowiec', @Kategoria, @Podkategoria, 0, @Handlowiec);
                                SELECT SCOPE_IDENTITY();";

                            using (SqlCommand cmd = new SqlCommand(queryReklamacja, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@UserID", userId);
                                cmd.Parameters.AddWithValue("@IdDokumentu", idDokumentu);
                                cmd.Parameters.AddWithValue("@NumerDokumentu", numerDokumentu);
                                cmd.Parameters.AddWithValue("@IdKontrahenta", idKontrahenta);
                                cmd.Parameters.AddWithValue("@NazwaKontrahenta", nazwaKontrahenta);
                                cmd.Parameters.AddWithValue("@Opis", txtOpis.Text.Trim());
                                cmd.Parameters.AddWithValue("@SumaKg", sumaKg);
                                cmd.Parameters.AddWithValue("@SumaWartosc", sumaWartosc);
                                cmd.Parameters.AddWithValue("@TypReklamacji", typReklamacji);
                                cmd.Parameters.AddWithValue("@Priorytet", priorytet);
                                cmd.Parameters.AddWithValue("@Kategoria", (object)typReklamacji ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@Podkategoria", (object)podkategoria ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@Handlowiec", handlowiecNazwa);

                                idReklamacji = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            // 2. Zapisz towary
                            string queryTowary = @"
                                INSERT INTO [dbo].[ReklamacjeTowary]
                                (IdReklamacji, IdTowaru, Symbol, Nazwa, Waga, Cena, Wartosc)
                                VALUES
                                (@IdReklamacji, @IdTowaru, @Symbol, @Nazwa, @Waga, @Cena, @Wartosc)";

                            foreach (TowarReklamacji towar in zaznaczoneTowary)
                            {
                                using (SqlCommand cmd = new SqlCommand(queryTowary, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                                    cmd.Parameters.AddWithValue("@IdTowaru", towar.ID);
                                    cmd.Parameters.AddWithValue("@Symbol", towar.Symbol ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Nazwa", towar.Nazwa ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Waga", towar.Waga);
                                    cmd.Parameters.AddWithValue("@Cena", towar.Cena);
                                    cmd.Parameters.AddWithValue("@Wartosc", towar.Wartosc);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            // 3. Zapisz partie (jeśli są)
                            var zaznaczonePartie = partie.Where(p => p.IsSelected).ToList();
                            if (zaznaczonePartie.Count > 0)
                            {
                                string queryPartie = @"
                                    INSERT INTO [dbo].[ReklamacjePartie]
                                    (IdReklamacji, GuidPartii, NumerPartii, CustomerID, CustomerName)
                                    VALUES
                                    (@IdReklamacji, @GuidPartii, @NumerPartii, @CustomerID, @CustomerName)";

                                foreach (PartiaDostawcy partia in zaznaczonePartie)
                                {
                                    using (SqlCommand cmd = new SqlCommand(queryPartie, conn, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                                        cmd.Parameters.AddWithValue("@GuidPartii", partia.GuidPartii != Guid.Empty ? (object)partia.GuidPartii : DBNull.Value);
                                        cmd.Parameters.AddWithValue("@NumerPartii", partia.NrPartii ?? "");
                                        cmd.Parameters.AddWithValue("@CustomerID", partia.IdDostawcy ?? (object)DBNull.Value);
                                        cmd.Parameters.AddWithValue("@CustomerName", partia.NazwaDostawcy ?? (object)DBNull.Value);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            // 4. Zapisz zdjęcia (jeśli są)
                            if (sciezkiZdjec.Count > 0)
                            {
                                string folderReklamacji = Path.Combine(
                                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                    "ReklamacjeZdjecia",
                                    idReklamacji.ToString());

                                Directory.CreateDirectory(folderReklamacji);

                                // Sprawdź czy kolumna DaneZdjecia istnieje
                                bool maKolumneDaneZdjecia = false;
                                try
                                {
                                    using (var cmdCheck = new SqlCommand(
                                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ReklamacjeZdjecia' AND COLUMN_NAME = 'DaneZdjecia'", conn, transaction))
                                    {
                                        maKolumneDaneZdjecia = Convert.ToInt32(cmdCheck.ExecuteScalar()) > 0;
                                    }
                                }
                                catch { }

                                string queryZdjecia;
                                if (maKolumneDaneZdjecia)
                                {
                                    queryZdjecia = @"
                                        INSERT INTO [dbo].[ReklamacjeZdjecia]
                                        (IdReklamacji, NazwaPliku, SciezkaPliku, DodanePrzez, DaneZdjecia)
                                        VALUES
                                        (@IdReklamacji, @NazwaPliku, @SciezkaPliku, @DodanePrzez, @DaneZdjecia)";
                                }
                                else
                                {
                                    queryZdjecia = @"
                                        INSERT INTO [dbo].[ReklamacjeZdjecia]
                                        (IdReklamacji, NazwaPliku, SciezkaPliku, DodanePrzez)
                                        VALUES
                                        (@IdReklamacji, @NazwaPliku, @SciezkaPliku, @DodanePrzez)";
                                }

                                foreach (string sciezkaZrodlowa in sciezkiZdjec)
                                {
                                    string nazwaPliku = Path.GetFileName(sciezkaZrodlowa);
                                    string nowaSciezka = Path.Combine(folderReklamacji, nazwaPliku);

                                    // Skopiuj plik lokalnie
                                    File.Copy(sciezkaZrodlowa, nowaSciezka, true);

                                    using (SqlCommand cmd = new SqlCommand(queryZdjecia, conn, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                                        cmd.Parameters.AddWithValue("@NazwaPliku", nazwaPliku);
                                        cmd.Parameters.AddWithValue("@SciezkaPliku", nowaSciezka);
                                        cmd.Parameters.AddWithValue("@DodanePrzez", userId);

                                        // Dodaj BLOB tylko jeśli kolumna istnieje
                                        if (maKolumneDaneZdjecia)
                                        {
                                            byte[] daneZdjecia = File.ReadAllBytes(sciezkaZrodlowa);
                                            cmd.Parameters.Add("@DaneZdjecia", SqlDbType.VarBinary, -1).Value = daneZdjecia;
                                        }

                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            // 5. Dodaj wpis do historii
                            string queryHistoria = @"
                                INSERT INTO [dbo].[ReklamacjeHistoria]
                                (IdReklamacji, UserID, StatusNowy, Komentarz, TypAkcji)
                                VALUES
                                (@IdReklamacji, @UserID, 'Nowa', 'Utworzenie reklamacji', 'Utworzenie')";

                            using (SqlCommand cmd = new SqlCommand(queryHistoria, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                                cmd.Parameters.AddWithValue("@UserID", userId);
                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();

                            // 6. Powiaz z korekta
                            // Priorytet: przypisanaKorektaId (z konstruktora) → reczny wybor z listy
                            int idKorektyRek = 0;
                            string numerKorektyInfo = null;

                            if (przypisanaKorektaId.HasValue && przypisanaKorektaId.Value > 0 && idReklamacji > 0)
                            {
                                // Formularz byl otwarty w trybie "przypiety do korekty"
                                // WAZNE: NIE zmieniamy StatusV2 na POWIAZANA — to jest uzupelnienie info,
                                // nie zamykanie sprawy. Dzial jakosci musi to nadal rozpatrzyc.
                                // Linkujemy tylko bidirectional + zdejmujemy WymagaUzupelnienia (info sa).
                                try
                                {
                                    using (var conn2 = new SqlConnection(connectionStringLibraNet))
                                    {
                                        conn2.Open();
                                        using (var cmdK = new SqlCommand(
                                            "SELECT Id FROM [dbo].[Reklamacje] WHERE IdDokumentu = @IdDok AND TypReklamacji = 'Faktura korygujaca'", conn2))
                                        {
                                            cmdK.Parameters.AddWithValue("@IdDok", przypisanaKorektaId.Value);
                                            var r = cmdK.ExecuteScalar();
                                            if (r != null) idKorektyRek = Convert.ToInt32(r);
                                        }

                                        if (idKorektyRek > 0)
                                        {
                                            // Bidirectional link + zdejmujemy flage WymagaUzupelnienia (info sa uzupelnione)
                                            // Status zostaje ZGLOSZONA - dzial jakosci musi rozpatrzyc
                                            using (var cmdP = new SqlCommand(@"
                                                UPDATE [dbo].[Reklamacje]
                                                SET PowiazanaReklamacjaId=@B,
                                                    WymagaUzupelnienia=0,
                                                    DataPowiazania=GETDATE(),
                                                    UserPowiazania=@U
                                                WHERE Id=@A;

                                                UPDATE [dbo].[Reklamacje]
                                                SET PowiazanaReklamacjaId=@A,
                                                    WymagaUzupelnienia=0,
                                                    DataPowiazania=GETDATE(),
                                                    UserPowiazania=@U
                                                WHERE Id=@B;", conn2))
                                            {
                                                cmdP.Parameters.AddWithValue("@A", idReklamacji);
                                                cmdP.Parameters.AddWithValue("@B", idKorektyRek);
                                                cmdP.Parameters.AddWithValue("@U", userId);
                                                cmdP.ExecuteNonQuery();
                                            }
                                            numerKorektyInfo = przypisanaKorektaNumer;
                                        }
                                    }
                                }
                                catch { }
                            }
                            else
                            {
                                // Stary tryb - reczny wybor z listy
                                var wybranaKorekta = korekty.FirstOrDefault(k => k.IsSelected);
                                if (wybranaKorekta != null && idReklamacji > 0)
                                {
                                    try
                                    {
                                        using (var conn2 = new SqlConnection(connectionStringLibraNet))
                                        {
                                            conn2.Open();
                                            using (var cmdK = new SqlCommand(
                                                "SELECT Id FROM [dbo].[Reklamacje] WHERE IdDokumentu = @IdDok AND TypReklamacji = 'Faktura korygujaca'", conn2))
                                            {
                                                cmdK.Parameters.AddWithValue("@IdDok", wybranaKorekta.IdDokumentu);
                                                var r = cmdK.ExecuteScalar();
                                                if (r != null) idKorektyRek = Convert.ToInt32(r);
                                            }

                                            if (idKorektyRek > 0)
                                            {
                                                using (var cmdP = new SqlCommand(@"
                                                    UPDATE [dbo].[Reklamacje] SET PowiazanaReklamacjaId=@B, StatusV2='POWIAZANA', WymagaUzupelnienia=0, DataPowiazania=GETDATE(), UserPowiazania=@U WHERE Id=@A;
                                                    UPDATE [dbo].[Reklamacje] SET PowiazanaReklamacjaId=@A, StatusV2='POWIAZANA', WymagaUzupelnienia=0, DataPowiazania=GETDATE(), UserPowiazania=@U WHERE Id=@B;", conn2))
                                                {
                                                    cmdP.Parameters.AddWithValue("@A", idReklamacji);
                                                    cmdP.Parameters.AddWithValue("@B", idKorektyRek);
                                                    cmdP.Parameters.AddWithValue("@U", userId);
                                                    cmdP.ExecuteNonQuery();
                                                }
                                                numerKorektyInfo = wybranaKorekta.NumerDokumentu;
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }

                            string korekInfo = numerKorektyInfo != null ? $"\nPowiazano z korekta: {numerKorektyInfo}" : "";
                            MessageBox.Show(
                                $"Reklamacja nr {idReklamacji} została pomyślnie zgłoszona!\n\n" +
                                $"Typ: {typReklamacji}\n" +
                                $"Priorytet: {priorytet}\n" +
                                $"Towarów: {zaznaczoneTowary.Count}\n" +
                                $"Suma kg: {sumaKg:N2}\n" +
                                $"Wartość: {sumaWartosc:N2} zł{korekInfo}",
                                "Sukces",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);

                            IdUtworzonejReklamacji = idReklamacji;
                            ReklamacjaZapisana = true;
                            this.DialogResult = true;
                            this.Close();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw new Exception($"Błąd podczas zapisywania: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania reklamacji:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // Klasy pomocnicze
    public class TowarReklamacji : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;
        private decimal _waga;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
        public int ID { get; set; }
        public string Symbol { get; set; }
        public string Nazwa { get; set; }
        public decimal Waga
        {
            get => _waga;
            set
            {
                _waga = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Waga)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Wartosc)));
            }
        }
        public decimal Cena { get; set; }
        public decimal Wartosc => Math.Round(Waga * Cena, 2);

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }

    public class KorektaItem : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected))); }
        }
        public int IdDokumentu { get; set; }
        public string NumerDokumentu { get; set; }
        public DateTime Data { get; set; }
        public decimal Wartosc { get; set; }
        public decimal SumaKg { get; set; }
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }

    public class PartiaDostawcy : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
        public Guid GuidPartii { get; set; }
        public string GuidPartiiStr { get; set; }
        public string NrPartii { get; set; }
        public string IdDostawcy { get; set; }
        public string NazwaDostawcy { get; set; }
        public string DataUtworzenia { get; set; }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }
}
