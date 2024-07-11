namespace Kalendarz1
{
    partial class Platnosci
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Platnosci));
            dataGridView1 = new System.Windows.Forms.DataGridView();
            refreshButton = new System.Windows.Forms.Button();
            textBox1 = new System.Windows.Forms.TextBox();
            showAllCheckBox = new System.Windows.Forms.CheckBox();
            textBox2 = new System.Windows.Forms.TextBox();
            button1 = new System.Windows.Forms.Button();
            dataGridView2 = new System.Windows.Forms.DataGridView();
            dataGridView3 = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView2).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView3).BeginInit();
            SuspendLayout();
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Location = new System.Drawing.Point(12, 82);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowTemplate.Height = 25;
            dataGridView1.Size = new System.Drawing.Size(494, 708);
            dataGridView1.TabIndex = 0;
            dataGridView1.RowPrePaint += dataGridView1_RowPrePaint;
            // 
            // refreshButton
            // 
            refreshButton.Location = new System.Drawing.Point(12, 12);
            refreshButton.Name = "refreshButton";
            refreshButton.Size = new System.Drawing.Size(66, 64);
            refreshButton.TabIndex = 1;
            refreshButton.Text = "Wczytaj";
            refreshButton.UseVisualStyleBackColor = true;
            refreshButton.Click += refreshButton_Click;
            // 
            // textBox1
            // 
            textBox1.Location = new System.Drawing.Point(84, 53);
            textBox1.Name = "textBox1";
            textBox1.Size = new System.Drawing.Size(187, 23);
            textBox1.TabIndex = 2;
            // 
            // showAllCheckBox
            // 
            showAllCheckBox.AutoSize = true;
            showAllCheckBox.Location = new System.Drawing.Point(277, 57);
            showAllCheckBox.Name = "showAllCheckBox";
            showAllCheckBox.Size = new System.Drawing.Size(77, 19);
            showAllCheckBox.TabIndex = 3;
            showAllCheckBox.Text = "Szczegóły";
            showAllCheckBox.UseVisualStyleBackColor = true;
            // 
            // textBox2
            // 
            textBox2.Location = new System.Drawing.Point(360, 53);
            textBox2.Name = "textBox2";
            textBox2.Size = new System.Drawing.Size(134, 23);
            textBox2.TabIndex = 4;
            // 
            // button1
            // 
            button1.Location = new System.Drawing.Point(277, 28);
            button1.Name = "button1";
            button1.Size = new System.Drawing.Size(75, 23);
            button1.TabIndex = 5;
            button1.Text = "Kopiuj";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click_1;
            // 
            // dataGridView2
            // 
            dataGridView2.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView2.Location = new System.Drawing.Point(520, 82);
            dataGridView2.Name = "dataGridView2";
            dataGridView2.RowTemplate.Height = 25;
            dataGridView2.Size = new System.Drawing.Size(473, 708);
            dataGridView2.TabIndex = 6;
            // 
            // dataGridView3
            // 
            dataGridView3.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView3.Location = new System.Drawing.Point(999, 82);
            dataGridView3.Name = "dataGridView3";
            dataGridView3.RowTemplate.Height = 25;
            dataGridView3.Size = new System.Drawing.Size(473, 708);
            dataGridView3.TabIndex = 7;
            // 
            // Platnosci
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1512, 802);
            Controls.Add(dataGridView3);
            Controls.Add(dataGridView2);
            Controls.Add(button1);
            Controls.Add(textBox2);
            Controls.Add(showAllCheckBox);
            Controls.Add(textBox1);
            Controls.Add(refreshButton);
            Controls.Add(dataGridView1);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Name = "Platnosci";
            Text = "Platnosci";
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView2).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView3).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Button refreshButton;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.CheckBox showAllCheckBox;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.DataGridView dataGridView2;
        private System.Windows.Forms.DataGridView dataGridView3;
    }
}