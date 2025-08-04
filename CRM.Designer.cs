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
            comboBoxStatusFilter = new System.Windows.Forms.ComboBox();
            comboBoxPowiatFilter = new System.Windows.Forms.ComboBox();
            label1 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
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
            dataGridViewOdbiorcy.RowTemplate.Height = 25;
            dataGridViewOdbiorcy.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            dataGridViewOdbiorcy.Size = new System.Drawing.Size(1543, 704);
            dataGridViewOdbiorcy.TabIndex = 2;
            dataGridViewOdbiorcy.CellEnter += dataGridViewOdbiorcy_CellEnter;
            dataGridViewOdbiorcy.CellValueChanged += dataGridViewOdbiorcy_CellValueChanged;
            dataGridViewOdbiorcy.CurrentCellDirtyStateChanged += dataGridViewOdbiorcy_CurrentCellDirtyStateChanged;
            dataGridViewOdbiorcy.RowPrePaint += dataGridViewOdbiorcy_RowPrePaint;
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
            // comboBoxStatusFilter
            // 
            comboBoxStatusFilter.FormattingEnabled = true;
            comboBoxStatusFilter.Location = new System.Drawing.Point(684, 95);
            comboBoxStatusFilter.Name = "comboBoxStatusFilter";
            comboBoxStatusFilter.Size = new System.Drawing.Size(174, 23);
            comboBoxStatusFilter.TabIndex = 6;
            comboBoxStatusFilter.SelectedIndexChanged += comboBoxStatusFilter_SelectedIndexChanged;
            // 
            // comboBoxPowiatFilter
            // 
            comboBoxPowiatFilter.FormattingEnabled = true;
            comboBoxPowiatFilter.Location = new System.Drawing.Point(864, 95);
            comboBoxPowiatFilter.Name = "comboBoxPowiatFilter";
            comboBoxPowiatFilter.Size = new System.Drawing.Size(174, 23);
            comboBoxPowiatFilter.TabIndex = 7;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(684, 77);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(68, 15);
            label1.TabIndex = 8;
            label1.Text = "Filtr : Status";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(864, 77);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(72, 15);
            label2.TabIndex = 9;
            label2.Text = "Filtr : Powiat";
            // 
            // CRM
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1557, 840);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(comboBoxPowiatFilter);
            Controls.Add(comboBoxStatusFilter);
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
        private System.Windows.Forms.ComboBox comboBoxStatusFilter;
        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.ComboBox comboBoxPowiatFilter;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
    }
}