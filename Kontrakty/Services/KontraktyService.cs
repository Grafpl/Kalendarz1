using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kalendarz1.AnalitykaPelna.Services;
using Kalendarz1.Kontrakty.Models;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Kontrakty.Services
{
    /// <summary>
    /// Warstwa danych modułu Kontrakty Hodowców (LibraNet).
    /// Czyta widoki ze skryptu Kontrakty/SQL/01_Kontrakty_v2_schema.sql:
    ///   v_KontraktyAktualne, v_ArimrCompliance, v_HodowcyBezKontraktu.
    /// Odporna na brak schematu — gdy tabele nie istnieją, metody zwracają puste/zera,
    /// a okno pokazuje baner "uruchom skrypt SQL" (patrz CzySchematIstniejeAsync).
    /// </summary>
    public partial class KontraktyService
    {
        private readonly string _conn;
        private const int Timeout = 60;

        public KontraktyService()
        {
            AnalitykaConfig.ZaladujJesliTrzeba();
            _conn = AnalitykaConfig.ConnLibraNet;
        }

        public KontraktyService(string conn) => _conn = conn;

        // Wspólny SELECT listy: nagłówek + aktualna wersja + nazwa twórcy (operators)
        private const string KolumnyListy = @"
SELECT k.Id, k.NumerKontraktu, k.DostawcaId, k.Hodowca, k.TypKontraktu, k.LiczySieDoArimr, k.Podmiot,
       k.WersjaId, k.NrWersji, k.Status, k.DataPodpisania, k.ObowiazujeOd, k.ObowiazujeDo,
       k.ProcentUbytku, k.TypCeny, k.Cena, k.TerminPlatnosciDni, k.SciezkaWord, k.SciezkaPdfSkan, k.DniDoWygasniecia,
       k.UtworzylUserId, k.UtworzylKiedy, ISNULL(op.Name, k.UtworzylUserId) AS UtworzylNazwa";
        // Wariant z podtytułem hodowcy (NIP + nr gospodarstwa) — kolumny 23/24, czytane defensywnie.
        private const string KolumnyListyExt = KolumnyListy + @",
       ISNULL(k.HodowcaNip,'') AS HodowcaNip, ISNULL(k.HodowcaGospodarstwo,'') AS HodowcaGospodarstwo";
        private const string ZrodloListy = @"
FROM dbo.v_KontraktyAktualne k
LEFT JOIN dbo.operators op ON op.ID = k.UtworzylUserId";

        // ─── Czy schemat wdrożony? ────────────────────────────────────────────
        public async Task<bool> CzySchematIstniejeAsync()
        {
            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(
                    "SELECT CASE WHEN OBJECT_ID('dbo.Kontrakty','U') IS NOT NULL AND OBJECT_ID('dbo.v_KontraktyAktualne','V') IS NOT NULL THEN 1 ELSE 0 END",
                    cn) { CommandTimeout = 15 };
                var r = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(r) == 1;
            }
            catch { return false; }
        }

        // ─── Lista kontraktów (filtrowana) ────────────────────────────────────
        public async Task<List<KontraktListItem>> GetKontraktyAsync(
            string? szukaj, string statusGrupa, string? typ, bool tylkoArimr, string? tylkoMojeUserId = null)
        {
            var lista = new List<KontraktListItem>();
            string where = BuildStatusFilter(statusGrupa);
            if (!string.IsNullOrWhiteSpace(szukaj))
                where += " AND (k.Hodowca LIKE @q OR k.NumerKontraktu LIKE @q OR op.Name LIKE @q)";
            if (!string.IsNullOrWhiteSpace(typ) && typ != "WSZYSTKIE")
                where += " AND k.TypKontraktu = @typ";
            if (tylkoArimr)
                where += " AND k.LiczySieDoArimr = 1";
            if (!string.IsNullOrWhiteSpace(tylkoMojeUserId))
                where += " AND k.UtworzylUserId = @me";

            string ogon = $@"
{ZrodloListy}
WHERE {where}
ORDER BY (CASE WHEN k.DniDoWygasniecia IS NULL THEN 1 ELSE 0 END),
         k.DniDoWygasniecia ASC, k.NumerKontraktu DESC;";

            void Bind(SqlCommand cmd)
            {
                if (!string.IsNullOrWhiteSpace(szukaj)) cmd.Parameters.AddWithValue("@q", "%" + szukaj.Trim() + "%");
                if (!string.IsNullOrWhiteSpace(typ) && typ != "WSZYSTKIE") cmd.Parameters.AddWithValue("@typ", typ);
                if (!string.IsNullOrWhiteSpace(tylkoMojeUserId)) cmd.Parameters.AddWithValue("@me", tylkoMojeUserId);
            }

            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                // Najpierw z podtytułem (NIP/gospodarstwo); jeśli widok jeszcze bez tych kolumn → fallback bez nich.
                foreach (var kolumny in new[] { KolumnyListyExt, KolumnyListy })
                {
                    try
                    {
                        using var cmd = new SqlCommand(kolumny + ogon, cn) { CommandTimeout = Timeout };
                        Bind(cmd);
                        using var r = await cmd.ExecuteReaderAsync();
                        while (await r.ReadAsync()) lista.Add(MapItem(r));
                        break; // sukces — nie próbuj wariantu fallback
                    }
                    catch (SqlException) when (kolumny == KolumnyListyExt)
                    {
                        lista.Clear(); // częściowo wczytane przy błędzie kolumny — wyczyść i spróbuj base
                    }
                }
            }
            catch (SqlException) { /* brak schematu lub niedostępna baza — zwróć pustą */ }
            return lista;
        }

        private static string BuildStatusFilter(string grupa) => grupa switch
        {
            "AKTYWNE" => "k.Status IN ('ACTIVE','EXPIRING','SIGNED')",
            "WYGASAJACE" => "k.Status IN ('ACTIVE','EXPIRING') AND k.DniDoWygasniecia BETWEEN 0 AND 90",
            "ROBOCZE" => "k.Status IN ('DRAFT','NEGOCJACJE','SENT')",
            "WYGASLE" => "k.Status IN ('EXPIRED','TERMINATED')",
            _ => "1 = 1"
        };

        // ─── Wszystkie kontrakty danego hodowcy (dla „skopiuj z istniejącego") ─
        public async Task<List<KontraktListItem>> GetKontraktyHodowcyAsync(string dostawcaId)
        {
            var lista = new List<KontraktListItem>();
            string sql = $@"
{KolumnyListy}
{ZrodloListy}
WHERE k.DostawcaId = @d
ORDER BY k.Rok DESC, k.LpRoku DESC;";
            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(sql, cn) { CommandTimeout = Timeout };
                cmd.Parameters.AddWithValue("@d", dostawcaId);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) lista.Add(MapItem(r));
            }
            catch (SqlException) { }
            return lista;
        }

        // ─── Aktywne kontrakty danego hodowcy (wejście ze Sprawdzalki) ────────
        public async Task<List<KontraktListItem>> GetAktywneKontraktyHodowcyAsync(string dostawcaId)
        {
            var lista = new List<KontraktListItem>();
            string sql = $@"
{KolumnyListy}
{ZrodloListy}
WHERE k.DostawcaId = @d AND k.Status IN ('ACTIVE','EXPIRING','SIGNED')
ORDER BY (CASE WHEN k.DniDoWygasniecia IS NULL THEN 1 ELSE 0 END), k.DniDoWygasniecia ASC;";
            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(sql, cn) { CommandTimeout = Timeout };
                cmd.Parameters.AddWithValue("@d", dostawcaId);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) lista.Add(MapItem(r));
            }
            catch (SqlException) { }
            return lista;
        }

        // ─── Wygasające w N dniach (dashboard „do działania") ─────────────────
        public async Task<List<KontraktListItem>> GetWygasajaceAsync(int dni)
        {
            var lista = new List<KontraktListItem>();
            string sql = $@"
{KolumnyListy}
{ZrodloListy}
WHERE k.Status IN ('ACTIVE','EXPIRING') AND k.DniDoWygasniecia IS NOT NULL AND k.DniDoWygasniecia <= @dni
ORDER BY k.DniDoWygasniecia ASC;";
            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(sql, cn) { CommandTimeout = Timeout };
                cmd.Parameters.AddWithValue("@dni", dni);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) lista.Add(MapItem(r));
            }
            catch (SqlException) { }
            return lista;
        }

        // ─── Liczniki inwentarza ──────────────────────────────────────────────
        public async Task<KontraktyInwentarz> GetInwentarzAsync()
        {
            var inw = new KontraktyInwentarz();
            const string sql = @"
SELECT
  SUM(CASE WHEN Status IN ('ACTIVE','EXPIRING','SIGNED') THEN 1 ELSE 0 END) AS Aktywne,
  SUM(CASE WHEN Status IN ('ACTIVE','EXPIRING') AND DniDoWygasniecia BETWEEN 0 AND 90 THEN 1 ELSE 0 END) AS Wygasajace,
  SUM(CASE WHEN Status IN ('EXPIRED','TERMINATED') THEN 1 ELSE 0 END) AS Wygasle,
  SUM(CASE WHEN Status IN ('DRAFT','NEGOCJACJE','SENT') THEN 1 ELSE 0 END) AS Robocze,
  COUNT(*) AS Razem
FROM dbo.v_KontraktyAktualne;";
            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(sql, cn) { CommandTimeout = Timeout };
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    inw.Aktywne = GetIntSafe(r, 0);
                    inw.Wygasajace90 = GetIntSafe(r, 1);
                    inw.Wygasle = GetIntSafe(r, 2);
                    inw.Robocze = GetIntSafe(r, 3);
                    inw.Razem = GetIntSafe(r, 4);
                }
            }
            catch (SqlException) { }
            return inw;
        }

        // ─── Compliance ARiMR ─────────────────────────────────────────────────
        public async Task<ArimrCompliance> GetComplianceAsync()
        {
            var c = new ArimrCompliance();
            const string sql = @"
SELECT SurowiecCaloscKg, SurowiecArimrKg, HodowcowOgolem, HodowcowArimr, ProcentArimr, Status, WyliczonoKiedy
FROM dbo.v_ArimrCompliance;";
            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(sql, cn) { CommandTimeout = Timeout };
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    c.SurowiecCaloscKg = GetDecSafe(r, 0);
                    c.SurowiecArimrKg = GetDecSafe(r, 1);
                    c.HodowcowOgolem = GetIntSafe(r, 2);
                    c.HodowcowArimr = GetIntSafe(r, 3);
                    c.ProcentArimr = GetDecSafe(r, 4);
                    c.Status = r.IsDBNull(5) ? "BRAK_DANYCH" : r.GetString(5);
                    c.WyliczonoKiedy = r.IsDBNull(6) ? DateTime.Now : r.GetDateTime(6);
                }
            }
            catch (SqlException) { }
            return c;
        }

        // ─── Sugestie warunków z historii dostaw (FarmerCalc, 12 mies.) ───────
        public async Task<WarunkiSugestia> GetSugestieWarunkowAsync(string dostawcaId)
        {
            var s = new WarunkiSugestia();
            if (string.IsNullOrWhiteSpace(dostawcaId)) return s;
            const string sql = @"
SELECT
  COUNT(*) AS Dostaw,
  AVG(CASE WHEN Price>0    THEN Price    END) AS CenaSr,
  AVG(CASE WHEN Loss>0     THEN Loss     END) AS UbytekSr,
  AVG(CASE WHEN AvWeight>0 THEN AvWeight END) AS WagaSr,
  MAX(CalcDate) AS Ostatnia,
  (SELECT TOP 1 Price FROM dbo.FarmerCalc f2
     WHERE LTRIM(RTRIM(f2.CustomerGID))=@id AND f2.Price>0 ORDER BY f2.CalcDate DESC) AS CenaOst
FROM dbo.FarmerCalc
WHERE LTRIM(RTRIM(CustomerGID))=@id AND CalcDate >= DATEADD(MONTH, -12, CAST(GETDATE() AS DATE));";
            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(sql, cn) { CommandTimeout = Timeout };
                cmd.Parameters.AddWithValue("@id", dostawcaId.Trim());
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    s.Dostaw = GetIntSafe(r, 0);
                    s.CenaSrednia    = r.IsDBNull(1) ? null : Math.Round(r.GetDecimal(1), 2);
                    s.UbytekSredniProc = r.IsDBNull(2) ? null : Math.Round(r.GetDecimal(2) * 100m, 1);
                    s.WagaSrednia    = r.IsDBNull(3) ? null : Math.Round(r.GetDecimal(3), 2);
                    s.OstatniaDostawa = r.IsDBNull(4) ? null : r.GetDateTime(4);
                    s.CenaOstatnia   = r.IsDBNull(5) ? null : Math.Round(r.GetDecimal(5), 2);
                }
            }
            catch (SqlException) { }
            return s;
        }

        // ─── Ostatnie dostawy POTWIERDZONE scalone w 10-dniowe grupy (mini-karty w Warunkach) ─
        // Lista dostaw z HarmonogramDostaw gdzie Bufor IN ('Potwierdzony','Potwierdzone'),
        // wartości handlowe (cena/dodatek/ubytek/typ ceny) z FarmerCalc po dacie+hodowcy.
        // Scala w okna ≤10 dni, zwraca TOP N grup z agregatami: AVG cena/dodatek/ubytek,
        // najczęstszy typ ceny i rozliczana waga.
        public async Task<List<DostawaSugestia>> GetOstatnieDostawyDoSugestiiAsync(
            string dostawcaId, string nazwaHodowcy, int topGrup = 3)
        {
            var wynik = new List<DostawaSugestia>();
            if (string.IsNullOrWhiteSpace(dostawcaId) || string.IsNullOrWhiteSpace(nazwaHodowcy))
                return wynik;

            const string sql = @"
SELECT
    hd.DataOdbioru,
    fc.Price,
    fc.Addition,
    fc.Loss,
    ISNULL(pt.Name, '') AS TypCenyName
FROM dbo.HarmonogramDostaw hd
LEFT JOIN dbo.FarmerCalc fc
       ON LTRIM(RTRIM(fc.CustomerGID)) = @id
      AND CAST(fc.CalcDate AS DATE) = CAST(hd.DataOdbioru AS DATE)
      AND ISNULL(fc.Deleted, 0) = 0
LEFT JOIN dbo.PriceType pt ON pt.ID = fc.PriceTypeID
WHERE hd.Dostawca = @nazwa
  AND hd.Bufor IN ('Potwierdzony', 'Potwierdzone')
  AND hd.DataOdbioru >= DATEADD(MONTH, -12, CAST(GETDATE() AS DATE))
ORDER BY hd.DataOdbioru DESC;";

            // Surowe wiersze
            var surowe = new List<(DateTime Data, decimal? Cena, decimal? Dodatek, decimal? Loss, string TypCeny)>();
            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(sql, cn) { CommandTimeout = Timeout };
                cmd.Parameters.AddWithValue("@id", dostawcaId.Trim());
                cmd.Parameters.AddWithValue("@nazwa", nazwaHodowcy.Trim());
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    surowe.Add((
                        r.IsDBNull(0) ? DateTime.MinValue : r.GetDateTime(0),
                        r.IsDBNull(1) ? null : (decimal?)r.GetDecimal(1),
                        r.IsDBNull(2) ? null : (decimal?)r.GetDecimal(2),
                        r.IsDBNull(3) ? null : (decimal?)r.GetDecimal(3),
                        r.IsDBNull(4) ? "" : r.GetString(4)
                    ));
                }
            }
            catch (SqlException) { return wynik; }
            if (surowe.Count == 0) return wynik;

            // Scalanie 10-dniowe — najnowsza zaczyna grupę, dodajemy starsze gdy w oknie ≤10 dni
            // licząc od NAJNOWSZEJ dostawy w grupie.
            var grupy = new List<List<(DateTime Data, decimal? Cena, decimal? Dodatek, decimal? Loss, string TypCeny)>>();
            DateTime? glowaGrupy = null;
            foreach (var d in surowe)  // już posortowane DESC po CalcDate
            {
                if (glowaGrupy is null || (glowaGrupy.Value - d.Data).TotalDays > 10)
                {
                    grupy.Add(new());
                    glowaGrupy = d.Data;
                    if (grupy.Count > topGrup) break;
                }
                grupy[^1].Add(d);
            }
            // Mogliśmy dorzucić 1 nadmiarową grupę przy break — utnij
            if (grupy.Count > topGrup) grupy.RemoveAt(grupy.Count - 1);

            // Agregacja per grupa
            foreach (var g in grupy)
            {
                if (g.Count == 0) continue;
                decimal? cenaSr = AvgOrNull(g.Select(x => x.Cena));
                decimal? dodSr = AvgOrNull(g.Select(x => x.Dodatek));
                decimal? lossSr = AvgOrNull(g.Select(x => x.Loss));
                string typCeny = g.Where(x => !string.IsNullOrWhiteSpace(x.TypCeny))
                                  .GroupBy(x => x.TypCeny)
                                  .OrderByDescending(x => x.Count())
                                  .Select(x => x.Key).FirstOrDefault() ?? "";
                // Czyja waga — heurystyka per dostawa, potem mode
                string czyja = g.Select(x => x.Loss is { } l && l > 0 ? "Hodowca" : "Ubojnia")
                                .GroupBy(x => x).OrderByDescending(x => x.Count())
                                .Select(x => x.Key).FirstOrDefault() ?? "Ubojnia";

                wynik.Add(new DostawaSugestia
                {
                    Data = g.Max(x => x.Data),
                    DataDo = g.Min(x => x.Data),
                    LiczbaDostaw = g.Count,
                    Cena = cenaSr is { } cs ? Math.Round(cs, 2) : null,
                    Dodatek = dodSr is { } ds ? Math.Round(ds, 2) : null,
                    UbytekProc = lossSr is { } ls ? Math.Round(ls * 100m, 1) : null,
                    TypCeny = typCeny,
                    CzyjaWaga = czyja
                });
            }
            return wynik;

            static decimal? AvgOrNull(IEnumerable<decimal?> seq)
            {
                var vals = seq.Where(v => v.HasValue).Select(v => v!.Value).ToList();
                return vals.Count == 0 ? (decimal?)null : vals.Average();
            }
        }

        // ─── Snapshot zgodności ARiMR (trend) — idempotentny per dzień ────────
        public async Task ZapiszComplianceSnapshotAsync()
        {
            try
            {
                var c = await GetComplianceAsync();
                if (c.Status == "BRAK_DANYCH") return;
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(@"
MERGE dbo.KontraktyComplianceSnapshot AS t
USING (SELECT CAST(GETDATE() AS DATE) AS D) AS s ON t.DataSnapshotu = s.D
WHEN MATCHED THEN UPDATE SET ProcentArimr=@p, SurowiecCaloscKg=@c, SurowiecArimrKg=@a,
     HodowcowArimr=@ha, HodowcowOgolem=@ho, Status=@st
WHEN NOT MATCHED THEN INSERT (DataSnapshotu, ProcentArimr, SurowiecCaloscKg, SurowiecArimrKg, HodowcowArimr, HodowcowOgolem, Status)
     VALUES (s.D, @p, @c, @a, @ha, @ho, @st);", cn) { CommandTimeout = Timeout };
                cmd.Parameters.AddWithValue("@p", c.ProcentArimr);
                cmd.Parameters.AddWithValue("@c", c.SurowiecCaloscKg);
                cmd.Parameters.AddWithValue("@a", c.SurowiecArimrKg);
                cmd.Parameters.AddWithValue("@ha", c.HodowcowArimr);
                cmd.Parameters.AddWithValue("@ho", c.HodowcowOgolem);
                cmd.Parameters.AddWithValue("@st", c.Status);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException) { }
        }

        public async Task<List<ComplianceTrendPunkt>> GetComplianceTrendAsync(int dni = 180)
        {
            var lista = new List<ComplianceTrendPunkt>();
            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(@"
SELECT DataSnapshotu, ProcentArimr FROM dbo.KontraktyComplianceSnapshot
WHERE DataSnapshotu >= DATEADD(DAY, -@d, CAST(GETDATE() AS DATE))
ORDER BY DataSnapshotu;", cn) { CommandTimeout = Timeout };
                cmd.Parameters.AddWithValue("@d", dni);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    lista.Add(new ComplianceTrendPunkt { Data = r.GetDateTime(0), Procent = r.GetDecimal(1) });
            }
            catch (SqlException) { }
            return lista;
        }

        // ─── Hodowcy bez kontraktu (high-value first) ─────────────────────────
        public async Task<List<HodowcaBezKontraktu>> GetHodowcyBezKontraktuAsync(int top = 15)
        {
            var lista = new List<HodowcaBezKontraktu>();
            string sql = $@"
SELECT TOP ({top}) DostawcaId, Hodowca, LiczbaDostaw, WagaKg12m, OstatniaDostawa
FROM dbo.v_HodowcyBezKontraktu
ORDER BY WagaKg12m DESC;";
            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(sql, cn) { CommandTimeout = Timeout };
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    lista.Add(new HodowcaBezKontraktu
                    {
                        DostawcaId = r.IsDBNull(0) ? "" : r.GetValue(0).ToString() ?? "",
                        Hodowca = r.IsDBNull(1) ? "(brak nazwy)" : r.GetString(1),
                        LiczbaDostaw = GetIntSafe(r, 2),
                        WagaKg12m = GetDecSafe(r, 3),
                        OstatniaDostawa = r.IsDBNull(4) ? null : r.GetDateTime(4)
                    });
                }
            }
            catch (SqlException) { }
            return lista;
        }

        // ─── Ranking hodowców wg wolumenu (12 mies.) + flaga kontrakt/ARiMR ───
        public async Task<List<RankingHodowca>> GetRankingHodowcowAsync(int top = 50)
        {
            var lista = new List<RankingHodowca>();
            string sql = $@"
;WITH okr AS (SELECT DATEADD(MONTH,-12,CAST(GETDATE() AS DATE)) Od, CAST(GETDATE() AS DATE) Do),
vol AS (
  SELECT LTRIM(RTRIM(fc.CustomerGID)) AS gid,
         SUM(ISNULL(fc.NettoFarmWeight, ISNULL(fc.FullFarmWeight,0)-ISNULL(fc.EmptyFarmWeight,0))) AS kg,
         COUNT(*) AS dostaw, MAX(fc.CalcDate) AS ost
  FROM dbo.FarmerCalc fc CROSS JOIN okr o
  WHERE fc.CalcDate BETWEEN o.Od AND o.Do
  GROUP BY LTRIM(RTRIM(fc.CustomerGID))
)
SELECT TOP ({top}) v.gid, ISNULL(d.Name, v.gid) AS Nazwa, v.kg, v.dostaw, v.ost,
  CASE WHEN EXISTS (SELECT 1 FROM dbo.Kontrakty k JOIN dbo.KontraktyWersje w ON w.KontraktId=k.Id AND w.IsAktualna=1
        WHERE k.DostawcaId=v.gid AND w.Status IN ('ACTIVE','EXPIRING','SIGNED')) THEN 1 ELSE 0 END AS MaKontrakt,
  CASE WHEN EXISTS (SELECT 1 FROM dbo.Kontrakty k JOIN dbo.KontraktyWersje w ON w.KontraktId=k.Id AND w.IsAktualna=1
        WHERE k.DostawcaId=v.gid AND k.LiczySieDoArimr=1 AND w.Status IN ('ACTIVE','EXPIRING','SIGNED')) THEN 1 ELSE 0 END AS MaArimr
FROM vol v LEFT JOIN dbo.Dostawcy d ON d.ID = v.gid
WHERE v.kg > 0
ORDER BY v.kg DESC;";
            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(sql, cn) { CommandTimeout = Timeout };
                using var r = await cmd.ExecuteReaderAsync();
                int poz = 0;
                while (await r.ReadAsync())
                {
                    lista.Add(new RankingHodowca
                    {
                        Pozycja = ++poz,
                        DostawcaId = r.IsDBNull(0) ? "" : (r.GetValue(0).ToString() ?? "").Trim(),
                        Nazwa = r.IsDBNull(1) ? "(brak nazwy)" : r.GetString(1),
                        WagaKg12m = GetDecSafe(r, 2),
                        LiczbaDostaw = GetIntSafe(r, 3),
                        OstatniaDostawa = r.IsDBNull(4) ? null : r.GetDateTime(4),
                        MaKontrakt = !r.IsDBNull(5) && r.GetInt32(5) == 1,
                        MaArimr = !r.IsDBNull(6) && r.GetInt32(6) == 1
                    });
                }
            }
            catch (SqlException) { }
            return lista;
        }

        // ─── Mapowanie wiersza ────────────────────────────────────────────────
        private static KontraktListItem MapItem(SqlDataReader r)
        {
            var it = MapItemBase(r);
            if (r.FieldCount > 23) // wariant z podtytułem hodowcy
            {
                it.HodowcaNip = r.IsDBNull(23) ? "" : r.GetString(23);
                it.HodowcaGospodarstwo = r.IsDBNull(24) ? "" : r.GetString(24);
            }
            return it;
        }

        private static KontraktListItem MapItemBase(SqlDataReader r) => new()
        {
            Id = r.GetInt32(0),
            NumerKontraktu = r.IsDBNull(1) ? "" : r.GetString(1),
            DostawcaId = r.IsDBNull(2) ? "" : (r.GetValue(2).ToString() ?? ""),
            Hodowca = r.IsDBNull(3) ? "" : r.GetString(3),
            TypKontraktu = r.IsDBNull(4) ? "" : r.GetString(4),
            LiczySieDoArimr = !r.IsDBNull(5) && r.GetBoolean(5),
            Podmiot = r.IsDBNull(6) ? "" : r.GetString(6),
            WersjaId = r.IsDBNull(7) ? null : r.GetInt32(7),
            NrWersji = r.IsDBNull(8) ? null : r.GetInt32(8),
            Status = r.IsDBNull(9) ? null : r.GetString(9),
            DataPodpisania = r.IsDBNull(10) ? null : r.GetDateTime(10),
            ObowiazujeOd = r.IsDBNull(11) ? null : r.GetDateTime(11),
            ObowiazujeDo = r.IsDBNull(12) ? null : r.GetDateTime(12),
            ProcentUbytku = r.IsDBNull(13) ? null : r.GetDecimal(13),
            TypCeny = r.IsDBNull(14) ? null : r.GetString(14),
            Cena = r.IsDBNull(15) ? null : r.GetDecimal(15),
            TerminPlatnosciDni = r.IsDBNull(16) ? null : r.GetInt32(16),
            SciezkaWord = r.IsDBNull(17) ? null : r.GetString(17),
            SciezkaPdfSkan = r.IsDBNull(18) ? null : r.GetString(18),
            DniDoWygasniecia = r.IsDBNull(19) ? null : r.GetInt32(19),
            UtworzylUserId = r.IsDBNull(20) ? "" : r.GetString(20),
            UtworzylKiedy = r.IsDBNull(21) ? null : r.GetDateTime(21),
            UtworzylNazwa = r.IsDBNull(22) ? "" : r.GetString(22),
        };

        private static int GetIntSafe(SqlDataReader r, int i)
            => r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i));

        private static decimal GetDecSafe(SqlDataReader r, int i)
            => r.IsDBNull(i) ? 0m : Convert.ToDecimal(r.GetValue(i));
    }
}
