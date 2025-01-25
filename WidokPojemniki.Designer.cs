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
            label2 = new System.Windows.Forms.Label();
            label1 = new System.Windows.Forms.Label();
            label3 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)dataGridViewZestawienie).BeginInit();
            SuspendLayout();
            // 
            // dataGridViewZestawienie
            // 
            dataGridViewZestawienie.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewZestawienie.Location = new System.Drawing.Point(12, 69);
            dataGridViewZestawienie.Name = "dataGridViewZestawienie";
            dataGridViewZestawienie.RowTemplate.Height = 25;
            dataGridViewZestawienie.Size = new System.Drawing.Size(730, 582);
            dataGridViewZestawienie.TabIndex = 0;
            // 
            // comboBoxKontrahent
            // 
            comboBoxKontrahent.FormattingEnabled = true;
            comboBoxKontrahent.Location = new System.Drawing.Point(12, 40);
            comboBoxKontrahent.Name = "comboBoxKontrahent";
            comboBoxKontrahent.Size = new System.Drawing.Size(195, 23);
            comboBoxKontrahent.TabIndex = 1;
            // 
            // dateTimePickerOd
            // 
            dateTimePickerOd.Location = new System.Drawing.Point(213, 40);
            dateTimePickerOd.Name = "dateTimePickerOd";
            dateTimePickerOd.Size = new System.Drawing.Size(218, 23);
            dateTimePickerOd.TabIndex = 2;
            dateTimePickerOd.Value = new System.DateTime(2024, 12, 31, 0, 0, 0, 0);
            dateTimePickerOd.ValueChanged += dateTimePickerOd_ValueChanged;
            // 
            // dateTimePickerDo
            // 
            dateTimePickerDo.Location = new System.Drawing.Point(437, 40);
            dateTimePickerDo.Name = "dateTimePickerDo";
            dateTimePickerDo.Size = new System.Drawing.Size(218, 23);
            dateTimePickerDo.TabIndex = 3;
            // 
            // btnSearch
            // 
            btnSearch.Location = new System.Drawing.Point(661, 10);
            btnSearch.Name = "btnSearch";
            btnSearch.Size = new System.Drawing.Size(58, 23);
            btnSearch.TabIndex = 4;
            btnSearch.Text = "Odśwież";
            btnSearch.UseVisualStyleBackColor = true;
            btnSearch.Click += btnSearch_Click;
            // 
            // btnGeneratePDF
            // 
            btnGeneratePDF.Location = new System.Drawing.Point(661, 39);
            btnGeneratePDF.Name = "btnGeneratePDF";
            btnGeneratePDF.Size = new System.Drawing.Size(81, 23);
            btnGeneratePDF.TabIndex = 5;
            btnGeneratePDF.Text = "PDF i Wyślij";
            btnGeneratePDF.UseVisualStyleBackColor = true;
            btnGeneratePDF.Click += btnGeneratePDF_Click;
            // 
            // label2
            // 
            label2.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label2.Location = new System.Drawing.Point(437, 3);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(218, 34);
            label2.TabIndex = 74;
            label2.Text = "Saldo na dzień:";
            label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label1
            // 
            label1.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label1.Location = new System.Drawing.Point(213, 3);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(218, 34);
            label1.TabIndex = 75;
            label1.Text = "Od dnia:";
            label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label3
            // 
            label3.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label3.Location = new System.Drawing.Point(12, 3);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(195, 34);
            label3.TabIndex = 76;
            label3.Text = "Odbiorca";
            label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // WidokPojemniki
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(746, 663);
            Controls.Add(label3);
            Controls.Add(label1);
            Controls.Add(label2);
            Controls.Add(btnGeneratePDF);
            Controls.Add(btnSearch);
            Controls.Add(dateTimePickerDo);
            Controls.Add(dateTimePickerOd);
            Controls.Add(comboBoxKontrahent);
            Controls.Add(dataGridViewZestawienie);
            Name = "WidokPojemniki";
            Text = "Salda opakowań";
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
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label3;
    }
}