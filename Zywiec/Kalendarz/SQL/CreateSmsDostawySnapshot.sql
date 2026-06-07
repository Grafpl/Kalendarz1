-- ============================================================================
-- SmsDostawySnapshot — historia SMS-ów o szczegółach dostawy + snapshot stanu
--
-- Cel: gdy zakupowiec kliknie "Kopiuj SMS o szczegółach dostawy", zapisujemy
-- snapshot kluczowych pól (DataOdbioru, Auta, SztukiDek, WagaDek). Później przy
-- ładowaniu listy dostaw porównujemy aktualne dane z najnowszym snapshotem —
-- jeśli się różnią (zmieniono datę lub liczbę aut), pokazujemy ⚠️ że trzeba
-- wysłać SMS aktualizujący.
--
-- Baza: LibraNet (192.168.0.109)
-- Uruchamiać raz na bazę.
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SmsDostawySnapshot' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.SmsDostawySnapshot
    (
        Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SmsDostawySnapshot PRIMARY KEY,
        LP           INT              NOT NULL,             -- HarmonogramDostaw.Lp
        Dostawca     NVARCHAR(255)    NULL,
        DataOdbioru  DATE             NOT NULL,             -- snapshot daty w momencie SMS-a
        Auta         INT              NOT NULL,             -- snapshot ilości aut
        SztukiDek    INT              NOT NULL,
        WagaDek      DECIMAL(10,2)    NOT NULL,
        Wariant      NVARCHAR(20)     NOT NULL DEFAULT 'pierwszy',  -- 'pierwszy' lub 'aktualizacja'
        SmsText      NVARCHAR(MAX)    NULL,                 -- pełna treść SMS-a (do podglądu/audytu)
        UserID       NVARCHAR(50)     NOT NULL,
        CreatedAt    DATETIME         NOT NULL CONSTRAINT DF_SmsDostawySnapshot_CreatedAt DEFAULT (GETDATE())
    );

    -- Indeks na LP + data: szybkie pobieranie najnowszego snapshota per dostawa
    CREATE INDEX IX_SmsDostawySnapshot_LP_CreatedAt
        ON dbo.SmsDostawySnapshot (LP, CreatedAt DESC);

    PRINT 'Utworzono tabelę dbo.SmsDostawySnapshot + indeks.';
END
ELSE
    PRINT 'Tabela dbo.SmsDostawySnapshot już istnieje — pomijam.';
GO
