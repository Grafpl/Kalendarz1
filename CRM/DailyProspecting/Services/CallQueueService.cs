using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kalendarz1.CRM.DailyProspecting
{
    /// <summary>
    /// Serwis do obsługi kolejki telefonów prospectingowych.
    /// Komunikuje się z bazą danych LibraNet.
    /// </summary>
    public class CallQueueService
    {
        private readonly string _connectionString;

        public CallQueueService(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Pobiera dzisiejszą kolejkę telefonów dla handlowca.
        /// </summary>
        public async Task<List<DailyCallItem>> GetDzisiejszaKolejkaAsync(string handlowiecId)
        {
            var lista = new List<DailyCallItem>();

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    SELECT * FROM vw_KolejkaDzisiejsza
                    WHERE HandlowiecID = @HandlowiecID
                    ORDER BY
                        CASE StatusRealizacji WHEN 'Oczekuje' THEN 0 ELSE 1 END,
                        Priorytet DESC", conn);

                cmd.Parameters.AddWithValue("@HandlowiecID", handlowiecId);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        lista.Add(MapToCallItem(reader));
                    }
                }
            }

            return lista;
        }

        /// <summary>
        /// Pobiera statystyki dzisiejsze dla handlowca.
        /// </summary>
        public async Task<StatystykiProspectingu> GetDzisiejszeStatystykiAsync(string handlowiecId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    SELECT
                        s.*,
                        o.Name as HandlowiecNazwa
                    FROM StatystykiProspectingu s
                    LEFT JOIN operators o ON s.HandlowiecID = CAST(o.ID AS NVARCHAR)
                    WHERE s.HandlowiecID = @HandlowiecID
                      AND s.Data = CAST(GETDATE() AS DATE)", conn);

                cmd.Parameters.AddWithValue("@HandlowiecID", handlowiecId);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new StatystykiProspectingu
                        {
                            StatID = reader.GetInt32(reader.GetOrdinal("StatID")),
                            HandlowiecID = reader["HandlowiecID"].ToString(),
                            HandlowiecNazwa = reader["HandlowiecNazwa"]?.ToString(),
                            Data = reader.GetDateTime(reader.GetOrdinal("Data")),
                            Przydzielone = reader.GetInt32(reader.GetOrdinal("Przydzielone")),
                            Wykonane = reader.GetInt32(reader.GetOrdinal("Wykonane")),
                            Rozmowy = reader.GetInt32(reader.GetOrdinal("Rozmowy")),
                            Nieodebrane = reader.GetInt32(reader.GetOrdinal("Nieodebrane")),
                            Callbacki = reader.GetInt32(reader.GetOrdinal("Callbacki")),
                            Odmowy = reader.GetInt32(reader.GetOrdinal("Odmowy")),
                            Oferty = reader.GetInt32(reader.GetOrdinal("Oferty")),
                            Pominiete = reader.GetInt32(reader.GetOrdinal("Pominiete"))
                        };
                    }
                }
            }

            // Brak statystyk - zwróć puste
            return new StatystykiProspectingu
            {
                HandlowiecID = handlowiecId,
                Data = DateTime.Today
            };
        }

        /// <summary>
        /// Oznacza telefon jako wykonany z podanym rezultatem.
        /// </summary>
        public async Task OznaczWykonanoAsync(int kolejkaId, string rezultat, string notatka, string handlowiecId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // Aktualizuj kolejkę
                        var cmdKolejka = new SqlCommand(@"
                            UPDATE CodzienaKolejkaTelefonow SET
                                StatusRealizacji = 'Wykonano',
                                GodzinaWykonania = GETDATE(),
                                RezultatRozmowy = @Rezultat,
                                Notatka = @Notatka
                            WHERE KolejkaID = @KolejkaID", conn, transaction);

                        cmdKolejka.Parameters.AddWithValue("@KolejkaID", kolejkaId);
                        cmdKolejka.Parameters.AddWithValue("@Rezultat", rezultat);
                        cmdKolejka.Parameters.AddWithValue("@Notatka", (object)notatka ?? DBNull.Value);

                        await cmdKolejka.ExecuteNonQueryAsync();

                        // Pobierz OdbiorcaID
                        var cmdGetOdbiorca = new SqlCommand(
                            "SELECT OdbiorcaID FROM CodzienaKolejkaTelefonow WHERE KolejkaID = @KolejkaID",
                            conn, transaction);
                        cmdGetOdbiorca.Parameters.AddWithValue("@KolejkaID", kolejkaId);
                        var odbiorcaId = (int)await cmdGetOdbiorca.ExecuteScalarAsync();

                        // Aktualizuj OdbiorcyCRM
                        var cmdOdbiorca = new SqlCommand(@"
                            UPDATE OdbiorcyCRM SET
                                OstatniRezultat = @Rezultat,
                                DataOstatniegoKontaktu = GETDATE()
                            WHERE ID = @OdbiorcaID", conn, transaction);

                        cmdOdbiorca.Parameters.AddWithValue("@OdbiorcaID", odbiorcaId);
                        cmdOdbiorca.Parameters.AddWithValue("@Rezultat", rezultat);
                        await cmdOdbiorca.ExecuteNonQueryAsync();

                        // Dodaj notatkę do CRM jeśli jest
                        if (!string.IsNullOrWhiteSpace(notatka))
                        {
                            var cmdNotatka = new SqlCommand(@"
                                INSERT INTO NotatkiCRM (IDOdbiorcy, Tresc, KtoDodal, DataUtworzenia, DataDodania)
                                VALUES (@IDOdbiorcy, @Tresc, @KtoDodal, GETDATE(), GETDATE())", conn, transaction);

                            cmdNotatka.Parameters.AddWithValue("@IDOdbiorcy", odbiorcaId);
                            cmdNotatka.Parameters.AddWithValue("@Tresc", $"[PROSPECTING - {rezultat}] {notatka}");
                            cmdNotatka.Parameters.AddWithValue("@KtoDodal", handlowiecId);
                            await cmdNotatka.ExecuteNonQueryAsync();
                        }

                        // Dodaj wpis do historii zmian
                        var cmdHistoria = new SqlCommand(@"
                            INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscNowa, KtoWykonal, DataZmiany)
                            VALUES (@IDOdbiorcy, 'Prospecting', @Rezultat, @KtoWykonal, GETDATE())", conn, transaction);

                        cmdHistoria.Parameters.AddWithValue("@IDOdbiorcy", odbiorcaId);
                        cmdHistoria.Parameters.AddWithValue("@Rezultat", rezultat);
                        cmdHistoria.Parameters.AddWithValue("@KtoWykonal", handlowiecId);
                        await cmdHistoria.ExecuteNonQueryAsync();

                        // Aktualizuj statystyki
                        var cmdStats = new SqlCommand(
                            "EXEC AktualizujStatystykiProspectingu @HandlowiecID, NULL",
                            conn, transaction);
                        cmdStats.Parameters.AddWithValue("@HandlowiecID", handlowiecId);
                        await cmdStats.ExecuteNonQueryAsync();

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Oznacza telefon jako pominięty.
        /// </summary>
        public async Task OznaczPominietoAsync(int kolejkaId, string powod, string handlowiecId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    UPDATE CodzienaKolejkaTelefonow SET
                        StatusRealizacji = 'Pominięto',
                        GodzinaWykonania = GETDATE(),
                        Notatka = @Powod
                    WHERE KolejkaID = @KolejkaID", conn);

                cmd.Parameters.AddWithValue("@KolejkaID", kolejkaId);
                cmd.Parameters.AddWithValue("@Powod", (object)powod ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync();

                // Aktualizuj statystyki
                var cmdStats = new SqlCommand(
                    "EXEC AktualizujStatystykiProspectingu @HandlowiecID, NULL", conn);
                cmdStats.Parameters.AddWithValue("@HandlowiecID", handlowiecId);
                await cmdStats.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Ustawia datę następnego kontaktu dla odbiorcy.
        /// </summary>
        public async Task UstawDataNastepnegoKontaktuAsync(int odbiorcaId, DateTime data, string handlowiecId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    UPDATE OdbiorcyCRM SET DataNastepnegoKontaktu = @Data WHERE ID = @ID", conn);

                cmd.Parameters.AddWithValue("@ID", odbiorcaId);
                cmd.Parameters.AddWithValue("@Data", data);

                await cmd.ExecuteNonQueryAsync();

                // Dodaj do historii
                var cmdHistoria = new SqlCommand(@"
                    INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscNowa, KtoWykonal, DataZmiany)
                    VALUES (@IDOdbiorcy, 'Ustawiono follow-up', @Data, @KtoWykonal, GETDATE())", conn);

                cmdHistoria.Parameters.AddWithValue("@IDOdbiorcy", odbiorcaId);
                cmdHistoria.Parameters.AddWithValue("@Data", data.ToString("yyyy-MM-dd"));
                cmdHistoria.Parameters.AddWithValue("@KtoWykonal", handlowiecId);
                await cmdHistoria.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Zmienia status odbiorcy w CRM.
        /// </summary>
        public async Task ZmienStatusCRMAsync(int odbiorcaId, string nowyStatus, string handlowiecId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    UPDATE OdbiorcyCRM SET Status = @Status WHERE ID = @ID", conn);

                cmd.Parameters.AddWithValue("@ID", odbiorcaId);
                cmd.Parameters.AddWithValue("@Status", nowyStatus);

                await cmd.ExecuteNonQueryAsync();

                // Dodaj do historii
                var cmdHistoria = new SqlCommand(@"
                    INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscNowa, KtoWykonal, DataZmiany)
                    VALUES (@IDOdbiorcy, 'Zmiana statusu', @Status, @KtoWykonal, GETDATE())", conn);

                cmdHistoria.Parameters.AddWithValue("@IDOdbiorcy", odbiorcaId);
                cmdHistoria.Parameters.AddWithValue("@Status", nowyStatus);
                cmdHistoria.Parameters.AddWithValue("@KtoWykonal", handlowiecId);
                await cmdHistoria.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Generuje kolejkę na dzisiaj (wywołuje procedurę SQL).
        /// </summary>
        public async Task GenerujKolejkeAsync(string handlowiecId = null)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                var cmd = new SqlCommand("EXEC GenerujCodzienaKolejke @Data, @HandlowiecID", conn);
                cmd.Parameters.AddWithValue("@Data", DateTime.Today);
                cmd.Parameters.AddWithValue("@HandlowiecID", (object)handlowiecId ?? DBNull.Value);
                cmd.CommandTimeout = 60;

                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Sprawdza czy handlowiec ma skonfigurowany prospecting.
        /// </summary>
        public async Task<bool> MaKonfiguracjeAsync(string handlowiecId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM KonfiguracjaProspectingu
                    WHERE HandlowiecID = @HandlowiecID AND Aktywny = 1", conn);

                cmd.Parameters.AddWithValue("@HandlowiecID", handlowiecId);

                return (int)await cmd.ExecuteScalarAsync() > 0;
            }
        }

        /// <summary>
        /// Pobiera konfigurację prospectingu dla handlowca.
        /// </summary>
        public async Task<KonfiguracjaProspectingu> GetKonfiguracjaAsync(string handlowiecId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
                    SELECT k.*, o.Name as HandlowiecNazwaDB
                    FROM KonfiguracjaProspectingu k
                    LEFT JOIN operators o ON k.HandlowiecID = CAST(o.ID AS NVARCHAR)
                    WHERE k.HandlowiecID = @HandlowiecID", conn);

                cmd.Parameters.AddWithValue("@HandlowiecID", handlowiecId);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new KonfiguracjaProspectingu
                        {
                            KonfigID = reader.GetInt32(reader.GetOrdinal("KonfigID")),
                            HandlowiecID = reader["HandlowiecID"].ToString(),
                            HandlowiecNazwa = reader["HandlowiecNazwa"]?.ToString() ?? reader["HandlowiecNazwaDB"]?.ToString(),
                            LimitDzienny = reader.GetInt32(reader.GetOrdinal("LimitDzienny")),
                            GodzinaStart = reader.GetTimeSpan(reader.GetOrdinal("GodzinaStart")),
                            GodzinaKoniec = reader.GetTimeSpan(reader.GetOrdinal("GodzinaKoniec")),
                            DniTygodnia = reader["DniTygodnia"]?.ToString() ?? "1,2,3,4,5",
                            Wojewodztwa = reader["Wojewodztwa"]?.ToString(),
                            TypyKlientow = reader["TypyKlientow"]?.ToString(),
                            PKD = reader["PKD"]?.ToString(),
                            PriorytetMin = reader.GetInt32(reader.GetOrdinal("PriorytetMin")),
                            PriorytetMax = reader.GetInt32(reader.GetOrdinal("PriorytetMax")),
                            Aktywny = reader.GetBoolean(reader.GetOrdinal("Aktywny")),
                            DataUtworzenia = reader.GetDateTime(reader.GetOrdinal("DataUtworzenia"))
                        };
                    }
                }
            }

            return null;
        }

        private DailyCallItem MapToCallItem(SqlDataReader reader)
        {
            return new DailyCallItem
            {
                KolejkaID = reader.GetInt32(reader.GetOrdinal("KolejkaID")),
                HandlowiecID = reader["HandlowiecID"].ToString(),
                OdbiorcaID = reader.GetInt32(reader.GetOrdinal("OdbiorcaID")),
                NazwaFirmy = reader["NazwaFirmy"]?.ToString(),
                Telefon = reader["Telefon"]?.ToString(),
                Email = reader["Email"]?.ToString(),
                Miasto = reader["Miasto"]?.ToString(),
                Wojewodztwo = reader["Wojewodztwo"]?.ToString(),
                Branza = reader["Branza"]?.ToString(),
                TypKlienta = reader["TypKlienta"]?.ToString(),
                StatusCRM = reader["StatusCRM"]?.ToString(),
                DataNastepnegoKontaktu = reader["DataNastepnegoKontaktu"] as DateTime?,
                Priorytet = reader.GetInt32(reader.GetOrdinal("Priorytet")),
                PowodPriorytetu = reader["PowodPriorytetu"]?.ToString(),
                StatusRealizacji = reader["StatusRealizacji"]?.ToString() ?? "Oczekuje",
                GodzinaWykonania = reader["GodzinaWykonania"] as DateTime?,
                RezultatRozmowy = reader["RezultatRozmowy"]?.ToString(),
                Notatka = reader["Notatka"]?.ToString(),
                OstatniaNot = reader["OstatniaNot"]?.ToString(),
                LiczbaNotatek = reader["LiczbaNotatek"] != DBNull.Value ? Convert.ToInt32(reader["LiczbaNotatek"]) : 0
            };
        }
    }
}
