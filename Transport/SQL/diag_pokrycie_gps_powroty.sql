-- ════════════════════════════════════════════════════════════════════════
-- DIAGNOSTYKA: pokrycie GPS klientów dla szacowania godziny powrotu (poziom A)
-- Tylko ODCZYT. Serwer 192.168.0.109 (LibraNet + TransportPL, ten sam instans).
-- Pyta: jaki % dostaw trafia do klienta ze znanymi współrzędnymi?
-- Im wyższy %, tym wiarygodniejszy szacunek EtaService (reszta = płaskie 30 min).
-- ════════════════════════════════════════════════════════════════════════

SET NOCOUNT ON;

PRINT '=== 1. KLIENCI z dostawami (ostatnie 90 dni) — pokrycie GPS w KartotekaOdbiorcyDane ===';
WITH KlienciDostaw AS (
    SELECT DISTINCT z.KlientId
    FROM LibraNet.dbo.ZamowieniaMieso z
    WHERE z.DataUboju >= DATEADD(DAY, -90, CAST(GETDATE() AS DATE))
      AND z.KlientId IS NOT NULL
)
SELECT
    COUNT(*)                                                                    AS Klienci_razem,
    SUM(CASE WHEN k.Latitude IS NOT NULL AND k.Latitude <> 0 THEN 1 ELSE 0 END) AS Klienci_z_GPS,
    CAST(100.0 * SUM(CASE WHEN k.Latitude IS NOT NULL AND k.Latitude <> 0 THEN 1 ELSE 0 END)
         / NULLIF(COUNT(*), 0) AS DECIMAL(5,1))                                  AS Proc_klientow_z_GPS
FROM KlienciDostaw d
LEFT JOIN LibraNet.dbo.KartotekaOdbiorcyDane k ON k.IdSymfonia = d.KlientId;

PRINT '';
PRINT '=== 2. DOSTAWY ważone (ostatnie 90 dni) — % dostaw do klienta ze znanym GPS ===';
SELECT
    COUNT(*)                                                                    AS Zamowien_razem,
    SUM(CASE WHEN k.Latitude IS NOT NULL AND k.Latitude <> 0 THEN 1 ELSE 0 END) AS Zamowien_z_GPS,
    CAST(100.0 * SUM(CASE WHEN k.Latitude IS NOT NULL AND k.Latitude <> 0 THEN 1 ELSE 0 END)
         / NULLIF(COUNT(*), 0) AS DECIMAL(5,1))                                  AS Proc_dostaw_z_GPS
FROM LibraNet.dbo.ZamowieniaMieso z
LEFT JOIN LibraNet.dbo.KartotekaOdbiorcyDane k ON k.IdSymfonia = z.KlientId
WHERE z.DataUboju >= DATEADD(DAY, -90, CAST(GETDATE() AS DATE))
  AND z.KlientId IS NOT NULL;

PRINT '';
PRINT '=== 3. Cache TransportPL.KlientAdres — ile wierszy i ile geokodowanych ===';
SELECT
    COUNT(*)                                                                       AS Wierszy_razem,
    SUM(CASE WHEN Latitude IS NOT NULL AND Latitude <> 0 THEN 1 ELSE 0 END)        AS Geokodowanych
FROM TransportPL.dbo.KlientAdres;

PRINT '';
PRINT '=== 4. TOP 20 klientów BEZ GPS wg liczby dostaw (kandydaci do geokodowania) ===';
SELECT TOP 20
    z.KlientId,
    COUNT(*)                       AS Liczba_dostaw_90dni
FROM LibraNet.dbo.ZamowieniaMieso z
LEFT JOIN LibraNet.dbo.KartotekaOdbiorcyDane k ON k.IdSymfonia = z.KlientId
WHERE z.DataUboju >= DATEADD(DAY, -90, CAST(GETDATE() AS DATE))
  AND z.KlientId IS NOT NULL
  AND (k.Latitude IS NULL OR k.Latitude = 0)
GROUP BY z.KlientId
ORDER BY COUNT(*) DESC;
