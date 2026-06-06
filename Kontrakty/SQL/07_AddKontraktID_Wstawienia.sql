-- =========================================================
-- Kolumna KontraktID w WstawieniaKurczakow
-- Łączy wstawienie z kontraktem ZPSP (dbo.Kontrakty.Id).
-- NULL = wstawienie utworzone ręcznie (poza kreatorem kontraktu).
-- =========================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = 'KontraktID' AND Object_ID = Object_ID('dbo.WstawieniaKurczakow')
)
BEGIN
    ALTER TABLE dbo.WstawieniaKurczakow ADD KontraktID INT NULL;
    PRINT 'Dodano kolumnę KontraktID';
END
ELSE
    PRINT 'KontraktID już istnieje — pomijam';
GO

-- Indeks pomocniczy (filtrowany — tylko nie-NULL, bo większość legacy = NULL)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WstawieniaKurczakow_KontraktID')
BEGIN
    CREATE NONCLUSTERED INDEX IX_WstawieniaKurczakow_KontraktID
        ON dbo.WstawieniaKurczakow(KontraktID)
        INCLUDE (Dostawca, DataWstawienia)
        WHERE KontraktID IS NOT NULL;
    PRINT 'Utworzono indeks IX_WstawieniaKurczakow_KontraktID';
END
GO

-- FK do dbo.Kontrakty (NO ACTION na DELETE — chcemy ostrzec, nie kaskadować)
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_WstawieniaKurczakow_Kontrakty'
)
BEGIN
    BEGIN TRY
        ALTER TABLE dbo.WstawieniaKurczakow
            ADD CONSTRAINT FK_WstawieniaKurczakow_Kontrakty
                FOREIGN KEY (KontraktID)
                REFERENCES dbo.Kontrakty(Id)
                ON DELETE NO ACTION;
        PRINT 'Utworzono FK_WstawieniaKurczakow_Kontrakty';
    END TRY
    BEGIN CATCH
        PRINT 'FK NIE UTWORZONY (dane orphan lub brak Kontrakty.Id?): ' + ERROR_MESSAGE();
    END CATCH
END
GO
