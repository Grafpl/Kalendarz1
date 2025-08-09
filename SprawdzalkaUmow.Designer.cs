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
            textBoxPartie = new System.Windows.Forms.TextBox();
            CommandButton_Insert = new System.Windows.Forms.Button();
            nieUzupelnione = new System.Windows.Forms.CheckBox();
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
            dataGridViewKalendarz.Size = new System.Drawing.Size(840, 757);
            dataGridViewKalendarz.TabIndex = 116;
            // 
            // dataGridViewPartie
            // 
            dataGridViewPartie.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewPartie.Location = new System.Drawing.Point(858, 81);
            dataGridViewPartie.Name = "dataGridViewPartie";
            dataGridViewPartie.RowHeadersVisible = false;
            dataGridViewPartie.RowTemplate.Height = 25;
            dataGridViewPartie.Size = new System.Drawing.Size(684, 757);
            dataGridViewPartie.TabIndex = 117;
            // 
            // textBoxPartie
            // 
            textBoxPartie.Location = new System.Drawing.Point(1363, 52);
            textBoxPartie.Name = "textBoxPartie";
            textBoxPartie.Size = new System.Drawing.Size(100, 23);
            textBoxPartie.TabIndex = 119;
            // 
            // CommandButton_Insert
            // 
            CommandButton_Insert.BackColor = System.Drawing.Color.Lime;
            CommandButton_Insert.Location = new System.Drawing.Point(12, 12);
            CommandButton_Insert.Name = "CommandButton_Insert";
            CommandButton_Insert.Size = new System.Drawing.Size(116, 63);
            CommandButton_Insert.TabIndex = 154;
            CommandButton_Insert.Text = "Dodaj umowe";
            CommandButton_Insert.UseVisualStyleBackColor = false;
            CommandButton_Insert.Click += CommandButton_Insert_Click;
            // 
            // nieUzupelnione
            // 
            nieUzupelnione.AutoSize = true;
            nieUzupelnione.Location = new System.Drawing.Point(134, 52);
            nieUzupelnione.Name = "nieUzupelnione";
            nieUzupelnione.Size = new System.Drawing.Size(112, 19);
            nieUzupelnione.TabIndex = 155;
            nieUzupelnione.Text = "Nie uzupełnione";
            nieUzupelnione.UseVisualStyleBackColor = true;
            nieUzupelnione.CheckedChanged += nieUzupelnione_CheckedChanged;
            // 
            // SprawdzalkaUmow
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1594, 850);
            Controls.Add(nieUzupelnione);
            Controls.Add(CommandButton_Insert);
            Controls.Add(textBoxPartie);
            Controls.Add(dataGridViewPartie);
            Controls.Add(dataGridViewKalendarz);
            Name = "SprawdzalkaUmow";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "SprawdzalkaUmow";
            WindowState = System.Windows.Forms.FormWindowState.Maximized;
            ((System.ComponentModel.ISupportInitialize)dataGridViewKalendarz).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewPartie).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridViewKalendarz;
        private System.Windows.Forms.DataGridView dataGridViewPartie;
        private System.Windows.Forms.TextBox textBoxPartie;
        private System.Windows.Forms.Button CommandButton_Insert;
        private System.Windows.Forms.CheckBox nieUzupelnione;
    }
}