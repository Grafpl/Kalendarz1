-- ═══════════════════════════════════════════════════════════════════════════
-- DIAGNOZA: Dlaczego pojazd nie ma kursow w mapie GPS?
-- ═══════════════════════════════════════════════════════════════════════════
-- Uruchom w SSMS na 192.168.0.109 (dowolna baza — script uzywa pelnych nazw).
-- Wynik dla kazdej sekcji wklej do chatu.
--
-- USTAWIENIA — zmien tylko ponizej:
DECLARE @PojazdID INT = 11;        -- ← TUTAJ wpisz PojazdID z popup
DECLARE @DniWstecz INT = 90;        -- ← okno historii (90 default)
-- ═══════════════════════════════════════════════════════════════════════════

SET NOCOUNT ON;
PRINT '╔══════════════════════════════════════════════════════════════════╗';
PRINT '║   DIAGNOZA POJAZDU — TransportPL + LibraNet (.109)               ║';
PRINT CONCAT('║   PojazdID = ', @PojazdID, '  ·  okno: ', @DniWstecz, ' dni                              ║');
PRINT CONCAT('║   data uruchomienia: ', CONVERT(varchar, GETDATE(), 120), '                ║');
PRINT '╚══════════════════════════════════════════════════════════════════╝';

-- ════════════════════════════════════════════════════════════════════
-- [1] CZY POJAZD ISTNIEJE W TransportPL.Pojazd?
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '── [1] TransportPL.Pojazd — istnieje? ─────────────────────────────';
SELECT
    PojazdID,
    Rejestracja,
    Marka,
    Model,
    PaletyH1,
    Aktywny,
    LibraNetCarTrailerID,
    UtworzonoUTC,
    ZmienionoUTC
FROM TransportPL.dbo.Pojazd
WHERE PojazdID = @PojazdID;

-- ════════════════════════════════════════════════════════════════════
-- [2] CZY POJAZD JEST W WebfleetVehicleMapping?
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '── [2] TransportPL.WebfleetVehicleMapping — Webfleet ↔ TransportPL ─';
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'WebfleetVehicleMapping')
    SELECT
        m.WebfleetObjectNo,
        m.WebfleetObjectName,
        m.PojazdID,
        m.CreatedAtUTC,
        m.ModifiedAtUTC,
        m.ModifiedBy,
        p.Rejestracja AS Pojazd_Rejestracja,
        p.Aktywny    AS Pojazd_Aktywny
    FROM TransportPL.dbo.WebfleetVehicleMapping m
    LEFT JOIN TransportPL.dbo.Pojazd p ON p.PojazdID = m.PojazdID
    WHERE m.PojazdID = @PojazdID;
ELSE
    PRINT '⚠ TABELA WebfleetVehicleMapping NIE ISTNIEJE — pierwszy raz uruchomi sie po user-action.';

-- ════════════════════════════════════════════════════════════════════
-- [3] WSZYSTKIE KURSY DLA TEGO POJAZDU (bez ograniczenia daty)
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '── [3] TransportPL.Kurs — WSZYSTKIE kursy dla tego pojazdu ────────';
SELECT
    k.KursID,
    k.DataKursu,
    k.GodzWyjazdu,
    k.GodzPowrotu,
    k.Status,
    k.Trasa,
    k.PojazdID,
    k.KierowcaID,
    CONCAT(ki.Imie, ' ', ki.Nazwisko) AS Kierowca,
    DATEDIFF(DAY, k.DataKursu, CAST(GETDATE() AS DATE)) AS Dni_temu
FROM TransportPL.dbo.Kurs k
LEFT JOIN TransportPL.dbo.Kierowca ki ON ki.KierowcaID = k.KierowcaID
WHERE k.PojazdID = @PojazdID
ORDER BY k.DataKursu DESC, k.GodzWyjazdu DESC;

-- ════════════════════════════════════════════════════════════════════
-- [4] PODSUMOWANIE — ile kursow lacznie / w 90 dniach / dzis
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '── [4] Statystyki kursow ──────────────────────────────────────────';
SELECT
    COUNT(*) AS Wszystkie_Kursy,
    SUM(CASE WHEN k.DataKursu >= DATEADD(DAY, -@DniWstecz, CAST(GETDATE() AS DATE)) THEN 1 ELSE 0 END) AS W_oknie_90_dni,
    SUM(CASE WHEN k.DataKursu = CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS Dzisiaj,
    SUM(CASE WHEN k.DataKursu = DATEADD(DAY, -1, CAST(GETDATE() AS DATE)) THEN 1 ELSE 0 END) AS Wczoraj,
    MIN(k.DataKursu) AS Najstarszy_Kurs,
    MAX(k.DataKursu) AS Najnowszy_Kurs
FROM TransportPL.dbo.Kurs k
WHERE k.PojazdID = @PojazdID;

-- ════════════════════════════════════════════════════════════════════
-- [5] TEST DOKLADNY zapytania uzywanego w aplikacji (z aplikacji MapaFlotyView)
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '── [5] DOKLADNIE to zapytanie ktore wykonuje aplikacja (rn <= 2) ──';
WITH KursyRanked AS (
    SELECT k.KursID, k.Trasa, k.Status, k.GodzWyjazdu, k.GodzPowrotu, k.DataKursu, k.PojazdID,
           CONCAT(ki.Imie, ' ', ki.Nazwisko) AS KierowcaNazwa, p.Rejestracja,
           ROW_NUMBER() OVER (PARTITION BY k.PojazdID
                              ORDER BY k.DataKursu DESC, k.GodzWyjazdu DESC) AS rn
    FROM TransportPL.dbo.Kurs k
    LEFT JOIN TransportPL.dbo.Kierowca ki ON k.KierowcaID = ki.KierowcaID
    LEFT JOIN TransportPL.dbo.Pojazd p ON k.PojazdID = p.PojazdID
    WHERE k.PojazdID IS NOT NULL
      AND k.DataKursu >= DATEADD(DAY, -@DniWstecz, CAST(GETDATE() AS DATE))
)
SELECT kr.*,
       (SELECT TOP 1 l.KodKlienta FROM TransportPL.dbo.Ladunek l
        WHERE l.KursID = kr.KursID ORDER BY l.Kolejnosc DESC) AS OstatniKodKlienta
FROM KursyRanked kr
WHERE kr.PojazdID = @PojazdID
  AND kr.rn <= 2
ORDER BY kr.rn;

-- ════════════════════════════════════════════════════════════════════
-- [6] LADUNKI dla najnowszego kursu (klient + awizacja z LibraNet)
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '── [6] Ladunki ostatniego kursu + awizacja z LibraNet ─────────────';
DECLARE @LastKursID BIGINT;
SELECT TOP 1 @LastKursID = KursID
FROM TransportPL.dbo.Kurs
WHERE PojazdID = @PojazdID
ORDER BY DataKursu DESC, GodzWyjazdu DESC;

IF @LastKursID IS NOT NULL
BEGIN
    PRINT CONCAT('Ostatni KursID: ', @LastKursID);
    SELECT
        l.LadunekID,
        l.Kolejnosc,
        l.KodKlienta,
        l.PojemnikiE2,
        l.PaletyH1,
        l.TrybE2,
        l.Uwagi,
        TRY_CAST(SUBSTRING(l.KodKlienta, 5, 20) AS INT) AS ZamId_z_KodKlienta,
        zm.Id            AS ZamowieniaMieso_Id,
        zm.DataPrzyjazdu AS GodzinaAwizacji,
        zm.KlientId      AS LibraNet_KlientId,
        zm.IloscKg       AS Zamowienie_IloscKg
    FROM TransportPL.dbo.Ladunek l
    LEFT JOIN LibraNet.dbo.ZamowieniaMieso zm
        ON zm.Id = TRY_CAST(SUBSTRING(l.KodKlienta, 5, 20) AS INT)
        AND l.KodKlienta LIKE 'ZAM[_]%'
    WHERE l.KursID = @LastKursID
    ORDER BY l.Kolejnosc;
END
ELSE PRINT 'Brak kursow do pokazania ladunkow.';

-- ════════════════════════════════════════════════════════════════════
-- [7] WSZYSTKIE pojazdy w TransportPL — moze EBR1E50 jest pod innym ID?
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '── [7] Wszystkie pojazdy w TransportPL.Pojazd (sortowane po ID) ───';
SELECT
    PojazdID,
    Rejestracja,
    Marka + ' ' + ISNULL(Model, '') AS Model,
    Aktywny,
    (SELECT COUNT(*) FROM TransportPL.dbo.Kurs WHERE PojazdID = p.PojazdID) AS Liczba_Kursow_Lacznie,
    (SELECT MAX(DataKursu) FROM TransportPL.dbo.Kurs WHERE PojazdID = p.PojazdID) AS Ostatni_Kurs_Data
FROM TransportPL.dbo.Pojazd p
ORDER BY PojazdID;

-- ════════════════════════════════════════════════════════════════════
-- [8] OSTATNIE 10 KURSOW W BAZIE (cala flota, dowolny pojazd)
-- ════════════════════════════════════════════════════════════════════
PRINT '';
PRINT '── [8] Ostatnie 10 kursow w bazie (cala flota) ────────────────────';
SELECT TOP 10
    k.KursID,
    k.DataKursu,
    k.GodzWyjazdu,
    k.PojazdID,
    p.Rejestracja,
    k.Trasa,
    k.Status,
    CONCAT(ki.Imie, ' ', ki.Nazwisko) AS Kierowca
FROM TransportPL.dbo.Kurs k
LEFT JOIN TransportPL.dbo.Pojazd p ON p.PojazdID = k.PojazdID
LEFT JOIN TransportPL.dbo.Kierowca ki ON ki.KierowcaID = k.KierowcaID
ORDER BY k.DataKursu DESC, k.GodzWyjazdu DESC;

PRINT '';
PRINT '════════════════════════════════════════════════════════════════════';
PRINT '  KONIEC RAPORTU.  Skopiuj wszystkie wyniki i wklej do chatu.';
PRINT '════════════════════════════════════════════════════════════════════';
