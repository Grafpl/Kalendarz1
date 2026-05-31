-- ============================================================
-- Reklamacje — indeksy poprawiajace startup okna Panel Reklamacji
-- Generowane na podstawie StartupProfiler — diagnoza 2026-05-31
-- DB: LibraNet (192.168.0.109)
-- ============================================================
-- Uruchom raz na bazie LibraNet (read/write user, np. sa).
-- Wszystkie indeksy stworzone z IF NOT EXISTS — bezpieczne do re-run.
-- ============================================================

USE [LibraNet];
GO

-- ============================================================
-- 1) Reklamacje: glowne sortowanie i filtrowanie
-- Wczesniej: WczytajReklamacje sortuje po DataZgloszenia DESC bez wsparcia indeksu => 539ms.
-- Po: covering index na 2 najczestsze kolumny w WHERE/ORDER => ~50-100ms.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Reklamacje_DataZgloszenia_Typ' AND object_id = OBJECT_ID('dbo.Reklamacje'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Reklamacje_DataZgloszenia_Typ
        ON [dbo].[Reklamacje]([DataZgloszenia] DESC, [TypReklamacji])
        INCLUDE ([StatusV2], [WymagaUzupelnienia], [PowiazanaReklamacjaId]);
    PRINT 'Utworzono: IX_Reklamacje_DataZgloszenia_Typ';
END
ELSE
    PRINT 'Pominieto: IX_Reklamacje_DataZgloszenia_Typ (juz istnieje)';
GO

-- ============================================================
-- 2) Reklamacje: szybki check duplikatow w SyncFakturyKorygujace
-- Wczesniej: SELECT IdDokumentu WHERE TypReklamacji='Faktura korygujaca' (table scan).
-- Po: filtered index po IdDokumentu na korektach => O(log n) lookup.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Reklamacje_IdDokumentu_Korekty' AND object_id = OBJECT_ID('dbo.Reklamacje'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Reklamacje_IdDokumentu_Korekty
        ON [dbo].[Reklamacje]([IdDokumentu])
        WHERE [TypReklamacji] = 'Faktura korygujaca' AND [IdDokumentu] IS NOT NULL;
    PRINT 'Utworzono: IX_Reklamacje_IdDokumentu_Korekty (filtered)';
END
ELSE
    PRINT 'Pominieto: IX_Reklamacje_IdDokumentu_Korekty (juz istnieje)';
GO

-- ============================================================
-- 3) ReklamacjeZdjecia: top-3 miniatur per reklamacja
-- Wczesniej: ROW_NUMBER() OVER (PARTITION BY IdReklamacji ORDER BY DataDodania DESC) skanuje cala tabele.
-- Po: indeks (IdReklamacji, DataDodania DESC) — naturalna kolejnosc dla ROW_NUMBER.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ReklamacjeZdjecia_IdReklamacji_Data' AND object_id = OBJECT_ID('dbo.ReklamacjeZdjecia'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ReklamacjeZdjecia_IdReklamacji_Data
        ON [dbo].[ReklamacjeZdjecia]([IdReklamacji], [DataDodania] DESC);
    PRINT 'Utworzono: IX_ReklamacjeZdjecia_IdReklamacji_Data';
END
ELSE
    PRINT 'Pominieto: IX_ReklamacjeZdjecia_IdReklamacji_Data (juz istnieje)';
GO

-- ============================================================
-- 4) Reklamacje: auto-match (ProbujAutoMatch wewnatrz SyncFakturyKorygujace)
-- Wczesniej: WHERE IdKontrahenta=@khid AND DataZgloszenia BETWEEN ... (table scan przy duzej liczbie reklamacji).
-- Po: covering index na (IdKontrahenta, DataZgloszenia).
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Reklamacje_IdKontrahenta_Data' AND object_id = OBJECT_ID('dbo.Reklamacje'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Reklamacje_IdKontrahenta_Data
        ON [dbo].[Reklamacje]([IdKontrahenta], [DataZgloszenia])
        INCLUDE ([StatusV2], [TypReklamacji], [PowiazanaReklamacjaId]);
    PRINT 'Utworzono: IX_Reklamacje_IdKontrahenta_Data';
END
ELSE
    PRINT 'Pominieto: IX_Reklamacje_IdKontrahenta_Data (juz istnieje)';
GO

-- ============================================================
-- Statystyki tabel po utworzeniu indeksow
-- ============================================================
PRINT '';
PRINT '=== STATYSTYKI TABEL ===';
SELECT
    OBJECT_NAME(object_id) AS Tabela,
    name AS Indeks,
    type_desc AS Typ,
    is_unique,
    has_filter,
    filter_definition
FROM sys.indexes
WHERE object_id IN (OBJECT_ID('dbo.Reklamacje'), OBJECT_ID('dbo.ReklamacjeZdjecia'))
  AND type_desc <> 'HEAP'
ORDER BY OBJECT_NAME(object_id), name;
GO

PRINT '';
PRINT '=== ROZMIAR TABEL ===';
SELECT
    t.name AS Tabela,
    SUM(p.rows) AS Wierszy,
    SUM(a.total_pages) * 8 AS RozmiarKB
FROM sys.tables t
INNER JOIN sys.partitions p ON t.object_id = p.object_id
INNER JOIN sys.allocation_units a ON p.partition_id = a.container_id
WHERE t.name IN ('Reklamacje', 'ReklamacjeZdjecia', 'ReklamacjeTowary', 'ReklamacjeKomentarze', 'ReklamacjeZalaczniki')
  AND p.index_id < 2
GROUP BY t.name
ORDER BY SUM(a.total_pages) DESC;
GO

PRINT '';
PRINT 'Skrypt zakonczony. Restart aplikacji + ponowny test startupu w StartupProfiler.';
