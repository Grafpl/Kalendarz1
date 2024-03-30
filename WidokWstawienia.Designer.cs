namespace Kalendarz1
{
    partial class WidokWstawienia
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
            textBox1 = new System.Windows.Forms.TextBox();
            label18 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            sumaSztuk = new System.Windows.Forms.TextBox();
            dataGridView2 = new System.Windows.Forms.DataGridView();
            label1 = new System.Windows.Forms.Label();
            dataGridView3 = new System.Windows.Forms.DataGridView();
            label3 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView2).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView3).BeginInit();
            SuspendLayout();
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Location = new System.Drawing.Point(12, 12);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowTemplate.Height = 25;
            dataGridView1.Size = new System.Drawing.Size(867, 642);
            dataGridView1.TabIndex = 0;
            dataGridView1.CellClick += dataGridView1_CellClick;
            dataGridView1.CellFormatting += dataGridView1_CellFormatting_1;
            // 
            // textBox1
            // 
            textBox1.Location = new System.Drawing.Point(885, 40);
            textBox1.Name = "textBox1";
            textBox1.Size = new System.Drawing.Size(100, 23);
            textBox1.TabIndex = 1;
            textBox1.TextChanged += textBox1_TextChanged;
            // 
            // label18
            // 
            label18.Location = new System.Drawing.Point(885, 12);
            label18.Name = "label18";
            label18.Size = new System.Drawing.Size(100, 25);
            label18.TabIndex = 29;
            label18.Text = "Szukaj";
            label18.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label2
            // 
            label2.Location = new System.Drawing.Point(1017, 19);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(100, 17);
            label2.TabIndex = 40;
            label2.Text = "Sztuki Wstawione";
            label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // sumaSztuk
            // 
            sumaSztuk.Location = new System.Drawing.Point(1017, 39);
            sumaSztuk.Name = "sumaSztuk";
            sumaSztuk.Size = new System.Drawing.Size(100, 23);
            sumaSztuk.TabIndex = 39;
            // 
            // dataGridView2
            // 
            dataGridView2.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView2.Location = new System.Drawing.Point(885, 136);
            dataGridView2.Name = "dataGridView2";
            dataGridView2.RowTemplate.Height = 25;
            dataGridView2.Size = new System.Drawing.Size(485, 176);
            dataGridView2.TabIndex = 41;
            // 
            // label1
            // 
            label1.Location = new System.Drawing.Point(885, 115);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(485, 18);
            label1.TabIndex = 42;
            label1.Text = "Zaplanowane dostawy ze wtawienia";
            label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // dataGridView3
            // 
            dataGridView3.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView3.Location = new System.Drawing.Point(885, 335);
            dataGridView3.Name = "dataGridView3";
            dataGridView3.RowTemplate.Height = 25;
            dataGridView3.Size = new System.Drawing.Size(485, 319);
            dataGridView3.TabIndex = 43;
            // 
            // label3
            // 
            label3.Location = new System.Drawing.Point(885, 315);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(485, 17);
            label3.TabIndex = 44;
            label3.Text = "Ostatnie wstawianie hodowcy";
            label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // WidokWstawienia
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1382, 685);
            Controls.Add(label3);
            Controls.Add(dataGridView3);
            Controls.Add(label1);
            Controls.Add(dataGridView2);
            Controls.Add(label2);
            Controls.Add(sumaSztuk);
            Controls.Add(label18);
            Controls.Add(textBox1);
            Controls.Add(dataGridView1);
            Name = "WidokWstawienia";
            Text = "Wstawienia";
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView2).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView3).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox sumaSztuk;
        private System.Windows.Forms.DataGridView dataGridView2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.DataGridView dataGridView3;
        private System.Windows.Forms.Label label3;
    }
}