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
            ID = new System.Windows.Forms.DataGridViewTextBoxColumn();
            Nr = new System.Windows.Forms.DataGridViewTextBoxColumn();
            Dostawca = new System.Windows.Forms.DataGridViewTextBoxColumn();
            RealDostawca = new System.Windows.Forms.DataGridViewTextBoxColumn();
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
            PiK = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            Ubytek = new System.Windows.Forms.DataGridViewTextBoxColumn();
            button1 = new System.Windows.Forms.Button();
            dataGridView2 = new System.Windows.Forms.DataGridView();
            btnLoadData = new System.Windows.Forms.Button();
            buttonDown = new System.Windows.Forms.Button();
            buttonUP = new System.Windows.Forms.Button();
            buttonBon = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView2).BeginInit();
            SuspendLayout();
            // 
            // dateTimePicker1
            // 
            dateTimePicker1.Location = new System.Drawing.Point(12, 12);
            dateTimePicker1.Name = "dateTimePicker1";
            dateTimePicker1.Size = new System.Drawing.Size(245, 23);
            dateTimePicker1.TabIndex = 0;
            dateTimePicker1.ValueChanged += dateTimePicker1_ValueChanged;
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { ID, Nr, Dostawca, RealDostawca, SztukiDek, Padle, CH, NW, ZM, BruttoHodowcy, TaraHodowcy, NettoHodowcy, BruttoUbojni, TaraUbojni, NettoUbojni, LUMEL, SztukiWybijak, KilogramyWybijak, Cena, TypCeny, PiK, Ubytek });
            dataGridView1.Location = new System.Drawing.Point(12, 81);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.Size = new System.Drawing.Size(1469, 521);
            dataGridView1.TabIndex = 1;
            dataGridView1.CellClick += dataGridView1_CellClick;
            dataGridView1.CellContentClick += dataGridView1_CellContentClick;
            dataGridView1.CellDoubleClick += DataGridView1_CellDoubleClick;
            dataGridView1.CellEndEdit += dataGridView1_CellEndEdit;
            dataGridView1.CellValueChanged += dataGridView1_CellValueChanged;
            dataGridView1.RowPostPaint += dataGridView1_RowPostPaint;
            // 
            // ID
            // 
            ID.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            ID.HeaderText = "ID";
            ID.Name = "ID";
            ID.Width = 45;
            // 
            // Nr
            // 
            Nr.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            Nr.HeaderText = "Nr";
            Nr.Name = "Nr";
            Nr.Width = 35;
            // 
            // Dostawca
            // 
            Dostawca.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            Dostawca.HeaderText = "Dostawca";
            Dostawca.Name = "Dostawca";
            Dostawca.Width = 83;
            // 
            // RealDostawca
            // 
            RealDostawca.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            RealDostawca.HeaderText = "Prawdziwy";
            RealDostawca.Name = "RealDostawca";
            RealDostawca.Width = 88;
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
            BruttoHodowcy.FillWeight = 55F;
            BruttoHodowcy.HeaderText = "Brutto Hodowcy";
            BruttoHodowcy.Name = "BruttoHodowcy";
            BruttoHodowcy.Width = 55;
            // 
            // TaraHodowcy
            // 
            TaraHodowcy.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            TaraHodowcy.HeaderText = "Tara Hodowcy";
            TaraHodowcy.Name = "TaraHodowcy";
            TaraHodowcy.Width = 55;
            // 
            // NettoHodowcy
            // 
            NettoHodowcy.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            NettoHodowcy.HeaderText = "Netto Hodowcy";
            NettoHodowcy.Name = "NettoHodowcy";
            NettoHodowcy.Width = 55;
            // 
            // BruttoUbojni
            // 
            BruttoUbojni.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            BruttoUbojni.HeaderText = "Brutto Ubojni";
            BruttoUbojni.Name = "BruttoUbojni";
            BruttoUbojni.Width = 55;
            // 
            // TaraUbojni
            // 
            TaraUbojni.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            TaraUbojni.HeaderText = "Tara Ubojni";
            TaraUbojni.Name = "TaraUbojni";
            TaraUbojni.Width = 55;
            // 
            // NettoUbojni
            // 
            NettoUbojni.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            NettoUbojni.HeaderText = "Netto Ubojni";
            NettoUbojni.Name = "NettoUbojni";
            NettoUbojni.Width = 55;
            // 
            // LUMEL
            // 
            LUMEL.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            LUMEL.HeaderText = "Sztuki LUMEL";
            LUMEL.Name = "LUMEL";
            LUMEL.Width = 50;
            // 
            // SztukiWybijak
            // 
            SztukiWybijak.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            SztukiWybijak.HeaderText = "Sztuki Wybijak";
            SztukiWybijak.Name = "SztukiWybijak";
            SztukiWybijak.Width = 50;
            // 
            // KilogramyWybijak
            // 
            KilogramyWybijak.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            KilogramyWybijak.HeaderText = "KG Wybijak";
            KilogramyWybijak.Name = "KilogramyWybijak";
            KilogramyWybijak.Width = 50;
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
            TypCeny.Width = 60;
            // 
            // PiK
            // 
            PiK.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            PiK.HeaderText = "Czy odliczamy kg za PiK?";
            PiK.Name = "PiK";
            PiK.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            PiK.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            PiK.Width = 70;
            // 
            // Ubytek
            // 
            Ubytek.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            Ubytek.HeaderText = "Ubytek";
            Ubytek.Name = "Ubytek";
            Ubytek.Width = 65;
            // 
            // button1
            // 
            button1.BackgroundImage = Properties.Resources.Printer1;
            button1.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            button1.Location = new System.Drawing.Point(427, 14);
            button1.Name = "button1";
            button1.Size = new System.Drawing.Size(90, 45);
            button1.TabIndex = 2;
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // dataGridView2
            // 
            dataGridView2.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView2.Location = new System.Drawing.Point(1487, 50);
            dataGridView2.Name = "dataGridView2";
            dataGridView2.Size = new System.Drawing.Size(264, 552);
            dataGridView2.TabIndex = 3;
            // 
            // btnLoadData
            // 
            btnLoadData.Location = new System.Drawing.Point(1487, 21);
            btnLoadData.Name = "btnLoadData";
            btnLoadData.Size = new System.Drawing.Size(75, 23);
            btnLoadData.TabIndex = 4;
            btnLoadData.Text = "Pokaż LUMEL";
            btnLoadData.UseVisualStyleBackColor = true;
            btnLoadData.Click += btnLoadData_Click_1;
            // 
            // buttonDown
            // 
            buttonDown.Location = new System.Drawing.Point(262, 38);
            buttonDown.Margin = new System.Windows.Forms.Padding(2);
            buttonDown.Name = "buttonDown";
            buttonDown.Size = new System.Drawing.Size(58, 23);
            buttonDown.TabIndex = 6;
            buttonDown.Text = "Dol";
            buttonDown.UseVisualStyleBackColor = true;
            buttonDown.Click += buttonDown_Click;
            // 
            // buttonUP
            // 
            buttonUP.Location = new System.Drawing.Point(262, 11);
            buttonUP.Margin = new System.Windows.Forms.Padding(2);
            buttonUP.Name = "buttonUP";
            buttonUP.Size = new System.Drawing.Size(58, 23);
            buttonUP.TabIndex = 5;
            buttonUP.Text = "Gora";
            buttonUP.UseVisualStyleBackColor = true;
            buttonUP.Click += buttonUP_Click;
            // 
            // buttonBon
            // 
            buttonBon.BackColor = System.Drawing.SystemColors.ActiveCaption;
            buttonBon.BackgroundImage = Properties.Resources.avilog;
            buttonBon.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            buttonBon.Location = new System.Drawing.Point(325, 12);
            buttonBon.Name = "buttonBon";
            buttonBon.Size = new System.Drawing.Size(96, 47);
            buttonBon.TabIndex = 7;
            buttonBon.UseVisualStyleBackColor = false;
            buttonBon.Click += buttonBon_Click;
            // 
            // WidokSpecyfikacje
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1831, 690);
            Controls.Add(buttonBon);
            Controls.Add(buttonDown);
            Controls.Add(buttonUP);
            Controls.Add(btnLoadData);
            Controls.Add(dataGridView2);
            Controls.Add(button1);
            Controls.Add(dataGridView1);
            Controls.Add(dateTimePicker1);
            Name = "WidokSpecyfikacje";
            Text = "Form1";
            Load += WidokSpecyfikacje_Load;
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView2).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.DateTimePicker dateTimePicker1;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.DataGridView dataGridView2;
        private System.Windows.Forms.Button btnLoadData;
        private System.Windows.Forms.Button buttonDown;
        private System.Windows.Forms.Button buttonUP;
        private System.Windows.Forms.DataGridViewTextBoxColumn ID;
        private System.Windows.Forms.DataGridViewTextBoxColumn Nr;
        private System.Windows.Forms.DataGridViewTextBoxColumn Dostawca;
        private System.Windows.Forms.DataGridViewTextBoxColumn RealDostawca;
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
        private System.Windows.Forms.DataGridViewCheckBoxColumn PiK;
        private System.Windows.Forms.DataGridViewTextBoxColumn Ubytek;
        private System.Windows.Forms.Button buttonBon;
    }
}