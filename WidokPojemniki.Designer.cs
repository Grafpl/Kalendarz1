namespace Kalendarz1
{
    partial class WidokPojemniki
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
            btnSearch.Size = new System.Drawing.Size(143, 23);
            btnSearch.TabIndex = 4;
            btnSearch.Text = "Odśwież";
            btnSearch.UseVisualStyleBackColor = true;
            btnSearch.Click += btnSearch_Click;
            // 
            // btnGeneratePDF
            // 
            btnGeneratePDF.Location = new System.Drawing.Point(972, 28);
            btnGeneratePDF.Name = "btnGeneratePDF";
            btnGeneratePDF.Size = new System.Drawing.Size(143, 23);
            btnGeneratePDF.TabIndex = 5;
            btnGeneratePDF.Text = "PDF i Wyślij";
            btnGeneratePDF.UseVisualStyleBackColor = true;
            btnGeneratePDF.Click += btnGeneratePDF_Click;
            // 
            // WidokPojemniki
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1315, 760);
            Controls.Add(btnGeneratePDF);
            Controls.Add(btnSearch);
            Controls.Add(dateTimePickerDo);
            Controls.Add(dateTimePickerOd);
            Controls.Add(comboBoxKontrahent);
            Controls.Add(dataGridViewZestawienie);
            Name = "WidokPojemniki";
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
    }
}