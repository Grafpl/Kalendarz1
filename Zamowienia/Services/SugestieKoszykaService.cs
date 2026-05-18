using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Kalendarz1.Zamowienia.Services
{
    /// <summary>
    /// Smart-fill koszyka — proponuje produkty per klient na bazie historii zamówień.
    /// Wzorzec analogiczny do NotatkiService. Czyta ZamowieniaMieso + ZamowieniaMiesoTowar
    /// (LibraNet); opcjonalnie loguje użycia chipów do KoszykiUzycia (auto-learning).
    /// </summary>
    public class SugestieKoszykaService
    {
        private readonly string _connLibra;
        private bool _strefaChecked;
        private bool _strefaExists;

        public SugestieKoszykaService(string connLibra) { _connLibra = connLibra; }

        public const string AkcjaWstawiona = "Wstawiona";   // klik chipa = pre-fill
        public const string AkcjaWpisana = "Wpisana";       // ręcznie zapisany przy save

        public class SugestiaProduktu
        {
            public int KodTowaru { get; set; }
            public decimal SugerowanaIlosc { get; set; }
            public string SugerowanaCena { get; set; } = "";

            // Procent zamówień (90 dni) gdzie flaga była ustawiona (0.0–1.0).
            // Pre-fill karty / chip status używa DOMINANT_THRESHOLD (≥0.7).
            public decimal E2Pct { get; set; }
            public decimal FoliaPct { get; set; }
            public decimal HallalPct { get; set; }
            public decimal StrefaPct { get; set; }
            public const decimal DOMINANT_THRESHOLD = 0.7m;
            public bool E2Dominant => E2Pct >= DOMINANT_THRESHOLD;
            public bool FoliaDominant => FoliaPct >= DOMINANT_THRESHOLD;
            public bool HallalDominant => HallalPct >= DOMINANT_THRESHOLD;
            public bool StrefaDominant => StrefaPct >= DOMINANT_THRESHOLD;

            public DateTime OstatnioUzyte { get; set; }
            public int Czestotliwosc { get; set; }
            public int CzestotliwoscOstatnich30 { get; set; }
            public double Score { get; set; }
        }

        public async Task EnsureSchemaAsync()
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            const string ddl = @"
                IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name='KoszykiUzycia' AND type='U')
                BEGIN
                    CREATE TABLE dbo.KoszykiUzycia (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        KlientId INT NULL,
                        UserId NVARCHAR(50) NULL,
                        KodTowaru INT NOT NULL,
                        Ilosc DECIMAL(18,2) NOT NULL DEFAULT 0,
                        Akcja NVARCHAR(20) NOT NULL,
                        DataAkcji DATETIME NOT NULL DEFAULT GETDATE()
                    );
                    CREATE INDEX IX_KoszykiUzycia_Klient ON dbo.KoszykiUzycia(KlientId, KodTowaru);
                    CREATE INDEX IX_KoszykiUzycia_User ON dbo.KoszykiUzycia(UserId);
                END;";
            await using var cmd = new SqlCommand(ddl, cn);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<SugestiaProduktu>> GetSuggestionsAsync(int klientId, int days = 90, int max = 12)
        {
            var result = new List<SugestiaProduktu>();
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            if (!_strefaChecked)
            {
                await using var col = new SqlCommand(
                    "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMiesoTowar') AND name = 'Strefa'", cn);
                _strefaExists = Convert.ToInt32(await col.ExecuteScalarAsync()) > 0;
                _strefaChecked = true;
            }

            string strefaAvg = _strefaExists
                ? "AVG(CAST(CASE WHEN ISNULL(zt.Strefa,0)=1 THEN 1.0 ELSE 0.0 END AS DECIMAL(6,4)))"
                : "CAST(0 AS DECIMAL(6,4))";

            string sql = $@"
                ;WITH TowaryAgg AS (
                    SELECT
                        zt.KodTowaru,
                        COUNT(*) AS Czestotliwosc,
                        MAX(zm.DataPrzyjazdu) AS Ostatnio,
                        SUM(CASE WHEN zm.DataPrzyjazdu > DATEADD(DAY, -30, GETDATE()) THEN 1 ELSE 0 END) AS Ostatnich30,
                        AVG(CAST(CASE WHEN ISNULL(zt.E2,0)=1     THEN 1.0 ELSE 0.0 END AS DECIMAL(6,4))) AS E2Pct,
                        AVG(CAST(CASE WHEN ISNULL(zt.Folia,0)=1  THEN 1.0 ELSE 0.0 END AS DECIMAL(6,4))) AS FoliaPct,
                        AVG(CAST(CASE WHEN ISNULL(zt.Hallal,0)=1 THEN 1.0 ELSE 0.0 END AS DECIMAL(6,4))) AS HallalPct,
                        {strefaAvg} AS StrefaPct
                    FROM dbo.ZamowieniaMieso zm
                    INNER JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = zm.Id
                    WHERE zm.KlientId = @kid
                      AND zm.DataPrzyjazdu > DATEADD(DAY, -@days, GETDATE())
                      AND ISNULL(zm.Status,'') NOT IN ('Anulowane','Anulowano')
                      AND zt.Ilosc > 0
                      AND zt.KodTowaru IS NOT NULL
                    GROUP BY zt.KodTowaru
                    HAVING COUNT(*) >= 2
                ),
                LastValues AS (
                    SELECT
                        zt.KodTowaru,
                        zt.Ilosc,
                        zt.Cena,
                        ROW_NUMBER() OVER (PARTITION BY zt.KodTowaru ORDER BY zm.DataPrzyjazdu DESC, zm.Id DESC) AS rn
                    FROM dbo.ZamowieniaMieso zm
                    INNER JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = zm.Id
                    WHERE zm.KlientId = @kid
                      AND zm.DataPrzyjazdu > DATEADD(DAY, -@days, GETDATE())
                      AND ISNULL(zm.Status,'') NOT IN ('Anulowane','Anulowano')
                      AND zt.Ilosc > 0
                      AND zt.KodTowaru IS NOT NULL
                )
                SELECT TOP (@max)
                    a.KodTowaru,
                    a.Czestotliwosc,
                    a.Ostatnio,
                    a.Ostatnich30,
                    a.E2Pct, a.FoliaPct, a.HallalPct, a.StrefaPct,
                    l.Ilosc AS LastIlosc,
                    l.Cena AS LastCena
                FROM TowaryAgg a
                INNER JOIN LastValues l ON l.KodTowaru = a.KodTowaru AND l.rn = 1
                ORDER BY a.Czestotliwosc DESC, a.Ostatnio DESC";

            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.Add("@kid", SqlDbType.Int).Value = klientId;
            cmd.Parameters.Add("@days", SqlDbType.Int).Value = Math.Max(7, days);
            cmd.Parameters.Add("@max", SqlDbType.Int).Value = Math.Max(1, Math.Min(50, max));

            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                if (!int.TryParse(rd["KodTowaru"]?.ToString(), out int kod)) continue;
                var p = new SugestiaProduktu
                {
                    KodTowaru = kod,
                    Czestotliwosc = Convert.ToInt32(rd["Czestotliwosc"]),
                    CzestotliwoscOstatnich30 = Convert.ToInt32(rd["Ostatnich30"]),
                    OstatnioUzyte = rd["Ostatnio"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(rd["Ostatnio"]),
                    SugerowanaIlosc = rd["LastIlosc"] == DBNull.Value ? 0m : Convert.ToDecimal(rd["LastIlosc"]),
                    SugerowanaCena = rd["LastCena"]?.ToString() ?? "",
                    E2Pct = rd["E2Pct"] == DBNull.Value ? 0m : Convert.ToDecimal(rd["E2Pct"]),
                    FoliaPct = rd["FoliaPct"] == DBNull.Value ? 0m : Convert.ToDecimal(rd["FoliaPct"]),
                    HallalPct = rd["HallalPct"] == DBNull.Value ? 0m : Convert.ToDecimal(rd["HallalPct"]),
                    StrefaPct = rd["StrefaPct"] == DBNull.Value ? 0m : Convert.ToDecimal(rd["StrefaPct"])
                };
                p.Score = ComputeScore(p.Czestotliwosc, p.CzestotliwoscOstatnich30, p.OstatnioUzyte);
                result.Add(p);
            }

            return result.OrderByDescending(x => x.Score).Take(max).ToList();
        }

        private static double ComputeScore(int freq, int freq30d, DateTime ostatnio)
        {
            double dni = ostatnio == DateTime.MinValue ? 365 : (DateTime.Now - ostatnio).TotalDays;
            double recency = 1.0 + 2.0 * Math.Exp(-dni / 30.0);
            double frequency = 1.0 + Math.Log(1 + freq) / Math.Log(20.0);
            double recentBoost = freq30d > 0 ? 1.0 + 0.3 * Math.Log(1 + freq30d) : 1.0;
            return frequency * recency * recentBoost;
        }

        public async Task LogUsageAsync(int? klientId, string userId, int kodTowaru, decimal ilosc, string akcja)
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                const string ins = @"
                    INSERT INTO dbo.KoszykiUzycia (KlientId, UserId, KodTowaru, Ilosc, Akcja)
                    VALUES (@kid, @uid, @kod, @il, @a)";
                await using var cmd = new SqlCommand(ins, cn);
                cmd.Parameters.AddWithValue("@kid", (object?)klientId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@uid", (object?)userId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@kod", kodTowaru);
                cmd.Parameters.AddWithValue("@il", ilosc);
                cmd.Parameters.AddWithValue("@a", akcja ?? "");
                await cmd.ExecuteNonQueryAsync();
            }
            catch { /* tracking best-effort */ }
        }
    }
}
