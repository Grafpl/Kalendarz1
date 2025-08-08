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
            data = new TextBox();
            frameDatePicker = new Panel();
            monthCalendar1 = new MonthCalendar();
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
            Dostawca = new TextBox();
            IRZPlus = new TextBox();
            PostalCode = new TextBox();
            Address = new TextBox();
            City = new TextBox();
            IDLibra = new TextBox();
            checkBox1 = new CheckBox();
            button1 = new Button();
            ComboBox1 = new ComboBox();
            dateButton0 = new Button();
            dateButton1 = new Button();
            button4 = new Button();
            button5 = new Button();
            dataPodpisania = new TextBox();
            label3 = new Label();
            label1 = new Label();
            label2 = new Label();
            label4 = new Label();
            label5 = new Label();
            label6 = new Label();
            label7 = new Label();
            label8 = new Label();
            SuspendLayout();
            // 
            // data
            // 
            data.Location = new System.Drawing.Point(140, 7);
            data.Name = "data";
            data.Size = new System.Drawing.Size(120, 23);
            data.TabIndex = 0;
            // 
            // frameDatePicker
            // 
            frameDatePicker.BorderStyle = BorderStyle.FixedSingle;
            frameDatePicker.Location = new System.Drawing.Point(570, 471);
            frameDatePicker.Name = "frameDatePicker";
            frameDatePicker.Size = new System.Drawing.Size(230, 185);
            frameDatePicker.TabIndex = 4;
            frameDatePicker.Visible = false;
            // 
            // monthCalendar1
            // 
            monthCalendar1.Location = new System.Drawing.Point(850, 500);
            monthCalendar1.MaxSelectionCount = 1;
            monthCalendar1.Name = "monthCalendar1";
            monthCalendar1.TabIndex = 0;
            // 
            // CommandButton_Update
            // 
            CommandButton_Update.Location = new System.Drawing.Point(370, 50);
            CommandButton_Update.Name = "CommandButton_Update";
            CommandButton_Update.Size = new System.Drawing.Size(180, 30);
            CommandButton_Update.TabIndex = 6;
            CommandButton_Update.Text = "Utwórz umowę (Word/PDF)";
            CommandButton_Update.UseVisualStyleBackColor = true;
            CommandButton_Update.Click += CommandButton_Update_Click_1;
            // 
            // Vatowiec
            // 
            Vatowiec.AutoSize = true;
            Vatowiec.Location = new System.Drawing.Point(12, 616);
            Vatowiec.Name = "Vatowiec";
            Vatowiec.Size = new System.Drawing.Size(76, 19);
            Vatowiec.TabIndex = 7;
            Vatowiec.Text = "VATowiec";
            Vatowiec.UseVisualStyleBackColor = true;
            // 
            // PaszaPisklak
            // 
            PaszaPisklak.FormattingEnabled = true;
            PaszaPisklak.Location = new System.Drawing.Point(285, 193);
            PaszaPisklak.Name = "PaszaPisklak";
            PaszaPisklak.Size = new System.Drawing.Size(121, 23);
            PaszaPisklak.TabIndex = 8;
            // 
            // CzyjaWaga
            // 
            CzyjaWaga.FormattingEnabled = true;
            CzyjaWaga.Location = new System.Drawing.Point(412, 193);
            CzyjaWaga.Name = "CzyjaWaga";
            CzyjaWaga.Size = new System.Drawing.Size(121, 23);
            CzyjaWaga.TabIndex = 9;
            // 
            // KonfPadl
            // 
            KonfPadl.FormattingEnabled = true;
            KonfPadl.Location = new System.Drawing.Point(539, 193);
            KonfPadl.Name = "KonfPadl";
            KonfPadl.Size = new System.Drawing.Size(121, 23);
            KonfPadl.TabIndex = 10;
            // 
            // typCeny
            // 
            typCeny.FormattingEnabled = true;
            typCeny.Location = new System.Drawing.Point(158, 193);
            typCeny.Name = "typCeny";
            typCeny.Size = new System.Drawing.Size(121, 23);
            typCeny.TabIndex = 11;
            // 
            // Address1
            // 
            Address1.Location = new System.Drawing.Point(12, 152);
            Address1.Name = "Address1";
            Address1.Size = new System.Drawing.Size(100, 23);
            Address1.TabIndex = 12;
            // 
            // Address2
            // 
            Address2.Location = new System.Drawing.Point(12, 181);
            Address2.Name = "Address2";
            Address2.Size = new System.Drawing.Size(100, 23);
            Address2.TabIndex = 13;
            // 
            // NIP
            // 
            NIP.Location = new System.Drawing.Point(12, 210);
            NIP.Name = "NIP";
            NIP.Size = new System.Drawing.Size(100, 23);
            NIP.TabIndex = 14;
            // 
            // REGON
            // 
            REGON.Location = new System.Drawing.Point(12, 239);
            REGON.Name = "REGON";
            REGON.Size = new System.Drawing.Size(100, 23);
            REGON.TabIndex = 15;
            // 
            // PESEL
            // 
            PESEL.Location = new System.Drawing.Point(12, 268);
            PESEL.Name = "PESEL";
            PESEL.Size = new System.Drawing.Size(100, 23);
            PESEL.TabIndex = 16;
            // 
            // Phone1
            // 
            Phone1.Location = new System.Drawing.Point(12, 297);
            Phone1.Name = "Phone1";
            Phone1.Size = new System.Drawing.Size(100, 23);
            Phone1.TabIndex = 17;
            // 
            // Phone2
            // 
            Phone2.Location = new System.Drawing.Point(12, 326);
            Phone2.Name = "Phone2";
            Phone2.Size = new System.Drawing.Size(100, 23);
            Phone2.TabIndex = 18;
            // 
            // Info1
            // 
            Info1.Location = new System.Drawing.Point(12, 355);
            Info1.Name = "Info1";
            Info1.Size = new System.Drawing.Size(100, 23);
            Info1.TabIndex = 19;
            // 
            // Info2
            // 
            Info2.Location = new System.Drawing.Point(12, 384);
            Info2.Name = "Info2";
            Info2.Size = new System.Drawing.Size(100, 23);
            Info2.TabIndex = 20;
            // 
            // Email
            // 
            Email.Location = new System.Drawing.Point(12, 413);
            Email.Name = "Email";
            Email.Size = new System.Drawing.Size(100, 23);
            Email.TabIndex = 21;
            // 
            // Cena
            // 
            Cena.Location = new System.Drawing.Point(666, 193);
            Cena.Name = "Cena";
            Cena.Size = new System.Drawing.Size(100, 23);
            Cena.TabIndex = 22;
            // 
            // NrGosp
            // 
            NrGosp.Location = new System.Drawing.Point(12, 442);
            NrGosp.Name = "NrGosp";
            NrGosp.Size = new System.Drawing.Size(100, 23);
            NrGosp.TabIndex = 23;
            // 
            // Ubytek
            // 
            Ubytek.Location = new System.Drawing.Point(12, 123);
            Ubytek.Name = "Ubytek";
            Ubytek.Size = new System.Drawing.Size(100, 23);
            Ubytek.TabIndex = 24;
            // 
            // srednia
            // 
            srednia.Location = new System.Drawing.Point(770, 193);
            srednia.Name = "srednia";
            srednia.Size = new System.Drawing.Size(100, 23);
            srednia.TabIndex = 25;
            // 
            // sztuki
            // 
            sztuki.Location = new System.Drawing.Point(876, 193);
            sztuki.Name = "sztuki";
            sztuki.Size = new System.Drawing.Size(100, 23);
            sztuki.TabIndex = 26;
            // 
            // Dostawca
            // 
            Dostawca.Location = new System.Drawing.Point(310, 500);
            Dostawca.Name = "Dostawca";
            Dostawca.Size = new System.Drawing.Size(120, 23);
            Dostawca.TabIndex = 27;
            // 
            // IRZPlus
            // 
            IRZPlus.Location = new System.Drawing.Point(12, 471);
            IRZPlus.Name = "IRZPlus";
            IRZPlus.Size = new System.Drawing.Size(100, 23);
            IRZPlus.TabIndex = 28;
            // 
            // PostalCode
            // 
            PostalCode.Location = new System.Drawing.Point(12, 500);
            PostalCode.Name = "PostalCode";
            PostalCode.Size = new System.Drawing.Size(100, 23);
            PostalCode.TabIndex = 29;
            // 
            // Address
            // 
            Address.Location = new System.Drawing.Point(12, 529);
            Address.Name = "Address";
            Address.Size = new System.Drawing.Size(200, 23);
            Address.TabIndex = 30;
            // 
            // City
            // 
            City.Location = new System.Drawing.Point(12, 558);
            City.Name = "City";
            City.Size = new System.Drawing.Size(100, 23);
            City.TabIndex = 31;
            // 
            // IDLibra
            // 
            IDLibra.Location = new System.Drawing.Point(12, 587);
            IDLibra.Name = "IDLibra";
            IDLibra.Size = new System.Drawing.Size(100, 23);
            IDLibra.TabIndex = 32;
            // 
            // checkBox1
            // 
            checkBox1.AutoSize = true;
            checkBox1.Location = new System.Drawing.Point(982, 195);
            checkBox1.Name = "checkBox1";
            checkBox1.Size = new System.Drawing.Size(82, 19);
            checkBox1.TabIndex = 33;
            checkBox1.Text = "checkBox1";
            // 
            // button1
            // 
            button1.Location = new System.Drawing.Point(416, 284);
            button1.Name = "button1";
            button1.Size = new System.Drawing.Size(75, 23);
            button1.TabIndex = 34;
            button1.Text = "button1";
            // 
            // ComboBox1
            // 
            ComboBox1.FormattingEnabled = true;
            ComboBox1.Location = new System.Drawing.Point(370, 12);
            ComboBox1.Name = "ComboBox1";
            ComboBox1.Size = new System.Drawing.Size(121, 23);
            ComboBox1.TabIndex = 5;
            // 
            // dateButton0
            // 
            dateButton0.Location = new System.Drawing.Point(270, 7);
            dateButton0.Name = "dateButton0";
            dateButton0.Size = new System.Drawing.Size(85, 23);
            dateButton0.TabIndex = 2;
            dateButton0.Text = "📅 Data";
            dateButton0.UseVisualStyleBackColor = true;
            // 
            // dateButton1
            // 
            dateButton1.Location = new System.Drawing.Point(270, 36);
            dateButton1.Name = "dateButton1";
            dateButton1.Size = new System.Drawing.Size(85, 23);
            dateButton1.TabIndex = 3;
            dateButton1.Text = "📅 Podpis";
            dateButton1.UseVisualStyleBackColor = true;
            // 
            // button4
            // 
            button4.Location = new System.Drawing.Point(703, 284);
            button4.Name = "button4";
            button4.Size = new System.Drawing.Size(75, 23);
            button4.TabIndex = 35;
            button4.Text = "button4";
            // 
            // button5
            // 
            button5.Location = new System.Drawing.Point(795, 284);
            button5.Name = "button5";
            button5.Size = new System.Drawing.Size(75, 23);
            button5.TabIndex = 36;
            button5.Text = "button5";
            // 
            // dataPodpisania
            // 
            dataPodpisania.Location = new System.Drawing.Point(140, 36);
            dataPodpisania.Name = "dataPodpisania";
            dataPodpisania.Size = new System.Drawing.Size(120, 23);
            dataPodpisania.TabIndex = 1;
            // 
            // label3
            // 
            label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label3.Location = new System.Drawing.Point(310, 459);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(120, 38);
            label3.TabIndex = 87;
            label3.Text = "Hodowca";
            label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label1
            // 
            label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label1.Location = new System.Drawing.Point(876, 152);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(100, 38);
            label1.TabIndex = 88;
            label1.Text = "Sztuki";
            label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label2
            // 
            label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label2.Location = new System.Drawing.Point(770, 152);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(100, 38);
            label2.TabIndex = 89;
            label2.Text = "Waga";
            label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label4
            // 
            label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label4.Location = new System.Drawing.Point(666, 152);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(100, 38);
            label4.TabIndex = 90;
            label4.Text = "Cena";
            label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label5
            // 
            label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label5.Location = new System.Drawing.Point(158, 152);
            label5.Name = "label5";
            label5.Size = new System.Drawing.Size(121, 38);
            label5.TabIndex = 91;
            label5.Text = "Typ Ceny";
            label5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label6
            // 
            label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label6.Location = new System.Drawing.Point(412, 152);
            label6.Name = "label6";
            label6.Size = new System.Drawing.Size(121, 38);
            label6.TabIndex = 92;
            label6.Text = "Hodowca?";
            label6.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label7
            // 
            label7.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label7.Location = new System.Drawing.Point(539, 152);
            label7.Name = "label7";
            label7.Size = new System.Drawing.Size(121, 38);
            label7.TabIndex = 93;
            label7.Text = "Czyja Waga?";
            label7.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label8
            // 
            label8.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label8.Location = new System.Drawing.Point(285, 152);
            label8.Name = "label8";
            label8.Size = new System.Drawing.Size(121, 38);
            label8.TabIndex = 94;
            label8.Text = "Odliczane padłe?";
            label8.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // UmowyForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1152, 700);
            Controls.Add(label8);
            Controls.Add(label7);
            Controls.Add(label6);
            Controls.Add(label5);
            Controls.Add(label4);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(label3);
            Controls.Add(monthCalendar1);
            Controls.Add(data);
            Controls.Add(dataPodpisania);
            Controls.Add(dateButton0);
            Controls.Add(dateButton1);
            Controls.Add(frameDatePicker);
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
            Controls.Add(Dostawca);
            Controls.Add(IRZPlus);
            Controls.Add(PostalCode);
            Controls.Add(Address);
            Controls.Add(City);
            Controls.Add(IDLibra);
            Controls.Add(checkBox1);
            Controls.Add(button1);
            Controls.Add(button4);
            Controls.Add(button5);
            Name = "UmowyForm";
            Text = "UmowyForm";
            ResumeLayout(false);
            PerformLayout();

            // Podłącz zdarzenie Load jeśli chcesz przez Designer (opcjonalnie – i tak podpinałem w kodzie)
            // this.Load += new System.EventHandler(this.UmowyForm_Load);
        }

        #endregion

        // ====== DEKLARACJE PÓL (muszą zgadzać się z nazwami użytymi w logice) ======
        private TextBox data;
        private Panel frameDatePicker;
        private MonthCalendar monthCalendar1;
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
        private TextBox Dostawca;
        private TextBox IRZPlus;
        private TextBox PostalCode;
        private TextBox Address;
        private TextBox City;
        private TextBox IDLibra;
        private System.Windows.Forms.CheckBox checkBox1;
        private Button button1;
        private ComboBox ComboBox1;
        private Button dateButton0;
        private Button dateButton1;
        private Button button4;
        private Button button5;
        private TextBox dataPodpisania;
        private Label label3;
        private Label label1;
        private Label label2;
        private Label label4;
        private Label label5;
        private Label label6;
        private Label label7;
        private Label label8;
    }
}
