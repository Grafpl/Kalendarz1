using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.AnalitykaPelna.Services
{
    /// <summary>
    /// Cache obrazków towarów z LibraNet.dbo.TowarZdjecia (BLOB varbinary).
    /// Pobiera raz na sesję (TTL 15 min), zwraca BitmapImage Frozen — bezpieczne do współdzielenia między oknami.
    /// Klucz: TW.id (int) z HANDEL.HM.TW — TowarZdjecia.TowarId musi być na ten sam ID.
    /// </summary>
    public static class TowaryZdjeciaService
    {
        private static Dictionary<int, ImageSource> _cache = new();
        private static DateTime _lastLoaded = DateTime.MinValue;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);
        private static readonly object _lock = new();
        private static Task? _loadingTask;

        public static int Count => _cache.Count;
        public static bool Loaded => _cache.Count > 0 || (DateTime.Now - _lastLoaded) < CacheTtl;

        public static ImageSource? Get(int towarId)
        {
            return _cache.TryGetValue(towarId, out var img) ? img : null;
        }

        public static async Task LoadAsync(string connLibra)
        {
            Task? existing;
            lock (_lock)
            {
                if (_cache.Count > 0 && (DateTime.Now - _lastLoaded) < CacheTtl) return;
                if (_loadingTask != null) { existing = _loadingTask; goto Wait; }
                _loadingTask = LoadInternalAsync(connLibra);
                existing = _loadingTask;
            }
        Wait:
            try { await existing!; } catch { }
        }

        private static async Task LoadInternalAsync(string connLibra)
        {
            var dict = new Dictionary<int, ImageSource>();
            try
            {
                await using var cn = new SqlConnection(connLibra);
                await cn.OpenAsync();

                await using (var c = new SqlCommand(
                    "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TowarZdjecia') THEN 1 ELSE 0 END", cn))
                {
                    var existsObj = await c.ExecuteScalarAsync();
                    if (existsObj is int i && i == 0) { Commit(dict); return; }
                }

                const string sql = "SELECT TowarId, Zdjecie FROM dbo.TowarZdjecia WHERE Aktywne = 1";
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int id = rd.GetInt32(0);
                    if (rd.IsDBNull(1)) continue;
                    try
                    {
                        byte[] data = (byte[])rd[1];
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.StreamSource = new MemoryStream(data);
                        bi.DecodePixelWidth = 240;
                        bi.EndInit();
                        bi.Freeze();
                        dict[id] = bi;
                    }
                    catch { }
                }
            }
            catch { }
            Commit(dict);
        }

        private static void Commit(Dictionary<int, ImageSource> dict)
        {
            lock (_lock)
            {
                _cache = dict;
                _lastLoaded = DateTime.Now;
                _loadingTask = null;
            }
        }
    }
}
