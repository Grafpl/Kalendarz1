namespace Kalendarz1
{
    partial class SprawdzalkaUmow
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
            dataGridViewKalendarz = new System.Windows.Forms.DataGridView();
            dataGridViewPartie = new System.Windows.Forms.DataGridView();
            textBoxKalendarz = new System.Windows.Forms.TextBox();
            textBoxPartie = new System.Windows.Forms.TextBox();
            dateTimePicker = new System.Windows.Forms.DateTimePicker();
            ((System.ComponentModel.ISupportInitialize)dataGridViewKalendarz).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewPartie).BeginInit();
            SuspendLayout();
            // 
            // dataGridViewKalendarz
            // 
            dataGridViewKalendarz.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewKalendarz.Location = new System.Drawing.Point(12, 81);
            dataGridViewKalendarz.Name = "dataGridViewKalendarz";
            dataGridViewKalendarz.RowHeadersVisible = false;
            dataGridViewKalendarz.RowTemplate.Height = 25;
            dataGridViewKalendarz.Size = new System.Drawing.Size(753, 757);
            dataGridViewKalendarz.TabIndex = 116;
            // 
            // dataGridViewPartie
            // 
            dataGridViewPartie.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewPartie.Location = new System.Drawing.Point(779, 81);
            dataGridViewPartie.Name = "dataGridViewPartie";
            dataGridViewPartie.RowHeadersVisible = false;
            dataGridViewPartie.RowTemplate.Height = 25;
            dataGridViewPartie.Size = new System.Drawing.Size(684, 757);
            dataGridViewPartie.TabIndex = 117;
            // 
            // textBoxKalendarz
            // 
            textBoxKalendarz.Location = new System.Drawing.Point(665, 52);
            textBoxKalendarz.Name = "textBoxKalendarz";
            textBoxKalendarz.Size = new System.Drawing.Size(100, 23);
            textBoxKalendarz.TabIndex = 118;

            // 
            // textBoxPartie
            // 
            textBoxPartie.Location = new System.Drawing.Point(1363, 52);
            textBoxPartie.Name = "textBoxPartie";
            textBoxPartie.Size = new System.Drawing.Size(100, 23);
            textBoxPartie.TabIndex = 119;

            // 
            // dateTimePicker
            // 
            dateTimePicker.CalendarFont = new System.Drawing.Font("Segoe UI Semibold", 15.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            dateTimePicker.Location = new System.Drawing.Point(651, 12);
            dateTimePicker.Name = "dateTimePicker";
            dateTimePicker.Size = new System.Drawing.Size(228, 23);
            dateTimePicker.TabIndex = 153;

            // 
            // SprawdzalkaUmow
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1594, 850);
            Controls.Add(dateTimePicker);
            Controls.Add(textBoxPartie);
            Controls.Add(textBoxKalendarz);
            Controls.Add(dataGridViewPartie);
            Controls.Add(dataGridViewKalendarz);
            Name = "SprawdzalkaUmow";
            Text = "SprawdzalkaUmow";
            ((System.ComponentModel.ISupportInitialize)dataGridViewKalendarz).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewPartie).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridViewKalendarz;
        private System.Windows.Forms.DataGridView dataGridViewPartie;
        private System.Windows.Forms.TextBox textBoxKalendarz;
        private System.Windows.Forms.TextBox textBoxPartie;
        private System.Windows.Forms.DateTimePicker dateTimePicker;
    }
}