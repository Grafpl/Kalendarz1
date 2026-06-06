// ════════════════════════════════════════════════════════════════════════════
// Transport/Services/CzasRozladunkuService.cs
//
// Per-klient czas rozładunku (minuty) konfigurowany w karcie odbiorcy.
// Przechowywane w LibraNet.KartotekaOdbiorcyDane.CzasRozladunkuMin (INT NULL).
// NULL = użyj domyślnej wartości (DefaultMin = 30).
//
// Używane przez EtaService.Calculate (szacowanie powrotu) i UI karty odbiorcy.
//
// Migracja kolumny działa lazy — przy pierwszym wywołaniu sprawdza i ALTER-uje
// tabelę, jeśli kolumny brak (bezpieczne dla istniejących instalacji).
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Transport.Services
{
    public class CzasRozladunkuService
    {
        public const int DefaultMin = 30;
        public const int MinMin = 5;
        public const int MaxMin = 240;

        private readonly string _connLibra;
        private static bool _columnEnsured;
        private static readonly object _lock = new();

        private static readonly string _connLibraDefault =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public CzasRozladunkuService() : this(_connLibraDefault) { }
        public CzasRozladunkuService(string connLibra) { _connLibra = connLibra; }

        /// <summary>Auto-ALTER tabeli przy pierwszym wywołaniu w sesji. Idempotentne.</summary>
        public async Task EnsureColumnAsync()
        {
            lock (_lock) { if (_columnEnsured) return; }

            const string sql = @"
                IF NOT EXISTS (SELECT * FROM sys.columns
                               WHERE object_id = OBJECT_ID('dbo.KartotekaOdbiorcyDane')
                                 AND name = 'CzasRozladunkuMin')
                    ALTER TABLE dbo.KartotekaOdbiorcyDane ADD CzasRozladunkuMin INT NULL;";

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                await cmd.ExecuteNonQueryAsync();
                lock (_lock) { _columnEnsured = true; }
            }
            catch (Exception ex) { Debug.WriteLine($"[CzasRozladunku.Ensure] {ex.Message}"); }
        }

        /// <summary>
        /// Czas rozładunku per klient (klientId → minuty). Klienci bez ustawienia w bazie
        /// dostają DefaultMin (30 min). Lista wejściowa może być pusta — zwracamy pusty dict.
        /// </summary>
        public async Task<Dictionary<int, int>> PobierzCzasyAsync(IEnumerable<int> klientIds)
        {
            var lista = new HashSet<int>(klientIds);
            var wynik = new Dictionary<int, int>();
            if (lista.Count == 0) return wynik;

            await EnsureColumnAsync();

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = cn.CreateCommand();

                // Zbuduj parametry @id0, @id1 ... — bezpieczniej niż IN z stringiem
                var parametry = new List<string>();
                int idx = 0;
                foreach (var id in lista)
                {
                    var p = $"@id{idx++}";
                    parametry.Add(p);
                    cmd.Parameters.AddWithValue(p, id);
                }
                // Hierarchia źródeł (priorytet po lewej):
                //   1. KartotekaOdbiorcyDane.CzasRozladunkuMin — ręcznie ustawione przez planistę
                //   2. EstymacjeRozladunku.MinutyMediana — gdy LiczbaProb >= MinProbDoZaufania (3)
                //   3. fallback DefaultMin (30) — domyślne, wypełniane na koniec
                cmd.CommandText = $@"
                    SELECT
                        kod.IdSymfonia,
                        COALESCE(
                            kod.CzasRozladunkuMin,
                            CASE WHEN er.LiczbaProb >= {HistoriaRozladunkuService.MinProbDoZaufania}
                                 THEN er.MinutyMediana ELSE NULL END
                        ) AS Min
                    FROM dbo.KartotekaOdbiorcyDane kod
                    LEFT JOIN dbo.EstymacjeRozladunku er ON er.KlientId = kod.IdSymfonia
                    WHERE kod.IdSymfonia IN ({string.Join(",", parametry)})";

                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int id = rd.GetInt32(0);
                    if (rd.IsDBNull(1)) continue;   // fallback do default w pętli niżej
                    int min = rd.GetInt32(1);
                    if (min >= MinMin && min <= MaxMin)
                        wynik[id] = min;
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[CzasRozladunku.Pobierz] {ex.Message}"); }

            // Wypełnij brakujące defaultem żeby konsument nie musiał sprawdzać TryGetValue
            foreach (var id in lista)
                if (!wynik.ContainsKey(id)) wynik[id] = DefaultMin;

            return wynik;
        }

        /// <summary>
        /// Mapowanie zamówień (ZAM_id) → (KlientId, MinutyRozladunku).
        /// Jedno query JOIN: ZamowieniaMieso → KartotekaOdbiorcyDane. Wygodne dla edytora kursu,
        /// gdzie ładunki są kodowane jako ZAM_id i potrzeba spersonalizowanego czasu rozładunku.
        /// Klienci bez ustawienia w bazie → DefaultMin.
        /// </summary>
        public async Task<Dictionary<int, (int KlientId, int RozladunekMin)>> PobierzCzasyDlaZamowienAsync(IEnumerable<int> zamIds)
        {
            var lista = new HashSet<int>(zamIds);
            var wynik = new Dictionary<int, (int, int)>();
            if (lista.Count == 0) return wynik;

            await EnsureColumnAsync();

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = cn.CreateCommand();
                var parametry = new List<string>();
                int idx = 0;
                foreach (var id in lista)
                {
                    var p = $"@id{idx++}";
                    parametry.Add(p);
                    cmd.Parameters.AddWithValue(p, id);
                }
                // Hierarchia: ręczne (karta) > historia (mediana z Webfleet) > default 30
                cmd.CommandText = $@"
                    SELECT zm.Id,
                           zm.KlientId,
                           COALESCE(
                               kod.CzasRozladunkuMin,
                               CASE WHEN er.LiczbaProb >= {HistoriaRozladunkuService.MinProbDoZaufania}
                                    THEN er.MinutyMediana ELSE NULL END,
                               {DefaultMin}
                           ) AS Minuty
                    FROM dbo.ZamowieniaMieso zm
                    LEFT JOIN dbo.KartotekaOdbiorcyDane kod ON kod.IdSymfonia = zm.KlientId
                    LEFT JOIN dbo.EstymacjeRozladunku er ON er.KlientId = zm.KlientId
                    WHERE zm.Id IN ({string.Join(",", parametry)})";

                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int zid = rd.GetInt32(0);
                    if (rd.IsDBNull(1)) continue;
                    // KlientId zawsze INT, ale bezpiecznie przez Convert (na wypadek schema)
                    int kid = Convert.ToInt32(rd.GetValue(1));
                    int min = rd.GetInt32(2);
                    if (min < MinMin) min = MinMin;
                    if (min > MaxMin) min = MaxMin;
                    if (kid > 0) wynik[zid] = (kid, min);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[CzasRozladunku.DlaZamowien] {ex.Message}"); }

            return wynik;
        }

        /// <summary>
        /// Zwraca czas rozładunku dla jednego klienta (lub null gdy brak konfiguracji).
        /// Używane przez OdbiorcaEdycjaWindow do wyświetlenia/edycji wartości.
        /// </summary>
        public async Task<int?> PobierzDlaKlientaAsync(int idSymfonia)
        {
            await EnsureColumnAsync();
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(
                    "SELECT CzasRozladunkuMin FROM dbo.KartotekaOdbiorcyDane WHERE IdSymfonia = @id", cn);
                cmd.Parameters.AddWithValue("@id", idSymfonia);
                var r = await cmd.ExecuteScalarAsync();
                if (r == null || r == DBNull.Value) return null;
                return Convert.ToInt32(r);
            }
            catch (Exception ex) { Debug.WriteLine($"[CzasRozladunku.PobierzKlient {idSymfonia}] {ex.Message}"); return null; }
        }

        /// <summary>
        /// UPSERT — czas rozładunku per klient. null = wyczyść (klient użyje DefaultMin).
        /// </summary>
        public async Task ZapiszDlaKlientaAsync(int idSymfonia, int? czasMin)
        {
            await EnsureColumnAsync();

            // Walidacja zakresu
            if (czasMin.HasValue && (czasMin.Value < MinMin || czasMin.Value > MaxMin))
                throw new ArgumentOutOfRangeException(nameof(czasMin), $"Czas {czasMin} poza zakresem [{MinMin},{MaxMin}] min.");

            const string sql = @"
                IF EXISTS (SELECT 1 FROM dbo.KartotekaOdbiorcyDane WHERE IdSymfonia = @id)
                    UPDATE dbo.KartotekaOdbiorcyDane SET CzasRozladunkuMin = @czas WHERE IdSymfonia = @id;
                ELSE
                    INSERT INTO dbo.KartotekaOdbiorcyDane (IdSymfonia, CzasRozladunkuMin)
                    VALUES (@id, @czas);";

            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@id", idSymfonia);
            cmd.Parameters.AddWithValue("@czas", (object?)czasMin ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
