using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kalendarz1.SkrzynkaZakupu.Models;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.SkrzynkaZakupu.Services
{
    /// <summary>
    /// Wspólna książka adresowa działu zakupu (LibraNet.Mail_Contacts).
    /// Zasilana ze skrzynki IMAP, importu Thunderbirda i przy wysyłce.
    /// </summary>
    public class MailContactsService
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public async Task<List<MailContact>> GetAllAsync()
        {
            var list = new List<MailContact>();
            try
            {
                using var conn = new SqlConnection(ConnLibra);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(
                    "SELECT Email, DisplayName, UseCount FROM dbo.Mail_Contacts ORDER BY UseCount DESC, DisplayName", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new MailContact
                    {
                        Email = r["Email"]?.ToString() ?? "",
                        DisplayName = r["DisplayName"] as string ?? "",
                        UseCount = r["UseCount"] is int i ? i : 0
                    });
                }
            }
            catch { /* brak tabeli/sieci -> pusta lista */ }
            return list;
        }

        public async Task<int> GetCountAsync()
        {
            try
            {
                using var conn = new SqlConnection(ConnLibra);
                await conn.OpenAsync();
                using var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Mail_Contacts", conn);
                return (int)(await cmd.ExecuteScalarAsync() ?? 0);
            }
            catch { return 0; }
        }

        public Task UpsertAsync(string email, string name, string source = "send")
            => UpsertManyAsync(new[] { new MailContact { Email = email, DisplayName = name ?? "" } }, source);

        /// <summary>
        /// Wstawia/aktualizuje wiele adresów (deduplikacja po znormalizowanym mailu).
        /// Zwraca liczbę unikalnych adresów zapisanych.
        /// </summary>
        public async Task<int> UpsertManyAsync(IEnumerable<MailContact> kontakty, string source = "imap")
        {
            // dedup + normalizacja w pamięci
            var mapa = new Dictionary<string, MailContact>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in kontakty)
            {
                var email = (k.Email ?? "").Trim().ToLowerInvariant();
                if (!CzyPoprawnyEmail(email)) continue;
                if (mapa.TryGetValue(email, out var ex))
                {
                    if (string.IsNullOrWhiteSpace(ex.DisplayName) && !string.IsNullOrWhiteSpace(k.DisplayName))
                        ex.DisplayName = k.DisplayName.Trim();
                }
                else
                {
                    mapa[email] = new MailContact { Email = email, DisplayName = (k.DisplayName ?? "").Trim() };
                }
            }
            if (mapa.Count == 0) return 0;

            int zapisane = 0;
            try
            {
                using var conn = new SqlConnection(ConnLibra);
                await conn.OpenAsync();
                using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
                using var cmd = new SqlCommand(@"
MERGE dbo.Mail_Contacts AS t
USING (SELECT @e AS Email) AS s ON t.Email = s.Email
WHEN MATCHED THEN UPDATE SET
    UseCount = t.UseCount + 1,
    LastSeen = GETDATE(),
    DisplayName = COALESCE(NULLIF(t.DisplayName,''), NULLIF(@n,''), t.DisplayName)
WHEN NOT MATCHED THEN INSERT (Email, DisplayName, UseCount, LastSeen, Source)
     VALUES (@e, @n, 1, GETDATE(), @src);", conn, tx);
                var pe = cmd.Parameters.Add("@e", System.Data.SqlDbType.NVarChar, 320);
                var pn = cmd.Parameters.Add("@n", System.Data.SqlDbType.NVarChar, 255);
                var ps = cmd.Parameters.Add("@src", System.Data.SqlDbType.NVarChar, 30);
                ps.Value = source;

                foreach (var k in mapa.Values)
                {
                    pe.Value = k.Email;
                    pn.Value = k.DisplayName ?? "";
                    await cmd.ExecuteNonQueryAsync();
                    zapisane++;
                }
                await tx.CommitAsync();
            }
            catch { /* best-effort */ }
            return zapisane;
        }

        public static bool CzyPoprawnyEmail(string? e)
        {
            if (string.IsNullOrWhiteSpace(e)) return false;
            int at = e.IndexOf('@');
            return at > 0 && at < e.Length - 3 && e.IndexOf('.', at) > at && !e.Contains(' ');
        }
    }
}
