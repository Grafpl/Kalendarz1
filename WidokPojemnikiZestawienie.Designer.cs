namespace Kalendarz1
{
    partial class WidokPojemnikiZestawienie
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
            dataGridViewZestawienie = new System.Windows.Forms.DataGridView();
            comboBoxKontrahent = new System.Windows.Forms.ComboBox();
            dateTimePickerOd = new System.Windows.Forms.DateTimePicker();
            dateTimePickerDo = new System.Windows.Forms.DateTimePicker();
            btnSearch = new System.Windows.Forms.Button();
            btnGeneratePDF = new System.Windows.Forms.Button();
            button1 = new System.Windows.Forms.Button();
            button2 = new System.Windows.Forms.Button();
            button3 = new System.Windows.Forms.Button();
            button4 = new System.Windows.Forms.Button();
            label2 = new System.Windows.Forms.Label();
            label1 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)dataGridViewZestawienie).BeginInit();
            SuspendLayout();
            // 
            // dataGridViewZestawienie
            // 
            dataGridViewZestawienie.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewZestawienie.Location = new System.Drawing.Point(12, 75);
            dataGridViewZestawienie.Name = "dataGridViewZestawienie";
            dataGridViewZestawienie.RowTemplate.Height = 25;
            dataGridViewZestawienie.Size = new System.Drawing.Size(551, 585);
            dataGridViewZestawienie.TabIndex = 0;
            // 
            // comboBoxKontrahent
            // 
            comboBoxKontrahent.FormattingEnabled = true;
            comboBoxKontrahent.Location = new System.Drawing.Point(616, 404);
            comboBoxKontrahent.Name = "comboBoxKontrahent";
            comboBoxKontrahent.Size = new System.Drawing.Size(10, 23);
            comboBoxKontrahent.TabIndex = 1;
            // 
            // dateTimePickerOd
            // 
            dateTimePickerOd.CalendarMonthBackground = System.Drawing.SystemColors.ScrollBar;
            dateTimePickerOd.CalendarTrailingForeColor = System.Drawing.Color.Gray;
            dateTimePickerOd.Location = new System.Drawing.Point(11, 46);
            dateTimePickerOd.Name = "dateTimePickerOd";
            dateTimePickerOd.Size = new System.Drawing.Size(273, 23);
            dateTimePickerOd.TabIndex = 2;
            dateTimePickerOd.Value = new System.DateTime(2024, 12, 31, 0, 0, 0, 0);
            // 
            // dateTimePickerDo
            // 
            dateTimePickerDo.Location = new System.Drawing.Point(290, 46);
            dateTimePickerDo.Name = "dateTimePickerDo";
            dateTimePickerDo.Size = new System.Drawing.Size(273, 23);
            dateTimePickerDo.TabIndex = 3;
            // 
            // btnSearch
            // 
            btnSearch.BackColor = System.Drawing.Color.White;
            btnSearch.Image = Properties.Resources.PojemnikE2;
            btnSearch.Location = new System.Drawing.Point(569, 75);
            btnSearch.Name = "btnSearch";
            btnSearch.Size = new System.Drawing.Size(130, 56);
            btnSearch.TabIndex = 4;
            btnSearch.UseVisualStyleBackColor = false;
            btnSearch.Click += btnSearch_Click;
            // 
            // btnGeneratePDF
            // 
            btnGeneratePDF.Location = new System.Drawing.Point(569, 17);
            btnGeneratePDF.Name = "btnGeneratePDF";
            btnGeneratePDF.Size = new System.Drawing.Size(128, 52);
            btnGeneratePDF.TabIndex = 5;
            btnGeneratePDF.Text = "PDF i Wyślij";
            btnGeneratePDF.UseVisualStyleBackColor = true;
            // 
            // button1
            // 
            button1.BackColor = System.Drawing.Color.White;
            button1.Image = Properties.Resources.PaletaH1;
            button1.Location = new System.Drawing.Point(569, 133);
            button1.Name = "button1";
            button1.Size = new System.Drawing.Size(130, 56);
            button1.TabIndex = 6;
            button1.UseVisualStyleBackColor = false;
            button1.Click += button1_Click;
            // 
            // button2
            // 
            button2.BackColor = System.Drawing.Color.White;
            button2.Image = Properties.Resources.PaletaEuro;
            button2.Location = new System.Drawing.Point(569, 196);
            button2.Name = "button2";
            button2.Size = new System.Drawing.Size(130, 56);
            button2.TabIndex = 7;
            button2.UseVisualStyleBackColor = false;
            button2.Click += button2_Click;
            // 
            // button3
            // 
            button3.BackColor = System.Drawing.Color.White;
            button3.Image = Properties.Resources.PaletaPlast;
            button3.Location = new System.Drawing.Point(569, 258);
            button3.Name = "button3";
            button3.Size = new System.Drawing.Size(130, 56);
            button3.TabIndex = 8;
            button3.UseVisualStyleBackColor = false;
            button3.Click += button3_Click;
            // 
            // button4
            // 
            button4.BackColor = System.Drawing.Color.White;
            button4.Image = Properties.Resources.PaletaDrewniana;
            button4.Location = new System.Drawing.Point(569, 320);
            button4.Name = "button4";
            button4.Size = new System.Drawing.Size(130, 56);
            button4.TabIndex = 9;
            button4.UseVisualStyleBackColor = false;
            button4.Click += button4_Click;
            // 
            // label2
            // 
            label2.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label2.Location = new System.Drawing.Point(11, 9);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(174, 34);
            label2.TabIndex = 73;
            label2.Text = "Do pierwszego zakresu";
            label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label1
            // 
            label1.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label1.Location = new System.Drawing.Point(290, 9);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(160, 34);
            label1.TabIndex = 74;
            label1.Text = "Do drugiego zakresu";
            label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // WidokPojemnikiZestawienie
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(701, 665);
            Controls.Add(label1);
            Controls.Add(label2);
            Controls.Add(button4);
            Controls.Add(button3);
            Controls.Add(button2);
            Controls.Add(button1);
            Controls.Add(btnGeneratePDF);
            Controls.Add(btnSearch);
            Controls.Add(dateTimePickerDo);
            Controls.Add(dateTimePickerOd);
            Controls.Add(comboBoxKontrahent);
            Controls.Add(dataGridViewZestawienie);
            Name = "WidokPojemnikiZestawienie";
            Text = "Zestawienie Opakowań";
            ((System.ComponentModel.ISupportInitialize)dataGridViewZestawienie).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridViewZestawienie;
        private System.Windows.Forms.ComboBox comboBoxKontrahent;
        private System.Windows.Forms.DateTimePicker dateTimePickerOd;
        private System.Windows.Forms.DateTimePicker dateTimePickerDo;
        private System.Windows.Forms.Button btnSearch;
        private System.Windows.Forms.Button btnGeneratePDF;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Button button4;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
    }
}