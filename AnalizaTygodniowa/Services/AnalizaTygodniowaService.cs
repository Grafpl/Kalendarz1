using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Kalendarz1.AnalizaTygodniowa.Models;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.AnalizaTygodniowa.Services
{
    public class AnalizaTygodniowaService
    {
        private const string KATALOG_SWIEZE = "67095";
        private readonly string _conn;

        public AnalizaTygodniowaService(string connectionString)
        {
            _conn = connectionString;
        }

        public async Task<List<TowarComboItem>> LoadTowaryAsync()
        {
            var result = new List<TowarComboItem>
            {
                new() { Id = 0, Kod = "— Wszystkie towary —" }
            };

            const string sql = @"
                SELECT DISTINCT TW.id, TW.kod, TW.nazwa
                FROM [HANDEL].[HM].[TW] TW
                INNER JOIN [HANDEL].[HM].[DP] DP ON TW.id = DP.idtw
                WHERE TW.katalog = @Katalog
                ORDER BY TW.kod";

            using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Katalog", KATALOG_SWIEZE);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new TowarComboItem
                {
                    Id = SafeInt(reader, 0),
                    Kod = SafeString(reader, 1),
                    Nazwa = SafeString(reader, 2)
                });
            }
            return result;
        }

        public async Task<List<SuroweDaneSQl>> LoadAnalitykaAsync(AnalizaTygodniowaFilter filter)
        {
            var result = new List<SuroweDaneSQl>();

            // Filtry handlowiec / odbiorca aplikujemy po stronie SQL.
            // Listy są walidowane (handlowcy: parametry; odbiorcy: tylko int → bezpieczne dla inline).
            string odbiorcaCondition = filter.OdbiorcyIds.Any()
                ? $" AND C.id IN ({string.Join(",", filter.OdbiorcyIds.Select(i => i.ToString()))})"
                : string.Empty;

            string handlowiecCondition = "";
            if (filter.Handlowcy.Any())
            {
                var parmNames = filter.Handlowcy.Select((_, i) => "@H" + i);
                handlowiecCondition = " AND ISNULL(WYM.CDim_Handlowiec_Val, 'BRAK') IN (" + string.Join(",", parmNames) + ")";
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
                        C.id AS KontrahentId,
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
                      {odbiorcaCondition}
                      {handlowiecCondition}
                )
                SELECT 'PRODUKCJA' AS TypOperacji, Data, idtw, KodTowaru, NazwaTowaru, ilosc,
                       0 AS cena, NULL AS NazwaKontrahenta, NULL AS Handlowiec, NumerDokumentu
                FROM Przychody
                UNION ALL
                SELECT 'SPRZEDAZ' AS TypOperacji, Data, idtw, KodTowaru, NazwaTowaru, ilosc,
                       cena, NazwaKontrahenta, Handlowiec, NumerDokumentu
                FROM Sprzedaz;";

            using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Katalog", KATALOG_SWIEZE);
            cmd.Parameters.AddWithValue("@DataOd", filter.DataOd.Date);
            cmd.Parameters.AddWithValue("@DataDo", filter.DataDo.Date);
            cmd.Parameters.AddWithValue("@TowarID", (object)filter.TowarId ?? DBNull.Value);
            for (int i = 0; i < filter.Handlowcy.Count; i++)
                cmd.Parameters.AddWithValue("@H" + i, filter.Handlowcy[i] ?? "");

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new SuroweDaneSQl
                {
                    TypOperacji = SafeString(reader, "TypOperacji"),
                    Data = SafeDate(reader, "Data"),
                    KodTowaru = SafeString(reader, "KodTowaru"),
                    NazwaTowaru = SafeString(reader, "NazwaTowaru"),
                    Ilosc = SafeDecimal(reader, "ilosc"),
                    Cena = SafeDecimal(reader, "cena"),
                    NazwaKontrahenta = SafeString(reader, "NazwaKontrahenta"),
                    Handlowiec = SafeString(reader, "Handlowiec"),
                    NumerDokumentu = SafeString(reader, "NumerDokumentu")
                });
            }
            return result;
        }

        public async Task<Dictionary<DayOfWeek, decimal>> LoadPrognozaAsync(DateTime dataStartOkresu, int? towarId)
        {
            // 8-tygodniowe średnie sprzedaży per dzień tygodnia
            const string sql = @"
                SELECT CAST(DK.data AS DATE) AS Data, DP.ilosc
                FROM [HANDEL].[HM].[DK] DK
                INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.id
                WHERE DK.anulowany = 0
                  AND TW.katalog = @Katalog
                  AND (@TowarID IS NULL OR DP.idtw = @TowarID)
                  AND CAST(DK.data AS DATE) BETWEEN @PrognozaDataOd AND @PrognozaDataDo";

            var rekordy = new List<(DateTime data, decimal ilosc)>();

            using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Katalog", KATALOG_SWIEZE);
            cmd.Parameters.AddWithValue("@TowarID", (object)towarId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PrognozaDataOd", dataStartOkresu.AddDays(-56));
            cmd.Parameters.AddWithValue("@PrognozaDataDo", dataStartOkresu.AddDays(-1));

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rekordy.Add((SafeDate(reader, 0), SafeDecimal(reader, 1)));
            }

            var result = new Dictionary<DayOfWeek, decimal>();
            foreach (DayOfWeek dow in Enum.GetValues(typeof(DayOfWeek)))
            {
                var rekDlaDnia = rekordy.Where(r => r.data.DayOfWeek == dow).ToList();
                if (rekDlaDnia.Count == 0) { result[dow] = 0; continue; }
                int tygodnie = rekDlaDnia
                    .Select(r => System.Globalization.CultureInfo.CurrentCulture.Calendar
                        .GetWeekOfYear(r.data, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday))
                    .Distinct().Count();
                result[dow] = tygodnie == 0 ? 0 : rekDlaDnia.Sum(r => -r.ilosc) / tygodnie;
            }
            return result;
        }

        public async Task<int?> GetIdDokumentuAsync(string numerDokumentu)
        {
            const string sql = "SELECT id FROM [HANDEL].[HM].[DK] WHERE kod = @NumerDokumentu";
            using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@NumerDokumentu", numerDokumentu ?? "");
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? null : Convert.ToInt32(result);
        }

        // ===== DBNull-safe readers =====
        private static string SafeString(IDataRecord r, int idx)
            => r.IsDBNull(idx) ? "" : r.GetValue(idx)?.ToString() ?? "";
        private static string SafeString(IDataRecord r, string col)
        {
            int idx = r.GetOrdinal(col);
            return r.IsDBNull(idx) ? "" : r.GetValue(idx)?.ToString() ?? "";
        }
        private static int SafeInt(IDataRecord r, int idx)
            => r.IsDBNull(idx) ? 0 : Convert.ToInt32(r.GetValue(idx));
        private static decimal SafeDecimal(IDataRecord r, int idx)
            => r.IsDBNull(idx) ? 0m : Convert.ToDecimal(r.GetValue(idx));
        private static decimal SafeDecimal(IDataRecord r, string col)
        {
            int idx = r.GetOrdinal(col);
            return r.IsDBNull(idx) ? 0m : Convert.ToDecimal(r.GetValue(idx));
        }
        private static DateTime SafeDate(IDataRecord r, int idx)
            => r.IsDBNull(idx) ? DateTime.MinValue : Convert.ToDateTime(r.GetValue(idx));
        private static DateTime SafeDate(IDataRecord r, string col)
        {
            int idx = r.GetOrdinal(col);
            return r.IsDBNull(idx) ? DateTime.MinValue : Convert.ToDateTime(r.GetValue(idx));
        }
    }
}
