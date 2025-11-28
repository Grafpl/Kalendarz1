-- =====================================================
-- SKRYPT DO AUTOMATYCZNEGO GENEROWANIA HISTORII SALD
-- Uruchamiane jako SQL Server Job codziennie o północy
-- Serwer: 192.168.0.109, Baza: LibraNet
-- =====================================================

USE [LibraNet]
GO

-- =====================================================
-- 1. PROCEDURA GŁÓWNA - GENEROWANIE DZIENNYCH SNAPSHOTÓW
-- =====================================================

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_GenerujDziennaHistorieSald')
    DROP PROCEDURE sp_GenerujDziennaHistorieSald
GO

CREATE PROCEDURE [dbo].[sp_GenerujDziennaHistorieSald]
    @Data DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Jeśli data nie podana, użyj wczorajszej daty
    IF @Data IS NULL
        SET @Data = DATEADD(DAY, -1, CAST(GETDATE() AS DATE));

    -- Tymczasowa tabela z saldami
    CREATE TABLE #TempSalda (
        KontrahentId INT,
        KontrahentShortcut NVARCHAR(50),
        SaldoE2 INT,
        SaldoH1 INT,
        SaldoEURO INT,
        SaldoPCV INT,
        SaldoDREW INT
    );

    -- Pobierz salda z serwera Handel przez RemoteServer
    INSERT INTO #TempSalda
    SELECT 
        C.id AS KontrahentId,
        C.Shortcut AS KontrahentShortcut,
        CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS SaldoE2,
        CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS SaldoH1,
        CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta EURO' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS SaldoEURO,
        CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta plastikowa' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS SaldoPCV,
        CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta Drewniana' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS SaldoDREW
    FROM [RemoteServer].[Handel].[HM].[MG] MG
    INNER JOIN [RemoteServer].[Handel].[HM].[MZ] MZ ON MG.id = MZ.super
    INNER JOIN [RemoteServer].[Handel].[HM].[TW] TW ON MZ.idtw = TW.id
    INNER JOIN [RemoteServer].[Handel].[SSCommon].[STContractors] C ON MG.khid = C.id
    WHERE MG.anulowany = 0
      AND MG.magazyn = 65559
      AND MG.typ_dk IN ('MW1', 'MP')
      AND CAST(MG.data AS DATE) <= @Data
      AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')
    GROUP BY C.id, C.Shortcut
    HAVING SUM(CASE WHEN TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana') 
               THEN MZ.Ilosc ELSE 0 END) <> 0;

    -- Wstaw lub zaktualizuj rekordy w HistoriaSaldOpakowan
    MERGE INTO HistoriaSaldOpakowan AS target
    USING #TempSalda AS source
    ON target.KontrahentId = source.KontrahentId AND target.Data = @Data
    WHEN MATCHED THEN
        UPDATE SET 
            SaldoE2 = source.SaldoE2,
            SaldoH1 = source.SaldoH1,
            SaldoEURO = source.SaldoEURO,
            SaldoPCV = source.SaldoPCV,
            SaldoDREW = source.SaldoDREW
    WHEN NOT MATCHED THEN
        INSERT (KontrahentId, KontrahentShortcut, Data, SaldoE2, SaldoH1, SaldoEURO, SaldoPCV, SaldoDREW)
        VALUES (source.KontrahentId, source.KontrahentShortcut, @Data, 
                source.SaldoE2, source.SaldoH1, source.SaldoEURO, source.SaldoPCV, source.SaldoDREW);

    -- Wyczyść tabelę tymczasową
    DROP TABLE #TempSalda;

    -- Log wykonania
    INSERT INTO LogOperacji (Operacja, Data, Szczegoly)
    VALUES ('GenerowanieDziennejHistoriiSald', GETDATE(), 
            'Wygenerowano historię sald na dzień: ' + CONVERT(VARCHAR, @Data, 120));
END
GO

-- =====================================================
-- 2. PROCEDURA UZUPEŁNIAJĄCA - WYPEŁNIENIE BRAKUJĄCYCH DNI
-- =====================================================

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_UzupelnijHistorieSald')
    DROP PROCEDURE sp_UzupelnijHistorieSald
GO

CREATE PROCEDURE [dbo].[sp_UzupelnijHistorieSald]
    @DataOd DATE,
    @DataDo DATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @AktualnaData DATE = @DataOd;

    WHILE @AktualnaData <= @DataDo
    BEGIN
        -- Sprawdź czy są już dane na ten dzień
        IF NOT EXISTS (SELECT 1 FROM HistoriaSaldOpakowan WHERE Data = @AktualnaData)
        BEGIN
            EXEC sp_GenerujDziennaHistorieSald @Data = @AktualnaData;
        END

        SET @AktualnaData = DATEADD(DAY, 1, @AktualnaData);
    END
END
GO

-- =====================================================
-- 3. TABELA LOGÓW
-- =====================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE type = 'U' AND name = 'LogOperacji')
BEGIN
    CREATE TABLE [dbo].[LogOperacji] (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Operacja NVARCHAR(100) NOT NULL,
        Data DATETIME NOT NULL DEFAULT GETDATE(),
        Szczegoly NVARCHAR(MAX),
        Blad NVARCHAR(MAX)
    )
END
GO

-- =====================================================
-- 4. TWORZENIE SQL SERVER JOB
-- =====================================================

-- UWAGA: Wykonaj ten kod na serwerze SQL Server Agent

/*
USE msdb
GO

-- Usuń job jeśli istnieje
IF EXISTS (SELECT job_id FROM msdb.dbo.sysjobs WHERE name = N'GenerujHistorieSaldOpakowan')
BEGIN
    EXEC msdb.dbo.sp_delete_job @job_name = N'GenerujHistorieSaldOpakowan'
END
GO

-- Utwórz nowy job
EXEC msdb.dbo.sp_add_job
    @job_name = N'GenerujHistorieSaldOpakowan',
    @enabled = 1,
    @description = N'Generuje dzienną historię sald opakowań dla wykresów'
GO

-- Dodaj krok
EXEC msdb.dbo.sp_add_jobstep
    @job_name = N'GenerujHistorieSaldOpakowan',
    @step_name = N'Generuj historię',
    @subsystem = N'TSQL',
    @command = N'EXEC LibraNet.dbo.sp_GenerujDziennaHistorieSald',
    @database_name = N'LibraNet',
    @retry_attempts = 3,
    @retry_interval = 5
GO

-- Dodaj harmonogram (codziennie o 00:30)
EXEC msdb.dbo.sp_add_schedule
    @schedule_name = N'DziennyHarmonogram',
    @freq_type = 4, -- Codziennie
    @freq_interval = 1,
    @active_start_time = 003000 -- 00:30:00
GO

-- Przypisz harmonogram do joba
EXEC msdb.dbo.sp_attach_schedule
    @job_name = N'GenerujHistorieSaldOpakowan',
    @schedule_name = N'DziennyHarmonogram'
GO

-- Przypisz job do serwera
EXEC msdb.dbo.sp_add_jobserver
    @job_name = N'GenerujHistorieSaldOpakowan',
    @server_name = N'(LOCAL)'
GO
*/

-- =====================================================
-- 5. PROCEDURA RĘCZNEGO URUCHOMIENIA
-- =====================================================

-- Użycie:
-- EXEC sp_GenerujDziennaHistorieSald -- Dla wczorajszej daty
-- EXEC sp_GenerujDziennaHistorieSald @Data = '2025-01-01' -- Dla konkretnej daty
-- EXEC sp_UzupelnijHistorieSald @DataOd = '2025-01-01', @DataDo = '2025-01-31' -- Dla zakresu

-- =====================================================
-- 6. WIDOK DO SPRAWDZENIA STATUSU
-- =====================================================

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_StatusHistoriiSald')
    DROP VIEW vw_StatusHistoriiSald
GO

CREATE VIEW [dbo].[vw_StatusHistoriiSald]
AS
SELECT 
    MIN(Data) AS NajstarszyRekord,
    MAX(Data) AS NajnowszyRekord,
    COUNT(DISTINCT Data) AS LiczbaDni,
    COUNT(DISTINCT KontrahentId) AS LiczbaKontrahentow,
    COUNT(*) AS LiczbaRekordow
FROM HistoriaSaldOpakowan
GO

-- Sprawdź status:
-- SELECT * FROM vw_StatusHistoriiSald

PRINT 'Skrypt wykonany pomyślnie. Procedury utworzone.'
GO
