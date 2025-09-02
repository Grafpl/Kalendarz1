using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class NewHodowcaForm : Form
    {
        private readonly string _connectionString;
        private TextBox txtName = new();
        private TextBox txtShort = new();
        private TextBox txtCity = new();
        private TextBox txtAddress = new();
        private TextBox txtPostal = new();
        private TextBox txtPhone1 = new();
        private TextBox txtPhone2 = new();
        private TextBox txtPhone3 = new();
        private TextBox txtIRZ = new();
        private TextBox txtAnimNo = new();
        private TextBox txtNip = new();
        private TextBox txtRegon = new();
        private TextBox txtPesel = new();
        private TextBox txtEmail = new();
        private TextBox txtKm = new();
        private TextBox txtDodatek = new();
        private TextBox txtUbytek = new();
        private CheckBox chkHalt = new();
        private ComboBox cmbPriceType = new();
        private readonly string? _currentUser;
        private TextBox txtProposedId = new() { ReadOnly = true };

        public string CurrentUser { get; }


        private Button btnSave = new() { Text = "Zapisz", AutoSize = true };
        private Button btnCancel = new() { Text = "Anuluj", AutoSize = true };

        public string CreatedSupplierId { get; private set; } = "";

        public NewHodowcaForm(string connectionString, string? currentUser = null)
        {
            _connectionString = connectionString;
            _currentUser = string.IsNullOrWhiteSpace(currentUser) ? Environment.UserName : currentUser;

            Text = "Nowy hodowca";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(720, 560);

            BuildUi();
            Load += async (_, __) => { await LoadPriceTypesAsync(); await SuggestNewIdAsync(); };

            btnSave.Click += async (_, __) => await SaveAsync();
            btnCancel.Click += (_, __) => DialogResult = DialogResult.Cancel;
        }

        private async System.Threading.Tasks.Task SuggestNewIdAsync()
        {
            using var con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            txtProposedId.Text = await GenerateSmallestFreeIdAsync(con, 1, 999, 3); // np. "098"
        }

        private void BuildUi()
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 14,
                Padding = new Padding(14),
                AutoScroll = true
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            Label L(string t) => new() { Text = t, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 0) };
            Control T(Control c) { c.Anchor = AnchorStyles.Left | AnchorStyles.Right; return c; }

            int r = 0;
            grid.Controls.Add(L("Nazwa*"), 0, r); grid.Controls.Add(T(txtName), 1, r);
            grid.Controls.Add(L("Skrót"), 2, r); grid.Controls.Add(T(txtShort), 3, r);
            // NOWY WIERSZ:
            grid.Controls.Add(L("Proponowane ID"), 0, ++r);
            grid.Controls.Add(txtProposedId, 1, r);


            grid.Controls.Add(L("Miasto"), 0, ++r); grid.Controls.Add(T(txtCity), 1, r);
            grid.Controls.Add(L("Adres"), 2, r); grid.Controls.Add(T(txtAddress), 3, r);

            grid.Controls.Add(L("Kod pocztowy"), 0, ++r); grid.Controls.Add(T(txtPostal), 1, r);
            grid.Controls.Add(L("Telefon 1"), 2, r); grid.Controls.Add(T(txtPhone1), 3, r);

            grid.Controls.Add(L("Telefon 2"), 0, ++r); grid.Controls.Add(T(txtPhone2), 1, r);
            grid.Controls.Add(L("Telefon 3"), 2, r); grid.Controls.Add(T(txtPhone3), 3, r);

            grid.Controls.Add(L("IRZ+"), 0, ++r); grid.Controls.Add(T(txtIRZ), 1, r);
            grid.Controls.Add(L("Numer stada"), 2, r); grid.Controls.Add(T(txtAnimNo), 3, r);

            grid.Controls.Add(L("NIP"), 0, ++r); grid.Controls.Add(T(txtNip), 1, r);
            grid.Controls.Add(L("REGON"), 2, r); grid.Controls.Add(T(txtRegon), 3, r);

            grid.Controls.Add(L("PESEL"), 0, ++r); grid.Controls.Add(T(txtPesel), 1, r);
            grid.Controls.Add(L("Email"), 2, r); grid.Controls.Add(T(txtEmail), 3, r);

            grid.Controls.Add(L("KM"), 0, ++r); grid.Controls.Add(T(txtKm), 1, r);
            grid.Controls.Add(L("Dodatek"), 2, r); grid.Controls.Add(T(txtDodatek), 3, r);

            grid.Controls.Add(L("Ubytek"), 0, ++r); grid.Controls.Add(T(txtUbytek), 1, r);
            grid.Controls.Add(L("Typ ceny"), 2, r); grid.Controls.Add(T(cmbPriceType), 3, r);

            grid.Controls.Add(L("Wstrzymany"), 0, ++r); grid.Controls.Add(chkHalt, 1, r);

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(14) };
            bottom.Controls.Add(btnSave);
            bottom.Controls.Add(btnCancel);
            StylePrimary(btnSave);
            StyleSecondary(btnCancel);
            btnSave.Text = "➕ Zapisz";
            btnCancel.Text = "✖ Anuluj";

            Controls.Add(grid);
            Controls.Add(bottom);
        }

        private async System.Threading.Tasks.Task LoadPriceTypesAsync()
        {
            const string q = @"SELECT ID, Name FROM LibraNet.dbo.PriceType ORDER BY Name;";
            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(q, con);
            await con.OpenAsync();
            using var rd = await cmd.ExecuteReaderAsync();

            var list = new List<KeyValuePair<int, string>>();
            while (await rd.ReadAsync())
                list.Add(new KeyValuePair<int, string>(rd.GetInt32(0), rd.GetString(1)));

            cmbPriceType.DisplayMember = "Value";
            cmbPriceType.ValueMember = "Key";
            cmbPriceType.DataSource = list;
            if (list.Count > 0) cmbPriceType.SelectedIndex = 0;
        }

        private (bool ok, string msg) ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
                return (false, "Pole „Nazwa” jest wymagane.");
            // Miękkie walidacje
            if (!string.IsNullOrWhiteSpace(txtNip.Text) && txtNip.Text.Length < 10)
                return (false, "NIP wygląda na zbyt krótki.");
            if (!string.IsNullOrWhiteSpace(txtEmail.Text) && !txtEmail.Text.Contains("@"))
                return (false, "Email wygląda niepoprawnie.");
            return (true, "");
        }

        private static object DbOrNull(string s) =>
            string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim();

        private static object DbOrNullDecimal(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return DBNull.Value;
            return decimal.TryParse(s.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d)
                ? d : DBNull.Value;
        }

        private static object DbOrNullInt(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return DBNull.Value;
            return int.TryParse(s, out var i) ? i : DBNull.Value;
        }

        private async System.Threading.Tasks.Task<bool> ExistsDuplicateAsync(SqlConnection con)
        {
            // duplikat po NIP/PESEL lub (Nazwa + Miasto)
            const string q = @"
SELECT TOP 1 1
FROM LibraNet.dbo.Dostawcy d
WHERE
    (NULLIF(@Nip,'') IS NOT NULL AND d.Nip = @Nip)
 OR (NULLIF(@Pesel,'') IS NOT NULL AND d.Pesel = @Pesel)
 OR (
        NULLIF(@Name,'') IS NOT NULL
    AND LOWER(LTRIM(RTRIM(d.Name))) = LOWER(LTRIM(RTRIM(@Name)))
    AND (LOWER(ISNULL(d.City,'')) = LOWER(ISNULL(@City,'')))
    );";
            using var cmd = new SqlCommand(q, con);
            cmd.Parameters.AddWithValue("@Nip", (object?)txtNip.Text?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Pesel", (object?)txtPesel.Text?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Name", (object?)txtName.Text?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@City", (object?)txtCity.Text?.Trim() ?? DBNull.Value);
            var o = await cmd.ExecuteScalarAsync();
            return o != null && o != DBNull.Value;
        }

        private async System.Threading.Tasks.Task<string> GenerateNewIdAsync(SqlConnection con)
        {
            const string q = "SELECT MAX(CAST(ID AS INT)) FROM LibraNet.dbo.Dostawcy WHERE ISNUMERIC(ID) = 1";
            using var cmd = new SqlCommand(q, con);
            var o = await cmd.ExecuteScalarAsync();
            int maxId = (o == DBNull.Value || o == null) ? 0 : Convert.ToInt32(o);
            return (maxId + 1).ToString();
        }

        private async System.Threading.Tasks.Task SaveAsync()
        {
            var validation = ValidateForm();
            if (!validation.ok)
            {
                MessageBox.Show(validation.msg, "Walidacja", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            // duplikaty
            if (await ExistsDuplicateAsync(con))
            {
                var res = MessageBox.Show(
                    "Znaleziono potencjalny duplikat (NIP/PESEL lub Nazwa+Miasto). Czy na pewno chcesz dodać?",
                    "Duplikat", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (res != DialogResult.Yes) return;
            }

            string newId = string.IsNullOrWhiteSpace(txtProposedId.Text)
                ? await GenerateSmallestFreeIdAsync(con, 1, 999, 3)
                : txtProposedId.Text.Trim();


            const string ins = @"
INSERT INTO LibraNet.dbo.Dostawcy
(
    GUID, ID,
    IsDeliverer, IsCustomer, IsRolnik, IsSkupowy,
    ShortName, Name,
    Address1, Address2,
    Nip, Halt,
    CreateData, CreateGodzina,
    ModificationData, ModificationGodzina,
    PriceTypeID, Addition, Loss,
    Address, PostalCode, City, ProvinceID, Distance,
    Phone1, Phone2, Phone3,
    Email, AnimNo, IRZPlus,
    Created, Modified,
    LastModifiedAtUTC, LastModifiedBy
)
VALUES
(
    NEWID(), @ID,
    1, 0, 0, 0,
    @Short, @Name,
    @Address1, @Address2,
    @Nip, @Halt,
    CONVERT(date, GETDATE()), CONVERT(varchar(8), GETDATE(), 108),
    CONVERT(date, GETDATE()), CONVERT(varchar(8), GETDATE(), 108),
    @PriceTypeID, @Dodatek, @Ubytek,
    @Address1, @Postal, @City, @ProvinceID, @Km,
    @Phone1, @Phone2, @Phone3,
    @Email, @AnimNo, @IRZ,
    GETDATE(), GETDATE(),
    SYSUTCDATETIME(), @User
);";

            using var cmd = new SqlCommand(ins, con);
            cmd.Parameters.AddWithValue("@ID", newId);
            cmd.Parameters.AddWithValue("@Name", txtName.Text.Trim());
            cmd.Parameters.AddWithValue("@Short", (object?)txtShort.Text?.Trim() ?? DBNull.Value);

            // Adres
            cmd.Parameters.AddWithValue("@Address1", DbOrNull(txtAddress.Text));
            cmd.Parameters.AddWithValue("@Address2", DBNull.Value);
            cmd.Parameters.AddWithValue("@Postal", DbOrNull(txtPostal.Text));
            cmd.Parameters.AddWithValue("@City", DbOrNull(txtCity.Text));
            cmd.Parameters.AddWithValue("@ProvinceID", DbOrNullInt("0"));
            cmd.Parameters.AddWithValue("@Km", DbOrNullInt(txtKm.Text));

            // Telefony
            cmd.Parameters.AddWithValue("@Phone1", DbOrNull(txtPhone1.Text));
            cmd.Parameters.AddWithValue("@Phone2", DbOrNull(txtPhone2.Text));
            cmd.Parameters.AddWithValue("@Phone3", DbOrNull(txtPhone3.Text));

            // Identyfikatory
            cmd.Parameters.AddWithValue("@Nip", DbOrNull(txtNip.Text));
            cmd.Parameters.AddWithValue("@Halt", chkHalt.Checked ? 1 : 0);

            // Ceny
            var priceTypeObj = cmbPriceType.SelectedValue;
            cmd.Parameters.AddWithValue("@PriceTypeID", priceTypeObj is int ptVal ? ptVal : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Dodatek", DbOrNullDecimal(txtDodatek.Text));
            cmd.Parameters.AddWithValue("@Ubytek", DbOrNullDecimal(txtUbytek.Text));

            // Kontakt/IRZ
            cmd.Parameters.AddWithValue("@Email", DbOrNull(txtEmail.Text));
            cmd.Parameters.AddWithValue("@AnimNo", DbOrNull(txtAnimNo.Text));
            cmd.Parameters.AddWithValue("@IRZ", DbOrNull(txtIRZ.Text));

            // Kto dodał
            cmd.Parameters.AddWithValue("@User", string.IsNullOrWhiteSpace(_currentUser) ? (object)DBNull.Value : _currentUser);

            await cmd.ExecuteNonQueryAsync();
            MessageBox.Show(
                BuildAddedSummary(newId),
                "Dodano hodowcę",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );

            CreatedSupplierId = newId;
            DialogResult = DialogResult.OK;
        }

        private static void StylePrimary(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = Color.FromArgb(0, 122, 204);
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI Semibold", 10f);
            b.Padding = new Padding(12, 6, 12, 6);
            b.Height = 36;
            b.Cursor = Cursors.Hand;
        }

        private static void StyleSecondary(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 235, 235);
            b.BackColor = Color.White;
            b.ForeColor = Color.FromArgb(60, 60, 60);
            b.Font = new Font("Segoe UI", 10f);
            b.Padding = new Padding(12, 6, 12, 6);
            b.Height = 36;
            b.Cursor = Cursors.Hand;
        }

        private static async System.Threading.Tasks.Task<string> GenerateSmallestFreeIdAsync(
     SqlConnection con, int min = 1, int max = 999, int width = 3)
        {
            const string sql = @"
;WITH N(n) AS (
    SELECT @Min
    UNION ALL
    SELECT n + 1 FROM N WHERE n < @Max
),
D AS (
    SELECT id = CAST(CASE WHEN ID NOT LIKE '%[^0-9]%' THEN ID ELSE NULL END AS int)
    FROM LibraNet.dbo.Dostawcy
)
SELECT TOP (1)
    RIGHT(REPLICATE('0', @Width) + CAST(N.n AS varchar(16)), @Width)
FROM N
LEFT JOIN D ON D.id = N.n
WHERE D.id IS NULL
ORDER BY N.n
OPTION (MAXRECURSION 0);";

            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@Min", min);
                cmd.Parameters.AddWithValue("@Max", max);
                cmd.Parameters.AddWithValue("@Width", width);
                var o = await cmd.ExecuteScalarAsync();
                if (o != null && o != DBNull.Value)
                    return (string)o;
            }

            // brak „dziur” – weź MAX+1 spośród czysto cyfrowych, z paddingiem
            const string sqlNext = @"
SELECT RIGHT(REPLICATE('0', @Width) + CAST(ISNULL(MAX(
           CAST(CASE WHEN ID NOT LIKE '%[^0-9]%' THEN ID END AS int)
       ),0) + 1 AS varchar(16)), @Width)
FROM LibraNet.dbo.Dostawcy;";

            using var cmd2 = new SqlCommand(sqlNext, con);
            cmd2.Parameters.AddWithValue("@Width", width);
            return (string)(await cmd2.ExecuteScalarAsync());
        }


        private string BuildAddedSummary(string newId)
        {
            var lines = new List<string>
    {
        $"ID: {newId}",
        $"Nazwa: {txtName.Text.Trim()}",
        $"Wstrzymany: {(chkHalt.Checked ? "tak" : "nie")}"
    };

            void Add(string label, string value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    lines.Add($"{label}: {value.Trim()}");
            }

            Add("Skrót", txtShort.Text);
            Add("Miasto", txtCity.Text);
            Add("Adres", txtAddress.Text);
            Add("Kod", txtPostal.Text);
            Add("Telefon 1", txtPhone1.Text);
            Add("Telefon 2", txtPhone2.Text);
            Add("Telefon 3", txtPhone3.Text);
            Add("Email", txtEmail.Text);
            Add("NIP", txtNip.Text);
            Add("REGON", txtRegon.Text);
            Add("PESEL", txtPesel.Text);
            Add("IRZ+", txtIRZ.Text);
            Add("Numer stada", txtAnimNo.Text);

            if (cmbPriceType.SelectedItem is KeyValuePair<int, string> kv)
                Add("Typ ceny", kv.Value);

            if (!string.IsNullOrWhiteSpace(txtKm.Text)) Add("KM", txtKm.Text);
            if (!string.IsNullOrWhiteSpace(txtDodatek.Text)) Add("Dodatek", txtDodatek.Text);
            if (!string.IsNullOrWhiteSpace(txtUbytek.Text)) Add("Ubytek", txtUbytek.Text);

            return string.Join(Environment.NewLine, lines);
        }


    }
}
