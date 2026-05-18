-- =============================================================================
-- EKSPLORACJA ZALEZNOSCI - dopasowane SELECT-y na bazie audytu kodu ZPSP
-- =============================================================================
-- Audyt zrobiony z kodu ZPSP (PartiaService.cs, KartotekaService.cs,
-- ArticleService.cs, FlotaService.cs, etc.) - znajduje WSZYSTKIE konkretne
-- queries jakie program robi.
--
-- Ten plik:
--   - Reprodukuje kluczowe JOINy z kodu (na realnych danych)
--   - Wykrywa broken FK i sieroty
--   - Sprawdza filtry charakterystyczne (IsClose=0, Status='Anulowane', etc.)
--   - Cross-DB queries (LibraNet x HANDEL x TransportPL)
--   - Walidacja typu (np. Cena VARCHAR rzutowana na decimal)
--
-- INSTRUKCJA:
--   1. SSMS -> 192.168.0.109 (pronova/pronova) - dla LibraNet/TransportPL
--   2. Ctrl+T -> F5 -> Ctrl+A -> Ctrl+C
--   3. Wklej do WYNIKI_ZALEZNOSCI.txt
--
-- UWAGA: Niektóre bloki mają cross-server query (Linked Server). Jeśli
-- nie masz LinkedServer, te bloki wyrzucą blad - to OK, pomiń.
-- =============================================================================

USE LibraNet;
GO

SET NOCOUNT ON;
GO

PRINT '################################################################';
PRINT '## EKSPLORACJA ZALEZNOSCI - START                              ##';
PRINT '################################################################';
GO

-- =============================================================================
-- BLOK 91 - listapartii FULL JOIN jak w PartiaService.GetPartieAsync()
-- =============================================================================
-- Reprodukcja query z Partie/Services/PartiaService.cs:140-197

SELECT TOP 30
    '91_A_listapartii_full_join' AS __SEKCJA,
    lp.GUID,
    lp.DIR_ID,
    lp.Partia,
    lp.CreateData,
    lp.CreateGodzina,
    lp.IsClose,
    lp.StatusV2,
    lp.HarmonogramLp,
    lp.ArticleID,
    -- Hodowca (PartiaDostawca)
    pd.CustomerID,
    pd.CustomerName,
    -- Operatorzy (operators)
    op_create.Name AS CreateOperator_Imie,
    op_close.Name AS CloseOperator_Imie,
    -- FarmerCalc (najnowsza dla partii)
    fc.NettoWeight AS FarmerCalc_Netto,
    fc.LumQnt AS FarmerCalc_Sztuki,
    fc.Price AS FarmerCalc_Cena,
    -- Out1A (wydania) - agregat
    w_out.WydanoKg,
    w_out.WydanoSzt,
    -- In0E (przyjęcia) - agregat
    w_in.PrzyjetoKg,
    w_in.PrzyjetoSzt,
    -- QC Podsum
    qcp.KlasaB_Proc,
    qcp.Przekarmienie_Kg,
    -- Temperatura rampa
    qct.Sonda1 AS TempRampa_Sonda1,
    -- Wady skale
    qcw.Skrzydla_Ocena,
    qcw.Nogi_Ocena,
    qcw.Oparzenia_Ocena
FROM dbo.listapartii lp
LEFT JOIN dbo.PartiaDostawca pd ON pd.Partia = lp.Partia
LEFT JOIN dbo.operators op_create ON CAST(op_create.ID AS varchar) = lp.CreateOperator
LEFT JOIN dbo.operators op_close ON CAST(op_close.ID AS varchar) = lp.CloseOperator
OUTER APPLY (
    SELECT TOP 1 NettoWeight, LumQnt, Price
    FROM dbo.FarmerCalc fc2
    WHERE fc2.Partia = lp.Partia
    ORDER BY fc2.ID DESC
) fc
LEFT JOIN (
    SELECT P1, SUM(ActWeight) AS WydanoKg, SUM(Quantity) AS WydanoSzt
    FROM dbo.Out1A
    WHERE ActWeight > 0
    GROUP BY P1
) w_out ON w_out.P1 = lp.Partia
LEFT JOIN (
    SELECT P1, SUM(ActWeight) AS PrzyjetoKg, SUM(Quantity) AS PrzyjetoSzt
    FROM dbo.In0E
    WHERE ActWeight > 0
    GROUP BY P1
) w_in ON w_in.P1 = lp.Partia
OUTER APPLY (
    SELECT TOP 1 KlasaB_Proc, Przekarmienie_Kg
    FROM dbo.vw_QC_Podsum vqp
    WHERE vqp.PartiaId = lp.Partia
    ORDER BY 1 DESC
) qcp
OUTER APPLY (
    SELECT TOP 1 Sonda1
    FROM dbo.Temperatury t
    WHERE t.PartiaId = lp.Partia
      AND LOWER(ISNULL(t.Miejsce, '')) LIKE '%rampa%'
    ORDER BY t.Id DESC
) qct
OUTER APPLY (
    SELECT TOP 1 Skrzydla_Ocena, Nogi_Ocena, Oparzenia_Ocena
    FROM dbo.vw_QC_WadySkale vqw
    WHERE vqw.PartiaId = lp.Partia
    ORDER BY 1 DESC
) qcw
WHERE lp.CreateData >= CONVERT(varchar(10), DATEADD(DAY, -7, GETDATE()), 120)
ORDER BY lp.CreateData DESC, lp.CreateGodzina DESC;
GO

-- 91.B - SIEROTY: listapartii bez wpisu w PartiaDostawca
SELECT
    '91_B_partie_bez_dostawcy' AS __SEKCJA,
    COUNT(*) AS razem,
    MIN(lp.CreateData) AS najstarsza,
    MAX(lp.CreateData) AS najnowsza
FROM dbo.listapartii lp
LEFT JOIN dbo.PartiaDostawca pd ON pd.Partia = lp.Partia
WHERE pd.Partia IS NULL;
GO

-- 91.C - SIEROTY: PartiaDostawca bez listapartii (rzadkie)
SELECT
    '91_C_dostawcy_bez_partii' AS __SEKCJA,
    COUNT(*) AS razem
FROM dbo.PartiaDostawca pd
LEFT JOIN dbo.listapartii lp ON lp.Partia = pd.Partia
WHERE lp.Partia IS NULL;
GO

-- 91.D - listapartii.IsClose - dystrybucja
SELECT
    '91_D_isclose_dystrybucja' AS __SEKCJA,
    IsClose,
    COUNT(*) AS razem,
    MIN(CreateData) AS pierwsza,
    MAX(CreateData) AS ostatnia
FROM dbo.listapartii
GROUP BY IsClose
ORDER BY IsClose;
GO

-- 91.E - StatusV2 - czy default 'IN_PRODUCTION' działa
SELECT
    '91_E_statusv2_dystrybucja' AS __SEKCJA,
    StatusV2,
    COUNT(*) AS razem,
    SUM(CASE WHEN IsClose = 1 THEN 1 ELSE 0 END) AS zamknietych,
    SUM(CASE WHEN ISNULL(IsClose,0) = 0 THEN 1 ELSE 0 END) AS otwartych
FROM dbo.listapartii
GROUP BY StatusV2
ORDER BY razem DESC;
GO

-- 91.F - DirID - dystrybucja (dział 1A/0E/0K)
SELECT
    '91_F_dirid_dystrybucja' AS __SEKCJA,
    DIR_ID,
    StatusV2,
    COUNT(*) AS razem
FROM dbo.listapartii
WHERE CreateData >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
GROUP BY DIR_ID, StatusV2
ORDER BY DIR_ID, razem DESC;
GO

-- =============================================================================
-- BLOK 92 - ZamowieniaMieso jak w ZPSP.Sales\SQL\SqlQueries.cs
-- =============================================================================
-- Reprodukcja query 16-54: zamówienia per dzień z agregatami pozycji

SELECT TOP 30
    '92_A_zamowienia_z_pozycjami' AS __SEKCJA,
    zm.Id,
    zm.KlientId,
    SUM(zmt.Ilosc) AS Ilosc_kg,
    zm.DataPrzyjazdu,
    zm.DataUtworzenia,
    zm.IdUser,
    zm.Status,
    zm.LiczbaPojemnikow,
    zm.LiczbaPalet,
    zm.TrybE2,
    zm.Uwagi,
    zm.TransportKursID,
    -- EXISTS checks (z kodu)
    CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId = zm.Id AND t.Folia = 1) THEN 1 ELSE 0 END AS bit) AS MaFolie,
    CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId = zm.Id AND t.Hallal = 1) THEN 1 ELSE 0 END AS bit) AS MaHallal,
    CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId = zm.Id AND ISNULL(t.Cena,'')<>'' AND ISNULL(t.Cena,'0')<>'0') THEN 1 ELSE 0 END AS bit) AS CzyMaCeny,
    zm.CzyZrealizowane,
    zm.DataWydania,
    zm.DataUboju
FROM dbo.ZamowieniaMieso zm
LEFT JOIN dbo.ZamowieniaMiesoTowar zmt ON zmt.ZamowienieId = zm.Id
WHERE zm.DataUboju >= CONVERT(varchar(10), DATEADD(DAY, -7, GETDATE()), 120)
  AND ISNULL(zm.Status, 'Nowe') NOT IN ('Anulowane')
GROUP BY zm.Id, zm.KlientId, zm.DataPrzyjazdu, zm.DataUtworzenia, zm.IdUser,
         zm.Status, zm.LiczbaPojemnikow, zm.LiczbaPalet, zm.TrybE2, zm.Uwagi,
         zm.TransportKursID, zm.CzyZrealizowane, zm.DataWydania, zm.DataUboju
ORDER BY zm.Id DESC;
GO

-- 92.B - WALIDACJA: czy Cena w ZamowieniaMiesoTowar daje się rzutować na decimal
SELECT
    '92_B_cena_walidacja' AS __SEKCJA,
    COUNT(*) AS razem,
    SUM(CASE WHEN Cena IS NULL OR Cena = '' THEN 1 ELSE 0 END) AS puste,
    SUM(CASE WHEN ISNUMERIC(REPLACE(Cena, ',', '.')) = 1 THEN 1 ELSE 0 END) AS numeryczne_OK,
    SUM(CASE WHEN ISNUMERIC(REPLACE(Cena, ',', '.')) = 0 AND Cena IS NOT NULL AND Cena <> '' THEN 1 ELSE 0 END) AS BLEDNE
FROM dbo.ZamowieniaMiesoTowar;
GO

-- 92.C - Sample BLEDNYCH wartosci Cena (jesli sa)
SELECT TOP 20
    '92_C_cena_bledne_sample' AS __SEKCJA,
    Id, ZamowienieId, KodTowaru, Ilosc, Cena
FROM dbo.ZamowieniaMiesoTowar
WHERE Cena IS NOT NULL AND Cena <> ''
  AND ISNUMERIC(REPLACE(Cena, ',', '.')) = 0;
GO

-- 92.D - Statusy zamowien per miesiac
SELECT
    '92_D_statusy_per_miesiac' AS __SEKCJA,
    YEAR(DataZamowienia) AS rok,
    MONTH(DataZamowienia) AS miesiac,
    Status,
    COUNT(*) AS razem
FROM dbo.ZamowieniaMieso
WHERE DataZamowienia >= DATEADD(MONTH, -6, GETDATE())
GROUP BY YEAR(DataZamowienia), MONTH(DataZamowienia), Status
ORDER BY rok DESC, miesiac DESC, razem DESC;
GO

-- 92.E - SIEROTY: ZamowieniaMieso.TransportKursID bez Kurs (cross-DB!)
-- Uwaga: to dziala tylko jesli LibraNet i TransportPL sa na tym samym serwerze
SELECT
    '92_E_zamowienia_bez_kursu' AS __SEKCJA,
    COUNT(*) AS razem
FROM dbo.ZamowieniaMieso zm
WHERE zm.TransportKursID IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM TransportPL.dbo.Kurs k
      WHERE k.KursID = zm.TransportKursID
  );
GO

-- =============================================================================
-- BLOK 93 - Article z auto-update logu (ArticleAuditLog)
-- =============================================================================

-- 93.A - Sample produktow z linkami do zdjec/partycji/konfiguracji
SELECT TOP 30
    '93_A_article_full' AS __SEKCJA,
    a.GUID, a.ID, a.ShortName, a.Name,
    a.Cena1, a.Cena2, a.Cena3,
    a.Wydajnosc, a.Halt,
    a.StandardWeight, a.StandardTol, a.StandardTolMinus,
    -- Zdjecie istnieje?
    CASE WHEN EXISTS(SELECT 1 FROM dbo.TowarZdjecia tz WHERE tz.TowarId = TRY_CAST(a.ID AS int) AND tz.Aktywne = 1) THEN 1 ELSE 0 END AS MaZdjecie,
    -- Partycja istnieje?
    CASE WHEN EXISTS(SELECT 1 FROM dbo.ArtPartitionD ap WHERE ap.ID = a.ID) THEN 1 ELSE 0 END AS MaPartycje,
    -- Konfiguracja produkcji
    CASE WHEN EXISTS(SELECT 1 FROM dbo.KonfiguracjaProduktow kp WHERE kp.TowarID = TRY_CAST(a.ID AS int) AND kp.Aktywny = 1) THEN 1 ELSE 0 END AS MaKonfig,
    a.ModificationData
FROM dbo.Article a
ORDER BY a.Name;
GO

-- 93.B - ArticleAuditLog - kto co zmienial
SELECT TOP 20
    '93_B_article_audit_recent' AS __SEKCJA, *
FROM dbo.ArticleAuditLog
ORDER BY ChangedAt DESC;
GO

-- 93.C - Top zmieniane pola w Article (audyt)
SELECT
    '93_C_article_top_zmiany' AS __SEKCJA,
    FieldName,
    COUNT(*) AS liczba_zmian,
    COUNT(DISTINCT ArticleGUID) AS liczba_produktow
FROM dbo.ArticleAuditLog
GROUP BY FieldName
ORDER BY liczba_zmian DESC;
GO

-- =============================================================================
-- BLOK 94 - HarmonogramDostaw workflow + audit
-- =============================================================================

-- 94.A - Workflow dostaw 7 dni
SELECT
    '94_A_harmonogram_workflow' AS __SEKCJA,
    h.LP, h.DataOdbioru, h.Dostawca, h.Bufor,
    h.Utworzone, h.Wysłane, h.Otrzymane, h.Posrednik,
    h.Auta, h.SztukiDek, h.WagaDek,
    -- Operator który utworzył
    op_utw.Name AS KtoUtw_Imie,
    h.KiedyUtw,
    op_wysl.Name AS KtoWysl_Imie,
    h.KiedyWysl,
    op_otrzm.Name AS KtoOtrzym_Imie,
    h.KiedyOtrzm
FROM dbo.HarmonogramDostaw h
LEFT JOIN dbo.operators op_utw ON op_utw.ID = h.KtoUtw
LEFT JOIN dbo.operators op_wysl ON op_wysl.ID = h.KtoWysl
LEFT JOIN dbo.operators op_otrzm ON op_otrzm.ID = h.KtoOtrzym
WHERE h.DataOdbioru >= CONVERT(varchar(10), DATEADD(DAY, -7, GETDATE()), 120)
ORDER BY h.DataOdbioru DESC;
GO

-- 94.B - Filtr Bufor='Potwierdzony' vs reszta (z kodu)
SELECT
    '94_B_bufor_dystrybucja' AS __SEKCJA,
    Bufor,
    COUNT(*) AS razem,
    SUM(CAST(ISNULL(Utworzone, 0) AS int)) AS utworzonych,
    SUM(CAST(ISNULL(Wysłane, 0) AS int)) AS wyslanych,
    SUM(CAST(ISNULL(Otrzymane, 0) AS int)) AS otrzymanych
FROM dbo.HarmonogramDostaw
GROUP BY Bufor
ORDER BY razem DESC;
GO

-- 94.C - Audit log harmonogramu (ostatnie zmiany)
SELECT TOP 20
    '94_C_audit_recent' AS __SEKCJA, *
FROM dbo.HarmonogramDostaw_AuditLog
ORDER BY ChangedAt DESC;
GO

-- =============================================================================
-- BLOK 95 - KartotekaOdbiorcy synch z OdbiorcyCRM (LibraNet)
-- =============================================================================

-- 95.A - Czy IdSymfonia w KartotekaOdbiorcyDane sie nie pokrywa z innymi tabelami klientow
SELECT
    '95_A_kartoteka_overlap' AS __SEKCJA,
    'KartotekaOdbiorcyDane.IdSymfonia' AS zrodlo,
    (SELECT COUNT(*) FROM dbo.KartotekaOdbiorcyDane) AS razem,
    (SELECT COUNT(*) FROM dbo.KartotekaOdbiorcyDane k
     WHERE EXISTS (SELECT 1 FROM dbo.OdbiorcyCRM o WHERE o.ID = k.IdSymfonia)) AS w_OdbiorcyCRM,
    (SELECT COUNT(*) FROM dbo.KartotekaOdbiorcyDane k
     WHERE EXISTS (SELECT 1 FROM dbo.kontrahenci o WHERE TRY_CAST(o.ID AS int) = k.IdSymfonia)) AS w_kontrahenci;
GO

-- 95.B - KategoriaHandlowca distribution
SELECT
    '95_B_kategorie' AS __SEKCJA,
    KategoriaHandlowca,
    COUNT(*) AS razem
FROM dbo.KartotekaOdbiorcyDane
GROUP BY KategoriaHandlowca
ORDER BY razem DESC;
GO

-- 95.C - Kontakty per typ
SELECT
    '95_C_typy_kontaktow' AS __SEKCJA,
    TypKontaktu,
    COUNT(*) AS razem
FROM dbo.KartotekaOdbiorcyKontakty
GROUP BY TypKontaktu
ORDER BY razem DESC;
GO

-- =============================================================================
-- BLOK 96 - TransportPL: Kurs+Kierowca+Pojazd+Ladunek
-- =============================================================================

USE TransportPL;
GO

-- 96.A - Kursy dziś z pełną informacją (kierowca, pojazd, ładunki)
SELECT TOP 30
    '96_A_kursy_dzis' AS __SEKCJA,
    k.KursID, k.DataKursu, k.Trasa, k.GodzWyjazdu, k.Status,
    ki.Imie + ' ' + ki.Nazwisko AS Kierowca,
    ki.Telefon,
    p.Rejestracja, p.Marka, p.Model,
    ISNULL(p.PaletyH1, 33) AS MaxPalety,
    -- Liczba ładunków
    (SELECT COUNT(*) FROM dbo.Ladunek l WHERE l.KursID = k.KursID) AS LiczbaLadunkow,
    (SELECT SUM(PojemnikiE2) FROM dbo.Ladunek l WHERE l.KursID = k.KursID) AS SumaE2,
    (SELECT SUM(PaletyH1) FROM dbo.Ladunek l WHERE l.KursID = k.KursID) AS SumaPalet
FROM dbo.Kurs k
LEFT JOIN dbo.Kierowca ki ON ki.KierowcaID = k.KierowcaID
LEFT JOIN dbo.Pojazd p ON p.PojazdID = k.PojazdID
WHERE k.DataKursu >= DATEADD(DAY, -3, GETDATE())
ORDER BY k.DataKursu DESC, k.GodzWyjazdu DESC;
GO

-- 96.B - WALIDACJA: Kurs.KierowcaID bez Kierowca (sieroty)
SELECT
    '96_B_kursy_bez_kierowcy' AS __SEKCJA,
    COUNT(*) AS razem
FROM dbo.Kurs k
WHERE k.KierowcaID IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.Kierowca ki WHERE ki.KierowcaID = k.KierowcaID);
GO

-- 96.C - Ladunek bez Kursu (sieroty)
SELECT
    '96_C_ladunki_bez_kursu' AS __SEKCJA,
    COUNT(*) AS razem
FROM dbo.Ladunek l
WHERE NOT EXISTS (SELECT 1 FROM dbo.Kurs k WHERE k.KursID = l.KursID);
GO

-- 96.D - Ladunek.KodKlienta wzorce (np. 'ZAM_xxx')
SELECT
    '96_D_kody_klientow_wzorce' AS __SEKCJA,
    CASE
        WHEN KodKlienta LIKE 'ZAM_%' THEN 'ZAM_*'
        WHEN KodKlienta IS NULL THEN 'NULL'
        WHEN KodKlienta = '' THEN 'EMPTY'
        ELSE 'INNE'
    END AS wzorzec,
    COUNT(*) AS razem
FROM dbo.Ladunek
GROUP BY CASE
        WHEN KodKlienta LIKE 'ZAM_%' THEN 'ZAM_*'
        WHEN KodKlienta IS NULL THEN 'NULL'
        WHEN KodKlienta = '' THEN 'EMPTY'
        ELSE 'INNE'
    END
ORDER BY razem DESC;
GO

-- 96.E - TransportZmiany - typy + statusy
SELECT
    '96_E_zmiany_typy_statusy' AS __SEKCJA,
    TypZmiany,
    StatusZmiany,
    COUNT(*) AS razem,
    MIN(DataZgloszenia) AS pierwsza,
    MAX(DataZgloszenia) AS ostatnia
FROM dbo.TransportZmiany
WHERE DataZgloszenia >= DATEADD(DAY, -30, GETDATE())
GROUP BY TypZmiany, StatusZmiany
ORDER BY razem DESC;
GO

USE LibraNet;
GO

-- =============================================================================
-- BLOK 97 - In0E ważenia + linki
-- =============================================================================

-- 97.A - In0E z linkiem do listapartii i PartiaDostawca
SELECT TOP 30
    '97_A_in0e_full_link' AS __SEKCJA,
    e.Data, e.Godzina, e.ArticleID, e.ArticleName,
    e.ActWeight, e.Weight, e.Tara,
    e.QntInCont AS Klasa,
    e.OperatorID, e.Wagowy,
    e.TermID, e.TermType,
    e.P1 AS Partia,
    -- Hodowca
    pd.CustomerID, pd.CustomerName,
    -- Status partii
    lp.IsClose, lp.StatusV2
FROM dbo.In0E e
LEFT JOIN dbo.PartiaDostawca pd ON pd.Partia = e.P1
LEFT JOIN dbo.listapartii lp ON lp.Partia = e.P1
WHERE e.Data >= CONVERT(varchar(10), DATEADD(DAY, -1, GETDATE()), 120)
  AND e.ActWeight > 0
ORDER BY e.Data DESC, e.Godzina DESC;
GO

-- 97.B - In0E.P1 sieroty (ważenia bez listapartii)
SELECT
    '97_B_wazenia_sieroty' AS __SEKCJA,
    COUNT(*) AS razem,
    MIN(Data) AS pierwsza,
    MAX(Data) AS ostatnia
FROM dbo.In0E e
WHERE e.P1 IS NOT NULL AND e.P1 <> ''
  AND NOT EXISTS (SELECT 1 FROM dbo.listapartii lp WHERE lp.Partia = e.P1)
  AND e.Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120);
GO

-- 97.C - WAGOCounter vs In0E (porównanie sztuk vs ważone kg per dzień)
SELECT TOP 30
    '97_C_wagocounter_vs_in0e' AS __SEKCJA,
    CONVERT(varchar(10), wc.CalcDate, 120) AS data_,
    SUM(wc.Quantity) AS wago_sztuk_policzone,
    (SELECT COUNT(*) FROM dbo.In0E e
     WHERE e.Data = CONVERT(varchar(10), wc.CalcDate, 120)
       AND e.ArticleID = '40' AND e.ActWeight > 0) AS in0e_palety_kurczaka_A,
    (SELECT SUM(e.ActWeight) FROM dbo.In0E e
     WHERE e.Data = CONVERT(varchar(10), wc.CalcDate, 120)
       AND e.ArticleID = '40' AND e.ActWeight > 0) AS in0e_kg_kurczaka_A,
    -- Średnia waga 1 sztuki = kg / wago_sztuk
    CASE WHEN SUM(wc.Quantity) > 0 THEN
        (SELECT SUM(e.ActWeight) FROM dbo.In0E e
         WHERE e.Data = CONVERT(varchar(10), wc.CalcDate, 120)
           AND e.ArticleID = '40' AND e.ActWeight > 0) / SUM(wc.Quantity)
    ELSE NULL END AS sr_kg_na_sztuke
FROM dbo.WagoCounter wc
WHERE wc.CalcDate >= DATEADD(DAY, -30, GETDATE())
GROUP BY CONVERT(varchar(10), wc.CalcDate, 120)
ORDER BY data_ DESC;
GO

-- =============================================================================
-- BLOK 98 - FarmerCalc top hodowcy z linkiem do partii
-- =============================================================================

-- 98.A - Top hodowcy 90 dni z dostawami
SELECT TOP 30
    '98_A_top_hodowcy_farmer' AS __SEKCJA,
    fc.CustomerGID,
    COUNT(*) AS liczba_dostaw,
    SUM(fc.LumQnt) AS suma_sztuk,
    SUM(fc.NettoWeight) AS suma_kg_netto,
    AVG(fc.AvWeight) AS sr_waga_sztuki,
    SUM(fc.IncDeadConf) AS suma_padlych,
    AVG(fc.Loss) AS sr_loss_proc,
    AVG(fc.Price + ISNULL(fc.Addition, 0)) AS sr_cena
FROM dbo.FarmerCalc fc
WHERE fc.CalcDate >= DATEADD(DAY, -90, GETDATE())
GROUP BY fc.CustomerGID
HAVING COUNT(*) >= 3
ORDER BY suma_kg_netto DESC;
GO

-- 98.B - FarmerCalc.Partia link do listapartii (jak często zgodne)
SELECT
    '98_B_farmer_vs_listapartii' AS __SEKCJA,
    COUNT(*) AS razem,
    SUM(CASE WHEN lp.Partia IS NOT NULL THEN 1 ELSE 0 END) AS z_listapartii,
    SUM(CASE WHEN lp.Partia IS NULL THEN 1 ELSE 0 END) AS bez_listapartii
FROM dbo.FarmerCalc fc
LEFT JOIN dbo.listapartii lp ON lp.Partia = fc.Partia
WHERE fc.CalcDate >= DATEADD(DAY, -90, GETDATE());
GO

-- 98.C - Średnia waga sztuki vs deklaracja (AvWeight vs WagaDek per hodowca)
SELECT TOP 20
    '98_C_waga_realna_vs_dek' AS __SEKCJA,
    fc.CustomerGID,
    COUNT(*) AS dostawy,
    AVG(fc.AvWeight) AS sr_av_weight,
    AVG(fc.WagaDek) AS sr_waga_dek,
    AVG(fc.AvWeight - fc.WagaDek) AS roznica_kg,
    AVG((fc.AvWeight - fc.WagaDek) * 100.0 / NULLIF(fc.WagaDek, 0)) AS roznica_proc
FROM dbo.FarmerCalc fc
WHERE fc.CalcDate >= DATEADD(DAY, -90, GETDATE())
  AND fc.WagaDek > 0
GROUP BY fc.CustomerGID
HAVING COUNT(*) >= 3
ORDER BY ABS(AVG(fc.AvWeight - fc.WagaDek)) DESC;
GO

-- =============================================================================
-- BLOK 99 - Reklamacje - prawdziwe vs auto-import
-- =============================================================================

-- 99.A - Statystyki: prawdziwe vs auto-import per miesiąc
SELECT
    '99_A_reklamacje_per_miesiac' AS __SEKCJA,
    YEAR(DataZgloszenia) AS rok,
    MONTH(DataZgloszenia) AS miesiac,
    COUNT(*) AS razem,
    SUM(CASE WHEN ZrodloZgloszenia = 'Symfonia' THEN 1 ELSE 0 END) AS auto_import,
    SUM(CASE WHEN ZrodloZgloszenia <> 'Symfonia' OR ZrodloZgloszenia IS NULL THEN 1 ELSE 0 END) AS reczne,
    SUM(CASE WHEN WymagaUzupelnienia = 0 AND DecyzjaJakosci IS NOT NULL THEN 1 ELSE 0 END) AS sprawdzone
FROM dbo.Reklamacje
WHERE DataZgloszenia >= DATEADD(MONTH, -12, GETDATE())
GROUP BY YEAR(DataZgloszenia), MONTH(DataZgloszenia)
ORDER BY rok DESC, miesiac DESC;
GO

-- 99.B - Top kategorie przyczyny w prawdziwych reklamacjach
SELECT
    '99_B_kategorie_przyczyn' AS __SEKCJA,
    KategoriaPrzyczyny,
    PodkategoriaPrzyczyny,
    COUNT(*) AS razem
FROM dbo.Reklamacje
WHERE (ZrodloZgloszenia <> 'Symfonia' OR ZrodloZgloszenia IS NULL
       OR (WymagaUzupelnienia = 0 AND DecyzjaJakosci IS NOT NULL))
  AND KategoriaPrzyczyny IS NOT NULL
GROUP BY KategoriaPrzyczyny, PodkategoriaPrzyczyny
ORDER BY razem DESC;
GO

-- 99.C - Top handlowcy w prawdziwych reklamacjach
SELECT
    '99_C_handlowcy_reklamacje' AS __SEKCJA,
    Handlowiec,
    COUNT(*) AS razem,
    SUM(CASE WHEN ZrodloZgloszenia = 'Symfonia' THEN 1 ELSE 0 END) AS auto_import,
    SUM(CASE WHEN ZrodloZgloszenia <> 'Symfonia' OR ZrodloZgloszenia IS NULL THEN 1 ELSE 0 END) AS reczne,
    SUM(SumaKg) AS suma_kg,
    SUM(SumaWartosc) AS suma_wartosci
FROM dbo.Reklamacje
WHERE DataZgloszenia >= DATEADD(MONTH, -6, GETDATE())
  AND Handlowiec IS NOT NULL
GROUP BY Handlowiec
ORDER BY reczne DESC;
GO

-- =============================================================================
-- BLOK 100 - PROBLEMY I ANOMALIE (broken FK, sieroty, nieprawidłowe dane)
-- =============================================================================

-- 100.A - CreateOperator/CloseOperator w listapartii - czy istnieją w operators
SELECT
    '100_A_partie_brak_operator' AS __SEKCJA,
    'CreateOperator' AS pole,
    COUNT(*) AS razem,
    SUM(CASE WHEN op.ID IS NULL AND lp.CreateOperator IS NOT NULL AND lp.CreateOperator <> '' THEN 1 ELSE 0 END) AS sieroty
FROM dbo.listapartii lp
LEFT JOIN dbo.operators op ON CAST(op.ID AS varchar) = lp.CreateOperator
UNION ALL
SELECT
    '100_A_partie_brak_operator',
    'CloseOperator',
    COUNT(*),
    SUM(CASE WHEN op.ID IS NULL AND lp.CloseOperator IS NOT NULL AND lp.CloseOperator <> '' THEN 1 ELSE 0 END)
FROM dbo.listapartii lp
LEFT JOIN dbo.operators op ON CAST(op.ID AS varchar) = lp.CloseOperator;
GO

-- 100.B - HarmonogramDostaw bez Bufor='Potwierdzony' (czyli niezatwierdzonych)
SELECT
    '100_B_harmonogram_niezatw' AS __SEKCJA,
    Bufor,
    COUNT(*) AS razem,
    MIN(DataOdbioru) AS najstarsza,
    MAX(DataOdbioru) AS najnowsza
FROM dbo.HarmonogramDostaw
GROUP BY Bufor
ORDER BY razem DESC;
GO

-- 100.C - In0E.OperatorID nieistniejący w operators
SELECT
    '100_C_in0e_operator_sieroty' AS __SEKCJA,
    e.OperatorID,
    e.Wagowy,
    COUNT(*) AS liczba_wazen
FROM dbo.In0E e
LEFT JOIN dbo.operators op ON CAST(op.ID AS varchar) = e.OperatorID
WHERE e.Data >= CONVERT(varchar(10), DATEADD(DAY, -30, GETDATE()), 120)
  AND e.OperatorID IS NOT NULL
  AND op.ID IS NULL
GROUP BY e.OperatorID, e.Wagowy
ORDER BY liczba_wazen DESC;
GO

-- 100.D - PartiaDostawca.CustomerID bez Dostawcy w innych tabelach (sprawdzenie spójności)
SELECT
    '100_D_dostawca_overlap' AS __SEKCJA,
    'PartiaDostawca.CustomerID' AS zrodlo,
    COUNT(DISTINCT pd.CustomerID) AS unikalne_id,
    SUM(CASE WHEN d.ID IS NOT NULL THEN 1 ELSE 0 END) AS w_Dostawcy
FROM (SELECT DISTINCT CustomerID FROM dbo.PartiaDostawca) pd
LEFT JOIN dbo.Dostawcy d ON CAST(d.ID AS varchar) = pd.CustomerID;
GO

-- 100.E - Listapartii: bez wpisu w Out1A i In0E (martwe partie?)
SELECT
    '100_E_partie_bez_wazen' AS __SEKCJA,
    COUNT(*) AS razem,
    MIN(lp.CreateData) AS najstarsza,
    MAX(lp.CreateData) AS najnowsza
FROM dbo.listapartii lp
WHERE NOT EXISTS (SELECT 1 FROM dbo.In0E e WHERE e.P1 = lp.Partia AND e.ActWeight > 0)
  AND NOT EXISTS (SELECT 1 FROM dbo.Out1A o WHERE o.P1 = lp.Partia AND o.ActWeight > 0)
  AND lp.CreateData >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120);
GO

-- 100.F - ZamowieniaMieso bez ZamowieniaMiesoTowar (zamówienia bez pozycji)
SELECT
    '100_F_zamowienia_bez_pozycji' AS __SEKCJA,
    COUNT(*) AS razem,
    SUM(CASE WHEN zm.Status = 'Anulowane' THEN 1 ELSE 0 END) AS anulowane
FROM dbo.ZamowieniaMieso zm
WHERE NOT EXISTS (SELECT 1 FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId = zm.Id)
  AND zm.DataZamowienia >= DATEADD(DAY, -90, GETDATE());
GO

-- 100.G - listapartii.HarmonogramLp bez HarmonogramDostaw (broken FK)
SELECT
    '100_G_partia_bez_harmonogramu' AS __SEKCJA,
    COUNT(*) AS razem
FROM dbo.listapartii lp
WHERE lp.HarmonogramLp IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.HarmonogramDostaw h WHERE h.LP = lp.HarmonogramLp);
GO

-- =============================================================================
-- BLOK 101 - WAGOCOUNTER cross-validation z FarmerCalc
-- =============================================================================
-- WagoCounter liczy sztuki PER auto. FarmerCalc ma deklarowane (LumQnt) i policzone (Pieces).
-- Sprawdzamy zgodność.

SELECT TOP 30
    '101_A_wago_vs_farmer' AS __SEKCJA,
    wc.CalcDate,
    wc.CarLP,
    wc.Quantity AS wago_szt,
    fc.LumQnt AS farmer_dek,
    fc.Pieces AS farmer_polic,
    wc.Quantity - ISNULL(fc.Pieces, 0) AS roznica_wago_vs_farmer,
    fc.CustomerGID
FROM dbo.WagoCounter wc
LEFT JOIN dbo.FarmerCalc fc ON CAST(fc.CalcDate AS date) = CAST(wc.CalcDate AS date)
                            AND fc.CarLp = wc.CarLP
WHERE wc.CalcDate >= DATEADD(DAY, -7, GETDATE())
ORDER BY wc.CalcDate DESC, wc.CarLP DESC;
GO

-- =============================================================================
-- BLOK 102 - WIDOKI vw_QC_* - sample i czy są używane
-- =============================================================================

-- 102.A - vw_QC_Podsum sample (z linkami do listapartii)
SELECT TOP 20
    '102_A_qc_podsum_sample' AS __SEKCJA,
    qcp.*,
    lp.IsClose,
    lp.CreateData,
    pd.CustomerName
FROM dbo.vw_QC_Podsum qcp
LEFT JOIN dbo.listapartii lp ON lp.Partia = qcp.PartiaId
LEFT JOIN dbo.PartiaDostawca pd ON pd.Partia = qcp.PartiaId
ORDER BY 1 DESC;
GO

-- 102.B - vw_QC_TempSummary sample
SELECT TOP 20
    '102_B_qc_temp_sample' AS __SEKCJA, *
FROM dbo.vw_QC_TempSummary
ORDER BY 1 DESC;
GO

-- 102.C - vw_QC_WadySkale sample
SELECT TOP 20
    '102_C_qc_wadyskale_sample' AS __SEKCJA, *
FROM dbo.vw_QC_WadySkale
ORDER BY 1 DESC;
GO

-- =============================================================================
-- BLOK 103 - Operator activity - kto realnie używa systemu
-- =============================================================================

-- 103.A - Top operatorzy ważenia (In0E) ostatnie 90 dni
SELECT TOP 20
    '103_A_top_operatorzy_wazenia' AS __SEKCJA,
    e.OperatorID,
    e.Wagowy,
    COUNT(*) AS liczba_wazen,
    SUM(CASE WHEN e.ActWeight > 0 THEN 1 ELSE 0 END) AS prawidlowe,
    SUM(CASE WHEN e.ActWeight < 0 THEN 1 ELSE 0 END) AS storno,
    SUM(e.ActWeight) AS suma_kg,
    MIN(e.Data) AS pierwszy_dzien,
    MAX(e.Data) AS ostatni_dzien
FROM dbo.In0E e
WHERE e.Data >= CONVERT(varchar(10), DATEADD(DAY, -90, GETDATE()), 120)
  AND e.OperatorID IS NOT NULL
GROUP BY e.OperatorID, e.Wagowy
ORDER BY liczba_wazen DESC;
GO

-- 103.B - Top operatorzy zmian zamowien (HistoriaZmianZamowien)
SELECT TOP 20
    '103_B_top_operatorzy_zamowienia' AS __SEKCJA,
    Uzytkownik,
    UzytkownikNazwa,
    COUNT(*) AS liczba_zmian,
    COUNT(DISTINCT ZamowienieId) AS unikalnych_zamowien,
    MAX(DataZmiany) AS ostatnia_aktywnosc
FROM dbo.HistoriaZmianZamowien
WHERE DataZmiany >= DATEADD(DAY, -90, GETDATE())
GROUP BY Uzytkownik, UzytkownikNazwa
ORDER BY liczba_zmian DESC;
GO

-- 103.C - Aktywnosc per user (telemetria z 'Aktywnosc')
SELECT TOP 20
    '103_C_aktywnosc_per_user' AS __SEKCJA,
    a.KtoStworzyl,
    op.Name AS operator_imie,
    COUNT(*) AS liczba_aktywnosci,
    MIN(a.Data) AS pierwsza,
    MAX(a.Data) AS ostatnia
FROM dbo.Aktywnosc a
LEFT JOIN dbo.operators op ON op.ID = a.KtoStworzyl
WHERE a.Data >= DATEADD(DAY, -30, GETDATE())
GROUP BY a.KtoStworzyl, op.Name
ORDER BY liczba_aktywnosci DESC;
GO

PRINT '################################################################';
PRINT '## EKSPLORACJA ZALEZNOSCI - KONIEC                             ##';
PRINT '################################################################';
GO
