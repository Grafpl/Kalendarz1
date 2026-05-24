/* ════════════════════════════════════════════════════════════════════════════
   DIAGNOSTYKA FAKTUR HANDEL — uruchom NA 192.168.0.112 (Sage Symfonia)
   Cel: sprawdzić czy zakładka "Faktury" / "Weryfikacja" pokazuje tylko od 12/25
        z powodu danych w bazie HANDEL.

   PRZED uruchomieniem: zmień @KlientId na khid klienta z HANDEL
   (= ten sam ID który był wybrany w UI Customer 360).
   ════════════════════════════════════════════════════════════════════════════ */

USE HANDEL;
SET NOCOUNT ON;
DECLARE @KlientId INT = NULL;  -- ← WPISZ khid klienta (zgodny z LibraNet.ZamowieniaMieso.KlientId)


/* ────────────────────────────────────────────────────────────────────────────
   #1  CHRONOLOGIA WSZYSTKICH FAKTUR (24 mies) — czy HANDEL ma starsze niż 12/2025?
       Globalnie, bez filtra klienta.
   ──────────────────────────────────────────────────────────────────────────── */
SELECT
    YEAR(DK.data) AS Rok, MONTH(DK.data) AS Mies,
    COUNT(*)                                                                  AS Faktur_Razem,
    SUM(CASE WHEN DK.typ_dk IN (N'FVS', N'FS', N'FVK', N'KFS') THEN 1 ELSE 0 END) AS Sprzedaz,
    SUM(CASE WHEN DK.typ_dk IN (N'WZ', N'WZ-F', N'DWZ')        THEN 1 ELSE 0 END) AS WZ_etki,
    SUM(CASE WHEN DK.typ_dk IN (N'PA', N'PAS')                 THEN 1 ELSE 0 END) AS Paragony,
    MIN(DK.data) AS Pierwsza, MAX(DK.data) AS Ostatnia
FROM HM.DK DK
WHERE DK.anulowany = 0
  AND DK.data >= DATEADD(MONTH, -24, GETDATE())
GROUP BY YEAR(DK.data), MONTH(DK.data)
ORDER BY Rok DESC, Mies DESC;


/* ────────────────────────────────────────────────────────────────────────────
   #2  ROZKŁAD typ_dk × miesiąc (24 mies) — może niektóre typy są tylko nowsze?
   ──────────────────────────────────────────────────────────────────────────── */
SELECT
    DK.typ_dk,
    YEAR(DK.data)  AS Rok, MONTH(DK.data) AS Mies,
    COUNT(*)       AS Faktur
FROM HM.DK DK
WHERE DK.anulowany = 0
  AND DK.data >= DATEADD(MONTH, -24, GETDATE())
GROUP BY DK.typ_dk, YEAR(DK.data), MONTH(DK.data)
ORDER BY DK.typ_dk, Rok DESC, Mies DESC;


/* ────────────────────────────────────────────────────────────────────────────
   #3  ABSOLUTNE MIN/MAX dat faktur w HANDEL — najdalsza wstecz.
   ──────────────────────────────────────────────────────────────────────────── */
SELECT
    COUNT(*)                                                                  AS Wszystkie_Faktury,
    MIN(DK.data)                                                              AS Najstarsza,
    MAX(DK.data)                                                              AS Najmlodsza,
    COUNT(DISTINCT YEAR(DK.data))                                             AS Liczba_Lat,
    COUNT(DISTINCT DK.khid)                                                   AS Liczba_Klientow,
    COUNT(DISTINCT DK.typ_dk)                                                 AS Liczba_TypowDk
FROM HM.DK DK
WHERE DK.anulowany = 0;


/* ════════════════════════════════════════════════════════════════════════════
   PONIŻEJ — dla konkretnego klienta. WPISZ @KlientId na górze!
   ════════════════════════════════════════════════════════════════════════════ */

IF @KlientId IS NULL
BEGIN
    PRINT '⚠ @KlientId NULL — uruchom tylko #1-#3, lub wpisz khid na górze';
    RETURN;
END;


/* ────────────────────────────────────────────────────────────────────────────
   #4  FAKTURY TEGO KLIENTA — pełna chronologia (bez filtra dat).
   ──────────────────────────────────────────────────────────────────────────── */
SELECT
    YEAR(DK.data)  AS Rok, MONTH(DK.data) AS Mies,
    COUNT(*) AS Faktur,
    SUM(CASE WHEN DK.typ_dk IN (N'FVS', N'FS', N'FVK', N'KFS') THEN 1 ELSE 0 END) AS Sprzedaz,
    SUM(CASE WHEN DK.typ_dk IN (N'WZ', N'WZ-F', N'DWZ')        THEN 1 ELSE 0 END) AS WZ_etki,
    MIN(DK.data) AS Najwcz, MAX(DK.data) AS Najpoz
FROM HM.DK DK
WHERE DK.khid = @KlientId
  AND DK.anulowany = 0
GROUP BY YEAR(DK.data), MONTH(DK.data)
ORDER BY Rok ASC, Mies ASC;


/* ────────────────────────────────────────────────────────────────────────────
   #5  PORÓWNANIE OKIEN DAT dla TEGO klienta.
   ──────────────────────────────────────────────────────────────────────────── */
SELECT
    COUNT(*)                                                                                                  AS Wszystkie,
    SUM(CASE WHEN DK.data >= DATEADD(MONTH,-12,GETDATE())                                  THEN 1 ELSE 0 END) AS Ostatnie_12M,
    SUM(CASE WHEN DK.data >= DATEADD(MONTH,-6, GETDATE())                                  THEN 1 ELSE 0 END) AS Ostatnie_6M,
    SUM(CASE WHEN DK.data >= '2025-12-01'                                                  THEN 1 ELSE 0 END) AS Od_12_2025,
    SUM(CASE WHEN DK.typ_dk IN (N'FVS', N'FS', N'FVK', N'KFS', N'WZ', N'WZ-F', N'DWZ', N'PA', N'PAS') THEN 1 ELSE 0 END) AS Typy_C360,
    MIN(DK.data) AS Pierwsza, MAX(DK.data) AS Ostatnia
FROM HM.DK DK
WHERE DK.khid = @KlientId
  AND DK.anulowany = 0;


/* ────────────────────────────────────────────────────────────────────────────
   #6  TOP 50 FAKTUR tego klienta (sample od najstarszej).
   ──────────────────────────────────────────────────────────────────────────── */
SELECT TOP 50
    DK.id,
    DK.typ_dk,
    DK.kod,
    DK.data,
    DK.khid,
    DK.anulowany,
    (SELECT COUNT(*) FROM HM.DP DP WHERE DP.super = DK.id)                    AS Pozycji,
    (SELECT ISNULL(SUM(ABS(DP.ilosc)),0) FROM HM.DP DP WHERE DP.super = DK.id) AS SumaKg
FROM HM.DK DK
WHERE DK.khid = @KlientId
  AND DK.anulowany = 0
  AND DK.typ_dk IN (N'FVS', N'FS', N'FVK', N'KFS', N'WZ', N'WZ-F', N'DWZ', N'PA', N'PAS')
ORDER BY DK.data ASC;


/* ────────────────────────────────────────────────────────────────────────────
   #7  CZY HANDEL ZNA TEGO KLIENTA — STContractors snapshot.
   ──────────────────────────────────────────────────────────────────────────── */
SELECT
    C.Id, C.Shortcut, C.Name, C.NIP
FROM SSCommon.STContractors C
WHERE C.Id = @KlientId;


/* ════════════════════════════════════════════════════════════════════════════
   INTERPRETACJA:
   - #1 pokaże czy HANDEL globalnie ma starsze faktury niż 12/2025.
     Jeśli faktury są dopiero od 12/2025 → migracja Sage w grudniu (cold start).
   - #2 wykryje czy konkretny typ dokumentu został wprowadzony dopiero teraz.
   - #4-#5 dla wybranego klienta odpowiada na pytanie: czy klient ma starsze
     faktury w HANDEL? Jeśli nie — albo nie kupował, albo migracja.
   - #6 sample do verification.
   - #7 — jeśli C nie znajdzie klienta → ID się nie matchuje (LibraNet vs HANDEL).
   ════════════════════════════════════════════════════════════════════════════ */
