using System.Windows.Forms;

namespace Kalendarz1
{
    partial class UmowyForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            CommandButton_Update = new Button();
            Vatowiec = new CheckBox();
            PaszaPisklak = new ComboBox();
            CzyjaWaga = new ComboBox();
            KonfPadl = new ComboBox();
            typCeny = new ComboBox();
            Address1 = new TextBox();
            Address2 = new TextBox();
            NIP = new TextBox();
            REGON = new TextBox();
            PESEL = new TextBox();
            Phone1 = new TextBox();
            Phone2 = new TextBox();
            Info1 = new TextBox();
            Info2 = new TextBox();
            Email = new TextBox();
            Cena = new TextBox();
            NrGosp = new TextBox();
            Ubytek = new TextBox();
            srednia = new TextBox();
            sztuki = new TextBox();
            IRZPlus = new TextBox();
            PostalCode = new TextBox();
            Address = new TextBox();
            City = new TextBox();
            IDLibra = new TextBox();
            ComboBox1 = new ComboBox();
            label3 = new Label();
            label9 = new Label();
            label10 = new Label();
            label11 = new Label();
            label12 = new Label();
            label13 = new Label();
            label14 = new Label();
            label15 = new Label();
            label16 = new Label();
            label18 = new Label();
            label19 = new Label();
            label20 = new Label();
            label21 = new Label();
            label17 = new Label();
            label22 = new Label();
            label23 = new Label();
            label24 = new Label();
            label5 = new Label();
            label25 = new Label();
            label6 = new Label();
            label8 = new Label();
            label7 = new Label();
            label26 = new Label();
            label27 = new Label();
            label28 = new Label();
            label1 = new Label();
            dtpData = new DateTimePicker();
            dtpDataPodpisania = new DateTimePicker();
            textBoxFiltrKontrahent = new TextBox();
            dataGridViewKontrahenci = new DataGridView();
            dataGridViewHodowcy = new DataGridView();
            label2 = new Label();
            label4 = new Label();
            label29 = new Label();
            label30 = new Label();
            label31 = new Label();
            Dostawca = new TextBox();
            comboBoxDostawca = new ComboBox();
            Dostawca1 = new TextBox();
            ((System.ComponentModel.ISupportInitialize)dataGridViewKontrahenci).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewHodowcy).BeginInit();
            SuspendLayout();
            // 
            // CommandButton_Update
            // 
            CommandButton_Update.Font = new System.Drawing.Font("Segoe UI Semibold", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            CommandButton_Update.Location = new System.Drawing.Point(515, 410);
            CommandButton_Update.Name = "CommandButton_Update";
            CommandButton_Update.Size = new System.Drawing.Size(266, 80);
            CommandButton_Update.TabIndex = 6;
            CommandButton_Update.Text = "Utwórz umowę (Word/PDF)";
            CommandButton_Update.UseVisualStyleBackColor = true;
            CommandButton_Update.Click += CommandButton_Update_Click_1;
            // 
            // Vatowiec
            // 
            Vatowiec.AutoSize = true;
            Vatowiec.Location = new System.Drawing.Point(580, 333);
            Vatowiec.Name = "Vatowiec";
            Vatowiec.Size = new System.Drawing.Size(76, 19);
            Vatowiec.TabIndex = 7;
            Vatowiec.Text = "VATowiec";
            Vatowiec.UseVisualStyleBackColor = true;
            // 
            // PaszaPisklak
            // 
            PaszaPisklak.FormattingEnabled = true;
            PaszaPisklak.Location = new System.Drawing.Point(580, 121);
            PaszaPisklak.Name = "PaszaPisklak";
            PaszaPisklak.Size = new System.Drawing.Size(121, 23);
            PaszaPisklak.TabIndex = 8;
            // 
            // CzyjaWaga
            // 
            CzyjaWaga.FormattingEnabled = true;
            CzyjaWaga.Location = new System.Drawing.Point(581, 150);
            CzyjaWaga.Name = "CzyjaWaga";
            CzyjaWaga.Size = new System.Drawing.Size(121, 23);
            CzyjaWaga.TabIndex = 9;
            // 
            // KonfPadl
            // 
            KonfPadl.FormattingEnabled = true;
            KonfPadl.Location = new System.Drawing.Point(581, 181);
            KonfPadl.Name = "KonfPadl";
            KonfPadl.Size = new System.Drawing.Size(121, 23);
            KonfPadl.TabIndex = 10;
            // 
            // typCeny
            // 
            typCeny.FormattingEnabled = true;
            typCeny.Location = new System.Drawing.Point(581, 92);
            typCeny.Name = "typCeny";
            typCeny.Size = new System.Drawing.Size(121, 23);
            typCeny.TabIndex = 11;
            // 
            // Address1
            // 
            Address1.Location = new System.Drawing.Point(136, 64);
            Address1.Name = "Address1";
            Address1.Size = new System.Drawing.Size(161, 23);
            Address1.TabIndex = 12;
            // 
            // Address2
            // 
            Address2.Location = new System.Drawing.Point(136, 93);
            Address2.Name = "Address2";
            Address2.Size = new System.Drawing.Size(161, 23);
            Address2.TabIndex = 13;
            // 
            // NIP
            // 
            NIP.Location = new System.Drawing.Point(136, 122);
            NIP.Name = "NIP";
            NIP.Size = new System.Drawing.Size(161, 23);
            NIP.TabIndex = 14;
            // 
            // REGON
            // 
            REGON.Location = new System.Drawing.Point(136, 180);
            REGON.Name = "REGON";
            REGON.Size = new System.Drawing.Size(161, 23);
            REGON.TabIndex = 15;
            // 
            // PESEL
            // 
            PESEL.Location = new System.Drawing.Point(136, 151);
            PESEL.Name = "PESEL";
            PESEL.Size = new System.Drawing.Size(161, 23);
            PESEL.TabIndex = 16;
            // 
            // Phone1
            // 
            Phone1.Location = new System.Drawing.Point(136, 209);
            Phone1.Name = "Phone1";
            Phone1.Size = new System.Drawing.Size(161, 23);
            Phone1.TabIndex = 17;
            // 
            // Phone2
            // 
            Phone2.Location = new System.Drawing.Point(136, 238);
            Phone2.Name = "Phone2";
            Phone2.Size = new System.Drawing.Size(161, 23);
            Phone2.TabIndex = 18;
            // 
            // Info1
            // 
            Info1.Location = new System.Drawing.Point(300, 209);
            Info1.Name = "Info1";
            Info1.Size = new System.Drawing.Size(143, 23);
            Info1.TabIndex = 19;
            // 
            // Info2
            // 
            Info2.Location = new System.Drawing.Point(300, 238);
            Info2.Name = "Info2";
            Info2.Size = new System.Drawing.Size(143, 23);
            Info2.TabIndex = 20;
            // 
            // Email
            // 
            Email.Location = new System.Drawing.Point(136, 267);
            Email.Name = "Email";
            Email.Size = new System.Drawing.Size(307, 23);
            Email.TabIndex = 21;
            // 
            // Cena
            // 
            Cena.Location = new System.Drawing.Point(581, 210);
            Cena.Name = "Cena";
            Cena.Size = new System.Drawing.Size(100, 23);
            Cena.TabIndex = 22;
            // 
            // NrGosp
            // 
            NrGosp.Location = new System.Drawing.Point(136, 296);
            NrGosp.Name = "NrGosp";
            NrGosp.Size = new System.Drawing.Size(161, 23);
            NrGosp.TabIndex = 23;
            // 
            // Ubytek
            // 
            Ubytek.Location = new System.Drawing.Point(581, 297);
            Ubytek.Name = "Ubytek";
            Ubytek.Size = new System.Drawing.Size(100, 23);
            Ubytek.TabIndex = 24;
            // 
            // srednia
            // 
            srednia.Location = new System.Drawing.Point(581, 239);
            srednia.Name = "srednia";
            srednia.Size = new System.Drawing.Size(100, 23);
            srednia.TabIndex = 25;
            // 
            // sztuki
            // 
            sztuki.Location = new System.Drawing.Point(580, 268);
            sztuki.Name = "sztuki";
            sztuki.Size = new System.Drawing.Size(100, 23);
            sztuki.TabIndex = 26;
            // 
            // IRZPlus
            // 
            IRZPlus.Location = new System.Drawing.Point(136, 325);
            IRZPlus.Name = "IRZPlus";
            IRZPlus.Size = new System.Drawing.Size(161, 23);
            IRZPlus.TabIndex = 28;
            // 
            // PostalCode
            // 
            PostalCode.Location = new System.Drawing.Point(136, 354);
            PostalCode.Name = "PostalCode";
            PostalCode.Size = new System.Drawing.Size(161, 23);
            PostalCode.TabIndex = 29;
            // 
            // Address
            // 
            Address.Location = new System.Drawing.Point(136, 383);
            Address.Name = "Address";
            Address.Size = new System.Drawing.Size(161, 23);
            Address.TabIndex = 30;
            // 
            // City
            // 
            City.Location = new System.Drawing.Point(136, 412);
            City.Name = "City";
            City.Size = new System.Drawing.Size(161, 23);
            City.TabIndex = 31;
            // 
            // IDLibra
            // 
            IDLibra.Location = new System.Drawing.Point(136, 441);
            IDLibra.Name = "IDLibra";
            IDLibra.Size = new System.Drawing.Size(161, 23);
            IDLibra.TabIndex = 32;
            // 
            // ComboBox1
            // 
            ComboBox1.BackColor = System.Drawing.Color.DarkGray;
            ComboBox1.FormattingEnabled = true;
            ComboBox1.Location = new System.Drawing.Point(581, 8);
            ComboBox1.Name = "ComboBox1";
            ComboBox1.Size = new System.Drawing.Size(121, 23);
            ComboBox1.TabIndex = 5;
            // 
            // label3
            // 
            label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label3.Location = new System.Drawing.Point(12, 9);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(118, 22);
            label3.TabIndex = 87;
            label3.Text = "Wybór";
            label3.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label9
            // 
            label9.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label9.Location = new System.Drawing.Point(454, 36);
            label9.Name = "label9";
            label9.Size = new System.Drawing.Size(121, 23);
            label9.TabIndex = 95;
            label9.Text = "Data Odbioru";
            label9.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label10
            // 
            label10.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label10.Location = new System.Drawing.Point(454, 63);
            label10.Name = "label10";
            label10.Size = new System.Drawing.Size(121, 23);
            label10.TabIndex = 96;
            label10.Text = "Data Podpisania";
            label10.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label11
            // 
            label11.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label11.Location = new System.Drawing.Point(12, 65);
            label11.Name = "label11";
            label11.Size = new System.Drawing.Size(118, 22);
            label11.TabIndex = 97;
            label11.Text = "Ulica";
            label11.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label12
            // 
            label12.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label12.Location = new System.Drawing.Point(12, 94);
            label12.Name = "label12";
            label12.Size = new System.Drawing.Size(118, 22);
            label12.TabIndex = 98;
            label12.Text = "Kod Pocztowy";
            label12.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label13
            // 
            label13.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label13.Location = new System.Drawing.Point(12, 123);
            label13.Name = "label13";
            label13.Size = new System.Drawing.Size(118, 22);
            label13.TabIndex = 99;
            label13.Text = "NIP";
            label13.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label14
            // 
            label14.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label14.Location = new System.Drawing.Point(12, 152);
            label14.Name = "label14";
            label14.Size = new System.Drawing.Size(118, 22);
            label14.TabIndex = 100;
            label14.Text = "Pesel";
            label14.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label15
            // 
            label15.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label15.Location = new System.Drawing.Point(12, 178);
            label15.Name = "label15";
            label15.Size = new System.Drawing.Size(118, 22);
            label15.TabIndex = 101;
            label15.Text = "REGON";
            label15.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label16
            // 
            label16.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label16.Location = new System.Drawing.Point(12, 210);
            label16.Name = "label16";
            label16.Size = new System.Drawing.Size(118, 22);
            label16.TabIndex = 102;
            label16.Text = "Numer kontaktowy 1";
            label16.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label18
            // 
            label18.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label18.Location = new System.Drawing.Point(12, 239);
            label18.Name = "label18";
            label18.Size = new System.Drawing.Size(118, 22);
            label18.TabIndex = 104;
            label18.Text = "Numer kontaktowy 2";
            label18.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label19
            // 
            label19.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label19.Location = new System.Drawing.Point(12, 268);
            label19.Name = "label19";
            label19.Size = new System.Drawing.Size(118, 22);
            label19.TabIndex = 105;
            label19.Text = "Email";
            label19.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label20
            // 
            label20.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label20.Location = new System.Drawing.Point(12, 296);
            label20.Name = "label20";
            label20.Size = new System.Drawing.Size(118, 22);
            label20.TabIndex = 106;
            label20.Text = "Nr Gospodarstwa";
            label20.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label21
            // 
            label21.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label21.Location = new System.Drawing.Point(12, 326);
            label21.Name = "label21";
            label21.Size = new System.Drawing.Size(118, 22);
            label21.TabIndex = 107;
            label21.Text = "IRZPlus";
            label21.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label17
            // 
            label17.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label17.Location = new System.Drawing.Point(12, 355);
            label17.Name = "label17";
            label17.Size = new System.Drawing.Size(118, 22);
            label17.TabIndex = 108;
            label17.Text = "Adres Kurnika";
            label17.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label22
            // 
            label22.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label22.Location = new System.Drawing.Point(12, 384);
            label22.Name = "label22";
            label22.Size = new System.Drawing.Size(118, 22);
            label22.TabIndex = 109;
            label22.Text = "Kod pocztowy kurnika";
            label22.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label23
            // 
            label23.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label23.Location = new System.Drawing.Point(12, 413);
            label23.Name = "label23";
            label23.Size = new System.Drawing.Size(118, 22);
            label23.TabIndex = 110;
            label23.Text = "Miejscowość kurnika";
            label23.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label24
            // 
            label24.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label24.Location = new System.Drawing.Point(12, 442);
            label24.Name = "label24";
            label24.Size = new System.Drawing.Size(118, 22);
            label24.TabIndex = 111;
            label24.Text = "Id Libra";
            label24.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label5
            // 
            label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label5.Location = new System.Drawing.Point(454, 92);
            label5.Name = "label5";
            label5.Size = new System.Drawing.Size(121, 23);
            label5.TabIndex = 112;
            label5.Text = "Typ Ceny";
            label5.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label25
            // 
            label25.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label25.Location = new System.Drawing.Point(454, 119);
            label25.Name = "label25";
            label25.Size = new System.Drawing.Size(121, 23);
            label25.TabIndex = 113;
            label25.Text = "Czy jest Pasza/Pisklak";
            label25.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label6
            // 
            label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label6.Location = new System.Drawing.Point(454, 148);
            label6.Name = "label6";
            label6.Size = new System.Drawing.Size(121, 23);
            label6.TabIndex = 114;
            label6.Text = "Waga Samochodowa";
            label6.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label8
            // 
            label8.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label8.Location = new System.Drawing.Point(454, 179);
            label8.Name = "label8";
            label8.Size = new System.Drawing.Size(121, 23);
            label8.TabIndex = 115;
            label8.Text = "Obiążenie padłymi";
            label8.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label7
            // 
            label7.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label7.Location = new System.Drawing.Point(454, 210);
            label7.Name = "label7";
            label7.Size = new System.Drawing.Size(121, 23);
            label7.TabIndex = 116;
            label7.Text = "Cena";
            label7.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label26
            // 
            label26.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label26.Location = new System.Drawing.Point(454, 237);
            label26.Name = "label26";
            label26.Size = new System.Drawing.Size(121, 23);
            label26.TabIndex = 117;
            label26.Text = "Waga";
            label26.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label27
            // 
            label27.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label27.Location = new System.Drawing.Point(454, 266);
            label27.Name = "label27";
            label27.Size = new System.Drawing.Size(121, 23);
            label27.TabIndex = 118;
            label27.Text = "Sztuki";
            label27.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label28
            // 
            label28.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label28.Location = new System.Drawing.Point(454, 9);
            label28.Name = "label28";
            label28.Size = new System.Drawing.Size(121, 23);
            label28.TabIndex = 119;
            label28.Text = "Identyfikator";
            label28.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label1
            // 
            label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label1.Location = new System.Drawing.Point(454, 295);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(121, 23);
            label1.TabIndex = 120;
            label1.Text = "Ubytek";
            label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // dtpData
            // 
            dtpData.Location = new System.Drawing.Point(580, 35);
            dtpData.Name = "dtpData";
            dtpData.Size = new System.Drawing.Size(200, 23);
            dtpData.TabIndex = 121;
            // 
            // dtpDataPodpisania
            // 
            dtpDataPodpisania.Location = new System.Drawing.Point(581, 64);
            dtpDataPodpisania.Name = "dtpDataPodpisania";
            dtpDataPodpisania.Size = new System.Drawing.Size(200, 23);
            dtpDataPodpisania.TabIndex = 122;
            // 
            // textBoxFiltrKontrahent
            // 
            textBoxFiltrKontrahent.Location = new System.Drawing.Point(610, 358);
            textBoxFiltrKontrahent.Name = "textBoxFiltrKontrahent";
            textBoxFiltrKontrahent.Size = new System.Drawing.Size(161, 23);
            textBoxFiltrKontrahent.TabIndex = 123;
            // 
            // dataGridViewKontrahenci
            // 
            dataGridViewKontrahenci.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewKontrahenci.Location = new System.Drawing.Point(786, 321);
            dataGridViewKontrahenci.Name = "dataGridViewKontrahenci";
            dataGridViewKontrahenci.RowTemplate.Height = 25;
            dataGridViewKontrahenci.Size = new System.Drawing.Size(749, 169);
            dataGridViewKontrahenci.TabIndex = 124;
            // 
            // dataGridViewHodowcy
            // 
            dataGridViewHodowcy.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewHodowcy.Location = new System.Drawing.Point(786, 63);
            dataGridViewHodowcy.Name = "dataGridViewHodowcy";
            dataGridViewHodowcy.RowTemplate.Height = 25;
            dataGridViewHodowcy.Size = new System.Drawing.Size(749, 198);
            dataGridViewHodowcy.TabIndex = 125;
            // 
            // label2
            // 
            label2.BackColor = System.Drawing.SystemColors.MenuText;
            label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label2.ForeColor = System.Drawing.Color.Coral;
            label2.Location = new System.Drawing.Point(443, 358);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(161, 23);
            label2.TabIndex = 126;
            label2.Text = "Szukaj dane hodowcy:";
            label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label4
            // 
            label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label4.Location = new System.Drawing.Point(356, 457);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(121, 23);
            label4.TabIndex = 127;
            label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label29
            // 
            label29.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label29.Location = new System.Drawing.Point(1008, 2);
            label29.Name = "label29";
            label29.Size = new System.Drawing.Size(353, 57);
            label29.TabIndex = 128;
            label29.Text = "Tabela z 'Dane Hodowcy'";
            label29.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label30
            // 
            label30.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label30.Location = new System.Drawing.Point(1008, 261);
            label30.Name = "label30";
            label30.Size = new System.Drawing.Size(353, 57);
            label30.TabIndex = 129;
            label30.Text = "Tabela z bazy danych Symfonia";
            label30.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label31
            // 
            label31.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label31.Location = new System.Drawing.Point(12, 37);
            label31.Name = "label31";
            label31.Size = new System.Drawing.Size(118, 22);
            label31.TabIndex = 131;
            label31.Text = "Hodowca";
            label31.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // Dostawca
            // 
            Dostawca.Location = new System.Drawing.Point(136, 36);
            Dostawca.Name = "Dostawca";
            Dostawca.Size = new System.Drawing.Size(161, 23);
            Dostawca.TabIndex = 130;
            // 
            // comboBoxDostawca
            // 
            comboBoxDostawca.DropDownHeight = 300;
            comboBoxDostawca.DropDownWidth = 200;
            comboBoxDostawca.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            comboBoxDostawca.FormattingEnabled = true;
            comboBoxDostawca.IntegralHeight = false;
            comboBoxDostawca.Location = new System.Drawing.Point(136, 10);
            comboBoxDostawca.Name = "comboBoxDostawca";
            comboBoxDostawca.Size = new System.Drawing.Size(161, 20);
            comboBoxDostawca.TabIndex = 132;
            comboBoxDostawca.SelectedIndexChanged += comboBoxDostawca_SelectedIndexChanged;
            // 
            // Dostawca1
            // 
            Dostawca1.Location = new System.Drawing.Point(300, 8);
            Dostawca1.Name = "Dostawca1";
            Dostawca1.Size = new System.Drawing.Size(140, 23);
            Dostawca1.TabIndex = 133;
            Dostawca1.Visible = false;
            // 
            // UmowyForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1547, 498);
            Controls.Add(Dostawca1);
            Controls.Add(comboBoxDostawca);
            Controls.Add(label31);
            Controls.Add(Dostawca);
            Controls.Add(label30);
            Controls.Add(label29);
            Controls.Add(label4);
            Controls.Add(label2);
            Controls.Add(dataGridViewHodowcy);
            Controls.Add(dataGridViewKontrahenci);
            Controls.Add(textBoxFiltrKontrahent);
            Controls.Add(dtpDataPodpisania);
            Controls.Add(dtpData);
            Controls.Add(label1);
            Controls.Add(label28);
            Controls.Add(label27);
            Controls.Add(label26);
            Controls.Add(label7);
            Controls.Add(label8);
            Controls.Add(label6);
            Controls.Add(label25);
            Controls.Add(label5);
            Controls.Add(label24);
            Controls.Add(label23);
            Controls.Add(label22);
            Controls.Add(label17);
            Controls.Add(label21);
            Controls.Add(label20);
            Controls.Add(label19);
            Controls.Add(label18);
            Controls.Add(label16);
            Controls.Add(label15);
            Controls.Add(label14);
            Controls.Add(label13);
            Controls.Add(label12);
            Controls.Add(label11);
            Controls.Add(label10);
            Controls.Add(label9);
            Controls.Add(label3);
            Controls.Add(ComboBox1);
            Controls.Add(CommandButton_Update);
            Controls.Add(Vatowiec);
            Controls.Add(PaszaPisklak);
            Controls.Add(CzyjaWaga);
            Controls.Add(KonfPadl);
            Controls.Add(typCeny);
            Controls.Add(Address1);
            Controls.Add(Address2);
            Controls.Add(NIP);
            Controls.Add(REGON);
            Controls.Add(PESEL);
            Controls.Add(Phone1);
            Controls.Add(Phone2);
            Controls.Add(Info1);
            Controls.Add(Info2);
            Controls.Add(Email);
            Controls.Add(Cena);
            Controls.Add(NrGosp);
            Controls.Add(Ubytek);
            Controls.Add(srednia);
            Controls.Add(sztuki);
            Controls.Add(IRZPlus);
            Controls.Add(PostalCode);
            Controls.Add(Address);
            Controls.Add(City);
            Controls.Add(IDLibra);
            Name = "UmowyForm";
            Text = "UmowyForm";
            Load += UmowyForm_Load_1;
            ((System.ComponentModel.ISupportInitialize)dataGridViewKontrahenci).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewHodowcy).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private Button CommandButton_Update;
        private System.Windows.Forms.CheckBox Vatowiec;

        private ComboBox PaszaPisklak;
        private ComboBox CzyjaWaga;
        private ComboBox KonfPadl;
        private ComboBox typCeny;
        private TextBox Address1;
        private TextBox Address2;
        private TextBox NIP;
        private TextBox REGON;
        private TextBox PESEL;
        private TextBox Phone1;
        private TextBox Phone2;
        private TextBox Info1;
        private TextBox Info2;
        private TextBox Email;
        private TextBox Cena;
        private TextBox NrGosp;
        private TextBox Ubytek;
        private TextBox srednia;
        private TextBox sztuki;
        private TextBox IRZPlus;
        private TextBox PostalCode;
        private TextBox Address;
        private TextBox City;
        private TextBox IDLibra;
        private ComboBox ComboBox1;
        private Label label3;
        private Label label9;
        private Label label10;
        private Label label11;
        private Label label12;
        private Label label13;
        private Label label14;
        private Label label15;
        private Label label16;
        private Label label18;
        private Label label19;
        private Label label20;
        private Label label21;
        private Label label17;
        private Label label22;
        private Label label23;
        private Label label24;
        private Label label5;
        private Label label25;
        private Label label6;
        private Label label8;
        private Label label7;
        private Label label26;
        private Label label27;
        private Label label28;
        private Label label1;
        private DateTimePicker dtpData;
        private DateTimePicker dtpDataPodpisania;
        private TextBox textBoxFiltrKontrahent;
        private DataGridView dataGridViewKontrahenci;
        private DataGridView dataGridViewHodowcy;
        private Label label2;
        private Label label4;
        private Label label29;
        private Label label30;
        private Label label31;
        private TextBox Dostawca;
        private ComboBox comboBoxDostawca;
        private TextBox Dostawca1;
    }
}
