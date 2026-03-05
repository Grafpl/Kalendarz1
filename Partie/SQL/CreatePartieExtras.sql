-- ============================================================
-- PARTIE MODULE - Dodatkowe obiekty SQL
-- Baza: LibraNet (192.168.0.109)
-- ============================================================

-- 1. Tabela audytu operacji na partiach
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PartiaAuditLog')
BEGIN
    CREATE TABLE PartiaAuditLog (
        ID              int           NOT NULL IDENTITY PRIMARY KEY,
        Partia          varchar(15)   NOT NULL,
        Akcja           nvarchar(30)  NOT NULL,   -- 'Otwarta','Zamknieta','PonownieOtwarta','QC_Temp','QC_Wady','Zdjecie'
        Opis            nvarchar(500) NULL,
        OperatorID      varchar(15)   NULL,
        OperatorNazwa   nvarchar(50)  NULL,
        CreatedAtUTC    datetime2     NOT NULL DEFAULT SYSUTCDATETIME()
    );
    PRINT 'Created table PartiaAuditLog';
END
GO

CREATE INDEX IX_PAL_Partia ON PartiaAuditLog(Partia);
GO

-- 2. Indeksy krytyczne na tabelach wazen (Out1A ~1.9M rek, In0E ~2M rek)
--    Bez nich zapytania z GROUP BY P1 beda trwaly minuty zamiast sekund.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Out1A_P1_ActWeight' AND object_id = OBJECT_ID('Out1A'))
BEGIN
    CREATE INDEX IX_Out1A_P1_ActWeight ON Out1A(P1) INCLUDE (ActWeight, Quantity, ArticleID, ArticleName, JM)
    WHERE ActWeight IS NOT NULL;
    PRINT 'Created index IX_Out1A_P1_ActWeight on Out1A';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_In0E_P1_ActWeight' AND object_id = OBJECT_ID('In0E'))
BEGIN
    CREATE INDEX IX_In0E_P1_ActWeight ON In0E(P1) INCLUDE (ActWeight, Quantity, ArticleID, ArticleName, JM)
    WHERE ActWeight IS NOT NULL;
    PRINT 'Created index IX_In0E_P1_ActWeight on In0E';
END
GO

-- 3. Indeks na listapartii.Partia (jesli nie istnieje)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_listapartii_Partia' AND object_id = OBJECT_ID('listapartii'))
BEGIN
    CREATE UNIQUE INDEX IX_listapartii_Partia ON listapartii(Partia);
    PRINT 'Created index IX_listapartii_Partia';
END
GO

-- 4. Indeks na PartiaDostawca.Partia
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PartiaDostawca_Partia' AND object_id = OBJECT_ID('PartiaDostawca'))
BEGIN
    CREATE INDEX IX_PartiaDostawca_Partia ON PartiaDostawca(Partia) INCLUDE (CustomerID, CustomerName);
    PRINT 'Created index IX_PartiaDostawca_Partia';
END
GO

PRINT 'Partie module SQL setup complete.';
GO
