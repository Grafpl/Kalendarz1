namespace Kalendarz1
{
    partial class LoginForm
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
            UsernameTextBox = new System.Windows.Forms.TextBox();
            LoginButton = new System.Windows.Forms.Button();
            btnCancel = new System.Windows.Forms.Button();
            pictureBox1 = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            SuspendLayout();
            // 
            // UsernameTextBox
            // 
            UsernameTextBox.Location = new System.Drawing.Point(118, 178);
            UsernameTextBox.Name = "UsernameTextBox";
            UsernameTextBox.Size = new System.Drawing.Size(100, 23);
            UsernameTextBox.TabIndex = 0;
            // 
            // LoginButton
            // 
            LoginButton.Location = new System.Drawing.Point(100, 207);
            LoginButton.Name = "LoginButton";
            LoginButton.Size = new System.Drawing.Size(64, 23);
            LoginButton.TabIndex = 1;
            LoginButton.Text = "Zaloguj";
            LoginButton.UseVisualStyleBackColor = true;
            LoginButton.Click += LoginButton_Click;
            // 
            // btnCancel
            // 
            btnCancel.Location = new System.Drawing.Point(183, 207);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new System.Drawing.Size(62, 23);
            btnCancel.TabIndex = 2;
            btnCancel.Text = "Anuluj";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += CancelButton_Click;
            // 
            // pictureBox1
            // 
            pictureBox1.Image = Properties.Resources.Screenshot_8;
            pictureBox1.Location = new System.Drawing.Point(100, 12);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new System.Drawing.Size(145, 143);
            pictureBox1.TabIndex = 3;
            pictureBox1.TabStop = false;
            // 
            // LoginForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(362, 237);
            Controls.Add(pictureBox1);
            Controls.Add(btnCancel);
            Controls.Add(LoginButton);
            Controls.Add(UsernameTextBox);
            Name = "LoginForm";
            Text = "LoginForm";
            Load += LoginForm_Load;
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TextBox UsernameTextBox;
        private System.Windows.Forms.Button LoginButton;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.PictureBox pictureBox1;
    }
}