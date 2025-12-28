-- =====================================================
-- OPTYMALIZACJA BAZY DANYCH - INDEKSY I STORED PROCEDURES
-- Uruchom na bazie LibraNet
-- Wersja 2.0 - dostosowana do aktualnego schematu
-- =====================================================

PRINT '=== TWORZENIE INDEKSÓW ==='
GO

-- 1. Indeks dla wyszukiwania zamówień po dacie przyjazdu
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ZamowieniaMieso_DataPrzyjazdu')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ZamowieniaMieso_DataPrzyjazdu]
    ON [dbo].[ZamowieniaMieso] ([DataPrzyjazdu])
    INCLUDE ([KlientId], [Status])
    PRINT 'Utworzono indeks IX_ZamowieniaMieso_DataPrzyjazdu'
END
GO

-- 2. Indeks dla wyszukiwania po kliencie i statusie
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ZamowieniaMieso_KlientId_Status')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ZamowieniaMieso_KlientId_Status]
    ON [dbo].[ZamowieniaMieso] ([KlientId], [Status])
    INCLUDE ([DataPrzyjazdu], [DataZamowienia])
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
    INCLUDE ([KodTowaru], [Ilosc], [Cena])
    PRINT 'Utworzono indeks IX_ZamowieniaMiesoTowar_ZamowienieId'
END
GO

-- 5. Indeks dla transportu
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ZamowieniaMieso_TransportKursID')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ZamowieniaMieso_TransportKursID]
    ON [dbo].[ZamowieniaMieso] ([TransportKursID])
    WHERE [TransportKursID] IS NOT NULL
    PRINT 'Utworzono indeks IX_ZamowieniaMieso_TransportKursID'
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
    @DataPrzyjazdu DATE,
    @Status NVARCHAR(50) = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 100
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        zm.Id,
        zm.KlientId,
        zm.DataPrzyjazdu,
        zm.DataZamowienia,
        zm.Status,
        zm.Uwagi,
        zm.IdUser,
        zm.DataUtworzenia,
        zm.AnulowanePrzez,
        zm.DataAnulowania,
        zm.PrzyczynaAnulowania,
        zm.TransportKursID,
        zm.CzyZrealizowane,
        ISNULL(zmt.IloscSuma, 0) as IloscTowarow
    FROM [dbo].[ZamowieniaMieso] zm
    LEFT JOIN (
        SELECT ZamowienieId,
               SUM(Ilosc) as IloscSuma
        FROM [dbo].[ZamowieniaMiesoTowar]
        GROUP BY ZamowienieId
    ) zmt ON zm.Id = zmt.ZamowienieId
    WHERE zm.DataPrzyjazdu = @DataPrzyjazdu
      AND (@Status IS NULL OR zm.Status = @Status)
    ORDER BY zm.Id
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    -- Zwróć też łączną liczbę rekordów
    SELECT COUNT(*) as TotalCount
    FROM [dbo].[ZamowieniaMieso]
    WHERE DataPrzyjazdu = @DataPrzyjazdu
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
        MAX(COALESCE(zm.DataAnulowania, zm.DataPrzyjazdu, zm.DataZamowienia)) as OstatniaData
    FROM [dbo].[ZamowieniaMieso] zm
    LEFT JOIN (
        SELECT ZamowienieId, SUM(Ilosc) as IloscSuma
        FROM [dbo].[ZamowieniaMiesoTowar]
        GROUP BY ZamowienieId
    ) zmt ON zm.Id = zmt.ZamowienieId
    WHERE zm.Status = 'Anulowane'
      AND COALESCE(zm.DataAnulowania, zm.DataPrzyjazdu, zm.DataZamowienia) >= @DataOd
      AND COALESCE(zm.DataAnulowania, zm.DataPrzyjazdu, zm.DataZamowienia) <= @DataDo
    GROUP BY zm.KlientId
    ORDER BY LiczbaAnulowanych DESC;

    -- Statystyki per przyczyna
    SELECT
        ISNULL(PrzyczynaAnulowania, 'Brak przyczyny') as Przyczyna,
        COUNT(*) as Liczba
    FROM [dbo].[ZamowieniaMieso]
    WHERE Status = 'Anulowane'
      AND COALESCE(DataAnulowania, DataPrzyjazdu, DataZamowienia) >= @DataOd
      AND COALESCE(DataAnulowania, DataPrzyjazdu, DataZamowienia) <= @DataDo
    GROUP BY ISNULL(PrzyczynaAnulowania, 'Brak przyczyny')
    ORDER BY Liczba DESC;
END
GO

PRINT 'Utworzono sp_GetStatystykiAnulowanych'
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
        (SELECT COUNT(*) FROM [dbo].[ZamowieniaMieso] WHERE DataPrzyjazdu = @Data) as ZamowieniaDzisiaj,
        (SELECT COUNT(*) FROM [dbo].[ZamowieniaMieso] WHERE DataPrzyjazdu = @Data AND (Status IS NULL OR Status = '' OR Status = 'Aktywne')) as ZamowieniaAktywne,
        (SELECT COUNT(*) FROM [dbo].[ZamowieniaMieso] WHERE DataPrzyjazdu = @Data AND Status = 'Anulowane') as ZamowieniaAnulowane,
        (SELECT ISNULL(SUM(zmt.Ilosc), 0) FROM [dbo].[ZamowieniaMieso] zm
         INNER JOIN [dbo].[ZamowieniaMiesoTowar] zmt ON zm.Id = zmt.ZamowienieId
         WHERE zm.DataPrzyjazdu = @Data AND (zm.Status IS NULL OR zm.Status != 'Anulowane')) as SumaKgDzisiaj,

        -- Zamówienia tydzień
        (SELECT COUNT(*) FROM [dbo].[ZamowieniaMieso]
         WHERE DataPrzyjazdu >= DATEADD(DAY, -7, @Data) AND DataPrzyjazdu <= @Data) as ZamowieniaTydzien,

        -- Zamówienia miesiąc
        (SELECT COUNT(*) FROM [dbo].[ZamowieniaMieso]
         WHERE DataPrzyjazdu >= DATEADD(MONTH, -1, @Data) AND DataPrzyjazdu <= @Data) as ZamowieniaMiesiac,

        -- Liczba klientów dziś
        (SELECT COUNT(DISTINCT KlientId) FROM [dbo].[ZamowieniaMieso] WHERE DataPrzyjazdu = @Data) as LiczbaKlientowDzisiaj;
END
GO

PRINT 'Utworzono sp_GetDashboardKPIs'
GO

-- =====================================================
-- SP: Podsumowanie towarów na dzień
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetPodsumowanieTowarowNaDzien')
    DROP PROCEDURE sp_GetPodsumowanieTowarowNaDzien
GO

CREATE PROCEDURE [dbo].[sp_GetPodsumowanieTowarowNaDzien]
    @Data DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        zmt.KodTowaru,
        SUM(zmt.Ilosc) as SumaIlosc,
        COUNT(DISTINCT zm.Id) as LiczbaZamowien
    FROM [dbo].[ZamowieniaMieso] zm
    INNER JOIN [dbo].[ZamowieniaMiesoTowar] zmt ON zm.Id = zmt.ZamowienieId
    WHERE zm.DataPrzyjazdu = @Data
      AND (zm.Status IS NULL OR zm.Status != 'Anulowane')
    GROUP BY zmt.KodTowaru
    ORDER BY SumaIlosc DESC;
END
GO

PRINT 'Utworzono sp_GetPodsumowanieTowarowNaDzien'
GO

PRINT ''
PRINT '=== OPTYMALIZACJA ZAKOŃCZONA ==='
PRINT 'Utworzono indeksy i stored procedures.'
PRINT ''
GO
