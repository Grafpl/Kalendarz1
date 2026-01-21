using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Kalendarz1.Spotkania.Models;

namespace Kalendarz1.Spotkania.Services
{
    /// <summary>
    /// Serwis zarządzający powiadomieniami o spotkaniach
    /// </summary>
    public class NotyfikacjeService : IDisposable
    {
        private const string CONNECTION_STRING = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova";

        private readonly string _operatorID;
        private readonly DispatcherTimer _timer;
        private readonly NotyfikacjeLicznik _licznik;

        private bool _isDisposed;
        private DateTime _ostatnieSprawdzenie = DateTime.MinValue;

        /// <summary>
        /// Event wywoływany gdy pojawią się nowe powiadomienia
        /// </summary>
        public event EventHandler<List<NotyfikacjaModel>>? NoweNotyfikacje;

        /// <summary>
        /// Event wywoływany gdy zmieni się licznik nieprzeczytanych
        /// </summary>
        public event EventHandler<NotyfikacjeLicznik>? ZmianaLicznika;

        /// <summary>
        /// Event wywoływany gdy spotkanie jest za chwilę (5 min)
        /// </summary>
        public event EventHandler<NotyfikacjaModel>? SpotkanieZaChwile;

        public NotyfikacjeLicznik Licznik => _licznik;

        public NotyfikacjeService(string operatorID)
        {
            _operatorID = operatorID;
            _licznik = new NotyfikacjeLicznik();

            // Timer sprawdzający co minutę
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(1)
            };
            _timer.Tick += Timer_Tick;
        }

        #region Zarządzanie timerem

        /// <summary>
        /// Uruchamia automatyczne sprawdzanie powiadomień
        /// </summary>
        public void Start()
        {
            if (_isDisposed) return;

            // Pierwsze sprawdzenie od razu
            _ = SprawdzPowiadomieniaAsync();

            _timer.Start();
        }

        /// <summary>
        /// Zatrzymuje automatyczne sprawdzanie
        /// </summary>
        public void Stop()
        {
            _timer.Stop();
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            await SprawdzPowiadomieniaAsync();
        }

        #endregion

        #region Sprawdzanie powiadomień

        /// <summary>
        /// Sprawdza nowe powiadomienia i tworzy przypomnienia
        /// </summary>
        public async Task SprawdzPowiadomieniaAsync()
        {
            try
            {
                // 1. Utwórz nowe przypomnienia (procedura SQL)
                await UtworzPrzypomnienia();

                // 2. Pobierz nieprzeczytane powiadomienia
                var powiadomienia = await PobierzNieprzeczytane();

                // 3. Aktualizuj licznik
                var stareLiczba = _licznik.Nieprzeczytane;
                _licznik.Nieprzeczytane = powiadomienia.Count;
                _licznik.Pilne = powiadomienia.FindAll(p => p.CzyPilne).Count;

                // 4. Sprawdź czy są nowe
                if (powiadomienia.Count > stareLiczba && _ostatnieSprawdzenie != DateTime.MinValue)
                {
                    _licznik.MaNoweNotyfikacje = true;

                    // Znajdź nowe powiadomienia
                    var nowe = powiadomienia.FindAll(p => p.DataUtworzenia > _ostatnieSprawdzenie);
                    if (nowe.Count > 0)
                    {
                        NoweNotyfikacje?.Invoke(this, nowe);

                        // Sprawdź pilne (za 5 min lub mniej)
                        foreach (var p in nowe)
                        {
                            if (p.TypNotyfikacji == TypNotyfikacji.Przypomnienie5m ||
                                (p.SpotkanieDataSpotkania.HasValue &&
                                 (p.SpotkanieDataSpotkania.Value - DateTime.Now).TotalMinutes <= 5))
                            {
                                SpotkanieZaChwile?.Invoke(this, p);
                            }
                        }
                    }
                }

                _ostatnieSprawdzenie = DateTime.Now;
                ZmianaLicznika?.Invoke(this, _licznik);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd sprawdzania powiadomień: {ex.Message}");
            }
        }

        /// <summary>
        /// Wywołuje procedurę tworzącą przypomnienia
        /// </summary>
        private async Task UtworzPrzypomnienia()
        {
            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                using var cmd = new SqlCommand("sp_UtworzPrzypomnienia", conn);
                cmd.CommandType = System.Data.CommandType.StoredProcedure;

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd tworzenia przypomnień: {ex.Message}");
            }
        }

        #endregion

        #region Operacje na powiadomieniach

        /// <summary>
        /// Pobiera nieprzeczytane powiadomienia (tylko przyszłe spotkania)
        /// </summary>
        public async Task<List<NotyfikacjaModel>> PobierzNieprzeczytane()
        {
            var lista = new List<NotyfikacjaModel>();

            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                using var cmd = new SqlCommand("sp_PobierzNieprzeczytaneNotyfikacje", conn);
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@OperatorID", _operatorID);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var notyfikacja = new NotyfikacjaModel
                    {
                        NotyfikacjaID = reader.GetInt64(reader.GetOrdinal("NotyfikacjaID")),
                        SpotkaniID = reader.GetInt64(reader.GetOrdinal("SpotkaniID")),
                        TypNotyfikacji = ParseTypNotyfikacji(reader.GetString(reader.GetOrdinal("TypNotyfikacji"))),
                        Tytul = reader.IsDBNull(reader.GetOrdinal("Tytul")) ? null : reader.GetString(reader.GetOrdinal("Tytul")),
                        Tresc = reader.IsDBNull(reader.GetOrdinal("Tresc")) ? null : reader.GetString(reader.GetOrdinal("Tresc")),
                        CzyPrzeczytana = reader.GetBoolean(reader.GetOrdinal("CzyPrzeczytana")),
                        DataUtworzenia = reader.GetDateTime(reader.GetOrdinal("DataUtworzenia")),
                        OperatorID = _operatorID
                    };

                    if (!reader.IsDBNull(reader.GetOrdinal("SpotkanieDataSpotkania")))
                        notyfikacja.SpotkanieDataSpotkania = reader.GetDateTime(reader.GetOrdinal("SpotkanieDataSpotkania"));

                    if (!reader.IsDBNull(reader.GetOrdinal("SpotkanieTytul")))
                        notyfikacja.SpotkanieTytul = reader.GetString(reader.GetOrdinal("SpotkanieTytul"));

                    if (!reader.IsDBNull(reader.GetOrdinal("LinkAkcji")))
                        notyfikacja.LinkAkcji = reader.GetString(reader.GetOrdinal("LinkAkcji"));

                    if (!reader.IsDBNull(reader.GetOrdinal("LinkSpotkania")))
                        notyfikacja.LinkSpotkania = reader.GetString(reader.GetOrdinal("LinkSpotkania"));

                    if (!reader.IsDBNull(reader.GetOrdinal("Lokalizacja")))
                        notyfikacja.Lokalizacja = reader.GetString(reader.GetOrdinal("Lokalizacja"));

                    notyfikacja.MinutyDoSpotkania = reader.GetInt32(reader.GetOrdinal("MinutyDoSpotkania"));

                    // Filtruj - pokaż tylko przyszłe spotkania
                    if (!notyfikacja.SpotkanieDataSpotkania.HasValue ||
                        notyfikacja.SpotkanieDataSpotkania.Value > DateTime.Now)
                    {
                        lista.Add(notyfikacja);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania powiadomień: {ex.Message}");
            }

            // Sortuj po dacie spotkania (najbliższe najpierw)
            return lista.OrderBy(n => n.SpotkanieDataSpotkania ?? DateTime.MaxValue).ToList();
        }

        /// <summary>
        /// Pobiera wszystkie powiadomienia (również przeczytane)
        /// </summary>
        public async Task<List<NotyfikacjaModel>> PobierzWszystkie(int limit = 50)
        {
            var lista = new List<NotyfikacjaModel>();

            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                string sql = @"
                    SELECT TOP (@Limit)
                        n.*, s.LinkSpotkania, s.Lokalizacja,
                        DATEDIFF(MINUTE, GETDATE(), n.SpotkanieDataSpotkania) AS MinutyDoSpotkania
                    FROM SpotkaniaNotyfikacje n
                    LEFT JOIN Spotkania s ON n.SpotkaniID = s.SpotkaniID
                    WHERE n.OperatorID = @OperatorID
                    ORDER BY n.DataUtworzenia DESC";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@OperatorID", _operatorID);
                cmd.Parameters.AddWithValue("@Limit", limit);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var notyfikacja = new NotyfikacjaModel
                    {
                        NotyfikacjaID = reader.GetInt64(reader.GetOrdinal("NotyfikacjaID")),
                        SpotkaniID = reader.GetInt64(reader.GetOrdinal("SpotkaniID")),
                        TypNotyfikacji = ParseTypNotyfikacji(reader.GetString(reader.GetOrdinal("TypNotyfikacji"))),
                        Tytul = reader.IsDBNull(reader.GetOrdinal("Tytul")) ? null : reader.GetString(reader.GetOrdinal("Tytul")),
                        Tresc = reader.IsDBNull(reader.GetOrdinal("Tresc")) ? null : reader.GetString(reader.GetOrdinal("Tresc")),
                        CzyPrzeczytana = reader.GetBoolean(reader.GetOrdinal("CzyPrzeczytana")),
                        DataUtworzenia = reader.GetDateTime(reader.GetOrdinal("DataUtworzenia")),
                        OperatorID = _operatorID
                    };

                    if (!reader.IsDBNull(reader.GetOrdinal("SpotkanieDataSpotkania")))
                        notyfikacja.SpotkanieDataSpotkania = reader.GetDateTime(reader.GetOrdinal("SpotkanieDataSpotkania"));

                    if (!reader.IsDBNull(reader.GetOrdinal("SpotkanieTytul")))
                        notyfikacja.SpotkanieTytul = reader.GetString(reader.GetOrdinal("SpotkanieTytul"));

                    if (!reader.IsDBNull(reader.GetOrdinal("DataPrzeczytania")))
                        notyfikacja.DataPrzeczytania = reader.GetDateTime(reader.GetOrdinal("DataPrzeczytania"));

                    if (!reader.IsDBNull(reader.GetOrdinal("LinkSpotkania")))
                        notyfikacja.LinkSpotkania = reader.GetString(reader.GetOrdinal("LinkSpotkania"));

                    if (!reader.IsDBNull(reader.GetOrdinal("Lokalizacja")))
                        notyfikacja.Lokalizacja = reader.GetString(reader.GetOrdinal("Lokalizacja"));

                    lista.Add(notyfikacja);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania wszystkich powiadomień: {ex.Message}");
            }

            return lista;
        }

        /// <summary>
        /// Oznacza powiadomienie jako przeczytane
        /// </summary>
        public async Task OznaczJakoPrzeczytane(long? notyfikacjaId = null)
        {
            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                using var cmd = new SqlCommand("sp_OznaczNotyfikacjePrzeczytane", conn);
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@OperatorID", _operatorID);
                cmd.Parameters.AddWithValue("@NotyfikacjaID", (object?)notyfikacjaId ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync();

                // Odśwież licznik
                await SprawdzPowiadomieniaAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd oznaczania powiadomień: {ex.Message}");
            }
        }

        /// <summary>
        /// Tworzy powiadomienie o zaproszeniu na spotkanie
        /// </summary>
        public async Task UtworzZaproszenie(long spotkaniId, string tytul, DateTime dataSpotkania,
            List<string> uczestnicyIds, string organizatorNazwa)
        {
            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                string sql = @"
                    INSERT INTO SpotkaniaNotyfikacje
                        (SpotkaniID, OperatorID, TypNotyfikacji, Tytul, Tresc,
                         SpotkanieDataSpotkania, SpotkanieTytul, DataWygasniecia)
                    VALUES
                        (@SpotkaniID, @OperatorID, 'Zaproszenie', @Tytul, @Tresc,
                         @DataSpotkania, @SpotkanieTytul, @DataWygasniecia)";

                foreach (var uczestnikId in uczestnicyIds)
                {
                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@SpotkaniID", spotkaniId);
                    cmd.Parameters.AddWithValue("@OperatorID", uczestnikId);
                    cmd.Parameters.AddWithValue("@Tytul", $"Zaproszenie na spotkanie: {tytul}");
                    cmd.Parameters.AddWithValue("@Tresc",
                        $"{organizatorNazwa} zaprasza Cię na spotkanie \"{tytul}\" w dniu {dataSpotkania:dd.MM.yyyy} o godzinie {dataSpotkania:HH:mm}.");
                    cmd.Parameters.AddWithValue("@DataSpotkania", dataSpotkania);
                    cmd.Parameters.AddWithValue("@SpotkanieTytul", tytul);
                    cmd.Parameters.AddWithValue("@DataWygasniecia", dataSpotkania);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd tworzenia zaproszeń: {ex.Message}");
            }
        }

        /// <summary>
        /// Tworzy powiadomienie o zmianie spotkania
        /// </summary>
        public async Task UtworzPowiadomienieZmiany(long spotkaniId, string tytul, DateTime dataSpotkania,
            List<string> uczestnicyIds, string opis)
        {
            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                string sql = @"
                    INSERT INTO SpotkaniaNotyfikacje
                        (SpotkaniID, OperatorID, TypNotyfikacji, Tytul, Tresc,
                         SpotkanieDataSpotkania, SpotkanieTytul, DataWygasniecia)
                    VALUES
                        (@SpotkaniID, @OperatorID, 'Zmiana', @Tytul, @Tresc,
                         @DataSpotkania, @SpotkanieTytul, @DataWygasniecia)";

                foreach (var uczestnikId in uczestnicyIds)
                {
                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@SpotkaniID", spotkaniId);
                    cmd.Parameters.AddWithValue("@OperatorID", uczestnikId);
                    cmd.Parameters.AddWithValue("@Tytul", $"Zmiana w spotkaniu: {tytul}");
                    cmd.Parameters.AddWithValue("@Tresc", opis);
                    cmd.Parameters.AddWithValue("@DataSpotkania", dataSpotkania);
                    cmd.Parameters.AddWithValue("@SpotkanieTytul", tytul);
                    cmd.Parameters.AddWithValue("@DataWygasniecia", dataSpotkania);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd tworzenia powiadomień o zmianie: {ex.Message}");
            }
        }

        /// <summary>
        /// Tworzy powiadomienie o anulowaniu spotkania
        /// </summary>
        public async Task UtworzPowiadomienieAnulowania(long spotkaniId, string tytul, DateTime dataSpotkania,
            List<string> uczestnicyIds, string powod)
        {
            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                string sql = @"
                    INSERT INTO SpotkaniaNotyfikacje
                        (SpotkaniID, OperatorID, TypNotyfikacji, Tytul, Tresc,
                         SpotkanieDataSpotkania, SpotkanieTytul)
                    VALUES
                        (@SpotkaniID, @OperatorID, 'Anulowanie', @Tytul, @Tresc,
                         @DataSpotkania, @SpotkanieTytul)";

                foreach (var uczestnikId in uczestnicyIds)
                {
                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@SpotkaniID", spotkaniId);
                    cmd.Parameters.AddWithValue("@OperatorID", uczestnikId);
                    cmd.Parameters.AddWithValue("@Tytul", $"Spotkanie anulowane: {tytul}");
                    cmd.Parameters.AddWithValue("@Tresc",
                        $"Spotkanie \"{tytul}\" zaplanowane na {dataSpotkania:dd.MM.yyyy HH:mm} zostało anulowane." +
                        (string.IsNullOrWhiteSpace(powod) ? "" : $" Powód: {powod}"));
                    cmd.Parameters.AddWithValue("@DataSpotkania", dataSpotkania);
                    cmd.Parameters.AddWithValue("@SpotkanieTytul", tytul);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd tworzenia powiadomień o anulowaniu: {ex.Message}");
            }
        }

        #endregion

        #region Pomocnicze

        private TypNotyfikacji ParseTypNotyfikacji(string value)
        {
            return value switch
            {
                "Zaproszenie" => TypNotyfikacji.Zaproszenie,
                "Przypomnienie24h" => TypNotyfikacji.Przypomnienie24h,
                "Przypomnienie1h" => TypNotyfikacji.Przypomnienie1h,
                "Przypomnienie15m" => TypNotyfikacji.Przypomnienie15m,
                "Przypomnienie5m" => TypNotyfikacji.Przypomnienie5m,
                "Zmiana" => TypNotyfikacji.Zmiana,
                "Anulowanie" => TypNotyfikacji.Anulowanie,
                "AkceptacjaZaproszenia" => TypNotyfikacji.AkceptacjaZaproszenia,
                "OdrzucenieZaproszenia" => TypNotyfikacji.OdrzucenieZaproszenia,
                "NowaTranskrypcja" => TypNotyfikacji.NowaTranskrypcja,
                "NowaNotatka" => TypNotyfikacji.NowaNotatka,
                _ => TypNotyfikacji.Zaproszenie
            };
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _timer.Stop();
            _timer.Tick -= Timer_Tick;
        }

        #endregion
    }

    /// <summary>
    /// Globalny manager powiadomień dla całej aplikacji
    /// </summary>
    public static class NotyfikacjeManager
    {
        private static NotyfikacjeService? _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Pobiera lub tworzy instancję serwisu powiadomień
        /// </summary>
        public static NotyfikacjeService GetInstance(string operatorID)
        {
            lock (_lock)
            {
                if (_instance == null || _instance.Licznik == null)
                {
                    _instance = new NotyfikacjeService(operatorID);
                }
                return _instance;
            }
        }

        /// <summary>
        /// Zatrzymuje i zwalnia serwis
        /// </summary>
        public static void Shutdown()
        {
            lock (_lock)
            {
                _instance?.Dispose();
                _instance = null;
            }
        }

        /// <summary>
        /// Sprawdza czy serwis jest uruchomiony
        /// </summary>
        public static bool IsRunning => _instance != null;
    }
}
