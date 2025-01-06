namespace Kalendarz1
{
    partial class WidokZamowienia
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
            comboBoxOdbiorca = new System.Windows.Forms.ComboBox();
            textBoxNazwaOdbiorca = new System.Windows.Forms.TextBox();
            label1 = new System.Windows.Forms.Label();
            textBoxNIP = new System.Windows.Forms.TextBox();
            textBoxKod = new System.Windows.Forms.TextBox();
            textBoxMiejscowosc = new System.Windows.Forms.TextBox();
            textBoxHandlowiec = new System.Windows.Forms.TextBox();
            textBoxSuma = new System.Windows.Forms.TextBox();
            label8 = new System.Windows.Forms.Label();
            groupBox1 = new System.Windows.Forms.GroupBox();
            groupBox2 = new System.Windows.Forms.GroupBox();
            dateTimePickerGodzinaPrzyjazdu = new System.Windows.Forms.DateTimePicker();
            dateTimePickerSprzedaz = new System.Windows.Forms.DateTimePicker();
            label2 = new System.Windows.Forms.Label();
            textBoxLimit = new System.Windows.Forms.TextBox();
            groupBox3 = new System.Windows.Forms.GroupBox();
            textBox2 = new System.Windows.Forms.TextBox();
            textBox1 = new System.Windows.Forms.TextBox();
            label6 = new System.Windows.Forms.Label();
            label7 = new System.Windows.Forms.Label();
            label9 = new System.Windows.Forms.Label();
            dataGridViewZamowienie = new System.Windows.Forms.DataGridView();
            cancelButton = new System.Windows.Forms.Button();
            CommandButton_Update = new System.Windows.Forms.Button();
            textBoxUwagi = new System.Windows.Forms.TextBox();
            label4 = new System.Windows.Forms.Label();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            groupBox3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewZamowienie).BeginInit();
            SuspendLayout();
            // 
            // comboBoxOdbiorca
            // 
            comboBoxOdbiorca.FormattingEnabled = true;
            comboBoxOdbiorca.Location = new System.Drawing.Point(8, 22);
            comboBoxOdbiorca.Name = "comboBoxOdbiorca";
            comboBoxOdbiorca.Size = new System.Drawing.Size(429, 23);
            comboBoxOdbiorca.TabIndex = 0;
            comboBoxOdbiorca.SelectedIndexChanged += comboBoxOdbiorca_SelectedIndexChanged;
            // 
            // textBoxNazwaOdbiorca
            // 
            textBoxNazwaOdbiorca.BackColor = System.Drawing.Color.FromArgb(224, 224, 224);
            textBoxNazwaOdbiorca.Location = new System.Drawing.Point(8, 54);
            textBoxNazwaOdbiorca.Name = "textBoxNazwaOdbiorca";
            textBoxNazwaOdbiorca.Size = new System.Drawing.Size(429, 23);
            textBoxNazwaOdbiorca.TabIndex = 2;
            // 
            // label1
            // 
            label1.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label1.Location = new System.Drawing.Point(6, 36);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(130, 25);
            label1.TabIndex = 109;
            label1.Text = "Data Sprzedaży :";
            label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // textBoxNIP
            // 
            textBoxNIP.BackColor = System.Drawing.Color.FromArgb(224, 224, 224);
            textBoxNIP.Location = new System.Drawing.Point(244, 83);
            textBoxNIP.Name = "textBoxNIP";
            textBoxNIP.Size = new System.Drawing.Size(94, 23);
            textBoxNIP.TabIndex = 111;
            // 
            // textBoxKod
            // 
            textBoxKod.BackColor = System.Drawing.Color.FromArgb(224, 224, 224);
            textBoxKod.Location = new System.Drawing.Point(8, 83);
            textBoxKod.Name = "textBoxKod";
            textBoxKod.Size = new System.Drawing.Size(68, 23);
            textBoxKod.TabIndex = 113;
            // 
            // textBoxMiejscowosc
            // 
            textBoxMiejscowosc.BackColor = System.Drawing.Color.FromArgb(224, 224, 224);
            textBoxMiejscowosc.Location = new System.Drawing.Point(82, 83);
            textBoxMiejscowosc.Name = "textBoxMiejscowosc";
            textBoxMiejscowosc.Size = new System.Drawing.Size(156, 23);
            textBoxMiejscowosc.TabIndex = 115;
            // 
            // textBoxHandlowiec
            // 
            textBoxHandlowiec.BackColor = System.Drawing.Color.FromArgb(224, 224, 224);
            textBoxHandlowiec.Location = new System.Drawing.Point(344, 83);
            textBoxHandlowiec.Name = "textBoxHandlowiec";
            textBoxHandlowiec.Size = new System.Drawing.Size(93, 23);
            textBoxHandlowiec.TabIndex = 117;
            // 
            // textBoxSuma
            // 
            textBoxSuma.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            textBoxSuma.Location = new System.Drawing.Point(849, 414);
            textBoxSuma.Name = "textBoxSuma";
            textBoxSuma.Size = new System.Drawing.Size(130, 25);
            textBoxSuma.TabIndex = 120;
            textBoxSuma.TabStop = false;
            textBoxSuma.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // label8
            // 
            label8.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label8.Location = new System.Drawing.Point(849, 386);
            label8.Name = "label8";
            label8.Size = new System.Drawing.Size(130, 25);
            label8.TabIndex = 123;
            label8.Text = "Wartość zamówienia";
            label8.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(comboBoxOdbiorca);
            groupBox1.Controls.Add(textBoxNazwaOdbiorca);
            groupBox1.Controls.Add(textBoxNIP);
            groupBox1.Controls.Add(textBoxMiejscowosc);
            groupBox1.Controls.Add(textBoxKod);
            groupBox1.Controls.Add(textBoxHandlowiec);
            groupBox1.Location = new System.Drawing.Point(12, 12);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new System.Drawing.Size(443, 114);
            groupBox1.TabIndex = 126;
            groupBox1.TabStop = false;
            groupBox1.Text = "Odbiorca";
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(dateTimePickerGodzinaPrzyjazdu);
            groupBox2.Controls.Add(dateTimePickerSprzedaz);
            groupBox2.Controls.Add(label2);
            groupBox2.Controls.Add(label1);
            groupBox2.Location = new System.Drawing.Point(461, 12);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new System.Drawing.Size(376, 114);
            groupBox2.TabIndex = 127;
            groupBox2.TabStop = false;
            groupBox2.Text = "Data";
            // 
            // dateTimePickerGodzinaPrzyjazdu
            // 
            dateTimePickerGodzinaPrzyjazdu.Location = new System.Drawing.Point(142, 70);
            dateTimePickerGodzinaPrzyjazdu.Name = "dateTimePickerGodzinaPrzyjazdu";
            dateTimePickerGodzinaPrzyjazdu.Size = new System.Drawing.Size(86, 23);
            dateTimePickerGodzinaPrzyjazdu.TabIndex = 113;
            // 
            // dateTimePickerSprzedaz
            // 
            dateTimePickerSprzedaz.Location = new System.Drawing.Point(142, 38);
            dateTimePickerSprzedaz.Name = "dateTimePickerSprzedaz";
            dateTimePickerSprzedaz.Size = new System.Drawing.Size(228, 23);
            dateTimePickerSprzedaz.TabIndex = 112;
            // 
            // label2
            // 
            label2.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label2.Location = new System.Drawing.Point(6, 68);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(130, 25);
            label2.TabIndex = 110;
            label2.Text = "Godzina Odbioru :";
            label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // textBoxLimit
            // 
            textBoxLimit.BackColor = System.Drawing.Color.FromArgb(224, 224, 224);
            textBoxLimit.Location = new System.Drawing.Point(142, 20);
            textBoxLimit.Name = "textBoxLimit";
            textBoxLimit.Size = new System.Drawing.Size(100, 23);
            textBoxLimit.TabIndex = 3;
            // 
            // groupBox3
            // 
            groupBox3.Controls.Add(textBox2);
            groupBox3.Controls.Add(textBox1);
            groupBox3.Controls.Add(label6);
            groupBox3.Controls.Add(label7);
            groupBox3.Controls.Add(label9);
            groupBox3.Controls.Add(textBoxLimit);
            groupBox3.Location = new System.Drawing.Point(843, 12);
            groupBox3.Name = "groupBox3";
            groupBox3.Size = new System.Drawing.Size(250, 114);
            groupBox3.TabIndex = 128;
            groupBox3.TabStop = false;
            groupBox3.Text = "Limit handlowy";
            // 
            // textBox2
            // 
            textBox2.BackColor = System.Drawing.Color.FromArgb(224, 224, 224);
            textBox2.Location = new System.Drawing.Point(142, 83);
            textBox2.Name = "textBox2";
            textBox2.Size = new System.Drawing.Size(100, 23);
            textBox2.TabIndex = 113;
            // 
            // textBox1
            // 
            textBox1.BackColor = System.Drawing.Color.FromArgb(224, 224, 224);
            textBox1.Location = new System.Drawing.Point(142, 52);
            textBox1.Name = "textBox1";
            textBox1.Size = new System.Drawing.Size(100, 23);
            textBox1.TabIndex = 112;
            // 
            // label6
            // 
            label6.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label6.Location = new System.Drawing.Point(6, 83);
            label6.Name = "label6";
            label6.Size = new System.Drawing.Size(130, 25);
            label6.TabIndex = 111;
            label6.Text = "Różnica :";
            label6.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label7
            // 
            label7.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label7.Location = new System.Drawing.Point(6, 52);
            label7.Name = "label7";
            label7.Size = new System.Drawing.Size(130, 25);
            label7.TabIndex = 110;
            label7.Text = "Bieżący należnowści :";
            label7.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label9
            // 
            label9.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label9.Location = new System.Drawing.Point(6, 20);
            label9.Name = "label9";
            label9.Size = new System.Drawing.Size(130, 25);
            label9.TabIndex = 109;
            label9.Text = "Limit Odbiorcy :";
            label9.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // dataGridViewZamowienie
            // 
            dataGridViewZamowienie.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewZamowienie.Location = new System.Drawing.Point(20, 142);
            dataGridViewZamowienie.Name = "dataGridViewZamowienie";
            dataGridViewZamowienie.RowTemplate.Height = 25;
            dataGridViewZamowienie.Size = new System.Drawing.Size(817, 299);
            dataGridViewZamowienie.TabIndex = 129;
            // 
            // cancelButton
            // 
            cancelButton.BackColor = System.Drawing.Color.IndianRed;
            cancelButton.Location = new System.Drawing.Point(985, 400);
            cancelButton.Name = "cancelButton";
            cancelButton.Size = new System.Drawing.Size(57, 39);
            cancelButton.TabIndex = 131;
            cancelButton.Text = "Anuluj";
            cancelButton.UseVisualStyleBackColor = false;
            // 
            // CommandButton_Update
            // 
            CommandButton_Update.BackColor = System.Drawing.Color.Chartreuse;
            CommandButton_Update.Location = new System.Drawing.Point(1048, 389);
            CommandButton_Update.Name = "CommandButton_Update";
            CommandButton_Update.Size = new System.Drawing.Size(75, 52);
            CommandButton_Update.TabIndex = 130;
            CommandButton_Update.Text = "Stwórz";
            CommandButton_Update.UseVisualStyleBackColor = false;
            CommandButton_Update.Click += CommandButton_Update_Click;
            // 
            // textBoxUwagi
            // 
            textBoxUwagi.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            textBoxUwagi.Location = new System.Drawing.Point(879, 277);
            textBoxUwagi.Multiline = true;
            textBoxUwagi.Name = "textBoxUwagi";
            textBoxUwagi.Size = new System.Drawing.Size(244, 106);
            textBoxUwagi.TabIndex = 132;
            textBoxUwagi.TabStop = false;
            textBoxUwagi.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // label4
            // 
            label4.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label4.Location = new System.Drawing.Point(879, 249);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(244, 25);
            label4.TabIndex = 133;
            label4.Text = "Notatka";
            label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // WidokZamowienia
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1135, 451);
            Controls.Add(label4);
            Controls.Add(textBoxUwagi);
            Controls.Add(cancelButton);
            Controls.Add(CommandButton_Update);
            Controls.Add(dataGridViewZamowienie);
            Controls.Add(groupBox3);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Controls.Add(textBoxSuma);
            Controls.Add(label8);
            Name = "WidokZamowienia";
            Text = "data";
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox3.ResumeLayout(false);
            groupBox3.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewZamowienie).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.ComboBox comboBoxOdbiorca;
        private System.Windows.Forms.TextBox textBoxNazwaOdbiorca;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBoxNIP;
        private System.Windows.Forms.TextBox textBoxKod;
        private System.Windows.Forms.TextBox textBoxMiejscowosc;
        private System.Windows.Forms.TextBox textBoxHandlowiec;
        private System.Windows.Forms.TextBox textBoxSuma;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textBoxLimit;
        private System.Windows.Forms.DateTimePicker dateTimePickerGodzinaPrzyjazdu;
        private System.Windows.Forms.DateTimePicker dateTimePickerSprzedaz;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.DataGridView dataGridViewZamowienie;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Button CommandButton_Update;
        private System.Windows.Forms.TextBox textBoxUwagi;
        private System.Windows.Forms.Label label4;
    }
}