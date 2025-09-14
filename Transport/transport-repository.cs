// Plik: Transport/TransportRepozytorium.cs
// Repozytorium dla operacji na bazie danych TransportPL

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.Transport.Pakowanie;

namespace Kalendarz1.Transport.Repozytorium
{
    /// <summary>
    /// Repozytorium zarządzające danymi transportu
    /// </summary>
    public class TransportRepozytorium
    {
        private readonly string _connectionString;
        private readonly string _libraConnectionString;
        private readonly IPakowanieSerwis _pakowanieSerwis;

        public TransportRepozytorium(string connectionString, string libraConnectionString = null)
        {
            _connectionString = connectionString;
            _libraConnectionString = libraConnectionString ??
                "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
            _pakowanieSerwis = new PakowanieSerwis();
        }

        #region Kierowcy

        public async Task<List<Kierowca>> PobierzKierowcowAsync(bool tylkoAktywni = true)
        {
            var kierowcy = new List<Kierowca>();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT KierowcaID, Imie, Nazwisko, Telefon, Aktywny, UtworzonoUTC, ZmienionoUTC 
                       FROM dbo.Kierowca 
                       WHERE (@TylkoAktywni = 0 OR Aktywny = 1)
                       ORDER BY Nazwisko, Imie";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@TylkoAktywni", tylkoAktywni ? 1 : 0);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                kierowcy.Add(new Kierowca
                {
                    KierowcaID = reader.GetInt32(0),
                    Imie = reader.GetString(1),
                    Nazwisko = reader.GetString(2),
                    Telefon = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Aktywny = reader.GetBoolean(4),
                    UtworzonoUTC = reader.GetDateTime(5),
                    ZmienionoUTC = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
                });
            }

            return kierowcy;
        }

        public async Task<int> DodajKierowceAsync(Kierowca kierowca)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"INSERT INTO dbo.Kierowca (Imie, Nazwisko, Telefon, Aktywny) 
                       OUTPUT INSERTED.KierowcaID
                       VALUES (@Imie, @Nazwisko, @Telefon, @Aktywny)";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Imie", kierowca.Imie);
            cmd.Parameters.AddWithValue("@Nazwisko", kierowca.Nazwisko);
            cmd.Parameters.AddWithValue("@Telefon", (object)kierowca.Telefon ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Aktywny", kierowca.Aktywny);

            return (int)await cmd.ExecuteScalarAsync();
        }

        public async Task AktualizujKierowceAsync(Kierowca kierowca)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"UPDATE dbo.Kierowca 
                       SET Imie = @Imie, Nazwisko = @Nazwisko, Telefon = @Telefon, 
                           Aktywny = @Aktywny, ZmienionoUTC = SYSUTCDATETIME()
                       WHERE KierowcaID = @KierowcaID";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@KierowcaID", kierowca.KierowcaID);
            cmd.Parameters.AddWithValue("@Imie", kierowca.Imie);
            cmd.Parameters.AddWithValue("@Nazwisko", kierowca.Nazwisko);
            cmd.Parameters.AddWithValue("@Telefon", (object)kierowca.Telefon ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Aktywny", kierowca.Aktywny);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UstawAktywnyKierowcaAsync(int kierowcaId, bool aktywny)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"UPDATE dbo.Kierowca 
                       SET Aktywny = @Aktywny, ZmienionoUTC = SYSUTCDATETIME()
                       WHERE KierowcaID = @KierowcaID";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@KierowcaID", kierowcaId);
            cmd.Parameters.AddWithValue("@Aktywny", aktywny);

            await cmd.ExecuteNonQueryAsync();
        }

        #endregion

        #region Pojazdy

        public async Task<List<Pojazd>> PobierzPojazdyAsync(bool tylkoAktywne = true)
        {
            var pojazdy = new List<Pojazd>();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT PojazdID, Rejestracja, Marka, Model, PaletyH1, Aktywny, 
                              UtworzonoUTC, ZmienionoUTC 
                       FROM dbo.Pojazd 
                       WHERE (@TylkoAktywne = 0 OR Aktywny = 1)
                       ORDER BY Rejestracja";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@TylkoAktywne", tylkoAktywne ? 1 : 0);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                pojazdy.Add(new Pojazd
                {
                    PojazdID = reader.GetInt32(0),
                    Rejestracja = reader.GetString(1),
                    Marka = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Model = reader.IsDBNull(3) ? null : reader.GetString(3),
                    PaletyH1 = reader.GetInt32(4),
                    Aktywny = reader.GetBoolean(5),
                    UtworzonoUTC = reader.GetDateTime(6),
                    ZmienionoUTC = reader.IsDBNull(7) ? null : reader.GetDateTime(7)
                });
            }

            return pojazdy;
        }

        public async Task<int> DodajPojazdAsync(Pojazd pojazd)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"INSERT INTO dbo.Pojazd (Rejestracja, Marka, Model, PaletyH1, Aktywny) 
                       OUTPUT INSERTED.PojazdID
                       VALUES (@Rejestracja, @Marka, @Model, @PaletyH1, @Aktywny)";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Rejestracja", pojazd.Rejestracja);
            cmd.Parameters.AddWithValue("@Marka", (object)pojazd.Marka ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Model", (object)pojazd.Model ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PaletyH1", pojazd.PaletyH1);
            cmd.Parameters.AddWithValue("@Aktywny", pojazd.Aktywny);

            return (int)await cmd.ExecuteScalarAsync();
        }

        public async Task AktualizujPojazdAsync(Pojazd pojazd)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"UPDATE dbo.Pojazd 
                       SET Rejestracja = @Rejestracja, Marka = @Marka, Model = @Model, 
                           PaletyH1 = @PaletyH1, Aktywny = @Aktywny, ZmienionoUTC = SYSUTCDATETIME()
                       WHERE PojazdID = @PojazdID";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@PojazdID", pojazd.PojazdID);
            cmd.Parameters.AddWithValue("@Rejestracja", pojazd.Rejestracja);
            cmd.Parameters.AddWithValue("@Marka", (object)pojazd.Marka ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Model", (object)pojazd.Model ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PaletyH1", pojazd.PaletyH1);
            cmd.Parameters.AddWithValue("@Aktywny", pojazd.Aktywny);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UstawAktywnyPojazdAsync(int pojazdId, bool aktywny)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"UPDATE dbo.Pojazd 
                       SET Aktywny = @Aktywny, ZmienionoUTC = SYSUTCDATETIME()
                       WHERE PojazdID = @PojazdID";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@PojazdID", pojazdId);
            cmd.Parameters.AddWithValue("@Aktywny", aktywny);

            await cmd.ExecuteNonQueryAsync();
        }

        #endregion

        #region Kursy

        // Metoda do pobrania pojedynczego kursu po ID
        public async Task<Kurs> PobierzKursPoIdAsync(long kursId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT 
                    k.KursID, k.DataKursu, k.KierowcaID, k.PojazdID, k.Trasa,
                    k.GodzWyjazdu, k.GodzPowrotu, k.Status, k.PlanE2NaPalete,
                    k.UtworzonoUTC, k.Utworzyl, k.ZmienionoUTC, k.Zmienil,
                    CONCAT(ki.Imie, ' ', ki.Nazwisko) AS KierowcaNazwa,
                    p.Rejestracja,
                    ISNULL(p.PaletyH1, 33) AS PaletyPojazdu
                FROM dbo.Kurs k
                LEFT JOIN dbo.Kierowca ki ON k.KierowcaID = ki.KierowcaID
                LEFT JOIN dbo.Pojazd p ON k.PojazdID = p.PojazdID
                WHERE k.KursID = @KursID";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@KursID", kursId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var kurs = new Kurs
                {
                    KursID = reader.GetInt64(0),
                    DataKursu = reader.GetDateTime(1),
                    KierowcaID = reader.GetInt32(2),
                    PojazdID = reader.GetInt32(3),
                    Trasa = reader.IsDBNull(4) ? null : reader.GetString(4),
                    GodzWyjazdu = reader.IsDBNull(5) ? null : reader.GetTimeSpan(5),
                    GodzPowrotu = reader.IsDBNull(6) ? null : reader.GetTimeSpan(6),
                    Status = reader.GetString(7),
                    PlanE2NaPalete = reader.GetByte(8),
                    UtworzonoUTC = reader.GetDateTime(9),
                    Utworzyl = reader.IsDBNull(10) ? null : reader.GetString(10),
                    ZmienionoUTC = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                    Zmienil = reader.IsDBNull(12) ? null : reader.GetString(12),
                    KierowcaNazwa = reader.IsDBNull(13) ? "" : reader.GetString(13),
                    PojazdRejestracja = reader.IsDBNull(14) ? "" : reader.GetString(14),
                    PaletyPojazdu = reader.IsDBNull(15) ? 33 : reader.GetInt32(15)
                };

                // Oblicz wypełnienie
                try
                {
                    var wynik = await ObliczPakowanieKursuAsync(kurs.KursID);
                    kurs.SumaE2 = wynik.SumaE2;
                    kurs.PaletyNominal = wynik.PaletyNominal;
                    kurs.PaletyMax = wynik.PaletyMax;
                    kurs.ProcNominal = wynik.ProcNominal;
                    kurs.ProcMax = wynik.ProcMax;
                }
                catch
                {
                    kurs.SumaE2 = 0;
                    kurs.PaletyNominal = 0;
                    kurs.PaletyMax = 0;
                    kurs.ProcNominal = 0;
                    kurs.ProcMax = 0;
                }

                return kurs;
            }

            return null;
        }

        public async Task<List<Kurs>> PobierzKursyPoDacieAsync(DateTime data)
        {
            var kursy = new List<Kurs>();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT 
                    k.KursID, k.DataKursu, k.KierowcaID, k.PojazdID, k.Trasa,
                    k.GodzWyjazdu, k.GodzPowrotu, k.Status, k.PlanE2NaPalete,
                    k.UtworzonoUTC, k.Utworzyl, k.ZmienionoUTC, k.Zmienil,
                    CONCAT(ki.Imie, ' ', ki.Nazwisko) AS KierowcaNazwa,
                    p.Rejestracja,
                    ISNULL(p.PaletyH1, 33) AS PaletyPojazdu,
                    0 AS SumaE2,
                    0 AS PaletyNominal,
                    0 AS PaletyMax,
                    CAST(0 AS decimal(10,2)) AS ProcNominal,
                    CAST(0 AS decimal(10,2)) AS ProcMax
                FROM dbo.Kurs k
                LEFT JOIN dbo.Kierowca ki ON k.KierowcaID = ki.KierowcaID
                LEFT JOIN dbo.Pojazd p ON k.PojazdID = p.PojazdID
                WHERE k.DataKursu = @Data
                ORDER BY k.GodzWyjazdu, k.KursID";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Data", data.Date);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                try
                {
                    var kurs = new Kurs
                    {
                        KursID = reader.GetInt64(0),
                        DataKursu = reader.GetDateTime(1),
                        KierowcaID = reader.GetInt32(2),
                        PojazdID = reader.GetInt32(3),
                        Trasa = reader.IsDBNull(4) ? null : reader.GetString(4),
                        GodzWyjazdu = reader.IsDBNull(5) ? null : reader.GetTimeSpan(5),
                        GodzPowrotu = reader.IsDBNull(6) ? null : reader.GetTimeSpan(6),
                        Status = reader.GetString(7),
                        PlanE2NaPalete = reader.GetByte(8),
                        UtworzonoUTC = reader.GetDateTime(9),
                        Utworzyl = reader.IsDBNull(10) ? null : reader.GetString(10),
                        ZmienionoUTC = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                        Zmienil = reader.IsDBNull(12) ? null : reader.GetString(12),
                        KierowcaNazwa = reader.IsDBNull(13) ? "" : reader.GetString(13),
                        PojazdRejestracja = reader.IsDBNull(14) ? "" : reader.GetString(14),
                        PaletyPojazdu = reader.IsDBNull(15) ? 33 : reader.GetInt32(15),
                        SumaE2 = 0,
                        PaletyNominal = 0,
                        PaletyMax = 0,
                        ProcNominal = 0,
                        ProcMax = 0
                    };

                    kursy.Add(kurs);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Błąd podczas parsowania kursu: {ex.Message}");
                }
            }

            // Oblicz wypełnienia dla każdego kursu
            foreach (var kurs in kursy)
            {
                try
                {
                    var wynik = await ObliczPakowanieKursuAsync(kurs.KursID);
                    kurs.SumaE2 = wynik.SumaE2;
                    kurs.PaletyNominal = wynik.PaletyNominal;
                    kurs.PaletyMax = wynik.PaletyMax;
                    kurs.ProcNominal = wynik.ProcNominal;
                    kurs.ProcMax = wynik.ProcMax;
                }
                catch
                {
                    // Pozostaw domyślne wartości
                }
            }

            return kursy;
        }

        public async Task<long> DodajKursAsync(Kurs kurs, string uzytkownik)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"INSERT INTO dbo.Kurs 
                       (DataKursu, KierowcaID, PojazdID, Trasa, GodzWyjazdu, GodzPowrotu, 
                        Status, PlanE2NaPalete, Utworzyl) 
                       OUTPUT INSERTED.KursID
                       VALUES (@DataKursu, @KierowcaID, @PojazdID, @Trasa, @GodzWyjazdu, 
                               @GodzPowrotu, @Status, @PlanE2NaPalete, @Utworzyl)";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@DataKursu", kurs.DataKursu);
            cmd.Parameters.AddWithValue("@KierowcaID", kurs.KierowcaID);
            cmd.Parameters.AddWithValue("@PojazdID", kurs.PojazdID);
            cmd.Parameters.AddWithValue("@Trasa", (object)kurs.Trasa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@GodzWyjazdu", (object)kurs.GodzWyjazdu ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@GodzPowrotu", (object)kurs.GodzPowrotu ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", kurs.Status);
            cmd.Parameters.AddWithValue("@PlanE2NaPalete", kurs.PlanE2NaPalete);
            cmd.Parameters.AddWithValue("@Utworzyl", uzytkownik ?? Environment.UserName);

            return (long)await cmd.ExecuteScalarAsync();
        }

        public async Task AktualizujNaglowekKursuAsync(Kurs kurs, string uzytkownik)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"UPDATE dbo.Kurs 
                       SET DataKursu = @DataKursu, KierowcaID = @KierowcaID, PojazdID = @PojazdID,
                           Trasa = @Trasa, GodzWyjazdu = @GodzWyjazdu, GodzPowrotu = @GodzPowrotu,
                           Status = @Status, PlanE2NaPalete = @PlanE2NaPalete,
                           ZmienionoUTC = SYSUTCDATETIME(), Zmienil = @Zmienil
                       WHERE KursID = @KursID";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@KursID", kurs.KursID);
            cmd.Parameters.AddWithValue("@DataKursu", kurs.DataKursu);
            cmd.Parameters.AddWithValue("@KierowcaID", kurs.KierowcaID);
            cmd.Parameters.AddWithValue("@PojazdID", kurs.PojazdID);
            cmd.Parameters.AddWithValue("@Trasa", (object)kurs.Trasa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@GodzWyjazdu", (object)kurs.GodzWyjazdu ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@GodzPowrotu", (object)kurs.GodzPowrotu ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", kurs.Status);
            cmd.Parameters.AddWithValue("@PlanE2NaPalete", kurs.PlanE2NaPalete);
            cmd.Parameters.AddWithValue("@Zmienil", uzytkownik ?? Environment.UserName);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UsunKursAsync(long kursId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Najpierw usuń ładunki
            var sqlLadunki = "DELETE FROM dbo.Ladunek WHERE KursID = @KursID";
            using var cmdLadunki = new SqlCommand(sqlLadunki, connection);
            cmdLadunki.Parameters.AddWithValue("@KursID", kursId);
            await cmdLadunki.ExecuteNonQueryAsync();

            // Potem usuń kurs
            var sql = "DELETE FROM dbo.Kurs WHERE KursID = @KursID";
            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@KursID", kursId);
            await cmd.ExecuteNonQueryAsync();
        }

        #endregion

        #region Ładunki

        public async Task<List<Ladunek>> PobierzLadunkiAsync(long kursId)
        {
            var ladunki = new List<Ladunek>();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT LadunekID, KursID, Kolejnosc, KodKlienta, PojemnikiE2, 
                              PaletyH1, PlanE2NaPaleteOverride, Uwagi, UtworzonoUTC
                       FROM dbo.Ladunek 
                       WHERE KursID = @KursID
                       ORDER BY Kolejnosc";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@KursID", kursId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                ladunki.Add(new Ladunek
                {
                    LadunekID = reader.GetInt64(0),
                    KursID = reader.GetInt64(1),
                    Kolejnosc = reader.GetInt32(2),
                    KodKlienta = reader.IsDBNull(3) ? null : reader.GetString(3),
                    PojemnikiE2 = reader.GetInt32(4),
                    PaletyH1 = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    PlanE2NaPaleteOverride = reader.IsDBNull(6) ? null : reader.GetByte(6),
                    Uwagi = reader.IsDBNull(7) ? null : reader.GetString(7),
                    UtworzonoUTC = reader.GetDateTime(8)
                });
            }

            return ladunki;
        }

        public async Task<long> DodajLadunekAsync(Ladunek ladunek)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Znajdź następną kolejność
            var sqlMaxKolejnosc = "SELECT ISNULL(MAX(Kolejnosc), 0) + 1 FROM dbo.Ladunek WHERE KursID = @KursID";
            using var cmdMax = new SqlCommand(sqlMaxKolejnosc, connection);
            cmdMax.Parameters.AddWithValue("@KursID", ladunek.KursID);
            var nowaKolejnosc = (int)await cmdMax.ExecuteScalarAsync();

            var sql = @"INSERT INTO dbo.Ladunek 
                       (KursID, Kolejnosc, KodKlienta, PojemnikiE2, PaletyH1, PlanE2NaPaleteOverride, Uwagi) 
                       OUTPUT INSERTED.LadunekID
                       VALUES (@KursID, @Kolejnosc, @KodKlienta, @PojemnikiE2, @PaletyH1, 
                               @PlanE2NaPaleteOverride, @Uwagi)";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@KursID", ladunek.KursID);
            cmd.Parameters.AddWithValue("@Kolejnosc", nowaKolejnosc);
            cmd.Parameters.AddWithValue("@KodKlienta", (object)ladunek.KodKlienta ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PojemnikiE2", ladunek.PojemnikiE2);
            cmd.Parameters.AddWithValue("@PaletyH1", (object)ladunek.PaletyH1 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PlanE2NaPaleteOverride", (object)ladunek.PlanE2NaPaleteOverride ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Uwagi", (object)ladunek.Uwagi ?? DBNull.Value);

            return (long)await cmd.ExecuteScalarAsync();
        }

        public async Task AktualizujLadunekAsync(Ladunek ladunek)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"UPDATE dbo.Ladunek 
                       SET Kolejnosc = @Kolejnosc, KodKlienta = @KodKlienta, 
                           PojemnikiE2 = @PojemnikiE2, PaletyH1 = @PaletyH1,
                           PlanE2NaPaleteOverride = @PlanE2NaPaleteOverride, Uwagi = @Uwagi
                       WHERE LadunekID = @LadunekID";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@LadunekID", ladunek.LadunekID);
            cmd.Parameters.AddWithValue("@Kolejnosc", ladunek.Kolejnosc);
            cmd.Parameters.AddWithValue("@KodKlienta", (object)ladunek.KodKlienta ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PojemnikiE2", ladunek.PojemnikiE2);
            cmd.Parameters.AddWithValue("@PaletyH1", (object)ladunek.PaletyH1 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PlanE2NaPaleteOverride", (object)ladunek.PlanE2NaPaleteOverride ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Uwagi", (object)ladunek.Uwagi ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UsunLadunekAsync(long ladunekId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "DELETE FROM dbo.Ladunek WHERE LadunekID = @LadunekID";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@LadunekID", ladunekId);

            await cmd.ExecuteNonQueryAsync();
        }

        #endregion

        #region Pakowanie

        public async Task<WynikPakowania> ObliczPakowanieKursuAsync(long kursId)
        {
            // Pobierz dane kursu
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sqlKurs = @"SELECT p.PaletyH1, k.PlanE2NaPalete 
                           FROM dbo.Kurs k
                           JOIN dbo.Pojazd p ON k.PojazdID = p.PojazdID
                           WHERE k.KursID = @KursID";

            using var cmdKurs = new SqlCommand(sqlKurs, connection);
            cmdKurs.Parameters.AddWithValue("@KursID", kursId);

            int paletyPojazdu = 33;
            int planE2NaPalete = 36;

            using (var reader = await cmdKurs.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    paletyPojazdu = reader.GetInt32(0);
                    planE2NaPalete = reader.GetByte(1);
                }
            }

            // Pobierz ładunki
            var ladunki = await PobierzLadunkiAsync(kursId);
            var pozycje = ladunki.Select(l => l.ToPozycjaLike()).ToList();

            // Oblicz pakowanie
            return _pakowanieSerwis.ObliczKurs(pozycje, paletyPojazdu, planE2NaPalete);
        }

        #endregion

        #region Integracja z zamówieniami

        public async Task<List<ZamowienieTransport>> PobierzWolneZamowieniaNaDateAsync(DateTime data)
        {
            var zamowienia = new List<ZamowienieTransport>();

            using var connection = new SqlConnection(_libraConnectionString);
            await connection.OpenAsync();

            // Pobierz zamówienia które nie są jeszcze przypisane do żadnego kursu
            var sql = @"
                SELECT 
                    z.Id AS ZamowienieID,
                    z.KlientId AS KlientID,
                    'Klient ' + CAST(z.KlientId AS NVARCHAR(50)) AS KlientNazwa,
                    z.DataZamowienia,
                    SUM(ISNULL(zt.Ilosc, 0)) AS IloscKg,
                    ISNULL(z.Status, 'Nowe') AS Status,
                    '' AS Handlowiec
                FROM dbo.ZamowieniaMieso z
                LEFT JOIN dbo.ZamowieniaMiesoTowar zt ON z.Id = zt.ZamowienieId
                WHERE z.DataZamowienia = @Data
                  AND ISNULL(z.Status, 'Nowe') NOT IN ('Anulowane', 'Zrealizowane')
                GROUP BY z.Id, z.KlientId, z.DataZamowienia, z.Status
                ORDER BY z.Id";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Data", data.Date);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                zamowienia.Add(new ZamowienieTransport
                {
                    ZamowienieID = reader.GetInt32(0),
                    KlientID = reader.GetInt32(1),
                    KlientNazwa = reader.GetString(2),
                    DataZamowienia = reader.GetDateTime(3),
                    IloscKg = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                    Status = reader.GetString(5),
                    Handlowiec = reader.GetString(6)
                });
            }

            return zamowienia;
        }

        public async Task<bool> DodajZamowienieDoKursuAsync(long kursId, int zamowienieId)
        {
            try
            {
                // Pobierz dane zamówienia
                using var connectionLibra = new SqlConnection(_libraConnectionString);
                await connectionLibra.OpenAsync();

                var sqlZamowienie = @"
                    SELECT 
                        z.Id,
                        z.KlientId,
                        'Klient ' + CAST(z.KlientId AS NVARCHAR(50)) AS KlientNazwa,
                        SUM(ISNULL(zt.Ilosc, 0)) AS IloscKg
                    FROM dbo.ZamowieniaMieso z
                    LEFT JOIN dbo.ZamowieniaMiesoTowar zt ON z.Id = zt.ZamowienieId
                    WHERE z.Id = @ZamowienieID
                    GROUP BY z.Id, z.KlientId";

                using var cmdZamowienie = new SqlCommand(sqlZamowienie, connectionLibra);
                cmdZamowienie.Parameters.AddWithValue("@ZamowienieID", zamowienieId);

                string kodKlienta = "";
                int pojemnikiE2 = 0;
                string uwagi = "";

                using (var reader = await cmdZamowienie.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        var klientNazwa = reader.GetString(2);
                        var iloscKg = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3);

                        kodKlienta = zamowienieId.ToString();
                        pojemnikiE2 = (int)Math.Ceiling(iloscKg / 15.0m);
                        uwagi = $"{klientNazwa} - Zam.{zamowienieId}";
                    }
                }

                // Dodaj jako ładunek
                var ladunek = new Ladunek
                {
                    KursID = kursId,
                    KodKlienta = kodKlienta,
                    PojemnikiE2 = pojemnikiE2,
                    Uwagi = uwagi
                };

                await DodajLadunekAsync(ladunek);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Pomocnicze

        public async Task<(List<Kurs> kursy, Dictionary<long, WynikPakowania> wypelnienia)> PobierzKursyZWypelnieniemAsync(DateTime data)
        {
            var kursy = await PobierzKursyPoDacieAsync(data);
            var wypelnienia = new Dictionary<long, WynikPakowania>();

            foreach (var kurs in kursy)
            {
                var wynik = await ObliczPakowanieKursuAsync(kurs.KursID);
                wypelnienia[kurs.KursID] = wynik;
            }

            return (kursy, wypelnienia);
        }

        #endregion
    }
}