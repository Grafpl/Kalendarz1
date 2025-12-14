using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.SmsSprzedaz
{
    /// <summary>
    /// Serwis do automatycznego wysyłania SMS-ów do handlowców o wydaniach towaru
    /// Używa SMSAPI.pl jako dostawcy SMS
    /// </summary>
    public class SmsSprzedazService
    {
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private readonly string _connTransport = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // Cache
        private Dictionary<string, string> _handlowiecTelefonCache = new();
        private SmsApiClient _smsApiClient = null;
        private SmsApiConfig _apiConfig = null;
        private bool _configLoaded = false;

        /// <summary>
        /// Pobiera konfigurację SMSAPI z bazy danych
        /// </summary>
        public async Task<SmsApiConfig> PobierzKonfiguracjeApiAsync()
        {
            if (_configLoaded && _apiConfig != null)
                return _apiConfig;

            _apiConfig = new SmsApiConfig();

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Upewnij się że tabela konfiguracji istnieje
                await UtworzTabeleKonfiguracjiAsync(cn);

                var sql = @"SELECT TOP 1 ApiToken, NadawcaNazwa, Aktywny, TestMode
                            FROM dbo.SmsApiKonfiguracja
                            WHERE Aktywny = 1
                            ORDER BY Id DESC";

                await using var cmd = new SqlCommand(sql, cn);
                await using var rd = await cmd.ExecuteReaderAsync();

                if (await rd.ReadAsync())
                {
                    _apiConfig.ApiToken = rd.IsDBNull(0) ? "" : rd.GetString(0);
                    _apiConfig.NadawcaNazwa = rd.IsDBNull(1) ? "" : rd.GetString(1);
                    _apiConfig.Aktywny = rd.GetBoolean(2);
                    _apiConfig.TestMode = rd.GetBoolean(3);
                }

                _configLoaded = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania konfiguracji API: {ex.Message}");
            }

            return _apiConfig;
        }

        /// <summary>
        /// Tworzy tabelę konfiguracji SMSAPI jeśli nie istnieje
        /// </summary>
        private async Task UtworzTabeleKonfiguracjiAsync(SqlConnection cn)
        {
            var sql = @"IF NOT EXISTS (SELECT * FROM sys.objects WHERE name='SmsApiKonfiguracja' AND type='U')
                        BEGIN
                            CREATE TABLE dbo.SmsApiKonfiguracja (
                                Id INT IDENTITY(1,1) PRIMARY KEY,
                                ApiToken NVARCHAR(200) NOT NULL,
                                NadawcaNazwa NVARCHAR(11) NULL,
                                Aktywny BIT NOT NULL DEFAULT 1,
                                TestMode BIT NOT NULL DEFAULT 0,
                                DataUtworzenia DATETIME NOT NULL DEFAULT GETDATE(),
                                DataModyfikacji DATETIME NULL
                            );

                            -- Wstaw domyślny rekord (pusty - do uzupełnienia)
                            INSERT INTO dbo.SmsApiKonfiguracja (ApiToken, NadawcaNazwa, Aktywny, TestMode)
                            VALUES ('', 'PRONOVA', 0, 1);

                            PRINT 'Tabela SmsApiKonfiguracja utworzona. Uzupełnij ApiToken w bazie!'
                        END";

            await using var cmd = new SqlCommand(sql, cn);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Pobiera lub tworzy klienta SMSAPI
        /// </summary>
        private async Task<SmsApiClient> GetSmsApiClientAsync()
        {
            if (_smsApiClient != null)
                return _smsApiClient;

            var config = await PobierzKonfiguracjeApiAsync();

            if (!string.IsNullOrEmpty(config.ApiToken) && config.Aktywny)
            {
                _smsApiClient = new SmsApiClient(config.ApiToken, config.NadawcaNazwa);
            }

            return _smsApiClient;
        }

        /// <summary>
        /// Pobiera informacje o wydaniu do wysłania SMS
        /// </summary>
        public async Task<WydanieInfo> PobierzInfoWydaniaAsync(int zamowienieId)
        {
            var info = new WydanieInfo { ZamowienieId = zamowienieId };

            try
            {
                // Pobierz podstawowe dane zamówienia z LibraNet
                await using var cnLibra = new SqlConnection(_connLibra);
                await cnLibra.OpenAsync();

                var sqlZam = @"SELECT z.KlientId,
                                      (SELECT SUM(ISNULL(t.Ilosc, 0)) FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId = z.Id) AS TotalIlosc,
                                      z.DataWydania, z.KtoWydal, z.TransportKursID,
                                      CAST(CASE WHEN z.TransportStatus = 'Wlasny' THEN 1 ELSE 0 END AS BIT) AS WlasnyTransport,
                                      z.DataPrzyjazdu
                               FROM dbo.ZamowieniaMieso z
                               WHERE z.Id = @Id";

                await using var cmdZam = new SqlCommand(sqlZam, cnLibra);
                cmdZam.Parameters.AddWithValue("@Id", zamowienieId);

                await using var rdZam = await cmdZam.ExecuteReaderAsync();
                if (await rdZam.ReadAsync())
                {
                    info.KlientId = rdZam.GetInt32(0);
                    info.IloscKg = rdZam.IsDBNull(1) ? 0 : rdZam.GetDecimal(1);
                    info.DataWydania = rdZam.IsDBNull(2) ? null : rdZam.GetDateTime(2);
                    info.KtoWydal = rdZam.IsDBNull(3) ? "" : rdZam.GetString(3);
                    var transportKursId = rdZam.IsDBNull(4) ? (long?)null : rdZam.GetInt64(4);
                    info.WlasnyTransport = rdZam.GetBoolean(5);
                    info.DataPrzyjazdu = rdZam.IsDBNull(6) ? null : rdZam.GetDateTime(6);

                    // Pobierz dane kursu
                    if (transportKursId.HasValue)
                    {
                        await rdZam.CloseAsync();
                        var kursData = await PobierzDaneKursuAsync(transportKursId.Value);
                        info.CzasWyjazdu = kursData.CzasWyjazdu;
                        info.DataKursu = kursData.DataKursu;
                        info.Kierowca = kursData.Kierowca;
                        info.NumerRejestracyjny = kursData.NumerRejestracyjny;
                    }
                }
                await rdZam.CloseAsync();

                // Pobierz dane klienta i handlowca z Handel
                await using var cnHandel = new SqlConnection(_connHandel);
                await cnHandel.OpenAsync();

                var sqlKlient = @"SELECT c.Shortcut, ISNULL(w.CDim_Handlowiec_Val, '(Brak)') AS Handlowiec
                                  FROM SSCommon.STContractors c
                                  LEFT JOIN SSCommon.ContractorClassification w ON c.Id = w.ElementId
                                  WHERE c.Id = @KlientId";

                await using var cmdKlient = new SqlCommand(sqlKlient, cnHandel);
                cmdKlient.Parameters.AddWithValue("@KlientId", info.KlientId);

                await using var rdKlient = await cmdKlient.ExecuteReaderAsync();
                if (await rdKlient.ReadAsync())
                {
                    info.KlientNazwa = rdKlient.IsDBNull(0) ? $"KH {info.KlientId}" : rdKlient.GetString(0).Trim();
                    info.Handlowiec = rdKlient.IsDBNull(1) ? "(Brak)" : rdKlient.GetString(1).Trim();
                }
                await rdKlient.CloseAsync();

                // Pobierz telefon handlowca
                info.HandlowiecTelefon = await PobierzTelefonHandlowcaAsync(info.Handlowiec);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania info wydania: {ex.Message}");
            }

            return info;
        }

        /// <summary>
        /// Pobiera dane kursu transportowego
        /// </summary>
        private async Task<(TimeSpan? CzasWyjazdu, DateTime? DataKursu, string Kierowca, string NumerRejestracyjny)> PobierzDaneKursuAsync(long kursId)
        {
            try
            {
                await using var cn = new SqlConnection(_connTransport);
                await cn.OpenAsync();

                var sql = @"SELECT k.DataKursu, k.GodzWyjazdu,
                                   ISNULL(kie.Imie + ' ' + kie.Nazwisko, '') as Kierowca,
                                   p.Rejestracja
                            FROM dbo.Kurs k
                            LEFT JOIN dbo.Kierowca kie ON k.KierowcaID = kie.KierowcaID
                            LEFT JOIN dbo.Pojazd p ON k.PojazdID = p.PojazdID
                            WHERE k.KursID = @KursId";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@KursId", kursId);

                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    return (
                        rd.IsDBNull(1) ? null : rd.GetTimeSpan(1),
                        rd.IsDBNull(0) ? null : rd.GetDateTime(0),
                        rd.IsDBNull(2) ? "" : rd.GetString(2),
                        rd.IsDBNull(3) ? "" : rd.GetString(3)
                    );
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania danych kursu: {ex.Message}");
            }

            return (null, null, "", "");
        }

        /// <summary>
        /// Pobiera numer telefonu handlowca na podstawie jego nazwy
        /// </summary>
        public async Task<string> PobierzTelefonHandlowcaAsync(string handlowiecNazwa)
        {
            if (string.IsNullOrWhiteSpace(handlowiecNazwa) || handlowiecNazwa == "(Brak)")
                return "";

            // Sprawdź cache
            if (_handlowiecTelefonCache.TryGetValue(handlowiecNazwa, out var cachedTelefon))
                return cachedTelefon;

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Najpierw znajdź OperatorID na podstawie nazwy handlowca
                var sqlOperator = @"SELECT TOP 1 o.ID
                                    FROM dbo.operators o
                                    WHERE o.Name LIKE @Nazwa + '%' OR o.Name = @Nazwa
                                    ORDER BY o.ID";

                await using var cmdOp = new SqlCommand(sqlOperator, cn);
                cmdOp.Parameters.AddWithValue("@Nazwa", handlowiecNazwa);

                var operatorId = await cmdOp.ExecuteScalarAsync();

                if (operatorId != null && operatorId != DBNull.Value)
                {
                    // Pobierz telefon z OperatorzyKontakt
                    var sqlTelefon = @"SELECT Telefon FROM dbo.OperatorzyKontakt WHERE OperatorID = @OpId";
                    await using var cmdTel = new SqlCommand(sqlTelefon, cn);
                    cmdTel.Parameters.AddWithValue("@OpId", operatorId);

                    var telefon = await cmdTel.ExecuteScalarAsync();
                    var wynik = telefon?.ToString() ?? "";

                    _handlowiecTelefonCache[handlowiecNazwa] = wynik;
                    return wynik;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania telefonu handlowca: {ex.Message}");
            }

            _handlowiecTelefonCache[handlowiecNazwa] = "";
            return "";
        }

        /// <summary>
        /// Wysyła SMS o wydaniu towaru do handlowca - AUTOMATYCZNIE przez SMSAPI.pl
        /// </summary>
        public async Task<WynikWyslaniaSms> WyslijSmsWydaniaAsync(WydanieInfo info, string userId)
        {
            var wynik = new WynikWyslaniaSms();

            try
            {
                // Generuj treść SMS
                var trescSms = SzablonSmsSprzedaz.GenerujTrescSms(info);

                // Sprawdź czy mamy numer telefonu
                if (string.IsNullOrWhiteSpace(info.HandlowiecTelefon))
                {
                    wynik.Sukces = false;
                    wynik.Wiadomosc = $"Brak numeru telefonu dla handlowca {info.Handlowiec}";
                    await ZapiszHistorieSmsAsync(info, trescSms, userId, "Blad", "Brak numeru telefonu");
                    return wynik;
                }

                // Pobierz klienta SMSAPI
                var smsClient = await GetSmsApiClientAsync();
                var config = await PobierzKonfiguracjeApiAsync();

                if (smsClient == null || !config.Aktywny || string.IsNullOrEmpty(config.ApiToken))
                {
                    // API nie skonfigurowane - fallback do schowka
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Clipboard.SetText($"DO: {info.HandlowiecTelefon}\n\n{trescSms}");
                    });

                    wynik.Sukces = true;
                    wynik.SkopiowaDoSchowka = true;
                    wynik.Wiadomosc = "SMSAPI nie skonfigurowane - SMS skopiowany do schowka";
                    await ZapiszHistorieSmsAsync(info, trescSms, userId, "Kopiowany", "API nie skonfigurowane");
                    return wynik;
                }

                // Tryb testowy - nie wysyłaj, tylko loguj
                if (config.TestMode)
                {
                    wynik.Sukces = true;
                    wynik.SkopiowaDoSchowka = false;
                    wynik.Wiadomosc = $"[TEST] SMS do {info.Handlowiec} ({info.HandlowiecTelefon})";
                    wynik.SmsId = "TEST-" + DateTime.Now.Ticks;
                    await ZapiszHistorieSmsAsync(info, trescSms, userId, "Test", "Tryb testowy");
                    return wynik;
                }

                // WYŚLIJ SMS PRZEZ SMSAPI.PL
                var smsResult = await smsClient.WyslijSmsAsync(info.HandlowiecTelefon, trescSms);

                if (smsResult.Sukces)
                {
                    wynik.Sukces = true;
                    wynik.SkopiowaDoSchowka = false;
                    wynik.SmsId = smsResult.SmsId;
                    wynik.Wiadomosc = $"SMS wysłany do {info.Handlowiec}";

                    await ZapiszHistorieSmsAsync(info, trescSms, userId, "Wyslany", $"ID: {smsResult.SmsId}");
                }
                else
                {
                    wynik.Sukces = false;
                    wynik.Wiadomosc = $"Błąd SMSAPI: {smsResult.Blad}";

                    await ZapiszHistorieSmsAsync(info, trescSms, userId, "Blad", smsResult.Blad);
                }
            }
            catch (Exception ex)
            {
                wynik.Sukces = false;
                wynik.Wiadomosc = $"Błąd: {ex.Message}";

                await ZapiszHistorieSmsAsync(info, "", userId, "Blad", ex.Message);
            }

            return wynik;
        }

        /// <summary>
        /// Zapisuje historię wysłanego SMS
        /// </summary>
        private async Task ZapiszHistorieSmsAsync(WydanieInfo info, string trescSms, string userId, string status, string bladOpis)
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Upewnij się że tabela istnieje
                await UtworzTabeleSmsHistoriaAsync(cn);

                var sql = @"INSERT INTO dbo.SmsSprzedazHistoria
                            (ZamowienieId, KlientId, KlientNazwa, Handlowiec, TelefonHandlowca,
                             TrescSms, IloscKg, CzasWyjazdu, DataWyslania, KtoWyslal, Status, BladOpis)
                            VALUES
                            (@ZamowienieId, @KlientId, @KlientNazwa, @Handlowiec, @TelefonHandlowca,
                             @TrescSms, @IloscKg, @CzasWyjazdu, GETDATE(), @KtoWyslal, @Status, @BladOpis)";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@ZamowienieId", info.ZamowienieId);
                cmd.Parameters.AddWithValue("@KlientId", info.KlientId);
                cmd.Parameters.AddWithValue("@KlientNazwa", info.KlientNazwa ?? "");
                cmd.Parameters.AddWithValue("@Handlowiec", info.Handlowiec ?? "");
                cmd.Parameters.AddWithValue("@TelefonHandlowca", info.HandlowiecTelefon ?? "");
                cmd.Parameters.AddWithValue("@TrescSms", trescSms ?? "");
                cmd.Parameters.AddWithValue("@IloscKg", info.IloscKg);

                if (info.CzasWyjazdu.HasValue && info.DataKursu.HasValue)
                    cmd.Parameters.AddWithValue("@CzasWyjazdu", info.DataKursu.Value.Add(info.CzasWyjazdu.Value));
                else if (info.DataPrzyjazdu.HasValue)
                    cmd.Parameters.AddWithValue("@CzasWyjazdu", info.DataPrzyjazdu.Value);
                else
                    cmd.Parameters.AddWithValue("@CzasWyjazdu", DBNull.Value);

                cmd.Parameters.AddWithValue("@KtoWyslal", userId ?? "");
                cmd.Parameters.AddWithValue("@Status", status);
                cmd.Parameters.AddWithValue("@BladOpis", bladOpis ?? "");

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd zapisywania historii SMS: {ex.Message}");
            }
        }

        /// <summary>
        /// Tworzy tabelę historii SMS jeśli nie istnieje
        /// </summary>
        private async Task UtworzTabeleSmsHistoriaAsync(SqlConnection cn)
        {
            var checkSql = @"IF NOT EXISTS (SELECT * FROM sys.objects WHERE name='SmsSprzedazHistoria' AND type='U')
                             BEGIN
                                CREATE TABLE dbo.SmsSprzedazHistoria (
                                    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                                    ZamowienieId INT NOT NULL,
                                    KlientId INT NOT NULL,
                                    KlientNazwa NVARCHAR(200) NULL,
                                    Handlowiec NVARCHAR(100) NULL,
                                    TelefonHandlowca NVARCHAR(20) NULL,
                                    TrescSms NVARCHAR(500) NULL,
                                    IloscKg DECIMAL(18,2) NULL,
                                    CzasWyjazdu DATETIME NULL,
                                    DataWyslania DATETIME NOT NULL DEFAULT GETDATE(),
                                    KtoWyslal NVARCHAR(100) NULL,
                                    Status NVARCHAR(20) NOT NULL DEFAULT 'Wyslany',
                                    BladOpis NVARCHAR(500) NULL
                                );
                                CREATE INDEX IX_SmsSprzedaz_ZamowienieId ON dbo.SmsSprzedazHistoria(ZamowienieId);
                                CREATE INDEX IX_SmsSprzedaz_Handlowiec ON dbo.SmsSprzedazHistoria(Handlowiec);
                                CREATE INDEX IX_SmsSprzedaz_DataWyslania ON dbo.SmsSprzedazHistoria(DataWyslania);
                             END";

            await using var cmd = new SqlCommand(checkSql, cn);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Sprawdza czy SMS dla tego zamówienia został już wysłany
        /// </summary>
        public async Task<bool> CzySmsJuzWyslanyAsync(int zamowienieId)
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                var sql = @"SELECT COUNT(*) FROM dbo.SmsSprzedazHistoria
                            WHERE ZamowienieId = @ZamId AND Status IN ('Wyslany', 'Test')
                            AND CAST(DataWyslania AS DATE) = CAST(GETDATE() AS DATE)";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@ZamId", zamowienieId);

                var count = (int)await cmd.ExecuteScalarAsync();
                return count > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Sprawdza stan konta SMSAPI
        /// </summary>
        public async Task<SmsApiAccountInfo> SprawdzKontoSmsApiAsync()
        {
            var client = await GetSmsApiClientAsync();
            if (client == null)
            {
                return new SmsApiAccountInfo
                {
                    Sukces = false,
                    Blad = "SMSAPI nie skonfigurowane"
                };
            }

            return await client.SprawdzKontoAsync();
        }

        /// <summary>
        /// Pobiera historię SMS dla handlowca
        /// </summary>
        public async Task<List<SmsSprzedazHistoria>> PobierzHistorieHandlowcaAsync(string handlowiec, DateTime? dataOd = null, DateTime? dataDo = null)
        {
            var historia = new List<SmsSprzedazHistoria>();

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                var sql = @"SELECT Id, ZamowienieId, KlientId, KlientNazwa, Handlowiec,
                                   TelefonHandlowca, TrescSms, IloscKg, CzasWyjazdu,
                                   DataWyslania, KtoWyslal, Status, BladOpis
                            FROM dbo.SmsSprzedazHistoria
                            WHERE (@Handlowiec IS NULL OR @Handlowiec = '' OR Handlowiec = @Handlowiec)
                              AND (@DataOd IS NULL OR DataWyslania >= @DataOd)
                              AND (@DataDo IS NULL OR DataWyslania <= @DataDo)
                            ORDER BY DataWyslania DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Handlowiec", handlowiec ?? "");
                cmd.Parameters.AddWithValue("@DataOd", (object)dataOd ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DataDo", (object)dataDo ?? DBNull.Value);

                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    historia.Add(new SmsSprzedazHistoria
                    {
                        Id = rd.GetInt64(0),
                        ZamowienieId = rd.GetInt32(1),
                        KlientId = rd.GetInt32(2),
                        KlientNazwa = rd.IsDBNull(3) ? "" : rd.GetString(3),
                        Handlowiec = rd.IsDBNull(4) ? "" : rd.GetString(4),
                        TelefonHandlowca = rd.IsDBNull(5) ? "" : rd.GetString(5),
                        TrescSms = rd.IsDBNull(6) ? "" : rd.GetString(6),
                        IloscKg = rd.IsDBNull(7) ? 0 : rd.GetDecimal(7),
                        CzasWyjazdu = rd.IsDBNull(8) ? null : rd.GetDateTime(8),
                        DataWyslania = rd.GetDateTime(9),
                        KtoWyslal = rd.IsDBNull(10) ? "" : rd.GetString(10),
                        Status = rd.IsDBNull(11) ? "" : rd.GetString(11),
                        BladOpis = rd.IsDBNull(12) ? "" : rd.GetString(12)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania historii SMS: {ex.Message}");
            }

            return historia;
        }

        /// <summary>
        /// Pobiera listę wszystkich handlowców z ich konfiguracją SMS
        /// </summary>
        public async Task<List<HandlowiecSmsConfig>> PobierzKonfiguracjeHandlowcowAsync()
        {
            var konfiguracje = new List<HandlowiecSmsConfig>();

            try
            {
                await using var cnHandel = new SqlConnection(_connHandel);
                await cnHandel.OpenAsync();

                var sqlHandlowcy = @"SELECT DISTINCT CDim_Handlowiec_Val
                                     FROM SSCommon.ContractorClassification
                                     WHERE CDim_Handlowiec_Val IS NOT NULL
                                       AND CDim_Handlowiec_Val NOT IN ('', '(Brak)', 'Ogólne')
                                     ORDER BY CDim_Handlowiec_Val";

                await using var cmd = new SqlCommand(sqlHandlowcy, cnHandel);
                await using var rd = await cmd.ExecuteReaderAsync();

                while (await rd.ReadAsync())
                {
                    var nazwaHandlowca = rd.GetString(0);
                    var telefon = await PobierzTelefonHandlowcaAsync(nazwaHandlowca);

                    konfiguracje.Add(new HandlowiecSmsConfig
                    {
                        HandlowiecNazwa = nazwaHandlowca,
                        Telefon = telefon,
                        SmsAktywny = !string.IsNullOrEmpty(telefon),
                        SmsPoWydaniu = true
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania konfiguracji handlowców: {ex.Message}");
            }

            return konfiguracje;
        }
    }

    /// <summary>
    /// Konfiguracja SMSAPI.pl
    /// </summary>
    public class SmsApiConfig
    {
        public string ApiToken { get; set; } = "";
        public string NadawcaNazwa { get; set; } = "";
        public bool Aktywny { get; set; } = false;
        public bool TestMode { get; set; } = true;
    }
}
