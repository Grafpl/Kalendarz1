namespace Kalendarz1
{
    partial class WidokKontrahenci
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.SplitContainer split;
        private System.Windows.Forms.DataGridView dgvDeliveries;

        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripLabel lblSearch;
        private System.Windows.Forms.ToolStripTextBox txtSearch;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripComboBox cmbPriceTypeFilter;
        private System.Windows.Forms.ToolStripComboBox cmbStatusFilter;
        private System.Windows.Forms.ToolStripButton btnRefresh;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripButton btnPrev;
        private System.Windows.Forms.ToolStripLabel lblPage;
        private System.Windows.Forms.ToolStripButton btnNext;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripButton btnDuplicates;
        private System.Windows.Forms.ToolStripButton btnExportCsv;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
        private System.Windows.Forms.ToolStripButton btnAdd;
        private System.Windows.Forms.ToolStripButton btnEdit;

        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblCount;

        private System.Windows.Forms.TabControl tabsRight;
        private System.Windows.Forms.TabPage tabDetails;
        private System.Windows.Forms.TabPage tabDeliveries;

        private System.Windows.Forms.TextBox txtDetId, txtDetName, txtDetShort, txtDetCity, txtDetAddress, txtDetPostal;
        private System.Windows.Forms.TextBox txtDetPhone, txtDetEmail, txtDetNip, txtDetRegon, txtDetPesel, txtDetTypCeny;
        private System.Windows.Forms.TextBox txtDetKm, txtDetDodatek, txtDetUbytek, txtDetOstatnie;
        private System.Windows.Forms.CheckBox chkDetHalt;
        private System.Windows.Forms.Label lblDeliveries;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            toolStrip = new System.Windows.Forms.ToolStrip();
            lblSearch = new System.Windows.Forms.ToolStripLabel();
            txtSearch = new System.Windows.Forms.ToolStripTextBox();
            toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            cmbPriceTypeFilter = new System.Windows.Forms.ToolStripComboBox();
            cmbStatusFilter = new System.Windows.Forms.ToolStripComboBox();
            btnRefresh = new System.Windows.Forms.ToolStripButton();
            toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            btnPrev = new System.Windows.Forms.ToolStripButton();
            lblPage = new System.Windows.Forms.ToolStripLabel();
            btnNext = new System.Windows.Forms.ToolStripButton();
            toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            btnDuplicates = new System.Windows.Forms.ToolStripButton();
            btnExportCsv = new System.Windows.Forms.ToolStripButton();
            toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            btnAdd = new System.Windows.Forms.ToolStripButton();
            btnEdit = new System.Windows.Forms.ToolStripButton();
            statusStrip = new System.Windows.Forms.StatusStrip();
            lblCount = new System.Windows.Forms.ToolStripStatusLabel();
            split = new System.Windows.Forms.SplitContainer();
            tabsRight = new System.Windows.Forms.TabControl();
            tabDetails = new System.Windows.Forms.TabPage();
            tabDeliveries = new System.Windows.Forms.TabPage();
            dgvDeliveries = new System.Windows.Forms.DataGridView();
            lblDeliveries = new System.Windows.Forms.Label();
            txtDetId = new System.Windows.Forms.TextBox();
            txtDetName = new System.Windows.Forms.TextBox();
            txtDetShort = new System.Windows.Forms.TextBox();
            txtDetCity = new System.Windows.Forms.TextBox();
            txtDetAddress = new System.Windows.Forms.TextBox();
            txtDetPostal = new System.Windows.Forms.TextBox();
            txtDetPhone = new System.Windows.Forms.TextBox();
            txtDetEmail = new System.Windows.Forms.TextBox();
            txtDetNip = new System.Windows.Forms.TextBox();
            txtDetRegon = new System.Windows.Forms.TextBox();
            txtDetPesel = new System.Windows.Forms.TextBox();
            txtDetTypCeny = new System.Windows.Forms.TextBox();
            txtDetKm = new System.Windows.Forms.TextBox();
            txtDetDodatek = new System.Windows.Forms.TextBox();
            txtDetUbytek = new System.Windows.Forms.TextBox();
            txtDetOstatnie = new System.Windows.Forms.TextBox();
            chkDetHalt = new System.Windows.Forms.CheckBox();
            dgvSuppliers = new System.Windows.Forms.DataGridView();
            toolStrip.SuspendLayout();
            statusStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)split).BeginInit();
            split.Panel1.SuspendLayout();
            split.Panel2.SuspendLayout();
            split.SuspendLayout();
            tabsRight.SuspendLayout();
            tabDeliveries.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvDeliveries).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvSuppliers).BeginInit();
            SuspendLayout();
            // 
            // toolStrip
            // 
            toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { lblSearch, txtSearch, toolStripSeparator1, cmbPriceTypeFilter, cmbStatusFilter, btnRefresh, toolStripSeparator2, btnPrev, lblPage, btnNext, toolStripSeparator3, btnDuplicates, btnExportCsv, toolStripSeparator4, btnAdd, btnEdit });
            toolStrip.Location = new System.Drawing.Point(0, 0);
            toolStrip.Name = "toolStrip";
            toolStrip.Size = new System.Drawing.Size(1084, 25);
            toolStrip.TabIndex = 1;
            // 
            // lblSearch
            // 
            lblSearch.Name = "lblSearch";
            lblSearch.Size = new System.Drawing.Size(0, 22);
            // 
            // txtSearch
            // 
            txtSearch.Name = "txtSearch";
            txtSearch.Size = new System.Drawing.Size(100, 25);
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new System.Drawing.Size(6, 25);
            // 
            // cmbPriceTypeFilter
            // 
            cmbPriceTypeFilter.Name = "cmbPriceTypeFilter";
            cmbPriceTypeFilter.Size = new System.Drawing.Size(121, 25);
            // 
            // cmbStatusFilter
            // 
            cmbStatusFilter.Name = "cmbStatusFilter";
            cmbStatusFilter.Size = new System.Drawing.Size(121, 25);
            // 
            // btnRefresh
            // 
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new System.Drawing.Size(23, 22);
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new System.Drawing.Size(6, 25);
            // 
            // btnPrev
            // 
            btnPrev.Name = "btnPrev";
            btnPrev.Size = new System.Drawing.Size(23, 22);
            // 
            // lblPage
            // 
            lblPage.Name = "lblPage";
            lblPage.Size = new System.Drawing.Size(0, 22);
            // 
            // btnNext
            // 
            btnNext.Name = "btnNext";
            btnNext.Size = new System.Drawing.Size(23, 22);
            // 
            // toolStripSeparator3
            // 
            toolStripSeparator3.Name = "toolStripSeparator3";
            toolStripSeparator3.Size = new System.Drawing.Size(6, 25);
            // 
            // btnDuplicates
            // 
            btnDuplicates.Name = "btnDuplicates";
            btnDuplicates.Size = new System.Drawing.Size(23, 22);
            // 
            // btnExportCsv
            // 
            btnExportCsv.Name = "btnExportCsv";
            btnExportCsv.Size = new System.Drawing.Size(23, 22);
            // 
            // toolStripSeparator4
            // 
            toolStripSeparator4.Name = "toolStripSeparator4";
            toolStripSeparator4.Size = new System.Drawing.Size(6, 25);
            // 
            // btnAdd
            // 
            btnAdd.Name = "btnAdd";
            btnAdd.Size = new System.Drawing.Size(23, 22);
            // 
            // btnEdit
            // 
            btnEdit.Name = "btnEdit";
            btnEdit.Size = new System.Drawing.Size(23, 22);
            // 
            // statusStrip
            // 
            statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { lblCount });
            statusStrip.Location = new System.Drawing.Point(0, 639);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new System.Drawing.Size(1084, 22);
            statusStrip.TabIndex = 2;
            // 
            // lblCount
            // 
            lblCount.Name = "lblCount";
            lblCount.Size = new System.Drawing.Size(0, 17);
            // 
            // split
            // 
            split.Dock = System.Windows.Forms.DockStyle.Fill;
            split.Location = new System.Drawing.Point(0, 25);
            split.Name = "split";
            // 
            // split.Panel1
            // 
            split.Panel1.Controls.Add(dgvSuppliers);
            // 
            // split.Panel2
            // 
            split.Panel2.Controls.Add(tabsRight);
            split.Size = new System.Drawing.Size(1084, 614);
            split.SplitterDistance = 874;
            split.TabIndex = 0;
            // 
            // tabsRight
            // 
            tabsRight.Controls.Add(tabDetails);
            tabsRight.Controls.Add(tabDeliveries);
            tabsRight.Dock = System.Windows.Forms.DockStyle.Fill;
            tabsRight.Location = new System.Drawing.Point(0, 0);
            tabsRight.Name = "tabsRight";
            tabsRight.SelectedIndex = 0;
            tabsRight.Size = new System.Drawing.Size(206, 614);
            tabsRight.TabIndex = 0;
            // 
            // tabDetails
            // 
            tabDetails.Location = new System.Drawing.Point(4, 24);
            tabDetails.Name = "tabDetails";
            tabDetails.Size = new System.Drawing.Size(198, 586);
            tabDetails.TabIndex = 0;
            tabDetails.Text = "Szczegóły";
            // 
            // tabDeliveries
            // 
            tabDeliveries.Controls.Add(dgvDeliveries);
            tabDeliveries.Controls.Add(lblDeliveries);
            tabDeliveries.Location = new System.Drawing.Point(4, 24);
            tabDeliveries.Name = "tabDeliveries";
            tabDeliveries.Size = new System.Drawing.Size(198, 586);
            tabDeliveries.TabIndex = 1;
            tabDeliveries.Text = "Dostawy";
            // 
            // dgvDeliveries
            // 
            dgvDeliveries.Dock = System.Windows.Forms.DockStyle.Fill;
            dgvDeliveries.Location = new System.Drawing.Point(0, 23);
            dgvDeliveries.Name = "dgvDeliveries";
            dgvDeliveries.Size = new System.Drawing.Size(198, 563);
            dgvDeliveries.TabIndex = 0;
            // 
            // lblDeliveries
            // 
            lblDeliveries.Dock = System.Windows.Forms.DockStyle.Top;
            lblDeliveries.Location = new System.Drawing.Point(0, 0);
            lblDeliveries.Name = "lblDeliveries";
            lblDeliveries.Size = new System.Drawing.Size(198, 23);
            lblDeliveries.TabIndex = 1;
            lblDeliveries.Text = "Dostawy: 0";
            // 
            // txtDetId
            // 
            txtDetId.Location = new System.Drawing.Point(0, 0);
            txtDetId.Name = "txtDetId";
            txtDetId.Size = new System.Drawing.Size(100, 23);
            txtDetId.TabIndex = 0;
            // 
            // txtDetName
            // 
            txtDetName.Location = new System.Drawing.Point(0, 0);
            txtDetName.Name = "txtDetName";
            txtDetName.Size = new System.Drawing.Size(100, 23);
            txtDetName.TabIndex = 0;
            // 
            // txtDetShort
            // 
            txtDetShort.Location = new System.Drawing.Point(0, 0);
            txtDetShort.Name = "txtDetShort";
            txtDetShort.Size = new System.Drawing.Size(100, 23);
            txtDetShort.TabIndex = 0;
            // 
            // txtDetCity
            // 
            txtDetCity.Location = new System.Drawing.Point(0, 0);
            txtDetCity.Name = "txtDetCity";
            txtDetCity.Size = new System.Drawing.Size(100, 23);
            txtDetCity.TabIndex = 0;
            // 
            // txtDetAddress
            // 
            txtDetAddress.Location = new System.Drawing.Point(0, 0);
            txtDetAddress.Name = "txtDetAddress";
            txtDetAddress.Size = new System.Drawing.Size(100, 23);
            txtDetAddress.TabIndex = 0;
            // 
            // txtDetPostal
            // 
            txtDetPostal.Location = new System.Drawing.Point(0, 0);
            txtDetPostal.Name = "txtDetPostal";
            txtDetPostal.Size = new System.Drawing.Size(100, 23);
            txtDetPostal.TabIndex = 0;
            // 
            // txtDetPhone
            // 
            txtDetPhone.Location = new System.Drawing.Point(0, 0);
            txtDetPhone.Name = "txtDetPhone";
            txtDetPhone.Size = new System.Drawing.Size(100, 23);
            txtDetPhone.TabIndex = 0;
            // 
            // txtDetEmail
            // 
            txtDetEmail.Location = new System.Drawing.Point(0, 0);
            txtDetEmail.Name = "txtDetEmail";
            txtDetEmail.Size = new System.Drawing.Size(100, 23);
            txtDetEmail.TabIndex = 0;
            // 
            // txtDetNip
            // 
            txtDetNip.Location = new System.Drawing.Point(0, 0);
            txtDetNip.Name = "txtDetNip";
            txtDetNip.Size = new System.Drawing.Size(100, 23);
            txtDetNip.TabIndex = 0;
            // 
            // txtDetRegon
            // 
            txtDetRegon.Location = new System.Drawing.Point(0, 0);
            txtDetRegon.Name = "txtDetRegon";
            txtDetRegon.Size = new System.Drawing.Size(100, 23);
            txtDetRegon.TabIndex = 0;
            // 
            // txtDetPesel
            // 
            txtDetPesel.Location = new System.Drawing.Point(0, 0);
            txtDetPesel.Name = "txtDetPesel";
            txtDetPesel.Size = new System.Drawing.Size(100, 23);
            txtDetPesel.TabIndex = 0;
            // 
            // txtDetTypCeny
            // 
            txtDetTypCeny.Location = new System.Drawing.Point(0, 0);
            txtDetTypCeny.Name = "txtDetTypCeny";
            txtDetTypCeny.Size = new System.Drawing.Size(100, 23);
            txtDetTypCeny.TabIndex = 0;
            // 
            // txtDetKm
            // 
            txtDetKm.Location = new System.Drawing.Point(0, 0);
            txtDetKm.Name = "txtDetKm";
            txtDetKm.Size = new System.Drawing.Size(100, 23);
            txtDetKm.TabIndex = 0;
            // 
            // txtDetDodatek
            // 
            txtDetDodatek.Location = new System.Drawing.Point(0, 0);
            txtDetDodatek.Name = "txtDetDodatek";
            txtDetDodatek.Size = new System.Drawing.Size(100, 23);
            txtDetDodatek.TabIndex = 0;
            // 
            // txtDetUbytek
            // 
            txtDetUbytek.Location = new System.Drawing.Point(0, 0);
            txtDetUbytek.Name = "txtDetUbytek";
            txtDetUbytek.Size = new System.Drawing.Size(100, 23);
            txtDetUbytek.TabIndex = 0;
            // 
            // txtDetOstatnie
            // 
            txtDetOstatnie.Location = new System.Drawing.Point(0, 0);
            txtDetOstatnie.Name = "txtDetOstatnie";
            txtDetOstatnie.Size = new System.Drawing.Size(100, 23);
            txtDetOstatnie.TabIndex = 0;
            // 
            // chkDetHalt
            // 
            chkDetHalt.Location = new System.Drawing.Point(0, 0);
            chkDetHalt.Name = "chkDetHalt";
            chkDetHalt.Size = new System.Drawing.Size(104, 24);
            chkDetHalt.TabIndex = 0;
            // 
            // dgvSuppliers
            // 
            dgvSuppliers.Dock = System.Windows.Forms.DockStyle.Fill;
            dgvSuppliers.Location = new System.Drawing.Point(0, 0);
            dgvSuppliers.Name = "dgvSuppliers";
            dgvSuppliers.Size = new System.Drawing.Size(874, 614);
            dgvSuppliers.TabIndex = 0;
            // 
            // WidokKontrahenci
            // 
            ClientSize = new System.Drawing.Size(1084, 661);
            Controls.Add(split);
            Controls.Add(toolStrip);
            Controls.Add(statusStrip);
            MinimumSize = new System.Drawing.Size(1100, 700);
            Name = "WidokKontrahenci";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Centrum hodowców";
            WindowState = System.Windows.Forms.FormWindowState.Maximized;
            toolStrip.ResumeLayout(false);
            toolStrip.PerformLayout();
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            split.Panel1.ResumeLayout(false);
            split.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)split).EndInit();
            split.ResumeLayout(false);
            tabsRight.ResumeLayout(false);
            tabDeliveries.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvDeliveries).EndInit();
            ((System.ComponentModel.ISupportInitialize)dgvSuppliers).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.DataGridView dgvSuppliers;
    }
}
