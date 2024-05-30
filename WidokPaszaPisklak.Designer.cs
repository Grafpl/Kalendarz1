namespace Kalendarz1
{
    partial class WidokPaszaPisklak
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
            label18 = new System.Windows.Forms.Label();
            textBox1 = new System.Windows.Forms.TextBox();
            dataGridView1 = new System.Windows.Forms.DataGridView();
            checkBoxGroupBySupplier = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            SuspendLayout();
            // 
            // label18
            // 
            label18.Location = new System.Drawing.Point(12, -3);
            label18.Name = "label18";
            label18.Size = new System.Drawing.Size(100, 16);
            label18.TabIndex = 43;
            label18.Text = "Szukaj";
            label18.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // textBox1
            // 
            textBox1.Location = new System.Drawing.Point(12, 16);
            textBox1.Name = "textBox1";
            textBox1.Size = new System.Drawing.Size(100, 23);
            textBox1.TabIndex = 42;
            textBox1.TextChanged += textBox1_TextChanged;
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Location = new System.Drawing.Point(12, 45);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowTemplate.Height = 25;
            dataGridView1.Size = new System.Drawing.Size(867, 649);
            dataGridView1.TabIndex = 41;
            // 
            // checkBoxGroupBySupplier
            // 
            checkBoxGroupBySupplier.AutoSize = true;
            checkBoxGroupBySupplier.Font = new System.Drawing.Font("Segoe UI", 6F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            checkBoxGroupBySupplier.Location = new System.Drawing.Point(127, 16);
            checkBoxGroupBySupplier.Name = "checkBoxGroupBySupplier";
            checkBoxGroupBySupplier.Size = new System.Drawing.Size(49, 15);
            checkBoxGroupBySupplier.TabIndex = 120;
            checkBoxGroupBySupplier.Text = "Grupuj";
            checkBoxGroupBySupplier.UseVisualStyleBackColor = true;
            // 
            // WidokWaga
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(882, 706);
            Controls.Add(checkBoxGroupBySupplier);
            Controls.Add(label18);
            Controls.Add(textBox1);
            Controls.Add(dataGridView1);
            Name = "WidokWaga";
            Text = "Wagi";
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.CheckBox checkBoxGroupBySupplier;
    }
}