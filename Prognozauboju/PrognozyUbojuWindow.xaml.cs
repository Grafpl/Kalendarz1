using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.IO;
using System.Text;
using System.Reflection;
using System.Globalization;

namespace Kalendarz1.PrognozyUboju
{
    public partial class PrognozyUbojuWindow : Window
    {
        private string connectionString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private List<TowarPrognozyModel> daneTowarow = new List<TowarPrognozyModel>();
        private List<OdbiorcaTowarModel> daneOdbiorcow = new List<OdbiorcaTowarModel>();
        private List<HandlowiecPrognozyModel> daneHandlowcow = new List<HandlowiecPrognozyModel>();
        private FiltryPrognozy aktualneFiltry = new FiltryPrognozy();
        private string currentUserId;

        private List<int> wybraniKontrahenci = new List<int>();
        private List<string> wybraniHandlowcy = new List<string>();
        private int? wybranyTowarId = null;

        public PrognozyUbojuWindow()
        {
            InitializeComponent();
            currentUserId = App.UserID;

            if (txtUserInfo != null)
                txtUserInfo.Text = $"👤 {currentUserId}";

            this.Loaded += PrognozyUbojuWindow_Loaded;
        }

        private void PrognozyUbojuWindow_Loaded(object sender, RoutedEventArgs e)
        {
            WczytajTowary();
            WczytajDane();
        }

        private void WczytajTowary()
        {
            try
            {
                string query = @"
                    SELECT DISTINCT 
                        TW.id,
                        TW.kod,
                        TW.nazwa,
                        COUNT(DISTINCT DP.super) AS LiczbaTransakcji
                    FROM [HANDEL].[HM].[TW] TW
                    INNER JOIN [HANDEL].[HM].[DP] DP ON TW.id = DP.idtw
                    INNER JOIN [HANDEL].[HM].[DK] DK ON DP.super = DK.id
                    WHERE TW.katalog = '67095'
                      AND DK.anulowany = 0
                      AND DK.data >= DATEADD(MONTH, -6, GETDATE())
                    GROUP BY TW.id, TW.kod, TW.nazwa
                    HAVING COUNT(DISTINCT DP.super) > 0
                    ORDER BY TW.kod;";

                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        var towary = new List<TowarItem>();

                        towary.Add(new TowarItem
                        {
                            Id = 0,
                            Kod = "--- Wszystkie towary ---",
                            Nazwa = ""
                        });

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                towary.Add(new TowarItem
                                {
                                    Id = reader.GetInt32(0),
                                    Kod = reader.GetString(1),
                                    Nazwa = reader.GetString(2),
                                    LiczbaTransakcji = reader.GetInt32(3)
                                });
                            }
                        }

                        cmbTowar.ItemsSource = towary;
                        cmbTowar.DisplayMemberPath = "DisplayText";
                        cmbTowar.SelectedValuePath = "Id";
                        cmbTowar.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania towarów:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- OBSŁUGA FILTRÓW ---

        private void CmbOkresAnalizy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbOkresAnalizy?.SelectedItem is ComboBoxItem item)
            {
                aktualneFiltry.LiczbaTygodni = int.Parse(item.Tag.ToString());
                if (this.IsLoaded) WczytajDane();
            }
        }

        private void CmbWidok_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsLoaded) OdswiezWidok();
        }

        private void TxtMinIlosc_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtMinIlosc == null || !this.IsLoaded) return;
            if (decimal.TryParse(txtMinIlosc.Text, out decimal min))
            {
                aktualneFiltry.MinimalnaIlosc = min;
                OdswiezWidok();
            }
        }

        private void CmbTowar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded || cmbTowar.SelectedValue == null) return;
            int towarId = (int)cmbTowar.SelectedValue;
            wybranyTowarId = towarId == 0 ? null : (int?)towarId;
            AktualizujWskaznikiFiltrow();
            WczytajDane();
        }

        private void BtnWybierzKontrahentow_Click(object sender, RoutedEventArgs e)
        {
            var oknoWyboru = new FormWyborKontrahentow(connectionString, wybraniKontrahenci);
            if (oknoWyboru.ShowDialog() == true)
            {
                wybraniKontrahenci = oknoWyboru.WybraniKontrahenci;
                AktualizujWskaznikiFiltrow();
                WczytajDane();
            }
        }

        // NOWA METODA - Wybór handlowców
        private void BtnWybierzHandlowcow_Click(object sender, RoutedEventArgs e)
        {
            var oknoWyboru = new FormWyborHandlowcow(connectionString, wybraniHandlowcy);
            if (oknoWyboru.ShowDialog() == true)
            {
                wybraniHandlowcy = oknoWyboru.WybraniHandlowcy;
                AktualizujWskaznikiFiltrow();
                WczytajDane();
            }
        }

        private void BtnWyczyscKontrahentow_Click(object sender, RoutedEventArgs e)
        {
            wybraniKontrahenci.Clear();
            AktualizujWskaznikiFiltrow();
            WczytajDane();
        }

        // NOWA METODA - Czyszczenie filtra handlowców
        private void BtnWyczyscHandlowcow_Click(object sender, RoutedEventArgs e)
        {
            wybraniHandlowcy.Clear();
            AktualizujWskaznikiFiltrow();
            WczytajDane();
        }

        private void BtnWyczyscTowar_Click(object sender, RoutedEventArgs e)
        {
            wybranyTowarId = null;
            if (cmbTowar != null) cmbTowar.SelectedIndex = 0;
            AktualizujWskaznikiFiltrow();
            WczytajDane();
        }

        private void AktualizujWskaznikiFiltrow()
        {
            // Wskaźnik kontrahentów
            if (wybraniKontrahenci != null && wybraniKontrahenci.Count > 0)
            {
                borderAktywneKontrahenci.Visibility = Visibility.Visible;
                txtAktywneKontrahenci.Text = $"{wybraniKontrahenci.Count} wybranych";
            }
            else
            {
                borderAktywneKontrahenci.Visibility = Visibility.Collapsed;
            }

            // NOWY WSKAŹNIK - Handlowcy
            if (wybraniHandlowcy != null && wybraniHandlowcy.Count > 0)
            {
                borderAktywniHandlowcy.Visibility = Visibility.Visible;
                if (wybraniHandlowcy.Count == 1)
                {
                    txtAktywniHandlowcy.Text = wybraniHandlowcy[0];
                }
                else
                {
                    txtAktywniHandlowcy.Text = $"{wybraniHandlowcy.Count} wybranych";
                }
            }
            else
            {
                borderAktywniHandlowcy.Visibility = Visibility.Collapsed;
            }

            // Wskaźnik towaru
            if (wybranyTowarId.HasValue)
            {
                borderAktywnyTowar.Visibility = Visibility.Visible;
                var towar = cmbTowar.SelectedItem as TowarItem;
                txtAktywnyTowar.Text = towar?.Kod ?? "Nieznany";
            }
            else
            {
                borderAktywnyTowar.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            WczytajDane();
        }

        // --- GŁÓWNA LOGIKA ŁADOWANIA I PRZETWARZANIA DANYCH ---

        private async void WczytajDane()
        {
            if (txtStatus == null || dgDane == null) return;

            try
            {
                txtStatus.Text = "⏳ Ładowanie danych...";
                dgDane.IsEnabled = false;
                DateTime dataOd = DateTime.Today.AddDays(-7 * aktualneFiltry.LiczbaTygodni);

                // ZMODYFIKOWANE ZAPYTANIE SQL
                string query = @"
WITH DaneSprzedazy AS (
    SELECT 
        DK.khid AS KontrahentId,
        C.shortcut AS NazwaKontrahenta,
        DP.idtw AS TowarId,
        TW.kod AS KodTowaru,
        TW.nazwa AS NazwaTowaru,
        ISNULL(WYM.CDim_Handlowiec_Val, 'BRAK') AS Handlowiec,
        DATEPART(WEEKDAY, DK.data) AS DzienTygodnia,
        CAST(SUM(DP.ilosc) AS DECIMAL(18,2)) AS Ilosc
    FROM [HANDEL].[HM].[DK] DK
    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
    INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.id
    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
    WHERE 
        DK.data >= @DataOd
        AND DK.anulowany = 0
        AND TW.katalog = '67095'";

                var parameters = new List<SqlParameter> { new SqlParameter("@DataOd", dataOd) };

                if (wybranyTowarId.HasValue)
                {
                    query += " AND DP.idtw = @TowarId";
                    parameters.Add(new SqlParameter("@TowarId", wybranyTowarId.Value));
                }
                if (wybraniKontrahenci != null && wybraniKontrahenci.Count > 0)
                {
                    query += $" AND DK.khid IN ({string.Join(",", wybraniKontrahenci)})";
                }
                if (wybraniHandlowcy != null && wybraniHandlowcy.Count > 0)
                {
                    var handlowcyParams = new List<string>();
                    for (int i = 0; i < wybraniHandlowcy.Count; i++)
                    {
                        string paramName = $"@Handlowiec{i}";
                        handlowcyParams.Add(paramName);
                        parameters.Add(new SqlParameter(paramName, wybraniHandlowcy[i]));
                    }
                    query += $" AND ISNULL(WYM.CDim_Handlowiec_Val, 'BRAK') IN ({string.Join(",", handlowcyParams)})";
                }

                query += @"
    GROUP BY 
        DK.khid, C.shortcut, DP.idtw, TW.kod, TW.nazwa, 
        ISNULL(WYM.CDim_Handlowiec_Val, 'BRAK'), DATEPART(WEEKDAY, DK.data)
)
SELECT * FROM DaneSprzedazy ORDER BY KodTowaru, NazwaKontrahenta, DzienTygodnia;";

                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddRange(parameters.ToArray());

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            var daneSurowe = new List<(int KontrahentId, string NazwaKontrahenta,
                                int TowarId, string KodTowaru, string NazwaTowaru, string Handlowiec,
                                int DzienTygodnia, decimal Ilosc)>();

                            while (await reader.ReadAsync())
                            {
                                daneSurowe.Add((
                                    reader.GetInt32(0),
                                    reader.GetString(1),
                                    reader.GetInt32(2),
                                    reader.GetString(3),
                                    reader.GetString(4),
                                    reader.GetString(5),
                                    reader.GetInt32(6),
                                    Convert.ToDecimal(reader[7])
                                ));
                            }

                            PrzetworzDaneTowarow(daneSurowe);
                            PrzetworzDaneOdbiorcow(daneSurowe);
                            PrzetworzDaneHandlowcow(daneSurowe);
                        }
                    }
                }
                OdswiezWidok();
                AktualizujPodsumowanie();
                txtStatus.Text = $"✓ Załadowano dane z ostatnich {aktualneFiltry.LiczbaTygodni} tygodni";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "❌ Błąd ładowania";
            }
            finally
            {
                if (dgDane != null) dgDane.IsEnabled = true;
            }
        }

        private void PrzetworzDaneTowarow(List<(int KontrahentId, string NazwaKontrahenta,
            int TowarId, string KodTowaru, string NazwaTowaru, string Handlowiec, int DzienTygodnia, decimal Ilosc)> dane)
        {
            daneTowarow.Clear();

            var grupaTowarow = dane
                .GroupBy(d => new { d.TowarId, d.KodTowaru, d.NazwaTowaru });

            foreach (var grupa in grupaTowarow)
            {
                var model = new TowarPrognozyModel
                {
                    TowarId = grupa.Key.TowarId,
                    KodTowaru = grupa.Key.KodTowaru,
                    NazwaTowaru = grupa.Key.NazwaTowaru,
                    LiczbaTygodni = aktualneFiltry.LiczbaTygodni
                };

                var dniGrupy = grupa.GroupBy(g => g.DzienTygodnia);

                foreach (var dzien in dniGrupy)
                {
                    decimal srednia = dzien.Sum(d => d.Ilosc) / aktualneFiltry.LiczbaTygodni;

                    switch (dzien.Key)
                    {
                        case 2: model.Poniedzialek = srednia; break;
                        case 3: model.Wtorek = srednia; break;
                        case 4: model.Sroda = srednia; break;
                        case 5: model.Czwartek = srednia; break;
                        case 6: model.Piatek = srednia; break;
                        case 7: model.Sobota = srednia; break;
                        case 1: model.Niedziela = srednia; break;
                    }
                }

                daneTowarow.Add(model);
            }
        }

        private void PrzetworzDaneOdbiorcow(List<(int KontrahentId, string NazwaKontrahenta,
            int TowarId, string KodTowaru, string NazwaTowaru, string Handlowiec, int DzienTygodnia, decimal Ilosc)> dane)
        {
            daneOdbiorcow.Clear();

            var grupy = dane.GroupBy(d => new { d.KontrahentId, d.NazwaKontrahenta, d.TowarId, d.KodTowaru, d.NazwaTowaru, d.Handlowiec });

            foreach (var grupa in grupy)
            {
                var model = new OdbiorcaTowarModel
                {
                    KontrahentId = grupa.Key.KontrahentId,
                    NazwaKontrahenta = grupa.Key.NazwaKontrahenta,
                    KodTowaru = grupa.Key.KodTowaru,
                    NazwaTowaru = grupa.Key.NazwaTowaru,
                    Handlowiec = grupa.Key.Handlowiec
                };

                var dniGrupy = grupa.GroupBy(g => g.DzienTygodnia);

                foreach (var dzien in dniGrupy)
                {
                    decimal srednia = dzien.Sum(d => d.Ilosc) / aktualneFiltry.LiczbaTygodni;

                    switch (dzien.Key)
                    {
                        case 2: model.Pon = srednia; break;
                        case 3: model.Wt = srednia; break;
                        case 4: model.Sr = srednia; break;
                        case 5: model.Czw = srednia; break;
                        case 6: model.Pt = srednia; break;
                        case 7: model.Sob = srednia; break;
                        case 1: model.Ndz = srednia; break;
                    }
                }

                daneOdbiorcow.Add(model);
            }
        }

        // NOWA METODA - Przetwarzanie danych dla widoku Handlowców
        private void PrzetworzDaneHandlowcow(List<(int KontrahentId, string NazwaKontrahenta,
            int TowarId, string KodTowaru, string NazwaTowaru, string Handlowiec, int DzienTygodnia, decimal Ilosc)> dane)
        {
            daneHandlowcow.Clear();

            var grupaHandlowcow = dane.GroupBy(d => d.Handlowiec);

            foreach (var grupa in grupaHandlowcow)
            {
                var model = new HandlowiecPrognozyModel
                {
                    NazwaHandlowca = grupa.Key,
                    LiczbaTygodni = aktualneFiltry.LiczbaTygodni
                };

                var dniGrupy = grupa.GroupBy(g => g.DzienTygodnia);

                foreach (var dzien in dniGrupy)
                {
                    decimal srednia = dzien.Sum(d => d.Ilosc) / aktualneFiltry.LiczbaTygodni;
                    switch (dzien.Key)
                    {
                        case 2: model.Poniedzialek = srednia; break;
                        case 3: model.Wtorek = srednia; break;
                        case 4: model.Sroda = srednia; break;
                        case 5: model.Czwartek = srednia; break;
                        case 6: model.Piatek = srednia; break;
                        case 7: model.Sobota = srednia; break;
                        case 1: model.Niedziela = srednia; break;
                    }
                }
                daneHandlowcow.Add(model);
            }
        }

        // --- KONFIGURACJA WIDOKÓW I KOLUMN ---

        private void OdswiezWidok()
        {
            if (cmbWidok?.SelectedItem is ComboBoxItem item && dgDane != null)
            {
                string widok = item.Tag.ToString();
                if (widok == "Towary")
                {
                    dgDane.ItemsSource = daneTowarow
                        .Where(t => t.SumaTydzien >= aktualneFiltry.MinimalnaIlosc)
                        .OrderByDescending(t => t.SumaTydzien).ToList();
                    KonfigurujKolumnyTowary();
                }
                else if (widok == "Odbiorcy")
                {
                    dgDane.ItemsSource = daneOdbiorcow
                        .Where(o => o.SumaTydzien >= aktualneFiltry.MinimalnaIlosc)
                        .OrderBy(o => o.NazwaKontrahenta).ThenBy(o => o.KodTowaru).ToList();
                    KonfigurujKolumnyOdbiorcy();
                }
                else // NOWY WIDOK
                {
                    dgDane.ItemsSource = daneHandlowcow
                        .Where(h => h.SumaTydzien >= aktualneFiltry.MinimalnaIlosc)
                        .OrderByDescending(h => h.SumaTydzien).ToList();
                    KonfigurujKolumnyHandlowcy();
                }
                AktualizujPodsumowanie();
            }
        }

        private void KonfigurujKolumnyTowary()
        {
            dgDane.Columns.Clear();
            dgDane.Columns.Add(new DataGridTextColumn { Header = "📦 Towar", Binding = new Binding("KodTowaru"), Width = new DataGridLength(200) });
            dgDane.Columns.Add(new DataGridTextColumn { Header = "📝 Nazwa", Binding = new Binding("NazwaTowaru"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });

            DodajKolumneDnia("Pon (kg)", "Poniedzialek");
            DodajKolumneDnia("Wt (kg)", "Wtorek");
            DodajKolumneDnia("Śr (kg)", "Sroda");
            DodajKolumneDnia("Czw (kg)", "Czwartek");
            DodajKolumneDnia("Pt (kg)", "Piatek");
            DodajKolumneDnia("Sob (kg)", "Sobota");
            DodajKolumneDnia("Ndz (kg)", "Niedziela");

            DodajKolumneSumy("∑ Tydzień", "SumaTydzien");
        }

        private void KonfigurujKolumnyOdbiorcy()
        {
            dgDane.Columns.Clear();
            dgDane.Columns.Add(new DataGridTextColumn { Header = "🏢 Kontrahent", Binding = new Binding("NazwaKontrahenta"), Width = new DataGridLength(250) });
            dgDane.Columns.Add(new DataGridTextColumn { Header = "📦 Towar", Binding = new Binding("KodTowaru"), Width = new DataGridLength(180) });
            dgDane.Columns.Add(new DataGridTextColumn { Header = "👥 Handlowiec", Binding = new Binding("Handlowiec"), Width = new DataGridLength(120) });

            DodajKolumneDnia("Pon", "Pon");
            DodajKolumneDnia("Wt", "Wt");
            DodajKolumneDnia("Śr", "Sr");
            DodajKolumneDnia("Czw", "Czw");
            DodajKolumneDnia("Pt", "Pt");
            DodajKolumneDnia("Sob", "Sob");
            DodajKolumneDnia("Ndz", "Ndz");

            DodajKolumneSumy("∑", "FormatSuma");
        }

        // NOWA METODA - Konfiguracja kolumn dla widoku Handlowców
        private void KonfigurujKolumnyHandlowcy()
        {
            dgDane.Columns.Clear();
            dgDane.Columns.Add(new DataGridTextColumn { Header = "👥 Handlowiec", Binding = new Binding("NazwaHandlowca"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });

            DodajKolumneDnia("Pon (kg)", "Poniedzialek");
            DodajKolumneDnia("Wt (kg)", "Wtorek");
            DodajKolumneDnia("Śr (kg)", "Sroda");
            DodajKolumneDnia("Czw (kg)", "Czwartek");
            DodajKolumneDnia("Pt (kg)", "Piatek");
            DodajKolumneDnia("Sob (kg)", "Sobota");
            DodajKolumneDnia("Ndz (kg)", "Niedziela");

            DodajKolumneSumy("∑ Tydzień", "SumaTydzien");
        }

        private void DodajKolumneDnia(string naglowek, string binding)
        {
            if (dgDane == null) return;

            var col = new DataGridTextColumn
            {
                Header = naglowek,
                Binding = new Binding(binding) { StringFormat = "N1" },
                Width = new DataGridLength(90)
            };

            col.ElementStyle = new Style(typeof(TextBlock))
            {
                Setters =
                {
                    new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right),
                    new Setter(TextBlock.PaddingProperty, new Thickness(5))
                }
            };

            col.CellStyle = new Style(typeof(DataGridCell));
            col.CellStyle.Setters.Add(new EventSetter(DataGridCell.LoadedEvent,
                new RoutedEventHandler((s, e) =>
                {
                    if (s is DataGridCell cell && cell.DataContext != null)
                    {
                        var property = cell.DataContext.GetType().GetProperty(binding);
                        if (property != null && property.PropertyType == typeof(decimal))
                        {
                            decimal val = (decimal)property.GetValue(cell.DataContext);
                            cell.Background = GetColorForValue(val);
                        }
                    }
                })));

            dgDane.Columns.Add(col);
        }

        private void DodajKolumneSumy(string naglowek, string binding)
        {
            var colSuma = new DataGridTextColumn
            {
                Header = naglowek,
                Binding = new Binding(binding) { StringFormat = "N0" },
                Width = new DataGridLength(110)
            };
            colSuma.ElementStyle = new Style(typeof(TextBlock))
            {
                Setters =
                {
                    new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right),
                    new Setter(TextBlock.FontWeightProperty, FontWeights.Bold),
                    new Setter(TextBlock.BackgroundProperty, new SolidColorBrush(Color.FromRgb(232, 245, 233)))
                }
            };
            dgDane.Columns.Add(colSuma);
        }

        private Brush GetColorForValue(decimal value)
        {
            if (value <= 0) return Brushes.Transparent;

            byte intensity;
            if (value < 100)
                intensity = (byte)(255 - (value / 100 * 100));
            else if (value < 500)
                intensity = (byte)(155 - ((value - 100) / 400 * 100));
            else
                intensity = 55;

            return new SolidColorBrush(Color.FromRgb(intensity, 255, intensity));
        }

        // ZMODYFIKOWANA METODA - Lepsze podsumowanie
        private void AktualizujPodsumowanie()
        {
            if (txtPodsumowanie == null) return;
            if (dgDane.ItemsSource == null || !dgDane.Items.Cast<object>().Any())
            {
                txtPodsumowanie.Text = "📊 Brak danych do wyświetlenia dla bieżących filtrów";
                txtNajwiekszyDzien.Text = "-";
                txtNajmniejszyDzien.Text = "-";
                txtSredniaTygodniowa.Text = "Średnia tyg: 0 kg";
                return;
            }

            decimal sumaTydzien = 0;
            decimal sumaPon = 0, sumaWt = 0, sumaSr = 0, sumaCzw = 0, sumaPt = 0, sumaSob = 0, sumaNdz = 0;
            int count = 0;

            string widok = (cmbWidok.SelectedItem as ComboBoxItem).Tag.ToString();

            if (widok == "Towary" && dgDane.ItemsSource is List<TowarPrognozyModel> towary)
            {
                sumaTydzien = towary.Sum(t => t.SumaTydzien);
                sumaPon = towary.Sum(t => t.Poniedzialek);
                sumaWt = towary.Sum(t => t.Wtorek);
                sumaSr = towary.Sum(t => t.Sroda);
                sumaCzw = towary.Sum(t => t.Czwartek);
                sumaPt = towary.Sum(t => t.Piatek);
                sumaSob = towary.Sum(t => t.Sobota);
                sumaNdz = towary.Sum(t => t.Niedziela);
                count = towary.Count;
            }
            else if (widok == "Odbiorcy" && dgDane.ItemsSource is List<OdbiorcaTowarModel> odbiorcy)
            {
                sumaTydzien = odbiorcy.Sum(t => t.SumaTydzien);
                sumaPon = odbiorcy.Sum(t => t.Pon);
                sumaWt = odbiorcy.Sum(t => t.Wt);
                sumaSr = odbiorcy.Sum(t => t.Sr);
                sumaCzw = odbiorcy.Sum(t => t.Czw);
                sumaPt = odbiorcy.Sum(t => t.Pt);
                sumaSob = odbiorcy.Sum(t => t.Sob);
                sumaNdz = odbiorcy.Sum(t => t.Ndz);
                count = odbiorcy.Count;
            }
            else if (widok == "Handlowcy" && dgDane.ItemsSource is List<HandlowiecPrognozyModel> handlowcy)
            {
                sumaTydzien = handlowcy.Sum(t => t.SumaTydzien);
                sumaPon = handlowcy.Sum(t => t.Poniedzialek);
                sumaWt = handlowcy.Sum(t => t.Wtorek);
                sumaSr = handlowcy.Sum(t => t.Sroda);
                sumaCzw = handlowcy.Sum(t => t.Czwartek);
                sumaPt = handlowcy.Sum(t => t.Piatek);
                sumaSob = handlowcy.Sum(t => t.Sobota);
                sumaNdz = handlowcy.Sum(t => t.Niedziela);
                count = handlowcy.Count;
            }

            var sumy = new Dictionary<string, decimal>
            {
                ["Poniedziałek"] = sumaPon,
                ["Wtorek"] = sumaWt,
                ["Środa"] = sumaSr,
                ["Czwartek"] = sumaCzw,
                ["Piątek"] = sumaPt,
                ["Sobota"] = sumaSob,
                ["Niedziela"] = sumaNdz
            };

            var sumyRobocze = sumy.Where(s => s.Key != "Sobota" && s.Key != "Niedziela" && s.Value > 0).ToDictionary(s => s.Key, s => s.Value);

            var maxDzien = sumy.OrderByDescending(s => s.Value).First();
            var minDzien = sumyRobocze.Any() ? sumyRobocze.OrderBy(s => s.Value).First() : new KeyValuePair<string, decimal>("-", 0);

            string procentMax = sumaTydzien > 0 ? $"({(maxDzien.Value / sumaTydzien):P1})" : "";
            string procentMin = sumaTydzien > 0 && minDzien.Value > 0 ? $"({(minDzien.Value / sumaTydzien):P1})" : "";

            txtPodsumowanie.Text = $"📊 Podsumowanie: {count} pozycji | Suma tygodniowa: {sumaTydzien:N0} kg";
            txtNajwiekszyDzien.Text = $"{maxDzien.Key} ({maxDzien.Value:N0} kg) {procentMax}";
            txtNajmniejszyDzien.Text = $"{minDzien.Key} ({minDzien.Value:N0} kg) {procentMin}";
            txtSredniaTygodniowa.Text = $"Średnia tyg: {sumaTydzien:N0} kg | Średnia dzienna: {(sumaTydzien / 7):N0} kg";
        }

        // --- PRZYCISKI AKCJI I EKSPORTU ---

        private void BtnEksportCSV_Click(object sender, RoutedEventArgs e)
        {
            if (dgDane.ItemsSource == null || !dgDane.Items.Cast<object>().Any())
            {
                MessageBox.Show("ℹ Brak danych do eksportu", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv",
                    FileName = $"Prognoza_Uboju_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var csv = new StringBuilder();

                    IEnumerable<string> headers = dgDane.Columns.Select(c => c.Header.ToString());
                    csv.AppendLine(string.Join(";", headers));

                    foreach (object item in dgDane.Items)
                    {
                        var row = new List<string>();
                        foreach (var col in dgDane.Columns)
                        {
                            var binding = (col as DataGridBoundColumn)?.Binding as Binding;
                            if (binding != null)
                            {
                                var property = item.GetType().GetProperty(binding.Path.Path);
                                if (property != null)
                                {
                                    object value = property.GetValue(item);
                                    string formattedValue = String.Format(CultureInfo.CurrentCulture, "{0}", value);

                                    if (value is decimal d)
                                    {
                                        formattedValue = d.ToString("F2", CultureInfo.InvariantCulture);
                                    }

                                    row.Add(formattedValue);
                                }
                            }
                        }
                        csv.AppendLine(string.Join(";", row));
                    }

                    File.WriteAllText(saveDialog.FileName, csv.ToString(), Encoding.UTF8);
                    MessageBox.Show("✓ Eksport zakończony pomyślnie!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd eksportu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnWykresTygodniowy_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Funkcja wykresu tygodniowego w przygotowaniu.\n\n" +
                "Będzie pokazywać:\n" +
                "• Wykres słupkowy zapotrzebowania na poszczególne dni\n" +
                "• Podział na kategorie towarów\n" +
                "• Trendy tygodniowe",
                "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnInstrukcja_Click(object sender, RoutedEventArgs e)
        {
            string instrukcja = @"📖 INSTRUKCJA PROGNOZY UBOJU

🎯 CEL APLIKACJI
Aplikacja analizuje historię zakupów towarów (katalog 67095) przez odbiorców 
i oblicza średnie ilości dla każdego dnia tygodnia. Dzięki temu można przewidzieć 
przewidywalne zapotrzebowanie na ubój w poszczególne dni.

📊 WIDOKI

1. AGREGACJA PO TOWARACH
   • Pokazuje sumę wszystkich odbiorców dla każdego towaru
   • Najlepszy do planowania całkowitego uboju

2. SZCZEGÓŁY ODBIORCÓW
   • Pokazuje rozbicie na poszczególnych odbiorców
   • Przydatne do analizy zachowań i przypisanych handlowców

3. AGREGACJA PO HANDLOWCACH (NOWOŚĆ)
   • Pokazuje sumę sprzedaży dla każdego handlowca
   • Pomaga ocenić tygodniowy rytm pracy zespołu

⚙ FILTRY

👥 HANDLOWCY (NOWOŚĆ)
   Filtruj dane, aby zobaczyć sprzedaż tylko wybranych handlowców.

📅 OKRES ANALIZY
   Wybierz ile ostatnich tygodni uwzględnić w analizie (4-16).
   8 tygodni to stabilna, domyślna prognoza.

⚖ MIN. ILOŚĆ
   Filtruj pozycje z minimalną średnią tygodniową sprzedażą.

🎨 KOLOROWANIE
   • Jaśniejszy kolor = mniejsze ilości
   • Ciemniejszy kolor = większe ilości
   • Ułatwia szybką identyfikację szczytów zapotrzebowania

📊 PODSUMOWANIE
   • Dzień o największym zapotrzebowaniu - planuj główny ubój
   • Dzień o najmniejszym zapotrzebowaniu (Pon-Pt) - potencjalny dzień na mniejszą produkcję
   • Procenty pokazują udział danego dnia w sprzedaży tygodniowej

💡 WSKAZÓWKI
   ✓ Używaj dłuższych okresów (8-12 tygodni) dla stabilniejszych prognoz
   ✓ Eksportuj dane do dalszej analizy w Excel";

            MessageBox.Show(instrukcja, "📖 Instrukcja użytkowania",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class TowarItem
    {
        public int Id { get; set; }
        public string Kod { get; set; }
        public string Nazwa { get; set; }
        public int LiczbaTransakcji { get; set; }

        public string DisplayText
        {
            get
            {
                if (Id == 0) return Kod;
                return string.IsNullOrEmpty(Nazwa) ? Kod : $"{Kod} - {Nazwa}";
            }
        }
    }
}