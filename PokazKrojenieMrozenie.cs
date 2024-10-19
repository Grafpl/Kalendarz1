using System;
using System.Windows.Forms;
using System.Globalization;

namespace Kalendarz1
{
    public partial class PokazKrojenieMrozenie : Form
    {
        private MojeObliczenia obliczenia = new MojeObliczenia();
        private TextBox sztuki;
        private TextBox sztukNaSzuflade;
        private Label label10;
        private Label label1;
        private Label label2;
        private TextBox textBox1;
        private Label label3;
        private Label label4;
        private TextBox obliczeniaAut;
        private Label label12;
        private Label label5;
        private Label label6;
        private TextBox sumaSztuk;
        private Button buttonZamknij;
        private Button button1;
        private TextBox TuszkaKG;
        private Label label7;
        private TextBox TuszkaCena;
        private Label label8;
        private TextBox TuszkaWartosc;
        private Label label9;
        private TextBox FiletWartosc;
        private TextBox FiletCena;
        private TextBox FiletKG;
        private TextBox CwiartkaWartosc;
        private TextBox CwiartkaCena;
        private TextBox CwiartkaKG;
        private TextBox SkrzydloWartosc;
        private TextBox SkrzydloCena;
        private TextBox SkrzydloKG;
        private TextBox KorpusWartosc;
        private TextBox KorpusCena;
        private TextBox KorpusKG;
        private Label label13;
        private Label label14;
        private Label label15;
        private Label label16;
        private Label label17;
        private TextBox PozostaleWartosc;
        private TextBox PozostaleCena;
        private TextBox PozostaleKG;
        private TextBox TuszkaWydajnosc;
        private Label label18;
        private TextBox FiletWydajnosc;
        private TextBox CwiartkaWydajnosc;
        private TextBox SkrzydloWydajnosc;
        private TextBox KorpusWydajnosc;
        private TextBox PozostaleWydajnosc;
        private TextBox SumaWydajnosc;
        private Label label19;
        private TextBox SumaWartosc;
        private TextBox SumaCena;
        private TextBox SumaKG;
        private TextBox RoznicaTuszkaElement;
        private TextBox SumaWartosc2;
        private Label label21;
        private Label label22;
        private TextBox TuszkaKG2;
        private TextBox WspolczynnikKrojenia;
        private TextBox WartoscKrojenia;
        private Label label20;
        private TextBox WartoscElementowPoKrojeniu;
        private Label label23;
        private TextBox WartoscElementowPoKrojeniu2;
        private Label label25;
        private TextBox TuszkaWartosc2;
        private Label label26;
        private TextBox RoznicaTuszkaElement2;
        private Label label27;
        private Label label28;
        private Label label29;
        private Label label30;
        private TextBox textBox2;
        private Label label32;
        private TextBox textBox3;
        private Label label33;
        private TextBox textBox4;
        private TextBox textBox5;
        private PictureBox pictureBox3;
        private TextBox textBox6;
        private Label label31;
        private PictureBox pictureBox1;
        private PictureBox pictureBox2;
        private PictureBox pictureBox4;
        private Label label34;
        private Label label35;
        private TextBox SktrechNaPalete;
        private Label label36;
        private TextBox PaletaDrewniana;
        private TextBox Karton;
        private Label label37;
        private Label label38;
        private PictureBox pictureBox6;
        private Label label39;
        private TextBox PradMroznia;
        private PictureBox pictureBox7;
        private TextBox SumaKosztowMrozenia;
        private Label label40;
        private Label label41;
        private TextBox KilogramyDoZamrozenia;
        private TextBox SktrechNaPaleteCena;
        private TextBox FoliaPojemnikCena;
        private TextBox PaletaDrewnianaCena;
        private TextBox KartonCena;
        private TextBox PradMrozniaCena;
        private Label label42;
        private TextBox RozwazanieTowaruCena;
        private TextBox RozwazanieTowaru;
        private TextBox FoliaPojemnik;
        private PictureBox pictureBox8;
        private PictureBox pictureBox10;
        private PictureBox pictureBox11;
        private Label label24;
        private Label label43;
        private Label label44;
        private TextBox textBox7;
        private PictureBox pictureBox9;
        private PictureBox pictureBox5;
        private TextBox WartoscKrojenia2;
        private PictureBox pictureBox13;
        private TextBox WartoscElementowZamrozonych;
        private PictureBox pictureBox14;
        private TextBox SumaKosztowMrozenia2;
        private Label label45;
        private Label label46;
        private PictureBox pictureBox15;
        private PictureBox pictureBox12;
        private Label label47;
        private Label label48;
        private Label label49;
        private Label label50;
        private PictureBox pictureBox16;
        private TextBox KosztZanizeniaCeny;
        private Label label51;
        private PictureBox pictureBox17;
        private TextBox WartoscElementowMrozonychPoObnizce;
        private Label label52;
        private PictureBox pictureBox18;
        private TextBox TuszkaWartosc3;
        private Label label53;
        private PictureBox pictureBox19;
        private TextBox WartoscElementowMrozonychPoObnizce2;
        private Label label54;
        private TextBox Strata;
        private PictureBox pictureBox20;
        private PictureBox pictureBox29;
        private PictureBox pictureBox21;
        private Label label55;
        private Label label56;
        private PictureBox pictureBox22;
        private TextBox RoznicaTuszkaElement3;
        private Label label11;

        public PokazKrojenieMrozenie()
        {
            InitializeComponent();

            TuszkaWydajnosc.Text = "100";
            SumaWydajnosc.Text = "100";
            FiletWydajnosc.Text = "31,4";
            CwiartkaWydajnosc.Text = "35,4";
            SkrzydloWydajnosc.Text = "9,7";
            KorpusWydajnosc.Text = "22,7";
            PozostaleWydajnosc.Text = "0,8";

            WspolczynnikKrojenia.Text = "1,7";
            RozwazanieTowaruCena.Text = "1,5";
            FoliaPojemnikCena.Text = "0,38";
            SktrechNaPaleteCena.Text = "24,00";
            KartonCena.Text = "0,30";
            PaletaDrewnianaCena.Text = "13,20";
            PradMrozniaCena.Text = "0,30";


        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PokazKrojenieMrozenie));
            sztuki = new TextBox();
            label11 = new Label();
            sztukNaSzuflade = new TextBox();
            label10 = new Label();
            label1 = new Label();
            label2 = new Label();
            textBox1 = new TextBox();
            label3 = new Label();
            label4 = new Label();
            obliczeniaAut = new TextBox();
            label12 = new Label();
            label5 = new Label();
            label6 = new Label();
            sumaSztuk = new TextBox();
            buttonZamknij = new Button();
            button1 = new Button();
            TuszkaKG = new TextBox();
            label7 = new Label();
            TuszkaCena = new TextBox();
            label8 = new Label();
            TuszkaWartosc = new TextBox();
            label9 = new Label();
            FiletWartosc = new TextBox();
            FiletCena = new TextBox();
            FiletKG = new TextBox();
            CwiartkaWartosc = new TextBox();
            CwiartkaCena = new TextBox();
            CwiartkaKG = new TextBox();
            SkrzydloWartosc = new TextBox();
            SkrzydloCena = new TextBox();
            SkrzydloKG = new TextBox();
            KorpusWartosc = new TextBox();
            KorpusCena = new TextBox();
            KorpusKG = new TextBox();
            label13 = new Label();
            label14 = new Label();
            label15 = new Label();
            label16 = new Label();
            label17 = new Label();
            PozostaleWartosc = new TextBox();
            PozostaleCena = new TextBox();
            PozostaleKG = new TextBox();
            TuszkaWydajnosc = new TextBox();
            label18 = new Label();
            FiletWydajnosc = new TextBox();
            CwiartkaWydajnosc = new TextBox();
            SkrzydloWydajnosc = new TextBox();
            KorpusWydajnosc = new TextBox();
            PozostaleWydajnosc = new TextBox();
            SumaWydajnosc = new TextBox();
            label19 = new Label();
            SumaWartosc = new TextBox();
            SumaCena = new TextBox();
            SumaKG = new TextBox();
            RoznicaTuszkaElement = new TextBox();
            SumaWartosc2 = new TextBox();
            label21 = new Label();
            label22 = new Label();
            TuszkaKG2 = new TextBox();
            WspolczynnikKrojenia = new TextBox();
            WartoscKrojenia = new TextBox();
            label20 = new Label();
            WartoscElementowPoKrojeniu = new TextBox();
            label23 = new Label();
            WartoscElementowPoKrojeniu2 = new TextBox();
            label25 = new Label();
            TuszkaWartosc2 = new TextBox();
            label26 = new Label();
            RoznicaTuszkaElement2 = new TextBox();
            label27 = new Label();
            label28 = new Label();
            label29 = new Label();
            label30 = new Label();
            textBox2 = new TextBox();
            label32 = new Label();
            textBox3 = new TextBox();
            label33 = new Label();
            textBox4 = new TextBox();
            textBox5 = new TextBox();
            pictureBox3 = new PictureBox();
            textBox6 = new TextBox();
            label31 = new Label();
            pictureBox1 = new PictureBox();
            pictureBox2 = new PictureBox();
            pictureBox4 = new PictureBox();
            label34 = new Label();
            label35 = new Label();
            label36 = new Label();
            label37 = new Label();
            label38 = new Label();
            pictureBox6 = new PictureBox();
            label39 = new Label();
            PradMroznia = new TextBox();
            pictureBox7 = new PictureBox();
            SumaKosztowMrozenia = new TextBox();
            label40 = new Label();
            label41 = new Label();
            KilogramyDoZamrozenia = new TextBox();
            SktrechNaPaleteCena = new TextBox();
            FoliaPojemnikCena = new TextBox();
            PaletaDrewnianaCena = new TextBox();
            KartonCena = new TextBox();
            PradMrozniaCena = new TextBox();
            label42 = new Label();
            RozwazanieTowaruCena = new TextBox();
            RozwazanieTowaru = new TextBox();
            FoliaPojemnik = new TextBox();
            SktrechNaPalete = new TextBox();
            PaletaDrewniana = new TextBox();
            Karton = new TextBox();
            pictureBox8 = new PictureBox();
            pictureBox10 = new PictureBox();
            pictureBox11 = new PictureBox();
            label24 = new Label();
            label43 = new Label();
            label44 = new Label();
            textBox7 = new TextBox();
            pictureBox9 = new PictureBox();
            pictureBox5 = new PictureBox();
            WartoscKrojenia2 = new TextBox();
            pictureBox13 = new PictureBox();
            WartoscElementowZamrozonych = new TextBox();
            pictureBox14 = new PictureBox();
            SumaKosztowMrozenia2 = new TextBox();
            label45 = new Label();
            label46 = new Label();
            pictureBox15 = new PictureBox();
            pictureBox12 = new PictureBox();
            label47 = new Label();
            label48 = new Label();
            label49 = new Label();
            label50 = new Label();
            pictureBox16 = new PictureBox();
            KosztZanizeniaCeny = new TextBox();
            label51 = new Label();
            pictureBox17 = new PictureBox();
            WartoscElementowMrozonychPoObnizce = new TextBox();
            label52 = new Label();
            pictureBox18 = new PictureBox();
            TuszkaWartosc3 = new TextBox();
            label53 = new Label();
            pictureBox19 = new PictureBox();
            WartoscElementowMrozonychPoObnizce2 = new TextBox();
            label54 = new Label();
            Strata = new TextBox();
            pictureBox20 = new PictureBox();
            pictureBox29 = new PictureBox();
            pictureBox21 = new PictureBox();
            label55 = new Label();
            label56 = new Label();
            pictureBox22 = new PictureBox();
            RoznicaTuszkaElement3 = new TextBox();
            ((System.ComponentModel.ISupportInitialize)pictureBox3).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox2).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox4).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox6).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox7).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox8).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox10).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox11).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox9).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox5).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox13).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox14).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox15).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox12).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox16).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox17).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox18).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox19).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox20).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox29).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox21).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox22).BeginInit();
            SuspendLayout();
            // 
            // sztuki
            // 
            sztuki.BackColor = System.Drawing.SystemColors.ControlLight;
            sztuki.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            sztuki.Location = new System.Drawing.Point(1587, 233);
            sztuki.Name = "sztuki";
            sztuki.Size = new System.Drawing.Size(62, 25);
            sztuki.TabIndex = 26;
            sztuki.TextAlign = HorizontalAlignment.Center;
            // 
            // label11
            // 
            label11.Location = new System.Drawing.Point(1403, 739);
            label11.Name = "label11";
            label11.Size = new System.Drawing.Size(62, 47);
            label11.TabIndex = 25;
            label11.Text = "Ilość skrzynek na 1 auto";
            label11.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // sztukNaSzuflade
            // 
            sztukNaSzuflade.BackColor = System.Drawing.Color.White;
            sztukNaSzuflade.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            sztukNaSzuflade.Location = new System.Drawing.Point(1322, 789);
            sztukNaSzuflade.Name = "sztukNaSzuflade";
            sztukNaSzuflade.Size = new System.Drawing.Size(57, 25);
            sztukNaSzuflade.TabIndex = 28;
            sztukNaSzuflade.TextAlign = HorizontalAlignment.Center;
            // 
            // label10
            // 
            label10.Location = new System.Drawing.Point(1621, 690);
            label10.Name = "label10";
            label10.Size = new System.Drawing.Size(57, 47);
            label10.TabIndex = 27;
            label10.Text = "Sztuki na szuflade";
            label10.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.BackColor = System.Drawing.SystemColors.ControlLight;
            label1.Location = new System.Drawing.Point(1385, 794);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(12, 16);
            label1.TabIndex = 29;
            label1.Text = "*";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(1471, 794);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(14, 16);
            label2.TabIndex = 30;
            label2.Text = "=";
            // 
            // textBox1
            // 
            textBox1.BackColor = System.Drawing.SystemColors.ControlLight;
            textBox1.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            textBox1.Location = new System.Drawing.Point(1435, 653);
            textBox1.Name = "textBox1";
            textBox1.Size = new System.Drawing.Size(62, 25);
            textBox1.TabIndex = 31;
            textBox1.Text = "264";
            textBox1.TextAlign = HorizontalAlignment.Center;
            // 
            // label3
            // 
            label3.Location = new System.Drawing.Point(1492, 736);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(62, 47);
            label3.TabIndex = 32;
            label3.Text = "Deklarowane sztuki na 1 auto";
            label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new System.Drawing.Point(1560, 794);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(12, 16);
            label4.TabIndex = 33;
            label4.Text = "*";
            // 
            // obliczeniaAut
            // 
            obliczeniaAut.BackColor = System.Drawing.SystemColors.Window;
            obliczeniaAut.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            obliczeniaAut.Location = new System.Drawing.Point(1578, 789);
            obliczeniaAut.Name = "obliczeniaAut";
            obliczeniaAut.Size = new System.Drawing.Size(36, 25);
            obliczeniaAut.TabIndex = 34;
            obliczeniaAut.TextAlign = HorizontalAlignment.Center;
            // 
            // label12
            // 
            label12.Location = new System.Drawing.Point(1578, 742);
            label12.Name = "label12";
            label12.Size = new System.Drawing.Size(36, 35);
            label12.TabIndex = 35;
            label12.Text = "Ilość aut";
            label12.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new System.Drawing.Point(1694, 335);
            label5.Name = "label5";
            label5.Size = new System.Drawing.Size(14, 16);
            label5.TabIndex = 36;
            label5.Text = "=";
            // 
            // label6
            // 
            label6.Location = new System.Drawing.Point(1550, 715);
            label6.Name = "label6";
            label6.Size = new System.Drawing.Size(93, 47);
            label6.TabIndex = 38;
            label6.Text = "Suma deklarowanych sztuk";
            label6.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // sumaSztuk
            // 
            sumaSztuk.BackColor = System.Drawing.SystemColors.ControlLight;
            sumaSztuk.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            sumaSztuk.Location = new System.Drawing.Point(1542, 212);
            sumaSztuk.Name = "sumaSztuk";
            sumaSztuk.Size = new System.Drawing.Size(86, 25);
            sumaSztuk.TabIndex = 37;
            sumaSztuk.TextAlign = HorizontalAlignment.Center;
            // 
            // buttonZamknij
            // 
            buttonZamknij.BackColor = System.Drawing.Color.IndianRed;
            buttonZamknij.Location = new System.Drawing.Point(1568, 839);
            buttonZamknij.Name = "buttonZamknij";
            buttonZamknij.Size = new System.Drawing.Size(75, 23);
            buttonZamknij.TabIndex = 40;
            buttonZamknij.Text = "Anuluj";
            buttonZamknij.UseVisualStyleBackColor = false;
            // 
            // button1
            // 
            button1.BackColor = System.Drawing.Color.Chartreuse;
            button1.Location = new System.Drawing.Point(1649, 824);
            button1.Name = "button1";
            button1.Size = new System.Drawing.Size(75, 23);
            button1.TabIndex = 39;
            button1.Text = "Stwórz";
            button1.UseVisualStyleBackColor = false;
            // 
            // TuszkaKG
            // 
            TuszkaKG.BackColor = System.Drawing.Color.LightGreen;
            TuszkaKG.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            TuszkaKG.Location = new System.Drawing.Point(141, 19);
            TuszkaKG.Name = "TuszkaKG";
            TuszkaKG.Size = new System.Drawing.Size(106, 27);
            TuszkaKG.TabIndex = 42;
            TuszkaKG.TextAlign = HorizontalAlignment.Center;
            TuszkaKG.TextChanged += TuszkaKG_TextChanged_1;
            // 
            // label7
            // 
            label7.Location = new System.Drawing.Point(141, 1);
            label7.Name = "label7";
            label7.Size = new System.Drawing.Size(106, 16);
            label7.TabIndex = 41;
            label7.Text = "KG";
            label7.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // TuszkaCena
            // 
            TuszkaCena.BackColor = System.Drawing.Color.LightGreen;
            TuszkaCena.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            TuszkaCena.Location = new System.Drawing.Point(253, 19);
            TuszkaCena.Name = "TuszkaCena";
            TuszkaCena.Size = new System.Drawing.Size(57, 27);
            TuszkaCena.TabIndex = 44;
            TuszkaCena.TextAlign = HorizontalAlignment.Center;
            TuszkaCena.TextChanged += TuszkaCena_TextChanged_1;
            // 
            // label8
            // 
            label8.Location = new System.Drawing.Point(253, 1);
            label8.Name = "label8";
            label8.Size = new System.Drawing.Size(57, 16);
            label8.TabIndex = 43;
            label8.Text = "Cena";
            label8.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // TuszkaWartosc
            // 
            TuszkaWartosc.BackColor = System.Drawing.Color.LightGreen;
            TuszkaWartosc.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            TuszkaWartosc.Location = new System.Drawing.Point(316, 19);
            TuszkaWartosc.Name = "TuszkaWartosc";
            TuszkaWartosc.Size = new System.Drawing.Size(117, 27);
            TuszkaWartosc.TabIndex = 46;
            TuszkaWartosc.TextAlign = HorizontalAlignment.Center;
            // 
            // label9
            // 
            label9.Location = new System.Drawing.Point(316, 1);
            label9.Name = "label9";
            label9.Size = new System.Drawing.Size(117, 16);
            label9.TabIndex = 45;
            label9.Text = "Wartość";
            label9.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // FiletWartosc
            // 
            FiletWartosc.BackColor = System.Drawing.Color.White;
            FiletWartosc.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            FiletWartosc.Location = new System.Drawing.Point(316, 88);
            FiletWartosc.Name = "FiletWartosc";
            FiletWartosc.Size = new System.Drawing.Size(117, 25);
            FiletWartosc.TabIndex = 52;
            FiletWartosc.TextAlign = HorizontalAlignment.Center;
            // 
            // FiletCena
            // 
            FiletCena.BackColor = System.Drawing.Color.White;
            FiletCena.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            FiletCena.Location = new System.Drawing.Point(253, 88);
            FiletCena.Name = "FiletCena";
            FiletCena.Size = new System.Drawing.Size(57, 25);
            FiletCena.TabIndex = 50;
            FiletCena.TextAlign = HorizontalAlignment.Center;
            FiletCena.TextChanged += FiletCena_TextChanged;
            // 
            // FiletKG
            // 
            FiletKG.BackColor = System.Drawing.Color.White;
            FiletKG.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            FiletKG.Location = new System.Drawing.Point(141, 88);
            FiletKG.Name = "FiletKG";
            FiletKG.Size = new System.Drawing.Size(106, 25);
            FiletKG.TabIndex = 48;
            FiletKG.TextAlign = HorizontalAlignment.Center;
            FiletKG.TextChanged += FiletKG_TextChanged;
            // 
            // CwiartkaWartosc
            // 
            CwiartkaWartosc.BackColor = System.Drawing.Color.White;
            CwiartkaWartosc.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            CwiartkaWartosc.Location = new System.Drawing.Point(316, 119);
            CwiartkaWartosc.Name = "CwiartkaWartosc";
            CwiartkaWartosc.Size = new System.Drawing.Size(117, 25);
            CwiartkaWartosc.TabIndex = 58;
            CwiartkaWartosc.TextAlign = HorizontalAlignment.Center;
            // 
            // CwiartkaCena
            // 
            CwiartkaCena.BackColor = System.Drawing.Color.White;
            CwiartkaCena.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            CwiartkaCena.Location = new System.Drawing.Point(253, 119);
            CwiartkaCena.Name = "CwiartkaCena";
            CwiartkaCena.Size = new System.Drawing.Size(57, 25);
            CwiartkaCena.TabIndex = 56;
            CwiartkaCena.TextAlign = HorizontalAlignment.Center;
            CwiartkaCena.TextChanged += CwiartkaCena_TextChanged;
            // 
            // CwiartkaKG
            // 
            CwiartkaKG.BackColor = System.Drawing.Color.White;
            CwiartkaKG.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            CwiartkaKG.Location = new System.Drawing.Point(141, 119);
            CwiartkaKG.Name = "CwiartkaKG";
            CwiartkaKG.Size = new System.Drawing.Size(106, 25);
            CwiartkaKG.TabIndex = 54;
            CwiartkaKG.TextAlign = HorizontalAlignment.Center;
            CwiartkaKG.TextChanged += CwiartkaKG_TextChanged;
            // 
            // SkrzydloWartosc
            // 
            SkrzydloWartosc.BackColor = System.Drawing.Color.White;
            SkrzydloWartosc.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            SkrzydloWartosc.Location = new System.Drawing.Point(316, 150);
            SkrzydloWartosc.Name = "SkrzydloWartosc";
            SkrzydloWartosc.Size = new System.Drawing.Size(117, 25);
            SkrzydloWartosc.TabIndex = 64;
            SkrzydloWartosc.TextAlign = HorizontalAlignment.Center;
            // 
            // SkrzydloCena
            // 
            SkrzydloCena.BackColor = System.Drawing.Color.White;
            SkrzydloCena.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            SkrzydloCena.Location = new System.Drawing.Point(253, 150);
            SkrzydloCena.Name = "SkrzydloCena";
            SkrzydloCena.Size = new System.Drawing.Size(57, 25);
            SkrzydloCena.TabIndex = 62;
            SkrzydloCena.TextAlign = HorizontalAlignment.Center;
            SkrzydloCena.TextChanged += SkrzydloCena_TextChanged;
            // 
            // SkrzydloKG
            // 
            SkrzydloKG.BackColor = System.Drawing.Color.White;
            SkrzydloKG.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            SkrzydloKG.Location = new System.Drawing.Point(141, 150);
            SkrzydloKG.Name = "SkrzydloKG";
            SkrzydloKG.Size = new System.Drawing.Size(106, 25);
            SkrzydloKG.TabIndex = 60;
            SkrzydloKG.TextAlign = HorizontalAlignment.Center;
            SkrzydloKG.TextChanged += SkrzydloKG_TextChanged;
            // 
            // KorpusWartosc
            // 
            KorpusWartosc.BackColor = System.Drawing.Color.White;
            KorpusWartosc.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            KorpusWartosc.Location = new System.Drawing.Point(316, 181);
            KorpusWartosc.Name = "KorpusWartosc";
            KorpusWartosc.Size = new System.Drawing.Size(117, 25);
            KorpusWartosc.TabIndex = 70;
            KorpusWartosc.TextAlign = HorizontalAlignment.Center;
            // 
            // KorpusCena
            // 
            KorpusCena.BackColor = System.Drawing.Color.White;
            KorpusCena.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            KorpusCena.Location = new System.Drawing.Point(253, 181);
            KorpusCena.Name = "KorpusCena";
            KorpusCena.Size = new System.Drawing.Size(57, 25);
            KorpusCena.TabIndex = 68;
            KorpusCena.TextAlign = HorizontalAlignment.Center;
            KorpusCena.TextChanged += KorpusCena_TextChanged;
            // 
            // KorpusKG
            // 
            KorpusKG.BackColor = System.Drawing.Color.White;
            KorpusKG.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            KorpusKG.Location = new System.Drawing.Point(141, 181);
            KorpusKG.Name = "KorpusKG";
            KorpusKG.Size = new System.Drawing.Size(106, 25);
            KorpusKG.TabIndex = 66;
            KorpusKG.TextAlign = HorizontalAlignment.Center;
            // 
            // label13
            // 
            label13.Location = new System.Drawing.Point(15, 88);
            label13.Name = "label13";
            label13.Size = new System.Drawing.Size(57, 25);
            label13.TabIndex = 71;
            label13.Text = "Filet";
            label13.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label14
            // 
            label14.Location = new System.Drawing.Point(15, 119);
            label14.Name = "label14";
            label14.Size = new System.Drawing.Size(57, 25);
            label14.TabIndex = 72;
            label14.Text = "Ćwiartka";
            label14.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label15
            // 
            label15.Location = new System.Drawing.Point(15, 150);
            label15.Name = "label15";
            label15.Size = new System.Drawing.Size(57, 25);
            label15.TabIndex = 73;
            label15.Text = "Skrzydło";
            label15.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label16
            // 
            label16.Location = new System.Drawing.Point(15, 181);
            label16.Name = "label16";
            label16.Size = new System.Drawing.Size(57, 25);
            label16.TabIndex = 74;
            label16.Text = "Korpus";
            label16.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label17
            // 
            label17.Location = new System.Drawing.Point(15, 212);
            label17.Name = "label17";
            label17.Size = new System.Drawing.Size(57, 25);
            label17.TabIndex = 78;
            label17.Text = "Pozostałe";
            label17.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // PozostaleWartosc
            // 
            PozostaleWartosc.BackColor = System.Drawing.Color.White;
            PozostaleWartosc.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            PozostaleWartosc.Location = new System.Drawing.Point(316, 212);
            PozostaleWartosc.Name = "PozostaleWartosc";
            PozostaleWartosc.Size = new System.Drawing.Size(117, 25);
            PozostaleWartosc.TabIndex = 77;
            PozostaleWartosc.TextAlign = HorizontalAlignment.Center;
            // 
            // PozostaleCena
            // 
            PozostaleCena.BackColor = System.Drawing.Color.White;
            PozostaleCena.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            PozostaleCena.Location = new System.Drawing.Point(253, 212);
            PozostaleCena.Name = "PozostaleCena";
            PozostaleCena.Size = new System.Drawing.Size(57, 25);
            PozostaleCena.TabIndex = 76;
            PozostaleCena.TextAlign = HorizontalAlignment.Center;
            PozostaleCena.TextChanged += PozostaleCena_TextChanged;
            // 
            // PozostaleKG
            // 
            PozostaleKG.BackColor = System.Drawing.Color.White;
            PozostaleKG.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            PozostaleKG.Location = new System.Drawing.Point(141, 212);
            PozostaleKG.Name = "PozostaleKG";
            PozostaleKG.Size = new System.Drawing.Size(106, 25);
            PozostaleKG.TabIndex = 75;
            PozostaleKG.TextAlign = HorizontalAlignment.Center;
            // 
            // TuszkaWydajnosc
            // 
            TuszkaWydajnosc.BackColor = System.Drawing.Color.LightGreen;
            TuszkaWydajnosc.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            TuszkaWydajnosc.Location = new System.Drawing.Point(78, 19);
            TuszkaWydajnosc.Name = "TuszkaWydajnosc";
            TuszkaWydajnosc.Size = new System.Drawing.Size(57, 27);
            TuszkaWydajnosc.TabIndex = 79;
            TuszkaWydajnosc.TextAlign = HorizontalAlignment.Center;
            // 
            // label18
            // 
            label18.Location = new System.Drawing.Point(78, 1);
            label18.Name = "label18";
            label18.Size = new System.Drawing.Size(57, 16);
            label18.TabIndex = 80;
            label18.Text = "%";
            label18.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // FiletWydajnosc
            // 
            FiletWydajnosc.BackColor = System.Drawing.Color.White;
            FiletWydajnosc.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            FiletWydajnosc.Location = new System.Drawing.Point(78, 88);
            FiletWydajnosc.Name = "FiletWydajnosc";
            FiletWydajnosc.Size = new System.Drawing.Size(57, 25);
            FiletWydajnosc.TabIndex = 81;
            FiletWydajnosc.TextAlign = HorizontalAlignment.Center;
            FiletWydajnosc.TextChanged += FiletWydajnosc_TextChanged;
            // 
            // CwiartkaWydajnosc
            // 
            CwiartkaWydajnosc.BackColor = System.Drawing.Color.White;
            CwiartkaWydajnosc.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            CwiartkaWydajnosc.Location = new System.Drawing.Point(78, 119);
            CwiartkaWydajnosc.Name = "CwiartkaWydajnosc";
            CwiartkaWydajnosc.Size = new System.Drawing.Size(57, 25);
            CwiartkaWydajnosc.TabIndex = 82;
            CwiartkaWydajnosc.TextAlign = HorizontalAlignment.Center;
            CwiartkaWydajnosc.TextChanged += CwiartkaWydajnosc_TextChanged;
            // 
            // SkrzydloWydajnosc
            // 
            SkrzydloWydajnosc.BackColor = System.Drawing.Color.White;
            SkrzydloWydajnosc.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            SkrzydloWydajnosc.Location = new System.Drawing.Point(78, 150);
            SkrzydloWydajnosc.Name = "SkrzydloWydajnosc";
            SkrzydloWydajnosc.Size = new System.Drawing.Size(57, 25);
            SkrzydloWydajnosc.TabIndex = 83;
            SkrzydloWydajnosc.TextAlign = HorizontalAlignment.Center;
            SkrzydloWydajnosc.TextChanged += SkrzydloWydajnosc_TextChanged;
            // 
            // KorpusWydajnosc
            // 
            KorpusWydajnosc.BackColor = System.Drawing.Color.White;
            KorpusWydajnosc.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            KorpusWydajnosc.Location = new System.Drawing.Point(78, 181);
            KorpusWydajnosc.Name = "KorpusWydajnosc";
            KorpusWydajnosc.Size = new System.Drawing.Size(57, 25);
            KorpusWydajnosc.TabIndex = 84;
            KorpusWydajnosc.TextAlign = HorizontalAlignment.Center;
            KorpusWydajnosc.TextChanged += KorpusWydajnosc_TextChanged;
            // 
            // PozostaleWydajnosc
            // 
            PozostaleWydajnosc.BackColor = System.Drawing.Color.White;
            PozostaleWydajnosc.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            PozostaleWydajnosc.Location = new System.Drawing.Point(78, 212);
            PozostaleWydajnosc.Name = "PozostaleWydajnosc";
            PozostaleWydajnosc.Size = new System.Drawing.Size(57, 25);
            PozostaleWydajnosc.TabIndex = 85;
            PozostaleWydajnosc.TextAlign = HorizontalAlignment.Center;
            PozostaleWydajnosc.TextChanged += PozostaleWydajnosc_TextChanged;
            // 
            // SumaWydajnosc
            // 
            SumaWydajnosc.BackColor = System.Drawing.Color.IndianRed;
            SumaWydajnosc.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            SumaWydajnosc.Location = new System.Drawing.Point(78, 55);
            SumaWydajnosc.Name = "SumaWydajnosc";
            SumaWydajnosc.Size = new System.Drawing.Size(57, 27);
            SumaWydajnosc.TabIndex = 90;
            SumaWydajnosc.TextAlign = HorizontalAlignment.Center;
            // 
            // label19
            // 
            label19.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label19.Location = new System.Drawing.Point(6, 55);
            label19.Name = "label19";
            label19.Size = new System.Drawing.Size(66, 25);
            label19.TabIndex = 89;
            label19.Text = "Elementy";
            label19.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // SumaWartosc
            // 
            SumaWartosc.BackColor = System.Drawing.Color.IndianRed;
            SumaWartosc.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            SumaWartosc.Location = new System.Drawing.Point(316, 55);
            SumaWartosc.Name = "SumaWartosc";
            SumaWartosc.Size = new System.Drawing.Size(117, 27);
            SumaWartosc.TabIndex = 88;
            SumaWartosc.TextAlign = HorizontalAlignment.Center;
            // 
            // SumaCena
            // 
            SumaCena.BackColor = System.Drawing.Color.IndianRed;
            SumaCena.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            SumaCena.Location = new System.Drawing.Point(253, 55);
            SumaCena.Name = "SumaCena";
            SumaCena.Size = new System.Drawing.Size(57, 27);
            SumaCena.TabIndex = 87;
            SumaCena.TextAlign = HorizontalAlignment.Center;
            SumaCena.TextChanged += SumaCena_TextChanged;
            // 
            // SumaKG
            // 
            SumaKG.BackColor = System.Drawing.Color.IndianRed;
            SumaKG.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            SumaKG.Location = new System.Drawing.Point(141, 55);
            SumaKG.Name = "SumaKG";
            SumaKG.Size = new System.Drawing.Size(106, 27);
            SumaKG.TabIndex = 86;
            SumaKG.TextAlign = HorizontalAlignment.Center;
            // 
            // RoznicaTuszkaElement
            // 
            RoznicaTuszkaElement.BackColor = System.Drawing.Color.White;
            RoznicaTuszkaElement.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            RoznicaTuszkaElement.Location = new System.Drawing.Point(439, 38);
            RoznicaTuszkaElement.Name = "RoznicaTuszkaElement";
            RoznicaTuszkaElement.Size = new System.Drawing.Size(89, 27);
            RoznicaTuszkaElement.TabIndex = 91;
            RoznicaTuszkaElement.TextAlign = HorizontalAlignment.Center;
            // 
            // SumaWartosc2
            // 
            SumaWartosc2.BackColor = System.Drawing.SystemColors.ButtonFace;
            SumaWartosc2.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            SumaWartosc2.Location = new System.Drawing.Point(768, 185);
            SumaWartosc2.Multiline = true;
            SumaWartosc2.Name = "SumaWartosc2";
            SumaWartosc2.Size = new System.Drawing.Size(207, 63);
            SumaWartosc2.TabIndex = 94;
            SumaWartosc2.TextAlign = HorizontalAlignment.Center;
            // 
            // label21
            // 
            label21.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label21.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            label21.Location = new System.Drawing.Point(1340, 272);
            label21.Name = "label21";
            label21.Size = new System.Drawing.Size(66, 27);
            label21.TabIndex = 95;
            label21.Text = "Elementy";
            label21.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label22
            // 
            label22.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label22.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            label22.Location = new System.Drawing.Point(1317, 308);
            label22.Name = "label22";
            label22.Size = new System.Drawing.Size(89, 25);
            label22.TabIndex = 97;
            label22.Text = "Koszt Krojenia";
            label22.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // TuszkaKG2
            // 
            TuszkaKG2.BackColor = System.Drawing.Color.LightGreen;
            TuszkaKG2.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            TuszkaKG2.Location = new System.Drawing.Point(153, 276);
            TuszkaKG2.Multiline = true;
            TuszkaKG2.Name = "TuszkaKG2";
            TuszkaKG2.Size = new System.Drawing.Size(206, 46);
            TuszkaKG2.TabIndex = 96;
            TuszkaKG2.TextAlign = HorizontalAlignment.Center;
            // 
            // WspolczynnikKrojenia
            // 
            WspolczynnikKrojenia.BackColor = System.Drawing.Color.Gainsboro;
            WspolczynnikKrojenia.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            WspolczynnikKrojenia.Location = new System.Drawing.Point(216, 328);
            WspolczynnikKrojenia.Multiline = true;
            WspolczynnikKrojenia.Name = "WspolczynnikKrojenia";
            WspolczynnikKrojenia.Size = new System.Drawing.Size(83, 23);
            WspolczynnikKrojenia.TabIndex = 98;
            WspolczynnikKrojenia.TextAlign = HorizontalAlignment.Center;
            WspolczynnikKrojenia.TextChanged += WspolczynnikKrojenia_TextChanged;
            // 
            // WartoscKrojenia
            // 
            WartoscKrojenia.BackColor = System.Drawing.Color.Gainsboro;
            WartoscKrojenia.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            WartoscKrojenia.Location = new System.Drawing.Point(154, 357);
            WartoscKrojenia.Multiline = true;
            WartoscKrojenia.Name = "WartoscKrojenia";
            WartoscKrojenia.Size = new System.Drawing.Size(205, 45);
            WartoscKrojenia.TabIndex = 99;
            WartoscKrojenia.TextAlign = HorizontalAlignment.Center;
            // 
            // label20
            // 
            label20.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label20.Location = new System.Drawing.Point(1634, 574);
            label20.Name = "label20";
            label20.Size = new System.Drawing.Size(89, 27);
            label20.TabIndex = 101;
            label20.Text = "Elementy po krojeniu";
            label20.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // WartoscElementowPoKrojeniu
            // 
            WartoscElementowPoKrojeniu.BackColor = System.Drawing.Color.IndianRed;
            WartoscElementowPoKrojeniu.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            WartoscElementowPoKrojeniu.Location = new System.Drawing.Point(1412, 272);
            WartoscElementowPoKrojeniu.Name = "WartoscElementowPoKrojeniu";
            WartoscElementowPoKrojeniu.Size = new System.Drawing.Size(117, 27);
            WartoscElementowPoKrojeniu.TabIndex = 100;
            WartoscElementowPoKrojeniu.TextAlign = HorizontalAlignment.Center;
            // 
            // label23
            // 
            label23.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label23.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            label23.Location = new System.Drawing.Point(1289, 163);
            label23.Name = "label23";
            label23.Size = new System.Drawing.Size(117, 27);
            label23.TabIndex = 103;
            label23.Text = "Wartość Elementów po krojeniu";
            label23.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // WartoscElementowPoKrojeniu2
            // 
            WartoscElementowPoKrojeniu2.BackColor = System.Drawing.SystemColors.ButtonFace;
            WartoscElementowPoKrojeniu2.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            WartoscElementowPoKrojeniu2.Location = new System.Drawing.Point(767, 323);
            WartoscElementowPoKrojeniu2.Multiline = true;
            WartoscElementowPoKrojeniu2.Name = "WartoscElementowPoKrojeniu2";
            WartoscElementowPoKrojeniu2.Size = new System.Drawing.Size(207, 63);
            WartoscElementowPoKrojeniu2.TabIndex = 102;
            WartoscElementowPoKrojeniu2.TextAlign = HorizontalAlignment.Center;
            // 
            // label25
            // 
            label25.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label25.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            label25.Location = new System.Drawing.Point(1289, 130);
            label25.Name = "label25";
            label25.Size = new System.Drawing.Size(117, 27);
            label25.TabIndex = 105;
            label25.Text = "Wartość Tuszki bez przerobu";
            label25.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // TuszkaWartosc2
            // 
            TuszkaWartosc2.BackColor = System.Drawing.SystemColors.ButtonFace;
            TuszkaWartosc2.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            TuszkaWartosc2.Location = new System.Drawing.Point(768, 49);
            TuszkaWartosc2.Multiline = true;
            TuszkaWartosc2.Name = "TuszkaWartosc2";
            TuszkaWartosc2.Size = new System.Drawing.Size(207, 61);
            TuszkaWartosc2.TabIndex = 104;
            TuszkaWartosc2.TextAlign = HorizontalAlignment.Center;
            // 
            // label26
            // 
            label26.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label26.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            label26.Location = new System.Drawing.Point(1289, 196);
            label26.Name = "label26";
            label26.Size = new System.Drawing.Size(117, 27);
            label26.TabIndex = 107;
            label26.Text = "Różnica między elementami a tuszką";
            label26.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // RoznicaTuszkaElement2
            // 
            RoznicaTuszkaElement2.BackColor = System.Drawing.Color.White;
            RoznicaTuszkaElement2.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            RoznicaTuszkaElement2.Location = new System.Drawing.Point(1412, 196);
            RoznicaTuszkaElement2.Name = "RoznicaTuszkaElement2";
            RoznicaTuszkaElement2.Size = new System.Drawing.Size(117, 27);
            RoznicaTuszkaElement2.TabIndex = 106;
            RoznicaTuszkaElement2.TextAlign = HorizontalAlignment.Center;
            // 
            // label27
            // 
            label27.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label27.Location = new System.Drawing.Point(1376, 372);
            label27.Name = "label27";
            label27.Size = new System.Drawing.Size(13, 27);
            label27.TabIndex = 108;
            label27.Text = "-";
            label27.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label28
            // 
            label28.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label28.Location = new System.Drawing.Point(1513, 373);
            label28.Name = "label28";
            label28.Size = new System.Drawing.Size(16, 27);
            label28.TabIndex = 109;
            label28.Text = "=";
            label28.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label29
            // 
            label29.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label29.Location = new System.Drawing.Point(1513, 790);
            label29.Name = "label29";
            label29.Size = new System.Drawing.Size(16, 27);
            label29.TabIndex = 117;
            label29.Text = "=";
            label29.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label30
            // 
            label30.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label30.Location = new System.Drawing.Point(1376, 789);
            label30.Name = "label30";
            label30.Size = new System.Drawing.Size(13, 27);
            label30.TabIndex = 116;
            label30.Text = "-";
            label30.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // textBox2
            // 
            textBox2.BackColor = System.Drawing.Color.White;
            textBox2.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            textBox2.Location = new System.Drawing.Point(1526, 872);
            textBox2.Name = "textBox2";
            textBox2.Size = new System.Drawing.Size(117, 27);
            textBox2.TabIndex = 114;
            textBox2.TextAlign = HorizontalAlignment.Center;
            // 
            // label32
            // 
            label32.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label32.Location = new System.Drawing.Point(1532, 623);
            label32.Name = "label32";
            label32.Size = new System.Drawing.Size(117, 27);
            label32.TabIndex = 113;
            label32.Text = "Wartość Tuszki bez przerobu";
            label32.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // textBox3
            // 
            textBox3.BackColor = System.Drawing.Color.Gainsboro;
            textBox3.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            textBox3.Location = new System.Drawing.Point(1234, 635);
            textBox3.Name = "textBox3";
            textBox3.Size = new System.Drawing.Size(117, 27);
            textBox3.TabIndex = 112;
            textBox3.TextAlign = HorizontalAlignment.Center;
            // 
            // label33
            // 
            label33.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label33.Location = new System.Drawing.Point(1253, 759);
            label33.Name = "label33";
            label33.Size = new System.Drawing.Size(117, 27);
            label33.TabIndex = 111;
            label33.Text = "Wartość Elementów po krojeniu";
            label33.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // textBox4
            // 
            textBox4.BackColor = System.Drawing.Color.FromArgb(128, 64, 0);
            textBox4.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            textBox4.Location = new System.Drawing.Point(1280, 864);
            textBox4.Name = "textBox4";
            textBox4.Size = new System.Drawing.Size(117, 27);
            textBox4.TabIndex = 110;
            textBox4.TextAlign = HorizontalAlignment.Center;
            // 
            // textBox5
            // 
            textBox5.BackColor = System.Drawing.Color.IndianRed;
            textBox5.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            textBox5.Location = new System.Drawing.Point(1513, 619);
            textBox5.Name = "textBox5";
            textBox5.Size = new System.Drawing.Size(117, 27);
            textBox5.TabIndex = 118;
            textBox5.TextAlign = HorizontalAlignment.Center;
            // 
            // pictureBox3
            // 
            pictureBox3.Image = (System.Drawing.Image)resources.GetObject("pictureBox3.Image");
            pictureBox3.Location = new System.Drawing.Point(11, 775);
            pictureBox3.Name = "pictureBox3";
            pictureBox3.Size = new System.Drawing.Size(136, 61);
            pictureBox3.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox3.TabIndex = 136;
            pictureBox3.TabStop = false;
            // 
            // textBox6
            // 
            textBox6.BackColor = System.Drawing.Color.IndianRed;
            textBox6.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            textBox6.Location = new System.Drawing.Point(1291, 402);
            textBox6.Multiline = true;
            textBox6.Name = "textBox6";
            textBox6.Size = new System.Drawing.Size(206, 61);
            textBox6.TabIndex = 137;
            textBox6.TextAlign = HorizontalAlignment.Center;
            textBox6.TextChanged += textBox6_TextChanged;
            // 
            // label31
            // 
            label31.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label31.Location = new System.Drawing.Point(1337, 77);
            label31.Name = "label31";
            label31.Size = new System.Drawing.Size(130, 60);
            label31.TabIndex = 138;
            label31.Text = "Elementy po krojeniu";
            label31.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // pictureBox1
            // 
            pictureBox1.Image = (System.Drawing.Image)resources.GetObject("pictureBox1.Image");
            pictureBox1.Location = new System.Drawing.Point(11, 505);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new System.Drawing.Size(136, 61);
            pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox1.TabIndex = 139;
            pictureBox1.TabStop = false;
            // 
            // pictureBox2
            // 
            pictureBox2.Image = (System.Drawing.Image)resources.GetObject("pictureBox2.Image");
            pictureBox2.Location = new System.Drawing.Point(10, 638);
            pictureBox2.Name = "pictureBox2";
            pictureBox2.Size = new System.Drawing.Size(136, 61);
            pictureBox2.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox2.TabIndex = 140;
            pictureBox2.TabStop = false;
            // 
            // pictureBox4
            // 
            pictureBox4.Image = (System.Drawing.Image)resources.GetObject("pictureBox4.Image");
            pictureBox4.Location = new System.Drawing.Point(11, 705);
            pictureBox4.Name = "pictureBox4";
            pictureBox4.Size = new System.Drawing.Size(136, 61);
            pictureBox4.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox4.TabIndex = 141;
            pictureBox4.TabStop = false;
            // 
            // label34
            // 
            label34.Font = new System.Drawing.Font("Segoe UI Light", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label34.Location = new System.Drawing.Point(482, 507);
            label34.Name = "label34";
            label34.Size = new System.Drawing.Size(130, 60);
            label34.TabIndex = 144;
            label34.Text = "Za rozważanie 15 kg towaru";
            label34.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label35
            // 
            label35.Font = new System.Drawing.Font("Segoe UI Light", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label35.Location = new System.Drawing.Point(482, 574);
            label35.Name = "label35";
            label35.Size = new System.Drawing.Size(130, 60);
            label35.TabIndex = 146;
            label35.Text = "Za folie za każde 10 kg";
            label35.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label36
            // 
            label36.Font = new System.Drawing.Font("Segoe UI Light", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label36.Location = new System.Drawing.Point(482, 708);
            label36.Name = "label36";
            label36.Size = new System.Drawing.Size(130, 60);
            label36.TabIndex = 148;
            label36.Text = "Paleta drewniana za każde 750 kg towaru";
            label36.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label37
            // 
            label37.Font = new System.Drawing.Font("Segoe UI Light", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label37.Location = new System.Drawing.Point(482, 641);
            label37.Name = "label37";
            label37.Size = new System.Drawing.Size(130, 60);
            label37.TabIndex = 148;
            label37.Text = "Za Sktrech za każde 3000 kg";
            label37.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label38
            // 
            label38.Font = new System.Drawing.Font("Segoe UI Light", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label38.Location = new System.Drawing.Point(482, 777);
            label38.Name = "label38";
            label38.Size = new System.Drawing.Size(130, 60);
            label38.TabIndex = 151;
            label38.Text = "Karton za każde 10 kg towaru";
            label38.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // pictureBox6
            // 
            pictureBox6.Image = (System.Drawing.Image)resources.GetObject("pictureBox6.Image");
            pictureBox6.Location = new System.Drawing.Point(11, 571);
            pictureBox6.Name = "pictureBox6";
            pictureBox6.Size = new System.Drawing.Size(136, 61);
            pictureBox6.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox6.TabIndex = 149;
            pictureBox6.TabStop = false;
            // 
            // label39
            // 
            label39.Font = new System.Drawing.Font("Segoe UI Light", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label39.Location = new System.Drawing.Point(482, 844);
            label39.Name = "label39";
            label39.Size = new System.Drawing.Size(130, 60);
            label39.TabIndex = 154;
            label39.Text = "Energia za każde 1 kg towaru";
            label39.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // PradMroznia
            // 
            PradMroznia.BackColor = System.Drawing.Color.Cyan;
            PradMroznia.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            PradMroznia.Location = new System.Drawing.Point(153, 841);
            PradMroznia.Multiline = true;
            PradMroznia.Name = "PradMroznia";
            PradMroznia.Size = new System.Drawing.Size(206, 61);
            PradMroznia.TabIndex = 153;
            PradMroznia.TextAlign = HorizontalAlignment.Center;
            PradMroznia.TextChanged += PradMroznia_TextChanged;
            // 
            // pictureBox7
            // 
            pictureBox7.Image = (System.Drawing.Image)resources.GetObject("pictureBox7.Image");
            pictureBox7.Location = new System.Drawing.Point(11, 841);
            pictureBox7.Name = "pictureBox7";
            pictureBox7.Size = new System.Drawing.Size(136, 61);
            pictureBox7.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox7.TabIndex = 152;
            pictureBox7.TabStop = false;
            // 
            // SumaKosztowMrozenia
            // 
            SumaKosztowMrozenia.BackColor = System.Drawing.Color.Cyan;
            SumaKosztowMrozenia.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            SumaKosztowMrozenia.Location = new System.Drawing.Point(153, 909);
            SumaKosztowMrozenia.Multiline = true;
            SumaKosztowMrozenia.Name = "SumaKosztowMrozenia";
            SumaKosztowMrozenia.Size = new System.Drawing.Size(206, 61);
            SumaKosztowMrozenia.TabIndex = 156;
            SumaKosztowMrozenia.TextAlign = HorizontalAlignment.Center;
            SumaKosztowMrozenia.TextChanged += SumaKosztowMrozenia_TextChanged;
            // 
            // label40
            // 
            label40.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label40.Location = new System.Drawing.Point(365, 909);
            label40.Name = "label40";
            label40.Size = new System.Drawing.Size(130, 60);
            label40.TabIndex = 157;
            label40.Text = "Koszt Mrożenia";
            label40.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label41
            // 
            label41.Font = new System.Drawing.Font("Segoe UI Light", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label41.Location = new System.Drawing.Point(482, 439);
            label41.Name = "label41";
            label41.Size = new System.Drawing.Size(130, 60);
            label41.TabIndex = 160;
            label41.Text = "Iloś kilogramów do zagospodarowania an mroźnie";
            label41.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // KilogramyDoZamrozenia
            // 
            KilogramyDoZamrozenia.BackColor = System.Drawing.Color.LightGreen;
            KilogramyDoZamrozenia.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            KilogramyDoZamrozenia.Location = new System.Drawing.Point(152, 436);
            KilogramyDoZamrozenia.Multiline = true;
            KilogramyDoZamrozenia.Name = "KilogramyDoZamrozenia";
            KilogramyDoZamrozenia.Size = new System.Drawing.Size(206, 61);
            KilogramyDoZamrozenia.TabIndex = 159;
            KilogramyDoZamrozenia.TextAlign = HorizontalAlignment.Center;
            KilogramyDoZamrozenia.TextChanged += KilogramyDoZamrozenia_TextChanged;
            // 
            // SktrechNaPaleteCena
            // 
            SktrechNaPaleteCena.BackColor = System.Drawing.Color.Cyan;
            SktrechNaPaleteCena.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            SktrechNaPaleteCena.Location = new System.Drawing.Point(365, 640);
            SktrechNaPaleteCena.Multiline = true;
            SktrechNaPaleteCena.Name = "SktrechNaPaleteCena";
            SktrechNaPaleteCena.Size = new System.Drawing.Size(111, 61);
            SktrechNaPaleteCena.TabIndex = 162;
            SktrechNaPaleteCena.TextAlign = HorizontalAlignment.Center;
            SktrechNaPaleteCena.TextChanged += SktrechNaPaleteCena_TextChanged;
            // 
            // FoliaPojemnikCena
            // 
            FoliaPojemnikCena.BackColor = System.Drawing.Color.Cyan;
            FoliaPojemnikCena.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            FoliaPojemnikCena.Location = new System.Drawing.Point(365, 571);
            FoliaPojemnikCena.Multiline = true;
            FoliaPojemnikCena.Name = "FoliaPojemnikCena";
            FoliaPojemnikCena.Size = new System.Drawing.Size(111, 61);
            FoliaPojemnikCena.TabIndex = 163;
            FoliaPojemnikCena.TextAlign = HorizontalAlignment.Center;
            FoliaPojemnikCena.TextChanged += FoliaPojemnikCena_TextChanged;
            // 
            // PaletaDrewnianaCena
            // 
            PaletaDrewnianaCena.BackColor = System.Drawing.Color.Cyan;
            PaletaDrewnianaCena.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            PaletaDrewnianaCena.Location = new System.Drawing.Point(365, 705);
            PaletaDrewnianaCena.Multiline = true;
            PaletaDrewnianaCena.Name = "PaletaDrewnianaCena";
            PaletaDrewnianaCena.Size = new System.Drawing.Size(111, 61);
            PaletaDrewnianaCena.TabIndex = 164;
            PaletaDrewnianaCena.TextAlign = HorizontalAlignment.Center;
            PaletaDrewnianaCena.TextChanged += PaletaDrewnianaCena_TextChanged;
            // 
            // KartonCena
            // 
            KartonCena.BackColor = System.Drawing.Color.Cyan;
            KartonCena.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            KartonCena.Location = new System.Drawing.Point(365, 774);
            KartonCena.Multiline = true;
            KartonCena.Name = "KartonCena";
            KartonCena.Size = new System.Drawing.Size(111, 61);
            KartonCena.TabIndex = 165;
            KartonCena.TextAlign = HorizontalAlignment.Center;
            KartonCena.TextChanged += KartonCena_TextChanged;
            // 
            // PradMrozniaCena
            // 
            PradMrozniaCena.BackColor = System.Drawing.Color.Cyan;
            PradMrozniaCena.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            PradMrozniaCena.Location = new System.Drawing.Point(365, 841);
            PradMrozniaCena.Multiline = true;
            PradMrozniaCena.Name = "PradMrozniaCena";
            PradMrozniaCena.Size = new System.Drawing.Size(111, 61);
            PradMrozniaCena.TabIndex = 166;
            PradMrozniaCena.TextAlign = HorizontalAlignment.Center;
            PradMrozniaCena.TextChanged += PradMrozniaCena_TextChanged;
            // 
            // label42
            // 
            label42.Location = new System.Drawing.Point(365, 433);
            label42.Name = "label42";
            label42.Size = new System.Drawing.Size(111, 64);
            label42.TabIndex = 167;
            label42.Text = "Ceny za poszczególne elementy kosztów mroźni";
            label42.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // RozwazanieTowaruCena
            // 
            RozwazanieTowaruCena.BackColor = System.Drawing.Color.Cyan;
            RozwazanieTowaruCena.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            RozwazanieTowaruCena.Location = new System.Drawing.Point(365, 505);
            RozwazanieTowaruCena.Multiline = true;
            RozwazanieTowaruCena.Name = "RozwazanieTowaruCena";
            RozwazanieTowaruCena.Size = new System.Drawing.Size(111, 61);
            RozwazanieTowaruCena.TabIndex = 168;
            RozwazanieTowaruCena.TextAlign = HorizontalAlignment.Center;
            RozwazanieTowaruCena.TextChanged += RozwazanieTowaruCena_TextChanged;
            // 
            // RozwazanieTowaru
            // 
            RozwazanieTowaru.BackColor = System.Drawing.Color.Cyan;
            RozwazanieTowaru.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            RozwazanieTowaru.Location = new System.Drawing.Point(153, 505);
            RozwazanieTowaru.Multiline = true;
            RozwazanieTowaru.Name = "RozwazanieTowaru";
            RozwazanieTowaru.Size = new System.Drawing.Size(206, 61);
            RozwazanieTowaru.TabIndex = 169;
            RozwazanieTowaru.TextAlign = HorizontalAlignment.Center;
            RozwazanieTowaru.TextChanged += RozwazanieTowaru_TextChanged;
            // 
            // FoliaPojemnik
            // 
            FoliaPojemnik.BackColor = System.Drawing.Color.Cyan;
            FoliaPojemnik.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            FoliaPojemnik.Location = new System.Drawing.Point(153, 574);
            FoliaPojemnik.Multiline = true;
            FoliaPojemnik.Name = "FoliaPojemnik";
            FoliaPojemnik.Size = new System.Drawing.Size(206, 61);
            FoliaPojemnik.TabIndex = 170;
            FoliaPojemnik.TextAlign = HorizontalAlignment.Center;
            FoliaPojemnik.TextChanged += FoliaPojemnik_TextChanged;
            // 
            // SktrechNaPalete
            // 
            SktrechNaPalete.BackColor = System.Drawing.Color.Cyan;
            SktrechNaPalete.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            SktrechNaPalete.Location = new System.Drawing.Point(153, 639);
            SktrechNaPalete.Multiline = true;
            SktrechNaPalete.Name = "SktrechNaPalete";
            SktrechNaPalete.Size = new System.Drawing.Size(206, 61);
            SktrechNaPalete.TabIndex = 171;
            SktrechNaPalete.TextAlign = HorizontalAlignment.Center;
            SktrechNaPalete.TextChanged += SktrechNaPalete_TextChanged;
            // 
            // PaletaDrewniana
            // 
            PaletaDrewniana.BackColor = System.Drawing.Color.Cyan;
            PaletaDrewniana.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            PaletaDrewniana.Location = new System.Drawing.Point(153, 705);
            PaletaDrewniana.Multiline = true;
            PaletaDrewniana.Name = "PaletaDrewniana";
            PaletaDrewniana.Size = new System.Drawing.Size(206, 61);
            PaletaDrewniana.TabIndex = 172;
            PaletaDrewniana.TextAlign = HorizontalAlignment.Center;
            PaletaDrewniana.TextChanged += PaletaDrewniana_TextChanged;
            // 
            // Karton
            // 
            Karton.BackColor = System.Drawing.Color.Cyan;
            Karton.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            Karton.Location = new System.Drawing.Point(153, 774);
            Karton.Multiline = true;
            Karton.Name = "Karton";
            Karton.Size = new System.Drawing.Size(206, 61);
            Karton.TabIndex = 173;
            Karton.TextAlign = HorizontalAlignment.Center;
            Karton.TextChanged += Karton_TextChanged;
            // 
            // pictureBox8
            // 
            pictureBox8.Image = (System.Drawing.Image)resources.GetObject("pictureBox8.Image");
            pictureBox8.Location = new System.Drawing.Point(1605, 477);
            pictureBox8.Name = "pictureBox8";
            pictureBox8.Size = new System.Drawing.Size(103, 57);
            pictureBox8.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox8.TabIndex = 174;
            pictureBox8.TabStop = false;
            // 
            // pictureBox10
            // 
            pictureBox10.Image = (System.Drawing.Image)resources.GetObject("pictureBox10.Image");
            pictureBox10.Location = new System.Drawing.Point(12, 276);
            pictureBox10.Name = "pictureBox10";
            pictureBox10.Size = new System.Drawing.Size(137, 75);
            pictureBox10.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox10.TabIndex = 176;
            pictureBox10.TabStop = false;
            // 
            // pictureBox11
            // 
            pictureBox11.Image = (System.Drawing.Image)resources.GetObject("pictureBox11.Image");
            pictureBox11.Location = new System.Drawing.Point(11, 357);
            pictureBox11.Name = "pictureBox11";
            pictureBox11.Size = new System.Drawing.Size(135, 45);
            pictureBox11.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox11.TabIndex = 178;
            pictureBox11.TabStop = false;
            // 
            // label24
            // 
            label24.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label24.Location = new System.Drawing.Point(10, 405);
            label24.Name = "label24";
            label24.Size = new System.Drawing.Size(348, 28);
            label24.TabIndex = 180;
            label24.Text = "Obliczanie kosztu mrożenia";
            label24.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label43
            // 
            label43.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            label43.Location = new System.Drawing.Point(11, 242);
            label43.Name = "label43";
            label43.Size = new System.Drawing.Size(348, 34);
            label43.TabIndex = 181;
            label43.Text = "Obliczanie kosztu krojenia";
            label43.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label44
            // 
            label44.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label44.Location = new System.Drawing.Point(366, 357);
            label44.Name = "label44";
            label44.Size = new System.Drawing.Size(130, 45);
            label44.TabIndex = 182;
            label44.Text = "Koszt Krojenia";
            label44.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // textBox7
            // 
            textBox7.BackColor = System.Drawing.Color.Gainsboro;
            textBox7.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            textBox7.Location = new System.Drawing.Point(1412, 306);
            textBox7.Multiline = true;
            textBox7.Name = "textBox7";
            textBox7.Size = new System.Drawing.Size(117, 27);
            textBox7.TabIndex = 183;
            textBox7.TextAlign = HorizontalAlignment.Center;
            // 
            // pictureBox9
            // 
            pictureBox9.Image = (System.Drawing.Image)resources.GetObject("pictureBox9.Image");
            pictureBox9.Location = new System.Drawing.Point(624, 49);
            pictureBox9.Name = "pictureBox9";
            pictureBox9.Size = new System.Drawing.Size(138, 61);
            pictureBox9.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox9.TabIndex = 184;
            pictureBox9.TabStop = false;
            // 
            // pictureBox5
            // 
            pictureBox5.Image = (System.Drawing.Image)resources.GetObject("pictureBox5.Image");
            pictureBox5.Location = new System.Drawing.Point(624, 254);
            pictureBox5.Name = "pictureBox5";
            pictureBox5.Size = new System.Drawing.Size(137, 63);
            pictureBox5.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox5.TabIndex = 186;
            pictureBox5.TabStop = false;
            // 
            // WartoscKrojenia2
            // 
            WartoscKrojenia2.BackColor = System.Drawing.SystemColors.ButtonFace;
            WartoscKrojenia2.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            WartoscKrojenia2.ForeColor = System.Drawing.Color.Red;
            WartoscKrojenia2.Location = new System.Drawing.Point(767, 254);
            WartoscKrojenia2.Multiline = true;
            WartoscKrojenia2.Name = "WartoscKrojenia2";
            WartoscKrojenia2.Size = new System.Drawing.Size(207, 63);
            WartoscKrojenia2.TabIndex = 187;
            WartoscKrojenia2.TextAlign = HorizontalAlignment.Center;
            // 
            // pictureBox13
            // 
            pictureBox13.Image = (System.Drawing.Image)resources.GetObject("pictureBox13.Image");
            pictureBox13.Location = new System.Drawing.Point(624, 461);
            pictureBox13.Name = "pictureBox13";
            pictureBox13.Size = new System.Drawing.Size(138, 63);
            pictureBox13.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox13.TabIndex = 189;
            pictureBox13.TabStop = false;
            // 
            // WartoscElementowZamrozonych
            // 
            WartoscElementowZamrozonych.BackColor = System.Drawing.SystemColors.ButtonFace;
            WartoscElementowZamrozonych.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            WartoscElementowZamrozonych.Location = new System.Drawing.Point(768, 461);
            WartoscElementowZamrozonych.Multiline = true;
            WartoscElementowZamrozonych.Name = "WartoscElementowZamrozonych";
            WartoscElementowZamrozonych.Size = new System.Drawing.Size(207, 63);
            WartoscElementowZamrozonych.TabIndex = 188;
            WartoscElementowZamrozonych.TextAlign = HorizontalAlignment.Center;
            // 
            // pictureBox14
            // 
            pictureBox14.Image = (System.Drawing.Image)resources.GetObject("pictureBox14.Image");
            pictureBox14.Location = new System.Drawing.Point(623, 392);
            pictureBox14.Name = "pictureBox14";
            pictureBox14.Size = new System.Drawing.Size(138, 63);
            pictureBox14.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox14.TabIndex = 191;
            pictureBox14.TabStop = false;
            // 
            // SumaKosztowMrozenia2
            // 
            SumaKosztowMrozenia2.BackColor = System.Drawing.SystemColors.ButtonFace;
            SumaKosztowMrozenia2.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            SumaKosztowMrozenia2.ForeColor = System.Drawing.Color.Red;
            SumaKosztowMrozenia2.Location = new System.Drawing.Point(767, 392);
            SumaKosztowMrozenia2.Multiline = true;
            SumaKosztowMrozenia2.Name = "SumaKosztowMrozenia2";
            SumaKosztowMrozenia2.Size = new System.Drawing.Size(207, 63);
            SumaKosztowMrozenia2.TabIndex = 190;
            SumaKosztowMrozenia2.TextAlign = HorizontalAlignment.Center;
            // 
            // label45
            // 
            label45.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label45.ForeColor = System.Drawing.Color.Red;
            label45.Location = new System.Drawing.Point(980, 392);
            label45.Name = "label45";
            label45.Size = new System.Drawing.Size(215, 63);
            label45.TabIndex = 192;
            label45.Text = "Koszt Mrożenia";
            label45.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label46
            // 
            label46.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label46.ForeColor = System.Drawing.Color.Red;
            label46.Location = new System.Drawing.Point(980, 254);
            label46.Name = "label46";
            label46.Size = new System.Drawing.Size(215, 63);
            label46.TabIndex = 193;
            label46.Text = "Koszt Krojenia";
            label46.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // pictureBox15
            // 
            pictureBox15.Image = (System.Drawing.Image)resources.GetObject("pictureBox15.Image");
            pictureBox15.Location = new System.Drawing.Point(11, 434);
            pictureBox15.Name = "pictureBox15";
            pictureBox15.Size = new System.Drawing.Size(138, 63);
            pictureBox15.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox15.TabIndex = 194;
            pictureBox15.TabStop = false;
            // 
            // pictureBox12
            // 
            pictureBox12.Image = (System.Drawing.Image)resources.GetObject("pictureBox12.Image");
            pictureBox12.Location = new System.Drawing.Point(10, 909);
            pictureBox12.Name = "pictureBox12";
            pictureBox12.Size = new System.Drawing.Size(137, 60);
            pictureBox12.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox12.TabIndex = 179;
            pictureBox12.TabStop = false;
            // 
            // label47
            // 
            label47.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label47.Location = new System.Drawing.Point(980, 323);
            label47.Name = "label47";
            label47.Size = new System.Drawing.Size(215, 63);
            label47.TabIndex = 195;
            label47.Text = "Wartość Elementów";
            label47.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label48
            // 
            label48.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label48.Location = new System.Drawing.Point(981, 49);
            label48.Name = "label48";
            label48.Size = new System.Drawing.Size(214, 61);
            label48.TabIndex = 196;
            label48.Text = "Wartość Tuszki";
            label48.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label49
            // 
            label49.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label49.Location = new System.Drawing.Point(981, 461);
            label49.Name = "label49";
            label49.Size = new System.Drawing.Size(214, 63);
            label49.TabIndex = 197;
            label49.Text = "Wartość Elementów Mrożonych";
            label49.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label50
            // 
            label50.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label50.ForeColor = System.Drawing.Color.Red;
            label50.Location = new System.Drawing.Point(980, 530);
            label50.Name = "label50";
            label50.Size = new System.Drawing.Size(215, 63);
            label50.TabIndex = 200;
            label50.Text = "Koszt zaniżenia ceny";
            label50.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // pictureBox16
            // 
            pictureBox16.Image = (System.Drawing.Image)resources.GetObject("pictureBox16.Image");
            pictureBox16.Location = new System.Drawing.Point(623, 530);
            pictureBox16.Name = "pictureBox16";
            pictureBox16.Size = new System.Drawing.Size(138, 63);
            pictureBox16.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox16.TabIndex = 199;
            pictureBox16.TabStop = false;
            // 
            // KosztZanizeniaCeny
            // 
            KosztZanizeniaCeny.BackColor = System.Drawing.SystemColors.ButtonFace;
            KosztZanizeniaCeny.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            KosztZanizeniaCeny.ForeColor = System.Drawing.Color.Red;
            KosztZanizeniaCeny.Location = new System.Drawing.Point(767, 530);
            KosztZanizeniaCeny.Multiline = true;
            KosztZanizeniaCeny.Name = "KosztZanizeniaCeny";
            KosztZanizeniaCeny.Size = new System.Drawing.Size(207, 63);
            KosztZanizeniaCeny.TabIndex = 198;
            KosztZanizeniaCeny.TextAlign = HorizontalAlignment.Center;
            // 
            // label51
            // 
            label51.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label51.Location = new System.Drawing.Point(980, 599);
            label51.Name = "label51";
            label51.Size = new System.Drawing.Size(215, 63);
            label51.TabIndex = 203;
            label51.Text = "Wartość Elementów Mrożonych po zniżce";
            label51.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // pictureBox17
            // 
            pictureBox17.Image = (System.Drawing.Image)resources.GetObject("pictureBox17.Image");
            pictureBox17.Location = new System.Drawing.Point(623, 599);
            pictureBox17.Name = "pictureBox17";
            pictureBox17.Size = new System.Drawing.Size(138, 63);
            pictureBox17.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox17.TabIndex = 202;
            pictureBox17.TabStop = false;
            // 
            // WartoscElementowMrozonychPoObnizce
            // 
            WartoscElementowMrozonychPoObnizce.BackColor = System.Drawing.SystemColors.ButtonFace;
            WartoscElementowMrozonychPoObnizce.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            WartoscElementowMrozonychPoObnizce.Location = new System.Drawing.Point(767, 599);
            WartoscElementowMrozonychPoObnizce.Multiline = true;
            WartoscElementowMrozonychPoObnizce.Name = "WartoscElementowMrozonychPoObnizce";
            WartoscElementowMrozonychPoObnizce.Size = new System.Drawing.Size(207, 63);
            WartoscElementowMrozonychPoObnizce.TabIndex = 201;
            WartoscElementowMrozonychPoObnizce.TextAlign = HorizontalAlignment.Center;
            // 
            // label52
            // 
            label52.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label52.Location = new System.Drawing.Point(980, 694);
            label52.Name = "label52";
            label52.Size = new System.Drawing.Size(215, 61);
            label52.TabIndex = 206;
            label52.Text = "Wartość Tuszki";
            label52.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // pictureBox18
            // 
            pictureBox18.Image = (System.Drawing.Image)resources.GetObject("pictureBox18.Image");
            pictureBox18.Location = new System.Drawing.Point(623, 694);
            pictureBox18.Name = "pictureBox18";
            pictureBox18.Size = new System.Drawing.Size(138, 61);
            pictureBox18.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox18.TabIndex = 205;
            pictureBox18.TabStop = false;
            // 
            // TuszkaWartosc3
            // 
            TuszkaWartosc3.BackColor = System.Drawing.Color.LightGreen;
            TuszkaWartosc3.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            TuszkaWartosc3.Location = new System.Drawing.Point(767, 694);
            TuszkaWartosc3.Multiline = true;
            TuszkaWartosc3.Name = "TuszkaWartosc3";
            TuszkaWartosc3.Size = new System.Drawing.Size(207, 61);
            TuszkaWartosc3.TabIndex = 204;
            TuszkaWartosc3.TextAlign = HorizontalAlignment.Center;
            // 
            // label53
            // 
            label53.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label53.Location = new System.Drawing.Point(980, 759);
            label53.Name = "label53";
            label53.Size = new System.Drawing.Size(215, 63);
            label53.TabIndex = 209;
            label53.Text = "Wartość Elementów Mrożonych po zniżce";
            label53.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // pictureBox19
            // 
            pictureBox19.Image = (System.Drawing.Image)resources.GetObject("pictureBox19.Image");
            pictureBox19.Location = new System.Drawing.Point(623, 759);
            pictureBox19.Name = "pictureBox19";
            pictureBox19.Size = new System.Drawing.Size(138, 63);
            pictureBox19.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox19.TabIndex = 208;
            pictureBox19.TabStop = false;
            // 
            // WartoscElementowMrozonychPoObnizce2
            // 
            WartoscElementowMrozonychPoObnizce2.BackColor = System.Drawing.SystemColors.ActiveCaption;
            WartoscElementowMrozonychPoObnizce2.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            WartoscElementowMrozonychPoObnizce2.Location = new System.Drawing.Point(767, 759);
            WartoscElementowMrozonychPoObnizce2.Multiline = true;
            WartoscElementowMrozonychPoObnizce2.Name = "WartoscElementowMrozonychPoObnizce2";
            WartoscElementowMrozonychPoObnizce2.Size = new System.Drawing.Size(207, 63);
            WartoscElementowMrozonychPoObnizce2.TabIndex = 207;
            WartoscElementowMrozonychPoObnizce2.TextAlign = HorizontalAlignment.Center;
            // 
            // label54
            // 
            label54.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label54.Location = new System.Drawing.Point(980, 828);
            label54.Name = "label54";
            label54.Size = new System.Drawing.Size(215, 63);
            label54.TabIndex = 212;
            label54.Text = "Strata";
            label54.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // Strata
            // 
            Strata.BackColor = System.Drawing.SystemColors.ButtonFace;
            Strata.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            Strata.ForeColor = System.Drawing.Color.Red;
            Strata.Location = new System.Drawing.Point(767, 828);
            Strata.Multiline = true;
            Strata.Name = "Strata";
            Strata.Size = new System.Drawing.Size(207, 63);
            Strata.TabIndex = 210;
            Strata.TextAlign = HorizontalAlignment.Center;
            // 
            // pictureBox20
            // 
            pictureBox20.Image = (System.Drawing.Image)resources.GetObject("pictureBox20.Image");
            pictureBox20.Location = new System.Drawing.Point(623, 828);
            pictureBox20.Name = "pictureBox20";
            pictureBox20.Size = new System.Drawing.Size(138, 63);
            pictureBox20.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox20.TabIndex = 213;
            pictureBox20.TabStop = false;
            // 
            // pictureBox29
            // 
            pictureBox29.Image = (System.Drawing.Image)resources.GetObject("pictureBox29.Image");
            pictureBox29.Location = new System.Drawing.Point(623, 323);
            pictureBox29.Name = "pictureBox29";
            pictureBox29.Size = new System.Drawing.Size(138, 63);
            pictureBox29.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox29.TabIndex = 185;
            pictureBox29.TabStop = false;
            // 
            // pictureBox21
            // 
            pictureBox21.Image = (System.Drawing.Image)resources.GetObject("pictureBox21.Image");
            pictureBox21.Location = new System.Drawing.Point(624, 116);
            pictureBox21.Name = "pictureBox21";
            pictureBox21.Size = new System.Drawing.Size(138, 63);
            pictureBox21.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox21.TabIndex = 214;
            pictureBox21.TabStop = false;
            // 
            // label55
            // 
            label55.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label55.Location = new System.Drawing.Point(981, 116);
            label55.Name = "label55";
            label55.Size = new System.Drawing.Size(214, 61);
            label55.TabIndex = 215;
            label55.Text = "Zysk na elementach nie wliczając kosztów krojenia";
            label55.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label56
            // 
            label56.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label56.Location = new System.Drawing.Point(981, 185);
            label56.Name = "label56";
            label56.Size = new System.Drawing.Size(214, 61);
            label56.TabIndex = 218;
            label56.Text = "Wartość Elementów";
            label56.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // pictureBox22
            // 
            pictureBox22.Image = (System.Drawing.Image)resources.GetObject("pictureBox22.Image");
            pictureBox22.Location = new System.Drawing.Point(624, 185);
            pictureBox22.Name = "pictureBox22";
            pictureBox22.Size = new System.Drawing.Size(138, 63);
            pictureBox22.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox22.TabIndex = 217;
            pictureBox22.TabStop = false;
            // 
            // RoznicaTuszkaElement3
            // 
            RoznicaTuszkaElement3.BackColor = System.Drawing.SystemColors.ButtonFace;
            RoznicaTuszkaElement3.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            RoznicaTuszkaElement3.ForeColor = System.Drawing.Color.FromArgb(128, 255, 128);
            RoznicaTuszkaElement3.Location = new System.Drawing.Point(767, 116);
            RoznicaTuszkaElement3.Multiline = true;
            RoznicaTuszkaElement3.Name = "RoznicaTuszkaElement3";
            RoznicaTuszkaElement3.Size = new System.Drawing.Size(207, 63);
            RoznicaTuszkaElement3.TabIndex = 216;
            RoznicaTuszkaElement3.TextAlign = HorizontalAlignment.Center;
            // 
            // PokazKrojenieMrozenie
            // 
            ClientSize = new System.Drawing.Size(1434, 993);
            Controls.Add(label56);
            Controls.Add(pictureBox22);
            Controls.Add(RoznicaTuszkaElement3);
            Controls.Add(label55);
            Controls.Add(pictureBox21);
            Controls.Add(pictureBox20);
            Controls.Add(label54);
            Controls.Add(Strata);
            Controls.Add(label53);
            Controls.Add(pictureBox19);
            Controls.Add(WartoscElementowMrozonychPoObnizce2);
            Controls.Add(label52);
            Controls.Add(pictureBox18);
            Controls.Add(TuszkaWartosc3);
            Controls.Add(label51);
            Controls.Add(pictureBox17);
            Controls.Add(WartoscElementowMrozonychPoObnizce);
            Controls.Add(label50);
            Controls.Add(pictureBox16);
            Controls.Add(KosztZanizeniaCeny);
            Controls.Add(label49);
            Controls.Add(label48);
            Controls.Add(label47);
            Controls.Add(pictureBox15);
            Controls.Add(label46);
            Controls.Add(label45);
            Controls.Add(pictureBox14);
            Controls.Add(SumaKosztowMrozenia2);
            Controls.Add(pictureBox13);
            Controls.Add(WartoscElementowZamrozonych);
            Controls.Add(WartoscKrojenia2);
            Controls.Add(pictureBox5);
            Controls.Add(pictureBox29);
            Controls.Add(pictureBox9);
            Controls.Add(textBox7);
            Controls.Add(label44);
            Controls.Add(label43);
            Controls.Add(label24);
            Controls.Add(pictureBox12);
            Controls.Add(pictureBox11);
            Controls.Add(pictureBox10);
            Controls.Add(pictureBox8);
            Controls.Add(Karton);
            Controls.Add(PaletaDrewniana);
            Controls.Add(SktrechNaPalete);
            Controls.Add(FoliaPojemnik);
            Controls.Add(RozwazanieTowaru);
            Controls.Add(RozwazanieTowaruCena);
            Controls.Add(label42);
            Controls.Add(PradMrozniaCena);
            Controls.Add(KartonCena);
            Controls.Add(PaletaDrewnianaCena);
            Controls.Add(FoliaPojemnikCena);
            Controls.Add(SktrechNaPaleteCena);
            Controls.Add(label41);
            Controls.Add(KilogramyDoZamrozenia);
            Controls.Add(label40);
            Controls.Add(SumaKosztowMrozenia);
            Controls.Add(label39);
            Controls.Add(PradMroznia);
            Controls.Add(pictureBox7);
            Controls.Add(label38);
            Controls.Add(pictureBox6);
            Controls.Add(label37);
            Controls.Add(label36);
            Controls.Add(label35);
            Controls.Add(label34);
            Controls.Add(pictureBox4);
            Controls.Add(pictureBox2);
            Controls.Add(pictureBox1);
            Controls.Add(label31);
            Controls.Add(textBox6);
            Controls.Add(pictureBox3);
            Controls.Add(textBox5);
            Controls.Add(label29);
            Controls.Add(label30);
            Controls.Add(textBox2);
            Controls.Add(label32);
            Controls.Add(textBox3);
            Controls.Add(label33);
            Controls.Add(textBox4);
            Controls.Add(label28);
            Controls.Add(label27);
            Controls.Add(label26);
            Controls.Add(RoznicaTuszkaElement2);
            Controls.Add(label25);
            Controls.Add(TuszkaWartosc2);
            Controls.Add(label23);
            Controls.Add(WartoscElementowPoKrojeniu2);
            Controls.Add(label20);
            Controls.Add(WartoscElementowPoKrojeniu);
            Controls.Add(WartoscKrojenia);
            Controls.Add(WspolczynnikKrojenia);
            Controls.Add(label22);
            Controls.Add(TuszkaKG2);
            Controls.Add(label21);
            Controls.Add(SumaWartosc2);
            Controls.Add(RoznicaTuszkaElement);
            Controls.Add(SumaWydajnosc);
            Controls.Add(label19);
            Controls.Add(SumaWartosc);
            Controls.Add(SumaCena);
            Controls.Add(SumaKG);
            Controls.Add(PozostaleWydajnosc);
            Controls.Add(KorpusWydajnosc);
            Controls.Add(SkrzydloWydajnosc);
            Controls.Add(CwiartkaWydajnosc);
            Controls.Add(FiletWydajnosc);
            Controls.Add(label18);
            Controls.Add(TuszkaWydajnosc);
            Controls.Add(label17);
            Controls.Add(PozostaleWartosc);
            Controls.Add(PozostaleCena);
            Controls.Add(PozostaleKG);
            Controls.Add(label16);
            Controls.Add(label15);
            Controls.Add(label14);
            Controls.Add(label13);
            Controls.Add(KorpusWartosc);
            Controls.Add(KorpusCena);
            Controls.Add(KorpusKG);
            Controls.Add(SkrzydloWartosc);
            Controls.Add(SkrzydloCena);
            Controls.Add(SkrzydloKG);
            Controls.Add(CwiartkaWartosc);
            Controls.Add(CwiartkaCena);
            Controls.Add(CwiartkaKG);
            Controls.Add(FiletWartosc);
            Controls.Add(FiletCena);
            Controls.Add(FiletKG);
            Controls.Add(TuszkaWartosc);
            Controls.Add(label9);
            Controls.Add(TuszkaCena);
            Controls.Add(label8);
            Controls.Add(TuszkaKG);
            Controls.Add(label7);
            Controls.Add(buttonZamknij);
            Controls.Add(button1);
            Controls.Add(label6);
            Controls.Add(sumaSztuk);
            Controls.Add(label5);
            Controls.Add(label12);
            Controls.Add(obliczeniaAut);
            Controls.Add(label4);
            Controls.Add(label3);
            Controls.Add(textBox1);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(sztukNaSzuflade);
            Controls.Add(label10);
            Controls.Add(sztuki);
            Controls.Add(label11);
            Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            Name = "PokazKrojenieMrozenie";
            Text = " ";
            Load += PokazKrojenieMrozenie_Load;
            ((System.ComponentModel.ISupportInitialize)pictureBox3).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox2).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox4).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox6).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox7).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox8).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox10).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox11).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox9).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox5).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox13).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox14).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox15).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox12).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox16).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox17).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox18).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox19).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox20).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox29).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox21).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox22).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        // Obsługa wszystkich zmian danych w TextBoxach
        private void ObliczWszystko()
        {
            try
            {
                // Oblicz wydajność elementów na podstawie tuszki
                double tuszkaKG = PobierzWartosc(TuszkaKG.Text);
                double tuszkaCena = PobierzWartosc(TuszkaCena.Text);

                double cwiartkaWydajnosc = PobierzWartosc(CwiartkaWydajnosc.Text) / 100;
                double filetWydajnosc = PobierzWartosc(FiletWydajnosc.Text) / 100;
                double korpusWydajnosc = PobierzWartosc(KorpusWydajnosc.Text) / 100;
                double pozostaleWydajnosc = PobierzWartosc(PozostaleWydajnosc.Text) / 100;
                double skrzydloWydajnosc = PobierzWartosc(SkrzydloWydajnosc.Text) / 100;

                // Obliczenia dla poszczególnych elementów (wydajność i cena)
                double cwiartkaKG = tuszkaKG * cwiartkaWydajnosc;
                double filetKG = tuszkaKG * filetWydajnosc;
                double korpusKG = tuszkaKG * korpusWydajnosc;
                double pozostaleKG = tuszkaKG * pozostaleWydajnosc;
                double skrzydloKG = tuszkaKG * skrzydloWydajnosc;

                double cwiartkaCena = PobierzWartosc(CwiartkaCena.Text);
                double filetCena = PobierzWartosc(FiletCena.Text);
                double korpusCena = PobierzWartosc(KorpusCena.Text);
                double pozostaleCena = PobierzWartosc(PozostaleCena.Text);
                double skrzydloCena = PobierzWartosc(SkrzydloCena.Text);


                // Obliczenia wartości każdego elementu
                double cwiartkaWartosc = cwiartkaKG * cwiartkaCena;
                double filetWartosc = filetKG * filetCena;
                double korpusWartosc = korpusKG * korpusCena;
                double pozostaleWartosc = pozostaleKG * pozostaleCena;
                double skrzydloWartosc = skrzydloKG * skrzydloCena;
                double tuszkaWartosc = tuszkaKG * tuszkaCena;

                // Ustawienie wartości i kilogramów w TextBoxach z separatorem tysięcy i 2 miejscami po przecinku
                CwiartkaKG.Text = cwiartkaKG.ToString("N2");
                FiletKG.Text = filetKG.ToString("N2");
                KorpusKG.Text = korpusKG.ToString("N2");
                PozostaleKG.Text = pozostaleKG.ToString("N2");
                SkrzydloKG.Text = skrzydloKG.ToString("N2");
                TuszkaKG2.Text = tuszkaKG.ToString("N2");

                CwiartkaWartosc.Text = cwiartkaWartosc.ToString("N2");
                FiletWartosc.Text = filetWartosc.ToString("N2");
                KorpusWartosc.Text = korpusWartosc.ToString("N2");
                PozostaleWartosc.Text = pozostaleWartosc.ToString("N2");
                SkrzydloWartosc.Text = skrzydloWartosc.ToString("N2");

                // Suma wartości wszystkich elementów
                double sumaWartosciElementow = cwiartkaWartosc + filetWartosc + korpusWartosc + pozostaleWartosc + skrzydloWartosc;

                // Oblicz sumy kilogramów i wartości
                double sumaKg = cwiartkaKG + filetKG + korpusKG + pozostaleKG + skrzydloKG;

                // Ustawienie sumy kilogramów i wartości w TextBoxach z separatorem tysięcy i 2 miejscami po przecinku
                SumaKG.Text = sumaKg.ToString("N2");
                SumaWartosc.Text = sumaWartosciElementow.ToString("N2");
                SumaWartosc2.Text = sumaWartosciElementow.ToString("N2");

                double roznicaTuszkaElement = sumaWartosciElementow - tuszkaWartosc;
                RoznicaTuszkaElement.Text = roznicaTuszkaElement.ToString("N2");

                // Zmienianie koloru czcionki w zależności od wartości
                if (roznicaTuszkaElement > 0)
                {
                    RoznicaTuszkaElement.ForeColor = System.Drawing.Color.Green;  // Zielona czcionka
                }
                else if (roznicaTuszkaElement < 0)
                {
                    RoznicaTuszkaElement.ForeColor = System.Drawing.Color.Red;  // Czerwona czcionka
                }
                else
                {
                    RoznicaTuszkaElement.ForeColor = System.Drawing.Color.Black;  // Czarna czcionka, jeśli wynik to 0
                }


                // Ustawienie wartości tuszki w TextBoxie z separatorem tysięcy i 2 miejscami po przecinku
                TuszkaWartosc.Text = tuszkaWartosc.ToString("N2");
                TuszkaWartosc2.Text = tuszkaWartosc.ToString("N2");
                TuszkaWartosc3.Text = tuszkaWartosc.ToString("N2") + " zł";

                double przelicznikKrojenia = PobierzWartosc(WspolczynnikKrojenia.Text);
                double wynikKrojenia = tuszkaKG / przelicznikKrojenia;

                WartoscKrojenia.Text = wynikKrojenia.ToString("N2");
                WartoscKrojenia2.Text = wynikKrojenia.ToString("N2") + " zł";

                double wartoscElementowPoKrojeniu = sumaWartosciElementow - wynikKrojenia;
                WartoscElementowPoKrojeniu.Text = wartoscElementowPoKrojeniu.ToString("N2");
                WartoscElementowPoKrojeniu2.Text = wartoscElementowPoKrojeniu.ToString("N2");


                double roznicaTuszkaElementPoKrojeniu = wartoscElementowPoKrojeniu - tuszkaWartosc;
                RoznicaTuszkaElement2.Text = roznicaTuszkaElementPoKrojeniu.ToString("N2") + " zł";
                RoznicaTuszkaElement3.Text = roznicaTuszkaElementPoKrojeniu.ToString("N2") + " zł";

                // Zmienianie koloru czcionki w zależności od wartości
                if (roznicaTuszkaElementPoKrojeniu > 0)
                {
                    RoznicaTuszkaElement2.ForeColor = System.Drawing.Color.Green;  // Zielona czcionka
                    RoznicaTuszkaElement3.ForeColor = System.Drawing.Color.Green;  // Zielona czcionka
                }
                else if (roznicaTuszkaElementPoKrojeniu < 0)
                {
                    RoznicaTuszkaElement2.ForeColor = System.Drawing.Color.Red;  // Czerwona czcionka
                    RoznicaTuszkaElement3.ForeColor = System.Drawing.Color.Red;  // Czerwona czcionka
                }
                else
                {
                    RoznicaTuszkaElement2.ForeColor = System.Drawing.Color.Black;  // Czarna czcionka, jeśli wynik to 0
                    RoznicaTuszkaElement3.ForeColor = System.Drawing.Color.Black;  // Czarna czcionka, jeśli wynik to 0
                }





            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd: " + ex.Message, "Błąd konwersji", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Funkcja pomocnicza do pobierania wartości
        private double PobierzWartosc(string tekst)
        {
            // Próba konwersji tekstu na double; jeśli niepoprawne, zwraca 0
            if (double.TryParse(tekst, out double wynik))
            {
                return wynik;
            }
            return 0;
        }


        // Obsługa wszystkich zdarzeń zmiany wartości w TextBoxach
        private void TextBox_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void TuszkaKG_TextChanged_1(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void TuszkaCena_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void SumaCena_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void FiletCena_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void CwiartkaCena_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void SkrzydloCena_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void KorpusCena_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void PozostaleCena_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void TuszkaCena_TextChanged_1(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void FiletKG_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void CwiartkaKG_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void SkrzydloKG_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void WspolczynnikKrojenia_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void FiletWydajnosc_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void CwiartkaWydajnosc_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void SkrzydloWydajnosc_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void KorpusWydajnosc_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void PozostaleWydajnosc_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void PrzetworzDaneCalosc()
        {
            try
            {
                // Zmienna do przechowywania sumy kosztów mrożenia
                double sumaKosztowMrozenia = 0;

                // Przetwarzanie danych i dodawanie wyniku do sumy
                sumaKosztowMrozenia += PrzetworzDaneCeny(KilogramyDoZamrozenia, RozwazanieTowaruCena, RozwazanieTowaru, 15);
                sumaKosztowMrozenia += PrzetworzDaneCeny(KilogramyDoZamrozenia, FoliaPojemnikCena, FoliaPojemnik, 10);
                sumaKosztowMrozenia += PrzetworzDaneCeny(KilogramyDoZamrozenia, SktrechNaPaleteCena, SktrechNaPalete, 3000);
                sumaKosztowMrozenia += PrzetworzDaneCeny(KilogramyDoZamrozenia, KartonCena, Karton, 10);
                sumaKosztowMrozenia += PrzetworzDaneCeny(KilogramyDoZamrozenia, PaletaDrewnianaCena, PaletaDrewniana, 750);
                sumaKosztowMrozenia += PrzetworzDaneCeny(KilogramyDoZamrozenia, PradMrozniaCena, PradMroznia, 1);

                // Wyświetlanie sumy w TextBoxie SumaKosztowMrozenia z zaokrągleniem do 2 miejsc po przecinku i z "zł"
                SumaKosztowMrozenia.Text = sumaKosztowMrozenia.ToString("N2") + " zł";
                SumaKosztowMrozenia2.Text = sumaKosztowMrozenia.ToString("N2") + " zł";

                double Elementy = PobierzWartosc(WartoscElementowPoKrojeniu2.Text);
                double ElementyPoKosztach = Elementy - sumaKosztowMrozenia;
                WartoscElementowZamrozonych.Text = ElementyPoKosztach.ToString("N2") + " zł";

                double obnizenieTowaruMrozonego = ElementyPoKosztach * 0.82;

                double roznicaWoBnizce = ElementyPoKosztach - obnizenieTowaruMrozonego;
                KosztZanizeniaCeny.Text = roznicaWoBnizce.ToString("N2") + " zł";

                WartoscElementowMrozonychPoObnizce.Text = obnizenieTowaruMrozonego.ToString("N2") + " zł";
                WartoscElementowMrozonychPoObnizce2.Text = obnizenieTowaruMrozonego.ToString("N2") + " zł";

                double tuszkaWartosc = PobierzWartosc(TuszkaWartosc.Text);
                double wynikStraty = tuszkaWartosc - obnizenieTowaruMrozonego;

                Strata.Text = wynikStraty.ToString("N2") + " zł";

            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd: " + ex.Message, "Błąd przetwarzania danych", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Metoda przetwarzająca dane i zwracająca wynik, by można było sumować
        private double PrzetworzDaneCeny(TextBox textBox1, TextBox textBox2, TextBox textBox3, int liczbaInt)
        {
            try
            {
                // Pobranie wartości z TextBox1 jako liczba zmiennoprzecinkowa (double)
                double kilogramyDoZamrozenia = PobierzWartosc(textBox1.Text);

                // Podzielenie wartości przez liczbę int
                kilogramyDoZamrozenia /= liczbaInt;

                // Pobranie wartości z TextBox2 jako liczba zmiennoprzecinkowa (double)
                double wartoscDoMnozenia = PobierzWartosc(textBox2.Text);

                // Mnożenie wartości przez kilogramyDoZamrozenia
                double wynik = wartoscDoMnozenia * kilogramyDoZamrozenia;

                // Ustawienie wyniku w TextBox3 z zaokrągleniem do 2 miejsc po przecinku i z "zł"
                textBox3.Text = wynik.ToString("N2") + " zł";

                // Zwrócenie wyniku dla sumowania
                return wynik;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd: " + ex.Message, "Błąd przetwarzania danych", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 0;
            }
        }

        private void RozwazanieTowaru_TextChanged(object sender, EventArgs e)
        {
            PrzetworzDaneCalosc();
        }

        private void RozwazanieTowaruCena_TextChanged(object sender, EventArgs e)
        {
            PrzetworzDaneCalosc();
        }

        private void FoliaPojemnikCena_TextChanged(object sender, EventArgs e)
        {
            PrzetworzDaneCalosc();
        }

        private void FoliaPojemnik_TextChanged(object sender, EventArgs e)
        {
            PrzetworzDaneCalosc();
        }

        private void SktrechNaPalete_TextChanged(object sender, EventArgs e)
        {
            PrzetworzDaneCalosc();
        }

        private void SktrechNaPaleteCena_TextChanged(object sender, EventArgs e)
        {
            PrzetworzDaneCalosc();
        }

        private void PaletaDrewniana_TextChanged(object sender, EventArgs e)
        {
            PrzetworzDaneCalosc();
        }

        private void PaletaDrewnianaCena_TextChanged(object sender, EventArgs e)
        {
            PrzetworzDaneCalosc();
        }

        private void KartonCena_TextChanged(object sender, EventArgs e)
        {
            PrzetworzDaneCalosc();
        }

        private void Karton_TextChanged(object sender, EventArgs e)
        {
            PrzetworzDaneCalosc();
        }

        private void PradMroznia_TextChanged(object sender, EventArgs e)
        {
            PrzetworzDaneCalosc();
        }

        private void PradMrozniaCena_TextChanged(object sender, EventArgs e)
        {
            PrzetworzDaneCalosc();
        }

        private void SumaKosztowMrozenia_TextChanged(object sender, EventArgs e)
        {
            PrzetworzDaneCalosc();
        }

        private void textBox6_TextChanged(object sender, EventArgs e)
        {
            PrzetworzDaneCalosc();
        }

        private void KilogramyDoZamrozenia_TextChanged(object sender, EventArgs e)
        {
            PrzetworzDaneCalosc();
        }

        private void PokazKrojenieMrozenie_Load(object sender, EventArgs e)
        {

        }
    }
}

