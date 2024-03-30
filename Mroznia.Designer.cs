namespace Kalendarz1
{
    partial class Mroznia
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
            dataPoczatek = new System.Windows.Forms.DateTimePicker();
            dataKoniec = new System.Windows.Forms.DateTimePicker();
            label9 = new System.Windows.Forms.Label();
            label1 = new System.Windows.Forms.Label();
            buttonPokaz = new System.Windows.Forms.Button();
            ShowChartButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            SuspendLayout();
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Location = new System.Drawing.Point(12, 12);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowTemplate.Height = 25;
            dataGridView1.Size = new System.Drawing.Size(784, 745);
            dataGridView1.TabIndex = 0;
            // 
            // dataPoczatek
            // 
            dataPoczatek.Location = new System.Drawing.Point(802, 35);
            dataPoczatek.Name = "dataPoczatek";
            dataPoczatek.Size = new System.Drawing.Size(228, 23);
            dataPoczatek.TabIndex = 1;
            // 
            // dataKoniec
            // 
            dataKoniec.Location = new System.Drawing.Point(802, 87);
            dataKoniec.Name = "dataKoniec";
            dataKoniec.Size = new System.Drawing.Size(228, 23);
            dataKoniec.TabIndex = 2;
            // 
            // label9
            // 
            label9.Location = new System.Drawing.Point(802, 13);
            label9.Name = "label9";
            label9.Size = new System.Drawing.Size(228, 22);
            label9.TabIndex = 20;
            label9.Text = "Początkowa data";
            label9.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label1
            // 
            label1.Location = new System.Drawing.Point(802, 62);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(228, 22);
            label1.TabIndex = 21;
            label1.Text = "Końcowa data";
            label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // buttonPokaz
            // 
            buttonPokaz.Location = new System.Drawing.Point(804, 136);
            buttonPokaz.Name = "buttonPokaz";
            buttonPokaz.Size = new System.Drawing.Size(75, 23);
            buttonPokaz.TabIndex = 23;
            buttonPokaz.Text = "Pokaż";
            buttonPokaz.UseVisualStyleBackColor = true;
            buttonPokaz.Click += buttonPokaz_Click;
            // 
            // ShowChartButton
            // 
            ShowChartButton.Location = new System.Drawing.Point(804, 165);
            ShowChartButton.Name = "ShowChartButton";
            ShowChartButton.Size = new System.Drawing.Size(75, 23);
            ShowChartButton.TabIndex = 24;
            ShowChartButton.Text = "Pokaż";
            ShowChartButton.UseVisualStyleBackColor = true;
            ShowChartButton.Click += ShowChartButton_Click;
            // 
            // Mroznia
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1248, 769);
            Controls.Add(ShowChartButton);
            Controls.Add(buttonPokaz);
            Controls.Add(label1);
            Controls.Add(label9);
            Controls.Add(dataKoniec);
            Controls.Add(dataPoczatek);
            Controls.Add(dataGridView1);
            Name = "Mroznia";
            Text = " ";
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.DateTimePicker dataPoczatek;
        private System.Windows.Forms.DateTimePicker dataKoniec;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button buttonPokaz;
        private System.Windows.Forms.Button ShowChartButton;
    }
}