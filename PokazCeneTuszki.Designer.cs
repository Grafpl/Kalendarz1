namespace Kalendarz1
{
    partial class PokazCeneTuszki
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PokazCeneTuszki));
            dataGridView1 = new System.Windows.Forms.DataGridView();
            dateTimePicker1 = new System.Windows.Forms.DateTimePicker();
            sredniaCena = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            SuspendLayout();
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Location = new System.Drawing.Point(12, 37);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowTemplate.Height = 25;
            dataGridView1.Size = new System.Drawing.Size(332, 307);
            dataGridView1.TabIndex = 0;
            // 
            // dateTimePicker1
            // 
            dateTimePicker1.Location = new System.Drawing.Point(12, 8);
            dateTimePicker1.Name = "dateTimePicker1";
            dateTimePicker1.Size = new System.Drawing.Size(221, 23);
            dateTimePicker1.TabIndex = 24;
            // 
            // sredniaCena
            // 
            sredniaCena.Location = new System.Drawing.Point(280, 8);
            sredniaCena.Name = "sredniaCena";
            sredniaCena.Size = new System.Drawing.Size(54, 23);
            sredniaCena.TabIndex = 40;
            // 
            // PokazCeneTuszki
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(346, 352);
            Controls.Add(sredniaCena);
            Controls.Add(dateTimePicker1);
            Controls.Add(dataGridView1);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Name = "PokazCeneTuszki";
            Text = "Tuszka";
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.DateTimePicker dateTimePicker1;
        private System.Windows.Forms.TextBox sredniaCena;
    }
}