using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Kalendarz1.Spotkania.Models;
using Kalendarz1.NotatkiZeSpotkan;

namespace Kalendarz1.Spotkania.Services
{
    /// <summary>
    /// Serwis do zarządzania spotkaniami
    /// </summary>
    public class SpotkaniaService
    {
        private const string CONNECTION_STRING = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova";
        private const string HANDEL_CONNECTION_STRING = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=s3cretPassw0rd!;TrustServerCertificate=True";

        private readonly NotyfikacjeService? _notyfikacje;

        public SpotkaniaService(NotyfikacjeService? notyfikacjeService = null)
        {
            _notyfikacje = notyfikacjeService;
        }

        #region CRUD - Spotkania

        /// <summary>
        /// Tworzy nowe spotkanie
        /// </summary>
        public async Task<long> UtworzSpotkanie(SpotkanieModel spotkanie)
        {
            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // 1. Wstaw spotkanie
                string sql = @"
                    INSERT INTO Spotkania
                        (Tytul, Opis, DataSpotkania, DataZakonczenia, CzasTrwaniaMin, TypSpotkania,
                         Lokalizacja, Status, OrganizatorID, OrganizatorNazwa, KontrahentID,
                         KontrahentNazwa, KontrahentTyp, LinkSpotkania, Priorytet, Kategoria, Kolor,
                         PrzypomnienieMinuty, NotatkaID, DataUtworzenia)
                    OUTPUT INSERTED.SpotkaniID
                    VALUES
                        (@Tytul, @Opis, @DataSpotkania, @DataZakonczenia, @CzasTrwania, @Typ,
                         @Lokalizacja, @Status, @OrganizatorID, @OrganizatorNazwa, @KontrahentID,
                         @KontrahentNazwa, @KontrahentTyp, @Link, @Priorytet, @Kategoria, @Kolor,
                         @Przypomnienia, @NotatkaID, GETDATE())";

                using var cmd = new SqlCommand(sql, conn, transaction);
                cmd.Parameters.AddWithValue("@Tytul", spotkanie.Tytul);
                cmd.Parameters.AddWithValue("@Opis", (object?)spotkanie.Opis ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DataSpotkania", spotkanie.DataSpotkania);
                cmd.Parameters.AddWithValue("@DataZakonczenia", (object?)spotkanie.DataZakonczenia ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CzasTrwania", spotkanie.CzasTrwaniaMin);
                cmd.Parameters.AddWithValue("@Typ", spotkanie.TypSpotkaniaDisplay);
                cmd.Parameters.AddWithValue("@Lokalizacja", (object?)spotkanie.Lokalizacja ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Status", spotkanie.StatusDisplay);
                cmd.Parameters.AddWithValue("@OrganizatorID", spotkanie.OrganizatorID);
                cmd.Parameters.AddWithValue("@OrganizatorNazwa", (object?)spotkanie.OrganizatorNazwa ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@KontrahentID", (object?)spotkanie.KontrahentID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@KontrahentNazwa", (object?)spotkanie.KontrahentNazwa ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@KontrahentTyp", (object?)spotkanie.KontrahentTyp ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Link", (object?)spotkanie.LinkSpotkania ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Priorytet", spotkanie.PriorytetDisplay);
                cmd.Parameters.AddWithValue("@Kategoria", (object?)spotkanie.Kategoria ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Kolor", spotkanie.Kolor);
                cmd.Parameters.AddWithValue("@Przypomnienia", string.Join(",", spotkanie.PrzypomnienieMinuty));
                cmd.Parameters.AddWithValue("@NotatkaID", (object?)spotkanie.NotatkaID ?? DBNull.Value);

                var spotkaniId = (long)await cmd.ExecuteScalarAsync();
                spotkanie.SpotkaniID = spotkaniId;

                // 2. Dodaj uczestników
                foreach (var u in spotkanie.Uczestnicy)
                {
                    await DodajUczestnika(conn, transaction, spotkaniId, u);
                }

                transaction.Commit();

                // 3. Wyślij zaproszenia (asynchronicznie, bez blokowania)
                if (_notyfikacje != null && spotkanie.Uczestnicy.Count > 0)
                {
                    var uczestnicyIds = spotkanie.Uczestnicy
                        .Where(u => u.OperatorID != spotkanie.OrganizatorID)
                        .Select(u => u.OperatorID)
                        .ToList();

                    _ = _notyfikacje.UtworzZaproszenie(
                        spotkaniId,
                        spotkanie.Tytul,
                        spotkanie.DataSpotkania,
                        uczestnicyIds,
                        spotkanie.OrganizatorNazwa ?? "Nieznany");
                }

                return spotkaniId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Aktualizuje spotkanie
        /// </summary>
        public async Task AktualizujSpotkanie(SpotkanieModel spotkanie, bool powiadomUczestnikow = true)
        {
            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // 1. Aktualizuj spotkanie
                string sql = @"
                    UPDATE Spotkania SET
                        Tytul = @Tytul,
                        Opis = @Opis,
                        DataSpotkania = @DataSpotkania,
                        DataZakonczenia = @DataZakonczenia,
                        CzasTrwaniaMin = @CzasTrwania,
                        TypSpotkania = @Typ,
                        Lokalizacja = @Lokalizacja,
                        Status = @Status,
                        KontrahentID = @KontrahentID,
                        KontrahentNazwa = @KontrahentNazwa,
                        KontrahentTyp = @KontrahentTyp,
                        LinkSpotkania = @Link,
                        Priorytet = @Priorytet,
                        Kategoria = @Kategoria,
                        Kolor = @Kolor,
                        PrzypomnienieMinuty = @Przypomnienia,
                        NotatkaID = @NotatkaID,
                        DataModyfikacji = GETDATE()
                    WHERE SpotkaniID = @ID";

                using var cmd = new SqlCommand(sql, conn, transaction);
                cmd.Parameters.AddWithValue("@ID", spotkanie.SpotkaniID);
                cmd.Parameters.AddWithValue("@Tytul", spotkanie.Tytul);
                cmd.Parameters.AddWithValue("@Opis", (object?)spotkanie.Opis ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DataSpotkania", spotkanie.DataSpotkania);
                cmd.Parameters.AddWithValue("@DataZakonczenia", (object?)spotkanie.DataZakonczenia ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CzasTrwania", spotkanie.CzasTrwaniaMin);
                cmd.Parameters.AddWithValue("@Typ", spotkanie.TypSpotkaniaDisplay);
                cmd.Parameters.AddWithValue("@Lokalizacja", (object?)spotkanie.Lokalizacja ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Status", spotkanie.StatusDisplay);
                cmd.Parameters.AddWithValue("@KontrahentID", (object?)spotkanie.KontrahentID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@KontrahentNazwa", (object?)spotkanie.KontrahentNazwa ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@KontrahentTyp", (object?)spotkanie.KontrahentTyp ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Link", (object?)spotkanie.LinkSpotkania ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Priorytet", spotkanie.PriorytetDisplay);
                cmd.Parameters.AddWithValue("@Kategoria", (object?)spotkanie.Kategoria ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Kolor", spotkanie.Kolor);
                cmd.Parameters.AddWithValue("@Przypomnienia", string.Join(",", spotkanie.PrzypomnienieMinuty));
                cmd.Parameters.AddWithValue("@NotatkaID", (object?)spotkanie.NotatkaID ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync();

                // 2. Aktualizuj uczestników (usuń i dodaj na nowo)
                string deleteSql = "DELETE FROM SpotkaniaUczestnicy WHERE SpotkaniID = @ID";
                using var deleteCmd = new SqlCommand(deleteSql, conn, transaction);
                deleteCmd.Parameters.AddWithValue("@ID", spotkanie.SpotkaniID);
                await deleteCmd.ExecuteNonQueryAsync();

                foreach (var u in spotkanie.Uczestnicy)
                {
                    await DodajUczestnika(conn, transaction, spotkanie.SpotkaniID, u);
                }

                transaction.Commit();

                // 3. Wyślij powiadomienia o zmianie
                if (_notyfikacje != null && powiadomUczestnikow && spotkanie.Uczestnicy.Count > 0)
                {
                    var uczestnicyIds = spotkanie.Uczestnicy.Select(u => u.OperatorID).ToList();
                    _ = _notyfikacje.UtworzPowiadomienieZmiany(
                        spotkanie.SpotkaniID,
                        spotkanie.Tytul,
                        spotkanie.DataSpotkania,
                        uczestnicyIds,
                        $"Spotkanie \"{spotkanie.Tytul}\" zostało zaktualizowane.");
                }
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Anuluje spotkanie
        /// </summary>
        public async Task AnulujSpotkanie(long spotkaniId, string powod = "")
        {
            // Pobierz dane spotkania przed anulowaniem
            var spotkanie = await PobierzSpotkanie(spotkaniId);
            if (spotkanie == null) return;

            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            string sql = "UPDATE Spotkania SET Status = 'Anulowane', DataModyfikacji = GETDATE() WHERE SpotkaniID = @ID";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", spotkaniId);
            await cmd.ExecuteNonQueryAsync();

            // Wyślij powiadomienia
            if (_notyfikacje != null && spotkanie.Uczestnicy.Count > 0)
            {
                var uczestnicyIds = spotkanie.Uczestnicy.Select(u => u.OperatorID).ToList();
                _ = _notyfikacje.UtworzPowiadomienieAnulowania(
                    spotkaniId,
                    spotkanie.Tytul,
                    spotkanie.DataSpotkania,
                    uczestnicyIds,
                    powod);
            }
        }

        /// <summary>
        /// Usuwa spotkanie (trwale)
        /// </summary>
        public async Task UsunSpotkanie(long spotkaniId)
        {
            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            // Kaskadowe usunięcie dzięki ON DELETE CASCADE
            string sql = "DELETE FROM Spotkania WHERE SpotkaniID = @ID";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", spotkaniId);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Pobiera szczegóły spotkania
        /// </summary>
        public async Task<SpotkanieModel?> PobierzSpotkanie(long spotkaniId)
        {
            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            string sql = "SELECT * FROM Spotkania WHERE SpotkaniID = @ID";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", spotkaniId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            var spotkanie = MapujSpotkanie(reader);
            await reader.CloseAsync();

            // Pobierz uczestników
            spotkanie.Uczestnicy = await PobierzUczestnikow(conn, spotkaniId);

            return spotkanie;
        }

        #endregion

        #region Listy i wyszukiwanie

        /// <summary>
        /// Pobiera listę spotkań z filtrami
        /// </summary>
        public async Task<List<SpotkanieListItem>> PobierzListeSpotkań(
            string? operatorId = null,
            DateTime? odDaty = null,
            DateTime? doDaty = null,
            string? typSpotkania = null,
            string? status = null,
            bool tylkoMojeSpotkania = false)
        {
            var lista = new List<SpotkanieListItem>();

            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            var sql = new System.Text.StringBuilder(@"
                SELECT DISTINCT
                    s.SpotkaniID, s.Tytul, s.DataSpotkania, s.CzasTrwaniaMin,
                    s.TypSpotkania, s.Status, s.OrganizatorNazwa, s.KontrahentNazwa,
                    s.Lokalizacja, s.Priorytet, s.Kolor, s.NotatkaID,
                    s.FirefliesTranscriptID, s.LinkSpotkania,
                    (SELECT COUNT(*) FROM SpotkaniaUczestnicy u WHERE u.SpotkaniID = s.SpotkaniID) AS LiczbaUczestnikow,
                    (SELECT COUNT(*) FROM SpotkaniaUczestnicy u WHERE u.SpotkaniID = s.SpotkaniID AND u.StatusZaproszenia = 'Zaakceptowane') AS LiczbaPotwierdzonych
                FROM Spotkania s
                LEFT JOIN SpotkaniaUczestnicy su ON s.SpotkaniID = su.SpotkaniID
                WHERE 1=1");

            if (!string.IsNullOrEmpty(operatorId) && tylkoMojeSpotkania)
            {
                sql.Append(" AND (s.OrganizatorID = @OperatorID OR su.OperatorID = @OperatorID)");
            }
            if (odDaty.HasValue)
                sql.Append(" AND s.DataSpotkania >= @OdDaty");
            if (doDaty.HasValue)
                sql.Append(" AND s.DataSpotkania <= @DoDaty");
            if (!string.IsNullOrEmpty(typSpotkania))
                sql.Append(" AND s.TypSpotkania = @Typ");
            if (!string.IsNullOrEmpty(status))
                sql.Append(" AND s.Status = @Status");

            sql.Append(" ORDER BY s.DataSpotkania DESC");

            using var cmd = new SqlCommand(sql.ToString(), conn);
            if (!string.IsNullOrEmpty(operatorId))
                cmd.Parameters.AddWithValue("@OperatorID", operatorId);
            if (odDaty.HasValue)
                cmd.Parameters.AddWithValue("@OdDaty", odDaty.Value);
            if (doDaty.HasValue)
                cmd.Parameters.AddWithValue("@DoDaty", doDaty.Value);
            if (!string.IsNullOrEmpty(typSpotkania))
                cmd.Parameters.AddWithValue("@Typ", typSpotkania);
            if (!string.IsNullOrEmpty(status))
                cmd.Parameters.AddWithValue("@Status", status);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new SpotkanieListItem
                {
                    SpotkaniID = reader.GetInt64(0),
                    Tytul = reader.GetString(1),
                    DataSpotkania = reader.GetDateTime(2),
                    CzasTrwaniaMin = reader.GetInt32(3),
                    TypSpotkania = reader.GetString(4),
                    Status = reader.GetString(5),
                    OrganizatorNazwa = reader.IsDBNull(6) ? null : reader.GetString(6),
                    KontrahentNazwa = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Lokalizacja = reader.IsDBNull(8) ? null : reader.GetString(8),
                    Priorytet = reader.GetString(9),
                    Kolor = reader.GetString(10),
                    MaNotatke = !reader.IsDBNull(11),
                    MaTranskrypcje = !reader.IsDBNull(12),
                    MaLink = !reader.IsDBNull(13),
                    LiczbaUczestnikow = reader.GetInt32(14),
                    LiczbaPotwierdzonych = reader.GetInt32(15)
                });
            }

            return lista;
        }

        /// <summary>
        /// Pobiera nadchodzące spotkania dla użytkownika
        /// </summary>
        public async Task<List<SpotkanieListItem>> PobierzNadchodzaceSpotkania(string operatorId, int dni = 7)
        {
            return await PobierzListeSpotkań(
                operatorId: operatorId,
                odDaty: DateTime.Now,
                doDaty: DateTime.Now.AddDays(dni),
                status: "Zaplanowane",
                tylkoMojeSpotkania: true);
        }

        /// <summary>
        /// Pobiera spotkania na dany dzień (do kalendarza)
        /// </summary>
        public async Task<List<SpotkanieKalendarzItem>> PobierzSpotkaniaKalendarz(
            DateTime dataPoczatek, DateTime dataKoniec, string? operatorId = null)
        {
            var lista = new List<SpotkanieKalendarzItem>();

            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            var sql = @"
                SELECT DISTINCT
                    s.SpotkaniID, s.Tytul, s.DataSpotkania, s.DataZakonczenia,
                    s.Kolor, s.Status, s.Priorytet, s.Lokalizacja, s.LinkSpotkania
                FROM Spotkania s
                LEFT JOIN SpotkaniaUczestnicy su ON s.SpotkaniID = su.SpotkaniID
                WHERE s.DataSpotkania BETWEEN @Start AND @End
                  AND s.Status != 'Anulowane'";

            if (!string.IsNullOrEmpty(operatorId))
            {
                sql += " AND (s.OrganizatorID = @OperatorID OR su.OperatorID = @OperatorID)";
            }

            sql += " ORDER BY s.DataSpotkania";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Start", dataPoczatek);
            cmd.Parameters.AddWithValue("@End", dataKoniec);
            if (!string.IsNullOrEmpty(operatorId))
                cmd.Parameters.AddWithValue("@OperatorID", operatorId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new SpotkanieKalendarzItem
                {
                    SpotkaniID = reader.GetInt64(0),
                    Tytul = reader.GetString(1),
                    DataSpotkania = reader.GetDateTime(2),
                    DataZakonczenia = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                    Kolor = reader.GetString(4),
                    Status = reader.GetString(5),
                    Priorytet = reader.GetString(6),
                    Lokalizacja = reader.IsDBNull(7) ? null : reader.GetString(7),
                    MaLink = !reader.IsDBNull(8)
                });
            }

            return lista;
        }

        #endregion

        #region Uczestnicy

        /// <summary>
        /// Dodaje uczestnika do spotkania
        /// </summary>
        private async Task DodajUczestnika(SqlConnection conn, SqlTransaction transaction,
            long spotkaniId, UczestnikSpotkaniaModel uczestnik)
        {
            string sql = @"
                INSERT INTO SpotkaniaUczestnicy
                    (SpotkaniID, OperatorID, OperatorNazwa, StatusZaproszenia, CzyObowiazkowy)
                VALUES
                    (@SpotkaniID, @OperatorID, @OperatorNazwa, @Status, @Obowiazkowy)";

            using var cmd = new SqlCommand(sql, conn, transaction);
            cmd.Parameters.AddWithValue("@SpotkaniID", spotkaniId);
            cmd.Parameters.AddWithValue("@OperatorID", uczestnik.OperatorID);
            cmd.Parameters.AddWithValue("@OperatorNazwa", (object?)uczestnik.OperatorNazwa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", uczestnik.StatusDisplay);
            cmd.Parameters.AddWithValue("@Obowiazkowy", uczestnik.CzyObowiazkowy);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Pobiera uczestników spotkania
        /// </summary>
        private async Task<List<UczestnikSpotkaniaModel>> PobierzUczestnikow(SqlConnection conn, long spotkaniId)
        {
            var lista = new List<UczestnikSpotkaniaModel>();

            string sql = "SELECT * FROM SpotkaniaUczestnicy WHERE SpotkaniID = @ID";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", spotkaniId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new UczestnikSpotkaniaModel
                {
                    SpotkaniID = reader.GetInt64(reader.GetOrdinal("SpotkaniID")),
                    OperatorID = reader.GetString(reader.GetOrdinal("OperatorID")),
                    OperatorNazwa = reader.IsDBNull(reader.GetOrdinal("OperatorNazwa")) ? null : reader.GetString(reader.GetOrdinal("OperatorNazwa")),
                    StatusZaproszenia = ParseStatusZaproszenia(reader.GetString(reader.GetOrdinal("StatusZaproszenia"))),
                    CzyObowiazkowy = reader.GetBoolean(reader.GetOrdinal("CzyObowiazkowy")),
                    CzyPowiadomiony = reader.GetBoolean(reader.GetOrdinal("CzyPowiadomiony"))
                });
            }

            return lista;
        }

        /// <summary>
        /// Aktualizuje status zaproszenia uczestnika
        /// </summary>
        public async Task AktualizujStatusUczestnika(long spotkaniId, string operatorId, StatusZaproszenia status)
        {
            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            string sql = @"UPDATE SpotkaniaUczestnicy
                SET StatusZaproszenia = @Status
                WHERE SpotkaniID = @SpotkaniID AND OperatorID = @OperatorID";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@SpotkaniID", spotkaniId);
            cmd.Parameters.AddWithValue("@OperatorID", operatorId);
            cmd.Parameters.AddWithValue("@Status", status.ToString());

            await cmd.ExecuteNonQueryAsync();

            // Powiadom organizatora
            if (_notyfikacje != null)
            {
                var spotkanie = await PobierzSpotkanie(spotkaniId);
                if (spotkanie != null && spotkanie.OrganizatorID != operatorId)
                {
                    var uczestnik = spotkanie.Uczestnicy.FirstOrDefault(u => u.OperatorID == operatorId);
                    var typNotyfikacji = status == StatusZaproszenia.Zaakceptowane
                        ? "AkceptacjaZaproszenia"
                        : "OdrzucenieZaproszenia";

                    // Utwórz powiadomienie dla organizatora
                    using var conn2 = new SqlConnection(CONNECTION_STRING);
                    await conn2.OpenAsync();

                    string sqlNotif = @"
                        INSERT INTO SpotkaniaNotyfikacje
                            (SpotkaniID, OperatorID, TypNotyfikacji, Tytul, Tresc,
                             SpotkanieDataSpotkania, SpotkanieTytul)
                        VALUES
                            (@SpotkaniID, @OperatorID, @Typ, @Tytul, @Tresc,
                             @DataSpotkania, @SpotkanieTytul)";

                    using var cmdNotif = new SqlCommand(sqlNotif, conn2);
                    cmdNotif.Parameters.AddWithValue("@SpotkaniID", spotkaniId);
                    cmdNotif.Parameters.AddWithValue("@OperatorID", spotkanie.OrganizatorID);
                    cmdNotif.Parameters.AddWithValue("@Typ", typNotyfikacji);
                    cmdNotif.Parameters.AddWithValue("@Tytul",
                        status == StatusZaproszenia.Zaakceptowane
                            ? $"{uczestnik?.OperatorNazwa ?? operatorId} zaakceptował zaproszenie"
                            : $"{uczestnik?.OperatorNazwa ?? operatorId} odrzucił zaproszenie");
                    cmdNotif.Parameters.AddWithValue("@Tresc",
                        $"{uczestnik?.OperatorNazwa ?? operatorId} " +
                        (status == StatusZaproszenia.Zaakceptowane ? "potwierdził" : "odrzucił") +
                        $" uczestnictwo w spotkaniu \"{spotkanie.Tytul}\".");
                    cmdNotif.Parameters.AddWithValue("@DataSpotkania", spotkanie.DataSpotkania);
                    cmdNotif.Parameters.AddWithValue("@SpotkanieTytul", spotkanie.Tytul);

                    await cmdNotif.ExecuteNonQueryAsync();
                }
            }
        }

        #endregion

        #region Operatorzy (pracownicy)

        /// <summary>
        /// Pobiera listę wszystkich operatorów
        /// </summary>
        public async Task<List<OperatorDTO>> PobierzOperatorow()
        {
            var lista = new List<OperatorDTO>();

            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            string sql = "SELECT ID, Name FROM operators ORDER BY Name";
            using var cmd = new SqlCommand(sql, conn);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new OperatorDTO
                {
                    ID = reader.GetString(0),
                    Name = reader.GetString(1)
                });
            }

            return lista;
        }

        /// <summary>
        /// Pobiera nazwę operatora po ID
        /// </summary>
        public async Task<string?> PobierzNazweOperatora(string operatorId)
        {
            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            string sql = "SELECT Name FROM operators WHERE ID = @ID";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", operatorId);

            return await cmd.ExecuteScalarAsync() as string;
        }

        #endregion

        #region Kontrahenci (Odbiorcy i Hodowcy)

        /// <summary>
        /// Pobiera listę odbiorców
        /// </summary>
        public async Task<List<KontrahentDTO>> PobierzOdbiorcow(string? szukaj = null)
        {
            var lista = new List<KontrahentDTO>();

            try
            {
                using var conn = new SqlConnection(HANDEL_CONNECTION_STRING);
                await conn.OpenAsync();

                var sql = @"SELECT TOP 200 ID, Name, Address, Phone1
                    FROM [SSCommon].[STContractors]
                    WHERE Active = 1";

                if (!string.IsNullOrWhiteSpace(szukaj))
                    sql += " AND (Name LIKE @Szukaj OR ID LIKE @Szukaj)";

                sql += " ORDER BY Name";

                using var cmd = new SqlCommand(sql, conn);
                if (!string.IsNullOrWhiteSpace(szukaj))
                    cmd.Parameters.AddWithValue("@Szukaj", $"%{szukaj}%");

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    lista.Add(new KontrahentDTO
                    {
                        ID = reader.GetString(0),
                        Nazwa = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Adres = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Telefon = reader.IsDBNull(3) ? null : reader.GetString(3)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania odbiorców: {ex.Message}");
            }

            return lista;
        }

        /// <summary>
        /// Pobiera listę hodowców
        /// </summary>
        public async Task<List<KontrahentDTO>> PobierzHodowcow(string? szukaj = null)
        {
            var lista = new List<KontrahentDTO>();

            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                var sql = @"SELECT TOP 200 ID, Name, Address, Phone1
                    FROM Dostawcy
                    WHERE Halt = 0";

                if (!string.IsNullOrWhiteSpace(szukaj))
                    sql += " AND (Name LIKE @Szukaj OR ID LIKE @Szukaj)";

                sql += " ORDER BY Name";

                using var cmd = new SqlCommand(sql, conn);
                if (!string.IsNullOrWhiteSpace(szukaj))
                    cmd.Parameters.AddWithValue("@Szukaj", $"%{szukaj}%");

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    lista.Add(new KontrahentDTO
                    {
                        ID = reader.GetString(0),
                        Nazwa = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Adres = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Telefon = reader.IsDBNull(3) ? null : reader.GetString(3)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania hodowców: {ex.Message}");
            }

            return lista;
        }

        #endregion

        #region Integracja z notatkami

        /// <summary>
        /// Tworzy notatkę ze spotkania i powiązuje ją
        /// </summary>
        public async Task<long> UtworzNotatkeZeSpotkania(long spotkaniId, string trescNotatki)
        {
            var spotkanie = await PobierzSpotkanie(spotkaniId);
            if (spotkanie == null)
                throw new InvalidOperationException("Nie znaleziono spotkania");

            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // 1. Utwórz notatkę
                string sql = @"
                    INSERT INTO NotatkiZeSpotkan
                        (TypSpotkania, DataSpotkania, DataUtworzenia, TworcaID, TworcaNazwa,
                         KontrahentID, KontrahentNazwa, KontrahentTyp, Temat, TrescNotatki)
                    OUTPUT INSERTED.NotatkaID
                    VALUES
                        (@Typ, @DataSpotkania, GETDATE(), @TworcaID, @TworcaNazwa,
                         @KontrahentID, @KontrahentNazwa, @KontrahentTyp, @Temat, @Tresc)";

                using var cmd = new SqlCommand(sql, conn, transaction);
                cmd.Parameters.AddWithValue("@Typ", spotkanie.TypSpotkaniaDisplay);
                cmd.Parameters.AddWithValue("@DataSpotkania", spotkanie.DataSpotkania);
                cmd.Parameters.AddWithValue("@TworcaID", spotkanie.OrganizatorID);
                cmd.Parameters.AddWithValue("@TworcaNazwa", (object?)spotkanie.OrganizatorNazwa ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@KontrahentID", (object?)spotkanie.KontrahentID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@KontrahentNazwa", (object?)spotkanie.KontrahentNazwa ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@KontrahentTyp", (object?)spotkanie.KontrahentTyp ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Temat", spotkanie.Tytul);
                cmd.Parameters.AddWithValue("@Tresc", trescNotatki);

                var notatkaId = (long)await cmd.ExecuteScalarAsync();

                // 2. Dodaj uczestników do notatki
                foreach (var u in spotkanie.Uczestnicy)
                {
                    string sqlU = "INSERT INTO NotatkiUczestnicy (NotatkaID, OperatorID) VALUES (@NotatkaID, @OperatorID)";
                    using var cmdU = new SqlCommand(sqlU, conn, transaction);
                    cmdU.Parameters.AddWithValue("@NotatkaID", notatkaId);
                    cmdU.Parameters.AddWithValue("@OperatorID", u.OperatorID);
                    await cmdU.ExecuteNonQueryAsync();
                }

                // 3. Powiąż spotkanie z notatką
                string sqlLink = "UPDATE Spotkania SET NotatkaID = @NotatkaID, DataModyfikacji = GETDATE() WHERE SpotkaniID = @SpotkaniID";
                using var cmdLink = new SqlCommand(sqlLink, conn, transaction);
                cmdLink.Parameters.AddWithValue("@NotatkaID", notatkaId);
                cmdLink.Parameters.AddWithValue("@SpotkaniID", spotkaniId);
                await cmdLink.ExecuteNonQueryAsync();

                transaction.Commit();
                return notatkaId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        #endregion

        #region Pomocnicze

        private SpotkanieModel MapujSpotkanie(SqlDataReader reader)
        {
            var spotkanie = new SpotkanieModel
            {
                SpotkaniID = reader.GetInt64(reader.GetOrdinal("SpotkaniID")),
                Tytul = reader.GetString(reader.GetOrdinal("Tytul")),
                DataSpotkania = reader.GetDateTime(reader.GetOrdinal("DataSpotkania")),
                CzasTrwaniaMin = reader.GetInt32(reader.GetOrdinal("CzasTrwaniaMin")),
                OrganizatorID = reader.GetString(reader.GetOrdinal("OrganizatorID")),
                DataUtworzenia = reader.GetDateTime(reader.GetOrdinal("DataUtworzenia"))
            };

            if (!reader.IsDBNull(reader.GetOrdinal("Opis")))
                spotkanie.Opis = reader.GetString(reader.GetOrdinal("Opis"));

            if (!reader.IsDBNull(reader.GetOrdinal("DataZakonczenia")))
                spotkanie.DataZakonczenia = reader.GetDateTime(reader.GetOrdinal("DataZakonczenia"));

            spotkanie.TypSpotkania = ParseTypSpotkania(reader.GetString(reader.GetOrdinal("TypSpotkania")));

            if (!reader.IsDBNull(reader.GetOrdinal("Lokalizacja")))
                spotkanie.Lokalizacja = reader.GetString(reader.GetOrdinal("Lokalizacja"));

            spotkanie.Status = ParseStatus(reader.GetString(reader.GetOrdinal("Status")));

            if (!reader.IsDBNull(reader.GetOrdinal("OrganizatorNazwa")))
                spotkanie.OrganizatorNazwa = reader.GetString(reader.GetOrdinal("OrganizatorNazwa"));

            if (!reader.IsDBNull(reader.GetOrdinal("KontrahentID")))
                spotkanie.KontrahentID = reader.GetString(reader.GetOrdinal("KontrahentID"));

            if (!reader.IsDBNull(reader.GetOrdinal("KontrahentNazwa")))
                spotkanie.KontrahentNazwa = reader.GetString(reader.GetOrdinal("KontrahentNazwa"));

            if (!reader.IsDBNull(reader.GetOrdinal("KontrahentTyp")))
                spotkanie.KontrahentTyp = reader.GetString(reader.GetOrdinal("KontrahentTyp"));

            if (!reader.IsDBNull(reader.GetOrdinal("LinkSpotkania")))
                spotkanie.LinkSpotkania = reader.GetString(reader.GetOrdinal("LinkSpotkania"));

            if (!reader.IsDBNull(reader.GetOrdinal("FirefliesTranscriptID")))
                spotkanie.FirefliesTranscriptID = reader.GetString(reader.GetOrdinal("FirefliesTranscriptID"));

            if (!reader.IsDBNull(reader.GetOrdinal("NotatkaID")))
                spotkanie.NotatkaID = reader.GetInt64(reader.GetOrdinal("NotatkaID"));

            spotkanie.Priorytet = ParsePriorytet(reader.GetString(reader.GetOrdinal("Priorytet")));

            if (!reader.IsDBNull(reader.GetOrdinal("Kategoria")))
                spotkanie.Kategoria = reader.GetString(reader.GetOrdinal("Kategoria"));

            spotkanie.Kolor = reader.GetString(reader.GetOrdinal("Kolor"));

            if (!reader.IsDBNull(reader.GetOrdinal("PrzypomnienieMinuty")))
            {
                var przypStr = reader.GetString(reader.GetOrdinal("PrzypomnienieMinuty"));
                spotkanie.PrzypomnienieMinuty = przypStr.Split(',')
                    .Where(s => int.TryParse(s, out _))
                    .Select(int.Parse)
                    .ToList();
            }

            if (!reader.IsDBNull(reader.GetOrdinal("DataModyfikacji")))
                spotkanie.DataModyfikacji = reader.GetDateTime(reader.GetOrdinal("DataModyfikacji"));

            return spotkanie;
        }

        private TypSpotkania ParseTypSpotkania(string value) => value switch
        {
            "Zespół" => TypSpotkania.Zespol,
            "Odbiorca" => TypSpotkania.Odbiorca,
            "Hodowca" => TypSpotkania.Hodowca,
            "Online" => TypSpotkania.Online,
            _ => TypSpotkania.Zespol
        };

        private StatusSpotkania ParseStatus(string value) => value switch
        {
            "Zaplanowane" => StatusSpotkania.Zaplanowane,
            "W trakcie" => StatusSpotkania.WTrakcie,
            "Zakończone" => StatusSpotkania.Zakonczone,
            "Anulowane" => StatusSpotkania.Anulowane,
            _ => StatusSpotkania.Zaplanowane
        };

        private PriorytetSpotkania ParsePriorytet(string value) => value switch
        {
            "Niski" => PriorytetSpotkania.Niski,
            "Normalny" => PriorytetSpotkania.Normalny,
            "Wysoki" => PriorytetSpotkania.Wysoki,
            "Pilny" => PriorytetSpotkania.Pilny,
            _ => PriorytetSpotkania.Normalny
        };

        private StatusZaproszenia ParseStatusZaproszenia(string value) => value switch
        {
            "Oczekuje" => StatusZaproszenia.Oczekuje,
            "Zaakceptowane" or "Potwierdzone" => StatusZaproszenia.Zaakceptowane,
            "Odrzucone" => StatusZaproszenia.Odrzucone,
            "Może" => StatusZaproszenia.Moze,
            _ => StatusZaproszenia.Oczekuje
        };

        #endregion
    }
}
