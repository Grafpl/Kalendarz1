namespace Kalendarz1
{
    partial class SprawdzalkaUmow
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            dgvContracts = new System.Windows.Forms.DataGridView();
            btnAddContract = new System.Windows.Forms.Button();
            chkShowOnlyIncomplete = new System.Windows.Forms.CheckBox();
            lblSearch = new System.Windows.Forms.Label();
            txtSearch = new System.Windows.Forms.TextBox();
            panelTop = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)dgvContracts).BeginInit();
            panelTop.SuspendLayout();
            SuspendLayout();
            // 
            // dgvContracts
            // 
            dgvContracts.AllowUserToAddRows = false;
            dgvContracts.AllowUserToDeleteRows = false;
            dgvContracts.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            dgvContracts.BackgroundColor = System.Drawing.Color.White;
            dgvContracts.BorderStyle = System.Windows.Forms.BorderStyle.None;
            dgvContracts.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
            dgvContracts.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.FromArgb(92, 138, 58);
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 238);
            dataGridViewCellStyle1.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.Color.FromArgb(92, 138, 58);
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            dgvContracts.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            dgvContracts.ColumnHeadersHeight = 40;
            dgvContracts.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Segoe UI", 9F);
            dataGridViewCellStyle2.ForeColor = System.Drawing.Color.FromArgb(44, 62, 80);
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.Color.FromArgb(75, 115, 47);
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            dgvContracts.DefaultCellStyle = dataGridViewCellStyle2;
            dgvContracts.EnableHeadersVisualStyles = false;
            dgvContracts.GridColor = System.Drawing.Color.FromArgb(224, 224, 224);
            dgvContracts.Location = new System.Drawing.Point(0, 81);
            dgvContracts.Name = "dgvContracts";
            dgvContracts.RowHeadersVisible = false;
            dgvContracts.RowTemplate.Height = 30;
            dgvContracts.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            dgvContracts.Size = new System.Drawing.Size(1594, 769);
            dgvContracts.TabIndex = 116;
            // 
            // btnAddContract
            // 
            btnAddContract.BackColor = System.Drawing.Color.FromArgb(92, 138, 58);
            btnAddContract.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(92, 138, 58);
            btnAddContract.FlatAppearance.BorderSize = 0;
            btnAddContract.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnAddContract.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 238);
            btnAddContract.ForeColor = System.Drawing.Color.White;
            btnAddContract.Location = new System.Drawing.Point(22, 12);
            btnAddContract.Name = "btnAddContract";
            btnAddContract.Size = new System.Drawing.Size(150, 55);
            btnAddContract.TabIndex = 154;
            btnAddContract.Text = "Dodaj umowę";
            btnAddContract.UseVisualStyleBackColor = false;
            btnAddContract.Click += CommandButton_Insert_Click;
            // 
            // chkShowOnlyIncomplete
            // 
            chkShowOnlyIncomplete.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            chkShowOnlyIncomplete.AutoSize = true;
            chkShowOnlyIncomplete.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 238);
            chkShowOnlyIncomplete.ForeColor = System.Drawing.Color.FromArgb(44, 62, 80);
            chkShowOnlyIncomplete.Location = new System.Drawing.Point(361, 44);
            chkShowOnlyIncomplete.Name = "chkShowOnlyIncomplete";
            chkShowOnlyIncomplete.Size = new System.Drawing.Size(152, 21);
            chkShowOnlyIncomplete.TabIndex = 155;
            chkShowOnlyIncomplete.Text = "Pokaż nieuzupełnione";
            chkShowOnlyIncomplete.UseVisualStyleBackColor = true;
            chkShowOnlyIncomplete.CheckedChanged += nieUzupelnione_CheckedChanged;
            // 
            // lblSearch
            // 
            lblSearch.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            lblSearch.AutoSize = true;
            lblSearch.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 238);
            lblSearch.ForeColor = System.Drawing.Color.FromArgb(127, 140, 141);
            lblSearch.Location = new System.Drawing.Point(185, 12);
            lblSearch.Name = "lblSearch";
            lblSearch.Size = new System.Drawing.Size(161, 15);
            lblSearch.TabIndex = 157;
            lblSearch.Text = "Szukaj po dostawcy lub dacie";
            lblSearch.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtSearch
            // 
            txtSearch.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            txtSearch.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 238);
            txtSearch.Location = new System.Drawing.Point(185, 40);
            txtSearch.Name = "txtSearch";
            txtSearch.Size = new System.Drawing.Size(170, 27);
            txtSearch.TabIndex = 156;
            txtSearch.TextChanged += textBoxSearch_TextChanged;
            // 
            // panelTop
            // 
            panelTop.BackColor = System.Drawing.Color.White;
            panelTop.Controls.Add(btnAddContract);
            panelTop.Controls.Add(lblSearch);
            panelTop.Controls.Add(chkShowOnlyIncomplete);
            panelTop.Controls.Add(txtSearch);
            panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            panelTop.Location = new System.Drawing.Point(0, 0);
            panelTop.Name = "panelTop";
            panelTop.Size = new System.Drawing.Size(1594, 81);
            panelTop.TabIndex = 158;
            // 
            // SprawdzalkaUmow
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.White;
            ClientSize = new System.Drawing.Size(1594, 850);
            Controls.Add(panelTop);
            Controls.Add(dgvContracts);
            Font = new System.Drawing.Font("Segoe UI", 9F);
            Name = "SprawdzalkaUmow";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Sprawdzarka Umów";
            WindowState = System.Windows.Forms.FormWindowState.Maximized;
            ((System.ComponentModel.ISupportInitialize)dgvContracts).EndInit();
            panelTop.ResumeLayout(false);
            panelTop.PerformLayout();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView dgvContracts;
        private System.Windows.Forms.Button btnAddContract;
        private System.Windows.Forms.CheckBox chkShowOnlyIncomplete;
        private System.Windows.Forms.Label lblSearch;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.Panel panelTop;
    }
}