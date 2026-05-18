/* ============================================================================
   analiza_paulina_ZAKUP.sql — Analiza zakupu żywca (dziedzina Pauliny)
   ----------------------------------------------------------------------------
   Uruchom na: 192.168.0.109 / LibraNet (user pronova/pronova)

   KONTEKST:
   Paulina obsługuje dział zakupów (negocjacje z hodowcami żywca). Sergiusz
   planuje przesunąć Maję częściowo na zakup żywca — Maja musi:
     1. Utrzymać obecne ~25M obrotu sprzedażowego rocznie (29 klientów)
     2. Przejąć część operacji Pauliny: harmonogramy dostaw, ceny, hodowcy
     3. Rozwinąć portfel sprzedażowy

   Skrypt pokazuje WSZYSTKO co Paulina robi przez tabele LibraNet:
     • HarmonogramDostaw — plan zakupu żywca, kontrakty
     • Pozyskiwanie_Hodowcy + Aktywnosci — CRM hodowców (1874 leadów)
     • listapartii + PartiaDostawca — partie ubojowe od hodowców
     • FarmerCalc — rozliczenia finansowe z hodowcami
     • ReklamacjePartie — reklamacje na partie hodowców

   ============================================================================ */

USE [LibraNet];
GO

SET NOCOUNT ON;
SET ANSI_WARNINGS ON;

DECLARE @DataOd DATE = DATEADD(MONTH, -12, GETDATE());   -- 12 mies. wstecz
DECLARE @DataDo DATE = CAST(GETDATE() AS DATE);

/* ============================================================================
   ===  A. HARMONOGRAM DOSTAW ŻYWCA  ==========================================
   ============================================================================ */
SELECT N'A.1 — Skala harmonogramu dostaw żywca (12 mies.)' AS [Raport];

SELECT COUNT(*)                                                                                  AS LiczbaWierszy,
       SUM(CAST(CASE WHEN Bufor = 'Potwierdzony' THEN 1 ELSE 0 END AS INT))                       AS Potwierdzonych,
       SUM(CAST(CASE WHEN Bufor <> 'Potwierdzony' OR Bufor IS NULL THEN 1 ELSE 0 END AS INT))     AS Niepotwierdzonych,
       COUNT(DISTINCT DostawcaID)                                                                 AS UnikalnychDostawcow,
       CAST(SUM(CAST(SztukiDek AS DECIMAL(18,0))) AS DECIMAL(18,0))                               AS SztukDeklarowanych,
       CAST(SUM(CAST(SztukiDek AS DECIMAL(18,2)) * CAST(WagaDek AS DECIMAL(10,2))) AS DECIMAL(18,1)) AS KgDeklarowanych,
       CAST(SUM(CAST(PotwSztuki AS DECIMAL(18,0))) AS DECIMAL(18,0))                              AS SztukPotwierdzonych,
       CAST(SUM(CAST(PotwWaga AS DECIMAL(18,2))) AS DECIMAL(18,1))                                AS KgPotwierdzonych,
       MIN(DataOdbioru)                                                                           AS PierwszaDostawa,
       MAX(DataOdbioru)                                                                           AS OstatniaDostawa
FROM dbo.HarmonogramDostaw
WHERE DataOdbioru BETWEEN @DataOd AND @DataDo;

SELECT N'A.2 — Klasyfikacja TypCeny (kontrakt vs wolny rynek)' AS [Raport];

SELECT ISNULL(TypCeny, N'(brak)')                              AS TypCeny,
       CASE WHEN LOWER(ISNULL(TypCeny,N'')) IN (N'wolnyrynek',N'wolnorynkowa')
            THEN N'Wolny rynek' ELSE N'Kontrakt' END           AS Kategoria,
       COUNT(*)                                                AS LiczbaWierszy,
       COUNT(DISTINCT DostawcaID)                              AS UnikalnychDostawcow,
       CAST(SUM(SztukiDek * WagaDek) AS DECIMAL(18,1))         AS KgDeklarowanych,
       CAST(SUM(PotwWaga) AS DECIMAL(18,1))                    AS KgPotwierdzonych,
       CAST(AVG(CAST(Cena AS DECIMAL(10,4))) AS DECIMAL(10,2)) AS SredniaCena,
       CAST(MIN(Cena) AS DECIMAL(10,2))                        AS MinCena,
       CAST(MAX(Cena) AS DECIMAL(10,2))                        AS MaxCena
FROM dbo.HarmonogramDostaw
WHERE DataOdbioru BETWEEN @DataOd AND @DataDo
  AND Bufor = 'Potwierdzony'
GROUP BY ISNULL(TypCeny, N'(brak)'),
         CASE WHEN LOWER(ISNULL(TypCeny,N'')) IN (N'wolnyrynek',N'wolnorynkowa')
              THEN N'Wolny rynek' ELSE N'Kontrakt' END
ORDER BY KgPotwierdzonych DESC;

SELECT N'A.3 — Trend miesięczny: kontrakt vs wolny rynek (kg + cena)' AS [Raport];

SELECT CONVERT(CHAR(7), DataOdbioru, 120)                              AS RokMiesiac,
       CAST(SUM(CASE WHEN LOWER(ISNULL(TypCeny,N'')) IN (N'wolnyrynek',N'wolnorynkowa')
                     THEN PotwWaga ELSE 0 END) AS DECIMAL(18,1))        AS Wolny_Kg,
       CAST(SUM(CASE WHEN LOWER(ISNULL(TypCeny,N'')) NOT IN (N'wolnyrynek',N'wolnorynkowa')
                     THEN PotwWaga ELSE 0 END) AS DECIMAL(18,1))        AS Kontrakt_Kg,
       CAST(SUM(PotwWaga) AS DECIMAL(18,1))                             AS Razem_Kg,
       CAST(SUM(CASE WHEN LOWER(ISNULL(TypCeny,N'')) IN (N'wolnyrynek',N'wolnorynkowa')
                     THEN PotwWaga ELSE 0 END) * 100.0
            / NULLIF(SUM(PotwWaga), 0) AS DECIMAL(6,2))                 AS Wolny_Proc,
       -- średnia ważona cena per okres
       CAST(SUM(Cena * PotwWaga) / NULLIF(SUM(PotwWaga), 0) AS DECIMAL(10,2)) AS SredniaCenaWazona,
       CAST(SUM(Cena * SztukiDek) / NULLIF(SUM(SztukiDek), 0) AS DECIMAL(10,2)) AS SredniaCenaWgSztuk
FROM dbo.HarmonogramDostaw
WHERE DataOdbioru BETWEEN @DataOd AND @DataDo
  AND Bufor = 'Potwierdzony'
GROUP BY CONVERT(CHAR(7), DataOdbioru, 120)
ORDER BY RokMiesiac;

SELECT N'A.4 — TOP 30 dostawców żywca (12 mies., potwierdzone)' AS [Raport];

SELECT TOP 30
       DostawcaID,
       MAX(Dostawca)                                          AS Dostawca,
       COUNT(*)                                               AS LiczbaDostaw,
       SUM(Auta)                                              AS LacznieAut,
       CAST(SUM(SztukiDek) AS DECIMAL(18,0))                  AS SztukDeklar,
       CAST(SUM(PotwSztuki) AS DECIMAL(18,0))                 AS SztukPotw,
       CAST(SUM(SztukiDek * WagaDek) AS DECIMAL(18,1))        AS KgDeklar,
       CAST(SUM(PotwWaga) AS DECIMAL(18,1))                   AS KgPotw,
       CAST(SUM(Cena * PotwWaga) / NULLIF(SUM(PotwWaga), 0) AS DECIMAL(10,2)) AS SredniaCena,
       MIN(DataOdbioru) AS PierwszaDostawa,
       MAX(DataOdbioru) AS OstatniaDostawa,
       SUM(CASE WHEN LOWER(ISNULL(TypCeny,N'')) IN (N'wolnyrynek',N'wolnorynkowa') THEN 1 ELSE 0 END) AS DostawWolnyRynek,
       SUM(CASE WHEN LOWER(ISNULL(TypCeny,N'')) NOT IN (N'wolnyrynek',N'wolnorynkowa') THEN 1 ELSE 0 END) AS DostawKontrakt
FROM dbo.HarmonogramDostaw
WHERE DataOdbioru BETWEEN @DataOd AND @DataDo
  AND Bufor = 'Potwierdzony'
GROUP BY DostawcaID
ORDER BY KgPotw DESC;

SELECT N'A.5 — Kontrakty na przyszłość (potwierdzone, jeszcze nie dostarczone)' AS [Raport];

SELECT TOP 50
       DataOdbioru, DostawcaID, Dostawca, TypCeny, TypUmowy,
       CAST(SztukiDek AS DECIMAL(18,0)) AS SztukiDek,
       CAST(WagaDek AS DECIMAL(10,2)) AS WagaDekKg,
       CAST(SztukiDek * WagaDek AS DECIMAL(18,1)) AS KgPlanowanych,
       Auta, KmK, KmH, SztSzuflada,
       Cena, UWAGI,
       KtoStwo, DataUtw
FROM dbo.HarmonogramDostaw
WHERE Bufor = 'Potwierdzony'
  AND DataOdbioru >= CAST(GETDATE() AS DATE)
ORDER BY DataOdbioru;

/* ============================================================================
   ===  B. POZYSKIWANIE HODOWCÓW (CRM PAULINY)  ===============================
   ============================================================================ */
SELECT N'B.1 — Lejek hodowców: ile w każdym statusie' AS [Raport];

-- defensywnie: sprawdzam jakie kolumny ma Pozyskiwanie_Hodowcy
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Pozyskiwanie_Hodowcy' AND COLUMN_NAME='Status')
BEGIN
    SELECT Status,
           COUNT(*) AS Liczba
    FROM dbo.Pozyskiwanie_Hodowcy
    GROUP BY Status
    ORDER BY Liczba DESC;
END
ELSE
    SELECT N'⚠ Pozyskiwanie_Hodowcy nie ma kolumny Status — sprawdź eksploracja_LIBRANET sekcja 8' AS Info;

SELECT N'B.2 — Aktywności CRM ostatnio (12 mies.) — Paulina vs inni' AS [Raport];

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Pozyskiwanie_Aktywnosci')
BEGIN
    DECLARE @hasAutor BIT = (CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                                                WHERE TABLE_NAME='Pozyskiwanie_Aktywnosci' AND COLUMN_NAME='Autor')
                                  THEN 1 ELSE 0 END);
    IF @hasAutor = 1
    BEGIN
        DECLARE @sqlB2 NVARCHAR(MAX) = N'
            SELECT Autor, COUNT(*) AS LiczbaAkcji,
                   MIN(DataAkcji) AS Pierwsza, MAX(DataAkcji) AS Ostatnia
            FROM dbo.Pozyskiwanie_Aktywnosci
            WHERE DataAkcji >= @DataOd
            GROUP BY Autor ORDER BY LiczbaAkcji DESC;';
        BEGIN TRY
            EXEC sp_executesql @sqlB2, N'@DataOd DATE', @DataOd = @DataOd;
        END TRY
        BEGIN CATCH SELECT N'⚠ Błąd B.2: ' + ERROR_MESSAGE() AS Info; END CATCH;
    END
    ELSE
    BEGIN
        SELECT N'⚠ Pozyskiwanie_Aktywnosci nie ma kolumny Autor — pokazuję strukturę:' AS Info;
        SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME = 'Pozyskiwanie_Aktywnosci' ORDER BY ORDINAL_POSITION;
    END
END;

/* ============================================================================
   ===  C. PARTIE UBOJOWE — DIAGNOSTYKA (faktyczne kolumny listapartii)
   ============================================================================ */
SELECT N'C.0 — DIAGNOSTYKA: kolumny tabeli listapartii (uzupełnimy raporty po wyniku)' AS [Raport];

SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS Dlugosc, IS_NULLABLE, ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'listapartii'
ORDER BY ORDINAL_POSITION;

SELECT N'C.1 — Sample TOP 5 z listapartii (zobacz jakie są nazwy kolumn)' AS [Raport];
SELECT TOP 5 * FROM dbo.listapartii ORDER BY 1 DESC;

SELECT N'C.2 — Tabela PartiaDostawca — sample 5 wierszy' AS [Raport];
SELECT TOP 5 * FROM dbo.PartiaDostawca ORDER BY 1 DESC;

SELECT N'C.3 — Reklamacje na partie od dostawców (ReklamacjePartie)' AS [Raport];

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='ReklamacjePartie')
BEGIN
    SELECT rp.CustomerID, rp.CustomerName,
           COUNT(*) AS LiczbaReklamacji,
           COUNT(DISTINCT rp.NumerPartii) AS PartiiZReklamacja,
           MIN(rp.DataDodania) AS Pierwsza,
           MAX(rp.DataDodania) AS Ostatnia
    FROM dbo.ReklamacjePartie rp
    WHERE rp.DataDodania >= @DataOd
    GROUP BY rp.CustomerID, rp.CustomerName
    ORDER BY LiczbaReklamacji DESC;
END
ELSE
    SELECT N'⚠ ReklamacjePartie nie istnieje' AS Info;

/* ============================================================================
   ===  D. ROZLICZENIA FINANSOWE Z HODOWCAMI (FarmerCalc)  ====================
   ============================================================================ */
SELECT N'D.1 — Struktura kolumn FarmerCalc' AS [Raport];
SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'FarmerCalc' ORDER BY ORDINAL_POSITION;

SELECT N'D.2 — Sample 10 ostatnich wpisów FarmerCalc' AS [Raport];
SELECT TOP 10 * FROM dbo.FarmerCalc ORDER BY 1 DESC;

/* ============================================================================
   ===  E. CYKL DOSTAW (dni tygodnia, sezonowość)  =============================
   ============================================================================ */
SELECT N'E.1 — Dni tygodnia: kiedy hodowcy dostarczają (1=Nd, 2=Pn, ..., 7=Sb)' AS [Raport];

SELECT DATEPART(WEEKDAY, DataOdbioru) AS DzienTyg,
       CASE DATEPART(WEEKDAY, DataOdbioru)
            WHEN 1 THEN N'Niedziela' WHEN 2 THEN N'Poniedziałek' WHEN 3 THEN N'Wtorek'
            WHEN 4 THEN N'Środa'     WHEN 5 THEN N'Czwartek'     WHEN 6 THEN N'Piątek'
            WHEN 7 THEN N'Sobota' END AS Dzien,
       COUNT(*) AS LiczbaDostaw,
       CAST(SUM(PotwWaga) AS DECIMAL(18,1)) AS KgRazem,
       SUM(Auta) AS LacznieAut
FROM dbo.HarmonogramDostaw
WHERE DataOdbioru BETWEEN @DataOd AND @DataDo
  AND Bufor = 'Potwierdzony'
GROUP BY DATEPART(WEEKDAY, DataOdbioru)
ORDER BY DzienTyg;

SELECT N'E.2 — Miesiące roku: sezonowość zakupu żywca' AS [Raport];

SELECT MONTH(DataOdbioru) AS Miesiac,
       COUNT(*) AS LiczbaDostaw,
       CAST(SUM(PotwWaga) AS DECIMAL(18,1)) AS KgRazem,
       CAST(SUM(Cena * PotwWaga) / NULLIF(SUM(PotwWaga), 0) AS DECIMAL(10,2)) AS SredCenaWaz
FROM dbo.HarmonogramDostaw
WHERE Bufor = 'Potwierdzony'
GROUP BY MONTH(DataOdbioru)
ORDER BY Miesiac;

/* ============================================================================
   ===  F. ROZRZUT CEN ŻYWCA (zmienność rynku)  ===============================
   ============================================================================ */
SELECT N'F.1 — Histogram cen żywca (kontrakt 4.40-5.23, wolny rynek ~4.00)' AS [Raport];

SELECT
    CASE
        WHEN Cena < 3.50 THEN N'01 <3.50'
        WHEN Cena < 4.00 THEN N'02 3.50-4.00'
        WHEN Cena < 4.20 THEN N'03 4.00-4.20'
        WHEN Cena < 4.40 THEN N'04 4.20-4.40'
        WHEN Cena < 4.60 THEN N'05 4.40-4.60'
        WHEN Cena < 4.80 THEN N'06 4.60-4.80'
        WHEN Cena < 5.00 THEN N'07 4.80-5.00'
        WHEN Cena < 5.20 THEN N'08 5.00-5.20'
        WHEN Cena < 5.50 THEN N'09 5.20-5.50'
        ELSE                  N'10 5.50+'
    END AS Bucket_Cena,
    COUNT(*) AS LiczbaDostaw,
    CAST(SUM(PotwWaga) AS DECIMAL(18,1)) AS KgRazem,
    COUNT(DISTINCT DostawcaID) AS DostawcowUnikalnych
FROM dbo.HarmonogramDostaw
WHERE Bufor = 'Potwierdzony'
  AND DataOdbioru BETWEEN @DataOd AND @DataDo
  AND Cena IS NOT NULL AND Cena > 0
GROUP BY CASE
        WHEN Cena < 3.50 THEN N'01 <3.50'
        WHEN Cena < 4.00 THEN N'02 3.50-4.00'
        WHEN Cena < 4.20 THEN N'03 4.00-4.20'
        WHEN Cena < 4.40 THEN N'04 4.20-4.40'
        WHEN Cena < 4.60 THEN N'05 4.40-4.60'
        WHEN Cena < 4.80 THEN N'06 4.60-4.80'
        WHEN Cena < 5.00 THEN N'07 4.80-5.00'
        WHEN Cena < 5.20 THEN N'08 5.00-5.20'
        WHEN Cena < 5.50 THEN N'09 5.20-5.50'
        ELSE                  N'10 5.50+' END
ORDER BY Bucket_Cena;

/* ============================================================================
   ===  G. ANALIZA PORÓWNAWCZA: ile pracy ma Paulina  =========================
   ============================================================================ */
SELECT N'G.1 — Skala działania Pauliny: ile dostaw / hodowców rocznie' AS [Raport];

SELECT
    CAST(SUM(PotwWaga) / 1000 AS DECIMAL(18,1))                          AS Ton_Zywca_Rocznie,
    COUNT(*)                                                              AS Dostaw_Rocznie,
    COUNT(DISTINCT DostawcaID)                                            AS Unikalnych_Hodowcow,
    CAST(SUM(Cena * PotwWaga) AS DECIMAL(18,0))                          AS Wartosc_Zakupu_PLN,
    CAST(SUM(Cena * PotwWaga) / 1000000.0 AS DECIMAL(8,1))                AS Wartosc_Zakupu_M_PLN,
    CAST(SUM(CASE WHEN LOWER(ISNULL(TypCeny,N'')) IN (N'wolnyrynek',N'wolnorynkowa')
                  THEN PotwWaga ELSE 0 END) * 100.0
         / NULLIF(SUM(PotwWaga), 0) AS DECIMAL(6,2))                     AS Wolny_Rynek_Proc,
    COUNT(*) / 52                                                         AS Sredn_Dostaw_Tygodniowo,
    COUNT(*) / 12                                                         AS Sredn_Dostaw_Miesiecznie
FROM dbo.HarmonogramDostaw
WHERE Bufor = 'Potwierdzony'
  AND DataOdbioru BETWEEN @DataOd AND @DataDo;

SELECT N'G.2 — Aktywność per autor (kto wprowadza harmonogramy = czyja to praca)' AS [Raport];

SELECT ISNULL(KtoStwo, N'(brak)') AS KtoWprowadzil,
       COUNT(*) AS LiczbaWpisow,
       SUM(CAST(CASE WHEN Bufor = 'Potwierdzony' THEN 1 ELSE 0 END AS INT)) AS Potwierdzonych,
       MIN(DataUtw) AS Pierwszy,
       MAX(DataUtw) AS Ostatni
FROM dbo.HarmonogramDostaw
WHERE DataUtw >= @DataOd
GROUP BY ISNULL(KtoStwo, N'(brak)')
ORDER BY LiczbaWpisow DESC;

SELECT N'G.3 — Zakończono analizę zakupu żywca (Paulina)' AS Info;
