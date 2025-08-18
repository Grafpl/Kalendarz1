namespace Kalendarz1
{
    partial class WidokWstawienia
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(WidokWstawienia));
            dataGridView1 = new System.Windows.Forms.DataGridView();
            textBox1 = new System.Windows.Forms.TextBox();
            label18 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            sumaSztuk = new System.Windows.Forms.TextBox();
            dataGridView2 = new System.Windows.Forms.DataGridView();
            label1 = new System.Windows.Forms.Label();
            dataGridView3 = new System.Windows.Forms.DataGridView();
            label3 = new System.Windows.Forms.Label();
            label4 = new System.Windows.Forms.Label();
            dataGridView4 = new System.Windows.Forms.DataGridView();
            button3 = new System.Windows.Forms.Button();
            button1 = new System.Windows.Forms.Button();
            button2 = new System.Windows.Forms.Button();
            btnSnooze = new System.Windows.Forms.Button();
            label5 = new System.Windows.Forms.Label();
            txtNote = new System.Windows.Forms.TextBox();
            label6 = new System.Windows.Forms.Label();
            txtMonths = new System.Windows.Forms.TextBox();
            datapickerOdlozenie = new System.Windows.Forms.DateTimePicker();
            btnNoContact = new System.Windows.Forms.Button();
            datagridWpisy = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView2).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView3).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView4).BeginInit();
            ((System.ComponentModel.ISupportInitialize)datagridWpisy).BeginInit();
            SuspendLayout();
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Location = new System.Drawing.Point(12, 47);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowTemplate.Height = 25;
            dataGridView1.Size = new System.Drawing.Size(592, 719);
            dataGridView1.TabIndex = 0;
            dataGridView1.CellClick += dataGridView1_CellClick;
            dataGridView1.CellFormatting += dataGridView1_CellFormatting_1;
            // 
            // textBox1
            // 
            textBox1.Location = new System.Drawing.Point(12, 18);
            textBox1.Name = "textBox1";
            textBox1.Size = new System.Drawing.Size(100, 23);
            textBox1.TabIndex = 1;
            textBox1.TextChanged += textBox1_TextChanged;
            // 
            // label18
            // 
            label18.Location = new System.Drawing.Point(12, 2);
            label18.Name = "label18";
            label18.Size = new System.Drawing.Size(100, 13);
            label18.TabIndex = 29;
            label18.Text = "Szukaj";
            label18.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label2
            // 
            label2.Location = new System.Drawing.Point(118, 2);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(100, 13);
            label2.TabIndex = 40;
            label2.Text = "Sztuki Wstawione";
            label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // sumaSztuk
            // 
            sumaSztuk.Location = new System.Drawing.Point(118, 18);
            sumaSztuk.Name = "sumaSztuk";
            sumaSztuk.Size = new System.Drawing.Size(100, 23);
            sumaSztuk.TabIndex = 39;
            // 
            // dataGridView2
            // 
            dataGridView2.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView2.Location = new System.Drawing.Point(610, 446);
            dataGridView2.Name = "dataGridView2";
            dataGridView2.RowTemplate.Height = 25;
            dataGridView2.Size = new System.Drawing.Size(485, 154);
            dataGridView2.TabIndex = 41;
            // 
            // label1
            // 
            label1.Location = new System.Drawing.Point(610, 425);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(485, 18);
            label1.TabIndex = 42;
            label1.Text = "Zaplanowane dostawy ze wtawienia";
            label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // dataGridView3
            // 
            dataGridView3.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView3.Location = new System.Drawing.Point(610, 18);
            dataGridView3.Name = "dataGridView3";
            dataGridView3.RowTemplate.Height = 25;
            dataGridView3.Size = new System.Drawing.Size(485, 405);
            dataGridView3.TabIndex = 43;
            dataGridView3.CellClick += dataGridView3_CellClick;
            dataGridView3.CellDoubleClick += dataGridView3_CellDoubleClick;
            // 
            // label3
            // 
            label3.Location = new System.Drawing.Point(610, -2);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(485, 17);
            label3.TabIndex = 44;
            label3.Text = "Ostatnie wstawianie hodowcy";
            label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label4
            // 
            label4.Location = new System.Drawing.Point(619, 609);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(485, 17);
            label4.TabIndex = 46;
            label4.Text = "Ostatnie wstawianie hodowcy";
            label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // dataGridView4
            // 
            dataGridView4.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView4.Location = new System.Drawing.Point(610, 629);
            dataGridView4.Name = "dataGridView4";
            dataGridView4.RowTemplate.Height = 25;
            dataGridView4.Size = new System.Drawing.Size(485, 137);
            dataGridView4.TabIndex = 45;
            // 
            // button3
            // 
            button3.BackColor = System.Drawing.SystemColors.ActiveCaptionText;
            button3.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            button3.Location = new System.Drawing.Point(466, 14);
            button3.Name = "button3";
            button3.Size = new System.Drawing.Size(115, 27);
            button3.TabIndex = 100;
            button3.Text = "Usuń wstawienie";
            button3.UseVisualStyleBackColor = false;
            button3.Click += button3_Click;
            // 
            // button1
            // 
            button1.BackColor = System.Drawing.Color.PaleGreen;
            button1.ForeColor = System.Drawing.SystemColors.ActiveCaptionText;
            button1.Location = new System.Drawing.Point(224, 14);
            button1.Name = "button1";
            button1.Size = new System.Drawing.Size(115, 27);
            button1.TabIndex = 101;
            button1.Text = "Dodaj Wstawienie";
            button1.UseVisualStyleBackColor = false;
            button1.Click += button1_Click;
            // 
            // button2
            // 
            button2.BackColor = System.Drawing.Color.Gold;
            button2.ForeColor = System.Drawing.SystemColors.ActiveCaptionText;
            button2.Location = new System.Drawing.Point(345, 14);
            button2.Name = "button2";
            button2.Size = new System.Drawing.Size(115, 27);
            button2.TabIndex = 102;
            button2.Text = "Modyfi.wstawienie";
            button2.UseVisualStyleBackColor = false;
            button2.Click += button2_Click;
            // 
            // btnSnooze
            // 
            btnSnooze.BackColor = System.Drawing.Color.PaleGreen;
            btnSnooze.ForeColor = System.Drawing.SystemColors.ActiveCaptionText;
            btnSnooze.Location = new System.Drawing.Point(1099, 347);
            btnSnooze.Name = "btnSnooze";
            btnSnooze.Size = new System.Drawing.Size(120, 76);
            btnSnooze.TabIndex = 107;
            btnSnooze.Text = "Przełóż";
            btnSnooze.UseVisualStyleBackColor = false;
            btnSnooze.Click += btnSnooze_Click;
            // 
            // label5
            // 
            label5.Location = new System.Drawing.Point(1099, 218);
            label5.Name = "label5";
            label5.Size = new System.Drawing.Size(100, 13);
            label5.TabIndex = 106;
            label5.Text = "Notatka";
            label5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtNote
            // 
            txtNote.Location = new System.Drawing.Point(1099, 249);
            txtNote.Multiline = true;
            txtNote.Name = "txtNote";
            txtNote.Size = new System.Drawing.Size(179, 76);
            txtNote.TabIndex = 105;
            // 
            // label6
            // 
            label6.Location = new System.Drawing.Point(1101, 113);
            label6.Name = "label6";
            label6.Size = new System.Drawing.Size(100, 36);
            label6.TabIndex = 104;
            label6.Text = "Za ile miesięcy zadzwonić";
            label6.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtMonths
            // 
            txtMonths.Location = new System.Drawing.Point(1101, 152);
            txtMonths.Name = "txtMonths";
            txtMonths.Size = new System.Drawing.Size(100, 23);
            txtMonths.TabIndex = 103;
            txtMonths.TextChanged += txtMonths_TextChanged_1;
            // 
            // datapickerOdlozenie
            // 
            datapickerOdlozenie.Location = new System.Drawing.Point(1099, 180);
            datapickerOdlozenie.Name = "datapickerOdlozenie";
            datapickerOdlozenie.Size = new System.Drawing.Size(228, 23);
            datapickerOdlozenie.TabIndex = 109;
            // 
            // btnNoContact
            // 
            btnNoContact.BackColor = System.Drawing.Color.LightSlateGray;
            btnNoContact.Font = new System.Drawing.Font("Segoe UI", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnNoContact.ForeColor = System.Drawing.SystemColors.Control;
            btnNoContact.Location = new System.Drawing.Point(1101, 18);
            btnNoContact.Name = "btnNoContact";
            btnNoContact.Size = new System.Drawing.Size(120, 76);
            btnNoContact.TabIndex = 110;
            btnNoContact.Text = "Nie odberał";
            btnNoContact.UseVisualStyleBackColor = false;
            btnNoContact.Click += btnNoContact_Click;
            // 
            // datagridWpisy
            // 
            datagridWpisy.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            datagridWpisy.Location = new System.Drawing.Point(12, 782);
            datagridWpisy.Name = "datagridWpisy";
            datagridWpisy.RowTemplate.Height = 25;
            datagridWpisy.Size = new System.Drawing.Size(1083, 193);
            datagridWpisy.TabIndex = 111;
            // 
            // WidokWstawienia
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1330, 982);
            Controls.Add(datagridWpisy);
            Controls.Add(btnNoContact);
            Controls.Add(datapickerOdlozenie);
            Controls.Add(btnSnooze);
            Controls.Add(label5);
            Controls.Add(txtNote);
            Controls.Add(label6);
            Controls.Add(txtMonths);
            Controls.Add(button2);
            Controls.Add(button1);
            Controls.Add(button3);
            Controls.Add(label4);
            Controls.Add(dataGridView4);
            Controls.Add(label3);
            Controls.Add(dataGridView3);
            Controls.Add(label1);
            Controls.Add(dataGridView2);
            Controls.Add(label2);
            Controls.Add(sumaSztuk);
            Controls.Add(label18);
            Controls.Add(textBox1);
            Controls.Add(dataGridView1);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Name = "WidokWstawienia";
            Text = "Wstawienia";
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView2).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView3).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView4).EndInit();
            ((System.ComponentModel.ISupportInitialize)datagridWpisy).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox sumaSztuk;
        private System.Windows.Forms.DataGridView dataGridView2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.DataGridView dataGridView3;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.DataGridView dataGridView4;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button btnSnooze;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox txtNote;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox txtMonths;
        private System.Windows.Forms.DateTimePicker datapickerOdlozenie;
        private System.Windows.Forms.Button btnNoContact;
        private System.Windows.Forms.DataGridView datagridWpisy;
    }
}