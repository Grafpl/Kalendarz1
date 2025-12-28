-- =====================================================
-- OPTYMALIZACJA BAZY DANYCH - INDEKSY I STORED PROCEDURES
-- Uruchom na bazie LibraNet
-- =====================================================

PRINT '=== TWORZENIE INDEKSÓW ==='
GO

-- 1. Indeks dla wyszukiwania zamówień po dacie uboju
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ZamowieniaMieso_DataUboju')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ZamowieniaMieso_DataUboju]
    ON [dbo].[ZamowieniaMieso] ([DataUboju])
    INCLUDE ([KlientId], [Status], [IloscZamowiona])
    PRINT 'Utworzono indeks IX_ZamowieniaMieso_DataUboju'
END
GO

-- 2. Indeks dla wyszukiwania po kliencie i statusie
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ZamowieniaMieso_KlientId_Status')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ZamowieniaMieso_KlientId_Status]
    ON [dbo].[ZamowieniaMieso] ([KlientId], [Status])
    INCLUDE ([DataUboju], [DataZamowienia], [IloscZamowiona])
    PRINT 'Utworzono indeks IX_ZamowieniaMieso_KlientId_Status'
END
GO

-- 3. Indeks dla statystyk anulowanych zamówień
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ZamowieniaMieso_Status_DataAnulowania')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ZamowieniaMieso_Status_DataAnulowania]
    ON [dbo].[ZamowieniaMieso] ([Status], [DataAnulowania])
    INCLUDE ([KlientId], [PrzyczynaAnulowania], [AnulowanePrzez])
    WHERE [Status] = 'Anulowane'
    PRINT 'Utworzono indeks IX_ZamowieniaMieso_Status_DataAnulowania'
END
GO

-- 4. Indeks dla towarów zamówień
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ZamowieniaMiesoTowar_ZamowienieId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ZamowieniaMiesoTowar_ZamowienieId]
    ON [dbo].[ZamowieniaMiesoTowar] ([ZamowienieId])
    INCLUDE ([TowarId], [Ilosc], [Cena])
    PRINT 'Utworzono indeks IX_ZamowieniaMiesoTowar_ZamowienieId'
END
GO

-- 5. Indeks dla historii zmian
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ZamowieniaMiesoHistoria_ZamowienieId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ZamowieniaMiesoHistoria_ZamowienieId]
    ON [dbo].[ZamowieniaMiesoHistoria] ([ZamowienieId])
    INCLUDE ([DataZmiany], [TypZmiany], [Uzytkownik])
    PRINT 'Utworzono indeks IX_ZamowieniaMiesoHistoria_ZamowienieId'
END
GO

PRINT '=== TWORZENIE STORED PROCEDURES ==='
GO

-- =====================================================
-- SP: Pobieranie zamówień na dzień z paginacją
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetZamowieniaNaDzien')
    DROP PROCEDURE sp_GetZamowieniaNaDzien
GO

CREATE PROCEDURE [dbo].[sp_GetZamowieniaNaDzien]
    @DataUboju DATE,
    @Status NVARCHAR(50) = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 100
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        zm.Id,
        zm.KlientId,
        zm.DataUboju,
        zm.DataZamowienia,
        zm.Status,
        zm.IloscZamowiona,
        zm.Uwagi,
        zm.UtworzonePrzez,
        zm.DataUtworzenia,
        zm.AnulowanePrzez,
        zm.DataAnulowania,
        zm.PrzyczynaAnulowania,
        ISNULL(zmt.IloscSuma, 0) as IloscTowarow,
        ISNULL(zmt.WartoscSuma, 0) as WartoscZamowienia
    FROM [dbo].[ZamowieniaMieso] zm
    LEFT JOIN (
        SELECT ZamowienieId,
               SUM(Ilosc) as IloscSuma,
               SUM(Ilosc * ISNULL(Cena, 0)) as WartoscSuma
        FROM [dbo].[ZamowieniaMiesoTowar]
        GROUP BY ZamowienieId
    ) zmt ON zm.Id = zmt.ZamowienieId
    WHERE zm.DataUboju = @DataUboju
      AND (@Status IS NULL OR zm.Status = @Status)
    ORDER BY zm.Id
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    -- Zwróć też łączną liczbę rekordów
    SELECT COUNT(*) as TotalCount
    FROM [dbo].[ZamowieniaMieso]
    WHERE DataUboju = @DataUboju
      AND (@Status IS NULL OR Status = @Status);
END
GO

PRINT 'Utworzono sp_GetZamowieniaNaDzien'
GO

-- =====================================================
-- SP: Statystyki anulowanych zamówień
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetStatystykiAnulowanych')
    DROP PROCEDURE sp_GetStatystykiAnulowanych
GO

CREATE PROCEDURE [dbo].[sp_GetStatystykiAnulowanych]
    @DataOd DATE,
    @DataDo DATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Statystyki per odbiorca
    SELECT
        zm.KlientId,
        COUNT(*) as LiczbaAnulowanych,
        SUM(ISNULL(zmt.IloscSuma, 0)) as SumaKg,
        MAX(COALESCE(zm.DataAnulowania, zm.DataUboju, zm.DataZamowienia)) as OstatniaData
    FROM [dbo].[ZamowieniaMieso] zm
    LEFT JOIN (
        SELECT ZamowienieId, SUM(Ilosc) as IloscSuma
        FROM [dbo].[ZamowieniaMiesoTowar]
        GROUP BY ZamowienieId
    ) zmt ON zm.Id = zmt.ZamowienieId
    WHERE zm.Status = 'Anulowane'
      AND COALESCE(zm.DataAnulowania, zm.DataUboju, zm.DataZamowienia) >= @DataOd
      AND COALESCE(zm.DataAnulowania, zm.DataUboju, zm.DataZamowienia) <= @DataDo
    GROUP BY zm.KlientId
    ORDER BY LiczbaAnulowanych DESC;

    -- Statystyki per przyczyna
    SELECT
        ISNULL(PrzyczynaAnulowania, 'Brak przyczyny') as Przyczyna,
        COUNT(*) as Liczba
    FROM [dbo].[ZamowieniaMieso]
    WHERE Status = 'Anulowane'
      AND COALESCE(DataAnulowania, DataUboju, DataZamowienia) >= @DataOd
      AND COALESCE(DataAnulowania, DataUboju, DataZamowienia) <= @DataDo
    GROUP BY ISNULL(PrzyczynaAnulowania, 'Brak przyczyny')
    ORDER BY Liczba DESC;
END
GO

PRINT 'Utworzono sp_GetStatystykiAnulowanych'
GO

-- =====================================================
-- SP: Historia zmian z diff
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetHistoriaZmianZDiff')
    DROP PROCEDURE sp_GetHistoriaZmianZDiff
GO

CREATE PROCEDURE [dbo].[sp_GetHistoriaZmianZDiff]
    @DataOd DATE,
    @DataDo DATE,
    @PageNumber INT = 1,
    @PageSize INT = 100
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        h.Id,
        h.ZamowienieId,
        h.DataZmiany,
        h.TypZmiany,
        h.Uzytkownik,
        h.OpisZmiany,
        h.StaraWartosc,
        h.NowaWartosc,
        h.Pole,
        zm.KlientId
    FROM [dbo].[ZamowieniaMiesoHistoria] h
    INNER JOIN [dbo].[ZamowieniaMieso] zm ON h.ZamowienieId = zm.Id
    WHERE CAST(h.DataZmiany as DATE) >= @DataOd
      AND CAST(h.DataZmiany as DATE) <= @DataDo
    ORDER BY h.DataZmiany DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    SELECT COUNT(*) as TotalCount
    FROM [dbo].[ZamowieniaMiesoHistoria] h
    WHERE CAST(h.DataZmiany as DATE) >= @DataOd
      AND CAST(h.DataZmiany as DATE) <= @DataDo;
END
GO

PRINT 'Utworzono sp_GetHistoriaZmianZDiff'
GO

-- =====================================================
-- SP: Batch update statusu zamówień
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_BatchUpdateZamowieniaStatus')
    DROP PROCEDURE sp_BatchUpdateZamowieniaStatus
GO

CREATE PROCEDURE [dbo].[sp_BatchUpdateZamowieniaStatus]
    @IdsJson NVARCHAR(MAX),  -- JSON array of IDs: [1,2,3,4,5]
    @NowyStatus NVARCHAR(50),
    @Uzytkownik NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @UpdatedCount INT = 0;

    BEGIN TRANSACTION;
    BEGIN TRY
        -- Aktualizuj statusy
        UPDATE zm
        SET
            Status = @NowyStatus,
            DataAnulowania = CASE WHEN @NowyStatus = 'Anulowane' THEN GETDATE() ELSE DataAnulowania END,
            AnulowanePrzez = CASE WHEN @NowyStatus = 'Anulowane' THEN @Uzytkownik ELSE AnulowanePrzez END
        FROM [dbo].[ZamowieniaMieso] zm
        INNER JOIN OPENJSON(@IdsJson) ids ON zm.Id = CAST(ids.value AS INT);

        SET @UpdatedCount = @@ROWCOUNT;

        -- Dodaj wpis do historii dla każdego zaktualizowanego zamówienia
        INSERT INTO [dbo].[ZamowieniaMiesoHistoria] (ZamowienieId, DataZmiany, TypZmiany, Uzytkownik, OpisZmiany)
        SELECT
            CAST(ids.value AS INT),
            GETDATE(),
            'Zmiana statusu',
            @Uzytkownik,
            'Zmiana statusu na: ' + @NowyStatus
        FROM OPENJSON(@IdsJson) ids;

        COMMIT TRANSACTION;

        SELECT @UpdatedCount as UpdatedCount;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

PRINT 'Utworzono sp_BatchUpdateZamowieniaStatus'
GO

-- =====================================================
-- SP: Dashboard KPIs
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetDashboardKPIs')
    DROP PROCEDURE sp_GetDashboardKPIs
GO

CREATE PROCEDURE [dbo].[sp_GetDashboardKPIs]
    @Data DATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Wszystkie KPI w jednym zapytaniu
    SELECT
        -- Zamówienia na dziś
        (SELECT COUNT(*) FROM [dbo].[ZamowieniaMieso] WHERE DataUboju = @Data) as ZamowieniaDzisiaj,
        (SELECT COUNT(*) FROM [dbo].[ZamowieniaMieso] WHERE DataUboju = @Data AND Status = 'Aktywne') as ZamowieniaAktywne,
        (SELECT COUNT(*) FROM [dbo].[ZamowieniaMieso] WHERE DataUboju = @Data AND Status = 'Anulowane') as ZamowieniaAnulowane,
        (SELECT ISNULL(SUM(zmt.Ilosc), 0) FROM [dbo].[ZamowieniaMieso] zm
         INNER JOIN [dbo].[ZamowieniaMiesoTowar] zmt ON zm.Id = zmt.ZamowienieId
         WHERE zm.DataUboju = @Data AND zm.Status = 'Aktywne') as SumaKgDzisiaj,

        -- Zamówienia tydzień
        (SELECT COUNT(*) FROM [dbo].[ZamowieniaMieso]
         WHERE DataUboju >= DATEADD(DAY, -7, @Data) AND DataUboju <= @Data) as ZamowieniaTydzien,

        -- Zamówienia miesiąc
        (SELECT COUNT(*) FROM [dbo].[ZamowieniaMieso]
         WHERE DataUboju >= DATEADD(MONTH, -1, @Data) AND DataUboju <= @Data) as ZamowieniaMiesiac,

        -- Top 5 klientów dziś
        (SELECT COUNT(DISTINCT KlientId) FROM [dbo].[ZamowieniaMieso] WHERE DataUboju = @Data) as LiczbaKlientowDzisiaj;
END
GO

PRINT 'Utworzono sp_GetDashboardKPIs'
GO

-- =====================================================
-- SP: Wyszukiwanie z autouzupełnianiem
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_SearchOdbiorcy')
    DROP PROCEDURE sp_SearchOdbiorcy
GO

CREATE PROCEDURE [dbo].[sp_SearchOdbiorcy]
    @SearchText NVARCHAR(100),
    @MaxResults INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    -- Wyszukiwanie w cache lub delegacja do bazy Handel
    -- To jest placeholder - w rzeczywistości łączy się z bazą Handel
    SELECT TOP (@MaxResults)
        KlientId,
        MAX(DataUboju) as OstatnieZamowienie,
        COUNT(*) as LiczbaZamowien
    FROM [dbo].[ZamowieniaMieso]
    WHERE KlientId IN (
        SELECT DISTINCT KlientId FROM [dbo].[ZamowieniaMieso]
    )
    GROUP BY KlientId
    ORDER BY LiczbaZamowien DESC;
END
GO

PRINT 'Utworzono sp_SearchOdbiorcy'
GO

PRINT ''
PRINT '=== OPTYMALIZACJA ZAKOŃCZONA ==='
PRINT 'Utworzono indeksy i stored procedures.'
PRINT ''
GO
