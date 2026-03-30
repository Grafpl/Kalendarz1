-- =====================================================================
-- SPRAWDZENIE DAMAK — porownanie logiki Symfonia vs nasz modul
-- Uruchom w SSMS na 192.168.0.112 (baza Handel)
-- =====================================================================

-- 1. Znajdz ID kontrahenta DAMAK
SELECT id, Shortcut, Name FROM [SSCommon].[STContractors] WITH (NOLOCK)
WHERE Shortcut LIKE '%Damak%' OR Name LIKE '%Damak%';

-- 2. Wszystkie dokumenty DAMAK z magazynu 65559 (ostatnie 3 miesiace)
-- Pokaz surowe dane: typ_dk, kod dokumentu, data, pozycje z ilosciami
SELECT
    MG.typ_dk AS Typ,
    MG.kod AS NrDokumentu,
    MG.data AS Data,
    MZ.kod AS TowarKod,
    MZ.Ilosc AS IloscSurowa,
    CASE WHEN MZ.Ilosc > 0 THEN 'WYDANIE (+)' ELSE 'PRZYJECIE (-)' END AS Kierunek
FROM [HM].[MG] MG WITH (NOLOCK)
INNER JOIN [HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
WHERE MG.khid = (SELECT TOP 1 id FROM [SSCommon].[STContractors] WHERE Shortcut LIKE '%Damak%')
  AND MG.magazyn = 65559
  AND MG.anulowany = 0
  AND MG.typ_dk IN ('MW1', 'MP')
  AND MZ.kod IN ('Pojemnik drobiowy E2', 'PALETA H1', 'PALETA EURO', 'PALETA PLASTIKOWA', 'Paleta Drewniana')
  AND MG.data >= DATEADD(MONTH, -3, GETDATE())
ORDER BY MG.data DESC, MG.typ_dk;

-- 3. Saldo DAMAK metoda Symfonia (per dokument, jak w skrypcie AmBASIC)
-- Skrypt robi: jesli ilosc > 0 → wydano, jesli < 0 → przyjeto
-- saldo = -(wydano - przyjeto) per dokument
SELECT
    MG.kod AS NrDokumentu,
    MG.data AS Data,
    MG.typ_dk AS Typ,
    -- Surowa suma ilosci per towar
    SUM(CASE WHEN MZ.kod = 'Pojemnik drobiowy E2' AND MZ.Ilosc > 0 THEN MZ.Ilosc ELSE 0 END) AS E2_wydano,
    SUM(CASE WHEN MZ.kod = 'Pojemnik drobiowy E2' AND MZ.Ilosc < 0 THEN -MZ.Ilosc ELSE 0 END) AS E2_przyjeto,
    -- Saldo wg Symfonia: -(wydano - przyjeto)
    -(SUM(CASE WHEN MZ.kod = 'Pojemnik drobiowy E2' AND MZ.Ilosc > 0 THEN MZ.Ilosc ELSE 0 END)
     - SUM(CASE WHEN MZ.kod = 'Pojemnik drobiowy E2' AND MZ.Ilosc < 0 THEN -MZ.Ilosc ELSE 0 END)) AS E2_saldo_symfonia,
    -- Saldo wg naszego modulu: SUM(ilosc)
    SUM(CASE WHEN MZ.kod = 'Pojemnik drobiowy E2' THEN MZ.Ilosc ELSE 0 END) AS E2_saldo_nasz
FROM [HM].[MG] MG WITH (NOLOCK)
INNER JOIN [HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
WHERE MG.khid = (SELECT TOP 1 id FROM [SSCommon].[STContractors] WHERE Shortcut LIKE '%Damak%')
  AND MG.magazyn = 65559
  AND MG.anulowany = 0
  AND MG.typ_dk IN ('MW1', 'MP')
  AND MZ.kod IN ('Pojemnik drobiowy E2', 'PALETA H1', 'PALETA EURO', 'PALETA PLASTIKOWA', 'Paleta Drewniana')
  AND MG.data >= DATEADD(MONTH, -3, GETDATE())
GROUP BY MG.kod, MG.data, MG.typ_dk
ORDER BY MG.data DESC;

-- 4. SUMA CALKOWITA DAMAK — porownanie obu metod
SELECT
    -- Metoda Symfonia: -(wydano - przyjeto)
    -(SUM(CASE WHEN MZ.kod = 'Pojemnik drobiowy E2' AND MZ.Ilosc > 0 THEN MZ.Ilosc ELSE 0 END)
     - SUM(CASE WHEN MZ.kod = 'Pojemnik drobiowy E2' AND MZ.Ilosc < 0 THEN -MZ.Ilosc ELSE 0 END)) AS E2_Symfonia,
    -(SUM(CASE WHEN MZ.kod = 'PALETA H1' AND MZ.Ilosc > 0 THEN MZ.Ilosc ELSE 0 END)
     - SUM(CASE WHEN MZ.kod = 'PALETA H1' AND MZ.Ilosc < 0 THEN -MZ.Ilosc ELSE 0 END)) AS H1_Symfonia,
    -(SUM(CASE WHEN MZ.kod = 'PALETA EURO' AND MZ.Ilosc > 0 THEN MZ.Ilosc ELSE 0 END)
     - SUM(CASE WHEN MZ.kod = 'PALETA EURO' AND MZ.Ilosc < 0 THEN -MZ.Ilosc ELSE 0 END)) AS EURO_Symfonia,
    -(SUM(CASE WHEN MZ.kod = 'PALETA PLASTIKOWA' AND MZ.Ilosc > 0 THEN MZ.Ilosc ELSE 0 END)
     - SUM(CASE WHEN MZ.kod = 'PALETA PLASTIKOWA' AND MZ.Ilosc < 0 THEN -MZ.Ilosc ELSE 0 END)) AS PCV_Symfonia,
    -(SUM(CASE WHEN MZ.kod = 'Paleta Drewniana' AND MZ.Ilosc > 0 THEN MZ.Ilosc ELSE 0 END)
     - SUM(CASE WHEN MZ.kod = 'Paleta Drewniana' AND MZ.Ilosc < 0 THEN -MZ.Ilosc ELSE 0 END)) AS DREW_Symfonia,
    -- Metoda nasza: SUM(ilosc)
    SUM(CASE WHEN MZ.kod = 'Pojemnik drobiowy E2' THEN MZ.Ilosc ELSE 0 END) AS E2_Nasz,
    SUM(CASE WHEN MZ.kod = 'PALETA H1' THEN MZ.Ilosc ELSE 0 END) AS H1_Nasz,
    SUM(CASE WHEN MZ.kod = 'PALETA EURO' THEN MZ.Ilosc ELSE 0 END) AS EURO_Nasz,
    SUM(CASE WHEN MZ.kod = 'PALETA PLASTIKOWA' THEN MZ.Ilosc ELSE 0 END) AS PCV_Nasz,
    SUM(CASE WHEN MZ.kod = 'Paleta Drewniana' THEN MZ.Ilosc ELSE 0 END) AS DREW_Nasz
FROM [HM].[MG] MG WITH (NOLOCK)
INNER JOIN [HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
WHERE MG.khid = (SELECT TOP 1 id FROM [SSCommon].[STContractors] WHERE Shortcut LIKE '%Damak%')
  AND MG.magazyn = 65559
  AND MG.anulowany = 0
  AND MG.typ_dk IN ('MW1', 'MP')
  AND MZ.kod IN ('Pojemnik drobiowy E2', 'PALETA H1', 'PALETA EURO', 'PALETA PLASTIKOWA', 'Paleta Drewniana');

-- 5. Pelne saldo DAMAK od poczatku (bez limitu dat) — to powinno byc to co widzimy w gridzie
SELECT
    SUM(CASE WHEN MZ.kod = 'Pojemnik drobiowy E2' THEN MZ.Ilosc ELSE 0 END) AS E2_SUM,
    SUM(CASE WHEN MZ.kod = 'PALETA H1' THEN MZ.Ilosc ELSE 0 END) AS H1_SUM,
    SUM(CASE WHEN MZ.kod = 'PALETA EURO' THEN MZ.Ilosc ELSE 0 END) AS EURO_SUM,
    SUM(CASE WHEN MZ.kod = 'PALETA PLASTIKOWA' THEN MZ.Ilosc ELSE 0 END) AS PCV_SUM,
    SUM(CASE WHEN MZ.kod = 'Paleta Drewniana' THEN MZ.Ilosc ELSE 0 END) AS DREW_SUM,
    -- To samo zanegowane (jak Symfonia)
    -SUM(CASE WHEN MZ.kod = 'Pojemnik drobiowy E2' THEN MZ.Ilosc ELSE 0 END) AS E2_NEG,
    -SUM(CASE WHEN MZ.kod = 'PALETA H1' THEN MZ.Ilosc ELSE 0 END) AS H1_NEG,
    -SUM(CASE WHEN MZ.kod = 'PALETA EURO' THEN MZ.Ilosc ELSE 0 END) AS EURO_NEG,
    -SUM(CASE WHEN MZ.kod = 'PALETA PLASTIKOWA' THEN MZ.Ilosc ELSE 0 END) AS PCV_NEG,
    -SUM(CASE WHEN MZ.kod = 'Paleta Drewniana' THEN MZ.Ilosc ELSE 0 END) AS DREW_NEG
FROM [HM].[MG] MG WITH (NOLOCK)
INNER JOIN [HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
WHERE MG.khid = (SELECT TOP 1 id FROM [SSCommon].[STContractors] WHERE Shortcut LIKE '%Damak%')
  AND MG.magazyn = 65559
  AND MG.anulowany = 0
  AND MG.typ_dk IN ('MW1', 'MP')
  AND MZ.kod IN ('Pojemnik drobiowy E2', 'PALETA H1', 'PALETA EURO', 'PALETA PLASTIKOWA', 'Paleta Drewniana');
