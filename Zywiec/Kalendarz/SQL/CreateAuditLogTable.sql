-- =====================================================
-- TABELA AUDYTU ZMIAN - SYSTEM DOSTAW (HarmonogramDostaw)
-- =====================================================
-- Utworzono: 2026-01-17
-- Opis: Kompleksowa tabela do śledzenia wszystkich zmian
--       w systemie zarządzania dostawami
-- =====================================================

-- Sprawdź czy tabela już istnieje, jeśli tak - nie twórz ponownie
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLog_Dostawy')
BEGIN
    CREATE TABLE AuditLog_Dostawy (
        -- Identyfikator audytu
        AuditID             BIGINT IDENTITY(1,1) PRIMARY KEY,

        -- Kiedy zmiana została wykonana
        DataZmiany          DATETIME2 NOT NULL DEFAULT GETDATE(),

        -- Kto wykonał zmianę (UserID)
        UserID              NVARCHAR(50) NOT NULL,
        UserName            NVARCHAR(100) NULL,

        -- Nazwa tabeli, której dotyczy zmiana
        NazwaTabeli         NVARCHAR(100) NOT NULL,
        -- Możliwe wartości: 'HarmonogramDostaw', 'Notatki', 'WstawieniaKurczakow'

        -- Identyfikator rekordu (LP dostawy, ID notatki, itp.)
        RekordID            NVARCHAR(50) NOT NULL,

        -- Typ operacji
        TypOperacji         NVARCHAR(20) NOT NULL,
        -- Możliwe wartości: 'INSERT', 'UPDATE', 'DELETE'

        -- Źródło zmiany (jak została wykonana)
        ZrodloZmiany        NVARCHAR(100) NOT NULL,
        -- Możliwe wartości:
        -- 'DoubleClick_Auta'           - Kliknięcie w kolumnę A
        -- 'DoubleClick_Sztuki'         - Kliknięcie w kolumnę Szt
        -- 'DoubleClick_Waga'           - Kliknięcie w kolumnę Waga
        -- 'DoubleClick_Uwagi'          - Kliknięcie w kolumnę Uwagi
        -- 'Checkbox_Potwierdzenie'     - Checkbox potwierdzenia dostawy
        -- 'Checkbox_Wstawienie'        - Checkbox potwierdzenia wstawienia
        -- 'Button_DataUp'              - Przycisk przesunięcia daty +1
        -- 'Button_DataDown'            - Przycisk przesunięcia daty -1
        -- 'DragDrop'                   - Przeciągnięcie i upuszczenie
        -- 'Form_Zapisz'                - Formularz zapisu dostawy
        -- 'Form_DodajNotatke'          - Formularz dodania notatki
        -- 'QuickNote'                  - Szybka notatka
        -- 'Button_Duplikuj'            - Duplikacja dostawy
        -- 'Button_Usun'                - Usunięcie dostawy
        -- 'ContextMenu_Potwierdz'      - Menu kontekstowe potwierdzenia
        -- 'ContextMenu_Anuluj'         - Menu kontekstowe anulowania
        -- 'BulkConfirm'                - Masowe potwierdzenie
        -- 'BulkCancel'                 - Masowe anulowanie

        -- Nazwa pola, które zostało zmienione
        NazwaPola           NVARCHAR(100) NULL,
        -- Możliwe wartości: 'Auta', 'SztukiDek', 'WagaDek', 'Cena', 'DataOdbioru',
        --                   'Bufor', 'Dostawca', 'TypCeny', 'TypUmowy', 'Dodatek',
        --                   'SztSzuflada', 'Tresc', 'isConf', itp.

        -- Stara wartość (przed zmianą)
        StaraWartosc        NVARCHAR(MAX) NULL,

        -- Nowa wartość (po zmianie)
        NowaWartosc         NVARCHAR(MAX) NULL,

        -- Dodatkowe informacje kontekstowe (JSON)
        DodatkoweInfo       NVARCHAR(MAX) NULL,
        -- Może zawierać: dostawcę, datę dostawy, informacje o masowej operacji, itp.

        -- Adres IP klienta (opcjonalnie)
        AdresIP             NVARCHAR(50) NULL,

        -- Nazwa komputera klienta (opcjonalnie)
        NazwaKomputera      NVARCHAR(100) NULL,

        -- Opis zmiany (opcjonalny, czytelny dla człowieka)
        OpisZmiany          NVARCHAR(500) NULL
    );

    PRINT 'Tabela AuditLog_Dostawy została utworzona pomyślnie.';
END
ELSE
BEGIN
    PRINT 'Tabela AuditLog_Dostawy już istnieje.';
END
GO

-- =====================================================
-- INDEKSY DLA SZYBKIEGO WYSZUKIWANIA
-- =====================================================

-- Indeks na dacie zmiany (najczęściej używany do filtrowania)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditLog_DataZmiany')
BEGIN
    CREATE NONCLUSTERED INDEX IX_AuditLog_DataZmiany
    ON AuditLog_Dostawy (DataZmiany DESC);
    PRINT 'Indeks IX_AuditLog_DataZmiany utworzony.';
END
GO

-- Indeks na identyfikatorze rekordu (do wyszukiwania historii konkretnej dostawy)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditLog_RekordID')
BEGIN
    CREATE NONCLUSTERED INDEX IX_AuditLog_RekordID
    ON AuditLog_Dostawy (RekordID);
    PRINT 'Indeks IX_AuditLog_RekordID utworzony.';
END
GO

-- Indeks na użytkowniku (do wyszukiwania zmian konkretnego użytkownika)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditLog_UserID')
BEGIN
    CREATE NONCLUSTERED INDEX IX_AuditLog_UserID
    ON AuditLog_Dostawy (UserID);
    PRINT 'Indeks IX_AuditLog_UserID utworzony.';
END
GO

-- Indeks złożony dla typowych zapytań
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditLog_Combined')
BEGIN
    CREATE NONCLUSTERED INDEX IX_AuditLog_Combined
    ON AuditLog_Dostawy (NazwaTabeli, RekordID, DataZmiany DESC)
    INCLUDE (TypOperacji, NazwaPola, StaraWartosc, NowaWartosc, UserID);
    PRINT 'Indeks IX_AuditLog_Combined utworzony.';
END
GO

-- =====================================================
-- WIDOK POMOCNICZY DLA ŁATWIEJSZEGO PRZEGLĄDU
-- =====================================================

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_AuditLog_Czytelny')
    DROP VIEW vw_AuditLog_Czytelny;
GO

CREATE VIEW vw_AuditLog_Czytelny AS
SELECT
    AuditID,
    FORMAT(DataZmiany, 'yyyy-MM-dd HH:mm:ss') AS DataZmianyFormatowana,
    UserID,
    UserName,
    NazwaTabeli,
    RekordID AS LP,
    CASE TypOperacji
        WHEN 'INSERT' THEN 'Dodanie'
        WHEN 'UPDATE' THEN 'Zmiana'
        WHEN 'DELETE' THEN 'Usunięcie'
        ELSE TypOperacji
    END AS TypOperacjiPL,
    CASE ZrodloZmiany
        WHEN 'DoubleClick_Auta' THEN 'Dwuklik - Auta'
        WHEN 'DoubleClick_Sztuki' THEN 'Dwuklik - Sztuki'
        WHEN 'DoubleClick_Waga' THEN 'Dwuklik - Waga'
        WHEN 'DoubleClick_Uwagi' THEN 'Dwuklik - Uwagi'
        WHEN 'Checkbox_Potwierdzenie' THEN 'Checkbox potwierdzenia'
        WHEN 'Checkbox_Wstawienie' THEN 'Checkbox wstawienia'
        WHEN 'Button_DataUp' THEN 'Przycisk data +1'
        WHEN 'Button_DataDown' THEN 'Przycisk data -1'
        WHEN 'DragDrop' THEN 'Przeciągnij i upuść'
        WHEN 'Form_Zapisz' THEN 'Formularz zapisu'
        WHEN 'Form_DodajNotatke' THEN 'Dodaj notatkę'
        WHEN 'QuickNote' THEN 'Szybka notatka'
        WHEN 'Button_Duplikuj' THEN 'Duplikacja'
        WHEN 'Button_Usun' THEN 'Usunięcie'
        WHEN 'ContextMenu_Potwierdz' THEN 'Menu - Potwierdź'
        WHEN 'ContextMenu_Anuluj' THEN 'Menu - Anuluj'
        WHEN 'BulkConfirm' THEN 'Masowe potwierdzenie'
        WHEN 'BulkCancel' THEN 'Masowe anulowanie'
        ELSE ZrodloZmiany
    END AS ZrodloZmianyPL,
    CASE NazwaPola
        WHEN 'Auta' THEN 'Ilość aut'
        WHEN 'SztukiDek' THEN 'Sztuki'
        WHEN 'WagaDek' THEN 'Waga'
        WHEN 'Cena' THEN 'Cena'
        WHEN 'DataOdbioru' THEN 'Data odbioru'
        WHEN 'Bufor' THEN 'Status'
        WHEN 'Dostawca' THEN 'Dostawca'
        WHEN 'TypCeny' THEN 'Typ ceny'
        WHEN 'TypUmowy' THEN 'Typ umowy'
        WHEN 'Dodatek' THEN 'Dodatek'
        WHEN 'SztSzuflada' THEN 'Szt. na szufladę'
        WHEN 'Tresc' THEN 'Treść notatki'
        WHEN 'isConf' THEN 'Potwierdzenie'
        WHEN 'UWAGI' THEN 'Uwagi'
        ELSE NazwaPola
    END AS NazwaPolaPL,
    StaraWartosc,
    NowaWartosc,
    OpisZmiany,
    DodatkoweInfo
FROM AuditLog_Dostawy;
GO

PRINT 'Widok vw_AuditLog_Czytelny utworzony.';
GO

-- =====================================================
-- PROCEDURY SKŁADOWANE DO WSTAWIANIA AUDYTU
-- =====================================================

-- Procedura do zapisu pojedynczej zmiany
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_AuditLog_Insert')
    DROP PROCEDURE sp_AuditLog_Insert;
GO

CREATE PROCEDURE sp_AuditLog_Insert
    @UserID NVARCHAR(50),
    @UserName NVARCHAR(100) = NULL,
    @NazwaTabeli NVARCHAR(100),
    @RekordID NVARCHAR(50),
    @TypOperacji NVARCHAR(20),
    @ZrodloZmiany NVARCHAR(100),
    @NazwaPola NVARCHAR(100) = NULL,
    @StaraWartosc NVARCHAR(MAX) = NULL,
    @NowaWartosc NVARCHAR(MAX) = NULL,
    @DodatkoweInfo NVARCHAR(MAX) = NULL,
    @OpisZmiany NVARCHAR(500) = NULL,
    @AdresIP NVARCHAR(50) = NULL,
    @NazwaKomputera NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO AuditLog_Dostawy (
        UserID, UserName, NazwaTabeli, RekordID, TypOperacji, ZrodloZmiany,
        NazwaPola, StaraWartosc, NowaWartosc, DodatkoweInfo, OpisZmiany,
        AdresIP, NazwaKomputera
    )
    VALUES (
        @UserID, @UserName, @NazwaTabeli, @RekordID, @TypOperacji, @ZrodloZmiany,
        @NazwaPola, @StaraWartosc, @NowaWartosc, @DodatkoweInfo, @OpisZmiany,
        @AdresIP, @NazwaKomputera
    );

    SELECT SCOPE_IDENTITY() AS NewAuditID;
END
GO

PRINT 'Procedura sp_AuditLog_Insert utworzona.';
GO

-- =====================================================
-- PROCEDURA DO POBIERANIA HISTORII DOSTAWY
-- =====================================================

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_AuditLog_GetByLP')
    DROP PROCEDURE sp_AuditLog_GetByLP;
GO

CREATE PROCEDURE sp_AuditLog_GetByLP
    @LP NVARCHAR(50),
    @TopN INT = 100
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@TopN)
        AuditID,
        DataZmiany,
        UserID,
        UserName,
        TypOperacji,
        ZrodloZmiany,
        NazwaPola,
        StaraWartosc,
        NowaWartosc,
        OpisZmiany
    FROM AuditLog_Dostawy
    WHERE RekordID = @LP
    ORDER BY DataZmiany DESC;
END
GO

PRINT 'Procedura sp_AuditLog_GetByLP utworzona.';
GO

-- =====================================================
-- PROCEDURA DO POBIERANIA OSTATNICH ZMIAN
-- =====================================================

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_AuditLog_GetRecent')
    DROP PROCEDURE sp_AuditLog_GetRecent;
GO

CREATE PROCEDURE sp_AuditLog_GetRecent
    @Hours INT = 24,
    @TopN INT = 500
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@TopN)
        AuditID,
        DataZmiany,
        UserID,
        UserName,
        NazwaTabeli,
        RekordID,
        TypOperacji,
        ZrodloZmiany,
        NazwaPola,
        StaraWartosc,
        NowaWartosc,
        OpisZmiany
    FROM AuditLog_Dostawy
    WHERE DataZmiany >= DATEADD(HOUR, -@Hours, GETDATE())
    ORDER BY DataZmiany DESC;
END
GO

PRINT 'Procedura sp_AuditLog_GetRecent utworzona.';
GO

-- =====================================================
-- PROCEDURA DO POBIERANIA ZMIAN UŻYTKOWNIKA
-- =====================================================

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_AuditLog_GetByUser')
    DROP PROCEDURE sp_AuditLog_GetByUser;
GO

CREATE PROCEDURE sp_AuditLog_GetByUser
    @UserID NVARCHAR(50),
    @Days INT = 7,
    @TopN INT = 200
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@TopN)
        AuditID,
        DataZmiany,
        NazwaTabeli,
        RekordID,
        TypOperacji,
        ZrodloZmiany,
        NazwaPola,
        StaraWartosc,
        NowaWartosc,
        OpisZmiany
    FROM AuditLog_Dostawy
    WHERE UserID = @UserID
      AND DataZmiany >= DATEADD(DAY, -@Days, GETDATE())
    ORDER BY DataZmiany DESC;
END
GO

PRINT 'Procedura sp_AuditLog_GetByUser utworzona.';
GO

-- =====================================================
-- RAPORT STATYSTYK AUDYTU
-- =====================================================

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_AuditLog_Statistics')
    DROP PROCEDURE sp_AuditLog_Statistics;
GO

CREATE PROCEDURE sp_AuditLog_Statistics
    @Days INT = 30
AS
BEGIN
    SET NOCOUNT ON;

    -- Podsumowanie według typu operacji
    SELECT
        'Typ operacji' AS Kategoria,
        TypOperacji AS Wartosc,
        COUNT(*) AS Ilosc
    FROM AuditLog_Dostawy
    WHERE DataZmiany >= DATEADD(DAY, -@Days, GETDATE())
    GROUP BY TypOperacji

    UNION ALL

    -- Podsumowanie według źródła zmiany
    SELECT
        'Źródło zmiany' AS Kategoria,
        ZrodloZmiany AS Wartosc,
        COUNT(*) AS Ilosc
    FROM AuditLog_Dostawy
    WHERE DataZmiany >= DATEADD(DAY, -@Days, GETDATE())
    GROUP BY ZrodloZmiany

    UNION ALL

    -- Podsumowanie według użytkownika
    SELECT
        'Użytkownik' AS Kategoria,
        ISNULL(UserName, UserID) AS Wartosc,
        COUNT(*) AS Ilosc
    FROM AuditLog_Dostawy
    WHERE DataZmiany >= DATEADD(DAY, -@Days, GETDATE())
    GROUP BY ISNULL(UserName, UserID)

    ORDER BY Kategoria, Ilosc DESC;
END
GO

PRINT 'Procedura sp_AuditLog_Statistics utworzona.';
GO

PRINT '=========================================';
PRINT 'INSTALACJA SYSTEMU AUDYTU ZAKOŃCZONA';
PRINT '=========================================';
PRINT '';
PRINT 'Utworzone obiekty:';
PRINT '  - Tabela: AuditLog_Dostawy';
PRINT '  - Widok: vw_AuditLog_Czytelny';
PRINT '  - Procedura: sp_AuditLog_Insert';
PRINT '  - Procedura: sp_AuditLog_GetByLP';
PRINT '  - Procedura: sp_AuditLog_GetRecent';
PRINT '  - Procedura: sp_AuditLog_GetByUser';
PRINT '  - Procedura: sp_AuditLog_Statistics';
PRINT '  - Indeksy: 4 indeksy dla wydajności';
GO
