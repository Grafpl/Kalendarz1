using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.SkrzynkaZakupu.Services
{
    /// <summary>
    /// Stan "przeczytane/nieprzeczytane" PER UŻYTKOWNIK ZPSP (lokalnie w LibraNet).
    /// Foldery serwerowe są wspólne, ale każdy widzi własne nieprzeczytane.
    /// </summary>
    public class MailReadStateService
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private readonly string _userId;

        public MailReadStateService(string userId)
        {
            _userId = string.IsNullOrWhiteSpace(userId) ? "?" : userId;
        }

        /// <summary>
        /// Zwraca zbiór UID-ów oznaczonych jako PRZECZYTANE przez tego użytkownika w danym folderze.
        /// </summary>
        public async Task<HashSet<uint>> GetReadUidsAsync(string folderName)
        {
            var set = new HashSet<uint>();
            try
            {
                using var conn = new SqlConnection(ConnLibra);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(
                    "SELECT Uid FROM dbo.Mail_ReadState WHERE UserID=@u AND FolderName=@f AND IsRead=1", conn);
                cmd.Parameters.AddWithValue("@u", _userId);
                cmd.Parameters.AddWithValue("@f", folderName);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    set.Add((uint)Convert.ToInt64(r["Uid"]));
            }
            catch { /* brak tabeli / sieci — traktujemy wszystko jako nieprzeczytane */ }
            return set;
        }

        /// <summary>Oznacza wiele wiadomości naraz (np. „oznacz wszystkie jako przeczytane").</summary>
        public async Task SetManyReadAsync(string folderName, IEnumerable<uint> uids, bool isRead)
        {
            var lista = uids.Distinct().ToList();
            if (lista.Count == 0) return;
            try
            {
                using var conn = new SqlConnection(ConnLibra);
                await conn.OpenAsync();
                using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
                using var cmd = new SqlCommand(@"
MERGE dbo.Mail_ReadState AS t
USING (SELECT @u AS UserID, @f AS FolderName, @uid AS Uid) AS s
   ON t.UserID=s.UserID AND t.FolderName=s.FolderName AND t.Uid=s.Uid
WHEN MATCHED THEN UPDATE SET IsRead=@r, ReadAt=GETDATE()
WHEN NOT MATCHED THEN INSERT (UserID, FolderName, Uid, IsRead, ReadAt)
                     VALUES (@u, @f, @uid, @r, GETDATE());", conn, tx);
                cmd.Parameters.AddWithValue("@u", _userId);
                cmd.Parameters.AddWithValue("@f", folderName);
                var pUid = cmd.Parameters.Add("@uid", System.Data.SqlDbType.BigInt);
                cmd.Parameters.AddWithValue("@r", isRead);
                foreach (var uid in lista) { pUid.Value = (long)uid; await cmd.ExecuteNonQueryAsync(); }
                await tx.CommitAsync();
            }
            catch { /* best-effort */ }
        }

        public async Task SetReadAsync(string folderName, uint uid, bool isRead)
        {
            try
            {
                using var conn = new SqlConnection(ConnLibra);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
MERGE dbo.Mail_ReadState AS t
USING (SELECT @u AS UserID, @f AS FolderName, @uid AS Uid) AS s
   ON t.UserID=s.UserID AND t.FolderName=s.FolderName AND t.Uid=s.Uid
WHEN MATCHED THEN UPDATE SET IsRead=@r, ReadAt=GETDATE()
WHEN NOT MATCHED THEN INSERT (UserID, FolderName, Uid, IsRead, ReadAt)
                     VALUES (@u, @f, @uid, @r, GETDATE());", conn);
                cmd.Parameters.AddWithValue("@u", _userId);
                cmd.Parameters.AddWithValue("@f", folderName);
                cmd.Parameters.AddWithValue("@uid", (long)uid);
                cmd.Parameters.AddWithValue("@r", isRead);
                await cmd.ExecuteNonQueryAsync();
            }
            catch { /* best-effort */ }
        }
    }
}
