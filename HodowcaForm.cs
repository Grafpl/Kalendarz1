using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    public partial class HodowcaForm : Form
    {
        // ---- KONFIG ----
        private readonly string _connString =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private readonly string _id;     // ID hodowcy (VARCHAR(10) w bazie)
        private readonly string _appUser;

        // ---- STAN ----
        private DataTable _priceTypes;   // słownik PriceType
        private bool _hasRowVer;         // czy tabela ma kolumnę RowVer (rowversion)
        private byte[] _rowVer;          // ostatnia wartość rowversion, jeśli jest
        private bool _loaded;

        // ---- KONTROLKI ----
        // Krytyczne (READONLY – zmiana przez "Wniosek"):
        TextBox tbName, tbNip, tbAddress, tbPostal, tbCity, tbRegon, tbPesel, tbAnimNo, tbIRZPlus, tbIDCard, tbIDCardAuth;
        DateTimePicker dtpIDCardDate;
        ComboBox cbProvince, cbPriceType;
        NumericUpDown nudAddition, nudLoss;
        CheckBox chkHalt;

        // Operacyjne:
        TextBox tbShortName, tbPhone1, tbPhone2, tbPhone3, tbEmail, tbTrasa, tbInfo1, tbInfo2, tbInfo3, tbDistance;
        ComboBox cbTyp1, cbTyp2;

        // Adres fermy (krytyczne):
        TextBox tbFarmAddress, tbFarmPostal, tbFarmCity;
        ComboBox cbFarmProvince;

        // Flagi roli:
        CheckBox chkIsDeliverer, chkIsCustomer, chkIsRolnik, chkIsSkupowy;

        // Dół formularza:
        Button btnSave, btnRequest, btnClose;

        // Mapa: kolumna -> kontrolka (dla odczytu bieżącej wartości)
        private readonly Dictionary<string, Control> _bind = new();

        // ------- KONSTRUKTOR -------
        public HodowcaForm(string idKontrahenta, string appUser)
        {
            _id = idKontrahenta;
            _appUser = string.IsNullOrWhiteSpace(appUser) ? Environment.UserName : appUser;

            Text = $"Hodowca ID={_id}";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1000, 700);

            BuildUi();
            LoadPriceTypes();
            LoadDostawca();
        }

        // ------- BUDOWA UI -------
        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(14),
                AutoScroll = true
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            Controls.Add(root);

            // Helper do etykieta+kontrolka
            Control AddLabeled(Control ctrl, string label, int col, int row, bool readOnly = false)
            {
                var panel = new Panel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(0, 4, 8, 8) };
                var lbl = new Label { Text = label, Dock = DockStyle.Top, AutoSize = true };
                ctrl.Dock = DockStyle.Top;
                if (ctrl is TextBox tb) tb.ReadOnly = readOnly;
                if (ctrl is NumericUpDown nud && readOnly) nud.Enabled = false;
                if (ctrl is ComboBox cb && readOnly) cb.Enabled = false;
                if (ctrl is DateTimePicker dtp && readOnly) dtp.Enabled = false;
                panel.Controls.Add(ctrl);
                panel.Controls.Add(lbl);
                root.Controls.Add(panel, col, row);
                return ctrl;
            }

            root.RowStyles.Clear();
            for (int i = 0; i < 16; i++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            int r = 0;

            // --- Kolumna 0: ROLA/OPERACYJNE ---
            tbShortName = new TextBox();
            AddLabeled(tbShortName, "Skrót", 0, r++);

            chkIsDeliverer = new CheckBox { Text = "IsDeliverer" };
            chkIsCustomer = new CheckBox { Text = "IsCustomer" };
            chkIsRolnik = new CheckBox { Text = "IsRolnik" };
            chkIsSkupowy = new CheckBox { Text = "IsSkupowy" };
            var rolesPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32, AutoSize = true };
            rolesPanel.Controls.AddRange(new Control[] { chkIsDeliverer, chkIsCustomer, chkIsRolnik, chkIsSkupowy });
            root.Controls.Add(rolesPanel, 0, r++);

            tbPhone1 = new TextBox(); AddLabeled(tbPhone1, "Telefon 1", 0, r++);
            tbPhone2 = new TextBox(); AddLabeled(tbPhone2, "Telefon 2", 0, r++);
            tbPhone3 = new TextBox(); AddLabeled(tbPhone3, "Telefon 3", 0, r++);
            tbEmail = new TextBox(); AddLabeled(tbEmail, "Email", 0, r++);

            tbTrasa = new TextBox(); AddLabeled(tbTrasa, "Trasa (opis)", 0, r++);
            tbInfo1 = new TextBox(); AddLabeled(tbInfo1, "Info1", 0, r++);
            tbInfo2 = new TextBox(); AddLabeled(tbInfo2, "Info2", 0, r++);
            tbInfo3 = new TextBox(); AddLabeled(tbInfo3, "Info3", 0, r++);

            cbTyp1 = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cbTyp1.Items.AddRange(new[] { "", "Analityk", "Na Cel", "Relacyjny", "Wpływowy" });
            AddLabeled(cbTyp1, "Typ osobowości 1", 0, r++);

            cbTyp2 = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cbTyp2.Items.AddRange(new[] { "", "Analityk", "Na Cel", "Relacyjny", "Wpływowy" });
            AddLabeled(cbTyp2, "Typ osobowości 2", 0, r++);

            tbDistance = new TextBox(); AddLabeled(tbDistance, "Distance (km)", 0, r++);

            // --- Kolumna 1: KRYTYCZNE DANE DO FAKTURY ---
            r = 0;
            tbName = new TextBox(); AddLabeled(tbName, "Nazwa (krytyczne)", 1, r++, readOnly: true);
            tbNip = new TextBox(); AddLabeled(tbNip, "NIP (krytyczne)", 1, r++, readOnly: true);
            tbAddress = new TextBox(); AddLabeled(tbAddress, "Adres do faktury (krytyczne)", 1, r++, readOnly: true);
            tbPostal = new TextBox(); AddLabeled(tbPostal, "Kod pocztowy (krytyczne)", 1, r++, readOnly: true);
            tbCity = new TextBox(); AddLabeled(tbCity, "Miasto (krytyczne)", 1, r++, readOnly: true);
            cbProvince = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            for (int i = 0; i <= 200; i++) cbProvince.Items.Add(i); // placeholder
            AddLabeled(cbProvince, "ProvinceID (krytyczne)", 1, r++, readOnly: true);

            // --- Kolumna 2: PARAMETRY ROZLICZEŃ + STATUS (krytyczne) ---
            r = 0;
            cbPriceType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            AddLabeled(cbPriceType, "Typ ceny (krytyczne)", 2, r++, readOnly: true);

            nudAddition = new NumericUpDown { DecimalPlaces = 4, Increment = 0.01M, Minimum = -10, Maximum = 10 };
            AddLabeled(nudAddition, "Dodatek (krytyczne)", 2, r++, readOnly: true);

            nudLoss = new NumericUpDown { DecimalPlaces = 4, Increment = 0.01M, Minimum = -10, Maximum = 10 };
            AddLabeled(nudLoss, "Ubytek (krytyczne)", 2, r++, readOnly: true);

            chkHalt = new CheckBox { Text = "Halt (krytyczne – aktywność)", Enabled = false };
            root.Controls.Add(chkHalt, 2, r++);

            // --- Kolumna 3: REJESTRY/ID + FERMA (krytyczne) ---
            r = 0;
            tbRegon = new TextBox(); AddLabeled(tbRegon, "REGON (krytyczne)", 3, r++, readOnly: true);
            tbPesel = new TextBox(); AddLabeled(tbPesel, "PESEL (krytyczne)", 3, r++, readOnly: true);
            tbAnimNo = new TextBox(); AddLabeled(tbAnimNo, "AnimNo (krytyczne)", 3, r++, readOnly: true);
            tbIRZPlus = new TextBox(); AddLabeled(tbIRZPlus, "IRZPlus (krytyczne)", 3, r++, readOnly: true);

            tbIDCard = new TextBox(); AddLabeled(tbIDCard, "IDCard (krytyczne)", 3, r++, readOnly: true);
            dtpIDCardDate = new DateTimePicker { Format = DateTimePickerFormat.Short, Enabled = false };
            AddLabeled(dtpIDCardDate, "IDCardDate (krytyczne)", 3, r++, readOnly: true);
            tbIDCardAuth = new TextBox(); AddLabeled(tbIDCardAuth, "IDCardAuth (krytyczne)", 3, r++, readOnly: true);

            tbFarmAddress = new TextBox(); AddLabeled(tbFarmAddress, "Adres fermy (krytyczne)", 3, r++, readOnly: true);
            tbFarmPostal = new TextBox(); AddLabeled(tbFarmPostal, "Kod fermy (krytyczne)", 3, r++, readOnly: true);
            tbFarmCity = new TextBox(); AddLabeled(tbFarmCity, "Miasto fermy (krytyczne)", 3, r++, readOnly: true);
            cbFarmProvince = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            for (int i = 0; i <= 200; i++) cbFarmProvince.Items.Add(i);
            AddLabeled(cbFarmProvince, "ProvinceID fermy (krytyczne)", 3, r++, readOnly: true);

            // --- Dół: przyciski ---
            var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
            btnClose = new Button { Text = "Zamknij", AutoSize = true };
            btnSave = new Button { Text = "Zapisz (operacyjne)", AutoSize = true };
            btnRequest = new Button { Text = "Wniosek o zmianę (krytyczne)", AutoSize = true };

            btnClose.Click += (_, __) => Close();
            btnSave.Click += (_, __) => SaveOperational();
            btnRequest.Click += (_, __) => OpenChangeRequestDialog();

            footer.Controls.AddRange(new Control[] { btnClose, btnSave, btnRequest });
            Controls.Add(footer);

            // Mapowanie kolumn -> kontrolek
            _bind["ShortName"] = tbShortName;
            _bind["Name"] = tbName;
            _bind["Nip"] = tbNip;
            _bind["Address"] = tbAddress;
            _bind["PostalCode"] = tbPostal;
            _bind["City"] = tbCity;
            _bind["ProvinceID"] = cbProvince;

            _bind["PriceTypeID"] = cbPriceType;
            _bind["Addition"] = nudAddition;
            _bind["Loss"] = nudLoss;
            _bind["Halt"] = chkHalt;

            _bind["Phone1"] = tbPhone1;
            _bind["Phone2"] = tbPhone2;
            _bind["Phone3"] = tbPhone3;
            _bind["Email"] = tbEmail;
            _bind["Trasa"] = tbTrasa;
            _bind["Info1"] = tbInfo1;
            _bind["Info2"] = tbInfo2;
            _bind["Info3"] = tbInfo3;
            _bind["Distance"] = tbDistance;

            _bind["TypOsobowosci"] = cbTyp1;
            _bind["TypOsobowosci2"] = cbTyp2;

            _bind["Regon"] = tbRegon;
            _bind["Pesel"] = tbPesel;
            _bind["AnimNo"] = tbAnimNo;
            _bind["IRZPlus"] = tbIRZPlus;
            _bind["IDCard"] = tbIDCard;
            _bind["IDCardDate"] = dtpIDCardDate;
            _bind["IDCardAuth"] = tbIDCardAuth;

            _bind["FarmAddress"] = tbFarmAddress;
            _bind["FarmPostalCode"] = tbFarmPostal;
            _bind["FarmCity"] = tbFarmCity;
            _bind["FarmProvinceID"] = cbFarmProvince;

            _bind["IsDeliverer"] = chkIsDeliverer;
            _bind["IsCustomer"] = chkIsCustomer;
            _bind["IsRolnik"] = chkIsRolnik;
            _bind["IsSkupowy"] = chkIsSkupowy;
        }

        // ------- SŁOWNIK TYPÓW CEN -------
        private void LoadPriceTypes()
        {
            using var conn = new SqlConnection(_connString);
            using var da = new SqlDataAdapter("SELECT ID, Name FROM dbo.PriceType ORDER BY ID", conn);
            _priceTypes = new DataTable();
            da.Fill(_priceTypes);

            cbPriceType.DisplayMember = "Name";
            cbPriceType.ValueMember = "ID";
            cbPriceType.DataSource = _priceTypes;
        }

        // ------- ŁADOWANIE DANYCH HODOWCY -------
        private void LoadDostawca()
        {
            using var conn = new SqlConnection(_connString);
            conn.Open();

            // Czy jest RowVer?
            using (var cmdHas = new SqlCommand(@"
                SELECT 1 FROM sys.columns 
                WHERE object_id = OBJECT_ID('dbo.Dostawcy') AND name = 'RowVer';", conn))
            {
                _hasRowVer = cmdHas.ExecuteScalar() != null;
            }

            var sql = @"
SELECT 
    GUID, ID, IdSymf, IsDeliverer, IsCustomer, IsRolnik, IsSkupowy,
    ShortName, [Name], Address1, Address2, Nip, Halt, Trasa,
    CreateData, CreateGodzina, ModificationData, ModificationGodzina,
    GID, PriceTypeID, Addition, Loss, [Address], PostalCode, City, ProvinceID,
    Distance, Phone1, Phone2, Phone3, Info1, Info2, Info3, Email, AnimNo, IRZPlus,
    IsHomeAddress, HomeAddress, HomePostalCode, HomeCity, HomeProvinceID,
    FarmAddress, FarmPostalCode, FarmCity, FarmProvinceID, IncDeadConf, IsFarmAddress,
    Regon, Pesel, IDCard, IDCardDate, IDCardAuth, Created, Modified, TypOsobowosci, TypOsobowosci2
    " + (_hasRowVer ? ", RowVer" : "") + @"
FROM dbo.Dostawcy
WHERE ID = @ID;";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@ID", SqlDbType.VarChar, 10).Value = _id;

            try
            {
                using var rd = cmd.ExecuteReader();
                if (!rd.Read())
                {
                    MessageBox.Show($"Nie znaleziono dostawcy ID={_id}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Close();
                    return;
                }

                // ---- Wypełnianie kontrolek ----
                SetValue(tbShortName, rd["ShortName"]);
                SetValue(tbName, rd["Name"]);
                SetValue(tbNip, rd["Nip"]);

                SetValue(tbAddress, rd["Address"]);
                SetValue(tbPostal, rd["PostalCode"]);
                SetValue(tbCity, rd["City"]);
                SetCombo(cbProvince, rd["ProvinceID"]);

                SetCombo(cbPriceType, rd["PriceTypeID"]);
                SetNumeric(nudAddition, rd["Addition"]);
                SetNumeric(nudLoss, rd["Loss"]);
                SetCheck(chkHalt, rd["Halt"]);

                SetValue(tbPhone1, rd["Phone1"]);
                SetValue(tbPhone2, rd["Phone2"]);
                SetValue(tbPhone3, rd["Phone3"]);
                SetValue(tbEmail, rd["Email"]);
                SetValue(tbTrasa, rd["Trasa"]);
                SetValue(tbInfo1, rd["Info1"]);
                SetValue(tbInfo2, rd["Info2"]);
                SetValue(tbInfo3, rd["Info3"]);
                SetValue(tbDistance, rd["Distance"]);

                SetCombo(cbTyp1, rd["TypOsobowosci"]);
                SetCombo(cbTyp2, rd["TypOsobowosci2"]);

                SetValue(tbRegon, rd["Regon"]);
                SetValue(tbPesel, rd["Pesel"]);
                SetValue(tbAnimNo, rd["AnimNo"]);
                SetValue(tbIRZPlus, rd["IRZPlus"]);

                SetValue(tbIDCard, rd["IDCard"]);
                if (rd["IDCardDate"] != DBNull.Value &&
                    DateTime.TryParse(Convert.ToString(rd["IDCardDate"]), out var dtCard))
                {
                    dtpIDCardDate.Value = dtCard;
                }
                SetValue(tbIDCardAuth, rd["IDCardAuth"]);

                SetValue(tbFarmAddress, rd["FarmAddress"]);
                SetValue(tbFarmPostal, rd["FarmPostalCode"]);
                SetValue(tbFarmCity, rd["FarmCity"]);
                SetCombo(cbFarmProvince, rd["FarmProvinceID"]);

                SetCheck(chkIsDeliverer, rd["IsDeliverer"]);
                SetCheck(chkIsCustomer, rd["IsCustomer"]);
                SetCheck(chkIsRolnik, rd["IsRolnik"]);
                SetCheck(chkIsSkupowy, rd["IsSkupowy"]);

                if (_hasRowVer && rd["RowVer"] != DBNull.Value)
                    _rowVer = (byte[])rd["RowVer"];

                _loaded = true;
            }
            catch (SqlException ex)
            {
                MessageBox.Show(
                    $"Nie udało się wczytać danych hodowcy (ID={_id}).\n\n{ex.Message}",
                    "Błąd SQL", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        // ------- ZAPIS (tylko pola operacyjne) -------
        private void SaveOperational()
        {
            if (!_loaded) return;

            // Przykładowa walidacja (kod pocztowy – i tak nie zapisujemy go tutaj)
            if (!string.IsNullOrWhiteSpace(tbPostal.Text) && !IsPostal(tbPostal.Text))
            {
                MessageBox.Show("Kod pocztowy ma zły format (np. 99-120). Pola krytyczne i tak nie zapisujemy tutaj.",
                    "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var reason = PromptReason("Podaj powód zmiany (zostanie zapisany w audycie):");
            if (string.IsNullOrWhiteSpace(reason))
            {
                MessageBox.Show("Powód jest wymagany.", "Audyt", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var conn = new SqlConnection(_connString);
            conn.Open();

            // Kontekst sesji: kto i dlaczego
            using (var setCtx = new SqlCommand(
                "EXEC sp_set_session_context @k1,@v1; EXEC sp_set_session_context @k2,@v2;", conn))
            {
                setCtx.Parameters.AddWithValue("@k1", "AppUserID");
                setCtx.Parameters.AddWithValue("@v1", _appUser);
                setCtx.Parameters.AddWithValue("@k2", "ChangeReason");
                setCtx.Parameters.AddWithValue("@v2", reason);
                setCtx.ExecuteNonQuery();
            }

            // UPDATE tylko pól operacyjnych
            var sql = @"
UPDATE dbo.Dostawcy SET
    ShortName = @ShortName,
    Phone1 = @Phone1,
    Phone2 = @Phone2,
    Phone3 = @Phone3,
    Email  = @Email,
    Trasa  = @Trasa,
    Info1  = @Info1,
    Info2  = @Info2,
    Info3  = @Info3,
    Distance = @Distance,
    TypOsobowosci = @Typ1,
    TypOsobowosci2 = @Typ2,
    IsDeliverer = @IsDeliverer,
    IsCustomer  = @IsCustomer,
    IsRolnik    = @IsRolnik,
    IsSkupowy   = @IsSkupowy
" + (_hasRowVer ? " WHERE ID=@ID AND RowVer=@RowVer;" : " WHERE ID=@ID;");

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@ShortName", SqlDbType.VarChar, 80).Value = (object)tbShortName.Text ?? DBNull.Value;
            cmd.Parameters.Add("@Phone1", SqlDbType.VarChar, 20).Value = (object)tbPhone1.Text ?? DBNull.Value;
            cmd.Parameters.Add("@Phone2", SqlDbType.VarChar, 20).Value = (object)tbPhone2.Text ?? DBNull.Value;
            cmd.Parameters.Add("@Phone3", SqlDbType.VarChar, 20).Value = (object)tbPhone3.Text ?? DBNull.Value;
            cmd.Parameters.Add("@Email", SqlDbType.VarChar, 128).Value = (object)tbEmail.Text ?? DBNull.Value;
            cmd.Parameters.Add("@Trasa", SqlDbType.VarChar, 4).Value = (object)tbTrasa.Text ?? DBNull.Value;
            cmd.Parameters.Add("@Info1", SqlDbType.VarChar, 40).Value = (object)tbInfo1.Text ?? DBNull.Value;
            cmd.Parameters.Add("@Info2", SqlDbType.VarChar, 40).Value = (object)tbInfo2.Text ?? DBNull.Value;
            cmd.Parameters.Add("@Info3", SqlDbType.VarChar, 40).Value = (object)tbInfo3.Text ?? DBNull.Value;

            // Distance (int)
            if (int.TryParse(tbDistance.Text?.Trim(), out int km))
                cmd.Parameters.Add("@Distance", SqlDbType.Int).Value = km;
            else
                cmd.Parameters.Add("@Distance", SqlDbType.Int).Value = DBNull.Value;

            cmd.Parameters.Add("@Typ1", SqlDbType.VarChar, 128).Value = (object)(cbTyp1.SelectedItem?.ToString() ?? "") ?? DBNull.Value;
            cmd.Parameters.Add("@Typ2", SqlDbType.VarChar, 128).Value = (object)(cbTyp2.SelectedItem?.ToString() ?? "") ?? DBNull.Value;

            cmd.Parameters.Add("@IsDeliverer", SqlDbType.Bit).Value = chkIsDeliverer.Checked;
            cmd.Parameters.Add("@IsCustomer", SqlDbType.Bit).Value = chkIsCustomer.Checked;
            cmd.Parameters.Add("@IsRolnik", SqlDbType.Bit).Value = chkIsRolnik.Checked;
            cmd.Parameters.Add("@IsSkupowy", SqlDbType.Bit).Value = chkIsSkupowy.Checked;

            cmd.Parameters.Add("@ID", SqlDbType.VarChar, 10).Value = _id;

            if (_hasRowVer)
            {
                var p = cmd.Parameters.Add("@RowVer", SqlDbType.Timestamp);
                p.Value = _rowVer ?? (object)DBNull.Value;
            }

            int affected = cmd.ExecuteNonQuery();
            if (_hasRowVer && affected == 0)
            {
                MessageBox.Show("Rekord został zmieniony przez kogoś innego. Odśwież dane i spróbuj ponownie.",
                    "Konflikt współbieżności", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                LoadDostawca();
                return;
            }

            // odczyt nowego RowVer (jeśli jest)
            if (_hasRowVer)
            {
                using var r = new SqlCommand("SELECT RowVer FROM dbo.Dostawcy WHERE ID=@ID", conn);
                r.Parameters.Add("@ID", SqlDbType.VarChar, 10).Value = _id;
                _rowVer = r.ExecuteScalar() as byte[];
            }

            MessageBox.Show("Zapisano pola operacyjne.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ------- WNIOSEK O ZMIANĘ (krytyczne) -------
        private void OpenChangeRequestDialog()
        {
            using var dlg = new MultiChangeRequestForm(
                _connString,
                _appUser,
                _id,
                _bind,            // kolumna -> kontrolka (żeby podstawić „OldValue”)
                GetCriticalFields()
            );

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                MessageBox.Show("Wniosek zapisany.", "Wniosek", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // Krytyczne pola do wyboru w CR
        private List<(string Label, string Column)> GetCriticalFields() => new()
        {
            ("Nazwa", "Name"),
            ("NIP", "Nip"),
            ("Adres do faktury", "Address"),
            ("Kod pocztowy", "PostalCode"),
            ("Miasto", "City"),
            ("ProvinceID", "ProvinceID"),
            ("Typ ceny", "PriceTypeID"),
            ("Dodatek", "Addition"),
            ("Ubytek", "Loss"),
            ("Halt (aktywny)", "Halt"),
            ("REGON", "Regon"),
            ("PESEL", "Pesel"),
            ("AnimNo", "AnimNo"),
            ("IRZPlus", "IRZPlus"),
            ("IDCard", "IDCard"),
            ("IDCardDate", "IDCardDate"),
            ("IDCardAuth", "IDCardAuth"),
            ("Adres fermy", "FarmAddress"),
            ("Kod fermy", "FarmPostalCode"),
            ("Miasto fermy", "FarmCity"),
            ("ProvinceID fermy", "FarmProvinceID")
        };

        // Próba utworzenia tabeli wniosków (jeśli brak)
        private void EnsureChangeRequestTable(SqlConnection conn)
        {
            const string sql = @"
IF OBJECT_ID('dbo.DostawcyChangeRequest','U') IS NULL
BEGIN
    CREATE TABLE dbo.DostawcyChangeRequest
    (
        CRID            bigint IDENTITY(1,1) PRIMARY KEY,
        DostawcaID      varchar(10)    NOT NULL,
        Field           nvarchar(128)  NOT NULL,
        OldValue        nvarchar(4000) NULL,
        ProposedNewValue nvarchar(4000) NOT NULL,
        Reason          nvarchar(4000) NULL,
        RequestedBy     nvarchar(128)  NOT NULL,
        RequestedAtUTC  datetime2(3)   NOT NULL DEFAULT SYSUTCDATETIME(),
        EffectiveFrom   date           NOT NULL,
        Status          varchar(16)    NOT NULL DEFAULT 'Proposed'
    );
END";
            using var cmd = new SqlCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }

        // Odczyt aktualnej (starej) wartości pola do wniosku
        private string GetCurrentValueForColumn(string column)
        {
            if (string.IsNullOrWhiteSpace(column)) return null;
            if (!_bind.TryGetValue(column, out var ctrl) || ctrl == null) return null;

            return ctrl switch
            {
                TextBox tb => tb.Text,
                ComboBox cb => cb.Enabled ? cb.SelectedItem?.ToString() :
                                (cb.SelectedItem?.ToString() ?? cb.Text),
                CheckBox ch => ch.Checked ? "1" : "0",
                NumericUpDown nud => nud.Value.ToString(CultureInfo.InvariantCulture),
                DateTimePicker dtp => dtp.Value.ToString("yyyy-MM-dd"),
                _ => ctrl.Text
            };
        }

        // ------- HELPERY UI <-> DB -------
        private static void SetValue(TextBox tb, object val)
            => tb.Text = val == DBNull.Value ? "" : Convert.ToString(val);

        private static void SetValue(DateTimePicker dtp, object val)
        {
            if (val == DBNull.Value) return;
            if (DateTime.TryParse(Convert.ToString(val), out var dt))
                dtp.Value = dt;
        }

        private static void SetNumeric(NumericUpDown nud, object val)
        {
            if (val == DBNull.Value) return;
            if (decimal.TryParse(Convert.ToString(val), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                nud.Value = Math.Max(nud.Minimum, Math.Min(nud.Maximum, d));
        }

        private static void SetCheck(CheckBox cb, object val)
        {
            if (val == DBNull.Value) { cb.Checked = false; return; }
            var s = Convert.ToString(val).Trim();
            cb.Checked = s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase) || s.Equals("-1");
        }

        private static void SetCombo(ComboBox cb, object val)
        {
            if (cb == null) return;
            if (val == DBNull.Value || val == null)
            {
                if (cb.Items.Count > 0) cb.SelectedIndex = 0;
                return;
            }

            if (!string.IsNullOrEmpty(cb.ValueMember))
            {
                for (int i = 0; i < cb.Items.Count; i++)
                {
                    if (cb.Items[i] is DataRowView row && Equals(row[cb.ValueMember], val))
                    {
                        cb.SelectedIndex = i;
                        return;
                    }
                }
            }

            var text = Convert.ToString(val);
            int idx = cb.FindStringExact(text);
            cb.SelectedIndex = idx >= 0 ? idx : (cb.Items.Count > 0 ? 0 : -1);
        }

        private static object ParseNullableDecimal(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return DBNull.Value;
            if (decimal.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return d;
            return DBNull.Value;
        }

        private static bool IsPostal(string s)
        {
            s = s?.Trim() ?? "";
            if (s.Length != 6) return false;
            return char.IsDigit(s[0]) && char.IsDigit(s[1]) && s[2] == '-' &&
                   char.IsDigit(s[3]) && char.IsDigit(s[4]) && char.IsDigit(s[5]);
        }

        private static string PromptReason(string title)
        {
            using var f = new Form { Width = 500, Height = 180, Text = "Powód zmiany", StartPosition = FormStartPosition.CenterParent };
            var tb = new TextBox { Dock = DockStyle.Fill, Multiline = true };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Dock = DockStyle.Right, Width = 100 };
            var cancel = new Button { Text = "Anuluj", DialogResult = DialogResult.Cancel, Dock = DockStyle.Right, Width = 100 };
            var pnl = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.RightToLeft };
            pnl.Controls.AddRange(new Control[] { ok, cancel });

            var lbl = new Label { Text = title, Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
            f.Controls.Add(tb);
            f.Controls.Add(pnl);
            f.Controls.Add(lbl);
            f.AcceptButton = ok;
            f.CancelButton = cancel;

            return f.ShowDialog() == DialogResult.OK ? tb.Text?.Trim() : null;
        }
    }

    // Proste okienko do tworzenia "Wniosku o zmianę"
    internal class ChangeRequestForm : Form
    {
        private readonly ComboBox cbField = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox tbNewVal = new() { Multiline = false, Dock = DockStyle.Top };
        private readonly DateTimePicker dtpEff = new() { Format = DateTimePickerFormat.Short, Dock = DockStyle.Top };
        private readonly TextBox tbReason = new() { Multiline = true, Height = 80, Dock = DockStyle.Top };

        public string SelectedColumn => (cbField.SelectedItem as ComboItem)?.Value;
        public string NewValue => tbNewVal.Text?.Trim();
        public DateTime EffectiveFrom => dtpEff.Value.Date;
        public string Reason => tbReason.Text?.Trim();

        private class ComboItem
        {
            public string Text { get; set; }
            public string Value { get; set; }
            public override string ToString() => Text;
        }

        public ChangeRequestForm(List<(string Label, string Column)> fields, DateTime defaultEffectiveFrom)
        {
            Text = "Wniosek o zmianę (pola krytyczne)";
            StartPosition = FormStartPosition.CenterParent;
            Width = 520; Height = 320;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 8,
                Padding = new Padding(12),
                AutoScroll = true
            };
            Controls.Add(root);

            var lblField = new Label { Text = "Pole do zmiany:", AutoSize = true, Dock = DockStyle.Top };
            var lblNew = new Label { Text = "Proponowana nowa wartość:", AutoSize = true, Dock = DockStyle.Top };
            var lblEff = new Label { Text = "Obowiązuje od (domyślnie: jutro):", AutoSize = true, Dock = DockStyle.Top };
            var lblReason = new Label { Text = "Powód zmiany (wymagany):", AutoSize = true, Dock = DockStyle.Top };

            foreach (var f in fields)
                cbField.Items.Add(new ComboItem { Text = $"{f.Label} ({f.Column})", Value = f.Column });
            if (cbField.Items.Count > 0) cbField.SelectedIndex = 0;

            dtpEff.Value = defaultEffectiveFrom;

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft };
            var btnOk = new Button { Text = "Zapisz wniosek", DialogResult = DialogResult.OK, AutoSize = true };
            var btnCancel = new Button { Text = "Anuluj", DialogResult = DialogResult.Cancel, AutoSize = true };
            buttons.Controls.AddRange(new Control[] { btnOk, btnCancel });

            root.Controls.Add(lblField);
            root.Controls.Add(cbField);
            root.Controls.Add(lblNew);
            root.Controls.Add(tbNewVal);
            root.Controls.Add(lblEff);
            root.Controls.Add(dtpEff);
            root.Controls.Add(lblReason);
            root.Controls.Add(tbReason);
            Controls.Add(buttons);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            btnOk.Click += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(SelectedColumn))
                {
                    MessageBox.Show("Wybierz pole do zmiany.", "Wniosek", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.None;
                    return;
                }
                if (string.IsNullOrWhiteSpace(NewValue))
                {
                    MessageBox.Show("Podaj nową wartość.", "Wniosek", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.None;
                    return;
                }
                if (string.IsNullOrWhiteSpace(Reason))
                {
                    MessageBox.Show("Powód jest wymagany.", "Wniosek", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.None;
                }
            };
        }
    }
}
