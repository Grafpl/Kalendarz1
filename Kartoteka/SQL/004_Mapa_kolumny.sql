-- ============================================
-- Kolumny geokodowania dla mapy klientów
-- Baza: LibraNet (192.168.0.109)
-- Dodaje kolumny do istniejącej tabeli KartotekaOdbiorcyDane
-- ============================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.KartotekaOdbiorcyDane') AND name = 'Latitude')
BEGIN
    ALTER TABLE dbo.KartotekaOdbiorcyDane ADD Latitude DECIMAL(9,6);
END;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.KartotekaOdbiorcyDane') AND name = 'Longitude')
BEGIN
    ALTER TABLE dbo.KartotekaOdbiorcyDane ADD Longitude DECIMAL(9,6);
END;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.KartotekaOdbiorcyDane') AND name = 'GeokodowanieData')
BEGIN
    ALTER TABLE dbo.KartotekaOdbiorcyDane ADD GeokodowanieData DATETIME;
END;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.KartotekaOdbiorcyDane') AND name = 'GeokodowanieStatus')
BEGIN
    ALTER TABLE dbo.KartotekaOdbiorcyDane ADD GeokodowanieStatus NVARCHAR(50);
END;
