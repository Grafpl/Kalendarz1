using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.NotatkiZeSpotkan
{
    public partial class EdytorNotatki : Window
    {
        private readonly string _connectionString;
        private readonly string _userID;
        private readonly string _typSpotkania;
        private readonly long? _notatkaID;
        private bool _trybEdycji = false;

        private List<CheckBox> _wszystkieCheckboxyUczestnicy = new List<CheckBox>();
        private List<CheckBox> _wszystkieCheckboxyWidocznosc = new List<CheckBox>();

        private List<OperatorDTO> _wszyscyOperatorzy = new List<OperatorDTO>();
        private List<KontrahentDTO> _kontrahenci = new List<KontrahentDTO>();

        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        // Konstruktor dla NOWEJ notatki
        public EdytorNotatki(string connectionString, string userID, string typSpotkania)
        {
            _connectionString = connectionString;
            _userID = userID;
            _typSpotkania = typSpotkania;
            _notatkaID = null;
            _trybEdycji = false;

            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            InitializeNewNotatka();
        }

        // Konstruktor dla EDYCJI notatki
        public EdytorNotatki(string connectionString, string userID, long notatkaID)
        {
            _connectionString = connectionString;
            _userID = userID;
            _notatkaID = notatkaID;
            _trybEdycji = true;

            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            LoadNotatkaDoEdycji();
        }

        private void InitializeNewNotatka()
        {
            txtHeaderTitle.Text = "Nowa notatka ze spotkania";
            txtTypSpotkania.Text = _typSpotkania;
            dpDataSpotkania.SelectedDate = DateTime.Today;

            LoadOperatorzy();

            switch (_typSpotkania)
            {
                case "Zespół":
                    txtHeaderIcon.Text = "👥";
                    panelUczestnicy.Visibility = Visibility.Visible;
                    panelKontrahent.Visibility = Visibility.Collapsed;
                    GenerujCheckboksyUczestnicy();
                    break;

                case "Odbiorca":
                    txtHeaderIcon.Text = "🏢";
                    panelUczestnicy.Visibility = Visibility.Collapsed;
                    panelKontrahent.Visibility = Visibility.Visible;
                    lblKontrahent.Text = "Odbiorca: *";
                    LoadOdbiorcy();
                    break;

                case "Hodowca":
                    txtHeaderIcon.Text = "🐔";
                    panelUczestnicy.Visibility = Visibility.Collapsed;
                    panelKontrahent.Visibility = Visibility.Visible;
                    lblKontrahent.Text = "Hodowca: *";
                    LoadHodowcy();
                    break;
            }

            GenerujCheckboksyWidocznosc();
        }
        private void LoadNotatkaDoEdycji()
        {
            try
            {
                // NAJPIERW załaduj operatorów
                LoadOperatorzy();

                if (_wszyscyOperatorzy.Count == 0)
                {
                    MessageBox.Show("Nie można załadować listy użytkowników!",
                        "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string sql = @"
                        SELECT 
                            TypSpotkania, DataSpotkania, TworcaID, TworcaNazwa,
                            KontrahentID, KontrahentNazwa, KontrahentTyp,
                            Temat, TrescNotatki, OsobaKontaktowa, DodatkoweInfo
                        FROM NotatkiZeSpotkan
                        WHERE NotatkaID = @ID";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", _notatkaID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string typ = reader.GetString(0);
                                txtHeaderTitle.Text = "Edycja notatki ze spotkania";
                                txtTypSpotkania.Text = typ;
                                dpDataSpotkania.SelectedDate = reader.GetDateTime(1);
                                txtTemat.Text = reader.GetString(7);
                                txtTresc.Text = reader.GetString(8);
                                txtOsobaKontaktowa.Text = reader.IsDBNull(9) ? "" : reader.GetString(9);
                                txtDodatkoweInfo.Text = reader.IsDBNull(10) ? "" : reader.GetString(10);

                                LoadOperatorzy();

                                switch (typ)
                                {
                                    case "Zespół":
                                        txtHeaderIcon.Text = "👥";
                                        panelUczestnicy.Visibility = Visibility.Visible;
                                        panelKontrahent.Visibility = Visibility.Collapsed;
                                        GenerujCheckboksyUczestnicy();
                                        LoadUczestnicyZBazy();
                                        break;

                                    case "Odbiorca":
                                        txtHeaderIcon.Text = "🏢";
                                        panelKontrahent.Visibility = Visibility.Visible;
                                        panelUczestnicy.Visibility = Visibility.Collapsed;
                                        lblKontrahent.Text = "Odbiorca: *";
                                        LoadOdbiorcy();
                                        if (!reader.IsDBNull(4))
                                        {
                                            string kontrahentID = reader.GetString(4);
                                            cmbKontrahent.SelectedItem = _kontrahenci.FirstOrDefault(k => k.ID == kontrahentID);
                                        }
                                        break;

                                    case "Hodowca":
                                        txtHeaderIcon.Text = "🐔";
                                        panelKontrahent.Visibility = Visibility.Visible;
                                        panelUczestnicy.Visibility = Visibility.Collapsed;
                                        lblKontrahent.Text = "Hodowca: *";
                                        LoadHodowcy();
                                        if (!reader.IsDBNull(4))
                                        {
                                            string kontrahentID = reader.GetString(4);
                                            cmbKontrahent.SelectedItem = _kontrahenci.FirstOrDefault(k => k.ID == kontrahentID);
                                        }
                                        break;
                                }

                                GenerujCheckboksyWidocznosc();
                                LoadWidocznoscZBazy();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania notatki: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void LoadOperatorzy()
        {
            try
            {
                _wszyscyOperatorzy.Clear();

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    // ZMIANA - usuń WHERE ID != '11111', żeby admin też był na liście
                    string sql = "SELECT ID, Name FROM operators ORDER BY Name";

                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _wszyscyOperatorzy.Add(new OperatorDTO
                            {
                                ID = reader.GetString(0),
                                Name = reader.GetString(1)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania operatorów: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void LoadOdbiorcy()
        {
            try
            {
                _kontrahenci.Clear();

                using (var conn = new SqlConnection(_connHandel))
                {
                    conn.Open();
                    string sql = @"
                SELECT 
                    CAST(c.Id AS NVARCHAR(50)) AS Id,
                    ISNULL(c.Name, '') AS Nazwa, 
                    ISNULL(poa.Street, '') + ' ' + ISNULL(poa.PostCode, '') + ' ' + ISNULL(poa.Place, '') AS Adres,
                    '' AS Telefon
                FROM [HANDEL].[SSCommon].[STContractors] c
                LEFT JOIN [HANDEL].[SSCommon].[STPostOfficeAddresses] poa 
                    ON poa.ContactGuid = c.ContactGuid AND poa.AddressName = N'adres domyślny'
                WHERE c.Name IS NOT NULL AND c.Name != ''
                ORDER BY c.Name";

                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _kontrahenci.Add(new KontrahentDTO
                            {
                                ID = reader.GetString(0),  // Teraz to już string
                                Nazwa = reader.GetString(1),
                                Adres = reader.GetString(2),
                                Telefon = reader.GetString(3)
                            });
                        }
                    }
                }

                cmbKontrahent.ItemsSource = _kontrahenci;

                // Debug
                if (_kontrahenci.Count == 0)
                {
                    MessageBox.Show("Nie znaleziono odbiorców w bazie Handel.", "Informacja",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania odbiorców:\n\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void LoadHodowcy()
        {
            try
            {
                _kontrahenci.Clear();

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string sql = @"
                SELECT 
                    ISNULL(ID, '') AS ID,
                    ISNULL(Name, '') AS Name, 
                    ISNULL(Address, '') AS Address,
                    ISNULL(PostalCode, '') AS PostalCode,
                    ISNULL(City, '') AS City,
                    ISNULL(Phone1, '') AS Phone1
                FROM Dostawcy
                WHERE Halt = 0 
                  AND Name IS NOT NULL 
                  AND Name != ''
                  AND ID IS NOT NULL
                  AND ID != ''
                ORDER BY Name";

                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string id = reader.IsDBNull(0) ? "" : reader.GetString(0);
                            string name = reader.IsDBNull(1) ? "" : reader.GetString(1);
                            string address = reader.IsDBNull(2) ? "" : reader.GetString(2);
                            string postalCode = reader.IsDBNull(3) ? "" : reader.GetString(3);
                            string city = reader.IsDBNull(4) ? "" : reader.GetString(4);
                            string phone = reader.IsDBNull(5) ? "" : reader.GetString(5);

                            // Pomiń jeśli ID lub nazwa są puste
                            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                                continue;

                            string adres = $"{address} {postalCode} {city}".Trim();

                            _kontrahenci.Add(new KontrahentDTO
                            {
                                ID = id,
                                Nazwa = name,
                                Adres = string.IsNullOrWhiteSpace(adres) ? "" : adres,
                                Telefon = phone
                            });
                        }
                    }
                }

                cmbKontrahent.ItemsSource = _kontrahenci;

                if (_kontrahenci.Count == 0)
                {
                    MessageBox.Show("Nie znaleziono hodowców w bazie LibraNet.\n\nSprawdź tabelę Dostawcy.",
                        "Brak danych", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania hodowców:\n\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void GenerujCheckboksyUczestnicy()
        {
            stackUczestnicy.Children.Clear();
            _wszystkieCheckboxyUczestnicy.Clear();

            foreach (var op in _wszyscyOperatorzy)
            {
                var checkbox = new CheckBox
                {
                    Content = op.Name,
                    Tag = op,
                    FontSize = 13,
                    Margin = new Thickness(0, 5, 0, 5),
                    IsChecked = op.ID == _userID
                };

                _wszystkieCheckboxyUczestnicy.Add(checkbox);
                stackUczestnicy.Children.Add(checkbox);
            }
        }
        private void TxtFiltrUczestnicy_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string filtr = txtFiltrUczestnicy?.Text?.ToLower().Trim() ?? "";

            stackUczestnicy.Children.Clear();

            foreach (var checkbox in _wszystkieCheckboxyUczestnicy)
            {
                if (checkbox.Tag is OperatorDTO op)
                {
                    // Pokaż checkbox jeśli nazwa pasuje do filtra lub filtr jest pusty
                    if (string.IsNullOrEmpty(filtr) ||
                        op.Name.ToLower().Contains(filtr) ||
                        op.ID.ToLower().Contains(filtr))
                    {
                        stackUczestnicy.Children.Add(checkbox);
                    }
                }
            }
        }

        private void TxtFiltrWidocznosc_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string filtr = txtFiltrWidocznosc?.Text?.ToLower().Trim() ?? "";

            stackWidocznosc.Children.Clear();

            foreach (var checkbox in _wszystkieCheckboxyWidocznosc)
            {
                if (checkbox.Tag is OperatorDTO op)
                {
                    // Pokaż checkbox jeśli nazwa pasuje do filtra lub filtr jest pusty
                    if (string.IsNullOrEmpty(filtr) ||
                        op.Name.ToLower().Contains(filtr) ||
                        op.ID.ToLower().Contains(filtr))
                    {
                        stackWidocznosc.Children.Add(checkbox);
                    }
                }
            }
        }
        private void GenerujCheckboksyWidocznosc()
        {
            stackWidocznosc.Children.Clear();
            _wszystkieCheckboxyWidocznosc.Clear();

            foreach (var op in _wszyscyOperatorzy)
            {
                var checkbox = new CheckBox
                {
                    Content = op.Name,
                    Tag = op,
                    FontSize = 13,
                    Margin = new Thickness(0, 5, 0, 5),
                    IsChecked = false
                };

                _wszystkieCheckboxyWidocznosc.Add(checkbox);
                stackWidocznosc.Children.Add(checkbox);
            }
        }
        private void LoadUczestnicyZBazy()
        {
            try
            {
                var uczestnicyIDs = new HashSet<string>();

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string sql = "SELECT OperatorID FROM NotatkiUczestnicy WHERE NotatkaID = @ID";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", _notatkaID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                uczestnicyIDs.Add(reader.GetString(0));
                            }
                        }
                    }
                }

                // Zaznacz w oryginalnej liście
                foreach (var cb in _wszystkieCheckboxyUczestnicy)
                {
                    if (cb.Tag is OperatorDTO op)
                    {
                        cb.IsChecked = uczestnicyIDs.Contains(op.ID);
                    }
                }

                // Wymuś odświeżenie widoku (przefiltruj ponownie)
                if (txtFiltrUczestnicy != null)
                {
                    TxtFiltrUczestnicy_TextChanged(null, null);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania uczestników: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadWidocznoscZBazy()
        {
            try
            {
                var widocznoscIDs = new HashSet<string>();

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string sql = "SELECT OperatorID FROM NotatkiWidocznosc WHERE NotatkaID = @ID";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", _notatkaID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                widocznoscIDs.Add(reader.GetString(0));
                            }
                        }
                    }
                }

                // Zaznacz w oryginalnej liście
                foreach (var cb in _wszystkieCheckboxyWidocznosc)
                {
                    if (cb.Tag is OperatorDTO op)
                    {
                        cb.IsChecked = widocznoscIDs.Contains(op.ID);
                    }
                }

                // Wymuś odświeżenie widoku (przefiltruj ponownie)
                if (txtFiltrWidocznosc != null)
                {
                    TxtFiltrWidocznosc_TextChanged(null, null);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania widoczności: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CmbKontrahent_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Można tutaj dodać logikę np. automatycznego wpisywania osoby kontaktowej
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            // Walidacja
            if (!dpDataSpotkania.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz datę spotkania.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtTemat.Text))
            {
                MessageBox.Show("Podaj temat spotkania.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtTresc.Text))
            {
                MessageBox.Show("Wpisz treść notatki.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Walidacja kontrahenta dla Odbiorca/Hodowca
            string typDoSprawdzenia = _trybEdycji ? txtTypSpotkania.Text : _typSpotkania;
            if ((typDoSprawdzenia == "Odbiorca" || typDoSprawdzenia == "Hodowca") && cmbKontrahent.SelectedItem == null)
            {
                MessageBox.Show($"Wybierz {(typDoSprawdzenia == "Odbiorca" ? "odbiorcę" : "hodowcę")}.",
                    "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            long notatkaID;

                            if (_trybEdycji && _notatkaID.HasValue)
                            {
                                // ============================================
                                // AKTUALIZACJA ISTNIEJĄCEJ NOTATKI
                                // ============================================
                                notatkaID = _notatkaID.Value;

                                string sqlUpdate = @"
                            UPDATE NotatkiZeSpotkan SET
                                DataSpotkania = @DataSpotkania,
                                DataModyfikacji = GETDATE(),
                                KontrahentID = @KontrahentID,
                                KontrahentNazwa = @KontrahentNazwa,
                                KontrahentTyp = @KontrahentTyp,
                                Temat = @Temat,
                                TrescNotatki = @TrescNotatki,
                                OsobaKontaktowa = @OsobaKontaktowa,
                                DodatkoweInfo = @DodatkoweInfo
                            WHERE NotatkaID = @NotatkaID";

                                using (var cmd = new SqlCommand(sqlUpdate, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@NotatkaID", notatkaID);
                                    cmd.Parameters.AddWithValue("@DataSpotkania", dpDataSpotkania.SelectedDate.Value);

                                    var kontrahent = cmbKontrahent.SelectedItem as KontrahentDTO;
                                    cmd.Parameters.AddWithValue("@KontrahentID", (object?)kontrahent?.ID ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@KontrahentNazwa", (object?)kontrahent?.Nazwa ?? DBNull.Value);

                                    string typSpotkania = txtTypSpotkania.Text;
                                    cmd.Parameters.AddWithValue("@KontrahentTyp",
                                        (typSpotkania == "Odbiorca" || typSpotkania == "Hodowca") ? (object)typSpotkania : DBNull.Value);

                                    cmd.Parameters.AddWithValue("@Temat", txtTemat.Text.Trim());
                                    cmd.Parameters.AddWithValue("@TrescNotatki", txtTresc.Text.Trim());
                                    cmd.Parameters.AddWithValue("@OsobaKontaktowa",
                                        string.IsNullOrWhiteSpace(txtOsobaKontaktowa.Text) ? DBNull.Value : txtOsobaKontaktowa.Text.Trim());
                                    cmd.Parameters.AddWithValue("@DodatkoweInfo",
                                        string.IsNullOrWhiteSpace(txtDodatkoweInfo.Text) ? DBNull.Value : txtDodatkoweInfo.Text.Trim());

                                    cmd.ExecuteNonQuery();
                                }

                                // Usuń stare powiązania (uczestników i widoczność)
                                using (var cmdDel1 = new SqlCommand("DELETE FROM NotatkiUczestnicy WHERE NotatkaID = @ID", conn, transaction))
                                {
                                    cmdDel1.Parameters.AddWithValue("@ID", notatkaID);
                                    cmdDel1.ExecuteNonQuery();
                                }

                                using (var cmdDel2 = new SqlCommand("DELETE FROM NotatkiWidocznosc WHERE NotatkaID = @ID", conn, transaction))
                                {
                                    cmdDel2.Parameters.AddWithValue("@ID", notatkaID);
                                    cmdDel2.ExecuteNonQuery();
                                }
                            }
                            else
                            {
                                // ============================================
                                // TWORZENIE NOWEJ NOTATKI
                                // ============================================
                                string sqlInsert = @"
                            INSERT INTO NotatkiZeSpotkan 
                            (TypSpotkania, DataSpotkania, TworcaID, 
                             KontrahentID, KontrahentNazwa, KontrahentTyp,
                             Temat, TrescNotatki, OsobaKontaktowa, DodatkoweInfo)
                            VALUES 
                            (@TypSpotkania, @DataSpotkania, @TworcaID,
                             @KontrahentID, @KontrahentNazwa, @KontrahentTyp,
                             @Temat, @TrescNotatki, @OsobaKontaktowa, @DodatkoweInfo);
                            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

                                using (var cmd = new SqlCommand(sqlInsert, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@TypSpotkania", _typSpotkania);
                                    cmd.Parameters.AddWithValue("@DataSpotkania", dpDataSpotkania.SelectedDate.Value);
                                    cmd.Parameters.AddWithValue("@TworcaID", _userID);

                                    var kontrahent = cmbKontrahent.SelectedItem as KontrahentDTO;
                                    cmd.Parameters.AddWithValue("@KontrahentID", (object?)kontrahent?.ID ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@KontrahentNazwa", (object?)kontrahent?.Nazwa ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@KontrahentTyp",
                                        (_typSpotkania == "Odbiorca" || _typSpotkania == "Hodowca") ? (object)_typSpotkania : DBNull.Value);

                                    cmd.Parameters.AddWithValue("@Temat", txtTemat.Text.Trim());
                                    cmd.Parameters.AddWithValue("@TrescNotatki", txtTresc.Text.Trim());
                                    cmd.Parameters.AddWithValue("@OsobaKontaktowa",
                                        string.IsNullOrWhiteSpace(txtOsobaKontaktowa.Text) ? DBNull.Value : txtOsobaKontaktowa.Text.Trim());
                                    cmd.Parameters.AddWithValue("@DodatkoweInfo",
                                        string.IsNullOrWhiteSpace(txtDodatkoweInfo.Text) ? DBNull.Value : txtDodatkoweInfo.Text.Trim());

                                    notatkaID = (long)cmd.ExecuteScalar()!;
                                }
                            }

                            // ============================================
                            // ZAPISZ UCZESTNIKÓW (tylko dla spotkań zespołowych)
                            // ============================================
                            string aktualnyTyp = _trybEdycji ? txtTypSpotkania.Text : _typSpotkania;
                            if (aktualnyTyp == "Zespół")
                            {
                                // WAŻNE: iteruj po oryginalnej liście, nie po stackUczestnicy.Children
                                foreach (CheckBox cb in _wszystkieCheckboxyUczestnicy)
                                {
                                    if (cb.IsChecked == true && cb.Tag is OperatorDTO op)
                                    {
                                        string sqlUczestnik = @"
                                    INSERT INTO NotatkiUczestnicy (NotatkaID, OperatorID)
                                    VALUES (@NotatkaID, @OperatorID)";

                                        using (var cmd = new SqlCommand(sqlUczestnik, conn, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("@NotatkaID", notatkaID);
                                            cmd.Parameters.AddWithValue("@OperatorID", op.ID);
                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }

                            // ============================================
                            // ZAPISZ WIDOCZNOŚĆ (zawsze dodaj twórcę)
                            // ============================================
                            var widoczneIDs = new HashSet<string> { _userID };

                            // WAŻNE: iteruj po oryginalnej liście, nie po stackWidocznosc.Children
                            foreach (CheckBox cb in _wszystkieCheckboxyWidocznosc)
                            {
                                if (cb.IsChecked == true && cb.Tag is OperatorDTO op)
                                {
                                    widoczneIDs.Add(op.ID);
                                }
                            }

                            foreach (var operatorID in widoczneIDs)
                            {
                                string sqlWidocznosc = @"
                            INSERT INTO NotatkiWidocznosc (NotatkaID, OperatorID)
                            VALUES (@NotatkaID, @OperatorID)";

                                using (var cmd = new SqlCommand(sqlWidocznosc, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@NotatkaID", notatkaID);
                                    cmd.Parameters.AddWithValue("@OperatorID", operatorID);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            // ============================================
                            // COMMIT TRANSAKCJI
                            // ============================================
                            transaction.Commit();

                            MessageBox.Show("Notatka została zapisana pomyślnie!", "Sukces",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            DialogResult = true;
                            Close();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu:\n\n{ex.Message}\n\nSzczegóły:\n{ex.StackTrace}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}