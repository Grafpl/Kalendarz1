using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace Kalendarz1
{
    internal class FarmAddressDialog : Form
    {
        // Paleta (matching wizard)
        static readonly Color Navy1 = Color.FromArgb(30, 58, 95);
        static readonly Color Slate = Color.FromArgb(52, 73, 94);
        static readonly Color Gold = Color.FromArgb(212, 175, 55);
        static readonly Color AccGreen = Color.FromArgb(39, 174, 96);
        static readonly Color AccRed = Color.FromArgb(231, 76, 60);
        static readonly Color AccPurple = Color.FromArgb(142, 68, 173);
        static readonly Color LblColor = Color.FromArgb(100, 116, 139);
        static readonly Color BgPage = Color.FromArgb(241, 245, 249);
        static readonly Color CardBg = Color.White;
        static readonly Color BorderLight = Color.FromArgb(226, 232, 240);
        static readonly Color FieldFocus = Color.FromArgb(239, 246, 255);

        private TextBox edtName, edtAddress, edtPostalCode, edtCity;
        private ComboBox cbbProvince;
        private TextBox edtDistance, edtPhone1, edtInfo1, edtAnimNo, edtIRZPlus;
        private CheckBox cbHalt;
        private AnimatedButton btnOK, btnCancel;
        private ErrorProvider _errorProvider;

        private static readonly string[] Provinces =
        {
            "(brak)", "dolnoslaskie", "kujawsko-pomorskie", "lubelskie", "lubuskie",
            "lodzkie", "malopolskie", "mazowieckie", "opolskie", "podkarpackie",
            "podlaskie", "pomorskie", "slaskie", "swietokrzyskie",
            "warminsko-mazurskie", "wielkopolskie", "zachodniopomorskie"
        };

        public FarmAddressDialog(FarmAddressEntry existing = null)
        {
            Text = existing == null ? "Nowy adres fermy" : "Edycja adresu fermy";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 580);
            BackColor = BgPage;
            Font = new Font("Segoe UI", 10f);

            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            _errorProvider = new ErrorProvider(this) { BlinkStyle = ErrorBlinkStyle.NeverBlink };

            BuildUi();

            if (existing != null)
                LoadEntry(existing);

            btnOK.Click += (_, __) =>
            {
                _errorProvider.Clear();
                if (string.IsNullOrWhiteSpace(edtName.Text))
                {
                    _errorProvider.SetError(edtName, "Nazwa jest wymagana!");
                    edtName.Focus();
                    return;
                }
                DialogResult = DialogResult.OK;
            };

            btnCancel.Click += (_, __) => { DialogResult = DialogResult.Cancel; };

            KeyPreview = true;
            KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel;
            };
        }

        private void BuildUi()
        {
            // ── Header (gradient with gold accent) ──
            var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 52 };
            pnlHeader.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using var brush = new LinearGradientBrush(pnlHeader.ClientRectangle,
                    Navy1, Slate, LinearGradientMode.Horizontal);
                g.FillRectangle(brush, pnlHeader.ClientRectangle);

                // Icon
                using var iconFont = new Font("Segoe UI", 16f);
                TextRenderer.DrawText(g, "\U0001F3E0", iconFont, new Point(14, 10), Color.White);

                // Title
                using var titleFont = new Font("Segoe UI Semibold", 13f);
                TextRenderer.DrawText(g, Text, titleFont, new Point(48, 14), Color.White);

                // Gold accent line
                using var goldPen = new Pen(Gold, 2);
                g.DrawLine(goldPen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
            };
            Controls.Add(pnlHeader);

            // ── Footer ──
            var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 60 };
            pnlBottom.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderLight, 1);
                e.Graphics.DrawLine(pen, 0, 0, pnlBottom.Width, 0);
            };

            btnOK = new AnimatedButton("ZAPISZ", AccGreen) { Size = new Size(130, 38) };
            btnCancel = new AnimatedButton("ANULUJ", Color.FromArgb(148, 163, 184)) { Size = new Size(110, 38) };

            pnlBottom.Controls.Add(btnOK);
            pnlBottom.Controls.Add(btnCancel);

            pnlBottom.Resize += (_, __) =>
            {
                btnCancel.Location = new Point(pnlBottom.Width - btnCancel.Width - 18, 11);
                btnOK.Location = new Point(btnCancel.Left - btnOK.Width - 10, 11);
            };

            Controls.Add(pnlBottom);

            // ── Content area with rounded card ──
            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(20, 16, 20, 16)
            };

            var card = new Panel
            {
                Width = 500,
                Height = 440,
                Location = new Point(20, 10)
            };
            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);

                // Shadow
                using var shadowBrush = new SolidBrush(Color.FromArgb(15, 0, 0, 0));
                g.FillRoundedRectangle(shadowBrush, new Rectangle(rect.X + 2, rect.Y + 2, rect.Width, rect.Height), 8);

                // Card body
                using var cardBrush = new SolidBrush(CardBg);
                g.FillRoundedRectangle(cardBrush, rect, 8);

                // Purple top accent bar
                using var accentBrush = new SolidBrush(AccPurple);
                g.FillRoundedRectangle(accentBrush, new Rectangle(0, 0, card.Width - 1, 5), 8);
                g.FillRectangle(accentBrush, new Rectangle(0, 4, card.Width - 1, 2));

                // Border
                using var borderPen = new Pen(BorderLight, 1);
                g.DrawRoundedRectangle(borderPen, rect, 8);
            };

            // Table inside card
            var tbl = new TableLayoutPanel
            {
                Location = new Point(16, 18),
                Size = new Size(468, 410),
                ColumnCount = 2,
                RowCount = 12
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            edtName = MakeTextBox(100);
            edtAddress = MakeTextBox(100);
            edtPostalCode = MakeTextBox(10);
            edtCity = MakeTextBox(50);
            cbbProvince = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10f),
                FlatStyle = FlatStyle.Flat,
                BackColor = CardBg,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            foreach (var p in Provinces) cbbProvince.Items.Add(p);
            cbbProvince.SelectedIndex = 0;
            edtDistance = MakeTextBox(10);
            edtPhone1 = MakeTextBox(20);
            edtInfo1 = MakeTextBox(200);
            edtAnimNo = MakeTextBox(50);
            edtIRZPlus = MakeTextBox(50);
            cbHalt = new CheckBox
            {
                Text = "Wstrzymany",
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = AccRed
            };

            AttachDecimalFilter(edtDistance);
            AttachDigitsOnlyFilter(edtIRZPlus);
            AttachPhoneFilter(edtPhone1);

            int r = 0;
            AddRow(tbl, "Nazwa *", edtName, r++);
            AddRow(tbl, "Ulica", edtAddress, r++);
            AddRow(tbl, "Kod pocztowy", edtPostalCode, r++);
            AddRow(tbl, "Miejscowosc", edtCity, r++);
            AddRow(tbl, "Wojewodztwo", cbbProvince, r++);
            AddRow(tbl, "Odleglosc KM", edtDistance, r++);
            AddRow(tbl, "Telefon", edtPhone1, r++);
            AddRow(tbl, "Uwagi", edtInfo1, r++);
            AddRow(tbl, "Nr gosp.", edtAnimNo, r++);
            AddRow(tbl, "IRZPlus", edtIRZPlus, r++);
            tbl.Controls.Add(new Label(), 0, r);
            tbl.Controls.Add(cbHalt, 1, r);

            card.Controls.Add(tbl);
            scrollPanel.Controls.Add(card);

            // Resize card width with form
            scrollPanel.Resize += (_, __) =>
            {
                card.Width = Math.Max(400, scrollPanel.ClientSize.Width - 40);
                tbl.Width = card.Width - 32;
            };

            Controls.Add(scrollPanel);
        }

        private static void AddRow(TableLayoutPanel tbl, string label, Control ctrl, int row)
        {
            var lbl = new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = LblColor,
                Font = new Font("Segoe UI", 9.5f),
                Padding = new Padding(0, 7, 0, 0)
            };
            ctrl.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            tbl.Controls.Add(lbl, 0, row);
            tbl.Controls.Add(ctrl, 1, row);
        }

        private void LoadEntry(FarmAddressEntry e)
        {
            edtName.Text = e.Name;
            edtAddress.Text = e.Address;
            edtPostalCode.Text = e.PostalCode;
            edtCity.Text = e.City;
            if (e.ProvinceID >= 0 && e.ProvinceID < cbbProvince.Items.Count)
                cbbProvince.SelectedIndex = e.ProvinceID;
            edtDistance.Text = e.Distance != 0 ? e.Distance.ToString(CultureInfo.InvariantCulture) : "";
            edtPhone1.Text = e.Phone1;
            edtInfo1.Text = e.Info1;
            edtAnimNo.Text = e.AnimNo;
            edtIRZPlus.Text = e.IRZPlus;
            cbHalt.Checked = e.Halt;
        }

        public FarmAddressEntry GetEntry() => new()
        {
            Name = edtName.Text.Trim(),
            Address = edtAddress.Text.Trim(),
            PostalCode = edtPostalCode.Text.Trim(),
            City = edtCity.Text.Trim(),
            ProvinceID = cbbProvince.SelectedIndex > 0 ? cbbProvince.SelectedIndex : 0,
            Distance = decimal.TryParse(edtDistance.Text.Replace(',', '.'),
                NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0,
            Phone1 = edtPhone1.Text.Trim(),
            Info1 = edtInfo1.Text.Trim(),
            AnimNo = edtAnimNo.Text.Trim(),
            IRZPlus = edtIRZPlus.Text.Trim(),
            Halt = cbHalt.Checked
        };

        private TextBox MakeTextBox(int maxLength)
        {
            var tb = new TextBox
            {
                MaxLength = maxLength,
                Font = new Font("Segoe UI", 10f),
                BackColor = CardBg,
                BorderStyle = BorderStyle.FixedSingle
            };
            tb.Enter += (_, __) => tb.BackColor = FieldFocus;
            tb.Leave += (_, __) => { tb.BackColor = CardBg; _errorProvider.SetError(tb, ""); };
            return tb;
        }

        private static void AttachDigitsOnlyFilter(TextBox tb)
        {
            tb.KeyPress += (_, e) =>
            {
                if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar))
                    e.Handled = true;
            };
        }

        private static void AttachPhoneFilter(TextBox tb)
        {
            tb.KeyPress += (_, e) =>
            {
                if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar)
                    && e.KeyChar != '-' && e.KeyChar != '+' && e.KeyChar != ' ')
                    e.Handled = true;
            };
        }

        private static void AttachDecimalFilter(TextBox tb)
        {
            tb.KeyPress += (_, e) =>
            {
                if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar)
                    && e.KeyChar != ',' && e.KeyChar != '.')
                    e.Handled = true;
            };
        }
    }

    // Extension methods for rounded rectangles in Graphics
    internal static class GraphicsRoundedRectExtensions
    {
        public static GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle rect, int radius)
        {
            using var path = CreateRoundedRectPath(rect, radius);
            g.FillPath(brush, path);
        }

        public static void DrawRoundedRectangle(this Graphics g, Pen pen, Rectangle rect, int radius)
        {
            using var path = CreateRoundedRectPath(rect, radius);
            g.DrawPath(pen, path);
        }
    }
}
