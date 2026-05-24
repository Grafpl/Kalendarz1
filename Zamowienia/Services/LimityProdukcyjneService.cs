using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kalendarz1.Zamowienia.Services
{
    /// <summary>
    /// Konfigurowalne limity produkcyjne (Kurczak A, Filet A, Ćwiartka, …).
    /// Tabela LimityProdukcyjne w LibraNet — admin może zarządzać przez Panel Admin → Limity produkcyjne.
    ///
    /// 3 sposoby liczenia planu na dany dzień ubojowy:
    ///   - HarmonogramTuszkaA : Plan = Σ(WagaDek × SztukiDek) × WspolczynnikTuszki% × ProcentTuszkaA%  (dla Kurczaka A)
    ///   - ProcentKurczakaA   : Plan = (PlanKurczakaA) × ProcentZKurczakaA%                              (dla Filet/Ćwiartka)
    ///   - Staly              : Plan = PlanStalyKg (sztywna wartość kg/dzień)
    /// </summary>
    public class LimityProdukcyjneService
    {
        public const string SposobHarmonogramTuszkaA = "HarmonogramTuszkaA";
        public const string SposobProcentKurczakaA = "ProcentKurczakaA";
        public const string SposobStaly = "Staly";
        public const string SposobKonfiguracjaProduktow = "KonfiguracjaProduktow";  // ten sam wzór co "Podsumowanie dnia" w MainWindow

        private readonly string _connLibra;
        private readonly string _connHandel;
        private static readonly System.Threading.SemaphoreSlim _initLock = new(1, 1);
        private static volatile bool _initialized;

        // Domyślny HANDEL conn — używany gdy konstruktor woła wyłącznie z connLibra (backward compat)
        private const string DefaultConnHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        public LimityProdukcyjneService(string connLibra) : this(connLibra, DefaultConnHandel) { }
        public LimityProdukcyjneService(string connLibra, string connHandel)
        {
            _connLibra = connLibra;
            _connHandel = connHandel;
        }

        // ── DDL + seed ──────────────────────────────────────────────────────
        public async Task EnsureSchemaAsync()
        {
            if (_initialized) return;
            await _initLock.WaitAsync();
            try
            {
                if (_initialized) return;
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                const string ddl = @"
                    IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name='LimityProdukcyjne' AND type='U')
                    BEGIN
                        CREATE TABLE dbo.LimityProdukcyjne (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            NazwaGrupy NVARCHAR(100) NOT NULL,
                            Wzorzec NVARCHAR(200) NOT NULL,
                            SposobLiczeniaPlanu NVARCHAR(50) NOT NULL DEFAULT 'HarmonogramTuszkaA',
                            ProcentZKurczakaA DECIMAL(5,2) NULL,
                            PlanStalyKg DECIMAL(18,2) NULL,
                            ProcentLimitu DECIMAL(5,2) NOT NULL DEFAULT 92.00,
                            Aktywny BIT NOT NULL DEFAULT 1,
                            Ikona NVARCHAR(10) NULL,
                            Kolejnosc INT NOT NULL DEFAULT 0,
                            DataUtworzenia DATETIME NOT NULL DEFAULT GETDATE(),
                            UserUtworzenia NVARCHAR(50) NULL,
                            DataModyfikacji DATETIME NULL,
                            UserModyfikacji NVARCHAR(50) NULL
                        );
                        CREATE INDEX IX_LimityProdukcyjne_Aktywny ON dbo.LimityProdukcyjne(Aktywny, Kolejnosc);

                        -- Seed: Kurczak A 92% (domyślny limit)
                        INSERT INTO dbo.LimityProdukcyjne (NazwaGrupy, Wzorzec, SposobLiczeniaPlanu, ProcentLimitu, Ikona, Kolejnosc)
                        VALUES (N'Kurczak A', N'KURCZAK A', N'HarmonogramTuszkaA', 92.00, N'🍗', 1);
                    END;";
                await using var cmd = new SqlCommand(ddl, cn);
                await cmd.ExecuteNonQueryAsync();
                _initialized = true;
            }
            finally { _initLock.Release(); }
        }

        // ── CRUD ────────────────────────────────────────────────────────────
        public async Task<List<LimitProdukcyjny>> GetAllAsync(bool tylkoAktywne = false)
        {
            await EnsureSchemaAsync();
            var list = new List<LimitProdukcyjny>();
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            string sql = @"SELECT Id, NazwaGrupy, Wzorzec, SposobLiczeniaPlanu, ProcentZKurczakaA,
                                  PlanStalyKg, ProcentLimitu, Aktywny, ISNULL(Ikona,'🍗') AS Ikona,
                                  Kolejnosc, DataUtworzenia, UserUtworzenia, DataModyfikacji, UserModyfikacji
                           FROM dbo.LimityProdukcyjne
                           " + (tylkoAktywne ? "WHERE Aktywny = 1 " : "") +
                           "ORDER BY Kolejnosc, NazwaGrupy";
            await using var cmd = new SqlCommand(sql, cn);
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                list.Add(new LimitProdukcyjny
                {
                    Id = rd.GetInt32(0),
                    NazwaGrupy = rd.GetString(1),
                    Wzorzec = rd.GetString(2),
                    SposobLiczeniaPlanu = rd.GetString(3),
                    ProcentZKurczakaA = rd.IsDBNull(4) ? null : rd.GetDecimal(4),
                    PlanStalyKg = rd.IsDBNull(5) ? null : rd.GetDecimal(5),
                    ProcentLimitu = rd.GetDecimal(6),
                    Aktywny = rd.GetBoolean(7),
                    Ikona = rd.GetString(8),
                    Kolejnosc = rd.GetInt32(9),
                    DataUtworzenia = rd.GetDateTime(10),
                    UserUtworzenia = rd.IsDBNull(11) ? null : rd.GetString(11),
                    DataModyfikacji = rd.IsDBNull(12) ? null : rd.GetDateTime(12),
                    UserModyfikacji = rd.IsDBNull(13) ? null : rd.GetString(13)
                });
            }
            return list;
        }

        public async Task<int> AddAsync(LimitProdukcyjny m, string userId)
        {
            await EnsureSchemaAsync();
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            const string sql = @"
                INSERT INTO dbo.LimityProdukcyjne
                    (NazwaGrupy, Wzorzec, SposobLiczeniaPlanu, ProcentZKurczakaA, PlanStalyKg,
                     ProcentLimitu, Aktywny, Ikona, Kolejnosc, UserUtworzenia)
                VALUES
                    (@n, @w, @s, @pk, @ps, @pl, @a, @i, @k, @u);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";
            await using var cmd = new SqlCommand(sql, cn);
            BindParameters(cmd, m, userId);
            var r = await cmd.ExecuteScalarAsync();
            return r == null ? 0 : Convert.ToInt32(r);
        }

        public async Task UpdateAsync(LimitProdukcyjny m, string userId)
        {
            await EnsureSchemaAsync();
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            const string sql = @"
                UPDATE dbo.LimityProdukcyjne SET
                    NazwaGrupy = @n,
                    Wzorzec = @w,
                    SposobLiczeniaPlanu = @s,
                    ProcentZKurczakaA = @pk,
                    PlanStalyKg = @ps,
                    ProcentLimitu = @pl,
                    Aktywny = @a,
                    Ikona = @i,
                    Kolejnosc = @k,
                    DataModyfikacji = GETDATE(),
                    UserModyfikacji = @u
                WHERE Id = @id";
            await using var cmd = new SqlCommand(sql, cn);
            BindParameters(cmd, m, userId);
            cmd.Parameters.AddWithValue("@id", m.Id);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteAsync(int id)
        {
            await EnsureSchemaAsync();
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand("DELETE FROM dbo.LimityProdukcyjne WHERE Id = @id", cn);
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        private static void BindParameters(SqlCommand cmd, LimitProdukcyjny m, string userId)
        {
            cmd.Parameters.AddWithValue("@n", m.NazwaGrupy ?? "");
            cmd.Parameters.AddWithValue("@w", m.Wzorzec ?? "");
            cmd.Parameters.AddWithValue("@s", m.SposobLiczeniaPlanu ?? SposobHarmonogramTuszkaA);
            cmd.Parameters.AddWithValue("@pk", (object?)m.ProcentZKurczakaA ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ps", (object?)m.PlanStalyKg ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pl", m.ProcentLimitu);
            cmd.Parameters.AddWithValue("@a", m.Aktywny);
            cmd.Parameters.AddWithValue("@i", (object?)m.Ikona ?? "🍗");
            cmd.Parameters.AddWithValue("@k", m.Kolejnosc);
            cmd.Parameters.AddWithValue("@u", userId ?? "");
        }

        // ── Evaluation per dzień ────────────────────────────────────────────
        public class TowarKoszyka
        {
            public int Id { get; set; }
            public string Kod { get; set; } = "";
            public decimal QtyKg { get; set; }
        }

        public class GroupEvaluation
        {
            public LimitProdukcyjny Definicja { get; set; } = null!;
            public List<TowarKoszyka> Matching { get; set; } = new();  // towary z koszyka pasujące do wzorca
            public decimal PlanKg { get; set; }
            public decimal StanKg { get; set; }
            public decimal SumaInnychKg { get; set; }            // suma w innych zamówieniach (z wykluczeniem _editOrderId)
            public decimal SumaWKoszyku => Matching.Sum(m => m.QtyKg);
            public decimal LimitKg => Math.Round((PlanKg + StanKg) * (Definicja.ProcentLimitu / 100m), 0);
            public decimal PrzekroczenieKg => Math.Max(0, SumaInnychKg + SumaWKoszyku - LimitKg);
            public bool DaneBrakuje => PlanKg <= 0;
            public bool Przekroczony => !DaneBrakuje && PrzekroczenieKg > 0;
        }

        /// <summary>
        /// Liczy plan + stan + sumę innych zamówień dla WSZYSTKICH aktywnych grup naraz.
        /// Łączy zapytania (1 połączenie, kilka SQL) — wydajne dla walidacji po LostFocus.
        /// </summary>
        public async Task<List<GroupEvaluation>> EvaluateAllAsync(
            DateTime dataUboju,
            IReadOnlyList<TowarKoszyka> koszyk,
            int? excludeOrderId)
        {
            var groups = await GetAllAsync(tylkoAktywne: true);
            var results = new List<GroupEvaluation>();
            if (groups.Count == 0) return results;

            // Inicjuj evaluation per grupa + przyporządkuj towary z koszyka
            foreach (var g in groups)
            {
                var eval = new GroupEvaluation { Definicja = g };
                foreach (var t in koszyk)
                {
                    if (MatchesPattern(t.Kod, g.Wzorzec))
                        eval.Matching.Add(t);
                }
                results.Add(eval);
            }

            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            // ── 1) Konfiguracja wydajności ──
            decimal wspTuszki = 78m, procentA = 85m, procentB = 15m;
            try
            {
                await using var cmd = new SqlCommand(@"
                    SELECT TOP 1 WspolczynnikTuszki, ProcentTuszkaA, ProcentTuszkaB
                    FROM KonfiguracjaWydajnosci
                    WHERE DataOd <= @D AND Aktywny = 1
                    ORDER BY DataOd DESC", cn);
                cmd.Parameters.AddWithValue("@D", dataUboju.Date);
                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    wspTuszki = Convert.ToDecimal(rd["WspolczynnikTuszki"]);
                    procentA = Convert.ToDecimal(rd["ProcentTuszkaA"]);
                    procentB = Convert.ToDecimal(rd["ProcentTuszkaB"]);
                }
            }
            catch { }

            // ── 2) Plan Kurczaka A (potrzebny też do ProcentKurczakaA) ──
            decimal totalZywiec = 0m;
            await using (var cmd = new SqlCommand(@"
                SELECT WagaDek, SztukiDek FROM dbo.HarmonogramDostaw
                WHERE DataOdbioru = @D AND Bufor IN ('B.Wolny','B.Kontr.','Potwierdzony')", cn) { CommandTimeout = 10 })
            {
                cmd.Parameters.AddWithValue("@D", dataUboju.Date);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var wagaDek = rd.IsDBNull(0) ? 0m : Convert.ToDecimal(rd.GetValue(0));
                    var sztukiDek = rd.IsDBNull(1) ? 0m : Convert.ToDecimal(rd.GetValue(1));
                    totalZywiec += wagaDek * sztukiDek;
                }
            }
            decimal planKurczakaA = totalZywiec * (wspTuszki / 100m) * (procentA / 100m);
            decimal pulaTuszkiB = totalZywiec * (wspTuszki / 100m) * (procentB / 100m);  // dla sposobu KonfiguracjaProduktow (jak panel)

            // ── 3) Plan per grupa (na podstawie SposobLiczeniaPlanu) ──
            // Dla SposobKonfiguracjaProduktow potrzebujemy procentów z LibraNet.KonfiguracjaProduktow
            Dictionary<int, decimal>? konfiguracjaProduktow = null;

            foreach (var eval in results)
            {
                switch (eval.Definicja.SposobLiczeniaPlanu)
                {
                    case SposobHarmonogramTuszkaA:
                        eval.PlanKg = planKurczakaA;
                        break;
                    case SposobProcentKurczakaA:
                        eval.PlanKg = planKurczakaA * ((eval.Definicja.ProcentZKurczakaA ?? 0m) / 100m);
                        break;
                    case SposobStaly:
                        eval.PlanKg = eval.Definicja.PlanStalyKg ?? 0m;
                        break;
                    case SposobKonfiguracjaProduktow:
                        // Lazy load konfiguracji raz dla wszystkich grup
                        if (konfiguracjaProduktow == null)
                            konfiguracjaProduktow = await LoadKonfiguracjaProduktowAsync(cn, dataUboju);
                        // Plan = pulaTuszkiB × Σ(ProcentUdzialu dla wszystkich TowarID pasujących do wzorca)
                        // (dokładnie ta sama logika co MainWindow → Podsumowanie dnia)
                        eval.PlanKg = 0m;
                        break;
                    default:
                        eval.PlanKg = 0m;
                        break;
                }
            }

            // ── 4) Resolve product IDs from HANDEL HM.TW matching wzorzec ──
            // KRYTYCZNE: ZamowieniaMiesoTowar.KodTowaru = HM.TW.id (HANDEL), NIE Article.ID (LibraNet).
            // Dlatego matching musi iść po HM.TW.kod, nie Article.Name.
            var wzorceDoIds = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in groups)
                if (!wzorceDoIds.ContainsKey(g.Wzorzec))
                    wzorceDoIds[g.Wzorzec] = new List<int>();

            try
            {
                await using var cnH = new SqlConnection(_connHandel);
                await cnH.OpenAsync();
                foreach (var w in wzorceDoIds.Keys.ToList())
                {
                    try
                    {
                        await using var cmd = new SqlCommand(
                            "SELECT id FROM [HANDEL].[HM].[TW] WHERE kod LIKE @p", cnH);
                        cmd.Parameters.AddWithValue("@p", "%" + (w ?? "") + "%");
                        await using var rd = await cmd.ExecuteReaderAsync();
                        var ids = new List<int>();
                        while (await rd.ReadAsync()) ids.Add(rd.GetInt32(0));
                        wzorceDoIds[w] = ids;
                    }
                    catch { wzorceDoIds[w] = new List<int>(); }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Limit HM.TW match] {ex.Message}"); }

            // ── 4b) Plan dla SposobKonfiguracjaProduktow (po pobraniu IDs i konfiguracji) ──
            if (konfiguracjaProduktow != null)
            {
                foreach (var eval in results)
                {
                    if (eval.Definicja.SposobLiczeniaPlanu != SposobKonfiguracjaProduktow) continue;
                    if (!wzorceDoIds.TryGetValue(eval.Definicja.Wzorzec, out var ids) || ids.Count == 0) continue;
                    decimal sumaProcent = 0m;
                    foreach (var id in ids)
                        if (konfiguracjaProduktow.TryGetValue(id, out var p)) sumaProcent += p;
                    eval.PlanKg = pulaTuszkiB * (sumaProcent / 100m);
                }
            }

            // ── 5) Stan z StanyMagazynowe per grupa ──
            foreach (var eval in results)
            {
                if (!wzorceDoIds.TryGetValue(eval.Definicja.Wzorzec, out var ids) || ids.Count == 0)
                    continue;
                try
                {
                    string idsCsv = string.Join(",", ids);
                    string sqlStan = $@"SELECT ISNULL(SUM(Stan), 0) FROM dbo.StanyMagazynowe
                                        WHERE Data = @D AND ProduktId IN ({idsCsv})";
                    await using var cmd = new SqlCommand(sqlStan, cn);
                    cmd.Parameters.AddWithValue("@D", dataUboju.Date);
                    var r = await cmd.ExecuteScalarAsync();
                    if (r != null && r != DBNull.Value) eval.StanKg = Convert.ToDecimal(r);
                }
                catch { }
            }

            // ── 6) Suma innych zamówień (z wykluczeniem _editOrderId) ──
            bool hasDataUboju = await ColumnExistsAsync(cn, "ZamowieniaMieso", "DataUboju");
            string dateCol = hasDataUboju ? "DataUboju" : "DataZamowienia";

            foreach (var eval in results)
            {
                if (!wzorceDoIds.TryGetValue(eval.Definicja.Wzorzec, out var ids) || ids.Count == 0)
                    continue;
                try
                {
                    string idsCsv = string.Join(",", ids);
                    string excludeClause = excludeOrderId.HasValue ? $" AND zt.ZamowienieId <> {excludeOrderId.Value}" : "";
                    string sql = $@"
                        SELECT ISNULL(SUM(zt.Ilosc), 0)
                        FROM dbo.ZamowieniaMiesoTowar zt
                        INNER JOIN dbo.ZamowieniaMieso z ON z.Id = zt.ZamowienieId
                        WHERE CAST(z.{dateCol} AS DATE) = @D
                          AND ISNULL(z.Status, '') NOT IN ('Anulowane', 'Anulowano')
                          AND zt.KodTowaru IN ({idsCsv})
                          {excludeClause}";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@D", dataUboju.Date);
                    var r = await cmd.ExecuteScalarAsync();
                    if (r != null && r != DBNull.Value) eval.SumaInnychKg = Convert.ToDecimal(r);
                }
                catch { }
            }

            return results;
        }

        public static bool MatchesPattern(string kod, string wzorzec)
        {
            if (string.IsNullOrWhiteSpace(kod) || string.IsNullOrWhiteSpace(wzorzec)) return false;
            return kod.IndexOf(wzorzec, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Pobiera KonfiguracjaProduktow z LibraNet — identyczne źródło z MainWindow.GetKonfiguracjaProduktowAsync.
        // Zwraca Dictionary<TowarID, ProcentUdzialu>. Procenty per HM.TW.id dla najnowszej aktywnej konfiguracji.
        private static async Task<Dictionary<int, decimal>> LoadKonfiguracjaProduktowAsync(SqlConnection cn, DateTime data)
        {
            var result = new Dictionary<int, decimal>();
            try
            {
                const string sql = @"
                    SELECT kp.TowarID, kp.ProcentUdzialu
                    FROM KonfiguracjaProduktow kp
                    INNER JOIN (
                        SELECT MAX(DataOd) as MaxData FROM KonfiguracjaProduktow
                        WHERE DataOd <= @Data AND Aktywny = 1
                    ) sub ON kp.DataOd = sub.MaxData
                    WHERE kp.Aktywny = 1";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Data", data.Date);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int towarId = Convert.ToInt32(rd["TowarID"]);
                    decimal procent = Convert.ToDecimal(rd["ProcentUdzialu"]);
                    result[towarId] = procent;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[KonfigProd] {ex.Message}"); }
            return result;
        }

        public class PreviewProdukt
        {
            public int Id { get; set; }
            public string Kod { get; set; } = "";
            public string Nazwa { get; set; } = "";
            public System.Windows.Media.ImageSource? Image { get; set; }
            public System.Windows.Visibility HasImageVisibility =>
                Image != null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            public System.Windows.Visibility PlaceholderVisibility =>
                Image == null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        /// <summary>Zwraca wszystkie towary z HANDEL HM.TW pasujące do wzorca (do podglądu w admin window).
        /// Identyczne źródło z runtime matching → admin widzi dokładnie te same towary które walidacja zlicza.</summary>
        public async Task<List<PreviewProdukt>> PreviewMatchingArticlesAsync(string wzorzec)
        {
            var result = new List<PreviewProdukt>();
            if (string.IsNullOrWhiteSpace(wzorzec)) return result;
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(
                    "SELECT TOP 200 id, ISNULL(kod,'') AS Nazwa, CAST(katalog AS NVARCHAR(32)) AS Katalog " +
                    "FROM [HANDEL].[HM].[TW] WHERE kod LIKE @p ORDER BY kod", cn);
                cmd.Parameters.AddWithValue("@p", "%" + wzorzec + "%");
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    result.Add(new PreviewProdukt
                    {
                        Id = rd.GetInt32(0),
                        Kod = rd.IsDBNull(2) ? "" : rd.GetString(2),  // katalog jako "kod" pomocniczy
                        Nazwa = rd.GetString(1)
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PreviewArticles] {ex.Message}"); }
            return result;
        }

        private static async Task<bool> ColumnExistsAsync(SqlConnection cn, string tbl, string col)
        {
            try
            {
                await using var cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(@t) AND name = @c", cn);
                cmd.Parameters.AddWithValue("@t", $"dbo.{tbl}");
                cmd.Parameters.AddWithValue("@c", col);
                var r = await cmd.ExecuteScalarAsync();
                return r != null && Convert.ToInt32(r) > 0;
            }
            catch { return false; }
        }
    }

    public class LimitProdukcyjny
    {
        public int Id { get; set; }
        public string NazwaGrupy { get; set; } = "";
        public string Wzorzec { get; set; } = "";
        public string SposobLiczeniaPlanu { get; set; } = LimityProdukcyjneService.SposobHarmonogramTuszkaA;
        public decimal? ProcentZKurczakaA { get; set; }
        public decimal? PlanStalyKg { get; set; }
        public decimal ProcentLimitu { get; set; } = 92m;
        public bool Aktywny { get; set; } = true;
        public string Ikona { get; set; } = "🍗";
        public int Kolejnosc { get; set; }
        public DateTime DataUtworzenia { get; set; }
        public string? UserUtworzenia { get; set; }
        public DateTime? DataModyfikacji { get; set; }
        public string? UserModyfikacji { get; set; }

        public string SposobDisplay => SposobLiczeniaPlanu switch
        {
            LimityProdukcyjneService.SposobHarmonogramTuszkaA => "Plan z HarmonogramDostaw × WspTuszki × %TuszkaA",
            LimityProdukcyjneService.SposobProcentKurczakaA   => $"{ProcentZKurczakaA:N1}% planu Kurczaka A",
            LimityProdukcyjneService.SposobStaly              => $"Stała wartość {PlanStalyKg:N0} kg/dzień",
            LimityProdukcyjneService.SposobKonfiguracjaProduktow => "Z konfiguracji produktów (zgodnie z Podsumowaniem dnia)",
            _ => SposobLiczeniaPlanu
        };
    }
}
