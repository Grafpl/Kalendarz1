-- Skrypt do aktualizacji tabeli Kurs
-- Pozwala na NULL w kolumnach KierowcaID i PojazdID
-- Umożliwia tworzenie kursów bez natychmiastowego przypisania kierowcy/pojazdu

USE TransportPL;
GO

-- Najpierw usuń istniejące ograniczenia klucza obcego (jeśli istnieją)
-- Sprawdzenie i usunięcie FK dla KierowcaID
IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Kurs_Kierowca')
BEGIN
    ALTER TABLE dbo.Kurs DROP CONSTRAINT FK_Kurs_Kierowca;
END
GO

-- Sprawdzenie i usunięcie FK dla PojazdID
IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Kurs_Pojazd')
BEGIN
    ALTER TABLE dbo.Kurs DROP CONSTRAINT FK_Kurs_Pojazd;
END
GO

-- Zmiana kolumny KierowcaID na nullable
ALTER TABLE dbo.Kurs
ALTER COLUMN KierowcaID INT NULL;
GO

-- Zmiana kolumny PojazdID na nullable
ALTER TABLE dbo.Kurs
ALTER COLUMN PojazdID INT NULL;
GO

-- Ponowne utworzenie kluczy obcych z opcją ON DELETE SET NULL
ALTER TABLE dbo.Kurs
ADD CONSTRAINT FK_Kurs_Kierowca
FOREIGN KEY (KierowcaID) REFERENCES dbo.Kierowca(KierowcaID)
ON DELETE SET NULL;
GO

ALTER TABLE dbo.Kurs
ADD CONSTRAINT FK_Kurs_Pojazd
FOREIGN KEY (PojazdID) REFERENCES dbo.Pojazd(PojazdID)
ON DELETE SET NULL;
GO

-- Aktualizacja istniejących kursów bez statusu - ustaw domyślny status
UPDATE dbo.Kurs
SET Status = 'Planowany'
WHERE Status IS NULL;
GO

PRINT 'Tabela Kurs została zaktualizowana - KierowcaID i PojazdID mogą być teraz NULL';
