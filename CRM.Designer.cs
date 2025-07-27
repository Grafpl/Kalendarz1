namespace Kalendarz1
{
    partial class CRM
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
            dataGridViewOdbiorcy = new System.Windows.Forms.DataGridView();
            dataGridViewNotatki = new System.Windows.Forms.DataGridView();
            textBoxNotatka = new System.Windows.Forms.TextBox();
            buttonDodajNotatke = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)dataGridViewOdbiorcy).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewNotatki).BeginInit();
            SuspendLayout();
            // 
            // dataGridViewOdbiorcy
            // 
            dataGridViewOdbiorcy.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewOdbiorcy.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewOdbiorcy.Location = new System.Drawing.Point(2, 124);
            dataGridViewOdbiorcy.Name = "dataGridViewOdbiorcy";
            dataGridViewOdbiorcy.ReadOnly = true;
            dataGridViewOdbiorcy.RowTemplate.Height = 25;
            dataGridViewOdbiorcy.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            dataGridViewOdbiorcy.Size = new System.Drawing.Size(1543, 704);
            dataGridViewOdbiorcy.TabIndex = 2;
            dataGridViewOdbiorcy.CellClick += dataGridViewOdbiorcy_CellClick;
            // 
            // dataGridViewNotatki
            // 
            dataGridViewNotatki.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewNotatki.Location = new System.Drawing.Point(340, 12);
            dataGridViewNotatki.Name = "dataGridViewNotatki";
            dataGridViewNotatki.RowTemplate.Height = 25;
            dataGridViewNotatki.Size = new System.Drawing.Size(313, 106);
            dataGridViewNotatki.TabIndex = 3;
            // 
            // textBoxNotatka
            // 
            textBoxNotatka.Location = new System.Drawing.Point(2, 0);
            textBoxNotatka.Multiline = true;
            textBoxNotatka.Name = "textBoxNotatka";
            textBoxNotatka.Size = new System.Drawing.Size(240, 118);
            textBoxNotatka.TabIndex = 4;
            // 
            // buttonDodajNotatke
            // 
            buttonDodajNotatke.Location = new System.Drawing.Point(248, 75);
            buttonDodajNotatke.Name = "buttonDodajNotatke";
            buttonDodajNotatke.Size = new System.Drawing.Size(86, 43);
            buttonDodajNotatke.TabIndex = 5;
            buttonDodajNotatke.Text = "Dodaj notatke";
            buttonDodajNotatke.UseVisualStyleBackColor = true;
            buttonDodajNotatke.Click += buttonDodajNotatke_Click;
            // 
            // CRM
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1557, 840);
            Controls.Add(buttonDodajNotatke);
            Controls.Add(textBoxNotatka);
            Controls.Add(dataGridViewNotatki);
            Controls.Add(dataGridViewOdbiorcy);
            Name = "CRM";
            Text = " CRM";
            Load += CRM_Load;
            ((System.ComponentModel.ISupportInitialize)dataGridViewOdbiorcy).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewNotatki).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private System.Windows.Forms.DataGridView dataGridViewOdbiorcy;
        private System.Windows.Forms.DataGridView dataGridViewNotatki;
        private System.Windows.Forms.TextBox textBoxNotatka;
        private System.Windows.Forms.Button buttonDodajNotatke;
    }
}