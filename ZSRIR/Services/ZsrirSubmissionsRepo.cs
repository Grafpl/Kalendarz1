using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kalendarz1.ZSRIR.Models;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.ZSRIR.Services
{
    // CRUD historii wysyłek ZSRIR (tabela LibraNet.dbo.ZsrirSubmissions).
    public class ZsrirSubmissionsRepo
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // Cache: tabela jest tworzona raz per proces (idempotentnie).
        private static bool _tableEnsured;
        private static readonly System.Threading.SemaphoreSlim _ensureLock = new(1, 1);

        private static async Task EnsureTableExistsAsync()
        {
            if (_tableEnsured) return;
            await _ensureLock.WaitAsync();
            try
            {
                if (_tableEnsured) return;
                const string ddl = @"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ZsrirSubmissions' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.ZsrirSubmissions (
        Id                      INT IDENTITY(1,1) PRIMARY KEY,
        OkresOd                 DATE NOT NULL,
        OkresDo                 DATE NOT NULL,
        KategoriaTowaru         NVARCHAR(80) NOT NULL,
        CommodityGroupId        INT NULL,
        KgRazem                 DECIMAL(18,2) NOT NULL DEFAULT 0,
        TonyRazem               DECIMAL(18,3) NOT NULL DEFAULT 0,
        WartoscNetto            DECIMAL(18,2) NOT NULL DEFAULT 0,
        CenaZlTona              DECIMAL(18,2) NOT NULL DEFAULT 0,
        FormReportingPeriodId   INT NULL,
        DataSupplierId          INT NULL,
        Status                  VARCHAR(20) NOT NULL DEFAULT 'Pending',
        ApiResponse             NVARCHAR(MAX) NULL,
        ErrorMessage            NVARCHAR(1000) NULL,
        WyslanyPrzez            INT NULL,
        WyslanyDataCzas         DATETIME NULL,
        CreatedAt               DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT UQ_ZsrirSubmissions_Okres_Kategoria UNIQUE (OkresOd, OkresDo, KategoriaTowaru)
    );
    CREATE INDEX IX_ZsrirSubmissions_OkresOd ON dbo.ZsrirSubmissions(OkresOd DESC);
    CREATE INDEX IX_ZsrirSubmissions_Status ON dbo.ZsrirSubmissions(Status);
END";
                using var conn = new SqlConnection(ConnLibra);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(ddl, conn) { CommandTimeout = 30 };
                await cmd.ExecuteNonQueryAsync();
                _tableEnsured = true;
            }
            finally { _ensureLock.Release(); }
        }

        public async Task<int> InsertAsync(SubmissionRow r)
        {
            await EnsureTableExistsAsync();
            const string sql = @"
INSERT INTO dbo.ZsrirSubmissions
    (OkresOd, OkresDo, KategoriaTowaru, CommodityGroupId,
     KgRazem, TonyRazem, WartoscNetto, CenaZlTona,
     FormReportingPeriodId, DataSupplierId,
     Status, ApiResponse, ErrorMessage, WyslanyPrzez, WyslanyDataCzas)
VALUES
    (@OkresOd, @OkresDo, @KategoriaTowaru, @CommodityGroupId,
     @KgRazem, @TonyRazem, @WartoscNetto, @CenaZlTona,
     @FormReportingPeriodId, @DataSupplierId,
     @Status, @ApiResponse, @ErrorMessage, @WyslanyPrzez, @WyslanyDataCzas);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@OkresOd", r.OkresOd);
            cmd.Parameters.AddWithValue("@OkresDo", r.OkresDo);
            cmd.Parameters.AddWithValue("@KategoriaTowaru", r.KategoriaTowaru);
            cmd.Parameters.AddWithValue("@CommodityGroupId", (object?)r.CommodityGroupId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@KgRazem", r.KgRazem);
            cmd.Parameters.AddWithValue("@TonyRazem", r.TonyRazem);
            cmd.Parameters.AddWithValue("@WartoscNetto", r.WartoscNetto);
            cmd.Parameters.AddWithValue("@CenaZlTona", r.CenaZlTona);
            cmd.Parameters.AddWithValue("@FormReportingPeriodId", (object?)r.FormReportingPeriodId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DataSupplierId", (object?)r.DataSupplierId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", r.Status);
            cmd.Parameters.AddWithValue("@ApiResponse", (object?)r.ApiResponse ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ErrorMessage", (object?)r.ErrorMessage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@WyslanyPrzez", (object?)r.WyslanyPrzez ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@WyslanyDataCzas", (object?)r.WyslanyDataCzas ?? DBNull.Value);
            return (int)(await cmd.ExecuteScalarAsync() ?? 0);
        }

        public async Task<List<SubmissionRow>> GetRecentAsync(int top = 100)
        {
            await EnsureTableExistsAsync();
            const string sql = @"
SELECT TOP (@Top) s.Id, s.OkresOd, s.OkresDo, s.KategoriaTowaru, s.CommodityGroupId,
       s.KgRazem, s.TonyRazem, s.WartoscNetto, s.CenaZlTona,
       s.FormReportingPeriodId, s.DataSupplierId,
       s.Status, s.ApiResponse, s.ErrorMessage,
       s.WyslanyPrzez, o.Name AS WyslanyPrzezImie, s.WyslanyDataCzas, s.CreatedAt
FROM dbo.ZsrirSubmissions s
LEFT JOIN dbo.operators o ON o.ID = s.WyslanyPrzez
ORDER BY s.OkresOd DESC, s.Id DESC;";

            var lista = new List<SubmissionRow>();
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Top", top);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                lista.Add(new SubmissionRow
                {
                    Id = rdr.GetInt32(rdr.GetOrdinal("Id")),
                    OkresOd = rdr.GetDateTime(rdr.GetOrdinal("OkresOd")),
                    OkresDo = rdr.GetDateTime(rdr.GetOrdinal("OkresDo")),
                    KategoriaTowaru = rdr["KategoriaTowaru"]?.ToString() ?? "",
                    CommodityGroupId = rdr.IsDBNull(rdr.GetOrdinal("CommodityGroupId")) ? null : rdr.GetInt32(rdr.GetOrdinal("CommodityGroupId")),
                    KgRazem = rdr.GetDecimal(rdr.GetOrdinal("KgRazem")),
                    TonyRazem = rdr.GetDecimal(rdr.GetOrdinal("TonyRazem")),
                    WartoscNetto = rdr.GetDecimal(rdr.GetOrdinal("WartoscNetto")),
                    CenaZlTona = rdr.GetDecimal(rdr.GetOrdinal("CenaZlTona")),
                    FormReportingPeriodId = rdr.IsDBNull(rdr.GetOrdinal("FormReportingPeriodId")) ? null : rdr.GetInt32(rdr.GetOrdinal("FormReportingPeriodId")),
                    DataSupplierId = rdr.IsDBNull(rdr.GetOrdinal("DataSupplierId")) ? null : rdr.GetInt32(rdr.GetOrdinal("DataSupplierId")),
                    Status = rdr["Status"]?.ToString() ?? "",
                    ApiResponse = rdr["ApiResponse"]?.ToString(),
                    ErrorMessage = rdr["ErrorMessage"]?.ToString(),
                    WyslanyPrzez = rdr.IsDBNull(rdr.GetOrdinal("WyslanyPrzez")) ? null : rdr.GetInt32(rdr.GetOrdinal("WyslanyPrzez")),
                    WyslanyPrzezImie = rdr["WyslanyPrzezImie"]?.ToString(),
                    WyslanyDataCzas = rdr.IsDBNull(rdr.GetOrdinal("WyslanyDataCzas")) ? null : rdr.GetDateTime(rdr.GetOrdinal("WyslanyDataCzas")),
                    CreatedAt = rdr.GetDateTime(rdr.GetOrdinal("CreatedAt"))
                });
            }
            return lista;
        }

        // Sprawdza czy istnieje wysłany formularz dla okresu — żeby uniknąć duplikacji
        public async Task<bool> ExistsForPeriodAsync(DateTime od, DateTime doD, string kategoria, string sentStatus = "Sent")
        {
            await EnsureTableExistsAsync();
            const string sql = @"
SELECT COUNT(*) FROM dbo.ZsrirSubmissions
WHERE OkresOd = @Od AND OkresDo = @Do AND KategoriaTowaru = @Kat AND Status = @Status;";
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Od", od);
            cmd.Parameters.AddWithValue("@Do", doD);
            cmd.Parameters.AddWithValue("@Kat", kategoria);
            cmd.Parameters.AddWithValue("@Status", sentStatus);
            int n = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            return n > 0;
        }
    }
}
