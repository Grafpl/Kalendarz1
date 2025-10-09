using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Kalendarz1
{
    public class CoordinatesCache
    {
        private readonly string cacheFilePath;
        private ConcurrentDictionary<int, CoordinateEntry> cache;
        private readonly SemaphoreSlim saveSemaphore = new SemaphoreSlim(1, 1);

        public CoordinatesCache()
        {
            // Zapisuj w folderze aplikacji
            string appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MapaOdbiorcow"
            );

            if (!Directory.Exists(appDataFolder))
                Directory.CreateDirectory(appDataFolder);

            cacheFilePath = Path.Combine(appDataFolder, "coordinates_cache.json");
            LoadCache();
        }

        private void LoadCache()
        {
            if (File.Exists(cacheFilePath))
            {
                try
                {
                    var json = File.ReadAllText(cacheFilePath);
                    var dict = JsonConvert.DeserializeObject<Dictionary<int, CoordinateEntry>>(json);
                    cache = new ConcurrentDictionary<int, CoordinateEntry>(dict ?? new Dictionary<int, CoordinateEntry>());
                }
                catch
                {
                    cache = new ConcurrentDictionary<int, CoordinateEntry>();
                    CreateBackup();
                }
            }
            else
            {
                cache = new ConcurrentDictionary<int, CoordinateEntry>();
            }
        }

        private void CreateBackup()
        {
            try
            {
                if (File.Exists(cacheFilePath))
                {
                    string backupPath = cacheFilePath + $".backup_{DateTime.Now:yyyyMMddHHmmss}";
                    File.Copy(cacheFilePath, backupPath);
                }
            }
            catch { }
        }

        public async Task SaveCacheAsync()
        {
            await saveSemaphore.WaitAsync();
            try
            {
                var json = JsonConvert.SerializeObject(cache, Formatting.Indented);
                await File.WriteAllTextAsync(cacheFilePath, json);
            }
            finally
            {
                saveSemaphore.Release();
            }
        }

        public async Task<(double lat, double lng)?> GetCoordinatesAsync(int contractorId)
        {
            if (cache.TryGetValue(contractorId, out var entry))
            {
                return (entry.Latitude, entry.Longitude);
            }
            return null;
        }

        public async Task SetCoordinatesAsync(int contractorId, double lat, double lng)
        {
            var entry = new CoordinateEntry
            {
                ContractorId = contractorId,
                Latitude = lat,
                Longitude = lng,
                GeocodedAt = DateTime.Now,
                Source = "Nominatim"
            };

            cache.AddOrUpdate(contractorId, entry, (k, v) => entry);
            await SaveCacheAsync();
        }

        public int GetCachedCount()
        {
            return cache.Count;
        }

        public async Task<Dictionary<int, string>> GetCacheSummaryAsync()
        {
            var summary = new Dictionary<int, string>();
            foreach (var kvp in cache)
            {
                summary[kvp.Key] = $"{kvp.Value.Latitude:F6}, {kvp.Value.Longitude:F6} ({kvp.Value.GeocodedAt:yyyy-MM-dd})";
            }
            return summary;
        }

        private class CoordinateEntry
        {
            public int ContractorId { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public DateTime GeocodedAt { get; set; }
            public string Source { get; set; }
        }
    }
}