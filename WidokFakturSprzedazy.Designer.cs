namespace Kalendarz1
{
    partial class WidokFakturSprzedazy
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
            comboBoxTowar = new System.Windows.Forms.ComboBox();
            comboBoxKontrahent = new System.Windows.Forms.ComboBox();
            label1 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            dataGridViewAnaliza = new System.Windows.Forms.DataGridView();
            dataGridViewPlatnosci = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)dataGridViewOdbiorcy).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewNotatki).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewAnaliza).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewPlatnosci).BeginInit();
            SuspendLayout();
            // 
            // dataGridViewOdbiorcy
            // 
            dataGridViewOdbiorcy.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewOdbiorcy.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewOdbiorcy.Location = new System.Drawing.Point(1, 2);
            dataGridViewOdbiorcy.Name = "dataGridViewOdbiorcy";
            dataGridViewOdbiorcy.RowTemplate.Height = 25;
            dataGridViewOdbiorcy.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            dataGridViewOdbiorcy.Size = new System.Drawing.Size(715, 478);
            dataGridViewOdbiorcy.TabIndex = 2;
            // 
            // dataGridViewNotatki
            // 
            dataGridViewNotatki.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewNotatki.Location = new System.Drawing.Point(722, 2);
            dataGridViewNotatki.Name = "dataGridViewNotatki";
            dataGridViewNotatki.RowTemplate.Height = 25;
            dataGridViewNotatki.Size = new System.Drawing.Size(395, 167);
            dataGridViewNotatki.TabIndex = 3;
            // 
            // comboBoxTowar
            // 
            comboBoxTowar.FormattingEnabled = true;
            comboBoxTowar.Location = new System.Drawing.Point(763, 175);
            comboBoxTowar.Name = "comboBoxTowar";
            comboBoxTowar.Size = new System.Drawing.Size(126, 23);
            comboBoxTowar.TabIndex = 6;
            comboBoxTowar.SelectedIndexChanged += comboBoxTowar_SelectedIndexChanged;
            // 
            // comboBoxKontrahent
            // 
            comboBoxKontrahent.FormattingEnabled = true;
            comboBoxKontrahent.Location = new System.Drawing.Point(967, 175);
            comboBoxKontrahent.Name = "comboBoxKontrahent";
            comboBoxKontrahent.Size = new System.Drawing.Size(150, 23);
            comboBoxKontrahent.TabIndex = 7;
            comboBoxKontrahent.SelectedIndexChanged += comboBoxKontrahent_SelectedIndexChanged;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(718, 178);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(39, 15);
            label1.TabIndex = 8;
            label1.Text = "Towar";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(895, 178);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(66, 15);
            label2.TabIndex = 9;
            label2.Text = "Kontrahent";
            // 
            // dataGridViewAnaliza
            // 
            dataGridViewAnaliza.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewAnaliza.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewAnaliza.Location = new System.Drawing.Point(1, 486);
            dataGridViewAnaliza.Name = "dataGridViewAnaliza";
            dataGridViewAnaliza.RowTemplate.Height = 25;
            dataGridViewAnaliza.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            dataGridViewAnaliza.Size = new System.Drawing.Size(1116, 226);
            dataGridViewAnaliza.TabIndex = 12;
            // 
            // dataGridViewPlatnosci
            // 
            dataGridViewPlatnosci.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewPlatnosci.Location = new System.Drawing.Point(722, 204);
            dataGridViewPlatnosci.Name = "dataGridViewPlatnosci";
            dataGridViewPlatnosci.RowTemplate.Height = 25;
            dataGridViewPlatnosci.Size = new System.Drawing.Size(743, 276);
            dataGridViewPlatnosci.TabIndex = 13;
            dataGridViewPlatnosci.RowPostPaint += dataGridViewPlatnosci_RowPostPaint;
            // 
            // WidokFakturSprzedazy
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1477, 712);
            Controls.Add(dataGridViewPlatnosci);
            Controls.Add(dataGridViewAnaliza);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(comboBoxKontrahent);
            Controls.Add(comboBoxTowar);
            Controls.Add(dataGridViewNotatki);
            Controls.Add(dataGridViewOdbiorcy);
            Name = "WidokFakturSprzedazy";
            Text = " WidokFakturSprzedazy";
            Load += WidokFakturSprzedazy_Load;
            ((System.ComponentModel.ISupportInitialize)dataGridViewOdbiorcy).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewNotatki).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewAnaliza).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewPlatnosci).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private System.Windows.Forms.DataGridView dataGridViewOdbiorcy;
        private System.Windows.Forms.DataGridView dataGridViewNotatki;
        private System.Windows.Forms.ComboBox comboBoxTowar;
        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.ComboBox comboBoxKontrahent;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.DataGridView dataGridViewAnaliza;
        private System.Windows.Forms.DataGridView dataGridViewPlatnosci;
    }
}