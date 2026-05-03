-- ============================================================
-- 13 — Quirki: typy kolumn Data/Godzina w roznych tabelach
-- ============================================================
USE LibraNet;
GO

-- A) Wszystkie kolumny z nazwami Data, Godzina, Czas
SELECT
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE COLUMN_NAME IN ('Data','Godzina','Czas','CreateData','CreateGodzina',
                      'DataOdbioru','DataZamowienia','DataUboju','DataProdukcji',
                      'ModificationData','ModificationGodzina','CalcDate',
                      'Wyjazd','Zaladunek','Przyjazd','DataPrzyjazdu','DataPowrotu')
ORDER BY TABLE_NAME, COLUMN_NAME;
GO

-- B) Wszystkie kolumny typu varchar zawierajace 'Data' w nazwie
SELECT
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE COLUMN_NAME LIKE '%Data%'
  AND DATA_TYPE IN ('varchar', 'nvarchar', 'char', 'nchar')
ORDER BY TABLE_NAME, COLUMN_NAME;
GO

-- C) Kolumny IsClose / Status w roznych tabelach
SELECT
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE COLUMN_NAME IN ('IsClose', 'Status', 'StatusV2', 'TransportStatus',
                      'IsActive', 'Aktywny', 'IsCancelled', 'Anulowane')
ORDER BY TABLE_NAME, COLUMN_NAME;
GO

-- D) Kolumny GUID w roznych tabelach
SELECT
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE DATA_TYPE = 'uniqueidentifier'
ORDER BY TABLE_NAME, COLUMN_NAME;
GO

-- E) Kolumny CustomerID/CustomerName (do mapowania klientow/hodowcow)
SELECT
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE COLUMN_NAME IN ('CustomerID', 'CustomerName', 'KlientId', 'KontrahentId',
                      'DostawcaId', 'OperatorID', 'KierowcaID', 'PojazdID')
ORDER BY TABLE_NAME, COLUMN_NAME;
GO
