using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Kalendarz1.AnalizaPrzychoduProdukcji.Models;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.AnalizaPrzychoduProdukcji.Services
{
    public class PrzychodService
    {
        private readonly string _connLibra;

        // Filtr historyczny – ignoruj śmieci sprzed 2024
        private const string MinPartiaCreateData = "2024-01-01";

        public PrzychodService(string connLibra)
        {
            _connLibra = connLibra;
        }

        public async Task<List<PrzychodRecord>> LoadDataAsync(PrzychodFilter filter)
        {
            var result = new List<PrzychodRecord>();

            var sb = new StringBuilder(@"
                SELECT
                    e.ArticleID, e.ArticleName, e.JM, e.TermID, e.TermType, e.Weight, e.Quantity,
                    e.Direction, e.Data, e.Godzina, e.OperatorID, e.Wagowy, e.Tara, e.Price,
                    e.P1, e.P2, e.ActWeight, e.QntInCont
                FROM dbo.In0E e
                WHERE e.Data >= @DataOd AND e.Data <= @DataDo
                  AND ISNULL(e.ArticleName,'') <> ''");

            if (!string.IsNullOrEmpty(filter.ArticleID)) sb.Append(" AND e.ArticleID = @ArticleID");
            if (!string.IsNullOrEmpty(filter.OperatorID)) sb.Append(" AND e.OperatorID = @OperatorID");
            if (filter.TerminalId.HasValue && filter.TerminalId.Value > 0) sb.Append(" AND e.TermID = @TermID");
            if (!string.IsNullOrEmpty(filter.Partia)) sb.Append(" AND e.P1 = @Partia");
            if (filter.Klasa.HasValue) sb.Append(" AND e.QntInCont = @Klasa");
            if (filter.GodzinaOd.HasValue && filter.GodzinaOd.Value >= 0)
                sb.Append(" AND TRY_CAST(LEFT(e.Godzina,2) AS INT) >= @GodzOd");
            if (filter.GodzinaDo.HasValue && filter.GodzinaDo.Value >= 0)
                sb.Append(" AND TRY_CAST(LEFT(e.Godzina,2) AS INT) <= @GodzDo");
            // Filtr po dostawcy = subquery na PartiaDostawca
            if (!string.IsNullOrEmpty(filter.Dostawca))
                sb.Append(" AND e.P1 IN (SELECT pd.Partia FROM dbo.PartiaDostawca pd WHERE pd.CustomerID = @Dostawca OR pd.CustomerName = @Dostawca)");
            // Typ operatora - filtrujemy w pamięci, bo wymaga grupowania (operator może mieć i ArticleID=40 i inne)

            sb.Append(" ORDER BY e.Data, e.Godzina");

            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sb.ToString(), conn);
            cmd.Parameters.AddWithValue("@DataOd", filter.DataOd.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@DataDo", filter.DataDo.ToString("yyyy-MM-dd"));
            if (!string.IsNullOrEmpty(filter.ArticleID)) cmd.Parameters.AddWithValue("@ArticleID", filter.ArticleID);
            if (!string.IsNullOrEmpty(filter.OperatorID)) cmd.Parameters.AddWithValue("@OperatorID", filter.OperatorID);
            if (filter.TerminalId.HasValue && filter.TerminalId.Value > 0) cmd.Parameters.AddWithValue("@TermID", filter.TerminalId.Value);
            if (!string.IsNullOrEmpty(filter.Partia)) cmd.Parameters.AddWithValue("@Partia", filter.Partia);
            if (filter.Klasa.HasValue) cmd.Parameters.AddWithValue("@Klasa", filter.Klasa.Value);
            if (filter.GodzinaOd.HasValue && filter.GodzinaOd.Value >= 0) cmd.Parameters.AddWithValue("@GodzOd", filter.GodzinaOd.Value);
            if (filter.GodzinaDo.HasValue && filter.GodzinaDo.Value >= 0) cmd.Parameters.AddWithValue("@GodzDo", filter.GodzinaDo.Value);
            if (!string.IsNullOrEmpty(filter.Dostawca)) cmd.Parameters.AddWithValue("@Dostawca", filter.Dostawca);

            using var reader = await cmd.ExecuteReaderAsync();
            int fieldCount = reader.FieldCount;
            while (await reader.ReadAsync())
            {
                DateTime dataValue = DateTime.MinValue;
                DateTime godzinaValue = DateTime.MinValue;

                string dataStr = (fieldCount > 8 && !reader.IsDBNull(8)) ? reader.GetValue(8)?.ToString() ?? "" : "";
                string godzinaStr = (fieldCount > 9 && !reader.IsDBNull(9)) ? reader.GetValue(9)?.ToString() ?? "" : "";

                DateTime.TryParse(dataStr, out dataValue);
                if (!string.IsNullOrEmpty(godzinaStr))
                {
                    if (TimeSpan.TryParse(godzinaStr, out TimeSpan ts))
                        godzinaValue = dataValue.Date.Add(ts);
                    else
                        DateTime.TryParse(godzinaStr, out godzinaValue);
                }

                result.Add(new PrzychodRecord
                {
                    ArticleID = (fieldCount > 0 && !reader.IsDBNull(0)) ? reader.GetValue(0)?.ToString() ?? "" : "",
                    NazwaTowaru = (fieldCount > 1 && !reader.IsDBNull(1)) ? reader.GetValue(1)?.ToString() ?? "" : "",
                    JM = (fieldCount > 2 && !reader.IsDBNull(2)) ? reader.GetValue(2)?.ToString() ?? "" : "",
                    TermID = (fieldCount > 3 && !reader.IsDBNull(3)) ? Convert.ToInt32(reader.GetValue(3)) : 0,
                    Terminal = (fieldCount > 4 && !reader.IsDBNull(4)) ? reader.GetValue(4)?.ToString() ?? "" : "",
                    Weight = (fieldCount > 5 && !reader.IsDBNull(5)) ? Convert.ToDecimal(reader.GetValue(5)) : 0,
                    Data = dataValue,
                    Godzina = godzinaValue,
                    OperatorID = (fieldCount > 10 && !reader.IsDBNull(10)) ? reader.GetValue(10)?.ToString() ?? "" : "",
                    Operator = (fieldCount > 11 && !reader.IsDBNull(11)) ? reader.GetValue(11)?.ToString() ?? "" : "",
                    Tara = (fieldCount > 12 && !reader.IsDBNull(12)) ? Convert.ToDecimal(reader.GetValue(12)) : 0,
                    Partia = (fieldCount > 14 && !reader.IsDBNull(14)) ? reader.GetValue(14)?.ToString() ?? "" : "",
                    ActWeight = (fieldCount > 16 && !reader.IsDBNull(16)) ? Convert.ToDecimal(reader.GetValue(16)) : 0,
                    Klasa = (fieldCount > 17 && !reader.IsDBNull(17)) ? Convert.ToInt32(reader.GetValue(17)) : 0
                });
            }

            return result;
        }

        public async Task<List<ComboItemString>> LoadTowaryAsync(Dictionary<string, string> outDict = null)
        {
            var towary = new List<ComboItemString> { new ComboItemString { Wartosc = "", Nazwa = "-- Wszystkie towary --" } };

            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                SELECT ID, Name, ShortName
                FROM dbo.Article
                WHERE ID IS NOT NULL AND ID <> '' AND Name IS NOT NULL AND Name <> ''
                ORDER BY Name", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string id = reader.IsDBNull(0) ? "" : reader.GetValue(0)?.ToString() ?? "";
                string nazwa = reader.IsDBNull(1) ? "" : reader.GetValue(1)?.ToString() ?? "";
                string skrot = reader.IsDBNull(2) ? "" : reader.GetValue(2)?.ToString() ?? "";
                if (!string.IsNullOrEmpty(id))
                {
                    string displayName = !string.IsNullOrEmpty(skrot) ? $"{skrot} - {nazwa}" : nazwa;
                    towary.Add(new ComboItemString { Wartosc = id, Nazwa = displayName });
                    if (outDict != null) outDict[id] = nazwa;
                }
            }
            return towary;
        }

        public async Task<List<ComboItemString>> LoadOperatorzyAsync(Dictionary<string, string> outDict = null)
        {
            var operatorzy = new List<ComboItemString> { new ComboItemString { Wartosc = "", Nazwa = "-- Wszyscy operatorzy --" } };

            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            // Tylko operatorzy aktywni w ostatnich 90 dniach
            using var cmd = new SqlCommand(@"
                SELECT DISTINCT OperatorID, Wagowy
                FROM dbo.In0E
                WHERE OperatorID IS NOT NULL AND Wagowy IS NOT NULL AND Wagowy <> ''
                  AND Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
                ORDER BY Wagowy", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string id = reader.IsDBNull(0) ? "" : reader.GetValue(0)?.ToString() ?? "";
                string nazwa = reader.IsDBNull(1) ? $"Operator {id}" : reader.GetValue(1)?.ToString() ?? "";
                if (!string.IsNullOrEmpty(id))
                {
                    operatorzy.Add(new ComboItemString { Wartosc = id, Nazwa = nazwa });
                    if (outDict != null) outDict[id] = nazwa;
                }
            }
            return operatorzy;
        }

        public async Task<List<ComboItem>> LoadTerminaleAsync()
        {
            var terminale = new List<ComboItem> { new ComboItem { Id = 0, Nazwa = "-- Wszystkie --" } };

            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                SELECT DISTINCT TermID, TermType
                FROM dbo.In0E
                WHERE TermID IS NOT NULL
                ORDER BY TermID", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                int id = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0));
                string typ = reader.IsDBNull(1) ? $"T{id}" : reader.GetValue(1)?.ToString() ?? $"T{id}";
                if (id > 0) terminale.Add(new ComboItem { Id = id, Nazwa = typ });
            }
            return terminale;
        }

        public async Task<List<ComboItemString>> LoadPartieAsync()
        {
            var partie = new List<ComboItemString> { new ComboItemString { Wartosc = "", Nazwa = "-- Wszystkie partie --" } };

            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            // TOP 200 najnowszych aktywnych partii (mających ważenia w ostatnich 60 dniach)
            using var cmd = new SqlCommand(@"
                SELECT DISTINCT TOP 200 P1
                FROM dbo.In0E
                WHERE P1 IS NOT NULL AND P1 <> ''
                  AND Data >= CONVERT(varchar(10), DATEADD(DAY, -60, GETDATE()), 120)
                ORDER BY P1 DESC", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string p1 = reader.IsDBNull(0) ? "" : reader.GetValue(0)?.ToString() ?? "";
                if (!string.IsNullOrEmpty(p1)) partie.Add(new ComboItemString { Wartosc = p1, Nazwa = p1 });
            }
            return partie;
        }

        public async Task<List<ComboItemString>> LoadDostawcyAsync()
        {
            var dostawcy = new List<ComboItemString> { new ComboItemString { Wartosc = "", Nazwa = "-- Wszyscy dostawcy --" } };

            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            // Tylko dostawcy z partii aktywnych w ostatnich 90 dniach
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
                string id = reader.IsDBNull(0) ? "" : reader.GetValue(0)?.ToString() ?? "";
                string name = reader.IsDBNull(1) ? "" : reader.GetValue(1)?.ToString() ?? "";
                if (!string.IsNullOrEmpty(name))
                    dostawcy.Add(new ComboItemString { Wartosc = id, Nazwa = name });
            }
            return dostawcy;
        }

        public async Task<Dictionary<string, (string CustomerID, string CustomerName)>> LoadPartiaDostawcaMapAsync(string dataOd, string dataDo)
        {
            var dict = new Dictionary<string, (string, string)>();

            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                SELECT pd.Partia, pd.CustomerID, pd.CustomerName
                FROM dbo.PartiaDostawca pd
                WHERE pd.Partia IN (
                    SELECT DISTINCT P1 FROM dbo.In0E
                    WHERE P1 IS NOT NULL AND P1 <> ''
                      AND Data >= @DataOd AND Data <= @DataDo
                )", conn);
            cmd.Parameters.AddWithValue("@DataOd", dataOd);
            cmd.Parameters.AddWithValue("@DataDo", dataDo);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string partia = reader.IsDBNull(0) ? "" : reader.GetValue(0)?.ToString() ?? "";
                string custId = reader.IsDBNull(1) ? "" : reader.GetValue(1)?.ToString() ?? "";
                string custName = reader.IsDBNull(2) ? "" : reader.GetValue(2)?.ToString() ?? "";
                if (!string.IsNullOrEmpty(partia) && !dict.ContainsKey(partia))
                    dict[partia] = (custId, custName);
            }
            return dict;
        }

        public async Task<Dictionary<string, ArticleInfo>> LoadArticlesAsync()
        {
            var dict = new Dictionary<string, ArticleInfo>();

            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                SELECT ID, ShortName, Name
                FROM dbo.Article
                WHERE ID IS NOT NULL AND ID <> ''", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string id = reader.IsDBNull(0) ? "" : reader.GetValue(0)?.ToString() ?? "";
                string shortName = reader.IsDBNull(1) ? "" : reader.GetValue(1)?.ToString() ?? "";
                string name = reader.IsDBNull(2) ? "" : reader.GetValue(2)?.ToString() ?? "";

                if (!string.IsNullOrEmpty(id) && !dict.ContainsKey(id))
                {
                    dict[id] = new ArticleInfo { ID = id, ShortName = shortName, Name = name };
                }
            }
            return dict;
        }

        // ─────────────────────────────────────────────────────
        //  SPRZEDAŻ – Out1A (kg sprzedane + wartość zł)
        // ─────────────────────────────────────────────────────

        public async Task<List<SalesRecord>> LoadSalesAsync(PrzychodFilter filter)
        {
            var result = new List<SalesRecord>();

            var sb = new StringBuilder(@"
                SELECT
                    o.ArticleID, o.ArticleName, o.CustomerID,
                    o.Data, o.Godzina, o.Weight, o.ActWeight,
                    o.Price, o.P1, o.Related_IN, o.DocNo, o.OrderNo
                FROM dbo.Out1A o
                WHERE o.Data >= @DataOd AND o.Data <= @DataDo
                  AND ISNULL(o.ArticleName,'') <> ''");

            if (!string.IsNullOrEmpty(filter.ArticleID)) sb.Append(" AND o.ArticleID = @ArticleID");
            if (!string.IsNullOrEmpty(filter.Partia)) sb.Append(" AND (o.P1 = @Partia OR o.Related_IN = @Partia)");

            sb.Append(" ORDER BY o.Data DESC, o.Godzina DESC");

            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sb.ToString(), conn);
            cmd.Parameters.AddWithValue("@DataOd", filter.DataOd.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@DataDo", filter.DataDo.ToString("yyyy-MM-dd"));
            if (!string.IsNullOrEmpty(filter.ArticleID)) cmd.Parameters.AddWithValue("@ArticleID", filter.ArticleID);
            if (!string.IsNullOrEmpty(filter.Partia)) cmd.Parameters.AddWithValue("@Partia", filter.Partia);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                DateTime dataValue = DateTime.MinValue;
                DateTime godzinaValue = DateTime.MinValue;

                string dataStr = !reader.IsDBNull(3) ? reader.GetValue(3)?.ToString() ?? "" : "";
                string godzinaStr = !reader.IsDBNull(4) ? reader.GetValue(4)?.ToString() ?? "" : "";

                DateTime.TryParse(dataStr, out dataValue);
                if (!string.IsNullOrEmpty(godzinaStr) && TimeSpan.TryParse(godzinaStr, out TimeSpan ts))
                    godzinaValue = dataValue.Date.Add(ts);

                decimal actW = !reader.IsDBNull(6) ? Convert.ToDecimal(reader.GetValue(6)) : 0;
                decimal price = !reader.IsDBNull(7) ? Convert.ToDecimal(reader.GetValue(7)) : 0;

                result.Add(new SalesRecord
                {
                    ArticleID = !reader.IsDBNull(0) ? reader.GetValue(0)?.ToString() ?? "" : "",
                    ArticleName = !reader.IsDBNull(1) ? reader.GetValue(1)?.ToString() ?? "" : "",
                    CustomerID = !reader.IsDBNull(2) ? reader.GetValue(2)?.ToString() ?? "" : "",
                    CustomerName = "", // dosypiemy z mapy klientów
                    Data = dataValue,
                    Godzina = godzinaValue,
                    Weight = !reader.IsDBNull(5) ? Convert.ToDecimal(reader.GetValue(5)) : 0,
                    ActWeight = actW,
                    Price = price,
                    Wartosc = actW * price,
                    PartiaOut = !reader.IsDBNull(8) ? reader.GetValue(8)?.ToString() ?? "" : "",
                    PartiaIn = !reader.IsDBNull(9) ? reader.GetValue(9)?.ToString() ?? "" : "",
                    DocNo = !reader.IsDBNull(10) ? Convert.ToInt32(reader.GetValue(10)) : (int?)null,
                    OrderNo = !reader.IsDBNull(11) ? reader.GetValue(11)?.ToString() ?? "" : ""
                });
            }

            return result;
        }
    }
}
