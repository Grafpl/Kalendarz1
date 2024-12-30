namespace Kalendarz1
{
    partial class WidokZamowienia
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
            comboBoxOdbiorca = new System.Windows.Forms.ComboBox();
            textBoxIdOdbiorca = new System.Windows.Forms.TextBox();
            textBoxNazwaOdbiorca = new System.Windows.Forms.TextBox();
            SuspendLayout();
            // 
            // comboBoxOdbiorca
            // 
            comboBoxOdbiorca.FormattingEnabled = true;
            comboBoxOdbiorca.Location = new System.Drawing.Point(12, 3);
            comboBoxOdbiorca.Name = "comboBoxOdbiorca";
            comboBoxOdbiorca.Size = new System.Drawing.Size(280, 23);
            comboBoxOdbiorca.TabIndex = 0;
            comboBoxOdbiorca.SelectedIndexChanged += comboBoxOdbiorca_SelectedIndexChanged;
            // 
            // textBoxIdOdbiorca
            // 
            textBoxIdOdbiorca.Location = new System.Drawing.Point(298, 3);
            textBoxIdOdbiorca.Name = "textBoxIdOdbiorca";
            textBoxIdOdbiorca.Size = new System.Drawing.Size(100, 23);
            textBoxIdOdbiorca.TabIndex = 1;
            // 
            // textBoxNazwaOdbiorca
            // 
            textBoxNazwaOdbiorca.Location = new System.Drawing.Point(404, 3);
            textBoxNazwaOdbiorca.Name = "textBoxNazwaOdbiorca";
            textBoxNazwaOdbiorca.Size = new System.Drawing.Size(232, 23);
            textBoxNazwaOdbiorca.TabIndex = 2;
            // 
            // WidokZamowienia
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(800, 450);
            Controls.Add(textBoxNazwaOdbiorca);
            Controls.Add(textBoxIdOdbiorca);
            Controls.Add(comboBoxOdbiorca);
            Name = "WidokZamowienia";
            Text = "WidokZamowienia";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.ComboBox comboBoxOdbiorca;
        private System.Windows.Forms.TextBox textBoxIdOdbiorca;
        private System.Windows.Forms.TextBox textBoxNazwaOdbiorca;
    }
}