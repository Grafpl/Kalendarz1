using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kalendarz1.AnalitykaPelna.Models;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.AnalitykaPelna.Services
{
    /// <summary>
    /// Co teraz wjechało na wagę. In0E LIVE — wyciągnięte z PrzychodService,
    /// ale: filtr operatora przeniesiony do SQL, JOIN z PartiaDostawca zamiast subquery,
    /// i CommandTimeout ustawiony.
    /// </summary>
    public class RealizacjaService
    {
        private readonly string _connLibra;
        private const int SqlTimeoutSec = 60;

        public RealizacjaService()
        {
            AnalitykaConfig.ZaladujJesliTrzeba();
            _connLibra = AnalitykaConfig.ConnLibraNet;
        }

        public RealizacjaService(string connLibra)
        {
            _connLibra = connLibra;
        }

        public async Task<List<WazenieRekord>> LoadWazeniaAsync(FiltryAnaliz f)
        {
            var result = new List<WazenieRekord>();

            var sb = new StringBuilder(@"
                SELECT
                    e.ArticleID, e.ArticleName, e.TermID, e.TermType,
                    e.Weight, e.ActWeight, e.Data, e.Godzina,
                    e.OperatorID, e.Wagowy, e.Tara, e.P1, e.QntInCont,
                    pd.CustomerID, pd.CustomerName
                FROM dbo.In0E e
                LEFT JOIN dbo.PartiaDostawca pd ON e.P1 = pd.Partia
                WHERE e.Data >= @DataOd AND e.Data <= @DataDo
                  AND ISNULL(e.ArticleName,'') <> ''");

            if (!string.IsNullOrEmpty(f.TowarIdLibra)) sb.Append(" AND e.ArticleID = @ArticleID");
            if (!string.IsNullOrEmpty(f.OperatorID)) sb.Append(" AND e.OperatorID = @OperatorID");
            if (f.TerminalId is > 0) sb.Append(" AND e.TermID = @TermID");
            if (!string.IsNullOrEmpty(f.Partia)) sb.Append(" AND e.P1 = @Partia");
            if (f.KlasaKurczaka.HasValue) sb.Append(" AND e.QntInCont = @Klasa");
            if (f.GodzinaOd is >= 0) sb.Append(" AND TRY_CAST(LEFT(e.Godzina,2) AS INT) >= @GodzOd");
            if (f.GodzinaDo is >= 0) sb.Append(" AND TRY_CAST(LEFT(e.Godzina,2) AS INT) <= @GodzDo");
            if (!string.IsNullOrEmpty(f.Dostawca))
                sb.Append(" AND (pd.CustomerID = @Dostawca OR pd.CustomerName = @Dostawca)");

            sb.Append(" ORDER BY e.Data, e.Godzina");

            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sb.ToString(), conn) { CommandTimeout = SqlTimeoutSec };
            cmd.Parameters.AddWithValue("@DataOd", f.DataOd.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@DataDo", f.DataDo.ToString("yyyy-MM-dd"));
            if (!string.IsNullOrEmpty(f.TowarIdLibra)) cmd.Parameters.AddWithValue("@ArticleID", f.TowarIdLibra);
            if (!string.IsNullOrEmpty(f.OperatorID)) cmd.Parameters.AddWithValue("@OperatorID", f.OperatorID);
            if (f.TerminalId is > 0) cmd.Parameters.AddWithValue("@TermID", f.TerminalId.Value);
            if (!string.IsNullOrEmpty(f.Partia)) cmd.Parameters.AddWithValue("@Partia", f.Partia);
            if (f.KlasaKurczaka.HasValue) cmd.Parameters.AddWithValue("@Klasa", f.KlasaKurczaka.Value);
            if (f.GodzinaOd is >= 0) cmd.Parameters.AddWithValue("@GodzOd", f.GodzinaOd!.Value);
            if (f.GodzinaDo is >= 0) cmd.Parameters.AddWithValue("@GodzDo", f.GodzinaDo!.Value);
            if (!string.IsNullOrEmpty(f.Dostawca)) cmd.Parameters.AddWithValue("@Dostawca", f.Dostawca);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var dataValue = SqlSafe.ParseDate(SqlSafe.ReadString(reader, 6));
                var godzinaValue = SqlSafe.ParseGodzina(dataValue, SqlSafe.ReadString(reader, 7));

                result.Add(new WazenieRekord
                {
                    ArticleID = SqlSafe.ReadString(reader, 0),
                    NazwaTowaru = SqlSafe.ReadString(reader, 1),
                    TermID = SqlSafe.ReadInt(reader, 2),
                    Terminal = SqlSafe.ReadString(reader, 3),
                    Weight = SqlSafe.ReadDecimal(reader, 4),
                    ActWeight = SqlSafe.ReadDecimal(reader, 5),
                    Data = dataValue,
                    Godzina = godzinaValue,
                    OperatorID = SqlSafe.ReadString(reader, 8),
                    Wagowy = SqlSafe.ReadString(reader, 9),
                    Tara = SqlSafe.ReadDecimal(reader, 10),
                    Partia = SqlSafe.ReadString(reader, 11),
                    Klasa = SqlSafe.ReadInt(reader, 12),
                    CustomerID = SqlSafe.ReadString(reader, 13),
                    Hodowca = SqlSafe.ReadString(reader, 14)
                });
            }
            return result;
        }

        public List<RankingOperatora> BudujRankingOperatorow(List<WazenieRekord> wazenia, int topN)
        {
            var grupy = wazenia
                .Where(w => !string.IsNullOrEmpty(w.OperatorID))
                .GroupBy(w => w.OperatorID + "|" + w.Wagowy)
                .Select(g =>
                {
                    var parts = g.Key.Split('|', 2);
                    var liczbaAnulacji = g.Count(w => w.ActWeight < 0);
                    return new RankingOperatora
                    {
                        OperatorID = parts[0],
                        Wagowy = parts.Length > 1 ? parts[1] : parts[0],
                        LiczbaWazen = g.Count(w => w.ActWeight >= 0),
                        LiczbaAnulacji = liczbaAnulacji,
                        SumaKg = g.Sum(w => w.ActWeight),
                        SredniaKg = g.Where(w => w.ActWeight >= 0).Select(w => w.ActWeight).DefaultIfEmpty(0).Average()
                    };
                })
                .OrderByDescending(r => r.SumaKg)
                .Take(topN)
                .ToList();

            decimal lider = grupy.Count > 0 ? grupy[0].SumaKg : 0;
            for (int i = 0; i < grupy.Count; i++)
            {
                grupy[i].Pozycja = i + 1;
                grupy[i].ProcentLidera = lider <= 0 ? 0 : grupy[i].SumaKg / lider * 100m;
            }
            return grupy;
        }

        public List<RankingPartii> BudujRankingPartii(List<WazenieRekord> wazenia, int topN)
        {
            return wazenia
                .Where(w => !string.IsNullOrEmpty(w.Partia))
                .GroupBy(w => w.Partia)
                .Select(g => new RankingPartii
                {
                    Partia = g.Key,
                    Hodowca = g.Select(w => w.Hodowca).FirstOrDefault(s => !string.IsNullOrEmpty(s)) ?? "",
                    PierwszeWazenie = g.Min(w => w.Godzina),
                    OstatnieWazenie = g.Max(w => w.Godzina),
                    LiczbaWazen = g.Count(w => w.ActWeight >= 0),
                    SumaKg = g.Sum(w => w.ActWeight),
                    Towary = g.Select(w => w.NazwaTowaru).Where(s => !string.IsNullOrEmpty(s)).Distinct().Take(5).ToList()
                })
                .OrderByDescending(r => r.SumaKg)
                .Take(topN)
                .ToList();
        }

        public List<HeatmapaGodzinowa> BudujHeatmapeGodzinowa(List<WazenieRekord> wazenia, int dniWstecz)
        {
            var dataMin = DateTime.Today.AddDays(-dniWstecz);
            var dni = wazenia
                .Where(w => w.Data.Date >= dataMin && w.ActWeight > 0)
                .GroupBy(w => w.Data.Date)
                .OrderBy(g => g.Key)
                .Select(g => new HeatmapaGodzinowa
                {
                    Data = g.Key,
                    KgPerGodzina = g.GroupBy(w => w.Godzina.Hour)
                        .ToDictionary(gg => gg.Key, gg => gg.Sum(w => w.ActWeight))
                })
                .ToList();
            return dni;
        }

        public List<StatystykaZmian> BudujStatystykiZmian(List<WazenieRekord> wazenia)
        {
            int dStart = AnalitykaConfig.ZmianaDziennaStart;
            int nStart = AnalitykaConfig.ZmianaNocnaStart;

            return wazenia
                .Where(w => w.ActWeight > 0)
                .GroupBy(w => w.Data.Date)
                .OrderBy(g => g.Key)
                .Select(g => new StatystykaZmian
                {
                    Data = g.Key,
                    KgZmianaDzienna = g.Where(w => CzyZmianaDzienna(w.Godzina.Hour, dStart, nStart)).Sum(w => w.ActWeight),
                    LiczbaWazenDzienna = g.Count(w => CzyZmianaDzienna(w.Godzina.Hour, dStart, nStart)),
                    KgZmianaNocna = g.Where(w => !CzyZmianaDzienna(w.Godzina.Hour, dStart, nStart)).Sum(w => w.ActWeight),
                    LiczbaWazenNocna = g.Count(w => !CzyZmianaDzienna(w.Godzina.Hour, dStart, nStart))
                })
                .ToList();
        }

        private static bool CzyZmianaDzienna(int godzina, int dStart, int nStart)
            => godzina >= dStart && godzina < nStart;

        // ──────────── Combo loaders ────────────

        public async Task<List<TowarComboItem>> LoadTowaryLibraAsync()
        {
            var lista = new List<TowarComboItem>
            {
                new() { IdHandel = 0, KodHandel = "— Wszystkie towary —" }
            };

            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                SELECT ID, ShortName, Name FROM dbo.Article
                WHERE ID IS NOT NULL AND ID <> '' AND Name IS NOT NULL AND Name <> ''
                ORDER BY Name", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string id = SqlSafe.ReadString(reader, 0);
                string skrot = SqlSafe.ReadString(reader, 1);
                string nazwa = SqlSafe.ReadString(reader, 2);
                if (string.IsNullOrEmpty(id)) continue;
                int.TryParse(id, out int idInt);
                lista.Add(new TowarComboItem
                {
                    IdHandel = idInt,
                    KodHandel = id,  // używamy ArticleID jako klucza
                    Nazwa = !string.IsNullOrEmpty(skrot) ? $"{skrot} - {nazwa}" : nazwa
                });
            }
            return lista;
        }

        public async Task<List<OperatorComboItem>> LoadOperatorzyAsync()
        {
            var lista = new List<OperatorComboItem>
            {
                new() { OperatorID = "", Wagowy = "— Wszyscy operatorzy —" }
            };
            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                SELECT DISTINCT OperatorID, Wagowy
                FROM dbo.In0E
                WHERE OperatorID IS NOT NULL AND Wagowy IS NOT NULL AND Wagowy <> ''
                  AND Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
                ORDER BY Wagowy", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string id = SqlSafe.ReadString(reader, 0);
                string nazwa = SqlSafe.ReadString(reader, 1);
                if (!string.IsNullOrEmpty(id))
                    lista.Add(new OperatorComboItem { OperatorID = id, Wagowy = nazwa });
            }
            return lista;
        }

        public async Task<List<HodowcaComboItem>> LoadHodowcyAsync()
        {
            var lista = new List<HodowcaComboItem>
            {
                new() { CustomerID = "", CustomerName = "— Wszyscy hodowcy —" }
            };
            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                SELECT DISTINCT pd.CustomerID, pd.CustomerName
                FROM dbo.PartiaDostawca pd
                WHERE pd.CustomerName IS NOT NULL AND pd.CustomerName <> ''
                  AND pd.Partia IN (
                      SELECT DISTINCT P1 FROM dbo.In0E
                      WHERE P1 IS NOT NULL AND P1 <> ''
                        AND Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
                  )
                ORDER BY pd.CustomerName", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string id = SqlSafe.ReadString(reader, 0);
                string name = SqlSafe.ReadString(reader, 1);
                if (!string.IsNullOrEmpty(name))
                    lista.Add(new HodowcaComboItem { CustomerID = id, CustomerName = name });
            }
            return lista;
        }
    }
}
