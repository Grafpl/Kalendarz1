using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Sprawozdania.Services
{
    // ════════════════════════════════════════════════════════════════════
    // P-02 — Zapasy wyrobów na koniec okresu sprawozdawczego
    //
    // ŹRÓDŁO: HM.SM (Stany Magazynowe) — TABELA SNAPSHOT Sage Symfonia.
    // Odkryte przez SQL Profiler 2026-05-21 (trace Untitled - 2.trc):
    //   Sage UI używa zapytania: SELECT TW.kod, ISNULL(s.stan,0) + ISNULL(a.ilosc,0)
    //   FROM TW JOIN SM s ... LEFT JOIN MZ a (zmiany od daty)
    //
    // Tabela SM trzyma BIEŻĄCY stan per (idtw, magazyn). Sage aktualizuje SM
    // na bieżąco — to JEDYNE wiarygodne źródło stanu (sumowanie MZ kumulatywnie
    // daje fałszywe wartości bo Sage robi okresowe rebalances).
    //
    // Stan na dziś: SM.stan
    // Stan historyczny (np. koniec poprzedniego miesiąca):
    //   SM.stan - SUM(MZ.ilosc gdzie data > end_okresu AND data <= dziś)
    //   gdzie MZ.bufor=0 AND (MZ.typ & 0x01)=0
    // Uwaga: dla okresów > 1 miesiąca przeliczanie może być nieprecyzyjne.
    // ════════════════════════════════════════════════════════════════════
    public class P02ZapasService
    {
        private const string ConnHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        // Magazyny (po analizie SM dla mięs 2026-05-21):
        //   65552 = M.MROZN    ← 116 t (mrożone — TU JEST WIĘKSZOŚĆ)
        //   65556 = M.DYST     ← 21 t (świeże + trochę mrożonych gotowych do wysyłki)
        //   65554 = M.PROD     ← 0.1 t
        //   65562 = M.MASAR    ← 1.4 t
        //   65555 = M.UBOJ     ← 0 t (krótki postój po uboju)
        //
        // STRATEGIA P-02 (z Sergiuszem 2026-05-21):
        //   • Świeże (10.12.10-x) → M.DYST (gdzie czekają na wysyłkę)
        //   • Mrożone (10.12.20-x) → M.MROZN (mroźnia trzyma większość)
        //   • Klasyfikator decyduje PER TOWAR (kod, nazwa, katalog)
        public enum MetodaLiczenia
        {
            // Metoda A — REKOMENDOWANA. SM dla M.MROZN + M.DYST.
            // Mrożone z mroźni, świeże z dystrybucji. Sage UI używa tej tabeli.
            SM_MroznDyst,

            // Metoda B — SM dla wszystkich magazynów mięsnych.
            SM_Wszystkie,

            // Metoda C — SM tylko M.DYST.
            SM_TylkoDyst,

            // Metoda D — SM tylko M.MROZN.
            SM_TylkoMrozn
        }

        // ════════════════════════════════════════════════════════════════
        // Główna metoda — zwraca zapasy per PKWiU za koniec miesiąca
        // ════════════════════════════════════════════════════════════════
        public async Task<Dictionary<string, decimal>> PobierzZapasyNaKoniecOkresuAsync(
            int rok, int miesiac, MetodaLiczenia metoda = MetodaLiczenia.SM_MroznDyst)
        {
            DateTime ostatniDzien = new DateTime(rok, miesiac, 1).AddMonths(1).AddDays(-1);
            DateTime dzis = DateTime.Today;

            string filtrMag = MagazynyFiltrSM(metoda);

            // Sage logic (z trace .trc):
            //   stan_historyczny = SM.stan - (zmiany w MZ od koniec_okresu+1 do dziś)
            //   gdzie MZ.bufor=0 AND (MZ.typ & 0x01)=0
            // Dla ostatniDzien >= dzis — używamy tylko SM (stan bieżący).
            bool histRequired = ostatniDzien < dzis;

            string sql = histRequired ? $@"
WITH PoZmianyOdEnd AS (
    SELECT MZ.idtw, MZ.magazyn, SUM(MZ.ilosc) AS Zmiana
    FROM [Handel].[HM].[MZ] MZ
    WHERE MZ.data > @KoniecMc
      AND MZ.data <= @Dzis
      AND ISNULL(MZ.bufor, 0) = 0
      AND (ISNULL(MZ.typ, 0) & 0x01) = 0
    GROUP BY MZ.idtw, MZ.magazyn
)
SELECT
    TW.kod, TW.nazwa, TW.katalog, TW.sww,
    SUM(SM.stan + ISNULL(P.Zmiana, 0)) AS StanKg
FROM [Handel].[HM].[SM] SM
INNER JOIN [Handel].[HM].[TW] TW ON TW.id = SM.idtw
LEFT JOIN PoZmianyOdEnd P ON P.idtw = SM.idtw AND P.magazyn = SM.Magazyn
WHERE 1=1 {filtrMag}
  AND TW.katalog IN (67095, 67104, 67153)
GROUP BY TW.kod, TW.nazwa, TW.katalog, TW.sww
HAVING SUM(SM.stan + ISNULL(P.Zmiana, 0)) > 0;"
            : $@"
SELECT
    TW.kod, TW.nazwa, TW.katalog, TW.sww,
    SUM(SM.stan) AS StanKg
FROM [Handel].[HM].[SM] SM
INNER JOIN [Handel].[HM].[TW] TW ON TW.id = SM.idtw
WHERE 1=1 {filtrMag}
  AND TW.katalog IN (67095, 67104, 67153)
GROUP BY TW.kod, TW.nazwa, TW.katalog, TW.sww
HAVING SUM(SM.stan) > 0;";

            var perPkwiu = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in PkwiuKlasyfikator.PkwiuKolejnosc) perPkwiu[p] = 0m;

            using var conn = new SqlConnection(ConnHandel);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 300 };
            cmd.Parameters.Add("@KoniecMc", SqlDbType.Date).Value = ostatniDzien;
            if (histRequired) cmd.Parameters.Add("@Dzis", SqlDbType.Date).Value = dzis;
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                string kod = rdr["kod"]?.ToString() ?? "";
                string nazwa = rdr["nazwa"]?.ToString() ?? "";
                int? kat = rdr.IsDBNull(rdr.GetOrdinal("katalog")) ? null : Convert.ToInt32(rdr.GetValue(rdr.GetOrdinal("katalog")));
                string sww = rdr["sww"]?.ToString() ?? "";
                decimal kg = rdr.IsDBNull(rdr.GetOrdinal("StanKg")) ? 0m : Convert.ToDecimal(rdr.GetValue(rdr.GetOrdinal("StanKg")));
                if (kg <= 0) continue;

                // Klasyfikator produkcji (zapasy to produkty wyprodukowane, nie sprzedane)
                string? pkwiu = PkwiuKlasyfikator.KlasyfikujProdukcje(kod, nazwa, kat, sww, null);
                if (pkwiu == null) continue;

                if (!perPkwiu.ContainsKey(pkwiu)) perPkwiu[pkwiu] = 0m;
                perPkwiu[pkwiu] += kg;
            }

            // Konwersja kg → tony (zaokrąglone)
            var wynik = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in perPkwiu)
                wynik[kv.Key] = Math.Round(kv.Value / 1000m, 0, MidpointRounding.AwayFromZero);
            return wynik;
        }

        // ════════════════════════════════════════════════════════════════
        // Audyt — pełna lista kodów towarów ze stanem (do drill-down)
        // ════════════════════════════════════════════════════════════════
        public async Task<List<ZapasyKodRow>> PobierzZapasyPerKodAsync(
            int rok, int miesiac, MetodaLiczenia metoda = MetodaLiczenia.SM_MroznDyst)
        {
            DateTime ostatni = new DateTime(rok, miesiac, 1).AddMonths(1).AddDays(-1);
            DateTime dzis = DateTime.Today;
            bool histRequired = ostatni < dzis;
            string filtrMag = MagazynyFiltrSM(metoda);

            string sql = histRequired ? $@"
WITH P AS (
    SELECT MZ.idtw, MZ.magazyn, SUM(MZ.ilosc) AS Zmiana
    FROM [Handel].[HM].[MZ] MZ
    WHERE MZ.data > @KoniecMc AND MZ.data <= @Dzis
      AND ISNULL(MZ.bufor, 0) = 0 AND (ISNULL(MZ.typ, 0) & 0x01) = 0
    GROUP BY MZ.idtw, MZ.magazyn
)
SELECT TW.kod, TW.nazwa, TW.katalog,
       SUM(SM.stan + ISNULL(P.Zmiana, 0)) AS StanKg
FROM [Handel].[HM].[SM] SM
INNER JOIN [Handel].[HM].[TW] TW ON TW.id = SM.idtw
LEFT JOIN P ON P.idtw = SM.idtw AND P.magazyn = SM.Magazyn
WHERE 1=1 {filtrMag}
  AND TW.katalog IN (67095, 67104, 67153)
GROUP BY TW.kod, TW.nazwa, TW.katalog
HAVING SUM(SM.stan + ISNULL(P.Zmiana, 0)) > 0
ORDER BY SUM(SM.stan + ISNULL(P.Zmiana, 0)) DESC;"
            : $@"
SELECT TW.kod, TW.nazwa, TW.katalog,
       SUM(SM.stan) AS StanKg
FROM [Handel].[HM].[SM] SM
INNER JOIN [Handel].[HM].[TW] TW ON TW.id = SM.idtw
WHERE 1=1 {filtrMag}
  AND TW.katalog IN (67095, 67104, 67153)
GROUP BY TW.kod, TW.nazwa, TW.katalog
HAVING SUM(SM.stan) > 0
ORDER BY SUM(SM.stan) DESC;";

            var lista = new List<ZapasyKodRow>();
            using var conn = new SqlConnection(ConnHandel);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 300 };
            cmd.Parameters.Add("@KoniecMc", SqlDbType.Date).Value = ostatni;
            if (histRequired) cmd.Parameters.Add("@Dzis", SqlDbType.Date).Value = dzis;
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                lista.Add(new ZapasyKodRow(
                    rdr["kod"]?.ToString() ?? "",
                    rdr["nazwa"]?.ToString() ?? "",
                    rdr.IsDBNull(rdr.GetOrdinal("katalog")) ? null : Convert.ToInt32(rdr.GetValue(rdr.GetOrdinal("katalog"))),
                    rdr.IsDBNull(rdr.GetOrdinal("StanKg")) ? 0m : Convert.ToDecimal(rdr.GetValue(rdr.GetOrdinal("StanKg")))
                ));
            }
            return lista;
        }

        // ════════════════════════════════════════════════════════════════
        // Porównanie 3 metod (test dialog) — sumy per metoda per PKWiU
        // ════════════════════════════════════════════════════════════════
        public async Task<Dictionary<MetodaLiczenia, Dictionary<string, decimal>>> PorownajWszystkieMetodyAsync(int rok, int miesiac)
        {
            var tA = PobierzZapasyNaKoniecOkresuAsync(rok, miesiac, MetodaLiczenia.SM_MroznDyst);
            var tB = PobierzZapasyNaKoniecOkresuAsync(rok, miesiac, MetodaLiczenia.SM_Wszystkie);
            var tC = PobierzZapasyNaKoniecOkresuAsync(rok, miesiac, MetodaLiczenia.SM_TylkoDyst);
            var tD = PobierzZapasyNaKoniecOkresuAsync(rok, miesiac, MetodaLiczenia.SM_TylkoMrozn);
            await Task.WhenAll(tA, tB, tC, tD);
            return new Dictionary<MetodaLiczenia, Dictionary<string, decimal>>
            {
                [MetodaLiczenia.SM_MroznDyst] = tA.Result,
                [MetodaLiczenia.SM_Wszystkie] = tB.Result,
                [MetodaLiczenia.SM_TylkoDyst] = tC.Result,
                [MetodaLiczenia.SM_TylkoMrozn] = tD.Result
            };
        }

        // ════════════════════════════════════════════════════════════════
        // Helper — filtr WHERE dla SM.Magazyn na podstawie wybranej metody
        // ════════════════════════════════════════════════════════════════
        private static string MagazynyFiltrSM(MetodaLiczenia m) => m switch
        {
            MetodaLiczenia.SM_MroznDyst => " AND SM.Magazyn IN (65552, 65556) ",
            MetodaLiczenia.SM_TylkoDyst => " AND SM.Magazyn = 65556 ",
            MetodaLiczenia.SM_TylkoMrozn => " AND SM.Magazyn = 65552 ",
            MetodaLiczenia.SM_Wszystkie => " AND SM.Magazyn IN (65555, 65554, 65552, 65556, 65562) ",
            _ => " AND SM.Magazyn IN (65552, 65556) "
        };

        public record ZapasyKodRow(string Kod, string Nazwa, int? Katalog, decimal Kg);

        // ════════════════════════════════════════════════════════════════
        // PEŁNE ROZBICIE per (towar, magazyn) — do drill-down w dialogu
        // Zwraca: kod, nazwa, katalog, magazyn, stan_dzis (SM), zmiana_po_okresie, stan_historyczny
        // ════════════════════════════════════════════════════════════════
        public async Task<List<ZapasyRichRow>> PobierzPelneZapasyAsync(
            int rok, int miesiac, MetodaLiczenia metoda = MetodaLiczenia.SM_MroznDyst)
        {
            DateTime ostatni = new DateTime(rok, miesiac, 1).AddMonths(1).AddDays(-1);
            DateTime dzis = DateTime.Today;
            bool histRequired = ostatni < dzis;
            string filtrMag = MagazynyFiltrSM(metoda);

            string sql = histRequired ? $@"
WITH P AS (
    SELECT MZ.idtw, MZ.magazyn, SUM(MZ.ilosc) AS Zmiana
    FROM [Handel].[HM].[MZ] MZ
    WHERE MZ.data > @KoniecMc AND MZ.data <= @Dzis
      AND ISNULL(MZ.bufor, 0) = 0 AND (ISNULL(MZ.typ, 0) & 0x01) = 0
    GROUP BY MZ.idtw, MZ.magazyn
)
SELECT TW.kod, TW.nazwa, TW.katalog, SM.Magazyn,
       SM.stan AS StanDzis,
       ISNULL(P.Zmiana, 0) AS Zmiana,
       SM.stan + ISNULL(P.Zmiana, 0) AS StanHist
FROM [Handel].[HM].[SM] SM
INNER JOIN [Handel].[HM].[TW] TW ON TW.id = SM.idtw
LEFT JOIN P ON P.idtw = SM.idtw AND P.magazyn = SM.Magazyn
WHERE 1=1 {filtrMag}
  AND TW.katalog IN (67095, 67104, 67153)
ORDER BY (SM.stan + ISNULL(P.Zmiana, 0)) DESC;"
            : $@"
SELECT TW.kod, TW.nazwa, TW.katalog, SM.Magazyn,
       SM.stan AS StanDzis,
       CAST(0 AS FLOAT) AS Zmiana,
       SM.stan AS StanHist
FROM [Handel].[HM].[SM] SM
INNER JOIN [Handel].[HM].[TW] TW ON TW.id = SM.idtw
WHERE 1=1 {filtrMag}
  AND TW.katalog IN (67095, 67104, 67153)
ORDER BY SM.stan DESC;";

            var lista = new List<ZapasyRichRow>();
            using var conn = new SqlConnection(ConnHandel);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 300 };
            cmd.Parameters.Add("@KoniecMc", SqlDbType.Date).Value = ostatni;
            if (histRequired) cmd.Parameters.Add("@Dzis", SqlDbType.Date).Value = dzis;

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                string kod = rdr["kod"]?.ToString() ?? "";
                string nazwa = rdr["nazwa"]?.ToString() ?? "";
                int? kat = rdr.IsDBNull(rdr.GetOrdinal("katalog")) ? null : Convert.ToInt32(rdr.GetValue(rdr.GetOrdinal("katalog")));
                int magazyn = rdr.GetInt32(rdr.GetOrdinal("Magazyn"));
                decimal sd = rdr.IsDBNull(rdr.GetOrdinal("StanDzis")) ? 0m : Convert.ToDecimal(rdr.GetValue(rdr.GetOrdinal("StanDzis")));
                decimal zm = rdr.IsDBNull(rdr.GetOrdinal("Zmiana")) ? 0m : Convert.ToDecimal(rdr.GetValue(rdr.GetOrdinal("Zmiana")));
                decimal sh = rdr.IsDBNull(rdr.GetOrdinal("StanHist")) ? 0m : Convert.ToDecimal(rdr.GetValue(rdr.GetOrdinal("StanHist")));

                string? pkwiu = PkwiuKlasyfikator.KlasyfikujProdukcje(kod, nazwa, kat, null, null);
                lista.Add(new ZapasyRichRow(kod, nazwa, kat, magazyn, sd, zm, sh, pkwiu ?? "(brak)"));
            }
            return lista;
        }

        public record ZapasyRichRow(
            string Kod, string Nazwa, int? Katalog, int Magazyn,
            decimal StanDzis, decimal ZmianaPoOkresie, decimal StanHistoryczny,
            string PkwiuKlasyfikacja);
    }
}
