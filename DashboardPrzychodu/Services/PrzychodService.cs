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
        /// </summary>
        public async Task<List<DostawaItem>> GetDostawyAsync(DateTime data)
        {
            var dostawy = new List<DostawaItem>();

            // UWAGA: Dodano NULLIF żeby uniknąć dzielenia przez zero i overflow
            const string query = @"
                SELECT
                    fc.ID,
                    fc.CarLp AS NrKursu,
                    fc.CalcDate AS Data,

                    -- Hodowca
                    ISNULL(d.Name, 'Nieznany') AS Hodowca,
                    ISNULL(d.ShortName, '') AS HodowcaSkrot,

                    -- PLAN (deklarowane)
                    ISNULL(fc.DeclI1, 0) AS SztukiPlan,
                    CAST(COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) AS DECIMAL(18,2)) AS KgPlan,

                    -- Średnia waga plan (z zabezpieczeniem przed dzieleniem przez 0)
                    CASE
                        WHEN ISNULL(fc.DeclI1, 0) > 0 AND COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) > 0
                        THEN CAST(COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) / NULLIF(fc.DeclI1, 0) AS DECIMAL(10,3))
                        ELSE NULL
                    END AS SredniaWagaPlan,

                    -- RZECZYWISTE (z portiera)
                    CAST(ISNULL(fc.FullWeight, 0) AS DECIMAL(18,2)) AS Brutto,
                    CAST(ISNULL(fc.EmptyWeight, 0) AS DECIMAL(18,2)) AS Tara,
                    CAST(ISNULL(fc.NettoWeight, 0) AS DECIMAL(18,2)) AS KgRzeczywiste,
                    ISNULL(fc.LumQnt, 0) AS SztukiRzeczywiste,

                    -- ŚREDNIA WAGA rzeczywista (z zabezpieczeniem)
                    CASE
                        WHEN ISNULL(fc.LumQnt, 0) > 0 AND ISNULL(fc.NettoWeight, 0) > 0
                        THEN CAST(fc.NettoWeight / NULLIF(fc.LumQnt, 0) AS DECIMAL(10,3))
                        ELSE NULL
                    END AS SredniaWagaRzeczywista,

                    -- ODCHYLENIE (z zabezpieczeniem)
                    CASE
                        WHEN ISNULL(fc.NettoWeight, 0) > 0 AND COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) > 0
                        THEN CAST(fc.NettoWeight - COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) AS DECIMAL(18,2))
                        ELSE NULL
                    END AS OdchylenieKg,

                    CASE
                        WHEN ISNULL(fc.NettoWeight, 0) > 0 AND COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) > 0
                        THEN CAST((fc.NettoWeight - COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0))
                             / NULLIF(COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0), 0) * 100 AS DECIMAL(10,2))
                        ELSE NULL
                    END AS OdchylenieProc,

                    -- STATUS
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
                LEFT JOIN dbo.Dostawcy d ON LTRIM(RTRIM(CAST(d.ID AS NVARCHAR(50)))) = LTRIM(RTRIM(fc.CustomerGID))
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
                                    SztukiPlan = reader.GetInt32(reader.GetOrdinal("SztukiPlan")),
                                    KgPlan = reader.GetDecimal(reader.GetOrdinal("KgPlan")),
                                    SredniaWagaPlan = reader.IsDBNull(reader.GetOrdinal("SredniaWagaPlan")) ? null : reader.GetDecimal(reader.GetOrdinal("SredniaWagaPlan")),
                                    Brutto = reader.GetDecimal(reader.GetOrdinal("Brutto")),
                                    Tara = reader.GetDecimal(reader.GetOrdinal("Tara")),
                                    KgRzeczywiste = reader.GetDecimal(reader.GetOrdinal("KgRzeczywiste")),
                                    SztukiRzeczywiste = reader.GetInt32(reader.GetOrdinal("SztukiRzeczywiste")),
                                    SredniaWagaRzeczywista = reader.IsDBNull(reader.GetOrdinal("SredniaWagaRzeczywista")) ? null : reader.GetDecimal(reader.GetOrdinal("SredniaWagaRzeczywista")),
                                    OdchylenieKg = reader.IsDBNull(reader.GetOrdinal("OdchylenieKg")) ? null : reader.GetDecimal(reader.GetOrdinal("OdchylenieKg")),
                                    OdchylenieProc = reader.IsDBNull(reader.GetOrdinal("OdchylenieProc")) ? null : reader.GetDecimal(reader.GetOrdinal("OdchylenieProc")),
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
        /// </summary>
        public async Task<PodsumowanieDnia> GetPodsumowanieAsync(DateTime data)
        {
            var podsumowanie = new PodsumowanieDnia();

            const string query = @"
                SELECT
                    -- PLAN
                    SUM(ISNULL(fc.DeclI1, 0)) AS SztukiPlanSuma,
                    SUM(CAST(COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) AS DECIMAL(18,2))) AS KgPlanSuma,

                    -- ZWAŻONE (tylko kompletne)
                    SUM(CASE WHEN ISNULL(fc.FullWeight,0) > 0 AND ISNULL(fc.EmptyWeight,0) > 0
                        THEN ISNULL(fc.LumQnt, 0) ELSE 0 END) AS SztukiZwazoneSuma,
                    SUM(CASE WHEN ISNULL(fc.FullWeight,0) > 0 AND ISNULL(fc.EmptyWeight,0) > 0
                        THEN CAST(ISNULL(fc.NettoWeight, 0) AS DECIMAL(18,2)) ELSE 0 END) AS KgZwazoneSuma,

                    -- PLAN dla zważonych (do obliczenia odchylenia)
                    SUM(CASE WHEN ISNULL(fc.FullWeight,0) > 0 AND ISNULL(fc.EmptyWeight,0) > 0
                        THEN CAST(COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) AS DECIMAL(18,2)) ELSE 0 END) AS KgPlanDoZwazonych,

                    -- LICZNIKI STATUSÓW
                    COUNT(*) AS LiczbaDostawOgolem,
                    SUM(CASE WHEN ISNULL(fc.FullWeight,0) > 0 AND ISNULL(fc.EmptyWeight,0) > 0 THEN 1 ELSE 0 END) AS LiczbaZwazonych,
                    SUM(CASE WHEN ISNULL(fc.FullWeight,0) > 0 AND ISNULL(fc.EmptyWeight,0) = 0 THEN 1 ELSE 0 END) AS LiczbaCzekaNaTare,
                    SUM(CASE WHEN ISNULL(fc.FullWeight,0) = 0 THEN 1 ELSE 0 END) AS LiczbaOczekujacych,

                    -- ODCHYLENIE GLOBALNE
                    SUM(CASE WHEN ISNULL(fc.FullWeight,0) > 0 AND ISNULL(fc.EmptyWeight,0) > 0
                        THEN CAST(ISNULL(fc.NettoWeight, 0) AS DECIMAL(18,2)) ELSE 0 END)
                    - SUM(CASE WHEN ISNULL(fc.FullWeight,0) > 0 AND ISNULL(fc.EmptyWeight,0) > 0
                        THEN CAST(COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) AS DECIMAL(18,2)) ELSE 0 END) AS OdchylenieKgSuma

                FROM dbo.FarmerCalc fc
                WHERE fc.CalcDate = @Data
                  AND ISNULL(fc.Deleted, 0) = 0";

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
                                podsumowanie.SztukiPlanSuma = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                                podsumowanie.KgPlanSuma = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                                podsumowanie.SztukiZwazoneSuma = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                                podsumowanie.KgZwazoneSuma = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3);
                                podsumowanie.KgPlanDoZwazonych = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4);
                                podsumowanie.LiczbaDostawOgolem = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
                                podsumowanie.LiczbaZwazonych = reader.IsDBNull(6) ? 0 : reader.GetInt32(6);
                                podsumowanie.LiczbaCzekaNaTare = reader.IsDBNull(7) ? 0 : reader.GetInt32(7);
                                podsumowanie.LiczbaOczekujacych = reader.IsDBNull(8) ? 0 : reader.GetInt32(8);
                                podsumowanie.OdchylenieKgSuma = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9);
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
