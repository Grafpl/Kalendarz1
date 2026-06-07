/* ============================================================================
   ODZYSKANIE UPRAWNIEŃ skasowanych przez bug ucinania stringa operators.Access
   (nowy panel WPF zapisywał 68 znaków → kasował poz. 13/14/20/21/27/28/36 i 68–81)

   Baza: LibraNet (192.168.0.109)   |   Tabela: dbo.operators.Access
   Data: 2026-06-02

   Bezpieczeństwo:
     - skrypt TYLKO USTAWIA bity na '1' (nigdy nie odbiera),
     - tylko dla userów, którzy wg dbo.PermissionAudit.OldAccess HISTORYCZNIE mieli '1'
       na danej pozycji (te 7 modułów nigdy nie było w panelu → '1' = legalne, nie pomyłka),
     - idempotentny (można puścić wielokrotnie),
     - owinięty w transakcję; na końcu weryfikacja. Najpierw URUCHOM SEKCJĘ 1 (DRY-RUN).

   Pozycje (1-based w stringu = pozycja 0-based + 1):
     14=PodsumowanieSaldOpak  15=SaldaOdbiorcowOpak  21=PrognozyUboju
     22=AnalizaTygodniowa     28=AnalizaWydajnosci   29=RezerwacjaKlas   37=AnalizaPrzychodu
   ============================================================================ */

------------------------------------------------------------------------------
-- SEKCJA 1 — DRY RUN: kogo i co przywrócimy (URUCHOM NAJPIERW, przejrzyj)
------------------------------------------------------------------------------
;WITH Poz AS (
    SELECT * FROM (VALUES
        (14,'PodsumowanieSaldOpak'),(15,'SaldaOdbiorcowOpak'),(21,'PrognozyUboju'),
        (22,'AnalizaTygodniowa'),(28,'AnalizaWydajnosci'),(29,'RezerwacjaKlas'),(37,'AnalizaPrzychodu')
    ) p(SqlPos, Modul)
),
Mieli AS (
    SELECT DISTINCT a.UserId, p.SqlPos, p.Modul
    FROM dbo.PermissionAudit a CROSS JOIN Poz p
    WHERE LEN(a.OldAccess) >= p.SqlPos AND SUBSTRING(a.OldAccess, p.SqlPos, 1) = '1'
)
SELECT m.UserId, o.Name, m.Modul, m.SqlPos
FROM Mieli m JOIN dbo.operators o ON o.ID = m.UserId
WHERE LEN(o.Access) < m.SqlPos OR SUBSTRING(o.Access, m.SqlPos, 1) = '0'
ORDER BY m.UserId, m.SqlPos;
GO

------------------------------------------------------------------------------
-- SEKCJA 2 — NAPRAWA (uruchom po akceptacji DRY-RUN)
------------------------------------------------------------------------------
BEGIN TRY
    BEGIN TRAN;

    -- KROK 0: bezpieczne dopełnienie do 82 znaków (same zera na końcu — niczego nie odbiera)
    UPDATE dbo.operators
        SET Access = LEFT(ISNULL(Access,'') + REPLICATE('0', 82), 82)
    WHERE LEN(ISNULL(Access,'')) < 82;

    -- KROK 1–7: przywróć '1' na każdej pozycji tam, gdzie historia='1' a teraz='0'
    DECLARE @pos INT;
    DECLARE pos_cur CURSOR LOCAL FAST_FORWARD FOR
        SELECT v.SqlPos FROM (VALUES (14),(15),(21),(22),(28),(29),(37)) v(SqlPos);
    OPEN pos_cur; FETCH NEXT FROM pos_cur INTO @pos;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        UPDATE o SET Access = STUFF(o.Access, @pos, 1, '1')
        FROM dbo.operators o
        WHERE SUBSTRING(o.Access, @pos, 1) = '0'
          AND EXISTS (
              SELECT 1 FROM dbo.PermissionAudit a
              WHERE a.UserId = o.ID AND LEN(a.OldAccess) >= @pos
                AND SUBSTRING(a.OldAccess, @pos, 1) = '1'
          );
        FETCH NEXT FROM pos_cur INTO @pos;
    END
    CLOSE pos_cur; DEALLOCATE pos_cur;

    -- Ślad w audycie (Source='restore:bugfix-2026-06-02')
    INSERT INTO dbo.PermissionAudit (UserId, OldAccess, NewAccess, DiffAdded, DiffRemoved, ChangedBy, Source)
    SELECT DISTINCT a.UserId, '', o.Access, 'restore phantom modules', '', 'SYSTEM',
           'restore:bugfix-2026-06-02'
    FROM dbo.PermissionAudit a JOIN dbo.operators o ON o.ID = a.UserId
    WHERE a.UserId IN ('1199','1995','2121','3131','6622','6969','8921','9741','9911');

    COMMIT;
    PRINT 'OK — uprawnienia przywrócone.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    PRINT 'BŁĄD — wycofano. ' + ERROR_MESSAGE();
END CATCH
GO

------------------------------------------------------------------------------
-- SEKCJA 3 — WERYFIKACJA: powinno zwrócić 0 wierszy
------------------------------------------------------------------------------
;WITH Poz AS (
    SELECT * FROM (VALUES (14,'PodsumowanieSaldOpak'),(15,'SaldaOdbiorcowOpak'),(21,'PrognozyUboju'),
        (22,'AnalizaTygodniowa'),(28,'AnalizaWydajnosci'),(29,'RezerwacjaKlas'),(37,'AnalizaPrzychodu')) p(SqlPos, Modul)
),
Mieli AS (
    SELECT DISTINCT a.UserId, p.SqlPos, p.Modul FROM dbo.PermissionAudit a CROSS JOIN Poz p
    WHERE LEN(a.OldAccess) >= p.SqlPos AND SUBSTRING(a.OldAccess, p.SqlPos, 1) = '1'
)
SELECT m.UserId, o.Name, m.Modul
FROM Mieli m JOIN dbo.operators o ON o.ID = m.UserId
WHERE LEN(o.Access) < m.SqlPos OR SUBSTRING(o.Access, m.SqlPos, 1) = '0'
ORDER BY m.UserId, m.Modul;
GO
