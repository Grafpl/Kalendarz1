-- ════════════════════════════════════════════════════════════════════
-- COLD CHAIN HACCP — warstwa na ISTNIEJĄCYCH danych
-- Baza: LibraNet (192.168.0.109)
--
-- WAŻNE: Cold Chain NIE tworzy własnych tabel pomiarów ani norm.
-- Używa tego co już masz:
--   • dbo.TemperaturyMiejsca  — pomiary (PartiaId, Miejsce, Proba1..4, Srednia, Wykonal, DataPomiaru)
--   • dbo.QC_Normy            — progi (Kategoria='TEMPERATURA': TempRampa/TempChillera/TempTunel)
--
-- Ten skrypt dodaje TYLKO rejestr działań naprawczych HACCP (korekty incydentów).
-- Ciągły monitoring sondami (24/7) = osobny plik CreateColdChainSensors.sql (na później).
-- Uruchom raz na LibraNet. Bezpieczne (IF NOT EXISTS).
-- ════════════════════════════════════════════════════════════════════

-- Rejestr korekt HACCP do incydentów temperaturowych.
-- Incydent = wpis w TemperaturyMiejsca którego Srednia jest poza normą z QC_Normy.
-- Incydenty wykrywamy w locie (query), tu trzymamy tylko działanie naprawcze.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ColdChainKorekta')
BEGIN
    CREATE TABLE dbo.ColdChainKorekta (
        Id                   INT IDENTITY(1,1) PRIMARY KEY,
        TemperaturaMiejscaId INT           NOT NULL,   -- FK logiczny do TemperaturyMiejsca.Id
        KorektaOpis          NVARCHAR(2000) NOT NULL,   -- przyczyna + działanie naprawcze
        KorektaPrzez         NVARCHAR(50)  NULL,
        KorektaDateTime      DATETIME      NOT NULL CONSTRAINT DF_CCK_Dt DEFAULT GETDATE(),
        Status               NVARCHAR(20)  NOT NULL CONSTRAINT DF_CCK_St DEFAULT 'ZAMKNIETY'
    );
    CREATE UNIQUE INDEX UX_ColdChainKorekta_Pomiar ON dbo.ColdChainKorekta(TemperaturaMiejscaId);
    PRINT 'Utworzono ColdChainKorekta';
END
ELSE PRINT 'ColdChainKorekta już istnieje';
GO

-- Indeks pod dashboard (zakres dat) — jeśli brak.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TempMiejsca_Data' AND object_id = OBJECT_ID('dbo.TemperaturyMiejsca'))
BEGIN
    CREATE INDEX IX_TempMiejsca_Data ON dbo.TemperaturyMiejsca(DataPomiaru);
    PRINT 'Utworzono indeks IX_TempMiejsca_Data';
END
ELSE PRINT 'Indeks IX_TempMiejsca_Data już istnieje';
GO

-- Normy temperatur dla NOWYCH miejsc: oparzalnik (woda gorąca) + schładzalnik (wanna).
-- Idempotentne — dodaje tylko jeśli brak. (Moduł ma też fallback w kodzie, ale to źródło prawdy.)
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'QC_Normy')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.QC_Normy WHERE Nazwa = 'TempOparzalnik')
        INSERT INTO dbo.QC_Normy (Nazwa, Opis, MinWartosc, MaxWartosc, JednostkaMiary, Kategoria, Kolejnosc)
        VALUES ('TempOparzalnik', 'Woda oparzalnika (parzenie)', 50.00, 62.00, 'C', 'TEMPERATURA', 4);

    IF NOT EXISTS (SELECT 1 FROM dbo.QC_Normy WHERE Nazwa = 'TempSchladzalnik')
        INSERT INTO dbo.QC_Normy (Nazwa, Opis, MinWartosc, MaxWartosc, JednostkaMiary, Kategoria, Kolejnosc)
        VALUES ('TempSchladzalnik', 'Woda schladzalnika / wanna', 0.00, 4.00, 'C', 'TEMPERATURA', 5);

    PRINT 'Dodano normy TempOparzalnik + TempSchladzalnik (jesli brak).';
END
ELSE
    PRINT 'UWAGA: brak tabeli QC_Normy - uruchom najpierw Partie/SQL/CreatePartieV2.sql.';
GO

PRINT 'Cold Chain SQL gotowy (na TemperaturyMiejsca + QC_Normy).';
GO
