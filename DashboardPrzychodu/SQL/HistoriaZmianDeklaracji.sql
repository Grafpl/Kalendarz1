-- ============================================================================
-- Dashboard Przychod Zywca LIVE - historia zmian DEKLARACJI HODOWCY
-- Zrodlo: dbo.HarmonogramDostaw_ChangeLog (audit log harmonogramu, trigger AFTER UPDATE)
-- Filtruje:
--   * tylko zmiany dokonane DZISIAJ (ChangedAt >= dzisiaj 00:00)
--   * tylko zmiany dotyczące DZISIEJSZYCH dostaw (DataOdbioru = @Data)
--   * 3 typy zmian: SztukiDek (sztuki), WagaDek (srednia waga), Auta (ilosc aut)
--
-- Parametry: @Data (DATE) - data dostawy (DataOdbioru w HarmonogramDostaw)
-- Sortowanie: najnowsze na gorze (ChangedAt DESC)
-- Limit: TOP 60 - sidebar z 3 kolumnami * 20 wierszy
-- ============================================================================

SELECT TOP 60
    ChangedAt,
    ISNULL(Dostawca, '?') AS Hodowca,
    FieldName,
    ISNULL(OldValue, '') AS OldValue,
    ISNULL(NewValue, '') AS NewValue,
    ISNULL(UserName, '') AS UserName
FROM dbo.HarmonogramDostaw_ChangeLog
WHERE DataOdbioru = @Data
  AND CAST(ChangedAt AS DATE) = CAST(GETDATE() AS DATE)  -- zmiany TYLKO z dzisiaj
  AND FieldName IN (N'SztukiDek', N'WagaDek', N'Auta')
ORDER BY ChangedAt DESC;
