using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.DashboardPrzychodu.Models;

namespace Kalendarz1.DashboardPrzychodu.Services
{
    /// <summary>
    /// Serwis pobierania danych przychodu żywca z bazy LibraNet
    /// </summary>
    public class PrzychodService
    {
        private readonly string _connectionString;
        private DateTime _lastFetch = DateTime.MinValue;
        private readonly TimeSpan _minRefreshInterval = TimeSpan.FromSeconds(5);

        // Przechowuje ostatni błąd diagnostyczny
        public string LastDiagnosticError { get; private set; }

        public PrzychodService(string connectionString = null)
        {
            _connectionString = connectionString ??
                "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True;Pooling=true;Min Pool Size=2;Max Pool Size=10;";
        }

        /// <summary>
        /// Sprawdza czy można wykonać odświeżenie (minimalny interwał 5 sekund)
        /// </summary>
        public bool CanRefresh => DateTime.Now - _lastFetch >= _minRefreshInterval;

        /// <summary>
        /// DIAGNOSTYKA - testuje każdą kolumnę osobno żeby znaleźć problematyczną
        /// </summary>
        public async Task<string> DiagnoseQueryAsync(DateTime data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== DIAGNOSTYKA ZAPYTANIA ===");
            sb.AppendLine($"Data: {data:yyyy-MM-dd}");
            sb.AppendLine();

            // Lista kolumn do przetestowania
            var columns = new[]
            {
                ("fc.ID", "ID"),
                ("fc.CarLp", "NrKursu"),
                ("fc.CalcDate", "Data"),
                ("d.Name", "Hodowca"),
                ("d.ShortName", "HodowcaSkrot"),
                ("fc.DeclI1", "DeclI1"),
                ("fc.NettoFarmWeight", "NettoFarmWeight"),
                ("fc.WagaDek", "WagaDek"),
                ("fc.FullWeight", "FullWeight"),
                ("fc.EmptyWeight", "EmptyWeight"),
                ("fc.NettoWeight", "NettoWeight"),
                ("fc.LumQnt", "LumQnt"),
                ("fc.DeclI2", "DeclI2"),
                ("fc.DeclI3", "DeclI3"),
                ("fc.DeclI4", "DeclI4"),
                ("fc.DeclI5", "DeclI5"),
                ("fc.Przyjazd", "Przyjazd"),
                ("fc.SlaughterWeightDate", "SlaughterWeightDate"),
                ("fc.SlaughterWeightUser", "SlaughterWeightUser"),
            };

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    sb.AppendLine("[OK] Połączenie z bazą: SUKCES");
                    sb.AppendLine();

                    // Test 1: Sprawdź czy tabela FarmerCalc istnieje i ma dane
                    sb.AppendLine("--- TEST 1: Liczba rekordów w FarmerCalc ---");
                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM dbo.FarmerCalc WHERE CalcDate = @Data AND ISNULL(Deleted, 0) = 0", conn))
                    {
                        cmd.Parameters.AddWithValue("@Data", data.Date);
                        var count = (int)await cmd.ExecuteScalarAsync();
                        sb.AppendLine($"[OK] Znaleziono {count} rekordów na datę {data:yyyy-MM-dd}");
                    }
                    sb.AppendLine();

                    // Test 2: Sprawdź każdą kolumnę osobno
                    sb.AppendLine("--- TEST 2: Testowanie kolumn ---");
                    foreach (var (colExpr, colName) in columns)
                    {
                        try
                        {
                            string testQuery = $@"
                                SELECT TOP 1 {colExpr} AS TestCol
                                FROM dbo.FarmerCalc fc
                                LEFT JOIN dbo.Dostawcy d ON LTRIM(RTRIM(d.ID)) = LTRIM(RTRIM(fc.CustomerGID))
                                WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted, 0) = 0";

                            using (var cmd = new SqlCommand(testQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@Data", data.Date);
                                var result = await cmd.ExecuteScalarAsync();
                                string valueStr = result == null || result == DBNull.Value ? "NULL" : result.ToString();
                                if (valueStr.Length > 50) valueStr = valueStr.Substring(0, 50) + "...";
                                sb.AppendLine($"[OK] {colName,-25} = {valueStr}");
                            }
                        }
                        catch (Exception ex)
                        {
                            sb.AppendLine($"[BŁĄD] {colName,-25} = {ex.Message}");
                        }
                    }
                    sb.AppendLine();

                    // Test 3: Testuj obliczenia CASE
                    sb.AppendLine("--- TEST 3: Testowanie obliczeń CASE ---");

                    var caseExpressions = new[]
                    {
                        ("SredniaWagaPlan", @"CASE WHEN ISNULL(fc.DeclI1, 0) > 0 AND COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) > 0
                            THEN CAST(COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) / NULLIF(fc.DeclI1, 0) AS DECIMAL(10,3)) ELSE NULL END"),

                        ("SredniaWagaRzecz", @"CASE WHEN ISNULL(fc.LumQnt, 0) > 0 AND ISNULL(fc.NettoWeight, 0) > 0
                            THEN CAST(fc.NettoWeight / NULLIF(fc.LumQnt, 0) AS DECIMAL(10,3)) ELSE NULL END"),

                        ("OdchylenieKg", @"CASE WHEN ISNULL(fc.NettoWeight, 0) > 0 AND COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) > 0
                            THEN fc.NettoWeight - COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) ELSE NULL END"),

                        ("OdchylenieProc", @"CASE WHEN ISNULL(fc.NettoWeight, 0) > 0 AND COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) > 0
                            THEN CAST((fc.NettoWeight - COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0)) / NULLIF(COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0), 0) * 100 AS DECIMAL(10,2)) ELSE NULL END"),
                    };

                    foreach (var (name, expr) in caseExpressions)
                    {
                        try
                        {
                            string testQuery = $@"
                                SELECT TOP 1 {expr} AS TestCol
                                FROM dbo.FarmerCalc fc
                                LEFT JOIN dbo.Dostawcy d ON LTRIM(RTRIM(d.ID)) = LTRIM(RTRIM(fc.CustomerGID))
                                WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted, 0) = 0";

                            using (var cmd = new SqlCommand(testQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@Data", data.Date);
                                var result = await cmd.ExecuteScalarAsync();
                                string valueStr = result == null || result == DBNull.Value ? "NULL" : result.ToString();
                                sb.AppendLine($"[OK] {name,-20} = {valueStr}");
                            }
                        }
                        catch (Exception ex)
                        {
                            sb.AppendLine($"[BŁĄD] {name,-20} = {ex.Message}");
                        }
                    }
                    sb.AppendLine();

                    // Test 4: Pokaż przykładowy rekord surowy
                    sb.AppendLine("--- TEST 4: Przykładowy rekord (surowe dane) ---");
                    using (var cmd = new SqlCommand(@"
                        SELECT TOP 1
                            fc.ID, fc.CarLp, fc.CustomerGID,
                            fc.DeclI1, fc.NettoFarmWeight, fc.WagaDek,
                            fc.FullWeight, fc.EmptyWeight, fc.NettoWeight, fc.LumQnt
                        FROM dbo.FarmerCalc fc
                        WHERE fc.CalcDate = @Data AND ISNULL(fc.Deleted, 0) = 0", conn))
                    {
                        cmd.Parameters.AddWithValue("@Data", data.Date);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    var val = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString();
                                    var type = reader.GetFieldType(i).Name;
                                    sb.AppendLine($"  {reader.GetName(i),-20} [{type,-10}] = {val}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[BŁĄD KRYTYCZNY] {ex.GetType().Name}: {ex.Message}");
                sb.AppendLine();
                sb.AppendLine("Stack trace:");
                sb.AppendLine(ex.StackTrace);
            }

            LastDiagnosticError = sb.ToString();
            return LastDiagnosticError;
        }

        /// <summary>
        /// Pobiera liste dostaw zywca na dany dzien z DYNAMICZNYM PLANEM.
        /// PLAN z HarmonogramDostaw, RZECZYWISTE z FarmerCalc.
        /// Ostatnie auto = RESZTA z harmonogramu (nie sztywny plan 1/n)
        /// </summary>
        public async Task<List<DostawaItem>> GetDostawyAsync(DateTime data)
        {
            var dostawy = new List<DostawaItem>();

            // Zapytanie z CTE: dynamiczny plan gdzie ostatnie auto = RESZTA z harmonogramu
            const string query = @"
                WITH SumaZwazonychPerHarmonogram AS (
                    -- Suma juz zwazonych dla kazdego harmonogramu
                    SELECT
                        fc.LpDostawy,
                        COUNT(*) AS AutaZwazone,
                        SUM(ISNULL(fc.LumQnt, 0)) AS SztukiZwazoneSuma,
                        SUM(ISNULL(fc.NettoWeight, 0)) AS KgZwazoneSuma
                    FROM dbo.FarmerCalc fc
                    WHERE fc.CalcDate = @Data
                      AND ISNULL(fc.Deleted, 0) = 0
                      AND ISNULL(fc.FullWeight, 0) > 0
                      AND ISNULL(fc.EmptyWeight, 0) > 0  -- tylko zwazone (brutto + tara)
                    GROUP BY fc.LpDostawy
                ),
                SumaWszystkichPerHarmonogram AS (
                    -- Wszystkie auta (zwazone + oczekujace) dla kazdego harmonogramu
                    SELECT
                        fc.LpDostawy,
                        COUNT(*) AS AutaOgolem
                    FROM dbo.FarmerCalc fc
                    WHERE fc.CalcDate = @Data
                      AND ISNULL(fc.Deleted, 0) = 0
                    GROUP BY fc.LpDostawy
                ),
                PozostaloPerHarmonogram AS (
                    -- Obliczenie pozostalej ilosci dla kazdego harmonogramu
                    SELECT
                        fc.LpDostawy,
                        hd.Lp AS HarmonogramLp,
                        hd.Dostawca AS HodowcaHarmonogram,
                        ISNULL(hd.SztukiDek, 0) AS PlanSztukiLacznie,
                        CAST(ISNULL(hd.SztukiDek, 0) * ISNULL(hd.WagaDek, 0) AS DECIMAL(12,0)) AS PlanKgLacznie,
                        ISNULL(hd.WagaDek, 0) AS WagaDekl,
                        TRY_CAST(hd.SztSzuflada AS DECIMAL(10,2)) AS SztPojPlan,
                        ISNULL(TRY_CAST(hd.Auta AS INT), 1) AS AutaPlanowane,
                        ISNULL(sz.AutaZwazone, 0) AS AutaZwazone,
                        ISNULL(sw.AutaOgolem, 0) AS AutaOgolem,
                        ISNULL(sz.SztukiZwazoneSuma, 0) AS SztukiZwazoneSuma,
                        ISNULL(sz.KgZwazoneSuma, 0) AS KgZwazoneSuma,
                        -- POZOSTALO
                        ISNULL(hd.SztukiDek, 0) - ISNULL(sz.SztukiZwazoneSuma, 0) AS SztukiPozostalo,
                        CAST(ISNULL(hd.SztukiDek, 0) * ISNULL(hd.WagaDek, 0) AS DECIMAL(12,0)) - ISNULL(sz.KgZwazoneSuma, 0) AS KgPozostalo,
                        -- Ile aut jeszcze czeka
                        ISNULL(sw.AutaOgolem, 0) - ISNULL(sz.AutaZwazone, 0) AS AutaCzekajacych,
                        -- Procent realizacji
                        CASE WHEN CAST(ISNULL(hd.SztukiDek, 0) * ISNULL(hd.WagaDek, 0) AS DECIMAL(12,0)) > 0
                             THEN CAST(ISNULL(sz.KgZwazoneSuma, 0) * 100.0 / (ISNULL(hd.SztukiDek, 0) * ISNULL(hd.WagaDek, 0)) AS DECIMAL(5,1))
                             ELSE 0 END AS RealizacjaProc,
                        -- Trend (srednia na zwazone auto vs plan na auto)
                        CASE WHEN ISNULL(sz.AutaZwazone, 0) > 0 AND ISNULL(TRY_CAST(hd.Auta AS INT), 1) > 0
                             THEN CAST((ISNULL(sz.KgZwazoneSuma, 0) / sz.AutaZwazone)
                                  / NULLIF((CAST(ISNULL(hd.SztukiDek, 0) * ISNULL(hd.WagaDek, 0) AS DECIMAL(12,0)) / ISNULL(TRY_CAST(hd.Auta AS INT), 1)), 0) * 100 AS DECIMAL(5,1))
                             ELSE 100 END AS TrendProc
                    FROM (SELECT DISTINCT LpDostawy FROM dbo.FarmerCalc WHERE CalcDate = @Data AND ISNULL(Deleted, 0) = 0) fc
                    LEFT JOIN dbo.HarmonogramDostaw hd ON fc.LpDostawy = hd.Lp
                    LEFT JOIN SumaZwazonychPerHarmonogram sz ON fc.LpDostawy = sz.LpDostawy
                    LEFT JOIN SumaWszystkichPerHarmonogram sw ON fc.LpDostawy = sw.LpDostawy
                )
                SELECT
                    fc.ID,
                    fc.CarLp AS NrKursu,
                    fc.CalcDate AS Data,
                    fc.LpDostawy,

                    -- Hodowca
                    ISNULL(d.Name, ISNULL(ph.HodowcaHarmonogram, 'Nieznany')) AS Hodowca,
                    ISNULL(d.ShortName, '') AS HodowcaSkrot,

                    -- ========== PLAN LACZNY Z HARMONOGRAMU ==========
                    ISNULL(ph.PlanSztukiLacznie, 0) AS PlanSztukiLacznie,
                    ISNULL(ph.PlanKgLacznie, 0) AS PlanKgLacznie,
                    ISNULL(ph.WagaDekl, 0) AS WagaDeklHarmonogram,
                    ph.SztPojPlan,
                    ISNULL(ph.AutaPlanowane, 1) AS AutaPlanowane,

                    -- ========== POSTEP HARMONOGRAMU ==========
                    ISNULL(ph.AutaZwazone, 0) AS AutaZwazone,
                    ISNULL(ph.AutaOgolem, 0) AS AutaOgolem,
                    ISNULL(ph.AutaCzekajacych, 0) AS AutaCzekajacych,
                    ISNULL(ph.SztukiZwazoneSuma, 0) AS SztukiZwazoneSuma,
                    ISNULL(ph.KgZwazoneSuma, 0) AS KgZwazoneSuma,
                    ISNULL(ph.SztukiPozostalo, 0) AS SztukiPozostalo,
                    ISNULL(ph.KgPozostalo, 0) AS KgPozostalo,
                    ISNULL(ph.RealizacjaProc, 0) AS RealizacjaProc,
                    ISNULL(ph.TrendProc, 100) AS TrendProc,

                    -- ========== PLAN NA POJEDYNCZE AUTO (proporcjonalny) ==========
                    CASE WHEN ISNULL(ph.AutaPlanowane, 0) > 0
                         THEN CAST(ISNULL(ph.PlanSztukiLacznie, 0) / ph.AutaPlanowane AS INT)
                         ELSE ISNULL(fc.DeclI1, 0) END AS SztukiPlan,
                    CASE WHEN ISNULL(ph.AutaPlanowane, 0) > 0
                         THEN CAST(ISNULL(ph.PlanKgLacznie, 0) / ph.AutaPlanowane AS DECIMAL(12,0))
                         ELSE CAST(ISNULL(fc.DeclI1, 0) * COALESCE(ph.WagaDekl, fc.WagaDek, 0) AS DECIMAL(12,0)) END AS KgPlan,

                    -- Srednia waga deklarowana
                    CAST(ISNULL(ph.WagaDekl, COALESCE(fc.WagaDek, 0)) AS DECIMAL(10,3)) AS SredniaWagaPlan,

                    -- ========== RZECZYWISTE (z FarmerCalc - portier) ==========
                    CAST(ISNULL(fc.FullWeight, 0) AS DECIMAL(18,2)) AS Brutto,
                    CAST(ISNULL(fc.EmptyWeight, 0) AS DECIMAL(18,2)) AS Tara,
                    CAST(ISNULL(fc.NettoWeight, 0) AS DECIMAL(18,2)) AS KgRzeczywiste,
                    ISNULL(fc.LumQnt, 0) AS SztukiRzeczywiste,

                    -- Srednia waga rzeczywista [kg/szt]
                    CASE WHEN ISNULL(fc.LumQnt, 0) > 0 AND ISNULL(fc.NettoWeight, 0) > 0
                         THEN CAST(fc.NettoWeight / fc.LumQnt AS DECIMAL(10,3))
                         ELSE NULL END AS SredniaWagaRzeczywista,

                    -- Rzeczywiste szt/pojemnik
                    TRY_CAST(fc.SztPoj AS DECIMAL(10,2)) AS SztPojRzecz,

                    -- ========== ODCHYLENIE KG (wzgledem planu na auto) ==========
                    CASE WHEN ISNULL(ph.AutaPlanowane, 0) > 0 AND ISNULL(fc.NettoWeight, 0) > 0
                         THEN CAST(fc.NettoWeight - (ISNULL(ph.PlanKgLacznie, 0) / ph.AutaPlanowane) AS DECIMAL(12,0))
                         WHEN ISNULL(fc.NettoWeight, 0) > 0 AND COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) > 0
                         THEN CAST(fc.NettoWeight - COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) AS DECIMAL(18,2))
                         ELSE NULL END AS OdchylenieKg,

                    -- Odchylenie %
                    CASE WHEN ISNULL(ph.AutaPlanowane, 0) > 0
                              AND ISNULL(fc.NettoWeight, 0) > 0
                              AND (ISNULL(ph.PlanKgLacznie, 0) / ph.AutaPlanowane) > 0
                         THEN CAST(
                              (fc.NettoWeight - (ISNULL(ph.PlanKgLacznie, 0) / ph.AutaPlanowane))
                              / (ISNULL(ph.PlanKgLacznie, 0) / ph.AutaPlanowane) * 100
                              AS DECIMAL(5,2))
                         WHEN ISNULL(fc.NettoWeight, 0) > 0 AND COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) > 0
                         THEN CAST((fc.NettoWeight - COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0))
                              / NULLIF(COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0), 0) * 100 AS DECIMAL(10,2))
                         ELSE NULL END AS OdchylenieProc,

                    -- ========== ODCHYLENIE SREDNIEJ WAGI ==========
                    CASE WHEN ISNULL(fc.LumQnt, 0) > 0
                              AND ISNULL(fc.NettoWeight, 0) > 0
                              AND ISNULL(ph.WagaDekl, COALESCE(fc.WagaDek, 0)) > 0
                         THEN CAST((fc.NettoWeight / fc.LumQnt) - ISNULL(ph.WagaDekl, COALESCE(fc.WagaDek, 0)) AS DECIMAL(10,3))
                         ELSE NULL END AS OdchylenieWagi,

                    -- ========== STATUS ==========
                    CASE
                        WHEN ISNULL(fc.FullWeight, 0) > 0 AND ISNULL(fc.EmptyWeight, 0) > 0 THEN 2
                        WHEN ISNULL(fc.FullWeight, 0) > 0 THEN 1
                        ELSE 0
                    END AS StatusId,

                    -- KONFISKATY
                    ISNULL(fc.DeclI2, 0) AS Padle,
                    ISNULL(fc.DeclI3, 0) + ISNULL(fc.DeclI4, 0) + ISNULL(fc.DeclI5, 0) AS Konfiskaty,

                    -- TIMESTAMPY
                    fc.Przyjazd,
                    fc.SlaughterWeightDate AS GodzinaWazenia,
                    fc.SlaughterWeightUser AS KtoWazyl

                FROM dbo.FarmerCalc fc
                LEFT JOIN PozostaloPerHarmonogram ph ON fc.LpDostawy = ph.LpDostawy
                LEFT JOIN dbo.Dostawcy d ON LTRIM(RTRIM(CAST(d.ID AS NVARCHAR(20)))) = LTRIM(RTRIM(fc.CustomerGID))
                WHERE fc.CalcDate = @Data
                  AND ISNULL(fc.Deleted, 0) = 0
                ORDER BY fc.CarLp";

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Data", data.Date);
                        cmd.CommandTimeout = 30;

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var item = new DostawaItem
                                {
                                    ID = reader.GetInt32(reader.GetOrdinal("ID")),
                                    NrKursu = reader.IsDBNull(reader.GetOrdinal("NrKursu")) ? 0 : reader.GetInt32(reader.GetOrdinal("NrKursu")),
                                    Data = reader.GetDateTime(reader.GetOrdinal("Data")),
                                    LpDostawy = reader.IsDBNull(reader.GetOrdinal("LpDostawy")) ? null : reader.GetInt32(reader.GetOrdinal("LpDostawy")),
                                    Hodowca = reader.GetString(reader.GetOrdinal("Hodowca")),
                                    HodowcaSkrot = reader.GetString(reader.GetOrdinal("HodowcaSkrot")),

                                    // Plan laczny z harmonogramu
                                    PlanSztukiLacznie = reader.IsDBNull(reader.GetOrdinal("PlanSztukiLacznie")) ? 0 : Convert.ToInt32(reader.GetValue(reader.GetOrdinal("PlanSztukiLacznie"))),
                                    PlanKgLacznie = reader.IsDBNull(reader.GetOrdinal("PlanKgLacznie")) ? 0 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("PlanKgLacznie"))),
                                    AutaPlanowane = reader.IsDBNull(reader.GetOrdinal("AutaPlanowane")) ? 1 : reader.GetInt32(reader.GetOrdinal("AutaPlanowane")),

                                    // Postep harmonogramu
                                    AutaZwazone = reader.IsDBNull(reader.GetOrdinal("AutaZwazone")) ? 0 : reader.GetInt32(reader.GetOrdinal("AutaZwazone")),
                                    AutaOgolem = reader.IsDBNull(reader.GetOrdinal("AutaOgolem")) ? 0 : reader.GetInt32(reader.GetOrdinal("AutaOgolem")),
                                    SztukiZwazoneSuma = reader.IsDBNull(reader.GetOrdinal("SztukiZwazoneSuma")) ? 0 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("SztukiZwazoneSuma"))),
                                    KgZwazoneSuma = reader.IsDBNull(reader.GetOrdinal("KgZwazoneSuma")) ? 0 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("KgZwazoneSuma"))),
                                    SztukiPozostalo = reader.IsDBNull(reader.GetOrdinal("SztukiPozostalo")) ? 0 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("SztukiPozostalo"))),
                                    KgPozostalo = reader.IsDBNull(reader.GetOrdinal("KgPozostalo")) ? 0 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("KgPozostalo"))),
                                    RealizacjaProc = reader.IsDBNull(reader.GetOrdinal("RealizacjaProc")) ? 0 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("RealizacjaProc"))),
                                    TrendProc = reader.IsDBNull(reader.GetOrdinal("TrendProc")) ? 100 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("TrendProc"))),

                                    // Plan na pojedyncze auto
                                    SztukiPlan = reader.IsDBNull(reader.GetOrdinal("SztukiPlan")) ? 0 : reader.GetInt32(reader.GetOrdinal("SztukiPlan")),
                                    KgPlan = reader.IsDBNull(reader.GetOrdinal("KgPlan")) ? 0 : reader.GetDecimal(reader.GetOrdinal("KgPlan")),
                                    SredniaWagaPlan = reader.IsDBNull(reader.GetOrdinal("SredniaWagaPlan")) ? null : reader.GetDecimal(reader.GetOrdinal("SredniaWagaPlan")),
                                    WagaDeklHarmonogram = reader.IsDBNull(reader.GetOrdinal("WagaDeklHarmonogram")) ? null : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("WagaDeklHarmonogram"))),
                                    SztPojPlan = reader.IsDBNull(reader.GetOrdinal("SztPojPlan")) ? null : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("SztPojPlan"))),

                                    // Rzeczywiste
                                    Brutto = reader.GetDecimal(reader.GetOrdinal("Brutto")),
                                    Tara = reader.GetDecimal(reader.GetOrdinal("Tara")),
                                    KgRzeczywiste = reader.GetDecimal(reader.GetOrdinal("KgRzeczywiste")),
                                    SztukiRzeczywiste = reader.GetInt32(reader.GetOrdinal("SztukiRzeczywiste")),
                                    SredniaWagaRzeczywista = reader.IsDBNull(reader.GetOrdinal("SredniaWagaRzeczywista")) ? null : reader.GetDecimal(reader.GetOrdinal("SredniaWagaRzeczywista")),
                                    SztPojRzecz = reader.IsDBNull(reader.GetOrdinal("SztPojRzecz")) ? null : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("SztPojRzecz"))),

                                    // Odchylenia
                                    OdchylenieKg = reader.IsDBNull(reader.GetOrdinal("OdchylenieKg")) ? null : reader.GetDecimal(reader.GetOrdinal("OdchylenieKg")),
                                    OdchylenieProc = reader.IsDBNull(reader.GetOrdinal("OdchylenieProc")) ? null : reader.GetDecimal(reader.GetOrdinal("OdchylenieProc")),
                                    OdchylenieWagi = reader.IsDBNull(reader.GetOrdinal("OdchylenieWagi")) ? null : reader.GetDecimal(reader.GetOrdinal("OdchylenieWagi")),

                                    StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
                                    Padle = reader.GetInt32(reader.GetOrdinal("Padle")),
                                    Konfiskaty = reader.GetInt32(reader.GetOrdinal("Konfiskaty")),
                                    Przyjazd = reader.IsDBNull(reader.GetOrdinal("Przyjazd")) ? null : reader.GetDateTime(reader.GetOrdinal("Przyjazd")),
                                    GodzinaWazenia = reader.IsDBNull(reader.GetOrdinal("GodzinaWazenia")) ? null : reader.GetDateTime(reader.GetOrdinal("GodzinaWazenia")),
                                    KtoWazyl = reader.IsDBNull(reader.GetOrdinal("KtoWazyl")) ? null : reader.GetString(reader.GetOrdinal("KtoWazyl"))
                                };
                                dostawy.Add(item);
                            }
                        }
                    }
                }

                _lastFetch = DateTime.Now;
                Debug.WriteLine($"[PrzychodService] Pobrano {dostawy.Count} dostaw na {data:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PrzychodService] Błąd GetDostawyAsync: {ex.Message}");
                Debug.WriteLine($"[PrzychodService] Stack: {ex.StackTrace}");

                // Zapisz szczegóły błędu
                LastDiagnosticError = $"Błąd: {ex.Message}\n\nStack trace:\n{ex.StackTrace}";
                throw;
            }

            return dostawy;
        }

        /// <summary>
        /// Pobiera podsumowanie dzienne
        /// PLAN z HarmonogramDostaw (unikalne LpDostawy), RZECZYWISTE z FarmerCalc
        /// </summary>
        public async Task<PodsumowanieDnia> GetPodsumowanieAsync(DateTime data)
        {
            var podsumowanie = new PodsumowanieDnia();

            // Nowe zapytanie z CTE: Plan z unikalnych harmonogramów
            // UWAGA: hd.Auta i hd.SztSzuflada mogą zawierać tekst, więc używamy TRY_CAST
            const string query = @"
                WITH UnikalneHarmonogramy AS (
                    -- Każdy LpDostawy tylko raz (bo może być kilka aut z tego samego harmonogramu)
                    SELECT DISTINCT
                        fc.LpDostawy,
                        hd.SztukiDek,
                        hd.WagaDek,
                        TRY_CAST(hd.SztSzuflada AS DECIMAL(10,2)) AS SztSzuflada,
                        TRY_CAST(hd.Auta AS INT) AS Auta,
                        CAST(ISNULL(hd.SztukiDek, 0) * ISNULL(hd.WagaDek, 0) AS DECIMAL(12,0)) AS KgPlanLacznie
                    FROM dbo.FarmerCalc fc
                    INNER JOIN dbo.HarmonogramDostaw hd ON fc.LpDostawy = hd.Lp
                    WHERE fc.CalcDate = @Data
                      AND ISNULL(fc.Deleted, 0) = 0
                      AND fc.LpDostawy IS NOT NULL
                ),
                PlanZFarmerCalc AS (
                    -- Fallback: plan z FarmerCalc dla rekordów bez LpDostawy
                    SELECT
                        SUM(ISNULL(fc.DeclI1, 0)) AS SztukiPlan,
                        SUM(CAST(COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) AS DECIMAL(18,2))) AS KgPlan
                    FROM dbo.FarmerCalc fc
                    WHERE fc.CalcDate = @Data
                      AND ISNULL(fc.Deleted, 0) = 0
                      AND fc.LpDostawy IS NULL
                ),
                RzeczywisteDnia AS (
                    SELECT
                        SUM(ISNULL(fc.LumQnt, 0)) AS SztukiRzeczSuma,
                        SUM(ISNULL(fc.NettoWeight, 0)) AS KgRzeczSuma,
                        COUNT(*) AS LiczbaDostawOgolem,
                        SUM(CASE WHEN ISNULL(fc.FullWeight,0) > 0 AND ISNULL(fc.EmptyWeight,0) > 0 THEN 1 ELSE 0 END) AS LiczbaZwazonych,
                        SUM(CASE WHEN ISNULL(fc.FullWeight,0) > 0 AND ISNULL(fc.EmptyWeight,0) = 0 THEN 1 ELSE 0 END) AS LiczbaCzekaNaTare,
                        SUM(CASE WHEN ISNULL(fc.FullWeight,0) = 0 THEN 1 ELSE 0 END) AS LiczbaOczekujacych,

                        -- Zważone kg
                        SUM(CASE WHEN ISNULL(fc.FullWeight,0) > 0 AND ISNULL(fc.EmptyWeight,0) > 0
                            THEN CAST(ISNULL(fc.NettoWeight, 0) AS DECIMAL(18,2)) ELSE 0 END) AS KgZwazoneSuma,
                        SUM(CASE WHEN ISNULL(fc.FullWeight,0) > 0 AND ISNULL(fc.EmptyWeight,0) > 0
                            THEN ISNULL(fc.LumQnt, 0) ELSE 0 END) AS SztukiZwazoneSuma
                    FROM dbo.FarmerCalc fc
                    WHERE fc.CalcDate = @Data
                      AND ISNULL(fc.Deleted, 0) = 0
                )
                SELECT
                    -- PLAN (z harmonogramów + fallback)
                    ISNULL((SELECT SUM(uh.SztukiDek) FROM UnikalneHarmonogramy uh), 0) + ISNULL((SELECT SztukiPlan FROM PlanZFarmerCalc), 0) AS SztukiPlanSuma,
                    ISNULL((SELECT SUM(uh.KgPlanLacznie) FROM UnikalneHarmonogramy uh), 0) + ISNULL((SELECT KgPlan FROM PlanZFarmerCalc), 0) AS KgPlanSuma,

                    -- Średnia waga z harmonogramów (ważona)
                    CASE WHEN (SELECT SUM(uh.SztukiDek) FROM UnikalneHarmonogramy uh) > 0
                         THEN CAST((SELECT SUM(uh.KgPlanLacznie) FROM UnikalneHarmonogramy uh) / NULLIF((SELECT SUM(uh.SztukiDek) FROM UnikalneHarmonogramy uh), 0) AS DECIMAL(10,3))
                         ELSE NULL END AS SrWagaPlanSrednia,

                    -- RZECZYWISTE
                    r.SztukiRzeczSuma,
                    r.KgRzeczSuma,
                    r.KgZwazoneSuma,
                    r.SztukiZwazoneSuma,

                    -- Średnia waga rzeczywista
                    CASE WHEN r.SztukiZwazoneSuma > 0
                         THEN CAST(r.KgZwazoneSuma / NULLIF(r.SztukiZwazoneSuma, 0) AS DECIMAL(10,3))
                         ELSE NULL END AS SrWagaRzeczSrednia,

                    -- ODCHYLENIE (zważone - plan dla zważonych)
                    r.KgZwazoneSuma - ISNULL((SELECT SUM(uh.KgPlanLacznie) FROM UnikalneHarmonogramy uh), 0) AS OdchylenieKgSuma,

                    -- Plan do zważonych (do obliczenia %)
                    ISNULL((SELECT SUM(uh.KgPlanLacznie) FROM UnikalneHarmonogramy uh), 0) AS KgPlanDoZwazonych,

                    -- LICZNIKI
                    r.LiczbaDostawOgolem,
                    r.LiczbaZwazonych,
                    r.LiczbaCzekaNaTare,
                    r.LiczbaOczekujacych

                FROM RzeczywisteDnia r";

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Data", data.Date);
                        cmd.CommandTimeout = 30;

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                podsumowanie.SztukiPlanSuma = reader.IsDBNull(reader.GetOrdinal("SztukiPlanSuma")) ? 0 : Convert.ToInt32(reader.GetValue(reader.GetOrdinal("SztukiPlanSuma")));
                                podsumowanie.KgPlanSuma = reader.IsDBNull(reader.GetOrdinal("KgPlanSuma")) ? 0 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("KgPlanSuma")));
                                podsumowanie.SrWagaPlanSrednia = reader.IsDBNull(reader.GetOrdinal("SrWagaPlanSrednia")) ? null : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("SrWagaPlanSrednia")));

                                podsumowanie.SztukiZwazoneSuma = reader.IsDBNull(reader.GetOrdinal("SztukiZwazoneSuma")) ? 0 : Convert.ToInt32(reader.GetValue(reader.GetOrdinal("SztukiZwazoneSuma")));
                                podsumowanie.KgZwazoneSuma = reader.IsDBNull(reader.GetOrdinal("KgZwazoneSuma")) ? 0 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("KgZwazoneSuma")));
                                podsumowanie.SrWagaRzeczSrednia = reader.IsDBNull(reader.GetOrdinal("SrWagaRzeczSrednia")) ? null : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("SrWagaRzeczSrednia")));

                                podsumowanie.OdchylenieKgSuma = reader.IsDBNull(reader.GetOrdinal("OdchylenieKgSuma")) ? 0 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("OdchylenieKgSuma")));
                                podsumowanie.KgPlanDoZwazonych = reader.IsDBNull(reader.GetOrdinal("KgPlanDoZwazonych")) ? 0 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("KgPlanDoZwazonych")));

                                podsumowanie.LiczbaDostawOgolem = reader.IsDBNull(reader.GetOrdinal("LiczbaDostawOgolem")) ? 0 : Convert.ToInt32(reader.GetValue(reader.GetOrdinal("LiczbaDostawOgolem")));
                                podsumowanie.LiczbaZwazonych = reader.IsDBNull(reader.GetOrdinal("LiczbaZwazonych")) ? 0 : Convert.ToInt32(reader.GetValue(reader.GetOrdinal("LiczbaZwazonych")));
                                podsumowanie.LiczbaCzekaNaTare = reader.IsDBNull(reader.GetOrdinal("LiczbaCzekaNaTare")) ? 0 : Convert.ToInt32(reader.GetValue(reader.GetOrdinal("LiczbaCzekaNaTare")));
                                podsumowanie.LiczbaOczekujacych = reader.IsDBNull(reader.GetOrdinal("LiczbaOczekujacych")) ? 0 : Convert.ToInt32(reader.GetValue(reader.GetOrdinal("LiczbaOczekujacych")));
                            }
                        }
                    }
                }

                Debug.WriteLine($"[PrzychodService] Podsumowanie: Plan {podsumowanie.KgPlanSuma:N0} kg, Zważone {podsumowanie.KgZwazoneSuma:N0} kg");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PrzychodService] Błąd GetPodsumowanieAsync: {ex.Message}");
                LastDiagnosticError = $"Błąd podsumowania: {ex.Message}\n\nStack trace:\n{ex.StackTrace}";
                throw;
            }

            return podsumowanie;
        }

        /// <summary>
        /// Pobiera prognoze koncowa dnia z alertem redukcji zamowien.
        /// Prognoza = KgZwazone * (AutaOgolem / AutaZwazone)
        /// </summary>
        public async Task<PrognozaDnia> GetPrognozaDniaAsync(DateTime data)
        {
            var prognoza = new PrognozaDnia();

            const string query = @"
                WITH DaneDnia AS (
                    SELECT
                        SUM(ISNULL(fc.NettoWeight, 0)) AS KgZwazone,
                        SUM(CASE WHEN ISNULL(fc.FullWeight,0) > 0 AND ISNULL(fc.EmptyWeight,0) > 0
                                 THEN 1 ELSE 0 END) AS AutaZwazone,
                        COUNT(*) AS AutaOgolem
                    FROM dbo.FarmerCalc fc
                    WHERE fc.CalcDate = @Data
                      AND ISNULL(fc.Deleted, 0) = 0
                ),
                PlanDnia AS (
                    SELECT
                        SUM(CAST(ISNULL(hd.SztukiDek, 0) * ISNULL(hd.WagaDek, 0) AS DECIMAL(12,0))) AS KgPlanLacznie
                    FROM (SELECT DISTINCT LpDostawy FROM dbo.FarmerCalc WHERE CalcDate = @Data AND ISNULL(Deleted, 0) = 0 AND LpDostawy IS NOT NULL) fc
                    INNER JOIN dbo.HarmonogramDostaw hd ON fc.LpDostawy = hd.Lp
                )
                SELECT
                    ISNULL(p.KgPlanLacznie, 0) AS KgPlanLacznie,
                    ISNULL(d.KgZwazone, 0) AS KgZwazone,
                    ISNULL(d.AutaZwazone, 0) AS AutaZwazone,
                    ISNULL(d.AutaOgolem, 0) AS AutaOgolem
                FROM DaneDnia d
                CROSS JOIN PlanDnia p";

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Data", data.Date);
                        cmd.CommandTimeout = 30;

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                prognoza.KgPlanLacznie = reader.IsDBNull(reader.GetOrdinal("KgPlanLacznie")) ? 0 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("KgPlanLacznie")));
                                prognoza.KgZwazone = reader.IsDBNull(reader.GetOrdinal("KgZwazone")) ? 0 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("KgZwazone")));
                                prognoza.AutaZwazone = reader.IsDBNull(reader.GetOrdinal("AutaZwazone")) ? 0 : Convert.ToInt32(reader.GetValue(reader.GetOrdinal("AutaZwazone")));
                                prognoza.AutaOgolem = reader.IsDBNull(reader.GetOrdinal("AutaOgolem")) ? 0 : Convert.ToInt32(reader.GetValue(reader.GetOrdinal("AutaOgolem")));
                            }
                        }
                    }
                }

                Debug.WriteLine($"[PrzychodService] Prognoza: Plan {prognoza.KgPlanLacznie:N0} kg, Zwazone {prognoza.KgZwazone:N0} kg, Trend {prognoza.TrendProc:N0}%");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PrzychodService] Blad GetPrognozaDniaAsync: {ex.Message}");
                LastDiagnosticError = $"Blad prognozy: {ex.Message}\n\nStack trace:\n{ex.StackTrace}";
                throw;
            }

            return prognoza;
        }

        /// <summary>
        /// Pobiera liste postepow realizacji harmonogramow per hodowca.
        /// </summary>
        public async Task<List<PostepHarmonogramu>> GetPostepyHarmonogramowAsync(DateTime data)
        {
            var postepy = new List<PostepHarmonogramu>();

            const string query = @"
                WITH SumaZwazonychPerHarmonogram AS (
                    SELECT
                        fc.LpDostawy,
                        COUNT(*) AS AutaZwazone,
                        SUM(ISNULL(fc.LumQnt, 0)) AS SztukiZwazoneSuma,
                        SUM(ISNULL(fc.NettoWeight, 0)) AS KgZwazoneSuma
                    FROM dbo.FarmerCalc fc
                    WHERE fc.CalcDate = @Data
                      AND ISNULL(fc.Deleted, 0) = 0
                      AND ISNULL(fc.FullWeight, 0) > 0
                      AND ISNULL(fc.EmptyWeight, 0) > 0
                    GROUP BY fc.LpDostawy
                ),
                SumaWszystkichPerHarmonogram AS (
                    SELECT
                        fc.LpDostawy,
                        COUNT(*) AS AutaOgolem
                    FROM dbo.FarmerCalc fc
                    WHERE fc.CalcDate = @Data
                      AND ISNULL(fc.Deleted, 0) = 0
                    GROUP BY fc.LpDostawy
                )
                SELECT
                    hd.Lp AS LpDostawy,
                    ISNULL(hd.Dostawca, 'Nieznany') AS Hodowca,
                    ISNULL(sz.AutaZwazone, 0) AS AutaZwazone,
                    ISNULL(sw.AutaOgolem, 0) AS AutaOgolem,
                    ISNULL(TRY_CAST(hd.Auta AS INT), 1) AS AutaPlanowane,
                    ISNULL(hd.SztukiDek, 0) AS PlanSztukiLacznie,
                    CAST(ISNULL(hd.SztukiDek, 0) * ISNULL(hd.WagaDek, 0) AS DECIMAL(12,0)) AS PlanKgLacznie,
                    ISNULL(sz.SztukiZwazoneSuma, 0) AS SztukiZwazoneSuma,
                    ISNULL(sz.KgZwazoneSuma, 0) AS KgZwazoneSuma
                FROM (SELECT DISTINCT LpDostawy FROM dbo.FarmerCalc WHERE CalcDate = @Data AND ISNULL(Deleted, 0) = 0 AND LpDostawy IS NOT NULL) fc
                INNER JOIN dbo.HarmonogramDostaw hd ON fc.LpDostawy = hd.Lp
                LEFT JOIN SumaZwazonychPerHarmonogram sz ON fc.LpDostawy = sz.LpDostawy
                LEFT JOIN SumaWszystkichPerHarmonogram sw ON fc.LpDostawy = sw.LpDostawy
                ORDER BY hd.Dostawca";

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Data", data.Date);
                        cmd.CommandTimeout = 30;

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var item = new PostepHarmonogramu
                                {
                                    LpDostawy = reader.GetInt32(reader.GetOrdinal("LpDostawy")),
                                    Hodowca = reader.GetString(reader.GetOrdinal("Hodowca")),
                                    AutaZwazone = reader.IsDBNull(reader.GetOrdinal("AutaZwazone")) ? 0 : reader.GetInt32(reader.GetOrdinal("AutaZwazone")),
                                    AutaOgolem = reader.IsDBNull(reader.GetOrdinal("AutaOgolem")) ? 0 : reader.GetInt32(reader.GetOrdinal("AutaOgolem")),
                                    AutaPlanowane = reader.IsDBNull(reader.GetOrdinal("AutaPlanowane")) ? 1 : reader.GetInt32(reader.GetOrdinal("AutaPlanowane")),
                                    PlanSztukiLacznie = reader.IsDBNull(reader.GetOrdinal("PlanSztukiLacznie")) ? 0 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("PlanSztukiLacznie"))),
                                    PlanKgLacznie = reader.IsDBNull(reader.GetOrdinal("PlanKgLacznie")) ? 0 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("PlanKgLacznie"))),
                                    SztukiZwazoneSuma = reader.IsDBNull(reader.GetOrdinal("SztukiZwazoneSuma")) ? 0 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("SztukiZwazoneSuma"))),
                                    KgZwazoneSuma = reader.IsDBNull(reader.GetOrdinal("KgZwazoneSuma")) ? 0 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("KgZwazoneSuma")))
                                };
                                postepy.Add(item);
                            }
                        }
                    }
                }

                Debug.WriteLine($"[PrzychodService] Pobrano {postepy.Count} harmonogramow");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PrzychodService] Blad GetPostepyHarmonogramowAsync: {ex.Message}");
                LastDiagnosticError = $"Blad postepow: {ex.Message}\n\nStack trace:\n{ex.StackTrace}";
                throw;
            }

            return postepy;
        }

        /// <summary>
        /// Testuje polaczenie z baza danych
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PrzychodService] Test połączenia nieudany: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Pobiera faktyczny przychód produkcji (PWP) z systemu Symfonia (Handel)
        /// Zwraca (FaktKlasaA, FaktKlasaB) w kg
        /// Katalog 67095 = Kurczak A, Katalog 67153 = Kurczak B
        /// </summary>
        public async Task<(decimal KlasaA, decimal KlasaB)> GetFaktycznyPrzychodAsync(DateTime data)
        {
            decimal faktA = 0;
            decimal faktB = 0;

            // Connection string do bazy Handel (Symfonia)
            const string handelConnStr =
                "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True;Connection Timeout=10;";

            // Zapytanie pobierające sumy z dokumentów PWP per katalog produktu
            // Katalog 67095 = Kurczak A (tuszki całe)
            // Katalog 67153 = Kurczak B (elementy do krojenia)
            const string query = @"
                SELECT
                    TW.katalog AS Katalog,
                    SUM(ABS(MZ.ilosc)) AS Ilosc
                FROM [HM].[MZ] MZ
                JOIN [HM].[MG] MG ON MZ.super = MG.id
                JOIN [HM].[TW] TW ON MZ.idtw = TW.ID
                WHERE MG.seria IN ('sPWP', 'PWP')
                  AND MG.aktywny = 1
                  AND MG.data = @Data
                  AND TW.katalog IN (67095, 67153)
                GROUP BY TW.katalog";

            try
            {
                using (var conn = new SqlConnection(handelConnStr))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Data", data.Date);
                        cmd.CommandTimeout = 15;

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int katalog = reader.GetInt32(reader.GetOrdinal("Katalog"));
                                decimal ilosc = reader.IsDBNull(reader.GetOrdinal("Ilosc"))
                                    ? 0 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("Ilosc")));

                                if (katalog == 67095)
                                    faktA = ilosc;
                                else if (katalog == 67153)
                                    faktB = ilosc;
                            }
                        }
                    }
                }

                Debug.WriteLine($"[PrzychodService] Faktyczny przychód Symfonia: A={faktA:N0} kg, B={faktB:N0} kg");
            }
            catch (Exception ex)
            {
                // Nie rzucamy błędu - dane z Symfonia są opcjonalne
                Debug.WriteLine($"[PrzychodService] Błąd GetFaktycznyPrzychodAsync (Handel): {ex.Message}");
                // Zwracamy zera - UI pokaże "-"
            }

            return (faktA, faktB);
        }
    }
}
