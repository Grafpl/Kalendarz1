-- Tabela przechowująca zdjęcia produktów
-- Uruchom ten skrypt w bazie LibraNet

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TowarZdjecia')
BEGIN
    CREATE TABLE dbo.TowarZdjecia (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        TowarId INT NOT NULL,                    -- ID towaru z HANDEL.HM.TW
        Zdjecie VARBINARY(MAX) NOT NULL,         -- Dane binarne obrazka
        NazwaPliku NVARCHAR(255) NULL,           -- Oryginalna nazwa pliku
        TypMIME NVARCHAR(100) NULL,              -- np. image/jpeg, image/png
        Szerokosc INT NULL,                      -- Szerokość w pikselach
        Wysokosc INT NULL,                       -- Wysokość w pikselach
        RozmiarKB INT NULL,                      -- Rozmiar w KB
        DataDodania DATETIME DEFAULT GETDATE(), -- Data dodania
        DodanyPrzez NVARCHAR(100) NULL,          -- Użytkownik który dodał
        Aktywne BIT DEFAULT 1                    -- Czy zdjęcie jest aktywne
    );

    -- Indeks na TowarId dla szybkiego wyszukiwania
    CREATE INDEX IX_TowarZdjecia_TowarId ON dbo.TowarZdjecia(TowarId);

    PRINT 'Tabela TowarZdjecia została utworzona.';
END
ELSE
BEGIN
    PRINT 'Tabela TowarZdjecia już istnieje.';
END
GO

-- Procedura do pobrania zdjęcia produktu
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'GetTowarZdjecie')
    DROP PROCEDURE dbo.GetTowarZdjecie;
GO

CREATE PROCEDURE dbo.GetTowarZdjecie
    @TowarId INT
AS
BEGIN
    SELECT TOP 1 Id, Zdjecie, NazwaPliku, TypMIME, Szerokosc, Wysokosc
    FROM dbo.TowarZdjecia
    WHERE TowarId = @TowarId AND Aktywne = 1
    ORDER BY DataDodania DESC;
END
GO

-- Procedura do zapisania/aktualizacji zdjęcia produktu
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'SaveTowarZdjecie')
    DROP PROCEDURE dbo.SaveTowarZdjecie;
GO

CREATE PROCEDURE dbo.SaveTowarZdjecie
    @TowarId INT,
    @Zdjecie VARBINARY(MAX),
    @NazwaPliku NVARCHAR(255),
    @TypMIME NVARCHAR(100),
    @Szerokosc INT,
    @Wysokosc INT,
    @RozmiarKB INT,
    @DodanyPrzez NVARCHAR(100)
AS
BEGIN
    -- Dezaktywuj poprzednie zdjęcia tego towaru
    UPDATE dbo.TowarZdjecia
    SET Aktywne = 0
    WHERE TowarId = @TowarId;

    -- Dodaj nowe zdjęcie
    INSERT INTO dbo.TowarZdjecia (TowarId, Zdjecie, NazwaPliku, TypMIME, Szerokosc, Wysokosc, RozmiarKB, DodanyPrzez)
    VALUES (@TowarId, @Zdjecie, @NazwaPliku, @TypMIME, @Szerokosc, @Wysokosc, @RozmiarKB, @DodanyPrzez);

    SELECT SCOPE_IDENTITY() AS NewId;
END
GO

PRINT 'Procedury zostały utworzone.';
