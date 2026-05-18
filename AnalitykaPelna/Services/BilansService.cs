using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kalendarz1.AnalitykaPelna.Models;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.AnalitykaPelna.Services
{
    /// <summary>
    /// Produkcja vs sprzedaż w okresie + 8-tygodniowa prognoza + anomalie 2σ per-towar.
    /// Wyciągnięte z AnalizaTygodniowaService — uzupełnione o per-towar wariancję.
    /// </summary>
    public class BilansService
    {
        private readonly string _connHandel;
        private readonly string _katalog;

        public BilansService()
        {
            AnalitykaConfig.ZaladujJesliTrzeba();
            _connHandel = AnalitykaConfig.ConnHandel;
            _katalog = AnalitykaConfig.KatalogMieso;
        }

        public BilansService(string connHandel, string katalogMieso)
        {
            _connHandel = connHandel;
            _katalog = katalogMieso;
        }

        public async Task<List<BilansSurowyRekord>> LoadAnalitykaAsync(FiltryAnaliz filtry)
        {
            var result = new List<BilansSurowyRekord>();

            string odbiorcaCond = filtry.OdbiorcyIds.Any()
                ? $" AND C.id IN ({string.Join(",", filtry.OdbiorcyIds.Select(i => i.ToString()))})"
                : "";

            string handlowiecCond = "";
            if (filtry.Handlowcy.Any())
            {
                var nazwy = filtry.Handlowcy.Select((_, i) => "@H" + i);
                handlowiecCond = " AND ISNULL(WYM.CDim_Handlowiec_Val, 'BRAK') IN (" + string.Join(",", nazwy) + ")";
            }

            string sql = $@"
                WITH SeriePrzychodowe AS (
                    SELECT 'sPWU' AS seria UNION ALL SELECT 'PWP' UNION ALL
                    SELECT 'PWX'  UNION ALL SELECT 'PRZY' UNION ALL SELECT 'PZ'
                ),
                Przychody AS (
                    SELECT
                        CAST(MZ.data AS DATE) AS Data,
                        MZ.idtw,
                        TW.kod AS KodTowaru,
                        TW.nazwa AS NazwaTowaru,
                        ABS(MZ.ilosc) AS ilosc,
                        MG.kod AS NumerDokumentu
                    FROM HANDEL.HM.MZ AS MZ
                    JOIN HANDEL.HM.MG AS MG ON MG.id = MZ.super
                    JOIN HANDEL.HM.TW AS TW ON TW.id = MZ.idtw
                    JOIN SeriePrzychodowe S ON S.seria = MG.seria
                    WHERE TW.katalog = @Katalog
                      AND MG.anulowany = 0
                      AND CAST(MZ.data AS DATE) BETWEEN @DataOd AND @DataDo
                      AND (@TowarID IS NULL OR MZ.idtw = @TowarID)
                ),
                Sprzedaz AS (
                    SELECT
                        CAST(DK.data AS DATE) AS Data,
                        DP.idtw,
                        TW.kod AS KodTowaru,
                        TW.nazwa AS NazwaTowaru,
                        DP.ilosc,
                        DP.cena,
                        C.shortcut AS NazwaKontrahenta,
                        ISNULL(WYM.CDim_Handlowiec_Val, 'BRAK') AS Handlowiec,
                        DK.kod AS NumerDokumentu
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                    INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.id
                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                    WHERE DK.anulowany = 0
                      AND TW.katalog = @Katalog
                      AND CAST(DK.data AS DATE) BETWEEN @DataOd AND @DataDo
                      AND (@TowarID IS NULL OR DP.idtw = @TowarID)
                      {odbiorcaCond}
                      {handlowiecCond}
                )
                SELECT 'PRODUKCJA' AS Typ, Data, KodTowaru, NazwaTowaru, ilosc,
                       0 AS cena, NULL AS NazwaKontrahenta, NULL AS Handlowiec, NumerDokumentu
                FROM Przychody
                UNION ALL
                SELECT 'SPRZEDAZ' AS Typ, Data, KodTowaru, NazwaTowaru, ilosc,
                       cena, NazwaKontrahenta, Handlowiec, NumerDokumentu
                FROM Sprzedaz;";

            using var conn = new SqlConnection(_connHandel);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Katalog", _katalog);
            cmd.Parameters.AddWithValue("@DataOd", filtry.DataOd.Date);
            cmd.Parameters.AddWithValue("@DataDo", filtry.DataDo.Date);
            cmd.Parameters.AddWithValue("@TowarID", (object?)filtry.TowarIdHandel ?? DBNull.Value);
            for (int i = 0; i < filtry.Handlowcy.Count; i++)
                cmd.Parameters.AddWithValue("@H" + i, filtry.Handlowcy[i] ?? "");

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new BilansSurowyRekord
                {
                    TypOperacji = SqlSafe.ReadString(reader, 0),
                    Data = SqlSafe.ReadDate(reader, 1),
                    KodTowaru = SqlSafe.ReadString(reader, 2),
                    NazwaTowaru = SqlSafe.ReadString(reader, 3),
                    Ilosc = Math.Abs(SqlSafe.ReadDecimal(reader, 4)),
                    Cena = SqlSafe.ReadDecimal(reader, 5),
                    NazwaKontrahenta = SqlSafe.ReadString(reader, 6),
                    Handlowiec = SqlSafe.ReadString(reader, 7),
                    NumerDokumentu = SqlSafe.ReadString(reader, 8)
                });
            }
            return result;
        }

        /// <summary>
        /// Buduje listę dni z bilansem produkcja vs sprzedaż.
        /// Wykrywa anomalie: |Δ - mean| > 2σ liczone PER TOWAR (nie globalnie — fix po audycie).
        /// </summary>
        public List<BilansDzien> BudujBilans(List<BilansSurowyRekord> surowe, FiltryAnaliz filtry,
            Dictionary<DateTime, decimal>? prognozaSprzedazy = null)
        {
            var dni = new List<BilansDzien>();
            var datyZakres = EnumDni(filtry.DataOd, filtry.DataDo).ToList();

            // Per-data agregaty
            foreach (var data in datyZakres)
            {
                var rekDnia = surowe.Where(r => r.Data.Date == data).ToList();
                var dzien = new BilansDzien
                {
                    Data = data,
                    Produkcja = rekDnia.Where(r => r.TypOperacji == "PRODUKCJA").Sum(r => r.Ilosc),
                    Sprzedaz = rekDnia.Where(r => r.TypOperacji == "SPRZEDAZ").Sum(r => r.Ilosc),
                    Wartosc = rekDnia.Where(r => r.TypOperacji == "SPRZEDAZ").Sum(r => r.Ilosc * r.Cena)
                };
                if (prognozaSprzedazy != null && prognozaSprzedazy.TryGetValue(data, out var p))
                    dzien.Prognoza = p;

                dzien.SzczegolySprzedazy = rekDnia
                    .Where(r => r.TypOperacji == "SPRZEDAZ")
                    .Select(r => new BilansSzczegolSprzedazy
                    {
                        NazwaKontrahenta = r.NazwaKontrahenta,
                        Handlowiec = r.Handlowiec,
                        KodTowaru = r.KodTowaru,
                        NazwaTowaru = r.NazwaTowaru,
                        Ilosc = r.Ilosc,
                        Cena = r.Cena,
                        NumerDokumentu = r.NumerDokumentu
                    }).ToList();

                dzien.SzczegolyProdukcji = rekDnia
                    .Where(r => r.TypOperacji == "PRODUKCJA")
                    .Select(r => new BilansSzczegolProdukcji
                    {
                        KodTowaru = r.KodTowaru,
                        NazwaTowaru = r.NazwaTowaru,
                        Ilosc = r.Ilosc,
                        NumerDokumentu = r.NumerDokumentu
                    }).ToList();

                dni.Add(dzien);
            }

            OznaczAnomaliePerTowar(surowe, dni);
            return dni;
        }

        // Detekcja anomalii: 2σ wariancji sprzedaży per towar.
        // Jeśli wariancja sprzedaży konkretnego towaru w danym dniu odstaje od średniej tego towaru
        // o więcej niż 2σ → cały dzień oznaczamy jako anomaliczny.
        private static void OznaczAnomaliePerTowar(List<BilansSurowyRekord> surowe, List<BilansDzien> dni)
        {
            var perTowar = surowe
                .Where(r => r.TypOperacji == "SPRZEDAZ")
                .GroupBy(r => r.KodTowaru);

            var anomalniDni = new HashSet<DateTime>();
            foreach (var grTowar in perTowar)
            {
                var sumyDni = grTowar
                    .GroupBy(r => r.Data.Date)
                    .Select(g => new { Data = g.Key, Suma = g.Sum(r => r.Ilosc) })
                    .ToList();
                if (sumyDni.Count < 3) continue;

                double mean = (double)sumyDni.Average(s => s.Suma);
                double sigma = Math.Sqrt(sumyDni.Average(s => Math.Pow((double)s.Suma - mean, 2)));
                if (sigma <= 0) continue;

                foreach (var s in sumyDni)
                    if (Math.Abs((double)s.Suma - mean) > 2 * sigma)
                        anomalniDni.Add(s.Data);
            }

            foreach (var d in dni)
                d.Anomalia = anomalniDni.Contains(d.Data.Date);
        }

        private static IEnumerable<DateTime> EnumDni(DateTime od, DateTime doData)
        {
            for (var d = od.Date; d.Date <= doData.Date; d = d.AddDays(1))
                yield return d;
        }

        public List<BilansRanking> BudujRankingOdbiorcow(List<BilansSurowyRekord> surowe, int topN)
        {
            return surowe.Where(r => r.TypOperacji == "SPRZEDAZ" && !string.IsNullOrEmpty(r.NazwaKontrahenta))
                .GroupBy(r => r.NazwaKontrahenta)
                .Select(g => new BilansRanking
                {
                    Klucz = g.Key,
                    Nazwa = g.Key,
                    Ilosc = g.Sum(r => r.Ilosc),
                    Wartosc = g.Sum(r => r.Ilosc * r.Cena)
                })
                .OrderByDescending(x => x.Ilosc)
                .Take(topN)
                .Select((x, i) => { x.Pozycja = i + 1; return x; })
                .ToList();
        }

        public List<BilansRanking> BudujRankingTowarow(List<BilansSurowyRekord> surowe, int topN)
        {
            return surowe.Where(r => r.TypOperacji == "SPRZEDAZ")
                .GroupBy(r => r.KodTowaru + "|" + r.NazwaTowaru)
                .Select(g =>
                {
                    var parts = g.Key.Split('|', 2);
                    return new BilansRanking
                    {
                        Klucz = parts[0],
                        Nazwa = parts.Length > 1 ? parts[1] : parts[0],
                        Ilosc = g.Sum(r => r.Ilosc),
                        Wartosc = g.Sum(r => r.Ilosc * r.Cena)
                    };
                })
                .OrderByDescending(x => x.Ilosc)
                .Take(topN)
                .Select((x, i) => { x.Pozycja = i + 1; return x; })
                .ToList();
        }

        public List<BilansRanking> BudujRankingHandlowcow(List<BilansSurowyRekord> surowe, int topN)
        {
            return surowe.Where(r => r.TypOperacji == "SPRZEDAZ" && !string.IsNullOrEmpty(r.Handlowiec))
                .GroupBy(r => r.Handlowiec)
                .Select(g => new BilansRanking
                {
                    Klucz = g.Key,
                    Nazwa = g.Key,
                    Ilosc = g.Sum(r => r.Ilosc),
                    Wartosc = g.Sum(r => r.Ilosc * r.Cena)
                })
                .OrderByDescending(x => x.Wartosc)
                .Take(topN)
                .Select((x, i) => { x.Pozycja = i + 1; return x; })
                .ToList();
        }

        /// <summary>
        /// 8-tygodniowa prognoza dziennej sprzedaży uwzględniająca filtr towaru.
        /// Zwraca słownik (Data → kg) dla zakresu DataOd..DataDo.
        /// </summary>
        public async Task<Dictionary<DateTime, decimal>> LoadPrognozaSprzedazyAsync(FiltryAnaliz filtry)
        {
            var rezultat = new Dictionary<DateTime, decimal>();
            DateTime probaOd = filtry.DataOd.AddDays(-56);
            DateTime probaDo = filtry.DataOd.AddDays(-1);

            const string sql = @"
                SELECT CAST(DK.data AS DATE) AS Data, ABS(SUM(DP.ilosc)) AS Suma
                FROM [HANDEL].[HM].[DK] DK
                INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.id
                WHERE DK.anulowany = 0
                  AND TW.katalog = @Katalog
                  AND (@TowarID IS NULL OR DP.idtw = @TowarID)
                  AND CAST(DK.data AS DATE) BETWEEN @ProbaOd AND @ProbaDo
                GROUP BY CAST(DK.data AS DATE);";

            var probki = new List<(DateTime data, decimal kg)>();

            using (var conn = new SqlConnection(_connHandel))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Katalog", _katalog);
                cmd.Parameters.AddWithValue("@TowarID", (object?)filtry.TowarIdHandel ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ProbaOd", probaOd.Date);
                cmd.Parameters.AddWithValue("@ProbaDo", probaDo.Date);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    probki.Add((SqlSafe.ReadDate(reader, 0), SqlSafe.ReadDecimal(reader, 1)));
            }

            // Średnia per DayOfWeek
            var sredniePerDow = probki
                .GroupBy(p => p.data.DayOfWeek)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => x.kg) / Math.Max(1, g.Select(x => GetWeekIso(x.data)).Distinct().Count()));

            for (var d = filtry.DataOd.Date; d.Date <= filtry.DataDo.Date; d = d.AddDays(1))
                rezultat[d] = sredniePerDow.TryGetValue(d.DayOfWeek, out var v) ? v : 0m;

            return rezultat;
        }

        private static int GetWeekIso(DateTime d)
            => System.Globalization.ISOWeek.GetWeekOfYear(d);

        public async Task<int?> GetIdDokumentuAsync(string numer)
        {
            const string sql = "SELECT id FROM [HANDEL].[HM].[DK] WHERE kod = @N";
            using var conn = new SqlConnection(_connHandel);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@N", numer ?? "");
            var v = await cmd.ExecuteScalarAsync();
            return v == null || v == DBNull.Value ? null : Convert.ToInt32(v);
        }
    }
}
