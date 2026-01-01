-- Tabela przechowująca domyślną kolejność towarów w Dashboard
-- Uruchom ten skrypt w bazie LibraNet

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KolejnoscTowarow')
BEGIN
    CREATE TABLE dbo.KolejnoscTowarow (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        TowarId INT NOT NULL,              -- ID towaru z HANDEL.HM.TW
        Pozycja INT NOT NULL,              -- Numer pozycji (1, 2, 3...)
        DataModyfikacji DATETIME DEFAULT GETDATE(),
        ZmodyfikowalPrzez NVARCHAR(100) NULL
    );

    -- Unikalny indeks - każdy towar może mieć tylko jedną pozycję
    CREATE UNIQUE INDEX IX_KolejnoscTowarow_TowarId ON dbo.KolejnoscTowarow(TowarId);

    -- Indeks na pozycję dla szybkiego sortowania
    CREATE INDEX IX_KolejnoscTowarow_Pozycja ON dbo.KolejnoscTowarow(Pozycja);

    PRINT 'Tabela KolejnoscTowarow została utworzona.';
END
ELSE
BEGIN
    PRINT 'Tabela KolejnoscTowarow już istnieje.';
END
GO
