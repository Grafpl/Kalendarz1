namespace Kalendarz1
{
    partial class WidokKontrahenci
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
            dataGridView1 = new System.Windows.Forms.DataGridView();
            label18 = new System.Windows.Forms.Label();
            textBox1 = new System.Windows.Forms.TextBox();
            buttonSprawdzDuplikaty = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            SuspendLayout();
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Location = new System.Drawing.Point(2, 59);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowTemplate.Height = 25;
            dataGridView1.Size = new System.Drawing.Size(1845, 626);
            dataGridView1.TabIndex = 0;
            dataGridView1.CellEndEdit += dataGridView1_CellEndEdit;
            dataGridView1.CellFormatting += dataGridView1_CellFormatting;
            // 
            // label18
            // 
            label18.Location = new System.Drawing.Point(2, 2);
            label18.Name = "label18";
            label18.Size = new System.Drawing.Size(100, 25);
            label18.TabIndex = 31;
            label18.Text = "Szukaj";
            label18.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // textBox1
            // 
            textBox1.Location = new System.Drawing.Point(2, 30);
            textBox1.Name = "textBox1";
            textBox1.Size = new System.Drawing.Size(100, 23);
            textBox1.TabIndex = 30;
            textBox1.TextChanged += textBox1_TextChanged;
            // 
            // buttonSprawdzDuplikaty
            // 
            buttonSprawdzDuplikaty.Location = new System.Drawing.Point(108, 29);
            buttonSprawdzDuplikaty.Name = "buttonSprawdzDuplikaty";
            buttonSprawdzDuplikaty.Size = new System.Drawing.Size(114, 23);
            buttonSprawdzDuplikaty.TabIndex = 32;
            buttonSprawdzDuplikaty.Text = "Sprawdź Duplikaty";
            buttonSprawdzDuplikaty.UseVisualStyleBackColor = true;
            buttonSprawdzDuplikaty.Click += buttonSprawdzDuplikaty_Click;
            // 
            // WidokKontrahenci
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1859, 697);
            Controls.Add(buttonSprawdzDuplikaty);
            Controls.Add(label18);
            Controls.Add(textBox1);
            Controls.Add(dataGridView1);
            Name = "WidokKontrahenci";
            Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Button buttonSprawdzDuplikaty;
    }
}