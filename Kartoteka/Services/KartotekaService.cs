using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.Kartoteka.Models;

namespace Kalendarz1.Kartoteka.Services
{
    public class KartotekaService
    {
        private readonly string _libraNetConnectionString;
        private readonly string _handelConnectionString;

        public KartotekaService(string libraNetConnectionString, string handelConnectionString)
        {
            _libraNetConnectionString = libraNetConnectionString;
            _handelConnectionString = handelConnectionString;
        }

        public async Task EnsureTablesExistAsync()
        {
            using var conn = new SqlConnection(_libraNetConnectionString);
            await conn.OpenAsync();

            var sql = @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KartotekaOdbiorcyDane')
BEGIN
    CREATE TABLE dbo.KartotekaOdbiorcyDane (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        IdSymfonia INT NOT NULL,
        OsobaKontaktowa NVARCHAR(100),
        TelefonKontakt NVARCHAR(50),
        EmailKontakt NVARCHAR(100),
        Asortyment NVARCHAR(500),
        PreferencjePakowania NVARCHAR(200),
        PreferencjeJakosci NVARCHAR(200),
        PreferencjeDostawy NVARCHAR(200),
        PreferowanyDzienDostawy NVARCHAR(100),
        PreferowanaGodzinaDostawy NVARCHAR(50),
        AdresDostawyInny NVARCHAR(300),
        Trasa NVARCHAR(50),
        KategoriaHandlowca CHAR(1) DEFAULT 'C',
        Notatki NVARCHAR(MAX),
        DataUtworzenia DATETIME DEFAULT GETDATE(),
        DataModyfikacji DATETIME,
        ModyfikowalPrzez NVARCHAR(50),
        CONSTRAINT UQ_KartotekaOdbiorcyDane_IdSymfonia UNIQUE (IdSymfonia)
    );
    CREATE INDEX IX_KartotekaOdbiorcyDane_IdSymfonia ON dbo.KartotekaOdbiorcyDane(IdSymfonia);
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KartotekaOdbiorcyKontakty')
BEGIN
    CREATE TABLE dbo.KartotekaOdbiorcyKontakty (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        IdSymfonia INT NOT NULL,
        TypKontaktu NVARCHAR(50) NOT NULL,
        Imie NVARCHAR(50),
        Nazwisko NVARCHAR(50),
        Telefon NVARCHAR(50),
        Email NVARCHAR(100),
        Stanowisko NVARCHAR(100),
        Notatka NVARCHAR(500),
        DataUtworzenia DATETIME DEFAULT GETDATE(),
        DataModyfikacji DATETIME
    );
    CREATE INDEX IX_KartotekaOdbiorcyKontakty_IdSymfonia ON dbo.KartotekaOdbiorcyKontakty(IdSymfonia);
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KartotekaOdbiorcyNotatki')
BEGIN
    CREATE TABLE dbo.KartotekaOdbiorcyNotatki (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        IdSymfonia INT NOT NULL,
        Tresc NVARCHAR(MAX) NOT NULL,
        Autor NVARCHAR(100),
        DataUtworzenia DATETIME DEFAULT GETDATE()
    );
    CREATE INDEX IX_KartotekaOdbiorcyNotatki_IdSymfonia ON dbo.KartotekaOdbiorcyNotatki(IdSymfonia);
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KartotekaTypyKontaktow')
BEGIN
    CREATE TABLE dbo.KartotekaTypyKontaktow (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Nazwa NVARCHAR(50) NOT NULL,
        Ikona NVARCHAR(10),
        Kolejnosc INT DEFAULT 0
    );
    INSERT INTO dbo.KartotekaTypyKontaktow (Nazwa, Ikona, Kolejnosc) VALUES
    (N'Główny', N'U+1F464', 1),
    (N'Księgowość', N'U+1F4CA', 2),
    (N'Opakowania', N'U+1F4E6', 3),
    (N'Właściciel', N'U+1F454', 4),
    (N'Magazyn', N'U+1F3ED', 5),
    (N'Inny', N'U+1F4CB', 99);
END;
";
            using var cmd = new SqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<OdbiorcaHandlowca>> PobierzOdbiorcowAsync(string handlowiec = null, bool pokazWszystkich = false)
        {
            var result = new List<OdbiorcaHandlowca>();

            using var conn = new SqlConnection(_handelConnectionString);
            await conn.OpenAsync();

            var sql = @"
SELECT
    C.Id AS IdSymfonia,
    C.Name AS NazwaFirmy,
    ISNULL(C.Shortcut, '') AS Skrot,
    ISNULL(POA.Place, '') AS Miasto,
    ISNULL(POA.Street, '') AS Ulica,
    ISNULL(POA.PostCode, '') AS KodPocztowy,
    ISNULL(C.NIP, '') AS NIP,
    ISNULL(C.LimitAmount, 0) AS LimitKupiecki,
    ISNULL(WYM.CDim_Handlowiec_Val, N'Nieprzypisany') AS Handlowiec,
    ISNULL((
        SELECT SUM(DK.walbrutto - ISNULL(PN.KwotaRozliczona, 0))
        FROM [HM].[DK] DK
        LEFT JOIN (
            SELECT dkid, SUM(kwotarozl) AS KwotaRozliczona
            FROM [HM].[PN]
            GROUP BY dkid
        ) PN ON PN.dkid = DK.id
        WHERE DK.khid = C.Id
          AND DK.typ_dk IN ('FVS', 'FVR', 'FVZ')
          AND DK.aktywny = 1
          AND DK.anulowany = 0
          AND DK.ok = 0
          AND (DK.walbrutto - ISNULL(PN.KwotaRozliczona, 0)) > 0.01
    ), 0) AS WykorzystanoLimit,
    ISNULL((
        SELECT SUM(DK.walbrutto - ISNULL(PN.KwotaRozliczona, 0))
        FROM [HM].[DK] DK
        LEFT JOIN (
            SELECT dkid, SUM(kwotarozl) AS KwotaRozliczona,
                   MAX(Termin) AS TerminPrawdziwy
            FROM [HM].[PN]
            GROUP BY dkid
        ) PN ON PN.dkid = DK.id
        WHERE DK.khid = C.Id
          AND DK.typ_dk IN ('FVS', 'FVR', 'FVZ')
          AND DK.aktywny = 1
          AND DK.anulowany = 0
          AND DK.ok = 0
          AND (DK.walbrutto - ISNULL(PN.KwotaRozliczona, 0)) > 0.01
          AND GETDATE() > ISNULL(PN.TerminPrawdziwy, DK.plattermin)
    ), 0) AS KwotaPrzeterminowana,
    (
        SELECT TOP 1 DK.data
        FROM [HM].[DK] DK
        WHERE DK.khid = C.Id
          AND DK.typ_dk = 'FVS'
          AND DK.aktywny = 1
          AND DK.anulowany = 0
        ORDER BY DK.data DESC
    ) AS OstatniaFakturaData
FROM [SSCommon].[STContractors] C
LEFT JOIN [SSCommon].[STPostOfficeAddresses] POA
    ON POA.ContactGuid = C.ContactGuid
    AND POA.AddressName = N'adres domyślny'
LEFT JOIN [SSCommon].[ContractorClassification] WYM
    ON C.Id = WYM.ElementId
WHERE
    (@PokazWszystkich = 1 OR ISNULL(WYM.CDim_Handlowiec_Val, '') = @Handlowiec)
ORDER BY C.Name;
";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@PokazWszystkich", pokazWszystkich ? 1 : 0);
            cmd.Parameters.AddWithValue("@Handlowiec", handlowiec ?? "");

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new OdbiorcaHandlowca
                {
                    IdSymfonia = reader.GetInt32(reader.GetOrdinal("IdSymfonia")),
                    NazwaFirmy = reader["NazwaFirmy"]?.ToString() ?? "",
                    Skrot = reader["Skrot"]?.ToString() ?? "",
                    Miasto = reader["Miasto"]?.ToString() ?? "",
                    Ulica = reader["Ulica"]?.ToString() ?? "",
                    KodPocztowy = reader["KodPocztowy"]?.ToString() ?? "",
                    NIP = reader["NIP"]?.ToString() ?? "",
                    LimitKupiecki = reader.IsDBNull(reader.GetOrdinal("LimitKupiecki")) ? 0 : Convert.ToDecimal(reader["LimitKupiecki"]),
                    Handlowiec = reader["Handlowiec"]?.ToString() ?? "",
                    WykorzystanoLimit = reader.IsDBNull(reader.GetOrdinal("WykorzystanoLimit")) ? 0 : Convert.ToDecimal(reader["WykorzystanoLimit"]),
                    KwotaPrzeterminowana = reader.IsDBNull(reader.GetOrdinal("KwotaPrzeterminowana")) ? 0 : Convert.ToDecimal(reader["KwotaPrzeterminowana"]),
                    OstatniaFakturaData = reader.IsDBNull(reader.GetOrdinal("OstatniaFakturaData")) ? null : (DateTime?)Convert.ToDateTime(reader["OstatniaFakturaData"])
                });
            }

            return result;
        }

        public async Task WczytajDaneWlasneAsync(List<OdbiorcaHandlowca> odbiorcy)
        {
            if (odbiorcy == null || odbiorcy.Count == 0) return;

            using var conn = new SqlConnection(_libraNetConnectionString);
            await conn.OpenAsync();

            var sql = @"SELECT IdSymfonia, OsobaKontaktowa, TelefonKontakt, EmailKontakt,
                               Asortyment, PreferencjePakowania, PreferencjeJakosci, PreferencjeDostawy,
                               PreferowanyDzienDostawy, PreferowanaGodzinaDostawy, AdresDostawyInny,
                               Trasa, KategoriaHandlowca, Notatki, DataModyfikacji, ModyfikowalPrzez
                        FROM dbo.KartotekaOdbiorcyDane";

            var dane = new Dictionary<int, OdbiorcaHandlowca>();
            using (var cmd = new SqlCommand(sql, conn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(reader.GetOrdinal("IdSymfonia"));
                    dane[id] = new OdbiorcaHandlowca
                    {
                        OsobaKontaktowa = reader["OsobaKontaktowa"]?.ToString(),
                        TelefonKontakt = reader["TelefonKontakt"]?.ToString(),
                        EmailKontakt = reader["EmailKontakt"]?.ToString(),
                        Asortyment = reader["Asortyment"]?.ToString(),
                        PreferencjePakowania = reader["PreferencjePakowania"]?.ToString(),
                        PreferencjeJakosci = reader["PreferencjeJakosci"]?.ToString(),
                        PreferencjeDostawy = reader["PreferencjeDostawy"]?.ToString(),
                        PreferowanyDzienDostawy = reader["PreferowanyDzienDostawy"]?.ToString(),
                        PreferowanaGodzinaDostawy = reader["PreferowanaGodzinaDostawy"]?.ToString(),
                        AdresDostawyInny = reader["AdresDostawyInny"]?.ToString(),
                        Trasa = reader["Trasa"]?.ToString(),
                        KategoriaHandlowca = reader["KategoriaHandlowca"]?.ToString()?.Trim() ?? "C",
                        Notatki = reader["Notatki"]?.ToString(),
                        DataModyfikacji = reader.IsDBNull(reader.GetOrdinal("DataModyfikacji")) ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("DataModyfikacji")),
                        ModyfikowalPrzez = reader["ModyfikowalPrzez"]?.ToString()
                    };
                }
            }

            foreach (var o in odbiorcy)
            {
                if (dane.TryGetValue(o.IdSymfonia, out var d))
                {
                    o.OsobaKontaktowa = d.OsobaKontaktowa;
                    o.TelefonKontakt = d.TelefonKontakt;
                    o.EmailKontakt = d.EmailKontakt;
                    o.Asortyment = d.Asortyment;
                    o.PreferencjePakowania = d.PreferencjePakowania;
                    o.PreferencjeJakosci = d.PreferencjeJakosci;
                    o.PreferencjeDostawy = d.PreferencjeDostawy;
                    o.PreferowanyDzienDostawy = d.PreferowanyDzienDostawy;
                    o.PreferowanaGodzinaDostawy = d.PreferowanaGodzinaDostawy;
                    o.AdresDostawyInny = d.AdresDostawyInny;
                    o.Trasa = d.Trasa;
                    o.KategoriaHandlowca = d.KategoriaHandlowca;
                    o.Notatki = d.Notatki;
                    o.DataModyfikacji = d.DataModyfikacji;
                    o.ModyfikowalPrzez = d.ModyfikowalPrzez;
                }
            }
        }

        public async Task ZapiszDaneWlasneAsync(OdbiorcaHandlowca odbiorca, string uzytkownik)
        {
            using var conn = new SqlConnection(_libraNetConnectionString);
            await conn.OpenAsync();

            var sql = @"
MERGE dbo.KartotekaOdbiorcyDane AS target
USING (SELECT @IdSymfonia AS IdSymfonia) AS source
ON target.IdSymfonia = source.IdSymfonia
WHEN MATCHED THEN
    UPDATE SET
        OsobaKontaktowa = @OsobaKontaktowa,
        TelefonKontakt = @TelefonKontakt,
        EmailKontakt = @EmailKontakt,
        Asortyment = @Asortyment,
        PreferencjePakowania = @PreferencjePakowania,
        PreferencjeJakosci = @PreferencjeJakosci,
        PreferencjeDostawy = @PreferencjeDostawy,
        PreferowanyDzienDostawy = @PreferowanyDzienDostawy,
        PreferowanaGodzinaDostawy = @PreferowanaGodzinaDostawy,
        AdresDostawyInny = @AdresDostawyInny,
        Trasa = @Trasa,
        KategoriaHandlowca = @Kategoria,
        Notatki = @Notatki,
        DataModyfikacji = GETDATE(),
        ModyfikowalPrzez = @Uzytkownik
WHEN NOT MATCHED THEN
    INSERT (IdSymfonia, OsobaKontaktowa, TelefonKontakt, EmailKontakt,
            Asortyment, PreferencjePakowania, PreferencjeJakosci, PreferencjeDostawy,
            PreferowanyDzienDostawy, PreferowanaGodzinaDostawy, AdresDostawyInny,
            Trasa, KategoriaHandlowca, Notatki, ModyfikowalPrzez)
    VALUES (@IdSymfonia, @OsobaKontaktowa, @TelefonKontakt, @EmailKontakt,
            @Asortyment, @PreferencjePakowania, @PreferencjeJakosci, @PreferencjeDostawy,
            @PreferowanyDzienDostawy, @PreferowanaGodzinaDostawy, @AdresDostawyInny,
            @Trasa, @Kategoria, @Notatki, @Uzytkownik);
";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@IdSymfonia", odbiorca.IdSymfonia);
            cmd.Parameters.AddWithValue("@OsobaKontaktowa", (object)odbiorca.OsobaKontaktowa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TelefonKontakt", (object)odbiorca.TelefonKontakt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EmailKontakt", (object)odbiorca.EmailKontakt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Asortyment", (object)odbiorca.Asortyment ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PreferencjePakowania", (object)odbiorca.PreferencjePakowania ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PreferencjeJakosci", (object)odbiorca.PreferencjeJakosci ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PreferencjeDostawy", (object)odbiorca.PreferencjeDostawy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PreferowanyDzienDostawy", (object)odbiorca.PreferowanyDzienDostawy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PreferowanaGodzinaDostawy", (object)odbiorca.PreferowanaGodzinaDostawy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AdresDostawyInny", (object)odbiorca.AdresDostawyInny ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Trasa", (object)odbiorca.Trasa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Kategoria", odbiorca.KategoriaHandlowca ?? "C");
            cmd.Parameters.AddWithValue("@Notatki", (object)odbiorca.Notatki ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Uzytkownik", uzytkownik ?? "");

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<KontaktOdbiorcy>> PobierzKontaktyAsync(int idSymfonia)
        {
            var result = new List<KontaktOdbiorcy>();

            using var conn = new SqlConnection(_libraNetConnectionString);
            await conn.OpenAsync();

            var sql = @"SELECT Id, IdSymfonia, TypKontaktu, Imie, Nazwisko, Telefon, Email, Stanowisko, Notatka, DataUtworzenia, DataModyfikacji
                        FROM dbo.KartotekaOdbiorcyKontakty
                        WHERE IdSymfonia = @IdSymfonia
                        ORDER BY CASE TypKontaktu
                            WHEN N'Główny' THEN 1
                            WHEN N'Księgowość' THEN 2
                            WHEN N'Opakowania' THEN 3
                            WHEN N'Właściciel' THEN 4
                            WHEN N'Magazyn' THEN 5
                            ELSE 99 END";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@IdSymfonia", idSymfonia);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new KontaktOdbiorcy
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    IdSymfonia = reader.GetInt32(reader.GetOrdinal("IdSymfonia")),
                    TypKontaktu = reader["TypKontaktu"]?.ToString() ?? "",
                    Imie = reader["Imie"]?.ToString() ?? "",
                    Nazwisko = reader["Nazwisko"]?.ToString() ?? "",
                    Telefon = reader["Telefon"]?.ToString() ?? "",
                    Email = reader["Email"]?.ToString() ?? "",
                    Stanowisko = reader["Stanowisko"]?.ToString() ?? "",
                    Notatka = reader["Notatka"]?.ToString() ?? "",
                    DataUtworzenia = reader.GetDateTime(reader.GetOrdinal("DataUtworzenia")),
                    DataModyfikacji = reader.IsDBNull(reader.GetOrdinal("DataModyfikacji")) ? null : (DateTime?)reader.GetDateTime(reader.GetOrdinal("DataModyfikacji"))
                });
            }

            return result;
        }

        public async Task ZapiszKontaktAsync(KontaktOdbiorcy kontakt)
        {
            using var conn = new SqlConnection(_libraNetConnectionString);
            await conn.OpenAsync();

            string sql;
            if (kontakt.Id > 0)
            {
                sql = @"UPDATE dbo.KartotekaOdbiorcyKontakty SET
                            TypKontaktu = @TypKontaktu, Imie = @Imie, Nazwisko = @Nazwisko,
                            Telefon = @Telefon, Email = @Email, Stanowisko = @Stanowisko,
                            Notatka = @Notatka, DataModyfikacji = GETDATE()
                        WHERE Id = @Id";
            }
            else
            {
                sql = @"INSERT INTO dbo.KartotekaOdbiorcyKontakty
                            (IdSymfonia, TypKontaktu, Imie, Nazwisko, Telefon, Email, Stanowisko, Notatka)
                        VALUES (@IdSymfonia, @TypKontaktu, @Imie, @Nazwisko, @Telefon, @Email, @Stanowisko, @Notatka)";
            }

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", kontakt.Id);
            cmd.Parameters.AddWithValue("@IdSymfonia", kontakt.IdSymfonia);
            cmd.Parameters.AddWithValue("@TypKontaktu", kontakt.TypKontaktu ?? "Inny");
            cmd.Parameters.AddWithValue("@Imie", (object)kontakt.Imie ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Nazwisko", (object)kontakt.Nazwisko ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Telefon", (object)kontakt.Telefon ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Email", (object)kontakt.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Stanowisko", (object)kontakt.Stanowisko ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Notatka", (object)kontakt.Notatka ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UsunKontaktAsync(int id)
        {
            using var conn = new SqlConnection(_libraNetConnectionString);
            await conn.OpenAsync();

            using var cmd = new SqlCommand("DELETE FROM dbo.KartotekaOdbiorcyKontakty WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<FakturaOdbiorcy>> PobierzFakturyAsync(int idSymfonia, int ostatnieMiesiecy = 12)
        {
            var result = new List<FakturaOdbiorcy>();

            using var conn = new SqlConnection(_handelConnectionString);
            await conn.OpenAsync();

            var sql = @"SELECT DK.khid, DK.kod AS NumerDokumentu,
                               CAST(DK.walbrutto AS DECIMAL(18,2)) AS brutto,
                               ISNULL(PN.KwotaRozliczona, 0) AS rozliczono,
                               DK.typ_dk AS typ, DK.anulowany,
                               DK.data AS data_faktury,
                               ISNULL(PN.TerminPrawdziwy, DK.plattermin) AS termin_platnosci,
                               ISNULL(GT.GlownyTowar, '') AS GlownyTowar
                        FROM [HM].[DK] DK
                        LEFT JOIN (
                            SELECT dkid,
                                   SUM(kwotarozl) AS KwotaRozliczona,
                                   MAX(Termin) AS TerminPrawdziwy
                            FROM [HM].[PN]
                            GROUP BY dkid
                        ) PN ON PN.dkid = DK.id
                        OUTER APPLY (
                            SELECT TOP 1 TW.nazwa AS GlownyTowar
                            FROM [HM].[DP] DP
                            INNER JOIN [HM].[TW] TW ON DP.idtw = TW.ID
                            WHERE DP.super = DK.id AND DP.ilosc > 0
                            ORDER BY DP.cena * DP.ilosc DESC
                        ) GT
                        WHERE DK.khid = @IdSymfonia
                          AND DK.typ_dk IN ('FVS', 'FVR', 'FVZ')
                          AND DK.aktywny = 1
                          AND DK.data >= DATEADD(MONTH, -@Miesiace, GETDATE())
                        ORDER BY DK.data DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@IdSymfonia", idSymfonia);
            cmd.Parameters.AddWithValue("@Miesiace", ostatnieMiesiecy);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new FakturaOdbiorcy
                {
                    KontrahentId = Convert.ToInt32(reader["khid"]),
                    NumerDokumentu = reader["NumerDokumentu"]?.ToString() ?? "",
                    Brutto = reader.IsDBNull(reader.GetOrdinal("brutto")) ? 0 : Convert.ToDecimal(reader["brutto"]),
                    Rozliczono = reader.IsDBNull(reader.GetOrdinal("rozliczono")) ? 0 : Convert.ToDecimal(reader["rozliczono"]),
                    Typ = reader["typ"]?.ToString() ?? "",
                    GlownyTowar = reader["GlownyTowar"]?.ToString() ?? "",
                    Anulowany = Convert.ToInt16(reader["anulowany"]) != 0,
                    DataFaktury = Convert.ToDateTime(reader["data_faktury"]),
                    TerminPlatnosci = reader.IsDBNull(reader.GetOrdinal("termin_platnosci"))
                        ? Convert.ToDateTime(reader["data_faktury"])
                        : Convert.ToDateTime(reader["termin_platnosci"])
                });
            }

            return result;
        }

        public async Task<List<string>> PobierzHandlowcowAsync()
        {
            var result = new List<string>();

            using var conn = new SqlConnection(_handelConnectionString);
            await conn.OpenAsync();

            var sql = @"SELECT DISTINCT WYM.CDim_Handlowiec_Val
                        FROM [SSCommon].[ContractorClassification] WYM
                        WHERE WYM.CDim_Handlowiec_Val IS NOT NULL AND WYM.CDim_Handlowiec_Val <> ''
                        ORDER BY WYM.CDim_Handlowiec_Val";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(reader.GetString(0));
            }

            return result;
        }

        /// <summary>
        /// Pobiera asortyment (listę produktów) kupowanych przez kontrahentów z ostatnich N miesięcy.
        /// Zwraca słownik: khid -> lista nazw produktów (posortowana wg wartości malejąco).
        /// </summary>
        public async Task<Dictionary<int, string>> PobierzAsortymentAsync(List<int> khids, int ostatnieMiesiecy = 6)
        {
            var result = new Dictionary<int, string>();
            if (khids == null || khids.Count == 0) return result;

            using var conn = new SqlConnection(_handelConnectionString);
            await conn.OpenAsync();

            // Przetwarzaj w partiach po 500
            const int batch = 500;
            for (int i = 0; i < khids.Count; i += batch)
            {
                var batchIds = khids.Skip(i).Take(batch).ToList();
                var idParams = string.Join(",", batchIds);

                var sql = $@"
SELECT
    DK.khid,
    TW.kod AS ProduktKod,
    CAST(SUM(DP.ilosc) AS DECIMAL(18,2)) AS SumaKg
FROM [HM].[DK] DK
INNER JOIN [HM].[DP] DP ON DK.id = DP.super
INNER JOIN [HM].[TW] TW ON DP.idtw = TW.ID
WHERE DK.khid IN ({idParams})
  AND DK.typ_dk IN ('FVS', 'FVR')
  AND DK.aktywny = 1
  AND DK.anulowany = 0
  AND DK.data >= DATEADD(MONTH, -{ostatnieMiesiecy}, GETDATE())
  AND DP.ilosc > 0
  AND TW.katalog IN (67095, 67153)
GROUP BY DK.khid, TW.kod
ORDER BY DK.khid, SumaKg DESC";

                using var cmd = new SqlCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                var perCustomer = new Dictionary<int, List<string>>();
                while (await reader.ReadAsync())
                {
                    var khid = Convert.ToInt32(reader["khid"]);
                    var kod = reader["ProduktKod"]?.ToString() ?? "";
                    if (!perCustomer.ContainsKey(khid))
                        perCustomer[khid] = new List<string>();
                    perCustomer[khid].Add(kod);
                }

                foreach (var kv in perCustomer)
                {
                    result[kv.Key] = string.Join(", ", kv.Value);
                }
            }

            return result;
        }

        /// <summary>
        /// Pobiera szczegółowy asortyment dla jednego kontrahenta (do zakładki Asortyment).
        /// </summary>
        public async Task<List<AsortymentPozycja>> PobierzAsortymentSzczegolyAsync(int khid, int ostatnieMiesiecy = 12)
        {
            var result = new List<AsortymentPozycja>();

            using var conn = new SqlConnection(_handelConnectionString);
            await conn.OpenAsync();

            var sql = @"
SELECT
    TW.kod AS ProduktKod,
    ISNULL(TW.nazwa, '') AS ProduktNazwa,
    CAST(SUM(DP.ilosc) AS DECIMAL(18,2)) AS SumaKg,
    CAST(SUM(DP.cena * DP.ilosc) AS DECIMAL(18,2)) AS SumaWartosc,
    CAST(CASE WHEN SUM(DP.ilosc) > 0 THEN SUM(DP.cena * DP.ilosc) / SUM(DP.ilosc) ELSE 0 END AS DECIMAL(18,2)) AS SredniaCena,
    COUNT(DISTINCT DK.id) AS LiczbaFaktur,
    MAX(DK.data) AS OstatniaSprzedaz
FROM [HM].[DK] DK
INNER JOIN [HM].[DP] DP ON DK.id = DP.super
INNER JOIN [HM].[TW] TW ON DP.idtw = TW.ID
WHERE DK.khid = @KhId
  AND DK.typ_dk IN ('FVS', 'FVR')
  AND DK.aktywny = 1
  AND DK.anulowany = 0
  AND DK.data >= DATEADD(MONTH, -@Miesiace, GETDATE())
  AND DP.ilosc > 0
  AND TW.katalog IN (67095, 67153)
GROUP BY TW.kod, TW.nazwa
ORDER BY SumaWartosc DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@KhId", khid);
            cmd.Parameters.AddWithValue("@Miesiace", ostatnieMiesiecy);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new AsortymentPozycja
                {
                    ProduktKod = reader["ProduktKod"]?.ToString() ?? "",
                    ProduktNazwa = reader["ProduktNazwa"]?.ToString() ?? "",
                    SumaKg = Convert.ToDecimal(reader["SumaKg"]),
                    SumaWartosc = Convert.ToDecimal(reader["SumaWartosc"]),
                    SredniaCena = Convert.ToDecimal(reader["SredniaCena"]),
                    LiczbaFaktur = Convert.ToInt32(reader["LiczbaFaktur"]),
                    OstatniaSprzedaz = Convert.ToDateTime(reader["OstatniaSprzedaz"])
                });
            }

            return result;
        }

        public async Task<List<NotatkaPozycja>> PobierzNotatkiAsync(int idSymfonia)
        {
            var result = new List<NotatkaPozycja>();

            using var conn = new SqlConnection(_libraNetConnectionString);
            await conn.OpenAsync();

            var sql = @"SELECT Id, IdSymfonia, Tresc, Autor, DataUtworzenia
                        FROM dbo.KartotekaOdbiorcyNotatki
                        WHERE IdSymfonia = @IdSymfonia
                        ORDER BY DataUtworzenia DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@IdSymfonia", idSymfonia);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new NotatkaPozycja
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    IdSymfonia = reader.GetInt32(reader.GetOrdinal("IdSymfonia")),
                    Tresc = reader["Tresc"]?.ToString() ?? "",
                    Autor = reader["Autor"]?.ToString() ?? "",
                    DataUtworzenia = reader.GetDateTime(reader.GetOrdinal("DataUtworzenia"))
                });
            }

            return result;
        }

        public async Task DodajNotatkeAsync(int idSymfonia, string tresc, string autor)
        {
            using var conn = new SqlConnection(_libraNetConnectionString);
            await conn.OpenAsync();

            var sql = @"INSERT INTO dbo.KartotekaOdbiorcyNotatki (IdSymfonia, Tresc, Autor)
                        VALUES (@IdSymfonia, @Tresc, @Autor)";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@IdSymfonia", idSymfonia);
            cmd.Parameters.AddWithValue("@Tresc", tresc ?? "");
            cmd.Parameters.AddWithValue("@Autor", autor ?? "");

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UsunNotatkeAsync(int id)
        {
            using var conn = new SqlConnection(_libraNetConnectionString);
            await conn.OpenAsync();

            using var cmd = new SqlCommand("DELETE FROM dbo.KartotekaOdbiorcyNotatki WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Pobiera zamówienia klienta z ostatnich N miesięcy (z LibraNet.ZamowieniaMieso)
        /// do analizy wzorców dostaw: DataUboju (produkcja), DataPrzyjazdu (awizacja), ilości, status.
        /// </summary>
        public async Task<List<ZamowienieDostawy>> PobierzZamowieniaDostawAsync(int klientId, int ostatnieMiesiecy = 12)
        {
            var result = new List<ZamowienieDostawy>();

            using var conn = new SqlConnection(_libraNetConnectionString);
            await conn.OpenAsync();

            var sql = @"
SELECT
    zm.Id,
    zm.DataUboju,
    zm.DataPrzyjazdu,
    zm.DataWydania,
    ISNULL(zm.Status, 'Nowe') AS Status,
    ISNULL(SUM(zmt.Ilosc), 0) AS IloscKg,
    ISNULL(zm.LiczbaPojemnikow, 0) AS LiczbaPojemnikow,
    ISNULL(zm.LiczbaPalet, 0) AS LiczbaPalet,
    ISNULL(zm.Uwagi, '') AS Uwagi,
    zm.TransportKursID
FROM dbo.ZamowieniaMieso zm
LEFT JOIN dbo.ZamowieniaMiesoTowar zmt ON zm.Id = zmt.ZamowienieId
WHERE zm.KlientId = @KlientId
  AND zm.DataUboju >= DATEADD(MONTH, -@Miesiace, GETDATE())
GROUP BY zm.Id, zm.DataUboju, zm.DataPrzyjazdu, zm.DataWydania,
         zm.Status, zm.LiczbaPojemnikow, zm.LiczbaPalet, zm.Uwagi, zm.TransportKursID
ORDER BY zm.DataUboju DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@KlientId", klientId);
            cmd.Parameters.AddWithValue("@Miesiace", ostatnieMiesiecy);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new ZamowienieDostawy
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    DataUboju = Convert.ToDateTime(reader["DataUboju"]),
                    DataPrzyjazdu = reader.IsDBNull(reader.GetOrdinal("DataPrzyjazdu")) ? null : (DateTime?)Convert.ToDateTime(reader["DataPrzyjazdu"]),
                    DataWydania = reader.IsDBNull(reader.GetOrdinal("DataWydania")) ? null : (DateTime?)Convert.ToDateTime(reader["DataWydania"]),
                    Status = reader["Status"]?.ToString() ?? "Nowe",
                    IloscKg = Convert.ToDecimal(reader["IloscKg"]),
                    LiczbaPojemnikow = Convert.ToInt32(reader["LiczbaPojemnikow"]),
                    LiczbaPalet = Convert.ToDecimal(reader["LiczbaPalet"]),
                    Uwagi = reader["Uwagi"]?.ToString() ?? "",
                    TransportKursId = reader.IsDBNull(reader.GetOrdinal("TransportKursID")) ? null : (int?)Convert.ToInt32(reader["TransportKursID"])
                });
            }

            return result;
        }

        public async Task<List<TowarKatalog>> PobierzTowaryKatalogAsync()
        {
            var result = new List<TowarKatalog>();

            using var conn = new SqlConnection(_handelConnectionString);
            await conn.OpenAsync();

            var sql = @"SELECT TW.ID, TW.kod, TW.nazwa,
                               CASE WHEN TW.katalog = 67153 THEN N'Mrożonki' ELSE N'Świeże' END AS Katalog
                        FROM [HM].[TW] TW
                        WHERE TW.katalog IN (67095, 67153)
                          AND TW.aktywny = 1
                        ORDER BY TW.nazwa";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new TowarKatalog
                {
                    Id = Convert.ToInt32(reader["ID"]),
                    Kod = reader["kod"]?.ToString() ?? "",
                    Nazwa = reader["nazwa"]?.ToString() ?? "",
                    Katalog = reader["Katalog"]?.ToString() ?? ""
                });
            }

            return result;
        }
    }
}
