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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UmowyForm));
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
            comboBoxDostawcaS = new ComboBox();
            DostawcaS = new TextBox();
            Address1S = new TextBox();
            Address2S = new TextBox();
            NIPS = new TextBox();
            REGONS = new TextBox();
            PESELS = new TextBox();
            Phone1S = new TextBox();
            Phone2S = new TextBox();
            textBox9 = new TextBox();
            textBox10 = new TextBox();
            EmailS = new TextBox();
            textBox12 = new TextBox();
            textBox13 = new TextBox();
            textBox14 = new TextBox();
            textBox15 = new TextBox();
            textBox16 = new TextBox();
            IDLibraS = new TextBox();
            label34 = new Label();
            label35 = new Label();
            label36 = new Label();
            label37 = new Label();
            label38 = new Label();
            label39 = new Label();
            label40 = new Label();
            label41 = new Label();
            label42 = new Label();
            label43 = new Label();
            label44 = new Label();
            label45 = new Label();
            label46 = new Label();
            label47 = new Label();
            label48 = new Label();
            label49 = new Label();
            numer = new TextBox();
            buttonZapisz = new Button();
            buttonZapiszDaneLibra = new Button();
            button1 = new Button();
            button2 = new Button();
            groupBox1 = new GroupBox();
            groupBox2 = new GroupBox();
            pictureBox4 = new PictureBox();
            pictureBox7 = new PictureBox();
            pictureBox10 = new PictureBox();
            pictureBox11 = new PictureBox();
            dodatek = new TextBox();
            label32 = new Label();
            ((System.ComponentModel.ISupportInitialize)dataGridViewKontrahenci).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewHodowcy).BeginInit();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox4).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox7).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox10).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox11).BeginInit();
            SuspendLayout();
            // 
            // CommandButton_Update
            // 
            CommandButton_Update.Font = new System.Drawing.Font("Segoe UI Semibold", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            CommandButton_Update.Location = new System.Drawing.Point(42, 471);
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
            Vatowiec.Location = new System.Drawing.Point(142, 446);
            Vatowiec.Name = "Vatowiec";
            Vatowiec.Size = new System.Drawing.Size(76, 19);
            Vatowiec.TabIndex = 7;
            Vatowiec.Text = "VATowiec";
            Vatowiec.UseVisualStyleBackColor = true;
            // 
            // PaszaPisklak
            // 
            PaszaPisklak.FormattingEnabled = true;
            PaszaPisklak.Location = new System.Drawing.Point(142, 152);
            PaszaPisklak.Name = "PaszaPisklak";
            PaszaPisklak.Size = new System.Drawing.Size(121, 23);
            PaszaPisklak.TabIndex = 8;
            // 
            // CzyjaWaga
            // 
            CzyjaWaga.FormattingEnabled = true;
            CzyjaWaga.Location = new System.Drawing.Point(143, 181);
            CzyjaWaga.Name = "CzyjaWaga";
            CzyjaWaga.Size = new System.Drawing.Size(121, 23);
            CzyjaWaga.TabIndex = 9;
            // 
            // KonfPadl
            // 
            KonfPadl.FormattingEnabled = true;
            KonfPadl.Location = new System.Drawing.Point(143, 212);
            KonfPadl.Name = "KonfPadl";
            KonfPadl.Size = new System.Drawing.Size(121, 23);
            KonfPadl.TabIndex = 10;
            // 
            // typCeny
            // 
            typCeny.FormattingEnabled = true;
            typCeny.Location = new System.Drawing.Point(143, 123);
            typCeny.Name = "typCeny";
            typCeny.Size = new System.Drawing.Size(121, 23);
            typCeny.TabIndex = 11;
            // 
            // Address1
            // 
            Address1.Location = new System.Drawing.Point(130, 158);
            Address1.Name = "Address1";
            Address1.Size = new System.Drawing.Size(161, 23);
            Address1.TabIndex = 12;
            // 
            // Address2
            // 
            Address2.Location = new System.Drawing.Point(130, 187);
            Address2.Name = "Address2";
            Address2.Size = new System.Drawing.Size(161, 23);
            Address2.TabIndex = 13;
            // 
            // NIP
            // 
            NIP.Location = new System.Drawing.Point(130, 216);
            NIP.Name = "NIP";
            NIP.Size = new System.Drawing.Size(161, 23);
            NIP.TabIndex = 14;
            // 
            // REGON
            // 
            REGON.Location = new System.Drawing.Point(130, 274);
            REGON.Name = "REGON";
            REGON.Size = new System.Drawing.Size(161, 23);
            REGON.TabIndex = 15;
            // 
            // PESEL
            // 
            PESEL.Location = new System.Drawing.Point(130, 245);
            PESEL.Name = "PESEL";
            PESEL.Size = new System.Drawing.Size(161, 23);
            PESEL.TabIndex = 16;
            // 
            // Phone1
            // 
            Phone1.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            Phone1.Location = new System.Drawing.Point(130, 303);
            Phone1.Name = "Phone1";
            Phone1.Size = new System.Drawing.Size(75, 22);
            Phone1.TabIndex = 17;
            // 
            // Phone2
            // 
            Phone2.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            Phone2.Location = new System.Drawing.Point(130, 332);
            Phone2.Name = "Phone2";
            Phone2.Size = new System.Drawing.Size(75, 22);
            Phone2.TabIndex = 18;
            // 
            // Info1
            // 
            Info1.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            Info1.Location = new System.Drawing.Point(211, 303);
            Info1.Name = "Info1";
            Info1.Size = new System.Drawing.Size(80, 22);
            Info1.TabIndex = 19;
            // 
            // Info2
            // 
            Info2.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            Info2.Location = new System.Drawing.Point(211, 332);
            Info2.Name = "Info2";
            Info2.Size = new System.Drawing.Size(80, 22);
            Info2.TabIndex = 20;
            // 
            // Email
            // 
            Email.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            Email.Location = new System.Drawing.Point(130, 361);
            Email.Name = "Email";
            Email.Size = new System.Drawing.Size(161, 22);
            Email.TabIndex = 21;
            // 
            // Cena
            // 
            Cena.Location = new System.Drawing.Point(72, 289);
            Cena.Name = "Cena";
            Cena.Size = new System.Drawing.Size(100, 23);
            Cena.TabIndex = 22;
            // 
            // NrGosp
            // 
            NrGosp.Location = new System.Drawing.Point(130, 390);
            NrGosp.Name = "NrGosp";
            NrGosp.Size = new System.Drawing.Size(161, 23);
            NrGosp.TabIndex = 23;
            // 
            // Ubytek
            // 
            Ubytek.Location = new System.Drawing.Point(142, 417);
            Ubytek.Name = "Ubytek";
            Ubytek.Size = new System.Drawing.Size(100, 23);
            Ubytek.TabIndex = 24;
            // 
            // srednia
            // 
            srednia.Location = new System.Drawing.Point(142, 335);
            srednia.Name = "srednia";
            srednia.Size = new System.Drawing.Size(100, 23);
            srednia.TabIndex = 25;
            // 
            // sztuki
            // 
            sztuki.Location = new System.Drawing.Point(142, 374);
            sztuki.Name = "sztuki";
            sztuki.Size = new System.Drawing.Size(100, 23);
            sztuki.TabIndex = 26;
            // 
            // IRZPlus
            // 
            IRZPlus.Location = new System.Drawing.Point(130, 419);
            IRZPlus.Name = "IRZPlus";
            IRZPlus.Size = new System.Drawing.Size(161, 23);
            IRZPlus.TabIndex = 28;
            // 
            // PostalCode
            // 
            PostalCode.Location = new System.Drawing.Point(130, 448);
            PostalCode.Name = "PostalCode";
            PostalCode.Size = new System.Drawing.Size(161, 23);
            PostalCode.TabIndex = 29;
            // 
            // Address
            // 
            Address.Location = new System.Drawing.Point(130, 477);
            Address.Name = "Address";
            Address.Size = new System.Drawing.Size(161, 23);
            Address.TabIndex = 30;
            // 
            // City
            // 
            City.Location = new System.Drawing.Point(130, 506);
            City.Name = "City";
            City.Size = new System.Drawing.Size(161, 23);
            City.TabIndex = 31;
            // 
            // IDLibra
            // 
            IDLibra.Location = new System.Drawing.Point(130, 535);
            IDLibra.Name = "IDLibra";
            IDLibra.Size = new System.Drawing.Size(161, 23);
            IDLibra.TabIndex = 32;
            // 
            // ComboBox1
            // 
            ComboBox1.BackColor = System.Drawing.Color.DarkGray;
            ComboBox1.FormattingEnabled = true;
            ComboBox1.Location = new System.Drawing.Point(143, 39);
            ComboBox1.Name = "ComboBox1";
            ComboBox1.Size = new System.Drawing.Size(121, 23);
            ComboBox1.TabIndex = 5;
            // 
            // label3
            // 
            label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label3.Location = new System.Drawing.Point(6, 103);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(118, 22);
            label3.TabIndex = 87;
            label3.Text = "Wybór";
            label3.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label9
            // 
            label9.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label9.Location = new System.Drawing.Point(16, 67);
            label9.Name = "label9";
            label9.Size = new System.Drawing.Size(121, 23);
            label9.TabIndex = 95;
            label9.Text = "Data Odbioru";
            label9.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label10
            // 
            label10.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label10.Location = new System.Drawing.Point(16, 94);
            label10.Name = "label10";
            label10.Size = new System.Drawing.Size(121, 23);
            label10.TabIndex = 96;
            label10.Text = "Data Podpisania";
            label10.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label11
            // 
            label11.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label11.Location = new System.Drawing.Point(6, 159);
            label11.Name = "label11";
            label11.Size = new System.Drawing.Size(118, 22);
            label11.TabIndex = 97;
            label11.Text = "Ulica";
            label11.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label12
            // 
            label12.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label12.Location = new System.Drawing.Point(6, 188);
            label12.Name = "label12";
            label12.Size = new System.Drawing.Size(118, 22);
            label12.TabIndex = 98;
            label12.Text = "Kod Pocztowy";
            label12.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label13
            // 
            label13.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label13.Location = new System.Drawing.Point(6, 217);
            label13.Name = "label13";
            label13.Size = new System.Drawing.Size(118, 22);
            label13.TabIndex = 99;
            label13.Text = "NIP";
            label13.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label14
            // 
            label14.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label14.Location = new System.Drawing.Point(6, 246);
            label14.Name = "label14";
            label14.Size = new System.Drawing.Size(118, 22);
            label14.TabIndex = 100;
            label14.Text = "Pesel";
            label14.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label15
            // 
            label15.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label15.Location = new System.Drawing.Point(6, 272);
            label15.Name = "label15";
            label15.Size = new System.Drawing.Size(118, 22);
            label15.TabIndex = 101;
            label15.Text = "REGON";
            label15.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label16
            // 
            label16.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label16.Location = new System.Drawing.Point(6, 304);
            label16.Name = "label16";
            label16.Size = new System.Drawing.Size(118, 22);
            label16.TabIndex = 102;
            label16.Text = "Numer kontaktowy 1";
            label16.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label18
            // 
            label18.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label18.Location = new System.Drawing.Point(6, 333);
            label18.Name = "label18";
            label18.Size = new System.Drawing.Size(118, 22);
            label18.TabIndex = 104;
            label18.Text = "Numer kontaktowy 2";
            label18.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label19
            // 
            label19.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label19.Location = new System.Drawing.Point(6, 362);
            label19.Name = "label19";
            label19.Size = new System.Drawing.Size(118, 22);
            label19.TabIndex = 105;
            label19.Text = "Email";
            label19.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label20
            // 
            label20.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label20.Location = new System.Drawing.Point(6, 390);
            label20.Name = "label20";
            label20.Size = new System.Drawing.Size(118, 22);
            label20.TabIndex = 106;
            label20.Text = "Nr Gospodarstwa";
            label20.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label21
            // 
            label21.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label21.Location = new System.Drawing.Point(6, 420);
            label21.Name = "label21";
            label21.Size = new System.Drawing.Size(118, 22);
            label21.TabIndex = 107;
            label21.Text = "IRZPlus";
            label21.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label17
            // 
            label17.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label17.Location = new System.Drawing.Point(6, 449);
            label17.Name = "label17";
            label17.Size = new System.Drawing.Size(118, 22);
            label17.TabIndex = 108;
            label17.Text = "Adres Kurnika";
            label17.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label22
            // 
            label22.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label22.Location = new System.Drawing.Point(6, 478);
            label22.Name = "label22";
            label22.Size = new System.Drawing.Size(118, 22);
            label22.TabIndex = 109;
            label22.Text = "Kod pocztowy kurnika";
            label22.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label23
            // 
            label23.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label23.Location = new System.Drawing.Point(6, 507);
            label23.Name = "label23";
            label23.Size = new System.Drawing.Size(118, 22);
            label23.TabIndex = 110;
            label23.Text = "Miejscowość kurnika";
            label23.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label24
            // 
            label24.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label24.Location = new System.Drawing.Point(6, 536);
            label24.Name = "label24";
            label24.Size = new System.Drawing.Size(118, 22);
            label24.TabIndex = 111;
            label24.Text = "Id Libra";
            label24.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label5
            // 
            label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label5.Location = new System.Drawing.Point(16, 123);
            label5.Name = "label5";
            label5.Size = new System.Drawing.Size(121, 23);
            label5.TabIndex = 112;
            label5.Text = "Typ Ceny";
            label5.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label25
            // 
            label25.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label25.Location = new System.Drawing.Point(16, 150);
            label25.Name = "label25";
            label25.Size = new System.Drawing.Size(121, 23);
            label25.TabIndex = 113;
            label25.Text = "Czy jest Pasza/Pisklak";
            label25.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label6
            // 
            label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label6.Location = new System.Drawing.Point(16, 179);
            label6.Name = "label6";
            label6.Size = new System.Drawing.Size(121, 23);
            label6.TabIndex = 114;
            label6.Text = "Waga Samochodowa";
            label6.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label8
            // 
            label8.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label8.Location = new System.Drawing.Point(16, 210);
            label8.Name = "label8";
            label8.Size = new System.Drawing.Size(121, 23);
            label8.TabIndex = 115;
            label8.Text = "Obiążenie padłymi";
            label8.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label7
            // 
            label7.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label7.Location = new System.Drawing.Point(7, 289);
            label7.Name = "label7";
            label7.Size = new System.Drawing.Size(59, 23);
            label7.TabIndex = 116;
            label7.Text = "Cena";
            label7.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label26
            // 
            label26.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label26.Location = new System.Drawing.Point(15, 333);
            label26.Name = "label26";
            label26.Size = new System.Drawing.Size(121, 23);
            label26.TabIndex = 117;
            label26.Text = "Waga";
            label26.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label27
            // 
            label27.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label27.Location = new System.Drawing.Point(16, 372);
            label27.Name = "label27";
            label27.Size = new System.Drawing.Size(121, 23);
            label27.TabIndex = 118;
            label27.Text = "Sztuki";
            label27.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label28
            // 
            label28.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label28.Location = new System.Drawing.Point(16, 40);
            label28.Name = "label28";
            label28.Size = new System.Drawing.Size(121, 23);
            label28.TabIndex = 119;
            label28.Text = "Identyfikator";
            label28.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label1
            // 
            label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label1.Location = new System.Drawing.Point(15, 415);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(121, 23);
            label1.TabIndex = 120;
            label1.Text = "Ubytek";
            label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // dtpData
            // 
            dtpData.Location = new System.Drawing.Point(142, 66);
            dtpData.Name = "dtpData";
            dtpData.Size = new System.Drawing.Size(200, 23);
            dtpData.TabIndex = 121;
            // 
            // dtpDataPodpisania
            // 
            dtpDataPodpisania.Location = new System.Drawing.Point(143, 95);
            dtpDataPodpisania.Name = "dtpDataPodpisania";
            dtpDataPodpisania.Size = new System.Drawing.Size(200, 23);
            dtpDataPodpisania.TabIndex = 122;
            // 
            // textBoxFiltrKontrahent
            // 
            textBoxFiltrKontrahent.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            textBoxFiltrKontrahent.Location = new System.Drawing.Point(476, 625);
            textBoxFiltrKontrahent.Multiline = true;
            textBoxFiltrKontrahent.Name = "textBoxFiltrKontrahent";
            textBoxFiltrKontrahent.Size = new System.Drawing.Size(282, 70);
            textBoxFiltrKontrahent.TabIndex = 123;
            // 
            // dataGridViewKontrahenci
            // 
            dataGridViewKontrahenci.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewKontrahenci.Location = new System.Drawing.Point(143, 807);
            dataGridViewKontrahenci.Name = "dataGridViewKontrahenci";
            dataGridViewKontrahenci.RowTemplate.Height = 25;
            dataGridViewKontrahenci.Size = new System.Drawing.Size(843, 98);
            dataGridViewKontrahenci.TabIndex = 124;
            // 
            // dataGridViewHodowcy
            // 
            dataGridViewHodowcy.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewHodowcy.Location = new System.Drawing.Point(142, 707);
            dataGridViewHodowcy.Name = "dataGridViewHodowcy";
            dataGridViewHodowcy.RowTemplate.Height = 25;
            dataGridViewHodowcy.Size = new System.Drawing.Size(843, 89);
            dataGridViewHodowcy.TabIndex = 125;
            // 
            // label2
            // 
            label2.BackColor = System.Drawing.SystemColors.MenuText;
            label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label2.ForeColor = System.Drawing.Color.Transparent;
            label2.Location = new System.Drawing.Point(309, 625);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(161, 70);
            label2.TabIndex = 126;
            label2.Text = "Szukaj dane hodowcy:";
            label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label4
            // 
            label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label4.Location = new System.Drawing.Point(121, 567);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(121, 23);
            label4.TabIndex = 127;
            label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label29
            // 
            label29.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label29.Location = new System.Drawing.Point(11, 707);
            label29.Name = "label29";
            label29.Size = new System.Drawing.Size(126, 89);
            label29.TabIndex = 128;
            label29.Text = "Tabela z 'Dane Hodowcy'";
            label29.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label30
            // 
            label30.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label30.Location = new System.Drawing.Point(12, 807);
            label30.Name = "label30";
            label30.Size = new System.Drawing.Size(125, 98);
            label30.TabIndex = 129;
            label30.Text = "Tabela z bazy danych Symfonia";
            label30.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label31
            // 
            label31.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label31.Location = new System.Drawing.Point(6, 131);
            label31.Name = "label31";
            label31.Size = new System.Drawing.Size(118, 22);
            label31.TabIndex = 131;
            label31.Text = "Hodowca";
            label31.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // Dostawca
            // 
            Dostawca.Location = new System.Drawing.Point(130, 130);
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
            comboBoxDostawca.Location = new System.Drawing.Point(130, 104);
            comboBoxDostawca.Name = "comboBoxDostawca";
            comboBoxDostawca.Size = new System.Drawing.Size(161, 20);
            comboBoxDostawca.TabIndex = 132;
            comboBoxDostawca.SelectedIndexChanged += comboBoxDostawca_SelectedIndexChanged;
            // 
            // Dostawca1
            // 
            Dostawca1.Location = new System.Drawing.Point(297, 564);
            Dostawca1.Name = "Dostawca1";
            Dostawca1.Size = new System.Drawing.Size(140, 23);
            Dostawca1.TabIndex = 133;
            Dostawca1.Visible = false;
            // 
            // comboBoxDostawcaS
            // 
            comboBoxDostawcaS.DropDownHeight = 300;
            comboBoxDostawcaS.DropDownWidth = 200;
            comboBoxDostawcaS.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            comboBoxDostawcaS.FormattingEnabled = true;
            comboBoxDostawcaS.IntegralHeight = false;
            comboBoxDostawcaS.Location = new System.Drawing.Point(297, 104);
            comboBoxDostawcaS.Name = "comboBoxDostawcaS";
            comboBoxDostawcaS.Size = new System.Drawing.Size(161, 20);
            comboBoxDostawcaS.TabIndex = 151;
            comboBoxDostawcaS.SelectionChangeCommitted += comboBoxDostawcaS_SelectionChangeCommitted;
            // 
            // DostawcaS
            // 
            DostawcaS.BackColor = System.Drawing.SystemColors.ControlDark;
            DostawcaS.Location = new System.Drawing.Point(297, 130);
            DostawcaS.Name = "DostawcaS";
            DostawcaS.ReadOnly = true;
            DostawcaS.Size = new System.Drawing.Size(161, 23);
            DostawcaS.TabIndex = 150;
            // 
            // Address1S
            // 
            Address1S.BackColor = System.Drawing.SystemColors.ControlDark;
            Address1S.Location = new System.Drawing.Point(297, 158);
            Address1S.Name = "Address1S";
            Address1S.ReadOnly = true;
            Address1S.Size = new System.Drawing.Size(137, 23);
            Address1S.TabIndex = 134;
            // 
            // Address2S
            // 
            Address2S.BackColor = System.Drawing.SystemColors.ControlDark;
            Address2S.Location = new System.Drawing.Point(297, 187);
            Address2S.Name = "Address2S";
            Address2S.ReadOnly = true;
            Address2S.Size = new System.Drawing.Size(161, 23);
            Address2S.TabIndex = 135;
            // 
            // NIPS
            // 
            NIPS.BackColor = System.Drawing.SystemColors.ControlDark;
            NIPS.Location = new System.Drawing.Point(297, 216);
            NIPS.Name = "NIPS";
            NIPS.ReadOnly = true;
            NIPS.Size = new System.Drawing.Size(161, 23);
            NIPS.TabIndex = 136;
            // 
            // REGONS
            // 
            REGONS.BackColor = System.Drawing.SystemColors.ControlDark;
            REGONS.Location = new System.Drawing.Point(297, 274);
            REGONS.Name = "REGONS";
            REGONS.ReadOnly = true;
            REGONS.Size = new System.Drawing.Size(161, 23);
            REGONS.TabIndex = 137;
            // 
            // PESELS
            // 
            PESELS.BackColor = System.Drawing.SystemColors.ControlDark;
            PESELS.Location = new System.Drawing.Point(297, 245);
            PESELS.Name = "PESELS";
            PESELS.ReadOnly = true;
            PESELS.Size = new System.Drawing.Size(161, 23);
            PESELS.TabIndex = 138;
            // 
            // Phone1S
            // 
            Phone1S.BackColor = System.Drawing.SystemColors.ControlDark;
            Phone1S.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            Phone1S.Location = new System.Drawing.Point(297, 303);
            Phone1S.Name = "Phone1S";
            Phone1S.ReadOnly = true;
            Phone1S.Size = new System.Drawing.Size(75, 22);
            Phone1S.TabIndex = 139;
            // 
            // Phone2S
            // 
            Phone2S.BackColor = System.Drawing.SystemColors.ControlDark;
            Phone2S.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            Phone2S.Location = new System.Drawing.Point(297, 332);
            Phone2S.Name = "Phone2S";
            Phone2S.ReadOnly = true;
            Phone2S.Size = new System.Drawing.Size(75, 22);
            Phone2S.TabIndex = 140;
            // 
            // textBox9
            // 
            textBox9.BackColor = System.Drawing.SystemColors.ControlDark;
            textBox9.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            textBox9.Location = new System.Drawing.Point(378, 303);
            textBox9.Name = "textBox9";
            textBox9.ReadOnly = true;
            textBox9.Size = new System.Drawing.Size(80, 22);
            textBox9.TabIndex = 141;
            // 
            // textBox10
            // 
            textBox10.BackColor = System.Drawing.SystemColors.ControlDark;
            textBox10.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            textBox10.Location = new System.Drawing.Point(378, 332);
            textBox10.Name = "textBox10";
            textBox10.ReadOnly = true;
            textBox10.Size = new System.Drawing.Size(80, 22);
            textBox10.TabIndex = 142;
            // 
            // EmailS
            // 
            EmailS.BackColor = System.Drawing.SystemColors.ControlDark;
            EmailS.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            EmailS.Location = new System.Drawing.Point(297, 361);
            EmailS.Name = "EmailS";
            EmailS.ReadOnly = true;
            EmailS.Size = new System.Drawing.Size(161, 22);
            EmailS.TabIndex = 143;
            // 
            // textBox12
            // 
            textBox12.BackColor = System.Drawing.SystemColors.ControlDark;
            textBox12.Location = new System.Drawing.Point(297, 390);
            textBox12.Name = "textBox12";
            textBox12.ReadOnly = true;
            textBox12.Size = new System.Drawing.Size(161, 23);
            textBox12.TabIndex = 144;
            // 
            // textBox13
            // 
            textBox13.BackColor = System.Drawing.SystemColors.ControlDark;
            textBox13.Location = new System.Drawing.Point(297, 419);
            textBox13.Name = "textBox13";
            textBox13.ReadOnly = true;
            textBox13.Size = new System.Drawing.Size(161, 23);
            textBox13.TabIndex = 145;
            // 
            // textBox14
            // 
            textBox14.BackColor = System.Drawing.SystemColors.ControlDark;
            textBox14.Location = new System.Drawing.Point(297, 448);
            textBox14.Name = "textBox14";
            textBox14.ReadOnly = true;
            textBox14.Size = new System.Drawing.Size(161, 23);
            textBox14.TabIndex = 146;
            // 
            // textBox15
            // 
            textBox15.BackColor = System.Drawing.SystemColors.ControlDark;
            textBox15.Location = new System.Drawing.Point(297, 477);
            textBox15.Name = "textBox15";
            textBox15.ReadOnly = true;
            textBox15.Size = new System.Drawing.Size(161, 23);
            textBox15.TabIndex = 147;
            // 
            // textBox16
            // 
            textBox16.BackColor = System.Drawing.SystemColors.ControlDark;
            textBox16.Location = new System.Drawing.Point(297, 506);
            textBox16.Name = "textBox16";
            textBox16.ReadOnly = true;
            textBox16.Size = new System.Drawing.Size(161, 23);
            textBox16.TabIndex = 148;
            // 
            // IDLibraS
            // 
            IDLibraS.BackColor = System.Drawing.SystemColors.ControlDark;
            IDLibraS.Location = new System.Drawing.Point(297, 535);
            IDLibraS.Name = "IDLibraS";
            IDLibraS.ReadOnly = true;
            IDLibraS.Size = new System.Drawing.Size(161, 23);
            IDLibraS.TabIndex = 149;
            // 
            // label34
            // 
            label34.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label34.Location = new System.Drawing.Point(464, 131);
            label34.Name = "label34";
            label34.Size = new System.Drawing.Size(118, 22);
            label34.TabIndex = 169;
            label34.Text = "Hodowca";
            label34.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label35
            // 
            label35.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label35.Location = new System.Drawing.Point(464, 536);
            label35.Name = "label35";
            label35.Size = new System.Drawing.Size(118, 22);
            label35.TabIndex = 168;
            label35.Text = "Id Symfonia";
            label35.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label36
            // 
            label36.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label36.Location = new System.Drawing.Point(464, 507);
            label36.Name = "label36";
            label36.Size = new System.Drawing.Size(118, 22);
            label36.TabIndex = 167;
            label36.Text = "Miejscowość kurnika";
            label36.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label37
            // 
            label37.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label37.Location = new System.Drawing.Point(464, 478);
            label37.Name = "label37";
            label37.Size = new System.Drawing.Size(118, 22);
            label37.TabIndex = 166;
            label37.Text = "Kod pocztowy kurnika";
            label37.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label38
            // 
            label38.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label38.Location = new System.Drawing.Point(464, 449);
            label38.Name = "label38";
            label38.Size = new System.Drawing.Size(118, 22);
            label38.TabIndex = 165;
            label38.Text = "Adres Kurnika";
            label38.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label39
            // 
            label39.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label39.Location = new System.Drawing.Point(464, 420);
            label39.Name = "label39";
            label39.Size = new System.Drawing.Size(118, 22);
            label39.TabIndex = 164;
            label39.Text = "IRZPlus";
            label39.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label40
            // 
            label40.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label40.Location = new System.Drawing.Point(464, 390);
            label40.Name = "label40";
            label40.Size = new System.Drawing.Size(118, 22);
            label40.TabIndex = 163;
            label40.Text = "Nr Gospodarstwa";
            label40.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label41
            // 
            label41.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label41.Location = new System.Drawing.Point(464, 362);
            label41.Name = "label41";
            label41.Size = new System.Drawing.Size(118, 22);
            label41.TabIndex = 162;
            label41.Text = "Email";
            label41.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label42
            // 
            label42.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label42.Location = new System.Drawing.Point(464, 333);
            label42.Name = "label42";
            label42.Size = new System.Drawing.Size(118, 22);
            label42.TabIndex = 161;
            label42.Text = "Numer kontaktowy 2";
            label42.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label43
            // 
            label43.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label43.Location = new System.Drawing.Point(464, 304);
            label43.Name = "label43";
            label43.Size = new System.Drawing.Size(118, 22);
            label43.TabIndex = 160;
            label43.Text = "Numer kontaktowy 1";
            label43.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label44
            // 
            label44.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label44.Location = new System.Drawing.Point(464, 272);
            label44.Name = "label44";
            label44.Size = new System.Drawing.Size(118, 22);
            label44.TabIndex = 159;
            label44.Text = "REGON";
            label44.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label45
            // 
            label45.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label45.Location = new System.Drawing.Point(464, 246);
            label45.Name = "label45";
            label45.Size = new System.Drawing.Size(118, 22);
            label45.TabIndex = 158;
            label45.Text = "Pesel";
            label45.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label46
            // 
            label46.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label46.Location = new System.Drawing.Point(464, 217);
            label46.Name = "label46";
            label46.Size = new System.Drawing.Size(118, 22);
            label46.TabIndex = 157;
            label46.Text = "NIP";
            label46.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label47
            // 
            label47.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label47.Location = new System.Drawing.Point(464, 188);
            label47.Name = "label47";
            label47.Size = new System.Drawing.Size(118, 22);
            label47.TabIndex = 156;
            label47.Text = "Kod Pocztowy";
            label47.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label48
            // 
            label48.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label48.Location = new System.Drawing.Point(483, 160);
            label48.Name = "label48";
            label48.Size = new System.Drawing.Size(118, 22);
            label48.TabIndex = 155;
            label48.Text = "Ulica";
            label48.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label49
            // 
            label49.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label49.Location = new System.Drawing.Point(464, 103);
            label49.Name = "label49";
            label49.Size = new System.Drawing.Size(118, 22);
            label49.TabIndex = 154;
            label49.Text = "Wybór";
            label49.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // numer
            // 
            numer.BackColor = System.Drawing.SystemColors.ControlDark;
            numer.Location = new System.Drawing.Point(434, 159);
            numer.Name = "numer";
            numer.ReadOnly = true;
            numer.Size = new System.Drawing.Size(43, 23);
            numer.TabIndex = 170;
            // 
            // buttonZapisz
            // 
            buttonZapisz.Font = new System.Drawing.Font("Arial Narrow", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            buttonZapisz.Location = new System.Drawing.Point(464, 26);
            buttonZapisz.Name = "buttonZapisz";
            buttonZapisz.Size = new System.Drawing.Size(137, 72);
            buttonZapisz.TabIndex = 171;
            buttonZapisz.Text = "Powiąż Libre ze Symfonią";
            buttonZapisz.UseVisualStyleBackColor = true;
            buttonZapisz.Click += buttonZapisz_Click;
            // 
            // buttonZapiszDaneLibra
            // 
            buttonZapiszDaneLibra.Font = new System.Drawing.Font("Arial Narrow", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            buttonZapiszDaneLibra.Location = new System.Drawing.Point(15, 564);
            buttonZapiszDaneLibra.Name = "buttonZapiszDaneLibra";
            buttonZapiszDaneLibra.Size = new System.Drawing.Size(276, 34);
            buttonZapiszDaneLibra.TabIndex = 172;
            buttonZapiszDaneLibra.Text = "Zaaktualizuj dane Libry";
            buttonZapiszDaneLibra.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            buttonZapiszDaneLibra.UseVisualStyleBackColor = true;
            buttonZapiszDaneLibra.Click += buttonZapiszDaneLibra_Click;
            // 
            // button1
            // 
            button1.BackgroundImage = (System.Drawing.Image)resources.GetObject("button1.BackgroundImage");
            button1.BackgroundImageLayout = ImageLayout.Stretch;
            button1.Font = new System.Drawing.Font("Segoe UI Semibold", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            button1.Location = new System.Drawing.Point(297, 22);
            button1.Name = "button1";
            button1.Size = new System.Drawing.Size(161, 76);
            button1.TabIndex = 173;
            button1.UseVisualStyleBackColor = true;
            // 
            // button2
            // 
            button2.BackgroundImage = (System.Drawing.Image)resources.GetObject("button2.BackgroundImage");
            button2.BackgroundImageLayout = ImageLayout.Stretch;
            button2.Font = new System.Drawing.Font("Segoe UI Semibold", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            button2.Location = new System.Drawing.Point(130, 22);
            button2.Name = "button2";
            button2.Size = new System.Drawing.Size(161, 76);
            button2.TabIndex = 174;
            button2.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(button2);
            groupBox1.Controls.Add(IDLibra);
            groupBox1.Controls.Add(City);
            groupBox1.Controls.Add(button1);
            groupBox1.Controls.Add(Address);
            groupBox1.Controls.Add(buttonZapiszDaneLibra);
            groupBox1.Controls.Add(PostalCode);
            groupBox1.Controls.Add(buttonZapisz);
            groupBox1.Controls.Add(IRZPlus);
            groupBox1.Controls.Add(numer);
            groupBox1.Controls.Add(NrGosp);
            groupBox1.Controls.Add(label34);
            groupBox1.Controls.Add(Email);
            groupBox1.Controls.Add(label35);
            groupBox1.Controls.Add(Info2);
            groupBox1.Controls.Add(label36);
            groupBox1.Controls.Add(Info1);
            groupBox1.Controls.Add(label37);
            groupBox1.Controls.Add(Phone2);
            groupBox1.Controls.Add(label38);
            groupBox1.Controls.Add(Phone1);
            groupBox1.Controls.Add(label39);
            groupBox1.Controls.Add(PESEL);
            groupBox1.Controls.Add(label40);
            groupBox1.Controls.Add(REGON);
            groupBox1.Controls.Add(label41);
            groupBox1.Controls.Add(NIP);
            groupBox1.Controls.Add(label42);
            groupBox1.Controls.Add(Address2);
            groupBox1.Controls.Add(label43);
            groupBox1.Controls.Add(Address1);
            groupBox1.Controls.Add(label44);
            groupBox1.Controls.Add(label3);
            groupBox1.Controls.Add(label45);
            groupBox1.Controls.Add(label11);
            groupBox1.Controls.Add(label46);
            groupBox1.Controls.Add(label12);
            groupBox1.Controls.Add(label47);
            groupBox1.Controls.Add(label13);
            groupBox1.Controls.Add(label48);
            groupBox1.Controls.Add(label14);
            groupBox1.Controls.Add(label49);
            groupBox1.Controls.Add(label15);
            groupBox1.Controls.Add(comboBoxDostawcaS);
            groupBox1.Controls.Add(label16);
            groupBox1.Controls.Add(DostawcaS);
            groupBox1.Controls.Add(label18);
            groupBox1.Controls.Add(Address1S);
            groupBox1.Controls.Add(label19);
            groupBox1.Controls.Add(Address2S);
            groupBox1.Controls.Add(label20);
            groupBox1.Controls.Add(NIPS);
            groupBox1.Controls.Add(label21);
            groupBox1.Controls.Add(REGONS);
            groupBox1.Controls.Add(label17);
            groupBox1.Controls.Add(PESELS);
            groupBox1.Controls.Add(label22);
            groupBox1.Controls.Add(Phone1S);
            groupBox1.Controls.Add(label23);
            groupBox1.Controls.Add(Phone2S);
            groupBox1.Controls.Add(label24);
            groupBox1.Controls.Add(textBox9);
            groupBox1.Controls.Add(Dostawca);
            groupBox1.Controls.Add(textBox10);
            groupBox1.Controls.Add(label31);
            groupBox1.Controls.Add(EmailS);
            groupBox1.Controls.Add(comboBoxDostawca);
            groupBox1.Controls.Add(textBox12);
            groupBox1.Controls.Add(Dostawca1);
            groupBox1.Controls.Add(textBox13);
            groupBox1.Controls.Add(IDLibraS);
            groupBox1.Controls.Add(textBox14);
            groupBox1.Controls.Add(textBox16);
            groupBox1.Controls.Add(textBox15);
            groupBox1.Location = new System.Drawing.Point(12, 12);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new System.Drawing.Size(612, 602);
            groupBox1.TabIndex = 175;
            groupBox1.TabStop = false;
            groupBox1.Text = "Dane Hodowcy";
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(label32);
            groupBox2.Controls.Add(dodatek);
            groupBox2.Controls.Add(pictureBox4);
            groupBox2.Controls.Add(pictureBox7);
            groupBox2.Controls.Add(pictureBox10);
            groupBox2.Controls.Add(pictureBox11);
            groupBox2.Controls.Add(label28);
            groupBox2.Controls.Add(sztuki);
            groupBox2.Controls.Add(srednia);
            groupBox2.Controls.Add(Ubytek);
            groupBox2.Controls.Add(label4);
            groupBox2.Controls.Add(Cena);
            groupBox2.Controls.Add(typCeny);
            groupBox2.Controls.Add(KonfPadl);
            groupBox2.Controls.Add(CzyjaWaga);
            groupBox2.Controls.Add(PaszaPisklak);
            groupBox2.Controls.Add(dtpDataPodpisania);
            groupBox2.Controls.Add(Vatowiec);
            groupBox2.Controls.Add(dtpData);
            groupBox2.Controls.Add(CommandButton_Update);
            groupBox2.Controls.Add(label1);
            groupBox2.Controls.Add(ComboBox1);
            groupBox2.Controls.Add(label9);
            groupBox2.Controls.Add(label27);
            groupBox2.Controls.Add(label10);
            groupBox2.Controls.Add(label26);
            groupBox2.Controls.Add(label5);
            groupBox2.Controls.Add(label7);
            groupBox2.Controls.Add(label25);
            groupBox2.Controls.Add(label8);
            groupBox2.Controls.Add(label6);
            groupBox2.Location = new System.Drawing.Point(640, 12);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new System.Drawing.Size(348, 607);
            groupBox2.TabIndex = 176;
            groupBox2.TabStop = false;
            groupBox2.Text = "Dane Dostawy";
            // 
            // pictureBox4
            // 
            pictureBox4.Image = (System.Drawing.Image)resources.GetObject("pictureBox4.Image");
            pictureBox4.Location = new System.Drawing.Point(267, 279);
            pictureBox4.Name = "pictureBox4";
            pictureBox4.Size = new System.Drawing.Size(76, 33);
            pictureBox4.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox4.TabIndex = 131;
            pictureBox4.TabStop = false;
            // 
            // pictureBox7
            // 
            pictureBox7.Image = (System.Drawing.Image)resources.GetObject("pictureBox7.Image");
            pictureBox7.Location = new System.Drawing.Point(248, 329);
            pictureBox7.Name = "pictureBox7";
            pictureBox7.Size = new System.Drawing.Size(58, 29);
            pictureBox7.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox7.TabIndex = 130;
            pictureBox7.TabStop = false;
            // 
            // pictureBox10
            // 
            pictureBox10.Image = (System.Drawing.Image)resources.GetObject("pictureBox10.Image");
            pictureBox10.Location = new System.Drawing.Point(250, 417);
            pictureBox10.Name = "pictureBox10";
            pictureBox10.Size = new System.Drawing.Size(58, 27);
            pictureBox10.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox10.TabIndex = 129;
            pictureBox10.TabStop = false;
            // 
            // pictureBox11
            // 
            pictureBox11.Image = (System.Drawing.Image)resources.GetObject("pictureBox11.Image");
            pictureBox11.Location = new System.Drawing.Point(248, 374);
            pictureBox11.Name = "pictureBox11";
            pictureBox11.Size = new System.Drawing.Size(62, 23);
            pictureBox11.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox11.TabIndex = 128;
            pictureBox11.TabStop = false;
            // 
            // dodatek
            // 
            dodatek.Location = new System.Drawing.Point(178, 289);
            dodatek.Name = "dodatek";
            dodatek.Size = new System.Drawing.Size(64, 23);
            dodatek.TabIndex = 132;
            // 
            // label32
            // 
            label32.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label32.Location = new System.Drawing.Point(178, 263);
            label32.Name = "label32";
            label32.Size = new System.Drawing.Size(59, 23);
            label32.TabIndex = 133;
            label32.Text = "Dodatek";
            label32.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // UmowyForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1000, 908);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Controls.Add(label30);
            Controls.Add(label29);
            Controls.Add(dataGridViewHodowcy);
            Controls.Add(dataGridViewKontrahenci);
            Controls.Add(label2);
            Controls.Add(textBoxFiltrKontrahent);
            Name = "UmowyForm";
            Text = "UmowyForm";
            ((System.ComponentModel.ISupportInitialize)dataGridViewKontrahenci).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewHodowcy).EndInit();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox4).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox7).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox10).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox11).EndInit();
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
        private ComboBox comboBoxDostawcaS;
        private TextBox DostawcaS;
        private TextBox Address1S;
        private TextBox Address2S;
        private TextBox NIPS;
        private TextBox REGONS;
        private TextBox PESELS;
        private TextBox Phone1S;
        private TextBox Phone2S;
        private TextBox textBox9;
        private TextBox textBox10;
        private TextBox EmailS;
        private TextBox textBox12;
        private TextBox textBox13;
        private TextBox textBox14;
        private TextBox textBox15;
        private TextBox textBox16;
        private TextBox IDLibraS;
        private Label label34;
        private Label label35;
        private Label label36;
        private Label label37;
        private Label label38;
        private Label label39;
        private Label label40;
        private Label label41;
        private Label label42;
        private Label label43;
        private Label label44;
        private Label label45;
        private Label label46;
        private Label label47;
        private Label label48;
        private Label label49;
        private TextBox numer;
        private Button buttonZapisz;
        private Button buttonZapiszDaneLibra;
        private Button button1;
        private Button button2;
        private GroupBox groupBox1;
        private GroupBox groupBox2;
        private PictureBox pictureBox11;
        private PictureBox pictureBox10;
        private PictureBox pictureBox7;
        private PictureBox pictureBox4;
        private Label label32;
        private TextBox dodatek;
    }
}
