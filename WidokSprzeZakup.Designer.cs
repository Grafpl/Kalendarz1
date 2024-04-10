namespace Kalendarz1
{
    partial class WidokSprzeZakup
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
            label1 = new System.Windows.Forms.Label();
            label9 = new System.Windows.Forms.Label();
            dataKoniec = new System.Windows.Forms.DateTimePicker();
            dataPoczatek = new System.Windows.Forms.DateTimePicker();
            groupBox1 = new System.Windows.Forms.GroupBox();
            dataGridView2 = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView2).BeginInit();
            SuspendLayout();
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Location = new System.Drawing.Point(12, 12);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowTemplate.Height = 25;
            dataGridView1.Size = new System.Drawing.Size(597, 661);
            dataGridView1.TabIndex = 0;
            // 
            // label1
            // 
            label1.Location = new System.Drawing.Point(6, 70);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(228, 22);
            label1.TabIndex = 25;
            label1.Text = "Końcowa data";
            label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label9
            // 
            label9.Location = new System.Drawing.Point(6, 21);
            label9.Name = "label9";
            label9.Size = new System.Drawing.Size(228, 22);
            label9.TabIndex = 24;
            label9.Text = "Początkowa data";
            label9.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // dataKoniec
            // 
            dataKoniec.Location = new System.Drawing.Point(6, 95);
            dataKoniec.Name = "dataKoniec";
            dataKoniec.Size = new System.Drawing.Size(228, 23);
            dataKoniec.TabIndex = 23;
            dataKoniec.ValueChanged += dataKoniec_ValueChanged;
            // 
            // dataPoczatek
            // 
            dataPoczatek.Location = new System.Drawing.Point(6, 43);
            dataPoczatek.Name = "dataPoczatek";
            dataPoczatek.Size = new System.Drawing.Size(228, 23);
            dataPoczatek.TabIndex = 22;
            dataPoczatek.ValueChanged += dataPoczatek_ValueChanged;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(dataKoniec);
            groupBox1.Controls.Add(label1);
            groupBox1.Controls.Add(dataPoczatek);
            groupBox1.Controls.Add(label9);
            groupBox1.Location = new System.Drawing.Point(615, 12);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new System.Drawing.Size(245, 132);
            groupBox1.TabIndex = 26;
            groupBox1.TabStop = false;
            groupBox1.Text = "Przedział dni";
            // 
            // dataGridView2
            // 
            dataGridView2.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView2.Location = new System.Drawing.Point(866, 12);
            dataGridView2.Name = "dataGridView2";
            dataGridView2.RowTemplate.Height = 25;
            dataGridView2.Size = new System.Drawing.Size(597, 661);
            dataGridView2.TabIndex = 27;
            // 
            // WidokSprzeZakup
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1597, 685);
            Controls.Add(dataGridView2);
            Controls.Add(groupBox1);
            Controls.Add(dataGridView1);
            Name = "WidokSprzeZakup";
            Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            groupBox1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridView2).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.DateTimePicker dataKoniec;
        private System.Windows.Forms.DateTimePicker dataPoczatek;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.DataGridView dataGridView2;
    }
}