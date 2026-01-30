using System;
using System.Collections.Generic;
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
    C.City AS Miasto,
    C.Street AS Ulica,
    C.PostCode AS KodPocztowy,
    C.TaxId AS NIP,
    CASE C.PaymentType
        WHEN 0 THEN N'Gotówka'
        WHEN 1 THEN N'Przelew'
        WHEN 2 THEN N'Przedpłata'
        ELSE N'Inny'
    END AS FormaPlatnosci,
    C.PaymentDays AS TerminPlatnosci,
    ISNULL(C.CreditLimit, 0) AS LimitKupiecki,
    C.IsActive,
    ISNULL(WYM.CDim_Handlowiec_Val, N'Nieprzypisany') AS Handlowiec,
    ISNULL((
        SELECT SUM(FK.brutto - ISNULL(FK.rozliczono, 0))
        FROM [HM].[FakturaKontrahent] FK
        WHERE FK.khid = C.Id
          AND FK.typ = 1
          AND FK.anulowany = 0
          AND FK.brutto > ISNULL(FK.rozliczono, 0)
    ), 0) AS WykorzystanoLimit,
    ISNULL((
        SELECT SUM(FK.brutto - ISNULL(FK.rozliczono, 0))
        FROM [HM].[FakturaKontrahent] FK
        WHERE FK.khid = C.Id
          AND FK.typ = 1
          AND FK.anulowany = 0
          AND FK.brutto > ISNULL(FK.rozliczono, 0)
          AND FK.termin_platnosci < GETDATE()
    ), 0) AS KwotaPrzeterminowana
FROM [SSCommon].[STContractors] C
LEFT JOIN [SSCommon].[ContractorClassification] WYM
    ON C.Id = WYM.ElementId
WHERE
    C.IsActive = 1
    AND (@PokazWszystkich = 1 OR ISNULL(WYM.CDim_Handlowiec_Val, '') = @Handlowiec)
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
                    Miasto = reader["Miasto"]?.ToString() ?? "",
                    Ulica = reader["Ulica"]?.ToString() ?? "",
                    KodPocztowy = reader["KodPocztowy"]?.ToString() ?? "",
                    NIP = reader["NIP"]?.ToString() ?? "",
                    FormaPlatnosci = reader["FormaPlatnosci"]?.ToString() ?? "",
                    TerminPlatnosci = reader.IsDBNull(reader.GetOrdinal("TerminPlatnosci")) ? 0 : reader.GetInt32(reader.GetOrdinal("TerminPlatnosci")),
                    LimitKupiecki = reader.IsDBNull(reader.GetOrdinal("LimitKupiecki")) ? 0 : reader.GetDecimal(reader.GetOrdinal("LimitKupiecki")),
                    IsActive = reader.IsDBNull(reader.GetOrdinal("IsActive")) ? true : reader.GetBoolean(reader.GetOrdinal("IsActive")),
                    Handlowiec = reader["Handlowiec"]?.ToString() ?? "",
                    WykorzystanoLimit = reader.IsDBNull(reader.GetOrdinal("WykorzystanoLimit")) ? 0 : reader.GetDecimal(reader.GetOrdinal("WykorzystanoLimit")),
                    KwotaPrzeterminowana = reader.IsDBNull(reader.GetOrdinal("KwotaPrzeterminowana")) ? 0 : reader.GetDecimal(reader.GetOrdinal("KwotaPrzeterminowana"))
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

            var sql = @"SELECT FK.khid, FK.brutto, ISNULL(FK.rozliczono, 0) AS rozliczono,
                               FK.typ, FK.anulowany, FK.data_faktury, FK.termin_platnosci
                        FROM [HM].[FakturaKontrahent] FK
                        WHERE FK.khid = @IdSymfonia
                          AND FK.data_faktury >= DATEADD(MONTH, -@Miesiace, GETDATE())
                        ORDER BY FK.data_faktury DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@IdSymfonia", idSymfonia);
            cmd.Parameters.AddWithValue("@Miesiace", ostatnieMiesiecy);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new FakturaOdbiorcy
                {
                    KontrahentId = reader.GetInt32(reader.GetOrdinal("khid")),
                    Brutto = reader.IsDBNull(reader.GetOrdinal("brutto")) ? 0 : reader.GetDecimal(reader.GetOrdinal("brutto")),
                    Rozliczono = reader.IsDBNull(reader.GetOrdinal("rozliczono")) ? 0 : reader.GetDecimal(reader.GetOrdinal("rozliczono")),
                    Typ = reader.IsDBNull(reader.GetOrdinal("typ")) ? 0 : reader.GetInt32(reader.GetOrdinal("typ")),
                    Anulowany = reader.IsDBNull(reader.GetOrdinal("anulowany")) ? false : reader.GetBoolean(reader.GetOrdinal("anulowany")),
                    DataFaktury = reader.GetDateTime(reader.GetOrdinal("data_faktury")),
                    TerminPlatnosci = reader.GetDateTime(reader.GetOrdinal("termin_platnosci"))
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
    }
}
