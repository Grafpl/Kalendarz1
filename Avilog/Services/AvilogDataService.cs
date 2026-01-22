using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.Avilog.Models;

namespace Kalendarz1.Avilog.Services
{
    /// <summary>
    /// Serwis dostępu do danych dla rozliczeń Avilog
    /// </summary>
    public class AvilogDataService
    {
        private readonly string _connectionString;

        public AvilogDataService(string connectionString = null)
        {
            _connectionString = connectionString ??
                "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        }

        /// <summary>
        /// Pobiera wszystkie kursy z podanego zakresu dat
        /// </summary>
        public async Task<List<AvilogKursModel>> GetKursyAsync(DateTime dataOd, DateTime dataDo)
        {
            var kursy = new List<AvilogKursModel>();

            string query = @"
                SELECT
                    fc.ID,
                    fc.CalcDate,
                    fc.CarLp,
                    fc.CarID,
                    fc.TrailerID,
                    fc.DriverGID,
                    ISNULL(d.Name, 'Nieznany') AS KierowcaNazwa,

                    -- Dostawca
                    fc.CustomerGID,
                    fc.CustomerRealGID,
                    ISNULL(dos.ShortName, fc.CustomerRealGID) AS HodowcaNazwa,

                    -- Sztuki
                    ISNULL(fc.DeclI1, 0) AS SztukiZadeklarowane,
                    ISNULL(fc.LumQnt, 0) AS SztukiLumel,
                    ISNULL(fc.DeclI2, 0) AS SztukiPadle,

                    -- Wagi hodowcy
                    ISNULL(fc.FullFarmWeight, 0) AS BruttoHodowcy,
                    ISNULL(fc.EmptyFarmWeight, 0) AS TaraHodowcy,
                    ISNULL(fc.NettoFarmWeight, ISNULL(fc.FullFarmWeight, 0) - ISNULL(fc.EmptyFarmWeight, 0)) AS NettoHodowcy,

                    -- Wagi ubojni
                    ISNULL(fc.FullWeight, 0) AS BruttoUbojni,
                    ISNULL(fc.EmptyWeight, 0) AS TaraUbojni,
                    ISNULL(fc.NettoWeight, 0) AS NettoUbojni,

                    -- Kilometry
                    ISNULL(fc.StartKM, 0) AS StartKM,
                    ISNULL(fc.StopKM, 0) AS StopKM,

                    -- Czasy
                    fc.PoczatekUslugi,
                    fc.Wyjazd,
                    fc.DojazdHodowca,
                    fc.Zaladunek,
                    fc.ZaladunekKoniec,
                    fc.WyjazdHodowca,
                    fc.Przyjazd,
                    fc.KoniecUslugi

                FROM [LibraNet].[dbo].[FarmerCalc] fc
                LEFT JOIN [LibraNet].[dbo].[Dostawcy] dos ON fc.CustomerRealGID = dos.ID
                LEFT JOIN [LibraNet].[dbo].[Driver] d ON fc.DriverGID = d.GID

                WHERE fc.CalcDate >= @DataOd AND fc.CalcDate < DATEADD(day, 1, @DataDo)
                  AND ISNULL(fc.LumQnt, 0) > 0

                ORDER BY fc.CalcDate, fc.CarLp";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@DataOd", dataOd.Date);
                    cmd.Parameters.AddWithValue("@DataDo", dataDo.Date);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        int lp = 0;
                        while (await reader.ReadAsync())
                        {
                            lp++;
                            var kurs = new AvilogKursModel
                            {
                                LP = lp,
                                ID = reader.GetInt32(reader.GetOrdinal("ID")),
                                CalcDate = reader.GetDateTime(reader.GetOrdinal("CalcDate")),
                                CarLp = GetIntSafe(reader, "CarLp"),
                                CarID = GetStringSafe(reader, "CarID"),
                                TrailerID = GetStringSafe(reader, "TrailerID"),
                                DriverGID = GetIntNullable(reader, "DriverGID"),
                                KierowcaNazwa = GetStringSafe(reader, "KierowcaNazwa"),
                                CustomerGID = GetStringSafe(reader, "CustomerGID"),
                                CustomerRealGID = GetStringSafe(reader, "CustomerRealGID"),
                                HodowcaNazwa = GetStringSafe(reader, "HodowcaNazwa"),
                                SztukiZadeklarowane = GetIntSafe(reader, "SztukiZadeklarowane"),
                                SztukiLumel = GetIntSafe(reader, "SztukiLumel"),
                                SztukiPadle = GetIntSafe(reader, "SztukiPadle"),
                                BruttoHodowcy = GetDecimalSafe(reader, "BruttoHodowcy"),
                                TaraHodowcy = GetDecimalSafe(reader, "TaraHodowcy"),
                                NettoHodowcy = GetDecimalSafe(reader, "NettoHodowcy"),
                                BruttoUbojni = GetDecimalSafe(reader, "BruttoUbojni"),
                                TaraUbojni = GetDecimalSafe(reader, "TaraUbojni"),
                                NettoUbojni = GetDecimalSafe(reader, "NettoUbojni"),
                                StartKM = GetIntSafe(reader, "StartKM"),
                                StopKM = GetIntSafe(reader, "StopKM"),
                                PoczatekUslugi = GetDateTimeNullable(reader, "PoczatekUslugi"),
                                Wyjazd = GetDateTimeNullable(reader, "Wyjazd"),
                                DojazdHodowca = GetDateTimeNullable(reader, "DojazdHodowca"),
                                Zaladunek = GetDateTimeNullable(reader, "Zaladunek"),
                                ZaladunekKoniec = GetDateTimeNullable(reader, "ZaladunekKoniec"),
                                WyjazdHodowca = GetDateTimeNullable(reader, "WyjazdHodowca"),
                                Przyjazd = GetDateTimeNullable(reader, "Przyjazd"),
                                KoniecUslugi = GetDateTimeNullable(reader, "KoniecUslugi")
                            };
                            kursy.Add(kurs);
                        }
                    }
                }
            }

            return kursy;
        }

        /// <summary>
        /// Pobiera podsumowania dzienne z podanego zakresu dat
        /// </summary>
        public async Task<List<AvilogDayModel>> GetPodsumowaniaDzienneAsync(DateTime dataOd, DateTime dataDo)
        {
            var dni = new List<AvilogDayModel>();

            string query = @"
                SELECT
                    CAST(fc.CalcDate AS DATE) AS Data,
                    DATENAME(WEEKDAY, fc.CalcDate) AS DzienTygodnia,
                    COUNT(*) AS LiczbaKursow,
                    COUNT(DISTINCT CONCAT(fc.CarID, '-', fc.TrailerID)) AS LiczbaZestawow,

                    SUM(ISNULL(fc.LumQnt, 0) + ISNULL(fc.DeclI2, 0)) AS SumaSztuk,
                    SUM(ISNULL(fc.FullFarmWeight, 0)) AS SumaBrutto,
                    SUM(ISNULL(fc.EmptyFarmWeight, 0)) AS SumaTara,
                    SUM(ISNULL(fc.NettoFarmWeight, ISNULL(fc.FullFarmWeight, 0) - ISNULL(fc.EmptyFarmWeight, 0))) AS SumaNetto,
                    SUM(ISNULL(fc.DeclI2, 0)) AS SumaUpadkowSzt,

                    -- Upadki kg = suma(padłe × średnia waga)
                    SUM(
                        CASE
                            WHEN (ISNULL(fc.LumQnt, 0) + ISNULL(fc.DeclI2, 0)) > 0
                            THEN ROUND(ISNULL(fc.DeclI2, 0) * (ISNULL(fc.NettoFarmWeight, 0) / (ISNULL(fc.LumQnt, 0) + ISNULL(fc.DeclI2, 0))), 0)
                            ELSE 0
                        END
                    ) AS SumaUpadkowKg,

                    SUM(ISNULL(fc.StopKM, 0) - ISNULL(fc.StartKM, 0)) AS SumaKM,

                    SUM(
                        CASE
                            WHEN fc.PoczatekUslugi IS NOT NULL AND fc.KoniecUslugi IS NOT NULL
                            THEN DATEDIFF(MINUTE, fc.PoczatekUslugi, fc.KoniecUslugi) / 60.0
                            ELSE 0
                        END
                    ) AS SumaGodzin,

                    -- Liczba braków danych
                    SUM(CASE WHEN ISNULL(fc.StartKM, 0) = 0 OR ISNULL(fc.StopKM, 0) = 0 THEN 1 ELSE 0 END) AS LiczbaBrakowKM,
                    SUM(CASE WHEN fc.PoczatekUslugi IS NULL OR fc.KoniecUslugi IS NULL THEN 1 ELSE 0 END) AS LiczbaBrakowGodzin

                FROM [LibraNet].[dbo].[FarmerCalc] fc
                WHERE fc.CalcDate >= @DataOd AND fc.CalcDate < DATEADD(day, 1, @DataDo)
                  AND ISNULL(fc.LumQnt, 0) > 0
                GROUP BY CAST(fc.CalcDate AS DATE), DATENAME(WEEKDAY, fc.CalcDate)
                ORDER BY Data";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@DataOd", dataOd.Date);
                    cmd.Parameters.AddWithValue("@DataDo", dataDo.Date);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        int lp = 0;
                        while (await reader.ReadAsync())
                        {
                            lp++;
                            var dzien = new AvilogDayModel
                            {
                                LP = lp,
                                Data = reader.GetDateTime(reader.GetOrdinal("Data")),
                                DzienTygodnia = GetStringSafe(reader, "DzienTygodnia"),
                                LiczbaKursow = GetIntSafe(reader, "LiczbaKursow"),
                                LiczbaZestawow = GetIntSafe(reader, "LiczbaZestawow"),
                                SumaSztuk = GetIntSafe(reader, "SumaSztuk"),
                                SumaBrutto = GetDecimalSafe(reader, "SumaBrutto"),
                                SumaTara = GetDecimalSafe(reader, "SumaTara"),
                                SumaNetto = GetDecimalSafe(reader, "SumaNetto"),
                                SumaUpadkowSzt = GetIntSafe(reader, "SumaUpadkowSzt"),
                                SumaUpadkowKg = GetDecimalSafe(reader, "SumaUpadkowKg"),
                                SumaKM = GetIntSafe(reader, "SumaKM"),
                                SumaGodzin = GetDecimalSafe(reader, "SumaGodzin"),
                                LiczbaBrakowKM = GetIntSafe(reader, "LiczbaBrakowKM"),
                                LiczbaBrakowGodzin = GetIntSafe(reader, "LiczbaBrakowGodzin")
                            };
                            dzien.MaBrakiDanych = dzien.LiczbaBrakowKM > 0 || dzien.LiczbaBrakowGodzin > 0;
                            dni.Add(dzien);
                        }
                    }
                }
            }

            return dni;
        }

        /// <summary>
        /// Pobiera aktualną stawkę za kg
        /// </summary>
        public async Task<decimal> GetAktualnaStawkaAsync()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    // Sprawdź czy tabela istnieje
                    string checkTable = @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                                         WHERE TABLE_NAME = 'AvilogSettings'";
                    using (var checkCmd = new SqlCommand(checkTable, conn))
                    {
                        int exists = (int)await checkCmd.ExecuteScalarAsync();
                        if (exists == 0)
                        {
                            await CreateAvilogSettingsTableAsync(conn);
                            return 0.119m; // Domyślna stawka
                        }
                    }

                    string query = @"SELECT TOP 1 StawkaZaKg FROM [dbo].[AvilogSettings]
                                    WHERE DataDo IS NULL OR DataDo >= GETDATE()
                                    ORDER BY DataOd DESC";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        var result = await cmd.ExecuteScalarAsync();
                        return result != null && result != DBNull.Value ? (decimal)result : 0.119m;
                    }
                }
            }
            catch
            {
                return 0.119m; // Domyślna stawka w przypadku błędu
            }
        }

        /// <summary>
        /// Zapisuje nową stawkę za kg
        /// </summary>
        public async Task SaveStawkaAsync(decimal stawka, string zmienionePrzez, string uwagi = null)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Upewnij się, że tabela istnieje
                string checkTable = @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                                     WHERE TABLE_NAME = 'AvilogSettings'";
                using (var checkCmd = new SqlCommand(checkTable, conn))
                {
                    int exists = (int)await checkCmd.ExecuteScalarAsync();
                    if (exists == 0)
                    {
                        await CreateAvilogSettingsTableAsync(conn);
                    }
                }

                // Zamknij poprzednią stawkę
                string updateQuery = @"UPDATE [dbo].[AvilogSettings]
                                      SET DataDo = DATEADD(day, -1, GETDATE())
                                      WHERE DataDo IS NULL";
                using (var updateCmd = new SqlCommand(updateQuery, conn))
                {
                    await updateCmd.ExecuteNonQueryAsync();
                }

                // Wstaw nową stawkę
                string insertQuery = @"INSERT INTO [dbo].[AvilogSettings]
                                      (StawkaZaKg, DataOd, ZmienionePrzez, DataZmiany, Uwagi)
                                      VALUES (@Stawka, GETDATE(), @ZmienionePrzez, GETDATE(), @Uwagi)";
                using (var insertCmd = new SqlCommand(insertQuery, conn))
                {
                    insertCmd.Parameters.AddWithValue("@Stawka", stawka);
                    insertCmd.Parameters.AddWithValue("@ZmienionePrzez", zmienionePrzez ?? Environment.UserName);
                    insertCmd.Parameters.AddWithValue("@Uwagi", (object)uwagi ?? DBNull.Value);
                    await insertCmd.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Pobiera historię zmian stawki
        /// </summary>
        public async Task<List<AvilogSettingsModel>> GetHistoriaStawekAsync()
        {
            var historia = new List<AvilogSettingsModel>();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    string query = @"SELECT ID, StawkaZaKg, DataOd, DataDo, ZmienionePrzez, DataZmiany, Uwagi
                                    FROM [dbo].[AvilogSettings]
                                    ORDER BY DataOd DESC";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                historia.Add(new AvilogSettingsModel
                                {
                                    ID = reader.GetInt32(0),
                                    StawkaZaKg = reader.GetDecimal(1),
                                    DataOd = reader.GetDateTime(2),
                                    DataDo = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                                    ZmienionePrzez = GetStringSafe(reader, "ZmienionePrzez"),
                                    DataZmiany = reader.GetDateTime(5),
                                    Uwagi = GetStringSafe(reader, "Uwagi")
                                });
                            }
                        }
                    }
                }
            }
            catch { }

            return historia;
        }

        /// <summary>
        /// Tworzy tabelę ustawień Avilog
        /// </summary>
        private async Task CreateAvilogSettingsTableAsync(SqlConnection conn)
        {
            string createTable = @"
                CREATE TABLE [dbo].[AvilogSettings] (
                    [ID] INT IDENTITY(1,1) PRIMARY KEY,
                    [StawkaZaKg] DECIMAL(10,4) NOT NULL DEFAULT 0.119,
                    [DataOd] DATETIME NOT NULL DEFAULT GETDATE(),
                    [DataDo] DATETIME NULL,
                    [ZmienionePrzez] NVARCHAR(100) NULL,
                    [DataZmiany] DATETIME NOT NULL DEFAULT GETDATE(),
                    [Uwagi] NVARCHAR(500) NULL
                );

                -- Wstaw domyślną stawkę
                INSERT INTO [dbo].[AvilogSettings] (StawkaZaKg, DataOd, ZmienionePrzez, Uwagi)
                VALUES (0.119, GETDATE(), 'SYSTEM', 'Stawka początkowa');
            ";

            using (var cmd = new SqlCommand(createTable, conn))
            {
                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Pobiera statystyki top hodowców wg tonażu
        /// </summary>
        public async Task<List<(string Hodowca, decimal Tonaz, int Kursy)>> GetTopHodowcyAsync(DateTime dataOd, DateTime dataDo, int top = 5)
        {
            var result = new List<(string, decimal, int)>();

            string query = @"
                SELECT TOP (@Top)
                    ISNULL(dos.ShortName, fc.CustomerRealGID) AS Hodowca,
                    SUM(ISNULL(fc.NettoFarmWeight, 0)) AS Tonaz,
                    COUNT(*) AS Kursy
                FROM [LibraNet].[dbo].[FarmerCalc] fc
                LEFT JOIN [LibraNet].[dbo].[Dostawcy] dos ON fc.CustomerRealGID = dos.ID
                WHERE fc.CalcDate >= @DataOd AND fc.CalcDate < DATEADD(day, 1, @DataDo)
                  AND ISNULL(fc.LumQnt, 0) > 0
                GROUP BY ISNULL(dos.ShortName, fc.CustomerRealGID)
                ORDER BY Tonaz DESC";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Top", top);
                    cmd.Parameters.AddWithValue("@DataOd", dataOd.Date);
                    cmd.Parameters.AddWithValue("@DataDo", dataDo.Date);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add((
                                GetStringSafe(reader, "Hodowca"),
                                GetDecimalSafe(reader, "Tonaz"),
                                GetIntSafe(reader, "Kursy")
                            ));
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Pobiera statystyki top kierowców wg przejechanych km
        /// </summary>
        public async Task<List<(string Kierowca, int KM, int Kursy)>> GetTopKierowcyAsync(DateTime dataOd, DateTime dataDo, int top = 5)
        {
            var result = new List<(string, int, int)>();

            string query = @"
                SELECT TOP (@Top)
                    ISNULL(d.Name, 'Nieznany') AS Kierowca,
                    SUM(ISNULL(fc.StopKM, 0) - ISNULL(fc.StartKM, 0)) AS KM,
                    COUNT(*) AS Kursy
                FROM [LibraNet].[dbo].[FarmerCalc] fc
                LEFT JOIN [LibraNet].[dbo].[Driver] d ON fc.DriverGID = d.GID
                WHERE fc.CalcDate >= @DataOd AND fc.CalcDate < DATEADD(day, 1, @DataDo)
                  AND ISNULL(fc.LumQnt, 0) > 0
                GROUP BY ISNULL(d.Name, 'Nieznany')
                ORDER BY KM DESC";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Top", top);
                    cmd.Parameters.AddWithValue("@DataOd", dataOd.Date);
                    cmd.Parameters.AddWithValue("@DataDo", dataDo.Date);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add((
                                GetStringSafe(reader, "Kierowca"),
                                GetIntSafe(reader, "KM"),
                                GetIntSafe(reader, "Kursy")
                            ));
                        }
                    }
                }
            }

            return result;
        }

        // === HELPERY ===

        private static string GetStringSafe(SqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? "" : reader.GetString(ordinal);
        }

        private static int GetIntSafe(SqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal)) return 0;
            var value = reader.GetValue(ordinal);
            return Convert.ToInt32(value);
        }

        private static int? GetIntNullable(SqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
        }

        private static decimal GetDecimalSafe(SqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal)) return 0;
            var value = reader.GetValue(ordinal);
            return Convert.ToDecimal(value);
        }

        private static DateTime? GetDateTimeNullable(SqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
        }
    }
}
