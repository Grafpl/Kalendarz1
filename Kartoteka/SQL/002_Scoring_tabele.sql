-- ============================================
-- Scoring kredytowy klientów
-- Baza: LibraNet (192.168.0.109)
-- ============================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.KartotekaScoring') AND type = 'U')
BEGIN
    CREATE TABLE dbo.KartotekaScoring (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        KlientId INT NOT NULL,

        -- Składniki (0-max dla każdego)
        TerminowoscPkt INT,
        HistoriaPkt INT,
        RegularnoscPkt INT,
        TrendPkt INT,
        LimitPkt INT,

        -- Wynik
        ScoreTotal INT,
        Kategoria NVARCHAR(20),  -- Doskonaly, Dobry, Sredni, Slaby, Krytyczny

        -- Rekomendacje
        RekomendacjaLimitu DECIMAL(18,2),
        RekomendacjaOpis NVARCHAR(500),

        -- Metadata
        DataObliczenia DATETIME DEFAULT GETDATE()
    );

    CREATE NONCLUSTERED INDEX IX_Scoring_Klient ON dbo.KartotekaScoring (KlientId);
    CREATE NONCLUSTERED INDEX IX_Scoring_Score ON dbo.KartotekaScoring (ScoreTotal DESC);
END;
