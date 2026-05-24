using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.WPF
{
    public partial class SzczegolyDniaWindow : Window
    {
        public static readonly RoutedCommand CloseCmd = new RoutedCommand();

        private const string ConnHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private const int TwId7 = 67653;
        private const int TwId8 = 67654;

        private static readonly CultureInfo Pl = new CultureInfo("pl-PL");

        private readonly DateTime _data;

        public ObservableCollection<HandelFaktura> HandelRows { get; } = new();
        public ObservableCollection<LibraDostawa> LibraRows { get; } = new();
        public ObservableCollection<SpecHodowca> SpecRows { get; } = new();

        public SzczegolyDniaWindow(DateTime data)
        {
            InitializeComponent();
            _data = data.Date;

            CommandBindings.Add(new CommandBinding(CloseCmd, (s, e) => Close()));

            dgHandel.ItemsSource = HandelRows;
            dgLibra.ItemsSource = LibraRows;
            dgSpec.ItemsSource = SpecRows;

            lblTitleDate.Text = _data.ToString("d MMMM yyyy", Pl);
            lblTitleDow.Text = "· " + _data.ToString("dddd", Pl);

            Loaded += async (s, e) => await LoadAsync();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private async Task LoadAsync()
        {
            loadingOverlay.Visibility = Visibility.Visible;
            try
            {
                var taskH = Task.Run(() => PobierzHandel(_data));
                var taskL = Task.Run(() => PobierzLibra(_data));
                var taskS = Task.Run(() => PobierzSpec(_data));
                await Task.WhenAll(taskH, taskL, taskS);

                HandelRows.Clear();
                foreach (var r in await taskH) HandelRows.Add(r);

                LibraRows.Clear();
                foreach (var r in await taskL) LibraRows.Add(r);

                SpecRows.Clear();
                foreach (var r in await taskS) SpecRows.Add(r);

                AktualizujSummaryStrip();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd pobierania szczegółów:\n" + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void AktualizujSummaryStrip()
        {
            decimal hKg = 0, hW = 0;
            foreach (var r in HandelRows) { hKg += r.Kg; hW += r.Wartosc; }
            decimal lKg = 0;
            foreach (var r in LibraRows) lKg += r.RazemKg;
            decimal sKg = 0, sW = 0;
            foreach (var r in SpecRows) { sKg += r.DoZaplaty; sW += r.Wartosc; }

            sumHandelKg.Text = hKg.ToString("N0", Pl) + " kg";
            sumHandelW.Text = hW.ToString("N2", Pl) + " zł";
            sumLibraKg.Text = lKg.ToString("N0", Pl) + " kg";
            sumLibraInfo.Text = LibraRows.Count == 0 ? "Brak danych" : $"{LibraRows.Count} dostaw";
            sumSpecKg.Text = sKg.ToString("N0", Pl) + " kg";
            sumSpecW.Text = sW.ToString("N2", Pl) + " zł";

            decimal dKg = hKg - sKg;
            decimal dProc = sKg > 0 ? (dKg / sKg) * 100m : 0;
            sumDelta.Text = (dKg >= 0 ? "+" : "") + dKg.ToString("N0", Pl) + " kg";
            sumDeltaProc.Text = sKg > 0 ? (dProc >= 0 ? "+" : "") + dProc.ToString("N2", Pl) + "%" : "—";

            double abs = (double)Math.Abs(dProc);
            (Color bg, Color fg) = abs <= 0.5
                ? ((Color)ColorConverter.ConvertFromString("#E8F5E9"), (Color)ColorConverter.ConvertFromString("#1B5E20"))
                : abs <= 2.0
                ? ((Color)ColorConverter.ConvertFromString("#FFF4E0"), (Color)ColorConverter.ConvertFromString("#8A5500"))
                : ((Color)ColorConverter.ConvertFromString("#FDEAEA"), (Color)ColorConverter.ConvertFromString("#B72A2A"));
            sumDeltaBorder.Background = new SolidColorBrush(bg);
            var fgBrush = new SolidColorBrush(fg);
            sumDelta.Foreground = fgBrush;
            sumDeltaProc.Foreground = fgBrush;
        }

        // ============ SQL HANDEL — faktury z konkretnego dnia ============
        private List<HandelFaktura> PobierzHandel(DateTime data)
        {
            const string sql = @"
SELECT DK.kod, DK.typ_dk, KH.shortcut AS Dostawca, TW.kod AS Towar,
       DP.ilosc AS Kg, DP.wartNetto AS Wartosc,
       CASE WHEN DP.ilosc <> 0 THEN DP.wartNetto / DP.ilosc ELSE 0 END AS Cena
FROM [Handel].[HM].[DK] DK
INNER JOIN [Handel].[HM].[DP] DP ON DP.super = DK.id
INNER JOIN [Handel].[HM].[TW] TW ON TW.id = DP.idtw
LEFT JOIN [Handel].[SSCommon].[STContractors] KH ON KH.Id = DK.khid
WHERE DK.data = @Data
  AND ISNULL(DK.anulowany, 0) = 0 AND DK.aktywny = 1
  AND DK.typ_dk IN (N'FVZ', N'FVR', N'FKZ')
  AND DP.idtw IN (@TwId7, @TwId8)
ORDER BY DK.kod, TW.kod;
";
            var lista = new List<HandelFaktura>();
            using var conn = new SqlConnection(ConnHandel);
            conn.Open();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            cmd.Parameters.Add("@Data", System.Data.SqlDbType.Date).Value = data;
            cmd.Parameters.Add("@TwId7", System.Data.SqlDbType.Int).Value = TwId7;
            cmd.Parameters.Add("@TwId8", System.Data.SqlDbType.Int).Value = TwId8;
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                lista.Add(new HandelFaktura
                {
                    Kod = rdr["kod"]?.ToString() ?? "",
                    TypDk = rdr["typ_dk"]?.ToString() ?? "",
                    Dostawca = rdr["Dostawca"]?.ToString() ?? "",
                    Towar = rdr["Towar"]?.ToString() ?? "",
                    Kg = SafeAbs(rdr, "Kg"),
                    Wartosc = SafeAbs(rdr, "Wartosc"),
                    Cena = SafeAbs(rdr, "Cena")
                });
            }
            return lista;
        }

        // ============ SQL LibraNet HarmonogramDostaw — plan z dnia ============
        private List<LibraDostawa> PobierzLibra(DateTime data)
        {
            const string sql = @"
SELECT LP, Dostawca, SztukiDek, WagaDek, Cena, TypCeny, Auta, UWAGI
FROM [LibraNet].[dbo].[HarmonogramDostaw]
WHERE DataOdbioru = @Data
  AND Bufor IN ('Potwierdzony', 'Potwierdzone')
ORDER BY LP;
";
            var lista = new List<LibraDostawa>();
            using var conn = new SqlConnection(ConnLibra);
            conn.Open();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@Data", data.ToString("yyyy-MM-dd"));
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                int szt = rdr.IsDBNull(rdr.GetOrdinal("SztukiDek")) ? 0 : Convert.ToInt32(rdr["SztukiDek"]);
                decimal waga = rdr.IsDBNull(rdr.GetOrdinal("WagaDek")) ? 0m : Convert.ToDecimal(rdr["WagaDek"]);
                lista.Add(new LibraDostawa
                {
                    LP = rdr["LP"] != DBNull.Value ? Convert.ToInt32(rdr["LP"]) : 0,
                    Dostawca = (rdr["Dostawca"]?.ToString() ?? "").Trim(),
                    Sztuki = szt,
                    Waga = waga,
                    RazemKg = szt * waga,
                    Cena = rdr.IsDBNull(rdr.GetOrdinal("Cena")) ? 0m : Convert.ToDecimal(rdr["Cena"]),
                    TypCeny = (rdr["TypCeny"]?.ToString() ?? "").Trim(),
                    Auta = rdr.IsDBNull(rdr.GetOrdinal("Auta")) ? 0 : Convert.ToInt32(rdr["Auta"]),
                    Uwagi = (rdr["UWAGI"]?.ToString() ?? "").Trim()
                });
            }
            return lista;
        }

        // ============ SQL Specyfikacje FarmerCalc — per hodowca z dnia ============
        // Replikuje logikę PDF (GeneratePDFReport w WidokSpecyfikacje.xaml.cs)
        private List<SpecHodowca> PobierzSpec(DateTime data)
        {
            const string sql = @"
WITH Surowe AS (
    SELECT
        fc.ID,
        ISNULL(dos.ShortName, CAST(fc.CustomerRealGID AS NVARCHAR(50))) AS Hodowca,
        CASE WHEN ISNULL(fc.NettoFarmWeight,0) > 0 THEN fc.NettoFarmWeight ELSE fc.NettoWeight END AS Netto,
        ISNULL(fc.IncDeadConf, 0) AS CzyPiK,
        ISNULL(fc.Loss, 0) AS LossProc,
        ISNULL(fc.LumQnt, 0) AS Lumel,
        ISNULL(fc.DeclI2, 0) AS Padle,
        ISNULL(fc.DeclI3, 0) + ISNULL(fc.DeclI4, 0) + ISNULL(fc.DeclI5, 0) AS Konf,
        ISNULL(fc.Opasienie, 0) AS Opasienie,
        ISNULL(fc.KlasaB, 0) AS KlasaB,
        ISNULL(fc.Price, 0) + ISNULL(fc.Addition, 0) AS Cena
    FROM [LibraNet].[dbo].[FarmerCalc] fc
    LEFT JOIN [LibraNet].[dbo].[Dostawcy] dos ON dos.ID = fc.CustomerRealGID
    WHERE fc.CalcDate = @Data
),
PerSpec AS (
    SELECT
        ID, Hodowca, Netto, Cena, CzyPiK, LossProc,
        CASE WHEN (Lumel + Padle) > 0 THEN Netto / (Lumel + Padle) ELSE 0 END AS SredniaWaga,
        Padle, Konf, Opasienie, KlasaB
    FROM Surowe
),
KGRowy AS (
    SELECT
        ID, Hodowca, Netto, Cena, Opasienie, KlasaB,
        ROUND(Netto * LossProc, 0) AS UbytekKG,
        CASE WHEN CzyPiK = 1 THEN 0 ELSE ROUND(Padle * SredniaWaga, 0) END AS PadleKG,
        CASE WHEN CzyPiK = 1 THEN 0 ELSE ROUND(Konf  * SredniaWaga, 0) END AS KonfKG
    FROM PerSpec
)
SELECT
    Hodowca, Netto,
    PadleKG  AS Padle,
    KonfKG   AS Konf,
    UbytekKG AS Ubytek,
    Opasienie, KlasaB,
    (Netto - PadleKG - KonfKG - UbytekKG - Opasienie - KlasaB) AS DoZaplaty,
    Cena,
    (Netto - PadleKG - KonfKG - UbytekKG - Opasienie - KlasaB) * Cena AS Wartosc
FROM KGRowy
ORDER BY Hodowca;
";
            var lista = new List<SpecHodowca>();
            using var conn = new SqlConnection(ConnLibra);
            conn.Open();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@Data", data.ToString("yyyy-MM-dd"));
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                lista.Add(new SpecHodowca
                {
                    Hodowca = rdr["Hodowca"]?.ToString() ?? "?",
                    Netto = SafeDec(rdr, "Netto"),
                    Padle = SafeDec(rdr, "Padle"),
                    Konf = SafeDec(rdr, "Konf"),
                    Ubytek = SafeDec(rdr, "Ubytek"),
                    Opasienie = SafeDec(rdr, "Opasienie"),
                    KlasaB = SafeDec(rdr, "KlasaB"),
                    DoZaplaty = SafeDec(rdr, "DoZaplaty"),
                    Cena = SafeDec(rdr, "Cena"),
                    Wartosc = SafeDec(rdr, "Wartosc")
                });
            }
            return lista;
        }

        private static decimal SafeDec(SqlDataReader rdr, string col)
        {
            int i = rdr.GetOrdinal(col);
            return rdr.IsDBNull(i) ? 0m : Convert.ToDecimal(rdr.GetValue(i));
        }
        private static decimal SafeAbs(SqlDataReader rdr, string col)
        {
            int i = rdr.GetOrdinal(col);
            return rdr.IsDBNull(i) ? 0m : Math.Abs(Convert.ToDecimal(rdr.GetValue(i)));
        }
    }

    // ============ MODELS ============
    public class HandelFaktura
    {
        public string Kod { get; set; } = "";
        public string TypDk { get; set; } = "";
        public string Dostawca { get; set; } = "";
        public string Towar { get; set; } = "";
        public decimal Kg { get; set; }
        public decimal Cena { get; set; }
        public decimal Wartosc { get; set; }
    }

    public class LibraDostawa
    {
        public int LP { get; set; }
        public string Dostawca { get; set; } = "";
        public int Sztuki { get; set; }
        public decimal Waga { get; set; }
        public decimal RazemKg { get; set; }
        public decimal Cena { get; set; }
        public string TypCeny { get; set; } = "";
        public int Auta { get; set; }
        public string Uwagi { get; set; } = "";
    }

    public class SpecHodowca
    {
        public string Hodowca { get; set; } = "";
        public decimal Netto { get; set; }
        public decimal Padle { get; set; }
        public decimal Konf { get; set; }
        public decimal Ubytek { get; set; }
        public decimal Opasienie { get; set; }
        public decimal KlasaB { get; set; }
        public decimal DoZaplaty { get; set; }
        public decimal Cena { get; set; }
        public decimal Wartosc { get; set; }
    }
}
