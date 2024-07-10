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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(WidokSprzeZakup));
            dataGridView1 = new System.Windows.Forms.DataGridView();
            dataKoniec = new System.Windows.Forms.DateTimePicker();
            dataPoczatek = new System.Windows.Forms.DateTimePicker();
            dataGridView2 = new System.Windows.Forms.DataGridView();
            Grupowanie = new System.Windows.Forms.CheckBox();
            dataKoniec2 = new System.Windows.Forms.DateTimePicker();
            dataPoczatek2 = new System.Windows.Forms.DateTimePicker();
            refresh = new System.Windows.Forms.Button();
            button1 = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView2).BeginInit();
            SuspendLayout();
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Location = new System.Drawing.Point(3, 40);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowTemplate.Height = 25;
            dataGridView1.Size = new System.Drawing.Size(484, 819);
            dataGridView1.TabIndex = 0;
            // 
            // dataKoniec
            // 
            dataKoniec.Location = new System.Drawing.Point(237, 2);
            dataKoniec.Name = "dataKoniec";
            dataKoniec.Size = new System.Drawing.Size(228, 23);
            dataKoniec.TabIndex = 23;

            // 
            // dataPoczatek
            // 
            dataPoczatek.Location = new System.Drawing.Point(3, 2);
            dataPoczatek.Name = "dataPoczatek";
            dataPoczatek.Size = new System.Drawing.Size(228, 23);
            dataPoczatek.TabIndex = 22;

            // 
            // dataGridView2
            // 
            dataGridView2.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView2.Location = new System.Drawing.Point(490, 40);
            dataGridView2.Name = "dataGridView2";
            dataGridView2.RowTemplate.Height = 25;
            dataGridView2.Size = new System.Drawing.Size(487, 819);
            dataGridView2.TabIndex = 27;
            // 
            // Grupowanie
            // 
            Grupowanie.AutoSize = true;
            Grupowanie.Location = new System.Drawing.Point(465, 1);
            Grupowanie.Name = "Grupowanie";
            Grupowanie.Size = new System.Drawing.Size(45, 19);
            Grupowanie.TabIndex = 28;
            Grupowanie.Text = "Gru";
            Grupowanie.UseVisualStyleBackColor = true;
            // 
            // dataKoniec2
            // 
            dataKoniec2.Location = new System.Drawing.Point(750, 1);
            dataKoniec2.Name = "dataKoniec2";
            dataKoniec2.Size = new System.Drawing.Size(228, 23);
            dataKoniec2.TabIndex = 23;

            // 
            // dataPoczatek2
            // 
            dataPoczatek2.Location = new System.Drawing.Point(516, 2);
            dataPoczatek2.Name = "dataPoczatek2";
            dataPoczatek2.Size = new System.Drawing.Size(228, 23);
            dataPoczatek2.TabIndex = 22;

            // 
            // refresh
            // 
            refresh.Location = new System.Drawing.Point(469, 16);
            refresh.Name = "refresh";
            refresh.Size = new System.Drawing.Size(44, 23);
            refresh.TabIndex = 30;
            refresh.Text = "Odśwież";
            refresh.UseVisualStyleBackColor = true;
            refresh.Click += refresh_Click;
            // 
            // button1
            // 
            button1.Location = new System.Drawing.Point(983, 40);
            button1.Name = "button1";
            button1.Size = new System.Drawing.Size(44, 23);
            button1.TabIndex = 31;
            button1.Text = "Ex";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // WidokSprzeZakup
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1063, 866);
            Controls.Add(button1);
            Controls.Add(dataKoniec2);
            Controls.Add(dataKoniec);
            Controls.Add(refresh);
            Controls.Add(dataPoczatek2);
            Controls.Add(dataPoczatek);
            Controls.Add(Grupowanie);
            Controls.Add(dataGridView2);
            Controls.Add(dataGridView1);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Name = "WidokSprzeZakup";
            Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView2).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.DateTimePicker dataKoniec;
        private System.Windows.Forms.DateTimePicker dataPoczatek;
        private System.Windows.Forms.DataGridView dataGridView2;
        private System.Windows.Forms.CheckBox Grupowanie;
        private System.Windows.Forms.DateTimePicker dataKoniec2;
        private System.Windows.Forms.DateTimePicker dataPoczatek2;
        private System.Windows.Forms.Button refresh;
        private System.Windows.Forms.Button button1;
    }
}