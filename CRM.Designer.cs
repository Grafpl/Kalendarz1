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
            buttonDodajNotatke = new System.Windows.Forms.Button();
            comboBoxStatusFilter = new System.Windows.Forms.ComboBox();
            comboBoxPowiatFilter = new System.Windows.Forms.ComboBox();
            label1 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            label3 = new System.Windows.Forms.Label();
            dataGridViewRanking = new System.Windows.Forms.DataGridView();
            button1 = new System.Windows.Forms.Button();
            comboBoxPKD = new System.Windows.Forms.ComboBox();
            label5 = new System.Windows.Forms.Label();
            comboBoxWoj = new System.Windows.Forms.ComboBox();
            button2 = new System.Windows.Forms.Button();
            button3 = new System.Windows.Forms.Button();
            textBoxNotatka = new System.Windows.Forms.TextBox();
            textBoxSzukaj = new System.Windows.Forms.TextBox();
            label4 = new System.Windows.Forms.Label();
            label6 = new System.Windows.Forms.Label();
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
            dataGridViewOdbiorcy.Size = new System.Drawing.Size(1410, 518);
            dataGridViewOdbiorcy.TabIndex = 2;
            dataGridViewOdbiorcy.CellEnter += dataGridViewOdbiorcy_CellEnter;
            dataGridViewOdbiorcy.CellValueChanged += dataGridViewOdbiorcy_CellValueChanged;
            dataGridViewOdbiorcy.CurrentCellDirtyStateChanged += dataGridViewOdbiorcy_CurrentCellDirtyStateChanged;
            dataGridViewOdbiorcy.RowPrePaint += dataGridViewOdbiorcy_RowPrePaint;
            // 
            // dataGridViewNotatki
            // 
            dataGridViewNotatki.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewNotatki.Location = new System.Drawing.Point(12, -2);
            dataGridViewNotatki.Name = "dataGridViewNotatki";
            dataGridViewNotatki.RowTemplate.Height = 25;
            dataGridViewNotatki.Size = new System.Drawing.Size(334, 161);
            dataGridViewNotatki.TabIndex = 3;
            // 
            // buttonDodajNotatke
            // 
            buttonDodajNotatke.Location = new System.Drawing.Point(166, 207);
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
            comboBoxStatusFilter.Location = new System.Drawing.Point(352, 41);
            comboBoxStatusFilter.Name = "comboBoxStatusFilter";
            comboBoxStatusFilter.Size = new System.Drawing.Size(174, 23);
            comboBoxStatusFilter.TabIndex = 6;
            comboBoxStatusFilter.SelectedIndexChanged += comboBoxStatusFilter_SelectedIndexChanged;
            // 
            // comboBoxPowiatFilter
            // 
            comboBoxPowiatFilter.FormattingEnabled = true;
            comboBoxPowiatFilter.Location = new System.Drawing.Point(352, 115);
            comboBoxPowiatFilter.Name = "comboBoxPowiatFilter";
            comboBoxPowiatFilter.Size = new System.Drawing.Size(174, 23);
            comboBoxPowiatFilter.TabIndex = 7;
            comboBoxPowiatFilter.SelectedIndexChanged += comboBoxPowiatFilter_SelectedIndexChanged;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(458, 7);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(68, 15);
            label1.TabIndex = 8;
            label1.Text = "Filtr : Status";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(454, 83);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(72, 15);
            label2.TabIndex = 9;
            label2.Text = "Filtr : Powiat";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new System.Drawing.Point(12, 230);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(49, 15);
            label3.TabIndex = 10;
            label3.Text = "Notatka";
            // 
            // dataGridViewRanking
            // 
            dataGridViewRanking.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewRanking.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewRanking.Location = new System.Drawing.Point(624, 1);
            dataGridViewRanking.Name = "dataGridViewRanking";
            dataGridViewRanking.RowTemplate.Height = 25;
            dataGridViewRanking.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            dataGridViewRanking.Size = new System.Drawing.Size(798, 220);
            dataGridViewRanking.TabIndex = 11;
            dataGridViewRanking.CellFormatting += DataGridViewRanking_CellFormatting;
            // 
            // button1
            // 
            button1.Location = new System.Drawing.Point(532, 167);
            button1.Name = "button1";
            button1.Size = new System.Drawing.Size(86, 43);
            button1.TabIndex = 12;
            button1.Text = "Odśwież";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // comboBoxPKD
            // 
            comboBoxPKD.FormattingEnabled = true;
            comboBoxPKD.Location = new System.Drawing.Point(352, 227);
            comboBoxPKD.Name = "comboBoxPKD";
            comboBoxPKD.Size = new System.Drawing.Size(559, 23);
            comboBoxPKD.TabIndex = 13;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new System.Drawing.Point(415, 152);
            label5.Name = "label5";
            label5.Size = new System.Drawing.Size(111, 15);
            label5.TabIndex = 16;
            label5.Text = "Filtr : Wojewodztwo";
            // 
            // comboBoxWoj
            // 
            comboBoxWoj.FormattingEnabled = true;
            comboBoxWoj.Location = new System.Drawing.Point(352, 187);
            comboBoxWoj.Name = "comboBoxWoj";
            comboBoxWoj.Size = new System.Drawing.Size(174, 23);
            comboBoxWoj.TabIndex = 15;
            // 
            // button2
            // 
            button2.Location = new System.Drawing.Point(917, 227);
            button2.Name = "button2";
            button2.Size = new System.Drawing.Size(86, 23);
            button2.TabIndex = 17;
            button2.Text = "Google";
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // button3
            // 
            button3.Location = new System.Drawing.Point(1009, 227);
            button3.Name = "button3";
            button3.Size = new System.Drawing.Size(86, 23);
            button3.TabIndex = 18;
            button3.Text = "Mapa";
            button3.UseVisualStyleBackColor = true;
            button3.Click += button3_Click;
            // 
            // textBoxNotatka
            // 
            textBoxNotatka.Location = new System.Drawing.Point(12, 165);
            textBoxNotatka.Multiline = true;
            textBoxNotatka.Name = "textBoxNotatka";
            textBoxNotatka.Size = new System.Drawing.Size(148, 85);
            textBoxNotatka.TabIndex = 4;
            // 
            // textBoxSzukaj
            // 
            textBoxSzukaj.Location = new System.Drawing.Point(1212, 228);
            textBoxSzukaj.Multiline = true;
            textBoxSzukaj.Name = "textBoxSzukaj";
            textBoxSzukaj.Size = new System.Drawing.Size(210, 22);
            textBoxSzukaj.TabIndex = 19;
            textBoxSzukaj.TextChanged += textBoxSzukaj_TextChanged;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new System.Drawing.Point(258, 230);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(71, 15);
            label4.TabIndex = 14;
            label4.Text = "Filtr : Rodzaj";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new System.Drawing.Point(1160, 231);
            label6.Name = "label6";
            label6.Size = new System.Drawing.Size(46, 15);
            label6.TabIndex = 20;
            label6.Text = "Szukaj :";
            label6.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // CRM
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1432, 780);
            Controls.Add(label6);
            Controls.Add(textBoxSzukaj);
            Controls.Add(button3);
            Controls.Add(button2);
            Controls.Add(label5);
            Controls.Add(comboBoxWoj);
            Controls.Add(label4);
            Controls.Add(comboBoxPKD);
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
        private System.Windows.Forms.Button buttonDodajNotatke;
        private System.Windows.Forms.ComboBox comboBoxStatusFilter;
        private System.Windows.Forms.ComboBox comboBoxWoj;
        private System.Windows.Forms.ComboBox comboBoxPowiatFilter;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.DataGridView dataGridViewRanking;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.ComboBox comboBoxPKD;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.TextBox textBoxNotatka;
        private System.Windows.Forms.TextBox textBoxSzukaj;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label6;
    }
}