using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Kalendarz1.MagazynLiczenie.Modele;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.MagazynLiczenie.Repozytorium
{
    public class LiczenieRepozytorium
    {
        private readonly string _connLibra;
        private readonly string _connHandel;

        public LiczenieRepozytorium(string connLibra, string connHandel)
        {
            _connLibra = connLibra;
            _connHandel = connHandel;
        }

        public async Task<List<ProduktLiczenie>> PobierzProduktyDoLiczeniaAsync()
        {
            var produkty = new List<ProduktLiczenie>();

            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                // ✅ TYLKO KATALOG 67095 (produkty świeże), BEZ 67153 (mrożonki)
                const string sql = @"
            SELECT ID, kod 
            FROM [HANDEL].[HM].[TW] 
            WHERE katalog = 67095
            AND aktywny = 1
            ORDER BY kod";

                await using var cmd = new SqlCommand(sql, cn);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var produkt = new ProduktLiczenie
                    {
                        ProduktId = reader.GetInt32(0),
                        KodProduktu = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        NazwaProduktu = reader.IsDBNull(1) ? "Nieznany" : reader.GetString(1),
                        StanMagazynowy = 0m,
                        JestZmodyfikowany = false
                    };

                    produkty.Add(produkt);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd podczas pobierania produktów: {ex.Message}", ex);
            }

            return produkty;
        }
        public async Task<Dictionary<int, decimal>> PobierzAktualneStalyAsync(DateTime data)
        {
            var stany = new Dictionary<int, decimal>();

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                const string sql = @"
                    SELECT ProduktId, Stan 
                    FROM dbo.StanyMagazynowe 
                    WHERE Data = @Data";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Data", data.Date);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    int produktId = reader.GetInt32(0);
                    decimal stan = reader.GetDecimal(1);
                    stany[produktId] = stan;
                }
            }
            catch
            {
                // Jeśli tabela nie istnieje, zwróć pusty słownik
            }

            return stany;
        }

        public async Task ZapiszStanyAsync(List<ProduktLiczenie> produkty, DateTime data, string uzytkownik)
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var transaction = cn.BeginTransaction();

            try
            {
                // Upewnij się, że tabela istnieje
                await UtworzTabeleLiczenia(cn, transaction);

                // Usuń stare wpisy dla tej daty
                const string deleteSql = "DELETE FROM dbo.StanyMagazynowe WHERE Data = @Data";
                await using (var deleteCmd = new SqlCommand(deleteSql, cn, transaction))
                {
                    deleteCmd.Parameters.AddWithValue("@Data", data.Date);
                    await deleteCmd.ExecuteNonQueryAsync();
                }

                // Wstaw nowe wpisy
                const string insertSql = @"
                    INSERT INTO dbo.StanyMagazynowe (Data, ProduktId, Stan, Uzytkownik, DataZapisu)
                    VALUES (@Data, @ProduktId, @Stan, @Uzytkownik, GETDATE())";

                foreach (var produkt in produkty.Where(p => p.JestZmodyfikowany))
                {
                    await using var insertCmd = new SqlCommand(insertSql, cn, transaction);
                    insertCmd.Parameters.AddWithValue("@Data", data.Date);
                    insertCmd.Parameters.AddWithValue("@ProduktId", produkt.ProduktId);
                    insertCmd.Parameters.AddWithValue("@Stan", produkt.StanMagazynowy);
                    insertCmd.Parameters.AddWithValue("@Uzytkownik", uzytkownik);
                    await insertCmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<Dictionary<int, BitmapImage>> PobierzZdjeciaProduktowAsync()
        {
            var zdjecia = new Dictionary<int, BitmapImage>();
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                var cmdCheck = new SqlCommand("SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TowarZdjecia') THEN 1 ELSE 0 END", cn);
                if ((int)(await cmdCheck.ExecuteScalarAsync())! == 0) return zdjecia;

                var cmd = new SqlCommand("SELECT TowarId, Zdjecie FROM dbo.TowarZdjecia WHERE Aktywne = 1", cn);
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    int towarId = rdr.GetInt32(0);
                    if (!rdr.IsDBNull(1))
                    {
                        byte[] data = (byte[])rdr[1];
                        try
                        {
                            var ms = new MemoryStream(data);
                            var bi = new BitmapImage();
                            bi.BeginInit();
                            bi.StreamSource = ms;
                            bi.DecodePixelWidth = 140;
                            bi.CacheOption = BitmapCacheOption.OnLoad;
                            bi.EndInit();
                            bi.Freeze();
                            zdjecia[towarId] = bi;
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return zdjecia;
        }

        private async Task UtworzTabeleLiczenia(SqlConnection cn, SqlTransaction transaction)
        {
            const string checkTableSql = @"
                IF NOT EXISTS (SELECT * FROM sys.objects 
                WHERE object_id = OBJECT_ID(N'[dbo].[StanyMagazynowe]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [dbo].[StanyMagazynowe](
                        [Id] [int] IDENTITY(1,1) NOT NULL,
                        [Data] [date] NOT NULL,
                        [ProduktId] [int] NOT NULL,
                        [Stan] [decimal](18, 2) NOT NULL,
                        [Uzytkownik] [nvarchar](50) NULL,
                        [DataZapisu] [datetime] NOT NULL,
                        CONSTRAINT [PK_StanyMagazynowe] PRIMARY KEY CLUSTERED ([Id] ASC)
                    )
                    CREATE INDEX IX_StanyMagazynowe_Data ON [dbo].[StanyMagazynowe] ([Data])
                END";

            await using var cmd = new SqlCommand(checkTableSql, cn, transaction);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}