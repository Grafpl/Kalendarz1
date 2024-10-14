namespace Kalendarz1
{
    partial class WidokCenWszystkich
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(WidokCenWszystkich));
            label18 = new System.Windows.Forms.Label();
            textBox1 = new System.Windows.Forms.TextBox();
            dataGridView1 = new System.Windows.Forms.DataGridView();
            chkShowWeekend = new System.Windows.Forms.CheckBox();
            ExcelButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            SuspendLayout();
            // 
            // label18
            // 
            label18.Location = new System.Drawing.Point(12, -3);
            label18.Name = "label18";
            label18.Size = new System.Drawing.Size(100, 16);
            label18.TabIndex = 43;
            label18.Text = "Szukaj";
            label18.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // textBox1
            // 
            textBox1.Location = new System.Drawing.Point(12, 16);
            textBox1.Name = "textBox1";
            textBox1.Size = new System.Drawing.Size(100, 23);
            textBox1.TabIndex = 42;
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Location = new System.Drawing.Point(12, 45);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowTemplate.Height = 25;
            dataGridView1.Size = new System.Drawing.Size(614, 649);
            dataGridView1.TabIndex = 41;
            // 
            // chkShowWeekend
            // 
            chkShowWeekend.AutoSize = true;
            chkShowWeekend.Font = new System.Drawing.Font("Segoe UI", 6F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            chkShowWeekend.Location = new System.Drawing.Point(127, 16);
            chkShowWeekend.Name = "chkShowWeekend";
            chkShowWeekend.Size = new System.Drawing.Size(61, 15);
            chkShowWeekend.TabIndex = 120;
            chkShowWeekend.Text = "Weekendy";
            chkShowWeekend.UseVisualStyleBackColor = true;
            // 
            // ExcelButton
            // 
            ExcelButton.Location = new System.Drawing.Point(247, 15);
            ExcelButton.Name = "ExcelButton";
            ExcelButton.Size = new System.Drawing.Size(75, 23);
            ExcelButton.TabIndex = 121;
            ExcelButton.Text = "Excel";
            ExcelButton.UseVisualStyleBackColor = true;
            ExcelButton.Click += ExcelButton_Click;
            // 
            // WidokCenWszystkich
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(638, 706);
            Controls.Add(ExcelButton);
            Controls.Add(chkShowWeekend);
            Controls.Add(label18);
            Controls.Add(textBox1);
            Controls.Add(dataGridView1);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Name = "WidokCenWszystkich";
            Text = "Ceny";
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.CheckBox chkShowWeekend;
        private System.Windows.Forms.Button ExcelButton;
    }
}