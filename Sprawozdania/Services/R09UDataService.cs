using System;
using System.Data;
using System.Threading.Tasks;
using Kalendarz1.Sprawozdania.Models;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Sprawozdania.Services
{
    // ════════════════════════════════════════════════════════════════════
    // R-09U — agregacja danych ubojowych za miesiąc (wiersz w14 brojlery)
    //
    // Mapowanie kolumn r1..r5 (Sage v2.0):
    //   r1 = LiczbaSztuk           ← SUM(FarmerCalc.LumQnt)
    //   r2 = WagaZywaKg            ← SUM(FarmerCalc.NettoFarmWeight)  [waga deklarowana przez hodowcę]
    //   r3 = WagaPoubojowaBruttoKg ← SUM(FarmerCalc.NettoWeight)      [waga ubojni — gotowe tuszki brutto]
    //   r4 = WagaHandlowaNettoKg   ← SUM(FarmerCalc.NettoWeight) - ubytki [waga sprzedażna]
    //   r5 = WartoscZl             ← SUM faktur FVZ+FVR+FKZ żywiec netto
    // ════════════════════════════════════════════════════════════════════
    public class R09UDataService
    {
        private const string ConnHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private const int TwIdZywiecRolnik = 67653;
        private const int TwIdZywiecVatowiec = 67654;

        public async Task<R09UPozycja> PobierzBrojleryZaMiesiacAsync(int rok, int miesiac)
        {
            DateTime od = new(rok, miesiac, 1);
            DateTime doD = od.AddMonths(1).AddDays(-1);

            var tLibra = PobierzZLibraAsync(od, doD);
            var tHandel = PobierzZHandelAsync(od, doD);
            await Task.WhenAll(tLibra, tHandel);

            return new R09UPozycja
            {
                Wiersz = R09UWiersz.Brojlery_Kurze,
                LiczbaSztuk = tLibra.Result.Sztuki,
                WagaZywaKg = tLibra.Result.WagaFarmKg,
                WagaPoubojowaBruttoKg = tLibra.Result.WagaUbojniaKg,
                WagaHandlowaNettoKg = tLibra.Result.WagaUbojniaKg, // domyślnie = brutto, user koryguje
                WartoscZl = tHandel.Result.WartoscZl
            };
        }

        // LibraNet FarmerCalc — sztuki + obie wagi
        private async Task<(int Sztuki, decimal WagaFarmKg, decimal WagaUbojniaKg)> PobierzZLibraAsync(DateTime od, DateTime doD)
        {
            const string sql = @"
SELECT
    SUM(ISNULL(LumQnt, 0))            AS Sztuki,
    SUM(ISNULL(NettoFarmWeight, 0))   AS WagaFarm,
    SUM(ISNULL(NettoWeight, 0))       AS WagaUbojnia
FROM dbo.FarmerCalc
WHERE CalcDate >= @DataOd AND CalcDate <= @DataDo;";

            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 120 };
            cmd.Parameters.AddWithValue("@DataOd", od.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@DataDo", doD.ToString("yyyy-MM-dd"));

            using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
            {
                int szt = rdr.IsDBNull(0) ? 0 : Convert.ToInt32(rdr.GetValue(0));
                decimal wf = rdr.IsDBNull(1) ? 0m : Convert.ToDecimal(rdr.GetValue(1));
                decimal wu = rdr.IsDBNull(2) ? 0m : Convert.ToDecimal(rdr.GetValue(2));
                return (szt, wf, wu);
            }
            return (0, 0m, 0m);
        }

        // ════════════════════════════════════════════════════════════════
        // DRILL-DOWN: lista partii FarmerCalc za okres (dla r1-r4)
        // ════════════════════════════════════════════════════════════════
        public async Task<System.Collections.Generic.List<R09UPartiaRow>> PobierzPartieAsync(int rok, int miesiac)
        {
            DateTime od = new(rok, miesiac, 1);
            DateTime doD = od.AddMonths(1).AddDays(-1);

            const string sql = @"
SELECT
    FC.CalcDate,
    FC.LpDostawy,
    ISNULL(PD.Hodowca, '') AS Hodowca,
    ISNULL(FC.LumQnt, 0)            AS Sztuki,
    ISNULL(FC.NettoFarmWeight, 0)   AS WagaFarm,
    ISNULL(FC.NettoWeight, 0)       AS WagaUbojnia,
    ISNULL(FC.Price, 0) + ISNULL(FC.Addition, 0) AS Cena
FROM dbo.FarmerCalc FC
LEFT JOIN dbo.PartiaDostawca PD ON PD.Partia = FC.LpDostawy
WHERE FC.CalcDate >= @DataOd AND FC.CalcDate <= @DataDo
  AND ISNULL(FC.LumQnt, 0) > 0
ORDER BY FC.CalcDate DESC, FC.LpDostawy DESC;";

            var lista = new System.Collections.Generic.List<R09UPartiaRow>();
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 180 };
            cmd.Parameters.AddWithValue("@DataOd", od.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@DataDo", doD.ToString("yyyy-MM-dd"));

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                lista.Add(new R09UPartiaRow(
                    rdr.GetDateTime(0),
                    rdr.IsDBNull(1) ? "" : rdr.GetValue(1).ToString() ?? "",
                    rdr["Hodowca"]?.ToString() ?? "",
                    Convert.ToInt32(rdr.GetValue(3)),
                    Convert.ToDecimal(rdr.GetValue(4)),
                    Convert.ToDecimal(rdr.GetValue(5)),
                    Convert.ToDecimal(rdr.GetValue(6))
                ));
            }
            return lista;
        }

        // ════════════════════════════════════════════════════════════════
        // DRILL-DOWN: faktury żywca FVZ+FVR+FKZ za okres (dla r5)
        // ════════════════════════════════════════════════════════════════
        public async Task<System.Collections.Generic.List<R09UFakturaRow>> PobierzFakturyZywcaAsync(int rok, int miesiac)
        {
            DateTime od = new(rok, miesiac, 1);
            DateTime doD = od.AddMonths(1).AddDays(-1);

            const string sql = @"
SELECT
    DK.data,
    DK.typ_dk,
    DK.kod                AS Numer,
    ISNULL(KH.Shortcut,'') AS Kontrahent,
    TW.kod                AS KodTowaru,
    TW.nazwa              AS NazwaTowaru,
    ABS(DP.ilosc)         AS Kg,
    ABS(DP.wartNetto)     AS Wartosc
FROM [Handel].[HM].[DK] DK
INNER JOIN [Handel].[HM].[DP] DP ON DP.super = DK.id
INNER JOIN [Handel].[HM].[TW] TW ON TW.id = DP.idtw
LEFT  JOIN [Handel].[SSCommon].[STContractors] KH ON KH.Id = DK.khid
WHERE DK.data >= @DataOd
  AND DK.data <  DATEADD(DAY, 1, @DataDo)
  AND ISNULL(DK.anulowany, 0) = 0
  AND DK.aktywny = 1
  AND DK.typ_dk IN (N'FVZ', N'FVR', N'FKZ')
  AND DP.idtw IN (@TwId7, @TwId8)
ORDER BY DK.data DESC, DK.id DESC;";

            var lista = new System.Collections.Generic.List<R09UFakturaRow>();
            using var conn = new SqlConnection(ConnHandel);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 180 };
            cmd.Parameters.Add("@DataOd", SqlDbType.Date).Value = od;
            cmd.Parameters.Add("@DataDo", SqlDbType.Date).Value = doD;
            cmd.Parameters.AddWithValue("@TwId7", TwIdZywiecRolnik);
            cmd.Parameters.AddWithValue("@TwId8", TwIdZywiecVatowiec);

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                lista.Add(new R09UFakturaRow(
                    rdr.GetDateTime(0),
                    rdr["typ_dk"]?.ToString() ?? "",
                    rdr["Numer"]?.ToString() ?? "",
                    rdr["Kontrahent"]?.ToString() ?? "",
                    rdr["KodTowaru"]?.ToString() ?? "",
                    rdr["NazwaTowaru"]?.ToString() ?? "",
                    Convert.ToDecimal(rdr.GetValue(6)),
                    Convert.ToDecimal(rdr.GetValue(7))
                ));
            }
            return lista;
        }

        public record R09UPartiaRow(
            DateTime Data, string LpDostawy, string Hodowca,
            int Sztuki, decimal WagaFarmKg, decimal WagaUbojniaKg, decimal CenaZlKg);

        public record R09UFakturaRow(
            DateTime Data, string TypDk, string Numer, string Kontrahent,
            string KodTowaru, string NazwaTowaru, decimal Kg, decimal Wartosc);

        // HANDEL — faktury żywca (wartość netto)
        private async Task<(decimal WartoscZl, decimal WagaKg)> PobierzZHandelAsync(DateTime od, DateTime doD)
        {
            const string sql = @"
SELECT
    SUM(DP.wartNetto)    AS SumaNetto,
    SUM(DP.ilosc)        AS SumaIlosc
FROM [Handel].[HM].[DK] DK
INNER JOIN [Handel].[HM].[DP] DP ON DP.super = DK.id
WHERE DK.data >= @DataOd
  AND DK.data <  DATEADD(DAY, 1, @DataDo)
  AND ISNULL(DK.anulowany, 0) = 0
  AND DK.aktywny = 1
  AND DK.typ_dk IN (N'FVZ', N'FVR', N'FKZ')
  AND DP.idtw IN (@TwId7, @TwId8);";

            using var conn = new SqlConnection(ConnHandel);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 120 };
            cmd.Parameters.Add("@DataOd", SqlDbType.Date).Value = od;
            cmd.Parameters.Add("@DataDo", SqlDbType.Date).Value = doD;
            cmd.Parameters.AddWithValue("@TwId7", TwIdZywiecRolnik);
            cmd.Parameters.AddWithValue("@TwId8", TwIdZywiecVatowiec);

            using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
            {
                decimal zl = rdr.IsDBNull(0) ? 0m : Math.Abs(Convert.ToDecimal(rdr.GetValue(0)));
                decimal kg = rdr.IsDBNull(1) ? 0m : Math.Abs(Convert.ToDecimal(rdr.GetValue(1)));
                return (zl, kg);
            }
            return (0m, 0m);
        }
    }
}
