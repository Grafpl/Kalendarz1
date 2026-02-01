using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.Kartoteka.Models;

namespace Kalendarz1.Kartoteka.Features.Historia
{
    public class HistoriaZmianService
    {
        private readonly string _connLibra;

        public HistoriaZmianService(string connLibra)
        {
            _connLibra = connLibra;
        }

        public async Task EnsureTableExistsAsync()
        {
            const string sql = @"
                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.KartotekaHistoriaZmian') AND type = 'U')
                BEGIN
                    CREATE TABLE dbo.KartotekaHistoriaZmian (
                        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                        TabelaNazwa NVARCHAR(100) NOT NULL,
                        RekordId INT NOT NULL,
                        KlientId INT,
                        TypOperacji NVARCHAR(10) NOT NULL,
                        PoleNazwa NVARCHAR(100),
                        StaraWartosc NVARCHAR(MAX),
                        NowaWartosc NVARCHAR(MAX),
                        UzytkownikId NVARCHAR(100) NOT NULL,
                        UzytkownikNazwa NVARCHAR(200),
                        DataZmiany DATETIME NOT NULL DEFAULT GETDATE(),
                        Komentarz NVARCHAR(500),
                        CzyCofniete BIT DEFAULT 0,
                        CofnietePrzez NVARCHAR(100),
                        DataCofniecia DATETIME
                    );
                    CREATE NONCLUSTERED INDEX IX_Historia_KlientId ON dbo.KartotekaHistoriaZmian (KlientId);
                    CREATE NONCLUSTERED INDEX IX_Historia_DataZmiany ON dbo.KartotekaHistoriaZmian (DataZmiany DESC);
                    CREATE NONCLUSTERED INDEX IX_Historia_Uzytkownik ON dbo.KartotekaHistoriaZmian (UzytkownikId);
                END";

            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task LogujZmianeAsync(HistoriaZmiany zmiana)
        {
            const string sql = @"
                INSERT INTO dbo.KartotekaHistoriaZmian
                    (TabelaNazwa, RekordId, KlientId, TypOperacji, PoleNazwa, StaraWartosc, NowaWartosc,
                     UzytkownikId, UzytkownikNazwa, DataZmiany, Komentarz)
                VALUES
                    (@TabelaNazwa, @RekordId, @KlientId, @TypOperacji, @PoleNazwa, @StaraWartosc, @NowaWartosc,
                     @UzytkownikId, @UzytkownikNazwa, GETDATE(), @Komentarz)";

            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@TabelaNazwa", zmiana.TabelaNazwa ?? "KartotekaOdbiorcyDane");
            cmd.Parameters.AddWithValue("@RekordId", zmiana.RekordId);
            cmd.Parameters.AddWithValue("@KlientId", (object)zmiana.KlientId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TypOperacji", zmiana.TypOperacji ?? "UPDATE");
            cmd.Parameters.AddWithValue("@PoleNazwa", (object)zmiana.PoleNazwa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@StaraWartosc", (object)zmiana.StaraWartosc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@NowaWartosc", (object)zmiana.NowaWartosc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UzytkownikId", zmiana.UzytkownikId ?? "");
            cmd.Parameters.AddWithValue("@UzytkownikNazwa", (object)zmiana.UzytkownikNazwa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Komentarz", (object)zmiana.Komentarz ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task LogujZmianyAsync(int klientId, List<ZmianaPola> zmiany,
            string uzytkownikId, string uzytkownikNazwa, string tabelaNazwa = "KartotekaOdbiorcyDane")
        {
            if (zmiany == null || zmiany.Count == 0) return;

            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            foreach (var zmiana in zmiany)
            {
                const string sql = @"
                    INSERT INTO dbo.KartotekaHistoriaZmian
                        (TabelaNazwa, RekordId, KlientId, TypOperacji, PoleNazwa, StaraWartosc, NowaWartosc,
                         UzytkownikId, UzytkownikNazwa, DataZmiany)
                    VALUES
                        (@TabelaNazwa, @RekordId, @KlientId, 'UPDATE', @PoleNazwa, @StaraWartosc, @NowaWartosc,
                         @UzytkownikId, @UzytkownikNazwa, GETDATE())";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@TabelaNazwa", tabelaNazwa);
                cmd.Parameters.AddWithValue("@RekordId", klientId);
                cmd.Parameters.AddWithValue("@KlientId", klientId);
                cmd.Parameters.AddWithValue("@PoleNazwa", zmiana.NazwaPola);
                cmd.Parameters.AddWithValue("@StaraWartosc", (object)zmiana.StaraWartosc ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@NowaWartosc", (object)zmiana.NowaWartosc ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@UzytkownikId", uzytkownikId);
                cmd.Parameters.AddWithValue("@UzytkownikNazwa", (object)uzytkownikNazwa ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<List<HistoriaZmiany>> PobierzHistorieKlientaAsync(int klientId,
            DateTime? odDaty = null, DateTime? doDaty = null, string uzytkownik = null, int limit = 200)
        {
            var sql = @"SELECT TOP (@Limit) Id, TabelaNazwa, RekordId, KlientId, TypOperacji, PoleNazwa,
                               StaraWartosc, NowaWartosc, UzytkownikId, UzytkownikNazwa, DataZmiany,
                               Komentarz, CzyCofniete, CofnietePrzez, DataCofniecia
                        FROM dbo.KartotekaHistoriaZmian
                        WHERE KlientId = @KlientId";

            if (odDaty.HasValue) sql += " AND DataZmiany >= @OdDaty";
            if (doDaty.HasValue) sql += " AND DataZmiany <= @DoDaty";
            if (!string.IsNullOrEmpty(uzytkownik)) sql += " AND UzytkownikNazwa LIKE @Uzytkownik";

            sql += " ORDER BY DataZmiany DESC";

            var wynik = new List<HistoriaZmiany>();

            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@KlientId", klientId);
            cmd.Parameters.AddWithValue("@Limit", limit);
            if (odDaty.HasValue) cmd.Parameters.AddWithValue("@OdDaty", odDaty.Value);
            if (doDaty.HasValue) cmd.Parameters.AddWithValue("@DoDaty", doDaty.Value.Date.AddDays(1));
            if (!string.IsNullOrEmpty(uzytkownik)) cmd.Parameters.AddWithValue("@Uzytkownik", $"%{uzytkownik}%");

            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                wynik.Add(new HistoriaZmiany
                {
                    Id = rd.GetInt64(0),
                    TabelaNazwa = rd.GetString(1),
                    RekordId = rd.GetInt32(2),
                    KlientId = rd.IsDBNull(3) ? null : rd.GetInt32(3),
                    TypOperacji = rd.GetString(4),
                    PoleNazwa = rd.IsDBNull(5) ? null : rd.GetString(5),
                    StaraWartosc = rd.IsDBNull(6) ? null : rd.GetString(6),
                    NowaWartosc = rd.IsDBNull(7) ? null : rd.GetString(7),
                    UzytkownikId = rd.GetString(8),
                    UzytkownikNazwa = rd.IsDBNull(9) ? null : rd.GetString(9),
                    DataZmiany = rd.GetDateTime(10),
                    Komentarz = rd.IsDBNull(11) ? null : rd.GetString(11),
                    CzyCofniete = !rd.IsDBNull(12) && rd.GetBoolean(12),
                    CofnietePrzez = rd.IsDBNull(13) ? null : rd.GetString(13),
                    DataCofniecia = rd.IsDBNull(14) ? null : rd.GetDateTime(14)
                });
            }

            return wynik;
        }

        public async Task<bool> CofnijZmianeAsync(long historiaId, string uzytkownik)
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            // Pobierz szczegóły zmiany
            HistoriaZmiany zmiana = null;
            const string sqlSelect = @"SELECT Id, TabelaNazwa, RekordId, KlientId, TypOperacji, PoleNazwa,
                                              StaraWartosc, NowaWartosc FROM dbo.KartotekaHistoriaZmian WHERE Id = @Id AND CzyCofniete = 0";
            await using (var cmdSelect = new SqlCommand(sqlSelect, cn))
            {
                cmdSelect.Parameters.AddWithValue("@Id", historiaId);
                await using var rd = await cmdSelect.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    zmiana = new HistoriaZmiany
                    {
                        Id = rd.GetInt64(0),
                        TabelaNazwa = rd.GetString(1),
                        RekordId = rd.GetInt32(2),
                        KlientId = rd.IsDBNull(3) ? null : rd.GetInt32(3),
                        TypOperacji = rd.GetString(4),
                        PoleNazwa = rd.IsDBNull(5) ? null : rd.GetString(5),
                        StaraWartosc = rd.IsDBNull(6) ? null : rd.GetString(6),
                        NowaWartosc = rd.IsDBNull(7) ? null : rd.GetString(7)
                    };
                }
            }

            if (zmiana == null) return false;

            // Cofnij wartość w tabeli docelowej
            if (zmiana.TypOperacji == "UPDATE" && !string.IsNullOrEmpty(zmiana.PoleNazwa))
            {
                // Bezpieczna lista dozwolonych pól
                var dozwolonePola = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "OsobaKontaktowa", "TelefonKontakt", "EmailKontakt",
                    "Asortyment", "PreferencjePakowania", "PreferencjeJakosci", "PreferencjeDostawy",
                    "PreferowanyDzienDostawy", "PreferowanaGodzinaDostawy", "AdresDostawyInny",
                    "Trasa", "KategoriaHandlowca", "Notatki"
                };

                if (dozwolonePola.Contains(zmiana.PoleNazwa))
                {
                    var sqlUpdate = $"UPDATE dbo.KartotekaOdbiorcyDane SET [{zmiana.PoleNazwa}] = @StaraWartosc, " +
                                    "DataModyfikacji = GETDATE(), ModyfikowalPrzez = @Uzytkownik WHERE IdSymfonia = @IdSymfonia";

                    await using var cmdUpdate = new SqlCommand(sqlUpdate, cn);
                    cmdUpdate.Parameters.AddWithValue("@StaraWartosc",
                        string.IsNullOrEmpty(zmiana.StaraWartosc) ? (object)DBNull.Value : zmiana.StaraWartosc);
                    cmdUpdate.Parameters.AddWithValue("@Uzytkownik", uzytkownik);
                    cmdUpdate.Parameters.AddWithValue("@IdSymfonia", zmiana.RekordId);
                    await cmdUpdate.ExecuteNonQueryAsync();
                }
            }

            // Oznacz jako cofnięte
            const string sqlCofnij = @"UPDATE dbo.KartotekaHistoriaZmian
                                       SET CzyCofniete = 1, CofnietePrzez = @Uzytkownik, DataCofniecia = GETDATE()
                                       WHERE Id = @Id";
            await using var cmdCofnij = new SqlCommand(sqlCofnij, cn);
            cmdCofnij.Parameters.AddWithValue("@Id", historiaId);
            cmdCofnij.Parameters.AddWithValue("@Uzytkownik", uzytkownik);
            await cmdCofnij.ExecuteNonQueryAsync();

            // Zaloguj cofnięcie jako nową zmianę
            const string sqlLog = @"INSERT INTO dbo.KartotekaHistoriaZmian
                (TabelaNazwa, RekordId, KlientId, TypOperacji, PoleNazwa, StaraWartosc, NowaWartosc,
                 UzytkownikId, UzytkownikNazwa, DataZmiany, Komentarz)
                VALUES (@TabelaNazwa, @RekordId, @KlientId, 'UPDATE', @PoleNazwa, @NowaWartosc, @StaraWartosc,
                        @UzytkownikId, @UzytkownikNazwa, GETDATE(), @Komentarz)";

            await using var cmdLog = new SqlCommand(sqlLog, cn);
            cmdLog.Parameters.AddWithValue("@TabelaNazwa", zmiana.TabelaNazwa);
            cmdLog.Parameters.AddWithValue("@RekordId", zmiana.RekordId);
            cmdLog.Parameters.AddWithValue("@KlientId", (object)zmiana.KlientId ?? DBNull.Value);
            cmdLog.Parameters.AddWithValue("@PoleNazwa", (object)zmiana.PoleNazwa ?? DBNull.Value);
            cmdLog.Parameters.AddWithValue("@NowaWartosc", (object)zmiana.NowaWartosc ?? DBNull.Value);
            cmdLog.Parameters.AddWithValue("@StaraWartosc", (object)zmiana.StaraWartosc ?? DBNull.Value);
            cmdLog.Parameters.AddWithValue("@UzytkownikId", uzytkownik);
            cmdLog.Parameters.AddWithValue("@UzytkownikNazwa", uzytkownik);
            cmdLog.Parameters.AddWithValue("@Komentarz", $"Cofnięcie zmiany #{historiaId}");
            await cmdLog.ExecuteNonQueryAsync();

            return true;
        }

        public async Task<List<string>> PobierzUzytkownikowAsync(int klientId)
        {
            const string sql = @"SELECT DISTINCT UzytkownikNazwa FROM dbo.KartotekaHistoriaZmian
                                 WHERE KlientId = @KlientId AND UzytkownikNazwa IS NOT NULL
                                 ORDER BY UzytkownikNazwa";

            var wynik = new List<string>();
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@KlientId", klientId);
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                wynik.Add(rd.GetString(0));

            return wynik;
        }
    }
}
