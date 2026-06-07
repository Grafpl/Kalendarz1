using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.DashboardPrzychodu.Models;

namespace Kalendarz1.DashboardPrzychodu.Services
{
    /// <summary>
    /// Wynik skonsolidowanego pobrania danych dashboardu w jednym round-tripie.
    /// </summary>
    public sealed class PrzychodLiveSnapshot
    {
        public List<DostawaItem> Dostawy { get; init; } = new();
        public PodsumowanieDnia Podsumowanie { get; init; } = new();
        public PrognozaDnia Prognoza { get; init; } = new();
        public List<PostepHarmonogramu> Postepy { get; init; } = new();
    }

    /// <summary>
    /// Serwis pobierania danych przychodu żywca z bazy LibraNet.
    /// Wszystkie 4 zapytania w jednym round-tripie (PrzychodLiveAll.sql, multi-result-set).
    /// </summary>
    public class PrzychodService
    {
        private readonly string _connectionString;
        private readonly string _handelConnectionString;
        private DateTime _lastFetch = DateTime.MinValue;
        private readonly TimeSpan _minRefreshInterval = TimeSpan.FromSeconds(5);

        // Lazy-loadowane treści SQL z plików w DashboardPrzychodu/SQL/
        private static readonly Lazy<string> _sqlAll = new(() => LoadSql("PrzychodLiveAll.sql"));
        private static readonly Lazy<string> _sqlFaktyczny = new(() => LoadSql("FaktycznyPrzychodSymfonia.sql"));
        private static readonly Lazy<string> _sqlHistoria = new(() => LoadSql("HistoriaZmianDeklaracji.sql"));

        public string LastDiagnosticError { get; private set; }

        public PrzychodService(string connectionString = null, string handelConnectionString = null)
        {
            _connectionString = connectionString ??
                "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True;Pooling=true;Min Pool Size=2;Max Pool Size=10;";
            _handelConnectionString = handelConnectionString ??
                "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True;Connection Timeout=10;";
        }

        /// <summary>
        /// Minimalny interwał odświeżania = 5s (chroni przed double-fetch przy klikaniu).
        /// </summary>
        public bool CanRefresh => DateTime.Now - _lastFetch >= _minRefreshInterval;

        private static string LoadSql(string fileName)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "DashboardPrzychodu", "SQL", fileName);
            if (!File.Exists(path))
                throw new FileNotFoundException($"SQL file not found: {path}");
            return File.ReadAllText(path);
        }

        /// <summary>
        /// Skonsolidowany fetch: 1 zapytanie z 4 result-setami → komplet danych dashboardu.
        /// Zastępuje 4 osobne GetDostawy/Podsumowanie/Prognoza/Postepy (każda skanowała FarmerCalc).
        /// </summary>
        public async Task<PrzychodLiveSnapshot> GetAllAsync(DateTime data, CancellationToken ct = default)
        {
            var snapshot = new PrzychodLiveSnapshot();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync(ct);

                using var cmd = new SqlCommand(_sqlAll.Value, conn);
                cmd.Parameters.AddWithValue("@Data", data.Date);
                cmd.CommandTimeout = 60;

                using var reader = await cmd.ExecuteReaderAsync(ct);

                // === RESULT SET 1: Dostawy ===
                while (await reader.ReadAsync(ct))
                    snapshot.Dostawy.Add(MapDostawa(reader));

                // === RESULT SET 2: Podsumowanie ===
                if (await reader.NextResultAsync(ct) && await reader.ReadAsync(ct))
                    MapPodsumowanie(reader, snapshot.Podsumowanie);

                // === RESULT SET 3: Prognoza ===
                if (await reader.NextResultAsync(ct) && await reader.ReadAsync(ct))
                    MapPrognoza(reader, snapshot.Prognoza);

                // === RESULT SET 4: Postepy ===
                if (await reader.NextResultAsync(ct))
                {
                    while (await reader.ReadAsync(ct))
                        snapshot.Postepy.Add(MapPostep(reader));
                }

                _lastFetch = DateTime.Now;
                Debug.WriteLine($"[PrzychodService] GetAllAsync OK: dostaw={snapshot.Dostawy.Count}, postepow={snapshot.Postepy.Count}, plan={snapshot.Podsumowanie.KgPlanSuma:N0} kg, zwazone={snapshot.Podsumowanie.KgZwazoneSuma:N0} kg");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PrzychodService] Blad GetAllAsync: {ex.Message}");
                LastDiagnosticError = $"Blad GetAllAsync: {ex.Message}\n\nStack:\n{ex.StackTrace}";
                throw;
            }

            return snapshot;
        }

        // ===================================================================
        // BACKWARD-COMPATIBLE WRAPPERS (utrzymywane na potrzeby diagnostyki
        // i ewentualnego zewnetrznego uzycia - przekierowuja do GetAllAsync).
        // ===================================================================

        public async Task<List<DostawaItem>> GetDostawyAsync(DateTime data)
            => (await GetAllAsync(data)).Dostawy;

        public async Task<PodsumowanieDnia> GetPodsumowanieAsync(DateTime data)
            => (await GetAllAsync(data)).Podsumowanie;

        public async Task<PrognozaDnia> GetPrognozaDniaAsync(DateTime data)
            => (await GetAllAsync(data)).Prognoza;

        public async Task<List<PostepHarmonogramu>> GetPostepyHarmonogramowAsync(DateTime data)
            => (await GetAllAsync(data)).Postepy;

        // ===================================================================
        // MAPPERY
        // ===================================================================

        private static DostawaItem MapDostawa(SqlDataReader r)
        {
            int O(string name) => r.GetOrdinal(name);
            decimal? Dec(int i) => r.IsDBNull(i) ? null : Convert.ToDecimal(r.GetValue(i));
            int Int(int i, int def = 0) => r.IsDBNull(i) ? def : Convert.ToInt32(r.GetValue(i));

            return new DostawaItem
            {
                ID = r.GetInt32(O("ID")),
                NrKursu = Int(O("NrKursu")),
                Data = r.GetDateTime(O("Data")),
                LpDostawy = r.IsDBNull(O("LpDostawy")) ? null : r.GetInt32(O("LpDostawy")),
                Hodowca = r.GetString(O("Hodowca")),
                HodowcaSkrot = r.GetString(O("HodowcaSkrot")),

                PlanSztukiLacznie = Int(O("PlanSztukiLacznie")),
                PlanKgLacznie = Dec(O("PlanKgLacznie")) ?? 0,
                AutaPlanowane = Int(O("AutaPlanowane"), 1),

                AutaZwazone = Int(O("AutaZwazone")),
                AutaOgolem = Int(O("AutaOgolem")),
                SztukiZwazoneSuma = Dec(O("SztukiZwazoneSuma")) ?? 0,
                KgZwazoneSuma = Dec(O("KgZwazoneSuma")) ?? 0,
                SztukiPozostalo = Dec(O("SztukiPozostalo")) ?? 0,
                KgPozostalo = Dec(O("KgPozostalo")) ?? 0,
                RealizacjaProc = Dec(O("RealizacjaProc")) ?? 0,
                TrendProc = Dec(O("TrendProc")) ?? 100,

                SztukiPlan = Int(O("SztukiPlan")),
                KgPlan = r.GetDecimal(O("KgPlan")),
                SredniaWagaPlan = Dec(O("SredniaWagaPlan")),
                WagaDeklHarmonogram = Dec(O("WagaDeklHarmonogram")),
                SztPojPlan = Dec(O("SztPojPlan")),

                Brutto = r.GetDecimal(O("Brutto")),
                Tara = r.GetDecimal(O("Tara")),
                KgRzeczywiste = r.GetDecimal(O("KgRzeczywiste")),
                SztukiRzeczywiste = r.GetInt32(O("SztukiRzeczywiste")),
                SredniaWagaRzeczywista = Dec(O("SredniaWagaRzeczywista")),
                SztPojRzecz = Dec(O("SztPojRzecz")),

                // NOWE (#2): rozdzielone odchylenia - dwie semantyki w osobnych polach
                OdchylenieVsPlanAutoKg = Dec(O("OdchylenieVsPlanAutoKg")),
                OdchylenieVsPlanAutoProc = Dec(O("OdchylenieVsPlanAutoProc")),
                OdchylenieVsDeklHodowcaKg = Dec(O("OdchylenieVsDeklHodowcaKg")),
                OdchylenieVsDeklHodowcaProc = Dec(O("OdchylenieVsDeklHodowcaProc")),

                OdchylenieWagi = Dec(O("OdchylenieWagi")),

                StatusId = r.GetInt32(O("StatusId")),
                Padle = r.GetInt32(O("Padle")),
                Konfiskaty = r.GetInt32(O("Konfiskaty")),
                Przyjazd = r.IsDBNull(O("Przyjazd")) ? null : r.GetDateTime(O("Przyjazd")),
                GodzinaWazenia = r.IsDBNull(O("GodzinaWazenia")) ? null : r.GetDateTime(O("GodzinaWazenia")),
                KtoWazyl = r.IsDBNull(O("KtoWazyl")) ? null : r.GetString(O("KtoWazyl")),

                SztukiExcel = Int(O("SztukiExcel"))
            };
        }

        private static void MapPodsumowanie(SqlDataReader r, PodsumowanieDnia p)
        {
            int O(string name) => r.GetOrdinal(name);
            decimal? Dec(int i) => r.IsDBNull(i) ? null : Convert.ToDecimal(r.GetValue(i));
            int Int(int i) => r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i));

            p.SztukiPlanSuma = Int(O("SztukiPlanSuma"));
            p.KgPlanSuma = Dec(O("KgPlanSuma")) ?? 0;
            p.SrWagaPlanSrednia = Dec(O("SrWagaPlanSrednia"));

            p.SztukiZwazoneSuma = Int(O("SztukiZwazoneSuma"));
            p.KgZwazoneSuma = Dec(O("KgZwazoneSuma")) ?? 0;
            p.SrWagaRzeczSrednia = Dec(O("SrWagaRzeczSrednia"));

            p.OdchylenieKgSuma = Dec(O("OdchylenieKgSuma")) ?? 0;
            p.KgPlanDoZwazonych = Dec(O("KgPlanDoZwazonych")) ?? 0;

            p.LiczbaDostawOgolem = Int(O("LiczbaDostawOgolem"));
            p.LiczbaZwazonych = Int(O("LiczbaZwazonych"));
            p.LiczbaCzekaNaTare = Int(O("LiczbaCzekaNaTare"));
            p.LiczbaOczekujacych = Int(O("LiczbaOczekujacych"));

            // Tempo: pierwsze/ostatnie wazenie dnia (do liczenia ETA, pace)
            p.PierwszeWazenie = r.IsDBNull(O("PierwszeWazenie")) ? null : r.GetDateTime(O("PierwszeWazenie"));
            p.OstatnieWazenie = r.IsDBNull(O("OstatnieWazenie")) ? null : r.GetDateTime(O("OstatnieWazenie"));
        }

        private static void MapPrognoza(SqlDataReader r, PrognozaDnia p)
        {
            int O(string name) => r.GetOrdinal(name);
            decimal Dec(int i) => r.IsDBNull(i) ? 0 : Convert.ToDecimal(r.GetValue(i));
            int Int(int i) => r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i));

            p.KgPlanLacznie = Dec(O("KgPlanLacznie"));
            p.KgZwazone = Dec(O("KgZwazone"));
            p.AutaZwazone = Int(O("AutaZwazone"));
            p.AutaOgolem = Int(O("AutaOgolem"));
        }

        private static PostepHarmonogramu MapPostep(SqlDataReader r)
        {
            int O(string name) => r.GetOrdinal(name);
            decimal? Dec(int i) => r.IsDBNull(i) ? null : Convert.ToDecimal(r.GetValue(i));
            int Int(int i, int def = 0) => r.IsDBNull(i) ? def : Convert.ToInt32(r.GetValue(i));

            return new PostepHarmonogramu
            {
                LpDostawy = r.GetInt32(O("LpDostawy")),
                Hodowca = r.GetString(O("Hodowca")),
                AutaZwazone = Int(O("AutaZwazone")),
                AutaOgolem = Int(O("AutaOgolem")),
                AutaPlanowane = Int(O("AutaPlanowane"), 1),
                PlanSztukiLacznie = Dec(O("PlanSztukiLacznie")) ?? 0,
                PlanKgLacznie = Dec(O("PlanKgLacznie")) ?? 0,
                SztukiZwazoneSuma = Dec(O("SztukiZwazoneSuma")) ?? 0,
                KgZwazoneSuma = Dec(O("KgZwazoneSuma")) ?? 0,
                SredniaWagaPlan = Dec(O("SredniaWagaPlan")),
                SredniaWagaRzecz = Dec(O("SredniaWagaRzecz")),
                PotwWaga = !r.IsDBNull(O("PotwWaga")) && r.GetBoolean(O("PotwWaga")),
                PotwSztuki = !r.IsDBNull(O("PotwSztuki")) && r.GetBoolean(O("PotwSztuki")),
                DataOstatniejZmiany = r.IsDBNull(O("DataOstatniejZmiany")) ? (DateTime?)null : r.GetDateTime(O("DataOstatniejZmiany")),
                RealniHodowcy = r.IsDBNull(O("RealniHodowcy")) ? "" : r.GetString(O("RealniHodowcy")),
                LiczbaRealnychHodowcow = r.IsDBNull(O("LiczbaRealnychHodowcow")) ? 0 : r.GetInt32(O("LiczbaRealnychHodowcow"))
            };
        }

        /// <summary>
        /// Faktyczny przychod produkcji (PWU) z Symfonii (HANDEL).
        /// Osobny round-trip - inna baza danych, opcjonalne dane.
        /// </summary>
        public async Task<(decimal KlasaA, decimal KlasaB)> GetFaktycznyPrzychodAsync(DateTime data, CancellationToken ct = default)
        {
            decimal faktA = 0, faktB = 0;

            try
            {
                using var conn = new SqlConnection(_handelConnectionString);
                await conn.OpenAsync(ct);
                using var cmd = new SqlCommand(_sqlFaktyczny.Value, conn);
                cmd.Parameters.AddWithValue("@Data", data.Date);
                cmd.CommandTimeout = 15;

                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    string klasa = reader.IsDBNull(0) ? "X" : reader.GetString(0);
                    decimal ilosc = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetValue(1));
                    if (klasa == "A") faktA = ilosc;
                    else if (klasa == "B") faktB = ilosc;
                }

                Debug.WriteLine($"[PrzychodService] Faktyczny przychod sPWU: A={faktA:N0} kg, B={faktB:N0} kg");
            }
            catch (Exception ex)
            {
                // Symfonia dane opcjonalne - zwracamy zera, UI pokaze "-"
                Debug.WriteLine($"[PrzychodService] Blad GetFaktycznyPrzychodAsync (Handel): {ex.Message}");
            }

            return (faktA, faktB);
        }

        /// <summary>
        /// Historia zmian wag i sztuk DEKLAROWANYCH dla danej daty (FarmerCalcChangeLog).
        /// TOP 100 najnowszych, filtruje 3 pola: Szt.Dek, Waga Brutto Hodowca, Waga Tara Hodowca.
        /// Osobny round-trip — wczytywane parallel z GetAllAsync.
        /// </summary>
        public async Task<List<HistoriaZmianItem>> GetHistoriaZmianAsync(DateTime data, CancellationToken ct = default)
        {
            var historia = new List<HistoriaZmianItem>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync(ct);
                using var cmd = new SqlCommand(_sqlHistoria.Value, conn);
                cmd.Parameters.AddWithValue("@Data", data.Date);
                cmd.CommandTimeout = 15;

                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    historia.Add(new HistoriaZmianItem
                    {
                        ChangedAt = reader.GetDateTime(0),
                        Hodowca = reader.GetString(1),
                        FieldName = reader.GetString(2),
                        OldValue = reader.GetString(3),
                        NewValue = reader.GetString(4),
                        UserName = reader.GetString(5)
                    });
                }
                Debug.WriteLine($"[PrzychodService] Historia zmian: {historia.Count} wpisow dla {data:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PrzychodService] Blad GetHistoriaZmianAsync: {ex.Message}");
                // Historia opcjonalna - sidebar pokaze pusta liste
            }
            return historia;
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PrzychodService] Test polaczenia nieudany: {ex.Message}");
                return false;
            }
        }

        // ===================================================================
        // DIAGNOSTYKA - per-kolumna test, used by BtnDiagnose w UI
        // ===================================================================

        public async Task<string> DiagnoseQueryAsync(DateTime data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== DIAGNOSTYKA ZAPYTANIA ===");
            sb.AppendLine($"Data: {data:yyyy-MM-dd}");
            sb.AppendLine();

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
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                sb.AppendLine("[OK] Polaczenie z baza: SUKCES");
                sb.AppendLine();

                // Test 1: liczba rekordow
                sb.AppendLine("--- TEST 1: Liczba rekordow w FarmerCalc ---");
                using (var cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM dbo.FarmerCalc WHERE CalcDate = @Data AND ISNULL(Deleted, 0) = 0", conn))
                {
                    cmd.Parameters.AddWithValue("@Data", data.Date);
                    var count = (int)await cmd.ExecuteScalarAsync();
                    sb.AppendLine($"[OK] Znaleziono {count} rekordow na date {data:yyyy-MM-dd}");
                }
                sb.AppendLine();

                // Test 2: per-kolumna
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

                        using var cmd = new SqlCommand(testQuery, conn);
                        cmd.Parameters.AddWithValue("@Data", data.Date);
                        var result = await cmd.ExecuteScalarAsync();
                        string v = result == null || result == DBNull.Value ? "NULL" : result.ToString();
                        if (v.Length > 50) v = v.Substring(0, 50) + "...";
                        sb.AppendLine($"[OK] {colName,-25} = {v}");
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"[BLAD] {colName,-25} = {ex.Message}");
                    }
                }
                sb.AppendLine();

                // Test 3: pelne GetAllAsync
                sb.AppendLine("--- TEST 3: Pelne GetAllAsync ---");
                try
                {
                    var snap = await GetAllAsync(data);
                    sb.AppendLine($"[OK] GetAllAsync OK: dostaw={snap.Dostawy.Count}, postepow={snap.Postepy.Count}");
                    sb.AppendLine($"     Plan: {snap.Podsumowanie.KgPlanSuma:N0} kg, Zwazone: {snap.Podsumowanie.KgZwazoneSuma:N0} kg");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"[BLAD] GetAllAsync: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[BLAD KRYTYCZNY] {ex.GetType().Name}: {ex.Message}");
                sb.AppendLine();
                sb.AppendLine("Stack trace:");
                sb.AppendLine(ex.StackTrace);
            }

            LastDiagnosticError = sb.ToString();
            return LastDiagnosticError;
        }
    }
}
