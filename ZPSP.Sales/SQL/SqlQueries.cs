namespace ZPSP.Sales.SQL
{
    /// <summary>
    /// Centralne repozytorium wszystkich zapytań SQL.
    /// Eliminuje rozrzucone inline SQL w całym kodzie.
    /// Wszystkie zapytania są parametryzowane (bezpieczeństwo SQL injection).
    /// </summary>
    public static class SqlQueries
    {
        #region Orders - LibraNet.ZamowieniaMieso

        /// <summary>
        /// Pobiera zamówienia dla wskazanej daty (z pozycjami zagregowanymi).
        /// Parametry: @Day (date)
        /// </summary>
        public const string GetOrdersForDate = @"
            SELECT
                zm.Id,
                zm.KlientId,
                SUM(ISNULL(zmt.Ilosc, 0)) AS Ilosc,
                zm.DataPrzyjazdu,
                zm.DataUtworzenia,
                zm.IdUser,
                zm.Status,
                zm.LiczbaPojemnikow,
                zm.LiczbaPalet,
                zm.TrybE2,
                zm.Uwagi,
                zm.TransportKursID,
                CAST(CASE WHEN EXISTS(
                    SELECT 1 FROM dbo.ZamowieniaMiesoTowar
                    WHERE ZamowienieId = zm.Id AND Folia = 1
                ) THEN 1 ELSE 0 END AS BIT) AS MaFolie,
                CAST(CASE WHEN EXISTS(
                    SELECT 1 FROM dbo.ZamowieniaMiesoTowar
                    WHERE ZamowienieId = zm.Id AND Hallal = 1
                ) THEN 1 ELSE 0 END AS BIT) AS MaHallal,
                CAST(CASE WHEN NOT EXISTS(
                    SELECT 1 FROM dbo.ZamowieniaMiesoTowar
                    WHERE ZamowienieId = zm.Id AND (Cena IS NULL OR Cena = '' OR Cena = '0')
                ) AND EXISTS(
                    SELECT 1 FROM dbo.ZamowieniaMiesoTowar WHERE ZamowienieId = zm.Id
                ) THEN 1 ELSE 0 END AS BIT) AS CzyMaCeny,
                ISNULL(zm.CzyZrealizowane, 0) AS CzyZrealizowane,
                zm.DataWydania,
                zm.DataUboju,
                ISNULL(zm.Waluta, 'PLN') AS Waluta
            FROM dbo.ZamowieniaMieso zm
            LEFT JOIN dbo.ZamowieniaMiesoTowar zmt ON zm.Id = zmt.ZamowienieId
            WHERE zm.DataUboju = @Day
            GROUP BY zm.Id, zm.KlientId, zm.DataPrzyjazdu, zm.DataUtworzenia, zm.IdUser, zm.Status,
                     zm.LiczbaPojemnikow, zm.LiczbaPalet, zm.TrybE2, zm.Uwagi, zm.TransportKursID,
                     zm.CzyZrealizowane, zm.DataWydania, zm.DataUboju, zm.Waluta
            ORDER BY zm.Id";

        /// <summary>
        /// Pobiera zamówienia dla wskazanej daty z filtrem po produkcie.
        /// Parametry: @Day (date), @ProductId (int)
        /// </summary>
        public const string GetOrdersForDateWithProduct = @"
            SELECT
                zm.Id,
                zm.KlientId,
                SUM(ISNULL(zmt.Ilosc, 0)) AS Ilosc,
                zm.DataPrzyjazdu,
                zm.DataUtworzenia,
                zm.IdUser,
                zm.Status,
                zm.LiczbaPojemnikow,
                zm.LiczbaPalet,
                zm.TrybE2,
                zm.Uwagi,
                zm.TransportKursID,
                CAST(CASE WHEN EXISTS(
                    SELECT 1 FROM dbo.ZamowieniaMiesoTowar
                    WHERE ZamowienieId = zm.Id AND Folia = 1
                ) THEN 1 ELSE 0 END AS BIT) AS MaFolie,
                CAST(CASE WHEN EXISTS(
                    SELECT 1 FROM dbo.ZamowieniaMiesoTowar
                    WHERE ZamowienieId = zm.Id AND Hallal = 1
                ) THEN 1 ELSE 0 END AS BIT) AS MaHallal,
                CAST(CASE WHEN NOT EXISTS(
                    SELECT 1 FROM dbo.ZamowieniaMiesoTowar
                    WHERE ZamowienieId = zm.Id AND (Cena IS NULL OR Cena = '' OR Cena = '0')
                ) AND EXISTS(
                    SELECT 1 FROM dbo.ZamowieniaMiesoTowar WHERE ZamowienieId = zm.Id
                ) THEN 1 ELSE 0 END AS BIT) AS CzyMaCeny,
                ISNULL(zm.CzyZrealizowane, 0) AS CzyZrealizowane,
                zm.DataWydania,
                zm.DataUboju,
                ISNULL(zm.Waluta, 'PLN') AS Waluta
            FROM dbo.ZamowieniaMieso zm
            LEFT JOIN dbo.ZamowieniaMiesoTowar zmt ON zm.Id = zmt.ZamowienieId
            WHERE zm.DataUboju = @Day
              AND (zmt.KodTowaru = @ProductId OR zmt.KodTowaru IS NULL)
            GROUP BY zm.Id, zm.KlientId, zm.DataPrzyjazdu, zm.DataUtworzenia, zm.IdUser, zm.Status,
                     zm.LiczbaPojemnikow, zm.LiczbaPalet, zm.TrybE2, zm.Uwagi, zm.TransportKursID,
                     zm.CzyZrealizowane, zm.DataWydania, zm.DataUboju, zm.Waluta
            ORDER BY zm.Id";

        /// <summary>
        /// Pobiera pozycje zamówienia.
        /// Parametry: @OrderId (int)
        /// </summary>
        public const string GetOrderItems = @"
            SELECT
                ZamowienieId,
                KodTowaru,
                Ilosc,
                ISNULL(Cena, '0') AS Cena,
                ISNULL(Pojemniki, 0) AS Pojemniki,
                ISNULL(Palety, 0) AS Palety,
                ISNULL(E2, 0) AS E2,
                ISNULL(Folia, 0) AS Folia,
                ISNULL(Hallal, 0) AS Hallal
            FROM dbo.ZamowieniaMiesoTowar
            WHERE ZamowienieId = @OrderId";

        /// <summary>
        /// Pobiera pozycje dla wielu zamówień (batch loading - eliminuje N+1).
        /// Parametry: @OrderIds (string - lista ID oddzielona przecinkami)
        /// </summary>
        public const string GetOrderItemsBatch = @"
            SELECT
                ZamowienieId,
                KodTowaru,
                SUM(Ilosc) AS Ilosc
            FROM dbo.ZamowieniaMiesoTowar
            WHERE ZamowienieId IN (SELECT value FROM STRING_SPLIT(@OrderIds, ','))
            GROUP BY ZamowienieId, KodTowaru";

        /// <summary>
        /// Pobiera średnią ważoną cenę dla zamówień.
        /// Parametry: @OrderIds (string)
        /// </summary>
        public const string GetAveragePricesForOrders = @"
            SELECT
                ZamowienieId,
                CASE WHEN SUM(Ilosc) > 0
                     THEN SUM(Ilosc * TRY_CAST(Cena AS DECIMAL(18,2))) / SUM(Ilosc)
                     ELSE 0 END AS SredniaCena
            FROM dbo.ZamowieniaMiesoTowar
            WHERE ZamowienieId IN (SELECT value FROM STRING_SPLIT(@OrderIds, ','))
              AND Cena IS NOT NULL AND Cena <> '' AND Cena <> '0'
            GROUP BY ZamowienieId";

        /// <summary>
        /// Aktualizuje uwagi zamówienia.
        /// Parametry: @Id (int), @Uwagi (string)
        /// </summary>
        public const string UpdateOrderNotes = @"
            UPDATE dbo.ZamowieniaMieso
            SET Uwagi = @Uwagi
            WHERE Id = @Id";

        /// <summary>
        /// Anuluje zamówienie.
        /// Parametry: @Id (int), @AnulowanePrzez (string), @PrzyczynaAnulowania (string)
        /// </summary>
        public const string CancelOrder = @"
            UPDATE dbo.ZamowieniaMieso
            SET Status = 'Anulowane',
                AnulowanePrzez = @AnulowanePrzez,
                DataAnulowania = GETDATE(),
                PrzyczynaAnulowania = @PrzyczynaAnulowania
            WHERE Id = @Id";

        /// <summary>
        /// Przywraca anulowane zamówienie.
        /// Parametry: @Id (int)
        /// </summary>
        public const string RestoreOrder = @"
            UPDATE dbo.ZamowieniaMieso
            SET Status = 'Nowe',
                AnulowanePrzez = NULL,
                DataAnulowania = NULL,
                PrzyczynaAnulowania = NULL
            WHERE Id = @Id";

        /// <summary>
        /// Pobiera ID klientów z zamówieniami na dany dzień.
        /// Parametry: @Day (date)
        /// </summary>
        public const string GetClientIdsWithOrders = @"
            SELECT DISTINCT KlientId
            FROM dbo.ZamowieniaMieso
            WHERE DataUboju = @Day AND KlientId IS NOT NULL";

        #endregion

        #region Customers - Handel.STContractors

        /// <summary>
        /// Pobiera wszystkich kontrahentów z handlowcami.
        /// </summary>
        public const string GetAllCustomers = @"
            SELECT
                c.Id,
                c.Shortcut,
                c.Name1 AS Nazwa,
                c.NIP,
                c.Street AS Adres,
                c.City AS Miasto,
                c.PostalCode AS KodPocztowy,
                c.Phone AS Telefon,
                c.Email,
                wym.CDim_Handlowiec_Val AS Handlowiec
            FROM [HANDEL].[SSCommon].[STContractors] c
            LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] wym ON c.Id = wym.ElementId";

        /// <summary>
        /// Pobiera kontrahentów jako słownik (Id -> Shortcut, Handlowiec).
        /// Optymalizacja dla cache.
        /// </summary>
        public const string GetCustomersLookup = @"
            SELECT
                c.Id,
                c.Shortcut,
                wym.CDim_Handlowiec_Val AS Handlowiec
            FROM [HANDEL].[SSCommon].[STContractors] c
            LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] wym ON c.Id = wym.ElementId";

        /// <summary>
        /// Pobiera kontrahenta po ID.
        /// Parametry: @Id (int)
        /// </summary>
        public const string GetCustomerById = @"
            SELECT
                c.Id,
                c.Shortcut,
                c.Name1 AS Nazwa,
                c.NIP,
                c.Street AS Adres,
                c.City AS Miasto,
                c.PostalCode AS KodPocztowy,
                c.Phone AS Telefon,
                c.Email,
                wym.CDim_Handlowiec_Val AS Handlowiec
            FROM [HANDEL].[SSCommon].[STContractors] c
            LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] wym ON c.Id = wym.ElementId
            WHERE c.Id = @Id";

        #endregion

        #region Products - Handel.TW

        /// <summary>
        /// Pobiera produkty z katalogów 67095 (Kurczak A) i 67153 (Kurczak B).
        /// </summary>
        public const string GetMeatProducts = @"
            SELECT
                ID AS Id,
                kod AS Kod,
                nazwa AS Nazwa,
                katalog AS Katalog,
                jm AS JM
            FROM [HANDEL].[HM].[TW]
            WHERE katalog IN (67095, 67153)
            ORDER BY katalog, kod";

        /// <summary>
        /// Pobiera produkt po ID.
        /// Parametry: @Id (int)
        /// </summary>
        public const string GetProductById = @"
            SELECT
                ID AS Id,
                kod AS Kod,
                nazwa AS Nazwa,
                katalog AS Katalog,
                jm AS JM
            FROM [HANDEL].[HM].[TW]
            WHERE ID = @Id";

        /// <summary>
        /// Pobiera nazwy produktów dla listy ID.
        /// Parametry: @Ids (string - lista ID)
        /// </summary>
        public const string GetProductNames = @"
            SELECT ID, kod AS Kod
            FROM [HANDEL].[HM].[TW]
            WHERE ID IN (SELECT value FROM STRING_SPLIT(@Ids, ','))";

        #endregion

        #region Releases - Handel.MZ/MG (Wydania WZ)

        /// <summary>
        /// Pobiera wydania (WZ) per klient per produkt na dany dzień.
        /// Parametry: @Day (date), @ProductIds (string - lista ID)
        /// </summary>
        public const string GetReleasesPerClientProduct = @"
            SELECT
                MG.khid AS KlientId,
                MZ.idtw AS ProduktId,
                SUM(ABS(MZ.ilosc)) AS Ilosc
            FROM [HANDEL].[HM].[MZ] MZ
            JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id
            WHERE MG.seria IN ('sWZ', 'sWZ-W')
              AND MG.aktywny = 1
              AND MG.data = @Day
              AND MG.khid IS NOT NULL
              AND MZ.idtw IN (SELECT value FROM STRING_SPLIT(@ProductIds, ','))
            GROUP BY MG.khid, MZ.idtw";

        /// <summary>
        /// Pobiera sumę wydań per produkt na dany dzień.
        /// Parametry: @Day (date)
        /// </summary>
        public const string GetReleasesTotalPerProduct = @"
            SELECT
                MZ.idtw AS ProduktId,
                SUM(ABS(MZ.ilosc)) AS Ilosc
            FROM [HANDEL].[HM].[MZ] MZ
            JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id
            WHERE MG.seria IN ('sWZ', 'sWZ-W')
              AND MG.aktywny = 1
              AND MG.data = @Day
            GROUP BY MZ.idtw";

        #endregion

        #region Income - Handel.MZ/MG (Przychody PWP)

        /// <summary>
        /// Pobiera faktyczny przychód produkcji (PWP) per produkt na dany dzień.
        /// Parametry: @Day (date), @ProductIds (string - lista ID)
        /// </summary>
        public const string GetActualIncomePerProduct = @"
            SELECT
                MZ.idtw AS ProduktId,
                SUM(ABS(MZ.ilosc)) AS Ilosc
            FROM [HANDEL].[HM].[MZ] MZ
            JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id
            WHERE MG.seria IN ('sPWP', 'PWP')
              AND MG.aktywny = 1
              AND MG.data = @Day
              AND MZ.idtw IN (SELECT value FROM STRING_SPLIT(@ProductIds, ','))
            GROUP BY MZ.idtw";

        #endregion

        #region Aggregation - Optymalizowane agregaty

        /// <summary>
        /// Pobiera podsumowanie zamówień per produkt na dany dzień (w jednym zapytaniu!).
        /// Parametry: @Day (date)
        /// </summary>
        public const string GetOrderSummaryPerProduct = @"
            SELECT
                t.KodTowaru AS ProduktId,
                SUM(t.Ilosc) AS SumaZamowien,
                COUNT(DISTINCT z.KlientId) AS LiczbaKlientow
            FROM dbo.ZamowieniaMiesoTowar t
            JOIN dbo.ZamowieniaMieso z ON t.ZamowienieId = z.Id
            WHERE z.DataUboju = @Day
              AND (z.Status IS NULL OR z.Status <> 'Anulowane')
            GROUP BY t.KodTowaru";

        /// <summary>
        /// Pobiera pełne dane dla dashboardu w jednym zapytaniu.
        /// Parametry: @Day (date)
        /// </summary>
        public const string GetDashboardSummary = @"
            SELECT
                SUM(ISNULL(t.Ilosc, 0)) AS SumaZamowien,
                COUNT(DISTINCT z.Id) AS LiczbaZamowien,
                COUNT(DISTINCT z.KlientId) AS LiczbaKlientow,
                SUM(ISNULL(z.LiczbaPalet, 0)) AS SumaPalet,
                SUM(CASE WHEN z.Status = 'Anulowane' THEN 1 ELSE 0 END) AS LiczbaAnulowanych
            FROM dbo.ZamowieniaMieso z
            LEFT JOIN dbo.ZamowieniaMiesoTowar t ON z.Id = t.ZamowienieId
            WHERE z.DataUboju = @Day";

        #endregion

        #region Transport - TransportPL

        /// <summary>
        /// Pobiera kursy transportowe na dany dzień.
        /// Parametry: @Day (date)
        /// </summary>
        public const string GetTransportCoursesForDate = @"
            SELECT
                k.KursID,
                k.DataKursu,
                k.Trasa,
                k.GodzWyjazdu,
                k.GodzPowrotu,
                k.Status,
                k.KierowcaID,
                CONCAT(ki.Imie, ' ', ki.Nazwisko) AS Kierowca,
                ki.Telefon AS TelefonKierowcy,
                k.PojazdID,
                p.Rejestracja,
                p.Marka AS MarkaPojazdu,
                p.Model AS ModelPojazdu,
                ISNULL(p.PaletyH1, 33) AS MaxPalety,
                k.Uwagi
            FROM dbo.Kurs k
            LEFT JOIN dbo.Kierowca ki ON k.KierowcaID = ki.KierowcaID
            LEFT JOIN dbo.Pojazd p ON k.PojazdID = p.PojazdID
            WHERE k.DataKursu = @Day
            ORDER BY k.GodzWyjazdu";

        /// <summary>
        /// Pobiera kurs po ID.
        /// Parametry: @KursId (long)
        /// </summary>
        public const string GetTransportCourseById = @"
            SELECT
                k.KursID,
                k.DataKursu,
                k.Trasa,
                k.GodzWyjazdu,
                k.GodzPowrotu,
                k.Status,
                CONCAT(ki.Imie, ' ', ki.Nazwisko) AS Kierowca,
                ki.Telefon AS TelefonKierowcy,
                p.Rejestracja,
                p.Marka AS MarkaPojazdu,
                p.Model AS ModelPojazdu,
                ISNULL(p.PaletyH1, 33) AS MaxPalety
            FROM dbo.Kurs k
            LEFT JOIN dbo.Kierowca ki ON k.KierowcaID = ki.KierowcaID
            LEFT JOIN dbo.Pojazd p ON k.PojazdID = p.PojazdID
            WHERE k.KursID = @KursId";

        /// <summary>
        /// Pobiera ładunki dla kursu.
        /// Parametry: @KursId (long)
        /// </summary>
        public const string GetLoadsForCourse = @"
            SELECT
                l.LadunekID AS LadunekId,
                l.KursID AS KursId,
                l.Kolejnosc,
                l.KodKlienta,
                l.PaletyH1 AS Palety,
                l.PojemnikiE2 AS Pojemniki,
                l.Uwagi
            FROM dbo.Ladunek l
            WHERE l.KursID = @KursId
            ORDER BY l.Kolejnosc";

        /// <summary>
        /// Pobiera ID kursów przypisanych do zamówień na dany dzień.
        /// Parametry: @Day (date)
        /// </summary>
        public const string GetCourseIdsForOrdersOnDate = @"
            SELECT DISTINCT TransportKursID
            FROM dbo.ZamowieniaMieso
            WHERE DataUboju = @Day
              AND TransportKursID IS NOT NULL";

        #endregion

        #region Configuration - LibraNet

        /// <summary>
        /// Pobiera konfigurację produktów na dany dzień.
        /// Parametry: @Day (date)
        /// </summary>
        public const string GetProductConfiguration = @"
            SELECT
                ProduktId,
                ProcentUdzialu,
                GrupaScalania,
                Kolejnosc
            FROM dbo.KonfiguracjaProdukty
            WHERE DataOd <= @Day AND (DataDo IS NULL OR DataDo >= @Day)
            ORDER BY Kolejnosc";

        /// <summary>
        /// Pobiera konfigurację wydajności na dany dzień.
        /// Parametry: @Day (date)
        /// </summary>
        public const string GetYieldConfiguration = @"
            SELECT TOP 1
                Wspolczynnik,
                ProcentA,
                ProcentB
            FROM dbo.KonfiguracjaWydajnosc
            WHERE DataOd <= @Day AND (DataDo IS NULL OR DataDo >= @Day)
            ORDER BY DataOd DESC";

        /// <summary>
        /// Pobiera mapowanie scalania produktów w grupy.
        /// </summary>
        public const string GetProductGroupMapping = @"
            SELECT
                ProduktId,
                NazwaGrupy
            FROM dbo.MapowanieScalowania
            WHERE Aktywne = 1";

        /// <summary>
        /// Pobiera stany magazynowe na dany dzień.
        /// Parametry: @Day (date)
        /// </summary>
        public const string GetInventoryStocks = @"
            SELECT ProduktId, Stan
            FROM dbo.StanyMagazynowe
            WHERE Data = @Day";

        /// <summary>
        /// Pobiera masę z harmonogramu dostaw na dany dzień.
        /// Parametry: @Day (date)
        /// </summary>
        public const string GetScheduledMass = @"
            SELECT SUM(WagaDek * SztukiDek) AS Masa
            FROM dbo.HarmonogramDostaw
            WHERE DataOdbioru = @Day
              AND Bufor IN ('B.Wolny', 'B.Kontr.', 'Potwierdzony')";

        #endregion

        #region History - HistoriaZmianZamowien

        /// <summary>
        /// Pobiera historię zmian dla zamówienia.
        /// Parametry: @ZamowienieId (int)
        /// </summary>
        public const string GetOrderHistory = @"
            SELECT
                Id,
                ZamowienieId,
                DataZmiany,
                TypZmiany,
                PoleZmienione,
                WartoscPoprzednia,
                WartoscNowa,
                Uzytkownik,
                UzytkownikNazwa,
                OpisZmiany
            FROM dbo.HistoriaZmianZamowien
            WHERE ZamowienieId = @ZamowienieId
            ORDER BY DataZmiany DESC";

        /// <summary>
        /// Pobiera historię zmian dla zamówień w zakresie dat.
        /// Parametry: @OrderIds (string - lista ID)
        /// </summary>
        public const string GetOrderHistoryBatch = @"
            SELECT
                h.Id,
                h.ZamowienieId,
                h.DataZmiany,
                h.TypZmiany,
                h.UzytkownikNazwa,
                h.OpisZmiany
            FROM dbo.HistoriaZmianZamowien h
            WHERE h.ZamowienieId IN (SELECT value FROM STRING_SPLIT(@OrderIds, ','))
            ORDER BY h.DataZmiany DESC";

        /// <summary>
        /// Wstawia wpis do historii zmian.
        /// </summary>
        public const string InsertOrderHistory = @"
            INSERT INTO dbo.HistoriaZmianZamowien
            (ZamowienieId, DataZmiany, TypZmiany, PoleZmienione, WartoscPoprzednia, WartoscNowa, Uzytkownik, UzytkownikNazwa, OpisZmiany)
            VALUES
            (@ZamowienieId, GETDATE(), @TypZmiany, @PoleZmienione, @WartoscPoprzednia, @WartoscNowa, @Uzytkownik, @UzytkownikNazwa, @OpisZmiany)";

        #endregion

        #region Users - LibraNet.operators

        /// <summary>
        /// Pobiera użytkowników jako słownik (ID -> Nazwa).
        /// </summary>
        public const string GetUsersLookup = @"
            SELECT ID, Name FROM dbo.operators";

        #endregion
    }
}
