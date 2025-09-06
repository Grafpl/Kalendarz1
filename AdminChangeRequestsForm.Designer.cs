#nullable enable

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
        private FlowLayoutPanel panelPrzyciskowGlobalnych;
        private GroupBox panelAkcji;

        internal ComboBox cbStatus;
        internal TextBox tbSzukaj;
        internal Button btnOdswiez, btnAkceptuj, btnOdrzuc, btnZamknij, btnZapiszNotatke;
        internal Label lblWierszy;
        internal TextBox txtNotatka;

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
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
            root.Controls.Add(pasekFiltrow, 0, 0);
            root.Controls.Add(dgvNaglowki, 0, 1);
            root.Controls.Add(dolnyPanel, 0, 2);
            root.Controls.Add(panelPrzyciskowGlobalnych, 0, 3);
            root.Location = new Point(0, 0);
            root.Name = "root";
            root.RowStyles.Add(new RowStyle());
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
            root.RowStyles.Add(new RowStyle());
            root.Size = new Size(200, 100);
            root.TabIndex = 0;
            // 
            // pasekFiltrow
            // 
            pasekFiltrow.Controls.Add(cbStatus);
            pasekFiltrow.Controls.Add(tbSzukaj);
            pasekFiltrow.Controls.Add(btnOdswiez);
            pasekFiltrow.Controls.Add(lblWierszy);
            pasekFiltrow.Location = new Point(3, 3);
            pasekFiltrow.Name = "pasekFiltrow";
            pasekFiltrow.Size = new Size(194, 100);
            pasekFiltrow.TabIndex = 0;
            // 
            // cbStatus
            // 
            cbStatus.Items.AddRange(new object[] { "Wszystkie", "Proposed", "Zdecydowany" });
            cbStatus.Location = new Point(3, 3);
            cbStatus.Name = "cbStatus";
            cbStatus.Size = new Size(121, 23);
            cbStatus.TabIndex = 1;
            // 
            // tbSzukaj
            // 
            tbSzukaj.Location = new Point(3, 32);
            tbSzukaj.Name = "tbSzukaj";
            tbSzukaj.Size = new Size(100, 23);
            tbSzukaj.TabIndex = 3;
            // 
            // btnOdswiez
            // 
            btnOdswiez.Location = new Point(109, 32);
            btnOdswiez.Name = "btnOdswiez";
            btnOdswiez.Size = new Size(75, 23);
            btnOdswiez.TabIndex = 5;
            // 
            // lblWierszy
            // 
            lblWierszy.Location = new Point(3, 58);
            lblWierszy.Name = "lblWierszy";
            lblWierszy.Size = new Size(100, 23);
            lblWierszy.TabIndex = 6;
            // 
            // dgvNaglowki
            // 
            dgvNaglowki.Location = new Point(3, 109);
            dgvNaglowki.Name = "dgvNaglowki";
            dgvNaglowki.Size = new Size(194, 1);
            dgvNaglowki.TabIndex = 1;
            // 
            // dolnyPanel
            // 
            dolnyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            dolnyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 350F));
            dolnyPanel.Controls.Add(dgvPozycje, 0, 0);
            dolnyPanel.Controls.Add(panelAkcji, 1, 0);
            dolnyPanel.Location = new Point(3, 48);
            dolnyPanel.Name = "dolnyPanel";
            dolnyPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            dolnyPanel.Size = new Size(194, 1);
            dolnyPanel.TabIndex = 2;
            // 
            // dgvPozycje
            // 
            dgvPozycje.Location = new Point(3, 3);
            dgvPozycje.Name = "dgvPozycje";
            dgvPozycje.Size = new Size(1, 14);
            dgvPozycje.TabIndex = 0;
            // 
            // panelAkcji
            // 
            panelAkcji.Controls.Add(layoutAkcji);
            panelAkcji.Location = new Point(-153, 3);
            panelAkcji.Name = "panelAkcji";
            panelAkcji.Size = new Size(200, 14);
            panelAkcji.TabIndex = 1;
            panelAkcji.TabStop = false;
            // 
            // layoutAkcji
            // 
            layoutAkcji.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
            layoutAkcji.Controls.Add(txtNotatka, 0, 1);
            layoutAkcji.Controls.Add(btnZapiszNotatke, 0, 2);
            layoutAkcji.Controls.Add(panelDecyzji, 0, 3);
            layoutAkcji.Location = new Point(0, 0);
            layoutAkcji.Name = "layoutAkcji";
            layoutAkcji.RowStyles.Add(new RowStyle());
            layoutAkcji.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layoutAkcji.RowStyles.Add(new RowStyle());
            layoutAkcji.RowStyles.Add(new RowStyle());
            layoutAkcji.Size = new Size(200, 100);
            layoutAkcji.TabIndex = 0;
            // 
            // txtNotatka
            // 
            txtNotatka.Location = new Point(3, 3);
            txtNotatka.Name = "txtNotatka";
            txtNotatka.Size = new Size(100, 23);
            txtNotatka.TabIndex = 1;
            // 
            // btnZapiszNotatke
            // 
            btnZapiszNotatke.Location = new Point(3, -32);
            btnZapiszNotatke.Name = "btnZapiszNotatke";
            btnZapiszNotatke.Size = new Size(75, 23);
            btnZapiszNotatke.TabIndex = 2;
            // 
            // panelDecyzji
            // 
            panelDecyzji.Controls.Add(btnAkceptuj);
            panelDecyzji.Controls.Add(btnOdrzuc);
            panelDecyzji.Location = new Point(3, -3);
            panelDecyzji.Name = "panelDecyzji";
            panelDecyzji.Size = new Size(194, 100);
            panelDecyzji.TabIndex = 3;
            // 
            // btnAkceptuj
            // 
            btnAkceptuj.Location = new Point(3, 3);
            btnAkceptuj.Name = "btnAkceptuj";
            btnAkceptuj.Size = new Size(75, 23);
            btnAkceptuj.TabIndex = 0;
            // 
            // btnOdrzuc
            // 
            btnOdrzuc.Location = new Point(84, 3);
            btnOdrzuc.Name = "btnOdrzuc";
            btnOdrzuc.Size = new Size(75, 23);
            btnOdrzuc.TabIndex = 1;
            // 
            // panelPrzyciskowGlobalnych
            // 
            panelPrzyciskowGlobalnych.Controls.Add(btnZamknij);
            panelPrzyciskowGlobalnych.Location = new Point(3, -2);
            panelPrzyciskowGlobalnych.Name = "panelPrzyciskowGlobalnych";
            panelPrzyciskowGlobalnych.Size = new Size(194, 100);
            panelPrzyciskowGlobalnych.TabIndex = 3;
            // 
            // btnZamknij
            // 
            btnZamknij.Location = new Point(3, 3);
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
        private TableLayoutPanel layoutAkcji;
        private FlowLayoutPanel panelDecyzji;
    }
}