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
            label14 = new System.Windows.Forms.Label();
            label15 = new System.Windows.Forms.Label();
            UzgodnienieSaldButton = new System.Windows.Forms.Button();
            buttonPojemnikiZestawienie = new System.Windows.Forms.Button();
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
            // label14
            // 
            label14.Font = new System.Drawing.Font("Tahoma", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label14.Location = new System.Drawing.Point(415, 203);
            label14.Name = "label14";
            label14.Size = new System.Drawing.Size(262, 30);
            label14.TabIndex = 60;
            label14.Text = "Salda odbiorcy opak.";
            label14.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label15
            // 
            label15.Font = new System.Drawing.Font("Tahoma", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label15.Location = new System.Drawing.Point(147, 203);
            label15.Name = "label15";
            label15.Size = new System.Drawing.Size(262, 30);
            label15.TabIndex = 59;
            label15.Text = "Podsumowanie sald Opak.";
            label15.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // UzgodnienieSaldButton
            // 
            UzgodnienieSaldButton.BackgroundImage = (System.Drawing.Image)resources.GetObject("UzgodnienieSaldButton.BackgroundImage");
            UzgodnienieSaldButton.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            UzgodnienieSaldButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            UzgodnienieSaldButton.Location = new System.Drawing.Point(415, 236);
            UzgodnienieSaldButton.Name = "UzgodnienieSaldButton";
            UzgodnienieSaldButton.Size = new System.Drawing.Size(262, 193);
            UzgodnienieSaldButton.TabIndex = 58;
            UzgodnienieSaldButton.UseVisualStyleBackColor = true;
            // 
            // buttonPojemnikiZestawienie
            // 
            buttonPojemnikiZestawienie.BackgroundImage = (System.Drawing.Image)resources.GetObject("buttonPojemnikiZestawienie.BackgroundImage");
            buttonPojemnikiZestawienie.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            buttonPojemnikiZestawienie.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            buttonPojemnikiZestawienie.Location = new System.Drawing.Point(147, 236);
            buttonPojemnikiZestawienie.Name = "buttonPojemnikiZestawienie";
            buttonPojemnikiZestawienie.Size = new System.Drawing.Size(262, 193);
            buttonPojemnikiZestawienie.TabIndex = 57;
            buttonPojemnikiZestawienie.UseVisualStyleBackColor = true;
            // 
            // MenuPojemniki
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.White;
            ClientSize = new System.Drawing.Size(820, 493);
            Controls.Add(label14);
            Controls.Add(label15);
            Controls.Add(UzgodnienieSaldButton);
            Controls.Add(buttonPojemnikiZestawienie);
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
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.Button UzgodnienieSaldButton;
        private System.Windows.Forms.Button buttonPojemnikiZestawienie;
    }
}