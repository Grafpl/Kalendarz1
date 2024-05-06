namespace Kalendarz1
{
    partial class WidokSpecyfikacje
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
            dateTimePicker1 = new System.Windows.Forms.DateTimePicker();
            dataGridView1 = new System.Windows.Forms.DataGridView();
            button1 = new System.Windows.Forms.Button();
            ID = new System.Windows.Forms.DataGridViewTextBoxColumn();
            Numer = new System.Windows.Forms.DataGridViewTextBoxColumn();
            Dostawca = new System.Windows.Forms.DataGridViewTextBoxColumn();
            SztukiDek = new System.Windows.Forms.DataGridViewTextBoxColumn();
            Padle = new System.Windows.Forms.DataGridViewTextBoxColumn();
            CH = new System.Windows.Forms.DataGridViewTextBoxColumn();
            NW = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ZM = new System.Windows.Forms.DataGridViewTextBoxColumn();
            BruttoHodowcy = new System.Windows.Forms.DataGridViewTextBoxColumn();
            TaraHodowcy = new System.Windows.Forms.DataGridViewTextBoxColumn();
            NettoHodowcy = new System.Windows.Forms.DataGridViewTextBoxColumn();
            BruttoUbojni = new System.Windows.Forms.DataGridViewTextBoxColumn();
            TaraUbojni = new System.Windows.Forms.DataGridViewTextBoxColumn();
            NettoUbojni = new System.Windows.Forms.DataGridViewTextBoxColumn();
            LUMEL = new System.Windows.Forms.DataGridViewTextBoxColumn();
            SztukiWybijak = new System.Windows.Forms.DataGridViewTextBoxColumn();
            KilogramyWybijak = new System.Windows.Forms.DataGridViewTextBoxColumn();
            Cena = new System.Windows.Forms.DataGridViewTextBoxColumn();
            TypCeny = new System.Windows.Forms.DataGridViewTextBoxColumn();
            PiK = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            SuspendLayout();
            // 
            // dateTimePicker1
            // 
            dateTimePicker1.Location = new System.Drawing.Point(12, 12);
            dateTimePicker1.Name = "dateTimePicker1";
            dateTimePicker1.Size = new System.Drawing.Size(200, 23);
            dateTimePicker1.TabIndex = 0;
            dateTimePicker1.ValueChanged += dateTimePicker1_ValueChanged;
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { ID, Numer, Dostawca, SztukiDek, Padle, CH, NW, ZM, BruttoHodowcy, TaraHodowcy, NettoHodowcy, BruttoUbojni, TaraUbojni, NettoUbojni, LUMEL, SztukiWybijak, KilogramyWybijak, Cena, TypCeny, PiK });
            dataGridView1.Location = new System.Drawing.Point(12, 50);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowTemplate.Height = 25;
            dataGridView1.Size = new System.Drawing.Size(1414, 552);
            dataGridView1.TabIndex = 1;
            dataGridView1.CellDoubleClick += DataGridView1_CellDoubleClick;
            dataGridView1.CellEndEdit += dataGridView1_CellEndEdit;
            // 
            // button1
            // 
            button1.Location = new System.Drawing.Point(575, 12);
            button1.Name = "button1";
            button1.Size = new System.Drawing.Size(75, 23);
            button1.TabIndex = 2;
            button1.Text = "button1";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // ID
            // 
            ID.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            ID.HeaderText = "ID";
            ID.Name = "ID";
            ID.Width = 35;
            // 
            // Numer
            // 
            Numer.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            Numer.HeaderText = "Nr";
            Numer.Name = "Numer";
            Numer.Width = 35;
            // 
            // Dostawca
            // 
            Dostawca.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            Dostawca.HeaderText = "Dostawca";
            Dostawca.Name = "Dostawca";
            Dostawca.Width = 200;
            // 
            // SztukiDek
            // 
            SztukiDek.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            SztukiDek.HeaderText = "Sztuki Dek.";
            SztukiDek.Name = "SztukiDek";
            SztukiDek.Width = 50;
            // 
            // Padle
            // 
            Padle.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            Padle.HeaderText = "Padłe";
            Padle.Name = "Padle";
            Padle.Width = 35;
            // 
            // CH
            // 
            CH.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            CH.HeaderText = "CH";
            CH.Name = "CH";
            CH.Width = 30;
            // 
            // NW
            // 
            NW.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            NW.HeaderText = "NW";
            NW.Name = "NW";
            NW.Width = 30;
            // 
            // ZM
            // 
            ZM.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            ZM.HeaderText = "ZM";
            ZM.Name = "ZM";
            ZM.Width = 30;
            // 
            // BruttoHodowcy
            // 
            BruttoHodowcy.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            BruttoHodowcy.FillWeight = 65F;
            BruttoHodowcy.HeaderText = "Brutto Hodowcy";
            BruttoHodowcy.Name = "BruttoHodowcy";
            BruttoHodowcy.Width = 65;
            // 
            // TaraHodowcy
            // 
            TaraHodowcy.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            TaraHodowcy.HeaderText = "Tara Hodowcy";
            TaraHodowcy.Name = "TaraHodowcy";
            TaraHodowcy.Width = 65;
            // 
            // NettoHodowcy
            // 
            NettoHodowcy.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            NettoHodowcy.HeaderText = "Netto Hodowcy";
            NettoHodowcy.Name = "NettoHodowcy";
            NettoHodowcy.Width = 65;
            // 
            // BruttoUbojni
            // 
            BruttoUbojni.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            BruttoUbojni.HeaderText = "Brutto Ubojni";
            BruttoUbojni.Name = "BruttoUbojni";
            BruttoUbojni.Width = 65;
            // 
            // TaraUbojni
            // 
            TaraUbojni.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            TaraUbojni.HeaderText = "Tara Ubojni";
            TaraUbojni.Name = "TaraUbojni";
            TaraUbojni.Width = 65;
            // 
            // NettoUbojni
            // 
            NettoUbojni.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            NettoUbojni.HeaderText = "Netto Ubojni";
            NettoUbojni.Name = "NettoUbojni";
            NettoUbojni.Width = 65;
            // 
            // LUMEL
            // 
            LUMEL.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            LUMEL.HeaderText = "Sztuki LUMEL";
            LUMEL.Name = "LUMEL";
            LUMEL.Width = 60;
            // 
            // SztukiWybijak
            // 
            SztukiWybijak.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            SztukiWybijak.HeaderText = "Sztuki Wybijak";
            SztukiWybijak.Name = "SztukiWybijak";
            SztukiWybijak.Width = 60;
            // 
            // KilogramyWybijak
            // 
            KilogramyWybijak.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            KilogramyWybijak.HeaderText = "KG Wybijak";
            KilogramyWybijak.Name = "KilogramyWybijak";
            KilogramyWybijak.Width = 60;
            // 
            // Cena
            // 
            Cena.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            Cena.HeaderText = "Cena";
            Cena.Name = "Cena";
            Cena.Width = 40;
            // 
            // TypCeny
            // 
            TypCeny.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            TypCeny.HeaderText = "Typ Ceny";
            TypCeny.Name = "TypCeny";
            TypCeny.Width = 85;
            // 
            // PiK
            // 
            PiK.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            PiK.HeaderText = "Czy odliczamy kg za PiK?";
            PiK.Name = "PiK";
            PiK.Width = 70;
            // 
            // WidokSpecyfikacje
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1459, 698);
            Controls.Add(button1);
            Controls.Add(dataGridView1);
            Controls.Add(dateTimePicker1);
            Name = "WidokSpecyfikacje";
            Text = "Form1";
            Load += WidokSpecyfikacje_Load;
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.DateTimePicker dateTimePicker1;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.DataGridViewTextBoxColumn ID;
        private System.Windows.Forms.DataGridViewTextBoxColumn Numer;
        private System.Windows.Forms.DataGridViewTextBoxColumn Dostawca;
        private System.Windows.Forms.DataGridViewTextBoxColumn SztukiDek;
        private System.Windows.Forms.DataGridViewTextBoxColumn Padle;
        private System.Windows.Forms.DataGridViewTextBoxColumn CH;
        private System.Windows.Forms.DataGridViewTextBoxColumn NW;
        private System.Windows.Forms.DataGridViewTextBoxColumn ZM;
        private System.Windows.Forms.DataGridViewTextBoxColumn BruttoHodowcy;
        private System.Windows.Forms.DataGridViewTextBoxColumn TaraHodowcy;
        private System.Windows.Forms.DataGridViewTextBoxColumn NettoHodowcy;
        private System.Windows.Forms.DataGridViewTextBoxColumn BruttoUbojni;
        private System.Windows.Forms.DataGridViewTextBoxColumn TaraUbojni;
        private System.Windows.Forms.DataGridViewTextBoxColumn NettoUbojni;
        private System.Windows.Forms.DataGridViewTextBoxColumn LUMEL;
        private System.Windows.Forms.DataGridViewTextBoxColumn SztukiWybijak;
        private System.Windows.Forms.DataGridViewTextBoxColumn KilogramyWybijak;
        private System.Windows.Forms.DataGridViewTextBoxColumn Cena;
        private System.Windows.Forms.DataGridViewTextBoxColumn TypCeny;
        private System.Windows.Forms.DataGridViewTextBoxColumn PiK;
    }
}