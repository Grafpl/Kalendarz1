using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Kalendarz1.AnalitykaPelna.Services;

namespace Kalendarz1.DOA
{
    /// <summary>
    /// DOA (Dead On Arrival) — ANALITYKA padłych sztuk.
    /// Źródło danych: FarmerCalc (LibraNet) — DeclI1 = SztukiDek (zadeklarowane),
    /// DeclI2 = Padle. Te dane są wpisywane w module Specyfikacji Drobiu (WidokSpecyfikacje).
    /// DOA NIE tworzy własnej tabeli — tylko czyta i analizuje istniejące dane.
    /// </summary>
    public class DOAService
    {
        private readonly string _conn;

        public DOAService()
        {
            AnalitykaConfig.ZaladujJesliTrzeba();
            _conn = AnalitykaConfig.ConnLibraNet;
        }

        /// <summary>Ranking hodowców po średnim DOA (z FarmerCalc, GROUP BY CustomerGID).</summary>
        public async Task<List<DOAHodowca>> GetRankingHodowcowAsync(DateTime od, DateTime doDate)
        {
            var lista = new List<DOAHodowca>();
            const string sql = @"
SELECT LTRIM(RTRIM(fc.CustomerGID)) AS HodowcaId,
       MAX(ISNULL(cust.ShortName, '(brak nazwy)')) AS Hodowca,
       COUNT(*) AS LiczbaPartii,
       SUM(CAST(ISNULL(fc.DeclI1,0) AS BIGINT)) AS SumaSztukDek,
       SUM(CAST(ISNULL(fc.DeclI2,0) AS BIGINT)) AS SumaPadlych
FROM [LibraNet].[dbo].[FarmerCalc] fc
LEFT JOIN [LibraNet].[dbo].[Dostawcy] cust ON fc.CustomerGID = cust.ID
WHERE fc.CalcDate BETWEEN @Od AND @Do
GROUP BY LTRIM(RTRIM(fc.CustomerGID))
HAVING SUM(CAST(ISNULL(fc.DeclI1,0) AS BIGINT)) > 0
ORDER BY (CAST(SUM(CAST(ISNULL(fc.DeclI2,0) AS BIGINT)) AS DECIMAL(18,4))
          / NULLIF(SUM(CAST(ISNULL(fc.DeclI1,0) AS BIGINT)),0)) DESC;";

            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@Od", od.Date);
            cmd.Parameters.AddWithValue("@Do", doDate.Date);
            using var r = await cmd.ExecuteReaderAsync();
            int poz = 1;
            while (await r.ReadAsync())
            {
                lista.Add(new DOAHodowca
                {
                    Pozycja = poz++,
                    HodowcaId = r.IsDBNull(0) ? null : r.GetString(0),
                    Hodowca = r.GetString(1),
                    LiczbaPartii = r.GetInt32(2),
                    SumaSztukDek = r.GetInt64(3),
                    SumaPadlych = r.GetInt64(4)
                });
            }
            return lista;
        }

        /// <summary>Szczegółowe rekordy (per dostawa/partia) z FarmerCalc w zakresie dat.</summary>
        public async Task<List<DOARekord>> GetRekordyAsync(DateTime od, DateTime doDate)
        {
            var lista = new List<DOARekord>();
            const string sql = @"
SELECT fc.CalcDate, fc.CarLp,
       COALESCE(fc.PartiaNumber, CONCAT(pd.CustomerID, pd.Partia), '') AS Partia,
       ISNULL(cust.ShortName, '(brak)') AS Hodowca,
       ISNULL(fc.DeclI1,0) AS SztukiDek,
       ISNULL(fc.DeclI2,0) AS Padle
FROM [LibraNet].[dbo].[FarmerCalc] fc
LEFT JOIN [LibraNet].[dbo].[PartiaDostawca] pd ON fc.PartiaGuid = pd.guid
LEFT JOIN [LibraNet].[dbo].[Dostawcy] cust ON fc.CustomerGID = cust.ID
WHERE fc.CalcDate BETWEEN @Od AND @Do
ORDER BY fc.CalcDate DESC, fc.CarLp;";

            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@Od", od.Date);
            cmd.Parameters.AddWithValue("@Do", doDate.Date);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                lista.Add(new DOARekord
                {
                    Data = r.GetDateTime(0),
                    CarLp = r.IsDBNull(1) ? 0 : r.GetInt32(1),
                    Partia = r.GetString(2),
                    Hodowca = r.GetString(3)?.Trim() ?? "",
                    SztukiDek = r.GetInt32(4),
                    Padle = r.GetInt32(5)
                });
            }
            return lista;
        }
    }
}
