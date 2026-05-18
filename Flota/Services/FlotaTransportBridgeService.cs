using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kalendarz1.Flota.Services
{
    /// <summary>
    /// Faza 6-B — Bridge spinający TransportPL.Kierowca/Pojazd z LibraNet.Driver/CarTrailer (Flota).
    ///
    /// Wymaga uruchomienia <c>Transport/SQL/alter_link_to_flota.sql</c> (Faza 6-A) —
    /// dodaje kolumny LibraNetDriverGID i LibraNetCarTrailerID do TransportPL.
    ///
    /// Konwencja CLAUDE.md: brak cross-DB JOIN, ładujemy 2 listy osobno i łączymy w .NET.
    /// </summary>
    public class FlotaTransportBridgeService
    {
        private static readonly string _connTransport =
            "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private static readonly string _connLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // ════════════════════════════════════════════════════════════════════
        // Modele
        // ════════════════════════════════════════════════════════════════════

        public class TransportPojazd
        {
            public int PojazdID { get; set; }
            public string Rejestracja { get; set; } = "";
            public string? Marka { get; set; }
            public string? Model { get; set; }
            public int PaletyH1 { get; set; }
            public bool Aktywny { get; set; }
            public string? LibraNetCarTrailerID { get; set; }
            public string Display => string.IsNullOrEmpty(Marka)
                ? Rejestracja
                : $"{Marka} {Model} ({Rejestracja})";
        }

        public class TransportKierowca
        {
            public int KierowcaID { get; set; }
            public string Imie { get; set; } = "";
            public string Nazwisko { get; set; } = "";
            public string? Telefon { get; set; }
            public bool Aktywny { get; set; }
            public int? LibraNetDriverGID { get; set; }
            public string Display => $"{Imie} {Nazwisko}";
        }

        public class FlotaPojazd
        {
            public string ID { get; set; } = "";
            public string? Kind { get; set; }
            public string? Brand { get; set; }
            public string? Model { get; set; }
            public string? Registration { get; set; }
            public string Display => $"{Brand} {Model} ({Registration ?? ID})";
        }

        public class FlotaKierowca
        {
            public int GID { get; set; }
            public string Name { get; set; } = "";
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public bool Halt { get; set; }
            public string Display => string.IsNullOrEmpty(FirstName)
                ? Name
                : $"{FirstName} {LastName} (GID {GID})";
        }

        // ════════════════════════════════════════════════════════════════════
        // TRANSPORT side (TransportPL)
        // ════════════════════════════════════════════════════════════════════

        public async Task<List<TransportPojazd>> GetTransportPojazdyAsync()
        {
            var list = new List<TransportPojazd>();
            using var conn = new SqlConnection(_connTransport);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT PojazdID, Rejestracja, Marka, Model, PaletyH1, Aktywny, LibraNetCarTrailerID
                FROM Pojazd
                ORDER BY Aktywny DESC, Rejestracja";
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new TransportPojazd
                {
                    PojazdID = r.GetInt32(0),
                    Rejestracja = r.IsDBNull(1) ? "" : r.GetString(1),
                    Marka = r.IsDBNull(2) ? null : r.GetString(2),
                    Model = r.IsDBNull(3) ? null : r.GetString(3),
                    PaletyH1 = r.IsDBNull(4) ? 33 : r.GetInt32(4),
                    Aktywny = !r.IsDBNull(5) && r.GetBoolean(5),
                    LibraNetCarTrailerID = r.IsDBNull(6) ? null : r.GetString(6)
                });
            }
            return list;
        }

        public async Task<List<TransportKierowca>> GetTransportKierowcyAsync()
        {
            var list = new List<TransportKierowca>();
            using var conn = new SqlConnection(_connTransport);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT KierowcaID, Imie, Nazwisko, Telefon, Aktywny, LibraNetDriverGID
                FROM Kierowca
                ORDER BY Aktywny DESC, Nazwisko, Imie";
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new TransportKierowca
                {
                    KierowcaID = r.GetInt32(0),
                    Imie = r.IsDBNull(1) ? "" : r.GetString(1),
                    Nazwisko = r.IsDBNull(2) ? "" : r.GetString(2),
                    Telefon = r.IsDBNull(3) ? null : r.GetString(3),
                    Aktywny = !r.IsDBNull(4) && r.GetBoolean(4),
                    LibraNetDriverGID = r.IsDBNull(5) ? null : r.GetInt32(5)
                });
            }
            return list;
        }

        // ════════════════════════════════════════════════════════════════════
        // FLOTA side (LibraNet)
        // ════════════════════════════════════════════════════════════════════

        public async Task<List<FlotaPojazd>> GetFlotaPojazdyAsync()
        {
            var list = new List<FlotaPojazd>();
            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT ct.ID, ct.Kind, ct.Brand, ct.Model, vd.Registration
                FROM CarTrailer ct
                LEFT JOIN VehicleDetails vd ON ct.ID = vd.CarTrailerID
                ORDER BY ct.Kind, ct.ID";
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                // Defensive: CarTrailer.ID może być int LUB varchar w istniejącej bazie LibraNet
                list.Add(new FlotaPojazd
                {
                    ID = SafeStr(r, 0),
                    Kind = SafeNullStr(r, 1),
                    Brand = SafeNullStr(r, 2),
                    Model = SafeNullStr(r, 3),
                    Registration = SafeNullStr(r, 4)
                });
            }
            return list;
        }

        public async Task<List<FlotaKierowca>> GetFlotaKierowcyAsync()
        {
            var list = new List<FlotaKierowca>();
            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT d.GID, d.Name, d.Halt, dd.FirstName, dd.LastName
                FROM Driver d
                LEFT JOIN DriverDetails dd ON d.GID = dd.DriverGID
                WHERE ISNULL(d.Deleted, 0) = 0
                ORDER BY d.Halt ASC, d.Name ASC";
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new FlotaKierowca
                {
                    GID = Convert.ToInt32(r.GetValue(0)),
                    Name = SafeStr(r, 1),
                    Halt = SafeBool(r, 2),
                    FirstName = SafeNullStr(r, 3),
                    LastName = SafeNullStr(r, 4)
                });
            }
            return list;
        }

        // ════════════════════════════════════════════════════════════════════
        // Defensive type readers — tolerują różne typy w istniejących tabelach LibraNet
        // ════════════════════════════════════════════════════════════════════
        private static string SafeStr(System.Data.IDataReader r, int i)
            => r.IsDBNull(i) ? "" : (Convert.ToString(r.GetValue(i)) ?? "");

        private static string? SafeNullStr(System.Data.IDataReader r, int i)
            => r.IsDBNull(i) ? null : Convert.ToString(r.GetValue(i));

        private static bool SafeBool(System.Data.IDataReader r, int i)
        {
            if (r.IsDBNull(i)) return false;
            var v = r.GetValue(i);
            return v switch
            {
                bool b => b,
                int n => n != 0,
                short s => s != 0,
                byte b => b != 0,
                long l => l != 0,
                string s => s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase),
                _ => Convert.ToBoolean(v)
            };
        }

        // ════════════════════════════════════════════════════════════════════
        // SAVE mapping
        // ════════════════════════════════════════════════════════════════════

        public async Task SaveMappingPojazdAsync(int pojazdId, string? carTrailerID)
        {
            using var conn = new SqlConnection(_connTransport);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Pojazd SET LibraNetCarTrailerID = @ct WHERE PojazdID = @id";
            cmd.Parameters.AddWithValue("@id", pojazdId);
            cmd.Parameters.AddWithValue("@ct", (object?)carTrailerID ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task SaveMappingKierowcaAsync(int kierowcaId, int? driverGID)
        {
            using var conn = new SqlConnection(_connTransport);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Kierowca SET LibraNetDriverGID = @gid WHERE KierowcaID = @id";
            cmd.Parameters.AddWithValue("@id", kierowcaId);
            cmd.Parameters.AddWithValue("@gid", (object?)driverGID ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        // ════════════════════════════════════════════════════════════════════
        // AUTO-MAP (po rejestracji / imię+nazwisko)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Auto-mapuje TransportPL.Pojazd → LibraNet.CarTrailer po polu Rejestracja.
        /// Match case-insensitive po znormalizowanej rejestracji (bez spacji, upper).
        /// Mapuje tylko Transport.Pojazd które jeszcze nie mają LibraNetCarTrailerID.
        /// </summary>
        public async Task<int> AutoMapPojazdyByRegistrationAsync()
        {
            var transports = await GetTransportPojazdyAsync();
            var flotas = await GetFlotaPojazdyAsync();

            string Normalize(string? s) => (s ?? "").Replace(" ", "").Replace("-", "").ToUpperInvariant();

            var flotaByReg = flotas
                .Where(f => !string.IsNullOrWhiteSpace(f.Registration))
                .GroupBy(f => Normalize(f.Registration))
                .ToDictionary(g => g.Key, g => g.First());

            int mapped = 0;
            foreach (var t in transports.Where(t => t.LibraNetCarTrailerID == null))
            {
                var key = Normalize(t.Rejestracja);
                if (flotaByReg.TryGetValue(key, out var f))
                {
                    await SaveMappingPojazdAsync(t.PojazdID, f.ID);
                    mapped++;
                }
            }
            return mapped;
        }

        /// <summary>
        /// Auto-mapuje TransportPL.Kierowca → LibraNet.Driver po Imię + Nazwisko.
        /// Match case-insensitive. Mapuje tylko Transport.Kierowca bez LibraNetDriverGID.
        /// </summary>
        public async Task<int> AutoMapKierowcyByNameAsync()
        {
            var transports = await GetTransportKierowcyAsync();
            var flotas = await GetFlotaKierowcyAsync();

            string Key(string? f, string? l) =>
                $"{(f ?? "").Trim().ToUpperInvariant()}|{(l ?? "").Trim().ToUpperInvariant()}";

            // Klucz z FirstName+LastName (DriverDetails) — jeśli puste fallback do Driver.Name parsowanego
            var flotaByName = new Dictionary<string, FlotaKierowca>();
            foreach (var f in flotas)
            {
                string fn = f.FirstName ?? "";
                string ln = f.LastName ?? "";
                if (string.IsNullOrWhiteSpace(fn) && string.IsNullOrWhiteSpace(ln))
                {
                    // Fallback: split Driver.Name "Imie Nazwisko"
                    var parts = (f.Name ?? "").Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1) fn = parts[0];
                    if (parts.Length >= 2) ln = parts[1];
                }
                var k = Key(fn, ln);
                if (!flotaByName.ContainsKey(k))
                    flotaByName[k] = f;
            }

            int mapped = 0;
            foreach (var t in transports.Where(t => t.LibraNetDriverGID == null))
            {
                var k = Key(t.Imie, t.Nazwisko);
                if (flotaByName.TryGetValue(k, out var f))
                {
                    await SaveMappingKierowcaAsync(t.KierowcaID, f.GID);
                    mapped++;
                }
            }
            return mapped;
        }
    }
}
