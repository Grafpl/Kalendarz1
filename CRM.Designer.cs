namespace Kalendarz1
{
    partial class CRM
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
            textBoxNotatka = new System.Windows.Forms.TextBox();
            buttonDodajNotatke = new System.Windows.Forms.Button();
            comboBoxStatusFilter = new System.Windows.Forms.ComboBox();
            comboBoxPowiatFilter = new System.Windows.Forms.ComboBox();
            label1 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            label3 = new System.Windows.Forms.Label();
            dataGridViewRanking = new System.Windows.Forms.DataGridView();
            button1 = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)dataGridViewOdbiorcy).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewNotatki).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewRanking).BeginInit();
            SuspendLayout();
            // 
            // dataGridViewOdbiorcy
            // 
            dataGridViewOdbiorcy.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewOdbiorcy.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewOdbiorcy.Location = new System.Drawing.Point(12, 256);
            dataGridViewOdbiorcy.Name = "dataGridViewOdbiorcy";
            dataGridViewOdbiorcy.RowTemplate.Height = 25;
            dataGridViewOdbiorcy.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            dataGridViewOdbiorcy.Size = new System.Drawing.Size(1713, 794);
            dataGridViewOdbiorcy.TabIndex = 2;
            dataGridViewOdbiorcy.CellEnter += dataGridViewOdbiorcy_CellEnter;
            dataGridViewOdbiorcy.CellValueChanged += dataGridViewOdbiorcy_CellValueChanged;
            dataGridViewOdbiorcy.CurrentCellDirtyStateChanged += dataGridViewOdbiorcy_CurrentCellDirtyStateChanged;
            dataGridViewOdbiorcy.RowPrePaint += dataGridViewOdbiorcy_RowPrePaint;
            // 
            // dataGridViewNotatki
            // 
            dataGridViewNotatki.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewNotatki.Location = new System.Drawing.Point(352, 9);
            dataGridViewNotatki.Name = "dataGridViewNotatki";
            dataGridViewNotatki.RowTemplate.Height = 25;
            dataGridViewNotatki.Size = new System.Drawing.Size(433, 241);
            dataGridViewNotatki.TabIndex = 3;
            // 
            // textBoxNotatka
            // 
            textBoxNotatka.Location = new System.Drawing.Point(14, 147);
            textBoxNotatka.Multiline = true;
            textBoxNotatka.Name = "textBoxNotatka";
            textBoxNotatka.Size = new System.Drawing.Size(240, 85);
            textBoxNotatka.TabIndex = 4;
            // 
            // buttonDodajNotatke
            // 
            buttonDodajNotatke.Location = new System.Drawing.Point(260, 189);
            buttonDodajNotatke.Name = "buttonDodajNotatke";
            buttonDodajNotatke.Size = new System.Drawing.Size(86, 43);
            buttonDodajNotatke.TabIndex = 5;
            buttonDodajNotatke.Text = "Dodaj notatke";
            buttonDodajNotatke.UseVisualStyleBackColor = true;
            buttonDodajNotatke.Click += buttonDodajNotatke_Click;
            // 
            // comboBoxStatusFilter
            // 
            comboBoxStatusFilter.FormattingEnabled = true;
            comboBoxStatusFilter.Location = new System.Drawing.Point(791, 43);
            comboBoxStatusFilter.Name = "comboBoxStatusFilter";
            comboBoxStatusFilter.Size = new System.Drawing.Size(174, 23);
            comboBoxStatusFilter.TabIndex = 6;
            comboBoxStatusFilter.SelectedIndexChanged += comboBoxStatusFilter_SelectedIndexChanged;
            // 
            // comboBoxPowiatFilter
            // 
            comboBoxPowiatFilter.FormattingEnabled = true;
            comboBoxPowiatFilter.Location = new System.Drawing.Point(791, 117);
            comboBoxPowiatFilter.Name = "comboBoxPowiatFilter";
            comboBoxPowiatFilter.Size = new System.Drawing.Size(174, 23);
            comboBoxPowiatFilter.TabIndex = 7;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(897, 9);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(68, 15);
            label1.TabIndex = 8;
            label1.Text = "Filtr : Status";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(893, 85);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(72, 15);
            label2.TabIndex = 9;
            label2.Text = "Filtr : Powiat";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new System.Drawing.Point(2, 9);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(49, 15);
            label3.TabIndex = 10;
            label3.Text = "Notatka";
            // 
            // dataGridViewRanking
            // 
            dataGridViewRanking.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewRanking.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewRanking.Location = new System.Drawing.Point(971, 9);
            dataGridViewRanking.Name = "dataGridViewRanking";
            dataGridViewRanking.RowTemplate.Height = 25;
            dataGridViewRanking.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            dataGridViewRanking.Size = new System.Drawing.Size(744, 241);
            dataGridViewRanking.TabIndex = 11;
            // 
            // button1
            // 
            button1.Location = new System.Drawing.Point(791, 207);
            button1.Name = "button1";
            button1.Size = new System.Drawing.Size(86, 43);
            button1.TabIndex = 12;
            button1.Text = "Odśwież";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // CRM
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1727, 1051);
            Controls.Add(button1);
            Controls.Add(dataGridViewRanking);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(comboBoxPowiatFilter);
            Controls.Add(comboBoxStatusFilter);
            Controls.Add(buttonDodajNotatke);
            Controls.Add(textBoxNotatka);
            Controls.Add(dataGridViewNotatki);
            Controls.Add(dataGridViewOdbiorcy);
            Name = "CRM";
            Text = " CRM";
            Load += CRM_Load;
            ((System.ComponentModel.ISupportInitialize)dataGridViewOdbiorcy).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewNotatki).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewRanking).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private System.Windows.Forms.DataGridView dataGridViewOdbiorcy;
        private System.Windows.Forms.DataGridView dataGridViewNotatki;
        private System.Windows.Forms.TextBox textBoxNotatka;
        private System.Windows.Forms.Button buttonDodajNotatke;
        private System.Windows.Forms.ComboBox comboBoxStatusFilter;
        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.ComboBox comboBoxPowiatFilter;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.DataGridView dataGridViewRanking;
        private System.Windows.Forms.Button button1;
    }
}