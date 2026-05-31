using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Kalendarz1.AnalitykaPelna.Services;

namespace Kalendarz1.ColdChain
{
    /// <summary>
    /// Cold Chain HACCP — warstwa na ISTNIEJĄCYCH danych:
    ///   • TemperaturyMiejsca — pomiary (PartiaId, Miejsce, Proba1..4, Srednia, Wykonal, DataPomiaru)
    ///   • QC_Normy (Kategoria='TEMPERATURA') — progi rampa/chiller/tunel
    ///   • ColdChainKorekta — działania naprawcze HACCP (nowa, mała tabela)
    /// Baza: LibraNet. Ciągły monitoring sondami = osobna warstwa (CCP_* / SQL Sensors).
    /// </summary>
    public class ColdChainService
    {
        private readonly string _conn;

        public ColdChainService()
        {
            AnalitykaConfig.ZaladujJesliTrzeba();
            _conn = AnalitykaConfig.ConnLibraNet;
        }

        // ─── Normy (QC_Normy) ──────────────────────────────────────────────
        public async Task<List<TempNorma>> GetNormyTempAsync()
        {
            var lista = new List<TempNorma>();
            const string sql = @"
SELECT ID, Nazwa, Opis, MinWartosc, MaxWartosc, ISNULL(JednostkaMiary,'C') AS Jm
FROM dbo.QC_Normy
WHERE IsAktywna = 1 AND Kategoria = 'TEMPERATURA'
ORDER BY Kolejnosc;";
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 30 };
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                lista.Add(new TempNorma
                {
                    Id = r.GetInt32(0),
                    Nazwa = r.GetString(1),
                    Opis = r.IsDBNull(2) ? null : r.GetString(2),
                    Min = r.IsDBNull(3) ? null : r.GetDecimal(3),
                    Max = r.IsDBNull(4) ? null : r.GetDecimal(4),
                    Jednostka = r.GetString(5)
                });
            }
            DopelnijNormyFallbackiem(lista);
            return lista;
        }

        /// <summary>
        /// Dla miejsc, których nie ma w QC_Normy, dodaje normy domyślne z kodu (MiejscaCC.DomyslneNormy).
        /// Dzięki temu oparzalnik/schładzalnik są oceniane vs norma nawet zanim uruchomisz SQL.
        /// </summary>
        private static void DopelnijNormyFallbackiem(List<TempNorma> zBazy)
        {
            var pokryte = new HashSet<string>(zBazy.Select(n => n.Miejsce), StringComparer.OrdinalIgnoreCase);
            foreach (var (kod, ikona, label) in MiejscaCC.Lista)
            {
                if (pokryte.Contains(kod)) continue;
                if (!MiejscaCC.DomyslneNormy.TryGetValue(kod, out var prog)) continue;
                zBazy.Add(new TempNorma
                {
                    Id = 0,
                    Nazwa = "Temp" + char.ToUpper(kod[0]) + kod.Substring(1),
                    Opis = label + " (norma domyślna z kodu)",
                    Min = prog.Min,
                    Max = prog.Max,
                    Jednostka = "C"
                });
            }
        }

        private Dictionary<string, TempNorma> MapaNorm(List<TempNorma> normy)
        {
            var d = new Dictionary<string, TempNorma>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in normy) d[n.Miejsce] = n;
            return d;
        }

        // ─── Pomiary (TemperaturyMiejsca) ──────────────────────────────────
        public async Task<List<TempPomiar>> GetPomiaryAsync(DateTime od, DateTime doDate, List<TempNorma>? normy = null)
        {
            normy ??= await GetNormyTempAsync();
            var mapa = MapaNorm(normy);
            var lista = new List<TempPomiar>();

            const string sql = @"
SELECT t.Id, t.PartiaId, LOWER(t.Miejsce) AS Miejsce,
       t.Proba1, t.Proba2, t.Proba3, t.Proba4, t.Srednia, t.Wykonal, t.DataPomiaru,
       pd.CustomerName AS Hodowca,
       CASE WHEN k.Id IS NOT NULL THEN 1 ELSE 0 END AS MaKorekta
FROM dbo.TemperaturyMiejsca t
LEFT JOIN dbo.PartiaDostawca pd ON pd.Partia = t.PartiaId
LEFT JOIN dbo.ColdChainKorekta k ON k.TemperaturaMiejscaId = t.Id
WHERE t.DataPomiaru BETWEEN @Od AND @Do
ORDER BY t.DataPomiaru DESC;";

            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@Od", od.Date);
            cmd.Parameters.AddWithValue("@Do", doDate.Date.AddDays(1).AddSeconds(-1));
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                string miejsce = r.IsDBNull(2) ? "" : r.GetString(2);
                var p = new TempPomiar
                {
                    Id = r.GetInt32(0),
                    PartiaId = r.IsDBNull(1) ? "" : r.GetString(1),
                    Miejsce = miejsce,
                    Proba1 = r.IsDBNull(3) ? null : r.GetDecimal(3),
                    Proba2 = r.IsDBNull(4) ? null : r.GetDecimal(4),
                    Proba3 = r.IsDBNull(5) ? null : r.GetDecimal(5),
                    Proba4 = r.IsDBNull(6) ? null : r.GetDecimal(6),
                    Srednia = r.IsDBNull(7) ? null : r.GetDecimal(7),
                    Wykonal = r.IsDBNull(8) ? null : r.GetString(8),
                    DataPomiaru = r.GetDateTime(9),
                    Hodowca = r.IsDBNull(10) ? null : r.GetString(10),
                    MaKorekta = r.GetInt32(11) == 1
                };
                if (mapa.TryGetValue(miejsce, out var norma))
                {
                    p.NormaMin = norma.Min;
                    p.NormaMax = norma.Max;
                }
                lista.Add(p);
            }
            return lista;
        }

        // ─── Kafelki dashboardu (agregat per miejsce) ──────────────────────
        public List<MiejsceKafel> BudujKafelki(List<TempPomiar> pomiary, List<TempNorma> normy)
        {
            var mapa = MapaNorm(normy);
            var kafelki = new List<MiejsceKafel>();
            foreach (var (kod, ikona, label) in MiejscaCC.Lista)
            {
                var grupa = pomiary.Where(p => string.Equals(p.Miejsce, kod, StringComparison.OrdinalIgnoreCase)
                                               && p.Srednia.HasValue).ToList();
                // Pokazuj kafelek tylko jeśli są pomiary LUB jest zdefiniowana norma (żeby nie zaśmiecać pustymi)
                bool maNorme = mapa.ContainsKey(kod);
                if (grupa.Count == 0 && !maNorme) continue;
                kafelki.Add(new MiejsceKafel
                {
                    Miejsce = label.Split('(')[0].Trim(),
                    Ikona = ikona,
                    LiczbaPomiarow = grupa.Count,
                    LiczbaWNormie = grupa.Count(p => !p.CzyPozaNorma),
                    SredniaTemp = grupa.Count > 0 ? Math.Round(grupa.Average(p => p.Srednia!.Value), 1) : null,
                    NormaText = maNorme ? mapa[kod].ZakresFormatted : "—"
                });
            }
            return kafelki;
        }

        // ─── Trend per miejsce (do wykresu) ────────────────────────────────
        public async Task<List<TempTrendPunkt>> GetTrendAsync(string miejsce, DateTime od, DateTime doDate)
        {
            var lista = new List<TempTrendPunkt>();
            const string sql = @"
SELECT t.DataPomiaru, t.Srednia
FROM dbo.TemperaturyMiejsca t
WHERE LOWER(t.Miejsce) = @M AND t.Srednia IS NOT NULL
  AND t.DataPomiaru BETWEEN @Od AND @Do
ORDER BY t.DataPomiaru;";
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@M", miejsce.ToLowerInvariant());
            cmd.Parameters.AddWithValue("@Od", od.Date);
            cmd.Parameters.AddWithValue("@Do", doDate.Date.AddDays(1).AddSeconds(-1));
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                lista.Add(new TempTrendPunkt { Data = r.GetDateTime(0), Srednia = r.GetDecimal(1) });
            return lista;
        }

        // ─── Wpis pomiaru (INSERT do TemperaturyMiejsca) ───────────────────
        public async Task ZapiszPomiarAsync(string partiaId, string miejsce,
            decimal? p1, decimal? p2, decimal? p3, decimal? p4, string? wykonal)
        {
            var proby = new[] { p1, p2, p3, p4 }.Where(x => x.HasValue).Select(x => x!.Value).ToList();
            decimal? srednia = proby.Count > 0 ? Math.Round(proby.Average(), 2) : null;

            const string sql = @"
INSERT INTO dbo.TemperaturyMiejsca (PartiaId, Miejsce, Proba1, Proba2, Proba3, Proba4, Srednia, Wykonal, DataPomiaru)
VALUES (@P, @M, @P1, @P2, @P3, @P4, @Sr, @W, GETDATE());";
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 30 };
            cmd.Parameters.AddWithValue("@P", partiaId);
            cmd.Parameters.AddWithValue("@M", miejsce);
            cmd.Parameters.AddWithValue("@P1", (object?)p1 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@P2", (object?)p2 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@P3", (object?)p3 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@P4", (object?)p4 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Sr", (object?)srednia ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@W", (object?)wykonal ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        // ─── Korekta HACCP (działanie naprawcze do incydentu) ──────────────
        public async Task ZapiszKorekteAsync(int pomiarId, string opis, string? user)
        {
            const string sql = @"
MERGE dbo.ColdChainKorekta AS t
USING (SELECT @Id AS Pid) AS s ON t.TemperaturaMiejscaId = s.Pid
WHEN MATCHED THEN UPDATE SET KorektaOpis=@Op, KorektaPrzez=@U, KorektaDateTime=GETDATE()
WHEN NOT MATCHED THEN INSERT (TemperaturaMiejscaId, KorektaOpis, KorektaPrzez, Status)
    VALUES (@Id, @Op, @U, 'ZAMKNIETY');";
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 30 };
            cmd.Parameters.AddWithValue("@Id", pomiarId);
            cmd.Parameters.AddWithValue("@Op", opis);
            cmd.Parameters.AddWithValue("@U", (object?)user ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<string?> GetKorektaOpisAsync(int pomiarId)
        {
            const string sql = "SELECT KorektaOpis FROM dbo.ColdChainKorekta WHERE TemperaturaMiejscaId=@Id;";
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 30 };
            cmd.Parameters.AddWithValue("@Id", pomiarId);
            var v = await cmd.ExecuteScalarAsync();
            return v as string;
        }

        // ─── Ranking hodowców po zgodności temperatur (z już-załadowanych pomiarów) ───
        public List<RankingHodowcaTemp> BudujRankingHodowcow(List<TempPomiar> pomiary)
        {
            return pomiary
                .Where(p => p.Srednia.HasValue && !string.IsNullOrEmpty(p.Hodowca))
                .GroupBy(p => p.Hodowca!)
                .Select(g => new RankingHodowcaTemp
                {
                    Hodowca = g.Key,
                    LiczbaPomiarow = g.Count(),
                    LiczbaPoza = g.Count(p => p.CzyPozaNorma)
                })
                .OrderByDescending(x => x.LiczbaPoza)
                .ThenBy(x => x.ProcZgodnosci)
                .Select((x, i) => { x.Pozycja = i + 1; return x; })
                .ToList();
        }

        // ─── Partie bez kompletu pomiarów (luka HACCP) ─────────────────────
        public List<NiekompletnaPartia> BudujNiekompletne(List<TempPomiar> pomiary)
        {
            string[] wymagane = { "rampa", "chiller", "tunel" };
            return pomiary
                .Where(p => !string.IsNullOrEmpty(p.PartiaId))
                .GroupBy(p => p.PartiaId)
                .Select(g =>
                {
                    var ma = g.Select(p => p.Miejsce).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    var brak = wymagane.Where(w => !ma.Contains(w, StringComparer.OrdinalIgnoreCase)).ToList();
                    return new NiekompletnaPartia
                    {
                        Partia = g.Key,
                        Hodowca = g.Select(p => p.Hodowca).FirstOrDefault(h => !string.IsNullOrEmpty(h)),
                        MaMiejsca = ma,
                        BrakujeMiejsc = brak,
                        OstatniPomiar = g.Max(p => p.DataPomiaru)
                    };
                })
                .Where(x => x.BrakujeMiejsc.Count > 0)
                .OrderByDescending(x => x.OstatniPomiar)
                .ToList();
        }

        // ─── Pomiary konkretnej partii (do krzywej schładzania) ────────────
        public async Task<List<TempPomiar>> GetPomiaryPartiiAsync(string partia, List<TempNorma>? normy = null)
        {
            normy ??= await GetNormyTempAsync();
            var mapa = MapaNorm(normy);
            var lista = new List<TempPomiar>();
            const string sql = @"
SELECT t.Id, t.PartiaId, LOWER(t.Miejsce) AS Miejsce,
       t.Proba1, t.Proba2, t.Proba3, t.Proba4, t.Srednia, t.Wykonal, t.DataPomiaru
FROM dbo.TemperaturyMiejsca t
WHERE t.PartiaId = @P
ORDER BY t.DataPomiaru;";
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 30 };
            cmd.Parameters.AddWithValue("@P", partia);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                string miejsce = r.IsDBNull(2) ? "" : r.GetString(2);
                var p = new TempPomiar
                {
                    Id = r.GetInt32(0),
                    PartiaId = r.IsDBNull(1) ? "" : r.GetString(1),
                    Miejsce = miejsce,
                    Proba1 = r.IsDBNull(3) ? null : r.GetDecimal(3),
                    Proba2 = r.IsDBNull(4) ? null : r.GetDecimal(4),
                    Proba3 = r.IsDBNull(5) ? null : r.GetDecimal(5),
                    Proba4 = r.IsDBNull(6) ? null : r.GetDecimal(6),
                    Srednia = r.IsDBNull(7) ? null : r.GetDecimal(7),
                    Wykonal = r.IsDBNull(8) ? null : r.GetString(8),
                    DataPomiaru = r.GetDateTime(9)
                };
                if (mapa.TryGetValue(miejsce, out var norma)) { p.NormaMin = norma.Min; p.NormaMax = norma.Max; }
                lista.Add(p);
            }
            return lista;
        }

        // ─── Partie do wyboru ──────────────────────────────────────────────
        public async Task<List<PartiaItem>> GetPartieAsync()
        {
            var lista = new List<PartiaItem>();
            const string sql = @"
SELECT TOP 300 lp.Partia, pd.CustomerName
FROM dbo.listapartii lp
LEFT JOIN dbo.PartiaDostawca pd ON pd.Partia = lp.Partia
ORDER BY lp.CreateData DESC;";
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                lista.Add(new PartiaItem
                {
                    Partia = r["Partia"]?.ToString() ?? "",
                    Hodowca = r["CustomerName"]?.ToString()
                });
            }
            return lista;
        }
    }
}
