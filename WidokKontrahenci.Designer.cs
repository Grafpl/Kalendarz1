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
        private System.Windows.Forms.ToolStripButton btnRefresh;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripLabel lblPage;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripButton btnAdd;
        private System.Windows.Forms.ToolStripButton btnEdit;

        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblCount;

        private System.Windows.Forms.TabControl tabsRight;
        private System.Windows.Forms.TabPage tabDetails;
        private System.Windows.Forms.TabPage tabDeliveries;
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
            btnRefresh = new System.Windows.Forms.ToolStripButton();
            toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            lblPage = new System.Windows.Forms.ToolStripLabel();
            toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            btnAdd = new System.Windows.Forms.ToolStripButton();
            btnEdit = new System.Windows.Forms.ToolStripButton();
            statusStrip = new System.Windows.Forms.StatusStrip();
            lblCount = new System.Windows.Forms.ToolStripStatusLabel();
            split = new System.Windows.Forms.SplitContainer();
            dgvSuppliers = new System.Windows.Forms.DataGridView();
            tabsRight = new System.Windows.Forms.TabControl();
            tabDetails = new System.Windows.Forms.TabPage();
            tabDeliveries = new System.Windows.Forms.TabPage();
            dgvDeliveries = new System.Windows.Forms.DataGridView();
            lblDeliveries = new System.Windows.Forms.Label();

            toolStrip.SuspendLayout();
            statusStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)split).BeginInit();
            split.Panel1.SuspendLayout();
            split.Panel2.SuspendLayout();
            split.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvSuppliers).BeginInit();
            tabsRight.SuspendLayout();
            tabDeliveries.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvDeliveries).BeginInit();
            SuspendLayout();

            // toolStrip
            toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                lblSearch,
                txtSearch,
                toolStripSeparator1,
                cmbPriceTypeFilter,
                btnRefresh,
                toolStripSeparator2,
                lblPage,
                toolStripSeparator3,
                btnAdd,
                btnEdit
            });
            toolStrip.Location = new System.Drawing.Point(0, 0);
            toolStrip.Name = "toolStrip";
            toolStrip.Size = new System.Drawing.Size(1400, 35);
            toolStrip.TabIndex = 1;
            toolStrip.BackColor = System.Drawing.Color.FromArgb(236, 240, 241);
            toolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            toolStrip.Padding = new System.Windows.Forms.Padding(5);

            // lblSearch
            lblSearch.Name = "lblSearch";
            lblSearch.Size = new System.Drawing.Size(75, 27);
            lblSearch.Text = "🔍 Szukaj:";
            lblSearch.Font = new System.Drawing.Font("Segoe UI", 10f);

            // txtSearch
            txtSearch.Name = "txtSearch";
            txtSearch.Size = new System.Drawing.Size(250, 35);
            txtSearch.Font = new System.Drawing.Font("Segoe UI", 10f);
            txtSearch.ToolTipText = "Wpisz nazwę, miasto, NIP lub telefon";

            // toolStripSeparator1
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new System.Drawing.Size(6, 35);

            // cmbPriceTypeFilter
            cmbPriceTypeFilter.Name = "cmbPriceTypeFilter";
            cmbPriceTypeFilter.Size = new System.Drawing.Size(180, 35);
            cmbPriceTypeFilter.ToolTipText = "Filtruj według typu ceny";
            cmbPriceTypeFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbPriceTypeFilter.Font = new System.Drawing.Font("Segoe UI", 10f);

            // btnRefresh
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new System.Drawing.Size(85, 32);
            btnRefresh.Text = "🔄 Odśwież";
            btnRefresh.Font = new System.Drawing.Font("Segoe UI", 10f, System.Drawing.FontStyle.Bold);
            btnRefresh.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            btnRefresh.BackColor = System.Drawing.Color.FromArgb(52, 152, 219);
            btnRefresh.ForeColor = System.Drawing.Color.White;

            // toolStripSeparator2
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new System.Drawing.Size(6, 35);

            // lblPage
            lblPage.Name = "lblPage";
            lblPage.Size = new System.Drawing.Size(70, 27);
            lblPage.Text = "Strona: 1";
            lblPage.Font = new System.Drawing.Font("Segoe UI", 10f);

            // toolStripSeparator3
            toolStripSeparator3.Name = "toolStripSeparator3";
            toolStripSeparator3.Size = new System.Drawing.Size(6, 35);

            // btnAdd
            btnAdd.BackColor = System.Drawing.Color.FromArgb(39, 174, 96);
            btnAdd.ForeColor = System.Drawing.Color.White;
            btnAdd.Name = "btnAdd";
            btnAdd.Size = new System.Drawing.Size(80, 32);
            btnAdd.Text = "➕ DODAJ";
            btnAdd.Font = new System.Drawing.Font("Segoe UI", 10f, System.Drawing.FontStyle.Bold);
            btnAdd.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            btnAdd.Padding = new System.Windows.Forms.Padding(8, 5, 8, 5);

            // btnEdit
            btnEdit.BackColor = System.Drawing.Color.FromArgb(243, 156, 18);
            btnEdit.ForeColor = System.Drawing.Color.White;
            btnEdit.Name = "btnEdit";
            btnEdit.Size = new System.Drawing.Size(110, 32);
            btnEdit.Text = "✏️ MODYFIKUJ";
            btnEdit.Font = new System.Drawing.Font("Segoe UI", 10f, System.Drawing.FontStyle.Bold);
            btnEdit.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            btnEdit.Padding = new System.Windows.Forms.Padding(8, 5, 8, 5);

            // statusStrip
            statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { lblCount });
            statusStrip.Location = new System.Drawing.Point(0, 828);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new System.Drawing.Size(1400, 22);
            statusStrip.TabIndex = 2;
            statusStrip.BackColor = System.Drawing.Color.FromArgb(44, 62, 80);

            // lblCount
            lblCount.Name = "lblCount";
            lblCount.Size = new System.Drawing.Size(80, 17);
            lblCount.Text = "Rekordów: 0";
            lblCount.ForeColor = System.Drawing.Color.White;
            lblCount.Font = new System.Drawing.Font("Segoe UI", 10f);

            // split
            split.Dock = System.Windows.Forms.DockStyle.Fill;
            split.Location = new System.Drawing.Point(0, 35);
            split.Name = "split";
            split.Size = new System.Drawing.Size(1400, 793);
            split.SplitterDistance = 1000;
            split.SplitterWidth = 5;
            split.TabIndex = 0;
            split.BackColor = System.Drawing.Color.FromArgb(236, 240, 241);

            // split.Panel1
            split.Panel1.Controls.Add(dgvSuppliers);

            // split.Panel2
            split.Panel2.Controls.Add(tabsRight);

            // dgvSuppliers
            dgvSuppliers.Dock = System.Windows.Forms.DockStyle.Fill;
            dgvSuppliers.Location = new System.Drawing.Point(0, 0);
            dgvSuppliers.Name = "dgvSuppliers";
            dgvSuppliers.Size = new System.Drawing.Size(1000, 793);
            dgvSuppliers.TabIndex = 0;
            dgvSuppliers.AllowUserToAddRows = false;
            dgvSuppliers.AllowUserToDeleteRows = false;
            dgvSuppliers.RowHeadersVisible = false;
            dgvSuppliers.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            dgvSuppliers.MultiSelect = false;
            dgvSuppliers.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dgvSuppliers.BorderStyle = System.Windows.Forms.BorderStyle.None;
            dgvSuppliers.BackgroundColor = System.Drawing.Color.FromArgb(250, 251, 252);
            dgvSuppliers.GridColor = System.Drawing.Color.FromArgb(230, 234, 237);
            dgvSuppliers.ColumnHeadersHeight = 45;
            dgvSuppliers.EnableHeadersVisualStyles = false;
            dgvSuppliers.RowTemplate.Height = 50;

            // tabsRight
            tabsRight.Controls.Add(tabDetails);
            tabsRight.Controls.Add(tabDeliveries);
            tabsRight.Dock = System.Windows.Forms.DockStyle.Fill;
            tabsRight.Location = new System.Drawing.Point(0, 0);
            tabsRight.Name = "tabsRight";
            tabsRight.SelectedIndex = 0;
            tabsRight.Size = new System.Drawing.Size(395, 793);
            tabsRight.TabIndex = 0;
            tabsRight.Font = new System.Drawing.Font("Segoe UI", 10f);

            // tabDetails (ukryta)
            tabDetails.Location = new System.Drawing.Point(4, 24);
            tabDetails.Name = "tabDetails";
            tabDetails.Size = new System.Drawing.Size(387, 765);
            tabDetails.TabIndex = 0;
            tabDetails.Text = "Szczegóły";
            tabDetails.UseVisualStyleBackColor = true;

            // tabDeliveries
            tabDeliveries.Controls.Add(dgvDeliveries);
            tabDeliveries.Controls.Add(lblDeliveries);
            tabDeliveries.Location = new System.Drawing.Point(4, 24);
            tabDeliveries.Name = "tabDeliveries";
            tabDeliveries.Padding = new System.Windows.Forms.Padding(3);
            tabDeliveries.Size = new System.Drawing.Size(387, 765);
            tabDeliveries.TabIndex = 1;
            tabDeliveries.Text = "📦 DOSTAWY";
            tabDeliveries.UseVisualStyleBackColor = true;
            tabDeliveries.BackColor = System.Drawing.Color.White;

            // dgvDeliveries
            dgvDeliveries.Dock = System.Windows.Forms.DockStyle.Fill;
            dgvDeliveries.Location = new System.Drawing.Point(3, 33);
            dgvDeliveries.Name = "dgvDeliveries";
            dgvDeliveries.Size = new System.Drawing.Size(381, 729);
            dgvDeliveries.TabIndex = 1;
            dgvDeliveries.AllowUserToAddRows = false;
            dgvDeliveries.AllowUserToDeleteRows = false;
            dgvDeliveries.ReadOnly = true;
            dgvDeliveries.RowHeadersVisible = false;
            dgvDeliveries.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            dgvDeliveries.MultiSelect = false;
            dgvDeliveries.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dgvDeliveries.BorderStyle = System.Windows.Forms.BorderStyle.None;
            dgvDeliveries.BackgroundColor = System.Drawing.Color.FromArgb(250, 251, 252);
            dgvDeliveries.GridColor = System.Drawing.Color.FromArgb(230, 234, 237);
            dgvDeliveries.RowTemplate.Height = 35;
            dgvDeliveries.Font = new System.Drawing.Font("Segoe UI", 9.5f);

            // lblDeliveries
            lblDeliveries.Dock = System.Windows.Forms.DockStyle.Top;
            lblDeliveries.Location = new System.Drawing.Point(3, 3);
            lblDeliveries.Name = "lblDeliveries";
            lblDeliveries.Size = new System.Drawing.Size(381, 30);
            lblDeliveries.TabIndex = 0;
            lblDeliveries.Text = "Wybierz dostawcę aby zobaczyć historię dostaw";
            lblDeliveries.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblDeliveries.Font = new System.Drawing.Font("Segoe UI Semibold", 11f);
            lblDeliveries.ForeColor = System.Drawing.Color.FromArgb(52, 73, 94);
            lblDeliveries.BackColor = System.Drawing.Color.FromArgb(236, 240, 241);
            lblDeliveries.Padding = new System.Windows.Forms.Padding(5);

            // WidokKontrahenci
            AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1400, 850);
            Controls.Add(split);
            Controls.Add(toolStrip);
            Controls.Add(statusStrip);
            Name = "WidokKontrahenci";
            Text = "📋 SYSTEM OCENY DOSTAWCÓW - Zarządzanie Jakością";
            WindowState = System.Windows.Forms.FormWindowState.Maximized;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Icon = System.Drawing.SystemIcons.Application;
            toolStrip.ResumeLayout(false);
            toolStrip.PerformLayout();
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            split.Panel1.ResumeLayout(false);
            split.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)split).EndInit();
            split.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvSuppliers).EndInit();
            tabsRight.ResumeLayout(false);
            tabDeliveries.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvDeliveries).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }
        #endregion
    }
}