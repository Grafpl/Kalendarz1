-- Skrypt dodający kolumnę PrzyczynaAnulowania do tabeli ZamowieniaMieso
-- Uruchom ten skrypt na bazie LibraNet

-- Sprawdź czy kolumna już istnieje, jeśli nie - dodaj ją
IF NOT EXISTS (SELECT * FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[dbo].[ZamowieniaMieso]')
               AND name = 'PrzyczynaAnulowania')
BEGIN
    ALTER TABLE [dbo].[ZamowieniaMieso]
    ADD [PrzyczynaAnulowania] NVARCHAR(200) NULL

    PRINT 'Dodano kolumnę PrzyczynaAnulowania do tabeli ZamowieniaMieso'
END
ELSE
BEGIN
    PRINT 'Kolumna PrzyczynaAnulowania już istnieje'
END
GO

-- Opcjonalnie: Utwórz indeks dla szybszego wyszukiwania po przyczynie
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE name = 'IX_ZamowieniaMieso_PrzyczynaAnulowania'
               AND object_id = OBJECT_ID('dbo.ZamowieniaMieso'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ZamowieniaMieso_PrzyczynaAnulowania]
    ON [dbo].[ZamowieniaMieso] ([PrzyczynaAnulowania])
    WHERE [Status] = 'Anulowane'

    PRINT 'Utworzono indeks IX_ZamowieniaMieso_PrzyczynaAnulowania'
END
GO
