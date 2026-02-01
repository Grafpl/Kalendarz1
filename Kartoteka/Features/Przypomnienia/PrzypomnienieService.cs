using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.Kartoteka.Models;

namespace Kalendarz1.Kartoteka.Features.Przypomnienia
{
    public class PrzypomnienieService
    {
        private readonly string _connLibra;

        public PrzypomnienieService(string connLibra)
        {
            _connLibra = connLibra;
        }

        public async Task EnsureTableExistsAsync()
        {
            const string sql = @"
                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.KartotekaPrzypomnienia') AND type = 'U')
                BEGIN
                    CREATE TABLE dbo.KartotekaPrzypomnienia (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        KlientId INT,
                        FakturaId INT,
                        KontaktId INT,
                        Typ NVARCHAR(50) NOT NULL,
                        Tytul NVARCHAR(200) NOT NULL,
                        Opis NVARCHAR(MAX),
                        Priorytet INT DEFAULT 3,
                        DataPrzypomnienia DATETIME NOT NULL,
                        DataWygasniecia DATETIME,
                        Status NVARCHAR(50) DEFAULT 'Aktywne',
                        PrzypisaneDo NVARCHAR(100),
                        CzyPowtarzalne BIT DEFAULT 0,
                        InterwalDni INT,
                        DataUtworzenia DATETIME DEFAULT GETDATE(),
                        UtworzonyPrzez NVARCHAR(100),
                        DataModyfikacji DATETIME
                    );
                    CREATE NONCLUSTERED INDEX IX_Przyp_Data ON dbo.KartotekaPrzypomnienia (DataPrzypomnienia);
                    CREATE NONCLUSTERED INDEX IX_Przyp_Klient ON dbo.KartotekaPrzypomnienia (KlientId);
                    CREATE NONCLUSTERED INDEX IX_Przyp_Status ON dbo.KartotekaPrzypomnienia (Status);
                END";

            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> DodajPrzypomnienieAsync(Przypomnienie p)
        {
            const string sql = @"
                INSERT INTO dbo.KartotekaPrzypomnienia
                    (KlientId, FakturaId, KontaktId, Typ, Tytul, Opis, Priorytet,
                     DataPrzypomnienia, DataWygasniecia, Status, PrzypisaneDo,
                     CzyPowtarzalne, InterwalDni, UtworzonyPrzez)
                OUTPUT INSERTED.Id
                VALUES
                    (@KlientId, @FakturaId, @KontaktId, @Typ, @Tytul, @Opis, @Priorytet,
                     @DataPrzypomnienia, @DataWygasniecia, 'Aktywne', @PrzypisaneDo,
                     @CzyPowtarzalne, @InterwalDni, @UtworzonyPrzez)";

            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@KlientId", (object)p.KlientId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FakturaId", (object)p.FakturaId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@KontaktId", (object)p.KontaktId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Typ", p.Typ);
            cmd.Parameters.AddWithValue("@Tytul", p.Tytul);
            cmd.Parameters.AddWithValue("@Opis", (object)p.Opis ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Priorytet", p.Priorytet);
            cmd.Parameters.AddWithValue("@DataPrzypomnienia", p.DataPrzypomnienia);
            cmd.Parameters.AddWithValue("@DataWygasniecia", (object)p.DataWygasniecia ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PrzypisaneDo", (object)p.PrzypisaneDo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CzyPowtarzalne", p.CzyPowtarzalne);
            cmd.Parameters.AddWithValue("@InterwalDni", (object)p.InterwalDni ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UtworzonyPrzez", (object)p.UtworzonyPrzez ?? DBNull.Value);

            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<List<Przypomnienie>> PobierzAktywnePrzypomnienia(string przypisaneDo = null, int? klientId = null)
        {
            var sql = @"SELECT p.Id, p.KlientId, p.FakturaId, p.KontaktId, p.Typ, p.Tytul, p.Opis, p.Priorytet,
                               p.DataPrzypomnienia, p.DataWygasniecia, p.Status, p.PrzypisaneDo,
                               p.CzyPowtarzalne, p.InterwalDni, p.DataUtworzenia, p.UtworzonyPrzez,
                               p.DataModyfikacji
                        FROM dbo.KartotekaPrzypomnienia p
                        WHERE p.Status IN ('Aktywne', 'Odlozone')
                          AND (p.DataWygasniecia IS NULL OR p.DataWygasniecia >= GETDATE())";

            if (!string.IsNullOrEmpty(przypisaneDo))
                sql += " AND (p.PrzypisaneDo IS NULL OR p.PrzypisaneDo = @PrzypisaneDo)";
            if (klientId.HasValue)
                sql += " AND p.KlientId = @KlientId";

            sql += " ORDER BY p.Priorytet ASC, p.DataPrzypomnienia ASC";

            var wynik = new List<Przypomnienie>();
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            if (!string.IsNullOrEmpty(przypisaneDo))
                cmd.Parameters.AddWithValue("@PrzypisaneDo", przypisaneDo);
            if (klientId.HasValue)
                cmd.Parameters.AddWithValue("@KlientId", klientId.Value);

            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                wynik.Add(MapPrzypomnienie(rd));
            }
            return wynik;
        }

        public async Task<List<Przypomnienie>> PobierzPrzypomnieniaDzisiaj(string przypisaneDo = null)
        {
            var sql = @"SELECT p.Id, p.KlientId, p.FakturaId, p.KontaktId, p.Typ, p.Tytul, p.Opis, p.Priorytet,
                               p.DataPrzypomnienia, p.DataWygasniecia, p.Status, p.PrzypisaneDo,
                               p.CzyPowtarzalne, p.InterwalDni, p.DataUtworzenia, p.UtworzonyPrzez,
                               p.DataModyfikacji
                        FROM dbo.KartotekaPrzypomnienia p
                        WHERE p.Status = 'Aktywne'
                          AND CAST(p.DataPrzypomnienia AS DATE) <= CAST(GETDATE() AS DATE)";

            if (!string.IsNullOrEmpty(przypisaneDo))
                sql += " AND (p.PrzypisaneDo IS NULL OR p.PrzypisaneDo = @PrzypisaneDo)";

            sql += " ORDER BY p.Priorytet ASC, p.DataPrzypomnienia ASC";

            var wynik = new List<Przypomnienie>();
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            if (!string.IsNullOrEmpty(przypisaneDo))
                cmd.Parameters.AddWithValue("@PrzypisaneDo", przypisaneDo);

            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                wynik.Add(MapPrzypomnienie(rd));

            return wynik;
        }

        public async Task ZmienStatusAsync(int id, string nowyStatus, string uzytkownik)
        {
            const string sql = @"UPDATE dbo.KartotekaPrzypomnienia
                                 SET Status = @Status, DataModyfikacji = GETDATE()
                                 WHERE Id = @Id";

            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Status", nowyStatus);
            await cmd.ExecuteNonQueryAsync();

            // Jeśli powtarzalne i oznaczone jako wykonane, utwórz następne
            if (nowyStatus == "Wykonane")
            {
                const string sqlCheck = @"SELECT CzyPowtarzalne, InterwalDni, KlientId, Typ, Tytul, Opis, Priorytet, PrzypisaneDo
                                          FROM dbo.KartotekaPrzypomnienia WHERE Id = @Id";
                await using var cmdCheck = new SqlCommand(sqlCheck, cn);
                cmdCheck.Parameters.AddWithValue("@Id", id);
                await using var rd = await cmdCheck.ExecuteReaderAsync();
                if (await rd.ReadAsync() && !rd.IsDBNull(0) && rd.GetBoolean(0) && !rd.IsDBNull(1))
                {
                    int interwal = rd.GetInt32(1);
                    var nowe = new Przypomnienie
                    {
                        KlientId = rd.IsDBNull(2) ? null : rd.GetInt32(2),
                        Typ = rd.GetString(3),
                        Tytul = rd.GetString(4),
                        Opis = rd.IsDBNull(5) ? null : rd.GetString(5),
                        Priorytet = rd.GetInt32(6),
                        DataPrzypomnienia = DateTime.Now.AddDays(interwal),
                        PrzypisaneDo = rd.IsDBNull(7) ? null : rd.GetString(7),
                        CzyPowtarzalne = true,
                        InterwalDni = interwal,
                        UtworzonyPrzez = uzytkownik
                    };
                    rd.Close();
                    await DodajPrzypomnienieAsync(nowe);
                }
            }
        }

        public async Task UsunPrzypomnienieAsync(int id)
        {
            const string sql = "DELETE FROM dbo.KartotekaPrzypomnienia WHERE Id = @Id";
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> PobierzLiczbePrzypomnieńDzisiaj(string przypisaneDo = null)
        {
            var sql = @"SELECT COUNT(*) FROM dbo.KartotekaPrzypomnienia
                        WHERE Status = 'Aktywne' AND CAST(DataPrzypomnienia AS DATE) <= CAST(GETDATE() AS DATE)";
            if (!string.IsNullOrEmpty(przypisaneDo))
                sql += " AND (PrzypisaneDo IS NULL OR PrzypisaneDo = @PrzypisaneDo)";

            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            if (!string.IsNullOrEmpty(przypisaneDo))
                cmd.Parameters.AddWithValue("@PrzypisaneDo", przypisaneDo);

            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        private Przypomnienie MapPrzypomnienie(SqlDataReader rd)
        {
            return new Przypomnienie
            {
                Id = rd.GetInt32(0),
                KlientId = rd.IsDBNull(1) ? null : rd.GetInt32(1),
                FakturaId = rd.IsDBNull(2) ? null : rd.GetInt32(2),
                KontaktId = rd.IsDBNull(3) ? null : rd.GetInt32(3),
                Typ = rd.GetString(4),
                Tytul = rd.GetString(5),
                Opis = rd.IsDBNull(6) ? null : rd.GetString(6),
                Priorytet = rd.GetInt32(7),
                DataPrzypomnienia = rd.GetDateTime(8),
                DataWygasniecia = rd.IsDBNull(9) ? null : rd.GetDateTime(9),
                Status = rd.IsDBNull(10) ? "Aktywne" : rd.GetString(10),
                PrzypisaneDo = rd.IsDBNull(11) ? null : rd.GetString(11),
                CzyPowtarzalne = !rd.IsDBNull(12) && rd.GetBoolean(12),
                InterwalDni = rd.IsDBNull(13) ? null : rd.GetInt32(13),
                DataUtworzenia = rd.IsDBNull(14) ? DateTime.Now : rd.GetDateTime(14),
                UtworzonyPrzez = rd.IsDBNull(15) ? null : rd.GetString(15),
                DataModyfikacji = rd.IsDBNull(16) ? null : rd.GetDateTime(16)
            };
        }
    }
}
