-- ============================================
-- System wykrywania zmian w zamówieniach
-- Data: 2025-12-09
-- ============================================

-- 1. Dodaj kolumnę DataOstatniejModyfikacji do głównej tabeli zamówień
-- (pozwala szybko sprawdzić czy zamówienie zostało zmodyfikowane)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMieso') AND name = 'DataOstatniejModyfikacji')
BEGIN
    ALTER TABLE dbo.ZamowieniaMieso ADD DataOstatniejModyfikacji DATETIME NULL;
    PRINT 'Dodano kolumnę DataOstatniejModyfikacji do ZamowieniaMieso';
END
GO

-- 1b. Dodaj kolumnę DataAkceptacjiMagazyn - osobna akceptacja dla magazynu
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMieso') AND name = 'DataAkceptacjiMagazyn')
BEGIN
    ALTER TABLE dbo.ZamowieniaMieso ADD DataAkceptacjiMagazyn DATETIME NULL;
    PRINT 'Dodano kolumnę DataAkceptacjiMagazyn do ZamowieniaMieso';
END
GO

-- 1c. Dodaj kolumnę DataAkceptacjiProdukcja - osobna akceptacja dla produkcji
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMieso') AND name = 'DataAkceptacjiProdukcja')
BEGIN
    ALTER TABLE dbo.ZamowieniaMieso ADD DataAkceptacjiProdukcja DATETIME NULL;
    PRINT 'Dodano kolumnę DataAkceptacjiProdukcja do ZamowieniaMieso';
END
GO

-- 2. Utwórz tabelę snapshotów pozycji zamówienia
-- (zapisuje stan pozycji w momencie realizacji/wydania)
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = 'ZamowieniaMiesoSnapshot' AND type = 'U')
BEGIN
    CREATE TABLE dbo.ZamowieniaMiesoSnapshot (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ZamowienieId INT NOT NULL,
        KodTowaru INT NOT NULL,
        Ilosc DECIMAL(18,3) NOT NULL,
        Folia BIT NULL,
        Hallal BIT NULL,
        DataSnapshotu DATETIME NOT NULL DEFAULT GETDATE(),
        TypSnapshotu NVARCHAR(20) NOT NULL, -- 'Realizacja' lub 'Wydanie'

        CONSTRAINT FK_Snapshot_Zamowienie FOREIGN KEY (ZamowienieId)
            REFERENCES dbo.ZamowieniaMieso(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_Snapshot_ZamowienieId ON dbo.ZamowieniaMiesoSnapshot(ZamowienieId);
    CREATE INDEX IX_Snapshot_Typ ON dbo.ZamowieniaMiesoSnapshot(ZamowienieId, TypSnapshotu);

    PRINT 'Utworzono tabelę ZamowieniaMiesoSnapshot';
END
GO

-- 3. Trigger do automatycznej aktualizacji DataOstatniejModyfikacji
-- przy każdej zmianie pozycji zamówienia
IF EXISTS (SELECT 1 FROM sys.triggers WHERE name = 'TR_ZamowieniaMiesoTowar_UpdateModyfikacja')
    DROP TRIGGER dbo.TR_ZamowieniaMiesoTowar_UpdateModyfikacja;
GO

CREATE TRIGGER dbo.TR_ZamowieniaMiesoTowar_UpdateModyfikacja
ON dbo.ZamowieniaMiesoTowar
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    -- Pobierz ID zamówień, które zostały zmodyfikowane
    DECLARE @ZamowieniaIds TABLE (Id INT);

    INSERT INTO @ZamowieniaIds (Id)
    SELECT DISTINCT ZamowienieId FROM inserted
    UNION
    SELECT DISTINCT ZamowienieId FROM deleted;

    -- Zaktualizuj datę modyfikacji dla tych zamówień
    UPDATE z
    SET DataOstatniejModyfikacji = GETDATE()
    FROM dbo.ZamowieniaMieso z
    INNER JOIN @ZamowieniaIds zi ON z.Id = zi.Id;
END
GO

PRINT 'Utworzono trigger TR_ZamowieniaMiesoTowar_UpdateModyfikacja';

-- 4. Procedura do zapisywania snapshotu pozycji zamówienia
IF EXISTS (SELECT 1 FROM sys.procedures WHERE name = 'sp_SaveOrderSnapshot')
    DROP PROCEDURE dbo.sp_SaveOrderSnapshot;
GO

CREATE PROCEDURE dbo.sp_SaveOrderSnapshot
    @ZamowienieId INT,
    @TypSnapshotu NVARCHAR(20) -- 'Realizacja' lub 'Wydanie'
AS
BEGIN
    SET NOCOUNT ON;

    -- Usuń stary snapshot tego samego typu (jeśli istnieje)
    DELETE FROM dbo.ZamowieniaMiesoSnapshot
    WHERE ZamowienieId = @ZamowienieId AND TypSnapshotu = @TypSnapshotu;

    -- Zapisz nowy snapshot
    INSERT INTO dbo.ZamowieniaMiesoSnapshot (ZamowienieId, KodTowaru, Ilosc, Folia, Hallal, TypSnapshotu)
    SELECT ZamowienieId, KodTowaru, Ilosc, Folia, Hallal, @TypSnapshotu
    FROM dbo.ZamowieniaMiesoTowar
    WHERE ZamowienieId = @ZamowienieId;

    RETURN @@ROWCOUNT;
END
GO

PRINT 'Utworzono procedurę sp_SaveOrderSnapshot';

-- 5. Funkcja do sprawdzania czy zamówienie zostało zmodyfikowane od snapshotu
IF EXISTS (SELECT 1 FROM sys.objects WHERE name = 'fn_IsOrderModifiedSinceSnapshot' AND type = 'FN')
    DROP FUNCTION dbo.fn_IsOrderModifiedSinceSnapshot;
GO

CREATE FUNCTION dbo.fn_IsOrderModifiedSinceSnapshot
(
    @ZamowienieId INT,
    @TypSnapshotu NVARCHAR(20)
)
RETURNS BIT
AS
BEGIN
    DECLARE @IsModified BIT = 0;

    -- Sprawdź czy snapshot istnieje
    IF NOT EXISTS (SELECT 1 FROM dbo.ZamowieniaMiesoSnapshot WHERE ZamowienieId = @ZamowienieId AND TypSnapshotu = @TypSnapshotu)
        RETURN 0; -- Brak snapshotu = nie można porównać

    -- Pobierz datę snapshotu
    DECLARE @DataSnapshotu DATETIME;
    SELECT TOP 1 @DataSnapshotu = DataSnapshotu
    FROM dbo.ZamowieniaMiesoSnapshot
    WHERE ZamowienieId = @ZamowienieId AND TypSnapshotu = @TypSnapshotu;

    -- Sprawdź czy DataOstatniejModyfikacji > DataSnapshotu
    IF EXISTS (
        SELECT 1 FROM dbo.ZamowieniaMieso
        WHERE Id = @ZamowienieId
        AND DataOstatniejModyfikacji IS NOT NULL
        AND DataOstatniejModyfikacji > @DataSnapshotu
    )
        SET @IsModified = 1;

    RETURN @IsModified;
END
GO

PRINT 'Utworzono funkcję fn_IsOrderModifiedSinceSnapshot';
PRINT '';
PRINT '=== INSTALACJA ZAKOŃCZONA POMYŚLNIE ===';
