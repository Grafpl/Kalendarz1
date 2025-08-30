using System.Windows.Forms;
using System.Drawing;

namespace Kalendarz1
{
    partial class AdminChangeRequestsForm
    {
        private TableLayoutPanel root;
        private FlowLayoutPanel pasekFiltrow;
        private DataGridView dgvNaglowki;
        private TableLayoutPanel dolnyPanel;
        private DataGridView dgvPozycje;
        private FlowLayoutPanel panelPrzyciskow;

        internal ComboBox cbStatus;
        internal TextBox tbSzukaj;
        internal Button btnOdswiez, btnAkceptuj, btnOdrzuc, btnZamknij;
        internal Label lblWierszy;

        private void InitializeComponent()
        {
            this.Text = "Wnioski o zmianę – akceptacja/odmowa";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Width = 1600; this.Height = 900;

            root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            Controls.Add(root);

            // Pasek filtrów
            pasekFiltrow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };

            cbStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
            cbStatus.Items.AddRange(new object[] { "Wszystkie", "Proposed", "Zdecydowany" });
            cbStatus.SelectedIndex = 0;

            tbSzukaj = new TextBox { Width = 320, PlaceholderText = "Szukaj (ID dostawcy lub nazwa)..." };
            btnOdswiez = new Button { Text = "Odśwież", AutoSize = true };
            lblWierszy = new Label { Text = "", AutoSize = true, Padding = new Padding(12, 7, 0, 0) };

            pasekFiltrow.Controls.Add(new Label { Text = "Status:", AutoSize = true, Padding = new Padding(0, 7, 6, 0) });
            pasekFiltrow.Controls.Add(cbStatus);
            pasekFiltrow.Controls.Add(new Label { Text = " ", Width = 12 });
            pasekFiltrow.Controls.Add(tbSzukaj);
            pasekFiltrow.Controls.Add(new Label { Text = " ", Width = 12 });
            pasekFiltrow.Controls.Add(btnOdswiez);
            pasekFiltrow.Controls.Add(lblWierszy);

            root.Controls.Add(pasekFiltrow, 0, 0);

            // Grid nagłówków
            dgvNaglowki = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false
            };
            root.Controls.Add(dgvNaglowki, 0, 1);

            // Dolny panel: pozycje + przyciski
            dolnyPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            dolnyPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            dolnyPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            dgvPozycje = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false
            };
            dolnyPanel.Controls.Add(dgvPozycje, 0, 0);

            panelPrzyciskow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
            btnZamknij = new Button { Text = "Zamknij", AutoSize = true };
            btnAkceptuj = new Button { Text = "Akceptuj", AutoSize = true };
            btnOdrzuc = new Button { Text = "Odrzuć", AutoSize = true };

            panelPrzyciskow.Controls.AddRange(new Control[] { btnZamknij, btnAkceptuj, btnOdrzuc });
            dolnyPanel.Controls.Add(panelPrzyciskow, 0, 1);

            root.Controls.Add(dolnyPanel, 0, 2);
        }
    }
}
