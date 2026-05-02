-- ════════════════════════════════════════════════════════════════════
-- FAZA 3 — LibraNet (192.168.0.109)
-- Pamiętaj: oddzielna sesja SSMS na 109! Brak TRY_CONVERT (stary SQL).
-- ════════════════════════════════════════════════════════════════════
USE LibraNet;
SET NOCOUNT ON;
GO

-- ─────────────────────────────────────────────────────────────────
-- 1. ⭐ REKLAMACJE — pełen przegląd ostatnich 90 dni
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ L3-1. Reklamacje — statusy i statystyki 90 dni ═══';
SELECT
    Status, StatusV2, TypReklamacji,
    COUNT(*) AS Liczba,
    SUM(SumaKg) AS LacznieKg,
    SUM(SumaWartosc) AS LacznieZl,
    SUM(KosztReklamacji) AS LacznyKoszt
FROM dbo.Reklamacje
WHERE DataZgloszenia >= DATEADD(MONTH, -3, GETDATE())
GROUP BY Status, StatusV2, TypReklamacji
ORDER BY Liczba DESC;
GO

PRINT '═══ L3-2. Top 10 PrzyczynaGlowna (6 mies) ═══';
SELECT TOP 10
    ISNULL(PrzyczynaGlowna, '(brak)') AS Przyczyna,
    ISNULL(KategoriaPrzyczyny, '(brak)') AS Kategoria,
    COUNT(*) AS Liczba, SUM(SumaKg) AS Kg, SUM(SumaWartosc) AS Zl
FROM dbo.Reklamacje
WHERE DataZgloszenia >= DATEADD(MONTH, -6, GETDATE())
GROUP BY PrzyczynaGlowna, KategoriaPrzyczyny
ORDER BY Liczba DESC;
GO

PRINT '═══ L3-3. Reklamacje per handlowiec (6 mies) ═══';
SELECT
    ISNULL(Handlowiec, '(brak)') AS Handlowiec,
    COUNT(*) AS Liczba,
    SUM(SumaKg) AS Kg, SUM(SumaWartosc) AS Zl,
    AVG(CAST(SumaKg AS FLOAT)) AS SrKg
FROM dbo.Reklamacje
WHERE DataZgloszenia >= DATEADD(MONTH, -6, GETDATE())
GROUP BY Handlowiec
ORDER BY Liczba DESC;
GO

PRINT '═══ L3-4. Trend reklamacji per miesiąc (12 mies) ═══';
SELECT
    YEAR(DataZgloszenia) AS Rok,
    MONTH(DataZgloszenia) AS Mies,
    COUNT(*) AS Reklamacji,
    SUM(SumaKg) AS Kg,
    SUM(SumaWartosc) AS Zl
FROM dbo.Reklamacje
WHERE DataZgloszenia >= DATEADD(MONTH, -12, GETDATE())
GROUP BY YEAR(DataZgloszenia), MONTH(DataZgloszenia)
ORDER BY Rok DESC, Mies DESC;
GO

-- ─────────────────────────────────────────────────────────────────
-- 5. ⭐ ZAMÓWIENIA — bilans na dzisiaj/jutro (Plan vs Realizacja)
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ L3-5. Zamówienia mięsa — co czeka na dziś/jutro/3 dni ═══';
SELECT
    Z.DataZamowienia,
    Z.Status,
    Z.TransportStatus,
    Z.ProcentRealizacji,
    Z.CzyZrealizowane, Z.CzyWydane, Z.CzyZafakturowane,
    Z.LiczbaPojemnikow, Z.LiczbaPalet,
    Z.NumerWZ, Z.NumerFaktury,
    COUNT(ZT.Id) AS PozycjiTowar,
    SUM(ZT.Ilosc) AS Lacznie_kg
FROM dbo.ZamowieniaMieso Z
LEFT JOIN dbo.ZamowieniaMiesoTowar ZT ON ZT.ZamowienieId = Z.Id
WHERE Z.DataZamowienia BETWEEN DATEADD(DAY, -1, CAST(GETDATE() AS DATE)) AND DATEADD(DAY, 7, CAST(GETDATE() AS DATE))
GROUP BY Z.Id, Z.DataZamowienia, Z.Status, Z.TransportStatus, Z.ProcentRealizacji,
         Z.CzyZrealizowane, Z.CzyWydane, Z.CzyZafakturowane, Z.LiczbaPojemnikow,
         Z.LiczbaPalet, Z.NumerWZ, Z.NumerFaktury
ORDER BY Z.DataZamowienia, Z.Id DESC;
GO

PRINT '═══ L3-6. Top produkty zamawiane vs zrealizowane (30 dni) ═══';
SELECT
    ZT.KodTowaru,
    COUNT(*) AS LiniiZam,
    SUM(ZT.Ilosc) AS Zam_kg,
    SUM(ISNULL(ZT.IloscZrealizowana, 0)) AS Zrealizowane_kg,
    SUM(ZT.Ilosc - ISNULL(ZT.IloscZrealizowana, 0)) AS Brakujace_kg,
    CASE WHEN SUM(ZT.Ilosc) > 0
         THEN CAST(SUM(ISNULL(ZT.IloscZrealizowana, 0)) * 100.0 / SUM(ZT.Ilosc) AS DECIMAL(5,1)) END AS ProcRealiz
FROM dbo.ZamowieniaMieso Z
JOIN dbo.ZamowieniaMiesoTowar ZT ON ZT.ZamowienieId = Z.Id
WHERE Z.DataZamowienia >= DATEADD(DAY, -30, GETDATE())
GROUP BY ZT.KodTowaru
ORDER BY Zam_kg DESC;
GO

PRINT '═══ L3-7. Powody braku realizacji (PowodBraku) ═══';
SELECT TOP 20
    PowodBraku,
    COUNT(*) AS Wystapien,
    SUM(Ilosc - ISNULL(IloscZrealizowana,0)) AS BrakujaceKg
FROM dbo.ZamowieniaMiesoTowar
WHERE PowodBraku IS NOT NULL AND PowodBraku <> ''
GROUP BY PowodBraku
ORDER BY Wystapien DESC;
GO

-- ─────────────────────────────────────────────────────────────────
-- 8. STATE0E — stan magazynowy (live snapshot)
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ L3-8. State0E — stan magazynu per artykuł (TOP 30) ═══';
SELECT TOP 30
    ArticleID, ArticleName,
    COUNT(*) AS Pojemnikow,
    SUM(InWeight) AS InW,
    SUM(ActWeight) AS ActW,
    SUM(OutWeight) AS OutW,
    SUM(InWeight - ActWeight - OutWeight) AS Pozostalo,
    MIN(InData) AS NajstarszaPartia,
    MAX(InData) AS NajnowszaPartia
FROM dbo.State0E
WHERE Status = '+'
GROUP BY ArticleID, ArticleName
ORDER BY Pozostalo DESC;
GO

PRINT '═══ L3-9. State0E — partie świeże (z ostatnich 7 dni — żeby zobaczyć rotację) ═══';
SELECT TOP 30
    ArticleName,
    Partia,
    COUNT(*) AS Pojemnikow,
    SUM(ActWeight - OutWeight) AS Pozostalo_kg,
    MIN(InData) AS Wprowadzono,
    DATEDIFF(DAY,
        CONVERT(date, MIN(InData), 120),
        CAST(GETDATE() AS DATE)) AS Wieku_dni
FROM dbo.State0E
WHERE Status = '+'
  AND InData >= CONVERT(varchar(10), DATEADD(DAY, -7, GETDATE()), 120)
GROUP BY ArticleName, Partia
ORDER BY Wprowadzono DESC;
GO

-- ─────────────────────────────────────────────────────────────────
-- 10. KONTRAHENCI — czy mamy mapę CRM↔Symfonia w LibraNet
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ L3-10. Tabela kontrahenci — kolumny ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ, max_length
FROM sys.columns
WHERE object_id = OBJECT_ID('dbo.kontrahenci')
ORDER BY column_id;
GO

PRINT '═══ L3-11. Próbka kontrahenci ═══';
SELECT TOP 5 * FROM dbo.kontrahenci;
GO

PRINT '═══ L3-12. KartotekaOdbiorcyDane — kolumny ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ
FROM sys.columns
WHERE object_id = OBJECT_ID('dbo.KartotekaOdbiorcyDane')
ORDER BY column_id;
GO

PRINT '═══ L3-13. Próbka KartotekaOdbiorcyDane (czy są ID Symfonii?) ═══';
SELECT TOP 5 * FROM dbo.KartotekaOdbiorcyDane;
GO

-- ─────────────────────────────────────────────────────────────────
-- 14. UNICARD / Pracownicy — sprawdzenie struktur HR
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ L3-14. Tabele "Pracownicy" itp. ═══';
SELECT name, p.[rows] AS Wierszy
FROM sys.tables t
LEFT JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
WHERE name LIKE '%racownic%' OR name LIKE '%UNICARD%' OR name LIKE '%KG_%' OR name LIKE '%HR_%'
   OR name LIKE '%Agenc%' OR name LIKE '%Stawk%'
ORDER BY name;
GO

PRINT '═══ L3-15. KG_HarmonogramPrzerw — co tu jest ═══';
SELECT TOP 5 * FROM dbo.KG_HarmonogramPrzerw;
GO

PRINT '═══ L3-16. KG_TypyNieobecnosci ═══';
SELECT * FROM dbo.KG_TypyNieobecnosci;
GO

-- ─────────────────────────────────────────────────────────────────
-- 17. CenaTuszki — historia roczna (do wykresów trendu)
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ L3-17. CenaTuszki — wszystkie wartości w tym roku ═══';
SELECT Data, Cena
FROM dbo.CenaTuszki
WHERE Data >= '2026-01-01'
ORDER BY Data DESC;
GO

-- ─────────────────────────────────────────────────────────────────
-- 18. WstawieniaKurczakow — co tam jest
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ L3-18. WstawieniaKurczakow — kolumny + 5 najnowszych ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ
FROM sys.columns
WHERE object_id = OBJECT_ID('dbo.WstawieniaKurczakow')
ORDER BY column_id;
GO
SELECT TOP 5 * FROM dbo.WstawieniaKurczakow ORDER BY 1 DESC;
GO

-- ─────────────────────────────────────────────────────────────────
-- 19. Aktywnosc — czy to CRM activity log?
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ L3-19. Aktywnosc (185k wpisów!) — kolumny + 3 najnowsze ═══';
SELECT name, TYPE_NAME(user_type_id) AS Typ
FROM sys.columns
WHERE object_id = OBJECT_ID('dbo.Aktywnosc')
ORDER BY column_id;
GO
SELECT TOP 3 * FROM dbo.Aktywnosc ORDER BY 1 DESC;
GO

-- ─────────────────────────────────────────────────────────────────
-- 20. Article — pełna lista 36 SKU
-- ─────────────────────────────────────────────────────────────────
PRINT '═══ L3-20. Pełna lista 36 SKU w Article ═══';
SELECT ID, ShortName, Name, Cena1, JM, isStandard, StandardWeight, NameLine1, NameLine2
FROM dbo.Article
ORDER BY ID;
GO

PRINT '═══ KONIEC LibraNet faza 3 ═══';
