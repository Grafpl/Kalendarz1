-- ============================================================================
-- BilansSkladniki — "pula bilansu" w Podsumowaniu dnia (Zamówienia Klientów)
-- ----------------------------------------------------------------------------
-- Mapuje towary-dzieci (np. Noga, Pałka, Udziec) na towar-rodzic (np. Ćwiartka).
-- Zamówienia/wydania dzieci są doliczane do puli rodzica na kartach
-- "Podsumowanie dnia" w oknie Zamówienia Klientów (WPF.MainWindow).
--
-- Baza: LibraNet (192.168.0.109)  — tu, bo user 'pronova' nie ma CREATE DATABASE.
-- Towar Id = HM.TW.id z HANDEL (Sage Symfonia .112) — ta sama przestrzeń ID
-- co LibraNet.ZamowieniaMiesoTowar.KodTowaru.
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BilansSkladniki')
BEGIN
    CREATE TABLE dbo.BilansSkladniki (
        Id               INT IDENTITY(1,1) PRIMARY KEY,
        ParentTowarId    INT            NOT NULL,   -- towar-rodzic (pula), np. Ćwiartka
        ParentNazwa      NVARCHAR(200)  NULL,       -- snapshot nazwy rodzica
        ChildTowarId     INT            NOT NULL,   -- towar-dziecko, np. Noga
        ChildNazwa       NVARCHAR(200)  NULL,       -- snapshot nazwy dziecka
        Aktywny          BIT            NOT NULL CONSTRAINT DF_BilansSkladniki_Aktywny DEFAULT 1,
        DataModyfikacji  DATETIME       NOT NULL CONSTRAINT DF_BilansSkladniki_Data DEFAULT GETDATE(),
        ModyfikowalPrzez NVARCHAR(100)  NULL
    );

    CREATE UNIQUE INDEX UX_BilansSkladniki_Pair
        ON dbo.BilansSkladniki(ParentTowarId, ChildTowarId);
END
GO
