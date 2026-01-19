using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.PrognozyUboju;

namespace Kalendarz1.AnalizaTygodniowa
{
    public partial class AnalizaTygodniowaWindow : Window
    {
        private string connectionString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private List<int> wybraniOdbiorcy = new List<int>();
        private List<string> wybraniHandlowcy = new List<string>();

        private List<PodsumowanieDniaModel> pelneDane = new List<PodsumowanieDniaModel>();
        private List<SuroweDaneSQl> suroweDaneHistoryczne = new List<SuroweDaneSQl>();

        public AnalizaTygodniowaWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            dpDataOd.SelectedDate = DateTime.Today.AddDays(-((int)DateTime.Today.DayOfWeek + 6) % 7).AddDays(-7);
            dpDataDo.SelectedDate = DateTime.Today.AddDays(-((int)DateTime.Today.DayOfWeek + 6) % 7).AddDays(-1);
            WczytajTowary();
        }

        private void WczytajTowary()
        {
            try
            {
                string query = @"
                    SELECT DISTINCT TW.id, TW.kod, TW.nazwa
                    FROM [HANDEL].[HM].[TW] TW
                    INNER JOIN [HANDEL].[HM].[DP] DP ON TW.id = DP.idtw
                    WHERE TW.katalog = '67095'
                    ORDER BY TW.kod;";

                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        var towary = new List<TowarItem>
                        {
                            new TowarItem { Id = 0, Kod = "--- Wszystkie towary ---" }
                        };

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                towary.Add(new TowarItem
                                {
                                    Id = reader.GetInt32(0),
                                    Kod = reader.GetString(1),
                                    Nazwa = reader.GetString(2)
                                });
                            }
                        }
                        cmbTowar.ItemsSource = towary;
                        cmbTowar.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania towarów:\n{ex.Message}", "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnAnalizuj_Click(object sender, RoutedEventArgs e)
        {
            if (!dpDataOd.SelectedDate.HasValue || !dpDataDo.SelectedDate.HasValue)
            {
                MessageBox.Show("Proszę wybrać prawidłowy zakres dat.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            txtStatus.Text = "⏳ Analizowanie danych...";
            btnAnalizuj.IsEnabled = false;

            try
            {
                suroweDaneHistoryczne = await PobierzDaneHistoryczneDoPrognozy(dpDataOd.SelectedDate.Value);
                var daneDlaOkresu = await PobierzDaneAnalityczne();
                pelneDane = PrzetworzSuroweDane(daneDlaOkresu);

                dgPodsumowanieDni.ItemsSource = pelneDane;
                AktualizujKPI();

                txtStatus.Text = $"✓ Analiza zakończona. Załadowano {pelneDane.Count} dni.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wystąpił krytyczny błąd podczas analizy danych:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "❌ Błąd analizy.";
            }
            finally
            {
                btnAnalizuj.IsEnabled = true;
            }
        }
        private List<PodsumowanieDniaModel> PrzetworzSuroweDane(List<SuroweDaneSQl> suroweDane)
        {
            var filtrowaneDane = suroweDane.AsEnumerable();

            // Filtrowanie po handlowcach
            if (wybraniHandlowcy.Any())
            {
                filtrowaneDane = filtrowaneDane.Where(d => d.TypOperacji == "PRODUKCJA" || wybraniHandlowcy.Contains(d.Handlowiec));
            }

            // Sprawdź czy ukrywamy korekty
            bool ukryjKorekty = chkUkryjKorekty?.IsChecked == true;

            var grupyPoDacie = filtrowaneDane.GroupBy(d => d.Data.Date);
            var wynik = new List<PodsumowanieDniaModel>();

            foreach (var grupa in grupyPoDacie.OrderBy(g => g.Key))
            {
                var podsumowanie = new PodsumowanieDniaModel { Data = grupa.Key };

                foreach (var wpis in grupa)
                {
                    if (wpis.TypOperacji == "PRODUKCJA")
                    {
                        podsumowanie.IloscWyprodukowana += wpis.Ilosc;
                        podsumowanie.SzczegolyProdukcji.Add(new SzczegolProdukcjiModel
                        {
                            NumerDokumentu = wpis.NumerDokumentu,
                            KodTowaru = wpis.KodTowaru,
                            NazwaTowaru = wpis.NazwaTowaru,
                            Ilosc = wpis.Ilosc
                        });
                    }
                    else // SPRZEDAŻ
                    {
                        var szczegol = new SzczegolSprzedazyModel
                        {
                            NazwaKontrahenta = wpis.NazwaKontrahenta,
                            Handlowiec = wpis.Handlowiec,
                            KodTowaru = wpis.KodTowaru,
                            NazwaTowaru = wpis.NazwaTowaru,
                            Ilosc = wpis.Ilosc,
                            Cena = wpis.Cena,
                            NumerDokumentu = wpis.NumerDokumentu
                        };

                        podsumowanie.SzczegolySprzedazy.Add(szczegol);

                        // POPRAWIONA LOGIKA:
                        // Jeśli ukrywamy korekty I wartość jest ujemna (korekta/zwrot) - NIE dodawaj do sumy
                        if (ukryjKorekty && wpis.Ilosc < 0)
                        {
                            // Pomijamy korekty (wartości ujemne) w obliczeniach
                            continue;
                        }

                        // Dodaj do sumy tylko wartości dodatnie (normalna sprzedaż)
                        // Wartości są już dodatnie w bazie, więc używamy bezpośrednio
                        podsumowanie.IloscSprzedana += wpis.Ilosc;
                    }
                }

                podsumowanie.PrognozaSprzedazy = ObliczPrognozeDlaDnia(grupa.Key);
                wynik.Add(podsumowanie);
            }

            return wynik;
        }

        private async Task<List<SuroweDaneSQl>> PobierzDaneAnalityczne()
        {
            var dataOd = dpDataOd.SelectedDate.Value.Date;
            var dataDo = dpDataDo.SelectedDate.Value.Date;
            int? towarId = cmbTowar.SelectedValue as int?;
            if (towarId == 0) towarId = null;

            var dane = new List<SuroweDaneSQl>();

            string query = @"
                WITH SeriePrzychodowe AS (
                    SELECT 'sPWU' AS seria UNION ALL SELECT 'PWP' UNION ALL
                    SELECT 'PWX'  UNION ALL SELECT 'PRZY' UNION ALL SELECT 'PZ'
                ),
                Przychody AS (
                    SELECT 
                        CAST(MZ.data AS DATE) AS Data,
                        MZ.idtw,
                        TW.kod AS KodTowaru,
                        TW.nazwa AS NazwaTowaru,
                        ABS(MZ.ilosc) AS ilosc,
                        MG.kod AS NumerDokumentu
                    FROM HANDEL.HM.MZ AS MZ
                    JOIN HANDEL.HM.MG AS MG ON MG.id = MZ.super
                    JOIN HANDEL.HM.TW AS TW ON TW.id = MZ.idtw
                    JOIN SeriePrzychodowe S ON S.seria = MG.seria
                    WHERE
                        TW.katalog = 67095
                        AND MG.anulowany = 0
                        AND CAST(MZ.data AS DATE) BETWEEN @DataOd AND @DataDo
                        AND (@TowarID IS NULL OR MZ.idtw = @TowarID)
                ),
                Sprzedaz AS (
                    SELECT 
                        CAST(DK.data AS DATE) AS Data,
                        DP.idtw,
                        TW.kod AS KodTowaru,
                        TW.nazwa AS NazwaTowaru,
                        DP.ilosc,
                        DP.cena,
                        C.id AS KontrahentId,
                        C.shortcut AS NazwaKontrahenta,
                        ISNULL(WYM.CDim_Handlowiec_Val, 'BRAK') AS Handlowiec,
                        DK.kod AS NumerDokumentu
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                    INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.id
                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                    WHERE DK.anulowany = 0
                      AND TW.katalog = '67095'
                      AND CAST(DK.data AS DATE) BETWEEN @DataOd AND @DataDo
                      AND (@TowarID IS NULL OR DP.idtw = @TowarID)
                )
                SELECT 
                    'PRODUKCJA' AS TypOperacji, Data, idtw, KodTowaru, NazwaTowaru, ilosc, 0 AS cena, 
                    NULL AS KontrahentId, NULL AS NazwaKontrahenta, NULL AS Handlowiec, NumerDokumentu 
                FROM Przychody
                UNION ALL
                SELECT 
                    'SPRZEDAZ' AS TypOperacji, Data, idtw, KodTowaru, NazwaTowaru, ilosc, cena, 
                    KontrahentId, NazwaKontrahenta, Handlowiec, NumerDokumentu
                FROM Sprzedaz;
            ";

            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@DataOd", dataOd);
                    cmd.Parameters.AddWithValue("@DataDo", dataDo);
                    cmd.Parameters.AddWithValue("@TowarID", (object)towarId ?? DBNull.Value);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            dane.Add(new SuroweDaneSQl
                            {
                                TypOperacji = reader["TypOperacji"].ToString(),
                                Data = Convert.ToDateTime(reader["Data"]),
                                KodTowaru = reader["KodTowaru"].ToString(),
                                NazwaTowaru = reader["NazwaTowaru"].ToString(),
                                Ilosc = Convert.ToDecimal(reader["ilosc"]),
                                Cena = Convert.ToDecimal(reader["cena"]),
                                NazwaKontrahenta = reader["NazwaKontrahenta"] as string,
                                Handlowiec = reader["Handlowiec"] as string,
                                NumerDokumentu = reader["NumerDokumentu"].ToString()
                            });
                        }
                    }
                }
            }
            return dane;
        }

        private async Task<List<SuroweDaneSQl>> PobierzDaneHistoryczneDoPrognozy(DateTime dataStartOkresu)
        {
            var dane = new List<SuroweDaneSQl>();
            string query = @"
                SELECT 
                    CAST(DK.data AS DATE) AS Data,
                    DP.idtw,
                    DP.ilosc
                FROM [HANDEL].[HM].[DK] DK
                INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.id
                WHERE DK.anulowany = 0
                  AND TW.katalog = '67095'
                  AND CAST(DK.data AS DATE) BETWEEN @PrognozaDataOd AND @PrognozaDataDo
            ";

            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PrognozaDataOd", dataStartOkresu.AddDays(-56));
                    cmd.Parameters.AddWithValue("@PrognozaDataDo", dataStartOkresu.AddDays(-1));
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            dane.Add(new SuroweDaneSQl
                            {
                                Data = reader.GetDateTime(0),
                                Ilosc = Convert.ToDecimal(reader[2])
                            });
                        }
                    }
                }
            }
            return dane;
        }

        private void OdswiezSzczegolySprzedazy()
        {
            if (dgPodsumowanieDni.SelectedItem is PodsumowanieDniaModel wybranyDzien)
            {
                var szczegoly = wybranyDzien.SzczegolySprzedazy;

                if (chkUkryjKorekty.IsChecked == true)
                {
                    szczegoly = szczegoly.Where(s => s.Ilosc > 0).ToList();
                }

                dgSzczegolySprzedazy.ItemsSource = szczegoly.OrderByDescending(s => s.Ilosc);

                // ✅ NOWE: Usuń stary handler i dodaj nowy
                dgSzczegolySprzedazy.MouseDoubleClick -= DgSzczegolySprzedazy_MouseDoubleClick;
                dgSzczegolySprzedazy.MouseDoubleClick += DgSzczegolySprzedazy_MouseDoubleClick;
            }
            else
            {
                dgSzczegolySprzedazy.ItemsSource = null;
            }
        }
        // ✅ NOWA METODA: Dwuklik w szczegółach sprzedaży - otwiera szczegóły faktury
        // ✅ ZAKTUALIZOWANA METODA: Dwuklik - otwiera WPF okno
        private void DgSzczegolySprzedazy_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgSzczegolySprzedazy.SelectedItem is SzczegolSprzedazyModel szczegol)
            {
                int? idDokumentu = PobierzIdDokumentu(szczegol.NumerDokumentu);

                if (idDokumentu.HasValue)
                {
                    // ✅ ZMIANA: WPF okno zamiast WinForms
                    var windowSzczegoly = new SzczegolyDokumentuWindow(
                        connectionString,
                        idDokumentu.Value,
                        szczegol.NumerDokumentu);

                    windowSzczegoly.ShowDialog();
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        $"❌ Nie znaleziono dokumentu o numerze: {szczegol.NumerDokumentu}",
                        "Błąd",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }
        // ✅ NOWA METODA: Pomocnicza - pobiera ID dokumentu
        private int? PobierzIdDokumentu(string numerDokumentu)
        {
            try
            {
                string query = "SELECT id FROM [HANDEL].[HM].[DK] WHERE kod = @NumerDokumentu";

                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@NumerDokumentu", numerDokumentu);
                        var result = cmd.ExecuteScalar();

                        if (result != null && result != DBNull.Value)
                        {
                            return Convert.ToInt32(result);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"❌ Błąd pobierania ID dokumentu: {ex.Message}",
                    "Błąd",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }

            return null;
        }
        private decimal ObliczPrognozeDlaDnia(DateTime data)
        {
            var dayOfWeek = data.DayOfWeek;
            var historyczneDlaDnia = suroweDaneHistoryczne.Where(h => h.Data.DayOfWeek == dayOfWeek);
            if (!historyczneDlaDnia.Any()) return 0;

            int liczbaTygodni = historyczneDlaDnia.Select(h => CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(h.Data, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday)).Distinct().Count();
            if (liczbaTygodni == 0) return 0;

            decimal sumaHistoryczna = historyczneDlaDnia.Sum(h => -h.Ilosc);
            return sumaHistoryczna / liczbaTygodni;
        }

        private void AktualizujKPI()
        {
            if (pelneDane == null || !pelneDane.Any())
            {
                kpiProdukcja.Text = "0 kg";
                kpiSprzedaz.Text = "0 kg";
                kpiProcent.Text = "0%";
                kpiWariancja.Text = "0 kg";
                return;
            }

            decimal totalProdukcja = pelneDane.Sum(d => d.IloscWyprodukowana);
            decimal totalSprzedaz = pelneDane.Sum(d => d.IloscSprzedana);
            decimal wariancja = totalProdukcja - totalSprzedaz;
            decimal procent = totalProdukcja > 0 ? (totalSprzedaz / totalProdukcja) * 100 : 0;

            kpiProdukcja.Text = $"{totalProdukcja:N0} kg";
            kpiSprzedaz.Text = $"{totalSprzedaz:N0} kg";
            kpiProcent.Text = $"{procent:N1}%";
            kpiWariancja.Text = $"{wariancja:N0} kg";
        }


 
        private void DgPodsumowanieDni_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            if (dgPodsumowanieDni.SelectedItem is PodsumowanieDniaModel wybranyDzien)
            {
                dgSzczegolyProdukcji.ItemsSource = wybranyDzien.SzczegolyProdukcji.OrderByDescending(p => p.Ilosc);
                OdswiezSzczegolySprzedazy();
            }
            else
            {
                dgSzczegolyProdukcji.ItemsSource = null;
                dgSzczegolySprzedazy.ItemsSource = null;
            }
        }


        private void ChkUkryjKorekty_Click(object sender, RoutedEventArgs e)
        {
            // Jeśli mamy już załadowane dane, przelicz je ponownie
            if (dgPodsumowanieDni.ItemsSource != null && pelneDane.Any())
            {
                // Zapisz surowe dane i przetworz je ponownie z nowym ustawieniem checkboxa
                BtnAnalizuj_Click(sender, e);
            }
            else
            {
                // Jeśli nie ma danych, tylko odśwież szczegóły
                OdswiezSzczegolySprzedazy();
            }
        }
        private void BtnWybierzHandlowcow_Click(object sender, RoutedEventArgs e)
        {
            var form = new PrognozyUboju.FormWyborHandlowcow(connectionString, wybraniHandlowcy);
            if (form.ShowDialog() == true)
            {
                wybraniHandlowcy = form.WybraniHandlowcy;
                btnWybierzHandlowcow.Content = wybraniHandlowcy.Any() ? $"{wybraniHandlowcy.Count} wybranych" : "Wybierz...";
            }
        }

        private void BtnWybierzOdbiorcow_Click(object sender, RoutedEventArgs e)
        {
            var form = new PrognozyUboju.FormWyborKontrahentow(connectionString, wybraniOdbiorcy);
            if (form.ShowDialog() == true)
            {
                wybraniOdbiorcy = form.WybraniKontrahenci;
                btnWybierzOdbiorcow.Content = wybraniOdbiorcy.Any() ? $"{wybraniOdbiorcy.Count} wybranych" : "Wybierz...";
            }
        }

        private void BtnInstrukcja_Click(object sender, RoutedEventArgs e)
        {
            string instrukcja = @"
📖 INSTRUKCJA OBSŁUGI DASHBOARDU ANALITYCZNEGO

🎯 GŁÓWNY CEL:
Narzędzie to służy do analizy bilansu między produkcją a sprzedażą świeżych towarów (katalog 67095) w ujęciu dziennym. Pomaga identyfikować nadwyżki (ryzyko strat) oraz niedobory, dostarczając danych do planowania operacyjnego i rozmów z zespołem handlowym.

---

📋 KROKI ANALIZY:

1. WYBIERZ FILTRY:
   • Zakres dat: Wybierz okres, który chcesz analizować (np. poprzedni tydzień).
   • Towar: Skup się na konkretnym produkcie lub analizuj wszystkie.
   • Handlowiec/Odbiorca: Zawęź analizę do konkretnych handlowców lub klientów.
   • Ukryj korekty: Zaznacz, aby ukryć zwroty i skupić się na czystej sprzedaży.

2. KLIKNIJ 'ANALIZUJ':
   System pobierze i przetworzy dane. Może to potrwać chwilę.

3. SPRAWDŹ WSKAŹNIKI (KPI):
   Górne karty dają Ci szybki obraz całej sytuacji w wybranym okresie.

4. ANALIZUJ TABELĘ DZIENNĄ:
   Główna tabela po lewej stronie pokazuje bilans dla każdego dnia:
   • Wariancja (Prod-Sprzed): KLUCZOWA METRYKA!
     - Czerwone tło (wartość > 0): Niesprzedany towar - ryzyko.
     - Zielone tło (wartość < 0): Sprzedano więcej niż wyprodukowano (zużyto zapasy).

5. PRZEJDŹ DO SZCZEGÓŁÓW (DRILL-DOWN):
   • Kliknij na interesujący Cię wiersz (dzień) w tabeli po lewej.
   • Po prawej stronie, w zakładkach 'Szczegóły Sprzedaży' i 'Szczegóły Produkcji', zobaczysz wszystkie transakcje dla tego dnia.
   • Wartości sprzedaży są UJEMNE (rozchód), a zwroty DODATNIE (przychód).
";
            MessageBox.Show(instrukcja, "Instrukcja obsługi dashboardu", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}