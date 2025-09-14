// Plik: /Transport/InitializeDatabase.cs
// Klasa pomocnicza do inicjalizacji bazy danych

using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Transport
{
    public static class TransportDatabaseInitializer
    {
        /// <summary>
        /// Sprawdza i tworzy bazę danych TransportPL jeśli nie istnieje
        /// </summary>
        public static async Task<bool> EnsureDatabaseExistsAsync()
        {
            try
            {
                // Connection string do master (bez określonej bazy)
                var masterConnString = "Server=192.168.0.109;Database=master;User Id=pronova;Password=pronova;TrustServerCertificate=True";

                using var connection = new SqlConnection(masterConnString);
                await connection.OpenAsync();

                // Sprawdź czy baza istnieje
                var checkDbSql = "SELECT COUNT(*) FROM sys.databases WHERE name = 'TransportPL'";
                using var checkCmd = new SqlCommand(checkDbSql, connection);
                var exists = (int)await checkCmd.ExecuteScalarAsync() > 0;

                if (!exists)
                {
                    // Utwórz bazę
                    var createDbSql = "CREATE DATABASE TransportPL";
                    using var createCmd = new SqlCommand(createDbSql, connection);
                    await createCmd.ExecuteNonQueryAsync();

                    // Poczekaj chwilę na utworzenie bazy
                    await Task.Delay(2000);

                    // Utwórz strukturę
                    await CreateDatabaseStructureAsync();

                    return true;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Błąd podczas sprawdzania/tworzenia bazy danych:\n{ex.Message}\n\n" +
                    "Upewnij się, że:\n" +
                    "1. Serwer SQL jest dostępny (192.168.0.109)\n" +
                    "2. Użytkownik 'pronova' ma uprawnienia do tworzenia baz danych\n" +
                    "3. Hasło jest poprawne",
                    "Błąd inicjalizacji bazy danych",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
                return false;
            }
        }

        private static async Task CreateDatabaseStructureAsync()
        {
            var connString = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

            using var connection = new SqlConnection(connString);
            await connection.OpenAsync();

            // Tabela Kierowca
            var sqlKierowca = @"
                IF OBJECT_ID('dbo.Kierowca','U') IS NULL
                CREATE TABLE dbo.Kierowca(
                    KierowcaID INT IDENTITY(1,1) PRIMARY KEY,
                    Imie NVARCHAR(50) NOT NULL,
                    Nazwisko NVARCHAR(80) NOT NULL,
                    Telefon NVARCHAR(30) NULL,
                    Aktywny BIT NOT NULL DEFAULT 1,
                    UtworzonoUTC DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
                    ZmienionoUTC DATETIME2(0) NULL
                )";
            using var cmdKierowca = new SqlCommand(sqlKierowca, connection);
            await cmdKierowca.ExecuteNonQueryAsync();

            // Tabela Pojazd
            var sqlPojazd = @"
                IF OBJECT_ID('dbo.Pojazd','U') IS NULL
                CREATE TABLE dbo.Pojazd(
                    PojazdID INT IDENTITY(1,1) PRIMARY KEY,
                    Rejestracja NVARCHAR(20) NOT NULL UNIQUE,
                    Marka NVARCHAR(50) NULL,
                    Model NVARCHAR(50) NULL,
                    PaletyH1 INT NOT NULL DEFAULT 0,
                    Aktywny BIT NOT NULL DEFAULT 1,
                    UtworzonoUTC DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
                    ZmienionoUTC DATETIME2(0) NULL
                )";
            using var cmdPojazd = new SqlCommand(sqlPojazd, connection);
            await cmdPojazd.ExecuteNonQueryAsync();

            // Tabela Kurs
            var sqlKurs = @"
                IF OBJECT_ID('dbo.Kurs','U') IS NULL
                CREATE TABLE dbo.Kurs(
                    KursID BIGINT IDENTITY(1,1) PRIMARY KEY,
                    DataKursu DATE NOT NULL,
                    KierowcaID INT NOT NULL FOREIGN KEY REFERENCES dbo.Kierowca(KierowcaID),
                    PojazdID INT NOT NULL FOREIGN KEY REFERENCES dbo.Pojazd(PojazdID),
                    Trasa NVARCHAR(120) NULL,
                    GodzWyjazdu TIME NULL,
                    GodzPowrotu TIME NULL,
                    Status NVARCHAR(20) NOT NULL DEFAULT 'Planowany'
                        CHECK (Status IN ('Planowany','WTrakcie','Zakonczony','Anulowany')),
                    PlanE2NaPalete TINYINT NOT NULL DEFAULT 36 CHECK (PlanE2NaPalete IN (36,40)),
                    UtworzonoUTC DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
                    Utworzyl NVARCHAR(64) NULL,
                    ZmienionoUTC DATETIME2(0) NULL,
                    Zmienil NVARCHAR(64) NULL
                )";
            using var cmdKurs = new SqlCommand(sqlKurs, connection);
            await cmdKurs.ExecuteNonQueryAsync();

            // Tabela Ladunek
            var sqlLadunek = @"
                IF OBJECT_ID('dbo.Ladunek','U') IS NULL
                CREATE TABLE dbo.Ladunek(
                    LadunekID BIGINT IDENTITY(1,1) PRIMARY KEY,
                    KursID BIGINT NOT NULL FOREIGN KEY REFERENCES dbo.Kurs(KursID) ON DELETE CASCADE,
                    Kolejnosc INT NOT NULL,
                    KodKlienta NVARCHAR(50) NULL,
                    PojemnikiE2 INT NOT NULL DEFAULT 0,
                    PaletyH1 INT NULL,
                    PlanE2NaPaleteOverride TINYINT NULL CHECK (PlanE2NaPaleteOverride IN (36,40)),
                    Uwagi NVARCHAR(255) NULL,
                    UtworzonoUTC DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME()
                )";
            using var cmdLadunek = new SqlCommand(sqlLadunek, connection);
            await cmdLadunek.ExecuteNonQueryAsync();

            // Indeks
            var sqlIndex = @"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_Ladunek_Kurs_Kolejnosc')
                    CREATE UNIQUE INDEX UX_Ladunek_Kurs_Kolejnosc ON dbo.Ladunek(KursID, Kolejnosc)";
            using var cmdIndex = new SqlCommand(sqlIndex, connection);
            await cmdIndex.ExecuteNonQueryAsync();

            // Widok
            var sqlDropView = "IF OBJECT_ID('dbo.vKursWypelnienie','V') IS NOT NULL DROP VIEW dbo.vKursWypelnienie";
            using var cmdDropView = new SqlCommand(sqlDropView, connection);
            await cmdDropView.ExecuteNonQueryAsync();

            var sqlView = @"
                CREATE VIEW dbo.vKursWypelnienie AS
                SELECT
                    k.KursID,
                    p.PaletyH1 AS PaletyPojazdu,
                    SUM(ISNULL(l.PojemnikiE2,0)) AS SumaE2,
                    CEILING(SUM(ISNULL(l.PojemnikiE2,0))/36.0) AS PaletyNominal,
                    CEILING(SUM(ISNULL(l.PojemnikiE2,0))/40.0) AS PaletyMax,
                    CAST(
                        CASE 
                            WHEN p.PaletyH1 > 0 
                            THEN (CEILING(SUM(ISNULL(l.PojemnikiE2,0))/36.0) * 100.0) / p.PaletyH1 
                            ELSE 0 
                        END AS DECIMAL(6,2)
                    ) AS ProcNominal,
                    CAST(
                        CASE 
                            WHEN p.PaletyH1 > 0 
                            THEN (CEILING(SUM(ISNULL(l.PojemnikiE2,0))/40.0) * 100.0) / p.PaletyH1 
                            ELSE 0 
                        END AS DECIMAL(6,2)
                    ) AS ProcMax
                FROM dbo.Kurs k
                JOIN dbo.Pojazd p ON k.PojazdID = p.PojazdID
                LEFT JOIN dbo.Ladunek l ON k.KursID = l.KursID
                GROUP BY k.KursID, p.PaletyH1";
            using var cmdView = new SqlCommand(sqlView, connection);
            await cmdView.ExecuteNonQueryAsync();

            // Dodaj przykładowe dane
            await AddSampleDataAsync(connection);
        }

        private static async Task AddSampleDataAsync(SqlConnection connection)
        {
            // Sprawdź czy są już dane
            var checkSql = "SELECT COUNT(*) FROM dbo.Kierowca";
            using var checkCmd = new SqlCommand(checkSql, connection);
            var count = (int)await checkCmd.ExecuteScalarAsync();

            if (count == 0)
            {
                // Dodaj kierowców
                var sqlKierowcy = @"
                    INSERT dbo.Kierowca(Imie, Nazwisko, Telefon) VALUES 
                    (N'Jan', N'Kowalski', '500-100-200'),
                    (N'Adam', N'Nowak', '501-200-300'),
                    (N'Piotr', N'Wiśniewski', '502-300-400')";
                using var cmdKierowcy = new SqlCommand(sqlKierowcy, connection);
                await cmdKierowcy.ExecuteNonQueryAsync();

                // Dodaj pojazdy
                var sqlPojazdy = @"
                    INSERT dbo.Pojazd(Rejestracja, Marka, Model, PaletyH1) VALUES 
                    ('WX 12345', 'Volvo', 'FH', 33),
                    ('WZ 67890', 'Mercedes', 'Actros', 33),
                    ('WE 11223', 'Scania', 'R450', 30)";
                using var cmdPojazdy = new SqlCommand(sqlPojazdy, connection);
                await cmdPojazdy.ExecuteNonQueryAsync();
            }
        }
    }
}