using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Services
{
    /// <summary>
    /// Ustawienia powiadomień o zmianach DOSTAW ŻYWCA (kalendarz dostaw).
    /// Odpowiednik ZmianyZamowienSettingsService, osobna tabela UstawieniaDostawyZywca (1 wiersz).
    /// Odbiorców (komu pokazywać) trzyma PowiadomieniaOdbiorcyService (kategoria DOSTAWY_ZYWCA).
    /// </summary>
    public class DostawyZywcaSettings
    {
        public bool CzyAktywne { get; set; } = true;
        public TimeSpan GodzinaOdKtorejPowiadamiac { get; set; } = new TimeSpan(11, 0, 0);
        public decimal MinimalnaZmianaKgDoPowiadomienia { get; set; }
        public string RodzajPowiadomienia { get; set; } = "Toast";
        public string DniTygodniaAktywne { get; set; } = "1,2,3,4,5";
        public DateTime? ModifiedAt { get; set; }
        public string? ModifiedBy { get; set; }
    }

    public static class DostawyZywcaSettingsService
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private static DostawyZywcaSettings? _cached;
        private static DateTime _cacheExpiry = DateTime.MinValue;
        private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
        private static readonly System.Threading.SemaphoreSlim _initLock = new(1, 1);
        private static volatile bool _initialized;

        private static async Task EnsureSchemaAsync(SqlConnection cn)
        {
            if (_initialized) return;
            await _initLock.WaitAsync();
            try
            {
                if (_initialized) return;
                const string ddl = @"
                    IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name='UstawieniaDostawyZywca' AND type='U')
                    BEGIN
                        CREATE TABLE dbo.UstawieniaDostawyZywca (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            CzyAktywne BIT NOT NULL DEFAULT 1,
                            GodzinaOdKtorejPowiadamiac TIME NOT NULL DEFAULT '11:00',
                            MinimalnaZmianaKgDoPowiadomienia DECIMAL(18,2) NOT NULL DEFAULT 0,
                            RodzajPowiadomienia NVARCHAR(30) NOT NULL DEFAULT 'Toast',
                            DniTygodniaAktywne NVARCHAR(20) NOT NULL DEFAULT '1,2,3,4,5',
                            ModifiedAt DATETIME NULL,
                            ModifiedBy NVARCHAR(50) NULL
                        );
                    END;";
                await using var cmd = new SqlCommand(ddl, cn);
                await cmd.ExecuteNonQueryAsync();
                _initialized = true;
            }
            finally { _initLock.Release(); }
        }

        public static async Task<DostawyZywcaSettings> GetSettingsAsync()
        {
            if (_cached != null && DateTime.Now < _cacheExpiry) return _cached;
            var s = new DostawyZywcaSettings();
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                await EnsureSchemaAsync(cn);
                await using var cmd = new SqlCommand("SELECT TOP 1 * FROM dbo.UstawieniaDostawyZywca ORDER BY Id", cn);
                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    s.CzyAktywne = rd["CzyAktywne"] != DBNull.Value && Convert.ToBoolean(rd["CzyAktywne"]);
                    s.GodzinaOdKtorejPowiadamiac = rd["GodzinaOdKtorejPowiadamiac"] is TimeSpan t ? t : new TimeSpan(11, 0, 0);
                    s.MinimalnaZmianaKgDoPowiadomienia = rd["MinimalnaZmianaKgDoPowiadomienia"] != DBNull.Value ? Convert.ToDecimal(rd["MinimalnaZmianaKgDoPowiadomienia"]) : 0m;
                    s.RodzajPowiadomienia = rd["RodzajPowiadomienia"]?.ToString() ?? "Toast";
                    s.DniTygodniaAktywne = rd["DniTygodniaAktywne"]?.ToString() ?? "1,2,3,4,5";
                    s.ModifiedAt = rd["ModifiedAt"] as DateTime?;
                    s.ModifiedBy = rd["ModifiedBy"]?.ToString();
                }
            }
            catch { }
            _cached = s;
            _cacheExpiry = DateTime.Now.Add(_cacheDuration);
            return s;
        }

        public static DostawyZywcaSettings GetSettingsCached() => _cached ?? new DostawyZywcaSettings();

        public static async Task SaveSettingsAsync(DostawyZywcaSettings s, string modifiedBy)
        {
            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();
            await EnsureSchemaAsync(cn);
            const string sql = @"
                IF EXISTS (SELECT 1 FROM dbo.UstawieniaDostawyZywca)
                    UPDATE dbo.UstawieniaDostawyZywca SET
                        CzyAktywne = @akt, GodzinaOdKtorejPowiadamiac = @godz,
                        MinimalnaZmianaKgDoPowiadomienia = @kg, RodzajPowiadomienia = @rodzaj,
                        DniTygodniaAktywne = @dni, ModifiedAt = GETDATE(), ModifiedBy = @by;
                ELSE
                    INSERT INTO dbo.UstawieniaDostawyZywca
                        (CzyAktywne, GodzinaOdKtorejPowiadamiac, MinimalnaZmianaKgDoPowiadomienia, RodzajPowiadomienia, DniTygodniaAktywne, ModifiedAt, ModifiedBy)
                    VALUES (@akt, @godz, @kg, @rodzaj, @dni, GETDATE(), @by);";
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@akt", s.CzyAktywne);
            cmd.Parameters.AddWithValue("@godz", s.GodzinaOdKtorejPowiadamiac);
            cmd.Parameters.AddWithValue("@kg", s.MinimalnaZmianaKgDoPowiadomienia);
            cmd.Parameters.AddWithValue("@rodzaj", s.RodzajPowiadomienia ?? "Toast");
            cmd.Parameters.AddWithValue("@dni", s.DniTygodniaAktywne ?? "1,2,3,4,5");
            cmd.Parameters.AddWithValue("@by", (object?)modifiedBy ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
            _cached = null; // invaliduj cache
        }

        /// <summary>Czy bieżący czas przekroczył godzinę cutoff dla dostaw żywca (z dniami tygodnia).</summary>
        public static async Task<bool> IsAfterCutoffAsync()
        {
            try
            {
                var s = await GetSettingsAsync();
                if (!s.CzyAktywne) return false;
                if (!string.IsNullOrWhiteSpace(s.DniTygodniaAktywne))
                {
                    var dows = s.DniTygodniaAktywne.Split(',');
                    int todayDow = (int)DateTime.Now.DayOfWeek;
                    if (todayDow == 0) todayDow = 7;
                    if (!Array.Exists(dows, x => x.Trim() == todayDow.ToString())) return false;
                }
                return DateTime.Now.TimeOfDay >= s.GodzinaOdKtorejPowiadamiac;
            }
            catch { return false; }
        }
    }
}
