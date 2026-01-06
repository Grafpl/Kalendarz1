-- ============================================
-- DAILY PROSPECTING - KONFIGURACJA HANDLOWCÓW
-- ============================================
-- Uruchom po zainstalowaniu głównego skryptu
-- Dostosuj wartości do swoich handlowców
-- ============================================

USE LibraNet;
GO

-- ============================================
-- KROK 1: WYŚWIETL DOSTĘPNYCH OPERATORÓW
-- ============================================

PRINT '=== LISTA OPERATORÓW (HANDLOWCÓW) ===';

SELECT
    ID,
    Name,
    Access,
    CASE
        WHEN Access LIKE '%SPRZEDAZ%' OR Access LIKE '%HANDLOWIEC%' THEN 'TAK'
        ELSE 'NIE'
    END as CzyHandlowiec
FROM operators
ORDER BY Name;

GO

-- ============================================
-- KROK 2: DODAJ KONFIGURACJĘ HANDLOWCÓW
-- ============================================
-- Odkomentuj i dostosuj poniższe INSERT-y

/*
-- Przykład 1: Handlowiec senior - 10 telefonów, trudne leady
INSERT INTO KonfiguracjaProspectingu
    (HandlowiecID, HandlowiecNazwa, LimitDzienny, GodzinaStart, GodzinaKoniec, Wojewodztwa, PriorytetMin, PriorytetMax)
VALUES
    ('11111', 'Kierownik Sprzedaży', 10, '09:00', '10:30', 'łódzkie,mazowieckie', 3, 5);

-- Przykład 2: Handlowiec mid - 8 telefonów, standardowe
INSERT INTO KonfiguracjaProspectingu
    (HandlowiecID, HandlowiecNazwa, LimitDzienny, GodzinaStart, GodzinaKoniec, Wojewodztwa, TypyKlientow)
VALUES
    ('22222', 'Maja', 8, '09:00', '10:00', 'łódzkie,wielkopolskie', 'Hurtownia,HoReCa');

-- Przykład 3: Handlowiec junior - 6 telefonów, łatwe leady
INSERT INTO KonfiguracjaProspectingu
    (HandlowiecID, HandlowiecNazwa, LimitDzienny, GodzinaStart, GodzinaKoniec, Wojewodztwa, PriorytetMin, PriorytetMax)
VALUES
    ('33333', 'Radek', 6, '09:30', '10:30', 'łódzkie', 1, 3);
*/

-- ============================================
-- KROK 3: SPRAWDŹ AKTUALNĄ KONFIGURACJĘ
-- ============================================

PRINT '';
PRINT '=== AKTUALNA KONFIGURACJA PROSPECTINGU ===';

SELECT
    k.HandlowiecID,
    ISNULL(k.HandlowiecNazwa, o.Name) as Handlowiec,
    k.LimitDzienny,
    CAST(k.GodzinaStart AS VARCHAR(5)) + ' - ' + CAST(k.GodzinaKoniec AS VARCHAR(5)) as GodzinyPracy,
    ISNULL(k.Wojewodztwa, 'Wszystkie') as Wojewodztwa,
    ISNULL(k.TypyKlientow, 'Wszystkie') as TypyKlientow,
    CAST(k.PriorytetMin AS VARCHAR) + '-' + CAST(k.PriorytetMax AS VARCHAR) as ZakresPriorytetow,
    CASE WHEN k.Aktywny = 1 THEN 'TAK' ELSE 'NIE' END as Aktywny
FROM KonfiguracjaProspectingu k
LEFT JOIN operators o ON k.HandlowiecID = CAST(o.ID AS NVARCHAR);

GO

-- ============================================
-- KROK 4: TESTOWE GENEROWANIE KOLEJKI
-- ============================================

PRINT '';
PRINT '=== TEST GENEROWANIA KOLEJKI ===';

-- Uruchom procedurę dla dzisiejszego dnia
EXEC GenerujCodzienaKolejke;

GO

-- ============================================
-- KROK 5: PODGLĄD WYGENEROWANEJ KOLEJKI
-- ============================================

PRINT '';
PRINT '=== DZISIEJSZA KOLEJKA TELEFONÓW ===';

SELECT
    HandlowiecNazwa,
    NazwaFirmy,
    Telefon,
    Miasto,
    Branza,
    StatusCRM,
    Priorytet,
    PowodPriorytetu,
    StatusRealizacji
FROM vw_KolejkaDzisiejsza
ORDER BY HandlowiecNazwa, Priorytet DESC;

GO

-- ============================================
-- KROK 6: STATYSTYKI LEADÓW W BAZIE
-- ============================================

PRINT '';
PRINT '=== STATYSTYKI BAZY LEADÓW ===';

SELECT 'Wszystkich leadów' as Metryka, COUNT(*) as Wartosc FROM OdbiorcyCRM
UNION ALL
SELECT 'Z telefonem', COUNT(*) FROM OdbiorcyCRM WHERE Telefon_K IS NOT NULL AND Telefon_K <> ''
UNION ALL
SELECT 'Do zadzwonienia', COUNT(*) FROM OdbiorcyCRM WHERE ISNULL(Status, 'Do zadzwonienia') = 'Do zadzwonienia'
UNION ALL
SELECT 'Zgoda na kontakt', COUNT(*) FROM OdbiorcyCRM WHERE Status = 'Zgoda na dalszy kontakt'
UNION ALL
SELECT 'Do wysłania oferta', COUNT(*) FROM OdbiorcyCRM WHERE Status = 'Do wysłania oferta'
UNION ALL
SELECT 'Nie zainteresowany', COUNT(*) FROM OdbiorcyCRM WHERE Status = 'Nie zainteresowany';

GO

-- ============================================
-- KROK 7: ROZKŁAD LEADÓW WG WOJEWÓDZTW
-- ============================================

PRINT '';
PRINT '=== ROZKŁAD WG WOJEWÓDZTW ===';

SELECT
    ISNULL(Wojewodztwo, '(brak)') as Wojewodztwo,
    COUNT(*) as Liczba,
    SUM(CASE WHEN Telefon_K IS NOT NULL AND Telefon_K <> '' THEN 1 ELSE 0 END) as ZTelefonem
FROM OdbiorcyCRM
WHERE ISNULL(Status, 'Do zadzwonienia') NOT IN ('Nie zainteresowany', 'Poprosił o usunięcie')
GROUP BY Wojewodztwo
ORDER BY COUNT(*) DESC;

GO

-- ============================================
-- KROK 8: ROZKŁAD WG BRANŻ (PKD_Opis)
-- ============================================

PRINT '';
PRINT '=== TOP 20 BRANŻ ===';

SELECT TOP 20
    ISNULL(PKD_Opis, '(brak)') as Branza,
    COUNT(*) as Liczba
FROM OdbiorcyCRM
WHERE ISNULL(Status, 'Do zadzwonienia') NOT IN ('Nie zainteresowany', 'Poprosił o usunięcie')
GROUP BY PKD_Opis
ORDER BY COUNT(*) DESC;

GO

-- ============================================
-- POMOCNICZE ZAPYTANIA
-- ============================================

-- Aktualizacja typu klienta na podstawie PKD_Opis
/*
UPDATE OdbiorcyCRM SET TypKlienta = 'Hurtownia'
WHERE PKD_Opis LIKE '%hurt%' OR PKD_Opis LIKE '%46.%';

UPDATE OdbiorcyCRM SET TypKlienta = 'HoReCa'
WHERE PKD_Opis LIKE '%restaur%' OR PKD_Opis LIKE '%gastr%' OR PKD_Opis LIKE '%56.%';

UPDATE OdbiorcyCRM SET TypKlienta = 'Sieć'
WHERE PKD_Opis LIKE '%detal%' OR PKD_Opis LIKE '%sklep%' OR PKD_Opis LIKE '%47.%';

UPDATE OdbiorcyCRM SET TypKlienta = 'Przetwórnia'
WHERE PKD_Opis LIKE '%przetw%' OR PKD_Opis LIKE '%produkcja%' OR PKD_Opis LIKE '%10.%';
*/

-- Usunięcie konfiguracji handlowca
-- DELETE FROM KonfiguracjaProspectingu WHERE HandlowiecID = 'XXXXX';

-- Wyczyszczenie kolejki (dla testów)
-- DELETE FROM CodzienaKolejkaTelefonow WHERE DataPrzydzialu = CAST(GETDATE() AS DATE);

PRINT '';
PRINT '=== KONFIGURACJA ZAKOŃCZONA ===';
GO
