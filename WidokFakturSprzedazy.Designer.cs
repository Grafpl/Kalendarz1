namespace Kalendarz1
{
    partial class WidokFakturSprzedazy
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
            dataGridViewOdbiorcy = new System.Windows.Forms.DataGridView();
            dataGridViewNotatki = new System.Windows.Forms.DataGridView();
            comboBoxTowar = new System.Windows.Forms.ComboBox();
            comboBoxKontrahent = new System.Windows.Forms.ComboBox();
            label1 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            dataGridViewPlatnosci = new System.Windows.Forms.DataGridView();
            dateTimePickerOd = new System.Windows.Forms.DateTimePicker();
            dateTimePickerDo = new System.Windows.Forms.DateTimePicker();
            label3 = new System.Windows.Forms.Label();
            label4 = new System.Windows.Forms.Label();
            btnShowAnalysis = new System.Windows.Forms.Button();
            btnRefresh = new System.Windows.Forms.Button();

            ((System.ComponentModel.ISupportInitialize)dataGridViewOdbiorcy).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewNotatki).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewPlatnosci).BeginInit();
            SuspendLayout();

            // 
            // dataGridViewOdbiorcy
            // 
            dataGridViewOdbiorcy.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewOdbiorcy.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewOdbiorcy.Location = new System.Drawing.Point(12, 45);
            dataGridViewOdbiorcy.Name = "dataGridViewOdbiorcy";
            dataGridViewOdbiorcy.RowTemplate.Height = 25;
            dataGridViewOdbiorcy.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            dataGridViewOdbiorcy.Size = new System.Drawing.Size(715, 600);
            dataGridViewOdbiorcy.TabIndex = 2;

            // 
            // dataGridViewNotatki
            // 
            dataGridViewNotatki.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewNotatki.Location = new System.Drawing.Point(733, 45);
            dataGridViewNotatki.Name = "dataGridViewNotatki";
            dataGridViewNotatki.RowTemplate.Height = 25;
            dataGridViewNotatki.Size = new System.Drawing.Size(732, 250);
            dataGridViewNotatki.TabIndex = 3;

            // 
            // dataGridViewPlatnosci
            // 
            dataGridViewPlatnosci.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewPlatnosci.Location = new System.Drawing.Point(733, 301);
            dataGridViewPlatnosci.Name = "dataGridViewPlatnosci";
            dataGridViewPlatnosci.RowTemplate.Height = 25;
            dataGridViewPlatnosci.Size = new System.Drawing.Size(732, 344);
            dataGridViewPlatnosci.TabIndex = 13;
            dataGridViewPlatnosci.RowPostPaint += dataGridViewPlatnosci_RowPostPaint;

            // Panel górny z filtrami
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(12, 15);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(39, 15);
            label1.TabIndex = 8;
            label1.Text = "Towar:";

            // 
            // comboBoxTowar
            // 
            comboBoxTowar.FormattingEnabled = true;
            comboBoxTowar.Location = new System.Drawing.Point(57, 12);
            comboBoxTowar.Name = "comboBoxTowar";
            comboBoxTowar.Size = new System.Drawing.Size(200, 23);
            comboBoxTowar.TabIndex = 6;
            comboBoxTowar.SelectedIndexChanged += comboBoxTowar_SelectedIndexChanged;

            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(270, 15);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(66, 15);
            label2.TabIndex = 9;
            label2.Text = "Kontrahent:";

            // 
            // comboBoxKontrahent
            // 
            comboBoxKontrahent.FormattingEnabled = true;
            comboBoxKontrahent.Location = new System.Drawing.Point(342, 12);
            comboBoxKontrahent.Name = "comboBoxKontrahent";
            comboBoxKontrahent.Size = new System.Drawing.Size(200, 23);
            comboBoxKontrahent.TabIndex = 7;
            comboBoxKontrahent.SelectedIndexChanged += comboBoxKontrahent_SelectedIndexChanged;

            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new System.Drawing.Point(560, 15);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(50, 15);
            label3.TabIndex = 14;
            label3.Text = "Data od:";

            // 
            // dateTimePickerOd
            // 
            dateTimePickerOd.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            dateTimePickerOd.Location = new System.Drawing.Point(616, 12);
            dateTimePickerOd.Name = "dateTimePickerOd";
            dateTimePickerOd.Size = new System.Drawing.Size(100, 23);
            dateTimePickerOd.TabIndex = 15;

            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new System.Drawing.Point(730, 15);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(50, 15);
            label4.TabIndex = 16;
            label4.Text = "Data do:";

            // 
            // dateTimePickerDo
            // 
            dateTimePickerDo.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            dateTimePickerDo.Location = new System.Drawing.Point(786, 12);
            dateTimePickerDo.Name = "dateTimePickerDo";
            dateTimePickerDo.Size = new System.Drawing.Size(100, 23);
            dateTimePickerDo.TabIndex = 17;

            // 
            // btnRefresh
            // 
            btnRefresh.BackColor = System.Drawing.Color.FromArgb(52, 152, 219);
            btnRefresh.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnRefresh.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            btnRefresh.ForeColor = System.Drawing.Color.White;
            btnRefresh.Location = new System.Drawing.Point(900, 11);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new System.Drawing.Size(100, 25);
            btnRefresh.TabIndex = 18;
            btnRefresh.Text = "🔄 Odśwież";
            btnRefresh.UseVisualStyleBackColor = false;
            btnRefresh.Click += btnRefresh_Click;

            // 
            // btnShowAnalysis
            // 
            btnShowAnalysis.BackColor = System.Drawing.Color.FromArgb(155, 89, 182);
            btnShowAnalysis.Enabled = false;
            btnShowAnalysis.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnShowAnalysis.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            btnShowAnalysis.ForeColor = System.Drawing.Color.White;
            btnShowAnalysis.Location = new System.Drawing.Point(1020, 9);
            btnShowAnalysis.Name = "btnShowAnalysis";
            btnShowAnalysis.Size = new System.Drawing.Size(180, 30);
            btnShowAnalysis.TabIndex = 19;
            btnShowAnalysis.Text = "📊 Analiza tygodniowa";
            btnShowAnalysis.UseVisualStyleBackColor = false;
            btnShowAnalysis.Click += btnShowAnalysis_Click;

            // 
            // WidokFakturSprzedazy
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1477, 657);
            Controls.Add(btnShowAnalysis);
            Controls.Add(btnRefresh);
            Controls.Add(dateTimePickerDo);
            Controls.Add(label4);
            Controls.Add(dateTimePickerOd);
            Controls.Add(label3);
            Controls.Add(dataGridViewPlatnosci);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(comboBoxKontrahent);
            Controls.Add(comboBoxTowar);
            Controls.Add(dataGridViewNotatki);
            Controls.Add(dataGridViewOdbiorcy);
            Name = "WidokFakturSprzedazy";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "System Zarządzania Fakturami Sprzedaży";
            WindowState = System.Windows.Forms.FormWindowState.Maximized;
            Load += WidokFakturSprzedazy_Load;
            ((System.ComponentModel.ISupportInitialize)dataGridViewOdbiorcy).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewNotatki).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewPlatnosci).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridViewOdbiorcy;
        private System.Windows.Forms.DataGridView dataGridViewNotatki;
        private System.Windows.Forms.ComboBox comboBoxTowar;
        private System.Windows.Forms.ComboBox comboBoxKontrahent;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.DataGridView dataGridViewPlatnosci;
        private System.Windows.Forms.DateTimePicker dateTimePickerOd;
        private System.Windows.Forms.DateTimePicker dateTimePickerDo;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnShowAnalysis;
        private System.Windows.Forms.Button btnRefresh;
    }
}