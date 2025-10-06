namespace Kalendarz1
{
    partial class WidokFakturSprzedazy
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

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            panelFilters = new System.Windows.Forms.Panel();
            splitContainerMain = new System.Windows.Forms.SplitContainer();
            splitContainerDetail = new System.Windows.Forms.SplitContainer();
            dataGridViewOdbiorcy = new System.Windows.Forms.DataGridView();
            dataGridViewPozycje = new System.Windows.Forms.DataGridView();
            tabControl = new System.Windows.Forms.TabControl();
            dataGridViewPlatnosci = new System.Windows.Forms.DataGridView();

            comboBoxKontrahent = new System.Windows.Forms.ComboBox();

            label2 = new System.Windows.Forms.Label();
            dateTimePickerOd = new System.Windows.Forms.DateTimePicker();
            dateTimePickerDo = new System.Windows.Forms.DateTimePicker();
            label3 = new System.Windows.Forms.Label();
            label4 = new System.Windows.Forms.Label();
            btnRefresh = new System.Windows.Forms.Button();
            panelPozycjeHeader = new System.Windows.Forms.Panel();
            lblPozycjeHeader = new System.Windows.Forms.Label();

            ((System.ComponentModel.ISupportInitialize)splitContainerMain).BeginInit();
            splitContainerMain.Panel1.SuspendLayout();
            splitContainerMain.Panel2.SuspendLayout();
            splitContainerMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainerDetail).BeginInit();
            splitContainerDetail.Panel1.SuspendLayout();
            splitContainerDetail.Panel2.SuspendLayout();
            splitContainerDetail.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewOdbiorcy).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewPozycje).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewPlatnosci).BeginInit();
            panelFilters.SuspendLayout();
            panelPozycjeHeader.SuspendLayout();
            SuspendLayout();

            // 
            // panelFilters - Panel górny z filtrami
            // 
            panelFilters.BackColor = System.Drawing.Color.White;
            panelFilters.Controls.Add(btnRefresh);
            panelFilters.Controls.Add(dateTimePickerDo);
            panelFilters.Controls.Add(label4);
            panelFilters.Controls.Add(dateTimePickerOd);
            panelFilters.Controls.Add(label3);
            panelFilters.Controls.Add(label2);
            panelFilters.Controls.Add(comboBoxKontrahent);

            panelFilters.Dock = System.Windows.Forms.DockStyle.Top;
            panelFilters.Location = new System.Drawing.Point(0, 0);
            panelFilters.Name = "panelFilters";
            panelFilters.Size = new System.Drawing.Size(1600, 50);
            panelFilters.TabIndex = 0;

            // 
            // splitContainerMain - Główny podział pionowy - ZMIANA: zwiększona szerokość lewego panelu
            // 
            splitContainerMain.BackColor = System.Drawing.Color.FromArgb(189, 195, 199);
            splitContainerMain.Dock = System.Windows.Forms.DockStyle.Fill;
            splitContainerMain.Location = new System.Drawing.Point(0, 50);
            splitContainerMain.Name = "splitContainerMain";
            splitContainerMain.Orientation = System.Windows.Forms.Orientation.Vertical;
            splitContainerMain.Panel1.Controls.Add(dataGridViewOdbiorcy);
            splitContainerMain.Panel2.Controls.Add(splitContainerDetail);
            splitContainerMain.Size = new System.Drawing.Size(1600, 850);
            splitContainerMain.SplitterDistance = 850;  // ZMIANA: z 500 na 850
            splitContainerMain.SplitterWidth = 6;
            splitContainerMain.TabIndex = 1;

            // ... reszta kodu splitContainerDetail bez zmian ...

            splitContainerDetail.BackColor = System.Drawing.Color.FromArgb(189, 195, 199);
            splitContainerDetail.Dock = System.Windows.Forms.DockStyle.Fill;
            splitContainerDetail.Location = new System.Drawing.Point(0, 0);
            splitContainerDetail.Name = "splitContainerDetail";
            splitContainerDetail.Orientation = System.Windows.Forms.Orientation.Horizontal;
            splitContainerDetail.Panel1.Controls.Add(dataGridViewPozycje);
            splitContainerDetail.Panel1.Controls.Add(panelPozycjeHeader);
            splitContainerDetail.Panel2.Controls.Add(tabControl);
            splitContainerDetail.Size = new System.Drawing.Size(744, 850);  // ZMIANA: automatycznie dostosowane
            splitContainerDetail.SplitterDistance = 350;
            splitContainerDetail.SplitterWidth = 6;
            splitContainerDetail.TabIndex = 0;

            // 
            // panelPozycjeHeader
            // 
            panelPozycjeHeader.BackColor = System.Drawing.Color.FromArgb(22, 160, 133);
            panelPozycjeHeader.Controls.Add(lblPozycjeHeader);
            panelPozycjeHeader.Dock = System.Windows.Forms.DockStyle.Top;
            panelPozycjeHeader.Location = new System.Drawing.Point(0, 0);
            panelPozycjeHeader.Name = "panelPozycjeHeader";
            panelPozycjeHeader.Size = new System.Drawing.Size(744, 40);
            panelPozycjeHeader.TabIndex = 1;

            // 
            // lblPozycjeHeader
            // 
            lblPozycjeHeader.Dock = System.Windows.Forms.DockStyle.Fill;
            lblPozycjeHeader.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            lblPozycjeHeader.ForeColor = System.Drawing.Color.White;
            lblPozycjeHeader.Location = new System.Drawing.Point(0, 0);
            lblPozycjeHeader.Name = "lblPozycjeHeader";
            lblPozycjeHeader.Padding = new System.Windows.Forms.Padding(15, 0, 0, 0);
            lblPozycjeHeader.Size = new System.Drawing.Size(744, 40);
            lblPozycjeHeader.TabIndex = 0;
            lblPozycjeHeader.Text = "POZYCJE DOKUMENTU";
            lblPozycjeHeader.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            // 
            // dataGridViewOdbiorcy
            // 
            dataGridViewOdbiorcy.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewOdbiorcy.BackgroundColor = System.Drawing.Color.White;
            dataGridViewOdbiorcy.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewOdbiorcy.Dock = System.Windows.Forms.DockStyle.Fill;
            dataGridViewOdbiorcy.Location = new System.Drawing.Point(0, 0);
            dataGridViewOdbiorcy.Name = "dataGridViewOdbiorcy";
            dataGridViewOdbiorcy.RowTemplate.Height = 25;
            dataGridViewOdbiorcy.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            dataGridViewOdbiorcy.Size = new System.Drawing.Size(850, 850);  // ZMIANA: automatycznie dostosowane
            dataGridViewOdbiorcy.TabIndex = 0;

            // 
            // dataGridViewPozycje
            // 
            dataGridViewPozycje.BackgroundColor = System.Drawing.Color.White;
            dataGridViewPozycje.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewPozycje.Dock = System.Windows.Forms.DockStyle.Fill;
            dataGridViewPozycje.Location = new System.Drawing.Point(0, 40);
            dataGridViewPozycje.Name = "dataGridViewPozycje";
            dataGridViewPozycje.RowTemplate.Height = 25;
            dataGridViewPozycje.Size = new System.Drawing.Size(744, 310);
            dataGridViewPozycje.TabIndex = 0;

            // 
            // tabControl
            // 
            tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            tabControl.Font = new System.Drawing.Font("Segoe UI", 9F);
            tabControl.Location = new System.Drawing.Point(0, 0);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new System.Drawing.Size(744, 494);
            tabControl.TabIndex = 0;

            // 
            // dataGridViewPlatnosci
            // 
            dataGridViewPlatnosci.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewPlatnosci.Dock = System.Windows.Forms.DockStyle.Fill;
            dataGridViewPlatnosci.Location = new System.Drawing.Point(0, 0);
            dataGridViewPlatnosci.Name = "dataGridViewPlatnosci";
            dataGridViewPlatnosci.RowTemplate.Height = 25;
            dataGridViewPlatnosci.Size = new System.Drawing.Size(738, 468);
            dataGridViewPlatnosci.TabIndex = 0;

            // pozostałe kontrolki bez zmian...


            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(260, 18);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(70, 15);
            label2.TabIndex = 2;
            label2.Text = "Kontrahent:";

            // 
            // comboBoxKontrahent
            // 
            comboBoxKontrahent.FormattingEnabled = true;
            comboBoxKontrahent.Location = new System.Drawing.Point(335, 15);
            comboBoxKontrahent.Name = "comboBoxKontrahent";
            comboBoxKontrahent.Size = new System.Drawing.Size(180, 23);
            comboBoxKontrahent.TabIndex = 3;
            comboBoxKontrahent.SelectedIndexChanged += comboBoxKontrahent_SelectedIndexChanged;

            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new System.Drawing.Point(800, 18);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(53, 15);
            label3.TabIndex = 4;
            label3.Text = "Data od:";

            // 
            // dateTimePickerOd
            // 
            dateTimePickerOd.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            dateTimePickerOd.Location = new System.Drawing.Point(850, 15);
            dateTimePickerOd.Name = "dateTimePickerOd";
            dateTimePickerOd.Size = new System.Drawing.Size(100, 23);
            dateTimePickerOd.TabIndex = 5;

            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new System.Drawing.Point(960, 18);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(53, 15);
            label4.TabIndex = 6;
            label4.Text = "Data do:";

            // 
            // dateTimePickerDo
            // 
            dateTimePickerDo.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            dateTimePickerDo.Location = new System.Drawing.Point(1010, 15);
            dateTimePickerDo.Name = "dateTimePickerDo";
            dateTimePickerDo.Size = new System.Drawing.Size(100, 23);
            dateTimePickerDo.TabIndex = 7;

            // 
            // btnRefresh
            // 
            btnRefresh.BackColor = System.Drawing.Color.FromArgb(52, 152, 219);
            btnRefresh.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnRefresh.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            btnRefresh.ForeColor = System.Drawing.Color.White;
            btnRefresh.Location = new System.Drawing.Point(1130, 13);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new System.Drawing.Size(100, 27);
            btnRefresh.TabIndex = 8;
            btnRefresh.Text = "🔄 Odśwież";
            btnRefresh.UseVisualStyleBackColor = false;
            btnRefresh.Click += btnRefresh_Click;

            // USUNIĘTO: btnShowAnalysis

            // 
            // WidokFakturSprzedazy
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1600, 900);
            Controls.Add(splitContainerMain);
            Controls.Add(panelFilters);
            MinimumSize = new System.Drawing.Size(1200, 700);
            Name = "WidokFakturSprzedazy";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "System Zarządzania Fakturami Sprzedaży";
            WindowState = System.Windows.Forms.FormWindowState.Maximized;
            Load += WidokFakturSprzedazy_Load;

            splitContainerMain.Panel1.ResumeLayout(false);
            splitContainerMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainerMain).EndInit();
            splitContainerMain.ResumeLayout(false);
            splitContainerDetail.Panel1.ResumeLayout(false);
            splitContainerDetail.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainerDetail).EndInit();
            splitContainerDetail.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridViewOdbiorcy).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewPozycje).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewPlatnosci).EndInit();
            panelFilters.ResumeLayout(false);
            panelFilters.PerformLayout();
            panelPozycjeHeader.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel panelFilters;
        private System.Windows.Forms.SplitContainer splitContainerMain;
        private System.Windows.Forms.SplitContainer splitContainerDetail;
        private System.Windows.Forms.DataGridView dataGridViewOdbiorcy;
        private System.Windows.Forms.DataGridView dataGridViewPozycje;
        private System.Windows.Forms.TabControl tabControl;

        private System.Windows.Forms.ComboBox comboBoxKontrahent;

        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.DataGridView dataGridViewPlatnosci;
        private System.Windows.Forms.DateTimePicker dateTimePickerOd;
        private System.Windows.Forms.DateTimePicker dateTimePickerDo;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Panel panelPozycjeHeader;
        private System.Windows.Forms.Label lblPozycjeHeader;
    }
}
// USUNIĘTO: private System.Windows.Forms.Button btnShowAnalysis;