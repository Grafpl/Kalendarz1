-- ============================================
-- System przypomnień
-- Baza: LibraNet (192.168.0.109)
-- ============================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.KartotekaPrzypomnienia') AND type = 'U')
BEGIN
    CREATE TABLE dbo.KartotekaPrzypomnienia (
        Id INT IDENTITY(1,1) PRIMARY KEY,

        -- Powiązania
        KlientId INT,
        FakturaId INT,
        KontaktId INT,

        -- Treść
        Typ NVARCHAR(50) NOT NULL,
        Tytul NVARCHAR(200) NOT NULL,
        Opis NVARCHAR(MAX),
        Priorytet INT DEFAULT 3,  -- 1=Krytyczny, 2=Wysoki, 3=Normalny

        -- Terminy
        DataPrzypomnienia DATETIME NOT NULL,
        DataWygasniecia DATETIME,

        -- Status
        Status NVARCHAR(50) DEFAULT 'Aktywne', -- Aktywne, Przeczytane, Odlozone, Wykonane, Anulowane

        -- Przypisanie
        PrzypisaneDo NVARCHAR(100),

        -- Powtarzanie
        CzyPowtarzalne BIT DEFAULT 0,
        InterwalDni INT,

        -- Metadata
        DataUtworzenia DATETIME DEFAULT GETDATE(),
        UtworzonyPrzez NVARCHAR(100),
        DataModyfikacji DATETIME
    );

    CREATE NONCLUSTERED INDEX IX_Przyp_Data ON dbo.KartotekaPrzypomnienia (DataPrzypomnienia);
    CREATE NONCLUSTERED INDEX IX_Przyp_Klient ON dbo.KartotekaPrzypomnienia (KlientId);
    CREATE NONCLUSTERED INDEX IX_Przyp_Status ON dbo.KartotekaPrzypomnienia (Status);
END;
