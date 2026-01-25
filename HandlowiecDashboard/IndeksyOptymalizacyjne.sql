-- =====================================================
-- INDEKSY OPTYMALIZACYJNE DLA DASHBOARD HANDLOWCA
-- Uruchom na odpowiednich serwerach baz danych
-- =====================================================

-- =====================================================
-- BAZA: LibraNet (192.168.0.109)
-- =====================================================

USE LibraNet;
GO

-- Indeks dla zapytan wg daty odbioru i anulowania
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dashboard_ZamowieniaMieso_DataOdbioru')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Dashboard_ZamowieniaMieso_DataOdbioru
    ON dbo.ZamowieniaMieso (DataOdbioru, AnulowanePrzez)
    INCLUDE (Id, OdbiorcaId, Odbiorca, Handlowiec, IdUser, KlientId);
    PRINT 'Utworzono indeks IX_Dashboard_ZamowieniaMieso_DataOdbioru';
END
GO

-- Indeks dla zapytan wg daty zamowienia
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dashboard_ZamowieniaMieso_DataZamowienia')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Dashboard_ZamowieniaMieso_DataZamowienia
    ON dbo.ZamowieniaMieso (DataZamowienia, AnulowanePrzez)
    INCLUDE (Id, OdbiorcaId, Odbiorca, Handlowiec, IdUser, KlientId, Status, TransportStatus);
    PRINT 'Utworzono indeks IX_Dashboard_ZamowieniaMieso_DataZamowienia';
END
GO

-- Indeks dla pozycji zamowien - kluczowy dla obliczen Kg i wartosci
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dashboard_ZamowieniaMiesoTowar_ZamowienieId')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Dashboard_ZamowieniaMiesoTowar_ZamowienieId
    ON dbo.ZamowieniaMiesoTowar (ZamowienieId)
    INCLUDE (Ilosc, Cena, KodTowaru, Towar);
    PRINT 'Utworzono indeks IX_Dashboard_ZamowieniaMiesoTowar_ZamowienieId';
END
GO

-- Indeks dla filtrowania po handlowcu
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dashboard_ZamowieniaMieso_Handlowiec')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Dashboard_ZamowieniaMieso_Handlowiec
    ON dbo.ZamowieniaMieso (Handlowiec, DataOdbioru)
    WHERE AnulowanePrzez IS NULL;
    PRINT 'Utworzono indeks IX_Dashboard_ZamowieniaMieso_Handlowiec';
END
GO

-- Indeks dla filtrowania po IdUser (handlowiec z systemu)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dashboard_ZamowieniaMieso_IdUser')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Dashboard_ZamowieniaMieso_IdUser
    ON dbo.ZamowieniaMieso (IdUser, DataZamowienia)
    WHERE AnulowanePrzez IS NULL;
    PRINT 'Utworzono indeks IX_Dashboard_ZamowieniaMieso_IdUser';
END
GO

-- Indeks dla CRM - kontakty
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dashboard_OdbiorcyCRM_Status')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Dashboard_OdbiorcyCRM_Status
    ON dbo.OdbiorcyCRM (Status, DataNastepnegoKontaktu)
    INCLUDE (ID, PKD_Opis);
    PRINT 'Utworzono indeks IX_Dashboard_OdbiorcyCRM_Status';
END
GO

-- Odswież statystyki dla LibraNet
UPDATE STATISTICS dbo.ZamowieniaMieso;
UPDATE STATISTICS dbo.ZamowieniaMiesoTowar;
PRINT 'Odswiezono statystyki dla LibraNet';
GO


-- =====================================================
-- BAZA: Handel (192.168.0.112)
-- =====================================================

USE Handel;
GO

-- Indeks dla faktur wg daty i kontrahenta
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dashboard_DK_Data')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Dashboard_DK_Data
    ON [HM].[DK] (data, khid)
    INCLUDE (id, numer, typ, walbrutto, zaplacono, plattermin, anulowany);
    PRINT 'Utworzono indeks IX_Dashboard_DK_Data';
END
GO

-- Indeks dla faktur wg roku i miesiaca
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dashboard_DK_DataYearMonth')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Dashboard_DK_DataYearMonth
    ON [HM].[DK] (data)
    INCLUDE (id, khid, walbrutto, plattermin, anulowany)
    WHERE anulowany = 0;
    PRINT 'Utworzono indeks IX_Dashboard_DK_DataYearMonth';
END
GO

-- Indeks dla pozycji faktur
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dashboard_DP_Super')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Dashboard_DP_Super
    ON [HM].[DP] (super)
    INCLUDE (idtw, ilosc, cena, wartNetto);
    PRINT 'Utworzono indeks IX_Dashboard_DP_Super';
END
GO

-- Indeks dla klasyfikacji handlowcow
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dashboard_ContractorClassification')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Dashboard_ContractorClassification
    ON [SSCommon].[ContractorClassification] (ElementId)
    INCLUDE (CDim_Handlowiec_Val);
    PRINT 'Utworzono indeks IX_Dashboard_ContractorClassification';
END
GO

-- Indeks dla towarow wg katalogu (swieze/mrozone)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dashboard_TW_Katalog')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Dashboard_TW_Katalog
    ON [HM].[TW] (katalog)
    INCLUDE (ID, kod, nazwa);
    PRINT 'Utworzono indeks IX_Dashboard_TW_Katalog';
END
GO

-- Indeks dla sald opakowan
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dashboard_OP_SALDO')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Dashboard_OP_SALDO
    ON [HM].[OP_SALDO] (id_kh)
    INCLUDE (id_opak, saldo);
    PRINT 'Utworzono indeks IX_Dashboard_OP_SALDO';
END
GO

-- Indeks dla MZ (ruchy magazynowe opakowan)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dashboard_MZ_Data')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Dashboard_MZ_Data
    ON [HM].[MZ] (data, idtw)
    INCLUDE (super, Ilosc);
    PRINT 'Utworzono indeks IX_Dashboard_MZ_Data';
END
GO

-- Indeks dla MG (dokumenty magazynowe)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dashboard_MG_KhId')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Dashboard_MG_KhId
    ON [HM].[MG] (khid, anulowany)
    INCLUDE (id);
    PRINT 'Utworzono indeks IX_Dashboard_MG_KhId';
END
GO

-- Indeks dla platnosci (PN)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dashboard_PN_DkId')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Dashboard_PN_DkId
    ON [HM].[PN] (dkid)
    INCLUDE (kwotarozl, Termin);
    PRINT 'Utworzono indeks IX_Dashboard_PN_DkId';
END
GO

-- Indeks dla kontrahentow
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dashboard_STContractors_Id')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Dashboard_STContractors_Id
    ON [SSCommon].[STContractors] (id)
    INCLUDE (shortcut, LimitAmount);
    PRINT 'Utworzono indeks IX_Dashboard_STContractors_Id';
END
GO

-- Odswież statystyki dla Handel
UPDATE STATISTICS [HM].[DK];
UPDATE STATISTICS [HM].[DP];
UPDATE STATISTICS [HM].[TW];
UPDATE STATISTICS [HM].[MZ];
UPDATE STATISTICS [HM].[MG];
UPDATE STATISTICS [HM].[PN];
UPDATE STATISTICS [SSCommon].[ContractorClassification];
UPDATE STATISTICS [SSCommon].[STContractors];
PRINT 'Odswiezono statystyki dla Handel';
GO


-- =====================================================
-- PODSUMOWANIE
-- =====================================================

PRINT '';
PRINT '=====================================================';
PRINT 'INDEKSY ZOSTALY UTWORZONE/ZAKTUALIZOWANE';
PRINT '=====================================================';
PRINT '';
PRINT 'LibraNet (192.168.0.109):';
PRINT '  - IX_Dashboard_ZamowieniaMieso_DataOdbioru';
PRINT '  - IX_Dashboard_ZamowieniaMieso_DataZamowienia';
PRINT '  - IX_Dashboard_ZamowieniaMiesoTowar_ZamowienieId';
PRINT '  - IX_Dashboard_ZamowieniaMieso_Handlowiec';
PRINT '  - IX_Dashboard_ZamowieniaMieso_IdUser';
PRINT '  - IX_Dashboard_OdbiorcyCRM_Status';
PRINT '';
PRINT 'Handel (192.168.0.112):';
PRINT '  - IX_Dashboard_DK_Data';
PRINT '  - IX_Dashboard_DK_DataYearMonth';
PRINT '  - IX_Dashboard_DP_Super';
PRINT '  - IX_Dashboard_ContractorClassification';
PRINT '  - IX_Dashboard_TW_Katalog';
PRINT '  - IX_Dashboard_OP_SALDO';
PRINT '  - IX_Dashboard_MZ_Data';
PRINT '  - IX_Dashboard_MG_KhId';
PRINT '  - IX_Dashboard_PN_DkId';
PRINT '  - IX_Dashboard_STContractors_Id';
PRINT '';
PRINT 'UWAGA: Dla pelnej optymalizacji, uruchom ten skrypt';
PRINT 'na odpowiednich serwerach baz danych.';
PRINT '=====================================================';
GO
