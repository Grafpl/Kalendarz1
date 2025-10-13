using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    // Konwerter sprawdzający czy wartość > 0
    public class GreaterThanZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
                return intValue > 0;
            if (value is decimal decValue)
                return decValue > 0;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class TygodniowyPlan : Window
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string connectionStringHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private DateTime aktualnyTydzien;
        private Dictionary<string, decimal> konfiguracjaProduktow;
        private List<PlanDziennyModel> aktualneDane;
        private WydajnoscModel aktualnaWydajnosc;

        // Ograniczenia dla handlowców
        private DateTime minData;
        private DateTime maxData;

        public TygodniowyPlan()
        {
            InitializeComponent();
            aktualnyTydzien = DateTime.Today;
            konfiguracjaProduktow = new Dictionary<string, decimal>();
            aktualneDane = new List<PlanDziennyModel>();

            // Ustaw ograniczenia dat - 6 tygodni wstecz, 2 tygodnie do przodu
            minData = DateTime.Today.AddDays(-42); // 6 tygodni wstecz
            maxData = DateTime.Today.AddDays(14);  // 2 tygodnie do przodu

            // Inicjalizacja domyślnej wydajności
            aktualnaWydajnosc = new WydajnoscModel
            {
                WspolczynnikTuszki = 78m,
                ProcentTuszkaA = 85m,
                ProcentTuszkaB = 15m
            };

            InicjalizujDomyslneWartosci();
            OdswiezWidok();
        }

        private void InicjalizujDomyslneWartosci()
        {
            konfiguracjaProduktow["Cwiartka"] = 38m;
            konfiguracjaProduktow["Skrzydlo"] = 9m;
            konfiguracjaProduktow["Filet"] = 28m;
            konfiguracjaProduktow["Korpus"] = 19m;

            WczytajKonfiguracjeProduktow();
        }

        private void WczytajKonfiguracjeProduktow()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"SELECT NazwaTowaru, ProcentUdzialu 
                                   FROM KonfiguracjaProduktow 
                                   WHERE Aktywny = 1";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string nazwa = reader["NazwaTowaru"].ToString();
                            decimal procent = Convert.ToDecimal(reader["ProcentUdzialu"]);
                            konfiguracjaProduktow[nazwa] = procent;
                        }
                    }
                }
            }
            catch
            {
                // Użyj domyślnych wartości
            }
        }

        private void WczytajWydajnoscDlaDaty(DateTime data)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT TOP 1 WspolczynnikTuszki, ProcentTuszkaA, ProcentTuszkaB
                        FROM KonfiguracjaWydajnosci
                        WHERE DataOd <= @Data AND Aktywny = 1
                        ORDER BY DataOd DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Data", data);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                aktualnaWydajnosc.WspolczynnikTuszki = Convert.ToDecimal(reader["WspolczynnikTuszki"]);
                                aktualnaWydajnosc.ProcentTuszkaA = Convert.ToDecimal(reader["ProcentTuszkaA"]);
                                aktualnaWydajnosc.ProcentTuszkaB = Convert.ToDecimal(reader["ProcentTuszkaB"]);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Użyj domyślnych wartości
            }
        }

        private bool CzyMoznaNavigowac(DateTime docelowaTydzien)
        {
            DateTime poniedzialek = GetPoniedzialek(docelowaTydzien);

            if (poniedzialek < minData)
            {
                MessageBox.Show($"Nie można wyświetlić danych starszych niż {minData:yyyy-MM-dd}.\n\nOgraniczenie: 6 tygodni wstecz.",
                              "Ograniczenie dostępu", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            if (poniedzialek > maxData)
            {
                MessageBox.Show($"Nie można wyświetlić danych późniejszych niż {maxData:yyyy-MM-dd}.\n\nOgraniczenie: 2 tygodnie do przodu.",
                              "Ograniczenie dostępu", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            return true;
        }

        private void OdswiezWidok()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                DateTime poniedzialek = GetPoniedzialek(aktualnyTydzien);
                DateTime niedziela = poniedzialek.AddDays(6);

                WczytajWydajnoscDlaDaty(poniedzialek);

                CultureInfo ciCurr = CultureInfo.CurrentCulture;
                int weekNum = ciCurr.Calendar.GetWeekOfYear(poniedzialek, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

                txtDataZakres.Text = $"{poniedzialek:dd.MM.yyyy} - {niedziela:dd.MM.yyyy}";
                txtNumerTygodnia.Text = $"(Tydzień {weekNum}/{poniedzialek.Year})";

                aktualneDane = PobierzDaneTygodnia(poniedzialek, niedziela);

                // Dodaj wiersz SUMA
                var wierszSuma = new PlanDziennyModel
                {
                    Data = "SUMA",
                    DzienTygodnia = "",
                    LiczbaAut = aktualneDane.Sum(x => x.LiczbaAut),
                    LiczbaUbiorek = aktualneDane.Sum(x => x.LiczbaUbiorek),  // NOWE
                    ZywiecKg = aktualneDane.Sum(x => x.ZywiecKg),
                    Sztuki = aktualneDane.Sum(x => x.Sztuki),
                    WagaSrednia = aktualneDane.Sum(x => x.Sztuki) > 0 ? aktualneDane.Sum(x => x.ZywiecKg) / aktualneDane.Sum(x => x.Sztuki) : 0,
                    TuszkaCalkowita = aktualneDane.Sum(x => x.TuszkaCalkowita),
                    TuszkaAB = $"{aktualnaWydajnosc.ProcentTuszkaA:F0}/{aktualnaWydajnosc.ProcentTuszkaB:F0}",
                    TuszkaA = aktualneDane.Sum(x => x.TuszkaA),
                    Cwiartka = aktualneDane.Sum(x => x.Cwiartka),
                    Skrzydlo = aktualneDane.Sum(x => x.Skrzydlo),
                    Filet = aktualneDane.Sum(x => x.Filet),
                    Korpus = aktualneDane.Sum(x => x.Korpus),
                    Pojemniki9 = aktualneDane.Sum(x => x.Pojemniki9),
                    Pojemniki10 = aktualneDane.Sum(x => x.Pojemniki10),
                    Pojemniki11 = aktualneDane.Sum(x => x.Pojemniki11),
                    Pojemniki12 = aktualneDane.Sum(x => x.Pojemniki12),
                    Pojemniki8 = aktualneDane.Sum(x => x.Pojemniki8),
                    Pojemniki7 = aktualneDane.Sum(x => x.Pojemniki7),
                    Pojemniki6 = aktualneDane.Sum(x => x.Pojemniki6),
                    Pojemniki5 = aktualneDane.Sum(x => x.Pojemniki5),
                    StatusTekst = "PODSUMOWANIE",
                    JestSuma = true
                };

                aktualneDane.Add(wierszSuma);

                dgPlan.ItemsSource = null;
                dgPlan.ItemsSource = aktualneDane;

                ObliczStatystyki(aktualneDane);
                ObliczStatystykiPojemnikow(aktualneDane);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd odświeżania widoku: {ex.Message}",
                              "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private DateTime GetPoniedzialek(DateTime data)
        {
            int diff = (7 + (data.DayOfWeek - DayOfWeek.Monday)) % 7;
            return data.AddDays(-1 * diff).Date;
        }

        private List<PlanDziennyModel> PobierzDaneTygodnia(DateTime od, DateTime doo)
        {
            List<PlanDziennyModel> wynik = new List<PlanDziennyModel>();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Inicjalizuj 7 dni tygodnia
                    for (int i = 0; i < 7; i++)
                    {
                        DateTime dzien = od.AddDays(i);
                        wynik.Add(new PlanDziennyModel
                        {
                            Data = dzien.ToString("yyyy-MM-dd"),
                            DzienTygodnia = GetPolskiDzienTygodnia(dzien),
                            LiczbaAut = 0,
                            ZywiecKg = 0,
                            Sztuki = 0,
                            WagaSrednia = 0,
                            CzyPotwierdzone = false,
                            ProcentPotwierdzenia = 0
                        });
                    }

                    // Pobierz WSZYSTKIE dostawy z tygodnia (każdą dostawę osobno)
                    string query = @"
                        SELECT 
                            DataOdbioru,
                            Auta,
                            SztukiDek,
                            WagaDek,
                            PotwWaga,
                            PotwSztuki,
                            Ubiorka
                        FROM HarmonogramDostaw
                        WHERE DataOdbioru >= @Od AND DataOdbioru <= @Do
                        AND Bufor IN ('B.Wolny', 'B.Kontr.', 'Potwierdzony')
                        ORDER BY DataOdbioru";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Od", od);
                        cmd.Parameters.AddWithValue("@Do", doo);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                DateTime dataOdbioru = Convert.ToDateTime(reader["DataOdbioru"]);

                                // Bezpieczne odczytywanie z obsługą NULL
                                int auta = 0;
                                if (!reader.IsDBNull(reader.GetOrdinal("Auta")))
                                {
                                    try { auta = Convert.ToInt32(reader["Auta"]); }
                                    catch { auta = 0; }
                                }

                                int sztuki = 0;
                                if (!reader.IsDBNull(reader.GetOrdinal("SztukiDek")))
                                {
                                    try { sztuki = Convert.ToInt32(reader["SztukiDek"]); }
                                    catch { sztuki = 0; }
                                }

                                decimal wagaDek = 0;
                                if (!reader.IsDBNull(reader.GetOrdinal("WagaDek")))
                                {
                                    try { wagaDek = Convert.ToDecimal(reader["WagaDek"]); }
                                    catch { wagaDek = 0; }
                                }

                                bool potwWaga = false;
                                if (!reader.IsDBNull(reader.GetOrdinal("PotwWaga")))
                                {
                                    try { potwWaga = Convert.ToBoolean(reader["PotwWaga"]); }
                                    catch { potwWaga = false; }
                                }

                                bool potwSztuki = false;
                                if (!reader.IsDBNull(reader.GetOrdinal("PotwSztuki")))
                                {
                                    try { potwSztuki = Convert.ToBoolean(reader["PotwSztuki"]); }
                                    catch { potwSztuki = false; }
                                }

                                int ubiorka = 0;
                                if (!reader.IsDBNull(reader.GetOrdinal("Ubiorka")))
                                {
                                    try { ubiorka = Convert.ToInt32(reader["Ubiorka"]); }
                                    catch { ubiorka = 0; }
                                }

                                decimal zywiecKg = sztuki * wagaDek;

                                // Znajdź odpowiedni dzień w wyniku
                                var dzien = wynik.FirstOrDefault(x => x.Data == dataOdbioru.ToString("yyyy-MM-dd"));
                                if (dzien != null)
                                {
                                    // Sumuj podstawowe wartości
                                    dzien.LiczbaAut += auta;
                                    dzien.LiczbaUbiorek += ubiorka;  // NOWE
                                    dzien.ZywiecKg += zywiecKg;
                                    dzien.Sztuki += sztuki;

                                    // Oblicz pojemniki dla tej konkretnej dostawy i dodaj do sumy dnia
                                    ObliczIPojemnikiDlaDostawy(dzien, wagaDek, zywiecKg);
                                }
                            }
                        }
                    }

                    // Teraz dla każdego dnia oblicz pozostałe wartości
                    foreach (var dzien in wynik)
                    {
                        if (dzien.Sztuki > 0)
                        {
                            dzien.WagaSrednia = dzien.ZywiecKg / dzien.Sztuki;
                        }

                        // Oblicz produkty (tuszka, filet, etc.)
                        ObliczProdukty(dzien);

                        // Oblicz status potwierdzeń
                        ObliczStatusPotwierdzenia(dzien, conn, od, doo);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd pobierania danych: {ex.Message}\n\nSzczegóły: {ex.StackTrace}",
                              "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return wynik;
        }

        private void ObliczIPojemnikiDlaDostawy(PlanDziennyModel dzien, decimal wagaDek, decimal zywiecKg)
        {
            try
            {
                if (wagaDek <= 0 || zywiecKg <= 0)
                    return;

                // Oblicz tuszką A dla tej dostawy
                decimal tuszkaCalkowita = zywiecKg * (aktualnaWydajnosc.WspolczynnikTuszki / 100m);
                decimal tuszkaA = tuszkaCalkowita * (aktualnaWydajnosc.ProcentTuszkaA / 100m);

                if (tuszkaA <= 0)
                    return;

                // Oblicz wagę tuszki dla pojedynczego kurczaka
                decimal tuszkaASztuka = wagaDek * (aktualnaWydajnosc.WspolczynnikTuszki / 100m);

                if (tuszkaASztuka <= 0)
                    return;

                // Ile sztuk mieści się w pojemniku 15kg
                decimal sztukWPojemniku = 15m / tuszkaASztuka;

                // Ile pojemników potrzeba dla tej dostawy
                decimal liczbaPojemnikow = tuszkaA / 15m;

                // Rozmiar dolny i górny
                int rozmiarDolny = (int)Math.Floor(sztukWPojemniku);
                int rozmiarGorny = (int)Math.Ceiling(sztukWPojemniku);

                // Część dziesiętna - determinuje proporcję
                decimal fractional = sztukWPojemniku - rozmiarDolny;

                int pojemnikiGorne = 0;
                int pojemnikiDolne = 0;

                if (rozmiarDolny == rozmiarGorny)
                {
                    pojemnikiDolne = (int)Math.Ceiling(liczbaPojemnikow);
                }
                else
                {
                    pojemnikiGorne = (int)Math.Round(liczbaPojemnikow * fractional);
                    pojemnikiDolne = (int)Math.Ceiling(liczbaPojemnikow) - pojemnikiGorne;
                }

                // Przypisz do odpowiednich kategorii - rozmiar dolny
                if (rozmiarDolny >= 12) dzien.Pojemniki12 += pojemnikiDolne;
                else if (rozmiarDolny == 11) dzien.Pojemniki11 += pojemnikiDolne;
                else if (rozmiarDolny == 10) dzien.Pojemniki10 += pojemnikiDolne;
                else if (rozmiarDolny == 9) dzien.Pojemniki9 += pojemnikiDolne;
                else if (rozmiarDolny == 8) dzien.Pojemniki8 += pojemnikiDolne;
                else if (rozmiarDolny == 7) dzien.Pojemniki7 += pojemnikiDolne;
                else if (rozmiarDolny == 6) dzien.Pojemniki6 += pojemnikiDolne;
                else if (rozmiarDolny >= 5) dzien.Pojemniki5 += pojemnikiDolne;

                // Przypisz do odpowiednich kategorii - rozmiar górny
                if (pojemnikiGorne > 0)
                {
                    if (rozmiarGorny >= 12) dzien.Pojemniki12 += pojemnikiGorne;
                    else if (rozmiarGorny == 11) dzien.Pojemniki11 += pojemnikiGorne;
                    else if (rozmiarGorny == 10) dzien.Pojemniki10 += pojemnikiGorne;
                    else if (rozmiarGorny == 9) dzien.Pojemniki9 += pojemnikiGorne;
                    else if (rozmiarGorny == 8) dzien.Pojemniki8 += pojemnikiGorne;
                    else if (rozmiarGorny == 7) dzien.Pojemniki7 += pojemnikiGorne;
                    else if (rozmiarGorny == 6) dzien.Pojemniki6 += pojemnikiGorne;
                    else if (rozmiarGorny >= 5) dzien.Pojemniki5 += pojemnikiGorne;
                }
            }
            catch (Exception ex)
            {
                // Logowanie błędu bez przerywania działania programu
                System.Diagnostics.Debug.WriteLine($"Błąd obliczania pojemników: {ex.Message}");
            }
        }

        private void ObliczStatusPotwierdzenia(PlanDziennyModel dzien, SqlConnection conn, DateTime od, DateTime doo)
        {
            try
            {
                DateTime dataWyszukiwania = DateTime.Parse(dzien.Data);

                string query = @"
                    SELECT 
                        COUNT(CASE WHEN PotwWaga = 1 AND PotwSztuki = 1 THEN 1 END) as LiczbaPotwierdzonych,
                        COUNT(*) as LiczbaRekordow
                    FROM HarmonogramDostaw
                    WHERE DataOdbioru = @Data
                    AND Bufor IN ('B.Wolny', 'B.Kontr.', 'Potwierdzony')";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Data", dataWyszukiwania);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int liczbaPotwierdzonych = 0;
                            int liczbaRekordow = 0;

                            if (!reader.IsDBNull(reader.GetOrdinal("LiczbaPotwierdzonych")))
                            {
                                try { liczbaPotwierdzonych = Convert.ToInt32(reader["LiczbaPotwierdzonych"]); }
                                catch { liczbaPotwierdzonych = 0; }
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("LiczbaRekordow")))
                            {
                                try { liczbaRekordow = Convert.ToInt32(reader["LiczbaRekordow"]); }
                                catch { liczbaRekordow = 0; }
                            }

                            dzien.CzyPotwierdzone = (liczbaPotwierdzonych == liczbaRekordow && liczbaRekordow > 0);
                            dzien.ProcentPotwierdzenia = liczbaRekordow > 0 ?
                                (decimal)liczbaPotwierdzonych / liczbaRekordow * 100 : 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // W razie błędu pozostaw domyślne wartości
                dzien.CzyPotwierdzone = false;
                dzien.ProcentPotwierdzenia = 0;
            }
        }

        private void ObliczProdukty(PlanDziennyModel dzien)
        {
            decimal tuszkaCalkowita = dzien.ZywiecKg * (aktualnaWydajnosc.WspolczynnikTuszki / 100m);
            dzien.TuszkaCalkowita = tuszkaCalkowita;

            dzien.TuszkaA = tuszkaCalkowita * (aktualnaWydajnosc.ProcentTuszkaA / 100m);
            dzien.TuszkaB = tuszkaCalkowita * (aktualnaWydajnosc.ProcentTuszkaB / 100m);

            dzien.TuszkaAB = $"{aktualnaWydajnosc.ProcentTuszkaA:F0}/{aktualnaWydajnosc.ProcentTuszkaB:F0}";

            dzien.Cwiartka = dzien.TuszkaB * (konfiguracjaProduktow.GetValueOrDefault("Cwiartka", 38m) / 100m);
            dzien.Skrzydlo = dzien.TuszkaB * (konfiguracjaProduktow.GetValueOrDefault("Skrzydlo", 9m) / 100m);
            dzien.Filet = dzien.TuszkaB * (konfiguracjaProduktow.GetValueOrDefault("Filet", 28m) / 100m);
            dzien.Korpus = dzien.TuszkaB * (konfiguracjaProduktow.GetValueOrDefault("Korpus", 19m) / 100m);

            // Pojemniki są już obliczone w ObliczIPojemnikiDlaDostawy - nie liczymy ich tutaj ponownie

            if (dzien.CzyPotwierdzone)
                dzien.StatusTekst = "✓ W pełni potwierdzone";
            else if (dzien.ProcentPotwierdzenia > 0)
                dzien.StatusTekst = $"⚠ Częściowo ({dzien.ProcentPotwierdzenia:F0}%)";
            else if (dzien.ZywiecKg > 0)
                dzien.StatusTekst = "○ Zaplanowane";
            else
                dzien.StatusTekst = "— Brak dostaw";
        }

        private string GetPolskiDzienTygodnia(DateTime data)
        {
            CultureInfo polskaCultura = new CultureInfo("pl-PL");
            string dzien = polskaCultura.DateTimeFormat.GetDayName(data.DayOfWeek);
            return char.ToUpper(dzien[0]) + dzien.Substring(1);
        }

        private void ObliczStatystyki(List<PlanDziennyModel> dane)
        {
            var daneRzeczywiste = dane.Where(x => !x.JestSuma).ToList();

            decimal sumaSurowiec = daneRzeczywiste.Sum(x => x.ZywiecKg);
            int sumaSztuki = daneRzeczywiste.Sum(x => x.Sztuki);
            decimal sumaTuszkaA = daneRzeczywiste.Sum(x => x.TuszkaA);
            decimal sumaFilet = daneRzeczywiste.Sum(x => x.Filet);
            decimal sumaCwiartka = daneRzeczywiste.Sum(x => x.Cwiartka);

            decimal sumaPotwierdzonych = daneRzeczywiste.Where(x => x.CzyPotwierdzone).Sum(x => x.ZywiecKg);
            decimal procentPotwierdzenia = sumaSurowiec > 0 ? (sumaPotwierdzonych / sumaSurowiec * 100) : 0;

            txtStatSurowiec.Text = $"{sumaSurowiec:N0} kg";
            txtStatTuszkaA.Text = $"{sumaTuszkaA:N0} kg";
            txtStatFilet.Text = $"{sumaFilet:N0} kg";
            txtStatPotwierdzenie.Text = $"{procentPotwierdzenia:F0}%";
            progressPotwierdzenie.Value = (double)procentPotwierdzenia;

            txtSumaSurowiec.Text = $"{sumaSurowiec:N0} kg";
            txtSumaTuszkaA.Text = $"{sumaTuszkaA:N0} kg";
            txtSumaFilet.Text = $"{sumaFilet:N0} kg";
            txtSumaCwiartka.Text = $"{sumaCwiartka:N0} kg";
        }

        private void ObliczStatystykiPojemnikow(List<PlanDziennyModel> dane)
        {
            // Pobierz wiersz SUMA
            var suma = dane.FirstOrDefault(x => x.JestSuma);
            if (suma == null) return;

            int sumaCala = suma.Pojemniki12 + suma.Pojemniki11 + suma.Pojemniki10 + suma.Pojemniki9 +
                           suma.Pojemniki8 + suma.Pojemniki7 + suma.Pojemniki6 + suma.Pojemniki5;

            if (sumaCala == 0)
            {
                // Wyczyść wszystkie pola jeśli nie ma danych
                txtPoj12Sztuk.Text = "0 poj";
                txtPoj11Sztuk.Text = "0 poj";
                txtPoj10Sztuk.Text = "0 poj";
                txtPoj9Sztuk.Text = "0 poj";
                txtPoj8Sztuk.Text = "0 poj";
                txtPoj7Sztuk.Text = "0 poj";
                txtPoj6Sztuk.Text = "0 poj";
                txtPoj5Sztuk.Text = "0 poj";

                txtPoj12Procent.Text = "0%";
                txtPoj11Procent.Text = "0%";
                txtPoj10Procent.Text = "0%";
                txtPoj9Procent.Text = "0%";
                txtPoj8Procent.Text = "0%";
                txtPoj7Procent.Text = "0%";
                txtPoj6Procent.Text = "0%";
                txtPoj5Procent.Text = "0%";

                txtPoj12Palety.Text = "";
                txtPoj11Palety.Text = "";
                txtPoj10Palety.Text = "";
                txtPoj9Palety.Text = "";
                txtPoj8Palety.Text = "";
                txtPoj7Palety.Text = "";
                txtPoj6Palety.Text = "";
                txtPoj5Palety.Text = "";

                txtMalyKurczakSuma.Text = "0 poj (0%)";
                txtDuzyKurczakSuma.Text = "0 poj (0%)";
                return;
            }

            // Obliczenia dla każdego typu pojemnika
            AktualizujStatystykePojemnika(txtPoj12Sztuk, txtPoj12Procent, txtPoj12Palety, suma.Pojemniki12, sumaCala);
            AktualizujStatystykePojemnika(txtPoj11Sztuk, txtPoj11Procent, txtPoj11Palety, suma.Pojemniki11, sumaCala);
            AktualizujStatystykePojemnika(txtPoj10Sztuk, txtPoj10Procent, txtPoj10Palety, suma.Pojemniki10, sumaCala);
            AktualizujStatystykePojemnika(txtPoj9Sztuk, txtPoj9Procent, txtPoj9Palety, suma.Pojemniki9, sumaCala);
            AktualizujStatystykePojemnika(txtPoj8Sztuk, txtPoj8Procent, txtPoj8Palety, suma.Pojemniki8, sumaCala);
            AktualizujStatystykePojemnika(txtPoj7Sztuk, txtPoj7Procent, txtPoj7Palety, suma.Pojemniki7, sumaCala);
            AktualizujStatystykePojemnika(txtPoj6Sztuk, txtPoj6Procent, txtPoj6Palety, suma.Pojemniki6, sumaCala);
            AktualizujStatystykePojemnika(txtPoj5Sztuk, txtPoj5Procent, txtPoj5Palety, suma.Pojemniki5, sumaCala);

            // Oblicz sumy dla małego i dużego kurczaka
            int sumaMaly = suma.Pojemniki12 + suma.Pojemniki11 + suma.Pojemniki10 + suma.Pojemniki9;
            int sumaDuzy = suma.Pojemniki8 + suma.Pojemniki7 + suma.Pojemniki6 + suma.Pojemniki5;

            decimal procentMaly = sumaCala > 0 ? (decimal)sumaMaly / sumaCala * 100 : 0;
            decimal procentDuzy = sumaCala > 0 ? (decimal)sumaDuzy / sumaCala * 100 : 0;

            txtMalyKurczakSuma.Text = $"{sumaMaly:N0} poj ({procentMaly:F1}%)";
            txtDuzyKurczakSuma.Text = $"{sumaDuzy:N0} poj ({procentDuzy:F1}%)";
        }

        private void AktualizujStatystykePojemnika(TextBlock txtSztuk, TextBlock txtProcent, TextBlock txtPalety,
                                                   int liczba, int sumaCala)
        {
            // Liczba pojemników
            txtSztuk.Text = $"{liczba:N0} poj";

            // Procent
            decimal procent = sumaCala > 0 ? (decimal)liczba / sumaCala * 100 : 0;
            txtProcent.Text = $"{procent:F1}%";

            // Palety (36 poj/paleta)
            if (liczba == 0)
            {
                txtPalety.Text = "";
            }
            else
            {
                int pelne = liczba / 36;
                int reszta = liczba % 36;

                if (pelne > 0 && reszta > 0)
                    txtPalety.Text = $"{pelne} pal + {reszta} poj";
                else if (pelne > 0)
                    txtPalety.Text = $"{pelne} palet";
                else
                    txtPalety.Text = $"{reszta} poj";
            }
        }

        private void DgPlan_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            var plan = e.Row.Item as PlanDziennyModel;
            if (plan != null)
            {
                if (plan.JestSuma)
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(52, 73, 94));
                    e.Row.Foreground = Brushes.White;
                    e.Row.FontWeight = FontWeights.Bold;
                }
                else if (plan.CzyPotwierdzone && plan.ZywiecKg > 0)
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(200, 230, 201));
                }
                else if (plan.ProcentPotwierdzenia > 0 && plan.ZywiecKg > 0)
                {
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 249, 196));
                }
                else if (plan.ZywiecKg == 0)
                {
                    e.Row.Background = Brushes.White;
                    e.Row.Foreground = new SolidColorBrush(Color.FromRgb(189, 195, 199));
                }
            }
        }

        private void DgPlan_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var plan = dgPlan.SelectedItem as PlanDziennyModel;
            if (plan != null && plan.ZywiecKg > 0 && !plan.JestSuma)
            {
                DateTime dataWybrana = DateTime.Parse(plan.Data);
                var oknoSzczegoly = new SzczegolyDnia(connectionString, dataWybrana, aktualnaWydajnosc, konfiguracjaProduktow);
                oknoSzczegoly.ShowDialog();
            }
        }

        private void BtnPoprzedniTydzien_Click(object sender, RoutedEventArgs e)
        {
            DateTime nowyTydzien = aktualnyTydzien.AddDays(-7);
            if (CzyMoznaNavigowac(nowyTydzien))
            {
                aktualnyTydzien = nowyTydzien;
                OdswiezWidok();
            }
        }

        private void BtnBiezacyTydzien_Click(object sender, RoutedEventArgs e)
        {
            aktualnyTydzien = DateTime.Today;
            OdswiezWidok();
        }

        private void BtnNastepnyTydzien_Click(object sender, RoutedEventArgs e)
        {
            DateTime nowyTydzien = aktualnyTydzien.AddDays(7);
            if (CzyMoznaNavigowac(nowyTydzien))
            {
                aktualnyTydzien = nowyTydzien;
                OdswiezWidok();
            }
        }

        private void BtnCzteryTygodnieWstecz_Click(object sender, RoutedEventArgs e)
        {
            DateTime nowyTydzien = aktualnyTydzien.AddDays(-28);
            if (CzyMoznaNavigowac(nowyTydzien))
            {
                aktualnyTydzien = nowyTydzien;
                OdswiezWidok();
            }
        }

        private void BtnCzteryTygodnieNaprzod_Click(object sender, RoutedEventArgs e)
        {
            DateTime nowyTydzien = aktualnyTydzien.AddDays(28);
            if (CzyMoznaNavigowac(nowyTydzien))
            {
                aktualnyTydzien = nowyTydzien;
                OdswiezWidok();
            }
        }

        private void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            WczytajKonfiguracjeProduktow();
            OdswiezWidok();
        }

        private void BtnKonfiguracjaWydajnosci_Click(object sender, RoutedEventArgs e)
        {
            var okno = new KonfiguracjaWydajnosci(connectionString);
            if (okno.ShowDialog() == true)
            {
                OdswiezWidok();
            }
        }

        private void BtnKonfiguracjaProduktow_Click(object sender, RoutedEventArgs e)
        {
            var okno = new KonfiguracjaProduktow(connectionString, connectionStringHandel, konfiguracjaProduktow);
            if (okno.ShowDialog() == true)
            {
                WczytajKonfiguracjeProduktow();
                OdswiezWidok();
            }
        }

        private void BtnKopiuj_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var suma = aktualneDane.FirstOrDefault(x => x.JestSuma);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("═══════════════════════════════════════════════════════════");
                sb.AppendLine("           TYGODNIOWY PLAN PRODUKCJI");
                sb.AppendLine("═══════════════════════════════════════════════════════════");
                sb.AppendLine($"Okres: {txtDataZakres.Text} {txtNumerTygodnia.Text}");
                sb.AppendLine($"Wygenerowano: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Wydajność: Tuszka {aktualnaWydajnosc.WspolczynnikTuszki}%, A/B {aktualnaWydajnosc.ProcentTuszkaA}/{aktualnaWydajnosc.ProcentTuszkaB}");
                sb.AppendLine();
                sb.AppendLine($"Status potwierdzeń: {txtStatPotwierdzenie.Text}");
                sb.AppendLine("═══════════════════════════════════════════════════════════");
                sb.AppendLine();

                // STATYSTYKI POJEMNIKÓW
                if (suma != null)
                {
                    sb.AppendLine("📦 STATYSTYKI POJEMNIKÓW (15kg/poj | 36 poj/paleta)");
                    sb.AppendLine("───────────────────────────────────────────────────────────");
                    sb.AppendLine("🔵 MAŁY KURCZAK (POJ 12-9):");
                    sb.AppendLine($"   POJ 12: {suma.Pojemniki12,4} poj ({txtPoj12Procent.Text,6}) - {txtPoj12Palety.Text}");
                    sb.AppendLine($"   POJ 11: {suma.Pojemniki11,4} poj ({txtPoj11Procent.Text,6}) - {txtPoj11Palety.Text}");
                    sb.AppendLine($"   POJ 10: {suma.Pojemniki10,4} poj ({txtPoj10Procent.Text,6}) - {txtPoj10Palety.Text}");
                    sb.AppendLine($"   POJ 9:  {suma.Pojemniki9,4} poj ({txtPoj9Procent.Text,6}) - {txtPoj9Palety.Text}");
                    sb.AppendLine($"   SUMA MAŁY: {txtMalyKurczakSuma.Text}");
                    sb.AppendLine();
                    sb.AppendLine("🟠 DUŻY KURCZAK (POJ 8-5):");
                    sb.AppendLine($"   POJ 8:  {suma.Pojemniki8,4} poj ({txtPoj8Procent.Text,6}) - {txtPoj8Palety.Text}");
                    sb.AppendLine($"   POJ 7:  {suma.Pojemniki7,4} poj ({txtPoj7Procent.Text,6}) - {txtPoj7Palety.Text}");
                    sb.AppendLine($"   POJ 6:  {suma.Pojemniki6,4} poj ({txtPoj6Procent.Text,6}) - {txtPoj6Palety.Text}");
                    sb.AppendLine($"   POJ 5:  {suma.Pojemniki5,4} poj ({txtPoj5Procent.Text,6}) - {txtPoj5Palety.Text}");
                    sb.AppendLine($"   SUMA DUŻY: {txtDuzyKurczakSuma.Text}");
                    sb.AppendLine("═══════════════════════════════════════════════════════════");
                    sb.AppendLine();
                }

                foreach (var dzien in aktualneDane.Where(x => !x.JestSuma))
                {
                    if (dzien.ZywiecKg > 0)
                    {
                        sb.AppendLine($"{dzien.Data} ({dzien.DzienTygodnia})");
                        sb.AppendLine($"   Auta: {dzien.LiczbaAut} | Żywiec: {dzien.ZywiecKg:N0} kg | Sztuki: {dzien.Sztuki:N0}");
                        sb.AppendLine($"   Tuszka A: {dzien.TuszkaA:N0} kg | Filet: {dzien.Filet:N0} kg");
                        sb.AppendLine($"   Status: {dzien.StatusTekst}");
                        sb.AppendLine();
                    }
                }

                sb.AppendLine("═══════════════════════════════════════════════════════════");
                sb.AppendLine("                    PODSUMOWANIE");
                sb.AppendLine("═══════════════════════════════════════════════════════════");
                sb.AppendLine($"Surowiec żywiec:  {txtSumaSurowiec.Text}");
                sb.AppendLine($"Tuszka A:         {txtSumaTuszkaA.Text}");
                sb.AppendLine($"Filet:            {txtSumaFilet.Text}");
                sb.AppendLine($"Ćwiartka:         {txtSumaCwiartka.Text}");
                sb.AppendLine("═══════════════════════════════════════════════════════════");

                Clipboard.SetText(sb.ToString());
                MessageBox.Show("Plan został skopiowany do schowka!",
                              "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd kopiowania: {ex.Message}",
                              "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnEksportExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StringBuilder csv = new StringBuilder();

                csv.AppendLine($"Tygodniowy Plan Produkcji;{txtDataZakres.Text}");
                csv.AppendLine($"Wygenerowano;{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                csv.AppendLine($"Wydajnosc;Tuszka {aktualnaWydajnosc.WspolczynnikTuszki}% A/B {aktualnaWydajnosc.ProcentTuszkaA}/{aktualnaWydajnosc.ProcentTuszkaB}");
                csv.AppendLine();

                csv.AppendLine("Data;Dzien;Auta;Zywiec [kg];Sztuki;Waga sr.;Tuszka [kg];A/B;Tuszka A [kg];Cwiartka [kg];Skrzydlo [kg];Filet [kg];Korpus [kg];POJ 12;POJ 11;POJ 10;POJ 9;POJ 8;POJ 7;POJ 6;POJ 5;Status");

                foreach (var dzien in aktualneDane)
                {
                    csv.AppendLine($"{dzien.Data};{dzien.DzienTygodnia};{dzien.LiczbaAut};{dzien.ZywiecKg:F2};{dzien.Sztuki};{dzien.WagaSrednia:F2};{dzien.TuszkaCalkowita:F2};{dzien.TuszkaAB};{dzien.TuszkaA:F2};{dzien.Cwiartka:F2};{dzien.Skrzydlo:F2};{dzien.Filet:F2};{dzien.Korpus:F2};{dzien.Pojemniki12};{dzien.Pojemniki11};{dzien.Pojemniki10};{dzien.Pojemniki9};{dzien.Pojemniki8};{dzien.Pojemniki7};{dzien.Pojemniki6};{dzien.Pojemniki5};{dzien.StatusTekst}");
                }

                string filename = $"Plan_Produkcji_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string filepath = System.IO.Path.Combine(desktop, filename);

                System.IO.File.WriteAllText(filepath, csv.ToString(), Encoding.UTF8);

                var result = MessageBox.Show($"Plik został zapisany na pulpicie:\n{filename}\n\nCzy chcesz go otworzyć?",
                                           "Eksport zakończony", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filepath) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd eksportu: {ex.Message}",
                              "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class PlanDziennyModel
    {
        public string Data { get; set; }
        public string DzienTygodnia { get; set; }
        public int LiczbaAut { get; set; }
        public int LiczbaUbiorek { get; set; }  // NOWE
        public decimal ZywiecKg { get; set; }
        public int Sztuki { get; set; }
        public decimal WagaSrednia { get; set; }
        public decimal TuszkaCalkowita { get; set; }
        public string TuszkaAB { get; set; }
        public decimal TuszkaA { get; set; }
        public decimal TuszkaB { get; set; }
        public decimal Cwiartka { get; set; }
        public decimal Skrzydlo { get; set; }
        public decimal Filet { get; set; }
        public decimal Korpus { get; set; }

        // Pojemniki według wielkości
        public int Pojemniki12 { get; set; }
        public int Pojemniki11 { get; set; }
        public int Pojemniki10 { get; set; }
        public int Pojemniki9 { get; set; }
        public int Pojemniki8 { get; set; }
        public int Pojemniki7 { get; set; }
        public int Pojemniki6 { get; set; }
        public int Pojemniki5 { get; set; }

        public bool CzyPotwierdzone { get; set; }
        public decimal ProcentPotwierdzenia { get; set; }
        public string StatusTekst { get; set; }
        public bool JestSuma { get; set; }
    }

    public class WydajnoscModel
    {
        public decimal WspolczynnikTuszki { get; set; }
        public decimal ProcentTuszkaA { get; set; }
        public decimal ProcentTuszkaB { get; set; }
    }
}