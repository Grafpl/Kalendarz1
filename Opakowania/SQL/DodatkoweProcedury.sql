-- ============================================================
-- DODATKOWE PROCEDURY ZARZĄDZANIA OPAKOWANIAMI
-- Serwer: 192.168.0.109
-- Baza: LibraNet
-- ============================================================

USE [LibraNet]
GO

-- ============================================================
-- PROCEDURA: sp_PobierzSaldaDoWykresu
-- Pobiera dane sald dla wykresu (dla konkretnego kontrahenta i typu opakowania)
-- ============================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_PobierzSaldaDoWykresu')
    DROP PROCEDURE sp_PobierzSaldaDoWykresu
GO

CREATE PROCEDURE [dbo].[sp_PobierzSaldaDoWykresu]
    @KontrahentId INT,
    @KodOpakowania NVARCHAR(20),
    @IloscDni INT = 30
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @DataOd DATE = DATEADD(DAY, -@IloscDni, GETDATE())
    
    SELECT 
        Data,
        CASE @KodOpakowania
            WHEN 'E2' THEN SaldoE2
            WHEN 'H1' THEN SaldoH1
            WHEN 'EURO' THEN SaldoEURO
            WHEN 'PCV' THEN SaldoPCV
            WHEN 'DREW' THEN SaldoDREW
            ELSE 0
        END AS Saldo
    FROM HistoriaSaldOpakowan
    WHERE KontrahentId = @KontrahentId
      AND Data >= @DataOd
    ORDER BY Data ASC
END
GO

PRINT 'Procedura sp_PobierzSaldaDoWykresu utworzona.'
GO

-- ============================================================
-- PROCEDURA: sp_PobierzStatystykiOpakowan
-- Pobiera statystyki zbiorcze dla dashboardu
-- ============================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_PobierzStatystykiOpakowan')
    DROP PROCEDURE sp_PobierzStatystykiOpakowan
GO

CREATE PROCEDURE [dbo].[sp_PobierzStatystykiOpakowan]
    @Handlowiec NVARCHAR(100) = NULL  -- NULL = wszyscy
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Utworzenie połączenia do bazy Handel
    DECLARE @sql NVARCHAR(MAX)
    
    -- Tabela tymczasowa na wyniki
    CREATE TABLE #Statystyki (
        TypOpakowania NVARCHAR(20),
        LiczbaKontrahentow INT,
        SumaDodatnia INT,
        SumaUjemna INT,
        LiczbaPotwierdzen INT
    )
    
    -- Pobierz dane z linkowanego serwera
    INSERT INTO #Statystyki (TypOpakowania, LiczbaKontrahentow, SumaDodatnia, SumaUjemna, LiczbaPotwierdzen)
    SELECT 
        'WSZYSTKIE' AS TypOpakowania,
        COUNT(DISTINCT v.KontrahentId) AS LiczbaKontrahentow,
        ISNULL(SUM(CASE WHEN v.SaldoE2 + v.SaldoH1 + v.SaldoEURO + v.SaldoPCV + v.SaldoDREW > 0 
            THEN v.SaldoE2 + v.SaldoH1 + v.SaldoEURO + v.SaldoPCV + v.SaldoDREW ELSE 0 END), 0) AS SumaDodatnia,
        ISNULL(SUM(CASE WHEN v.SaldoE2 + v.SaldoH1 + v.SaldoEURO + v.SaldoPCV + v.SaldoDREW < 0 
            THEN v.SaldoE2 + v.SaldoH1 + v.SaldoEURO + v.SaldoPCV + v.SaldoDREW ELSE 0 END), 0) AS SumaUjemna,
        (SELECT COUNT(*) FROM PotwierdzeniaSaldaOpakowan WHERE StatusPotwierdzenia = 'Potwierdzone' 
            AND DataPotwierdzenia >= DATEADD(DAY, -30, GETDATE())) AS LiczbaPotwierdzen
    FROM HistoriaSaldOpakowan v
    WHERE v.Data = (SELECT MAX(Data) FROM HistoriaSaldOpakowan)
      AND (@Handlowiec IS NULL OR 1=1) -- TODO: dodać filtr handlowca
    
    SELECT * FROM #Statystyki
    
    DROP TABLE #Statystyki
END
GO

PRINT 'Procedura sp_PobierzStatystykiOpakowan utworzona.'
GO

-- ============================================================
-- PROCEDURA: sp_CzyscStarePotwierdzenia
-- Archiwizuje/usuwa stare potwierdzenia (starsze niż X dni)
-- ============================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_CzyscStarePotwierdzenia')
    DROP PROCEDURE sp_CzyscStarePotwierdzenia
GO

CREATE PROCEDURE [dbo].[sp_CzyscStarePotwierdzenia]
    @DniDoZachowania INT = 365  -- Domyślnie zachowaj 1 rok
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @DataGraniczna DATE = DATEADD(DAY, -@DniDoZachowania, GETDATE())
    DECLARE @Usunieto INT
    
    -- Usuń stare anulowane potwierdzenia
    DELETE FROM PotwierdzeniaSaldaOpakowan
    WHERE StatusPotwierdzenia = 'Anulowane'
      AND DataWprowadzenia < @DataGraniczna
    
    SET @Usunieto = @@ROWCOUNT
    
    PRINT 'Usunięto ' + CAST(@Usunieto AS VARCHAR(10)) + ' anulowanych potwierdzeń starszych niż ' + 
          CONVERT(VARCHAR(10), @DataGraniczna, 120)
    
    -- Usuń starą historię sald (zachowaj tylko dane z ostatniego roku)
    DELETE FROM HistoriaSaldOpakowan
    WHERE Data < @DataGraniczna
    
    SET @Usunieto = @@ROWCOUNT
    
    PRINT 'Usunięto ' + CAST(@Usunieto AS VARCHAR(10)) + ' rekordów historii starszych niż ' + 
          CONVERT(VARCHAR(10), @DataGraniczna, 120)
END
GO

PRINT 'Procedura sp_CzyscStarePotwierdzenia utworzona.'
GO

-- ============================================================
-- PROCEDURA: sp_PobierzOstatniaAktywnosc
-- Pobiera ostatnią aktywność dla kontrahentów (do alertów)
-- ============================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_PobierzOstatniaAktywnosc')
    DROP PROCEDURE sp_PobierzOstatniaAktywnosc
GO

CREATE PROCEDURE [dbo].[sp_PobierzOstatniaAktywnosc]
    @DniBezAktywnosci INT = 90  -- Kontrahenci bez dokumentów dłużej niż X dni
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        h.KontrahentId,
        h.KontrahentShortcut,
        MAX(h.Data) AS OstatniaDana,
        h.SaldoE2 + h.SaldoH1 + h.SaldoEURO + h.SaldoPCV + h.SaldoDREW AS SaldoCalkowite,
        DATEDIFF(DAY, MAX(h.Data), GETDATE()) AS DniBezAktywnosci
    FROM HistoriaSaldOpakowan h
    GROUP BY h.KontrahentId, h.KontrahentShortcut, h.SaldoE2, h.SaldoH1, h.SaldoEURO, h.SaldoPCV, h.SaldoDREW
    HAVING DATEDIFF(DAY, MAX(h.Data), GETDATE()) > @DniBezAktywnosci
       AND (h.SaldoE2 + h.SaldoH1 + h.SaldoEURO + h.SaldoPCV + h.SaldoDREW) <> 0
    ORDER BY SaldoCalkowite DESC
END
GO

PRINT 'Procedura sp_PobierzOstatniaAktywnosc utworzona.'
GO

-- ============================================================
-- PROCEDURA: sp_WyszukajPodobneRozbieznosci
-- Szuka wzorców w rozbieżnościach (analiza)
-- ============================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_WyszukajPodobneRozbieznosci')
    DROP PROCEDURE sp_WyszukajPodobneRozbieznosci
GO

CREATE PROCEDURE [dbo].[sp_WyszukajPodobneRozbieznosci]
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Kontrahenci z częstymi rozbieżnościami
    SELECT 
        KontrahentId,
        KontrahentNazwa,
        KodOpakowania,
        COUNT(*) AS LiczbaRozbieznosci,
        AVG(Roznica) AS SredniaRoznica,
        MIN(Roznica) AS MinRoznica,
        MAX(Roznica) AS MaxRoznica,
        MIN(DataPotwierdzenia) AS PierwszaRozbieznosc,
        MAX(DataPotwierdzenia) AS OstatniaRozbieznosc
    FROM PotwierdzeniaSaldaOpakowan
    WHERE StatusPotwierdzenia = 'Rozbieżność'
    GROUP BY KontrahentId, KontrahentNazwa, KodOpakowania
    HAVING COUNT(*) >= 2  -- Co najmniej 2 rozbieżności
    ORDER BY COUNT(*) DESC, AVG(ABS(Roznica)) DESC
END
GO

PRINT 'Procedura sp_WyszukajPodobneRozbieznosci utworzona.'
GO

-- ============================================================
-- WIDOK: vw_PodsumowanieSaldOpakowan
-- Podsumowanie sald dla głównego dashboardu
-- ============================================================
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_PodsumowanieSaldOpakowan')
    DROP VIEW vw_PodsumowanieSaldOpakowan
GO

CREATE VIEW [dbo].[vw_PodsumowanieSaldOpakowan]
AS
SELECT 
    -- E2
    SUM(CASE WHEN SaldoE2 > 0 THEN SaldoE2 ELSE 0 END) AS E2_WinniNam,
    SUM(CASE WHEN SaldoE2 < 0 THEN SaldoE2 ELSE 0 END) AS E2_MyWinni,
    SUM(SaldoE2) AS E2_Bilans,
    COUNT(CASE WHEN SaldoE2 <> 0 THEN 1 END) AS E2_Kontrahentow,
    
    -- H1
    SUM(CASE WHEN SaldoH1 > 0 THEN SaldoH1 ELSE 0 END) AS H1_WinniNam,
    SUM(CASE WHEN SaldoH1 < 0 THEN SaldoH1 ELSE 0 END) AS H1_MyWinni,
    SUM(SaldoH1) AS H1_Bilans,
    COUNT(CASE WHEN SaldoH1 <> 0 THEN 1 END) AS H1_Kontrahentow,
    
    -- EURO
    SUM(CASE WHEN SaldoEURO > 0 THEN SaldoEURO ELSE 0 END) AS EURO_WinniNam,
    SUM(CASE WHEN SaldoEURO < 0 THEN SaldoEURO ELSE 0 END) AS EURO_MyWinni,
    SUM(SaldoEURO) AS EURO_Bilans,
    COUNT(CASE WHEN SaldoEURO <> 0 THEN 1 END) AS EURO_Kontrahentow,
    
    -- PCV
    SUM(CASE WHEN SaldoPCV > 0 THEN SaldoPCV ELSE 0 END) AS PCV_WinniNam,
    SUM(CASE WHEN SaldoPCV < 0 THEN SaldoPCV ELSE 0 END) AS PCV_MyWinni,
    SUM(SaldoPCV) AS PCV_Bilans,
    COUNT(CASE WHEN SaldoPCV <> 0 THEN 1 END) AS PCV_Kontrahentow,
    
    -- DREW
    SUM(CASE WHEN SaldoDREW > 0 THEN SaldoDREW ELSE 0 END) AS DREW_WinniNam,
    SUM(CASE WHEN SaldoDREW < 0 THEN SaldoDREW ELSE 0 END) AS DREW_MyWinni,
    SUM(SaldoDREW) AS DREW_Bilans,
    COUNT(CASE WHEN SaldoDREW <> 0 THEN 1 END) AS DREW_Kontrahentow,
    
    -- Ogółem
    COUNT(DISTINCT KontrahentId) AS OgolemKontrahentow,
    MAX(Data) AS DataAktualizacji
FROM HistoriaSaldOpakowan
WHERE Data = (SELECT MAX(Data) FROM HistoriaSaldOpakowan)
GO

PRINT 'Widok vw_PodsumowanieSaldOpakowan utworzony.'
GO

-- ============================================================
-- TRIGGER: trg_PotwierdzeniaSalda_Audit
-- Automatyczne ustawienie daty modyfikacji
-- ============================================================
IF EXISTS (SELECT * FROM sys.triggers WHERE name = 'trg_PotwierdzeniaSalda_Audit')
    DROP TRIGGER trg_PotwierdzeniaSalda_Audit
GO

CREATE TRIGGER [dbo].[trg_PotwierdzeniaSalda_Audit]
ON [dbo].[PotwierdzeniaSaldaOpakowan]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    
    UPDATE p
    SET DataModyfikacji = GETDATE()
    FROM PotwierdzeniaSaldaOpakowan p
    INNER JOIN inserted i ON p.Id = i.Id
END
GO

PRINT 'Trigger trg_PotwierdzeniaSalda_Audit utworzony.'
GO

-- ============================================================
-- DANE TESTOWE - MapowanieHandlowcow (jeśli puste)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM MapowanieHandlowcow)
BEGIN
    INSERT INTO MapowanieHandlowcow (UserId, HandlowiecNazwa, CzyAktywny) VALUES
    ('11111', 'Administrator', 1),
    ('12345', 'Jan Kowalski', 1),
    ('12346', 'Anna Nowak', 1),
    ('12347', 'Piotr Wiśniewski', 1),
    ('12348', 'Maria Dąbrowska', 1)
    
    PRINT 'Dodano przykładowe dane do MapowanieHandlowcow.'
END
GO

PRINT ''
PRINT '=============================================='
PRINT 'Wszystkie procedury i widoki utworzone pomyślnie!'
PRINT '=============================================='
GO
