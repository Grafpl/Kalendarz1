using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.ZSRIR.Services
{
    // Agreguje dane do raportu ZSRIR z bazy HANDEL (faktury) + LibraNet (specyfikacja FarmerCalc).
    // Decyzja architektoniczna: jako "wiążące" źródło wartości do raportu MRiRW
    // bierzemy SPECYFIKACJĘ (FarmerCalc) — to finalne rozliczenie po uboju,
    // identyczne z fakturami zakupu w stanie ustalonym. To samo co PDF.
    // HANDEL/Libra są tylko do weryfikacji w SprawozdaniaWindow (delta).
    public class ZsrirDataBuilder
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public record RaportTygodniowy(
            DateTime OkresOd,
            DateTime OkresDo,
            decimal Kg,
            decimal Tony,
            decimal WartoscNetto,
            decimal CenaZlKg,
            decimal CenaZlTona,
            int LiczbaHodowcow);

        public async Task<RaportTygodniowy> ZbudujRaportAsync(DateTime od, DateTime doD)
        {
            const string sql = @"
WITH Surowe AS (
    SELECT
        fc.ID,
        ISNULL(fc.CustomerRealGID, 0) AS HodowcaId,
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
    WHERE fc.CalcDate BETWEEN @Od AND @Do
),
PerSpec AS (
    SELECT
        ID, HodowcaId, Netto, Cena, CzyPiK, LossProc,
        CASE WHEN (Lumel + Padle) > 0 THEN Netto / (Lumel + Padle) ELSE 0 END AS SredniaWaga,
        Padle, Konf, Opasienie, KlasaB
    FROM Surowe
),
KGRowy AS (
    SELECT
        HodowcaId, Netto, Cena, Opasienie, KlasaB,
        ROUND(Netto * LossProc, 0) AS UbytekKG,
        CASE WHEN CzyPiK = 1 THEN 0 ELSE ROUND(Padle * SredniaWaga, 0) END AS PadleKG,
        CASE WHEN CzyPiK = 1 THEN 0 ELSE ROUND(Konf  * SredniaWaga, 0) END AS KonfKG
    FROM PerSpec
),
DoZap AS (
    SELECT HodowcaId, Cena,
           (Netto - PadleKG - KonfKG - UbytekKG - Opasienie - KlasaB) AS DoZaplaty
    FROM KGRowy
)
SELECT
    SUM(DoZaplaty)         AS KgRazem,
    SUM(DoZaplaty * Cena)  AS WartoscRazem,
    COUNT(DISTINCT HodowcaId) AS LiczbaHodowcow
FROM DoZap;
";
            decimal kg = 0, wart = 0;
            int hodowcow = 0;
            using (var conn = new SqlConnection(ConnLibra))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@Od", od.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@Do", doD.ToString("yyyy-MM-dd"));
                using var rdr = await cmd.ExecuteReaderAsync();
                if (await rdr.ReadAsync())
                {
                    kg = rdr.IsDBNull(0) ? 0 : Convert.ToDecimal(rdr.GetValue(0));
                    wart = rdr.IsDBNull(1) ? 0 : Convert.ToDecimal(rdr.GetValue(1));
                    hodowcow = rdr.IsDBNull(2) ? 0 : rdr.GetInt32(2);
                }
            }

            decimal tony = Math.Round(kg / 1000m, 3);
            decimal cenaKg = kg > 0 ? wart / kg : 0;
            decimal cenaTona = Math.Round(cenaKg * 1000m, 2);
            return new RaportTygodniowy(od, doD, Math.Round(kg, 2), tony, Math.Round(wart, 2), cenaKg, cenaTona, hodowcow);
        }

        // Pomocnik: poprzedni tydzień Pon-Niedz
        public static (DateTime od, DateTime doD) PoprzedniTydzien(DateTime? today = null)
        {
            var t = (today ?? DateTime.Today).Date;
            int dfm = ((int)t.DayOfWeek + 6) % 7;
            var thisMon = t.AddDays(-dfm);
            var lastMon = thisMon.AddDays(-7);
            return (lastMon, lastMon.AddDays(6));
        }
    }
}
