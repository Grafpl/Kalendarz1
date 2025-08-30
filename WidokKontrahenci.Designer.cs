// WidokKontrahenci.Designer.cs
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Kalendarz1
{
    partial class WidokKontrahenci
    {
        private IContainer components = null;

        private SplitContainer split;
        private DataGridView dgvSuppliers;
        private DataGridView dgvDeliveries;

        private ToolStrip toolStrip;
        private ToolStripLabel lblSearch;
        private ToolStripTextBox txtSearch;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripComboBox cmbPriceTypeFilter;
        private ToolStripComboBox cmbStatusFilter;
        private ToolStripButton btnRefresh;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripButton btnPrev;
        private ToolStripLabel lblPage;
        private ToolStripButton btnNext;
        private ToolStripSeparator toolStripSeparator3;
        private ToolStripButton btnDuplicates;
        private ToolStripButton btnExportCsv;
        private ToolStripSeparator toolStripSeparator4;
        private ToolStripButton btnAdd;
        private ToolStripButton btnEdit;

        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblCount;

        private TabControl tabsRight;
        private TabPage tabDetails;
        private TabPage tabDeliveries;

        // Szczegóły (prawy panel – zostaje, ale możesz ukryć tab, jeśli chcesz jeszcze prościej)
        private TextBox txtDetId, txtDetName, txtDetShort, txtDetCity, txtDetAddress, txtDetPostal;
        private TextBox txtDetPhone, txtDetEmail, txtDetNip, txtDetRegon, txtDetPesel, txtDetTypCeny;
        private TextBox txtDetKm, txtDetDodatek, txtDetUbytek, txtDetOstatnie;
        private CheckBox chkDetHalt;
        private Label lblDeliveries;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            components = new Container();

            // FORM
            this.Text = "Centrum hodowców";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.WindowState = FormWindowState.Maximized;
            this.MinimumSize = new Size(1100, 700);

            // TOOLSTRIP
            toolStrip = new ToolStrip();
            lblSearch = new ToolStripLabel { Text = "Szukaj:" };
            txtSearch = new ToolStripTextBox { AutoSize = false, Width = 220, ToolTipText = "Nazwa, miasto, NIP, telefon, email..." };
            toolStripSeparator1 = new ToolStripSeparator();

            var lblPriceType = new ToolStripLabel("Typ ceny:");
            cmbPriceTypeFilter = new ToolStripComboBox { AutoSize = false, Width = 160, ToolTipText = "Filtr: Typ ceny" };

            var lblStatus = new ToolStripLabel("Status:");
            cmbStatusFilter = new ToolStripComboBox { AutoSize = false, Width = 120, ToolTipText = "Status (Wszyscy/Aktywni/Wstrzymani)" };
            cmbStatusFilter.Items.AddRange(new object[] { "Wszyscy", "Aktywni", "Wstrzymani" });
            cmbStatusFilter.SelectedIndex = 0;

            btnRefresh = new ToolStripButton("Odśwież");
            toolStripSeparator2 = new ToolStripSeparator();
            btnPrev = new ToolStripButton("◀");
            lblPage = new ToolStripLabel("Strona: 1");
            btnNext = new ToolStripButton("▶");
            toolStripSeparator3 = new ToolStripSeparator();
            btnDuplicates = new ToolStripButton("Duplikaty");
            btnExportCsv = new ToolStripButton("Eksport CSV");
            toolStripSeparator4 = new ToolStripSeparator();
            btnAdd = new ToolStripButton("Dodaj");
            btnEdit = new ToolStripButton("Modyfikuj");

            toolStrip.Items.AddRange(new ToolStripItem[]
            {
                lblSearch, txtSearch, toolStripSeparator1,
                lblPriceType, cmbPriceTypeFilter,
                lblStatus, cmbStatusFilter,
                btnRefresh, toolStripSeparator2,
                btnPrev, lblPage, btnNext, toolStripSeparator3,
                btnDuplicates, btnExportCsv, toolStripSeparator4,
                btnAdd, btnEdit
            });

            // STATUSSTRIP
            statusStrip = new StatusStrip();
            lblCount = new ToolStripStatusLabel("Rekordy: 0");
            statusStrip.Items.Add(lblCount);

            // SPLIT
            split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 1000,
                FixedPanel = FixedPanel.Panel2
            };

            // LEWA – DGV
            dgvSuppliers = new DataGridView
            {
                Dock = DockStyle.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            };
            split.Panel1.Controls.Add(dgvSuppliers);

            // PRAWA – zakładki (szczegóły + dostawy)
            tabsRight = new TabControl { Dock = DockStyle.Fill };
            tabDetails = new TabPage("Szczegóły");
            tabDeliveries = new TabPage("Dostawy");

            // Szczegóły – prosta tabela readonly
            var panelDet = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 12,
                Padding = new Padding(10),
                AutoScroll = true
            };
            panelDet.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            panelDet.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            panelDet.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            panelDet.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            Label L(string t) => new Label { Text = t, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 0) };
            TextBox T() => new TextBox { ReadOnly = true, Anchor = AnchorStyles.Left | AnchorStyles.Right };

            int r = 0;
            panelDet.Controls.Add(L("ID"), 0, r); txtDetId = T(); panelDet.Controls.Add(txtDetId, 1, r);
            panelDet.Controls.Add(L("Nazwa"), 0, ++r); txtDetName = T(); panelDet.Controls.Add(txtDetName, 1, r);
            panelDet.Controls.Add(L("Skrót"), 0, ++r); txtDetShort = T(); panelDet.Controls.Add(txtDetShort, 1, r);
            panelDet.Controls.Add(L("Miasto"), 0, ++r); txtDetCity = T(); panelDet.Controls.Add(txtDetCity, 1, r);
            panelDet.Controls.Add(L("Adres"), 0, ++r); txtDetAddress = T(); panelDet.Controls.Add(txtDetAddress, 1, r);
            panelDet.Controls.Add(L("Kod"), 0, ++r); txtDetPostal = T(); panelDet.Controls.Add(txtDetPostal, 1, r);
            panelDet.Controls.Add(L("Telefon"), 0, ++r); txtDetPhone = T(); panelDet.Controls.Add(txtDetPhone, 1, r);
            panelDet.Controls.Add(L("Email"), 0, ++r); txtDetEmail = T(); panelDet.Controls.Add(txtDetEmail, 1, r);
            panelDet.Controls.Add(L("Halt"), 0, ++r); chkDetHalt = new CheckBox { Enabled = false, AutoSize = true, Anchor = AnchorStyles.Left }; panelDet.Controls.Add(chkDetHalt, 1, r);

            r = 0;
            panelDet.Controls.Add(L("NIP"), 2, r); txtDetNip = T(); panelDet.Controls.Add(txtDetNip, 3, r);
            panelDet.Controls.Add(L("REGON"), 2, ++r); txtDetRegon = T(); panelDet.Controls.Add(txtDetRegon, 3, r);
            panelDet.Controls.Add(L("PESEL"), 2, ++r); txtDetPesel = T(); panelDet.Controls.Add(txtDetPesel, 3, r);
            panelDet.Controls.Add(L("Typ Ceny"), 2, ++r); txtDetTypCeny = T(); panelDet.Controls.Add(txtDetTypCeny, 3, r);
            panelDet.Controls.Add(L("KM"), 2, ++r); txtDetKm = T(); panelDet.Controls.Add(txtDetKm, 3, r);
            panelDet.Controls.Add(L("Dodatek"), 2, ++r); txtDetDodatek = T(); panelDet.Controls.Add(txtDetDodatek, 3, r);
            panelDet.Controls.Add(L("Ubytek"), 2, ++r); txtDetUbytek = T(); panelDet.Controls.Add(txtDetUbytek, 3, r);
            panelDet.Controls.Add(L("Ost. dostawa"), 2, ++r); txtDetOstatnie = T(); panelDet.Controls.Add(txtDetOstatnie, 3, r);

            tabDetails.Controls.Add(panelDet);

            // Dostawy – mała siatka
            dgvDeliveries = new DataGridView
            {
                Dock = DockStyle.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            };
            var headerDel = new Panel { Dock = DockStyle.Top, Height = 32 };
            lblDeliveries = new Label
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                Padding = new Padding(8, 8, 0, 0),
                Text = "Dostawy: 0"
            };
            headerDel.Controls.Add(lblDeliveries);

            tabDeliveries.Controls.Add(dgvDeliveries);
            tabDeliveries.Controls.Add(headerDel);

            tabsRight.Controls.Add(tabDetails);
            tabsRight.Controls.Add(tabDeliveries);

            split.Panel2.Controls.Add(tabsRight);

            // Do formy
            this.Controls.Add(split);
            this.Controls.Add(toolStrip);
            this.Controls.Add(statusStrip);
            toolStrip.Dock = DockStyle.Top;
            statusStrip.Dock = DockStyle.Bottom;

            this.ResumeLayout(false);
            this.PerformLayout();
        }
        #endregion
    }
}
