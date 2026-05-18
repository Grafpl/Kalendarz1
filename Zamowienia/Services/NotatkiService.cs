using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Kalendarz1.Zamowienia.Services
{
    /// <summary>
    /// Inteligentne propozycje notatek dla okna zamówień.
    /// Łączy: szablony tworzone przez handlowców + historię zamówień + ranking
    /// uwzględniający klienta, ostatnie użycie, częstotliwość, koszyk i pin.
    /// </summary>
    public class NotatkiService
    {
        private readonly string _connLibra;
        public NotatkiService(string connLibra) { _connLibra = connLibra; }

        public const string KategoriaCiecie = "Cięcie";
        public const string KategoriaKaliber = "Kaliber";
        public const string KategoriaTransport = "Transport";
        public const string KategoriaJakosc = "Jakość";
        public const string KategoriaPakowanie = "Pakowanie";
        public const string KategoriaInne = "Inne";
        public static readonly string[] Kategorie = { KategoriaCiecie, KategoriaKaliber, KategoriaTransport, KategoriaJakosc, KategoriaPakowanie, KategoriaInne };

        public const string ZakresGlobalny = "Globalny";
        public const string ZakresPerKlient = "PerKlient";
        public const string ZakresPerHandlowiec = "PerHandlowiec";
        public static readonly string[] Zakresy = { ZakresGlobalny, ZakresPerKlient, ZakresPerHandlowiec };

        public const string AkcjaWstawiona = "Wstawiona";
        public const string AkcjaWpisana = "Wpisana";

        // ════════════════════════════════════════════════════════════════════
        // SCHEMA — auto-tworzenie i backfill
        // ════════════════════════════════════════════════════════════════════
        public async Task EnsureSchemaAsync()
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            const string ddl = @"
                IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name='NotatkiSzablony' AND type='U')
                BEGIN
                    CREATE TABLE dbo.NotatkiSzablony (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Tekst NVARCHAR(500) NOT NULL,
                        Kategoria NVARCHAR(40) NULL,
                        Zakres NVARCHAR(20) NOT NULL DEFAULT 'Globalny',
                        KlientId INT NULL,
                        UserId NVARCHAR(50) NULL,
                        Pinowane BIT NOT NULL DEFAULT 0,
                        LiczbaUzyc INT NOT NULL DEFAULT 0,
                        OstatnieUzycie DATETIME NULL,
                        UtworzonoPrzez NVARCHAR(50) NULL,
                        UtworzonoTsmp DATETIME NOT NULL DEFAULT GETDATE(),
                        Aktywne BIT NOT NULL DEFAULT 1
                    );
                    CREATE INDEX IX_NotatkiSzablony_Zakres ON dbo.NotatkiSzablony(Zakres, Aktywne);
                    CREATE INDEX IX_NotatkiSzablony_Klient ON dbo.NotatkiSzablony(KlientId, Aktywne);
                    CREATE INDEX IX_NotatkiSzablony_User ON dbo.NotatkiSzablony(UserId, Aktywne);
                END;
                IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name='NotatkiUzycia' AND type='U')
                BEGIN
                    CREATE TABLE dbo.NotatkiUzycia (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Tekst NVARCHAR(500) NOT NULL,
                        KlientId INT NULL,
                        UserId NVARCHAR(50) NULL,
                        Akcja NVARCHAR(20) NOT NULL,
                        TowaryKody NVARCHAR(500) NULL,
                        SzablonId INT NULL,
                        DataAkcji DATETIME NOT NULL DEFAULT GETDATE()
                    );
                    CREATE INDEX IX_NotatkiUzycia_Tekst ON dbo.NotatkiUzycia(Tekst);
                    CREATE INDEX IX_NotatkiUzycia_Klient ON dbo.NotatkiUzycia(KlientId);
                END;";
            await using var cmd = new SqlCommand(ddl, cn);
            await cmd.ExecuteNonQueryAsync();

            // Backfill TOP 30 z historii (jeśli tabela szablonów pusta)
            const string backfillCheck = "SELECT COUNT(*) FROM dbo.NotatkiSzablony";
            await using (var c = new SqlCommand(backfillCheck, cn))
            {
                int existing = Convert.ToInt32(await c.ExecuteScalarAsync());
                if (existing > 0) return;
            }

            const string backfill = @"
                INSERT INTO dbo.NotatkiSzablony (Tekst, Kategoria, Zakres, Pinowane, LiczbaUzyc, UtworzonoPrzez, Aktywne)
                SELECT TOP 30
                    LTRIM(RTRIM(Uwagi)) AS Tekst,
                    NULL AS Kategoria,
                    'Globalny' AS Zakres,
                    0 AS Pinowane,
                    COUNT(*) AS LiczbaUzyc,
                    'system-backfill' AS UtworzonoPrzez,
                    1 AS Aktywne
                FROM dbo.ZamowieniaMieso
                WHERE Uwagi IS NOT NULL
                  AND LTRIM(RTRIM(Uwagi)) <> ''
                  AND DataPrzyjazdu > DATEADD(MONTH, -6, GETDATE())
                  AND ISNULL(Status, '') NOT IN ('Anulowane', 'Anulowano')
                  AND LEN(LTRIM(RTRIM(Uwagi))) BETWEEN 4 AND 200
                GROUP BY LTRIM(RTRIM(Uwagi))
                HAVING COUNT(*) >= 5
                ORDER BY COUNT(*) DESC";
            await using (var c = new SqlCommand(backfill, cn))
            {
                try { await c.ExecuteNonQueryAsync(); } catch { /* best-effort */ }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // SUGESTIE — smart ranking
        // ════════════════════════════════════════════════════════════════════
        public class SuggestionVm
        {
            public int? SzablonId { get; set; }            // null = z historii (nie z szablonów)
            public string Text { get; set; } = "";
            public string Display { get; set; } = "";       // ucięty tekst do chipa
            public string Tooltip { get; set; } = "";
            public string Source { get; set; } = "";        // "Pin" / "Klient" / "Towary" / "Globalne" / "Historia"
            public string ChipColor { get; set; } = "";     // hex
            public string ChipBorder { get; set; } = "";    // hex
            public string Icon { get; set; } = "";
            public double Score { get; set; }
            public int CountTotal { get; set; }
            public bool Pinned { get; set; }
            public string Kategoria { get; set; } = "";
        }

        public async Task<List<SuggestionVm>> GetSuggestionsAsync(int klientId, string userId, IEnumerable<int> kodyTowarowWKoszyku, int maxResults = 18)
        {
            var koszyk = new HashSet<int>(kodyTowarowWKoszyku ?? Array.Empty<int>());
            var byText = new Dictionary<string, SuggestionVm>(StringComparer.OrdinalIgnoreCase);

            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            // 1) Szablony aktywne (właściwego zakresu)
            const string sqlSzab = @"
                SELECT Id, Tekst, ISNULL(Kategoria,'') AS Kategoria, Zakres, KlientId, UserId,
                       Pinowane, LiczbaUzyc, OstatnieUzycie
                FROM dbo.NotatkiSzablony
                WHERE Aktywne = 1
                  AND (Zakres = 'Globalny'
                       OR (Zakres = 'PerKlient' AND KlientId = @kid)
                       OR (Zakres = 'PerHandlowiec' AND UserId = @uid))";
            await using (var cmd = new SqlCommand(sqlSzab, cn))
            {
                cmd.Parameters.AddWithValue("@kid", klientId);
                cmd.Parameters.AddWithValue("@uid", userId ?? "");
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    string text = rd["Tekst"]?.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    var vm = new SuggestionVm
                    {
                        SzablonId = Convert.ToInt32(rd["Id"]),
                        Text = text,
                        Kategoria = rd["Kategoria"]?.ToString() ?? "",
                        Pinned = Convert.ToBoolean(rd["Pinowane"]),
                        CountTotal = Convert.ToInt32(rd["LiczbaUzyc"])
                    };
                    string zakres = rd["Zakres"]?.ToString() ?? "";
                    DateTime? ost = rd["OstatnieUzycie"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["OstatnieUzycie"]);
                    vm.Score = ScoreFromTemplate(vm, zakres, ost);
                    byText[NormalizeKey(text)] = vm;
                }
            }

            // 2) Historia: aggregacja per notatka — total, dla tego klienta, dla tego usera, ostatnio
            const string sqlHist = @"
                SELECT TOP 100 LTRIM(RTRIM(Uwagi)) AS Tekst,
                       COUNT(*) AS Total,
                       SUM(CASE WHEN KlientId = @kid THEN 1 ELSE 0 END) AS DlaKlienta,
                       SUM(CASE WHEN IdUser = @uid THEN 1 ELSE 0 END) AS DlaUsera,
                       MAX(DataPrzyjazdu) AS Ostatnio
                FROM dbo.ZamowieniaMieso
                WHERE Uwagi IS NOT NULL AND LTRIM(RTRIM(Uwagi)) <> ''
                  AND DataPrzyjazdu > DATEADD(MONTH, -6, GETDATE())
                  AND ISNULL(Status, '') NOT IN ('Anulowane', 'Anulowano')
                  AND LEN(LTRIM(RTRIM(Uwagi))) BETWEEN 3 AND 250
                GROUP BY LTRIM(RTRIM(Uwagi))
                HAVING COUNT(*) >= 1
                ORDER BY MAX(DataPrzyjazdu) DESC";
            var historiaTeksty = new List<string>();
            await using (var cmd = new SqlCommand(sqlHist, cn))
            {
                cmd.Parameters.AddWithValue("@kid", klientId);
                cmd.Parameters.AddWithValue("@uid", userId ?? "");
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    string text = rd["Tekst"]?.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    int total = Convert.ToInt32(rd["Total"]);
                    int dlaKl = Convert.ToInt32(rd["DlaKlienta"]);
                    int dlaUs = Convert.ToInt32(rd["DlaUsera"]);
                    DateTime ost = rd["Ostatnio"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(rd["Ostatnio"]);

                    string key = NormalizeKey(text);
                    historiaTeksty.Add(text);

                    if (byText.TryGetValue(key, out var existing))
                    {
                        // Wzbogać istniejący szablon o sygnał z historii (klient/user match)
                        existing.Score *= ScoreHistorySignals(dlaKl, dlaUs, ost, total);
                        if (existing.CountTotal < total) existing.CountTotal = total;
                    }
                    else
                    {
                        var vm = new SuggestionVm
                        {
                            Text = text,
                            CountTotal = total,
                            Score = ScoreFromHistory(total, dlaKl, dlaUs, ost)
                        };
                        byText[key] = vm;
                    }
                }
            }

            // 3) Towar-match: dla TOP-N kandydatów po prelim score, dolicz boost z koszyka
            //    (znajdź jakie towary były z każdą z TOP notatek; jeśli pokrywają się z koszykiem → boost)
            if (koszyk.Count > 0 && historiaTeksty.Count > 0)
            {
                // Liczymy tylko dla TOP 50 kandydatów po prelim score (oszczędność)
                var topKandydaci = byText.Values
                    .OrderByDescending(s => s.Score)
                    .Take(50)
                    .Select(s => s.Text)
                    .ToList();

                var towaryPerNotatka = await LoadTowaryPerNotatkaAsync(cn, topKandydaci);
                foreach (var s in byText.Values)
                {
                    if (!towaryPerNotatka.TryGetValue(NormalizeKey(s.Text), out var towary)) continue;
                    if (towary.Count == 0) continue;
                    double jaccard = JaccardSimilarity(koszyk, towary);
                    if (jaccard > 0)
                    {
                        s.Score *= (1.0 + 1.5 * jaccard);   // max ×2.5 przy idealnym dopasowaniu
                        s.Source = "Towary";
                    }
                }
            }

            // 4) Sortuj i przygotuj displayy + chip kolory + tooltipy
            var result = byText.Values
                .OrderByDescending(s => s.Pinned ? 1 : 0)
                .ThenByDescending(s => s.Score)
                .Take(maxResults)
                .ToList();

            foreach (var s in result)
                FillDisplayAndStyle(s, klientId);

            return result;
        }

        private async Task<Dictionary<string, HashSet<int>>> LoadTowaryPerNotatkaAsync(SqlConnection cn, List<string> teksty)
        {
            var result = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
            if (teksty.Count == 0) return result;

            // Buduj parametr listę. SQL Server nie ma natywnego "IN (@list)", więc parametry @t0,@t1,...
            var paramNames = new List<string>();
            for (int i = 0; i < teksty.Count; i++) paramNames.Add("@t" + i);
            string sql = $@"
                SELECT LTRIM(RTRIM(zm.Uwagi)) AS Tekst, zt.KodTowaru, COUNT(*) AS WspolnieIle
                FROM dbo.ZamowieniaMieso zm
                INNER JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = zm.Id
                WHERE LTRIM(RTRIM(zm.Uwagi)) IN ({string.Join(",", paramNames)})
                  AND zm.DataPrzyjazdu > DATEADD(MONTH, -6, GETDATE())
                  AND ISNULL(zm.Status,'') NOT IN ('Anulowane','Anulowano')
                  AND zt.Ilosc > 0
                GROUP BY LTRIM(RTRIM(zm.Uwagi)), zt.KodTowaru
                HAVING COUNT(*) >= 1";

            await using var cmd = new SqlCommand(sql, cn);
            for (int i = 0; i < teksty.Count; i++) cmd.Parameters.AddWithValue(paramNames[i], teksty[i]);
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                string tekst = rd["Tekst"]?.ToString() ?? "";
                if (!int.TryParse(rd["KodTowaru"]?.ToString(), out int kod)) continue;
                string key = NormalizeKey(tekst);
                if (!result.TryGetValue(key, out var set))
                {
                    set = new HashSet<int>();
                    result[key] = set;
                }
                set.Add(kod);
            }
            return result;
        }

        // ════════════════════════════════════════════════════════════════════
        // RANKING — funkcje wyniku
        // ════════════════════════════════════════════════════════════════════
        private static double ScoreFromTemplate(SuggestionVm vm, string zakres, DateTime? ostatnio)
        {
            double score = 1.0;
            // Pin → najsilniejszy boost
            if (vm.Pinned) score *= 5.0;
            // Zakres węższy = bardziej trafny
            if (zakres == ZakresPerKlient) score *= 4.0;
            else if (zakres == ZakresPerHandlowiec) score *= 2.5;
            // Recency tej sugestii (jeśli była używana)
            if (ostatnio.HasValue)
            {
                double dni = (DateTime.Now - ostatnio.Value).TotalDays;
                score *= 1.0 + 2.0 * Math.Exp(-dni / 30.0);
            }
            else
            {
                score *= 1.5;   // nigdy nie używany szablon dostaje umiarkowany boost (jest dostępny)
            }
            // Frequency
            score *= 1.0 + Math.Log(1 + vm.CountTotal) / Math.Log(50.0);
            return score;
        }

        private static double ScoreFromHistory(int total, int dlaKlienta, int dlaUsera, DateTime ostatnio)
        {
            double score = 1.0;
            // Frequency global
            score *= 1.0 + Math.Log(1 + total) / Math.Log(50.0);
            // Klient match — najsilniejszy
            if (dlaKlienta > 0) score *= 3.0 + Math.Log(1 + dlaKlienta) / Math.Log(20.0);
            // User match
            if (dlaUsera > 0) score *= 1.5;
            // Recency
            double dni = (DateTime.Now - ostatnio).TotalDays;
            score *= 1.0 + 2.0 * Math.Exp(-dni / 30.0);
            return score;
        }

        private static double ScoreHistorySignals(int dlaKlienta, int dlaUsera, DateTime ostatnio, int total)
        {
            // Boost dla istniejącego szablonu który dodatkowo ma dopasowanie historyczne
            double m = 1.0;
            if (dlaKlienta > 0) m *= 1.5 + Math.Log(1 + dlaKlienta) / Math.Log(20.0);
            if (dlaUsera > 0) m *= 1.2;
            double dni = (DateTime.Now - ostatnio).TotalDays;
            m *= 1.0 + Math.Exp(-dni / 30.0);
            return m;
        }

        private static double JaccardSimilarity(HashSet<int> a, HashSet<int> b)
        {
            if (a.Count == 0 || b.Count == 0) return 0.0;
            int inter = 0;
            foreach (var x in a) if (b.Contains(x)) inter++;
            int union = a.Count + b.Count - inter;
            return union == 0 ? 0 : (double)inter / union;
        }

        // ════════════════════════════════════════════════════════════════════
        // DISPLAY HELPERS
        // ════════════════════════════════════════════════════════════════════
        private static void FillDisplayAndStyle(SuggestionVm s, int klientId)
        {
            string raw = s.Text.Replace('\n', ' ').Replace('\r', ' ').Trim();
            string disp = raw.Length > 50 ? raw.Substring(0, 47) + "…" : raw;

            string icon, color, border, src;
            if (s.Pinned) { icon = "📌"; color = "#FFF8DC"; border = "#E0B040"; src = "Przypięty"; }
            else if (s.Source == "Towary") { icon = "🛒"; color = "#FFEFD5"; border = "#FFB347"; src = "Pasuje do koszyka"; }
            else if (s.SzablonId.HasValue) { icon = "⭐"; color = "#E8F1DC"; border = "#C5DDA8"; src = "Szablon"; }
            else { icon = "📋"; color = "#F0F4F8"; border = "#CBD5E0"; src = "Z historii"; }

            s.Display = icon + " " + disp;
            s.ChipColor = color;
            s.ChipBorder = border;
            s.Icon = icon;
            s.Source = src;
            s.Tooltip = $"[{src} · użyć: {s.CountTotal}]"
                + (string.IsNullOrEmpty(s.Kategoria) ? "" : $" · {s.Kategoria}")
                + "\n" + s.Text;
        }

        private static string NormalizeKey(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Trim().ToLowerInvariant();
        }

        // ════════════════════════════════════════════════════════════════════
        // ZAPIS / EDYCJA / USUWANIE / TRACKING
        // ════════════════════════════════════════════════════════════════════
        public async Task<int> SaveTemplateAsync(string tekst, string kategoria, string zakres, int? klientId, string userId, bool pinowane, string utworzonyPrzez)
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            // Sprawdź czy taki szablon już istnieje (zakres + tekst + klient/user) → wtedy aktywuj/zaktualizuj
            string findSql = @"
                SELECT TOP 1 Id FROM dbo.NotatkiSzablony
                WHERE Tekst = @t AND Zakres = @z
                  AND ISNULL(KlientId, -1) = ISNULL(@kid, -1)
                  AND ISNULL(UserId, '') = ISNULL(@uid, '')";
            await using (var c = new SqlCommand(findSql, cn))
            {
                c.Parameters.AddWithValue("@t", tekst);
                c.Parameters.AddWithValue("@z", zakres);
                c.Parameters.AddWithValue("@kid", (object?)klientId ?? DBNull.Value);
                c.Parameters.AddWithValue("@uid", (object?)userId ?? DBNull.Value);
                var found = await c.ExecuteScalarAsync();
                if (found != null && found != DBNull.Value)
                {
                    int id = Convert.ToInt32(found);
                    string upd = "UPDATE dbo.NotatkiSzablony SET Aktywne = 1, Pinowane = @p, Kategoria = @k WHERE Id = @id";
                    await using var u = new SqlCommand(upd, cn);
                    u.Parameters.AddWithValue("@p", pinowane);
                    u.Parameters.AddWithValue("@k", (object?)kategoria ?? DBNull.Value);
                    u.Parameters.AddWithValue("@id", id);
                    await u.ExecuteNonQueryAsync();
                    return id;
                }
            }

            string ins = @"
                INSERT INTO dbo.NotatkiSzablony (Tekst, Kategoria, Zakres, KlientId, UserId, Pinowane, UtworzonoPrzez, Aktywne)
                VALUES (@t, @k, @z, @kid, @uid, @p, @up, 1);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";
            await using (var cmd = new SqlCommand(ins, cn))
            {
                cmd.Parameters.AddWithValue("@t", tekst);
                cmd.Parameters.AddWithValue("@k", (object?)kategoria ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@z", zakres);
                cmd.Parameters.AddWithValue("@kid", (object?)klientId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@uid", (object?)userId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@p", pinowane);
                cmd.Parameters.AddWithValue("@up", (object?)utworzonyPrzez ?? DBNull.Value);
                var id = await cmd.ExecuteScalarAsync();
                return id == null ? 0 : Convert.ToInt32(id);
            }
        }

        public async Task LogUsageAsync(string tekst, int? klientId, string userId, string akcja, IEnumerable<int> towary, int? szablonId)
        {
            try
            {
                string towaryStr = towary == null ? "" : string.Join(",", towary);
                if (towaryStr.Length > 480) towaryStr = towaryStr.Substring(0, 480);

                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                string ins = @"
                    INSERT INTO dbo.NotatkiUzycia (Tekst, KlientId, UserId, Akcja, TowaryKody, SzablonId)
                    VALUES (@t, @kid, @uid, @a, @tw, @sid)";
                await using var cmd = new SqlCommand(ins, cn);
                cmd.Parameters.AddWithValue("@t", tekst ?? "");
                cmd.Parameters.AddWithValue("@kid", (object?)klientId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@uid", (object?)userId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@a", akcja ?? "");
                cmd.Parameters.AddWithValue("@tw", towaryStr);
                cmd.Parameters.AddWithValue("@sid", (object?)szablonId ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();

                // Auto-bump szablon (LiczbaUzyc + OstatnieUzycie)
                if (szablonId.HasValue && akcja == AkcjaWstawiona)
                {
                    string bump = "UPDATE dbo.NotatkiSzablony SET LiczbaUzyc = LiczbaUzyc + 1, OstatnieUzycie = GETDATE() WHERE Id = @id";
                    await using var b = new SqlCommand(bump, cn);
                    b.Parameters.AddWithValue("@id", szablonId.Value);
                    await b.ExecuteNonQueryAsync();
                }
            }
            catch { /* tracking best-effort, nie przerywaj UI */ }
        }
    }
}
