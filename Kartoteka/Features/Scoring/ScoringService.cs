using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.Kartoteka.Models;

namespace Kalendarz1.Kartoteka.Features.Scoring
{
    public class ScoringService
    {
        private readonly string _connLibra;
        private readonly string _connHandel;

        public ScoringService(string connLibra, string connHandel)
        {
            _connLibra = connLibra;
            _connHandel = connHandel;
        }

        public async Task EnsureTableExistsAsync()
        {
            const string sql = @"
                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.KartotekaScoring') AND type = 'U')
                BEGIN
                    CREATE TABLE dbo.KartotekaScoring (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        KlientId INT NOT NULL,
                        TerminowoscPkt INT,
                        HistoriaPkt INT,
                        RegularnoscPkt INT,
                        TrendPkt INT,
                        LimitPkt INT,
                        ScoreTotal INT,
                        Kategoria NVARCHAR(20),
                        RekomendacjaLimitu DECIMAL(18,2),
                        RekomendacjaOpis NVARCHAR(500),
                        DataObliczenia DATETIME DEFAULT GETDATE()
                    );
                    CREATE NONCLUSTERED INDEX IX_Scoring_Klient ON dbo.KartotekaScoring (KlientId);
                    CREATE NONCLUSTERED INDEX IX_Scoring_Score ON dbo.KartotekaScoring (ScoreTotal DESC);
                END";

            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<ScoringResult> ObliczScoringAsync(int klientId)
        {
            var result = new ScoringResult { KlientId = klientId };

            // 1. Terminowość płatności
            var (faktury, terminowe) = await PobierzTerminowoscAsync(klientId);
            result.TerminowoscPkt = ScoringCalculator.ObliczTerminowosc(faktury, terminowe);

            // 2. Historia współpracy
            var pierwszaFaktura = await PobierzPierwszaFaktureAsync(klientId);
            result.HistoriaPkt = ScoringCalculator.ObliczHistorie(pierwszaFaktura);

            // 3. Regularność zamówień
            var datyZamowien = await PobierzDatyZamowienAsync(klientId);
            result.RegularnoscPkt = ScoringCalculator.ObliczRegularnosc(datyZamowien);

            // 4. Trend obrotów
            var (biezace, poprzednie) = await PobierzObrotyAsync(klientId);
            result.TrendPkt = ScoringCalculator.ObliczTrend(biezace, poprzednie);

            // 5. Wykorzystanie limitu
            var (limit, wykorzystano) = await PobierzLimitAsync(klientId);
            result.LimitPkt = ScoringCalculator.ObliczWykorzystanieLimitu(limit, wykorzystano);

            // Suma
            result.ScoreTotal = result.TerminowoscPkt + result.HistoriaPkt +
                                result.RegularnoscPkt + result.TrendPkt + result.LimitPkt;
            result.Kategoria = ScoringCalculator.KategoryzujScore(result.ScoreTotal);
            result.RekomendacjaOpis = ScoringCalculator.RekomendacjaOpisu(result.ScoreTotal, limit);
            result.RekomendacjaLimitu = result.ScoreTotal >= 90 ? limit * 1.2m :
                                        result.ScoreTotal >= 70 ? limit :
                                        result.ScoreTotal >= 50 ? limit :
                                        result.ScoreTotal >= 30 ? limit * 0.7m : 0m;
            result.DataObliczenia = DateTime.Now;

            // Zapisz wynik
            await ZapiszScoringAsync(result);

            return result;
        }

        private async Task<(int faktury, int terminowe)> PobierzTerminowoscAsync(int klientId)
        {
            // HM.DK: khid=kontrahent, data=data faktury, plattermin=termin płatności, ok=rozliczony
            // HM.PN: dkid=FK do DK, kwotarozl=kwota rozliczona, Termin=data faktycznej zapłaty
            const string sql = @"
                SELECT
                    COUNT(*) AS Wszystkie,
                    SUM(CASE WHEN DK.ok = 1 AND ISNULL(PN.DataZaplaty, DK.plattermin) <= DK.plattermin THEN 1 ELSE 0 END) AS Terminowe
                FROM [HM].[DK] DK
                LEFT JOIN (
                    SELECT dkid, MAX(Termin) AS DataZaplaty
                    FROM [HM].[PN]
                    GROUP BY dkid
                ) PN ON PN.dkid = DK.id
                WHERE DK.khid = @KlientId
                  AND DK.typ_dk IN ('FVS', 'FVR', 'FVZ')
                  AND DK.aktywny = 1
                  AND DK.anulowany = 0
                  AND DK.data >= DATEADD(YEAR, -2, GETDATE())";

            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@KlientId", klientId);
                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                    return (rd.GetInt32(0), rd.IsDBNull(1) ? 0 : rd.GetInt32(1));
            }
            catch { }
            return (0, 0);
        }

        private async Task<DateTime?> PobierzPierwszaFaktureAsync(int klientId)
        {
            const string sql = @"SELECT MIN(DK.data)
                                 FROM [HM].[DK] DK
                                 WHERE DK.khid = @KlientId
                                   AND DK.typ_dk IN ('FVS', 'FVR', 'FVZ')
                                   AND DK.aktywny = 1
                                   AND DK.anulowany = 0";
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@KlientId", klientId);
                var result = await cmd.ExecuteScalarAsync();
                return result != null && result != DBNull.Value ? (DateTime)result : null;
            }
            catch { return null; }
        }

        private async Task<List<DateTime>> PobierzDatyZamowienAsync(int klientId)
        {
            const string sql = @"SELECT DISTINCT CAST(DataUtworzenia AS DATE) AS DataZam
                                 FROM dbo.ZamowieniaMieso
                                 WHERE KlientId = @KlientId AND Status != 'Anulowane'
                                   AND DataUtworzenia >= DATEADD(MONTH, -6, GETDATE())
                                 ORDER BY DataZam";
            var wynik = new List<DateTime>();
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@KlientId", klientId);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                    wynik.Add(rd.GetDateTime(0));
            }
            catch { }
            return wynik;
        }

        private async Task<(decimal biezace, decimal poprzednie)> PobierzObrotyAsync(int klientId)
        {
            const string sql = @"
                SELECT
                    ISNULL(SUM(CASE WHEN DK.data >= DATEADD(MONTH, -6, GETDATE()) THEN DK.walbrutto ELSE 0 END), 0) AS Biezace,
                    ISNULL(SUM(CASE WHEN DK.data >= DATEADD(MONTH, -12, GETDATE()) AND DK.data < DATEADD(MONTH, -6, GETDATE()) THEN DK.walbrutto ELSE 0 END), 0) AS Poprzednie
                FROM [HM].[DK] DK
                WHERE DK.khid = @KlientId
                  AND DK.typ_dk IN ('FVS', 'FVR', 'FVZ')
                  AND DK.aktywny = 1
                  AND DK.anulowany = 0
                  AND DK.data >= DATEADD(YEAR, -1, GETDATE())";
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@KlientId", klientId);
                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                    return (rd.GetDecimal(0), rd.GetDecimal(1));
            }
            catch { }
            return (0, 0);
        }

        private async Task<(decimal limit, decimal wykorzystano)> PobierzLimitAsync(int klientId)
        {
            // LimitAmount = limit kupiecki, naleznosci = suma otwartych faktur
            const string sql = @"
                SELECT
                    ISNULL(C.LimitAmount, 0) AS LimitKupiecki,
                    ISNULL((
                        SELECT SUM(DK.walbrutto - ISNULL(PN2.KwotaRozliczona, 0))
                        FROM [HM].[DK] DK
                        LEFT JOIN (
                            SELECT dkid, SUM(kwotarozl) AS KwotaRozliczona
                            FROM [HM].[PN]
                            GROUP BY dkid
                        ) PN2 ON PN2.dkid = DK.id
                        WHERE DK.khid = C.Id
                          AND DK.typ_dk IN ('FVS', 'FVR', 'FVZ')
                          AND DK.aktywny = 1
                          AND DK.anulowany = 0
                          AND DK.ok = 0
                          AND (DK.walbrutto - ISNULL(PN2.KwotaRozliczona, 0)) > 0.01
                    ), 0) AS Naleznosci
                FROM [SSCommon].[STContractors] C
                WHERE C.Id = @KlientId";
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@KlientId", klientId);
                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                    return (rd.GetDecimal(0), rd.GetDecimal(1));
            }
            catch { }
            return (0, 0);
        }

        private async Task ZapiszScoringAsync(ScoringResult result)
        {
            const string sql = @"
                INSERT INTO dbo.KartotekaScoring
                    (KlientId, TerminowoscPkt, HistoriaPkt, RegularnoscPkt, TrendPkt, LimitPkt,
                     ScoreTotal, Kategoria, RekomendacjaLimitu, RekomendacjaOpis, DataObliczenia)
                VALUES
                    (@KlientId, @TerminowoscPkt, @HistoriaPkt, @RegularnoscPkt, @TrendPkt, @LimitPkt,
                     @ScoreTotal, @Kategoria, @RekomendacjaLimitu, @RekomendacjaOpis, GETDATE())";

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@KlientId", result.KlientId);
                cmd.Parameters.AddWithValue("@TerminowoscPkt", result.TerminowoscPkt);
                cmd.Parameters.AddWithValue("@HistoriaPkt", result.HistoriaPkt);
                cmd.Parameters.AddWithValue("@RegularnoscPkt", result.RegularnoscPkt);
                cmd.Parameters.AddWithValue("@TrendPkt", result.TrendPkt);
                cmd.Parameters.AddWithValue("@LimitPkt", result.LimitPkt);
                cmd.Parameters.AddWithValue("@ScoreTotal", result.ScoreTotal);
                cmd.Parameters.AddWithValue("@Kategoria", result.Kategoria ?? "");
                cmd.Parameters.AddWithValue("@RekomendacjaLimitu", result.RekomendacjaLimitu);
                cmd.Parameters.AddWithValue("@RekomendacjaOpis", (object)result.RekomendacjaOpis ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            catch { }
        }

        public async Task<ScoringResult> PobierzOstatniScoringAsync(int klientId)
        {
            const string sql = @"SELECT TOP 1 Id, KlientId, TerminowoscPkt, HistoriaPkt, RegularnoscPkt,
                                        TrendPkt, LimitPkt, ScoreTotal, Kategoria,
                                        RekomendacjaLimitu, RekomendacjaOpis, DataObliczenia
                                 FROM dbo.KartotekaScoring WHERE KlientId = @KlientId
                                 ORDER BY DataObliczenia DESC";
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@KlientId", klientId);
                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    return new ScoringResult
                    {
                        Id = rd.GetInt32(0),
                        KlientId = rd.GetInt32(1),
                        TerminowoscPkt = rd.IsDBNull(2) ? 0 : rd.GetInt32(2),
                        HistoriaPkt = rd.IsDBNull(3) ? 0 : rd.GetInt32(3),
                        RegularnoscPkt = rd.IsDBNull(4) ? 0 : rd.GetInt32(4),
                        TrendPkt = rd.IsDBNull(5) ? 0 : rd.GetInt32(5),
                        LimitPkt = rd.IsDBNull(6) ? 0 : rd.GetInt32(6),
                        ScoreTotal = rd.IsDBNull(7) ? 0 : rd.GetInt32(7),
                        Kategoria = rd.IsDBNull(8) ? null : rd.GetString(8),
                        RekomendacjaLimitu = rd.IsDBNull(9) ? 0 : rd.GetDecimal(9),
                        RekomendacjaOpis = rd.IsDBNull(10) ? null : rd.GetString(10),
                        DataObliczenia = rd.IsDBNull(11) ? DateTime.Now : rd.GetDateTime(11)
                    };
                }
            }
            catch { }
            return null;
        }

        public async Task<List<ScoringResult>> PobierzHistorieScoringAsync(int klientId, int limit = 12)
        {
            const string sql = @"SELECT TOP (@Limit) Id, KlientId, TerminowoscPkt, HistoriaPkt, RegularnoscPkt,
                                        TrendPkt, LimitPkt, ScoreTotal, Kategoria,
                                        RekomendacjaLimitu, RekomendacjaOpis, DataObliczenia
                                 FROM dbo.KartotekaScoring WHERE KlientId = @KlientId
                                 ORDER BY DataObliczenia DESC";
            var wynik = new List<ScoringResult>();
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@KlientId", klientId);
                cmd.Parameters.AddWithValue("@Limit", limit);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    wynik.Add(new ScoringResult
                    {
                        Id = rd.GetInt32(0),
                        KlientId = rd.GetInt32(1),
                        TerminowoscPkt = rd.IsDBNull(2) ? 0 : rd.GetInt32(2),
                        HistoriaPkt = rd.IsDBNull(3) ? 0 : rd.GetInt32(3),
                        RegularnoscPkt = rd.IsDBNull(4) ? 0 : rd.GetInt32(4),
                        TrendPkt = rd.IsDBNull(5) ? 0 : rd.GetInt32(5),
                        LimitPkt = rd.IsDBNull(6) ? 0 : rd.GetInt32(6),
                        ScoreTotal = rd.IsDBNull(7) ? 0 : rd.GetInt32(7),
                        Kategoria = rd.IsDBNull(8) ? null : rd.GetString(8),
                        RekomendacjaLimitu = rd.IsDBNull(9) ? 0 : rd.GetDecimal(9),
                        RekomendacjaOpis = rd.IsDBNull(10) ? null : rd.GetString(10),
                        DataObliczenia = rd.IsDBNull(11) ? DateTime.Now : rd.GetDateTime(11)
                    });
                }
            }
            catch { }
            return wynik;
        }
    }
}
