using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Kalendarz1.Reklamacje
{
    public partial class FormPanelReklamacjiWindow : Window
    {
        private string connectionString;
        private string userId;
        private ObservableCollection<ReklamacjaItem> reklamacje = new ObservableCollection<ReklamacjaItem>();
        private System.ComponentModel.ICollectionView reklamacjeView;
        private string aktywnaZakladka = "DO_AKCJI";
        private bool isInitialized = false;
        private bool isJakosc = false;
        private DispatcherTimer searchDebounceTimer;

        private const string HandelConnString = ReklamacjeConnectionStrings.Handel;

        // Mapowanie handlowiec name -> UserID (ladowane raz)
        private Dictionary<string, string> _handlowiecMapowanie;
        private Dictionary<string, ImageSource> _handlowiecAvatarCache = new Dictionary<string, ImageSource>();

        // Dozwolone przejscia statusow - delegacja do centralnej definicji
        private static Dictionary<string, List<string>> dozwolonePrzejscia => FormRozpatrzenieWindow.dozwolonePrzejscia;

        public FormPanelReklamacjiWindow(string connString, string user)
        {
            connectionString = connString;
            userId = user;

            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            // Sprawdz uprawnienia
            SprawdzUprawnieniaJakosc();

            // Ustaw domyslne daty
            dpDataOd.SelectedDate = DateTime.Now.AddMonths(-1);
            dpDataDo.SelectedDate = DateTime.Now;

            // Workflow V2: DataGrid pokazuje przefiltrowany widok
            reklamacjeView = System.Windows.Data.CollectionViewSource.GetDefaultView(reklamacje);
            reklamacjeView.Filter = obj =>
            {
                if (!(obj is ReklamacjaItem r)) return false;
                return r.KategoriaZakladki == aktywnaZakladka;
            };
            dgReklamacje.ItemsSource = reklamacjeView;

            // Debounce timer dla wyszukiwania (300ms)
            searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            searchDebounceTimer.Tick += (s, args) =>
            {
                searchDebounceTimer.Stop();
                WczytajReklamacje();
            };

            // Ukryj/pokaz kontrolki na podstawie uprawnien
            UstawWidocznoscKontrolek();

            isInitialized = true;
            Loaded += Window_Loaded;
        }

        private void SprawdzUprawnieniaJakosc()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT Access FROM operators WHERE ID = @UserId", conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        var result = cmd.ExecuteScalar();
                        if (result != null)
                        {
                            string accessString = result.ToString();
                            // Pozycja 33 = ReklamacjeJakosc (zaktualizowana pozycja po merge)
                            if (accessString.Length > 33 && accessString[33] == '1')
                            {
                                isJakosc = true;
                            }
                        }
                    }
                }
            }
            catch
            {
                isJakosc = false;
            }
        }

        private void UstawWidocznoscKontrolek()
        {
            // Workflow V2: widocznosc przyciskow zalezy od zaznaczonej reklamacji + isJakosc
            // (UstawKontekstoweAkcje woli wlasciwe przyciski w SelectionChanged)
            bool isAdmin = userId == "11111";

            // Statystyki - tylko dla jakosci
            if (btnStatystyki != null) btnStatystyki.Visibility = isJakosc ? Visibility.Visible : Visibility.Collapsed;

            // Usuwanie + debug + ustawienia sync - tylko admin (UserID 11111)
            if (btnUsun != null) btnUsun.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            if (btnDebugSync != null) btnDebugSync.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            if (btnUstawieniaSync != null) btnUstawieniaSync.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;

            // Panel jakosci - ZAWSZE widoczny dla wszystkich (bez ograniczen rolowych)
            if (panelJakosci != null) panelJakosci.Visibility = Visibility.Visible;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            MigracjaBazy();
            WczytajMapowanieHandlowcow();
            WczytajHandlowcow();
            try { SyncFakturyKorygujace(); } catch { }
            WczytajReklamacje();
            WczytajStatystyki();
            PrzelaczZakladke("DO_AKCJI");

            // Footer collapse - schowaj panele akcji bez zaznaczenia
            if (paneleAkcji != null) paneleAkcji.Visibility = Visibility.Collapsed;
        }

        private void WczytajMapowanieHandlowcow()
        {
            _handlowiecMapowanie = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT HandlowiecName, UserID FROM [dbo].[UserHandlowcy]", conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            string name = r.GetString(0);
                            string uid = r.GetString(1);
                            _handlowiecMapowanie[name] = uid;
                        }
                    }
                }
            }
            catch { }
        }

        private void WczytajHandlowcow()
        {
            try
            {
                using (var conn = new SqlConnection(HandelConnString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT DISTINCT CDim_Handlowiec_Val
                        FROM [HANDEL].[SSCommon].[ContractorClassification]
                        WHERE CDim_Handlowiec_Val IS NOT NULL AND CDim_Handlowiec_Val <> '' AND CDim_Handlowiec_Val <> '-'
                        ORDER BY CDim_Handlowiec_Val", conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            cmbHandlowiec.Items.Add(new ComboBoxItem { Content = r.GetString(0) });
                        }
                    }
                }
            }
            catch { }
        }

        private void CmbHandlowiec_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) WczytajReklamacje();
        }

        private void MigracjaBazy()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Etap 0: PowiazanaReklamacjaId (stara migracja)
                    using (var cmd = new SqlCommand(@"
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Reklamacje') AND name = 'PowiazanaReklamacjaId')
                            ALTER TABLE [dbo].[Reklamacje] ADD [PowiazanaReklamacjaId] INT NULL", conn))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    // Usun reklamacje kontrahentow SD/* (wewnetrzne)
                    using (var cmd = new SqlCommand(@"
                        DECLARE @ids TABLE(Id INT);
                        INSERT INTO @ids SELECT Id FROM [dbo].[Reklamacje]
                            WHERE NazwaKontrahenta LIKE 'SD/%' AND TypReklamacji = 'Faktura korygujaca';
                        DELETE FROM [dbo].[ReklamacjeTowary] WHERE IdReklamacji IN (SELECT Id FROM @ids);
                        DELETE FROM [dbo].[ReklamacjeHistoria] WHERE IdReklamacji IN (SELECT Id FROM @ids);
                        DELETE FROM [dbo].[Reklamacje] WHERE Id IN (SELECT Id FROM @ids);", conn))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    // ======================================================
                    // ETAP 1: Workflow V2 - nowe kolumny
                    // ======================================================
                    string[] kolumnyDoDodania = new[]
                    {
                        "StatusV2 NVARCHAR(30) NULL",
                        "ZrodloZgloszenia NVARCHAR(30) NULL",
                        "NumerFakturyOryginalnej NVARCHAR(50) NULL",
                        "IdFakturyOryginalnej INT NULL",
                        "DecyzjaJakosci NVARCHAR(20) NULL",
                        "NotatkaJakosci NVARCHAR(MAX) NULL",
                        "DataAnalizy DATETIME NULL",
                        "UserAnalizy NVARCHAR(50) NULL",
                        "DataPowiazania DATETIME NULL",
                        "UserPowiazania NVARCHAR(50) NULL",
                        "WymagaUzupelnienia BIT NOT NULL DEFAULT 0",
                        // Etap 2.5: kategoryzacja przyczyn
                        "KategoriaPrzyczyny NVARCHAR(50) NULL",
                        "PodkategoriaPrzyczyny NVARCHAR(100) NULL",
                        // Handlowiec przypisany do kontrahenta
                        "Handlowiec NVARCHAR(100) NULL",
                        // Kto i kiedy ZAKONCZYL sprawe (Zatwierdz / Odrzuc) — pokazywane w kolumnie "Zakonczyl"
                        "DataZakonczenia DATETIME NULL",
                        "UserZakonczenia NVARCHAR(50) NULL"
                    };

                    foreach (var def in kolumnyDoDodania)
                    {
                        string nazwa = def.Split(' ')[0];
                        string sql = $@"
                            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Reklamacje') AND name = '{nazwa}')
                                ALTER TABLE [dbo].[Reklamacje] ADD [{nazwa}] {def.Substring(nazwa.Length + 1)}";
                        using (var cmd = new SqlCommand(sql, conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Mapowanie starych statusow na StatusV2 (tylko dla rekordow bez StatusV2)
                    // Stare -> Nowe:
                    //   Nowa                   -> ZGLOSZONA
                    //   Przyjeta               -> W_ANALIZIE
                    //   W trakcie              -> W_ANALIZIE
                    //   W analizie             -> W_ANALIZIE
                    //   W trakcie realizacji   -> W_ANALIZIE
                    //   Oczekuje na dostawce   -> W_ANALIZIE
                    //   Zaakceptowana          -> ZASADNA
                    //   Odrzucona              -> ODRZUCONA
                    //   Zamknieta              -> ZAMKNIETA
                    using (var cmd = new SqlCommand(@"
                        UPDATE [dbo].[Reklamacje]
                        SET StatusV2 = CASE
                            WHEN Status = 'Nowa' THEN 'ZGLOSZONA'
                            WHEN Status IN ('Przyjeta','W trakcie','W analizie','W trakcie realizacji','Oczekuje na dostawce') THEN 'W_ANALIZIE'
                            WHEN Status = 'Zaakceptowana' THEN 'ZASADNA'
                            WHEN Status = 'Odrzucona' THEN 'ODRZUCONA'
                            WHEN Status = 'Zamknieta' THEN 'ZAMKNIETA'
                            ELSE 'ZGLOSZONA'
                        END
                        WHERE StatusV2 IS NULL;

                        -- Oznacz istniejace korekty z Symfonii jako wymagajace uzupelnienia (jesli nie sa powiazane)
                        UPDATE [dbo].[Reklamacje]
                        SET WymagaUzupelnienia = 1
                        WHERE TypReklamacji = 'Faktura korygujaca'
                          AND (PowiazanaReklamacjaId IS NULL OR PowiazanaReklamacjaId = 0)
                          AND WymagaUzupelnienia = 0
                          AND StatusV2 NOT IN ('ZAMKNIETA','ODRZUCONA');

                        -- Powiazane = POWIAZANA
                        UPDATE [dbo].[Reklamacje]
                        SET StatusV2 = 'POWIAZANA'
                        WHERE PowiazanaReklamacjaId IS NOT NULL
                          AND PowiazanaReklamacjaId > 0
                          AND StatusV2 NOT IN ('ZAMKNIETA','ODRZUCONA');

                        -- Fix: zdejmij WymagaUzupelnienia dla juz rozpatrzonych (nie-ZGLOSZONA)
                        UPDATE [dbo].[Reklamacje]
                        SET WymagaUzupelnienia = 0
                        WHERE WymagaUzupelnienia = 1
                          AND StatusV2 NOT IN ('ZGLOSZONA');

                        -- Ustaw zrodlo zgloszenia dla istniejacych rekordow
                        UPDATE [dbo].[Reklamacje]
                        SET ZrodloZgloszenia = CASE
                            WHEN TypReklamacji = 'Faktura korygujaca' THEN 'Symfonia'
                            ELSE 'Handlowiec'
                        END
                        WHERE ZrodloZgloszenia IS NULL;", conn))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    // Uzupelnij kolumne Handlowiec z HANDEL dla istniejacych rekordow
                    try
                    {
                        using (var connH = new SqlConnection(HandelConnString))
                        {
                            connH.Open();
                            // Pobierz mapowanie khid -> handlowiec
                            var mapa = new Dictionary<int, string>();
                            using (var cmdH = new SqlCommand(@"
                                SELECT ElementId, CDim_Handlowiec_Val
                                FROM [HANDEL].[SSCommon].[ContractorClassification]
                                WHERE CDim_Handlowiec_Val IS NOT NULL AND CDim_Handlowiec_Val <> '' AND CDim_Handlowiec_Val <> '-'", connH))
                            using (var rdr = cmdH.ExecuteReader())
                            {
                                while (rdr.Read())
                                    mapa[rdr.GetInt32(0)] = rdr.GetString(1);
                            }

                            // Zaktualizuj reklamacje bez handlowca
                            using (var cmdR = new SqlCommand(@"
                                SELECT Id, IdKontrahenta FROM [dbo].[Reklamacje]
                                WHERE Handlowiec IS NULL AND IdKontrahenta IS NOT NULL AND IdKontrahenta > 0", conn))
                            using (var rdr = cmdR.ExecuteReader())
                            {
                                var doAktualizacji = new List<(int id, string handl)>();
                                while (rdr.Read())
                                {
                                    int khid = rdr.IsDBNull(1) ? 0 : rdr.GetInt32(1);
                                    if (khid > 0 && mapa.TryGetValue(khid, out string handl))
                                        doAktualizacji.Add((rdr.GetInt32(0), handl));
                                }
                                rdr.Close();

                                foreach (var (id, handl) in doAktualizacji)
                                {
                                    using (var cmdU = new SqlCommand("UPDATE [dbo].[Reklamacje] SET Handlowiec = @H WHERE Id = @Id", conn))
                                    {
                                        cmdU.Parameters.AddWithValue("@H", handl);
                                        cmdU.Parameters.AddWithValue("@Id", id);
                                        cmdU.ExecuteNonQuery();
                                    }
                                }
                            }
                        }
                    }
                    catch { /* HANDEL niedostepny — nie blokuj startu */ }

                    // Uzupelnij NumerFakturyOryginalnej / IdFakturyOryginalnej dla korekt (z HANDEL.DK.iddokkoryg)
                    try
                    {
                        using (var connH = new SqlConnection(HandelConnString))
                        {
                            connH.Open();
                            var mapaKorekt = new Dictionary<int, (int idFakt, string nrFakt)>();
                            using (var cmdH = new SqlCommand(@"
                                SELECT K.id AS IdKorekty, F.id AS IdFakt, F.kod AS NrFakt
                                FROM [HANDEL].[HM].[DK] K
                                INNER JOIN [HANDEL].[HM].[DK] F ON K.iddokkoryg = F.id
                                WHERE K.seria IN ('sFKS', 'sFKSB', 'sFWK')
                                  AND K.anulowany = 0
                                  AND K.iddokkoryg IS NOT NULL
                                  AND K.iddokkoryg > 0", connH))
                            using (var rdr = cmdH.ExecuteReader())
                            {
                                while (rdr.Read())
                                    mapaKorekt[rdr.GetInt32(0)] = (rdr.GetInt32(1), rdr.GetString(2));
                            }

                            using (var cmdR = new SqlCommand(@"
                                SELECT Id, IdDokumentu FROM [dbo].[Reklamacje]
                                WHERE TypReklamacji = 'Faktura korygujaca'
                                  AND (IdFakturyOryginalnej IS NULL OR IdFakturyOryginalnej = 0)
                                  AND IdDokumentu IS NOT NULL AND IdDokumentu > 0", conn))
                            using (var rdr = cmdR.ExecuteReader())
                            {
                                var doUp = new List<(int idRekl, int idFakt, string nrFakt)>();
                                while (rdr.Read())
                                {
                                    int idKor = rdr.GetInt32(1);
                                    if (mapaKorekt.TryGetValue(idKor, out var fakt))
                                        doUp.Add((rdr.GetInt32(0), fakt.idFakt, fakt.nrFakt));
                                }
                                rdr.Close();

                                foreach (var (idRekl, idFakt, nrFakt) in doUp)
                                {
                                    using (var cmdU = new SqlCommand(@"
                                        UPDATE [dbo].[Reklamacje]
                                        SET IdFakturyOryginalnej = @IdF, NumerFakturyOryginalnej = @NrF
                                        WHERE Id = @Id", conn))
                                    {
                                        cmdU.Parameters.AddWithValue("@IdF", idFakt);
                                        cmdU.Parameters.AddWithValue("@NrF", nrFakt);
                                        cmdU.Parameters.AddWithValue("@Id", idRekl);
                                        cmdU.ExecuteNonQuery();
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Uzupelnienie iddokkoryg blad: {ex.Message}");
                    }

                    // ======================================================
                    // Tabela zalacznikow (zdjecia, pdf, skany)
                    // ======================================================
                    using (var cmd = new SqlCommand(@"
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ReklamacjeZalaczniki')
                        BEGIN
                            CREATE TABLE [dbo].[ReklamacjeZalaczniki] (
                                [Id] INT IDENTITY(1,1) PRIMARY KEY,
                                [IdReklamacji] INT NOT NULL,
                                [NazwaPliku] NVARCHAR(260) NOT NULL,
                                [TypMime] NVARCHAR(100) NULL,
                                [Rozmiar] BIGINT NOT NULL DEFAULT 0,
                                [Dane] VARBINARY(MAX) NULL,
                                [UserID] NVARCHAR(50) NULL,
                                [DataDodania] DATETIME NOT NULL DEFAULT GETDATE(),
                                [Opis] NVARCHAR(500) NULL
                            );
                            CREATE INDEX IX_ReklamacjeZalaczniki_IdReklamacji
                                ON [dbo].[ReklamacjeZalaczniki]([IdReklamacji]);
                        END", conn))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    // Tabela komentarzy wewnetrznych
                    using (var cmd = new SqlCommand(@"
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ReklamacjeKomentarze')
                        BEGIN
                            CREATE TABLE [dbo].[ReklamacjeKomentarze] (
                                [Id] INT IDENTITY(1,1) PRIMARY KEY,
                                [IdReklamacji] INT NOT NULL,
                                [UserID] NVARCHAR(50) NOT NULL,
                                [Tresc] NVARCHAR(MAX) NOT NULL,
                                [DataDodania] DATETIME NOT NULL DEFAULT GETDATE()
                            );
                            CREATE INDEX IX_ReklamacjeKomentarze_IdReklamacji
                                ON [dbo].[ReklamacjeKomentarze]([IdReklamacji]);
                        END", conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Loguj tylko do Debug - migracja nie powinna blokowac okna
                System.Diagnostics.Debug.WriteLine($"MigracjaBazy blad: {ex.Message}");
            }
        }

        private void WczytajReklamacje()
        {
            if (!isInitialized || string.IsNullOrEmpty(connectionString)) return;

            try
            {
                reklamacje.Clear();

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Pobierz date od korekt (admin moze ja zmienic)
                    DateTime dataOdKorekt = PobierzDataOdKorekt();

                    string query = @"
                        SELECT
                            r.Id,
                            r.DataZgloszenia,
                            r.NumerDokumentu,
                            r.NazwaKontrahenta,
                            LEFT(ISNULL(r.Opis, ''), 100) + CASE WHEN LEN(ISNULL(r.Opis, '')) > 100 THEN '...' ELSE '' END AS Opis,
                            ISNULL(r.SumaKg, 0) AS SumaKg,
                            ISNULL(r.Status, 'Nowa') AS Status,
                            ISNULL(o.Name, r.UserID) AS Zglaszajacy,
                            ISNULL(o2.Name, r.OsobaRozpatrujaca) AS OsobaRozpatrujaca,
                            ISNULL(r.TypReklamacji, 'Inne') AS TypReklamacji,
                            ISNULL(r.Priorytet, 'Normalny') AS Priorytet,
                            r.UserID AS ZglaszajacyId,
                            ISNULL(r.OsobaRozpatrujaca, '') AS RozpatrujacyId,
                            r.PowiazanaReklamacjaId,
                            ISNULL(r.StatusV2, 'ZGLOSZONA') AS StatusV2,
                            ISNULL(r.ZrodloZgloszenia, 'Handlowiec') AS ZrodloZgloszenia,
                            r.NumerFakturyOryginalnej,
                            r.IdFakturyOryginalnej,
                            r.DecyzjaJakosci,
                            ISNULL(r.WymagaUzupelnienia, 0) AS WymagaUzupelnienia,
                            ISNULL(r.Handlowiec, '') AS Handlowiec,
                            ISNULL(r.UserZakonczenia, '') AS UserZakonczeniaId,
                            ISNULL(o3.Name, r.UserZakonczenia) AS UserZakonczeniaName,
                            r.DataZakonczenia,
                            r.DataAnalizy
                        FROM [dbo].[Reklamacje] r
                        LEFT JOIN [dbo].[operators] o ON r.UserID = o.ID
                        LEFT JOIN [dbo].[operators] o2 ON r.OsobaRozpatrujaca = o2.ID
                        LEFT JOIN [dbo].[operators] o3 ON r.UserZakonczenia = o3.ID
                        WHERE (r.TypReklamacji <> 'Faktura korygujaca' OR r.DataZgloszenia >= @DataOdKorekt OR ISNULL(r.StatusV2,'ZGLOSZONA') NOT IN ('ZGLOSZONA'))";

                    string statusFilter = (cmbStatus.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    string statusV2Filter = statusFilter switch
                    {
                        "Zgloszona" => "ZGLOSZONA",
                        "W analizie" => "W_ANALIZIE",
                        "Zasadna" => "ZASADNA",
                        "Odrzucona" => "ODRZUCONA",
                        "Powiazana" => "POWIAZANA",
                        "Zamknieta" => "ZAMKNIETA",
                        _ => null
                    };
                    if (!string.IsNullOrEmpty(statusV2Filter))
                    {
                        query += " AND ISNULL(r.StatusV2,'ZGLOSZONA') = @StatusV2";
                    }

                    string typFilter = (cmbTyp?.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    if (!string.IsNullOrEmpty(typFilter) && typFilter != "Wszystkie")
                    {
                        query += " AND r.TypReklamacji = @Typ";
                    }

                    string priorytetFilter = (cmbPriorytet?.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    if (!string.IsNullOrEmpty(priorytetFilter) && priorytetFilter != "Wszystkie")
                    {
                        query += " AND r.Priorytet = @Priorytet";
                    }

                    string handlowiecFilter = (cmbHandlowiec?.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    if (!string.IsNullOrEmpty(handlowiecFilter) && handlowiecFilter != "Wszyscy")
                    {
                        query += " AND r.Opis LIKE @Handlowiec";
                    }

                    // SZUKAJ — zwykle LIKE po kilku polach
                    string szukaj = txtSzukaj?.Text?.Trim();
                    bool maSzukaj = !string.IsNullOrEmpty(szukaj);
                    if (maSzukaj)
                    {
                        query += " AND (r.NumerDokumentu LIKE @Szukaj OR r.NazwaKontrahenta LIKE @Szukaj OR r.Opis LIKE @Szukaj OR r.TypReklamacji LIKE @Szukaj OR r.PrzyczynaGlowna LIKE @Szukaj)";
                    }

                    query += " ORDER BY r.DataZgloszenia DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DataOdKorekt", dataOdKorekt);

                        if (!string.IsNullOrEmpty(statusV2Filter))
                        {
                            cmd.Parameters.AddWithValue("@StatusV2", statusV2Filter);
                        }

                        if (!string.IsNullOrEmpty(typFilter) && typFilter != "Wszystkie")
                        {
                            cmd.Parameters.AddWithValue("@Typ", typFilter);
                        }

                        if (!string.IsNullOrEmpty(priorytetFilter) && priorytetFilter != "Wszystkie")
                        {
                            cmd.Parameters.AddWithValue("@Priorytet", priorytetFilter);
                        }

                        if (maSzukaj) cmd.Parameters.AddWithValue("@Szukaj", $"%{szukaj}%");

                        if (!string.IsNullOrEmpty(handlowiecFilter) && handlowiecFilter != "Wszyscy")
                        {
                            cmd.Parameters.AddWithValue("@Handlowiec", $"%{handlowiecFilter}%");
                        }

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var item2 = new ReklamacjaItem
                                {
                                    Id = reader.GetInt32(0),
                                    DataZgloszenia = reader.IsDBNull(1) ? DateTime.MinValue : reader.GetDateTime(1),
                                    NumerDokumentu = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    NazwaKontrahenta = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    Opis = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    SumaKg = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                                    Status = reader.IsDBNull(6) ? "Nowa" : reader.GetString(6),
                                    Zglaszajacy = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                    OsobaRozpatrujaca = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                    TypReklamacji = reader.IsDBNull(9) ? "Inne" : reader.GetString(9),
                                    Priorytet = reader.IsDBNull(10) ? "Normalny" : reader.GetString(10),
                                    ZglaszajacyId = reader.IsDBNull(11) ? "" : reader.GetString(11),
                                    RozpatrujacyId = reader.IsDBNull(12) ? "" : reader.GetString(12),
                                    PowiazanaReklamacjaId = reader.IsDBNull(13) ? (int?)null : reader.GetInt32(13),
                                    StatusV2 = reader.IsDBNull(14) ? StatusyV2.ZGLOSZONA : reader.GetString(14),
                                    ZrodloZgloszenia = reader.IsDBNull(15) ? "Handlowiec" : reader.GetString(15),
                                    NumerFakturyOryginalnej = reader.IsDBNull(16) ? null : reader.GetString(16),
                                    IdFakturyOryginalnej = reader.IsDBNull(17) ? (int?)null : reader.GetInt32(17),
                                    DecyzjaJakosci = reader.IsDBNull(18) ? null : reader.GetString(18),
                                    WymagaUzupelnienia = !reader.IsDBNull(19) && reader.GetBoolean(19),
                                    Handlowiec = reader.IsDBNull(20) ? "" : reader.GetString(20),
                                    UserZakonczeniaId = reader.IsDBNull(21) ? "" : reader.GetString(21),
                                    UserZakonczenia = reader.IsDBNull(22) ? "" : reader.GetString(22),
                                    DataZakonczenia = reader.IsDBNull(23) ? (DateTime?)null : reader.GetDateTime(23),
                                    DataAnalizy = reader.IsDBNull(24) ? (DateTime?)null : reader.GetDateTime(24)
                                };
                                item2.ZglaszajacyAvatar = GetCachedAvatar(item2.ZglaszajacyId, item2.Zglaszajacy);
                                item2.RozpatrujacyAvatar = GetCachedAvatar(item2.RozpatrujacyId, item2.OsobaRozpatrujaca);
                                item2.ZakonczylAvatar = GetCachedAvatar(item2.UserZakonczeniaId, item2.UserZakonczenia);
                                if (!string.IsNullOrEmpty(item2.Handlowiec) && item2.Handlowiec != "-")
                                {
                                    item2.HandlowiecAvatar = GetHandlowiecAvatar(item2.Handlowiec);
                                }
                                reklamacje.Add(item2);
                            }
                        }
                    }
                }

                // Zaladuj miniatury zdjec batchem
                ZaladujMiniaturyDoListy();

                reklamacjeView?.Refresh();
                AktualizujLicznikiZakladek();
                AktualizujLicznikZaznaczenia();
            }
            catch (Exception ex)
            {
                FriendlyError.Pokaz(ex, "Nie udalo sie zaladowac listy reklamacji.", this);
            }
        }

        // Pobiera 3 najnowsze zdjecia + licznik dla kazdej reklamacji jednym zapytaniem
        private void ZaladujMiniaturyDoListy()
        {
            if (reklamacje == null || reklamacje.Count == 0) return;
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Sprawdz czy DaneZdjecia istnieje
                    bool maBlob = false;
                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ReklamacjeZdjecia' AND COLUMN_NAME='DaneZdjecia'", conn))
                    {
                        maBlob = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }

                    // 1) Liczniki zdjec per reklamacja
                    using (var cmd = new SqlCommand(@"
                        SELECT IdReklamacji, COUNT(*) AS Liczba
                        FROM [dbo].[ReklamacjeZdjecia]
                        GROUP BY IdReklamacji", conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        var dict = new Dictionary<int, int>();
                        while (r.Read()) dict[r.GetInt32(0)] = r.GetInt32(1);
                        foreach (var item in reklamacje)
                            if (dict.TryGetValue(item.Id, out int n)) item.LiczbaZdjec = n;
                    }

                    // 2) Top 3 najnowsze zdjecia per reklamacja (jako blob lub sciezka)
                    string sql = maBlob
                        ? @"WITH Z AS (
                                SELECT Id, IdReklamacji, NazwaPliku, SciezkaPliku, DaneZdjecia,
                                       ROW_NUMBER() OVER (PARTITION BY IdReklamacji ORDER BY DataDodania DESC) AS Rn
                                FROM [dbo].[ReklamacjeZdjecia]
                            )
                            SELECT IdReklamacji, Rn, NazwaPliku, SciezkaPliku, DaneZdjecia FROM Z WHERE Rn <= 3"
                        : @"WITH Z AS (
                                SELECT Id, IdReklamacji, NazwaPliku, SciezkaPliku,
                                       ROW_NUMBER() OVER (PARTITION BY IdReklamacji ORDER BY DataDodania DESC) AS Rn
                                FROM [dbo].[ReklamacjeZdjecia]
                            )
                            SELECT IdReklamacji, Rn, NazwaPliku, SciezkaPliku FROM Z WHERE Rn <= 3";

                    using (var cmd = new SqlCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        var lookup = reklamacje.ToDictionary(x => x.Id);
                        while (r.Read())
                        {
                            int idRekl = r.GetInt32(0);
                            int rn = r.GetInt32(1);
                            if (!lookup.TryGetValue(idRekl, out var item)) continue;
                            ImageSource img = null;
                            byte[] blob = maBlob && !r.IsDBNull(4) ? (byte[])r["DaneZdjecia"] : null;
                            string sciezka = !r.IsDBNull(3) ? r.GetString(3) : null;
                            try { img = ZbudujMiniaturke(blob, sciezka, 36); } catch { img = null; }
                            if (img != null)
                            {
                                if (rn == 1) item.Miniatura1 = img;
                                else if (rn == 2) item.Miniatura2 = img;
                                else if (rn == 3) item.Miniatura3 = img;
                            }
                        }
                    }
                }
            }
            catch { /* nie blokuj listy gdy zdjec nie da sie zaladowac */ }
        }

        // Buduje miniaturke z blob lub sciezki, decoduje do zadanego rozmiaru
        private static ImageSource ZbudujMiniaturke(byte[] blob, string sciezka, int wysPx)
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.DecodePixelHeight = wysPx * 2; // 2x dla retina
                if (blob != null && blob.Length > 0)
                    bi.StreamSource = new System.IO.MemoryStream(blob);
                else if (!string.IsNullOrWhiteSpace(sciezka) && System.IO.File.Exists(sciezka))
                    bi.UriSource = new Uri(sciezka, UriKind.Absolute);
                else { return null; }
                bi.EndInit();
                if (bi.CanFreeze) bi.Freeze();
                return bi;
            }
            catch { return null; }
        }

        private void WczytajStatystyki()
        {
            if (!isInitialized || string.IsNullOrEmpty(connectionString)) return;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    DateTime dataOdKorekt = PobierzDataOdKorekt();

                    // Licz wg KategoriaZakladki - dokladnie tak jak zakladki
                    string query = @"
                        SELECT
                            CASE
                                WHEN ISNULL(StatusV2,'ZGLOSZONA') IN ('ZAMKNIETA','ODRZUCONA','POWIAZANA','ZASADNA') THEN 'ZAMKNIETE'
                                WHEN ISNULL(StatusV2,'ZGLOSZONA') = 'ZGLOSZONA' OR ISNULL(WymagaUzupelnienia,0) = 1 THEN 'DO_AKCJI'
                                ELSE 'W_TOKU'
                            END AS Kategoria,
                            COUNT(*) AS Liczba
                        FROM [dbo].[Reklamacje]
                        WHERE (TypReklamacji <> 'Faktura korygujaca' OR DataZgloszenia >= @DataOdKorekt OR ISNULL(StatusV2,'ZGLOSZONA') NOT IN ('ZGLOSZONA'))
                        GROUP BY
                            CASE
                                WHEN ISNULL(StatusV2,'ZGLOSZONA') IN ('ZAMKNIETA','ODRZUCONA','POWIAZANA','ZASADNA') THEN 'ZAMKNIETE'
                                WHEN ISNULL(StatusV2,'ZGLOSZONA') = 'ZGLOSZONA' OR ISNULL(WymagaUzupelnienia,0) = 1 THEN 'DO_AKCJI'
                                ELSE 'W_TOKU'
                            END";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DataOdKorekt", dataOdKorekt);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            int doAkcji = 0, wToku = 0, zamkniete = 0;
                            while (reader.Read())
                            {
                                string kat = reader.GetString(0);
                                int liczba = reader.GetInt32(1);
                                switch (kat)
                                {
                                    case "DO_AKCJI": doAkcji = liczba; break;
                                    case "W_TOKU": wToku = liczba; break;
                                    case "ZAMKNIETE": zamkniete = liczba; break;
                                }
                            }

                            txtStatNowe.Text = doAkcji.ToString();
                            txtStatWTrakcie.Text = wToku.ToString();
                            txtStatZaakceptowane.Text = zamkniete.ToString();
                        }
                    }
                }
            }
            catch
            {
                // Ignoruj bledy statystyk
            }
        }

        private void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            WczytajReklamacje();
            WczytajStatystyki();
        }

        private void CmbStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
                WczytajReklamacje();
        }

        private void DpData_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
                WczytajReklamacje();
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded)
            {
                searchDebounceTimer.Stop();
                searchDebounceTimer.Start();
            }
        }

        private void DgReklamacje_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool selected = dgReklamacje.SelectedItem != null;
            btnUzupelnij.IsEnabled = selected;
            btnSzczegoly.IsEnabled = selected;
            btnUsun.IsEnabled = selected;

            // Workflow V2: kontekstowa widocznosc przyciskow
            UstawKontekstoweAkcje(dgReklamacje.SelectedItem as ReklamacjaItem);

            if (selected && dgReklamacje.SelectedItem is ReklamacjaItem item)
            {
                string etykieta = StatusyV2.Etykieta(item.StatusV2);
                txtZaznaczenie.Text = $"Wybrano: #{item.Id} - {item.NazwaKontrahenta} ({etykieta})";
                if (paneleAkcji != null) paneleAkcji.Visibility = Visibility.Visible;
            }
            else
            {
                txtZaznaczenie.Text = "👈 Wybierz reklamacje z listy aby zobaczyc dostepne akcje";
                if (paneleAkcji != null) paneleAkcji.Visibility = Visibility.Collapsed;
            }
        }

        // ============================================================
        // KARTA KLIENTA — sticky panel boczny
        // ============================================================
        private void WyswietlKarteKlienta(ReklamacjaItem item)
        {
#if KARTA_KLIENTA_ENABLED
            if (item == null)
            {
                kkPlaceholder.Visibility = Visibility.Visible;
                kkContent.Visibility = Visibility.Collapsed;
                kkNazwa.Text = "Wybierz reklamacje";
                return;
            }

            kkPlaceholder.Visibility = Visibility.Collapsed;
            kkContent.Visibility = Visibility.Visible;
            kkNazwa.Text = item.NazwaKontrahenta;

            try
            {
                // Pobierz IdKontrahenta z reklamacji
                int khid = 0;
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT ISNULL(IdKontrahenta,0) FROM [dbo].[Reklamacje] WHERE Id=@Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", item.Id);
                        khid = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                    }
                }

                // Statystyki YTD reklamacji - zlicz z LOKALNEJ listy reklamacji + DB
                var rok = DateTime.Today.Year;
                int liczbaYtd = 0; int liczbaZasadne = 0; decimal stratagYtd = 0; decimal kgYtd = 0;
                int otwarteTeraz = 0;
                double sumDni = 0; int countDni = 0;
                var ostatnie = new List<KkSprawaItem>();

                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string sql = khid > 0
                        ? @"SELECT Id, DataZgloszenia, NumerDokumentu, ISNULL(SumaKg,0) AS Kg, ISNULL(StatusV2,'ZGLOSZONA') AS StatusV2,
                                  ISNULL(TypReklamacji,'') AS TypReklamacji, DataZakonczenia
                           FROM [dbo].[Reklamacje]
                           WHERE IdKontrahenta = @Khid
                           ORDER BY DataZgloszenia DESC"
                        : @"SELECT Id, DataZgloszenia, NumerDokumentu, ISNULL(SumaKg,0) AS Kg, ISNULL(StatusV2,'ZGLOSZONA') AS StatusV2,
                                  ISNULL(TypReklamacji,'') AS TypReklamacji, DataZakonczenia
                           FROM [dbo].[Reklamacje]
                           WHERE NazwaKontrahenta = @Nazwa
                           ORDER BY DataZgloszenia DESC";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        if (khid > 0) cmd.Parameters.AddWithValue("@Khid", khid);
                        else cmd.Parameters.AddWithValue("@Nazwa", item.NazwaKontrahenta ?? "");
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                int idR = r.GetInt32(0);
                                DateTime dataR = r.IsDBNull(1) ? DateTime.MinValue : r.GetDateTime(1);
                                string nrR = r.IsDBNull(2) ? "" : r.GetString(2);
                                decimal kgR = r.IsDBNull(3) ? 0m : r.GetDecimal(3);
                                string statR = r.IsDBNull(4) ? "ZGLOSZONA" : r.GetString(4);
                                string typR = r.IsDBNull(5) ? "" : r.GetString(5);
                                DateTime? dataKonR = r.IsDBNull(6) ? (DateTime?)null : r.GetDateTime(6);

                                if (dataR.Year == rok)
                                {
                                    liczbaYtd++;
                                    kgYtd += kgR;
                                    if (statR == "ZASADNA" || statR == "POWIAZANA" || statR == "ZAMKNIETA") liczbaZasadne++;
                                    if (statR == "ZGLOSZONA" || statR == "W_ANALIZIE") otwarteTeraz++;
                                    if (dataKonR.HasValue) { sumDni += (dataKonR.Value - dataR).TotalDays; countDni++; }
                                }

                                if (ostatnie.Count < 5)
                                {
                                    var (bg, fg, krotki) = StatusKolory(statR);
                                    ostatnie.Add(new KkSprawaItem
                                    {
                                        Tytul = string.IsNullOrEmpty(nrR) ? $"#{idR}" : nrR,
                                        Podtytul = $"{dataR:dd.MM.yyyy} • {typR}",
                                        Kwota = kgR > 0 ? $"{kgR:N1} kg" : "",
                                        StatusBg = bg, StatusFg = fg, StatusKrotki = krotki
                                    });
                                }
                            }
                        }
                    }
                }

                // Strata netto: oszacuj jako kg * 8 zł (placeholder — zastąpienie potem realna kwota z DP)
                stratagYtd = kgYtd * 8m;

                kkLiczbaYtd.Text = liczbaYtd.ToString();
                kkLiczbaZasadne.Text = $"{liczbaZasadne} zasadnych";
                kkStrataYtd.Text = $"{stratagYtd:N0} zl";
                kkStrataKg.Text = $"{kgYtd:N1} kg";
                kkOtwarte.Text = otwarteTeraz.ToString();
                kkSrCzas.Text = countDni > 0 ? $"{sumDni / countDni:N1} dni" : "—";

                // Pobierz obroty z HANDEL (faktury sprzedazy YTD)
                decimal obrot = 0; string ostFakturaTxt = "—"; string handlowiec = "—";
                if (khid > 0)
                {
                    try
                    {
                        using (var connH = new SqlConnection(HandelConnString))
                        {
                            connH.Open();
                            using (var cmd = new SqlCommand(@"
                                SELECT TOP 1
                                    (SELECT ISNULL(SUM(ABS(walNetto)),0) FROM [HANDEL].[HM].[DK]
                                     WHERE khid=@K AND seria NOT LIKE 'sFK%' AND anulowany=0 AND YEAR(data)=@Rok) AS Obrot,
                                    DK.kod, DK.data,
                                    ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec
                                FROM [HANDEL].[HM].[DK] DK
                                LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                                WHERE DK.khid=@K AND DK.seria NOT LIKE 'sFK%' AND DK.anulowany=0
                                ORDER BY DK.data DESC", connH))
                            {
                                cmd.Parameters.AddWithValue("@K", khid);
                                cmd.Parameters.AddWithValue("@Rok", rok);
                                using (var r = cmd.ExecuteReader())
                                {
                                    if (r.Read())
                                    {
                                        obrot = r.IsDBNull(0) ? 0m : Convert.ToDecimal(r.GetValue(0));
                                        string nr = r.IsDBNull(1) ? "" : r.GetString(1);
                                        DateTime dat = r.IsDBNull(2) ? DateTime.MinValue : r.GetDateTime(2);
                                        ostFakturaTxt = $"{nr} ({dat:dd.MM.yyyy})";
                                        handlowiec = r.IsDBNull(3) ? "—" : r.GetString(3);
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                kkObrot.Text = obrot > 0 ? $"{obrot:N0} zl" : "—";
                kkOstatniaFaktura.Text = ostFakturaTxt;
                kkHandlowiec.Text = handlowiec;

                // % obrotu (problematycznosc)
                double procent = obrot > 0 ? (double)(stratagYtd / obrot) * 100 : 0;
                kkProcent.Text = $"{procent:N1}%";
                // Pasek: 0% = 0 px, 5% = 100% szerokosci
                double frac = Math.Min(procent / 5.0, 1.0);
                kkProcentBar.Width = Math.Max(2, frac * 280);
                Color barColor = procent < 2 ? Color.FromRgb(0x27, 0xAE, 0x60)
                              : procent < 5 ? Color.FromRgb(0xF1, 0xC4, 0x0F)
                              : Color.FromRgb(0xC0, 0x39, 0x2B);
                kkProcentBar.Background = new SolidColorBrush(barColor);

                // Tag (VIP / Standard / Problematyczny)
                if (procent >= 5 || liczbaYtd >= 20)
                {
                    kkTagBadge.Background = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B));
                    kkTagText.Text = "🚨 PROBLEMATYCZNY";
                    kkProblematycznoscText.Text = $"{liczbaYtd} reklamacji w {rok}";
                }
                else if (obrot > 100000)
                {
                    kkTagBadge.Background = new SolidColorBrush(Color.FromRgb(0x9B, 0x59, 0xB6));
                    kkTagText.Text = "⭐ VIP";
                    kkProblematycznoscText.Text = $"{obrot/1000:N0}k obrotu";
                }
                else
                {
                    kkTagBadge.Background = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
                    kkTagText.Text = "STANDARD";
                    kkProblematycznoscText.Text = $"{liczbaYtd} reklamacji w {rok}";
                }

                kkOstatnieSprawy.ItemsSource = ostatnie;
            }
            catch (Exception)
            {
                // Cicho — karta nie krytyczna
            }
#endif
        }

        private static (Brush bg, Brush fg, string krotki) StatusKolory(string statusV2) => statusV2 switch
        {
            "ZGLOSZONA" => (new SolidColorBrush(Color.FromRgb(0xFD, 0xED, 0xEC)), new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B)), "NEW"),
            "W_ANALIZIE" => (new SolidColorBrush(Color.FromRgb(0xFF, 0xF8, 0xE1)), new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22)), "ANL"),
            "ZASADNA" => (new SolidColorBrush(Color.FromRgb(0xE8, 0xF5, 0xE9)), new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)), "OK"),
            "POWIAZANA" => (new SolidColorBrush(Color.FromRgb(0xF3, 0xE5, 0xF5)), new SolidColorBrush(Color.FromRgb(0x7B, 0x1F, 0xA2)), "PWZ"),
            "ZAMKNIETA" => (new SolidColorBrush(Color.FromRgb(0xEC, 0xEF, 0xF1)), new SolidColorBrush(Color.FromRgb(0x54, 0x6E, 0x7A)), "END"),
            "ODRZUCONA" => (new SolidColorBrush(Color.FromRgb(0xFF, 0xEB, 0xEE)), new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)), "REJ"),
            _ => (new SolidColorBrush(Colors.LightGray), new SolidColorBrush(Colors.DarkGray), "??")
        };

        // Model wewnetrzny dla listy ostatnich spraw w karcie klienta
        public class KkSprawaItem
        {
            public string Tytul { get; set; }
            public string Podtytul { get; set; }
            public string Kwota { get; set; }
            public Brush StatusBg { get; set; }
            public Brush StatusFg { get; set; }
            public string StatusKrotki { get; set; }
        }

        // ============================================================
        // CO DALEJ? — persona-aware sugestie akcji
        // ============================================================
        public class CoDalejItem
        {
            public string Ikona { get; set; }
            public string Tytul { get; set; }
            public string Opis { get; set; }
            public string Akcja { get; set; } // "filtruj_moje" | "filtruj_nowe" | "filtruj_krytyczne" | "filtruj_powiazz" | "do_akcji"
        }

        private void OdswiezCoDalej()
        {
#if CO_DALEJ_ENABLED
            try
            {
                var sugestie = ZbierzSugestieCoDalej();
                if (sugestie.Count == 0)
                {
                    coDalejPanel.Visibility = Visibility.Collapsed;
                    return;
                }
                coDalejPanel.Visibility = Visibility.Visible;
                coDalejList.ItemsSource = sugestie;
            }
            catch { coDalejPanel.Visibility = Visibility.Collapsed; }
#endif
        }

        private List<CoDalejItem> ZbierzSugestieCoDalej()
        {
            var lista = new List<CoDalejItem>();

            // 1. Krytyczne czekajace na akcje JAKOSCI (>24h od zgloszenia, status ZGLOSZONA)
            int krytyczne24h = reklamacje.Count(r =>
                r.StatusV2 == StatusyV2.ZGLOSZONA
                && (DateTime.Now - r.DataZgloszenia).TotalHours > 24);
            if (krytyczne24h > 0)
            {
                lista.Add(new CoDalejItem
                {
                    Ikona = "🔥",
                    Tytul = $"{krytyczne24h} zgloszen czeka >24h",
                    Opis = "kliknij aby pokazac",
                    Akcja = "filtruj_krytyczne"
                });
            }

            // 2. Moje sprawy gdzie jestem rozpatrujacy/zglaszajacy
            int moje = reklamacje.Count(r =>
                (r.RozpatrujacyId == userId || r.ZglaszajacyId == userId)
                && (r.StatusV2 == StatusyV2.ZGLOSZONA || r.StatusV2 == StatusyV2.W_ANALIZIE));
            if (moje > 0)
            {
                lista.Add(new CoDalejItem
                {
                    Ikona = "👤",
                    Tytul = $"{moje} mojich w toku",
                    Opis = "twoja akcja",
                    Akcja = "filtruj_moje"
                });
            }

            // 3. Zasadne czekajace na korekte z Symfonii
            int zasadnePowiaz = reklamacje.Count(r =>
                r.StatusV2 == StatusyV2.ZASADNA && !r.MaPowiazanie);
            if (zasadnePowiaz > 0)
            {
                lista.Add(new CoDalejItem
                {
                    Ikona = "🔗",
                    Tytul = $"{zasadnePowiaz} zasadnych do powiazania",
                    Opis = "z korekta Symfonii",
                    Akcja = "filtruj_powiazz"
                });
            }

            // 4. Nowe (24h)
            int nowe = reklamacje.Count(r =>
                (DateTime.Now - r.DataZgloszenia).TotalHours <= 24);
            if (nowe > 0)
            {
                lista.Add(new CoDalejItem
                {
                    Ikona = "🆕",
                    Tytul = $"{nowe} nowych dzisiaj",
                    Opis = "ostatnie 24h",
                    Akcja = "filtruj_nowe"
                });
            }

            // 5. Wykrycie partii (jesli jest >=3 reklamacji o ten sam numer dokumentu w 7 dni — wzor partii)
            var grupy = reklamacje
                .Where(r => r.DataZgloszenia >= DateTime.Now.AddDays(-7) && !string.IsNullOrEmpty(r.NumerDokumentu))
                .GroupBy(r => r.NazwaKontrahenta)
                .Where(g => g.Count() >= 3)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();
            if (grupy != null)
            {
                lista.Add(new CoDalejItem
                {
                    Ikona = "🚨",
                    Tytul = $"WZOR: {grupy.Count()}× {grupy.Key}",
                    Opis = "ostatnie 7 dni",
                    Akcja = $"filtruj_klient:{grupy.Key}"
                });
            }

            // Pokaz max 4
            return lista.Take(4).ToList();
        }

        private void CoDalejAkcja_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is System.Windows.Controls.Button btn) || !(btn.Tag is string akcja)) return;
            switch (akcja)
            {
                case "filtruj_krytyczne":
                    PrzelaczZakladke("DO_AKCJI");
                    txtSzukaj.Text = "";
                    cmbStatus.SelectedIndex = 1; // Zgloszona
                    break;
                case "filtruj_moje":
                    txtSzukaj.Text = "moje";
                    break;
                case "filtruj_nowe":
                    txtSzukaj.Text = "nowe";
                    PrzelaczZakladke("DO_AKCJI");
                    break;
                case "filtruj_powiazz":
                    cmbStatus.SelectedIndex = 3; // Zasadna
                    break;
                default:
                    if (akcja.StartsWith("filtruj_klient:"))
                    {
                        var klient = akcja.Substring("filtruj_klient:".Length);
                        txtSzukaj.Text = klient;
                    }
                    break;
            }
        }

        private void BtnCoDalejOdswiez_Click(object sender, RoutedEventArgs e) { /* usuniete z UI */ }

        // Wszystkie przyciski ZAWSZE widoczne. Aktywnosc (IsEnabled) zalezy WYLACZNIE od:
        //   - czy zaznaczona jest reklamacja
        //   - czy status pozwala na dana akcje
        // BEZ ograniczen po roli — kazdy moze klikac kazdy przycisk (na odpowiedzialnosc uzytkownika).
        private void UstawKontekstoweAkcje(ReklamacjaItem item)
        {
            // Reset
            btnPrzekazAnaliza.IsEnabled = false;
            btnZasadna.IsEnabled = false;
            btnNiezasadna.IsEnabled = false;
            btnPowiaz.IsEnabled = false;
            btnZamknij.IsEnabled = false;
            btnPrzekazAnaliza.Visibility = Visibility.Visible;
            btnZasadna.Visibility = Visibility.Visible;
            btnNiezasadna.Visibility = Visibility.Visible;
            btnPowiaz.Visibility = Visibility.Visible;
            btnZamknij.Visibility = Visibility.Visible;
            if (sepWorkflow != null) sepWorkflow.Visibility = Visibility.Collapsed;

            // Przycisk "Zglos do tej korekty" - kontekstowo Visible
            btnZglosDoKorekty.Visibility = Visibility.Collapsed;
            btnZglosDoKorekty.IsEnabled = false;
            if (item != null)
            {
                bool toKorekta = item.TypReklamacji == "Faktura korygujaca";
                bool otwarta = item.StatusV2 == StatusyV2.ZGLOSZONA || item.StatusV2 == StatusyV2.W_ANALIZIE;
                bool niepowiazana = !item.MaPowiazanie;
                if (toKorekta && otwarta && niepowiazana)
                {
                    btnZglosDoKorekty.Visibility = Visibility.Visible;
                    btnZglosDoKorekty.IsEnabled = true;
                }
            }

            // Brak zaznaczenia
            if (item == null)
            {
                if (txtJakoscBrakAkcji != null)
                {
                    txtJakoscBrakAkcji.Visibility = Visibility.Visible;
                    txtJakoscBrakAkcji.Text = "Wybierz reklamacje z listy aby aktywowac przyciski rozpatrywania";
                }
                AktualizujPasekStatusu(null);
                return;
            }

            // Aktywuj kontekstowe akcje na podstawie statusu
            string s = item.StatusV2 ?? StatusyV2.ZGLOSZONA;
            bool jestKorekta = item.TypReklamacji == "Faktura korygujaca" || item.WymagaUzupelnienia;

            switch (s)
            {
                case StatusyV2.ZGLOSZONA:
                    btnPrzekazAnaliza.IsEnabled = true;
                    btnNiezasadna.IsEnabled = true;
                    if (jestKorekta) btnPowiaz.IsEnabled = true;
                    break;

                case StatusyV2.W_ANALIZIE:
                    btnZasadna.IsEnabled = true;
                    btnNiezasadna.IsEnabled = true;
                    if (jestKorekta) btnPowiaz.IsEnabled = true;
                    break;

                case StatusyV2.ZASADNA:
                    btnNiezasadna.IsEnabled = true;
                    btnPowiaz.IsEnabled = true;
                    break;

                case StatusyV2.POWIAZANA:
                    btnPowiaz.IsEnabled = true; // do odlaczenia
                    btnZamknij.IsEnabled = true;
                    break;

                case StatusyV2.ZAMKNIETA:
                case StatusyV2.ODRZUCONA:
                    break;
            }

            // Komunikat pomocniczy
            if (txtJakoscBrakAkcji != null)
            {
                bool jakiekolwiek = btnPrzekazAnaliza.IsEnabled || btnZasadna.IsEnabled
                                  || btnNiezasadna.IsEnabled || btnPowiaz.IsEnabled
                                  || btnZamknij.IsEnabled;
                txtJakoscBrakAkcji.Visibility = Visibility.Visible;
                txtJakoscBrakAkcji.Text = jakiekolwiek
                    ? $"Aktualny status: '{StatusyV2.Etykieta(s)}' — kliknij aktywny (kolorowy) przycisk aby przejsc do nastepnego kroku"
                    : $"Status '{StatusyV2.Etykieta(s)}' — sprawa zakonczona, brak dalszych akcji";
            }

            // Pasek workflow usuniety z UI - no-op
        }

        private void AktualizujPasekStatusu(string status) { /* usuniete z UI */ }

        private void DgReklamacje_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            BtnSzczegoly_Click(sender, e);
        }

        private void BtnSzczegoly_Click(object sender, RoutedEventArgs e)
        {
            if (dgReklamacje.SelectedItem is ReklamacjaItem item)
            {
                try
                {
                    var window = new FormSzczegolyReklamacjiWindow(connectionString, item.Id, userId);
                    window.Owner = this;
                    window.ShowDialog();

                    if (window.StatusZmieniony)
                    {
                        WczytajReklamacje();
                        WczytajStatystyki();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Blad podczas otwierania szczegolow:\n{ex.Message}",
                        "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnZmienStatus_Click(object sender, RoutedEventArgs e)
        {
            if (dgReklamacje.SelectedItem is ReklamacjaItem item)
            {
                var dialog = new Window
                {
                    Title = "Zmiana statusu reklamacji",
                    Width = 400,
                    Height = 220,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize
                };

                var grid = new Grid { Margin = new Thickness(20) };
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var label = new TextBlock
                {
                    Text = $"Zmiana statusu reklamacji #{item.Id}",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 15)
                };
                Grid.SetRow(label, 0);
                grid.Children.Add(label);

                var labelStatus = new TextBlock { Text = "Wybierz nowy status:", Margin = new Thickness(0, 0, 0, 5) };
                Grid.SetRow(labelStatus, 1);
                grid.Children.Add(labelStatus);

                var combo = new ComboBox { Margin = new Thickness(0, 0, 0, 20) };

                // Ogranicz dozwolone przejscia statusow
                bool isAdmin = userId == "11111";
                if (isAdmin)
                {
                    foreach (var s in FormRozpatrzenieWindow.statusPipeline)
                        combo.Items.Add(s);
                }
                else if (dozwolonePrzejscia.ContainsKey(item.Status))
                {
                    foreach (var s in dozwolonePrzejscia[item.Status])
                        combo.Items.Add(s);
                }

                if (combo.Items.Count == 0)
                {
                    MessageBox.Show("Reklamacja jest zamknieta. Tylko administrator moze zmienic status.",
                        "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                combo.SelectedIndex = 0;
                Grid.SetRow(combo, 2);
                grid.Children.Add(combo);

                var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

                var btnOK = new Button
                {
                    Content = "Zapisz",
                    Width = 100,
                    Height = 35,
                    Background = System.Windows.Media.Brushes.Green,
                    Foreground = System.Windows.Media.Brushes.White,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                btnOK.Click += (s, args) =>
                {
                    ZmienStatus(item.Id, combo.SelectedItem?.ToString());
                    dialog.DialogResult = true;
                    dialog.Close();
                };
                buttonsPanel.Children.Add(btnOK);

                var btnCancel = new Button { Content = "Anuluj", Width = 100, Height = 35 };
                btnCancel.Click += (s, args) => dialog.Close();
                buttonsPanel.Children.Add(btnCancel);

                Grid.SetRow(buttonsPanel, 3);
                grid.Children.Add(buttonsPanel);

                dialog.Content = grid;

                if (dialog.ShowDialog() == true)
                {
                    WczytajReklamacje();
                    WczytajStatystyki();
                }
            }
        }

        private void BtnZaakceptuj_Click(object sender, RoutedEventArgs e)
        {
            if (dgReklamacje.SelectedItem is ReklamacjaItem item)
            {
                // Walidacja przejscia
                if (!CzyPrzejscieDozwolone(item.Status, "Zaakceptowana"))
                {
                    MessageBox.Show($"Nie mozna zaakceptowac reklamacji o statusie '{item.Status}'.",
                        "Niedozwolone przejscie", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (MessageBox.Show($"Czy na pewno chcesz zaakceptowac reklamacje #{item.Id}?",
                    "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    ZmienStatus(item.Id, "Zaakceptowana");
                    WczytajReklamacje();
                    WczytajStatystyki();
                }
            }
        }

        private void BtnOdrzuc_Click(object sender, RoutedEventArgs e)
        {
            if (dgReklamacje.SelectedItem is ReklamacjaItem item)
            {
                // Walidacja przejscia
                if (!CzyPrzejscieDozwolone(item.Status, "Odrzucona"))
                {
                    MessageBox.Show($"Nie mozna odrzucic reklamacji o statusie '{item.Status}'.",
                        "Niedozwolone przejscie", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (MessageBox.Show($"Czy na pewno chcesz odrzucic reklamacje #{item.Id}?",
                    "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    ZmienStatus(item.Id, "Odrzucona");
                    WczytajReklamacje();
                    WczytajStatystyki();
                }
            }
        }

        // ========================================
        // WORKFLOW V2 — 3 przyciski decyzyjne jakosci
        // ========================================

        // PRZYJMIJ - rozpoczyna rozpatrywanie (dostepne dla wszystkich)
        private void BtnPrzekazAnaliza_Click(object sender, RoutedEventArgs e)
        {
            if (!(dgReklamacje.SelectedItem is ReklamacjaItem item)) return;

            if (MessageBox.Show(
                $"Przyjac reklamacje #{item.Id} do rozpatrzenia?\n\nStatus zmieni sie na 'W analizie'. Pozniej bedziesz mogl zdecydowac o akceptacji lub odrzuceniu.",
                "Przyjecie do rozpatrzenia", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            UstawStatusV2Workflow(item.Id, StatusyV2.W_ANALIZIE,
                decyzja: null, notatka: null,
                przyczynaGlowna: null, akcjeNaprawcze: null,
                ustawDataAnalizy: true);

            WczytajReklamacje();
            WczytajStatystyki();
        }

        // ZATWIERDZ - dwa pola: przyczyna + akcje naprawcze (dostepne dla wszystkich)
        private void BtnZasadna_Click(object sender, RoutedEventArgs e)
        {
            if (!(dgReklamacje.SelectedItem is ReklamacjaItem item)) return;

            var (ok, przyczyna, akcje) = PokazDialogZaakceptowana(item);
            if (!ok) return;

            UstawStatusV2Workflow(item.Id, StatusyV2.ZASADNA,
                decyzja: "Zasadna", notatka: null,
                przyczynaGlowna: przyczyna, akcjeNaprawcze: akcje,
                ustawDataAnalizy: true);

            WczytajReklamacje();
            WczytajStatystyki();
        }

        // ODRZUC - jedno pole: powod odrzucenia (dostepne dla wszystkich)
        private void BtnNiezasadna_Click(object sender, RoutedEventArgs e)
        {
            if (!(dgReklamacje.SelectedItem is ReklamacjaItem item)) return;

            var (ok, powod) = PokazDialogOdrzucona(item);
            if (!ok) return;

            UstawStatusV2Workflow(item.Id, StatusyV2.ODRZUCONA,
                decyzja: "Niezasadna", notatka: powod,
                przyczynaGlowna: null, akcjeNaprawcze: null,
                ustawDataAnalizy: true);

            WczytajReklamacje();
            WczytajStatystyki();
        }

        // ZAMKNIJ - bez dialogu (dostepne dla wszystkich)
        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            if (!(dgReklamacje.SelectedItem is ReklamacjaItem item)) return;

            if (MessageBox.Show($"Zamknac reklamacje #{item.Id}?\n\nStatus zostanie zmieniony na ZAMKNIETA.",
                "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            UstawStatusV2Workflow(item.Id, StatusyV2.ZAMKNIETA,
                decyzja: null, notatka: null,
                przyczynaGlowna: null, akcjeNaprawcze: null,
                ustawDataAnalizy: false);

            WczytajReklamacje();
            WczytajStatystyki();
        }

        private void BrakUprawnien()
        {
            MessageBox.Show("Brak uprawnien. Ta operacja jest dostepna tylko dla dzialu jakosci.",
                "Brak uprawnien", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void UstawStatusV2Workflow(int idReklamacji, string nowyStatusV2,
            string decyzja, string notatka,
            string przyczynaGlowna, string akcjeNaprawcze,
            bool ustawDataAnalizy)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(@"
                        UPDATE [dbo].[Reklamacje]
                        SET StatusV2 = @StatusV2,
                            Status = CASE @StatusV2
                                WHEN 'ZGLOSZONA' THEN 'Nowa'
                                WHEN 'W_ANALIZIE' THEN 'W analizie'
                                WHEN 'ZASADNA' THEN 'Zaakceptowana'
                                WHEN 'POWIAZANA' THEN 'Zaakceptowana'
                                WHEN 'ZAMKNIETA' THEN 'Zamknieta'
                                WHEN 'ODRZUCONA' THEN 'Odrzucona'
                                ELSE Status
                            END,
                            DecyzjaJakosci = COALESCE(@Decyzja, DecyzjaJakosci),
                            NotatkaJakosci = CASE
                                WHEN @Notatka IS NOT NULL AND LEN(@Notatka) > 0
                                THEN ISNULL(NotatkaJakosci + CHAR(13) + CHAR(10), '') + @Stempel + @Notatka
                                ELSE NotatkaJakosci
                            END,
                            PrzyczynaGlowna = COALESCE(@PrzyczynaGlowna, PrzyczynaGlowna),
                            AkcjeNaprawcze = COALESCE(@AkcjeNaprawcze, AkcjeNaprawcze),
                            DataAnalizy = CASE WHEN @UstawDataAnalizy = 1 AND DataAnalizy IS NULL THEN GETDATE() ELSE DataAnalizy END,
                            UserAnalizy = CASE WHEN @UstawDataAnalizy = 1 AND UserAnalizy IS NULL THEN @User ELSE UserAnalizy END,
                            -- PRZYJMIJ: jezeli przechodzimy do W_ANALIZIE -> ustaw OsobaRozpatrujaca = aktualny user (jezeli pusta)
                            OsobaRozpatrujaca = CASE
                                WHEN @StatusV2 = 'W_ANALIZIE' AND (OsobaRozpatrujaca IS NULL OR LEN(OsobaRozpatrujaca) = 0)
                                THEN @User
                                ELSE OsobaRozpatrujaca
                            END,
                            -- ZAKONCZYL: kazda decyzja koncowa (ZASADNA/ODRZUCONA/ZAMKNIETA) -> kto i kiedy zakonczyl
                            DataZakonczenia = CASE
                                WHEN @StatusV2 IN ('ZASADNA','ODRZUCONA','ZAMKNIETA') AND DataZakonczenia IS NULL
                                THEN GETDATE()
                                ELSE DataZakonczenia
                            END,
                            UserZakonczenia = CASE
                                WHEN @StatusV2 IN ('ZASADNA','ODRZUCONA','ZAMKNIETA') AND UserZakonczenia IS NULL
                                THEN @User
                                ELSE UserZakonczenia
                            END,
                            WymagaUzupelnienia = CASE WHEN @StatusV2 IN ('W_ANALIZIE','ZASADNA','POWIAZANA','ZAMKNIETA','ODRZUCONA') THEN 0 ELSE WymagaUzupelnienia END
                        WHERE Id = @Id;", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", idReklamacji);
                        cmd.Parameters.AddWithValue("@StatusV2", nowyStatusV2);
                        cmd.Parameters.AddWithValue("@Decyzja", (object)decyzja ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Notatka", (object)notatka ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@PrzyczynaGlowna", (object)przyczynaGlowna ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@AkcjeNaprawcze", (object)akcjeNaprawcze ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Stempel", $"[{DateTime.Now:yyyy-MM-dd HH:mm} {userId}] ");
                        cmd.Parameters.AddWithValue("@UstawDataAnalizy", ustawDataAnalizy ? 1 : 0);
                        cmd.Parameters.AddWithValue("@User", userId ?? "");
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                FriendlyError.Pokaz(ex, "Nie udalo sie zmienic statusu reklamacji.", this);
            }
        }

        // ========================================
        // DIALOG: ZAAKCEPTOWANA (przyczyna + akcje naprawcze)
        // ========================================
        private (bool ok, string przyczyna, string akcje) PokazDialogZaakceptowana(ReklamacjaItem item)
        {
            var dialog = new Window
            {
                Title = "Akceptacja reklamacji jako zasadna",
                Width = 680, Height = 620,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = Brushes.White,
                FontFamily = new FontFamily("Segoe UI")
            };
            WindowIconHelper.SetIcon(dialog);

            var root = new Grid();
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

            // Header zielony
            var hdr = new Border
            {
                Padding = new Thickness(26, 18, 26, 18),
                Background = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#27AE60"),
                    (Color)ColorConverter.ConvertFromString("#229954"),
                    new System.Windows.Point(0, 0), new System.Windows.Point(1, 0))
            };
            var hdrStack = new StackPanel();
            hdrStack.Children.Add(new TextBlock
            {
                Text = "REKLAMACJA ZASADNA",
                FontSize = 17, FontWeight = FontWeights.Bold, Foreground = Brushes.White
            });
            hdrStack.Children.Add(new TextBlock
            {
                Text = $"#{item.Id}   |   {item.NumerDokumentu}   |   {item.NazwaKontrahenta}",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                Margin = new Thickness(0, 4, 0, 0)
            });
            hdr.Child = hdrStack;
            Grid.SetRow(hdr, 0);
            root.Children.Add(hdr);

            // Body
            var body = new StackPanel { Margin = new Thickness(26, 20, 26, 14) };

            TextBlock L(string text, string kolor = "#27AE60") => new TextBlock
            {
                Text = text,
                FontSize = 11.5, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kolor)),
                Margin = new Thickness(0, 0, 0, 6)
            };

            body.Children.Add(L("PRZYCZYNA GLOWNA *"));
            body.Children.Add(new TextBlock
            {
                Text = "Co bylo przyczyna problemu? (np. uszkodzona partia, bled pracownika, awaria urzadzenia)",
                FontSize = 10.5, FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6")),
                Margin = new Thickness(0, 0, 0, 6),
                TextWrapping = TextWrapping.Wrap
            });
            var txtPrzyczyna = new TextBox
            {
                FontSize = 13,
                Padding = new Thickness(12, 10, 12, 10),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 90,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A5D6A7")),
                BorderThickness = new Thickness(1.5),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FFF8")),
                Margin = new Thickness(0, 0, 0, 18)
            };
            body.Children.Add(txtPrzyczyna);

            body.Children.Add(L("AKCJE NAPRAWCZE *"));
            body.Children.Add(new TextBlock
            {
                Text = "Co robimy aby naprawic sytuacje? (np. zwrot towaru, korekta faktury, wymiana partii, zmiana procedury)",
                FontSize = 10.5, FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6")),
                Margin = new Thickness(0, 0, 0, 6),
                TextWrapping = TextWrapping.Wrap
            });
            var txtAkcje = new TextBox
            {
                FontSize = 13,
                Padding = new Thickness(12, 10, 12, 10),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 90,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A5D6A7")),
                BorderThickness = new Thickness(1.5),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FFF8"))
            };
            body.Children.Add(txtAkcje);

            Grid.SetRow(body, 1);
            root.Children.Add(body);

            // Footer
            var footer = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F9FA")),
                Padding = new Thickness(26, 14, 26, 14),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E8EB")),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            var footerStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnAnuluj = new Button
            {
                Content = "Anuluj",
                Padding = new Thickness(22, 10, 22, 10),
                FontSize = 12.5,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECF0F1")),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D")),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0),
                IsCancel = true
            };
            var btnOk = new Button
            {
                Content = "Potwierdz akceptacje",
                Padding = new Thickness(26, 10, 26, 10),
                FontSize = 12.5, FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                IsDefault = true
            };
            footerStack.Children.Add(btnAnuluj);
            footerStack.Children.Add(btnOk);
            footer.Child = footerStack;
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            dialog.Content = root;

            bool wynik = false;
            btnAnuluj.Click += (s, ev) => { wynik = false; dialog.Close(); };
            btnOk.Click += (s, ev) =>
            {
                if (string.IsNullOrWhiteSpace(txtPrzyczyna.Text))
                {
                    MessageBox.Show("Podaj przyczyne glowna.", "Brak danych", MessageBoxButton.OK, MessageBoxImage.Information);
                    txtPrzyczyna.Focus();
                    return;
                }
                if (string.IsNullOrWhiteSpace(txtAkcje.Text))
                {
                    MessageBox.Show("Podaj akcje naprawcze.", "Brak danych", MessageBoxButton.OK, MessageBoxImage.Information);
                    txtAkcje.Focus();
                    return;
                }
                wynik = true;
                dialog.Close();
            };

            dialog.Loaded += (s, ev) => txtPrzyczyna.Focus();
            dialog.ShowDialog();

            return (wynik, txtPrzyczyna.Text?.Trim() ?? "", txtAkcje.Text?.Trim() ?? "");
        }

        // ========================================
        // DIALOG: ODRZUCONA (powod odrzucenia)
        // ========================================
        private (bool ok, string powod) PokazDialogOdrzucona(ReklamacjaItem item)
        {
            var dialog = new Window
            {
                Title = "Odrzucenie reklamacji",
                Width = 620, Height = 480,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = Brushes.White,
                FontFamily = new FontFamily("Segoe UI")
            };
            WindowIconHelper.SetIcon(dialog);

            var root = new Grid();
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

            // Header czerwony
            var hdr = new Border
            {
                Padding = new Thickness(26, 18, 26, 18),
                Background = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#E74C3C"),
                    (Color)ColorConverter.ConvertFromString("#C0392B"),
                    new System.Windows.Point(0, 0), new System.Windows.Point(1, 0))
            };
            var hdrStack = new StackPanel();
            hdrStack.Children.Add(new TextBlock
            {
                Text = "REKLAMACJA ODRZUCONA",
                FontSize = 17, FontWeight = FontWeights.Bold, Foreground = Brushes.White
            });
            hdrStack.Children.Add(new TextBlock
            {
                Text = $"#{item.Id}   |   {item.NumerDokumentu}   |   {item.NazwaKontrahenta}",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                Margin = new Thickness(0, 4, 0, 0)
            });
            hdr.Child = hdrStack;
            Grid.SetRow(hdr, 0);
            root.Children.Add(hdr);

            // Body
            var body = new StackPanel { Margin = new Thickness(26, 20, 26, 14) };
            body.Children.Add(new TextBlock
            {
                Text = "POWOD ODRZUCENIA *",
                FontSize = 11.5, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C0392B")),
                Margin = new Thickness(0, 0, 0, 6)
            });
            body.Children.Add(new TextBlock
            {
                Text = "Dlaczego reklamacja jest odrzucana? (np. brak dowodu, niewlasciwy typ problemu, minimal nie spelnia kryteriow)",
                FontSize = 10.5, FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6")),
                Margin = new Thickness(0, 0, 0, 6),
                TextWrapping = TextWrapping.Wrap
            });
            var txtPowod = new TextBox
            {
                FontSize = 13,
                Padding = new Thickness(12, 10, 12, 10),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 160,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5B7B1")),
                BorderThickness = new Thickness(1.5),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF8F7"))
            };
            body.Children.Add(txtPowod);
            Grid.SetRow(body, 1);
            root.Children.Add(body);

            // Footer
            var footer = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F9FA")),
                Padding = new Thickness(26, 14, 26, 14),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E8EB")),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            var footerStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnAnuluj = new Button
            {
                Content = "Anuluj",
                Padding = new Thickness(22, 10, 22, 10),
                FontSize = 12.5,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECF0F1")),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D")),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0),
                IsCancel = true
            };
            var btnOk = new Button
            {
                Content = "Potwierdz odrzucenie",
                Padding = new Thickness(26, 10, 26, 10),
                FontSize = 12.5, FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                IsDefault = true
            };
            footerStack.Children.Add(btnAnuluj);
            footerStack.Children.Add(btnOk);
            footer.Child = footerStack;
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            dialog.Content = root;

            bool wynik = false;
            btnAnuluj.Click += (s, ev) => { wynik = false; dialog.Close(); };
            btnOk.Click += (s, ev) =>
            {
                if (string.IsNullOrWhiteSpace(txtPowod.Text))
                {
                    MessageBox.Show("Podaj powod odrzucenia.", "Brak danych", MessageBoxButton.OK, MessageBoxImage.Information);
                    txtPowod.Focus();
                    return;
                }
                wynik = true;
                dialog.Close();
            };

            dialog.Loaded += (s, ev) => txtPowod.Focus();
            dialog.ShowDialog();

            return (wynik, txtPowod.Text?.Trim() ?? "");
        }

        // Maly dialog z polem notatki, zwraca (ok, tekst)
        private void ZmienStatus(int idReklamacji, string nowyStatus)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // Pobierz poprzedni status
                            string poprzedniStatus = "";
                            using (var cmdGet = new SqlCommand("SELECT Status FROM [dbo].[Reklamacje] WHERE Id = @Id", conn, transaction))
                            {
                                cmdGet.Parameters.AddWithValue("@Id", idReklamacji);
                                poprzedniStatus = cmdGet.ExecuteScalar()?.ToString() ?? "";
                            }

                            // Mapowanie Status -> StatusV2
                            string nowyStatusV2 = nowyStatus switch
                            {
                                "Przyjeta" => StatusyV2.W_ANALIZIE,
                                "Zaakceptowana" => StatusyV2.ZASADNA,
                                "Odrzucona" => StatusyV2.ODRZUCONA,
                                "Zamknieta" => StatusyV2.ZAMKNIETA,
                                "Nowa" => StatusyV2.ZGLOSZONA,
                                _ => StatusyV2.ZGLOSZONA
                            };

                            // Aktualizuj reklamacje
                            string query = @"
                                UPDATE [dbo].[Reklamacje]
                                SET Status = @Status,
                                    StatusV2 = @StatusV2,
                                    OsobaRozpatrujaca = @Osoba,
                                    DataModyfikacji = GETDATE(),
                                    WymagaUzupelnienia = CASE WHEN @StatusV2 IN ('ZASADNA','ODRZUCONA','ZAMKNIETA') THEN 0 ELSE WymagaUzupelnienia END,
                                    DataAnalizy = CASE WHEN DataAnalizy IS NULL THEN GETDATE() ELSE DataAnalizy END,
                                    UserAnalizy = CASE WHEN UserAnalizy IS NULL THEN @Osoba ELSE UserAnalizy END,
                                    DataZamkniecia = CASE WHEN @Status = 'Zamknieta' THEN GETDATE() ELSE DataZamkniecia END
                                WHERE Id = @Id";

                            using (SqlCommand cmd = new SqlCommand(query, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Status", nowyStatus);
                                cmd.Parameters.AddWithValue("@StatusV2", nowyStatusV2);
                                cmd.Parameters.AddWithValue("@Osoba", userId);
                                cmd.Parameters.AddWithValue("@Id", idReklamacji);
                                cmd.ExecuteNonQuery();
                            }

                            // Dodaj wpis do historii z PoprzedniStatus
                            string queryHistoria = @"
                                INSERT INTO [dbo].[ReklamacjeHistoria]
                                (IdReklamacji, UserID, PoprzedniStatus, StatusNowy, Komentarz, TypAkcji)
                                VALUES
                                (@IdReklamacji, @UserID, @PoprzedniStatus, @StatusNowy, @Komentarz, 'ZmianaStatusu')";

                            using (SqlCommand cmd = new SqlCommand(queryHistoria, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                                cmd.Parameters.AddWithValue("@UserID", userId);
                                cmd.Parameters.AddWithValue("@PoprzedniStatus", poprzedniStatus);
                                cmd.Parameters.AddWithValue("@StatusNowy", nowyStatus);
                                cmd.Parameters.AddWithValue("@Komentarz", $"Zmiana statusu na: {nowyStatus}");
                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }

                MessageBox.Show($"Status reklamacji #{idReklamacji} zostal zmieniony na: {nowyStatus}",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad podczas zmiany statusu:\n{ex.Message}",
                    "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CmbTyp_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            WczytajReklamacje();
        }

        private void CmbPriorytet_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            WczytajReklamacje();
        }

        private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (reklamacje.Count == 0)
                {
                    MessageBox.Show("Brak danych do eksportu.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Plik CSV (*.csv)|*.csv",
                    FileName = $"Reklamacje_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    Title = "Eksportuj reklamacje"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var sb = new System.Text.StringBuilder();
                    // Naglowki
                    sb.AppendLine("ID;Data;Nr faktury;Kontrahent;Typ;Priorytet;Kg;Status;Zglaszajacy");

                    // Dane
                    foreach (var r in reklamacje)
                    {
                        sb.AppendLine($"{r.Id};{r.DataZgloszenia:yyyy-MM-dd};\"{r.NumerDokumentu?.Replace("\"", "\"\"")}\";\"{r.NazwaKontrahenta?.Replace("\"", "\"\"")}\";\"{r.TypReklamacji}\";\"{r.Priorytet}\";{r.SumaKg:N2};\"{r.Status}\";\"{r.Zglaszajacy?.Replace("\"", "\"\"")}\"");
                    }

                    System.IO.File.WriteAllText(saveDialog.FileName, sb.ToString(), System.Text.Encoding.UTF8);

                    MessageBox.Show($"Eksportowano {reklamacje.Count} reklamacji do pliku:\n{saveDialog.FileName}",
                        "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad eksportu: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static Dictionary<string, ImageSource> _avatarCache = new Dictionary<string, ImageSource>();
        private static ImageSource GetCachedAvatar(string odbiorcaId, string userName)
        {
            if (string.IsNullOrEmpty(odbiorcaId)) return null;
            if (_avatarCache.TryGetValue(odbiorcaId, out var cached)) return cached;
            var avatar = FormRozpatrzenieWindow.LoadWpfAvatar(odbiorcaId, userName, 64);
            _avatarCache[odbiorcaId] = avatar;
            return avatar;
        }

        // Avatar handlowca — wzorzec z HandlowiecDashboardWindow
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private ImageSource GetHandlowiecAvatar(string handlowiec)
        {
            if (string.IsNullOrEmpty(handlowiec) || handlowiec == "-") return null;
            if (_handlowiecAvatarCache.TryGetValue(handlowiec, out var cached)) return cached;
            if (_handlowiecMapowanie == null) return null;

            ImageSource result = null;

            // 1) Sprawdz mapowanie name -> UserID
            if (_handlowiecMapowanie.TryGetValue(handlowiec, out var uid))
            {
                try
                {
                    // 2) Prawdziwy avatar z udzialu sieciowego
                    if (UserAvatarManager.HasAvatar(uid))
                    {
                        using (var av = UserAvatarManager.GetAvatarRounded(uid, 64))
                            if (av != null) result = BitmapToWpf(av);
                    }
                    // 3) Fallback: wygenerowany avatar z inicjalami (kolor wg uid)
                    if (result == null)
                    {
                        using (var defAv = UserAvatarManager.GenerateDefaultAvatar(handlowiec, uid, 64))
                            result = BitmapToWpf(defAv);
                    }
                }
                catch { }
            }

            // 4) Brak w mapowaniu — generuj z samej nazwy
            if (result == null)
            {
                try
                {
                    using (var defAv = UserAvatarManager.GenerateDefaultAvatar(handlowiec, handlowiec, 64))
                        result = BitmapToWpf(defAv);
                }
                catch { }
            }

            if (result != null)
                _handlowiecAvatarCache[handlowiec] = result;

            return result;
        }

        private static ImageSource BitmapToWpf(System.Drawing.Image image)
        {
            if (image == null) return null;
            using (var bmp = new System.Drawing.Bitmap(image))
            {
                var hBitmap = bmp.GetHbitmap();
                try
                {
                    var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    return source;
                }
                finally { DeleteObject(hBitmap); }
            }
        }

        private bool CzyPrzejscieDozwolone(string obecnyStatus, string nowyStatus)
        {
            if (userId == "11111") return true; // Admin
            if (!dozwolonePrzejscia.ContainsKey(obecnyStatus)) return false;
            return dozwolonePrzejscia[obecnyStatus].Contains(nowyStatus);
        }

        private void BtnStatystyki_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Pobierz statystyki z bazy (nie tylko z aktualnie zaladowanych)
                var statRows = new List<(string kontrahent, int ile, decimal kg, decimal wartosc, int zasadne, int odrzucone, double srDni)>();
                int ogolneLaczna = 0, ogolneZasadne = 0, ogolneOdrzucone = 0;
                decimal ogolneKg = 0;
                double ogolneSrDni = 0;

                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT
                            NazwaKontrahenta,
                            COUNT(*) AS Ile,
                            SUM(ISNULL(SumaKg,0)) AS Kg,
                            SUM(ISNULL(SumaWartosc,0)) AS Wartosc,
                            SUM(CASE WHEN StatusV2 = 'ZASADNA' OR StatusV2 = 'POWIAZANA' OR StatusV2 = 'ZAMKNIETA' THEN 1 ELSE 0 END) AS Zasadne,
                            SUM(CASE WHEN StatusV2 = 'ODRZUCONA' THEN 1 ELSE 0 END) AS Odrzucone,
                            AVG(CAST(DATEDIFF(DAY, DataZgloszenia, ISNULL(DataAnalizy, GETDATE())) AS FLOAT)) AS SrDni
                        FROM [dbo].[Reklamacje]
                        WHERE TypReklamacji <> 'Faktura korygujaca'
                        GROUP BY NazwaKontrahenta
                        HAVING COUNT(*) >= 2
                        ORDER BY COUNT(*) DESC", conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            statRows.Add((
                                r.IsDBNull(0) ? "" : r.GetString(0),
                                r.GetInt32(1),
                                r.IsDBNull(2) ? 0m : Convert.ToDecimal(r.GetValue(2)),
                                r.IsDBNull(3) ? 0m : Convert.ToDecimal(r.GetValue(3)),
                                r.GetInt32(4),
                                r.GetInt32(5),
                                r.IsDBNull(6) ? 0 : r.GetDouble(6)
                            ));
                        }
                    }

                    using (var cmd = new SqlCommand(@"
                        SELECT COUNT(*),
                            SUM(CASE WHEN StatusV2 IN ('ZASADNA','POWIAZANA','ZAMKNIETA') THEN 1 ELSE 0 END),
                            SUM(CASE WHEN StatusV2 = 'ODRZUCONA' THEN 1 ELSE 0 END),
                            SUM(ISNULL(SumaKg,0)),
                            AVG(CAST(DATEDIFF(DAY, DataZgloszenia, ISNULL(DataAnalizy, GETDATE())) AS FLOAT))
                        FROM [dbo].[Reklamacje]
                        WHERE TypReklamacji <> 'Faktura korygujaca'", conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            ogolneLaczna = r.GetInt32(0);
                            ogolneZasadne = r.GetInt32(1);
                            ogolneOdrzucone = r.GetInt32(2);
                            ogolneKg = r.IsDBNull(3) ? 0m : Convert.ToDecimal(r.GetValue(3));
                            ogolneSrDni = r.IsDBNull(4) ? 0 : r.GetDouble(4);
                        }
                    }
                }

                // Pokaz dialog statystyk
                var dialog = new Window
                {
                    Title = "Statystyki reklamacji",
                    Width = 1000, Height = 700,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4F6F8")),
                    FontFamily = new FontFamily("Segoe UI")
                };
                WindowIconHelper.SetIcon(dialog);

                var root = new Grid();
                root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
                root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
                root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // Header
                var hdr = new Border
                {
                    Padding = new Thickness(24, 18, 24, 18),
                    Background = new LinearGradientBrush(
                        (Color)ColorConverter.ConvertFromString("#9B59B6"),
                        (Color)ColorConverter.ConvertFromString("#8E44AD"),
                        new System.Windows.Point(0, 0), new System.Windows.Point(1, 0))
                };
                var hdrStack = new StackPanel();
                hdrStack.Children.Add(new TextBlock { Text = "STATYSTYKI REKLAMACJI", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
                hdrStack.Children.Add(new TextBlock { Text = "Analiza wg kontrahentow — ranking problematycznych odbiorcow", FontSize = 12, Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), Margin = new Thickness(0, 4, 0, 0) });
                hdr.Child = hdrStack;
                Grid.SetRow(hdr, 0);
                root.Children.Add(hdr);

                // Kafelki ogolne
                var statsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 16, 20, 0) };
                void DodajKafelek(string tytul, string wartosc, string kolor)
                {
                    var card = new Border
                    {
                        Background = Brushes.White,
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(20, 14, 20, 14),
                        Margin = new Thickness(0, 0, 12, 0),
                        MinWidth = 140,
                        Effect = new System.Windows.Media.Effects.DropShadowEffect { ShadowDepth = 1, Opacity = 0.08, BlurRadius = 8 }
                    };
                    var sp = new StackPanel();
                    sp.Children.Add(new TextBlock { Text = tytul, FontSize = 10, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D")), FontWeight = FontWeights.SemiBold });
                    sp.Children.Add(new TextBlock { Text = wartosc, FontSize = 22, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kolor)), Margin = new Thickness(0, 4, 0, 0) });
                    card.Child = sp;
                    statsPanel.Children.Add(card);
                }
                DodajKafelek("LACZNIE", ogolneLaczna.ToString(), "#2C3E50");
                DodajKafelek("ZASADNE", ogolneZasadne.ToString(), "#27AE60");
                DodajKafelek("ODRZUCONE", ogolneOdrzucone.ToString(), "#E74C3C");
                double pctZasadne = ogolneLaczna > 0 ? (double)ogolneZasadne / ogolneLaczna * 100 : 0;
                DodajKafelek("% ZASADNYCH", $"{pctZasadne:F0}%", "#8E44AD");
                DodajKafelek("SR. CZAS (dni)", $"{ogolneSrDni:F1}", "#F39C12");
                DodajKafelek("SUMA KG", $"{ogolneKg:#,##0}", "#3498DB");
                Grid.SetRow(statsPanel, 1);
                root.Children.Add(statsPanel);

                // DataGrid ranking
                var dgStats = new DataGrid
                {
                    AutoGenerateColumns = false,
                    IsReadOnly = true,
                    HeadersVisibility = DataGridHeadersVisibility.Column,
                    GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                    HorizontalGridLinesBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0")),
                    BorderThickness = new Thickness(0),
                    RowHeight = 36,
                    FontSize = 12,
                    Background = Brushes.White,
                    RowBackground = Brushes.White,
                    AlternatingRowBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFBFC")),
                    Margin = new Thickness(20, 14, 20, 20),
                    ItemsSource = statRows.Select((s, idx) => new { Lp = idx + 1, s.kontrahent, s.ile, s.kg, s.wartosc, s.zasadne, s.odrzucone, s.srDni }).ToList()
                };
                dgStats.Columns.Add(new DataGridTextColumn { Header = "#", Binding = new System.Windows.Data.Binding("Lp"), Width = new DataGridLength(40) });
                dgStats.Columns.Add(new DataGridTextColumn { Header = "KONTRAHENT", Binding = new System.Windows.Data.Binding("kontrahent"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
                dgStats.Columns.Add(new DataGridTextColumn { Header = "REKLAMACJI", Binding = new System.Windows.Data.Binding("ile"), Width = new DataGridLength(100) });
                dgStats.Columns.Add(new DataGridTextColumn { Header = "KG", Binding = new System.Windows.Data.Binding("kg") { StringFormat = "#,##0.00" }, Width = new DataGridLength(100) });
                dgStats.Columns.Add(new DataGridTextColumn { Header = "WARTOSC", Binding = new System.Windows.Data.Binding("wartosc") { StringFormat = "#,##0.00 zl" }, Width = new DataGridLength(120) });
                dgStats.Columns.Add(new DataGridTextColumn { Header = "ZASADNE", Binding = new System.Windows.Data.Binding("zasadne"), Width = new DataGridLength(80) });
                dgStats.Columns.Add(new DataGridTextColumn { Header = "ODRZUCONE", Binding = new System.Windows.Data.Binding("odrzucone"), Width = new DataGridLength(90) });
                dgStats.Columns.Add(new DataGridTextColumn { Header = "SR. DNI", Binding = new System.Windows.Data.Binding("srDni") { StringFormat = "F1" }, Width = new DataGridLength(80) });
                Grid.SetRow(dgStats, 2);
                root.Children.Add(dgStats);

                dialog.Content = root;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                FriendlyError.Pokaz(ex, "Nie udalo sie wygenerowac statystyk.", this);
            }
        }

        // ========================================
        // POWIAZYWANIE REKLAMACJI
        // ========================================

        private void BtnPowiaz_Click(object sender, RoutedEventArgs e)
        {
            if (!(dgReklamacje.SelectedItem is ReklamacjaItem item)) return;

            // Jesli juz powiazana - pytaj czy odlaczyc
            if (item.MaPowiazanie)
            {
                var wynik = MessageBox.Show(
                    $"Reklamacja #{item.Id} jest powiazana z #{item.PowiazanaReklamacjaId}.\n\nCzy chcesz odlaczyc powiazanie?",
                    "Powiazanie", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (wynik == MessageBoxResult.Yes)
                {
                    OdlaczPowiazanie(item.Id, item.PowiazanaReklamacjaId.Value);
                    WczytajReklamacje();
                }
                return;
            }

            // Okresl typ przeciwny
            bool jestKorekta = item.TypReklamacji == "Faktura korygujaca";
            string szukanyTyp = jestKorekta ? "Faktura korygujaca" : "Faktura korygujaca";
            string tytulOkna = jestKorekta
                ? $"Powiaz korekter #{item.Id} z reklamacja handlowca"
                : $"Powiaz reklamacje #{item.Id} z korekta z Symfonii";

            // Pobierz niepowiazane reklamacje przeciwnego typu
            var kandydaci = new List<ReklamacjaItem>();
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = jestKorekta
                        ? @"SELECT Id, DataZgloszenia, NumerDokumentu, NazwaKontrahenta, ISNULL(Opis,'') AS Opis,
                                   ISNULL(SumaKg,0) AS SumaKg, ISNULL(TypReklamacji,'Inne') AS TypReklamacji
                            FROM [dbo].[Reklamacje]
                            WHERE TypReklamacji <> 'Faktura korygujaca'
                              AND (PowiazanaReklamacjaId IS NULL OR PowiazanaReklamacjaId = 0)
                            ORDER BY DataZgloszenia DESC"
                        : @"SELECT Id, DataZgloszenia, NumerDokumentu, NazwaKontrahenta, ISNULL(Opis,'') AS Opis,
                                   ISNULL(SumaKg,0) AS SumaKg, ISNULL(TypReklamacji,'Inne') AS TypReklamacji
                            FROM [dbo].[Reklamacje]
                            WHERE TypReklamacji = 'Faktura korygujaca'
                              AND (PowiazanaReklamacjaId IS NULL OR PowiazanaReklamacjaId = 0)
                            ORDER BY DataZgloszenia DESC";

                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            kandydaci.Add(new ReklamacjaItem
                            {
                                Id = reader.GetInt32(0),
                                DataZgloszenia = reader.IsDBNull(1) ? DateTime.MinValue : reader.GetDateTime(1),
                                NumerDokumentu = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                NazwaKontrahenta = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                Opis = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                SumaKg = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                                TypReklamacji = reader.IsDBNull(6) ? "" : reader.GetString(6)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                FriendlyError.Pokaz(ex, "Nie udalo sie pobrac kandydatow do powiazania.", this);
                return;
            }

            if (kandydaci.Count == 0)
            {
                MessageBox.Show(
                    jestKorekta
                        ? "Nie ma niepowiazanych reklamacji od handlowcow."
                        : "Nie ma niepowiazanych korekt z Symfonii.",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // === MATCH HELPER === oblicz score dopasowania dla kazdego kandydata
            // Algorytm: kontrahent (40%) + data ±3 dni (20%) + kg ±10% (20%) + numer dokumentu zawiera fragment (20%)
            foreach (var k in kandydaci)
            {
                int score = 0;
                // Kontrahent (40%)
                if (!string.IsNullOrEmpty(item.NazwaKontrahenta) && !string.IsNullOrEmpty(k.NazwaKontrahenta))
                {
                    var a = item.NazwaKontrahenta.Trim().ToLowerInvariant();
                    var b = k.NazwaKontrahenta.Trim().ToLowerInvariant();
                    if (a == b) score += 40;
                    else if (a.Length >= 4 && b.Contains(a.Substring(0, Math.Min(8, a.Length)))) score += 30;
                    else if (a.Length >= 4 && a.Substring(0, 4) == b.Substring(0, Math.Min(4, b.Length))) score += 20;
                }
                // Data ±N dni (20%)
                if (item.DataZgloszenia != DateTime.MinValue && k.DataZgloszenia != DateTime.MinValue)
                {
                    var dni = Math.Abs((item.DataZgloszenia - k.DataZgloszenia).TotalDays);
                    if (dni <= 1) score += 20;
                    else if (dni <= 3) score += 15;
                    else if (dni <= 7) score += 10;
                    else if (dni <= 14) score += 5;
                }
                // Kg ±10% (20%)
                if (item.SumaKg > 0 && k.SumaKg > 0)
                {
                    decimal roznicaProc = Math.Abs((item.SumaKg - k.SumaKg) / item.SumaKg);
                    if (roznicaProc <= 0.05m) score += 20;
                    else if (roznicaProc <= 0.10m) score += 15;
                    else if (roznicaProc <= 0.20m) score += 10;
                    else if (roznicaProc <= 0.50m) score += 5;
                }
                // Nr dokumentu zawiera fragment (20%)
                if (!string.IsNullOrEmpty(item.NumerDokumentu) && !string.IsNullOrEmpty(k.NumerDokumentu))
                {
                    var a = item.NumerDokumentu.Trim();
                    var b = k.NumerDokumentu.Trim();
                    // Wyciagnij liczby z numeru
                    var liczbyA = System.Text.RegularExpressions.Regex.Matches(a, @"\d+");
                    var liczbyB = System.Text.RegularExpressions.Regex.Matches(b, @"\d+");
                    foreach (System.Text.RegularExpressions.Match mA in liczbyA)
                    {
                        foreach (System.Text.RegularExpressions.Match mB in liczbyB)
                        {
                            if (mA.Value == mB.Value && mA.Value.Length >= 3)
                            {
                                score += 20;
                                goto endNumer;
                            }
                        }
                    }
                    endNumer:;
                }
                k.MatchScore = Math.Min(100, score);
            }
            // Sortuj po score
            kandydaci = kandydaci.OrderByDescending(k => k.MatchScore).ThenByDescending(k => k.DataZgloszenia).ToList();

            // Przygotuj fraze wstepna - pierwsze 7 znakow nazwy kontrahenta
            string frazaWstepna = "";
            if (!string.IsNullOrEmpty(item.NazwaKontrahenta))
                frazaWstepna = item.NazwaKontrahenta.Length > 7
                    ? item.NazwaKontrahenta.Substring(0, 7)
                    : item.NazwaKontrahenta;

            // Dialog wyboru
            var dialog = new Window
            {
                Title = tytulOkna,
                Width = 950,
                Height = 620,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F6FA")),
                FontFamily = new FontFamily("Segoe UI")
            };
            WindowIconHelper.SetIcon(dialog);

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

            // === ROW 0: Header z info o biezacej reklamacji ===
            var headerBorder = new Border
            {
                Padding = new Thickness(20, 14, 20, 14)
            };
            headerBorder.Background = new LinearGradientBrush(
                (Color)ColorConverter.ConvertFromString("#8E44AD"),
                (Color)ColorConverter.ConvertFromString("#9B59B6"),
                0);
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

            var headerLeft = new StackPanel();
            headerLeft.Children.Add(new TextBlock
            {
                Text = jestKorekta ? "POWIAZ KOREKTER Z REKLAMACJA HANDLOWCA" : "POWIAZ REKLAMACJE Z KOREKTA Z SYMFONII",
                FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Brushes.White
            });
            var subInfo = new TextBlock { FontSize = 11.5, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D7BDE2")), Margin = new Thickness(0, 4, 0, 0) };
            subInfo.Inlines.Add(new System.Windows.Documents.Run($"#{item.Id}") { FontWeight = FontWeights.Bold, Foreground = Brushes.White });
            subInfo.Inlines.Add(new System.Windows.Documents.Run($"  |  {item.NumerDokumentu}  |  {item.NazwaKontrahenta}  |  {item.SumaKg:#,##0.00} kg  |  {item.DataZgloszenia:dd.MM.yyyy}"));
            headerLeft.Children.Add(subInfo);
            Grid.SetColumn(headerLeft, 0);
            headerGrid.Children.Add(headerLeft);

            var headerRight = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var badgeTyp = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 4, 12, 4)
            };
            badgeTyp.Child = new TextBlock
            {
                Text = item.TypReklamacji,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11
            };
            headerRight.Children.Add(badgeTyp);
            Grid.SetColumn(headerRight, 1);
            headerGrid.Children.Add(headerRight);

            headerBorder.Child = headerGrid;
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // === ROW 1: Filtr + licznik ===
            var filterBorder = new Border
            {
                Background = Brushes.White,
                Padding = new Thickness(20, 10, 20, 10),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8E8E8")),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var filterGrid = new Grid();
            filterGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });
            filterGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            filterGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });
            filterGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

            filterGrid.Children.Add(new TextBlock
            {
                Text = "Szukaj:",
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });

            var txtFiltr = new TextBox
            {
                Text = frazaWstepna,
                FontSize = 12,
                Padding = new Thickness(10, 6, 10, 6),
                MinWidth = 250,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BDC3C7")),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(txtFiltr, 1);
            filterGrid.Children.Add(txtFiltr);

            var btnWyczysc = new Button
            {
                Content = "Wyczysc",
                Padding = new Thickness(10, 6, 10, 6),
                FontSize = 11,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECF0F1")),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D")),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(btnWyczysc, 2);
            filterGrid.Children.Add(btnWyczysc);

            var txtLicznik = new TextBlock
            {
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0)
            };
            Grid.SetColumn(txtLicznik, 3);
            filterGrid.Children.Add(txtLicznik);

            filterBorder.Child = filterGrid;
            Grid.SetRow(filterBorder, 1);
            mainGrid.Children.Add(filterBorder);

            // === ROW 2: DataGrid ===
            var dgKandydaci = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                SelectionMode = DataGridSelectionMode.Single,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0")),
                BorderThickness = new Thickness(0),
                RowHeight = 32,
                FontSize = 11.5,
                Margin = new Thickness(16, 8, 16, 0),
                Background = Brushes.White,
                RowBackground = Brushes.White,
                AlternatingRowBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFBFC"))
            };
            // KOLUMNA MATCH — wizualny score dopasowania
            var matchCol = new DataGridTemplateColumn { Header = "Match", Width = new DataGridLength(110), SortMemberPath = "MatchScore" };
            var matchTpl = new DataTemplate();
            var matchFef = new FrameworkElementFactory(typeof(Border));
            matchFef.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            matchFef.SetValue(Border.PaddingProperty, new Thickness(6, 2, 6, 2));
            matchFef.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            matchFef.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("MatchScoreColor"));
            var matchSp = new FrameworkElementFactory(typeof(StackPanel));
            var matchTb1 = new FrameworkElementFactory(typeof(TextBlock));
            matchTb1.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("MatchScoreText"));
            matchTb1.SetValue(TextBlock.FontSizeProperty, 12.0);
            matchTb1.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            matchTb1.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            matchTb1.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            var matchTb2 = new FrameworkElementFactory(typeof(TextBlock));
            matchTb2.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("MatchBadge"));
            matchTb2.SetValue(TextBlock.FontSizeProperty, 8.5);
            matchTb2.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            matchTb2.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            matchSp.AppendChild(matchTb1);
            matchSp.AppendChild(matchTb2);
            matchFef.AppendChild(matchSp);
            matchTpl.VisualTree = matchFef;
            matchCol.CellTemplate = matchTpl;
            dgKandydaci.Columns.Add(matchCol);

            dgKandydaci.Columns.Add(new DataGridTextColumn { Header = "ID", Binding = new System.Windows.Data.Binding("Id"), Width = new DataGridLength(50) });
            dgKandydaci.Columns.Add(new DataGridTextColumn { Header = "Data", Binding = new System.Windows.Data.Binding("DataZgloszenia") { StringFormat = "dd.MM.yyyy" }, Width = new DataGridLength(90) });
            dgKandydaci.Columns.Add(new DataGridTextColumn { Header = "Nr dokumentu", Binding = new System.Windows.Data.Binding("NumerDokumentu"), Width = new DataGridLength(140) });
            dgKandydaci.Columns.Add(new DataGridTextColumn { Header = "Kontrahent", Binding = new System.Windows.Data.Binding("NazwaKontrahenta"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            dgKandydaci.Columns.Add(new DataGridTextColumn { Header = "Kg", Binding = new System.Windows.Data.Binding("SumaKg") { StringFormat = "#,##0.00" }, Width = new DataGridLength(90) });
            dgKandydaci.Columns.Add(new DataGridTextColumn { Header = "Typ", Binding = new System.Windows.Data.Binding("TypReklamacji"), Width = new DataGridLength(130) });
            dgKandydaci.Columns.Add(new DataGridTextColumn { Header = "Opis", Binding = new System.Windows.Data.Binding("Opis"), Width = new DataGridLength(200) });
            Grid.SetRow(dgKandydaci, 2);
            mainGrid.Children.Add(dgKandydaci);

            // Filtrowanie
            var wszystkieKandydaci = new List<ReklamacjaItem>(kandydaci);

            Action filtruj = () =>
            {
                string fraza = txtFiltr.Text.Trim().ToLower();
                var przefiltrowane = string.IsNullOrEmpty(fraza)
                    ? wszystkieKandydaci
                    : wszystkieKandydaci.Where(k =>
                        (k.NazwaKontrahenta ?? "").ToLower().Contains(fraza) ||
                        (k.NumerDokumentu ?? "").ToLower().Contains(fraza) ||
                        (k.Opis ?? "").ToLower().Contains(fraza) ||
                        k.Id.ToString().Contains(fraza)
                    ).ToList();

                dgKandydaci.ItemsSource = przefiltrowane;
                txtLicznik.Text = $"{przefiltrowane.Count} / {wszystkieKandydaci.Count}";
            };

            // Debounce timer
            var filterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            filterTimer.Tick += (s2, e2) => { filterTimer.Stop(); filtruj(); };
            txtFiltr.TextChanged += (s2, e2) => { filterTimer.Stop(); filterTimer.Start(); };
            btnWyczysc.Click += (s2, e2) => { txtFiltr.Text = ""; filtruj(); };

            // Zastosuj filtr wstepny
            filtruj();

            // === ROW 3: Stopka z przyciskami ===
            var footerBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F5")),
                Padding = new Thickness(20, 10, 20, 10),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0")),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            var footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

            var footerInfo = new TextBlock
            {
                Text = "Kliknij dwukrotnie wiersz lub wybierz i kliknij 'Powiaz'",
                FontSize = 10.5,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6")),
                VerticalAlignment = VerticalAlignment.Center,
                FontStyle = FontStyles.Italic
            };
            Grid.SetColumn(footerInfo, 0);
            footerGrid.Children.Add(footerInfo);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal };

            var btnPowiazAction = new Button
            {
                Content = "Powiaz wybrana",
                Width = 140, Height = 36,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E44AD")),
                Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 12,
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };

            Action wykonajPowiazanie = () =>
            {
                if (dgKandydaci.SelectedItem is ReklamacjaItem wybrany)
                {
                    UtworzPowiazanie(item.Id, wybrany.Id);
                    dialog.Close();
                    WczytajReklamacje();
                    WczytajStatystyki();
                }
                else
                {
                    MessageBox.Show("Wybierz reklamacje z listy.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            btnPowiazAction.Click += (s, args) => wykonajPowiazanie();
            btnPanel.Children.Add(btnPowiazAction);

            // Double-click na wierszu tez powiazuje
            dgKandydaci.MouseDoubleClick += (s, args) =>
            {
                if (dgKandydaci.SelectedItem != null) wykonajPowiazanie();
            };

            var btnAnuluj = new Button
            {
                Content = "Anuluj",
                Width = 80, Height = 36,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6")),
                Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 12,
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand
            };
            btnAnuluj.Click += (s, args) => dialog.Close();
            btnPanel.Children.Add(btnAnuluj);

            Grid.SetColumn(btnPanel, 1);
            footerGrid.Children.Add(btnPanel);
            footerBorder.Child = footerGrid;
            Grid.SetRow(footerBorder, 3);
            mainGrid.Children.Add(footerBorder);

            dialog.Content = mainGrid;

            // Focus na pole szukania po otwarciu
            dialog.ContentRendered += (s, args) =>
            {
                txtFiltr.Focus();
                txtFiltr.SelectAll();
            };

            dialog.ShowDialog();
        }

        private void UtworzPowiazanie(int id1, int id2)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    // Powiaz obustronie
                    using (var cmd = new SqlCommand(@"
                        UPDATE [dbo].[Reklamacje] SET PowiazanaReklamacjaId = @Id2 WHERE Id = @Id1;
                        UPDATE [dbo].[Reklamacje] SET PowiazanaReklamacjaId = @Id1 WHERE Id = @Id2;", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id1", id1);
                        cmd.Parameters.AddWithValue("@Id2", id2);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad powiazywania:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OdlaczPowiazanie(int id1, int id2)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(@"
                        UPDATE [dbo].[Reklamacje] SET PowiazanaReklamacjaId = NULL WHERE Id = @Id1;
                        UPDATE [dbo].[Reklamacje] SET PowiazanaReklamacjaId = NULL WHERE Id = @Id2;", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id1", id1);
                        cmd.Parameters.AddWithValue("@Id2", id2);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad odlaczania:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ========================================
        // UZUPELNIANIE REKLAMACJI
        // ========================================

        private void BtnUzupelnij_Click(object sender, RoutedEventArgs e)
        {
            if (!(dgReklamacje.SelectedItem is ReklamacjaItem item)) return;

            // Korekta z Symfonii — otwieramy picker faktur bazowych jak "Nowa reklamacja"
            if (item.TypReklamacji == "Faktura korygujaca" || item.WymagaUzupelnienia)
            {
                UzupelnijKorektePickerem(item);
                return;
            }

            // Zwykla reklamacja — stare okno uzupelniania
            var window = new UzupelnijReklamacjeWindow(connectionString, item.Id, userId);
            window.Owner = this;
            if (window.ShowDialog() == true)
            {
                WczytajReklamacje();
                WczytajStatystyki();
            }
        }

        // Handlowiec zglasza pelna reklamacje do istniejacej korekty z Symfonii
        private void BtnZglosDoKorekty_Click(object sender, RoutedEventArgs e)
        {
            if (!(dgReklamacje.SelectedItem is ReklamacjaItem item)) return;
            if (item.TypReklamacji != "Faktura korygujaca")
            {
                MessageBox.Show("Ta opcja jest dostepna tylko dla korekt z Symfonii.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (item.MaPowiazanie)
            {
                MessageBox.Show($"Ta korekta jest juz powiazana z reklamacja #{item.PowiazanaReklamacjaId}.\n\nNie mozna dodac drugiej reklamacji.", "Juz powiazana", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // SZYBKA SCIEZKA: jesli znamy fakture bazowa z Symfonii (iddokkoryg), pomijamy picker
            if (item.IdFakturyOryginalnej.HasValue && item.IdFakturyOryginalnej.Value > 0
                && !string.IsNullOrEmpty(item.NumerFakturyOryginalnej))
            {
                // Pobierz IdKontrahenta + IdDokumentu (id korekty w HANDEL)
                int khid = 0;
                int idDokKorekty = 0;
                try
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand("SELECT IdKontrahenta, IdDokumentu FROM [dbo].[Reklamacje] WHERE Id = @Id", conn))
                        {
                            cmd.Parameters.AddWithValue("@Id", item.Id);
                            using (var r = cmd.ExecuteReader())
                            {
                                if (r.Read())
                                {
                                    khid = r.IsDBNull(0) ? 0 : r.GetInt32(0);
                                    idDokKorekty = r.IsDBNull(1) ? 0 : r.GetInt32(1);
                                }
                            }
                        }
                    }
                }
                catch { }

                if (khid > 0 && idDokKorekty > 0)
                {
                    // Uzyj konstruktora "przypiety do korekty" — formularz sam wykona link
                    var okno = new FormReklamacjaWindow(
                        HandelConnString, item.IdFakturyOryginalnej.Value, khid,
                        item.NumerFakturyOryginalnej, item.NazwaKontrahenta,
                        userId, connectionString,
                        idKorekty: idDokKorekty,
                        nrKorekty: item.NumerDokumentu,
                        dataKorekty: item.DataZgloszenia,
                        wartoscKorekty: null,
                        kgKorekty: item.SumaKg);
                    okno.Owner = this;
                    if (okno.ShowDialog() == true)
                    {
                        WczytajReklamacje();
                        WczytajStatystyki();
                    }
                    return;
                }
            }

            // Fallback: brak znanej faktury bazowej - pokaz picker
            UzupelnijKorektePickerem(item);
        }

        // Uzupelnienie korekty — picker faktur bazowych kontrahenta
        private void UzupelnijKorektePickerem(ReklamacjaItem korekta)
        {
            // Pobierz khid kontrahenta z reklamacji
            int khid = 0;
            string kontrahentNazwa = korekta.NazwaKontrahenta;
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT IdKontrahenta FROM [dbo].[Reklamacje] WHERE Id = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", korekta.Id);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value) khid = Convert.ToInt32(result);
                    }
                }
            }
            catch { }

            if (khid == 0)
            {
                MessageBox.Show("Nie mozna pobrac danych kontrahenta dla tej korekty.", "Blad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Pobierz faktury bazowe tego kontrahenta z HANDEL
            var faktury = new List<FakturaSprzedazyItem>();
            try
            {
                using (var connH = new SqlConnection(HandelConnString))
                {
                    connH.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT TOP 50
                               DK.id, DK.kod, DK.data,
                               ABS(ISNULL(DK.walNetto, 0)) AS Wartosc,
                               ABS(ISNULL((SELECT SUM(DP.ilosc) FROM [HANDEL].[HM].[DP] DP WHERE DP.super = DK.id), 0)) AS SumaKg,
                               ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec
                        FROM [HANDEL].[HM].[DK] DK
                        LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                        WHERE DK.khid = @Khid
                          AND DK.seria NOT IN ('sFKS', 'sFKSB', 'sFWK')
                          AND DK.anulowany = 0
                        ORDER BY DK.data DESC, DK.id DESC", connH))
                    {
                        cmd.Parameters.AddWithValue("@Khid", khid);
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                var data = r.GetDateTime(2);
                                int dniTemu = (int)Math.Floor((DateTime.Today - data.Date).TotalDays);
                                faktury.Add(new FakturaSprzedazyItem
                                {
                                    Id = r.GetInt32(0),
                                    NumerDokumentu = r.IsDBNull(1) ? "" : r.GetString(1),
                                    Data = data,
                                    IdKontrahenta = khid,
                                    NazwaKontrahenta = kontrahentNazwa,
                                    Wartosc = r.IsDBNull(3) ? 0m : Convert.ToDecimal(r.GetValue(3)),
                                    SumaKg = r.IsDBNull(4) ? 0m : Convert.ToDecimal(r.GetValue(4)),
                                    Handlowiec = r.IsDBNull(5) ? "-" : r.GetString(5),
                                    DniTemu = dniTemu,
                                    EtykietaCzasu = dniTemu == 0 ? "DZIS" : dniTemu == 1 ? "WCZORAJ" : $"{dniTemu} dni"
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                FriendlyError.Pokaz(ex, "Nie udalo sie pobrac faktur kontrahenta.", this);
                return;
            }

            if (faktury.Count == 0)
            {
                MessageBox.Show($"Brak faktur sprzedazy dla kontrahenta {kontrahentNazwa}.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Kolory etykiet czasu
            var brushDzisBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F8EF"));
            var brushDzis = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));
            var brushTydzienBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAF2FB"));
            var brushTydzien = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2980B9"));
            var brushStaryBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F3F5"));
            var brushStary = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D"));
            foreach (var f in faktury)
            {
                if (f.DniTemu <= 1) { f.EtykietaTloKolor = brushDzisBg; f.EtykietaTekstKolor = brushDzis; }
                else if (f.DniTemu <= 7) { f.EtykietaTloKolor = brushTydzienBg; f.EtykietaTekstKolor = brushTydzien; }
                else { f.EtykietaTloKolor = brushStaryBg; f.EtykietaTekstKolor = brushStary; }
            }

            // === DIALOG FULLSCREEN ===
            var dialog = new Window
            {
                Title = $"Uzupelnienie korekty #{korekta.Id} — wybierz fakture bazowa",
                WindowState = WindowState.Maximized,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4F6F8")),
                FontFamily = new FontFamily("Segoe UI")
            };
            WindowIconHelper.SetIcon(dialog);

            var root = new Grid();
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

            // HEADER
            var header = new Border
            {
                Padding = new Thickness(28, 16, 28, 16),
                Background = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#3498DB"),
                    (Color)ColorConverter.ConvertFromString("#2980B9"),
                    new System.Windows.Point(0, 0), new System.Windows.Point(1, 0))
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

            var headerLeft = new StackPanel();
            headerLeft.Children.Add(new TextBlock
            {
                Text = "ZGLOS REKLAMACJE DO KOREKTY Z SYMFONII",
                FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Brushes.White
            });
            headerLeft.Children.Add(new TextBlock
            {
                Text = $"Korekta: {korekta.NumerDokumentu}  |  Kontrahent: {kontrahentNazwa}  |  {korekta.SumaKg:#,##0.00} kg",
                FontSize = 12.5, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255)),
                Margin = new Thickness(0, 5, 0, 0)
            });
            headerLeft.Children.Add(new TextBlock
            {
                Text = "Wybierz fakture bazowa dla tej korekty → wypelnij szczegoly reklamacji (typ, opis, zdjecia) → system powiaze je automatycznie",
                FontSize = 11, FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            headerGrid.Children.Add(headerLeft);
            header.Child = headerGrid;
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // FILTR
            var filterCard = new Border
            {
                Background = Brushes.White,
                Margin = new Thickness(20, 14, 20, 0),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20, 14, 20, 14),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { ShadowDepth = 1, Opacity = 0.06, BlurRadius = 10 }
            };
            var searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });
            searchGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchGrid.Children.Add(new TextBlock { Text = "Szukaj", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D")), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) });
            var txtFiltr = new TextBox { FontSize = 14, Padding = new Thickness(12, 8, 12, 8), BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDE2E6")), BorderThickness = new Thickness(1.5), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F9FA")) };
            Grid.SetColumn(txtFiltr, 1);
            searchGrid.Children.Add(txtFiltr);
            filterCard.Child = searchGrid;
            Grid.SetRow(filterCard, 1);
            root.Children.Add(filterCard);

            // DATAGRID
            var gridCard = new Border
            {
                Background = Brushes.White,
                Margin = new Thickness(20, 10, 20, 0),
                CornerRadius = new CornerRadius(10),
                ClipToBounds = true,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { ShadowDepth = 1, Opacity = 0.06, BlurRadius = 10 }
            };
            var dgFaktury = new DataGrid
            {
                AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EFF2F5")),
                BorderThickness = new Thickness(0), RowHeight = 42, FontSize = 13,
                Background = Brushes.White, RowBackground = Brushes.White,
                AlternatingRowBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFBFC")),
                ColumnHeaderHeight = 40
            };

            dgFaktury.Columns.Add(new DataGridTextColumn { Header = "DATA", Binding = new System.Windows.Data.Binding("Data") { StringFormat = "dd.MM.yyyy" }, Width = new DataGridLength(105) });
            dgFaktury.Columns.Add(new DataGridTextColumn { Header = "NR FAKTURY", Binding = new System.Windows.Data.Binding("NumerDokumentu"), Width = new DataGridLength(180) });
            dgFaktury.Columns.Add(new DataGridTextColumn { Header = "HANDLOWIEC", Binding = new System.Windows.Data.Binding("Handlowiec"), Width = new DataGridLength(160) });
            dgFaktury.Columns.Add(new DataGridTextColumn { Header = "KG", Binding = new System.Windows.Data.Binding("SumaKg") { StringFormat = "#,##0.00" }, Width = new DataGridLength(120) });
            dgFaktury.Columns.Add(new DataGridTextColumn { Header = "WARTOSC NETTO", Binding = new System.Windows.Data.Binding("Wartosc") { StringFormat = "#,##0.00 zl" }, Width = new DataGridLength(160) });

            gridCard.Child = dgFaktury;
            Grid.SetRow(gridCard, 2);
            root.Children.Add(gridCard);

            // STOPKA
            var footer = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E8EB")),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(28, 14, 28, 14)
            };
            var footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

            var txtLicznik = new TextBlock { FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C3E50")), VerticalAlignment = VerticalAlignment.Center };
            footerGrid.Children.Add(txtLicznik);

            var footerBtns = new StackPanel { Orientation = Orientation.Horizontal };
            var btnAnuluj = new Button { Content = "Anuluj", Padding = new Thickness(22, 10, 22, 10), FontSize = 12.5, Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECF0F1")), Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D")), BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 10, 0), IsCancel = true };
            btnAnuluj.Click += (s, ev) => dialog.Close();
            footerBtns.Children.Add(btnAnuluj);

            var btnOk = new Button { Content = "Zglos reklamacje  →", Padding = new Thickness(24, 10, 24, 10), FontSize = 12.5, FontWeight = FontWeights.Bold, Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB")), Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, IsDefault = true, IsEnabled = false };
            footerBtns.Children.Add(btnOk);
            Grid.SetColumn(footerBtns, 1);
            footerGrid.Children.Add(footerBtns);
            footer.Child = footerGrid;
            Grid.SetRow(footer, 3);
            root.Children.Add(footer);

            dialog.Content = root;

            // LOGIKA
            Action filtruj = () =>
            {
                string fraza = txtFiltr.Text.Trim().ToLower();
                var przefiltrowane = string.IsNullOrEmpty(fraza) ? faktury
                    : faktury.Where(f => (f.NumerDokumentu ?? "").ToLower().Contains(fraza) || (f.Handlowiec ?? "").ToLower().Contains(fraza)).ToList();
                dgFaktury.ItemsSource = przefiltrowane;
                txtLicznik.Text = $"{przefiltrowane.Count} z {faktury.Count} faktur";
            };

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            timer.Tick += (s, ev) => { timer.Stop(); filtruj(); };
            txtFiltr.TextChanged += (s, ev) => { timer.Stop(); timer.Start(); };

            filtruj();

            dgFaktury.SelectionChanged += (s, ev) =>
            {
                btnOk.IsEnabled = dgFaktury.SelectedItem != null;
                btnOk.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dgFaktury.SelectedItem != null ? "#3498DB" : "#AED6F1"));
            };

            Action wykonaj = () =>
            {
                if (!(dgFaktury.SelectedItem is FakturaSprzedazyItem wybrana)) return;
                dialog.Close();

                // Otwieramy FormReklamacjaWindow z wybrana faktura bazowa
                var okno = new FormReklamacjaWindow(
                    HandelConnString, wybrana.Id, wybrana.IdKontrahenta,
                    wybrana.NumerDokumentu, wybrana.NazwaKontrahenta,
                    userId, connectionString);
                okno.Owner = this;
                if (okno.ShowDialog() == true)
                {
                    // Powiaz nowa reklamacje z korekta
                    try
                    {
                        using (var conn = new SqlConnection(connectionString))
                        {
                            conn.Open();
                            // Znajdz ID nowo utworzonej reklamacji (najnowsza dla tego usera)
                            int nowaId = 0;
                            using (var cmd = new SqlCommand("SELECT TOP 1 Id FROM [dbo].[Reklamacje] WHERE UserID = @U ORDER BY Id DESC", conn))
                            {
                                cmd.Parameters.AddWithValue("@U", userId);
                                nowaId = Convert.ToInt32(cmd.ExecuteScalar());
                            }
                            if (nowaId > 0)
                            {
                                UtworzPowiazanie(korekta.Id, nowaId);
                                // Zdejmij flage WymagaUzupelnienia z korekty
                                using (var cmd = new SqlCommand("UPDATE [dbo].[Reklamacje] SET WymagaUzupelnienia = 0, StatusV2 = 'POWIAZANA' WHERE Id = @Id", conn))
                                {
                                    cmd.Parameters.AddWithValue("@Id", korekta.Id);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                    catch { }

                    WczytajReklamacje();
                    WczytajStatystyki();
                }
            };

            btnOk.Click += (s, ev) => wykonaj();
            dgFaktury.MouseDoubleClick += (s, ev) => { if (dgFaktury.SelectedItem != null) wykonaj(); };
            dgFaktury.KeyDown += (s, ev) => { if (ev.Key == Key.Enter && dgFaktury.SelectedItem != null) { wykonaj(); ev.Handled = true; } };

            dialog.Loaded += (s, ev) => txtFiltr.Focus();
            dialog.ShowDialog();
        }

        // ========================================
        // USUWANIE (ADMIN)
        // ========================================

        private void BtnUsun_Click(object sender, RoutedEventArgs e)
        {
            if (userId != "11111")
            {
                MessageBox.Show("Brak uprawnien do usuwania reklamacji.", "Brak uprawnien", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dgReklamacje.SelectedItem is ReklamacjaItem item)
            {
                if (MessageBox.Show($"Czy na pewno chcesz USUNAC reklamacje #{item.Id} ({item.NumerDokumentu})?\n\nTa operacja jest nieodwracalna!",
                    "Potwierdzenie usuwania", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;

                try
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        using (var tr = conn.BeginTransaction())
                        {
                            try
                            {
                                foreach (var table in new[] { "ReklamacjeZdjecia", "ReklamacjeTowary", "ReklamacjeHistoria" })
                                {
                                    using (var cmd = new SqlCommand($"DELETE FROM [dbo].[{table}] WHERE IdReklamacji = @Id", conn, tr))
                                    {
                                        cmd.Parameters.AddWithValue("@Id", item.Id);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                                using (var cmd = new SqlCommand("DELETE FROM [dbo].[Reklamacje] WHERE Id = @Id", conn, tr))
                                {
                                    cmd.Parameters.AddWithValue("@Id", item.Id);
                                    cmd.ExecuteNonQuery();
                                }
                                tr.Commit();
                            }
                            catch
                            {
                                tr.Rollback();
                                throw;
                            }
                        }
                    }

                    MessageBox.Show($"Reklamacja #{item.Id} zostala usunieta.", "Usunieto", MessageBoxButton.OK, MessageBoxImage.Information);
                    WczytajReklamacje();
                    WczytajStatystyki();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Blad usuwania: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ========================================
        // USTAWIENIA SYNC (Admin)
        // ========================================

        private void UpewnijSieZeTabeUstawienIstnieje(SqlConnection conn)
        {
            using (var cmd = new SqlCommand(@"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ReklamacjeUstawienia')
                BEGIN
                    CREATE TABLE [dbo].[ReklamacjeUstawienia] (
                        [Id] INT IDENTITY(1,1) PRIMARY KEY,
                        [Klucz] NVARCHAR(100) NOT NULL UNIQUE,
                        [Wartosc] NVARCHAR(500) NULL,
                        [DataModyfikacji] DATETIME DEFAULT GETDATE(),
                        [ZmodyfikowalUser] NVARCHAR(50) NULL
                    );
                    INSERT INTO [dbo].[ReklamacjeUstawienia] (Klucz, Wartosc)
                    VALUES ('DataOdKorekt', CONVERT(NVARCHAR, DATEADD(MONTH, -6, GETDATE()), 23));
                END", conn))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private DateTime PobierzDataOdKorekt()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    UpewnijSieZeTabeUstawienIstnieje(conn);
                    using (var cmd = new SqlCommand(
                        "SELECT Wartosc FROM [dbo].[ReklamacjeUstawienia] WHERE Klucz = 'DataOdKorekt'", conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value && DateTime.TryParse(result.ToString(), out DateTime dt))
                            return dt;
                    }
                }
            }
            catch { }
            return DateTime.Now.AddMonths(-6);
        }

        private void ZapiszDataOdKorekt(DateTime data)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    UpewnijSieZeTabeUstawienIstnieje(conn);
                    using (var cmd = new SqlCommand(@"
                        UPDATE [dbo].[ReklamacjeUstawienia]
                        SET Wartosc = @Wartosc, DataModyfikacji = GETDATE(), ZmodyfikowalUser = @User
                        WHERE Klucz = 'DataOdKorekt';
                        IF @@ROWCOUNT = 0
                            INSERT INTO [dbo].[ReklamacjeUstawienia] (Klucz, Wartosc, ZmodyfikowalUser)
                            VALUES ('DataOdKorekt', @Wartosc, @User);", conn))
                    {
                        cmd.Parameters.AddWithValue("@Wartosc", data.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@User", userId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad zapisu ustawienia:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int UsunKorektySprzedDaty(DateTime dataOd)
        {
            int usunieto = 0;
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Pobierz ID reklamacji korygujacych sprzed daty
                    var ids = new List<int>();
                    using (var cmd = new SqlCommand(@"
                        SELECT Id FROM [dbo].[Reklamacje]
                        WHERE TypReklamacji = 'Faktura korygujaca'
                          AND DataZgloszenia < @DataOd", conn))
                    {
                        cmd.Parameters.AddWithValue("@DataOd", dataOd);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                ids.Add(reader.GetInt32(0));
                        }
                    }

                    if (ids.Count == 0) return 0;

                    // Usun powiazane dane i reklamacje
                    string idList = string.Join(",", ids);
                    string[] tabele = { "ReklamacjeTowary", "ReklamacjePartie", "ReklamacjeZdjecia", "ReklamacjeHistoria" };
                    foreach (var tabela in tabele)
                    {
                        try
                        {
                            using (var cmd = new SqlCommand(
                                $"DELETE FROM [dbo].[{tabela}] WHERE IdReklamacji IN ({idList})", conn))
                            {
                                cmd.ExecuteNonQuery();
                            }
                        }
                        catch { }
                    }

                    using (var cmd = new SqlCommand(
                        $"DELETE FROM [dbo].[Reklamacje] WHERE Id IN ({idList})", conn))
                    {
                        usunieto = cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad usuwania starych korekt:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return usunieto;
        }

        // ========================================
        // ZAKLADKI WORKFLOW V2
        // ========================================
        private void TabDoAkcji_Click(object sender, RoutedEventArgs e) => PrzelaczZakladke("DO_AKCJI");
        private void TabWToku_Click(object sender, RoutedEventArgs e) => PrzelaczZakladke("W_TOKU");
        private void TabZamkniete_Click(object sender, RoutedEventArgs e) => PrzelaczZakladke("ZAMKNIETE");

        private void PrzelaczZakladke(string kategoria)
        {
            aktywnaZakladka = kategoria;
            reklamacjeView?.Refresh();

            var doAkcji = (Color)ColorConverter.ConvertFromString("#E74C3C");
            var wToku = (Color)ColorConverter.ConvertFromString("#F39C12");
            var zamkn = (Color)ColorConverter.ConvertFromString("#27AE60");
            var nieaktywne = (Color)ColorConverter.ConvertFromString("#ECEFF1");
            var nieaktywneFg = (Color)ColorConverter.ConvertFromString("#546E7A");

            void Odcien(Button btn, Color aktywneTlo, Color aktywnyBadgeTlo, Color aktywnyBadgeFg)
            {
                var border = FindTemplateBorder(btn);
                if (border == null) return;
                bool aktywna = (btn == tabDoAkcji && kategoria == "DO_AKCJI")
                            || (btn == tabWToku && kategoria == "W_TOKU")
                            || (btn == tabZamkniete && kategoria == "ZAMKNIETE");
                if (aktywna)
                {
                    border.Background = new SolidColorBrush(aktywneTlo);
                    UstawKolorZakladki(border, aktywnyBadgeTlo, aktywnyBadgeFg, Brushes.White);
                }
                else
                {
                    border.Background = new SolidColorBrush(nieaktywne);
                    UstawKolorZakladki(border, new SolidColorBrush(nieaktywneFg).Color, Colors.White, new SolidColorBrush(nieaktywneFg));
                }
            }

            Odcien(tabDoAkcji, doAkcji, Colors.White, doAkcji);
            Odcien(tabWToku, wToku, Colors.White, wToku);
            Odcien(tabZamkniete, zamkn, Colors.White, zamkn);

            txtOpisZakladki.Text = kategoria switch
            {
                "DO_AKCJI" => "Nowe zgloszenia + korekty bez reklamacji — wymagaja decyzji",
                "W_TOKU" => "Przyjete do rozpatrzenia przez dzial jakosci",
                "ZAMKNIETE" => "Zaakceptowane, odrzucone, powiazane lub zamkniete",
                _ => ""
            };

            txtTytulListy.Text = kategoria switch
            {
                "DO_AKCJI" => "DO AKCJI - wymagaja decyzji",
                "W_TOKU" => "PRZYJETE - w trakcie rozpatrywania",
                "ZAMKNIETE" => "ZAMKNIETE",
                _ => "Lista reklamacji"
            };

            AktualizujLicznikZaznaczenia();
        }

        private Border FindTemplateBorder(Button btn)
        {
            if (btn?.Template == null) return null;
            btn.ApplyTemplate();
            return btn.Template.FindName("tabBorder", btn) as Border;
        }

        private void UstawKolorZakladki(Border tabBorder, Color badgeTlo, Color badgeFg, Brush txtKolor)
        {
            // Znajdz TextBlock tytulu (pierwsze dziecko StackPanel) + badge Border z liczba
            if (!(tabBorder.Child is StackPanel sp)) return;
            if (sp.Children.Count < 2) return;
            if (sp.Children[0] is TextBlock tbTytul) tbTytul.Foreground = txtKolor;
            if (sp.Children[1] is Border badgeBorder)
            {
                badgeBorder.Background = new SolidColorBrush(badgeTlo);
                if (badgeBorder.Child is TextBlock tbBadge) tbBadge.Foreground = new SolidColorBrush(badgeFg);
            }
        }

        private void AktualizujLicznikiZakladek()
        {
            int cntDoAkcji = 0, cntWToku = 0, cntZamkniete = 0;
            foreach (var r in reklamacje)
            {
                switch (r.KategoriaZakladki)
                {
                    case "DO_AKCJI": cntDoAkcji++; break;
                    case "W_TOKU": cntWToku++; break;
                    case "ZAMKNIETE": cntZamkniete++; break;
                }
            }

            var bDoAkcji = FindTemplateBorder(tabDoAkcji);
            if (bDoAkcji?.Child is StackPanel sp1 && sp1.Children.Count >= 2 && sp1.Children[1] is Border bd1 && bd1.Child is TextBlock tb1)
                tb1.Text = cntDoAkcji.ToString();

            var bWToku = FindTemplateBorder(tabWToku);
            if (bWToku?.Child is StackPanel sp2 && sp2.Children.Count >= 2 && sp2.Children[1] is Border bd2 && bd2.Child is TextBlock tb2)
                tb2.Text = cntWToku.ToString();

            var bZamkn = FindTemplateBorder(tabZamkniete);
            if (bZamkn?.Child is StackPanel sp3 && sp3.Children.Count >= 2 && sp3.Children[1] is Border bd3 && bd3.Child is TextBlock tb3)
                tb3.Text = cntZamkniete.ToString();
        }

        private void AktualizujLicznikZaznaczenia()
        {
            if (txtLiczbaReklamacji == null) return;
            int widoczne = 0;
            if (reklamacjeView != null)
                foreach (var _ in reklamacjeView) widoczne++;
            txtLiczbaReklamacji.Text = widoczne.ToString();
        }

        // ========================================
        // SPLIT BUTTON "+ NOWA REKLAMACJA" - 3 sciezki: faktura / korekta / bez faktury
        // ========================================
        private void BtnNowaSplit_Click(object sender, RoutedEventArgs e)
        {
            if (popupNowa != null) popupNowa.IsOpen = !popupNowa.IsOpen;
        }

        private void MenuNowa_DoFaktury_Click(object sender, RoutedEventArgs e)
        {
            if (popupNowa != null) popupNowa.IsOpen = false;
            // Sciezka: kontrahent -> jego ostatnie faktury (BtnNowaBezFaktury startuje od kontrahenta)
            BtnNowaBezFaktury_Click(sender, e);
        }

        private void MenuNowa_DoKorekty_Click(object sender, RoutedEventArgs e)
        {
            if (popupNowa != null) popupNowa.IsOpen = false;
            PokazPickerKorekty();
        }

        private void MenuNowa_BezFaktury_Click(object sender, RoutedEventArgs e)
        {
            if (popupNowa != null) popupNowa.IsOpen = false;
            // Sciezka "bez faktury": ten sam dialog co BtnNowaBezFaktury — uzytkownik moze pominac wybor faktury
            BtnNowaBezFaktury_Click(sender, e);
        }

        // Picker korekt z Symfonii — dla "Do istniejacej korekty"
        private void PokazPickerKorekty()
        {
            // Otworz okno z lista korekt FKS / FKSB / FWK z ostatnich 180 dni
            var korekty = new List<FakturaSprzedazyItem>();
            try
            {
                using (var connH = new SqlConnection(HandelConnString))
                {
                    connH.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT TOP 1000
                               DK.id, DK.kod, DK.data, DK.khid,
                               C.shortcut AS NazwaKontrahenta,
                               ABS(ISNULL(DK.walNetto, 0)) AS Wartosc,
                               ABS(ISNULL((SELECT SUM(DP.ilosc) FROM [HANDEL].[HM].[DP] DP WHERE DP.super = DK.id AND DP.ilosc >= 0), 0)) AS SumaKg,
                               ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec
                        FROM [HANDEL].[HM].[DK] DK
                        INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                        LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                        WHERE DK.seria IN ('sFKS', 'sFKSB', 'sFWK')
                          AND DK.anulowany = 0
                          AND DK.data >= DATEADD(DAY, -180, GETDATE())
                          AND C.shortcut NOT LIKE 'SD/%'
                        ORDER BY DK.data DESC, DK.id DESC", connH))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var dataK = r.GetDateTime(2);
                            int dni = (int)Math.Floor((DateTime.Today - dataK.Date).TotalDays);
                            korekty.Add(new FakturaSprzedazyItem
                            {
                                Id = r.GetInt32(0),
                                NumerDokumentu = r.IsDBNull(1) ? "" : r.GetString(1),
                                Data = dataK,
                                IdKontrahenta = r.GetInt32(3),
                                NazwaKontrahenta = r.IsDBNull(4) ? "" : r.GetString(4),
                                Wartosc = r.IsDBNull(5) ? 0m : Convert.ToDecimal(r.GetValue(5)),
                                SumaKg = r.IsDBNull(6) ? 0m : Convert.ToDecimal(r.GetValue(6)),
                                Handlowiec = r.IsDBNull(7) ? "-" : r.GetString(7),
                                DniTemu = dni,
                                EtykietaCzasu = dni == 0 ? "DZIS" : dni == 1 ? "WCZORAJ" : $"{dni} dni"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                FriendlyError.Pokaz(ex, "Nie udalo sie pobrac listy korekt z Symfonii.", this);
                return;
            }

            if (korekty.Count == 0)
            {
                MessageBox.Show("Brak korekt FKS/FKSB/FWK z ostatnich 180 dni.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Prosty dialog z DataGrid
            var dlg = new Window
            {
                Title = "Wybierz korekte z Symfonii",
                Width = 1100, Height = 720,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4F6F8")),
                FontFamily = new FontFamily("Segoe UI")
            };
            WindowIconHelper.SetIcon(dlg);

            var root = new Grid();
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

            var hdr = new Border
            {
                Padding = new Thickness(20, 14, 20, 14),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1ABC9C"))
            };
            var hdrSp = new StackPanel();
            hdrSp.Children.Add(new TextBlock { Text = "WYBIERZ KOREKTE", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
            hdrSp.Children.Add(new TextBlock { Text = "Korekty z Symfonii (FKS / FKSB / FWK) — kliknij aby zglosic reklamacje", FontSize = 11.5, Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)), Margin = new Thickness(0, 4, 0, 0) });
            hdr.Child = hdrSp;
            Grid.SetRow(hdr, 0); root.Children.Add(hdr);

            var dg = new DataGrid
            {
                AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
                SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow,
                HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                Margin = new Thickness(16), AlternatingRowBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFAFA")),
                ItemsSource = korekty, RowHeight = 36, FontSize = 12
            };
            dg.Columns.Add(new DataGridTextColumn { Header = "Numer korekty", Binding = new System.Windows.Data.Binding("NumerDokumentu") { StringFormat = null }, Width = new DataGridLength(180) });
            dg.Columns.Add(new DataGridTextColumn { Header = "Data", Binding = new System.Windows.Data.Binding("Data") { StringFormat = "dd.MM.yyyy" }, Width = new DataGridLength(100) });
            dg.Columns.Add(new DataGridTextColumn { Header = "Czas", Binding = new System.Windows.Data.Binding("EtykietaCzasu"), Width = new DataGridLength(80) });
            dg.Columns.Add(new DataGridTextColumn { Header = "Kontrahent", Binding = new System.Windows.Data.Binding("NazwaKontrahenta"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            dg.Columns.Add(new DataGridTextColumn { Header = "Kg", Binding = new System.Windows.Data.Binding("SumaKg") { StringFormat = "N2" }, Width = new DataGridLength(90) });
            dg.Columns.Add(new DataGridTextColumn { Header = "Wartosc", Binding = new System.Windows.Data.Binding("Wartosc") { StringFormat = "N2" }, Width = new DataGridLength(110) });
            dg.Columns.Add(new DataGridTextColumn { Header = "Handlowiec", Binding = new System.Windows.Data.Binding("Handlowiec"), Width = new DataGridLength(160) });

            Grid.SetRow(dg, 1); root.Children.Add(dg);

            var stopka = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16, 0, 16, 16) };
            var btnAnuluj = new Button { Content = "Anuluj", Width = 100, Height = 36, Margin = new Thickness(0, 0, 8, 0) };
            btnAnuluj.Click += (s2, e2) => dlg.Close();
            var btnWybierz = new Button
            {
                Content = "Wybierz korekte", Width = 160, Height = 36,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1ABC9C")),
                Foreground = Brushes.White, FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0)
            };
            btnWybierz.Click += (s2, e2) =>
            {
                if (!(dg.SelectedItem is FakturaSprzedazyItem k))
                {
                    MessageBox.Show("Wybierz korekte z listy.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                dlg.Close();
                OtworzReklamacjeDlaKorekty(k);
            };
            stopka.Children.Add(btnAnuluj); stopka.Children.Add(btnWybierz);
            Grid.SetRow(stopka, 2); root.Children.Add(stopka);

            dg.MouseDoubleClick += (s2, e2) =>
            {
                if (dg.SelectedItem is FakturaSprzedazyItem)
                    btnWybierz.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            };

            dlg.Content = root;
            dlg.ShowDialog();
        }

        // Otwiera FormReklamacjaWindow z przypieta korekta — handlowiec/jakosc opisuje powod
        private void OtworzReklamacjeDlaKorekty(FakturaSprzedazyItem korekta)
        {
            // Znajdz fakture bazowa korekty (iddokkoryg)
            int idFakturyBazowej = 0;
            string nrFakturyBazowej = null;
            try
            {
                using (var connH = new SqlConnection(HandelConnString))
                {
                    connH.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT TOP 1 DK_BAZA.id, DK_BAZA.kod
                        FROM [HANDEL].[HM].[DK] DK_KOR
                        LEFT JOIN [HANDEL].[HM].[DK] DK_BAZA ON DK_BAZA.id = DK_KOR.iddokkoryg
                        WHERE DK_KOR.id = @Id", connH))
                    {
                        cmd.Parameters.AddWithValue("@Id", korekta.Id);
                        using (var r = cmd.ExecuteReader())
                        {
                            if (r.Read() && !r.IsDBNull(0))
                            {
                                idFakturyBazowej = r.GetInt32(0);
                                nrFakturyBazowej = r.IsDBNull(1) ? null : r.GetString(1);
                            }
                        }
                    }
                }
            }
            catch { }

            // Jesli nie znaleziono faktury bazowej - uzyj danych korekty
            if (idFakturyBazowej == 0)
            {
                idFakturyBazowej = korekta.Id;
                nrFakturyBazowej = korekta.NumerDokumentu;
            }

            try
            {
                var okno = new FormReklamacjaWindow(
                    HandelConnString,
                    idFakturyBazowej,
                    korekta.IdKontrahenta,
                    nrFakturyBazowej ?? korekta.NumerDokumentu,
                    korekta.NazwaKontrahenta,
                    userId,
                    connectionString,
                    idKorekty: korekta.Id,
                    nrKorekty: korekta.NumerDokumentu,
                    dataKorekty: korekta.Data,
                    wartoscKorekty: korekta.Wartosc,
                    kgKorekty: korekta.SumaKg);
                okno.Owner = this;
                if (okno.ShowDialog() == true)
                {
                    WczytajReklamacje();
                    WczytajStatystyki();
                }
            }
            catch (Exception ex)
            {
                FriendlyError.Pokaz(ex, "Nie udalo sie otworzyc formularza reklamacji.", this);
            }
        }

        // ========================================
        // ETAP 3: Zgloszenie BEZ faktury korygujacej
        // Flow: wybierz kontrahenta -> pokaz jego 15 ostatnich faktur -> wybierz
        // fakture bazowa -> otworz ten sam FormReklamacjaWindow co przy "+ Nowa reklamacja"
        // ========================================
        private void BtnNowaBezFaktury_Click(object sender, RoutedEventArgs e)
        {
            // Pobierz liste kontrahentow z HANDEL (dla autocomplete)
            var kontrahenci = new List<KontrahentItem>();
            try
            {
                using (var connH = new SqlConnection(HandelConnString))
                {
                    connH.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT TOP 2000 id, shortcut, ISNULL(name, shortcut) AS name
                        FROM [HANDEL].[SSCommon].[STContractors]
                        WHERE shortcut NOT LIKE 'SD/%'
                        ORDER BY shortcut", connH))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            kontrahenci.Add(new KontrahentItem
                            {
                                Id = r.GetInt32(0),
                                Shortcut = r.IsDBNull(1) ? "" : r.GetString(1),
                                Name = r.IsDBNull(2) ? "" : r.GetString(2)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                FriendlyError.Pokaz(ex, "Nie udalo sie pobrac kontrahentow z Symfonii.", this);
                return;
            }

            // ====== DIALOG ======
            var dialog = new Window
            {
                Title = "Nowa reklamacja - wybierz kontrahenta",
                Width = 820, Height = 680,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.CanResize,
                MinWidth = 700, MinHeight = 560,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4F6F8")),
                FontFamily = new FontFamily("Segoe UI")
            };
            WindowIconHelper.SetIcon(dialog);

            var root = new Grid();
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

            // Header
            var hdr = new Border
            {
                Padding = new Thickness(24, 18, 24, 18),
                Background = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#3498DB"),
                    (Color)ColorConverter.ConvertFromString("#2980B9"),
                    new System.Windows.Point(0, 0), new System.Windows.Point(1, 0))
            };
            var hdrStack = new StackPanel();
            hdrStack.Children.Add(new TextBlock
            {
                Text = "NOWA REKLAMACJA - BEZ FAKTURY KORYGUJACEJ",
                FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Brushes.White
            });
            hdrStack.Children.Add(new TextBlock
            {
                Text = "1) Wybierz kontrahenta   2) Wybierz fakture bazowa z jego 15 ostatnich   3) Wypelnij reklamacje",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                Margin = new Thickness(0, 4, 0, 0)
            });
            hdr.Child = hdrStack;
            Grid.SetRow(hdr, 0);
            root.Children.Add(hdr);

            // Body — pionowy grid zeby DataGrid z towarami mogl rozciagac sie na *
            var form = new Grid { Margin = new Thickness(28, 20, 28, 14) };
            form.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            TextBlock Label(string text) => new TextBlock
            {
                Text = text,
                FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#546E7A")),
                Margin = new Thickness(0, 0, 0, 6)
            };

            // KONTRAHENT
            var lblKontr = Label("KONTRAHENT *");
            Grid.SetRow(lblKontr, 0);
            form.Children.Add(lblKontr);

            var cbKontrahent = new ComboBox
            {
                IsEditable = true,
                IsTextSearchEnabled = true,
                StaysOpenOnEdit = true,
                FontSize = 14,
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 16),
                ItemsSource = kontrahenci,
                DisplayMemberPath = "Shortcut",
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDE2E6")),
                BorderThickness = new Thickness(1.5)
            };
            Grid.SetRow(cbKontrahent, 1);
            form.Children.Add(cbKontrahent);

            // FAKTURA
            var lblFakt = Label("FAKTURA BAZOWA * (15 ostatnich dla wybranego kontrahenta)");
            Grid.SetRow(lblFakt, 2);
            form.Children.Add(lblFakt);

            var cbFaktura = new ComboBox
            {
                FontSize = 13,
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 8),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDE2E6")),
                BorderThickness = new Thickness(1.5),
                IsEnabled = false
            };
            Grid.SetRow(cbFaktura, 3);
            form.Children.Add(cbFaktura);

            var txtStatus = new TextBlock
            {
                Text = "Wybierz kontrahenta aby zobaczyc jego ostatnie faktury",
                FontSize = 11,
                FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6")),
                Margin = new Thickness(2, 0, 0, 12)
            };
            Grid.SetRow(txtStatus, 4);
            form.Children.Add(txtStatus);

            // SEKCJA TOWARÓW (pojawia się po wyborze faktury)
            var towaryCard = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E8EB")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(0),
                ClipToBounds = true
            };
            var towaryRoot = new Grid();
            towaryRoot.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            towaryRoot.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            towaryRoot.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

            var towaryHeader = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F9FA")),
                Padding = new Thickness(14, 9, 14, 9),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E8EB")),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var txtTowaryNaglowek = new TextBlock
            {
                Text = "TOWARY NA FAKTURZE",
                FontSize = 10.5, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#546E7A"))
            };
            towaryHeader.Child = txtTowaryNaglowek;
            Grid.SetRow(towaryHeader, 0);
            towaryRoot.Children.Add(towaryHeader);

            var dgTowary = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F3F5")),
                BorderThickness = new Thickness(0),
                RowHeight = 28,
                FontSize = 11.5,
                Background = Brushes.White,
                RowBackground = Brushes.White,
                AlternatingRowBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFBFC")),
                ColumnHeaderHeight = 30,
                CanUserAddRows = false,
                CanUserDeleteRows = false
            };
            var thStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            thStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F9FA"))));
            thStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D"))));
            thStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty, FontWeights.SemiBold));
            thStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontSizeProperty, 10.0));
            thStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty, new Thickness(10, 0, 10, 0)));
            dgTowary.ColumnHeaderStyle = thStyle;

            var cStyle = new Style(typeof(DataGridCell));
            cStyle.Setters.Add(new Setter(DataGridCell.PaddingProperty, new Thickness(10, 0, 10, 0)));
            cStyle.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));
            dgTowary.CellStyle = cStyle;

            dgTowary.Columns.Add(new DataGridTextColumn { Header = "SYMBOL", Binding = new System.Windows.Data.Binding("Symbol"), Width = new DataGridLength(95) });
            var colNazwa = new DataGridTextColumn { Header = "NAZWA", Binding = new System.Windows.Data.Binding("Nazwa"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) };
            var stNazwa = new Style(typeof(TextBlock));
            stNazwa.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            stNazwa.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            colNazwa.ElementStyle = stNazwa;
            dgTowary.Columns.Add(colNazwa);

            var colWaga = new DataGridTextColumn { Header = "KG", Binding = new System.Windows.Data.Binding("Waga") { StringFormat = "#,##0.00" }, Width = new DataGridLength(95) };
            var stWaga = new Style(typeof(TextBlock));
            stWaga.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            stWaga.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            colWaga.ElementStyle = stWaga;
            dgTowary.Columns.Add(colWaga);

            var colCena = new DataGridTextColumn { Header = "CENA", Binding = new System.Windows.Data.Binding("Cena") { StringFormat = "#,##0.00" }, Width = new DataGridLength(95) };
            colCena.ElementStyle = stWaga;
            dgTowary.Columns.Add(colCena);

            var colWart = new DataGridTextColumn { Header = "WARTOSC", Binding = new System.Windows.Data.Binding("Wartosc") { StringFormat = "#,##0.00 zl" }, Width = new DataGridLength(115) };
            var stWart = new Style(typeof(TextBlock));
            stWart.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            stWart.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            stWart.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
            stWart.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"))));
            colWart.ElementStyle = stWart;
            dgTowary.Columns.Add(colWart);

            Grid.SetRow(dgTowary, 1);
            towaryRoot.Children.Add(dgTowary);

            // Pasek podsumowania pod DataGrid
            var towaryFooter = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFBEA")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E8EB")),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(14, 8, 14, 8)
            };
            var txtTowarySuma = new TextBlock
            {
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5D6D7E"))
            };
            towaryFooter.Child = txtTowarySuma;
            Grid.SetRow(towaryFooter, 2);
            towaryRoot.Children.Add(towaryFooter);

            towaryCard.Child = towaryRoot;
            Grid.SetRow(towaryCard, 5);
            form.Children.Add(towaryCard);

            // Empty state - pokazywany gdy nic nie wybrano
            var towaryEmpty = new TextBlock
            {
                Text = "Wybierz fakture aby zobaczyc jej towary",
                FontSize = 12,
                FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B0B7BD")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Visible
            };
            Grid.SetRow(towaryEmpty, 5);
            form.Children.Add(towaryEmpty);

            Grid.SetRow(form, 1);
            root.Children.Add(form);

            // Stopka
            var footer = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E8EB")),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(24, 14, 24, 14)
            };
            var footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

            var hint = new TextBlock
            {
                Text = "Dalej otwiera ten sam formularz co przy '+ Nowa reklamacja'",
                FontSize = 10.5,
                FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BDC3C7")),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(hint, 0);
            footerGrid.Children.Add(hint);

            var footerStack = new StackPanel { Orientation = Orientation.Horizontal };
            var btnAnuluj = new Button
            {
                Content = "Anuluj",
                Padding = new Thickness(22, 10, 22, 10),
                FontSize = 12.5,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECF0F1")),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D")),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0),
                IsCancel = true
            };
            var btnDalej = new Button
            {
                Content = "Dalej  →",
                Padding = new Thickness(26, 10, 26, 10),
                FontSize = 12.5, FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BDC3C7")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                IsDefault = true,
                IsEnabled = false
            };
            footerStack.Children.Add(btnAnuluj);
            footerStack.Children.Add(btnDalej);
            Grid.SetColumn(footerStack, 1);
            footerGrid.Children.Add(footerStack);

            footer.Child = footerGrid;
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            dialog.Content = root;

            // ====== LOGIKA ======
            // Ladowanie faktur po wyborze kontrahenta
            Action<KontrahentItem> zaladujFaktury = (kontr) =>
            {
                cbFaktura.ItemsSource = null;
                cbFaktura.IsEnabled = false;
                btnDalej.IsEnabled = false;
                btnDalej.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BDC3C7"));

                if (kontr == null)
                {
                    txtStatus.Text = "Wybierz kontrahenta aby zobaczyc jego ostatnie faktury";
                    return;
                }

                var faktury = new List<FakturaSprzedazyItem>();
                try
                {
                    using (var connH = new SqlConnection(HandelConnString))
                    {
                        connH.Open();
                        using (var cmd = new SqlCommand(@"
                            SELECT TOP 15
                                   DK.id, DK.kod, DK.data,
                                   ABS(ISNULL(DK.walNetto, 0)) AS Wartosc,
                                   ABS(ISNULL((SELECT SUM(DP.ilosc) FROM [HANDEL].[HM].[DP] DP WHERE DP.super = DK.id), 0)) AS SumaKg
                            FROM [HANDEL].[HM].[DK] DK
                            WHERE DK.khid = @Khid
                              AND DK.seria NOT IN ('sFKS', 'sFKSB', 'sFWK')
                              AND DK.anulowany = 0
                            ORDER BY DK.data DESC, DK.id DESC", connH))
                        {
                            cmd.Parameters.AddWithValue("@Khid", kontr.Id);
                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    faktury.Add(new FakturaSprzedazyItem
                                    {
                                        Id = r.GetInt32(0),
                                        NumerDokumentu = r.IsDBNull(1) ? "" : r.GetString(1),
                                        Data = r.GetDateTime(2),
                                        IdKontrahenta = kontr.Id,
                                        NazwaKontrahenta = kontr.Shortcut,
                                        Wartosc = r.IsDBNull(3) ? 0m : Convert.ToDecimal(r.GetValue(3)),
                                        SumaKg = r.IsDBNull(4) ? 0m : Convert.ToDecimal(r.GetValue(4))
                                    });
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    txtStatus.Text = $"Blad: {ex.Message}";
                    return;
                }

                if (faktury.Count == 0)
                {
                    txtStatus.Text = "Brak faktur sprzedazy dla tego kontrahenta";
                    return;
                }

                cbFaktura.ItemsSource = faktury;
                cbFaktura.DisplayMemberPath = "OpisDoCombobox";
                cbFaktura.IsEnabled = true;
                cbFaktura.SelectedIndex = 0;
                txtStatus.Text = $"Zaladowano {faktury.Count} ostatnich faktur. Wybierz z listy.";
            };

            cbKontrahent.SelectionChanged += (s, ev) =>
            {
                if (cbKontrahent.SelectedItem is KontrahentItem k) zaladujFaktury(k);
            };
            // Pozwalamy tez na zatwierdzenie po wyszukaniu przez text (edit box)
            cbKontrahent.LostFocus += (s, ev) =>
            {
                if (cbKontrahent.SelectedItem == null && !string.IsNullOrWhiteSpace(cbKontrahent.Text))
                {
                    string tekst = cbKontrahent.Text.Trim();
                    var dopasowany = kontrahenci.FirstOrDefault(x => x.Shortcut != null && x.Shortcut.Equals(tekst, StringComparison.OrdinalIgnoreCase))
                                  ?? kontrahenci.FirstOrDefault(x => x.Shortcut != null && x.Shortcut.StartsWith(tekst, StringComparison.OrdinalIgnoreCase));
                    if (dopasowany != null)
                    {
                        cbKontrahent.SelectedItem = dopasowany;
                    }
                }
            };

            Action<FakturaSprzedazyItem> zaladujTowary = (fakt) =>
            {
                dgTowary.ItemsSource = null;
                txtTowarySuma.Text = "";

                if (fakt == null)
                {
                    towaryCard.Visibility = Visibility.Collapsed;
                    towaryEmpty.Visibility = Visibility.Visible;
                    return;
                }

                var pozycje = new List<TowarFaktury>();
                try
                {
                    using (var connH = new SqlConnection(HandelConnString))
                    {
                        connH.Open();
                        using (var cmd = new SqlCommand(@"
                            SELECT DP.kod AS Symbol,
                                   ISNULL(TW.nazwa, TW.kod) AS Nazwa,
                                   ABS(SUM(ISNULL(DP.ilosc, 0))) AS Waga,
                                   MAX(ABS(ISNULL(DP.cena, 0))) AS Cena,
                                   ABS(SUM(ISNULL(DP.ilosc, 0) * ISNULL(DP.cena, 0))) AS Wartosc
                            FROM [HANDEL].[HM].[DP] DP
                            LEFT JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.ID
                            WHERE DP.super = @IdDok
                            GROUP BY DP.kod, TW.nazwa, TW.kod
                            HAVING ABS(SUM(ISNULL(DP.ilosc, 0))) > 0.01
                            ORDER BY MIN(DP.lp)", connH))
                        {
                            cmd.Parameters.AddWithValue("@IdDok", fakt.Id);
                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    pozycje.Add(new TowarFaktury
                                    {
                                        Symbol = r.IsDBNull(0) ? "" : r.GetString(0),
                                        Nazwa = r.IsDBNull(1) ? "" : r.GetString(1),
                                        Waga = r.IsDBNull(2) ? 0m : Convert.ToDecimal(r.GetValue(2)),
                                        Cena = r.IsDBNull(3) ? 0m : Convert.ToDecimal(r.GetValue(3)),
                                        Wartosc = r.IsDBNull(4) ? 0m : Convert.ToDecimal(r.GetValue(4))
                                    });
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    txtTowarySuma.Text = $"Blad: {ex.Message}";
                    towaryCard.Visibility = Visibility.Visible;
                    towaryEmpty.Visibility = Visibility.Collapsed;
                    return;
                }

                dgTowary.ItemsSource = pozycje;
                decimal sWaga = 0, sWart = 0;
                foreach (var p in pozycje) { sWaga += p.Waga; sWart += p.Wartosc; }
                txtTowarySuma.Text = $"{pozycje.Count} pozycji   |   Razem: {sWaga:#,##0.00} kg   |   {sWart:#,##0.00} zl";

                towaryCard.Visibility = Visibility.Visible;
                towaryEmpty.Visibility = Visibility.Collapsed;
            };

            cbFaktura.SelectionChanged += (s, ev) =>
            {
                var wybrana = cbFaktura.SelectedItem as FakturaSprzedazyItem;
                bool ok = wybrana != null;
                btnDalej.IsEnabled = ok;
                btnDalej.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ok ? "#3498DB" : "#BDC3C7"));
                zaladujTowary(wybrana);
            };

            // Empty state domyslnie widoczny, karta ukryta
            towaryCard.Visibility = Visibility.Collapsed;

            btnAnuluj.Click += (s, ev) => dialog.Close();
            btnDalej.Click += (s, ev) =>
            {
                if (!(cbFaktura.SelectedItem is FakturaSprzedazyItem wybrana))
                {
                    MessageBox.Show("Wybierz fakture z listy.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                dialog.Close();

                // Otworz ten sam formularz co przy "+ Nowa reklamacja"
                var okno = new FormReklamacjaWindow(
                    HandelConnString,
                    wybrana.Id,
                    wybrana.IdKontrahenta,
                    wybrana.NumerDokumentu,
                    wybrana.NazwaKontrahenta,
                    userId,
                    connectionString);
                okno.Owner = this;
                if (okno.ShowDialog() == true)
                {
                    WczytajReklamacje();
                    WczytajStatystyki();
                }
            };

            dialog.Loaded += (s, ev) => cbKontrahent.Focus();
            dialog.ShowDialog();
        }


        private void BtnNowaReklamacja_Click(object sender, RoutedEventArgs e)
        {
            // Pobierz faktury sprzedazy z HANDEL (180 dni wstecz)
            var faktury = new List<FakturaSprzedazyItem>();
            try
            {
                using (var connHandel = new SqlConnection(HandelConnString))
                {
                    connHandel.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT TOP 1000
                               DK.id, DK.kod, DK.data, DK.khid,
                               C.shortcut AS NazwaKontrahenta,
                               ABS(ISNULL(DK.walNetto, 0)) AS Wartosc,
                               ABS(ISNULL((SELECT SUM(DP.ilosc) FROM [HANDEL].[HM].[DP] DP WHERE DP.super = DK.id), 0)) AS SumaKg,
                               ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec
                        FROM [HANDEL].[HM].[DK] DK
                        INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                        LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                        WHERE DK.seria NOT IN ('sFKS', 'sFKSB', 'sFWK')
                          AND DK.anulowany = 0
                          AND DK.data >= DATEADD(DAY, -180, GETDATE())
                          AND C.shortcut NOT LIKE 'SD/%'
                        ORDER BY DK.data DESC, DK.id DESC", connHandel))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var data = reader.GetDateTime(2);
                                int dniTemu = (int)Math.Floor((DateTime.Today - data.Date).TotalDays);
                                faktury.Add(new FakturaSprzedazyItem
                                {
                                    Id = reader.GetInt32(0),
                                    NumerDokumentu = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                    Data = data,
                                    IdKontrahenta = reader.GetInt32(3),
                                    NazwaKontrahenta = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    Wartosc = reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetValue(5)),
                                    SumaKg = reader.IsDBNull(6) ? 0m : Convert.ToDecimal(reader.GetValue(6)),
                                    Handlowiec = reader.IsDBNull(7) ? "-" : reader.GetString(7),
                                    DniTemu = dniTemu,
                                    EtykietaCzasu = dniTemu == 0 ? "DZIS"
                                                  : dniTemu == 1 ? "WCZORAJ"
                                                  : $"{dniTemu} dni"
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                FriendlyError.Pokaz(ex, "Nie udalo sie pobrac faktur z Symfonii.", this);
                return;
            }

            if (faktury.Count == 0)
            {
                MessageBox.Show("Nie znaleziono faktur sprzedazy z ostatnich 180 dni.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // ====== DIALOG ======
            var dialogPick = new Window
            {
                Title = "Nowa reklamacja",
                WindowState = WindowState.Maximized,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4F6F8")),
                FontFamily = new FontFamily("Segoe UI")
            };
            WindowIconHelper.SetIcon(dialogPick);

            var root = new Grid();
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

            // ===== HEADER =====
            var header = new Border
            {
                Padding = new Thickness(28, 18, 28, 18),
                Background = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#E74C3C"),
                    (Color)ColorConverter.ConvertFromString("#C0392B"),
                    new System.Windows.Point(0, 0), new System.Windows.Point(1, 0))
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

            // Ikona
            var iconCircle = new Border
            {
                Width = 52, Height = 52,
                CornerRadius = new CornerRadius(26),
                Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                Margin = new Thickness(0, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            iconCircle.Child = new TextBlock
            {
                Text = "+",
                FontSize = 32, FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -4, 0, 0)
            };
            Grid.SetColumn(iconCircle, 0);
            headerGrid.Children.Add(iconCircle);

            var headerText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            headerText.Children.Add(new TextBlock
            {
                Text = "Nowa reklamacja",
                FontSize = 20, FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            headerText.Children.Add(new TextBlock
            {
                Text = "Wybierz fakture sprzedazy, aby zglosic reklamacje",
                FontSize = 12.5,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FADBD8")),
                Margin = new Thickness(0, 4, 0, 0)
            });
            Grid.SetColumn(headerText, 1);
            headerGrid.Children.Add(headerText);

            // Statystyki w headerze
            var statBox = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(statBox, 2);

            var statBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 10, 16, 10)
            };
            var statPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var statLiczba = new TextBlock
            {
                Text = faktury.Count.ToString(),
                FontSize = 22, FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            statPanel.Children.Add(statLiczba);
            statPanel.Children.Add(new TextBlock
            {
                Text = "  faktur dostepnych",
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FADBD8")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            });
            statBorder.Child = statPanel;
            statBox.Children.Add(statBorder);
            headerGrid.Children.Add(statBox);

            header.Child = headerGrid;
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // ===== PASEK FILTRA =====
            var filterCard = new Border
            {
                Background = Brushes.White,
                Margin = new Thickness(20, 18, 20, 0),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20, 16, 20, 16),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black, Opacity = 0.06, BlurRadius = 14, ShadowDepth = 2, Direction = 270
                }
            };
            var filterStack = new StackPanel();

            // Linia 1: szukanie
            var searchRow = new Grid();
            searchRow.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });
            searchRow.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchRow.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

            searchRow.Children.Add(new TextBlock
            {
                Text = "Szukaj",
                FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0)
            });

            // TextBox + watermark via Grid
            var searchBoxGrid = new Grid();
            Grid.SetColumn(searchBoxGrid, 1);

            var txtPickFiltr = new TextBox
            {
                FontSize = 14,
                Padding = new Thickness(14, 10, 14, 10),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDE2E6")),
                BorderThickness = new Thickness(1.5),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F9FA")),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            var watermark = new TextBlock
            {
                Text = "Wpisz nazwe kontrahenta, numer faktury lub handlowca...",
                FontSize = 13,
                FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B0B7BD")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(18, 0, 0, 0),
                IsHitTestVisible = false
            };
            searchBoxGrid.Children.Add(txtPickFiltr);
            searchBoxGrid.Children.Add(watermark);
            searchRow.Children.Add(searchBoxGrid);

            var btnPickWyczysc = new Button
            {
                Content = "Wyczysc",
                Padding = new Thickness(14, 9, 14, 9),
                FontSize = 11.5,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECF0F1")),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D")),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(btnPickWyczysc, 2);
            searchRow.Children.Add(btnPickWyczysc);

            filterStack.Children.Add(searchRow);

            // Linia 2: szybkie filtry zakresu czasu
            var rangeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
            rangeRow.Children.Add(new TextBlock
            {
                Text = "Zakres",
                FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0)
            });

            var rangeButtons = new List<(string label, int dni, Button btn)>();
            int aktywnyZakres = 30;
            Button MakeRangeButton(string label, int dni)
            {
                var b = new Button
                {
                    Content = label,
                    Padding = new Thickness(16, 8, 16, 8),
                    FontSize = 11.5, FontWeight = FontWeights.SemiBold,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F3F5")),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D")),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                return b;
            }

            var bD = MakeRangeButton("Dzisiaj", 0);
            var b7 = MakeRangeButton("7 dni", 7);
            var b30 = MakeRangeButton("30 dni", 30);
            var b90 = MakeRangeButton("90 dni", 90);
            var bAll = MakeRangeButton("Wszystkie", 999);
            rangeButtons.Add(("Dzisiaj", 0, bD));
            rangeButtons.Add(("7 dni", 7, b7));
            rangeButtons.Add(("30 dni", 30, b30));
            rangeButtons.Add(("90 dni", 90, b90));
            rangeButtons.Add(("Wszystkie", 999, bAll));
            foreach (var (_, _, btn) in rangeButtons) rangeRow.Children.Add(btn);

            filterStack.Children.Add(rangeRow);

            filterCard.Child = filterStack;
            Grid.SetRow(filterCard, 1);
            root.Children.Add(filterCard);

            // ===== DATAGRID =====
            var gridCard = new Border
            {
                Background = Brushes.White,
                Margin = new Thickness(20, 14, 20, 0),
                CornerRadius = new CornerRadius(12),
                ClipToBounds = true,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black, Opacity = 0.06, BlurRadius = 14, ShadowDepth = 2, Direction = 270
                }
            };

            var gridRoot = new Grid();
            gridRoot.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var dgFaktury = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                SelectionMode = DataGridSelectionMode.Single,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EFF2F5")),
                BorderThickness = new Thickness(0),
                RowHeight = 44,
                FontSize = 13,
                Background = Brushes.White,
                RowBackground = Brushes.White,
                AlternatingRowBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFBFC")),
                ColumnHeaderHeight = 42
            };

            // Header style
            var headerStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F9FA"))));
            headerStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5D6D7E"))));
            headerStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
            headerStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontSizeProperty, 11.0));
            headerStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty, new Thickness(12, 0, 12, 0)));
            headerStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderBrushProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E8EB"))));
            headerStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            dgFaktury.ColumnHeaderStyle = headerStyle;

            // Cell padding
            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(DataGridCell.PaddingProperty, new Thickness(12, 0, 12, 0)));
            cellStyle.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));
            cellStyle.Setters.Add(new Setter(DataGridCell.FocusVisualStyleProperty, null));
            var cellTrigger = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
            cellTrigger.Setters.Add(new Setter(DataGridCell.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDEDEC"))));
            cellTrigger.Setters.Add(new Setter(DataGridCell.ForegroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C3E50"))));
            cellStyle.Triggers.Add(cellTrigger);
            dgFaktury.CellStyle = cellStyle;

            // Row hover
            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(DataGridRow.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            var rowHover = new Trigger { Property = DataGridRow.IsMouseOverProperty, Value = true };
            rowHover.Setters.Add(new Setter(DataGridRow.BackgroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF5F4"))));
            rowStyle.Triggers.Add(rowHover);
            dgFaktury.RowStyle = rowStyle;

            // KOLUMNY
            // Data
            dgFaktury.Columns.Add(new DataGridTextColumn
            {
                Header = "DATA",
                Binding = new System.Windows.Data.Binding("Data") { StringFormat = "dd.MM.yyyy" },
                Width = new DataGridLength(105)
            });

            // Etykieta czasu - chip
            var colCzas = new DataGridTemplateColumn { Header = "KIEDY", Width = new DataGridLength(95), SortMemberPath = "DniTemu" };
            var dtCzas = new DataTemplate();
            var fefCzas = new FrameworkElementFactory(typeof(Border));
            fefCzas.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            fefCzas.SetValue(Border.PaddingProperty, new Thickness(10, 3, 10, 3));
            fefCzas.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            fefCzas.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("EtykietaTloKolor"));
            var fefCzasText = new FrameworkElementFactory(typeof(TextBlock));
            fefCzasText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("EtykietaCzasu"));
            fefCzasText.SetValue(TextBlock.FontSizeProperty, 10.0);
            fefCzasText.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            fefCzasText.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("EtykietaTekstKolor"));
            fefCzas.AppendChild(fefCzasText);
            dtCzas.VisualTree = fefCzas;
            colCzas.CellTemplate = dtCzas;
            dgFaktury.Columns.Add(colCzas);

            // Numer faktury
            var colNr = new DataGridTextColumn
            {
                Header = "NR FAKTURY",
                Binding = new System.Windows.Data.Binding("NumerDokumentu"),
                Width = new DataGridLength(165)
            };
            var nrStyle = new Style(typeof(TextBlock));
            nrStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
            nrStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C3E50"))));
            nrStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            colNr.ElementStyle = nrStyle;
            dgFaktury.Columns.Add(colNr);

            // Kontrahent
            var colKontr = new DataGridTextColumn
            {
                Header = "KONTRAHENT",
                Binding = new System.Windows.Data.Binding("NazwaKontrahenta"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            };
            var kontrStyle = new Style(typeof(TextBlock));
            kontrStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            kontrStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            colKontr.ElementStyle = kontrStyle;
            dgFaktury.Columns.Add(colKontr);

            // Handlowiec
            var colHandl = new DataGridTextColumn
            {
                Header = "HANDLOWIEC",
                Binding = new System.Windows.Data.Binding("Handlowiec"),
                Width = new DataGridLength(150)
            };
            var handlStyle = new Style(typeof(TextBlock));
            handlStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            handlStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D"))));
            handlStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 12.0));
            colHandl.ElementStyle = handlStyle;
            dgFaktury.Columns.Add(colHandl);

            // Kg
            var colKg = new DataGridTextColumn
            {
                Header = "KG",
                Binding = new System.Windows.Data.Binding("SumaKg") { StringFormat = "#,##0.00" },
                Width = new DataGridLength(110)
            };
            var kgStyle = new Style(typeof(TextBlock));
            kgStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            kgStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            kgStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5D6D7E"))));
            colKg.ElementStyle = kgStyle;
            dgFaktury.Columns.Add(colKg);

            // Wartosc
            var colWart = new DataGridTextColumn
            {
                Header = "WARTOSC NETTO",
                Binding = new System.Windows.Data.Binding("Wartosc") { StringFormat = "#,##0.00 zl" },
                Width = new DataGridLength(150)
            };
            var wartStyle = new Style(typeof(TextBlock));
            wartStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            wartStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            wartStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));
            wartStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"))));
            colWart.ElementStyle = wartStyle;
            dgFaktury.Columns.Add(colWart);

            Grid.SetRow(dgFaktury, 0);
            gridRoot.Children.Add(dgFaktury);

            // Empty state overlay
            var emptyState = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            emptyState.Children.Add(new TextBlock
            {
                Text = "Brak wynikow",
                FontSize = 18, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6")),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            emptyState.Children.Add(new TextBlock
            {
                Text = "Zmien zakres czasu lub fraze wyszukiwania",
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BDC3C7")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            });
            Grid.SetRow(emptyState, 0);
            gridRoot.Children.Add(emptyState);

            gridCard.Child = gridRoot;
            Grid.SetRow(gridCard, 2);
            root.Children.Add(gridCard);

            // ===== STOPKA =====
            var footer = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E8EB")),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(28, 16, 28, 16),
                Margin = new Thickness(0, 14, 0, 0)
            };
            var footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });
            footerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

            // Lewa: licznik + suma
            var footerLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var txtPickLicznik = new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C3E50")),
                VerticalAlignment = VerticalAlignment.Center
            };
            footerLeft.Children.Add(txtPickLicznik);

            var sep1 = new Border
            {
                Width = 1, Height = 22,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E8EB")),
                Margin = new Thickness(16, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            footerLeft.Children.Add(sep1);

            var txtPickSuma = new TextBlock
            {
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D")),
                VerticalAlignment = VerticalAlignment.Center
            };
            footerLeft.Children.Add(txtPickSuma);

            Grid.SetColumn(footerLeft, 0);
            footerGrid.Children.Add(footerLeft);

            // Srodek: hint
            var hint = new TextBlock
            {
                Text = "Enter = wybierz   |   Esc = anuluj   |   Dwuklik na wierszu",
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BDC3C7")),
                FontStyle = FontStyles.Italic,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(hint, 1);
            footerGrid.Children.Add(hint);

            // Prawa: przyciski
            var footerBtns = new StackPanel { Orientation = Orientation.Horizontal };
            var btnPickAnuluj = new Button
            {
                Content = "Anuluj",
                Padding = new Thickness(22, 11, 22, 11),
                FontSize = 12.5,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECF0F1")),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D")),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0),
                IsCancel = true
            };
            btnPickAnuluj.Click += (s2, e2) => dialogPick.Close();
            footerBtns.Children.Add(btnPickAnuluj);

            var btnPickOk = new Button
            {
                Content = "Zglos reklamacje  →",
                Padding = new Thickness(24, 11, 24, 11),
                FontSize = 12.5, FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                IsDefault = true,
                IsEnabled = false
            };
            footerBtns.Children.Add(btnPickOk);

            Grid.SetColumn(footerBtns, 2);
            footerGrid.Children.Add(footerBtns);

            footer.Child = footerGrid;
            Grid.SetRow(footer, 3);
            root.Children.Add(footer);

            dialogPick.Content = root;

            // ===== LOGIKA =====
            // Kolory etykiet czasu
            var brushDzis = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));
            var brushDzisBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F8EF"));
            var brushTydzien = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2980B9"));
            var brushTydzienBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAF2FB"));
            var brushStary = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D"));
            var brushStaryBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F3F5"));

            foreach (var f in faktury)
            {
                if (f.DniTemu <= 1) { f.EtykietaTloKolor = brushDzisBg; f.EtykietaTekstKolor = brushDzis; }
                else if (f.DniTemu <= 7) { f.EtykietaTloKolor = brushTydzienBg; f.EtykietaTekstKolor = brushTydzien; }
                else { f.EtykietaTloKolor = brushStaryBg; f.EtykietaTekstKolor = brushStary; }
            }

            // Filtrowanie
            Action<int> ustawAktywnyZakres = (dni) =>
            {
                aktywnyZakres = dni;
                foreach (var (lbl, d, btn) in rangeButtons)
                {
                    if (d == dni)
                    {
                        btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
                        btn.Foreground = Brushes.White;
                    }
                    else
                    {
                        btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F3F5"));
                        btn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D"));
                    }
                }
            };

            Action filtrujPick = () =>
            {
                string fraza = txtPickFiltr.Text.Trim().ToLower();
                watermark.Visibility = string.IsNullOrEmpty(txtPickFiltr.Text) ? Visibility.Visible : Visibility.Collapsed;

                var przefiltrowane = faktury.Where(f =>
                {
                    if (aktywnyZakres < 999 && f.DniTemu > aktywnyZakres) return false;
                    if (string.IsNullOrEmpty(fraza)) return true;
                    return (f.NazwaKontrahenta ?? "").ToLower().Contains(fraza)
                        || (f.NumerDokumentu ?? "").ToLower().Contains(fraza)
                        || (f.Handlowiec ?? "").ToLower().Contains(fraza);
                }).ToList();

                dgFaktury.ItemsSource = przefiltrowane;

                txtPickLicznik.Text = $"{przefiltrowane.Count} z {faktury.Count} faktur";
                decimal sumaWart = przefiltrowane.Sum(x => x.Wartosc);
                decimal sumaKgF = przefiltrowane.Sum(x => x.SumaKg);
                txtPickSuma.Text = $"Suma: {sumaWart:#,##0.00} zl   |   {sumaKgF:#,##0.00} kg";

                emptyState.Visibility = przefiltrowane.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            };

            var pickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            pickTimer.Tick += (s2, e2) => { pickTimer.Stop(); filtrujPick(); };
            txtPickFiltr.TextChanged += (s2, e2) =>
            {
                watermark.Visibility = string.IsNullOrEmpty(txtPickFiltr.Text) ? Visibility.Visible : Visibility.Collapsed;
                pickTimer.Stop();
                pickTimer.Start();
            };
            btnPickWyczysc.Click += (s2, e2) => { txtPickFiltr.Text = ""; filtrujPick(); txtPickFiltr.Focus(); };

            foreach (var (lbl, d, btn) in rangeButtons)
            {
                int captured = d;
                btn.Click += (s2, e2) => { ustawAktywnyZakres(captured); filtrujPick(); };
            }

            ustawAktywnyZakres(30);
            filtrujPick();

            // Selection -> enable button
            dgFaktury.SelectionChanged += (s2, e2) =>
            {
                btnPickOk.IsEnabled = dgFaktury.SelectedItem != null;
                btnPickOk.Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(dgFaktury.SelectedItem != null ? "#E74C3C" : "#F5B7B1"));
            };

            Action uruchomReklamacje = () =>
            {
                if (!(dgFaktury.SelectedItem is FakturaSprzedazyItem wybrana)) return;
                dialogPick.Close();

                var okno = new FormReklamacjaWindow(
                    HandelConnString,
                    wybrana.Id,
                    wybrana.IdKontrahenta,
                    wybrana.NumerDokumentu,
                    wybrana.NazwaKontrahenta,
                    userId,
                    connectionString);
                okno.Owner = this;
                if (okno.ShowDialog() == true)
                {
                    WczytajReklamacje();
                    WczytajStatystyki();
                }
            };

            btnPickOk.Click += (s2, e2) =>
            {
                if (dgFaktury.SelectedItem == null)
                {
                    MessageBox.Show("Wybierz fakture z listy.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                uruchomReklamacje();
            };
            dgFaktury.MouseDoubleClick += (s2, e2) =>
            {
                if (dgFaktury.SelectedItem != null) uruchomReklamacje();
            };

            // Enter w textboxie -> jesli jest 1 wynik, wybierz, inaczej skocz na grid
            txtPickFiltr.KeyDown += (s2, e2) =>
            {
                if (e2.Key == Key.Enter)
                {
                    if (dgFaktury.Items.Count == 1)
                    {
                        dgFaktury.SelectedIndex = 0;
                        uruchomReklamacje();
                    }
                    else if (dgFaktury.Items.Count > 0)
                    {
                        dgFaktury.Focus();
                        dgFaktury.SelectedIndex = 0;
                    }
                    e2.Handled = true;
                }
                else if (e2.Key == Key.Down && dgFaktury.Items.Count > 0)
                {
                    dgFaktury.Focus();
                    dgFaktury.SelectedIndex = 0;
                    e2.Handled = true;
                }
            };

            dgFaktury.KeyDown += (s2, e2) =>
            {
                if (e2.Key == Key.Enter && dgFaktury.SelectedItem != null)
                {
                    uruchomReklamacje();
                    e2.Handled = true;
                }
            };

            dialogPick.Loaded += (s2, e2) => { txtPickFiltr.Focus(); };
            dialogPick.ShowDialog();
        }


        private void BtnUstawieniaSync_Click(object sender, RoutedEventArgs e)
        {
            DateTime obecnaData = PobierzDataOdKorekt();

            var dialog = new Window
            {
                Title = "Ustawienia synchronizacji korekt",
                Width = 480,
                Height = 320,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = Brushes.White
            };
            WindowIconHelper.SetIcon(dialog);

            var mainPanel = new StackPanel { Margin = new Thickness(24) };

            // Header
            var headerPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
            headerPanel.Children.Add(new TextBlock
            {
                Text = "USTAWIENIA SYNC KOREKT",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E44AD"))
            });
            headerPanel.Children.Add(new TextBlock
            {
                Text = "Ustaw date od kiedy pobierac faktury korygujace z Symfonii",
                FontSize = 11,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 4, 0, 0)
            });
            mainPanel.Children.Add(headerPanel);

            // Aktualna wartosc
            var infoPanel = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3E5F5")),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var infoText = new TextBlock
            {
                FontSize = 11.5,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A148C"))
            };
            infoText.Inlines.Add(new System.Windows.Documents.Run("Aktualna data od: ") { FontWeight = FontWeights.Normal });
            infoText.Inlines.Add(new System.Windows.Documents.Run(obecnaData.ToString("dd.MM.yyyy")) { FontWeight = FontWeights.Bold });
            infoPanel.Child = infoText;
            mainPanel.Children.Add(infoPanel);

            // DatePicker
            mainPanel.Children.Add(new TextBlock
            {
                Text = "Nowa data poczatkowa:",
                FontSize = 11,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 4)
            });

            var datePicker = new DatePicker
            {
                SelectedDate = obecnaData,
                FontSize = 13,
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 8),
                DisplayDateStart = new DateTime(2020, 1, 1),
                DisplayDateEnd = DateTime.Now
            };
            mainPanel.Children.Add(datePicker);

            mainPanel.Children.Add(new TextBlock
            {
                Text = "Korekty z Symfonii starsze niz ta data nie beda synchronizowane.",
                FontSize = 10,
                Foreground = Brushes.Gray,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 0, 0, 16)
            });

            // Buttons
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var btnZapisz = new Button
            {
                Content = "Zapisz i sync",
                Width = 120,
                Height = 36,
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E44AD")),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnZapisz.Click += (s, args) =>
            {
                if (!datePicker.SelectedDate.HasValue)
                {
                    MessageBox.Show("Wybierz date.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                DateTime nowaData = datePicker.SelectedDate.Value;

                var potwierdzenie = MessageBox.Show(
                    $"Zmienic date synchronizacji korekt na:\n{nowaData:dd.MM.yyyy}\n\nKorekty starsze niz ta data nie beda pobierane.\nPo zapisaniu zostanie uruchomiona ponowna synchronizacja.",
                    "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (potwierdzenie == MessageBoxResult.Yes)
                {
                    ZapiszDataOdKorekt(nowaData);
                    int usunieto = UsunKorektySprzedDaty(nowaData);
                    dialog.Close();

                    // Re-sync
                    try { SyncFakturyKorygujace(); } catch { }
                    WczytajReklamacje();
                    WczytajStatystyki();

                    MessageBox.Show(
                        $"Ustawienie zapisane.\nData od korekt: {nowaData:dd.MM.yyyy}\nUsunieto starych korekt: {usunieto}\nSynchronizacja wykonana.",
                        "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };
            btnPanel.Children.Add(btnZapisz);

            var btnAnuluj = new Button
            {
                Content = "Anuluj",
                Width = 80,
                Height = 36,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6")),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnAnuluj.Click += (s, args) => dialog.Close();
            btnPanel.Children.Add(btnAnuluj);

            mainPanel.Children.Add(btnPanel);
            dialog.Content = mainPanel;
            dialog.ShowDialog();
        }

        // ========================================
        // DEBUG SYNC
        // ========================================

        private void BtnDebugSync_Click(object sender, RoutedEventArgs e)
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine("=== DEBUG SYNC FAKTUR KORYGUJACYCH ===\n");

            // KROK 1: Test polaczenia HANDEL
            log.AppendLine("[1] Polaczenie z HANDEL (192.168.0.112)...");
            try
            {
                using (var conn = new SqlConnection(HandelConnString))
                {
                    conn.Open();
                    log.AppendLine("    OK - Polaczono\n");

                    // KROK 2: Sprawdz ile jest faktur korygujacych (bez filtrow)
                    log.AppendLine("[2] Szukam faktur korygujacych (seria IN sFKS, sFKSB, sFWK)...");
                    using (var cmd = new SqlCommand(
                        "SELECT seria, COUNT(*) AS cnt FROM [HANDEL].[HM].[DK] WHERE seria IN ('sFKS', 'sFKSB', 'sFWK') GROUP BY seria", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            bool any = false;
                            while (reader.Read())
                            {
                                any = true;
                                log.AppendLine($"    seria='{reader.GetString(0)}' -> {reader.GetInt32(1)} dokumentow");
                            }
                            if (!any) log.AppendLine("    BRAK wynikow! Sprawdz nazwy serii.");
                        }
                    }

                    // KROK 3: Sprawdz z filtrami (anulowany=0, data 6 mies)
                    log.AppendLine("\n[3] Z filtrami (anulowany=0, ostatnie 6 mies)...");
                    using (var cmd = new SqlCommand(@"
                        SELECT COUNT(*) FROM [HANDEL].[HM].[DK]
                        WHERE seria IN ('sFKS', 'sFKSB', 'sFWK')
                          AND anulowany = 0
                          AND data >= DATEADD(MONTH, -6, GETDATE())", conn))
                    {
                        int cnt = Convert.ToInt32(cmd.ExecuteScalar());
                        log.AppendLine($"    Znaleziono: {cnt} dokumentow");
                    }

                    // KROK 4: Pokaz przykladowe 5 dokumentow
                    log.AppendLine("\n[4] Przykladowe dokumenty (TOP 5)...");
                    using (var cmd = new SqlCommand(@"
                        SELECT TOP 5 DK.id, DK.kod, DK.seria, DK.data, DK.walNetto, DK.anulowany,
                               ISNULL(C.shortcut, '?') AS Kontrahent
                        FROM [HANDEL].[HM].[DK] DK
                        LEFT JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                        WHERE DK.seria IN ('sFKS', 'sFKSB', 'sFWK')
                        ORDER BY DK.data DESC", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                log.AppendLine($"    id={reader["id"]} kod={reader["kod"]} seria={reader["seria"]} data={reader["data"]} walNetto={reader["walNetto"]} anul={reader["anulowany"]} kontr={reader["Kontrahent"]}");
                            }
                        }
                    }

                    // KROK 4b: Sprawdz WSZYSTKIE unikalne serie w bazie
                    log.AppendLine("\n[4b] Wszystkie unikalne serie w DK (TOP 30)...");
                    using (var cmd = new SqlCommand(
                        "SELECT TOP 30 seria, COUNT(*) AS cnt FROM [HANDEL].[HM].[DK] GROUP BY seria ORDER BY cnt DESC", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string s = reader.IsDBNull(0) ? "(null)" : reader.GetString(0);
                                log.AppendLine($"    '{s}' -> {reader.GetInt32(1)}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.AppendLine($"    BLAD: {ex.Message}");
            }

            // KROK 5: Test polaczenia LibraNet
            log.AppendLine($"\n[5] Polaczenie z LibraNet ({connectionString.Substring(0, 40)}...)...");
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    log.AppendLine("    OK - Polaczono");

                    // Ile reklamacji typu Faktura korygujaca juz jest
                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM [dbo].[Reklamacje] WHERE TypReklamacji = 'Faktura korygujaca'", conn))
                    {
                        int cnt = Convert.ToInt32(cmd.ExecuteScalar());
                        log.AppendLine($"    Istniejace reklamacje typu 'Faktura korygujaca': {cnt}");
                    }

                    // Sprawdz czy kolumna IdDokumentu istnieje
                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Reklamacje' AND COLUMN_NAME = 'IdDokumentu'", conn))
                    {
                        int exists = Convert.ToInt32(cmd.ExecuteScalar());
                        log.AppendLine($"    Kolumna IdDokumentu istnieje: {(exists > 0 ? "TAK" : "NIE")}");
                    }

                    // Sprawdz czy kolumna SumaWartosc istnieje
                    using (var cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Reklamacje' AND COLUMN_NAME = 'SumaWartosc'", conn))
                    {
                        int exists = Convert.ToInt32(cmd.ExecuteScalar());
                        log.AppendLine($"    Kolumna SumaWartosc istnieje: {(exists > 0 ? "TAK" : "NIE")}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.AppendLine($"    BLAD: {ex.Message}");
            }

            // KROK 6: Probuj sync
            log.AppendLine("\n[6] Uruchamiam SyncFakturyKorygujace()...");
            try
            {
                SyncFakturyKorygujace();
                log.AppendLine("    OK - Sync zakonczony bez bledu");
            }
            catch (Exception ex)
            {
                log.AppendLine($"    BLAD SYNC: {ex.Message}\n    {ex.StackTrace}");
            }

            // Pokaz wynik
            var debugWindow = new Window
            {
                Title = "Debug Sync Faktur Korygujacych",
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };
            var textBox = new TextBox
            {
                Text = log.ToString(),
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Margin = new Thickness(10)
            };
            debugWindow.Content = textBox;
            debugWindow.ShowDialog();

            // Odswiez dane
            WczytajReklamacje();
            WczytajStatystyki();
        }

        // ========================================
        // SYNC FAKTUR KORYGUJACYCH Z HANDEL
        // ========================================

        private void SyncFakturyKorygujace()
        {
            try
            {
                // Pobierz date poczatkowa z ustawien (admin moze ja zmienic)
                DateTime dataOdKorekt = PobierzDataOdKorekt();

                // Pobierz faktury korygujace z HANDEL + JOIN do faktury bazowej przez iddokkoryg
                var korygujace = new List<(int id, string kod, DateTime data, decimal wartosc, int khid, string kontrahent, string handlowiec, decimal sumaKg, int? idFaktBazowej, string nrFaktBazowej)>();

                using (var connHandel = new SqlConnection(HandelConnString))
                {
                    connHandel.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT DK.id, DK.kod, DK.data,
                               ABS(DK.walNetto) AS Wartosc,
                               DK.khid,
                               C.shortcut AS NazwaKontrahenta,
                               ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec,
                               ABS(ISNULL((SELECT SUM(DP.ilosc) FROM [HANDEL].[HM].[DP] DP WHERE DP.super = DK.id), 0)) AS SumaKg,
                               DK.iddokkoryg AS IdFaktBazowej,
                               F.kod AS NrFaktBazowej
                        FROM [HANDEL].[HM].[DK] DK
                        INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                        LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                        LEFT JOIN [HANDEL].[HM].[DK] F ON DK.iddokkoryg = F.id
                        WHERE DK.seria IN ('sFKS', 'sFKSB', 'sFWK')
                          AND DK.anulowany = 0
                          AND DK.data >= @DataOd
                          AND C.shortcut NOT LIKE 'SD/%'", connHandel))
                    {
                        cmd.Parameters.AddWithValue("@DataOd", dataOdKorekt);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                korygujace.Add((
                                    reader.GetInt32(0),
                                    reader.GetString(1),
                                    reader.GetDateTime(2),
                                    reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3)),
                                    reader.GetInt32(4),
                                    reader.IsDBNull(5) ? "" : reader.GetString(5),
                                    reader.IsDBNull(6) ? "-" : reader.GetString(6),
                                    reader.IsDBNull(7) ? 0m : Convert.ToDecimal(reader.GetValue(7)),
                                    reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8),
                                    reader.IsDBNull(9) ? null : reader.GetString(9)
                                ));
                            }
                        }
                    }
                }

                if (korygujace.Count == 0) return;

                // Wstaw do LibraNet te, ktorych jeszcze nie ma
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    foreach (var fk in korygujace)
                    {
                        // Sprawdz czy juz istnieje
                        using (var cmdCheck = new SqlCommand(
                            "SELECT COUNT(*) FROM [dbo].[Reklamacje] WHERE IdDokumentu = @IdDok AND TypReklamacji = 'Faktura korygujaca'", conn))
                        {
                            cmdCheck.Parameters.AddWithValue("@IdDok", fk.id);
                            if (Convert.ToInt32(cmdCheck.ExecuteScalar()) > 0) continue;
                        }

                        // Wstaw nowa reklamacje (z polami Workflow V2 + faktura bazowa)
                        int noweIdReklamacji = 0;
                        using (var cmdInsert = new SqlCommand(@"
                            INSERT INTO [dbo].[Reklamacje]
                            (DataZgloszenia, UserID, IdDokumentu, NumerDokumentu, IdKontrahenta, NazwaKontrahenta,
                             Opis, SumaKg, SumaWartosc, Status, TypReklamacji, Priorytet,
                             StatusV2, ZrodloZgloszenia, WymagaUzupelnienia, Handlowiec,
                             IdFakturyOryginalnej, NumerFakturyOryginalnej)
                            VALUES
                            (@Data, '', @IdDok, @NrDok, @IdKontr, @Kontrahent,
                             @Opis, @SumaKg, @Wartosc, 'Nowa', 'Faktura korygujaca', 'Normalny',
                             'ZGLOSZONA', 'Symfonia', 1, @Handlowiec,
                             @IdFaktBaz, @NrFaktBaz);
                            SELECT SCOPE_IDENTITY();", conn))
                        {
                            cmdInsert.Parameters.AddWithValue("@Data", fk.data);
                            cmdInsert.Parameters.AddWithValue("@IdDok", fk.id);
                            cmdInsert.Parameters.AddWithValue("@NrDok", fk.kod);
                            cmdInsert.Parameters.AddWithValue("@IdKontr", fk.khid);
                            cmdInsert.Parameters.AddWithValue("@Kontrahent", fk.kontrahent);
                            string opisInfo = fk.nrFaktBazowej != null
                                ? $"Faktura korygujaca {fk.kod} do {fk.nrFaktBazowej} | Handlowiec: {fk.handlowiec}"
                                : $"Faktura korygujaca {fk.kod} | Handlowiec: {fk.handlowiec}";
                            cmdInsert.Parameters.AddWithValue("@Opis", opisInfo);
                            cmdInsert.Parameters.AddWithValue("@SumaKg", fk.sumaKg);
                            cmdInsert.Parameters.AddWithValue("@Wartosc", fk.wartosc);
                            cmdInsert.Parameters.AddWithValue("@Handlowiec", fk.handlowiec ?? "-");
                            cmdInsert.Parameters.AddWithValue("@IdFaktBaz", (object)fk.idFaktBazowej ?? DBNull.Value);
                            cmdInsert.Parameters.AddWithValue("@NrFaktBaz", (object)fk.nrFaktBazowej ?? DBNull.Value);
                            var result = cmdInsert.ExecuteScalar();
                            if (result != null && result != DBNull.Value)
                                noweIdReklamacji = Convert.ToInt32(result);
                        }

                        // AUTO-MATCHING: najpierw sprobuj po IdFakturyOryginalnej (100% trafnosc z iddokkoryg)
                        // potem fallback na khid + 14 dni
                        if (noweIdReklamacji > 0)
                        {
                            try { ProbujAutoMatch(conn, noweIdReklamacji, fk.khid, fk.data, fk.idFaktBazowej); } catch { }
                        }

                        // Wstaw towary z faktury korygujuacej z HANDEL
                        if (noweIdReklamacji > 0)
                        {
                            try
                            {
                                using (var connH = new SqlConnection(HandelConnString))
                                {
                                    connH.Open();
                                    using (var cmdTow = new SqlCommand(@"
                                        SELECT DP.kod AS Symbol,
                                               ISNULL(TW.nazwa, TW.kod) AS Nazwa,
                                               ABS(SUM(ISNULL(DP.ilosc, 0))) AS Waga,
                                               MAX(ABS(ISNULL(DP.cena, 0))) AS Cena,
                                               ABS(SUM(ISNULL(DP.ilosc, 0) * ISNULL(DP.cena, 0))) AS Wartosc
                                        FROM [HANDEL].[HM].[DP] DP
                                        LEFT JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.ID
                                        WHERE DP.super = @IdDok
                                        GROUP BY DP.kod, TW.nazwa, TW.kod
                                        HAVING ABS(SUM(ISNULL(DP.ilosc, 0))) > 0.01
                                        ORDER BY MIN(DP.lp)", connH))
                                    {
                                        cmdTow.Parameters.AddWithValue("@IdDok", fk.id);
                                        using (var rdrTow = cmdTow.ExecuteReader())
                                        {
                                            var pozycje = new List<(string symbol, string nazwa, decimal waga, decimal cena, decimal wartosc)>();
                                            while (rdrTow.Read())
                                            {
                                                pozycje.Add((
                                                    rdrTow.IsDBNull(0) ? "" : rdrTow.GetString(0),
                                                    rdrTow.IsDBNull(1) ? "" : rdrTow.GetString(1),
                                                    rdrTow.IsDBNull(2) ? 0m : Convert.ToDecimal(rdrTow.GetValue(2)),
                                                    rdrTow.IsDBNull(3) ? 0m : Convert.ToDecimal(rdrTow.GetValue(3)),
                                                    rdrTow.IsDBNull(4) ? 0m : Convert.ToDecimal(rdrTow.GetValue(4))
                                                ));
                                            }
                                            rdrTow.Close();

                                            foreach (var poz in pozycje)
                                            {
                                                using (var cmdInsTow = new SqlCommand(@"
                                                    INSERT INTO [dbo].[ReklamacjeTowary]
                                                    (IdReklamacji, IdTowaru, Symbol, Nazwa, Waga, Cena, Wartosc, PrzyczynaReklamacji)
                                                    VALUES (@IdRek, 0, @Symbol, @Nazwa, @Waga, @Cena, @Wartosc, 'Korekta')", conn))
                                                {
                                                    cmdInsTow.Parameters.AddWithValue("@IdRek", noweIdReklamacji);
                                                    cmdInsTow.Parameters.AddWithValue("@Symbol", poz.symbol);
                                                    cmdInsTow.Parameters.AddWithValue("@Nazwa", poz.nazwa);
                                                    cmdInsTow.Parameters.AddWithValue("@Waga", poz.waga);
                                                    cmdInsTow.Parameters.AddWithValue("@Cena", poz.cena);
                                                    cmdInsTow.Parameters.AddWithValue("@Wartosc", poz.wartosc);
                                                    cmdInsTow.ExecuteNonQuery();
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SyncFakturyKorygujace error: {ex.Message}");
                throw; // rethrow aby BtnDebugSync_Click mógł złapać
            }
        }

        // ========================================
        // AUTO-MATCHING (Workflow V2)
        // ========================================
        // Wolane po utworzeniu korekty z Symfonii. Szuka niepowiazanych reklamacji
        // handlowca dla tego samego kontrahenta, w oknie ±14 dni od daty korekty,
        // w statusach W_ANALIZIE lub ZASADNA. Jesli dokladnie 1 kandydat pasuje =>
        // laczymy bidirectional + zdejmujemy flage WymagaUzupelnienia.
        private void ProbujAutoMatch(SqlConnection conn, int idKorekty, int khid, DateTime dataKorekty, int? idFaktBazowej = null)
        {
            var kandydaci = new List<int>();

            // POZIOM 1: Idealne dopasowanie po fakturze bazowej (iddokkoryg z HANDEL)
            if (idFaktBazowej.HasValue && idFaktBazowej.Value > 0)
            {
                using (var cmd = new SqlCommand(@"
                    SELECT Id FROM [dbo].[Reklamacje]
                    WHERE IdDokumentu = @IdFakt
                      AND TypReklamacji <> 'Faktura korygujaca'
                      AND (PowiazanaReklamacjaId IS NULL OR PowiazanaReklamacjaId = 0)
                      AND ISNULL(StatusV2,'ZGLOSZONA') IN ('ZGLOSZONA','W_ANALIZIE','ZASADNA')", conn))
                {
                    cmd.Parameters.AddWithValue("@IdFakt", idFaktBazowej.Value);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read()) kandydaci.Add(rdr.GetInt32(0));
                    }
                }
            }

            // POZIOM 2: Fallback - khid + 14 dni (gdy brak iddokkoryg albo zero trafien)
            if (kandydaci.Count == 0)
            {
                using (var cmd = new SqlCommand(@"
                    SELECT Id
                    FROM [dbo].[Reklamacje]
                    WHERE IdKontrahenta = @Khid
                      AND TypReklamacji <> 'Faktura korygujaca'
                      AND (PowiazanaReklamacjaId IS NULL OR PowiazanaReklamacjaId = 0)
                      AND ISNULL(StatusV2, 'ZGLOSZONA') IN ('ZGLOSZONA','W_ANALIZIE','ZASADNA')
                      AND DataZgloszenia BETWEEN @DataOd AND @DataDo", conn))
                {
                    cmd.Parameters.AddWithValue("@Khid", khid);
                    cmd.Parameters.AddWithValue("@DataOd", dataKorekty.AddDays(-14));
                    cmd.Parameters.AddWithValue("@DataDo", dataKorekty.AddDays(14));
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read()) kandydaci.Add(rdr.GetInt32(0));
                    }
                }
            }

            // Tylko dokladne dopasowanie (1 kandydat) laczymy automatycznie
            if (kandydaci.Count != 1) return;

            int idRekl = kandydaci[0];
            using (var cmd = new SqlCommand(@"
                UPDATE [dbo].[Reklamacje]
                SET PowiazanaReklamacjaId = @B,
                    StatusV2 = 'POWIAZANA',
                    WymagaUzupelnienia = 0,
                    DataPowiazania = GETDATE(),
                    UserPowiazania = 'AUTO'
                WHERE Id = @A;

                UPDATE [dbo].[Reklamacje]
                SET PowiazanaReklamacjaId = @A,
                    StatusV2 = 'POWIAZANA',
                    WymagaUzupelnienia = 0,
                    DataPowiazania = GETDATE(),
                    UserPowiazania = 'AUTO'
                WHERE Id = @B;", conn))
            {
                cmd.Parameters.AddWithValue("@A", idKorekty);
                cmd.Parameters.AddWithValue("@B", idRekl);
                cmd.ExecuteNonQuery();
            }
        }

        // ============================================================
        // DENSITY TOGGLE — Compact / Normal / Spacious
        // ============================================================
        private void DensityCompact_Click(object sender, RoutedEventArgs e) => UstawDensity(28);
        private void DensityNormal_Click(object sender, RoutedEventArgs e) => UstawDensity(40);
        private void DensitySpacious_Click(object sender, RoutedEventArgs e) => UstawDensity(52);

        // ============================================================
        // BULK OPERATIONS — checkbox + floating action bar
        // ============================================================
        private void ChkBulk_Click(object sender, RoutedEventArgs e) => OdswiezBulkBar();
        private void ChkBulkAll_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is CheckBox cb)) return;
            bool zaznacz = cb.IsChecked == true;
            // Zastosuj do widocznych (przefiltrowanych) wierszy
            foreach (var item in reklamacje)
            {
                if (item.KategoriaZakladki == aktywnaZakladka)
                    item.IsBulkSelected = zaznacz;
            }
            OdswiezBulkBar();
        }
        private void BulkOdznacz_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in reklamacje) item.IsBulkSelected = false;
            OdswiezBulkBar();
        }
        private void BulkExport_Click(object sender, RoutedEventArgs e)
        {
            var zaznaczone = reklamacje.Where(r => r.IsBulkSelected).ToList();
            if (zaznaczone.Count == 0) { MessageBox.Show("Brak zaznaczonych reklamacji.", "Bulk Export", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            // Tymczasowo zamien ItemsSource na zaznaczone i wywolaj istniejacy export
            try
            {
                var saveDlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV (*.csv)|*.csv",
                    FileName = $"Reklamacje_zaznaczone_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                };
                if (saveDlg.ShowDialog() != true) return;
                using (var sw = new System.IO.StreamWriter(saveDlg.FileName, false, System.Text.Encoding.UTF8))
                {
                    sw.WriteLine("ID;Data;NumerDokumentu;Kontrahent;TypReklamacji;Priorytet;Status;Kg;Zglaszajacy;Rozpatruje;Zakonczyl");
                    foreach (var r in zaznaczone)
                    {
                        sw.WriteLine($"{r.Id};{r.DataZgloszenia:yyyy-MM-dd};{Csv(r.NumerDokumentu)};{Csv(r.NazwaKontrahenta)};{Csv(r.TypReklamacji)};{Csv(r.Priorytet)};{Csv(StatusyV2.Etykieta(r.StatusV2))};{r.SumaKg:N2};{Csv(r.Zglaszajacy)};{Csv(r.OsobaRozpatrujaca)};{Csv(r.UserZakonczenia)}");
                    }
                }
                MessageBox.Show($"Wyeksportowano {zaznaczone.Count} reklamacji do {saveDlg.FileName}", "Bulk Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { FriendlyError.Pokaz(ex, "Blad eksportu CSV.", this); }
        }
        private static string Csv(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace(";", ",").Replace("\r", " ").Replace("\n", " ");

        private void BulkZamknij_Click(object sender, RoutedEventArgs e)
        {
            var zaznaczone = reklamacje.Where(r => r.IsBulkSelected).ToList();
            if (zaznaczone.Count == 0) return;
            if (MessageBox.Show($"Zamknac {zaznaczone.Count} zaznaczonych reklamacji?\n\nStatus zmieni sie na ZAMKNIETA dla wszystkich.",
                "Bulk Zamknij", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            int ok = 0;
            foreach (var r in zaznaczone)
            {
                try
                {
                    UstawStatusV2Workflow(r.Id, StatusyV2.ZAMKNIETA, null, null, null, null, false);
                    ok++;
                }
                catch { }
            }
            MessageBox.Show($"Zamknieto {ok} z {zaznaczone.Count} reklamacji.", "Bulk Zamknij", MessageBoxButton.OK, MessageBoxImage.Information);
            WczytajReklamacje();
            WczytajStatystyki();
        }
        private void BulkPriorytet_Click(object sender, RoutedEventArgs e)
        {
            var zaznaczone = reklamacje.Where(r => r.IsBulkSelected).ToList();
            if (zaznaczone.Count == 0) return;
            // Maly dialog wyboru priorytetu
            var dlg = new Window { Title = "Zmien priorytet", Width = 320, Height = 220, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, ResizeMode = ResizeMode.NoResize };
            var sp = new StackPanel { Margin = new Thickness(20) };
            sp.Children.Add(new TextBlock { Text = $"Priorytet dla {zaznaczone.Count} reklamacji:", FontSize = 13, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 12) });
            var combo = new System.Windows.Controls.ComboBox { Margin = new Thickness(0, 0, 0, 16), FontSize = 12 };
            foreach (var p in new[] { "Niski", "Normalny", "Wysoki", "Krytyczny" }) combo.Items.Add(p);
            combo.SelectedIndex = 1;
            sp.Children.Add(combo);
            var btnOk = new System.Windows.Controls.Button { Content = "Zastosuj", Width = 100, Height = 32, HorizontalAlignment = HorizontalAlignment.Right, Background = Brushes.Green, Foreground = Brushes.White };
            btnOk.Click += (s2, e2) =>
            {
                string nowy = combo.SelectedItem?.ToString() ?? "Normalny";
                int ok = 0;
                try
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        foreach (var r in zaznaczone)
                        {
                            using (var cmd = new SqlCommand("UPDATE [dbo].[Reklamacje] SET Priorytet = @P WHERE Id = @Id", conn))
                            {
                                cmd.Parameters.AddWithValue("@P", nowy);
                                cmd.Parameters.AddWithValue("@Id", r.Id);
                                cmd.ExecuteNonQuery();
                                ok++;
                            }
                        }
                    }
                }
                catch (Exception ex) { FriendlyError.Pokaz(ex, "Blad zmiany priorytetu.", this); }
                MessageBox.Show($"Zmieniono priorytet na '{nowy}' dla {ok} reklamacji.", "Bulk Priorytet", MessageBoxButton.OK, MessageBoxImage.Information);
                dlg.Close();
                WczytajReklamacje();
            };
            sp.Children.Add(btnOk);
            dlg.Content = sp;
            dlg.ShowDialog();
        }

        private void OdswiezBulkBar()
        {
            int n = reklamacje.Count(r => r.IsBulkSelected);
            if (bulkActionBar != null)
            {
                bulkActionBar.Visibility = n > 0 ? Visibility.Visible : Visibility.Collapsed;
                if (txtBulkCount != null) txtBulkCount.Text = n.ToString();
            }
        }

        private void UstawDensity(int rowHeight)
        {
            if (dgReklamacje != null) dgReklamacje.RowHeight = rowHeight;
            // Podswietl aktywny przycisk
            var inactive = new SolidColorBrush(Colors.Transparent);
            var inactiveFg = new SolidColorBrush(Color.FromRgb(0x7F, 0x8C, 0x8D));
            var active = new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB));
            var activeFg = Brushes.White;
            if (btnDensityCompact != null) { btnDensityCompact.Background = inactive; ((TextBlock)btnDensityCompact.Content).Foreground = inactiveFg; }
            if (btnDensityNormal != null) { btnDensityNormal.Background = inactive; ((TextBlock)btnDensityNormal.Content).Foreground = inactiveFg; }
            if (btnDensitySpacious != null) { btnDensitySpacious.Background = inactive; ((TextBlock)btnDensitySpacious.Content).Foreground = inactiveFg; }
            if (rowHeight == 28 && btnDensityCompact != null) { btnDensityCompact.Background = active; ((TextBlock)btnDensityCompact.Content).Foreground = activeFg; }
            else if (rowHeight == 40 && btnDensityNormal != null) { btnDensityNormal.Background = active; ((TextBlock)btnDensityNormal.Content).Foreground = activeFg; }
            else if (rowHeight == 52 && btnDensitySpacious != null) { btnDensitySpacious.Background = active; ((TextBlock)btnDensitySpacious.Content).Foreground = activeFg; }
        }
    }

}
