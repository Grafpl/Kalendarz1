namespace Kalendarz1
{
    partial class WidokCena
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
            TypCeny = new System.Windows.Forms.ComboBox();
            Data1 = new System.Windows.Forms.DateTimePicker();
            label8 = new System.Windows.Forms.Label();
            label1 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            Data3 = new System.Windows.Forms.DateTimePicker();
            Data2 = new System.Windows.Forms.DateTimePicker();
            label3 = new System.Windows.Forms.Label();
            Cena = new System.Windows.Forms.TextBox();
            label4 = new System.Windows.Forms.Label();
            UtworzCena = new System.Windows.Forms.Button();
            button1 = new System.Windows.Forms.Button();
            SuspendLayout();
            // 
            // TypCeny
            // 
            TypCeny.FormattingEnabled = true;
            TypCeny.Location = new System.Drawing.Point(158, 12);
            TypCeny.Name = "TypCeny";
            TypCeny.Size = new System.Drawing.Size(121, 23);
            TypCeny.TabIndex = 0;
            TypCeny.SelectedIndexChanged += TypCeny_SelectedIndexChanged;
            // 
            // Data1
            // 
            Data1.Location = new System.Drawing.Point(158, 41);
            Data1.Name = "Data1";
            Data1.Size = new System.Drawing.Size(240, 23);
            Data1.TabIndex = 1;
            Data1.ValueChanged += Data1_ValueChanged;
            // 
            // label8
            // 
            label8.Location = new System.Drawing.Point(-1, 41);
            label8.Name = "label8";
            label8.Size = new System.Drawing.Size(153, 23);
            label8.TabIndex = 19;
            label8.Text = "Data ujawnienia ceny";
            label8.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label1
            // 
            label1.Location = new System.Drawing.Point(-1, 12);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(153, 23);
            label1.TabIndex = 20;
            label1.Text = "Typ ceny";
            label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label2
            // 
            label2.Location = new System.Drawing.Point(-1, 70);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(153, 23);
            label2.TabIndex = 22;
            label2.Text = "Cena obowiązuje od";
            label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // Data3
            // 
            Data3.CalendarMonthBackground = System.Drawing.SystemColors.ScrollBar;
            Data3.Location = new System.Drawing.Point(158, 70);
            Data3.Name = "Data3";
            Data3.Size = new System.Drawing.Size(240, 23);
            Data3.TabIndex = 21;
            // 
            // Data2
            // 
            Data2.CalendarMonthBackground = System.Drawing.SystemColors.ScrollBar;
            Data2.Location = new System.Drawing.Point(158, 99);
            Data2.Name = "Data2";
            Data2.Size = new System.Drawing.Size(240, 23);
            Data2.TabIndex = 23;
            // 
            // label3
            // 
            label3.Location = new System.Drawing.Point(-1, 99);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(153, 23);
            label3.TabIndex = 24;
            label3.Text = "Cena obowiązuje do";
            label3.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // Cena
            // 
            Cena.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            Cena.Location = new System.Drawing.Point(158, 128);
            Cena.Name = "Cena";
            Cena.Size = new System.Drawing.Size(58, 25);
            Cena.TabIndex = 25;
            Cena.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // label4
            // 
            label4.Location = new System.Drawing.Point(-1, 128);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(153, 23);
            label4.TabIndex = 26;
            label4.Text = "Ogłoszona cena";
            label4.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // UtworzCena
            // 
            UtworzCena.Location = new System.Drawing.Point(222, 130);
            UtworzCena.Name = "UtworzCena";
            UtworzCena.Size = new System.Drawing.Size(75, 23);
            UtworzCena.TabIndex = 27;
            UtworzCena.Text = "Dodaj";
            UtworzCena.UseVisualStyleBackColor = true;
            UtworzCena.Click += UtworzCena_Click;
            // 
            // button1
            // 
            button1.Location = new System.Drawing.Point(323, 130);
            button1.Name = "button1";
            button1.Size = new System.Drawing.Size(75, 23);
            button1.TabIndex = 28;
            button1.Text = "Anuluj";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // WidokCena
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(423, 157);
            Controls.Add(button1);
            Controls.Add(UtworzCena);
            Controls.Add(label4);
            Controls.Add(Cena);
            Controls.Add(label3);
            Controls.Add(Data2);
            Controls.Add(label2);
            Controls.Add(Data3);
            Controls.Add(label1);
            Controls.Add(label8);
            Controls.Add(Data1);
            Controls.Add(TypCeny);
            Name = "WidokCena";
            Text = "Form1";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.ComboBox TypCeny;
        private System.Windows.Forms.DateTimePicker Data1;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.DateTimePicker Data3;
        private System.Windows.Forms.DateTimePicker Data2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox Cena;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button UtworzCena;
        private System.Windows.Forms.Button button1;
    }
}