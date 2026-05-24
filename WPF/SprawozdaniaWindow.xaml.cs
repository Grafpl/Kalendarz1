using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.WPF
{
    public partial class SprawozdaniaWindow : Window
    {
        // ============ STATIC COMMANDS (KeyBindings w XAML) ============
        public static readonly RoutedCommand CloseCmd = new RoutedCommand();
        public static readonly RoutedCommand RefreshCmd = new RoutedCommand();
        public static readonly RoutedCommand ExportCsvCmd = new RoutedCommand();
        public static readonly RoutedCommand PrevWeekCmd = new RoutedCommand();
        public static readonly RoutedCommand NextWeekCmd = new RoutedCommand();

        // Connection strings — HANDEL (.112) dla faktur, LibraNet (.109) dla harmonogramu dostaw.
        // Hardcoded zgodnie z konwencją repo (CLAUDE.md): connection strings w klasach okien.
        private const string ConnHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private static readonly CultureInfo Pl = new CultureInfo("pl-PL");

        // Wyniki ostatniego pobrania — używane do generowania tekstu maila
        private decimal _suma7;
        private decimal _suma8;
        private decimal _kg7;
        private decimal _kg8;
        private int _faktur7;
        private int _faktur8;
        private int _pozycji7;
        private int _pozycji8;
        // LibraNet (Harmonogram dostaw — plan)
        private decimal _libraKg;
        private int _libraDostaw;
        private int _libraDostawcow;
        // Rozbicie per dzień (HANDEL + LibraNet plan + Specyfikacja PDF)
        private List<DzienKgWart> _perDzienHandel = new();
        private List<DzienKg> _perDzienLibra = new();
        private List<DzienKgWart> _perDzienSpec = new();
        private decimal _specKgRazem;
        private decimal _specWartoscRazem;
        private DateTime _ostatniOd;
        private DateTime _ostatniDo;
        private bool _hasData;

        // DataGrid binding source
        public ObservableCollection<DzienWiersz> DniRows { get; } = new();

        private record DzienKg(DateTime Data, decimal Kg);
        private record DzienKgWart(DateTime Data, decimal Kg, decimal Wartosc);

        public SprawozdaniaWindow()
        {
            InitializeComponent();
            dgPerDzien.ItemsSource = DniRows;

            // Komendy keyboardowe
            CommandBindings.Add(new CommandBinding(CloseCmd, (s, e) => Close()));
            CommandBindings.Add(new CommandBinding(RefreshCmd, async (s, e) => await PobierzAsync()));
            CommandBindings.Add(new CommandBinding(ExportCsvCmd, (s, e) => ExportCsv()));
            CommandBindings.Add(new CommandBinding(PrevWeekCmd, (s, e) => PrzesunZakres(-7)));
            CommandBindings.Add(new CommandBinding(NextWeekCmd, (s, e) => PrzesunZakres(+7)));

            UstawPoprzedniTydzien();
            chipPoprzTydzien.IsChecked = true;

            // Rozmiar = WorkArea (cały ekran MINUS taskbar Windowsa) — taskbar zostaje widoczny
            SourceInitialized += (s, e) =>
            {
                var wa = SystemParameters.WorkArea;
                Left = wa.Left; Top = wa.Top;
                Width = wa.Width; Height = wa.Height;
            };

            Loaded += async (s, e) => await PobierzAsync();
        }

        // ============ TITLE BAR (custom chrome) ============
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // ============ DOMYŚLNY ZAKRES: PON-NIEDZ poprzedniego tygodnia ============
        private void UstawPoprzedniTydzien()
        {
            var today = DateTime.Today;
            int daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
            var thisMonday = today.AddDays(-daysFromMonday);
            var lastMonday = thisMonday.AddDays(-7);
            var lastSunday = lastMonday.AddDays(6);
            dpFrom.SelectedDate = lastMonday;
            dpTo.SelectedDate = lastSunday;
        }

        // ============ PRESET BUTTONS ============
        private void Preset_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as ToggleButton;
            if (btn == null) return;

            // Mutual-exclusive: odznacz inne
            foreach (var c in new[] { chipPoprzTydzien, chipTenTydzien, chipPoprzMiesiac, chipTenMiesiac, chip30Dni })
                if (c != btn) c.IsChecked = false;
            btn.IsChecked = true;

            var today = DateTime.Today;
            DateTime od, doD;
            switch (btn.Tag as string)
            {
                case "PrevWeek":
                    int dfm1 = ((int)today.DayOfWeek + 6) % 7;
                    var thisMon = today.AddDays(-dfm1);
                    od = thisMon.AddDays(-7); doD = od.AddDays(6); break;
                case "ThisWeek":
                    int dfm2 = ((int)today.DayOfWeek + 6) % 7;
                    od = today.AddDays(-dfm2); doD = od.AddDays(6); break;
                case "PrevMonth":
                    var firstOfMonth = new DateTime(today.Year, today.Month, 1);
                    doD = firstOfMonth.AddDays(-1);
                    od = new DateTime(doD.Year, doD.Month, 1); break;
                case "ThisMonth":
                    od = new DateTime(today.Year, today.Month, 1);
                    doD = od.AddMonths(1).AddDays(-1); break;
                case "Last30":
                    od = today.AddDays(-29); doD = today; break;
                default:
                    return;
            }
            dpFrom.SelectedDate = od;
            dpTo.SelectedDate = doD;
            _ = PobierzAsync();
        }

        private async void BtnLoad_Click(object sender, RoutedEventArgs e) => await PobierzAsync();
        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await PobierzAsync();
        private void BtnExport_Click(object sender, RoutedEventArgs e) => ExportCsv();

        // ============ NAVIGATION ±1 TYDZIEŃ ============
        // Przesuwa OBA pickery o N dni zachowując długość zakresu.
        // Działa też dla niestandardowych zakresów (np. 10 dni) — shift = 7 dni dla obu krańców.
        private void BtnShiftWeek_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            int delta = (btn?.Tag as string) == "Prev" ? -7 : +7;
            PrzesunZakres(delta);
        }

        private void PrzesunZakres(int dni)
        {
            if (!dpFrom.SelectedDate.HasValue || !dpTo.SelectedDate.HasValue) return;
            dpFrom.SelectedDate = dpFrom.SelectedDate.Value.AddDays(dni);
            dpTo.SelectedDate = dpTo.SelectedDate.Value.AddDays(dni);

            // Po przesunięciu — to już nie jest żaden preset, odznacz wszystkie chipy
            foreach (var c in new[] { chipPoprzTydzien, chipTenTydzien, chipPoprzMiesiac, chipTenMiesiac, chip30Dni })
                c.IsChecked = false;

            _ = PobierzAsync();
        }

        // ============ TOGGLE TEXT BOXES ============
        private void BtnToggleText_Click(object sender, RoutedEventArgs e)
        {
            bool visible = textBoxesPanel.Visibility == Visibility.Visible;
            textBoxesPanel.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
            lblToggleText.Text = visible ? "📝  Pokaż teksty" : "📝  Ukryj teksty";
        }

        // ============ DETAILS DIALOG ============
        private void DgPerDzien_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Ignoruj dwuklik na nagłówku kolumny
            if (e.OriginalSource is System.Windows.DependencyObject dep)
            {
                var src = dep;
                while (src != null && src is not DataGridRow && src is not DataGridColumnHeader)
                    src = VisualTreeHelper.GetParent(src);
                if (src is DataGridColumnHeader) return;
            }
            PokazSzczegoly();
        }

        private void MenuShowDetails_Click(object sender, RoutedEventArgs e) => PokazSzczegoly();

        private void MenuCopyRow_Click(object sender, RoutedEventArgs e)
        {
            if (dgPerDzien.SelectedItem is not DzienWiersz w) return;
            try
            {
                Clipboard.SetText(
                    $"{w.DataText} {w.Dow}\t{w.HKg:N0} kg\t{w.HW:N2} zł\t{w.HC:N2} zł/kg\t" +
                    $"{w.LKg:N0} kg\t{w.SKg:N0} kg\t{w.SW:N2} zł\t{w.SC:N2} zł/kg\t" +
                    $"{w.DKgText}\t{w.DProcText}");
            }
            catch { }
        }

        private void PokazSzczegoly()
        {
            if (dgPerDzien.SelectedItem is not DzienWiersz w) return;
            if (w.IsTotal) return; // wiersz SUMA — bez sensu pokazywać "szczegóły"
            if (w.Data == default) return;

            try
            {
                var dlg = new SzczegolyDniaWindow(w.Data) { Owner = this };
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie można otworzyć okna szczegółów: " + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============ POBIERANIE DANYCH ============
        private async Task PobierzAsync()
        {
            if (!dpFrom.SelectedDate.HasValue || !dpTo.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz zakres dat.", "Brak danych", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DateTime dataOd = dpFrom.SelectedDate.Value.Date;
            DateTime dataDo = dpTo.SelectedDate.Value.Date;
            if (dataDo < dataOd)
            {
                MessageBox.Show("Data 'do' nie może być wcześniejsza niż 'od'.", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            loadingOverlay.Visibility = Visibility.Visible;
            try
            {
                // Równolegle: HANDEL (faktury) + LibraNet (harmonogram) + Specyfikacje (FarmerCalc)
                var taskHandel = Task.Run(() => PobierzFaktury(dataOd, dataDo));
                var taskHandelDni = Task.Run(() => PobierzFakturyPerDzien(dataOd, dataDo));
                var taskLibra = Task.Run(() => PobierzHarmonogram(dataOd, dataDo));
                var taskLibraDni = Task.Run(() => PobierzHarmonogramPerDzien(dataOd, dataDo));
                var taskSpec = Task.Run(() => PobierzSpecyfikacjePerDzien(dataOd, dataDo));
                await Task.WhenAll(taskHandel, taskHandelDni, taskLibra, taskLibraDni, taskSpec);

                var wynik = await taskHandel;
                _suma7 = wynik.Suma7; _suma8 = wynik.Suma8;
                _kg7 = wynik.Kg7; _kg8 = wynik.Kg8;
                _faktur7 = wynik.Faktur7; _faktur8 = wynik.Faktur8;
                _pozycji7 = wynik.Pozycji7; _pozycji8 = wynik.Pozycji8;

                var libra = await taskLibra;
                _libraKg = libra.SumaKg;
                _libraDostaw = libra.LiczbaDostaw;
                _libraDostawcow = libra.LiczbaDostawcow;

                _perDzienHandel = await taskHandelDni;
                _perDzienLibra = await taskLibraDni;
                _perDzienSpec = await taskSpec;
                _specKgRazem = 0; _specWartoscRazem = 0;
                foreach (var d in _perDzienSpec) { _specKgRazem += d.Kg; _specWartoscRazem += d.Wartosc; }

                _ostatniOd = dataOd;
                _ostatniDo = dataDo;
                _hasData = true;

                AktualizujUI();
                // Auto-wypełnij oba textboxy gotowymi tekstami.
                txtWewnetrzny.Text = ZbudujTekstWewnetrzny();
                txtOficjalny.Text = ZbudujTekstOficjalny();

                // Odśwież badge ZSRIR (czy okres był już wysłany)
                _ = AktualizujZsrirStatusAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd pobierania danych:\n{ex.Message}",
                    "Błąd SQL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private record WynikSprawozdania(
            decimal Suma7, decimal Suma8,
            decimal Kg7, decimal Kg8,
            int Faktur7, int Faktur8,
            int Pozycji7, int Pozycji8);

        private record WynikHarmonogram(decimal SumaKg, int LiczbaDostaw, int LiczbaDostawcow);

        // ============ SQL HANDEL — faktury zakupu ============
        // typ_dk: FVZ (vatowiec) + FVR (rolnik VAT RR) + FKZ (korekta zakupu).
        // Korekty sumują się algebraicznie z fakturami (mogą zwiększać lub zmniejszać sumę).
        // Pominięte: fzk, fkk, RKZ, RUZ — inne kategorie.
        // Towary po TW.id:
        //   - 67653 = Kurczak żywy -7 (rolnik / FVR)
        //   - 67654 = Kurczak żywy - 8 (vatowiec / FVZ)
        // wartNetto/ilosc w fakturach zakupu są UJEMNE — sumujemy zachowując znak,
        // potem ABS na końcu. Korekty na minus/plus naturalnie modyfikują saldo.
        private const int TwId7 = 67653;
        private const int TwId8 = 67654;

        private WynikSprawozdania PobierzFaktury(DateTime od, DateTime doData)
        {
            const string sql = @"
SELECT
    CASE DP.idtw WHEN @TwId7 THEN 7 WHEN @TwId8 THEN 8 END AS Klasa,
    COUNT(DISTINCT DK.id)  AS LiczbaFaktur,
    COUNT(*)               AS LiczbaPozycji,
    SUM(DP.wartNetto)      AS SumaNetto,   -- zachowuje znak (faktura - / korekta +/-)
    SUM(DP.ilosc)          AS SumaIlosc    -- zachowuje znak
FROM [Handel].[HM].[DK] DK
INNER JOIN [Handel].[HM].[DP] DP ON DP.super = DK.id
WHERE DK.data >= @DataOd
  AND DK.data <  DATEADD(DAY, 1, @DataDo)
  AND ISNULL(DK.anulowany, 0) = 0
  AND DK.aktywny = 1
  AND DK.typ_dk IN (N'FVZ', N'FVR', N'FKZ')
  AND DP.idtw IN (@TwId7, @TwId8)
GROUP BY CASE DP.idtw WHEN @TwId7 THEN 7 WHEN @TwId8 THEN 8 END;
";

            decimal suma7 = 0, suma8 = 0, kg7 = 0, kg8 = 0;
            int fakt7 = 0, fakt8 = 0, poz7 = 0, poz8 = 0;

            using var conn = new SqlConnection(ConnHandel);
            conn.Open();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            cmd.Parameters.Add("@DataOd", System.Data.SqlDbType.Date).Value = od;
            cmd.Parameters.Add("@DataDo", System.Data.SqlDbType.Date).Value = doData;
            cmd.Parameters.Add("@TwId7", System.Data.SqlDbType.Int).Value = TwId7;
            cmd.Parameters.Add("@TwId8", System.Data.SqlDbType.Int).Value = TwId8;

            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                int klasa = rdr.GetInt32(0);
                int fakt = rdr.GetInt32(1);
                int poz = rdr.GetInt32(2);
                decimal suma = rdr.IsDBNull(3) ? 0m : Convert.ToDecimal(rdr.GetValue(3));
                decimal kg = rdr.IsDBNull(4) ? 0m : Convert.ToDecimal(rdr.GetValue(4));

                // ABS na końcu — sumy są ujemne (faktura zakupu), korekty już uwzględnione algebraicznie
                if (klasa == 7) { suma7 = Math.Abs(suma); kg7 = Math.Abs(kg); fakt7 = fakt; poz7 = poz; }
                else if (klasa == 8) { suma8 = Math.Abs(suma); kg8 = Math.Abs(kg); fakt8 = fakt; poz8 = poz; }
            }

            return new WynikSprawozdania(suma7, suma8, kg7, kg8, fakt7, fakt8, poz7, poz8);
        }

        // ============ SQL LibraNet — Harmonogram dostaw (plan) ============
        // Źródło: ten sam co Menu > Specyfikacja Surowca. SztukiDek × WagaDek per dzień.
        // Pokazuje co BYŁO ZAPLANOWANE — niezależny pomiar do porównania z fakturami HANDEL.
        private WynikHarmonogram PobierzHarmonogram(DateTime od, DateTime doData)
        {
            const string sql = @"
SELECT
    SUM(CAST(SztukiDek AS DECIMAL(18,2)) * CAST(WagaDek AS DECIMAL(18,2))) AS SumaKg,
    COUNT(*) AS LiczbaDostaw,
    COUNT(DISTINCT Dostawca) AS LiczbaDostawcow
FROM [LibraNet].[dbo].[HarmonogramDostaw]
WHERE DataOdbioru >= @DataOd
  AND DataOdbioru <= @DataDo
  AND Bufor IN ('Potwierdzony', 'Potwierdzone');
";
            using var conn = new SqlConnection(ConnLibra);
            conn.Open();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            // LibraNet to SQL 2008 R2 — daty jako string (per CLAUDE.md sec.4)
            cmd.Parameters.AddWithValue("@DataOd", od.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@DataDo", doData.ToString("yyyy-MM-dd"));

            using var rdr = cmd.ExecuteReader();
            if (rdr.Read())
            {
                decimal kg = rdr.IsDBNull(0) ? 0m : Convert.ToDecimal(rdr.GetValue(0));
                int dostaw = rdr.IsDBNull(1) ? 0 : rdr.GetInt32(1);
                int dostawcow = rdr.IsDBNull(2) ? 0 : rdr.GetInt32(2);
                return new WynikHarmonogram(kg, dostaw, dostawcow);
            }
            return new WynikHarmonogram(0, 0, 0);
        }

        // ============ SQL HANDEL — per dzień (do textboxa wewnętrznego) ============
        private List<DzienKgWart> PobierzFakturyPerDzien(DateTime od, DateTime doData)
        {
            const string sql = @"
SELECT CAST(DK.data AS DATE) AS Dzien,
       SUM(DP.ilosc)     AS Ilosc,
       SUM(DP.wartNetto) AS Wartosc
FROM [Handel].[HM].[DK] DK
INNER JOIN [Handel].[HM].[DP] DP ON DP.super = DK.id
WHERE DK.data >= @DataOd
  AND DK.data <  DATEADD(DAY, 1, @DataDo)
  AND ISNULL(DK.anulowany, 0) = 0
  AND DK.aktywny = 1
  AND DK.typ_dk IN (N'FVZ', N'FVR', N'FKZ')
  AND DP.idtw IN (@TwId7, @TwId8)
GROUP BY CAST(DK.data AS DATE)
ORDER BY Dzien;
";
            var lista = new List<DzienKgWart>();
            using var conn = new SqlConnection(ConnHandel);
            conn.Open();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            cmd.Parameters.Add("@DataOd", System.Data.SqlDbType.Date).Value = od;
            cmd.Parameters.Add("@DataDo", System.Data.SqlDbType.Date).Value = doData;
            cmd.Parameters.Add("@TwId7", System.Data.SqlDbType.Int).Value = TwId7;
            cmd.Parameters.Add("@TwId8", System.Data.SqlDbType.Int).Value = TwId8;
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                DateTime data = rdr.GetDateTime(0);
                decimal kg = rdr.IsDBNull(1) ? 0m : Math.Abs(Convert.ToDecimal(rdr.GetValue(1)));
                decimal w = rdr.IsDBNull(2) ? 0m : Math.Abs(Convert.ToDecimal(rdr.GetValue(2)));
                lista.Add(new DzienKgWart(data, kg, w));
            }
            return lista;
        }

        // ============ SQL Specyfikacje (FarmerCalc) per dzień ============
        // Replikuje logikę PDF (GeneratePDFReport): DoZaplaty = Netto - PadleKG - KonfKG - UbytekKG - Opasienie - KlasaB.
        // Identyczne formuły jak Zywiec/WidokSpecyfikacji/WidokSpecyfikacje.xaml.cs:6910-6974.
        //   - waga: NettoFarmWeight (hodowca) jeśli >0, inaczej NettoWeight (ubojnia)
        //   - sredniaWaga = Netto / (LumQnt + DeclI2)
        //   - padleKG/konfKG = 0 jeśli IncDeadConf=1 (czyPiK), inaczej szt × sredniaWaga
        //   - ubytekKG = Netto × Loss
        //   - cena = Price + Addition
        //   - wartosc = cena × doZaplaty
        // Grupowanie po CalcDate.
        private List<DzienKgWart> PobierzSpecyfikacjePerDzien(DateTime od, DateTime doData)
        {
            const string sql = @"
WITH Surowe AS (
    SELECT
        CAST(CalcDate AS DATE) AS Dzien,
        CASE WHEN ISNULL(NettoFarmWeight,0) > 0 THEN NettoFarmWeight ELSE NettoWeight END AS Netto,
        ISNULL(IncDeadConf, 0) AS CzyPiK,
        ISNULL(Loss, 0) AS LossProc,
        ISNULL(LumQnt, 0) AS Lumel,
        ISNULL(DeclI2, 0) AS Padle,
        ISNULL(DeclI3, 0) + ISNULL(DeclI4, 0) + ISNULL(DeclI5, 0) AS Konf,
        ISNULL(Opasienie, 0) AS Opasienie,
        ISNULL(KlasaB, 0) AS KlasaB,
        ISNULL(Price, 0) + ISNULL(Addition, 0) AS Cena
    FROM [LibraNet].[dbo].[FarmerCalc]
    WHERE CalcDate >= @DataOd AND CalcDate <= @DataDo
),
PerSpec AS (
    SELECT
        Dzien, Netto, Cena, CzyPiK,
        CASE WHEN (Lumel + Padle) > 0 THEN Netto / (Lumel + Padle) ELSE 0 END AS SredniaWaga,
        Padle, Konf, Opasienie, KlasaB, LossProc
    FROM Surowe
),
KGRowy AS (
    SELECT
        Dzien, Netto, Cena, Opasienie, KlasaB,
        ROUND(Netto * LossProc, 0) AS UbytekKG,
        CASE WHEN CzyPiK = 1 THEN 0 ELSE ROUND(Padle * SredniaWaga, 0) END AS PadleKG,
        CASE WHEN CzyPiK = 1 THEN 0 ELSE ROUND(Konf  * SredniaWaga, 0) END AS KonfKG
    FROM PerSpec
),
DoZap AS (
    SELECT
        Dzien, Cena,
        (Netto - PadleKG - KonfKG - UbytekKG - Opasienie - KlasaB) AS DoZaplaty
    FROM KGRowy
)
SELECT Dzien, SUM(DoZaplaty) AS Kg, SUM(DoZaplaty * Cena) AS Wartosc
FROM DoZap
GROUP BY Dzien
ORDER BY Dzien;
";
            var lista = new List<DzienKgWart>();
            using var conn = new SqlConnection(ConnLibra);
            conn.Open();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@DataOd", od.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@DataDo", doData.ToString("yyyy-MM-dd"));
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                DateTime data = rdr.GetDateTime(0);
                decimal kg = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                decimal w = rdr.IsDBNull(2) ? 0m : Convert.ToDecimal(rdr.GetValue(2));
                lista.Add(new DzienKgWart(data, kg, w));
            }
            return lista;
        }

        // ============ SQL LibraNet — per dzień ============
        private List<DzienKg> PobierzHarmonogramPerDzien(DateTime od, DateTime doData)
        {
            const string sql = @"
SELECT DataOdbioru,
       SUM(CAST(SztukiDek AS DECIMAL(18,2)) * CAST(WagaDek AS DECIMAL(18,2))) AS Kg
FROM [LibraNet].[dbo].[HarmonogramDostaw]
WHERE DataOdbioru >= @DataOd
  AND DataOdbioru <= @DataDo
  AND Bufor IN ('Potwierdzony', 'Potwierdzone')
GROUP BY DataOdbioru
ORDER BY DataOdbioru;
";
            var lista = new List<DzienKg>();
            using var conn = new SqlConnection(ConnLibra);
            conn.Open();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@DataOd", od.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@DataDo", doData.ToString("yyyy-MM-dd"));
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                DateTime data = rdr.GetDateTime(0);
                decimal kg = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                lista.Add(new DzienKg(data, kg));
            }
            return lista;
        }

        // ============ UI — fillin karty + DataGrid ============
        private void AktualizujUI()
        {
            decimal hKg = _kg7 + _kg8;
            decimal hW = _suma7 + _suma8;
            decimal hC = hKg > 0 ? hW / hKg : 0;
            int hFakt = _faktur7 + _faktur8;
            int hPoz = _pozycji7 + _pozycji8;

            // ===== Pill HANDEL =====
            cardHandelKg.Text = hKg.ToString("N0", Pl) + " kg";
            cardHandelW.Text = hW.ToString("N0", Pl) + " zł";
            pillHandel.ToolTip = $"HANDEL .112 — Faktury zakupu (FVZ + FVR + FKZ)\n" +
                                 $"Ilość:        {hKg:N0} kg  ({hKg / 1000m:N3} t)\n" +
                                 $"Wartość:      {hW:N2} zł netto\n" +
                                 $"Cena średnia: {hC:N2} zł/kg\n" +
                                 $"Liczba:       {hFakt} faktur · {hPoz} pozycji";

            // ===== Pill LIBRANET =====
            cardLibraKg.Text = _libraKg.ToString("N0", Pl) + " kg";
            cardLibraInfo.Text = _libraDostaw == 0 ? "brak danych" : $"{_libraDostaw} dost.";
            pillLibra.ToolTip = $"LIBRANET .109 — Harmonogram dostaw (plan)\n" +
                                $"Ilość:     {_libraKg:N0} kg  ({_libraKg / 1000m:N3} t)\n" +
                                $"Dostawy:   {_libraDostaw}\n" +
                                $"Dostawcy:  {_libraDostawcow}";

            // ===== Pill SPECYFIKACJA =====
            decimal sC = _specKgRazem > 0 ? _specWartoscRazem / _specKgRazem : 0;
            cardSpecKg.Text = _specKgRazem.ToString("N0", Pl) + " kg";
            cardSpecC.Text = sC.ToString("N2", Pl) + " zł/kg";
            pillSpec.ToolTip = $"SPECYFIKACJA PDF (FarmerCalc) — rozliczenie po uboju\n" +
                               $"Ilość:        {_specKgRazem:N0} kg  ({_specKgRazem / 1000m:N3} t)\n" +
                               $"Wartość:      {_specWartoscRazem:N2} zł netto\n" +
                               $"Cena średnia: {sC:N2} zł/kg\n" +
                               $"(Netto - padłe - konfisk. - ubytek - opasienie - kl. B)";

            // ===== Pill DELTA HANDEL vs SPEC =====
            decimal delta = hKg - _specKgRazem;
            decimal proc = _specKgRazem > 0 ? (delta / _specKgRazem) * 100m : 0;
            decimal deltaLibra = hKg - _libraKg;
            decimal procLibra = _libraKg > 0 ? (deltaLibra / _libraKg) * 100m : 0;

            cardDeltaKg.Text = (delta >= 0 ? "+" : "") + delta.ToString("N0", Pl) + " kg";
            cardDeltaProc.Text = _specKgRazem > 0 ? (proc >= 0 ? "+" : "") + proc.ToString("N2", Pl) + "%" : "—";

            double absProc = (double)Math.Abs(proc);
            string statusKey = absProc <= 0.5 ? "Ok" : absProc <= 2.0 ? "Warn" : "Bad";
            ApplyDeltaCardColor(statusKey);

            string hint = statusKey switch
            {
                "Ok" => "✓ Dane się zgadzają w granicach normy",
                "Warn" => "⚠ Niewielkie rozbieżności — sprawdź korekty",
                "Bad" => "❌ Duża różnica — wymaga weryfikacji",
                _ => ""
            };
            cardDeltaBorder.ToolTip =
                $"Δ HANDEL − SPECYFIKACJA  ({hint})\n" +
                $"Δ vs SPEC:   {(delta >= 0 ? "+" : "")}{delta:N0} kg  ({(proc >= 0 ? "+" : "")}{proc:N2}%)\n" +
                $"Δ vs LIBRA:  {(deltaLibra >= 0 ? "+" : "")}{deltaLibra:N0} kg  ({(procLibra >= 0 ? "+" : "")}{procLibra:N2}%)";

            // ===== DataGrid per dzień =====
            BudujWierszePerDzien();

            // ===== Period label (title bar) + Footer status =====
            lblPeriodTop.Text = $"{_ostatniOd:dd.MM} – {_ostatniDo:dd.MM.yyyy}";
            lblFooterTime.Text = $"Ostatnia aktualizacja: {DateTime.Now:HH:mm:ss}";
            int dniCount = DniRows.Count - (DniRows.Count > 0 ? 1 : 0); // bez SUMA row
            lblFooterCount.Text = $"{dniCount} dni  ·  {hFakt} faktur  ·  {_libraDostaw} dostaw  ·  {_perDzienSpec.Count} specyfikacji";
            lblFooterStatus.Text = _hasData ? "Dane załadowane" : "Brak danych";
        }

        private void ApplyDeltaCardColor(string statusKey)
        {
            (string bg, string border) = statusKey switch
            {
                "Ok" => ("DeltaOkBg", "DeltaOk"),
                "Warn" => ("DeltaWarnBg", "DeltaWarn"),
                "Bad" => ("DeltaBadBg", "DeltaBad"),
                _ => ("DeltaOkBg", "DeltaOk")
            };
            if (TryFindResource(bg) is Brush bgBrush) cardDeltaBorder.Background = bgBrush;
            if (TryFindResource(border) is Brush bdBrush)
            {
                cardDeltaBorder.BorderBrush = bdBrush;
                cardDeltaKg.Foreground = bdBrush;
                cardDeltaProc.Foreground = bdBrush;
            }
        }

        private Brush GetDeltaBrush(decimal absProc)
        {
            string key = absProc <= 0.5m ? "DeltaOk" : absProc <= 2.0m ? "DeltaWarn" : "DeltaBad";
            return (TryFindResource(key) as Brush) ?? Brushes.Black;
        }

        // Per-day rows for DataGrid — łączy 3 źródła po dacie
        private void BudujWierszePerDzien()
        {
            DniRows.Clear();
            var idxH = new Dictionary<DateTime, DzienKgWart>();
            foreach (var d in _perDzienHandel) idxH[d.Data] = d;
            var idxL = new Dictionary<DateTime, decimal>();
            foreach (var d in _perDzienLibra) idxL[d.Data] = d.Kg;
            var idxS = new Dictionary<DateTime, DzienKgWart>();
            foreach (var d in _perDzienSpec) idxS[d.Data] = d;

            var daty = new SortedSet<DateTime>();
            foreach (var d in _perDzienHandel) daty.Add(d.Data);
            foreach (var d in _perDzienLibra) daty.Add(d.Data);
            foreach (var d in _perDzienSpec) daty.Add(d.Data);

            foreach (var data in daty)
            {
                decimal hKg = idxH.TryGetValue(data, out var h) ? h.Kg : 0;
                decimal hW = idxH.TryGetValue(data, out var h2) ? h2.Wartosc : 0;
                decimal hC = hKg > 0 ? hW / hKg : 0;
                decimal lKg = idxL.TryGetValue(data, out var l) ? l : 0;
                decimal sKg = idxS.TryGetValue(data, out var s) ? s.Kg : 0;
                decimal sW = idxS.TryGetValue(data, out var s2) ? s2.Wartosc : 0;
                decimal sC = sKg > 0 ? sW / sKg : 0;
                decimal dKg = hKg - sKg;
                decimal dProc = sKg > 0 ? (dKg / sKg) * 100m : 0;

                double abs = (double)Math.Abs(dProc);
                string status = sKg == 0 && hKg == 0 ? "Pending"
                              : abs <= 0.5 ? "Ok"
                              : abs <= 2.0 ? "Warn"
                              : "Bad";

                string tooltip = $"{data:dd MMMM yyyy} ({data.ToString("dddd", Pl)})\n" +
                                 $"\n" +
                                 $"HANDEL .112:    {hKg:N0} kg / {hW:N2} zł / {hC:N2} zł/kg\n" +
                                 $"LIBRANET .109:  {lKg:N0} kg (plan)\n" +
                                 $"SPECYFIKACJA:   {sKg:N0} kg / {sW:N2} zł / {sC:N2} zł/kg\n" +
                                 $"\n" +
                                 $"Δ HANDEL-SPEC:  {(dKg >= 0 ? "+" : "")}{dKg:N0} kg ({(dProc >= 0 ? "+" : "")}{dProc:N2}%)";

                DniRows.Add(new DzienWiersz
                {
                    Data = data,
                    DataText = data.ToString("dd.MM", Pl),
                    Dow = data.ToString("ddd", Pl).ToUpper().Replace(".", ""),
                    DowVis = Visibility.Visible,
                    HKg = hKg, HW = hW, HC = hC,
                    LKg = lKg,
                    SKg = sKg, SW = sW, SC = sC,
                    DKgText = (dKg >= 0 ? "+" : "") + dKg.ToString("N0", Pl) + " kg",
                    DProcText = sKg > 0 ? ((dProc >= 0 ? "+" : "") + dProc.ToString("N2", Pl) + "%") : "—",
                    StatusKey = status,
                    IsTotal = false,
                    TooltipText = tooltip
                });
            }

            // SUMA row na końcu — sumuje wszystko
            if (DniRows.Count > 0)
            {
                decimal sumHKg = 0, sumHW = 0, sumLKg = 0, sumSKg = 0, sumSW = 0;
                foreach (var d in _perDzienHandel) { sumHKg += d.Kg; sumHW += d.Wartosc; }
                foreach (var d in _perDzienLibra) { sumLKg += d.Kg; }
                foreach (var d in _perDzienSpec) { sumSKg += d.Kg; sumSW += d.Wartosc; }
                decimal sumHC = sumHKg > 0 ? sumHW / sumHKg : 0;
                decimal sumSC = sumSKg > 0 ? sumSW / sumSKg : 0;
                decimal sumDKg = sumHKg - sumSKg;
                decimal sumDProc = sumSKg > 0 ? (sumDKg / sumSKg) * 100m : 0;

                double abs = (double)Math.Abs(sumDProc);
                string status = abs <= 0.5 ? "Ok" : abs <= 2.0 ? "Warn" : "Bad";

                DniRows.Add(new DzienWiersz
                {
                    DataText = "RAZEM",
                    Dow = "",
                    DowVis = Visibility.Collapsed,
                    HKg = sumHKg, HW = sumHW, HC = sumHC,
                    LKg = sumLKg,
                    SKg = sumSKg, SW = sumSW, SC = sumSC,
                    DKgText = (sumDKg >= 0 ? "+" : "") + sumDKg.ToString("N0", Pl) + " kg",
                    DProcText = sumSKg > 0 ? ((sumDProc >= 0 ? "+" : "") + sumDProc.ToString("N2", Pl) + "%") : "—",
                    StatusKey = status,
                    IsTotal = true,
                    TooltipText = $"Podsumowanie okresu {_ostatniOd:dd.MM} – {_ostatniDo:dd.MM.yyyy}"
                });
            }

            // Empty state visibility
            if (emptyState != null)
                emptyState.Visibility = DniRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ============ CSV EXPORT ============
        private void ExportCsv()
        {
            if (!_hasData) { MessageBox.Show("Najpierw pobierz dane.", "Brak danych", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"Sprawozdanie_{_ostatniOd:yyyyMMdd}_{_ostatniDo:yyyyMMdd}.csv"
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Dzień;DOW;HANDEL_kg;HANDEL_zł;HANDEL_zł/kg;LIBRA_kg;SPEC_kg;SPEC_zł;SPEC_zł/kg;Δkg;Δ%;Status");
                foreach (var r in DniRows)
                {
                    sb.Append(r.Data.ToString("yyyy-MM-dd")).Append(';')
                      .Append(r.Dow).Append(';')
                      .Append(r.HKg.ToString("F2", CultureInfo.InvariantCulture)).Append(';')
                      .Append(r.HW.ToString("F2", CultureInfo.InvariantCulture)).Append(';')
                      .Append(r.HC.ToString("F4", CultureInfo.InvariantCulture)).Append(';')
                      .Append(r.LKg.ToString("F2", CultureInfo.InvariantCulture)).Append(';')
                      .Append(r.SKg.ToString("F2", CultureInfo.InvariantCulture)).Append(';')
                      .Append(r.SW.ToString("F2", CultureInfo.InvariantCulture)).Append(';')
                      .Append(r.SC.ToString("F4", CultureInfo.InvariantCulture)).Append(';')
                      .Append(r.DKgText).Append(';')
                      .Append(r.DProcText).Append(';')
                      .Append(r.StatusKey)
                      .AppendLine();
                }
                System.IO.File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                MessageBox.Show($"Wyeksportowano {DniRows.Count} wierszy.", "Eksport CSV",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show("Błąd eksportu: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        // ============ GENEROWANIE TEKSTU MAILA ============
        // ============ TEKST 1: WEWNĘTRZNY — kontrola dzienna 3 źródła ============
        // Porównuje 3 niezależne źródła prawdy:
        //   HANDEL .112 (faktury FVZ+FVR+FKZ)
        //   LibraNet .109 (Harmonogram dostaw — plan)
        //   Specyfikacja PDF .109 (FarmerCalc — finalne rozliczenie z hodowcą)
        private string ZbudujTekstWewnetrzny()
        {
            if (!_hasData) return "";

            // Indeksy do szybkiego lookupu per data
            var idxHandel = new Dictionary<DateTime, DzienKgWart>();
            foreach (var d in _perDzienHandel) idxHandel[d.Data] = d;
            var idxLibra = new Dictionary<DateTime, decimal>();
            foreach (var d in _perDzienLibra) idxLibra[d.Data] = d.Kg;
            var idxSpec = new Dictionary<DateTime, DzienKgWart>();
            foreach (var d in _perDzienSpec) idxSpec[d.Data] = d;

            // Union dat ze wszystkich źródeł
            var daty = new SortedSet<DateTime>();
            foreach (var d in _perDzienHandel) daty.Add(d.Data);
            foreach (var d in _perDzienLibra) daty.Add(d.Data);
            foreach (var d in _perDzienSpec) daty.Add(d.Data);

            decimal kgRazem = _kg7 + _kg8;
            decimal sumaRazem = _suma7 + _suma8;
            decimal sredniaCenaH = kgRazem > 0 ? sumaRazem / kgRazem : 0;
            decimal sredniaCenaS = _specKgRazem > 0 ? _specWartoscRazem / _specKgRazem : 0;

            var sb = new StringBuilder();
            sb.AppendLine("SKUP KURCZAKA — kontrola wewnętrzna (3 źródła)");
            sb.AppendLine($"Okres: {_ostatniOd:dd.MM.yyyy} – {_ostatniDo:dd.MM.yyyy}");
            sb.AppendLine();
            sb.AppendLine("HANDEL = faktury .112 (FVZ + FVR + korekty)");
            sb.AppendLine("LIBRA  = harmonogram dostaw .109 (plan)");
            sb.AppendLine("SPEC   = specyfikacje PDF (FarmerCalc — po uboju, po odliczeniach)");
            sb.AppendLine();
            sb.AppendLine("                 ┌─────── HANDEL .112 ────────┐  ┌── LIBRA ──┐  ┌────── SPECYFIKACJA PDF ─────┐  ┌── Δ HANDEL-SPEC ──┐");
            sb.AppendLine("  Dzień             kg        wartość     zł/kg       kg            kg        wartość     zł/kg      kg          %");
            sb.AppendLine("  ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────");

            foreach (var data in daty)
            {
                string dzien = data.ToString("dd.MM", Pl);
                string dow = data.ToString("ddd", Pl).ToUpper().Replace(".", "");
                decimal hKg = idxHandel.TryGetValue(data, out var h) ? h.Kg : 0;
                decimal hW = idxHandel.TryGetValue(data, out var h2) ? h2.Wartosc : 0;
                decimal hC = hKg > 0 ? hW / hKg : 0;
                decimal lKg = idxLibra.TryGetValue(data, out var l) ? l : 0;
                decimal sKg = idxSpec.TryGetValue(data, out var s) ? s.Kg : 0;
                decimal sW = idxSpec.TryGetValue(data, out var s2) ? s2.Wartosc : 0;
                decimal sC = sKg > 0 ? sW / sKg : 0;
                decimal dKg = hKg - sKg;
                decimal dProc = sKg > 0 ? (dKg / sKg) * 100m : 0;

                sb.AppendLine($"  {dzien} {dow,-4}  {hKg.ToString("N0", Pl),9}  {hW.ToString("N2", Pl),12}  {hC.ToString("N2", Pl),6}   " +
                             $"{lKg.ToString("N0", Pl),9}     {sKg.ToString("N0", Pl),9}  {sW.ToString("N2", Pl),12}  {sC.ToString("N2", Pl),6}    " +
                             $"{(dKg >= 0 ? "+" : "")}{dKg.ToString("N0", Pl),6}  {(dProc >= 0 ? "+" : "")}{dProc.ToString("N2", Pl),5}%");
            }

            sb.AppendLine("  ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────");
            decimal sumHKg = 0, sumHW = 0, sumLKg = 0, sumSKg = 0, sumSW = 0;
            foreach (var v in _perDzienHandel) { sumHKg += v.Kg; sumHW += v.Wartosc; }
            foreach (var v in _perDzienLibra) { sumLKg += v.Kg; }
            foreach (var v in _perDzienSpec) { sumSKg += v.Kg; sumSW += v.Wartosc; }
            decimal sumHC = sumHKg > 0 ? sumHW / sumHKg : 0;
            decimal sumSC = sumSKg > 0 ? sumSW / sumSKg : 0;
            decimal sumDKg = sumHKg - sumSKg;
            decimal sumDProc = sumSKg > 0 ? (sumDKg / sumSKg) * 100m : 0;

            sb.AppendLine($"  RAZEM         {sumHKg.ToString("N0", Pl),9}  {sumHW.ToString("N2", Pl),12}  {sumHC.ToString("N2", Pl),6}   " +
                         $"{sumLKg.ToString("N0", Pl),9}     {sumSKg.ToString("N0", Pl),9}  {sumSW.ToString("N2", Pl),12}  {sumSC.ToString("N2", Pl),6}    " +
                         $"{(sumDKg >= 0 ? "+" : "")}{sumDKg.ToString("N0", Pl),6}  {(sumDProc >= 0 ? "+" : "")}{sumDProc.ToString("N2", Pl),5}%");

            sb.AppendLine();
            sb.AppendLine("PODSUMOWANIE:");
            sb.AppendLine($"  HANDEL:        {sumHKg.ToString("N0", Pl)} kg / {sumHW.ToString("N2", Pl)} zł / {sumHC.ToString("N2", Pl)} zł/kg");
            sb.AppendLine($"  LibraNet plan: {sumLKg.ToString("N0", Pl)} kg");
            sb.AppendLine($"  Specyfikacja:  {sumSKg.ToString("N0", Pl)} kg / {sumSW.ToString("N2", Pl)} zł / {sumSC.ToString("N2", Pl)} zł/kg");
            sb.AppendLine($"  Δ HANDEL-SPEC: {(sumDKg >= 0 ? "+" : "")}{sumDKg.ToString("N0", Pl)} kg  ({(sumDProc >= 0 ? "+" : "")}{sumDProc.ToString("N2", Pl)}%)");
            return sb.ToString();
        }

        // ============ TEKST 2: OFICJALNY — notka do ministerstwa ============
        private string ZbudujTekstOficjalny()
        {
            if (!_hasData) return "";

            decimal kgRazem = _kg7 + _kg8;
            decimal sumaRazem = _suma7 + _suma8;
            decimal tonyRazem = kgRazem / 1000m;
            decimal sredniaCena = kgRazem > 0 ? sumaRazem / kgRazem : 0;

            var sb = new StringBuilder();
            sb.AppendLine($"Skup drobiu rzeźnego od {_ostatniOd:dd.MM.yyyy} do {_ostatniDo:dd.MM.yyyy}");
            sb.AppendLine();
            sb.AppendLine("Kurczak typu Broiler");
            sb.AppendLine($"  Ilość:        {kgRazem.ToString("N0", Pl)} kg  ({tonyRazem.ToString("N3", Pl)} t)");
            sb.AppendLine($"  Wartość:      {sumaRazem.ToString("N2", Pl)} zł netto");
            sb.AppendLine($"  Cena średnia: {sredniaCena.ToString("N2", Pl)} zł/kg");
            return sb.ToString();
        }

        private void BtnCopyWewnetrzny_Click(object sender, RoutedEventArgs e) => Kopiuj(txtWewnetrzny, lblCopyHintWew);
        private void BtnCopyOficjalny_Click(object sender, RoutedEventArgs e) => Kopiuj(txtOficjalny, lblCopyHintOf);

        private static void Kopiuj(TextBox tb, TextBlock hint)
        {
            if (string.IsNullOrWhiteSpace(tb.Text))
            {
                MessageBox.Show("Pole tekstowe jest puste — pobierz dane.", "Brak treści",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                Clipboard.SetText(tb.Text);
                if (hint != null) hint.Text = "✓ Skopiowane";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd kopiowania: " + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ============ MODEL ROW dla DataGrid ============
    public class DzienWiersz
    {
        public DateTime Data { get; set; }
        public string DataText { get; set; } = "";
        public string Dow { get; set; } = "";
        public Visibility DowVis { get; set; } = Visibility.Visible;
        public decimal HKg { get; set; }
        public decimal HW { get; set; }
        public decimal HC { get; set; }
        public decimal LKg { get; set; }
        public decimal SKg { get; set; }
        public decimal SW { get; set; }
        public decimal SC { get; set; }
        public string DKgText { get; set; } = "";
        public string DProcText { get; set; } = "";
        public string StatusKey { get; set; } = "Ok"; // Ok | Warn | Bad | Pending
        public bool IsTotal { get; set; }
        public string TooltipText { get; set; } = "";
    }
}
