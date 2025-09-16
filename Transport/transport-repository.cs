// Plik: /Repozytorium/TransportRepozytorium.cs
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

        /// <summary>
        /// Pobiera pojedynczy kurs po ID z pełną obsługą błędów
        /// </summary>
        public async Task<Kurs> PobierzKursAsync(long kursId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Sprawdź czy kurs istnieje
                var sqlCheck = "SELECT COUNT(*) FROM dbo.Kurs WHERE KursID = @KursID";
                using (var cmdCheck = new SqlCommand(sqlCheck, connection))
                {
                    cmdCheck.Parameters.AddWithValue("@KursID", kursId);
                    var count = (int)await cmdCheck.ExecuteScalarAsync();

                    if (count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Kurs o ID {kursId} nie istnieje w bazie danych");
                        return null;
                    }
                }

                // Pobierz dane kursu
                var sql = @"
            SELECT 
                k.KursID, 
                k.DataKursu, 
                ISNULL(k.KierowcaID, 0) AS KierowcaID, 
                ISNULL(k.PojazdID, 0) AS PojazdID, 
                k.Trasa,
                k.GodzWyjazdu, 
                k.GodzPowrotu, 
                ISNULL(k.Status, 'Planowany') AS Status, 
                ISNULL(k.PlanE2NaPalete, 36) AS PlanE2NaPalete,
                k.UtworzonoUTC, 
                k.Utworzyl, 
                k.ZmienionoUTC, 
                k.Zmienil,
                ISNULL(CONCAT(ki.Imie, ' ', ki.Nazwisko), 'Brak kierowcy') AS KierowcaNazwa,
                ISNULL(p.Rejestracja, 'Brak pojazdu') AS Rejestracja,
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
                        GodzWyjazdu = reader.IsDBNull(5) ? null : (TimeSpan?)reader.GetTimeSpan(5),
                        GodzPowrotu = reader.IsDBNull(6) ? null : (TimeSpan?)reader.GetTimeSpan(6),
                        Status = reader.GetString(7),
                        PlanE2NaPalete = SafeConvert<byte>(reader.GetValue(8), 36), // UŻYJ SafeConvert
                        UtworzonoUTC = reader.GetDateTime(9),
                        Utworzyl = reader.IsDBNull(10) ? null : reader.GetString(10),
                        ZmienionoUTC = reader.IsDBNull(11) ? null : (DateTime?)reader.GetDateTime(11),
                        Zmienil = reader.IsDBNull(12) ? null : reader.GetString(12),
                        KierowcaNazwa = reader.GetString(13),
                        PojazdRejestracja = reader.GetString(14),
                        PaletyPojazdu = reader.GetInt32(15),
                        SumaE2 = 0,
                        PaletyNominal = 0,
                        PaletyMax = 0,
                        ProcNominal = 0,
                        ProcMax = 0
                    };

                    // Oblicz wypełnienie
                    try
                    {
                        var sqlLadunki = @"
                    SELECT ISNULL(SUM(PojemnikiE2), 0) 
                    FROM dbo.Ladunek 
                    WHERE KursID = @KursID";

                        using (var cmdLadunki = new SqlCommand(sqlLadunki, connection))
                        {
                            cmdLadunki.Parameters.AddWithValue("@KursID", kursId);
                            var sumaE2 = Convert.ToInt32(await cmdLadunki.ExecuteScalarAsync());

                            kurs.SumaE2 = sumaE2;

                            if (kurs.PaletyPojazdu > 0 && kurs.PlanE2NaPalete > 0)
                            {
                                kurs.PaletyNominal = (int)Math.Ceiling((double)sumaE2 / kurs.PlanE2NaPalete);
                                kurs.PaletyMax = (int)Math.Ceiling((double)sumaE2 / 40);

                                kurs.ProcNominal = Math.Round(100.0m * kurs.PaletyNominal / kurs.PaletyPojazdu, 2);
                                kurs.ProcMax = Math.Round(100.0m * kurs.PaletyMax / kurs.PaletyPojazdu, 2);
                            }
                        }
                    }
                    catch (Exception exCalc)
                    {
                        System.Diagnostics.Debug.WriteLine($"Błąd obliczania wypełnienia: {exCalc.Message}");
                    }

                    return kurs;
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd przy pobieraniu kursu {kursId}: {ex.Message}");
                throw new Exception($"Błąd podczas pobierania kursu: {ex.Message}", ex);
            }
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
            ISNULL(v.PaletyPojazdu, p.PaletyH1) AS PaletyPojazdu,
            ISNULL(v.SumaE2, 0) AS SumaE2,
            ISNULL(v.PaletyNominal, 0) AS PaletyNominal,
            ISNULL(v.PaletyMax, 0) AS PaletyMax,
            ISNULL(v.ProcNominal, 0) AS ProcNominal,
            ISNULL(v.ProcMax, 0) AS ProcMax
        FROM dbo.Kurs k
        JOIN dbo.Kierowca ki ON k.KierowcaID = ki.KierowcaID
        JOIN dbo.Pojazd p ON k.PojazdID = p.PojazdID
        LEFT JOIN dbo.vKursWypelnienie v ON k.KursID = v.KursID
        WHERE k.DataKursu = @Data
        ORDER BY k.GodzWyjazdu, k.KursID";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Data", data.Date);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                kursy.Add(new Kurs
                {
                    KursID = reader.GetInt64(0),
                    DataKursu = reader.GetDateTime(1),
                    KierowcaID = reader.GetInt32(2),
                    PojazdID = reader.GetInt32(3),
                    Trasa = reader.IsDBNull(4) ? null : reader.GetString(4),
                    GodzWyjazdu = reader.IsDBNull(5) ? null : reader.GetTimeSpan(5),
                    GodzPowrotu = reader.IsDBNull(6) ? null : reader.GetTimeSpan(6),
                    Status = reader.GetString(7),
                    PlanE2NaPalete = SafeConvert<byte>(reader.GetValue(8), 36),  // ZMIENIONE
                    UtworzonoUTC = reader.GetDateTime(9),
                    Utworzyl = reader.IsDBNull(10) ? null : reader.GetString(10),
                    ZmienionoUTC = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                    Zmienil = reader.IsDBNull(12) ? null : reader.GetString(12),
                    KierowcaNazwa = reader.GetString(13),
                    PojazdRejestracja = reader.GetString(14),
                    PaletyPojazdu = SafeConvert<int>(reader.GetValue(15), 0),  // ZMIENIONE
                    SumaE2 = SafeConvert<int>(reader.GetValue(16), 0),  // ZMIENIONE
                    PaletyNominal = SafeConvert<int>(reader.GetValue(17), 0),  // ZMIENIONE
                    PaletyMax = SafeConvert<int>(reader.GetValue(18), 0),  // ZMIENIONE
                    ProcNominal = SafeConvert<decimal>(reader.GetValue(19), 0),  // ZMIENIONE
                    ProcMax = SafeConvert<decimal>(reader.GetValue(20), 0)  // ZMIENIONE
                });
            }

            return kursy;
        }
        private static T SafeConvert<T>(object value, T defaultValue = default)
        {
            if (value == null || value == DBNull.Value)
                return defaultValue;

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
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

        public async Task RenumerujLadunkiAsync(long kursId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                WITH CTE AS (
                    SELECT LadunekID, 
                           ROW_NUMBER() OVER (ORDER BY Kolejnosc, LadunekID) AS NowaKolejnosc
                    FROM dbo.Ladunek
                    WHERE KursID = @KursID
                )
                UPDATE l
                SET l.Kolejnosc = c.NowaKolejnosc
                FROM dbo.Ladunek l
                JOIN CTE c ON l.LadunekID = c.LadunekID";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@KursID", kursId);

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

            int paletyPojazdu = 0;
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

        #region Pomocnicze

        public async Task<bool> SprawdzPolaczenieAsync()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var cmd = new SqlCommand("SELECT 1", connection);
                await cmd.ExecuteScalarAsync();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd połączenia z bazą: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SprawdzCzyTabelaIstniejeAsync(string nazwaTabeli)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = @"
                    SELECT COUNT(*) 
                    FROM INFORMATION_SCHEMA.TABLES 
                    WHERE TABLE_SCHEMA = 'dbo' 
                      AND TABLE_NAME = @NazwaTabeli";

                using var cmd = new SqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@NazwaTabeli", nazwaTabeli);

                var count = (int)await cmd.ExecuteScalarAsync();
                return count > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> SprawdzStanBazyAsync()
        {
            var result = new System.Text.StringBuilder();

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                result.AppendLine("✓ Połączenie z bazą danych OK");

                // Sprawdź tabele
                var tabele = new[] { "Kierowca", "Pojazd", "Kurs", "Ladunek" };
                foreach (var tabela in tabele)
                {
                    if (await SprawdzCzyTabelaIstniejeAsync(tabela))
                    {
                        var sql = $"SELECT COUNT(*) FROM dbo.{tabela}";
                        using var cmd = new SqlCommand(sql, connection);
                        var count = (int)await cmd.ExecuteScalarAsync();
                        result.AppendLine($"✓ Tabela {tabela}: {count} rekordów");
                    }
                    else
                    {
                        result.AppendLine($"✗ Brak tabeli {tabela}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.AppendLine($"✗ Błąd: {ex.Message}");
            }

            return result.ToString();
        }

        #endregion
    }
}