-- ============================================================================
-- Dashboard Przychod Zywca LIVE - historia zmian wag i sztuk DEKLAROWANYCH
-- Zrodlo: dbo.FarmerCalcChangeLog (audit log FarmerCalc)
-- Filtruje 3 pola interesujace dla planowania:
--   * Szt.Dek               - sztuki deklarowane przez hodowce
--   * Waga Brutto Hodowca   - waga brutto deklarowana
--   * Waga Tara Hodowca     - tara deklarowana
--
-- Parametry: @Data (DATE) - data dostawy (CalcDate)
-- Sortowanie: najnowsze na gorze (ChangedAt DESC)
-- Limit: TOP 100 - sidebar nie powinien byc przeladowany
-- ============================================================================

SELECT TOP 100
    ChangedAt,
    ISNULL(Dostawca, '?') AS Hodowca,
    FieldName,
    ISNULL(OldValue, '') AS OldValue,
    ISNULL(NewValue, '') AS NewValue,
    ISNULL(UserName, '') AS UserName
FROM dbo.FarmerCalcChangeLog
WHERE CalcDate = @Data
  AND FieldName IN (N'Szt.Dek', N'Waga Brutto Hodowca', N'Waga Tara Hodowca')
ORDER BY ChangedAt DESC;
