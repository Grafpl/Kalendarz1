-- ============================================
-- Historia zmian w Kartotece Odbiorców
-- Baza: LibraNet (192.168.0.109)
-- ============================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.KartotekaHistoriaZmian') AND type = 'U')
BEGIN
    CREATE TABLE dbo.KartotekaHistoriaZmian (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,

        -- Co zostało zmienione
        TabelaNazwa NVARCHAR(100) NOT NULL,
        RekordId INT NOT NULL,
        KlientId INT,

        -- Szczegóły zmiany
        TypOperacji NVARCHAR(10) NOT NULL,  -- INSERT, UPDATE, DELETE
        PoleNazwa NVARCHAR(100),
        StaraWartosc NVARCHAR(MAX),
        NowaWartosc NVARCHAR(MAX),

        -- Kto i kiedy
        UzytkownikId NVARCHAR(100) NOT NULL,
        UzytkownikNazwa NVARCHAR(200),
        DataZmiany DATETIME NOT NULL DEFAULT GETDATE(),

        -- Dodatkowe
        Komentarz NVARCHAR(500),
        CzyCofniete BIT DEFAULT 0,
        CofnietePrzez NVARCHAR(100),
        DataCofniecia DATETIME
    );

    CREATE NONCLUSTERED INDEX IX_Historia_KlientId ON dbo.KartotekaHistoriaZmian (KlientId);
    CREATE NONCLUSTERED INDEX IX_Historia_DataZmiany ON dbo.KartotekaHistoriaZmian (DataZmiany DESC);
    CREATE NONCLUSTERED INDEX IX_Historia_Uzytkownik ON dbo.KartotekaHistoriaZmian (UzytkownikId);
END;
