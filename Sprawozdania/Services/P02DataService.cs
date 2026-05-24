using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Kalendarz1.Sprawozdania.Models;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Sprawozdania.Services
{
    // ════════════════════════════════════════════════════════════════════
    // P-02 — agregacja danych z HANDEL (Sage Symfonia) per PKWiU
    //
    // Jednostka raportowa: TONA (00130). Wszystkie wartości w tonach,
    // zaokrąglone do całych przy emisji XML.
    //
    // Źródła:
    //   • SPRZEDAŻ  = HM.DK typ FVS+FKS × HM.DP (krajowa + eksport razem)
    //   • PRODUKCJA = HM.MG typ PWU+PWP+PWK × HM.MZ (PWU dla tuszek, PWP elementów)
    //
    // Klasyfikacja PKWiU przez PkwiuKlasyfikator (reguły branżowe drobiu).
    // Okresy:
    //   • MC  = ten miesiąc
    //   • YTD = od 1 stycznia tego roku do końca tego miesiąca
    // ════════════════════════════════════════════════════════════════════
    public class P02DataService
    {
        private const string ConnHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        // ════════════════════════════════════════════════════════════════
        // GŁÓWNY ENTRY-POINT
        // Zwraca 4 pozycje (10.12.10-10, 10.12.10-50, 10.12.20-13, 10.12.20-53)
        // — zawsze w stałej kolejności, nawet jeśli niektóre są zero.
        // ════════════════════════════════════════════════════════════════
        public async Task<List<P02Pozycja>> PobierzZaMiesiacAsync(int rok, int miesiac)
        {
            DateTime mcOd = new DateTime(rok, miesiac, 1);
            DateTime mcDo = mcOd.AddMonths(1).AddDays(-1);
            DateTime ytdOd = new DateTime(rok, 1, 1);
            DateTime ytdDo = mcDo;

            // Równolegle pobieramy 4 zbiory danych
            var sprzMcTask = PobierzSprzedazAsync(mcOd, mcDo);
            var sprzYtdTask = PobierzSprzedazAsync(ytdOd, ytdDo);
            var prodMcTask = PobierzProdukcjeAsync(mcOd, mcDo);
            var prodYtdTask = PobierzProdukcjeAsync(ytdOd, ytdDo);

            await Task.WhenAll(sprzMcTask, sprzYtdTask, prodMcTask, prodYtdTask);

            var sprzMc = sprzMcTask.Result;
            var sprzYtd = sprzYtdTask.Result;
            var prodMc = prodMcTask.Result;
            var prodYtd = prodYtdTask.Result;

            // Budujemy 4 pozycje w stałej kolejności
            var wynik = new List<P02Pozycja>();
            foreach (var pkwiu in PkwiuKlasyfikator.PkwiuKolejnosc)
            {
                var poz = new P02Pozycja
                {
                    Pkwiu = pkwiu,
                    NazwaWyrobu = PkwiuKlasyfikator.NazwyWyrobow.TryGetValue(pkwiu, out var n) ? n : pkwiu,
                    JednostkaKod = "00130"
                };

                // Sprzedaż MC
                if (sprzMc.TryGetValue(pkwiu, out var sMc))
                {
                    poz.SprzedazWMiesiacuTony = KgDoTon(sMc.SumaKg);
                    foreach (var k in sMc.Kody) if (!poz.KodyTowarow.Contains(k)) poz.KodyTowarow.Add(k);
                }
                // Sprzedaż YTD
                if (sprzYtd.TryGetValue(pkwiu, out var sYtd))
                    poz.SprzedazOdPoczatkuRokuTony = KgDoTon(sYtd.SumaKg);

                // Produkcja MC
                if (prodMc.TryGetValue(pkwiu, out var pMc))
                {
                    poz.ProdukcjaWMiesiacuTony = KgDoTon(pMc.SumaKg);
                    foreach (var k in pMc.Kody) if (!poz.KodyTowarow.Contains(k)) poz.KodyTowarow.Add(k);
                }
                // Produkcja YTD
                if (prodYtd.TryGetValue(pkwiu, out var pYtd))
                    poz.ProdukcjaOdPoczatkuRokuTony = KgDoTon(pYtd.SumaKg);

                // Zapasy = 0 (wersja MVP, później dorobimy z bilansu MG)
                poz.ZapasyWyrobowTony = 0;
                poz.ZapasyTowarowTony = 0;

                wynik.Add(poz);
            }

            return wynik;
        }

        // ════════════════════════════════════════════════════════════════
        // SPRZEDAŻ — HM.DK typ FVS+FKS, ABS(ilosc), grupowanie po kodzie
        // Klasyfikator decyduje do której pozycji P-02 wpada każdy towar.
        // ════════════════════════════════════════════════════════════════
        private async Task<Dictionary<string, AgregatPkwiu>> PobierzSprzedazAsync(DateTime od, DateTime doD)
        {
            const string sql = @"
SELECT
    TW.kod                  AS KodTowaru,
    TW.nazwa                AS NazwaTowaru,
    TW.katalog              AS Katalog,
    TW.sww                  AS SwwBaza,
    SUM(DP.ilosc)           AS SumaIlosc
FROM [Handel].[HM].[DK] DK
INNER JOIN [Handel].[HM].[DP] DP ON DP.super = DK.id
INNER JOIN [Handel].[HM].[TW] TW ON TW.id = DP.idtw
WHERE DK.data >= @DataOd
  AND DK.data <  DATEADD(DAY, 1, @DataDo)
  AND ISNULL(DK.anulowany, 0) = 0
  AND DK.aktywny = 1
  AND DK.typ_dk IN (N'FVS', N'FKS')
  AND TW.katalog IN (@KatSwieze, @KatInne, @KatMrozne)
GROUP BY TW.kod, TW.nazwa, TW.katalog, TW.sww;";

            return await PobierzIKlasyfikujAsync(sql, od, doD, czyProdukcja: false);
        }

        // ════════════════════════════════════════════════════════════════
        // PRODUKCJA — HM.MG typ PWU+PWP+PWK × HM.MZ, ABS(ilosc)
        // PWU = przyjęcie z uboju (całe tuszki, "Kurczak A/B")
        // PWP = przyjęcie z produkcji (kawałki, "Filet", "Skrzydło"...)
        // PWK = przyjęcie kompensacyjne (rzadkie korekty)
        // ════════════════════════════════════════════════════════════════
        private async Task<Dictionary<string, AgregatPkwiu>> PobierzProdukcjeAsync(DateTime od, DateTime doD)
        {
            const string sql = @"
SELECT
    TW.kod                  AS KodTowaru,
    TW.nazwa                AS NazwaTowaru,
    TW.katalog              AS Katalog,
    TW.sww                  AS SwwBaza,
    SUM(ABS(MZ.ilosc))      AS SumaIlosc
FROM [Handel].[HM].[MG] MG
INNER JOIN [Handel].[HM].[MZ] MZ ON MZ.super = MG.id
INNER JOIN [Handel].[HM].[TW] TW ON TW.id = MZ.idtw
WHERE MG.data >= @DataOd
  AND MG.data <  DATEADD(DAY, 1, @DataDo)
  AND ISNULL(MG.anulowany, 0) = 0
  AND MG.typ_dk IN (N'PWU', N'PWP', N'PWK')
  AND TW.katalog IN (@KatSwieze, @KatInne, @KatMrozne)
GROUP BY TW.kod, TW.nazwa, TW.katalog, TW.sww;";

            return await PobierzIKlasyfikujAsync(sql, od, doD, czyProdukcja: true);
        }

        // Wspólny executor — wykonuje SQL, klasyfikuje każdy wiersz, sumuje per PKWiU
        private async Task<Dictionary<string, AgregatPkwiu>> PobierzIKlasyfikujAsync(
            string sql, DateTime od, DateTime doD, bool czyProdukcja)
        {
            var slownik = new Dictionary<string, AgregatPkwiu>(StringComparer.OrdinalIgnoreCase);

            using var conn = new SqlConnection(ConnHandel);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 180 };
            cmd.Parameters.Add("@DataOd", SqlDbType.Date).Value = od;
            cmd.Parameters.Add("@DataDo", SqlDbType.Date).Value = doD;
            cmd.Parameters.AddWithValue("@KatSwieze", PkwiuKlasyfikator.KatalogMiesoSwieze);
            cmd.Parameters.AddWithValue("@KatInne", PkwiuKlasyfikator.KatalogMiesoInne);
            cmd.Parameters.AddWithValue("@KatMrozne", PkwiuKlasyfikator.KatalogMiesoMrozne);

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                string kod = rdr["KodTowaru"]?.ToString() ?? "";
                string nazwa = rdr["NazwaTowaru"]?.ToString() ?? "";
                int? katalog = rdr.IsDBNull(rdr.GetOrdinal("Katalog")) ? null : Convert.ToInt32(rdr.GetValue(rdr.GetOrdinal("Katalog")));
                string sww = rdr["SwwBaza"]?.ToString() ?? "";
                decimal kg = rdr.IsDBNull(rdr.GetOrdinal("SumaIlosc"))
                    ? 0m
                    : Math.Abs(Convert.ToDecimal(rdr.GetValue(rdr.GetOrdinal("SumaIlosc"))));

                string? pkwiu = czyProdukcja
                    ? PkwiuKlasyfikator.KlasyfikujProdukcje(kod, nazwa, katalog, sww, null)
                    : PkwiuKlasyfikator.KlasyfikujSprzedaz(kod, nazwa, katalog, sww);

                if (pkwiu == null) continue;       // Kurczak B w sprzedaży / poza zakresem
                if (kg <= 0) continue;

                if (!slownik.TryGetValue(pkwiu, out var ag))
                {
                    ag = new AgregatPkwiu();
                    slownik[pkwiu] = ag;
                }
                ag.SumaKg += kg;
                if (!ag.Kody.Contains(kod)) ag.Kody.Add(kod);
            }
            return slownik;
        }

        // ════════════════════════════════════════════════════════════════
        // Konwersja kg → tony, zaokrąglone do int (GUS nie chce dziesiętnych)
        // ════════════════════════════════════════════════════════════════
        private static decimal KgDoTon(decimal kg) =>
            Math.Round(kg / 1000m, 0, MidpointRounding.AwayFromZero);

        // ════════════════════════════════════════════════════════════════
        // AUDYT — towary mięsne BEZ jakiejkolwiek klasyfikacji
        // (nie pasujące do żadnej z 4 kategorii P-02)
        // ════════════════════════════════════════════════════════════════
        public async Task<List<TowarBezKlasyfikacji>> PobierzTowaryBezKlasyfikacjiAsync(DateTime od, DateTime doD)
        {
            const string sql = @"
SELECT
    TW.kod, TW.nazwa, TW.katalog, TW.sww,
    SUM(ABS(DP.ilosc)) AS Kg
FROM [Handel].[HM].[DK] DK
INNER JOIN [Handel].[HM].[DP] DP ON DP.super = DK.id
INNER JOIN [Handel].[HM].[TW] TW ON TW.id = DP.idtw
WHERE DK.data >= @DataOd
  AND DK.data <  DATEADD(DAY, 1, @DataDo)
  AND ISNULL(DK.anulowany, 0) = 0
  AND DK.aktywny = 1
  AND DK.typ_dk IN (N'FVS', N'FKS')
  AND TW.katalog IN (@KatSwieze, @KatInne, @KatMrozne)
GROUP BY TW.kod, TW.nazwa, TW.katalog, TW.sww
HAVING SUM(ABS(DP.ilosc)) > 0;";

            var lista = new List<TowarBezKlasyfikacji>();
            using var conn = new SqlConnection(ConnHandel);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 120 };
            cmd.Parameters.Add("@DataOd", SqlDbType.Date).Value = od;
            cmd.Parameters.Add("@DataDo", SqlDbType.Date).Value = doD;
            cmd.Parameters.AddWithValue("@KatSwieze", PkwiuKlasyfikator.KatalogMiesoSwieze);
            cmd.Parameters.AddWithValue("@KatInne", PkwiuKlasyfikator.KatalogMiesoInne);
            cmd.Parameters.AddWithValue("@KatMrozne", PkwiuKlasyfikator.KatalogMiesoMrozne);

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                string kod = rdr["kod"]?.ToString() ?? "";
                string nazwa = rdr["nazwa"]?.ToString() ?? "";
                int? kat = rdr.IsDBNull(rdr.GetOrdinal("katalog")) ? null : Convert.ToInt32(rdr.GetValue(rdr.GetOrdinal("katalog")));
                string sww = rdr["sww"]?.ToString() ?? "";
                decimal kg = rdr.IsDBNull(rdr.GetOrdinal("Kg")) ? 0m : Convert.ToDecimal(rdr.GetValue(rdr.GetOrdinal("Kg")));
                if (PkwiuKlasyfikator.KlasyfikujSprzedaz(kod, nazwa, kat, sww) == null)
                {
                    lista.Add(new TowarBezKlasyfikacji(kod, nazwa, kat, sww, kg));
                }
            }
            return lista.OrderByDescending(x => x.Kg).ToList();
        }

        // ════════════════════════════════════════════════════════════════
        // DRILL-DOWN: lista FAKTUR SPRZEDAŻY (FVS+FKS) zaklasyfikowanych do danego PKWiU
        // za zadany okres. Używane gdy user dwuklika na komórkę "Sprz MC/YTD".
        // ════════════════════════════════════════════════════════════════
        public async Task<List<P02DokumentRow>> PobierzDokumentySprzedazyAsync(string pkwiuFilter, DateTime od, DateTime doD)
        {
            const string sql = @"
SELECT
    DK.data                 AS Data,
    DK.typ_dk               AS TypDk,
    DK.kod                  AS Numer,
    DK.khid                 AS Khid,
    ISNULL(KH.Shortcut, '') AS Kontrahent,
    DP.lp                   AS Lp,
    TW.kod                  AS KodTowaru,
    TW.nazwa                AS NazwaTowaru,
    TW.katalog              AS Katalog,
    TW.sww                  AS SwwBaza,
    DP.ilosc                AS Ilosc,
    DP.wartNetto            AS Wartosc
FROM [Handel].[HM].[DK] DK
INNER JOIN [Handel].[HM].[DP] DP ON DP.super = DK.id
INNER JOIN [Handel].[HM].[TW] TW ON TW.id = DP.idtw
LEFT  JOIN [Handel].[SSCommon].[STContractors] KH ON KH.Id = DK.khid
WHERE DK.data >= @DataOd
  AND DK.data <  DATEADD(DAY, 1, @DataDo)
  AND ISNULL(DK.anulowany, 0) = 0
  AND DK.aktywny = 1
  AND DK.typ_dk IN (N'FVS', N'FKS')
  AND TW.katalog IN (@KatSwieze, @KatInne, @KatMrozne)
ORDER BY DK.data DESC, DK.id DESC, DP.lp;";

            return await PobierzDokumentyAsync(sql, od, doD, pkwiuFilter, czyProdukcja: false);
        }

        // ════════════════════════════════════════════════════════════════
        // DRILL-DOWN: dokumenty PRODUKCJI (PWU+PWP+PWK) zaklasyfikowane do PKWiU
        // za zadany okres. Używane gdy user dwuklika na komórkę "Prod MC/YTD".
        // ════════════════════════════════════════════════════════════════
        public async Task<List<P02DokumentRow>> PobierzDokumentyProdukcjiAsync(string pkwiuFilter, DateTime od, DateTime doD)
        {
            const string sql = @"
SELECT
    MG.data                 AS Data,
    MG.typ_dk               AS TypDk,
    MG.kod                  AS Numer,
    NULL                    AS Khid,
    ''                      AS Kontrahent,
    MZ.lp                   AS Lp,
    TW.kod                  AS KodTowaru,
    TW.nazwa                AS NazwaTowaru,
    TW.katalog              AS Katalog,
    TW.sww                  AS SwwBaza,
    MZ.ilosc                AS Ilosc,
    CAST(0 AS DECIMAL(18,2)) AS Wartosc
FROM [Handel].[HM].[MG] MG
INNER JOIN [Handel].[HM].[MZ] MZ ON MZ.super = MG.id
INNER JOIN [Handel].[HM].[TW] TW ON TW.id = MZ.idtw
WHERE MG.data >= @DataOd
  AND MG.data <  DATEADD(DAY, 1, @DataDo)
  AND ISNULL(MG.anulowany, 0) = 0
  AND MG.typ_dk IN (N'PWU', N'PWP', N'PWK')
  AND TW.katalog IN (@KatSwieze, @KatInne, @KatMrozne)
ORDER BY MG.data DESC, MG.id DESC, MZ.lp;";

            return await PobierzDokumentyAsync(sql, od, doD, pkwiuFilter, czyProdukcja: true);
        }

        private async Task<List<P02DokumentRow>> PobierzDokumentyAsync(
            string sql, DateTime od, DateTime doD, string pkwiuFilter, bool czyProdukcja)
        {
            var wynik = new List<P02DokumentRow>();

            using var conn = new SqlConnection(ConnHandel);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 180 };
            cmd.Parameters.Add("@DataOd", SqlDbType.Date).Value = od;
            cmd.Parameters.Add("@DataDo", SqlDbType.Date).Value = doD;
            cmd.Parameters.AddWithValue("@KatSwieze", PkwiuKlasyfikator.KatalogMiesoSwieze);
            cmd.Parameters.AddWithValue("@KatInne", PkwiuKlasyfikator.KatalogMiesoInne);
            cmd.Parameters.AddWithValue("@KatMrozne", PkwiuKlasyfikator.KatalogMiesoMrozne);

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                string kod = rdr["KodTowaru"]?.ToString() ?? "";
                string nazwa = rdr["NazwaTowaru"]?.ToString() ?? "";
                int? katalog = rdr.IsDBNull(rdr.GetOrdinal("Katalog")) ? null : Convert.ToInt32(rdr.GetValue(rdr.GetOrdinal("Katalog")));
                string sww = rdr["SwwBaza"]?.ToString() ?? "";

                string? pkwiu = czyProdukcja
                    ? PkwiuKlasyfikator.KlasyfikujProdukcje(kod, nazwa, katalog, sww, null)
                    : PkwiuKlasyfikator.KlasyfikujSprzedaz(kod, nazwa, katalog, sww);

                if (pkwiu == null) continue;
                if (!string.Equals(pkwiu, pkwiuFilter, StringComparison.OrdinalIgnoreCase)) continue;

                decimal kg = rdr.IsDBNull(rdr.GetOrdinal("Ilosc"))
                    ? 0m : Math.Abs(Convert.ToDecimal(rdr.GetValue(rdr.GetOrdinal("Ilosc"))));
                if (kg <= 0) continue;

                decimal wartosc = rdr.IsDBNull(rdr.GetOrdinal("Wartosc"))
                    ? 0m : Math.Abs(Convert.ToDecimal(rdr.GetValue(rdr.GetOrdinal("Wartosc"))));

                wynik.Add(new P02DokumentRow
                {
                    Data = rdr.GetDateTime(rdr.GetOrdinal("Data")),
                    TypDokumentu = rdr["TypDk"]?.ToString() ?? "",
                    NumerDokumentu = rdr["Numer"]?.ToString() ?? "",
                    KontrahentId = rdr.IsDBNull(rdr.GetOrdinal("Khid")) ? null : Convert.ToInt32(rdr.GetValue(rdr.GetOrdinal("Khid"))),
                    Kontrahent = rdr["Kontrahent"]?.ToString(),
                    Lp = rdr.IsDBNull(rdr.GetOrdinal("Lp")) ? 0 : Convert.ToInt32(rdr.GetValue(rdr.GetOrdinal("Lp"))),
                    KodTowaru = kod,
                    NazwaTowaru = nazwa,
                    IloscKg = kg,
                    WartoscNetto = wartosc,
                    PkwiuKlasyfikacja = pkwiu
                });
            }
            return wynik;
        }

        private class AgregatPkwiu
        {
            public decimal SumaKg { get; set; }
            public List<string> Kody { get; } = new();
        }

        public record TowarBezKlasyfikacji(string Kod, string Nazwa, int? Katalog, string Sww, decimal Kg);
    }
}
