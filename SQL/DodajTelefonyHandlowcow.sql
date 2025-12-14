-- =====================================================
-- UZUPEŁNIANIE TELEFONÓW HANDLOWCÓW
-- Uruchom na serwerze 192.168.0.109 (LibraNet)
-- =====================================================

USE [LibraNet]
GO

-- =====================================================
-- 1. Sprawdź którzy handlowcy są aktywni w Handel
--    (mają przypisanych klientów z ostatniego roku)
-- =====================================================

PRINT '=========================================='
PRINT 'AKTYWNI HANDLOWCY (z bazy Handel .112):'
PRINT '=========================================='

SELECT
    h.Handlowiec,
    h.LiczbaKlientow,
    ISNULL(o.ID, 0) AS OperatorID,
    ISNULL(o.Name, '-- NIE ZNALEZIONO --') AS OperatorName,
    ISNULL(k.Telefon, '-- BRAK --') AS Telefon,
    CASE
        WHEN k.Telefon IS NOT NULL AND k.Telefon != '' THEN '✓ OK'
        WHEN o.ID IS NULL THEN '⚠ Brak operatora'
        ELSE '❌ BRAK TELEFONU'
    END AS Status
FROM (
    SELECT
        cc.CDim_Handlowiec_Val AS Handlowiec,
        COUNT(DISTINCT cc.ElementId) AS LiczbaKlientow
    FROM [192.168.0.112].Handel.SSCommon.ContractorClassification cc
    WHERE cc.CDim_Handlowiec_Val IS NOT NULL
      AND cc.CDim_Handlowiec_Val NOT IN ('', '(Brak)', 'Ogólne')
      AND EXISTS (
          SELECT 1 FROM [192.168.0.112].Handel.HM.DK dk
          WHERE dk.khid = cc.ElementId
          AND dk.data >= DATEADD(YEAR, -1, GETDATE())
      )
    GROUP BY cc.CDim_Handlowiec_Val
) h
LEFT JOIN dbo.operators o ON o.Name LIKE h.Handlowiec + '%' OR o.Name = h.Handlowiec
LEFT JOIN dbo.OperatorzyKontakt k ON o.ID = k.OperatorID
ORDER BY h.LiczbaKlientow DESC
GO

-- =====================================================
-- 2. DODAJ TELEFONY - UZUPEŁNIJ PONIŻEJ!
-- =====================================================

PRINT ''
PRINT '=========================================='
PRINT 'DODAWANIE TELEFONÓW:'
PRINT '=========================================='

-- Odkomentuj i uzupełnij telefony dla handlowców:

-- Przykład:
-- EXEC dbo.DodajTelefonOperatora 'Anna Jedynak', '+48508309315'
-- EXEC dbo.DodajTelefonOperatora 'Maja Leonard', '+48536802665'

-- Utwórz procedurę pomocniczą
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'DodajTelefonOperatora')
    DROP PROCEDURE dbo.DodajTelefonOperatora
GO

CREATE PROCEDURE dbo.DodajTelefonOperatora
    @NazwaOperatora NVARCHAR(100),
    @Telefon NVARCHAR(20)
AS
BEGIN
    DECLARE @OperatorID INT

    -- Znajdź operatora
    SELECT TOP 1 @OperatorID = ID
    FROM dbo.operators
    WHERE Name LIKE @NazwaOperatora + '%' OR Name = @NazwaOperatora
    ORDER BY ID DESC

    IF @OperatorID IS NULL
    BEGIN
        PRINT 'BŁĄD: Nie znaleziono operatora: ' + @NazwaOperatora
        RETURN
    END

    -- Sprawdź czy już istnieje
    IF EXISTS (SELECT 1 FROM dbo.OperatorzyKontakt WHERE OperatorID = @OperatorID)
    BEGIN
        -- Aktualizuj
        UPDATE dbo.OperatorzyKontakt
        SET Telefon = @Telefon, DataModyfikacji = GETDATE()
        WHERE OperatorID = @OperatorID
        PRINT '✓ Zaktualizowano telefon dla: ' + @NazwaOperatora + ' -> ' + @Telefon
    END
    ELSE
    BEGIN
        -- Wstaw nowy
        INSERT INTO dbo.OperatorzyKontakt (OperatorID, Telefon)
        VALUES (@OperatorID, @Telefon)
        PRINT '✓ Dodano telefon dla: ' + @NazwaOperatora + ' -> ' + @Telefon
    END
END
GO

-- =====================================================
-- 3. UŻYJ PROCEDURY DO DODANIA TELEFONÓW
-- =====================================================

-- Uzupełnij telefony handlowców (odkomentuj i zmień numery):

/*
EXEC dbo.DodajTelefonOperatora 'Aleksandra Drzewieck', '+48XXXXXXXXX'
EXEC dbo.DodajTelefonOperatora 'Andrzej Berliński', '+48XXXXXXXXX'
EXEC dbo.DodajTelefonOperatora 'Asia Marciniak', '+48XXXXXXXXX'
EXEC dbo.DodajTelefonOperatora 'Dawid Sosiński', '+48XXXXXXXXX'
EXEC dbo.DodajTelefonOperatora 'Gabriela Kowalczyk', '+48XXXXXXXXX'
EXEC dbo.DodajTelefonOperatora 'Grażyna Adrian', '+48XXXXXXXXX'
EXEC dbo.DodajTelefonOperatora 'Grażyna Panak', '+48XXXXXXXXX'
EXEC dbo.DodajTelefonOperatora 'Ilona Krakowiak', '+48XXXXXXXXX'
EXEC dbo.DodajTelefonOperatora 'Jolanta Kubiak', '+48XXXXXXXXX'
EXEC dbo.DodajTelefonOperatora 'Justyna TERKA', '+48XXXXXXXXX'
EXEC dbo.DodajTelefonOperatora 'Klaudia Osińska', '+48XXXXXXXXX'
EXEC dbo.DodajTelefonOperatora 'Marcin Płocki', '+48XXXXXXXXX'
*/

-- =====================================================
-- 4. SPRAWDŹ AKTUALNY STAN
-- =====================================================

PRINT ''
PRINT '=========================================='
PRINT 'AKTUALNA LISTA TELEFONÓW:'
PRINT '=========================================='

SELECT
    o.ID,
    o.Name AS [Operator],
    ISNULL(k.Telefon, '-- BRAK --') AS [Telefon],
    ISNULL(k.Email, '') AS [Email]
FROM dbo.operators o
LEFT JOIN dbo.OperatorzyKontakt k ON o.ID = k.OperatorID
WHERE o.Name IS NOT NULL AND o.Name != ''
ORDER BY
    CASE WHEN k.Telefon IS NOT NULL AND k.Telefon != '' THEN 0 ELSE 1 END,
    o.Name
GO

-- =====================================================
-- 5. PO DODANIU TELEFONÓW - WŁĄCZ WYSYŁANIE SMS
-- =====================================================

/*
-- Gdy wszystko jest gotowe, wyłącz tryb testowy:
UPDATE dbo.SmsApiKonfiguracja SET TestMode = 0

-- Sprawdź konfigurację:
SELECT * FROM dbo.SmsApiKonfiguracja
*/
