-- Dodanie kolumny Strefa do tabeli ZamowieniaMiesoTowar (per-product)
-- Strefa = 1 oznacza, że mięso pochodzi ze strefy ptasiej grypy lub pomoru
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[ZamowieniaMiesoTowar]')
    AND name = 'Strefa'
)
BEGIN
    ALTER TABLE [dbo].[ZamowieniaMiesoTowar] ADD Strefa BIT NOT NULL DEFAULT 0;
END

-- Dodanie kolumny Strefa do tabeli SzablonyZamowienTowar (templates)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[SzablonyZamowienTowar]')
    AND name = 'Strefa'
)
BEGIN
    ALTER TABLE [dbo].[SzablonyZamowienTowar] ADD Strefa BIT NOT NULL DEFAULT 0;
END
