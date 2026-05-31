-- ════════════════════════════════════════════════════════════════════════════
-- KONTRAKTY — snapshot danych osobowych Producenta (część 5)
-- PESEL / REGON / nr dowodu / telefon — zamrożone z momentu zawarcia (jak NipSnapshot).
-- Idempotentny. Target: LibraNet (192.168.0.109).
-- ════════════════════════════════════════════════════════════════════════════
USE LibraNet;
GO
IF COL_LENGTH('dbo.Kontrakty','PeselSnapshot')   IS NULL ALTER TABLE dbo.Kontrakty ADD PeselSnapshot   NVARCHAR(15) NULL;
IF COL_LENGTH('dbo.Kontrakty','RegonSnapshot')   IS NULL ALTER TABLE dbo.Kontrakty ADD RegonSnapshot   NVARCHAR(20) NULL;
IF COL_LENGTH('dbo.Kontrakty','NrDowoduSnapshot') IS NULL ALTER TABLE dbo.Kontrakty ADD NrDowoduSnapshot NVARCHAR(20) NULL;
IF COL_LENGTH('dbo.Kontrakty','TelefonSnapshot') IS NULL ALTER TABLE dbo.Kontrakty ADD TelefonSnapshot NVARCHAR(30) NULL;
GO
PRINT '✅ Snapshot osobowy (PESEL/REGON/dowód/telefon) dodany do dbo.Kontrakty.';
GO
