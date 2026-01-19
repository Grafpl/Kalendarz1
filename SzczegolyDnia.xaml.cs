using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    public partial class SzczegolyDnia : Window
    {
        private string connectionString;
        private DateTime dataWybrana;
        private WydajnoscModel wydajnosc;
        private Dictionary<string, decimal> konfiguracjaProduktow;
        private List<DostawaModel> dostawy;

        public SzczegolyDnia(string connString, DateTime data, WydajnoscModel wyd, Dictionary<string, decimal> konfig)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            connectionString = connString;
            dataWybrana = data;
            wydajnosc = wyd;
            konfiguracjaProduktow = konfig;
            dostawy = new List<DostawaModel>();

            UstawNaglowek();
            WczytajDostawy();
        }

        private void UstawNaglowek()
        {
            CultureInfo polskaCultura = new CultureInfo("pl-PL");
            string dzien = polskaCultura.DateTimeFormat.GetDayName(dataWybrana.DayOfWeek);
            dzien = char.ToUpper(dzien[0]) + dzien.Substring(1);

            txtData.Text = dataWybrana.ToString("yyyy-MM-dd");
            txtDzienTygodnia.Text = $"({dzien})";
        }

        private void WczytajDostawy()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT 
                            Lp,
                            Dostawca,
                            Auta,
                            WagaDek,
                            SztukiDek,
                            PotwWaga,
                            PotwSztuki,
                            Bufor
                        FROM HarmonogramDostaw
                        WHERE DataOdbioru = @Data
                        AND Bufor IN ('B.Wolny', 'B.Kontr.', 'Potwierdzony')
                        ORDER BY Lp";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Data", dataWybrana);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                // Bezpieczne odczytywanie wartości z obsługą NULL
                                int auta = 0;
                                if (!reader.IsDBNull(reader.GetOrdinal("Auta")))
                                {
                                    try { auta = Convert.ToInt32(reader["Auta"]); }
                                    catch { auta = 0; }
                                }

                                decimal wagaDek = 0;
                                if (!reader.IsDBNull(reader.GetOrdinal("WagaDek")))
                                {
                                    try { wagaDek = Convert.ToDecimal(reader["WagaDek"]); }
                                    catch { wagaDek = 0; }
                                }

                                int sztuki = 0;
                                if (!reader.IsDBNull(reader.GetOrdinal("SztukiDek")))
                                {
                                    try { sztuki = Convert.ToInt32(reader["SztukiDek"]); }
                                    catch { sztuki = 0; }
                                }

                                decimal zywiecKg = sztuki * wagaDek;

                                // Obliczenia produkcji
                                decimal tuszkaKg = zywiecKg * (wydajnosc.WspolczynnikTuszki / 100m);
                                decimal tuszkaA = tuszkaKg * (wydajnosc.ProcentTuszkaA / 100m);
                                decimal tuszkaB = tuszkaKg * (wydajnosc.ProcentTuszkaB / 100m);

                                decimal filet = tuszkaB * (konfiguracjaProduktow.GetValueOrDefault("Filet", 28m) / 100m);
                                decimal cwiartka = tuszkaB * (konfiguracjaProduktow.GetValueOrDefault("Cwiartka", 38m) / 100m);
                                decimal skrzydlo = tuszkaB * (konfiguracjaProduktow.GetValueOrDefault("Skrzydlo", 9m) / 100m);

                                // Oblicz pojemniki
                                var pojemniki = ObliczPojemniki(wagaDek, tuszkaA);

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

                                string bufor = reader.IsDBNull(reader.GetOrdinal("Bufor")) ? "" : reader["Bufor"].ToString();

                                string status;
                                if (potwWaga && potwSztuki)
                                    status = "✓ Potwierdzone";
                                else if (potwWaga || potwSztuki)
                                    status = "⚠ Częściowo potw.";
                                else if (!string.IsNullOrEmpty(bufor))
                                    status = $"○ {bufor}";
                                else
                                    status = "○ Zaplanowane";

                                int lp = 0;
                                if (!reader.IsDBNull(reader.GetOrdinal("Lp")))
                                {
                                    try { lp = Convert.ToInt32(reader["Lp"]); }
                                    catch { lp = 0; }
                                }

                                string dostawca = reader.IsDBNull(reader.GetOrdinal("Dostawca")) ? "Nieznany" : reader["Dostawca"].ToString();

                                dostawy.Add(new DostawaModel
                                {
                                    Lp = lp,
                                    Dostawca = dostawca,
                                    Auta = auta,
                                    WagaDek = wagaDek,
                                    SztukiDek = sztuki,
                                    ZywiecKg = zywiecKg,
                                    TuszkaKg = tuszkaKg,
                                    TuszkaA = tuszkaA,
                                    Filet = filet,
                                    Cwiartka = cwiartka,
                                    Skrzydlo = skrzydlo,
                                    Status = status,
                                    Pojemniki12 = pojemniki.Item1,
                                    Pojemniki11 = pojemniki.Item2,
                                    Pojemniki10 = pojemniki.Item3,
                                    Pojemniki9 = pojemniki.Item4,
                                    Pojemniki8 = pojemniki.Item5,
                                    Pojemniki7 = pojemniki.Item6,
                                    Pojemniki6 = pojemniki.Item7,
                                    Pojemniki5 = pojemniki.Item8
                                });
                            }
                        }
                    }
                }

                // SORTOWANIE po średniej wadze od największej do najmniejszej
                dostawy = dostawy.OrderByDescending(d => d.WagaDek).ToList();

                // Dodaj wiersz SUMA
                var suma = new DostawaModel
                {
                    Lp = 0,
                    Dostawca = "SUMA",
                    Auta = dostawy.Sum(d => d.Auta),
                    WagaDek = dostawy.Count > 0 ? dostawy.Sum(d => d.ZywiecKg) / dostawy.Sum(d => d.SztukiDek) : 0,
                    SztukiDek = dostawy.Sum(d => d.SztukiDek),
                    ZywiecKg = dostawy.Sum(d => d.ZywiecKg),
                    TuszkaKg = dostawy.Sum(d => d.TuszkaKg),
                    TuszkaA = dostawy.Sum(d => d.TuszkaA),
                    Filet = dostawy.Sum(d => d.Filet),
                    Cwiartka = dostawy.Sum(d => d.Cwiartka),
                    Skrzydlo = dostawy.Sum(d => d.Skrzydlo),
                    Pojemniki12 = dostawy.Sum(d => d.Pojemniki12),
                    Pojemniki11 = dostawy.Sum(d => d.Pojemniki11),
                    Pojemniki10 = dostawy.Sum(d => d.Pojemniki10),
                    Pojemniki9 = dostawy.Sum(d => d.Pojemniki9),
                    Pojemniki8 = dostawy.Sum(d => d.Pojemniki8),
                    Pojemniki7 = dostawy.Sum(d => d.Pojemniki7),
                    Pojemniki6 = dostawy.Sum(d => d.Pojemniki6),
                    Pojemniki5 = dostawy.Sum(d => d.Pojemniki5),
                    Status = "PODSUMOWANIE",
                    JestSuma = true
                };

                dostawy.Add(suma);

                dgDostawy.ItemsSource = dostawy;
                dgDostawy.LoadingRow += DgDostawy_LoadingRow;

                ObliczPodsumowanie();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania dostaw:\n\n{ex.Message}\n\nSzczegóły:\n{ex.StackTrace}",
                              "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private (int, int, int, int, int, int, int, int) ObliczPojemniki(decimal wagaDek, decimal tuszkaA)
        {
            if (wagaDek == 0 || tuszkaA == 0)
                return (0, 0, 0, 0, 0, 0, 0, 0);

            // Oblicz wagę tuszki dla pojedynczego kurczaka
            decimal tuszkaASztuka = wagaDek * (wydajnosc.WspolczynnikTuszki / 100m);

            // Ile sztuk mieści się w pojemniku 15kg
            decimal sztukWPojemniku = 15m / tuszkaASztuka;

            // Ile pojemników potrzeba dla całej tuszki A
            decimal liczbaPojemnikow = tuszkaA / 15m;

            // Rozmiar dolny i górny
            int rozmiarDolny = (int)Math.Floor(sztukWPojemniku);
            int rozmiarGorny = (int)Math.Ceiling(sztukWPojemniku);

            // Część dziesiętna - determinuje proporcję
            decimal fractional = sztukWPojemniku - rozmiarDolny;

            // Jeśli fractional = 0, to wszystkie pojemniki są tego samego rozmiaru
            int pojemnikiGorne = 0;
            int pojemnikiDolne = 0;

            if (rozmiarDolny == rozmiarGorny)
            {
                // Dokładna liczba - wszystkie pojemniki tego samego rozmiaru
                pojemnikiDolne = (int)Math.Ceiling(liczbaPojemnikow);
            }
            else
            {
                // Rozkład proporcjonalny
                // Im większa część dziesiętna, tym więcej pojemników z większą liczbą sztuk
                pojemnikiGorne = (int)Math.Round(liczbaPojemnikow * fractional);
                pojemnikiDolne = (int)Math.Ceiling(liczbaPojemnikow) - pojemnikiGorne;
            }

            // Inicjalizuj wynik
            int poj12 = 0, poj11 = 0, poj10 = 0, poj9 = 0;
            int poj8 = 0, poj7 = 0, poj6 = 0, poj5 = 0;

            // Przypisz do odpowiednich kategorii
            // Rozmiar dolny
            if (rozmiarDolny >= 12) poj12 += pojemnikiDolne;
            else if (rozmiarDolny == 11) poj11 += pojemnikiDolne;
            else if (rozmiarDolny == 10) poj10 += pojemnikiDolne;
            else if (rozmiarDolny == 9) poj9 += pojemnikiDolne;
            else if (rozmiarDolny == 8) poj8 += pojemnikiDolne;
            else if (rozmiarDolny == 7) poj7 += pojemnikiDolne;
            else if (rozmiarDolny == 6) poj6 += pojemnikiDolne;
            else poj5 += pojemnikiDolne;

            // Rozmiar górny
            if (pojemnikiGorne > 0)
            {
                if (rozmiarGorny >= 12) poj12 += pojemnikiGorne;
                else if (rozmiarGorny == 11) poj11 += pojemnikiGorne;
                else if (rozmiarGorny == 10) poj10 += pojemnikiGorne;
                else if (rozmiarGorny == 9) poj9 += pojemnikiGorne;
                else if (rozmiarGorny == 8) poj8 += pojemnikiGorne;
                else if (rozmiarGorny == 7) poj7 += pojemnikiGorne;
                else if (rozmiarGorny == 6) poj6 += pojemnikiGorne;
                else poj5 += pojemnikiGorne;
            }

            return (poj12, poj11, poj10, poj9, poj8, poj7, poj6, poj5);
        }

        private void DgDostawy_LoadingRow(object sender, System.Windows.Controls.DataGridRowEventArgs e)
        {
            var dostawa = e.Row.Item as DostawaModel;
            if (dostawa != null && dostawa.JestSuma)
            {
                e.Row.Background = new SolidColorBrush(Color.FromRgb(52, 73, 94));
                e.Row.Foreground = Brushes.White;
                e.Row.FontWeight = FontWeights.Bold;
            }
        }

        private void ObliczPodsumowanie()
        {
            var rzeczywiste = dostawy.Where(d => !d.JestSuma).ToList();
            var suma = dostawy.FirstOrDefault(d => d.JestSuma);

            int liczbaAut = rzeczywiste.Sum(d => d.Auta);
            decimal zywiec = rzeczywiste.Sum(d => d.ZywiecKg);
            int sztuki = rzeczywiste.Sum(d => d.SztukiDek);
            decimal tuszkaA = rzeczywiste.Sum(d => d.TuszkaA);
            decimal filet = rzeczywiste.Sum(d => d.Filet);

            int potwierdzonych = rzeczywiste.Count(d => d.Status.StartsWith("✓"));
            int wszystkich = rzeczywiste.Count;
            decimal procentPotwierdzenia = wszystkich > 0 ? (decimal)potwierdzonych / wszystkich * 100 : 0;

            txtLiczbaAut.Text = liczbaAut.ToString();
            txtZywiec.Text = $"{zywiec:N0} kg";
            txtSztuki.Text = $"{sztuki:N0} szt";
            txtTuszkaA.Text = $"{tuszkaA:N0} kg";
            txtFilet.Text = $"{filet:N0} kg";
            txtStatus.Text = $"{procentPotwierdzenia:F0}% potw. ({potwierdzonych}/{wszystkich})";

            // Oblicz statystyki pojemników
            ObliczStatystykiPojemnikowKompakt(suma);
        }

        private void ObliczStatystykiPojemnikowKompakt(DostawaModel suma)
        {
            if (suma == null)
            {
                WyczyscStatystykiPojemnikow();
                return;
            }

            int sumaCala = suma.Pojemniki12 + suma.Pojemniki11 + suma.Pojemniki10 + suma.Pojemniki9 +
                           suma.Pojemniki8 + suma.Pojemniki7 + suma.Pojemniki6 + suma.Pojemniki5;

            if (sumaCala == 0)
            {
                WyczyscStatystykiPojemnikow();
                return;
            }

            // Oblicz sumy dla małego i dużego
            int sumaMaly = suma.Pojemniki12 + suma.Pojemniki11 + suma.Pojemniki10 + suma.Pojemniki9;
            int sumaDuzy = suma.Pojemniki8 + suma.Pojemniki7 + suma.Pojemniki6 + suma.Pojemniki5;

            decimal procentMaly = (decimal)sumaMaly / sumaCala * 100;
            decimal procentDuzy = (decimal)sumaDuzy / sumaCala * 100;

            // Ustaw wartości dla małego kurczaka z procentami
            txtPoj12Kompakt.Text = suma.Pojemniki12.ToString();
            txtPoj12ProcentKompakt.Text = sumaCala > 0 ? $"{((decimal)suma.Pojemniki12 / sumaCala * 100):F1}%" : "0%";
            txtPoj12Pal.Text = FormatujPaletyKrotko(suma.Pojemniki12);

            txtPoj11Kompakt.Text = suma.Pojemniki11.ToString();
            txtPoj11ProcentKompakt.Text = sumaCala > 0 ? $"{((decimal)suma.Pojemniki11 / sumaCala * 100):F1}%" : "0%";
            txtPoj11Pal.Text = FormatujPaletyKrotko(suma.Pojemniki11);

            txtPoj10Kompakt.Text = suma.Pojemniki10.ToString();
            txtPoj10ProcentKompakt.Text = sumaCala > 0 ? $"{((decimal)suma.Pojemniki10 / sumaCala * 100):F1}%" : "0%";
            txtPoj10Pal.Text = FormatujPaletyKrotko(suma.Pojemniki10);

            txtPoj9Kompakt.Text = suma.Pojemniki9.ToString();
            txtPoj9ProcentKompakt.Text = sumaCala > 0 ? $"{((decimal)suma.Pojemniki9 / sumaCala * 100):F1}%" : "0%";
            txtPoj9Pal.Text = FormatujPaletyKrotko(suma.Pojemniki9);

            // Ustaw wartości dla dużego kurczaka z procentami
            txtPoj8Kompakt.Text = suma.Pojemniki8.ToString();
            txtPoj8ProcentKompakt.Text = sumaCala > 0 ? $"{((decimal)suma.Pojemniki8 / sumaCala * 100):F1}%" : "0%";
            txtPoj8Pal.Text = FormatujPaletyKrotko(suma.Pojemniki8);

            txtPoj7Kompakt.Text = suma.Pojemniki7.ToString();
            txtPoj7ProcentKompakt.Text = sumaCala > 0 ? $"{((decimal)suma.Pojemniki7 / sumaCala * 100):F1}%" : "0%";
            txtPoj7Pal.Text = FormatujPaletyKrotko(suma.Pojemniki7);

            txtPoj6Kompakt.Text = suma.Pojemniki6.ToString();
            txtPoj6ProcentKompakt.Text = sumaCala > 0 ? $"{((decimal)suma.Pojemniki6 / sumaCala * 100):F1}%" : "0%";
            txtPoj6Pal.Text = FormatujPaletyKrotko(suma.Pojemniki6);

            txtPoj5Kompakt.Text = suma.Pojemniki5.ToString();
            txtPoj5ProcentKompakt.Text = sumaCala > 0 ? $"{((decimal)suma.Pojemniki5 / sumaCala * 100):F1}%" : "0%";
            txtPoj5Pal.Text = FormatujPaletyKrotko(suma.Pojemniki5);

            // Sumy z procentami
            txtSumaMalyKompakt.Text = $"{sumaMaly:N0} poj ({procentMaly:F1}%)";
            txtSumaDuzyKompakt.Text = $"{sumaDuzy:N0} poj ({procentDuzy:F1}%)";
        }
        private void WyczyscStatystykiPojemnikow()
        {
            // Mały kurczak
            txtPoj12Kompakt.Text = "0";
            txtPoj12ProcentKompakt.Text = "0%";
            txtPoj12Pal.Text = "";

            txtPoj11Kompakt.Text = "0";
            txtPoj11ProcentKompakt.Text = "0%";
            txtPoj11Pal.Text = "";

            txtPoj10Kompakt.Text = "0";
            txtPoj10ProcentKompakt.Text = "0%";
            txtPoj10Pal.Text = "";

            txtPoj9Kompakt.Text = "0";
            txtPoj9ProcentKompakt.Text = "0%";
            txtPoj9Pal.Text = "";

            // Duży kurczak
            txtPoj8Kompakt.Text = "0";
            txtPoj8ProcentKompakt.Text = "0%";
            txtPoj8Pal.Text = "";

            txtPoj7Kompakt.Text = "0";
            txtPoj7ProcentKompakt.Text = "0%";
            txtPoj7Pal.Text = "";

            txtPoj6Kompakt.Text = "0";
            txtPoj6ProcentKompakt.Text = "0%";
            txtPoj6Pal.Text = "";

            txtPoj5Kompakt.Text = "0";
            txtPoj5ProcentKompakt.Text = "0%";
            txtPoj5Pal.Text = "";

            // Sumy
            txtSumaMalyKompakt.Text = "0 poj (0%)";
            txtSumaDuzyKompakt.Text = "0 poj (0%)";
        }
        private string FormatujPalety(int liczba)
        {
            if (liczba == 0) return "";

            int pelne = liczba / 36;
            int reszta = liczba % 36;

            if (pelne > 0 && reszta > 0)
                return $"{pelne} pal + {reszta} poj";
            else if (pelne > 0)
                return $"{pelne} palet";
            else
                return $"{reszta} poj";
        }

        private string FormatujPaletyKrotko(int liczba)
        {
            if (liczba == 0) return "";

            int pelne = liczba / 36;
            int reszta = liczba % 36;

            if (pelne > 0 && reszta > 0)
                return $"{pelne}p+{reszta}";
            else if (pelne > 0)
                return $"{pelne}pal";
            else
                return $"{reszta}poj";
        }

        private void BtnKopiuj_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var suma = dostawy.FirstOrDefault(d => d.JestSuma);
                int sumaCala = 0;
                if (suma != null)
                {
                    sumaCala = suma.Pojemniki12 + suma.Pojemniki11 + suma.Pojemniki10 + suma.Pojemniki9 +
                               suma.Pojemniki8 + suma.Pojemniki7 + suma.Pojemniki6 + suma.Pojemniki5;
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("═══════════════════════════════════════════════════════════");
                sb.AppendLine($"           SZCZEGÓŁY DNIA: {txtData.Text} {txtDzienTygodnia.Text}");
                sb.AppendLine("═══════════════════════════════════════════════════════════");
                sb.AppendLine();
                sb.AppendLine("PODSUMOWANIE:");
                sb.AppendLine($"Liczba aut:    {txtLiczbaAut.Text}");
                sb.AppendLine($"Żywiec:        {txtZywiec.Text}");
                sb.AppendLine($"Sztuki:        {txtSztuki.Text}");
                sb.AppendLine($"Tuszka A:      {txtTuszkaA.Text}");
                sb.AppendLine($"Filet:         {txtFilet.Text}");
                sb.AppendLine($"Status:        {txtStatus.Text}");
                sb.AppendLine();

                // STATYSTYKI POJEMNIKÓW
                if (suma != null && sumaCala > 0)
                {
                    sb.AppendLine("───────────────────────────────────────────────────────────");
                    sb.AppendLine("📦 STATYSTYKI POJEMNIKÓW (15kg/poj | 36 poj/paleta)");
                    sb.AppendLine("───────────────────────────────────────────────────────────");

                    int sumaMaly = suma.Pojemniki12 + suma.Pojemniki11 + suma.Pojemniki10 + suma.Pojemniki9;
                    int sumaDuzy = suma.Pojemniki8 + suma.Pojemniki7 + suma.Pojemniki6 + suma.Pojemniki5;

                    decimal procentMaly = (decimal)sumaMaly / sumaCala * 100;
                    decimal procentDuzy = (decimal)sumaDuzy / sumaCala * 100;

                    sb.AppendLine("🔵 MAŁY KURCZAK (POJ 12-9):");
                    if (suma.Pojemniki12 > 0)
                    {
                        decimal proc = (decimal)suma.Pojemniki12 / sumaCala * 100;
                        sb.AppendLine($"   POJ 12: {suma.Pojemniki12,4} poj ({proc,5:F1}%) - {FormatujPalety(suma.Pojemniki12)}");
                    }
                    if (suma.Pojemniki11 > 0)
                    {
                        decimal proc = (decimal)suma.Pojemniki11 / sumaCala * 100;
                        sb.AppendLine($"   POJ 11: {suma.Pojemniki11,4} poj ({proc,5:F1}%) - {FormatujPalety(suma.Pojemniki11)}");
                    }
                    if (suma.Pojemniki10 > 0)
                    {
                        decimal proc = (decimal)suma.Pojemniki10 / sumaCala * 100;
                        sb.AppendLine($"   POJ 10: {suma.Pojemniki10,4} poj ({proc,5:F1}%) - {FormatujPalety(suma.Pojemniki10)}");
                    }
                    if (suma.Pojemniki9 > 0)
                    {
                        decimal proc = (decimal)suma.Pojemniki9 / sumaCala * 100;
                        sb.AppendLine($"   POJ 9:  {suma.Pojemniki9,4} poj ({proc,5:F1}%) - {FormatujPalety(suma.Pojemniki9)}");
                    }
                    sb.AppendLine($"   SUMA MAŁY: {sumaMaly:N0} poj ({procentMaly:F1}%)");
                    sb.AppendLine();

                    sb.AppendLine("🟠 DUŻY KURCZAK (POJ 8-5):");
                    if (suma.Pojemniki8 > 0)
                    {
                        decimal proc = (decimal)suma.Pojemniki8 / sumaCala * 100;
                        sb.AppendLine($"   POJ 8:  {suma.Pojemniki8,4} poj ({proc,5:F1}%) - {FormatujPalety(suma.Pojemniki8)}");
                    }
                    if (suma.Pojemniki7 > 0)
                    {
                        decimal proc = (decimal)suma.Pojemniki7 / sumaCala * 100;
                        sb.AppendLine($"   POJ 7:  {suma.Pojemniki7,4} poj ({proc,5:F1}%) - {FormatujPalety(suma.Pojemniki7)}");
                    }
                    if (suma.Pojemniki6 > 0)
                    {
                        decimal proc = (decimal)suma.Pojemniki6 / sumaCala * 100;
                        sb.AppendLine($"   POJ 6:  {suma.Pojemniki6,4} poj ({proc,5:F1}%) - {FormatujPalety(suma.Pojemniki6)}");
                    }
                    if (suma.Pojemniki5 > 0)
                    {
                        decimal proc = (decimal)suma.Pojemniki5 / sumaCala * 100;
                        sb.AppendLine($"   POJ 5:  {suma.Pojemniki5,4} poj ({proc,5:F1}%) - {FormatujPalety(suma.Pojemniki5)}");
                    }
                    sb.AppendLine($"   SUMA DUŻY: {sumaDuzy:N0} poj ({procentDuzy:F1}%)");
                    sb.AppendLine();
                }

                sb.AppendLine("═══════════════════════════════════════════════════════════");
                sb.AppendLine("                    LISTA DOSTAW");
                sb.AppendLine("═══════════════════════════════════════════════════════════");
                sb.AppendLine();

                // Lista dostaw (posortowane po wadze)
                foreach (var dostawa in dostawy.Where(d => !d.JestSuma))
                {
                    sb.AppendLine($"{dostawa.Lp}. {dostawa.Dostawca}");
                    sb.AppendLine($"   Auta: {dostawa.Auta} | Waga śr.: {dostawa.WagaDek:F2} kg | Sztuki: {dostawa.SztukiDek:N0}");
                    sb.AppendLine($"   Żywiec: {dostawa.ZywiecKg:N0} kg | Tuszka A: {dostawa.TuszkaA:N0} kg");
                    sb.AppendLine($"   Filet: {dostawa.Filet:N0} kg | Ćwiartka: {dostawa.Cwiartka:N0} kg");

                    if (dostawa.Pojemniki12 > 0 || dostawa.Pojemniki11 > 0 || dostawa.Pojemniki10 > 0 || dostawa.Pojemniki9 > 0)
                    {
                        sb.Append($"   🔵 MAŁY:");
                        if (dostawa.Pojemniki12 > 0) sb.Append($" 12:{dostawa.Pojemniki12}");
                        if (dostawa.Pojemniki11 > 0) sb.Append($" 11:{dostawa.Pojemniki11}");
                        if (dostawa.Pojemniki10 > 0) sb.Append($" 10:{dostawa.Pojemniki10}");
                        if (dostawa.Pojemniki9 > 0) sb.Append($" 9:{dostawa.Pojemniki9}");
                        sb.AppendLine();
                    }

                    if (dostawa.Pojemniki8 > 0 || dostawa.Pojemniki7 > 0 || dostawa.Pojemniki6 > 0 || dostawa.Pojemniki5 > 0)
                    {
                        sb.Append($"   🟠 DUŻY:");
                        if (dostawa.Pojemniki8 > 0) sb.Append($" 8:{dostawa.Pojemniki8}");
                        if (dostawa.Pojemniki7 > 0) sb.Append($" 7:{dostawa.Pojemniki7}");
                        if (dostawa.Pojemniki6 > 0) sb.Append($" 6:{dostawa.Pojemniki6}");
                        if (dostawa.Pojemniki5 > 0) sb.Append($" 5:{dostawa.Pojemniki5}");
                        sb.AppendLine();
                    }

                    sb.AppendLine($"   Status: {dostawa.Status}");
                    sb.AppendLine();
                }

                sb.AppendLine("═══════════════════════════════════════════════════════════");

                Clipboard.SetText(sb.ToString());
                MessageBox.Show("Szczegóły dnia zostały skopiowane do schowka!",
                              "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd kopiowania: {ex.Message}",
                              "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void dgDostawy_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }

    public class DostawaModel
    {
        public int Lp { get; set; }
        public string Dostawca { get; set; }
        public int Auta { get; set; }
        public decimal WagaDek { get; set; }
        public int SztukiDek { get; set; }
        public decimal ZywiecKg { get; set; }
        public decimal TuszkaKg { get; set; }
        public decimal TuszkaA { get; set; }
        public decimal Filet { get; set; }
        public decimal Cwiartka { get; set; }
        public decimal Skrzydlo { get; set; }
        public string Status { get; set; }

        // Pojemniki według wielkości
        public int Pojemniki12 { get; set; }
        public int Pojemniki11 { get; set; }
        public int Pojemniki10 { get; set; }
        public int Pojemniki9 { get; set; }
        public int Pojemniki8 { get; set; }
        public int Pojemniki7 { get; set; }
        public int Pojemniki6 { get; set; }
        public int Pojemniki5 { get; set; }

        public bool JestSuma { get; set; }
    }
}