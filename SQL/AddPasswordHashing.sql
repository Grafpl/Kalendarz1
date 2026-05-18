-- =====================================================================
-- AddPasswordHashing.sql
-- Bezpieczeństwo logowania: BCrypt + throttling + audit log
-- Wymagania:
--   * Tabela operators (LibraNet) — dodanie kolumn dla hasła i lockoutu
--   * Tabela LoginAttempts (nowa) — audit log każdej próby logowania
--   * Uprawnienia admina przeniesione z hardcoded "11111" w Menu.cs do bazy
--
-- INSTRUKCJA URUCHOMIENIA:
--   1. Otwórz SSMS, połącz się z 192.168.0.109 (LibraNet)
--   2. Uruchom ten skrypt raz
--   3. Po uruchomieniu w aplikacji ZPSP — przy pierwszym logowaniu każdy
--      użytkownik (oprócz ID '0' i '1') USTAWIA nowe hasło
--
-- Data: 2026-05-11
-- =====================================================================

USE LibraNet;
GO

-- 1. Dodaj kolumny do tabeli operators (idempotentnie — IF NOT EXISTS)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name='PasswordHash' AND Object_ID=Object_ID('dbo.operators'))
    ALTER TABLE dbo.operators ADD PasswordHash NVARCHAR(255) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name='PasswordSetAt' AND Object_ID=Object_ID('dbo.operators'))
    ALTER TABLE dbo.operators ADD PasswordSetAt DATETIME NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name='IsAdmin' AND Object_ID=Object_ID('dbo.operators'))
    ALTER TABLE dbo.operators ADD IsAdmin BIT NOT NULL CONSTRAINT DF_operators_IsAdmin DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name='FailedAttempts' AND Object_ID=Object_ID('dbo.operators'))
    ALTER TABLE dbo.operators ADD FailedAttempts INT NOT NULL CONSTRAINT DF_operators_FailedAttempts DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name='LockedUntil' AND Object_ID=Object_ID('dbo.operators'))
    ALTER TABLE dbo.operators ADD LockedUntil DATETIME NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name='LastSuccessfulLogin' AND Object_ID=Object_ID('dbo.operators'))
    ALTER TABLE dbo.operators ADD LastSuccessfulLogin DATETIME NULL;
GO

-- 2. Ustaw uprawnienia admina z bazy (zamiast hardcoded "11111" w Menu.cs:1166)
UPDATE dbo.operators SET IsAdmin = 1 WHERE ID = '11111';
GO

-- 3. Audit log każdej próby logowania (sukces/błąd) — wymóg BRC sekcja 3.3 + IFS 2.2
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LoginAttempts')
BEGIN
    CREATE TABLE dbo.LoginAttempts (
        ID            BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserId        VARCHAR(50)   NOT NULL,
        Success       BIT           NOT NULL,
        AttemptedAt   DATETIME      NOT NULL CONSTRAINT DF_LoginAttempts_AttemptedAt DEFAULT GETDATE(),
        MachineName   VARCHAR(100)  NULL,
        FailureReason VARCHAR(200)  NULL
    );

    CREATE INDEX IX_LoginAttempts_UserDate
        ON dbo.LoginAttempts(UserId, AttemptedAt DESC);

    CREATE INDEX IX_LoginAttempts_Date
        ON dbo.LoginAttempts(AttemptedAt DESC);
END
GO

-- 4. Sanity check — pokaz strukturę po zmianach
SELECT 'operators schema after migration' AS Info;
SELECT name AS Column_Name, system_type_name AS Type, is_nullable AS Nullable
FROM sys.dm_exec_describe_first_result_set('SELECT * FROM dbo.operators', NULL, 0);

SELECT 'Admins after migration' AS Info;
SELECT ID, Name, IsAdmin FROM dbo.operators WHERE IsAdmin = 1;
GO
