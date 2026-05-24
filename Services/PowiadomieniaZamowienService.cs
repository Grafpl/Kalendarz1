using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kalendarz1.Services
{
    /// <summary>
    /// Tabela PowiadomieniaZamowienPoGodzinie + insert + polling query.
    /// Powiadomienie jest tworzone gdy handlowiec dodaje/edytuje zamówienie po godzinie cutoff
    /// (z ZmianyZamowienSettingsService) i zmiana kg dla pojedynczej pozycji ≥ progu (default 300).
    /// </summary>
    public static class PowiadomieniaZamowienService
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        /// <summary>Domyślny minimalny próg zmiany kg do powiadomienia.</summary>
        public const decimal DOMYSLNY_PROG_KG = 300m;

        public const string AkcjaDodanie = "DODANIE";
        public const string AkcjaZwiekszenie = "ZWIEKSZENIE";
        public const string AkcjaZmniejszenie = "ZMNIEJSZENIE";
        public const string AkcjaUsuniecie = "USUNIECIE";

        public class PowiadomienieZmiana
        {
            public string Akcja { get; set; } = "";       // DODANIE/ZWIEKSZENIE/ZMNIEJSZENIE/USUNIECIE
            public int? KodTowaru { get; set; }            // HM.TW.id (nullable dla całego zamówienia)
            public string NazwaTowaru { get; set; } = "";
            public decimal? StaraIlosc { get; set; }
            public decimal? NowaIlosc { get; set; }
            public decimal ZmianaKg { get; set; }           // ABS różnicy
        }

        public class PowiadomienieRekord
        {
            public int Id { get; set; }
            public int ZamowienieId { get; set; }
            public string Typ { get; set; } = "";          // NOWE / EDYCJA
            public string Akcja { get; set; } = "";
            public int? KodTowaru { get; set; }
            public string NazwaTowaru { get; set; } = "";
            public decimal? StaraIlosc { get; set; }
            public decimal? NowaIlosc { get; set; }
            public decimal ZmianaKg { get; set; }
            public int? KlientId { get; set; }
            public string KlientNazwa { get; set; } = "";
            public string Handlowiec { get; set; } = "";
            public DateTime? DataUboju { get; set; }
            public string UtworzonoPrzez { get; set; } = "";
            public DateTime UtworzonoAt { get; set; }
            public string PrzeczytaneCsv { get; set; } = "";
        }

        private static readonly System.Threading.SemaphoreSlim _initLock = new(1, 1);
        private static volatile bool _initialized;

        public static async Task EnsureSchemaAsync()
        {
            if (_initialized) return;
            await _initLock.WaitAsync();
            try
            {
                if (_initialized) return;
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                const string ddl = @"
                    IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name='PowiadomieniaZamowienPoGodzinie' AND type='U')
                    BEGIN
                        CREATE TABLE dbo.PowiadomieniaZamowienPoGodzinie (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            ZamowienieId INT NOT NULL,
                            Typ NVARCHAR(20) NOT NULL,
                            Akcja NVARCHAR(20) NOT NULL,
                            KodTowaru INT NULL,
                            NazwaTowaru NVARCHAR(200) NULL,
                            StaraIlosc DECIMAL(18,2) NULL,
                            NowaIlosc DECIMAL(18,2) NULL,
                            ZmianaKg DECIMAL(18,2) NOT NULL,
                            KlientId INT NULL,
                            KlientNazwa NVARCHAR(200) NULL,
                            Handlowiec NVARCHAR(100) NULL,
                            DataUboju DATE NULL,
                            UtworzonoPrzez NVARCHAR(50) NOT NULL,
                            UtworzonoAt DATETIME NOT NULL DEFAULT GETDATE(),
                            PrzeczytaneCsv NVARCHAR(MAX) NOT NULL DEFAULT ''
                        );
                        CREATE INDEX IX_PowZam_CreatedAt ON dbo.PowiadomieniaZamowienPoGodzinie(UtworzonoAt DESC);
                        CREATE INDEX IX_PowZam_Zam ON dbo.PowiadomieniaZamowienPoGodzinie(ZamowienieId);
                    END;";
                await using var cmd = new SqlCommand(ddl, cn);
                await cmd.ExecuteNonQueryAsync();
                _initialized = true;
            }
            finally { _initLock.Release(); }
        }

        /// <summary>
        /// Wpisuje listę zmian do tabeli. Wywoływane z NoweZamowienieTestWindow po zapisie.
        /// </summary>
        public static async Task InsertChangesAsync(
            int zamowienieId,
            string typ,                       // "NOWE" / "EDYCJA"
            int? klientId,
            string klientNazwa,
            string handlowiec,
            DateTime? dataUboju,
            string utworzonoPrzez,
            IReadOnlyList<PowiadomienieZmiana> zmiany)
        {
            if (zmiany == null || zmiany.Count == 0) return;
            await EnsureSchemaAsync();

            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();
            const string sql = @"
                INSERT INTO dbo.PowiadomieniaZamowienPoGodzinie
                    (ZamowienieId, Typ, Akcja, KodTowaru, NazwaTowaru, StaraIlosc, NowaIlosc, ZmianaKg,
                     KlientId, KlientNazwa, Handlowiec, DataUboju, UtworzonoPrzez)
                VALUES
                    (@zid, @typ, @akcja, @kod, @nazwa, @stara, @nowa, @zmiana,
                     @kid, @kn, @h, @du, @up);";
            foreach (var z in zmiany)
            {
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@zid", zamowienieId);
                cmd.Parameters.AddWithValue("@typ", typ ?? "");
                cmd.Parameters.AddWithValue("@akcja", z.Akcja ?? "");
                cmd.Parameters.AddWithValue("@kod", (object?)z.KodTowaru ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@nazwa", (object?)z.NazwaTowaru ?? "");
                cmd.Parameters.AddWithValue("@stara", (object?)z.StaraIlosc ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@nowa", (object?)z.NowaIlosc ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@zmiana", z.ZmianaKg);
                cmd.Parameters.AddWithValue("@kid", (object?)klientId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@kn", (object?)klientNazwa ?? "");
                cmd.Parameters.AddWithValue("@h", (object?)handlowiec ?? "");
                cmd.Parameters.AddWithValue("@du", (object?)dataUboju ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@up", utworzonoPrzez ?? "");
                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Polling — zwraca powiadomienia utworzone po wskazanym Id, pomijając własne i już przeczytane przez tego usera.
        /// </summary>
        public static async Task<List<PowiadomienieRekord>> PollNewAsync(int sinceId, string currentUserId)
        {
            await EnsureSchemaAsync();
            var list = new List<PowiadomienieRekord>();
            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();
            const string sql = @"
                SELECT TOP 100 Id, ZamowienieId, Typ, Akcja, KodTowaru, ISNULL(NazwaTowaru,'') AS NazwaTowaru,
                       StaraIlosc, NowaIlosc, ZmianaKg, KlientId, ISNULL(KlientNazwa,'') AS KlientNazwa,
                       ISNULL(Handlowiec,'') AS Handlowiec, DataUboju, UtworzonoPrzez, UtworzonoAt,
                       ISNULL(PrzeczytaneCsv,'') AS PrzeczytaneCsv
                FROM dbo.PowiadomieniaZamowienPoGodzinie
                WHERE Id > @sinceId
                  AND UtworzonoPrzez <> @uid
                  AND CHARINDEX(@uidMarker, ',' + ISNULL(PrzeczytaneCsv,'') + ',') = 0
                ORDER BY Id ASC";
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@sinceId", sinceId);
            cmd.Parameters.AddWithValue("@uid", currentUserId ?? "");
            cmd.Parameters.AddWithValue("@uidMarker", "," + (currentUserId ?? "") + ",");
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                list.Add(new PowiadomienieRekord
                {
                    Id = rd.GetInt32(0),
                    ZamowienieId = rd.GetInt32(1),
                    Typ = rd.GetString(2),
                    Akcja = rd.GetString(3),
                    KodTowaru = rd.IsDBNull(4) ? null : rd.GetInt32(4),
                    NazwaTowaru = rd.GetString(5),
                    StaraIlosc = rd.IsDBNull(6) ? null : rd.GetDecimal(6),
                    NowaIlosc = rd.IsDBNull(7) ? null : rd.GetDecimal(7),
                    ZmianaKg = rd.GetDecimal(8),
                    KlientId = rd.IsDBNull(9) ? null : rd.GetInt32(9),
                    KlientNazwa = rd.GetString(10),
                    Handlowiec = rd.GetString(11),
                    DataUboju = rd.IsDBNull(12) ? null : rd.GetDateTime(12),
                    UtworzonoPrzez = rd.GetString(13),
                    UtworzonoAt = rd.GetDateTime(14),
                    PrzeczytaneCsv = rd.GetString(15)
                });
            }
            return list;
        }

        /// <summary>Liczba nieprzeczytanych powiadomień dla usera — dla badge w kafelku menu.</summary>
        public static async Task<int> GetUnreadCountAsync(string currentUserId)
        {
            await EnsureSchemaAsync();
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                // Ostatnie 7 dni — starsze nie pokazujemy, żeby badge nie obrastał historycznie.
                const string sql = @"
                    SELECT COUNT(*) FROM dbo.PowiadomieniaZamowienPoGodzinie
                    WHERE UtworzonoAt >= DATEADD(DAY, -7, GETDATE())
                      AND UtworzonoPrzez <> @uid
                      AND CHARINDEX(@uidMarker, ',' + ISNULL(PrzeczytaneCsv,'') + ',') = 0";
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 5 };
                cmd.Parameters.AddWithValue("@uid", currentUserId ?? "");
                cmd.Parameters.AddWithValue("@uidMarker", "," + (currentUserId ?? "") + ",");
                var r = await cmd.ExecuteScalarAsync();
                return r == null || r == DBNull.Value ? 0 : Convert.ToInt32(r);
            }
            catch { return 0; }
        }

        /// <summary>Oznacz powiadomienie jako przeczytane przez usera (dodaj jego UserID do CSV).</summary>
        public static async Task MarkReadAsync(int powiadomienieId, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return;
            await EnsureSchemaAsync();
            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();
            const string sql = @"
                UPDATE dbo.PowiadomieniaZamowienPoGodzinie
                SET PrzeczytaneCsv = CASE
                    WHEN CHARINDEX(@uidMarker, ',' + ISNULL(PrzeczytaneCsv,'') + ',') > 0
                        THEN PrzeczytaneCsv
                    WHEN ISNULL(PrzeczytaneCsv,'') = ''
                        THEN @uid
                    ELSE PrzeczytaneCsv + ',' + @uid
                END
                WHERE Id = @id";
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@id", powiadomienieId);
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@uidMarker", "," + userId + ",");
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>Czy bieżący czas przekroczył godzinę cutoff dla bieżącego użytkownika? Honoruje indywidualne wyjątki.</summary>
        public static async Task<bool> IsAfterCutoffAsync(string currentUserId)
        {
            try
            {
                var settings = ZmianyZamowienSettingsService.GetSettingsCached();
                var now = DateTime.Now.TimeOfDay;
                var cutoff = settings.GodzinaOdKtorejPowiadamiac;

                // Sprawdź dni tygodnia
                if (!string.IsNullOrWhiteSpace(settings.DniTygodniaAktywne))
                {
                    var dows = settings.DniTygodniaAktywne.Split(',');
                    int todayDow = (int)DateTime.Now.DayOfWeek;
                    if (todayDow == 0) todayDow = 7; // ISO: Pn=1...Nd=7
                    if (!Array.Exists(dows, x => x.Trim() == todayDow.ToString()))
                        return false;
                }

                // Indywidualna godzina dla usera (lub wyłączenie)
                var wylaczeni = await ZmianyZamowienSettingsService.GetExemptionsAsync();
                var u = wylaczeni.Find(w => string.Equals(w.UserID, currentUserId, StringComparison.OrdinalIgnoreCase));
                if (u != null)
                {
                    if (u.CzyZwolnionyZPowiadomien && u.IndywidualnaGodzina == null) return false;
                    if (u.IndywidualnaGodzina.HasValue) cutoff = u.IndywidualnaGodzina.Value;
                }

                return now >= cutoff;
            }
            catch { return false; }
        }
    }
}
