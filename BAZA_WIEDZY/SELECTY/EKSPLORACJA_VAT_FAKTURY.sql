-- ════════════════════════════════════════════════════════════════════
-- EKSPLORACJA VAT w fakturach Sage Symfonia (HANDEL 192.168.0.112)
-- Cel: znaleźć wszystkie kolumny związane z VAT, stawkami i wartościami
--      netto/brutto żeby Panel Fakturzystki pokazywał wartości BRUTTO.
-- Data utworzenia: 2026-05-12
-- ════════════════════════════════════════════════════════════════════

-- ─────────────────────────────────────────────────────────────────
-- 1. STRUKTURA HM.DP (pozycje dokumentów) — wszystkie kolumny VAT
-- ─────────────────────────────────────────────────────────────────
SELECT
    ORDINAL_POSITION AS pos,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH AS len,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'HM' AND TABLE_NAME = 'DP'
  AND (
       LOWER(COLUMN_NAME) LIKE '%vat%'
    OR LOWER(COLUMN_NAME) LIKE '%netto%'
    OR LOWER(COLUMN_NAME) LIKE '%brutto%'
    OR LOWER(COLUMN_NAME) LIKE '%cena%'
    OR LOWER(COLUMN_NAME) LIKE '%wart%'
    OR LOWER(COLUMN_NAME) IN ('stvat','wartstvat','rejestrvat','rejestrvat2','vat50')
  )
ORDER BY ORDINAL_POSITION;

-- KLUCZOWE KOLUMNY (znaleziono wcześniej w WYNIKI_HANDEL.txt):
--   stvat       int       — FK do słownika stawek VAT (HM.STAWKIVAT?)
--   wartstvat   smallint  — stawka VAT × 100 (np. 800 = 8%, 2300 = 23%, 500 = 5%)
--   cena        float     — cena jednostkowa NETTO
--   wartNetto   float     — wartość netto pozycji (ilosc × cena)
--   wartVat     float     — kwota VAT pozycji
--   wartTowaru  float     — wartość brutto (wartNetto + wartVat) [do weryfikacji]
--   walNetto    float     — wartość netto (w walucie / PLN)
--   walBrutto   float     — wartość BRUTTO (w walucie / PLN) ✅ TO JEST CEL
--   rejestrVAT  int       — rejestr VAT (sprzedaż/zakup)


-- ─────────────────────────────────────────────────────────────────
-- 2. STRUKTURA HM.DK (header dokumentu) — sumaryczne kwoty VAT/Brutto
-- ─────────────────────────────────────────────────────────────────
SELECT ORDINAL_POSITION AS pos, COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'HM' AND TABLE_NAME = 'DK'
  AND (
       LOWER(COLUMN_NAME) LIKE '%vat%'
    OR LOWER(COLUMN_NAME) LIKE '%netto%'
    OR LOWER(COLUMN_NAME) LIKE '%brutto%'
    OR LOWER(COLUMN_NAME) LIKE '%wal%'
  )
ORDER BY ORDINAL_POSITION;

-- KLUCZOWE KOLUMNY:
--   netto      float — suma netto wszystkich pozycji
--   vat        float — suma VAT wszystkich pozycji
--   walNetto   float — netto w walucie
--   walBrutto  float — BRUTTO całego dokumentu ✅


-- ─────────────────────────────────────────────────────────────────
-- 3. STAWKI VAT — czy istnieje słownik?
-- ─────────────────────────────────────────────────────────────────
SELECT TABLE_SCHEMA + '.' + TABLE_NAME AS Tabela
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE'
  AND (LOWER(TABLE_NAME) LIKE '%vat%' OR LOWER(TABLE_NAME) LIKE '%stawk%')
ORDER BY TABLE_SCHEMA, TABLE_NAME;

-- Sprawdź HM.STAWKIVAT lub podobną:
-- SELECT TOP 50 * FROM HM.STAWKIVAT ORDER BY id;
-- (jeśli istnieje, mapuje stvat → procent)


-- ─────────────────────────────────────────────────────────────────
-- 4. STRUKTURA HM.TW (towar) — stawka VAT sprzedażowa per towar
-- ─────────────────────────────────────────────────────────────────
SELECT ORDINAL_POSITION AS pos, COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'HM' AND TABLE_NAME = 'TW'
  AND (LOWER(COLUMN_NAME) LIKE '%vat%' OR LOWER(COLUMN_NAME) LIKE '%stawk%')
ORDER BY ORDINAL_POSITION;

-- KLUCZOWE: vatsp (int) — FK do stawki sprzedaży, vatzk (int) — stawka zakupu
-- (znajduje się w towarze, ale na pozycji DP używa się stvat/wartstvat
--  bo można na pozycji zmienić stawkę)


-- ─────────────────────────────────────────────────────────────────
-- 5. SAMPLE — przykładowa faktura z pozycjami + VAT, brutto
-- ─────────────────────────────────────────────────────────────────
-- Zmień @kod na konkretny numer faktury, np. '3191/26/FVS'
DECLARE @kod NVARCHAR(50) = N'3191/26/FVS';

-- Header faktury (kwoty sumaryczne)
SELECT
    dk.id AS DkId,
    dk.kod AS NumerFaktury,
    dk.data AS DataWystawienia,
    dk.khid AS KontrahentId,
    kh.Name AS KontrahentNazwa,
    dk.netto    AS SumaNetto,
    dk.vat      AS SumaVat,
    dk.netto + dk.vat AS SumaBruttoLiczona,
    dk.walNetto AS WalNetto,
    dk.walBrutto AS WalBrutto,
    dk.plattermin AS TerminPlatnosci,
    dk.ok AS Rozliczony,
    ISNULL(dk.anulowany, 0) AS Anulowany
FROM HM.DK dk
LEFT JOIN SSCommon.STContractors kh ON kh.Id = dk.khid
WHERE dk.kod = @kod;

-- Pozycje faktury (każda pozycja z VAT i brutto)
SELECT
    dp.lp,
    dp.idtw AS TowarId,
    tw.kod  AS TowarKod,
    tw.nazwa AS TowarNazwa,
    dp.ilosc,
    dp.jm,
    dp.cena       AS CenaNetto,
    dp.wartstvat  AS StawkaVatx100,            -- 800 = 8%, 2300 = 23%
    dp.wartstvat / 100.0 AS StawkaVatProc,
    dp.wartNetto  AS WartoscNetto,
    dp.wartVat    AS KwotaVat,
    dp.wartNetto + dp.wartVat AS WartoscBruttoLiczona,
    -- Cena brutto przeliczona: cena * (1 + stawka/10000)
    CAST(dp.cena * (1.0 + dp.wartstvat / 10000.0) AS DECIMAL(18,4)) AS CenaBruttoLiczona,
    dp.walNetto,
    dp.walBrutto  AS WalBrutto,                 -- ✅ kwota brutto w walucie
    dp.stvat      AS StawkaVatFk                -- FK do słownika (rzadko potrzebne)
FROM HM.DP dp
INNER JOIN HM.DK dk ON dk.id = dp.super
LEFT JOIN HM.TW tw ON tw.id = dp.idtw
WHERE dk.kod = @kod
ORDER BY dp.lp;


-- ─────────────────────────────────────────────────────────────────
-- 6. ROZKŁAD STAWEK VAT na fakturze (pivot wg stawki)
-- ─────────────────────────────────────────────────────────────────
SELECT
    dp.wartstvat / 100.0 AS StawkaVat,
    COUNT(*) AS LiczbaPozycji,
    SUM(dp.wartNetto) AS SumaNetto,
    SUM(dp.wartVat)   AS SumaVat,
    SUM(dp.wartNetto + dp.wartVat) AS SumaBrutto
FROM HM.DP dp
INNER JOIN HM.DK dk ON dk.id = dp.super
WHERE dk.kod = @kod
GROUP BY dp.wartstvat
ORDER BY dp.wartstvat;


-- ─────────────────────────────────────────────────────────────────
-- 7. WERYFIKACJA — czy suma pozycji = header faktury?
-- ─────────────────────────────────────────────────────────────────
SELECT
    'Header DK' AS Zrodlo,
    dk.netto AS Netto,
    dk.vat AS Vat,
    dk.walBrutto AS Brutto
FROM HM.DK dk
WHERE dk.kod = @kod
UNION ALL
SELECT
    'SUM DP'     AS Zrodlo,
    SUM(dp.wartNetto),
    SUM(dp.wartVat),
    SUM(dp.wartNetto + dp.wartVat)
FROM HM.DP dp
INNER JOIN HM.DK dk ON dk.id = dp.super
WHERE dk.kod = @kod;


-- ─────────────────────────────────────────────────────────────────
-- 8. STAWKI VAT używane w sprzedaży 2026 (statystyki dla branży drobiarskiej)
-- ─────────────────────────────────────────────────────────────────
SELECT
    dp.wartstvat / 100.0 AS StawkaVat,
    COUNT(DISTINCT dk.id) AS LiczbaFaktur,
    COUNT(*) AS LiczbaPozycji,
    SUM(dp.wartNetto) AS LacznaWartoscNetto
FROM HM.DP dp
INNER JOIN HM.DK dk ON dk.id = dp.super
WHERE dk.typ_dk IN ('FVS', 'FVR', 'FVZ')
  AND dk.aktywny = 1
  AND ISNULL(dk.anulowany, 0) = 0
  AND dk.data >= DATEADD(MONTH, -3, GETDATE())
GROUP BY dp.wartstvat
ORDER BY LiczbaPozycji DESC;
-- Typowo w branży drobiarskiej: 5% (mięso świeże), 8% (pasze), 23% (opakowania, usługi)
