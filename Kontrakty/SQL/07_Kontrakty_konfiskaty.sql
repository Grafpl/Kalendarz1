-- ════════════════════════════════════════════════════════════════════════════
-- KONTRAKTY — migracja 07: kto pokrywa konfiskaty i padłe
-- Target: LibraNet (192.168.0.109). Idempotentna.
-- ════════════════════════════════════════════════════════════════════════════
USE LibraNet;
GO

IF COL_LENGTH('dbo.KontraktyWersje','KonfiskatyHodowca') IS NULL
BEGIN
    -- 1 = potrącane od hodowcy (default, zazwyczaj), 0 = pokrywa ubojnia
    ALTER TABLE dbo.KontraktyWersje ADD KonfiskatyHodowca BIT NOT NULL CONSTRAINT DF_KW_KonfiskatyHodowca DEFAULT 1;
    PRINT '✅ KontraktyWersje.KonfiskatyHodowca (BIT, default 1 = hodowca pokrywa)';
END
ELSE PRINT '◌ KontraktyWersje.KonfiskatyHodowca już istnieje';
GO
