using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    public partial class HistoriaFakturKontrahentaWindow : Window
    {
        private const string ConnHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private readonly int _idSymf;
        private readonly string _customerGid;
        private readonly string _dostawcaName;

        private readonly ObservableCollection<HistoriaWiersz> _items = new();

        public HistoriaFakturKontrahentaWindow(int idSymf, string customerGid, string dostawcaName)
        {
            InitializeComponent();
            _idSymf = idSymf;
            _customerGid = customerGid ?? "";
            _dostawcaName = dostawcaName ?? "";

            dgHistoria.ItemsSource = _items;

            dpOd.SelectedDate = DateTime.Today.AddMonths(-12);
            dpDo.SelectedDate = DateTime.Today;

            Loaded += async (_, __) => await LoadKontrahentMeta();
            Loaded += async (_, __) => await Odswiez();
        }

        private async System.Threading.Tasks.Task LoadKontrahentMeta()
        {
            string nip = "", miasto = "", adres = "";
            try
            {
                if (_idSymf > 0)
                {
                    using var conn = new SqlConnection(ConnHandel);
                    await conn.OpenAsync();
                    using var cmd = new SqlCommand(@"
                        SELECT TOP 1
                            ISNULL(NIP, '')       AS NIP,
                            ISNULL(City, '')      AS Miasto,
                            ISNULL(Street, '')    AS Adres
                        FROM SSCommon.STContractors
                        WHERE Id = @Id", conn);
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = _idSymf;
                    using var rdr = await cmd.ExecuteReaderAsync();
                    if (await rdr.ReadAsync())
                    {
                        nip = rdr["NIP"]?.ToString() ?? "";
                        miasto = rdr["Miasto"]?.ToString() ?? "";
                        adres = rdr["Adres"]?.ToString() ?? "";
                    }
                }
            }
            catch { /* nie krytyczne */ }

            txtKontrahent.Text = _dostawcaName;
            var meta = new List<string>();
            if (!string.IsNullOrWhiteSpace(nip)) meta.Add("NIP " + nip);
            if (_idSymf > 0) meta.Add("IdSymf " + _idSymf);
            else meta.Add("(brak mapowania na Symfonie)");
            if (!string.IsNullOrWhiteSpace(miasto) || !string.IsNullOrWhiteSpace(adres))
                meta.Add((adres + " " + miasto).Trim());
            txtKontrahentMeta.Text = string.Join("  •  ", meta);
        }

        private async System.Threading.Tasks.Task Odswiez()
        {
            _items.Clear();
            txtStatus.Text = "Ładowanie historii...";

            var od = dpOd.SelectedDate ?? DateTime.Today.AddMonths(-12);
            var doData = dpDo.SelectedDate ?? DateTime.Today;
            if (od > doData) (od, doData) = (doData, od);

            var listaFv = new List<HistoriaWiersz>();
            var listaPz = new List<HistoriaWiersz>();

            try
            {
                if (_idSymf > 0)
                {
                    if (chkFv.IsChecked == true || chkKor.IsChecked == true)
                        listaFv = await PobierzHandelFV(od, doData, chkFv.IsChecked == true, chkKor.IsChecked == true);
                    if (chkPz.IsChecked == true)
                        listaPz = await PobierzHandelPZ(od, doData);
                }

                foreach (var r in listaFv) _items.Add(r);
                foreach (var r in listaPz) _items.Add(r);

                var posortowane = _items.OrderByDescending(x => x.Data).ThenBy(x => x.Typ).ToList();
                _items.Clear();
                foreach (var r in posortowane) _items.Add(r);

                AktualizujKpi();
                txtStatus.Text = _items.Count == 0
                    ? (_idSymf > 0 ? "Brak dokumentów w wybranym zakresie." : "Dostawca bez mapowania na Symfonie — brak historii do pobrania.")
                    : $"Znaleziono {_items.Count} dokumentów ({listaFv.Count} FV/FKZ + {listaPz.Count} PZ).";
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Błąd: " + ex.Message;
            }
        }

        // ============ HANDEL — faktury zakupu (FVR/FVZ/FKZ) per pozycja ============
        private async System.Threading.Tasks.Task<List<HistoriaWiersz>> PobierzHandelFV(
            DateTime od, DateTime doData, bool wlaczFv, bool wlaczKor)
        {
            var typy = new List<string>();
            if (wlaczFv)  { typy.Add("FVR"); typy.Add("FVZ"); }
            if (wlaczKor) { typy.Add("FKZ"); }
            if (typy.Count == 0) return new List<HistoriaWiersz>();

            string inList = string.Join(",", typy.Select((_, i) => "@t" + i));

            string sql = $@"
SELECT
    DK.data        AS Data,
    DK.typ_dk      AS Typ,
    DK.kod         AS NrDok,
    ISNULL(TW.kod, '') AS Towar,
    DP.ilosc       AS Kg,
    DP.wartNetto   AS WartNetto,
    CASE WHEN DP.ilosc <> 0 THEN DP.wartNetto / DP.ilosc ELSE 0 END AS Cena
FROM HM.DK DK
INNER JOIN HM.DP DP ON DP.super = DK.id
LEFT JOIN  HM.TW TW ON TW.id = DP.idtw
WHERE DK.khid = @IdSymf
  AND DK.data >= @Od
  AND DK.data <  DATEADD(DAY, 1, @Do)
  AND ISNULL(DK.anulowany, 0) = 0
  AND DK.aktywny = 1
  AND DK.typ_dk IN ({inList})
ORDER BY DK.data DESC, DK.kod;";

            var lista = new List<HistoriaWiersz>();
            using var conn = new SqlConnection(ConnHandel);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            cmd.Parameters.Add("@IdSymf", SqlDbType.Int).Value = _idSymf;
            cmd.Parameters.Add("@Od", SqlDbType.Date).Value = od.Date;
            cmd.Parameters.Add("@Do", SqlDbType.Date).Value = doData.Date;
            for (int i = 0; i < typy.Count; i++)
                cmd.Parameters.Add("@t" + i, SqlDbType.NVarChar, 10).Value = typy[i];

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                lista.Add(new HistoriaWiersz
                {
                    Data    = rdr.GetDateTime(rdr.GetOrdinal("Data")),
                    Typ     = rdr["Typ"]?.ToString() ?? "",
                    NrDok   = rdr["NrDok"]?.ToString() ?? "",
                    Towar   = rdr["Towar"]?.ToString() ?? "",
                    Kg      = SafeAbs(rdr, "Kg"),
                    Cena    = SafeAbs(rdr, "Cena"),
                    Wartosc = SafeAbs(rdr, "WartNetto"),
                    Zrodlo  = "HANDEL"
                });
            }
            return lista;
        }

        // ============ HANDEL — PZ (HM.MG) ============
        private async System.Threading.Tasks.Task<List<HistoriaWiersz>> PobierzHandelPZ(DateTime od, DateTime doData)
        {
            const string sql = @"
SELECT
    MG.data        AS Data,
    MG.seria       AS Typ,
    MG.kod         AS NrDok,
    ISNULL(TW.kod, '') AS Towar,
    SUM(ABS(MZ.ilosc))     AS Kg,
    SUM(ABS(MZ.wartNetto)) AS WartNetto
FROM HM.MG MG
INNER JOIN HM.MZ MZ ON MZ.super = MG.id
LEFT JOIN  HM.TW TW ON TW.id = MZ.idtw
WHERE MG.khid = @IdSymf
  AND CAST(MG.data AS DATE) >= @Od
  AND CAST(MG.data AS DATE) <= @Do
  AND ISNULL(MG.anulowany, 0) = 0
  AND MG.seria LIKE N'%PZ%'
GROUP BY MG.data, MG.seria, MG.kod, TW.kod
ORDER BY MG.data DESC, MG.kod;";

            var lista = new List<HistoriaWiersz>();
            using var conn = new SqlConnection(ConnHandel);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            cmd.Parameters.Add("@IdSymf", SqlDbType.Int).Value = _idSymf;
            cmd.Parameters.Add("@Od", SqlDbType.Date).Value = od.Date;
            cmd.Parameters.Add("@Do", SqlDbType.Date).Value = doData.Date;

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                var kg = SafeAbs(rdr, "Kg");
                var w  = SafeAbs(rdr, "WartNetto");
                lista.Add(new HistoriaWiersz
                {
                    Data    = rdr.GetDateTime(rdr.GetOrdinal("Data")),
                    Typ     = rdr["Typ"]?.ToString() ?? "PZ",
                    NrDok   = rdr["NrDok"]?.ToString() ?? "",
                    Towar   = rdr["Towar"]?.ToString() ?? "",
                    Kg      = kg,
                    Cena    = kg > 0 ? w / kg : 0,
                    Wartosc = w,
                    Zrodlo  = "HANDEL"
                });
            }
            return lista;
        }

        private void AktualizujKpi()
        {
            var fv = _items.Where(x => x.Typ is "FVR" or "FVZ" or "FKZ").ToList();
            kpiLiczbaFv.Text  = fv.Select(x => x.NrDok).Distinct().Count().ToString();
            kpiSumaNetto.Text = fv.Sum(x => x.Wartosc).ToString("N2");
            kpiSumaKg.Text    = fv.Sum(x => x.Kg).ToString("N0");
            var totalKg = fv.Sum(x => x.Kg);
            kpiSrCena.Text    = totalKg > 0 ? (fv.Sum(x => x.Wartosc) / totalKg).ToString("N2") : "—";
            kpiOstatniaFv.Text = fv.Any() ? fv.Max(x => x.Data).ToString("yyyy-MM-dd") : "—";
        }

        private static decimal SafeAbs(SqlDataReader rdr, string col)
        {
            int idx = rdr.GetOrdinal(col);
            if (rdr.IsDBNull(idx)) return 0m;
            return Math.Abs(Convert.ToDecimal(rdr.GetValue(idx)));
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await Odswiez();

        private async void BtnQuick3M_Click(object sender, RoutedEventArgs e)
        {
            dpOd.SelectedDate = DateTime.Today.AddMonths(-3);
            dpDo.SelectedDate = DateTime.Today;
            await Odswiez();
        }

        private async void BtnQuick12M_Click(object sender, RoutedEventArgs e)
        {
            dpOd.SelectedDate = DateTime.Today.AddMonths(-12);
            dpDo.SelectedDate = DateTime.Today;
            await Odswiez();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        public class HistoriaWiersz
        {
            public DateTime Data { get; set; }
            public string Typ { get; set; } = "";
            public string NrDok { get; set; } = "";
            public string Towar { get; set; } = "";
            public decimal Kg { get; set; }
            public decimal Cena { get; set; }
            public decimal Wartosc { get; set; }
            public string Zrodlo { get; set; } = "";
        }
    }
}
