using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Sprawozdania.Services
{
    // ════════════════════════════════════════════════════════════════════
    // R-09U — Agregacja z karty PODSUMOWANIE specyfikacji surowca
    //
    // ŹRÓDŁO: LibraNet.FarmerCalc (1 wiersz = 1 specyfikacja = 1 partia z 1 dnia)
    // Logika 1:1 z WidokSpecyfikacje.LoadPodsumowanieData():
    //   srWaga    = Netto / (LumQnt + Padle)
    //   Netto     = NettoFarmWeight jeśli >0, inaczej NettoWeight
    //   KgPadle   = jeśli PiK=1 → 0, inaczej Padle × srWaga
    //   KgKonf    = jeśli PiK=1 → 0, inaczej (CH+NW+ZM) × srWaga
    //   KgUbytek  = Netto × Loss
    //   DoZaplaty = Netto - KgPadle - KgKonf - KgUbytek - Opasienie - KlasaB
    //
    // Kolumny w wyniku per dzień:
    //   Zdatne[szt]  = SUM(LumQnt - (CH+NW+ZM))   ← sztuki nadające się do konsumpcji
    //   Padle[szt]   = SUM(DeclI2)
    //   Konfi[szt]   = SUM(CH+NW+ZM)
    //   Konfi[kg]    = SUM(KgKonf)
    //   Padle[kg]    = SUM(KgPadle)
    //   Zywiec[kg]   = SUM(DoZaplaty)              ← realna waga zdatnego żywca
    //   Suma[kg]     = SUM(KgPadle + KgKonf)       ← waga odpadu (D2 R-09U)
    //
    // Mapowanie do R-09U (decyzja Sergiusza 2026-05-23):
    //   Dział 1 (ubój całkowity)        szt = Zdatne+Konfi (=LUMEL),  kg = Zywiec+Konfi[kg]
    //   Dział 2 (mięso nie do konsumpcji) szt = Padle+Konfi,           kg = Suma[kg]
    // ════════════════════════════════════════════════════════════════════
    public class R09USpecyfikacjeService
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public async Task<List<R09USpecDzien>> PobierzZaMiesiacAsync(int rok, int miesiac)
        {
            DateTime od = new(rok, miesiac, 1);
            DateTime doD = od.AddMonths(1).AddDays(-1);

            const string sql = @"
WITH Surowe AS (
    SELECT
        CAST(CalcDate AS DATE) AS Dzien,
        ISNULL(LumQnt, 0)                                                  AS LumQnt,
        ISNULL(DeclI2, 0)                                                  AS Padle,
        ISNULL(DeclI3, 0) + ISNULL(DeclI4, 0) + ISNULL(DeclI5, 0)          AS Konfi,
        ISNULL(IncDeadConf, 0)                                             AS PiK,
        ISNULL(Loss, 0)                                                    AS Loss,
        ISNULL(Opasienie, 0)                                               AS Opasienie,
        ISNULL(KlasaB, 0)                                                  AS KlasaB,
        CASE WHEN ISNULL(NettoFarmWeight, 0) > 0 THEN NettoFarmWeight
             ELSE ISNULL(NettoWeight, 0) END                               AS Netto
    FROM dbo.FarmerCalc
    WHERE CalcDate >= @DataOd AND CalcDate <= @DataDo
),
PerSpec AS (
    SELECT
        Dzien, LumQnt, Padle, Konfi, Netto, PiK, Loss, Opasienie, KlasaB,
        CASE WHEN (LumQnt + Padle) > 0 THEN Netto / (LumQnt + Padle) ELSE 0 END AS SrWaga
    FROM Surowe
),
WyliczKg AS (
    SELECT
        Dzien, LumQnt, Padle, Konfi, Netto,
        CASE WHEN PiK = 1 THEN 0 ELSE ROUND(Padle * SrWaga, 0) END AS KgPadle,
        CASE WHEN PiK = 1 THEN 0 ELSE ROUND(Konfi * SrWaga, 0) END AS KgKonf,
        ROUND(Netto * Loss, 0) AS KgUbytek,
        Opasienie, KlasaB
    FROM PerSpec
),
DoZap AS (
    SELECT
        Dzien, LumQnt, Padle, Konfi, KgPadle, KgKonf,
        (Netto - KgPadle - KgKonf - KgUbytek - Opasienie - KlasaB) AS DoZaplaty
    FROM WyliczKg
)
SELECT
    Dzien,
    SUM(LumQnt - Konfi)                  AS Zdatne_szt,
    SUM(Padle)                           AS Padle_szt,
    SUM(Konfi)                           AS Konfi_szt,
    SUM(KgKonf)                          AS Konfi_kg,
    SUM(KgPadle)                         AS Padle_kg,
    SUM(DoZaplaty)                       AS Zywiec_kg,
    SUM(KgPadle + KgKonf)                AS Suma_kg
FROM DoZap
GROUP BY Dzien
ORDER BY Dzien;";

            var lista = new List<R09USpecDzien>();
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 180 };
            cmd.Parameters.AddWithValue("@DataOd", od.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@DataDo", doD.ToString("yyyy-MM-dd"));

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                lista.Add(new R09USpecDzien
                {
                    Data = rdr.GetDateTime(0),
                    ZdatneSzt = Convert.ToInt32(rdr.GetValue(1)),
                    PadleSzt = Convert.ToInt32(rdr.GetValue(2)),
                    KonfiSzt = Convert.ToInt32(rdr.GetValue(3)),
                    KonfiKg = Convert.ToDecimal(rdr.GetValue(4)),
                    PadleKg = Convert.ToDecimal(rdr.GetValue(5)),
                    ZywiecKg = Convert.ToDecimal(rdr.GetValue(6)),
                    SumaKg = Convert.ToDecimal(rdr.GetValue(7))
                });
            }
            return lista;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // Wiersz dziennej tabeli + obliczone wartości R-09U Dział 1/2
    // ════════════════════════════════════════════════════════════════════
    public class R09USpecDzien
    {
        public DateTime Data { get; set; }
        public string DataText => Data.ToString("dd.MM");
        public string DowText => Data.ToString("ddd", new System.Globalization.CultureInfo("pl-PL"))
            .ToUpperInvariant().Replace(".", "");

        // Surowe kolumny z Podsumowania
        public int ZdatneSzt { get; set; }
        public int PadleSzt { get; set; }
        public int KonfiSzt { get; set; }
        public decimal KonfiKg { get; set; }
        public decimal PadleKg { get; set; }
        public decimal ZywiecKg { get; set; }
        public decimal SumaKg { get; set; }

        // ═══════ Mapowanie do R-09U ═══════
        // Dział 1: całkowity ubój — wszystkie zwierzęta które trafiły na linię
        public int D1_Sztuki => ZdatneSzt + KonfiSzt;       // = LUMEL
        public decimal D1_Kg => ZywiecKg + KonfiKg;          // waga żywa razem ze skonfiskowanymi

        // Dział 2: ubój zdyskwalifikowany (padłe + skonfiskowane na linii)
        public int D2_Sztuki => PadleSzt + KonfiSzt;
        public decimal D2_Kg => SumaKg;                     // = KgPadle + KgKonf

        public bool JestPusta =>
            ZdatneSzt == 0 && PadleSzt == 0 && KonfiSzt == 0 && ZywiecKg == 0;
    }
}
