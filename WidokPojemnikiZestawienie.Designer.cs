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
            ((System.ComponentModel.ISupportInitialize)dataGridViewZestawienie).BeginInit();
            SuspendLayout();
            // 
            // dataGridViewZestawienie
            // 
            dataGridViewZestawienie.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewZestawienie.Location = new System.Drawing.Point(12, 57);
            dataGridViewZestawienie.Name = "dataGridViewZestawienie";
            dataGridViewZestawienie.RowTemplate.Height = 25;
            dataGridViewZestawienie.Size = new System.Drawing.Size(1262, 691);
            dataGridViewZestawienie.TabIndex = 0;
            // 
            // comboBoxKontrahent
            // 
            comboBoxKontrahent.FormattingEnabled = true;
            comboBoxKontrahent.Location = new System.Drawing.Point(12, 28);
            comboBoxKontrahent.Name = "comboBoxKontrahent";
            comboBoxKontrahent.Size = new System.Drawing.Size(247, 23);
            comboBoxKontrahent.TabIndex = 1;
            // 
            // dateTimePickerOd
            // 
            dateTimePickerOd.Location = new System.Drawing.Point(265, 28);
            dateTimePickerOd.Name = "dateTimePickerOd";
            dateTimePickerOd.Size = new System.Drawing.Size(273, 23);
            dateTimePickerOd.TabIndex = 2;
            // 
            // dateTimePickerDo
            // 
            dateTimePickerDo.Location = new System.Drawing.Point(544, 28);
            dateTimePickerDo.Name = "dateTimePickerDo";
            dateTimePickerDo.Size = new System.Drawing.Size(273, 23);
            dateTimePickerDo.TabIndex = 3;
            // 
            // btnSearch
            // 
            btnSearch.Location = new System.Drawing.Point(823, 27);
            btnSearch.Name = "btnSearch";
            btnSearch.Size = new System.Drawing.Size(51, 23);
            btnSearch.TabIndex = 4;
            btnSearch.Text = "E2";
            btnSearch.UseVisualStyleBackColor = true;
            btnSearch.Click += btnSearch_Click;
            // 
            // btnGeneratePDF
            // 
            btnGeneratePDF.Location = new System.Drawing.Point(1160, 27);
            btnGeneratePDF.Name = "btnGeneratePDF";
            btnGeneratePDF.Size = new System.Drawing.Size(143, 23);
            btnGeneratePDF.TabIndex = 5;
            btnGeneratePDF.Text = "PDF i Wyślij";
            btnGeneratePDF.UseVisualStyleBackColor = true;
            // 
            // button1
            // 
            button1.Location = new System.Drawing.Point(880, 27);
            button1.Name = "button1";
            button1.Size = new System.Drawing.Size(51, 23);
            button1.TabIndex = 6;
            button1.Text = "H1";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // button2
            // 
            button2.Location = new System.Drawing.Point(937, 27);
            button2.Name = "button2";
            button2.Size = new System.Drawing.Size(51, 23);
            button2.TabIndex = 7;
            button2.Text = "Euro";
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // button3
            // 
            button3.Location = new System.Drawing.Point(1056, 27);
            button3.Name = "button3";
            button3.Size = new System.Drawing.Size(56, 23);
            button3.TabIndex = 8;
            button3.Text = "Plast.";
            button3.UseVisualStyleBackColor = true;
            button3.Click += button3_Click;
            // 
            // button4
            // 
            button4.Location = new System.Drawing.Point(994, 28);
            button4.Name = "button4";
            button4.Size = new System.Drawing.Size(56, 23);
            button4.TabIndex = 9;
            button4.Text = "Drewno";
            button4.UseVisualStyleBackColor = true;
            button4.Click += button4_Click;
            // 
            // WidokPojemnikiZestawienie
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1315, 760);
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
            Text = "WidokPojemniki";
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
    }
}