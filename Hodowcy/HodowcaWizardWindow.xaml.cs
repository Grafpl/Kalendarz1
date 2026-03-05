using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Hodowcy
{
    public partial class HodowcaWizardWindow : Window
    {
        private readonly string _connectionString;
        private readonly string _currentUser;
        private readonly string _existingId;   // null = create mode
        private readonly bool _isEditMode;

        private int _currentStep = 0;
        private readonly UIElement[] _stepPanels;
        private readonly Border[] _sideSteps;
        private readonly Ellipse[] _sideDots;
        private readonly TextBlock[] _sideLabels;

        private static readonly string[] StepTitles = { "Dane i adres", "Cennik i firma", "ARiMR i uwagi", "Adresy ferm" };
        private static readonly string[] StepColors = { "#3B82F6", "#22C55E", "#F59E0B", "#A78BFA" };

        // Farm addresses
        private readonly ObservableCollection<FarmAddressEntry> _farmAddresses = new();

        // Edit mode state
        private Dictionary<string, string> _originalValues;
        private bool _hasRowVer;
        private byte[] _rowVer;
        private bool _loaded;
        private int _gid; // GID from Dostawcy for edit mode

        // Critical fields that require change requests
        private static readonly HashSet<string> CriticalFields = new()
        {
            "Name", "Nip", "Address", "PostalCode", "City", "ProvinceID",
            "PriceTypeID", "Addition", "Loss", "Halt",
            "Regon", "Pesel", "AnimNo", "IRZPlus", "IDCard", "IDCardDate", "IDCardAuth",
            "IncDeadConf"
        };

        private static readonly Dictionary<string, string> FieldLabels = new()
        {
            ["Name"] = "Nazwa", ["Nip"] = "NIP", ["Address"] = "Adres do faktury",
            ["PostalCode"] = "Kod pocztowy", ["City"] = "Miasto", ["ProvinceID"] = "Wojewodztwo",
            ["PriceTypeID"] = "Typ ceny", ["Addition"] = "Dodatek", ["Loss"] = "Ubytek",
            ["Halt"] = "Halt (aktywny)", ["Regon"] = "REGON", ["Pesel"] = "PESEL",
            ["AnimNo"] = "Nr gosp.", ["IRZPlus"] = "IRZPlus", ["IDCard"] = "Nr dowodu",
            ["IDCardDate"] = "Data wydania dowodu", ["IDCardAuth"] = "Wydany przez",
            ["IncDeadConf"] = "Padle+konfiskaty",
            ["ShortName"] = "Skrot", ["Phone1"] = "Telefon 1", ["Phone2"] = "Telefon 2",
            ["Phone3"] = "Telefon 3", ["Email"] = "Email", ["Trasa"] = "Trasa",
            ["Info1"] = "Info 1", ["Info2"] = "Info 2", ["Info3"] = "Info 3",
            ["Distance"] = "KM", ["TypOsobowosci"] = "Typ osob. 1", ["TypOsobowosci2"] = "Typ osob. 2",
            ["IsDeliverer"] = "Dostawca", ["IsCustomer"] = "Odbiorca",
            ["IsRolnik"] = "Rolnik", ["IsSkupowy"] = "Skupowy",
            ["IsFarmAddress"] = "Adres=ferma"
        };

        // Province list
        private static readonly string[] Provinces =
        {
            "(brak)", "dolnoslaskie", "kujawsko-pomorskie", "lubelskie", "lubuskie",
            "lodzkie", "malopolskie", "mazowieckie", "opolskie", "podkarpackie",
            "podlaskie", "pomorskie", "slaskie", "swietokrzyskie",
            "warminsko-mazurskie", "wielkopolskie", "zachodniopomorskie"
        };

        // Change request items for confirmation panel
        private List<CrChangeItem> _pendingCrItems;
        private bool _hasPendingOperationalChanges;

        public string CreatedSupplierId { get; private set; } = "";

        // ── Create mode constructor ──
        public HodowcaWizardWindow(string connString, string currentUser)
        {
            _connectionString = connString;
            _currentUser = string.IsNullOrWhiteSpace(currentUser) ? (App.UserID ?? Environment.UserName) : currentUser;
            _existingId = null;
            _isEditMode = false;

            InitializeComponent();

            _stepPanels = new UIElement[] { step0, step1, step2, step3 };
            _sideSteps = new[] { sideStep0, sideStep1, sideStep2, sideStep3 };
            _sideDots = new[] { sideDot0, sideDot1, sideDot2, sideDot3 };
            _sideLabels = new[] { sideLbl0, sideLbl1, sideLbl2, sideLbl3 };

            dgFarmAddresses.ItemsSource = _farmAddresses;
            InitProvinceCombo();
            InitTypCombo();

            Title = "Nowy hodowca / dostawca";
            tbTitle.Text = "Nowy hodowca / dostawca";
            btnSave.Content = "UTWÓRZ";
        }

        // ── Edit mode constructor ──
        public HodowcaWizardWindow(string connString, string currentUser, string existingId)
        {
            _connectionString = connString;
            _currentUser = string.IsNullOrWhiteSpace(currentUser) ? (App.UserID ?? Environment.UserName) : currentUser;
            _existingId = existingId;
            _isEditMode = true;

            InitializeComponent();

            _stepPanels = new UIElement[] { step0, step1, step2, step3 };
            _sideSteps = new[] { sideStep0, sideStep1, sideStep2, sideStep3 };
            _sideDots = new[] { sideDot0, sideDot1, sideDot2, sideDot3 };
            _sideLabels = new[] { sideLbl0, sideLbl1, sideLbl2, sideLbl3 };

            dgFarmAddresses.ItemsSource = _farmAddresses;
            InitProvinceCombo();
            InitTypCombo();

            edtID.IsReadOnly = true;
            btnSave.Content = "ZAPISZ ZMIANY";
        }

        private void InitProvinceCombo()
        {
            foreach (var p in Provinces)
                cbbProvince.Items.Add(new ComboBoxItem { Content = p });
            cbbProvince.SelectedIndex = 0;
        }

        private void InitTypCombo()
        {
            var types = new[] { "", "Analityk", "Na Cel", "Relacyjny", "Wpływowy" };
            foreach (var t in types)
            {
                cbbTyp1.Items.Add(new ComboBoxItem { Content = t });
                cbbTyp2.Items.Add(new ComboBoxItem { Content = t });
            }
            cbbTyp1.SelectedIndex = 0;
            cbbTyp2.SelectedIndex = 0;
        }

        // ══════════════════════════════════════
        //  WINDOW EVENTS
        // ══════════════════════════════════════

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ShowStep(0);
            await LoadPriceTypesAsync();

            if (_isEditMode)
            {
                await LoadDostawcaAsync();
            }
            else
            {
                await SuggestNewIdAsync();
                edtName.Focus();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.S)
            {
                e.Handled = true;
                _ = SaveAsync();
            }
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Alt && e.Key == Key.Right)
            {
                e.Handled = true;
                GoNext();
            }
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Alt && e.Key == Key.Left)
            {
                e.Handled = true;
                GoPrev();
            }
        }

        // ══════════════════════════════════════
        //  WIZARD NAVIGATION
        // ══════════════════════════════════════

        private void ShowStep(int idx)
        {
            if (idx < 0 || idx >= 4) return;
            _currentStep = idx;

            for (int i = 0; i < 4; i++)
            {
                _stepPanels[i].Visibility = i == idx ? Visibility.Visible : Visibility.Collapsed;

                var color = (Color)ColorConverter.ConvertFromString(StepColors[i]);
                if (i == idx)
                {
                    _sideSteps[i].Background = new SolidColorBrush(color);
                    _sideDots[i].Width = 10; _sideDots[i].Height = 10;
                    _sideLabels[i].Foreground = Brushes.White;
                    _sideLabels[i].FontWeight = FontWeights.SemiBold;
                }
                else
                {
                    _sideSteps[i].Background = Brushes.Transparent;
                    _sideDots[i].Width = 8; _sideDots[i].Height = 8;
                    _sideLabels[i].Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
                    _sideLabels[i].FontWeight = FontWeights.Normal;
                }
            }

            btnPrev.Visibility = idx > 0 ? Visibility.Visible : Visibility.Collapsed;
            btnNext.Visibility = idx < 3 ? Visibility.Visible : Visibility.Collapsed;

            tbStepInfo.Text = $"Krok {idx + 1} z 4 — {StepTitles[idx]}";

            // Progress bar
            double pct = (idx + 1.0) / 4.0;
            progressFill.Width = progressFill.Parent is Border parent ? parent.ActualWidth * pct : 0;
            this.SizeChanged -= ProgressResize;
            this.SizeChanged += ProgressResize;
        }

        private void ProgressResize(object sender, SizeChangedEventArgs e)
        {
            double pct = (_currentStep + 1.0) / 4.0;
            if (progressFill.Parent is Border parent)
                progressFill.Width = parent.ActualWidth * pct;
        }

        private void GoNext() { if (_currentStep < 3) ShowStep(_currentStep + 1); }
        private void GoPrev() { if (_currentStep > 0) ShowStep(_currentStep - 1); }

        private void BtnNext_Click(object sender, RoutedEventArgs e) => GoNext();
        private void BtnPrev_Click(object sender, RoutedEventArgs e) => GoPrev();

        private void SideStep0_Click(object sender, MouseButtonEventArgs e) => ShowStep(0);
        private void SideStep1_Click(object sender, MouseButtonEventArgs e) => ShowStep(1);
        private void SideStep2_Click(object sender, MouseButtonEventArgs e) => ShowStep(2);
        private void SideStep3_Click(object sender, MouseButtonEventArgs e) => ShowStep(3);

        // ══════════════════════════════════════
        //  DATA LOADING
        // ══════════════════════════════════════

        private async Task LoadPriceTypesAsync()
        {
            try
            {
                using var con = new SqlConnection(_connectionString);
                using var cmd = new SqlCommand("SELECT ID, Name FROM PriceType ORDER BY Name", con);
                await con.OpenAsync();
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                    cbbPriceType.Items.Add(new ComboBoxItem
                    {
                        Content = rd.GetString(1),
                        Tag = rd.GetInt32(0)
                    });
                if (cbbPriceType.Items.Count > 0) cbbPriceType.SelectedIndex = 0;
            }
            catch { }
        }

        private async Task SuggestNewIdAsync()
        {
            try
            {
                using var con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                edtID.Text = await GenerateSmallestFreeIdAsync(con);
            }
            catch { }
        }

        private async Task LoadDostawcaAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Check RowVer
                using (var cmdHas = new SqlCommand(
                    "SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Dostawcy') AND name = 'RowVer';", conn))
                {
                    _hasRowVer = await cmdHas.ExecuteScalarAsync() != null;
                }

                var sql = @"
SELECT GUID, GID, ID, IdSymf, IsDeliverer, IsCustomer, IsRolnik, IsSkupowy,
    ShortName, [Name], Address1, Address2, Nip, Halt, Trasa,
    PriceTypeID, Addition, Loss, [Address], PostalCode, City, ProvinceID,
    Distance, Phone1, Phone2, Phone3, Info1, Info2, Info3, Email, AnimNo, IRZPlus,
    IsFarmAddress, IncDeadConf,
    Regon, Pesel, IDCard, IDCardDate, IDCardAuth, TypOsobowosci, TypOsobowosci2
    " + (_hasRowVer ? ", RowVer" : "") + @"
FROM dbo.Dostawcy WHERE ID = @ID;";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.Add("@ID", SqlDbType.VarChar, 10).Value = _existingId;

                using var rd = await cmd.ExecuteReaderAsync();
                if (!await rd.ReadAsync())
                {
                    MessageBox.Show($"Nie znaleziono dostawcy ID={_existingId}", "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                _gid = rd.GetInt32(rd.GetOrdinal("GID"));

                // Populate fields
                edtID.Text = S(rd, "ID");
                edtName.Text = S(rd, "Name");
                edtShortName.Text = S(rd, "ShortName");
                edtAddress.Text = S(rd, "Address");
                edtPostalCode.Text = S(rd, "PostalCode");
                edtCity.Text = S(rd, "City");
                SetComboIndex(cbbProvince, I(rd, "ProvinceID"));
                edtDistance.Text = S(rd, "Distance");
                SetCheck(cbIsFarmAddress, rd["IsFarmAddress"]);
                edtPhone1.Text = S(rd, "Phone1");
                edtPhone2.Text = S(rd, "Phone2");
                edtPhone3.Text = S(rd, "Phone3");
                edtEmail.Text = S(rd, "Email");

                SetPriceType(I(rd, "PriceTypeID"));
                edtAddition.Text = S(rd, "Addition");
                edtLoss.Text = S(rd, "Loss");
                SetCheck(cbIncDeadConf, rd["IncDeadConf"]);
                edtNIP.Text = S(rd, "Nip");
                edtRegon.Text = S(rd, "Regon");
                edtPesel.Text = S(rd, "Pesel");
                edtIDCard.Text = S(rd, "IDCard");
                edtIDCardAuth.Text = S(rd, "IDCardAuth");
                if (rd["IDCardDate"] != DBNull.Value && DateTime.TryParse(Convert.ToString(rd["IDCardDate"]), out var dtCard))
                    dpIDCardDate.SelectedDate = dtCard;

                edtAnimNo.Text = S(rd, "AnimNo");
                edtIRZPlus.Text = S(rd, "IRZPlus");
                edtTrasa.Text = S(rd, "Trasa");
                edtInfo1.Text = S(rd, "Info1");
                edtInfo2.Text = S(rd, "Info2");
                edtInfo3.Text = S(rd, "Info3");

                SetComboByText(cbbTyp1, S(rd, "TypOsobowosci"));
                SetComboByText(cbbTyp2, S(rd, "TypOsobowosci2"));

                SetCheck(chkIsDeliverer, rd["IsDeliverer"]);
                SetCheck(chkIsCustomer, rd["IsCustomer"]);
                SetCheck(chkIsRolnik, rd["IsRolnik"]);
                SetCheck(chkIsSkupowy, rd["IsSkupowy"]);
                SetCheck(chkHalt, rd["Halt"]);

                if (_hasRowVer && rd["RowVer"] != DBNull.Value)
                    _rowVer = (byte[])rd["RowVer"];

                rd.Close();

                // Load farm addresses
                await LoadFarmAddressesAsync(conn);

                // Snapshot original values
                _originalValues = SnapshotCurrentValues();
                _loaded = true;

                Title = $"Edycja hodowcy — {edtName.Text} (ID: {_existingId})";
                tbTitle.Text = $"Edycja hodowcy — {edtName.Text} (ID: {_existingId})";

                // Mark critical fields visually in edit mode
                MarkCriticalFields();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private async Task LoadFarmAddressesAsync(SqlConnection conn)
        {
            _farmAddresses.Clear();
            using var cmd = new SqlCommand(
                "SELECT Name, Address, PostalCode, City, ProvinceID, Distance, Phone1, Info1, AnimNo, IRZPlus, ISNULL(Halt,0) as Halt FROM DostawcyAdresy WHERE CustomerGID=@G AND Kind=2 AND ISNULL(Deleted,0)=0",
                conn);
            cmd.Parameters.AddWithValue("@G", _gid);
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                _farmAddresses.Add(new FarmAddressEntry
                {
                    Name = S(rd, "Name"),
                    Address = S(rd, "Address"),
                    PostalCode = S(rd, "PostalCode"),
                    City = S(rd, "City"),
                    ProvinceID = I(rd, "ProvinceID"),
                    Distance = decimal.TryParse(Convert.ToString(rd["Distance"]), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0,
                    Phone1 = S(rd, "Phone1"),
                    Info1 = S(rd, "Info1"),
                    AnimNo = S(rd, "AnimNo"),
                    IRZPlus = S(rd, "IRZPlus"),
                    Halt = Convert.ToString(rd["Halt"]) is "1" or "True"
                });
            }
        }

        // ══════════════════════════════════════
        //  CRITICAL FIELD VISUAL MARKING
        // ══════════════════════════════════════

        private void MarkCriticalFields()
        {
            // Apply amber left border to critical field textboxes
            var criticalControls = new FrameworkElement[]
            {
                edtName, edtAddress, edtPostalCode, edtCity, edtNIP,
                edtRegon, edtPesel, edtAnimNo, edtIRZPlus, edtIDCard, edtIDCardAuth,
                edtAddition, edtLoss
            };

            var amberBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));

            foreach (var ctrl in criticalControls)
            {
                if (ctrl is TextBox tb)
                {
                    tb.BorderBrush = amberBrush;
                    tb.BorderThickness = new Thickness(3, 1, 1, 1);
                    tb.ToolTip = "Zmiana tego pola wymaga zatwierdzenia (change request)";
                }
            }
        }

        // ══════════════════════════════════════
        //  VALUE SNAPSHOT & COMPARISON
        // ══════════════════════════════════════

        private Dictionary<string, string> SnapshotCurrentValues()
        {
            return new Dictionary<string, string>
            {
                ["Name"] = edtName.Text,
                ["ShortName"] = edtShortName.Text,
                ["Address"] = edtAddress.Text,
                ["PostalCode"] = edtPostalCode.Text,
                ["City"] = edtCity.Text,
                ["ProvinceID"] = cbbProvince.SelectedIndex.ToString(),
                ["Distance"] = edtDistance.Text,
                ["IsFarmAddress"] = cbIsFarmAddress.IsChecked == true ? "1" : "0",
                ["Phone1"] = edtPhone1.Text,
                ["Phone2"] = edtPhone2.Text,
                ["Phone3"] = edtPhone3.Text,
                ["Email"] = edtEmail.Text,
                ["PriceTypeID"] = GetPriceTypeId().ToString(),
                ["Addition"] = edtAddition.Text,
                ["Loss"] = edtLoss.Text,
                ["IncDeadConf"] = cbIncDeadConf.IsChecked == true ? "1" : "0",
                ["Nip"] = edtNIP.Text,
                ["Regon"] = edtRegon.Text,
                ["Pesel"] = edtPesel.Text,
                ["IDCard"] = edtIDCard.Text,
                ["IDCardDate"] = dpIDCardDate.SelectedDate?.ToString("yyyy-MM-dd") ?? "",
                ["IDCardAuth"] = edtIDCardAuth.Text,
                ["AnimNo"] = edtAnimNo.Text,
                ["IRZPlus"] = edtIRZPlus.Text,
                ["Trasa"] = edtTrasa.Text,
                ["Info1"] = edtInfo1.Text,
                ["Info2"] = edtInfo2.Text,
                ["Info3"] = edtInfo3.Text,
                ["TypOsobowosci"] = GetComboText(cbbTyp1),
                ["TypOsobowosci2"] = GetComboText(cbbTyp2),
                ["IsDeliverer"] = chkIsDeliverer.IsChecked == true ? "1" : "0",
                ["IsCustomer"] = chkIsCustomer.IsChecked == true ? "1" : "0",
                ["IsRolnik"] = chkIsRolnik.IsChecked == true ? "1" : "0",
                ["IsSkupowy"] = chkIsSkupowy.IsChecked == true ? "1" : "0",
                ["Halt"] = chkHalt.IsChecked == true ? "1" : "0",
            };
        }

        private Dictionary<string, string> GetCurrentValues() => SnapshotCurrentValues();

        private (List<(string Field, string Old, string New)> critical, List<(string Field, string Old, string New)> operational) DetectChanges()
        {
            var current = GetCurrentValues();
            var critical = new List<(string, string, string)>();
            var operational = new List<(string, string, string)>();

            foreach (var kvp in current)
            {
                if (!_originalValues.TryGetValue(kvp.Key, out var old)) old = "";
                if (kvp.Value == old) continue;

                if (CriticalFields.Contains(kvp.Key))
                    critical.Add((kvp.Key, old, kvp.Value));
                else
                    operational.Add((kvp.Key, old, kvp.Value));
            }

            return (critical, operational);
        }

        // ══════════════════════════════════════
        //  VALIDATION
        // ══════════════════════════════════════

        private bool ValidateAll()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(edtName.Text)) errors.Add("Nazwa jest wymagana");
            if (string.IsNullOrWhiteSpace(edtID.Text)) errors.Add("Symbol jest wymagany");
            if (!string.IsNullOrWhiteSpace(edtAddition.Text) && !TryDec(edtAddition.Text, out _))
                errors.Add("Błędna wartość dodatku");
            if (!string.IsNullOrWhiteSpace(edtLoss.Text) && !TryDec(edtLoss.Text, out _))
                errors.Add("Błędna wartość ubytku");
            if (!string.IsNullOrWhiteSpace(edtDistance.Text) && !TryDec(edtDistance.Text, out _))
                errors.Add("Błędna wartość KM");
            if (!string.IsNullOrWhiteSpace(edtEmail.Text) && !Regex.IsMatch(edtEmail.Text.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                errors.Add("Błędny email");

            if (errors.Count > 0)
            {
                MessageBox.Show(string.Join("\n", errors), "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        // ══════════════════════════════════════
        //  SAVE
        // ══════════════════════════════════════

        private async void BtnSave_Click(object sender, RoutedEventArgs e) => await SaveAsync();

        private async Task SaveAsync()
        {
            if (!ValidateAll()) return;

            if (_isEditMode)
                await SaveEditAsync();
            else
                await SaveCreateAsync();
        }

        // ── CREATE MODE ──
        private async Task SaveCreateAsync()
        {
            btnSave.IsEnabled = false;
            try
            {
                using var con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                // Check unique ID
                using (var c = new SqlCommand("SELECT GID FROM Dostawcy WHERE ID = @ID", con))
                {
                    c.Parameters.AddWithValue("@ID", edtID.Text.Trim());
                    if (await c.ExecuteScalarAsync() is not null and not DBNull)
                    {
                        ShowStep(0);
                        edtID.Focus();
                        MessageBox.Show("Istnieje już hodowca o takim symbolu!", "Błąd",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // Warn on duplicate name
                using (var c = new SqlCommand("SELECT GID FROM Dostawcy WHERE Name = @Name", con))
                {
                    c.Parameters.AddWithValue("@Name", edtName.Text.Trim());
                    if (await c.ExecuteScalarAsync() is not null and not DBNull)
                    {
                        if (MessageBox.Show("Istnieje już hodowca o takiej nazwie.\nCzy na pewno chcesz zapisać?",
                            "Ostrzeżenie", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                            return;
                    }
                }

                using var tx = con.BeginTransaction();
                try
                {
                    const string sql = @"
INSERT INTO Dostawcy (GUID,ID,ShortName,Name,Nip,PriceTypeID,Addition,Loss,Address,PostalCode,City,ProvinceID,Distance,
    IsDeliverer,Phone1,Phone2,Phone3,Info1,Info2,Info3,IsFarmAddress,Email,AnimNo,IRZPlus,IncDeadConf,Halt,
    Regon,Pesel,IDCard,IDCardDate,IDCardAuth,Created,Modified,Trasa,TypOsobowosci,TypOsobowosci2,IsCustomer,IsRolnik,IsSkupowy)
VALUES (NEWID(),@ID,@ShortName,@Name,@Nip,@PriceTypeID,@Addition,@Loss,@Address,@PostalCode,@City,@ProvinceID,@Distance,
    @IsDeliverer,@Phone1,@Phone2,@Phone3,@Info1,@Info2,@Info3,@IsFarmAddress,@Email,@AnimNo,@IRZPlus,@IncDeadConf,@Halt,
    @Regon,@Pesel,@IDCard,@IDCardDate,@IDCardAuth,GetDate(),GetDate(),@Trasa,@Typ1,@Typ2,@IsCustomer,@IsRolnik,@IsSkupowy);
SELECT SCOPE_IDENTITY();";

                    int gid;
                    using (var c = new SqlCommand(sql, con, tx))
                    {
                        AddCreateParams(c);
                        gid = Convert.ToInt32(await c.ExecuteScalarAsync());
                    }

                    // Kind=1 auto-copy
                    const string sqlAddr = @"
INSERT INTO DostawcyAdresy (CustomerGID,Kind,Name,Address,PostalCode,City,ProvinceID,Distance,Phone1,Info1,AnimNo,IRZPlus,Halt,DefAdr,Deleted,Created,CreatedBy,Modified,ModifiedBy)
SELECT @G,1,Name,Address,PostalCode,City,ProvinceID,Distance,Phone1,Info1,AnimNo,IRZPlus,ISNULL(Halt,0),0,0,GetDate(),@U,GetDate(),@U FROM Dostawcy WHERE GID=@G;";
                    using (var c = new SqlCommand(sqlAddr, con, tx))
                    {
                        c.Parameters.AddWithValue("@G", gid);
                        c.Parameters.AddWithValue("@U", _currentUser);
                        await c.ExecuteNonQueryAsync();
                    }

                    // Kind=2 farm addresses
                    foreach (var fa in _farmAddresses.Where(a => !a.Deleted))
                    {
                        const string sqlFa = @"
INSERT INTO DostawcyAdresy (CustomerGID,Kind,Name,Address,PostalCode,City,ProvinceID,Distance,Phone1,Info1,AnimNo,IRZPlus,Halt,DefAdr,Deleted,Created,CreatedBy,Modified,ModifiedBy)
VALUES (@G,2,@N,@A,@P,@C,@Pr,@D,@Ph,@I,@An,@Ir,@H,0,0,GetDate(),@U,GetDate(),@U);";
                        using var c = new SqlCommand(sqlFa, con, tx);
                        c.Parameters.AddWithValue("@G", gid);
                        c.Parameters.AddWithValue("@N", Dbn(fa.Name));
                        c.Parameters.AddWithValue("@A", Dbn(fa.Address));
                        c.Parameters.AddWithValue("@P", Dbn(fa.PostalCode));
                        c.Parameters.AddWithValue("@C", Dbn(fa.City));
                        c.Parameters.AddWithValue("@Pr", fa.ProvinceID);
                        c.Parameters.AddWithValue("@D", (int)fa.Distance);
                        c.Parameters.AddWithValue("@Ph", Dbn(fa.Phone1));
                        c.Parameters.AddWithValue("@I", Dbn(fa.Info1));
                        c.Parameters.AddWithValue("@An", Dbn(fa.AnimNo));
                        c.Parameters.AddWithValue("@Ir", Dbn(fa.IRZPlus));
                        c.Parameters.AddWithValue("@H", fa.Halt);
                        c.Parameters.AddWithValue("@U", _currentUser);
                        await c.ExecuteNonQueryAsync();
                    }

                    tx.Commit();
                    CreatedSupplierId = edtID.Text.Trim();
                    MessageBox.Show($"Dodano: {edtName.Text.Trim()} (ID: {CreatedSupplierId})", "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                }
                catch { try { tx.Rollback(); } catch { } throw; }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { btnSave.IsEnabled = true; }
        }

        private void AddCreateParams(SqlCommand c)
        {
            c.Parameters.AddWithValue("@ID", edtID.Text.Trim());
            c.Parameters.AddWithValue("@ShortName", Dbn(edtShortName.Text));
            c.Parameters.AddWithValue("@Name", edtName.Text.Trim());
            c.Parameters.AddWithValue("@Nip", Dbn(edtNIP.Text));
            c.Parameters.AddWithValue("@PriceTypeID", GetPriceTypeId() is int pt && pt > 0 ? pt : (object)DBNull.Value);
            c.Parameters.AddWithValue("@Addition", Dec(edtAddition.Text));
            c.Parameters.AddWithValue("@Loss", Dec(edtLoss.Text));
            c.Parameters.AddWithValue("@Address", Dbn(edtAddress.Text));
            c.Parameters.AddWithValue("@PostalCode", Dbn(edtPostalCode.Text));
            c.Parameters.AddWithValue("@City", Dbn(edtCity.Text));
            c.Parameters.AddWithValue("@ProvinceID", cbbProvince.SelectedIndex > 0 ? cbbProvince.SelectedIndex : 0);
            c.Parameters.AddWithValue("@Distance", IntParse(edtDistance.Text));
            c.Parameters.AddWithValue("@IsDeliverer", chkIsDeliverer.IsChecked == true ? 1 : 0);
            c.Parameters.AddWithValue("@Phone1", Dbn(edtPhone1.Text));
            c.Parameters.AddWithValue("@Phone2", Dbn(edtPhone2.Text));
            c.Parameters.AddWithValue("@Phone3", Dbn(edtPhone3.Text));
            c.Parameters.AddWithValue("@Info1", Dbn(edtInfo1.Text));
            c.Parameters.AddWithValue("@Info2", Dbn(edtInfo2.Text));
            c.Parameters.AddWithValue("@Info3", Dbn(edtInfo3.Text));
            c.Parameters.AddWithValue("@IsFarmAddress", cbIsFarmAddress.IsChecked == true ? 1 : 0);
            c.Parameters.AddWithValue("@Email", Dbn(edtEmail.Text));
            c.Parameters.AddWithValue("@AnimNo", Dbn(edtAnimNo.Text));
            c.Parameters.AddWithValue("@IRZPlus", Dbn(edtIRZPlus.Text));
            c.Parameters.AddWithValue("@IncDeadConf", cbIncDeadConf.IsChecked == true ? 1 : 0);
            c.Parameters.AddWithValue("@Halt", chkHalt.IsChecked == true ? 1 : 0);
            c.Parameters.AddWithValue("@Regon", Dbn(edtRegon.Text));
            c.Parameters.AddWithValue("@Pesel", Dbn(edtPesel.Text));
            c.Parameters.AddWithValue("@IDCard", Dbn(edtIDCard.Text));
            c.Parameters.AddWithValue("@IDCardDate", dpIDCardDate.SelectedDate.HasValue
                ? (object)dpIDCardDate.SelectedDate.Value.ToString("yyyy-MM-dd") : DBNull.Value);
            c.Parameters.AddWithValue("@IDCardAuth", Dbn(edtIDCardAuth.Text));
            c.Parameters.AddWithValue("@Trasa", Dbn(edtTrasa.Text));
            c.Parameters.AddWithValue("@Typ1", Dbn(GetComboText(cbbTyp1)));
            c.Parameters.AddWithValue("@Typ2", Dbn(GetComboText(cbbTyp2)));
            c.Parameters.AddWithValue("@IsCustomer", chkIsCustomer.IsChecked == true ? 1 : 0);
            c.Parameters.AddWithValue("@IsRolnik", chkIsRolnik.IsChecked == true ? 1 : 0);
            c.Parameters.AddWithValue("@IsSkupowy", chkIsSkupowy.IsChecked == true ? 1 : 0);
        }

        // ── EDIT MODE ──
        private async Task SaveEditAsync()
        {
            if (!_loaded) return;

            var (critical, operational) = DetectChanges();

            if (critical.Count == 0 && operational.Count == 0)
            {
                MessageBox.Show("Brak zmian do zapisania.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // If critical changes exist, show confirmation panel
            if (critical.Count > 0)
            {
                _pendingCrItems = critical.Select(c => new CrChangeItem
                {
                    FieldName = c.Field,
                    FieldLabel = FieldLabels.TryGetValue(c.Field, out var lbl) ? lbl : c.Field,
                    OldValue = c.Old,
                    NewValue = c.New
                }).ToList();
                _hasPendingOperationalChanges = operational.Count > 0;

                dgChanges.ItemsSource = _pendingCrItems;
                dpCrEffective.SelectedDate = DateTime.Today;
                tbCrReason.Text = "";
                string loggedUserId = App.UserID ?? _currentUser;
                lblCrCreator.Text = ResolveUserName(loggedUserId) + $" (ID: {loggedUserId})";
                lblCrDate.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
                panelChangeRequest.Visibility = Visibility.Visible;
                tbCrReason.Focus();
                return;
            }

            // Only operational changes — save directly
            await SaveOperationalAsync(operational);
        }

        private async Task SaveOperationalAsync(List<(string Field, string Old, string New)> changes)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Session context for audit
                using (var setCtx = new SqlCommand(
                    "EXEC sp_set_session_context @k1,@v1; EXEC sp_set_session_context @k2,@v2;", conn))
                {
                    setCtx.Parameters.AddWithValue("@k1", "AppUserID");
                    setCtx.Parameters.AddWithValue("@v1", _currentUser);
                    setCtx.Parameters.AddWithValue("@k2", "ChangeReason");
                    setCtx.Parameters.AddWithValue("@v2", "Zmiana operacyjna z formularza hodowcy");
                    setCtx.ExecuteNonQuery();
                }

                var sql = @"
UPDATE dbo.Dostawcy SET
    ShortName = @ShortName,
    Phone1 = @Phone1, Phone2 = @Phone2, Phone3 = @Phone3,
    Email = @Email, Trasa = @Trasa,
    Info1 = @Info1, Info2 = @Info2, Info3 = @Info3,
    Distance = @Distance,
    TypOsobowosci = @Typ1, TypOsobowosci2 = @Typ2,
    IsDeliverer = @IsDeliverer, IsCustomer = @IsCustomer,
    IsRolnik = @IsRolnik, IsSkupowy = @IsSkupowy,
    IsFarmAddress = @IsFarmAddress,
    Modified = GetDate()
" + (_hasRowVer ? " WHERE ID=@ID AND RowVer=@RowVer;" : " WHERE ID=@ID;");

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.Add("@ShortName", SqlDbType.VarChar, 80).Value = Dbn(edtShortName.Text);
                cmd.Parameters.Add("@Phone1", SqlDbType.VarChar, 20).Value = Dbn(edtPhone1.Text);
                cmd.Parameters.Add("@Phone2", SqlDbType.VarChar, 20).Value = Dbn(edtPhone2.Text);
                cmd.Parameters.Add("@Phone3", SqlDbType.VarChar, 20).Value = Dbn(edtPhone3.Text);
                cmd.Parameters.Add("@Email", SqlDbType.VarChar, 128).Value = Dbn(edtEmail.Text);
                cmd.Parameters.Add("@Trasa", SqlDbType.VarChar, 4).Value = Dbn(edtTrasa.Text);
                cmd.Parameters.Add("@Info1", SqlDbType.VarChar, 40).Value = Dbn(edtInfo1.Text);
                cmd.Parameters.Add("@Info2", SqlDbType.VarChar, 40).Value = Dbn(edtInfo2.Text);
                cmd.Parameters.Add("@Info3", SqlDbType.VarChar, 40).Value = Dbn(edtInfo3.Text);
                cmd.Parameters.Add("@Distance", SqlDbType.Int).Value = int.TryParse(edtDistance.Text?.Trim(), out int km) ? km : (object)DBNull.Value;
                cmd.Parameters.Add("@Typ1", SqlDbType.VarChar, 128).Value = Dbn(GetComboText(cbbTyp1));
                cmd.Parameters.Add("@Typ2", SqlDbType.VarChar, 128).Value = Dbn(GetComboText(cbbTyp2));
                cmd.Parameters.Add("@IsDeliverer", SqlDbType.Bit).Value = chkIsDeliverer.IsChecked == true;
                cmd.Parameters.Add("@IsCustomer", SqlDbType.Bit).Value = chkIsCustomer.IsChecked == true;
                cmd.Parameters.Add("@IsRolnik", SqlDbType.Bit).Value = chkIsRolnik.IsChecked == true;
                cmd.Parameters.Add("@IsSkupowy", SqlDbType.Bit).Value = chkIsSkupowy.IsChecked == true;
                cmd.Parameters.Add("@IsFarmAddress", SqlDbType.Bit).Value = cbIsFarmAddress.IsChecked == true;
                cmd.Parameters.Add("@ID", SqlDbType.VarChar, 10).Value = _existingId;

                if (_hasRowVer)
                {
                    var p = cmd.Parameters.Add("@RowVer", SqlDbType.Timestamp);
                    p.Value = _rowVer ?? (object)DBNull.Value;
                }

                int affected = cmd.ExecuteNonQuery();
                if (_hasRowVer && affected == 0)
                {
                    MessageBox.Show("Rekord został zmieniony przez kogoś innego. Odśwież dane i spróbuj ponownie.",
                        "Konflikt współbieżności", MessageBoxButton.OK, MessageBoxImage.Warning);
                    await ReloadDataAsync();
                    return;
                }

                // Refresh RowVer
                if (_hasRowVer)
                {
                    using var r = new SqlCommand("SELECT RowVer FROM dbo.Dostawcy WHERE ID=@ID", conn);
                    r.Parameters.Add("@ID", SqlDbType.VarChar, 10).Value = _existingId;
                    _rowVer = r.ExecuteScalar() as byte[];
                }

                _originalValues = SnapshotCurrentValues();
                MessageBox.Show("Zapisano pola operacyjne.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);

                if (_isEditMode)
                {
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Change Request Panel ──

        private void BtnCrCancel_Click(object sender, RoutedEventArgs e)
        {
            panelChangeRequest.Visibility = Visibility.Collapsed;
            _pendingCrItems = null;
        }

        private async void BtnCrSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tbCrReason.Text))
            {
                MessageBox.Show("Powód jest wymagany.", "Wniosek", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Insert CR header
                long crid;
                using (var cmd = new SqlCommand(@"
INSERT INTO dbo.DostawcyCR (DostawcaID, Reason, RequestedBy, EffectiveFrom, Status)
OUTPUT INSERTED.CRID
VALUES (@ID, @Reason, @User, @Eff, 'Proposed');", conn))
                {
                    cmd.Parameters.Add("@ID", SqlDbType.VarChar, 10).Value = _existingId;
                    cmd.Parameters.Add("@Reason", SqlDbType.NVarChar, 4000).Value = tbCrReason.Text.Trim();
                    cmd.Parameters.Add("@User", SqlDbType.NVarChar, 128).Value = App.UserID ?? _currentUser;
                    cmd.Parameters.Add("@Eff", SqlDbType.Date).Value = dpCrEffective.SelectedDate ?? DateTime.Today;
                    crid = (long)cmd.ExecuteScalar();
                }

                // Insert CR items
                foreach (var item in _pendingCrItems)
                {
                    using var cmdItem = new SqlCommand(@"
INSERT INTO dbo.DostawcyCRItem (CRID, Field, OldValue, ProposedNewValue)
VALUES (@CRID, @Field, @Old, @New);", conn);
                    cmdItem.Parameters.Add("@CRID", SqlDbType.BigInt).Value = crid;
                    cmdItem.Parameters.Add("@Field", SqlDbType.NVarChar, 128).Value = item.FieldName;
                    cmdItem.Parameters.Add("@Old", SqlDbType.NVarChar, 4000).Value = (object)item.OldValue ?? DBNull.Value;
                    cmdItem.Parameters.Add("@New", SqlDbType.NVarChar, 4000).Value = item.NewValue.Trim();
                    cmdItem.ExecuteNonQuery();
                }

                // Also save operational changes if any
                if (_hasPendingOperationalChanges)
                {
                    var (_, operational) = DetectChanges();
                    if (operational.Count > 0)
                        await SaveOperationalAsync(operational);
                }

                // Revert critical fields to original values
                RevertCriticalFields();

                panelChangeRequest.Visibility = Visibility.Collapsed;
                _pendingCrItems = null;

                MessageBox.Show($"Zapisano wniosek CRID={crid}.\nPola krytyczne czekają na zatwierdzenie.",
                    "Wniosek zapisany", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu wniosku:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RevertCriticalFields()
        {
            if (_originalValues == null) return;

            // Revert critical fields to their original values (since they await approval)
            edtName.Text = _originalValues.GetValueOrDefault("Name", "");
            edtAddress.Text = _originalValues.GetValueOrDefault("Address", "");
            edtPostalCode.Text = _originalValues.GetValueOrDefault("PostalCode", "");
            edtCity.Text = _originalValues.GetValueOrDefault("City", "");
            if (int.TryParse(_originalValues.GetValueOrDefault("ProvinceID", "0"), out var provIdx))
                SetComboIndex(cbbProvince, provIdx);
            edtNIP.Text = _originalValues.GetValueOrDefault("Nip", "");
            edtRegon.Text = _originalValues.GetValueOrDefault("Regon", "");
            edtPesel.Text = _originalValues.GetValueOrDefault("Pesel", "");
            edtAnimNo.Text = _originalValues.GetValueOrDefault("AnimNo", "");
            edtIRZPlus.Text = _originalValues.GetValueOrDefault("IRZPlus", "");
            edtIDCard.Text = _originalValues.GetValueOrDefault("IDCard", "");
            edtIDCardAuth.Text = _originalValues.GetValueOrDefault("IDCardAuth", "");
            edtAddition.Text = _originalValues.GetValueOrDefault("Addition", "0");
            edtLoss.Text = _originalValues.GetValueOrDefault("Loss", "0");
            if (int.TryParse(_originalValues.GetValueOrDefault("PriceTypeID", "0"), out var ptId))
                SetPriceType(ptId);
            SetCheck(chkHalt, _originalValues.GetValueOrDefault("Halt", "0") == "1" ? "1" : "0");
            SetCheck(cbIncDeadConf, _originalValues.GetValueOrDefault("IncDeadConf", "0") == "1" ? "1" : "0");

            var dateStr = _originalValues.GetValueOrDefault("IDCardDate", "");
            dpIDCardDate.SelectedDate = DateTime.TryParse(dateStr, out var dt) ? dt : null;

            _originalValues = SnapshotCurrentValues();
        }

        private async Task ReloadDataAsync()
        {
            _loaded = false;
            await LoadDostawcaAsync();
        }

        // ══════════════════════════════════════
        //  FARM ADDRESS MANAGEMENT
        // ══════════════════════════════════════

        private void DgFarm_DoubleClick(object sender, MouseButtonEventArgs e) => EditFarmAddress();

        private void BtnFarmAdd_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new FarmAddressWpfDialog();
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                _farmAddresses.Add(dlg.GetEntry());
            }
        }

        private void BtnFarmEdit_Click(object sender, RoutedEventArgs e) => EditFarmAddress();

        private void EditFarmAddress()
        {
            if (dgFarmAddresses.SelectedItem is not FarmAddressEntry entry) return;
            var dlg = new FarmAddressWpfDialog(entry);
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                var updated = dlg.GetEntry();
                var idx = _farmAddresses.IndexOf(entry);
                if (idx >= 0) _farmAddresses[idx] = updated;
            }
        }

        private void BtnFarmDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgFarmAddresses.SelectedItem is not FarmAddressEntry entry) return;
            if (MessageBox.Show($"Usunąć adres \"{entry.Name}\"?", "Potwierdzenie",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _farmAddresses.Remove(entry);
            }
        }

        // ══════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════

        private string ResolveUserName(string userId)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();
                using var cmd = new SqlCommand("SELECT Name FROM dbo.operators WHERE ID = TRY_CAST(@u AS INT);", conn);
                cmd.Parameters.AddWithValue("@u", userId);
                var result = cmd.ExecuteScalar();
                if (result is string name && !string.IsNullOrWhiteSpace(name)) return name;
            }
            catch { }
            return userId;
        }

        private static string S(SqlDataReader rd, string col)
        {
            var ordinal = rd.GetOrdinal(col);
            return rd.IsDBNull(ordinal) ? "" : Convert.ToString(rd[col]) ?? "";
        }

        private static int I(SqlDataReader rd, string col)
        {
            var ordinal = rd.GetOrdinal(col);
            if (rd.IsDBNull(ordinal)) return 0;
            return Convert.ToInt32(rd[col]);
        }

        private static object Dbn(string s) => string.IsNullOrWhiteSpace(s) ? DBNull.Value : (object)s.Trim();

        private static bool TryDec(string s, out decimal r)
        {
            if (string.IsNullOrWhiteSpace(s)) { r = 0; return true; }
            return decimal.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out r);
        }

        private static decimal Dec(string s) => TryDec(s, out var d) ? d : 0m;

        private static int IntParse(string s) =>
            int.TryParse(s?.Replace(',', '.').Split('.')[0], out var i) ? i : 0;

        private int? GetPriceTypeId()
        {
            if (cbbPriceType.SelectedItem is ComboBoxItem item && item.Tag is int id)
                return id;
            return null;
        }

        private void SetPriceType(int id)
        {
            for (int i = 0; i < cbbPriceType.Items.Count; i++)
            {
                if (cbbPriceType.Items[i] is ComboBoxItem item && item.Tag is int tagId && tagId == id)
                {
                    cbbPriceType.SelectedIndex = i;
                    return;
                }
            }
        }

        private static string GetComboText(ComboBox cb)
        {
            if (cb.SelectedItem is ComboBoxItem item) return item.Content?.ToString() ?? "";
            return "";
        }

        private static void SetComboIndex(ComboBox cb, int idx)
        {
            if (idx >= 0 && idx < cb.Items.Count)
                cb.SelectedIndex = idx;
        }

        private static void SetComboByText(ComboBox cb, string text)
        {
            for (int i = 0; i < cb.Items.Count; i++)
            {
                if (cb.Items[i] is ComboBoxItem item && item.Content?.ToString() == text)
                {
                    cb.SelectedIndex = i;
                    return;
                }
            }
        }

        private static void SetCheck(CheckBox cb, object val)
        {
            if (val == null || val == DBNull.Value) { cb.IsChecked = false; return; }
            var s = Convert.ToString(val).Trim();
            cb.IsChecked = s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "-1";
        }

        private static async Task<string> GenerateSmallestFreeIdAsync(SqlConnection con, int min = 1, int max = 999, int width = 3)
        {
            const string sql = @";WITH N(n) AS (SELECT @Min UNION ALL SELECT n+1 FROM N WHERE n<@Max),
D AS (SELECT id=CAST(CASE WHEN ID NOT LIKE '%[^0-9]%' THEN ID ELSE NULL END AS int) FROM Dostawcy)
SELECT TOP(1) RIGHT(REPLICATE('0',@Width)+CAST(N.n AS varchar(16)),@Width) FROM N LEFT JOIN D ON D.id=N.n WHERE D.id IS NULL ORDER BY N.n OPTION(MAXRECURSION 0);";
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@Min", min);
            cmd.Parameters.AddWithValue("@Max", max);
            cmd.Parameters.AddWithValue("@Width", width);
            var o = await cmd.ExecuteScalarAsync();
            if (o != null && o != DBNull.Value) return (string)o;
            const string sql2 = @"SELECT RIGHT(REPLICATE('0',@Width)+CAST(ISNULL(MAX(CAST(CASE WHEN ID NOT LIKE '%[^0-9]%' THEN ID END AS int)),0)+1 AS varchar(16)),@Width) FROM Dostawcy;";
            using var c2 = new SqlCommand(sql2, con);
            c2.Parameters.AddWithValue("@Width", width);
            return (string)(await c2.ExecuteScalarAsync())!;
        }

        // ── Change Request item model ──
        private class CrChangeItem
        {
            public string FieldName { get; set; }
            public string FieldLabel { get; set; }
            public string OldValue { get; set; }
            public string NewValue { get; set; }
        }
    }
}
