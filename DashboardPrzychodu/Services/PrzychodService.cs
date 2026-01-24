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
        /// Pobiera listę dostaw żywca na dany dzień
        /// PLAN z HarmonogramDostaw, RZECZYWISTE z FarmerCalc
        /// </summary>
        public async Task<List<DostawaItem>> GetDostawyAsync(DateTime data)
        {
            var dostawy = new List<DostawaItem>();

            // Nowe zapytanie: PLAN z HarmonogramDostaw (przez LpDostawy = Lp)
            // UWAGA: hd.Auta może zawierać tekst jak '0-1', więc używamy TRY_CAST
            const string query = @"
                SELECT
                    fc.ID,
                    fc.CarLp AS NrKursu,
                    fc.CalcDate AS Data,
                    fc.LpDostawy,

                    -- Hodowca
                    ISNULL(d.Name, ISNULL(hd.Dostawca, 'Nieznany')) AS Hodowca,
                    ISNULL(d.ShortName, '') AS HodowcaSkrot,

                    -- ========== PLAN (z HarmonogramDostaw) ==========
                    hd.Lp AS HarmonogramLp,
                    ISNULL(hd.SztukiDek, 0) AS SztukiPlanLacznie,
                    ISNULL(TRY_CAST(hd.Auta AS INT), 1) AS IloscAutPlan,
                    ISNULL(hd.WagaDek, 0) AS WagaDeklHarmonogram,
                    TRY_CAST(hd.SztSzuflada AS DECIMAL(10,2)) AS SztPojPlan,

                    -- Plan na JEDNO auto (proporcjonalnie):
                    -- TRY_CAST dla Auta - może zawierać tekst jak '0-1'
                    CASE WHEN ISNULL(TRY_CAST(hd.Auta AS INT), 0) > 0
                         THEN CAST(hd.SztukiDek / TRY_CAST(hd.Auta AS INT) AS INT)
                         ELSE ISNULL(hd.SztukiDek, ISNULL(fc.DeclI1, 0)) END AS SztukiPlan,

                    CASE WHEN ISNULL(TRY_CAST(hd.Auta AS INT), 0) > 0
                         THEN CAST((CAST(hd.SztukiDek AS DECIMAL) / TRY_CAST(hd.Auta AS INT)) * ISNULL(hd.WagaDek, 0) AS DECIMAL(12,0))
                         ELSE CAST(ISNULL(hd.SztukiDek, ISNULL(fc.DeclI1, 0)) * ISNULL(hd.WagaDek, COALESCE(fc.WagaDek, 0)) AS DECIMAL(12,0)) END AS KgPlan,

                    -- Średnia waga deklarowana (z harmonogramu)
                    CAST(ISNULL(hd.WagaDek, COALESCE(fc.WagaDek, 0)) AS DECIMAL(10,3)) AS SredniaWagaPlan,

                    -- ========== RZECZYWISTE (z FarmerCalc - portier) ==========
                    CAST(ISNULL(fc.FullWeight, 0) AS DECIMAL(18,2)) AS Brutto,
                    CAST(ISNULL(fc.EmptyWeight, 0) AS DECIMAL(18,2)) AS Tara,
                    CAST(ISNULL(fc.NettoWeight, 0) AS DECIMAL(18,2)) AS KgRzeczywiste,
                    ISNULL(fc.LumQnt, 0) AS SztukiRzeczywiste,

                    -- Średnia waga rzeczywista [kg/szt]
                    CASE WHEN ISNULL(fc.LumQnt, 0) > 0 AND ISNULL(fc.NettoWeight, 0) > 0
                         THEN CAST(fc.NettoWeight / fc.LumQnt AS DECIMAL(10,3))
                         ELSE NULL END AS SredniaWagaRzeczywista,

                    -- Rzeczywiste szt/pojemnik
                    TRY_CAST(fc.SztPoj AS DECIMAL(10,2)) AS SztPojRzecz,

                    -- ========== ODCHYLENIE KG ==========
                    CASE WHEN ISNULL(TRY_CAST(hd.Auta AS INT), 0) > 0 AND ISNULL(fc.NettoWeight, 0) > 0
                         THEN CAST(fc.NettoWeight - ((CAST(hd.SztukiDek AS DECIMAL) / TRY_CAST(hd.Auta AS INT)) * ISNULL(hd.WagaDek, 0)) AS DECIMAL(12,0))
                         WHEN ISNULL(fc.NettoWeight, 0) > 0 AND COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) > 0
                         THEN CAST(fc.NettoWeight - COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) AS DECIMAL(18,2))
                         ELSE NULL END AS OdchylenieKg,

                    -- Odchylenie %
                    CASE WHEN ISNULL(TRY_CAST(hd.Auta AS INT), 0) > 0
                              AND ISNULL(fc.NettoWeight, 0) > 0
                              AND (CAST(hd.SztukiDek AS DECIMAL) / TRY_CAST(hd.Auta AS INT)) * ISNULL(hd.WagaDek, 0) > 0
                         THEN CAST(
                              (fc.NettoWeight - ((CAST(hd.SztukiDek AS DECIMAL) / TRY_CAST(hd.Auta AS INT)) * hd.WagaDek))
                              / (((CAST(hd.SztukiDek AS DECIMAL) / TRY_CAST(hd.Auta AS INT)) * hd.WagaDek)) * 100
                              AS DECIMAL(5,2))
                         WHEN ISNULL(fc.NettoWeight, 0) > 0 AND COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) > 0
                         THEN CAST((fc.NettoWeight - COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0))
                              / NULLIF(COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0), 0) * 100 AS DECIMAL(10,2))
                         ELSE NULL END AS OdchylenieProc,

                    -- ========== ODCHYLENIE ŚREDNIEJ WAGI ==========
                    CASE WHEN ISNULL(fc.LumQnt, 0) > 0
                              AND ISNULL(fc.NettoWeight, 0) > 0
                              AND ISNULL(hd.WagaDek, COALESCE(fc.WagaDek, 0)) > 0
                         THEN CAST((fc.NettoWeight / fc.LumQnt) - ISNULL(hd.WagaDek, COALESCE(fc.WagaDek, 0)) AS DECIMAL(10,3))
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
                LEFT JOIN dbo.HarmonogramDostaw hd ON fc.LpDostawy = hd.Lp
                LEFT JOIN dbo.Dostawcy d ON TRY_CAST(LTRIM(RTRIM(fc.CustomerGID)) AS INT) = d.ID
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
                                    Hodowca = reader.GetString(reader.GetOrdinal("Hodowca")),
                                    HodowcaSkrot = reader.GetString(reader.GetOrdinal("HodowcaSkrot")),

                                    // Plan z HarmonogramDostaw
                                    SztukiPlan = reader.IsDBNull(reader.GetOrdinal("SztukiPlan")) ? 0 : reader.GetInt32(reader.GetOrdinal("SztukiPlan")),
                                    KgPlan = reader.IsDBNull(reader.GetOrdinal("KgPlan")) ? 0 : reader.GetDecimal(reader.GetOrdinal("KgPlan")),
                                    SredniaWagaPlan = reader.IsDBNull(reader.GetOrdinal("SredniaWagaPlan")) ? null : reader.GetDecimal(reader.GetOrdinal("SredniaWagaPlan")),
                                    WagaDeklHarmonogram = reader.IsDBNull(reader.GetOrdinal("WagaDeklHarmonogram")) ? null : reader.GetDecimal(reader.GetOrdinal("WagaDeklHarmonogram")),
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
        /// Testuje połączenie z bazą danych
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
    }
}
