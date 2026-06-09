using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Zamowienia.Services
{
    /// <summary>
    /// Warianty wewnętrzne towaru (np. Filet A → "Pojedynczy" / "Podwójny").
    /// W Symfonii zostaje JEDEN artykuł (KodTowaru = HM.TW.id) — wariant jest atrybutem
    /// pozycji zamówienia (kolumna ZamowieniaMiesoTowar.Wariant), widocznym dla produkcji i magazynu.
    /// Słownik dbo.TowarWarianty mówi, które towary mają warianty i jakie — bez zmian w kodzie.
    /// </summary>
    public class TowarWariantyService
    {
        private readonly string _connLibra;
        public TowarWariantyService(string connLibra) { _connLibra = connLibra; }

        public class Wariant
        {
            public int KodTowaru { get; set; }
            public string Kod { get; set; } = "";     // techniczny, np. "POJEDYNCZY"
            public string Nazwa { get; set; } = "";    // wyświetlany, np. "Filet pojedynczy"
            public int Lp { get; set; }
        }

        private static readonly System.Threading.SemaphoreSlim _initLock = new(1, 1);
        private static volatile bool _initialized;

        // Cache słownika (TTL 5 min) — wołane przy budowie listy produktów w oknie zamówienia
        private static Dictionary<int, List<Wariant>>? _cache;
        private static DateTime _cacheExpiry = DateTime.MinValue;
        private static readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(5);

        public async Task EnsureSchemaAsync()
        {
            if (_initialized) return;
            await _initLock.WaitAsync();
            try
            {
                if (_initialized) return;
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                const string ddl = @"
                    IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name='TowarWarianty' AND type='U')
                    BEGIN
                        CREATE TABLE dbo.TowarWarianty (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            KodTowaru INT NOT NULL,        -- HM.TW.id (artykuł Symfonii, np. Filet A)
                            Kod NVARCHAR(20) NOT NULL,     -- techniczny kod wariantu
                            Nazwa NVARCHAR(60) NOT NULL,   -- nazwa wyświetlana
                            Lp INT NOT NULL DEFAULT 0,
                            Aktywny BIT NOT NULL DEFAULT 1
                        );
                        CREATE INDEX IX_TowarWarianty_Kod ON dbo.TowarWarianty(KodTowaru);
                    END;
                    -- Kolumna wariantu na pozycji zamówienia (wewnętrzna, NIE idzie do Symfonii)
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMiesoTowar') AND name = 'Wariant')
                        ALTER TABLE dbo.ZamowieniaMiesoTowar ADD Wariant NVARCHAR(20) NULL;";
                await using var cmd = new SqlCommand(ddl, cn);
                await cmd.ExecuteNonQueryAsync();
                _initialized = true;
            }
            finally { _initLock.Release(); }
        }

        /// <summary>Mapa KodTowaru → lista wariantów (cache 5 min). Tylko aktywne, posortowane po Lp.</summary>
        public async Task<Dictionary<int, List<Wariant>>> GetMapaAsync()
        {
            if (_cache != null && DateTime.Now < _cacheExpiry) return _cache;
            await EnsureSchemaAsync();
            var mapa = new Dictionary<int, List<Wariant>>();
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            const string sql = @"SELECT KodTowaru, Kod, Nazwa, Lp FROM dbo.TowarWarianty
                                 WHERE Aktywny = 1 ORDER BY KodTowaru, Lp, Nazwa";
            await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 10 };
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                int kod = rd.GetInt32(0);
                var w = new Wariant
                {
                    KodTowaru = kod,
                    Kod = rd.IsDBNull(1) ? "" : rd.GetString(1),
                    Nazwa = rd.IsDBNull(2) ? "" : rd.GetString(2),
                    Lp = rd.IsDBNull(3) ? 0 : rd.GetInt32(3)
                };
                if (!mapa.TryGetValue(kod, out var list)) { list = new List<Wariant>(); mapa[kod] = list; }
                list.Add(w);
            }
            _cache = mapa;
            _cacheExpiry = DateTime.Now.Add(_cacheTtl);
            return mapa;
        }

        public async Task<List<Wariant>> GetWariantyAsync(int kodTowaru)
        {
            var mapa = await GetMapaAsync();
            return mapa.TryGetValue(kodTowaru, out var l) ? l : new List<Wariant>();
        }

        public static void InvalidateCache() { _cache = null; }

        /// <summary>Pomocniczo: ustaw warianty dla towaru (zastąp). Do użycia w kreatorze/adminie.</summary>
        public async Task SetWariantyAsync(int kodTowaru, IEnumerable<Wariant> warianty)
        {
            await EnsureSchemaAsync();
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var tx = cn.BeginTransaction();
            try
            {
                await using (var del = new SqlCommand("DELETE FROM dbo.TowarWarianty WHERE KodTowaru=@k", cn, tx))
                { del.Parameters.AddWithValue("@k", kodTowaru); await del.ExecuteNonQueryAsync(); }
                int lp = 0;
                foreach (var w in warianty)
                {
                    await using var ins = new SqlCommand(
                        "INSERT INTO dbo.TowarWarianty (KodTowaru, Kod, Nazwa, Lp, Aktywny) VALUES (@k,@kod,@n,@lp,1)", cn, tx);
                    ins.Parameters.AddWithValue("@k", kodTowaru);
                    ins.Parameters.AddWithValue("@kod", w.Kod ?? "");
                    ins.Parameters.AddWithValue("@n", w.Nazwa ?? "");
                    ins.Parameters.AddWithValue("@lp", lp++);
                    await ins.ExecuteNonQueryAsync();
                }
                tx.Commit();
                InvalidateCache();
            }
            catch { tx.Rollback(); throw; }
        }
    }
}
