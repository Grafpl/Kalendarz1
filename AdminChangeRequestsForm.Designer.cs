#nullable enable

using System.Windows.Forms;
using System.Drawing;

namespace Kalendarz1
{
    partial class AdminChangeRequestsForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private TableLayoutPanel root;
        private FlowLayoutPanel pasekFiltrow;
        private DataGridView dgvNaglowki;
        private TableLayoutPanel dolnyPanel;
        private DataGridView dgvPozycje;
        private FlowLayoutPanel panelPrzyciskowGlobalnych;
        private GroupBox panelAkcji;
        internal ComboBox cbStatus;
        internal TextBox tbSzukaj;
        internal Button btnOdswiez, btnAkceptuj, btnOdrzuc, btnZamknij, btnZapiszNotatke;
        internal Label lblWierszy;
        internal TextBox txtNotatka;
        private TableLayoutPanel layoutAkcji;
        private FlowLayoutPanel panelDecyzji;

        private void InitializeComponent()
        {
            root = new TableLayoutPanel();
            pasekFiltrow = new FlowLayoutPanel();
            cbStatus = new ComboBox();
            tbSzukaj = new TextBox();
            btnOdswiez = new Button();
            lblWierszy = new Label();
            dgvNaglowki = new DataGridView();
            dolnyPanel = new TableLayoutPanel();
            dgvPozycje = new DataGridView();
            panelAkcji = new GroupBox();
            layoutAkcji = new TableLayoutPanel();
            txtNotatka = new TextBox();
            btnZapiszNotatke = new Button();
            panelDecyzji = new FlowLayoutPanel();
            btnAkceptuj = new Button();
            btnOdrzuc = new Button();
            panelPrzyciskowGlobalnych = new FlowLayoutPanel();
            btnZamknij = new Button();
            root.SuspendLayout();
            pasekFiltrow.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvNaglowki).BeginInit();
            dolnyPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvPozycje).BeginInit();
            panelAkcji.SuspendLayout();
            layoutAkcji.SuspendLayout();
            panelDecyzji.SuspendLayout();
            panelPrzyciskowGlobalnych.SuspendLayout();
            SuspendLayout();
            // 
            // root
            // 
            root.ColumnCount = 1;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.Controls.Add(pasekFiltrow, 0, 0);
            root.Controls.Add(dgvNaglowki, 0, 1);
            root.Controls.Add(dolnyPanel, 0, 2);
            root.Controls.Add(panelPrzyciskowGlobalnych, 0, 3);
            root.Dock = DockStyle.Fill;
            root.Location = new Point(0, 0);
            root.Name = "root";
            root.Padding = new Padding(10);
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            root.Size = new Size(1584, 861);
            root.TabIndex = 0;
            // 
            // pasekFiltrow
            // 
            pasekFiltrow.Controls.Add(new Label());
            pasekFiltrow.Controls.Add(cbStatus);
            pasekFiltrow.Controls.Add(new Label());
            pasekFiltrow.Controls.Add(tbSzukaj);
            pasekFiltrow.Controls.Add(btnOdswiez);
            pasekFiltrow.Controls.Add(lblWierszy);
            pasekFiltrow.Dock = DockStyle.Fill;
            pasekFiltrow.Location = new Point(13, 13);
            pasekFiltrow.Name = "pasekFiltrow";
            pasekFiltrow.Size = new Size(1558, 54);
            pasekFiltrow.TabIndex = 0;
            // 
            // cbStatus
            // 
            cbStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            cbStatus.FormattingEnabled = true;
            cbStatus.Items.AddRange(new object[] { "Wszystkie", "Proposed", "Zdecydowany" });
            cbStatus.Location = new Point(3, 3);
            cbStatus.Name = "cbStatus";
            cbStatus.Size = new Size(180, 23);
            cbStatus.TabIndex = 1;
            cbStatus.SelectedIndex = 1;
            // 
            // tbSzukaj
            // 
            tbSzukaj.Location = new Point(189, 3);
            tbSzukaj.Name = "tbSzukaj";
            tbSzukaj.Size = new Size(250, 23);
            tbSzukaj.TabIndex = 3;
            tbSzukaj.PlaceholderText = "Wpisz ID lub nazwę dostawcy...";
            // 
            // btnOdswiez
            // 
            btnOdswiez.Location = new Point(445, 3);
            btnOdswiez.Name = "btnOdswiez";
            btnOdswiez.Size = new Size(75, 23);
            btnOdswiez.TabIndex = 5;
            // 
            // lblWierszy
            // 
            lblWierszy.Location = new Point(526, 0);
            lblWierszy.Name = "lblWierszy";
            lblWierszy.Size = new Size(200, 23);
            lblWierszy.TextAlign = ContentAlignment.MiddleLeft;
            lblWierszy.TabIndex = 6;
            // 
            // dgvNaglowki
            // 
            dgvNaglowki.AllowUserToAddRows = false;
            dgvNaglowki.AllowUserToDeleteRows = false;
            dgvNaglowki.Dock = DockStyle.Fill;
            dgvNaglowki.Location = new Point(13, 73);
            dgvNaglowki.Name = "dgvNaglowki";
            dgvNaglowki.ReadOnly = true;
            dgvNaglowki.Size = new Size(1558, 396);
            dgvNaglowki.TabIndex = 1;
            // 
            // dolnyPanel
            // 
            dolnyPanel.ColumnCount = 2;
            dolnyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            dolnyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 450F));
            dolnyPanel.Controls.Add(dgvPozycje, 0, 0);
            dolnyPanel.Controls.Add(panelAkcji, 1, 0);
            dolnyPanel.Dock = DockStyle.Fill;
            dolnyPanel.Location = new Point(13, 475);
            dolnyPanel.Name = "dolnyPanel";
            dolnyPanel.RowCount = 1;
            dolnyPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            dolnyPanel.Size = new Size(1558, 323);
            dolnyPanel.TabIndex = 2;
            // 
            // dgvPozycje
            // 
            dgvPozycje.AllowUserToAddRows = false;
            dgvPozycje.AllowUserToDeleteRows = false;
            dgvPozycje.Dock = DockStyle.Fill;
            dgvPozycje.Location = new Point(3, 3);
            dgvPozycje.Name = "dgvPozycje";
            dgvPozycje.ReadOnly = true;
            dgvPozycje.Size = new Size(1102, 317);
            dgvPozycje.TabIndex = 0;
            // 
            // panelAkcji
            // 
            panelAkcji.Controls.Add(layoutAkcji);
            panelAkcji.Dock = DockStyle.Fill;
            panelAkcji.Location = new Point(1111, 3);
            panelAkcji.Name = "panelAkcji";
            panelAkcji.Size = new Size(444, 317);
            panelAkcji.TabIndex = 1;
            panelAkcji.TabStop = false;
            // 
            // layoutAkcji
            // 
            layoutAkcji.ColumnCount = 1;
            layoutAkcji.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutAkcji.Controls.Add(new Label(), 0, 0);
            layoutAkcji.Controls.Add(txtNotatka, 0, 1);
            layoutAkcji.Controls.Add(btnZapiszNotatke, 0, 2);
            layoutAkcji.Controls.Add(panelDecyzji, 0, 3);
            layoutAkcji.Dock = DockStyle.Fill;
            layoutAkcji.Location = new Point(3, 19);
            layoutAkcji.Name = "layoutAkcji";
            layoutAkcji.RowCount = 4;
            layoutAkcji.RowStyles.Add(new RowStyle());
            layoutAkcji.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layoutAkcji.RowStyles.Add(new RowStyle());
            layoutAkcji.RowStyles.Add(new RowStyle());
            layoutAkcji.Size = new Size(438, 295);
            layoutAkcji.TabIndex = 0;
            // 
            // txtNotatka
            // 
            txtNotatka.Dock = DockStyle.Fill;
            txtNotatka.Location = new Point(3, 22);
            txtNotatka.Multiline = true;
            txtNotatka.ScrollBars = ScrollBars.Vertical;
            txtNotatka.Name = "txtNotatka";
            txtNotatka.Size = new Size(432, 172);
            txtNotatka.TabIndex = 1;
            // 
            // btnZapiszNotatke
            // 
            btnZapiszNotatke.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnZapiszNotatke.Location = new Point(3, 200);
            btnZapiszNotatke.Name = "btnZapiszNotatke";
            btnZapiszNotatke.Size = new Size(75, 23);
            btnZapiszNotatke.Margin = new Padding(3, 3, 3, 10);
            btnZapiszNotatke.TabIndex = 2;
            // 
            // panelDecyzji
            // 
            panelDecyzji.Controls.Add(btnAkceptuj);
            panelDecyzji.Controls.Add(btnOdrzuc);
            panelDecyzji.Dock = DockStyle.Fill;
            panelDecyzji.FlowDirection = FlowDirection.RightToLeft;
            panelDecyzji.Location = new Point(3, 236);
            panelDecyzji.Name = "panelDecyzji";
            panelDecyzji.Size = new Size(432, 56);
            panelDecyzji.TabIndex = 3;
            // 
            // btnAkceptuj
            // 
            btnAkceptuj.Location = new Point(354, 3);
            btnAkceptuj.Name = "btnAkceptuj";
            btnAkceptuj.Size = new Size(75, 23);
            btnAkceptuj.TabIndex = 0;
            // 
            // btnOdrzuc
            // 
            btnOdrzuc.Location = new Point(273, 3);
            btnOdrzuc.Name = "btnOdrzuc";
            btnOdrzuc.Size = new Size(75, 23);
            btnOdrzuc.TabIndex = 1;
            // 
            // panelPrzyciskowGlobalnych
            // 
            panelPrzyciskowGlobalnych.Controls.Add(btnZamknij);
            panelPrzyciskowGlobalnych.Dock = DockStyle.Fill;
            panelPrzyciskowGlobalnych.FlowDirection = FlowDirection.RightToLeft;
            panelPrzyciskowGlobalnych.Location = new Point(13, 804);
            panelPrzyciskowGlobalnych.Name = "panelPrzyciskowGlobalnych";
            panelPrzyciskowGlobalnych.Size = new Size(1558, 44);
            panelPrzyciskowGlobalnych.TabIndex = 3;
            // 
            // btnZamknij
            // 
            btnZamknij.Location = new Point(1480, 3);
            btnZamknij.Name = "btnZamknij";
            btnZamknij.Size = new Size(75, 23);
            btnZamknij.TabIndex = 0;
            // 
            // AdminChangeRequestsForm
            // 
            ClientSize = new Size(1584, 861);
            Controls.Add(root);
            Name = "AdminChangeRequestsForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Wnioski o zmianę – akceptacja/odmowa";
            MinimumSize = new Size(1200, 700);
            root.ResumeLayout(false);
            pasekFiltrow.ResumeLayout(false);
            pasekFiltrow.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvNaglowki).EndInit();
            dolnyPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvPozycje).EndInit();
            panelAkcji.ResumeLayout(false);
            layoutAkcji.ResumeLayout(false);
            layoutAkcji.PerformLayout();
            panelDecyzji.ResumeLayout(false);
            panelPrzyciskowGlobalnych.ResumeLayout(false);
            ResumeLayout(false);
        }
    }
}