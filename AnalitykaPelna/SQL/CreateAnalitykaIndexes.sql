-- ============================================================================
-- INDEKSY DLA MODUŁU "ANALITYKA PEŁNA"
-- TYLKO baza LibraNet (192.168.0.109) — NIE ruszamy bazy Handel/Symfonia (192.168.0.112).
-- Uruchomić raz w SSMS po zalogowaniu jako pronova/pronova na 192.168.0.109.
-- ============================================================================

USE [LibraNet];
GO

-- Compound index dla WidokRealizacja (filtry: Data + ArticleID + TermID)
-- Pokrywa większość zapytań nad tabelą In0E (~2.1M wierszy)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_In0E_Data_Article_Term' AND object_id = OBJECT_ID('dbo.In0E'))
BEGIN
    CREATE INDEX IX_In0E_Data_Article_Term
    ON dbo.In0E (Data, ArticleID, TermID)
    INCLUDE (ActWeight, OperatorID, P1, QntInCont, Godzina, Wagowy, Tara, ArticleName);
    PRINT '✅ IX_In0E_Data_Article_Term utworzony';
END
ELSE
    PRINT '⏩ IX_In0E_Data_Article_Term już istnieje';
GO

-- Index pomocniczy: PartiaDostawca po Partia (do JOIN-a w filtrze dostawcy w Realizacji)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PartiaDostawca_Partia' AND object_id = OBJECT_ID('dbo.PartiaDostawca'))
BEGIN
    CREATE INDEX IX_PartiaDostawca_Partia
    ON dbo.PartiaDostawca (Partia)
    INCLUDE (CustomerID, CustomerName);
    PRINT '✅ IX_PartiaDostawca_Partia utworzony';
END
ELSE
    PRINT '⏩ IX_PartiaDostawca_Partia już istnieje';
GO

-- Index dla cross-DB Wydajność (suma kg pre-cut per data)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Out1A_Data_Article' AND object_id = OBJECT_ID('dbo.Out1A'))
BEGIN
    CREATE INDEX IX_Out1A_Data_Article
    ON dbo.Out1A (Data, ArticleID)
    INCLUDE (ActWeight, P1);
    PRINT '✅ IX_Out1A_Data_Article utworzony';
END
ELSE
    PRINT '⏩ IX_Out1A_Data_Article już istnieje';
GO

PRINT '═══════════════════════════════════════════════';
PRINT '   Indeksy LibraNet dla Analityki Pełnej OK';
PRINT '   (Handel/Symfonia 192.168.0.112 — NIE ruszamy)';
PRINT '═══════════════════════════════════════════════';
