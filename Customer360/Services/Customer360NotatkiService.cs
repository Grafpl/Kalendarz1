using Kalendarz1.Customer360.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kalendarz1.Customer360.Services
{
    /// <summary>
    /// Notatki handlowca per klient — "dziennik klienta" w karcie C360.
    /// Tabela LibraNet.Customer360_Notatki tworzona auto przy pierwszym wywolaniu (konwencja modulu C360).
    /// </summary>
    public static class Customer360NotatkiService
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private const string SqlEnsure = @"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Customer360_Notatki')
BEGIN
    CREATE TABLE dbo.Customer360_Notatki (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        KlientId INT NOT NULL,
        Tresc NVARCHAR(MAX) NOT NULL,
        AutorId NVARCHAR(50) NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_C360Notatki_Klient ON dbo.Customer360_Notatki(KlientId, CreatedAt DESC);
END";

        public static async Task<List<NotatkaC360>> GetNotatkiAsync(int klientId)
        {
            var lista = new List<NotatkaC360>();
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                await using (var ens = new SqlCommand(SqlEnsure, cn) { CommandTimeout = 5 })
                    await ens.ExecuteNonQueryAsync();

                await using var cmd = new SqlCommand(
                    @"SELECT Id, Tresc, ISNULL(AutorId,''), CreatedAt
                      FROM dbo.Customer360_Notatki
                      WHERE KlientId=@kid
                      ORDER BY CreatedAt DESC", cn) { CommandTimeout = 8 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    lista.Add(new NotatkaC360
                    {
                        Id = rd.GetInt32(0),
                        Tresc = rd.IsDBNull(1) ? "" : rd.GetString(1),
                        AutorId = rd.IsDBNull(2) ? "" : rd.GetString(2),
                        CreatedAt = rd.GetDateTime(3)
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 notatki get] " + ex.Message); }
            return lista;
        }

        public static async Task<bool> DodajAsync(int klientId, string tresc, string autorId)
        {
            if (string.IsNullOrWhiteSpace(tresc)) return false;
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                await using (var ens = new SqlCommand(SqlEnsure, cn) { CommandTimeout = 5 })
                    await ens.ExecuteNonQueryAsync();

                await using var cmd = new SqlCommand(
                    @"INSERT INTO dbo.Customer360_Notatki (KlientId, Tresc, AutorId, CreatedAt)
                      VALUES (@kid, @t, @u, GETDATE())", cn) { CommandTimeout = 5 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                cmd.Parameters.AddWithValue("@t", tresc.Trim());
                cmd.Parameters.AddWithValue("@u", (object?)autorId ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
                return true;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 notatki dodaj] " + ex.Message); return false; }
        }

        public static async Task<bool> UsunAsync(int notatkaId)
        {
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(
                    "DELETE FROM dbo.Customer360_Notatki WHERE Id=@id", cn) { CommandTimeout = 5 };
                cmd.Parameters.AddWithValue("@id", notatkaId);
                int n = await cmd.ExecuteNonQueryAsync();
                return n > 0;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 notatki usun] " + ex.Message); return false; }
        }
    }
}
