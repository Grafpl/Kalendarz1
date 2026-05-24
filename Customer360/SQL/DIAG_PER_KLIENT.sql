/* ════════════════════════════════════════════════════════════════════════════
   DIAGNOSTYKA PER KLIENT — uruchom NA LibraNet (192.168.0.109)
   Cel: sprawdzić czy "tylko od 12/25" wynika z faktu że konkretny klient
        zaczął kupować w grudniu, czy faktycznie jest bug w aplikacji.

   PRZED uruchomieniem zmień @KlientId na ID klienta którego wybrałeś w UI.
   Jeśli nie wiesz — uruchom najpierw #A żeby zobaczyć TOP 30 i wybrać.
   ════════════════════════════════════════════════════════════════════════════ */

USE LibraNet;
SET NOCOUNT ON;
DECLARE @KlientId INT = 931;  -- ← WPISZ ID klienta (931=TOP klient, 308 zam/12M)


/* ────────────────────────────────────────────────────────────────────────────
   #A  TOP 30 KLIENTÓW WG LICZBY ZAMÓWIEŃ (24 mies) + min/max zamówień.
       Pozwoli wybrać klienta do dalszej analizy.
   ──────────────────────────────────────────────────────────────────────────── */
SELECT TOP 30
    z.KlientId,
    COUNT(*)                                              AS Zam_24M,
    SUM(CASE WHEN z.DataPrzyjazdu >= DATEADD(MONTH,-12,GETDATE()) THEN 1 ELSE 0 END) AS Zam_12M,
    SUM(CASE WHEN z.DataPrzyjazdu >= DATEADD(MONTH,-6, GETDATE()) THEN 1 ELSE 0 END) AS Zam_6M,
    MIN(z.DataPrzyjazdu)                                  AS Pierwsze_Zam,
    MAX(z.DataPrzyjazdu)                                  AS Ostatnie_Zam,
    DATEDIFF(DAY, MIN(z.DataPrzyjazdu), MAX(z.DataPrzyjazdu)) AS Dni_Aktywnosci,
    SUM(CASE WHEN ISNULL(z.Status,'') IN ('Anulowane','Anulowano') THEN 1 ELSE 0 END) AS Anulowane
FROM dbo.ZamowieniaMieso z
WHERE z.KlientId IS NOT NULL
  AND z.DataPrzyjazdu >= DATEADD(MONTH, -24, GETDATE())
GROUP BY z.KlientId
ORDER BY Zam_12M DESC;


/* ────────────────────────────────────────────────────────────────────────────
   #B  DLA KAŻDEGO KLIENTA — kiedy zaczął kupować?
       Pokazuje rozkład: ilu klientów pojawiło się pierwszy raz w którym miesiącu.
       Jeśli BARDZO DUŻO klientów miało "Pierwsze zamówienie" w 12/2025 →
       to znaczy że firma zaczęła obsługiwać nowych klientów masowo wtedy.
   ──────────────────────────────────────────────────────────────────────────── */
;WITH PierwszeZam AS (
    SELECT KlientId, MIN(DataPrzyjazdu) AS Pierwsze
    FROM dbo.ZamowieniaMieso
    WHERE KlientId IS NOT NULL AND DataPrzyjazdu IS NOT NULL
    GROUP BY KlientId
)
SELECT
    YEAR(Pierwsze) AS Rok, MONTH(Pierwsze) AS Mies,
    COUNT(*)       AS Nowych_Klientow_W_Mies
FROM PierwszeZam
GROUP BY YEAR(Pierwsze), MONTH(Pierwsze)
ORDER BY Rok DESC, Mies DESC;


/* ════════════════════════════════════════════════════════════════════════════
   PONIŻEJ — analiza KONKRETNEGO klienta. WPISZ @KlientId na górze!
   ════════════════════════════════════════════════════════════════════════════ */

IF @KlientId IS NULL
BEGIN
    PRINT '⚠ @KlientId jest NULL — wpisz ID klienta na górze skryptu i uruchom ponownie selekty #1-#6';
    RETURN;
END;


/* ────────────────────────────────────────────────────────────────────────────
   #1  CAŁA HISTORIA tego klienta — rozkład miesięczny.
       Pokaże WSZYSTKIE zamówienia (bez filtra dat) — od kiedy do kiedy.
   ──────────────────────────────────────────────────────────────────────────── */
SELECT
    YEAR(z.DataPrzyjazdu)  AS Rok,
    MONTH(z.DataPrzyjazdu) AS Mies,
    COUNT(*)               AS Zamowien,
    SUM(CASE WHEN ISNULL(z.Status,'') NOT IN ('Anulowane','Anulowano') THEN 1 ELSE 0 END) AS NieAnulowane,
    SUM(CASE WHEN ISNULL(z.Status,'') IN ('Anulowane','Anulowano') THEN 1 ELSE 0 END)     AS Anulowane,
    MIN(z.DataPrzyjazdu) AS Najwcz,
    MAX(z.DataPrzyjazdu) AS Najpoz
FROM dbo.ZamowieniaMieso z
WHERE z.KlientId = @KlientId
GROUP BY YEAR(z.DataPrzyjazdu), MONTH(z.DataPrzyjazdu)
ORDER BY Rok ASC, Mies ASC;


/* ────────────────────────────────────────────────────────────────────────────
   #2  WSZYSTKIE ZAMÓWIENIA klienta (bez filtra dat) — sample 50 najstarszych.
       Sprawdzimy czy faktycznie istnieją starsze niż 12/2025.
   ──────────────────────────────────────────────────────────────────────────── */
SELECT TOP 50
    z.Id,
    z.DataZamowienia,
    z.DataPrzyjazdu,
    z.DataUboju,
    z.DataWydania,
    z.Status,
    z.IdUser,
    z.NumerFaktury,
    z.NumerWZ,
    (SELECT COUNT(*) FROM dbo.ZamowieniaMiesoTowar zt WHERE zt.ZamowienieId = z.Id) AS Pozycji,
    (SELECT ISNULL(SUM(zt.Ilosc),0) FROM dbo.ZamowieniaMiesoTowar zt WHERE zt.ZamowienieId = z.Id) AS SumaKg
FROM dbo.ZamowieniaMieso z
WHERE z.KlientId = @KlientId
ORDER BY z.DataPrzyjazdu ASC;


/* ────────────────────────────────────────────────────────────────────────────
   #3  ZAMÓWIENIA KLIENTA W OKNIE -12 MIESIĘCY — dokładnie to co zobaczy C360.
       Jeśli to pokaże zamówienia z czerwca-listopada 2025, a UI ich nie ma →
       bug w aplikacji. Jeśli tylko od 12/2025 → klient zaczął wtedy kupować.
   ──────────────────────────────────────────────────────────────────────────── */
SELECT
    z.Id, z.DataZamowienia, z.DataPrzyjazdu, z.DataUboju, z.DataWydania,
    z.Status, z.IdUser
FROM dbo.ZamowieniaMieso z
WHERE z.KlientId = @KlientId
  AND z.DataPrzyjazdu >= DATEADD(MONTH, -12, GETDATE())
  AND ISNULL(z.Status,'') NOT IN ('Anulowane','Anulowano')
ORDER BY z.DataPrzyjazdu ASC;


/* ────────────────────────────────────────────────────────────────────────────
   #4  STATYSTYKI MIESIĘCZNE klienta (dokładnie jak w GetMonthlyStatsAsync).
   ──────────────────────────────────────────────────────────────────────────── */
SELECT
    YEAR(z.DataPrzyjazdu)  AS Rok,
    MONTH(z.DataPrzyjazdu) AS Mies,
    COUNT(DISTINCT z.Id)   AS Zamowien,
    ISNULL(SUM(zt.Ilosc), 0) AS SumaKg,
    ISNULL(SUM(zt.Ilosc * TRY_CAST(zt.Cena AS DECIMAL(18,2))), 0) AS Wartosc
FROM dbo.ZamowieniaMieso z
INNER JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = z.Id
WHERE z.KlientId = @KlientId
  AND z.DataPrzyjazdu >= DATEADD(MONTH, -12, GETDATE())
  AND ISNULL(z.Status,'') NOT IN ('Anulowane','Anulowano')
GROUP BY YEAR(z.DataPrzyjazdu), MONTH(z.DataPrzyjazdu)
ORDER BY Rok DESC, Mies DESC;


/* ────────────────────────────────────────────────────────────────────────────
   #5  TOP TOWARY klienta (jak w GetTopTowaryAsync).
   ──────────────────────────────────────────────────────────────────────────── */
SELECT TOP 5
    zt.KodTowaru,
    SUM(zt.Ilosc)                                                     AS SumaKg,
    SUM(zt.Ilosc * TRY_CAST(zt.Cena AS DECIMAL(18,2)))                AS Wartosc,
    COUNT(DISTINCT z.Id)                                              AS Zamowien
FROM dbo.ZamowieniaMieso z
INNER JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = z.Id
WHERE z.KlientId = @KlientId
  AND z.DataPrzyjazdu >= DATEADD(MONTH, -12, GETDATE())
  AND ISNULL(z.Status,'') NOT IN ('Anulowane','Anulowano')
GROUP BY zt.KodTowaru
ORDER BY SumaKg DESC;


/* ────────────────────────────────────────────────────────────────────────────
   #6  PORÓWNANIE: ile zamówień z każdego okna dat dla TEGO klienta.
       Pokaże dokładnie ile zamówień powinno być widać w C360.
   ──────────────────────────────────────────────────────────────────────────── */
SELECT
    COUNT(*)                                                                  AS WszystkieKiedykolwiek,
    SUM(CASE WHEN z.DataPrzyjazdu >= DATEADD(YEAR,-2, GETDATE()) THEN 1 ELSE 0 END) AS Ostatnie_24M,
    SUM(CASE WHEN z.DataPrzyjazdu >= DATEADD(MONTH,-12,GETDATE()) THEN 1 ELSE 0 END) AS Ostatnie_12M,
    SUM(CASE WHEN z.DataPrzyjazdu >= DATEADD(MONTH,-6, GETDATE()) THEN 1 ELSE 0 END) AS Ostatnie_6M,
    SUM(CASE WHEN z.DataPrzyjazdu >= DATEADD(MONTH,-3, GETDATE()) THEN 1 ELSE 0 END) AS Ostatnie_3M,
    SUM(CASE WHEN z.DataPrzyjazdu >= '2025-12-01' THEN 1 ELSE 0 END)                 AS Od_12_2025,
    SUM(CASE WHEN ISNULL(z.Status,'') IN ('Anulowane','Anulowano') THEN 1 ELSE 0 END) AS Anulowane,
    MIN(z.DataPrzyjazdu)                                                              AS Pierwsze,
    MAX(z.DataPrzyjazdu)                                                              AS Ostatnie
FROM dbo.ZamowieniaMieso z
WHERE z.KlientId = @KlientId;


/* ════════════════════════════════════════════════════════════════════════════
   INTERPRETACJA:
   - Jeśli #6.Ostatnie_12M >> #6.Od_12_2025  → BUG (UI nie pokazuje 6-11/2025).
   - Jeśli #6.Ostatnie_12M == #6.Od_12_2025  → KLIENT zaczął kupować dopiero
     w grudniu 2025 — to nie bug, tylko prawda biznesowa.
   - Jeśli #B pokazuje że MASA klientów ma "pierwsze zamówienie" w 12/2025 →
     możliwe że firma faktycznie ruszyła wtedy z większą bazą klientów.
   ════════════════════════════════════════════════════════════════════════════ */
