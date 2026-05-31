using Kalendarz1.HDI.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Kalendarz1.HDI.Services
{
    /// <summary>
    /// CRUD dla HDI (Handlowy Dokument Identyfikacyjny) + auto-numeracja per rok +
    /// auto-fill z zamówień (klient, towary, wagi) + auto-fill partii z listapartii.
    /// Tabele: dbo.HDIDokumenty + dbo.HDIPartie (LibraNet).
    /// </summary>
    public class HdiService
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private const string ConnHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private const string ConnTransport =
            "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // Static cache nazw operatorów per sesja — eliminuje powtarzające się DB hits
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _operatorCache = new();

        // ── KATALOG TOWARÓW (mięso świeże + mrożone) z miniaturami ──
        // Używany w TowarPickerDialog — wybór towaru gdy fakturzystka chce zmienić asortyment.
        public class TowarKatalog
        {
            public int Id { get; set; }
            public string Kod { get; set; } = "";
            public string Nazwa { get; set; } = "";
            public int Katalog { get; set; }   // 67095=Mięso świeże, 67153=Mrożone
            public bool IsMrozone => Katalog == 67153;
            public string KategoriaBadge => IsMrozone ? "❄ Mrożone" : "🍖 Świeże";
            public System.Windows.Media.ImageSource? Image { get; set; }
            public string DisplayName => string.IsNullOrWhiteSpace(Nazwa) ? Kod : Nazwa;
        }

        // Static cache listy katalogu (raz na sesję — ~500 produktów)
        private static List<TowarKatalog>? _katalogCache = null;
        private static readonly System.Threading.SemaphoreSlim _katalogLock = new(1, 1);

        public async Task<List<TowarKatalog>> GetKatalogTowarowAsync()
        {
            if (_katalogCache != null) return _katalogCache;
            await _katalogLock.WaitAsync();
            try
            {
                if (_katalogCache != null) return _katalogCache;
                var list = new List<TowarKatalog>();
                await using (var cn = new SqlConnection(ConnHandel))
                {
                    await cn.OpenAsync();
                    const string sql = @"SELECT ID, ISNULL(kod,'') AS kod, ISNULL(nazwa,'') AS nazwa, katalog
                                         FROM [HANDEL].[HM].[TW]
                                         WHERE katalog IN (67095, 67153)
                                         ORDER BY katalog, nazwa, kod";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.CommandTimeout = 20;
                    await using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        list.Add(new TowarKatalog
                        {
                            Id = Convert.ToInt32(rd.GetValue(0)),
                            Kod = rd.GetString(1),
                            Nazwa = rd.GetString(2),
                            Katalog = Convert.ToInt32(rd.GetValue(3))
                        });
                    }
                }
                // Batch obrazki
                var imgs = await GetTowarImagesAsync(list.Select(t => t.Id));
                foreach (var t in list)
                    if (imgs.TryGetValue(t.Id, out var img)) t.Image = img;
                _katalogCache = list;
                return list;
            }
            finally { _katalogLock.Release(); }
        }

        // Static cache obrazków towarów per sesja (BLOB z LibraNet.dbo.TowarZdjecia)
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, System.Windows.Media.ImageSource> _towarImageCache = new();
        private static volatile bool _towarImageTableMissing = false;

        /// Batch load obrazków dla podanych ID towarów — jedna kwerenda IN (...).
        /// Zwraca słownik: tylko te ID dla których obrazek istnieje. Cache statyczny.
        public async Task<Dictionary<int, System.Windows.Media.ImageSource>> GetTowarImagesAsync(IEnumerable<int> twIds)
        {
            var result = new Dictionary<int, System.Windows.Media.ImageSource>();
            if (_towarImageTableMissing) return result;
            var needed = twIds.Where(id => id > 0).Distinct().ToList();
            if (needed.Count == 0) return result;

            // Pierwsze: weź to co już w cache
            var missing = new List<int>();
            foreach (var id in needed)
            {
                if (_towarImageCache.TryGetValue(id, out var cached)) result[id] = cached;
                else missing.Add(id);
            }
            if (missing.Count == 0) return result;

            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                // Sprawdzenie istnienia tabeli (raz na sesję)
                if (!_towarImageTableMissing)
                {
                    await using var c = new SqlCommand(
                        "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TowarZdjecia') THEN 1 ELSE 0 END", cn);
                    if ((int)(await c.ExecuteScalarAsync())! == 0)
                    {
                        _towarImageTableMissing = true;
                        return result;
                    }
                }
                var paramNames = missing.Select((_, i) => $"@i{i}").ToList();
                string sql = $"SELECT TowarId, Zdjecie FROM dbo.TowarZdjecia WHERE Aktywne = 1 AND TowarId IN ({string.Join(",", paramNames)})";
                await using var cmd = new SqlCommand(sql, cn);
                for (int i = 0; i < missing.Count; i++) cmd.Parameters.AddWithValue(paramNames[i], missing[i]);
                cmd.CommandTimeout = 15;
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int id = Convert.ToInt32(rd.GetValue(0));
                    if (rd.IsDBNull(1)) continue;
                    try
                    {
                        byte[] data = (byte[])rd[1];
                        using var ms = new System.IO.MemoryStream(data);
                        var bi = new System.Windows.Media.Imaging.BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bi.StreamSource = ms;
                        bi.DecodePixelWidth = 60;   // miniatura — wystarczy
                        bi.EndInit();
                        bi.Freeze();
                        _towarImageCache[id] = bi;
                        result[id] = bi;
                    }
                    catch { /* pojedynczy obrazek może być uszkodzony — pomiń */ }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[TowarImages] {ex.Message}"); }
            return result;
        }

        // ── Pełna nazwa operatora (imię + nazwisko) dla UserID — z LibraNet.dbo.operators.Name
        // Cache statyczny: pierwszy call = DB, kolejne = z pamięci (instant)
        public async Task<string> GetOperatorFullNameAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return "";
            if (_operatorCache.TryGetValue(userId, out var cached)) return cached;
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(
                    "SELECT TOP 1 ISNULL(Name, ID) FROM dbo.operators WHERE ID = @id", cn);
                cmd.Parameters.AddWithValue("@id", userId);
                var r = await cmd.ExecuteScalarAsync();
                string s = r?.ToString() ?? "";
                string result = string.IsNullOrWhiteSpace(s) ? userId : s.Trim();
                _operatorCache[userId] = result;
                return result;
            }
            catch { return userId; }
        }

        // ── Transport: pobiera numer rejestracyjny + kierowcę dla danego zamówienia ─────
        public class TransportInfo
        {
            public string NumerRejestracyjny { get; set; } = "";
            public string Kierowca { get; set; } = "";
            public DateTime? DataKursu { get; set; }
        }

        public async Task<TransportInfo?> LoadTransportInfoAsync(int orderId)
        {
            using var _ = Kalendarz1.HDI.HdiDiag.Scope("HdiService.Transport", $"LoadTransportInfo(orderId={orderId})");
            try
            {
                // 1) Pobierz TransportKursID z zamówienia
                long? kursId = null;
                await using (var cnL = new SqlConnection(ConnLibra))
                {
                    await cnL.OpenAsync();
                    await using var cmd = new SqlCommand(
                        "SELECT TransportKursID FROM dbo.ZamowieniaMieso WHERE Id = @id", cnL);
                    cmd.Parameters.AddWithValue("@id", orderId);
                    var r = await cmd.ExecuteScalarAsync();
                    if (r != null && r != DBNull.Value) kursId = Convert.ToInt64(r);
                }
                if (!kursId.HasValue) return null;

                // 2) Pobierz Pojazd.Rejestracja + Kierowca z TransportPL
                await using (var cnT = new SqlConnection(ConnTransport))
                {
                    await cnT.OpenAsync();
                    await using var cmd = new SqlCommand(
                        @"SELECT ISNULL(p.Rejestracja, '') AS Rej,
                                 ISNULL(kier.Imie + ' ' + kier.Nazwisko, '') AS Kier,
                                 k.DataKursu
                          FROM dbo.Kurs k
                          LEFT JOIN dbo.Kierowca kier ON k.KierowcaID = kier.KierowcaID
                          LEFT JOIN dbo.Pojazd p ON k.PojazdID = p.PojazdID
                          WHERE k.KursID = @id", cnT);
                    cmd.Parameters.AddWithValue("@id", kursId.Value);
                    await using var rd = await cmd.ExecuteReaderAsync();
                    if (await rd.ReadAsync())
                    {
                        return new TransportInfo
                        {
                            NumerRejestracyjny = rd.IsDBNull(0) ? "" : rd.GetString(0),
                            Kierowca = rd.IsDBNull(1) ? "" : rd.GetString(1),
                            DataKursu = rd.IsDBNull(2) ? null : (DateTime?)rd.GetDateTime(2)
                        };
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Transport] {ex.Message}"); }
            return null;
        }

        private static readonly System.Threading.SemaphoreSlim _initLock = new(1, 1);
        private static volatile bool _initialized;

        public async Task EnsureSchemaAsync()
        {
            if (_initialized) return;
            await _initLock.WaitAsync();
            try
            {
                if (_initialized) return;
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                const string ddl = @"
                    IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name='HDIDokumenty' AND type='U')
                    BEGIN
                        CREATE TABLE dbo.HDIDokumenty (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            Numer INT NOT NULL,
                            Rok INT NOT NULL,
                            ZamowienieId INT NULL,
                            KlientId INT NULL,
                            KlientNazwa NVARCHAR(300) NULL,
                            KlientAdres NVARCHAR(500) NULL,
                            OpisTowaru NVARCHAR(500) NULL,
                            RodzajOpakowan NVARCHAR(200) NULL,
                            LiczbaOpakowan INT NULL,
                            WagaNetto DECIMAL(10,2) NULL,
                            WagaBrutto DECIMAL(10,2) NULL,
                            Pochodzenie NVARCHAR(100) NULL,
                            MiejscePozyskania NVARCHAR(300) NULL,
                            DataWysylki DATE NULL,
                            MiejscePrzeznaczenia NVARCHAR(500) NULL,
                            NumerRejestracyjny NVARCHAR(50) NULL,
                            UwagiTransport NVARCHAR(500) NULL,
                            UwagiTechnologia NVARCHAR(1000) NULL,
                            RynekKrajowy BIT NOT NULL DEFAULT 1,
                            RynekUE BIT NOT NULL DEFAULT 0,
                            RynekInny BIT NOT NULL DEFAULT 0,
                            InnePanstwo NVARCHAR(100) NULL,
                            MiejscowoscWystawienia NVARCHAR(100) NULL,
                            DataWystawienia DATETIME NOT NULL DEFAULT GETDATE(),
                            UtworzonoPrzez NVARCHAR(50) NULL,
                            Status NVARCHAR(20) NOT NULL DEFAULT 'AKTYWNY'
                        );
                        CREATE INDEX IX_HDI_Rok_Numer ON dbo.HDIDokumenty(Rok DESC, Numer DESC);
                        CREATE INDEX IX_HDI_Klient ON dbo.HDIDokumenty(KlientId);
                        CREATE INDEX IX_HDI_Zamowienie ON dbo.HDIDokumenty(ZamowienieId);
                    END;
                    -- Lazy migration: dodaj Wystawiajacy do istniejącej schemy
                    IF NOT EXISTS (SELECT 1 FROM sys.columns
                                   WHERE object_id = OBJECT_ID('dbo.HDIDokumenty') AND name = 'Wystawiajacy')
                    BEGIN
                        ALTER TABLE dbo.HDIDokumenty ADD Wystawiajacy NVARCHAR(200) NULL;
                    END;
                    -- Lazy migration: dodaj NumerRejNaczepy (manualne)
                    IF NOT EXISTS (SELECT 1 FROM sys.columns
                                   WHERE object_id = OBJECT_ID('dbo.HDIDokumenty') AND name = 'NumerRejNaczepy')
                    BEGIN
                        ALTER TABLE dbo.HDIDokumenty ADD NumerRejNaczepy NVARCHAR(50) NULL;
                    END;
                    -- Lazy migration: dodaj Idtw do HDIPartie (do ładowania obrazków przy edycji)
                    IF NOT EXISTS (SELECT 1 FROM sys.columns
                                   WHERE object_id = OBJECT_ID('dbo.HDIPartie') AND name = 'Idtw')
                    BEGIN
                        ALTER TABLE dbo.HDIPartie ADD Idtw INT NULL;
                    END;
                    -- HDISettings: ustawienia per rok (StartNumber = początkowy numer
                    -- żeby fakturzystka mogła zacząć od np. 412 jeśli wcześniej wystawiła 411 ręcznie).
                    IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name='HDISettings' AND type='U')
                    BEGIN
                        CREATE TABLE dbo.HDISettings (
                            Rok INT PRIMARY KEY,
                            StartNumber INT NOT NULL DEFAULT 0,
                            ModifiedAt DATETIME NOT NULL DEFAULT GETDATE(),
                            ModifiedBy NVARCHAR(50) NULL
                        );
                    END;
                    IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name='HDIPartie' AND type='U')
                    BEGIN
                        CREATE TABLE dbo.HDIPartie (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            HdiDokumentId INT NOT NULL,
                            Asortyment NVARCHAR(200) NULL,
                            NumerPartii NVARCHAR(50) NULL,
                            DataUboju DATE NULL,
                            DataMrozenia DATE NULL,
                            DataPrzydatnosci DATE NULL,
                            WagaKg DECIMAL(10,2) NULL,
                            Kolejnosc INT NOT NULL DEFAULT 0,
                            CONSTRAINT FK_HDIPartie_Dokument FOREIGN KEY (HdiDokumentId)
                                REFERENCES dbo.HDIDokumenty(Id) ON DELETE CASCADE
                        );
                        CREATE INDEX IX_HDIPartie_Dokument ON dbo.HDIPartie(HdiDokumentId);
                    END;";
                await using var cmd = new SqlCommand(ddl, cn);
                await cmd.ExecuteNonQueryAsync();
                _initialized = true;
            }
            finally { _initLock.Release(); }
        }

        // ── Auto-numeracja per rok ─────────────────────────────────────────
        // Next = max(MAX_zapisanych_HDI, StartNumber-1) + 1
        // Czyli jeśli admin ustawi StartNumber=412, a w bazie max(Numer)=10 → next=412.
        // Jeśli max(Numer)=500, a StartNumber=412 → next=501 (nie cofamy się).
        public async Task<int> GetNextNumberAsync(int rok)
        {
            await EnsureSchemaAsync();
            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(
                @"SELECT
                    CASE
                        WHEN ISNULL(s.StartNumber, 0) > ISNULL(m.MaxNumer, 0)
                            THEN ISNULL(s.StartNumber, 1)
                        ELSE ISNULL(m.MaxNumer, 0) + 1
                    END AS Next
                  FROM (SELECT 1 AS x) z
                  LEFT JOIN (SELECT MAX(Numer) AS MaxNumer FROM dbo.HDIDokumenty WHERE Rok = @rok) m ON 1=1
                  LEFT JOIN dbo.HDISettings s ON s.Rok = @rok", cn);
            cmd.Parameters.AddWithValue("@rok", rok);
            var r = await cmd.ExecuteScalarAsync();
            return r == null || r == DBNull.Value ? 1 : Convert.ToInt32(r);
        }

        // Czy numer jest już zajęty w danym roku (przez inny dokument niż excludeId)?
        public async Task<bool> IsNumberTakenAsync(int rok, int numer, int excludeId = 0)
        {
            await EnsureSchemaAsync();
            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM dbo.HDIDokumenty WHERE Rok = @rok AND Numer = @numer AND Id <> @ex", cn);
            cmd.Parameters.AddWithValue("@rok", rok);
            cmd.Parameters.AddWithValue("@numer", numer);
            cmd.Parameters.AddWithValue("@ex", excludeId);
            var r = await cmd.ExecuteScalarAsync();
            return r != null && Convert.ToInt32(r) > 0;
        }

        // Pobierz aktualny StartNumber dla roku (0 = nieustawiony)
        public async Task<int> GetStartNumberAsync(int rok)
        {
            await EnsureSchemaAsync();
            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT ISNULL(StartNumber, 0) FROM dbo.HDISettings WHERE Rok = @rok", cn);
            cmd.Parameters.AddWithValue("@rok", rok);
            var r = await cmd.ExecuteScalarAsync();
            return r == null || r == DBNull.Value ? 0 : Convert.ToInt32(r);
        }

        // UPSERT StartNumber dla roku
        public async Task SetStartNumberAsync(int rok, int startNumber, string? user = null)
        {
            await EnsureSchemaAsync();
            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                IF EXISTS (SELECT 1 FROM dbo.HDISettings WHERE Rok = @rok)
                    UPDATE dbo.HDISettings SET StartNumber = @sn, ModifiedAt = GETDATE(), ModifiedBy = @u WHERE Rok = @rok
                ELSE
                    INSERT INTO dbo.HDISettings (Rok, StartNumber, ModifiedBy) VALUES (@rok, @sn, @u);", cn);
            cmd.Parameters.AddWithValue("@rok", rok);
            cmd.Parameters.AddWithValue("@sn", startNumber);
            cmd.Parameters.AddWithValue("@u", (object?)user ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        // ── CRUD ─────────────────────────────────────────────────────────────
        public async Task<int> CreateAsync(HdiDokument d)
        {
            await EnsureSchemaAsync();
            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();
            await using var tr = (SqlTransaction)await cn.BeginTransactionAsync();
            try
            {
                if (d.Numer == 0)
                {
                    await using var cmdNext = new SqlCommand(@"
                        SELECT
                            CASE
                                WHEN ISNULL(s.StartNumber, 0) > ISNULL(m.MaxNumer, 0)
                                    THEN ISNULL(s.StartNumber, 1)
                                ELSE ISNULL(m.MaxNumer, 0) + 1
                            END
                        FROM (SELECT 1 AS x) z
                        LEFT JOIN (SELECT MAX(Numer) AS MaxNumer FROM dbo.HDIDokumenty WITH (UPDLOCK, HOLDLOCK) WHERE Rok = @rok) m ON 1=1
                        LEFT JOIN dbo.HDISettings s ON s.Rok = @rok", cn, tr);
                    cmdNext.Parameters.AddWithValue("@rok", d.Rok);
                    var rNext = await cmdNext.ExecuteScalarAsync();
                    d.Numer = rNext == null || rNext == DBNull.Value ? 1 : Convert.ToInt32(rNext);
                }

                const string sqlIns = @"
                    INSERT INTO dbo.HDIDokumenty
                        (Numer, Rok, ZamowienieId, KlientId, KlientNazwa, KlientAdres,
                         OpisTowaru, RodzajOpakowan, LiczbaOpakowan, WagaNetto, WagaBrutto,
                         Pochodzenie, MiejscePozyskania, DataWysylki, MiejscePrzeznaczenia,
                         NumerRejestracyjny, NumerRejNaczepy, UwagiTransport, UwagiTechnologia,
                         RynekKrajowy, RynekUE, RynekInny, InnePanstwo,
                         MiejscowoscWystawienia, DataWystawienia, UtworzonoPrzez, Wystawiajacy, Status)
                    VALUES
                        (@Numer, @Rok, @ZamowienieId, @KlientId, @KlientNazwa, @KlientAdres,
                         @OpisTowaru, @RodzajOpakowan, @LiczbaOpakowan, @WagaNetto, @WagaBrutto,
                         @Pochodzenie, @MiejscePozyskania, @DataWysylki, @MiejscePrzeznaczenia,
                         @NumerRejestracyjny, @NumerRejNaczepy, @UwagiTransport, @UwagiTechnologia,
                         @RynekKrajowy, @RynekUE, @RynekInny, @InnePanstwo,
                         @MiejscowoscWystawienia, @DataWystawienia, @UtworzonoPrzez, @Wystawiajacy, @Status);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";
                await using var cmd = new SqlCommand(sqlIns, cn, tr);
                BindParams(cmd, d);
                var rId = await cmd.ExecuteScalarAsync();
                d.Id = rId == null ? 0 : Convert.ToInt32(rId);

                int kolejnosc = 0;
                foreach (var p in d.Partie)
                {
                    p.HdiDokumentId = d.Id;
                    const string sqlIns2 = @"
                        INSERT INTO dbo.HDIPartie
                            (HdiDokumentId, Asortyment, NumerPartii, DataUboju, DataMrozenia, DataPrzydatnosci, WagaKg, Kolejnosc, Idtw)
                        VALUES (@hid, @aso, @num, @duboj, @mroz, @prze, @waga, @kol, @idtw);";
                    await using var cmd2 = new SqlCommand(sqlIns2, cn, tr);
                    cmd2.Parameters.AddWithValue("@hid", d.Id);
                    cmd2.Parameters.AddWithValue("@aso", (object?)p.Asortyment ?? "");
                    cmd2.Parameters.AddWithValue("@num", (object?)p.NumerPartii ?? "");
                    cmd2.Parameters.AddWithValue("@duboj", (object?)p.DataUboju ?? DBNull.Value);
                    cmd2.Parameters.AddWithValue("@mroz", (object?)p.DataMrozenia ?? DBNull.Value);
                    cmd2.Parameters.AddWithValue("@prze", (object?)p.DataPrzydatnosci ?? DBNull.Value);
                    cmd2.Parameters.AddWithValue("@waga", (object?)p.WagaKg ?? DBNull.Value);
                    cmd2.Parameters.AddWithValue("@kol", kolejnosc++);
                    cmd2.Parameters.AddWithValue("@idtw", (object?)p.Idtw ?? DBNull.Value);
                    await cmd2.ExecuteNonQueryAsync();
                }

                await tr.CommitAsync();
                return d.Id;
            }
            catch
            {
                try { await tr.RollbackAsync(); } catch { }
                throw;
            }
        }

        public async Task UpdateAsync(HdiDokument d)
        {
            await EnsureSchemaAsync();
            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();
            await using var tr = (SqlTransaction)await cn.BeginTransactionAsync();
            try
            {
                const string sqlUpd = @"
                    UPDATE dbo.HDIDokumenty SET
                        ZamowienieId = @ZamowienieId, KlientId = @KlientId,
                        KlientNazwa = @KlientNazwa, KlientAdres = @KlientAdres,
                        OpisTowaru = @OpisTowaru, RodzajOpakowan = @RodzajOpakowan,
                        LiczbaOpakowan = @LiczbaOpakowan, WagaNetto = @WagaNetto, WagaBrutto = @WagaBrutto,
                        Pochodzenie = @Pochodzenie, MiejscePozyskania = @MiejscePozyskania,
                        DataWysylki = @DataWysylki, MiejscePrzeznaczenia = @MiejscePrzeznaczenia,
                        NumerRejestracyjny = @NumerRejestracyjny, NumerRejNaczepy = @NumerRejNaczepy,
                        UwagiTransport = @UwagiTransport, UwagiTechnologia = @UwagiTechnologia,
                        RynekKrajowy = @RynekKrajowy, RynekUE = @RynekUE, RynekInny = @RynekInny, InnePanstwo = @InnePanstwo,
                        MiejscowoscWystawienia = @MiejscowoscWystawienia, DataWystawienia = @DataWystawienia,
                        Wystawiajacy = @Wystawiajacy,
                        Status = @Status
                    WHERE Id = @Id";
                await using var cmd = new SqlCommand(sqlUpd, cn, tr);
                BindParams(cmd, d);
                cmd.Parameters.AddWithValue("@Id", d.Id);
                await cmd.ExecuteNonQueryAsync();

                await using (var cmdDel = new SqlCommand("DELETE FROM dbo.HDIPartie WHERE HdiDokumentId = @id", cn, tr))
                {
                    cmdDel.Parameters.AddWithValue("@id", d.Id);
                    await cmdDel.ExecuteNonQueryAsync();
                }

                int kolejnosc = 0;
                foreach (var p in d.Partie)
                {
                    p.HdiDokumentId = d.Id;
                    const string sqlIns2 = @"
                        INSERT INTO dbo.HDIPartie
                            (HdiDokumentId, Asortyment, NumerPartii, DataUboju, DataMrozenia, DataPrzydatnosci, WagaKg, Kolejnosc, Idtw)
                        VALUES (@hid, @aso, @num, @duboj, @mroz, @prze, @waga, @kol, @idtw);";
                    await using var cmd2 = new SqlCommand(sqlIns2, cn, tr);
                    cmd2.Parameters.AddWithValue("@hid", d.Id);
                    cmd2.Parameters.AddWithValue("@aso", (object?)p.Asortyment ?? "");
                    cmd2.Parameters.AddWithValue("@num", (object?)p.NumerPartii ?? "");
                    cmd2.Parameters.AddWithValue("@duboj", (object?)p.DataUboju ?? DBNull.Value);
                    cmd2.Parameters.AddWithValue("@mroz", (object?)p.DataMrozenia ?? DBNull.Value);
                    cmd2.Parameters.AddWithValue("@prze", (object?)p.DataPrzydatnosci ?? DBNull.Value);
                    cmd2.Parameters.AddWithValue("@waga", (object?)p.WagaKg ?? DBNull.Value);
                    cmd2.Parameters.AddWithValue("@kol", kolejnosc++);
                    cmd2.Parameters.AddWithValue("@idtw", (object?)p.Idtw ?? DBNull.Value);
                    await cmd2.ExecuteNonQueryAsync();
                }

                await tr.CommitAsync();
            }
            catch
            {
                try { await tr.RollbackAsync(); } catch { }
                throw;
            }
        }

        public async Task DeleteAsync(int id)
        {
            await EnsureSchemaAsync();
            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(
                "UPDATE dbo.HDIDokumenty SET Status = 'ANULOWANY' WHERE Id = @id", cn);
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        // Hard delete — fizyczne usunięcie z bazy. Tylko dla admina!
        // Najpierw kasuje partie (FK CASCADE i tak by to zrobiło, ale jawnie dla bezpieczeństwa).
        public async Task HardDeleteAsync(int id)
        {
            await EnsureSchemaAsync();
            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();
            await using var tr = (SqlTransaction)await cn.BeginTransactionAsync();
            try
            {
                await using (var c1 = new SqlCommand("DELETE FROM dbo.HDIPartie WHERE HdiDokumentId = @id", cn, tr))
                {
                    c1.Parameters.AddWithValue("@id", id);
                    await c1.ExecuteNonQueryAsync();
                }
                await using (var c2 = new SqlCommand("DELETE FROM dbo.HDIDokumenty WHERE Id = @id", cn, tr))
                {
                    c2.Parameters.AddWithValue("@id", id);
                    await c2.ExecuteNonQueryAsync();
                }
                await tr.CommitAsync();
            }
            catch { try { await tr.RollbackAsync(); } catch { } throw; }
        }

        // Sprawdza flagę IsAdmin dla danego UserID w dbo.operators
        public async Task<bool> IsAdminAsync(string? userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return false;
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(
                    "SELECT ISNULL(IsAdmin, 0) FROM dbo.operators WHERE ID = @id", cn);
                cmd.Parameters.AddWithValue("@id", userId);
                var r = await cmd.ExecuteScalarAsync();
                return r != null && r != DBNull.Value && Convert.ToBoolean(r);
            }
            catch { return false; }
        }

        public async Task<HdiDokument?> GetByIdAsync(int id)
        {
            await EnsureSchemaAsync();
            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();
            HdiDokument? d = null;
            await using (var cmd = new SqlCommand("SELECT * FROM dbo.HDIDokumenty WHERE Id = @id", cn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync()) d = MapDokument(rd);
            }
            if (d == null) return null;

            await using (var cmd2 = new SqlCommand(
                "SELECT * FROM dbo.HDIPartie WHERE HdiDokumentId = @id ORDER BY Kolejnosc", cn))
            {
                cmd2.Parameters.AddWithValue("@id", id);
                await using var rd2 = await cmd2.ExecuteReaderAsync();
                while (await rd2.ReadAsync())
                {
                    d.Partie.Add(new HdiPartia
                    {
                        Id = (int)rd2["Id"],
                        HdiDokumentId = (int)rd2["HdiDokumentId"],
                        Asortyment = rd2["Asortyment"] == DBNull.Value ? "" : (string)rd2["Asortyment"],
                        NumerPartii = rd2["NumerPartii"] == DBNull.Value ? "" : (string)rd2["NumerPartii"],
                        DataUboju = rd2["DataUboju"] == DBNull.Value ? null : (DateTime?)rd2["DataUboju"],
                        DataMrozenia = rd2["DataMrozenia"] == DBNull.Value ? null : (DateTime?)rd2["DataMrozenia"],
                        DataPrzydatnosci = rd2["DataPrzydatnosci"] == DBNull.Value ? null : (DateTime?)rd2["DataPrzydatnosci"],
                        WagaKg = rd2["WagaKg"] == DBNull.Value ? null : (decimal?)rd2["WagaKg"],
                        Idtw = HasColumn(rd2, "Idtw") && rd2["Idtw"] != DBNull.Value ? (int?)Convert.ToInt32(rd2["Idtw"]) : null
                    });
                }
            }

            // BATCH load obrazków towarów dla partii (jeśli mają Idtw zapisane w bazie).
            // Stare HDI bez Idtw → fallback: dopasuj po nazwie asortymentu do HM.TW.
            await EnsureImagesForPartieAsync(d.Partie);

            return d;
        }

        // Ładuje obrazki dla listy partii. Jeśli Idtw jest w bazie — bezpośrednio.
        // Jeśli nie (stare HDI) — szuka HM.TW.nazwa po nazwie asortymentu (LIKE).
        public async Task EnsureImagesForPartieAsync(List<HdiPartia> partie)
        {
            if (partie == null || partie.Count == 0) return;

            // 1) Te które mają Idtw → batch
            var withId = partie.Where(p => p.Idtw.HasValue && p.Idtw.Value > 0).ToList();
            var withoutId = partie.Where(p => !p.Idtw.HasValue || p.Idtw.Value <= 0).ToList();

            if (withId.Count > 0)
            {
                var imgs = await GetTowarImagesAsync(withId.Select(p => p.Idtw!.Value));
                foreach (var p in withId)
                    if (p.Idtw.HasValue && imgs.TryGetValue(p.Idtw.Value, out var img))
                        p.Image = img;
            }

            // 2) Te bez Idtw → szukaj po nazwie w HM.TW (jedno zapytanie batch)
            if (withoutId.Count > 0)
            {
                try
                {
                    var nameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    var nazwy = withoutId.Select(p => p.Asortyment ?? "")
                                         .Where(n => !string.IsNullOrWhiteSpace(n))
                                         .Distinct()
                                         .ToList();
                    if (nazwy.Count > 0)
                    {
                        await using var cn = new SqlConnection(ConnHandel);
                        await cn.OpenAsync();
                        var paramNames = nazwy.Select((_, i) => $"@n{i}").ToList();
                        // Match po pełnej nazwie lub kodzie (case-insensitive)
                        string sql = $@"SELECT id, ISNULL(nazwa,'') AS nazwa, ISNULL(kod,'') AS kod
                                        FROM [HANDEL].[HM].[TW]
                                        WHERE ISNULL(nazwa,'') IN ({string.Join(",", paramNames)})
                                           OR ISNULL(kod,'')   IN ({string.Join(",", paramNames)})";
                        await using var cmd = new SqlCommand(sql, cn);
                        for (int i = 0; i < nazwy.Count; i++) cmd.Parameters.AddWithValue(paramNames[i], nazwy[i]);
                        cmd.CommandTimeout = 15;
                        await using var rd = await cmd.ExecuteReaderAsync();
                        while (await rd.ReadAsync())
                        {
                            int id = Convert.ToInt32(rd.GetValue(0));
                            string nazwa = rd.GetString(1);
                            string kod = rd.GetString(2);
                            if (!string.IsNullOrWhiteSpace(nazwa)) nameToId[nazwa] = id;
                            if (!string.IsNullOrWhiteSpace(kod)) nameToId[kod] = id;
                        }
                    }

                    // Dla każdej partii uzupełnij Idtw + Image
                    var idsToLoad = withoutId
                        .Where(p => nameToId.ContainsKey(p.Asortyment ?? ""))
                        .Select(p => nameToId[p.Asortyment!])
                        .Distinct()
                        .ToList();
                    if (idsToLoad.Count > 0)
                    {
                        var imgs2 = await GetTowarImagesAsync(idsToLoad);
                        foreach (var p in withoutId)
                        {
                            if (nameToId.TryGetValue(p.Asortyment ?? "", out var twId))
                            {
                                p.Idtw = twId;
                                if (imgs2.TryGetValue(twId, out var img)) p.Image = img;
                            }
                        }
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[EnsureImagesForPartie] {ex.Message}"); }
            }
        }

        public async Task<List<HdiListItem>> GetListAsync(int? rok = null, string? searchKlient = null)
        {
            await EnsureSchemaAsync();
            var list = new List<HdiListItem>();
            await using var cn = new SqlConnection(ConnLibra);
            await cn.OpenAsync();
            string where = "WHERE 1=1";
            if (rok.HasValue) where += " AND Rok = @rok";
            if (!string.IsNullOrWhiteSpace(searchKlient)) where += " AND KlientNazwa LIKE @sk";
            string sql = $@"
                SELECT TOP 1000 Id, Numer, Rok, DataWystawienia,
                       ISNULL(KlientNazwa,'') AS KlientNazwa,
                       ISNULL(OpisTowaru,'') AS OpisTowaru,
                       WagaNetto, LiczbaOpakowan,
                       ISNULL(UtworzonoPrzez,'') AS UtworzonoPrzez,
                       ISNULL(Status,'AKTYWNY') AS Status
                FROM dbo.HDIDokumenty {where}
                ORDER BY Rok DESC, Numer DESC";
            await using var cmd = new SqlCommand(sql, cn);
            if (rok.HasValue) cmd.Parameters.AddWithValue("@rok", rok.Value);
            if (!string.IsNullOrWhiteSpace(searchKlient)) cmd.Parameters.AddWithValue("@sk", "%" + searchKlient + "%");
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                list.Add(new HdiListItem
                {
                    Id = rd.GetInt32(0),
                    NumerPelny = $"{rd.GetInt32(1)}/{rd.GetInt32(2):00}",
                    DataWystawienia = rd.GetDateTime(3),
                    KlientNazwa = rd.GetString(4),
                    OpisTowaru = rd.GetString(5),
                    WagaNetto = rd.IsDBNull(6) ? null : rd.GetDecimal(6),
                    LiczbaOpakowan = rd.IsDBNull(7) ? null : rd.GetInt32(7),
                    UtworzonoPrzez = rd.GetString(8),
                    Status = rd.GetString(9)
                });
            }
            return list;
        }

        // ── AUTO-FILL: pobierz dane z zamówienia ────────────────────────────
        public class ZamowienieAutoFill
        {
            public int ZamowienieId { get; set; }
            public int? KlientId { get; set; }
            public string KlientNazwa { get; set; } = "";
            public string KlientAdres { get; set; } = "";
            public DateTime? DataWysylki { get; set; }
            public DateTime? DataUboju { get; set; }
            public string NumerRejestracyjny { get; set; } = "";
            public List<PozycjaZamowienia> Pozycje { get; set; } = new();
        }

        public class PozycjaZamowienia
        {
            public int KodTowaru { get; set; }
            public string Nazwa { get; set; } = "";
            public decimal Ilosc { get; set; }
            public int? Pojemniki { get; set; }
        }

        public async Task<ZamowienieAutoFill?> LoadZamowienieAutoFillAsync(int zamowienieId)
        {
            using var _ = Kalendarz1.HDI.HdiDiag.Scope("HdiService.Order", $"LoadZamowienieAutoFill({zamowienieId})");
            await using var cnL = new SqlConnection(ConnLibra);
            await cnL.OpenAsync();

            ZamowienieAutoFill? wynik = null;
            int? klientId = null;
            await using (var cmd = new SqlCommand(
                @"SELECT TOP 1 Id, KlientId, DataPrzyjazdu, DataUboju
                  FROM dbo.ZamowieniaMieso WHERE Id = @id", cnL))
            {
                cmd.Parameters.AddWithValue("@id", zamowienieId);
                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    wynik = new ZamowienieAutoFill
                    {
                        ZamowienieId = Convert.ToInt32(rd.GetValue(0)),
                        KlientId     = rd.IsDBNull(1) ? null : (int?)Convert.ToInt32(rd.GetValue(1)),
                        DataWysylki  = rd.IsDBNull(2) ? null : (DateTime?)rd.GetDateTime(2),
                        DataUboju    = rd.IsDBNull(3) ? null : (DateTime?)rd.GetDateTime(3)
                    };
                    klientId = wynik.KlientId;
                }
            }
            if (wynik == null) return null;

            // Pozycje. UWAGA: kolumny mogą być w bazie int LUB decimal (zależnie od wersji
            // schemy ZamowieniaMiesoTowar). Używamy Convert.ToXxx(GetValue) żeby uniknąć
            // InvalidCastException "Cannot cast Int32 to Decimal" / "Cannot cast Decimal to Int32".
            await using (var cmd = new SqlCommand(
                "SELECT KodTowaru, Ilosc, Pojemniki FROM dbo.ZamowieniaMiesoTowar WHERE ZamowienieId = @id", cnL))
            {
                cmd.Parameters.AddWithValue("@id", zamowienieId);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    wynik.Pozycje.Add(new PozycjaZamowienia
                    {
                        KodTowaru = rd.IsDBNull(0) ? 0 : Convert.ToInt32(rd.GetValue(0)),
                        Ilosc     = rd.IsDBNull(1) ? 0m : Convert.ToDecimal(rd.GetValue(1)),
                        Pojemniki = rd.IsDBNull(2) ? null : (int?)Convert.ToInt32(rd.GetValue(2))
                    });
                }
            }

            // Nazwy towarów z HANDEL HM.TW
            if (wynik.Pozycje.Count > 0)
            {
                string ids = string.Join(",", wynik.Pozycje.Select(p => p.KodTowaru));
                try
                {
                    await using var cnH = new SqlConnection(ConnHandel);
                    await cnH.OpenAsync();
                    await using var cmd = new SqlCommand(
                        $"SELECT id, ISNULL(kod,'') AS kod, ISNULL(nazwa,'') AS nazwa FROM [HANDEL].[HM].[TW] WHERE id IN ({ids})", cnH);
                    var nazwy = new Dictionary<int, string>();
                    await using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        int id = Convert.ToInt32(rd.GetValue(0));
                        string kod = rd.GetString(1);
                        string nazwa = rd.GetString(2);
                        // Preferujemy PEŁNĄ nazwę (tw.nazwa); fallback do kodu gdy nazwa pusta
                        nazwy[id] = !string.IsNullOrWhiteSpace(nazwa) ? nazwa : kod;
                    }
                    foreach (var p in wynik.Pozycje)
                        if (nazwy.TryGetValue(p.KodTowaru, out var n)) p.Nazwa = n;
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HM.TW lookup] {ex.Message}"); }
            }

            // Klient (z HANDEL kontrahentów)
            if (klientId.HasValue)
            {
                try
                {
                    await using var cnH = new SqlConnection(ConnHandel);
                    await cnH.OpenAsync();
                    await using var cmd = new SqlCommand(
                        @"SELECT TOP 1 ISNULL(C.Name,'')        AS KhName,
                                       ISNULL(POA.Street,'')    AS Street,
                                       ISNULL(POA.PostCode,'')  AS PostCode,
                                       ISNULL(POA.Place,'')     AS Place
                          FROM [HANDEL].[SSCommon].[STContractors] C
                          LEFT JOIN [HANDEL].[SSCommon].[STPostOfficeAddresses] POA
                              ON POA.ContactGuid = C.ContactGuid
                              AND POA.AddressName = N'adres domyślny'
                          WHERE C.Id = @kid", cnH);
                    cmd.Parameters.AddWithValue("@kid", klientId.Value);
                    await using var rd = await cmd.ExecuteReaderAsync();
                    if (await rd.ReadAsync())
                    {
                        wynik.KlientNazwa = rd.GetString(0).Trim();
                        string street   = rd.GetString(1).Trim();
                        string postCode = rd.GetString(2).Trim();
                        string place    = rd.GetString(3).Trim();
                        var parts = new List<string>();
                        if (!string.IsNullOrWhiteSpace(street)) parts.Add(street);
                        string post = $"{postCode} {place}".Trim();
                        if (!string.IsNullOrWhiteSpace(post)) parts.Add(post);
                        wynik.KlientAdres = string.Join(", ", parts);
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Klient lookup] {ex.Message}"); }
            }

            return wynik;
        }

        // ── AUTO-FILL: pozycje wprost z faktury Symfonii (HM.DP) ────────────
        // Używane gdy zamówienie ma już NumerFaktury — pobieramy DOKŁADNIE to co jest
        // na dokumencie Symfonii (ilości, nazwy, opakowania). To preferowane źródło
        // gdy faktura już istnieje, bo zamówienie mogło się zmienić po fakturowaniu.
        public class InvoiceAutoFill
        {
            public string NumerFaktury { get; set; } = "";
            public int? Khid { get; set; }
            public string KlientNazwa { get; set; } = "";
            public string KlientAdres { get; set; } = "";
            public DateTime? DataWystawienia { get; set; }
            public DateTime? DataSprzedazy { get; set; }
            public decimal SumaIlosc { get; set; }
            public List<InvoicePos> Pozycje { get; set; } = new();
        }
        public class InvoicePos
        {
            public int Lp { get; set; }
            public int Idtw { get; set; }
            public string Nazwa { get; set; } = "";   // HM.TW.kod (lub nazwa)
            public decimal Ilosc { get; set; }
            public string Jm { get; set; } = "";
        }

        public async Task<InvoiceAutoFill?> LoadFromInvoiceAsync(string? numerFaktury)
        {
            if (string.IsNullOrWhiteSpace(numerFaktury))
            {
                Kalendarz1.HDI.HdiDiag.Warn("HdiService.Invoice", "Pusty numerFaktury — return null");
                return null;
            }
            using var _ = Kalendarz1.HDI.HdiDiag.Scope("HdiService.Invoice", $"LoadFromInvoiceAsync('{numerFaktury}')");
            InvoiceAutoFill? wynik = null;
            try
            {
                var swCn = System.Diagnostics.Stopwatch.StartNew();
                await using var cn = new SqlConnection(ConnHandel);
                await cn.OpenAsync();
                Kalendarz1.HDI.HdiDiag.Time("HdiService.Invoice", "SqlConnection.Open(HANDEL)", swCn.ElapsedMilliseconds);

                // Header faktury — klient + adres + data. Anulowane WYŁĄCZONE.
                // Sage Symfonia: kh.Name = pełna nazwa; adres w osobnej tabeli STPostOfficeAddresses
                // joined przez ContactGuid (AddressName = N'adres domyślny').
                const string headerSql = @"SELECT TOP 1 dk.khid,
                                                  ISNULL(kh.Name, '')      AS KhName,
                                                  ISNULL(POA.Street, '')   AS Street,
                                                  ISNULL(POA.PostCode, '') AS PostCode,
                                                  ISNULL(POA.Place, '')    AS Place,
                                                  dk.data
                                           FROM HM.DK dk
                                           LEFT JOIN SSCommon.STContractors kh ON kh.Id = dk.khid
                                           LEFT JOIN SSCommon.STPostOfficeAddresses POA
                                               ON POA.ContactGuid = kh.ContactGuid
                                               AND POA.AddressName = N'adres domyślny'
                                           WHERE dk.kod = @kod AND ISNULL(dk.anulowany, 0) = 0";
                await using (var cmdHdr = new SqlCommand(headerSql, cn))
                {
                    cmdHdr.Parameters.AddWithValue("@kod", numerFaktury);
                    await using var rd = await cmdHdr.ExecuteReaderAsync();
                    if (await rd.ReadAsync())
                    {
                        string klientNazwa = (rd.IsDBNull(1) ? "" : rd.GetString(1)).Trim();
                        string street      = (rd.IsDBNull(2) ? "" : rd.GetString(2)).Trim();
                        string postCode    = (rd.IsDBNull(3) ? "" : rd.GetString(3)).Trim();
                        string place       = (rd.IsDBNull(4) ? "" : rd.GetString(4)).Trim();
                        // Zbuduj adres w czytelnej formie: "ul. X 1, 00-000 Miasto"
                        var parts = new List<string>();
                        if (!string.IsNullOrWhiteSpace(street)) parts.Add(street);
                        string post = $"{postCode} {place}".Trim();
                        if (!string.IsNullOrWhiteSpace(post)) parts.Add(post);
                        string adres = string.Join(", ", parts);

                        wynik = new InvoiceAutoFill
                        {
                            NumerFaktury = numerFaktury,
                            Khid = rd.IsDBNull(0) ? null : (int?)Convert.ToInt32(rd.GetValue(0)),
                            KlientNazwa = klientNazwa,
                            KlientAdres = adres,
                            DataWystawienia = rd.IsDBNull(5) ? null : (DateTime?)rd.GetDateTime(5),
                            DataSprzedazy = null
                        };
                    }
                }
                if (wynik == null) return null;

                // Pozycje faktury w kolejności dokumentu (lp) + join z HM.TW dla nazwy
                const string posSql = @"SELECT dp.lp, dp.idtw, dp.ilosc, ISNULL(dp.jm, '') AS jm,
                                               ISNULL(tw.kod, '') AS twKod, ISNULL(tw.nazwa, '') AS twNazwa
                                        FROM HM.DP dp
                                        INNER JOIN HM.DK dk ON dk.id = dp.super
                                        LEFT JOIN HM.TW tw ON tw.id = dp.idtw
                                        WHERE dk.kod = @kod AND ISNULL(dk.anulowany, 0) = 0
                                        ORDER BY dp.lp";
                await using (var cmdP = new SqlCommand(posSql, cn))
                {
                    cmdP.Parameters.AddWithValue("@kod", numerFaktury);
                    cmdP.CommandTimeout = 30;
                    await using var rd = await cmdP.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        int lp = rd.IsDBNull(0) ? 0 : Convert.ToInt32(rd.GetValue(0));
                        int idtw = rd.IsDBNull(1) ? 0 : Convert.ToInt32(rd.GetValue(1));
                        decimal ilosc = rd.IsDBNull(2) ? 0 : Convert.ToDecimal(rd.GetValue(2));
                        string jm = rd.IsDBNull(3) ? "" : rd.GetString(3);
                        string twKod = rd.IsDBNull(4) ? "" : rd.GetString(4);
                        string twNazwa = rd.IsDBNull(5) ? "" : rd.GetString(5);
                        wynik.Pozycje.Add(new InvoicePos
                        {
                            Lp = lp, Idtw = idtw,
                            // PEŁNA nazwa towaru z HM.TW.nazwa (preferowane) — fallback do kodu gdy nazwa pusta.
                            Nazwa = !string.IsNullOrWhiteSpace(twNazwa) ? twNazwa : twKod,
                            Ilosc = ilosc, Jm = jm
                        });
                        wynik.SumaIlosc += ilosc;
                    }
                }
            }
            catch (Exception ex) { Kalendarz1.HDI.HdiDiag.Error("HdiService.Invoice", "LoadFromInvoiceAsync FAIL", ex); }
            if (wynik != null)
                Kalendarz1.HDI.HdiDiag.Log("HdiService.Invoice", $"OK · klient='{wynik.KlientNazwa}' khid={wynik.Khid} pozycji={wynik.Pozycje.Count} suma={wynik.SumaIlosc}kg adres='{wynik.KlientAdres}'");
            else
                Kalendarz1.HDI.HdiDiag.Warn("HdiService.Invoice", $"Faktura '{numerFaktury}' NIE ZNALEZIONA (header reader nie zwrócił wiersza)");
            return wynik;
        }

        // ── AUTO-FILL: partie z listapartii dla danej daty uboju ────────────
        public async Task<List<HdiPartia>> LoadPartieDlaDniaAsync(DateTime dataUboju, string asortymentNazwa = "")
        {
            using var _ = Kalendarz1.HDI.HdiDiag.Scope("HdiService.Partie", $"LoadPartieDlaDnia({dataUboju:dd.MM.yyyy}, '{asortymentNazwa}')");
            var lista = new List<HdiPartia>();
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(
                    @"SELECT TOP 20 lp.Partia, lp.CreateData
                      FROM dbo.listapartii lp
                      WHERE CAST(lp.CreateData AS DATE) = @d
                      ORDER BY lp.Partia", cn);
                cmd.Parameters.AddWithValue("@d", dataUboju.Date);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int partia = Convert.ToInt32(rd.GetValue(0));   // defensywnie — bywa smallint/int/numeric
                    DateTime dataUbojuLib = rd.GetDateTime(1);
                    // Default termin przydatności: +6 miesięcy (zgodnie z wzorcem przykładu HDI)
                    lista.Add(new HdiPartia
                    {
                        Asortyment = asortymentNazwa,
                        NumerPartii = partia.ToString("000"),
                        DataUboju = dataUbojuLib.Date,
                        DataMrozenia = dataUbojuLib.Date,
                        DataPrzydatnosci = dataUbojuLib.Date.AddMonths(6)
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Partie load] {ex.Message}"); }
            return lista;
        }

        // ── Helpers ─────────────────────────────────────────────────────────
        private static HdiDokument MapDokument(SqlDataReader rd)
        {
            return new HdiDokument
            {
                Id = (int)rd["Id"],
                Numer = (int)rd["Numer"],
                Rok = (int)rd["Rok"],
                ZamowienieId = rd["ZamowienieId"] == DBNull.Value ? null : (int?)rd["ZamowienieId"],
                KlientId = rd["KlientId"] == DBNull.Value ? null : (int?)rd["KlientId"],
                KlientNazwa = rd["KlientNazwa"] == DBNull.Value ? "" : (string)rd["KlientNazwa"],
                KlientAdres = rd["KlientAdres"] == DBNull.Value ? "" : (string)rd["KlientAdres"],
                OpisTowaru = rd["OpisTowaru"] == DBNull.Value ? "" : (string)rd["OpisTowaru"],
                RodzajOpakowan = rd["RodzajOpakowan"] == DBNull.Value ? "" : (string)rd["RodzajOpakowan"],
                LiczbaOpakowan = rd["LiczbaOpakowan"] == DBNull.Value ? null : (int?)rd["LiczbaOpakowan"],
                WagaNetto = rd["WagaNetto"] == DBNull.Value ? null : (decimal?)rd["WagaNetto"],
                WagaBrutto = rd["WagaBrutto"] == DBNull.Value ? null : (decimal?)rd["WagaBrutto"],
                Pochodzenie = rd["Pochodzenie"] == DBNull.Value ? "" : (string)rd["Pochodzenie"],
                MiejscePozyskania = rd["MiejscePozyskania"] == DBNull.Value ? "" : (string)rd["MiejscePozyskania"],
                DataWysylki = rd["DataWysylki"] == DBNull.Value ? null : (DateTime?)rd["DataWysylki"],
                MiejscePrzeznaczenia = rd["MiejscePrzeznaczenia"] == DBNull.Value ? "" : (string)rd["MiejscePrzeznaczenia"],
                NumerRejestracyjny = rd["NumerRejestracyjny"] == DBNull.Value ? "" : (string)rd["NumerRejestracyjny"],
                NumerRejNaczepy = HasColumn(rd, "NumerRejNaczepy") && rd["NumerRejNaczepy"] != DBNull.Value ? (string)rd["NumerRejNaczepy"] : "",
                UwagiTransport = rd["UwagiTransport"] == DBNull.Value ? "" : (string)rd["UwagiTransport"],
                UwagiTechnologia = rd["UwagiTechnologia"] == DBNull.Value ? "" : (string)rd["UwagiTechnologia"],
                RynekKrajowy = rd["RynekKrajowy"] != DBNull.Value && (bool)rd["RynekKrajowy"],
                RynekUE = rd["RynekUE"] != DBNull.Value && (bool)rd["RynekUE"],
                RynekInny = rd["RynekInny"] != DBNull.Value && (bool)rd["RynekInny"],
                InnePanstwo = rd["InnePanstwo"] == DBNull.Value ? "" : (string)rd["InnePanstwo"],
                MiejscowoscWystawienia = rd["MiejscowoscWystawienia"] == DBNull.Value ? "" : (string)rd["MiejscowoscWystawienia"],
                DataWystawienia = (DateTime)rd["DataWystawienia"],
                UtworzonoPrzez = rd["UtworzonoPrzez"] == DBNull.Value ? "" : (string)rd["UtworzonoPrzez"],
                Wystawiajacy = HasColumn(rd, "Wystawiajacy") && rd["Wystawiajacy"] != DBNull.Value ? (string)rd["Wystawiajacy"] : "",
                Status = rd["Status"] == DBNull.Value ? "AKTYWNY" : (string)rd["Status"]
            };
        }

        private static bool HasColumn(SqlDataReader rd, string name)
        {
            for (int i = 0; i < rd.FieldCount; i++)
                if (string.Equals(rd.GetName(i), name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void BindParams(SqlCommand cmd, HdiDokument d)
        {
            cmd.Parameters.AddWithValue("@Numer", d.Numer);
            cmd.Parameters.AddWithValue("@Rok", d.Rok);
            cmd.Parameters.AddWithValue("@ZamowienieId", (object?)d.ZamowienieId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@KlientId", (object?)d.KlientId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@KlientNazwa", (object?)d.KlientNazwa ?? "");
            cmd.Parameters.AddWithValue("@KlientAdres", (object?)d.KlientAdres ?? "");
            cmd.Parameters.AddWithValue("@OpisTowaru", (object?)d.OpisTowaru ?? "");
            cmd.Parameters.AddWithValue("@RodzajOpakowan", (object?)d.RodzajOpakowan ?? "");
            cmd.Parameters.AddWithValue("@LiczbaOpakowan", (object?)d.LiczbaOpakowan ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@WagaNetto", (object?)d.WagaNetto ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@WagaBrutto", (object?)d.WagaBrutto ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Pochodzenie", (object?)d.Pochodzenie ?? "");
            cmd.Parameters.AddWithValue("@MiejscePozyskania", (object?)d.MiejscePozyskania ?? "");
            cmd.Parameters.AddWithValue("@DataWysylki", (object?)d.DataWysylki ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MiejscePrzeznaczenia", (object?)d.MiejscePrzeznaczenia ?? "");
            cmd.Parameters.AddWithValue("@NumerRejestracyjny", (object?)d.NumerRejestracyjny ?? "");
            cmd.Parameters.AddWithValue("@NumerRejNaczepy", (object?)d.NumerRejNaczepy ?? "");
            cmd.Parameters.AddWithValue("@UwagiTransport", (object?)d.UwagiTransport ?? "");
            cmd.Parameters.AddWithValue("@UwagiTechnologia", (object?)d.UwagiTechnologia ?? "");
            cmd.Parameters.AddWithValue("@RynekKrajowy", d.RynekKrajowy);
            cmd.Parameters.AddWithValue("@RynekUE", d.RynekUE);
            cmd.Parameters.AddWithValue("@RynekInny", d.RynekInny);
            cmd.Parameters.AddWithValue("@InnePanstwo", (object?)d.InnePanstwo ?? "");
            cmd.Parameters.AddWithValue("@MiejscowoscWystawienia", (object?)d.MiejscowoscWystawienia ?? "");
            cmd.Parameters.AddWithValue("@DataWystawienia", d.DataWystawienia);
            cmd.Parameters.AddWithValue("@UtworzonoPrzez", (object?)d.UtworzonoPrzez ?? "");
            cmd.Parameters.AddWithValue("@Wystawiajacy", (object?)d.Wystawiajacy ?? "");
            cmd.Parameters.AddWithValue("@Status", (object?)d.Status ?? "AKTYWNY");
        }
    }
}
