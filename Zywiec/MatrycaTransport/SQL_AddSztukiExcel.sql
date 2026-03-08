-- Dodaje kolumnę SztukiExcel do FarmerCalc
-- Przechowuje sztuki deklarowane importowane z Excela AVILOG (np. 5280 z kolumny ILOŚĆ)
-- Uruchom raz na serwerze 192.168.0.109 w bazie LibraNet

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.FarmerCalc') AND name = 'SztukiExcel'
)
BEGIN
    ALTER TABLE dbo.FarmerCalc ADD SztukiExcel INT NULL;
    PRINT 'Kolumna SztukiExcel dodana do FarmerCalc';
END
ELSE
BEGIN
    PRINT 'Kolumna SztukiExcel już istnieje';
END
