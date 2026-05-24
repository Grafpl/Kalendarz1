using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kalendarz1.Sprawozdania.Models;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Sprawozdania.Services
{
    // CRUD historii sprawozdań GUS (tabela LibraNet.dbo.GusSubmissions).
    // Wzorzec ZsrirSubmissionsRepo (per CLAUDE.md sec.4: conn string hardcoded w klasie).
    public class GusSubmissionsRepo
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public async Task<int> InsertAsync(GusSubmissionRow r)
        {
            const string sql = @"
INSERT INTO dbo.GusSubmissions
    (Formularz, FormularzWersja, OkresOd, OkresDo, Rok, Miesiac, Regon,
     GeneratedXml, PlikXml, Status, ValidationLog, ErrorMessage,
     NumerWPortalu, IloscPozycji, SumaWartosc,
     GeneratedBy, GeneratedAt, ExportedAt, SentAt, Notatki)
VALUES
    (@Formularz, @FormularzWersja, @OkresOd, @OkresDo, @Rok, @Miesiac, @Regon,
     @GeneratedXml, @PlikXml, @Status, @ValidationLog, @ErrorMessage,
     @NumerWPortalu, @IloscPozycji, @SumaWartosc,
     @GeneratedBy, @GeneratedAt, @ExportedAt, @SentAt, @Notatki);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Formularz", r.Formularz);
            cmd.Parameters.AddWithValue("@FormularzWersja", r.FormularzWersja ?? "");
            cmd.Parameters.AddWithValue("@OkresOd", r.OkresOd);
            cmd.Parameters.AddWithValue("@OkresDo", r.OkresDo);
            cmd.Parameters.AddWithValue("@Rok", r.Rok);
            cmd.Parameters.AddWithValue("@Miesiac", (object?)r.Miesiac ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Regon", r.Regon);
            cmd.Parameters.AddWithValue("@GeneratedXml", (object?)r.GeneratedXml ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PlikXml", (object?)r.PlikXml ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", r.Status);
            cmd.Parameters.AddWithValue("@ValidationLog", (object?)r.ValidationLog ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ErrorMessage", (object?)r.ErrorMessage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@NumerWPortalu", (object?)r.NumerWPortalu ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IloscPozycji", r.IloscPozycji);
            cmd.Parameters.AddWithValue("@SumaWartosc", r.SumaWartosc);
            cmd.Parameters.AddWithValue("@GeneratedBy", (object?)r.GeneratedBy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@GeneratedAt", r.GeneratedAt == default ? DateTime.Now : r.GeneratedAt);
            cmd.Parameters.AddWithValue("@ExportedAt", (object?)r.ExportedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SentAt", (object?)r.SentAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Notatki", (object?)r.Notatki ?? DBNull.Value);
            return (int)(await cmd.ExecuteScalarAsync() ?? 0);
        }

        public async Task<List<GusSubmissionRow>> GetRecentAsync(string? formularz = null, int top = 100)
        {
            string sql = @"
SELECT TOP (@Top) s.Id, s.Formularz, s.FormularzWersja, s.OkresOd, s.OkresDo, s.Rok, s.Miesiac, s.Regon,
       s.GeneratedXml, s.PlikXml, s.Status, s.ValidationLog, s.ErrorMessage,
       s.NumerWPortalu, s.IloscPozycji, s.SumaWartosc,
       s.GeneratedBy, o.Name AS GeneratedByImie, s.GeneratedAt, s.ExportedAt, s.SentAt, s.Notatki
FROM dbo.GusSubmissions s
LEFT JOIN dbo.operators o ON o.ID = s.GeneratedBy
" + (formularz != null ? "WHERE s.Formularz = @Formularz\n" : "") + @"
ORDER BY s.GeneratedAt DESC, s.Id DESC;";

            var lista = new List<GusSubmissionRow>();
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Top", top);
            if (formularz != null) cmd.Parameters.AddWithValue("@Formularz", formularz);

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                lista.Add(MapRow(rdr));
            }
            return lista;
        }

        public async Task<GusSubmissionRow?> GetForPeriodAsync(string formularz, int rok, int miesiac)
        {
            const string sql = @"
SELECT TOP 1 s.Id, s.Formularz, s.FormularzWersja, s.OkresOd, s.OkresDo, s.Rok, s.Miesiac, s.Regon,
       s.GeneratedXml, s.PlikXml, s.Status, s.ValidationLog, s.ErrorMessage,
       s.NumerWPortalu, s.IloscPozycji, s.SumaWartosc,
       s.GeneratedBy, o.Name AS GeneratedByImie, s.GeneratedAt, s.ExportedAt, s.SentAt, s.Notatki
FROM dbo.GusSubmissions s
LEFT JOIN dbo.operators o ON o.ID = s.GeneratedBy
WHERE s.Formularz = @Formularz AND s.Rok = @Rok AND s.Miesiac = @Miesiac
ORDER BY s.GeneratedAt DESC;";
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Formularz", formularz);
            cmd.Parameters.AddWithValue("@Rok", rok);
            cmd.Parameters.AddWithValue("@Miesiac", miesiac);
            using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync()) return MapRow(rdr);
            return null;
        }

        public async Task UpdateStatusAsync(int id, string status, string? errorMessage = null,
            string? plikXml = null, DateTime? exportedAt = null, DateTime? sentAt = null,
            string? numerWPortalu = null)
        {
            const string sql = @"
UPDATE dbo.GusSubmissions
SET Status = @Status,
    ErrorMessage = COALESCE(@ErrorMessage, ErrorMessage),
    PlikXml = COALESCE(@PlikXml, PlikXml),
    ExportedAt = COALESCE(@ExportedAt, ExportedAt),
    SentAt = COALESCE(@SentAt, SentAt),
    NumerWPortalu = COALESCE(@NumerWPortalu, NumerWPortalu)
WHERE Id = @Id;";
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.Parameters.AddWithValue("@ErrorMessage", (object?)errorMessage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PlikXml", (object?)plikXml ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ExportedAt", (object?)exportedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SentAt", (object?)sentAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@NumerWPortalu", (object?)numerWPortalu ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> ExistsSentForPeriodAsync(string formularz, int rok, int miesiac)
        {
            const string sql = @"
SELECT COUNT(*) FROM dbo.GusSubmissions
WHERE Formularz = @Formularz AND Rok = @Rok AND Miesiac = @Miesiac AND Status = 'Sent';";
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Formularz", formularz);
            cmd.Parameters.AddWithValue("@Rok", rok);
            cmd.Parameters.AddWithValue("@Miesiac", miesiac);
            int n = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            return n > 0;
        }

        private static GusSubmissionRow MapRow(SqlDataReader rdr)
        {
            return new GusSubmissionRow
            {
                Id = rdr.GetInt32(rdr.GetOrdinal("Id")),
                Formularz = rdr["Formularz"]?.ToString() ?? "",
                FormularzWersja = rdr["FormularzWersja"]?.ToString() ?? "",
                OkresOd = rdr.GetDateTime(rdr.GetOrdinal("OkresOd")),
                OkresDo = rdr.GetDateTime(rdr.GetOrdinal("OkresDo")),
                Rok = rdr.GetInt32(rdr.GetOrdinal("Rok")),
                Miesiac = rdr.IsDBNull(rdr.GetOrdinal("Miesiac")) ? null : rdr.GetInt32(rdr.GetOrdinal("Miesiac")),
                Regon = rdr["Regon"]?.ToString() ?? "",
                GeneratedXml = rdr["GeneratedXml"]?.ToString(),
                PlikXml = rdr["PlikXml"]?.ToString(),
                Status = rdr["Status"]?.ToString() ?? "Draft",
                ValidationLog = rdr["ValidationLog"]?.ToString(),
                ErrorMessage = rdr["ErrorMessage"]?.ToString(),
                NumerWPortalu = rdr["NumerWPortalu"]?.ToString(),
                IloscPozycji = rdr.GetInt32(rdr.GetOrdinal("IloscPozycji")),
                SumaWartosc = rdr.GetDecimal(rdr.GetOrdinal("SumaWartosc")),
                GeneratedBy = rdr.IsDBNull(rdr.GetOrdinal("GeneratedBy")) ? null : rdr.GetInt32(rdr.GetOrdinal("GeneratedBy")),
                GeneratedByImie = rdr["GeneratedByImie"]?.ToString(),
                GeneratedAt = rdr.GetDateTime(rdr.GetOrdinal("GeneratedAt")),
                ExportedAt = rdr.IsDBNull(rdr.GetOrdinal("ExportedAt")) ? null : rdr.GetDateTime(rdr.GetOrdinal("ExportedAt")),
                SentAt = rdr.IsDBNull(rdr.GetOrdinal("SentAt")) ? null : rdr.GetDateTime(rdr.GetOrdinal("SentAt")),
                Notatki = rdr["Notatki"]?.ToString()
            };
        }
    }
}
