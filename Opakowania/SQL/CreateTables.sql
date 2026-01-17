-- ============================================================
-- SKRYPT TWORZENIA TABEL DLA SYSTEMU ZARZĄDZANIA OPAKOWANIAMI
-- Serwer: 192.168.0.109 (LibraNet)
-- Linked Server: RemoteServer -> 192.168.0.112 (Handel)
-- ============================================================

USE [LibraNet]
GO

-- ============================================================
-- TABELA: PotwierdzeniaSaldaOpakowan
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PotwierdzeniaSaldaOpakowan')
BEGIN
    CREATE TABLE [dbo].[PotwierdzeniaSaldaOpakowan] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [KontrahentId] INT NOT NULL,
        [KontrahentNazwa] NVARCHAR(200) NOT NULL,
        [KontrahentShortcut] NVARCHAR(50) NULL,
        [TypOpakowania] NVARCHAR(100) NOT NULL,
        [KodOpakowania] NVARCHAR(20) NOT NULL,
        [DataPotwierdzenia] DATE NOT NULL,
        [IloscPotwierdzona] INT NOT NULL,
        [SaldoSystemowe] INT NOT NULL,
        [Roznica] AS ([IloscPotwierdzona] - [SaldoSystemowe]) PERSISTED,
        [StatusPotwierdzenia] NVARCHAR(20) NOT NULL DEFAULT 'Oczekujące',
        [NumerDokumentu] NVARCHAR(50) NULL,
        [SciezkaZalacznika] NVARCHAR(500) NULL,
        [Uwagi] NVARCHAR(1000) NULL,
        [UzytkownikId] NVARCHAR(20) NOT NULL,
        [UzytkownikNazwa] NVARCHAR(100) NULL,
        [DataWprowadzenia] DATETIME NOT NULL DEFAULT GETDATE(),
        [DataModyfikacji] DATETIME NULL,
        [ZmodyfikowalId] NVARCHAR(20) NULL,
        
        CONSTRAINT [CK_StatusPotwierdzenia] CHECK (
            [StatusPotwierdzenia] IN ('Potwierdzone', 'Rozbieżność', 'Oczekujące', 'Anulowane')
        )
    )

    CREATE NONCLUSTERED INDEX [IX_PotwierdzeniaSalda_Kontrahent] 
        ON [dbo].[PotwierdzeniaSaldaOpakowan] ([KontrahentId], [DataPotwierdzenia] DESC)

    CREATE NONCLUSTERED INDEX [IX_PotwierdzeniaSalda_TypOpakowania] 
        ON [dbo].[PotwierdzeniaSaldaOpakowan] ([TypOpakowania], [DataPotwierdzenia] DESC)

    CREATE NONCLUSTERED INDEX [IX_PotwierdzeniaSalda_Data] 
        ON [dbo].[PotwierdzeniaSaldaOpakowan] ([DataPotwierdzenia] DESC)

    CREATE NONCLUSTERED INDEX [IX_PotwierdzeniaSalda_Status] 
        ON [dbo].[PotwierdzeniaSaldaOpakowan] ([StatusPotwierdzenia])

    PRINT 'Tabela PotwierdzeniaSaldaOpakowan utworzona pomyślnie.'
END
ELSE
BEGIN
    PRINT 'Tabela PotwierdzeniaSaldaOpakowan już istnieje.'
END
GO

-- ============================================================
-- TABELA: HistoriaPrzypomnienSald
-- Historia wysłanych przypomnień o potwierdzenie sald
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HistoriaPrzypomnienSald')
BEGIN
    CREATE TABLE [dbo].[HistoriaPrzypomnienSald] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [KontrahentId] INT NOT NULL,
        [KontrahentNazwa] NVARCHAR(200) NULL,
        [Email] NVARCHAR(200) NULL,
        [DataWyslania] DATETIME NOT NULL DEFAULT GETDATE(),
        [UzytkownikId] NVARCHAR(20) NULL,
        [UzytkownikNazwa] NVARCHAR(100) NULL,
        [Typ] NVARCHAR(50) NOT NULL DEFAULT 'Przypomnienie', -- Przypomnienie, Potwierdzenie, Zestawienie
        [StatusWyslania] NVARCHAR(20) NULL DEFAULT 'Wyslane', -- Wyslane, Blad
        [Uwagi] NVARCHAR(500) NULL
    )

    CREATE NONCLUSTERED INDEX [IX_HistoriaPrzypomnien_Kontrahent]
        ON [dbo].[HistoriaPrzypomnienSald] ([KontrahentId], [DataWyslania] DESC)

    CREATE NONCLUSTERED INDEX [IX_HistoriaPrzypomnien_Data]
        ON [dbo].[HistoriaPrzypomnienSald] ([DataWyslania] DESC)

    PRINT 'Tabela HistoriaPrzypomnienSald utworzona pomyślnie.'
END
ELSE
BEGIN
    PRINT 'Tabela HistoriaPrzypomnienSald już istnieje.'
END
GO

-- ============================================================
-- TABELA: HistoriaSaldOpakowan
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HistoriaSaldOpakowan')
BEGIN
    CREATE TABLE [dbo].[HistoriaSaldOpakowan] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [KontrahentId] INT NOT NULL,
        [KontrahentShortcut] NVARCHAR(50) NOT NULL,
        [Data] DATE NOT NULL,
        [SaldoE2] INT NOT NULL DEFAULT 0,
        [SaldoH1] INT NOT NULL DEFAULT 0,
        [SaldoEURO] INT NOT NULL DEFAULT 0,
        [SaldoPCV] INT NOT NULL DEFAULT 0,
        [SaldoDREW] INT NOT NULL DEFAULT 0,
        [DataUtworzenia] DATETIME NOT NULL DEFAULT GETDATE(),
        
        CONSTRAINT [UQ_HistoriaSald_KontrahentData] UNIQUE ([KontrahentId], [Data])
    )

    CREATE NONCLUSTERED INDEX [IX_HistoriaSald_Kontrahent] 
        ON [dbo].[HistoriaSaldOpakowan] ([KontrahentId], [Data] DESC)

    PRINT 'Tabela HistoriaSaldOpakowan utworzona pomyślnie.'
END
GO

-- ============================================================
-- TABELA: MapowanieHandlowcow
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MapowanieHandlowcow')
BEGIN
    CREATE TABLE [dbo].[MapowanieHandlowcow] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [UserId] NVARCHAR(20) NOT NULL,
        [HandlowiecNazwa] NVARCHAR(100) NOT NULL,
        [CzyAktywny] BIT NOT NULL DEFAULT 1,
        [DataUtworzenia] DATETIME NOT NULL DEFAULT GETDATE(),
        
        CONSTRAINT [UQ_MapowanieHandlowcow_UserId] UNIQUE ([UserId])
    )

    INSERT INTO [dbo].[MapowanieHandlowcow] ([UserId], [HandlowiecNazwa])
    VALUES 
        ('11111', 'Administrator'),
        ('12345', 'Jan Kowalski'),
        ('12346', 'Anna Nowak')
    
    PRINT 'Tabela MapowanieHandlowcow utworzona pomyślnie.'
END
GO

-- ============================================================
-- WIDOK: vw_SaldaOpakowaniKontrahentow
-- UŻYWA LINKED SERVER: RemoteServer -> Handel (192.168.0.112)
-- ============================================================
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_SaldaOpakowaniKontrahentow')
    DROP VIEW [dbo].[vw_SaldaOpakowaniKontrahentow]
GO

CREATE VIEW [dbo].[vw_SaldaOpakowaniKontrahentow]
AS
SELECT 
    C.id AS KontrahentId,
    C.Shortcut AS KontrahentShortcut,
    C.Name AS KontrahentNazwa,
    ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec,
    ISNULL(SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END), 0) AS SaldoE2,
    ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END), 0) AS SaldoH1,
    ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta EURO' THEN MZ.Ilosc ELSE 0 END), 0) AS SaldoEURO,
    ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta plastikowa' THEN MZ.Ilosc ELSE 0 END), 0) AS SaldoPCV,
    ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta Drewniana' THEN MZ.Ilosc ELSE 0 END), 0) AS SaldoDREW,
    (
        SELECT TOP 1 P.DataPotwierdzenia 
        FROM [LibraNet].[dbo].[PotwierdzeniaSaldaOpakowan] P 
        WHERE P.KontrahentId = C.id AND P.StatusPotwierdzenia = 'Potwierdzone'
        ORDER BY P.DataPotwierdzenia DESC
    ) AS OstatniePotwierdzenie
FROM [RemoteServer].[Handel].[SSCommon].[STContractors] C
LEFT JOIN [RemoteServer].[Handel].[HM].[MG] MG ON MG.khid = C.id AND MG.anulowany = 0
LEFT JOIN [RemoteServer].[Handel].[HM].[MZ] MZ ON MZ.super = MG.id
LEFT JOIN [RemoteServer].[Handel].[HM].[TW] TW ON MZ.idtw = TW.id 
    AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')
LEFT JOIN [RemoteServer].[Handel].[SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId
GROUP BY C.id, C.Shortcut, C.Name, WYM.CDim_Handlowiec_Val
HAVING ISNULL(SUM(MZ.Ilosc), 0) != 0
GO

PRINT 'Widok vw_SaldaOpakowaniKontrahentow utworzony pomyślnie.'
GO

-- ============================================================
-- PROCEDURA: GenerujHistorieSald
-- UŻYWA LINKED SERVER
-- ============================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'GenerujHistorieSald')
    DROP PROCEDURE [dbo].[GenerujHistorieSald]
GO

CREATE PROCEDURE [dbo].[GenerujHistorieSald]
    @DataOd DATE = NULL,
    @DataDo DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    IF @DataOd IS NULL SET @DataOd = DATEADD(DAY, -30, GETDATE())
    IF @DataDo IS NULL SET @DataDo = GETDATE()
    
    DECLARE @CurrentDate DATE = @DataOd
    
    WHILE @CurrentDate <= @DataDo
    BEGIN
        MERGE INTO [LibraNet].[dbo].[HistoriaSaldOpakowan] AS target
        USING (
            SELECT 
                C.id AS KontrahentId,
                C.Shortcut AS KontrahentShortcut,
                @CurrentDate AS Data,
                ISNULL(SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END), 0) AS SaldoE2,
                ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END), 0) AS SaldoH1,
                ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta EURO' THEN MZ.Ilosc ELSE 0 END), 0) AS SaldoEURO,
                ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta plastikowa' THEN MZ.Ilosc ELSE 0 END), 0) AS SaldoPCV,
                ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta Drewniana' THEN MZ.Ilosc ELSE 0 END), 0) AS SaldoDREW
            FROM [RemoteServer].[Handel].[SSCommon].[STContractors] C
            LEFT JOIN [RemoteServer].[Handel].[HM].[MG] MG ON MG.khid = C.id 
                AND MG.anulowany = 0 
                AND MG.data <= @CurrentDate
            LEFT JOIN [RemoteServer].[Handel].[HM].[MZ] MZ ON MZ.super = MG.id
            LEFT JOIN [RemoteServer].[Handel].[HM].[TW] TW ON MZ.idtw = TW.id 
                AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')
            WHERE C.Shortcut IS NOT NULL
            GROUP BY C.id, C.Shortcut
            HAVING ISNULL(SUM(MZ.Ilosc), 0) != 0
        ) AS source
        ON target.KontrahentId = source.KontrahentId AND target.Data = source.Data
        WHEN MATCHED THEN
            UPDATE SET 
                SaldoE2 = source.SaldoE2,
                SaldoH1 = source.SaldoH1,
                SaldoEURO = source.SaldoEURO,
                SaldoPCV = source.SaldoPCV,
                SaldoDREW = source.SaldoDREW
        WHEN NOT MATCHED THEN
            INSERT (KontrahentId, KontrahentShortcut, Data, SaldoE2, SaldoH1, SaldoEURO, SaldoPCV, SaldoDREW)
            VALUES (source.KontrahentId, source.KontrahentShortcut, source.Data, 
                    source.SaldoE2, source.SaldoH1, source.SaldoEURO, source.SaldoPCV, source.SaldoDREW);
        
        SET @CurrentDate = DATEADD(DAY, 1, @CurrentDate)
    END
    
    PRINT 'Historia sald wygenerowana pomyślnie.'
END
GO

-- ============================================================
-- PROCEDURA: PobierzSaldoKontrahenta
-- UŻYWA LINKED SERVER
-- ============================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'PobierzSaldoKontrahenta')
    DROP PROCEDURE [dbo].[PobierzSaldoKontrahenta]
GO

CREATE PROCEDURE [dbo].[PobierzSaldoKontrahenta]
    @KontrahentId INT,
    @DataOd DATE,
    @DataDo DATE
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Saldo początkowe
    SELECT 
        'Saldo początkowe' AS Dokumenty,
        NULL AS NrDok,
        @DataOd AS Data,
        NULL AS DzienTyg,
        SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END) AS E2,
        SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END) AS H1,
        SUM(CASE WHEN TW.nazwa = 'Paleta EURO' THEN MZ.Ilosc ELSE 0 END) AS EURO,
        SUM(CASE WHEN TW.nazwa = 'Paleta plastikowa' THEN MZ.Ilosc ELSE 0 END) AS PCV,
        SUM(CASE WHEN TW.nazwa = 'Paleta Drewniana' THEN MZ.Ilosc ELSE 0 END) AS DREW
    FROM [RemoteServer].[Handel].[HM].[MG] MG
    JOIN [RemoteServer].[Handel].[HM].[MZ] MZ ON MG.id = MZ.super
    JOIN [RemoteServer].[Handel].[HM].[TW] TW ON MZ.idtw = TW.id
    WHERE MG.khid = @KontrahentId
      AND MG.magazyn = 65559
      AND MG.typ_dk IN ('MW1', 'MP')
      AND MG.anulowany = 0
      AND MG.data <= @DataOd

    UNION ALL

    -- Dokumenty w okresie
    SELECT 
        MG.opis AS Dokumenty,
        MG.kod AS NrDok,
        MG.data AS Data,
        DATENAME(WEEKDAY, MG.data) AS DzienTyg,
        SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END) AS E2,
        SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END) AS H1,
        SUM(CASE WHEN TW.nazwa = 'Paleta EURO' THEN MZ.Ilosc ELSE 0 END) AS EURO,
        SUM(CASE WHEN TW.nazwa = 'Paleta plastikowa' THEN MZ.Ilosc ELSE 0 END) AS PCV,
        SUM(CASE WHEN TW.nazwa = 'Paleta Drewniana' THEN MZ.Ilosc ELSE 0 END) AS DREW
    FROM [RemoteServer].[Handel].[HM].[MG] MG
    JOIN [RemoteServer].[Handel].[HM].[MZ] MZ ON MG.id = MZ.super
    JOIN [RemoteServer].[Handel].[HM].[TW] TW ON MZ.idtw = TW.id
    WHERE MG.khid = @KontrahentId
      AND MG.magazyn = 65559
      AND MG.typ_dk IN ('MW1', 'MP')
      AND MG.anulowany = 0
      AND MG.data > @DataOd
      AND MG.data <= @DataDo
    GROUP BY MG.id, MG.kod, MG.data, MG.opis
    ORDER BY Data DESC
END
GO

-- ============================================================
-- FUNKCJA: fn_CzyPotwierdzoneSaldo
-- ============================================================
IF EXISTS (SELECT * FROM sys.objects WHERE name = 'fn_CzyPotwierdzoneSaldo' AND type = 'FN')
    DROP FUNCTION [dbo].[fn_CzyPotwierdzoneSaldo]
GO

CREATE FUNCTION [dbo].[fn_CzyPotwierdzoneSaldo]
(
    @KontrahentId INT,
    @TypOpakowania NVARCHAR(100),
    @DniWstecz INT = 30
)
RETURNS BIT
AS
BEGIN
    DECLARE @Result BIT = 0
    
    IF EXISTS (
        SELECT 1 
        FROM [dbo].[PotwierdzeniaSaldaOpakowan]
        WHERE KontrahentId = @KontrahentId
          AND TypOpakowania = @TypOpakowania
          AND StatusPotwierdzenia = 'Potwierdzone'
          AND DataPotwierdzenia >= DATEADD(DAY, -@DniWstecz, GETDATE())
    )
    BEGIN
        SET @Result = 1
    END
    
    RETURN @Result
END
GO

PRINT ''
PRINT '============================================================'
PRINT 'WSZYSTKIE OBIEKTY BAZY DANYCH ZOSTAŁY UTWORZONE POMYŚLNIE!'
PRINT 'Linked Server: RemoteServer -> Handel (192.168.0.112)'
PRINT '============================================================'
GO
