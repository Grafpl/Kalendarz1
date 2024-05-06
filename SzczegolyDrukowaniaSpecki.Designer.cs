namespace Kalendarz1
{
    partial class SzczegolyDrukowaniaSpecki
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
            PrintButton = new System.Windows.Forms.Button();
            PrintersList = new System.Windows.Forms.ComboBox();
            textBox1 = new System.Windows.Forms.TextBox();
            textBox2 = new System.Windows.Forms.TextBox();
            SuspendLayout();
            // 
            // PrintButton
            // 
            PrintButton.Location = new System.Drawing.Point(128, 39);
            PrintButton.Name = "PrintButton";
            PrintButton.Size = new System.Drawing.Size(75, 23);
            PrintButton.TabIndex = 0;
            PrintButton.Text = "button1";
            PrintButton.UseVisualStyleBackColor = true;
            PrintButton.Click += PrintButton_Click;
            // 
            // PrintersList
            // 
            PrintersList.FormattingEnabled = true;
            PrintersList.Location = new System.Drawing.Point(389, 49);
            PrintersList.Name = "PrintersList";
            PrintersList.Size = new System.Drawing.Size(121, 23);
            PrintersList.TabIndex = 1;
            // 
            // textBox1
            // 
            textBox1.Location = new System.Drawing.Point(12, 10);
            textBox1.Name = "textBox1";
            textBox1.Size = new System.Drawing.Size(100, 23);
            textBox1.TabIndex = 2;
            // 
            // textBox2
            // 
            textBox2.Location = new System.Drawing.Point(12, 39);
            textBox2.Name = "textBox2";
            textBox2.Size = new System.Drawing.Size(100, 23);
            textBox2.TabIndex = 3;
            // 
            // SzczegolyDrukowaniaSpecki
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(868, 541);
            Controls.Add(textBox2);
            Controls.Add(textBox1);
            Controls.Add(PrintersList);
            Controls.Add(PrintButton);
            Name = "SzczegolyDrukowaniaSpecki";
            Text = "SzczegolyDrukowaniaSpecki";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Button PrintButton;
        private System.Windows.Forms.ComboBox PrintersList;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.TextBox textBox2;
    }
}