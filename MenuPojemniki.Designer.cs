namespace Kalendarz1
{
    partial class MenuPojemniki
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MenuPojemniki));
            pictureBox1 = new System.Windows.Forms.PictureBox();
            UzgodnienieSaldButton = new System.Windows.Forms.Button();
            button2 = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            SuspendLayout();
            // 
            // pictureBox1
            // 
            pictureBox1.BackgroundImage = Properties.Resources.pm;
            pictureBox1.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            pictureBox1.Location = new System.Drawing.Point(73, 11);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new System.Drawing.Size(656, 137);
            pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            pictureBox1.TabIndex = 8;
            pictureBox1.TabStop = false;
            // 
            // UzgodnienieSaldButton
            // 
            UzgodnienieSaldButton.BackgroundImage = (System.Drawing.Image)resources.GetObject("UzgodnienieSaldButton.BackgroundImage");
            UzgodnienieSaldButton.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            UzgodnienieSaldButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            UzgodnienieSaldButton.Location = new System.Drawing.Point(271, 157);
            UzgodnienieSaldButton.Name = "UzgodnienieSaldButton";
            UzgodnienieSaldButton.Size = new System.Drawing.Size(262, 193);
            UzgodnienieSaldButton.TabIndex = 9;
            UzgodnienieSaldButton.UseVisualStyleBackColor = true;
            UzgodnienieSaldButton.Click += UzgodnienieSaldButton_Click;
            // 
            // button2
            // 
            button2.BackgroundImage = (System.Drawing.Image)resources.GetObject("button2.BackgroundImage");
            button2.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            button2.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            button2.Location = new System.Drawing.Point(539, 157);
            button2.Name = "button2";
            button2.Size = new System.Drawing.Size(262, 193);
            button2.TabIndex = 10;
            button2.UseVisualStyleBackColor = true;
            // 
            // MenuPojemniki
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.White;
            ClientSize = new System.Drawing.Size(820, 362);
            Controls.Add(button2);
            Controls.Add(UzgodnienieSaldButton);
            Controls.Add(pictureBox1);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Name = "MenuPojemniki";
            Text = "Menu";
            Load += MENU_Load;
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ResumeLayout(false);
        }

        #endregion
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Button UzgodnienieSaldButton;
        private System.Windows.Forms.Button button2;
    }
}