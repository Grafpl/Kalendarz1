/* ════════════════════════════════════════════════════════════════════════════
   Customer360 — tabela cache scoringu klientów.
   Baza: LibraNet (192.168.0.109). Wykonać RĘCZNIE w SSMS.

   Cel: scoring (4-składnikowy) nie przelicza się przy każdym otwarciu karty —
        wynik jest cache'owany na ScoreCacheTtlDays (domyślnie 7 dni).
   Aplikacja działa też BEZ tej tabeli (degraduje się: liczy za każdym razem,
   bez błędu). Po wykonaniu skryptu cache zaczyna działać.
   ════════════════════════════════════════════════════════════════════════════ */

USE LibraNet;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Customer360_ScoreCache')
BEGIN
    CREATE TABLE dbo.Customer360_ScoreCache (
        KlientId    INT           NOT NULL PRIMARY KEY,
        ScoreJson   NVARCHAR(MAX) NOT NULL,
        ScoreLetter CHAR(1)       NOT NULL,
        ValidUntil  DATETIME      NOT NULL,
        CreatedAt   DATETIME      NOT NULL DEFAULT GETDATE()
    );

    CREATE INDEX IX_C360Cache_ValidUntil ON dbo.Customer360_ScoreCache(ValidUntil);

    PRINT 'Utworzono dbo.Customer360_ScoreCache + indeks.';
END
ELSE
    PRINT 'Tabela dbo.Customer360_ScoreCache juz istnieje — pominieto.';
GO

-- Czyszczenie wygasłych wpisów (opcjonalnie, można odpalać z harmonogramu):
-- DELETE FROM dbo.Customer360_ScoreCache WHERE ValidUntil < GETDATE();
