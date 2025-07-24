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
            buttonPokaz = new System.Windows.Forms.Button();
            ShowChartButton = new System.Windows.Forms.Button();
            dataGridView2 = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView2).BeginInit();
            SuspendLayout();
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Location = new System.Drawing.Point(12, 35);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowHeadersWidth = 51;
            dataGridView1.RowTemplate.Height = 25;
            dataGridView1.Size = new System.Drawing.Size(228, 630);
            dataGridView1.TabIndex = 0;
            dataGridView1.CellContentClick += dataGridView1_CellContentClick;
            // 
            // dataPoczatek
            // 
            dataPoczatek.Location = new System.Drawing.Point(12, 12);
            dataPoczatek.Name = "dataPoczatek";
            dataPoczatek.Size = new System.Drawing.Size(228, 23);
            dataPoczatek.TabIndex = 1;
            // 
            // dataKoniec
            // 
            dataKoniec.Location = new System.Drawing.Point(246, 12);
            dataKoniec.Name = "dataKoniec";
            dataKoniec.Size = new System.Drawing.Size(228, 23);
            dataKoniec.TabIndex = 2;
            // 
            // buttonPokaz
            // 
            buttonPokaz.Location = new System.Drawing.Point(480, 14);
            buttonPokaz.Name = "buttonPokaz";
            buttonPokaz.Size = new System.Drawing.Size(75, 23);
            buttonPokaz.TabIndex = 23;
            buttonPokaz.Text = "Pokaż";
            buttonPokaz.UseVisualStyleBackColor = true;
            buttonPokaz.Click += buttonPokaz_Click;
            // 
            // ShowChartButton
            // 
            ShowChartButton.Location = new System.Drawing.Point(785, 3);
            ShowChartButton.Name = "ShowChartButton";
            ShowChartButton.Size = new System.Drawing.Size(75, 23);
            ShowChartButton.TabIndex = 24;
            ShowChartButton.Text = "Pokaż";
            ShowChartButton.UseVisualStyleBackColor = true;
            ShowChartButton.Click += ShowChartButton_Click;
            // 
            // dataGridView2
            // 
            dataGridView2.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView2.Location = new System.Drawing.Point(246, 35);
            dataGridView2.Name = "dataGridView2";
            dataGridView2.RowHeadersWidth = 51;
            dataGridView2.RowTemplate.Height = 25;
            dataGridView2.Size = new System.Drawing.Size(614, 630);
            dataGridView2.TabIndex = 25;
            // 
            // Mroznia
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1283, 669);
            Controls.Add(dataGridView2);
            Controls.Add(ShowChartButton);
            Controls.Add(buttonPokaz);
            Controls.Add(dataKoniec);
            Controls.Add(dataPoczatek);
            Controls.Add(dataGridView1);
            Name = "Mroznia";
            Text = " ";
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView2).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.DateTimePicker dataPoczatek;
        private System.Windows.Forms.DateTimePicker dataKoniec;
        private System.Windows.Forms.Button buttonPokaz;
        private System.Windows.Forms.Button ShowChartButton;
        private System.Windows.Forms.DataGridView dataGridView2;
    }
}