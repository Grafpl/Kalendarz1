using System;
using System.Windows.Forms;
using System.Globalization;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using SqlConnection = Microsoft.Data.SqlClient.SqlConnection;
using SqlCommand = Microsoft.Data.SqlClient.SqlCommand;
using SqlDataAdapter = Microsoft.Data.SqlClient.SqlDataAdapter;
using SqlDataReader = Microsoft.Data.SqlClient.SqlDataReader;


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
        private TextBox textBox8;
        private TextBox textBox9;
        private TextBox textBox10;
        private TextBox textBox11;
        private TextBox textBox12;
        private TextBox textBox13;
        private TextBox textBox14;
        private TextBox textBox15;
        private TextBox textBox16;
        private TextBox textBox17;
        private TextBox textBox18;
        private Label label57;
        private TextBox textBox19;
        private PictureBox pictureBox23;
        private Label label58;
        private PictureBox pictureBox24;
        private Label label59;
        private Label label60;
        private Label label61;
        private Label label62;
        private PictureBox pictureBox25;
        private PictureBox pictureBox26;
        private PictureBox pictureBox27;
        private PictureBox pictureBox28;
        private TextBox textBox20;
        private Label label63;
        private TextBox textBox21;
        private PictureBox pictureBox30;
        private TextBox textBox22;
        private Label label64;
        private TextBox textBox23;
        private PictureBox pictureBox31;
        private Label label65;
        private TextBox CenaTuszka1;
        private TextBox CenaElementyKrojenie1;
        private TextBox CenaElementy1;
        private TextBox CenaElementyPoMrozeniu1;
        private TextBox CenaTuszka2;
        private TextBox CenyElementowPoObnizeniuMroz2;
        private TextBox CenaStrataSuma1;
        private TextBox CenyElementowPoObnizeniuMroz1;
        private TextBox Filet2Wydajnosc;
        private Label label66;
        private TextBox Filet2Wartosc;
        private TextBox Filet2Cena;
        private TextBox Filet2KG;
        private TextBox Cwiartka2Wydajnosc;
        private Label label67;
        private TextBox Cwiartka2Wartosc;
        private TextBox Cwiartka2Cena;
        private TextBox Cwiartka2KG;
        private TextBox Skrzydlo2Wydajnosc;
        private Label label68;
        private TextBox Skrzydlo2Wartosc;
        private TextBox Skrzydlo2Cena;
        private TextBox Skrzydlo2KG;
        private TextBox CenaRoznicaTuszkaElementy;
        private TextBox CenaKrojenia;
        private TextBox CenaMrozenia;
        private TextBox RoznicaObnizeniaCeny;
        private TextBox CenaRoznicaTuszkaElementyPokrojone;
        private Label label69;
        private TextBox KosztSprzedazyPokrojonego;
        private PictureBox pictureBox32;
        private PictureBox pictureBox33;
        private Button OdswiezButton;
        private DateTimePicker dateTimePicker2;
        private DateTimePicker dateTimePicker1;
        private Button PokazMroznie;
        private DataGridView dataGridView1;
        private Label label11;

        public PokazKrojenieMrozenie()
        {
            InitializeComponent();

            TuszkaWydajnosc.Text = "100";
            SumaWydajnosc.Text = "100";
            FiletWydajnosc.Text = "29,5";
            Filet2Wydajnosc.Text = "1,9";
            CwiartkaWydajnosc.Text = "33,4";
            Cwiartka2Wydajnosc.Text = "2,0";
            SkrzydloWydajnosc.Text = "8,7";
            Skrzydlo2Wydajnosc.Text = "1,0";
            KorpusWydajnosc.Text = "22,7";
            PozostaleWydajnosc.Text = "0,8";

            WspolczynnikKrojenia.Text = "1,7";
            RozwazanieTowaruCena.Text = "1,5";
            FoliaPojemnikCena.Text = "0,38";
            SktrechNaPaleteCena.Text = "24,00";
            KartonCena.Text = "3";
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
            textBox8 = new TextBox();
            textBox9 = new TextBox();
            textBox10 = new TextBox();
            textBox11 = new TextBox();
            textBox12 = new TextBox();
            textBox13 = new TextBox();
            textBox14 = new TextBox();
            textBox15 = new TextBox();
            textBox16 = new TextBox();
            textBox17 = new TextBox();
            textBox18 = new TextBox();
            label57 = new Label();
            textBox19 = new TextBox();
            pictureBox23 = new PictureBox();
            label58 = new Label();
            pictureBox24 = new PictureBox();
            label59 = new Label();
            label60 = new Label();
            label61 = new Label();
            label62 = new Label();
            pictureBox25 = new PictureBox();
            pictureBox26 = new PictureBox();
            pictureBox27 = new PictureBox();
            pictureBox28 = new PictureBox();
            textBox20 = new TextBox();
            label63 = new Label();
            textBox21 = new TextBox();
            pictureBox30 = new PictureBox();
            textBox22 = new TextBox();
            label64 = new Label();
            textBox23 = new TextBox();
            pictureBox31 = new PictureBox();
            label65 = new Label();
            CenaTuszka1 = new TextBox();
            CenaElementyKrojenie1 = new TextBox();
            CenaElementy1 = new TextBox();
            CenaElementyPoMrozeniu1 = new TextBox();
            CenaTuszka2 = new TextBox();
            CenyElementowPoObnizeniuMroz2 = new TextBox();
            CenaStrataSuma1 = new TextBox();
            CenyElementowPoObnizeniuMroz1 = new TextBox();
            Filet2Wydajnosc = new TextBox();
            label66 = new Label();
            Filet2Wartosc = new TextBox();
            Filet2Cena = new TextBox();
            Filet2KG = new TextBox();
            Cwiartka2Wydajnosc = new TextBox();
            label67 = new Label();
            Cwiartka2Wartosc = new TextBox();
            Cwiartka2Cena = new TextBox();
            Cwiartka2KG = new TextBox();
            Skrzydlo2Wydajnosc = new TextBox();
            label68 = new Label();
            Skrzydlo2Wartosc = new TextBox();
            Skrzydlo2Cena = new TextBox();
            Skrzydlo2KG = new TextBox();
            CenaRoznicaTuszkaElementy = new TextBox();
            CenaKrojenia = new TextBox();
            CenaMrozenia = new TextBox();
            RoznicaObnizeniaCeny = new TextBox();
            CenaRoznicaTuszkaElementyPokrojone = new TextBox();
            label69 = new Label();
            KosztSprzedazyPokrojonego = new TextBox();
            pictureBox32 = new PictureBox();
            pictureBox33 = new PictureBox();
            OdswiezButton = new Button();
            dateTimePicker2 = new DateTimePicker();
            dateTimePicker1 = new DateTimePicker();
            PokazMroznie = new Button();
            dataGridView1 = new DataGridView();
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
            ((System.ComponentModel.ISupportInitialize)pictureBox23).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox24).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox25).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox26).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox27).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox28).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox30).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox31).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox32).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox33).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            SuspendLayout();
            // 
            // sztuki
            // 
            sztuki.BackColor = SystemColors.ControlLight;
            sztuki.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            sztuki.Location = new Point(1642, 810);
            sztuki.Name = "sztuki";
            sztuki.Size = new Size(62, 29);
            sztuki.TabIndex = 26;
            sztuki.TextAlign = HorizontalAlignment.Center;
            // 
            // label11
            // 
            label11.Location = new Point(1403, 739);
            label11.Name = "label11";
            label11.Size = new Size(62, 47);
            label11.TabIndex = 25;
            label11.Text = "Ilość skrzynek na 1 auto";
            label11.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // sztukNaSzuflade
            // 
            sztukNaSzuflade.BackColor = Color.White;
            sztukNaSzuflade.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            sztukNaSzuflade.Location = new Point(1322, 789);
            sztukNaSzuflade.Name = "sztukNaSzuflade";
            sztukNaSzuflade.Size = new Size(57, 29);
            sztukNaSzuflade.TabIndex = 28;
            sztukNaSzuflade.TextAlign = HorizontalAlignment.Center;
            // 
            // label10
            // 
            label10.Location = new Point(1621, 690);
            label10.Name = "label10";
            label10.Size = new Size(57, 47);
            label10.TabIndex = 27;
            label10.Text = "Sztuki na szuflade";
            label10.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.BackColor = SystemColors.ControlLight;
            label1.Location = new Point(1385, 794);
            label1.Name = "label1";
            label1.Size = new Size(15, 20);
            label1.TabIndex = 29;
            label1.Text = "*";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(1471, 794);
            label2.Name = "label2";
            label2.Size = new Size(19, 20);
            label2.TabIndex = 30;
            label2.Text = "=";
            // 
            // textBox1
            // 
            textBox1.BackColor = SystemColors.ControlLight;
            textBox1.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            textBox1.Location = new Point(1490, 812);
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(62, 29);
            textBox1.TabIndex = 31;
            textBox1.Text = "264";
            textBox1.TextAlign = HorizontalAlignment.Center;
            // 
            // label3
            // 
            label3.Location = new Point(1492, 736);
            label3.Name = "label3";
            label3.Size = new Size(62, 47);
            label3.TabIndex = 32;
            label3.Text = "Deklarowane sztuki na 1 auto";
            label3.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(1560, 794);
            label4.Name = "label4";
            label4.Size = new Size(15, 20);
            label4.TabIndex = 33;
            label4.Text = "*";
            // 
            // obliczeniaAut
            // 
            obliczeniaAut.BackColor = SystemColors.Window;
            obliczeniaAut.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            obliczeniaAut.Location = new Point(1578, 789);
            obliczeniaAut.Name = "obliczeniaAut";
            obliczeniaAut.Size = new Size(36, 29);
            obliczeniaAut.TabIndex = 34;
            obliczeniaAut.TextAlign = HorizontalAlignment.Center;
            // 
            // label12
            // 
            label12.Location = new Point(1578, 742);
            label12.Name = "label12";
            label12.Size = new Size(36, 35);
            label12.TabIndex = 35;
            label12.Text = "Ilość aut";
            label12.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(1874, 376);
            label5.Name = "label5";
            label5.Size = new Size(19, 20);
            label5.TabIndex = 36;
            label5.Text = "=";
            // 
            // label6
            // 
            label6.Location = new Point(1550, 715);
            label6.Name = "label6";
            label6.Size = new Size(93, 47);
            label6.TabIndex = 38;
            label6.Text = "Suma deklarowanych sztuk";
            label6.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // sumaSztuk
            // 
            sumaSztuk.BackColor = SystemColors.ControlLight;
            sumaSztuk.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            sumaSztuk.Location = new Point(1597, 789);
            sumaSztuk.Name = "sumaSztuk";
            sumaSztuk.Size = new Size(86, 29);
            sumaSztuk.TabIndex = 37;
            sumaSztuk.TextAlign = HorizontalAlignment.Center;
            // 
            // buttonZamknij
            // 
            buttonZamknij.BackColor = Color.IndianRed;
            buttonZamknij.Location = new Point(1568, 839);
            buttonZamknij.Name = "buttonZamknij";
            buttonZamknij.Size = new Size(75, 23);
            buttonZamknij.TabIndex = 40;
            buttonZamknij.Text = "Anuluj";
            buttonZamknij.UseVisualStyleBackColor = false;
            // 
            // button1
            // 
            button1.BackColor = Color.Chartreuse;
            button1.Location = new Point(1649, 824);
            button1.Name = "button1";
            button1.Size = new Size(75, 23);
            button1.TabIndex = 39;
            button1.Text = "Stwórz";
            button1.UseVisualStyleBackColor = false;
            // 
            // TuszkaKG
            // 
            TuszkaKG.BackColor = Color.LightGreen;
            TuszkaKG.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            TuszkaKG.Location = new Point(141, 19);
            TuszkaKG.Name = "TuszkaKG";
            TuszkaKG.Size = new Size(106, 32);
            TuszkaKG.TabIndex = 42;
            TuszkaKG.TextAlign = HorizontalAlignment.Center;
            TuszkaKG.TextChanged += TuszkaKG_TextChanged_1;
            // 
            // label7
            // 
            label7.Location = new Point(141, 1);
            label7.Name = "label7";
            label7.Size = new Size(106, 16);
            label7.TabIndex = 41;
            label7.Text = "KG";
            label7.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // TuszkaCena
            // 
            TuszkaCena.BackColor = Color.LightGreen;
            TuszkaCena.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            TuszkaCena.Location = new Point(253, 19);
            TuszkaCena.Name = "TuszkaCena";
            TuszkaCena.Size = new Size(57, 32);
            TuszkaCena.TabIndex = 44;
            TuszkaCena.TextAlign = HorizontalAlignment.Center;
            TuszkaCena.TextChanged += TuszkaCena_TextChanged_1;
            // 
            // label8
            // 
            label8.Location = new Point(253, 1);
            label8.Name = "label8";
            label8.Size = new Size(57, 16);
            label8.TabIndex = 43;
            label8.Text = "Cena";
            label8.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // TuszkaWartosc
            // 
            TuszkaWartosc.BackColor = Color.LightGreen;
            TuszkaWartosc.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            TuszkaWartosc.Location = new Point(316, 19);
            TuszkaWartosc.Name = "TuszkaWartosc";
            TuszkaWartosc.Size = new Size(117, 32);
            TuszkaWartosc.TabIndex = 46;
            TuszkaWartosc.TextAlign = HorizontalAlignment.Center;
            // 
            // label9
            // 
            label9.Location = new Point(316, 1);
            label9.Name = "label9";
            label9.Size = new Size(117, 16);
            label9.TabIndex = 45;
            label9.Text = "Wartość";
            label9.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // FiletWartosc
            // 
            FiletWartosc.BackColor = Color.White;
            FiletWartosc.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            FiletWartosc.Location = new Point(316, 81);
            FiletWartosc.Name = "FiletWartosc";
            FiletWartosc.Size = new Size(117, 29);
            FiletWartosc.TabIndex = 52;
            FiletWartosc.TextAlign = HorizontalAlignment.Center;
            // 
            // FiletCena
            // 
            FiletCena.BackColor = Color.White;
            FiletCena.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            FiletCena.Location = new Point(253, 81);
            FiletCena.Name = "FiletCena";
            FiletCena.Size = new Size(57, 29);
            FiletCena.TabIndex = 50;
            FiletCena.TextAlign = HorizontalAlignment.Center;
            FiletCena.TextChanged += FiletCena_TextChanged;
            // 
            // FiletKG
            // 
            FiletKG.BackColor = Color.White;
            FiletKG.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            FiletKG.Location = new Point(141, 81);
            FiletKG.Name = "FiletKG";
            FiletKG.Size = new Size(106, 29);
            FiletKG.TabIndex = 48;
            FiletKG.TextAlign = HorizontalAlignment.Center;
            FiletKG.TextChanged += FiletKG_TextChanged;
            // 
            // CwiartkaWartosc
            // 
            CwiartkaWartosc.BackColor = Color.White;
            CwiartkaWartosc.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            CwiartkaWartosc.Location = new Point(316, 129);
            CwiartkaWartosc.Name = "CwiartkaWartosc";
            CwiartkaWartosc.Size = new Size(117, 29);
            CwiartkaWartosc.TabIndex = 58;
            CwiartkaWartosc.TextAlign = HorizontalAlignment.Center;
            CwiartkaWartosc.TextChanged += CwiartkaWartosc_TextChanged;
            // 
            // CwiartkaCena
            // 
            CwiartkaCena.BackColor = Color.White;
            CwiartkaCena.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            CwiartkaCena.Location = new Point(253, 129);
            CwiartkaCena.Name = "CwiartkaCena";
            CwiartkaCena.Size = new Size(57, 29);
            CwiartkaCena.TabIndex = 56;
            CwiartkaCena.TextAlign = HorizontalAlignment.Center;
            CwiartkaCena.TextChanged += CwiartkaCena_TextChanged;
            // 
            // CwiartkaKG
            // 
            CwiartkaKG.BackColor = Color.White;
            CwiartkaKG.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            CwiartkaKG.Location = new Point(141, 129);
            CwiartkaKG.Name = "CwiartkaKG";
            CwiartkaKG.Size = new Size(106, 29);
            CwiartkaKG.TabIndex = 54;
            CwiartkaKG.TextAlign = HorizontalAlignment.Center;
            CwiartkaKG.TextChanged += CwiartkaKG_TextChanged;
            // 
            // SkrzydloWartosc
            // 
            SkrzydloWartosc.BackColor = Color.White;
            SkrzydloWartosc.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            SkrzydloWartosc.Location = new Point(316, 177);
            SkrzydloWartosc.Name = "SkrzydloWartosc";
            SkrzydloWartosc.Size = new Size(117, 29);
            SkrzydloWartosc.TabIndex = 64;
            SkrzydloWartosc.TextAlign = HorizontalAlignment.Center;
            // 
            // SkrzydloCena
            // 
            SkrzydloCena.BackColor = Color.White;
            SkrzydloCena.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            SkrzydloCena.Location = new Point(253, 177);
            SkrzydloCena.Name = "SkrzydloCena";
            SkrzydloCena.Size = new Size(57, 29);
            SkrzydloCena.TabIndex = 62;
            SkrzydloCena.TextAlign = HorizontalAlignment.Center;
            SkrzydloCena.TextChanged += SkrzydloCena_TextChanged;
            // 
            // SkrzydloKG
            // 
            SkrzydloKG.BackColor = Color.White;
            SkrzydloKG.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            SkrzydloKG.Location = new Point(141, 177);
            SkrzydloKG.Name = "SkrzydloKG";
            SkrzydloKG.Size = new Size(106, 29);
            SkrzydloKG.TabIndex = 60;
            SkrzydloKG.TextAlign = HorizontalAlignment.Center;
            SkrzydloKG.TextChanged += SkrzydloKG_TextChanged;
            // 
            // KorpusWartosc
            // 
            KorpusWartosc.BackColor = Color.White;
            KorpusWartosc.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            KorpusWartosc.Location = new Point(316, 220);
            KorpusWartosc.Name = "KorpusWartosc";
            KorpusWartosc.Size = new Size(117, 29);
            KorpusWartosc.TabIndex = 70;
            KorpusWartosc.TextAlign = HorizontalAlignment.Center;
            // 
            // KorpusCena
            // 
            KorpusCena.BackColor = Color.White;
            KorpusCena.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            KorpusCena.Location = new Point(253, 220);
            KorpusCena.Name = "KorpusCena";
            KorpusCena.Size = new Size(57, 29);
            KorpusCena.TabIndex = 68;
            KorpusCena.TextAlign = HorizontalAlignment.Center;
            KorpusCena.TextChanged += KorpusCena_TextChanged;
            // 
            // KorpusKG
            // 
            KorpusKG.BackColor = Color.White;
            KorpusKG.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            KorpusKG.Location = new Point(141, 220);
            KorpusKG.Name = "KorpusKG";
            KorpusKG.Size = new Size(106, 29);
            KorpusKG.TabIndex = 66;
            KorpusKG.TextAlign = HorizontalAlignment.Center;
            // 
            // label13
            // 
            label13.Location = new Point(1, 81);
            label13.Name = "label13";
            label13.Size = new Size(71, 25);
            label13.TabIndex = 71;
            label13.Text = "Filet";
            label13.TextAlign = ContentAlignment.MiddleRight;
            // 
            // label14
            // 
            label14.Location = new Point(1, 129);
            label14.Name = "label14";
            label14.Size = new Size(71, 25);
            label14.TabIndex = 72;
            label14.Text = "Ćwiartka";
            label14.TextAlign = ContentAlignment.MiddleRight;
            // 
            // label15
            // 
            label15.Location = new Point(1, 177);
            label15.Name = "label15";
            label15.Size = new Size(71, 25);
            label15.TabIndex = 73;
            label15.Text = "Skrzydło";
            label15.TextAlign = ContentAlignment.MiddleRight;
            // 
            // label16
            // 
            label16.Location = new Point(1, 220);
            label16.Name = "label16";
            label16.Size = new Size(71, 25);
            label16.TabIndex = 74;
            label16.Text = "Korpus";
            label16.TextAlign = ContentAlignment.MiddleRight;
            // 
            // label17
            // 
            label17.Location = new Point(1, 245);
            label17.Name = "label17";
            label17.Size = new Size(71, 25);
            label17.TabIndex = 78;
            label17.Text = "Pozostałe";
            label17.TextAlign = ContentAlignment.MiddleRight;
            // 
            // PozostaleWartosc
            // 
            PozostaleWartosc.BackColor = Color.White;
            PozostaleWartosc.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            PozostaleWartosc.Location = new Point(316, 245);
            PozostaleWartosc.Name = "PozostaleWartosc";
            PozostaleWartosc.Size = new Size(117, 29);
            PozostaleWartosc.TabIndex = 77;
            PozostaleWartosc.TextAlign = HorizontalAlignment.Center;
            // 
            // PozostaleCena
            // 
            PozostaleCena.BackColor = Color.White;
            PozostaleCena.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            PozostaleCena.Location = new Point(253, 245);
            PozostaleCena.Name = "PozostaleCena";
            PozostaleCena.Size = new Size(57, 29);
            PozostaleCena.TabIndex = 76;
            PozostaleCena.TextAlign = HorizontalAlignment.Center;
            PozostaleCena.TextChanged += PozostaleCena_TextChanged;
            // 
            // PozostaleKG
            // 
            PozostaleKG.BackColor = Color.White;
            PozostaleKG.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            PozostaleKG.Location = new Point(141, 245);
            PozostaleKG.Name = "PozostaleKG";
            PozostaleKG.Size = new Size(106, 29);
            PozostaleKG.TabIndex = 75;
            PozostaleKG.TextAlign = HorizontalAlignment.Center;
            // 
            // TuszkaWydajnosc
            // 
            TuszkaWydajnosc.BackColor = Color.LightGreen;
            TuszkaWydajnosc.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            TuszkaWydajnosc.Location = new Point(78, 19);
            TuszkaWydajnosc.Name = "TuszkaWydajnosc";
            TuszkaWydajnosc.Size = new Size(57, 32);
            TuszkaWydajnosc.TabIndex = 79;
            TuszkaWydajnosc.TextAlign = HorizontalAlignment.Center;
            // 
            // label18
            // 
            label18.Location = new Point(78, 1);
            label18.Name = "label18";
            label18.Size = new Size(57, 16);
            label18.TabIndex = 80;
            label18.Text = "%";
            label18.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // FiletWydajnosc
            // 
            FiletWydajnosc.BackColor = Color.White;
            FiletWydajnosc.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            FiletWydajnosc.Location = new Point(78, 81);
            FiletWydajnosc.Name = "FiletWydajnosc";
            FiletWydajnosc.Size = new Size(57, 29);
            FiletWydajnosc.TabIndex = 81;
            FiletWydajnosc.TextAlign = HorizontalAlignment.Center;
            FiletWydajnosc.TextChanged += FiletWydajnosc_TextChanged;
            // 
            // CwiartkaWydajnosc
            // 
            CwiartkaWydajnosc.BackColor = Color.White;
            CwiartkaWydajnosc.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            CwiartkaWydajnosc.Location = new Point(78, 129);
            CwiartkaWydajnosc.Name = "CwiartkaWydajnosc";
            CwiartkaWydajnosc.Size = new Size(57, 29);
            CwiartkaWydajnosc.TabIndex = 82;
            CwiartkaWydajnosc.TextAlign = HorizontalAlignment.Center;
            CwiartkaWydajnosc.TextChanged += CwiartkaWydajnosc_TextChanged;
            // 
            // SkrzydloWydajnosc
            // 
            SkrzydloWydajnosc.BackColor = Color.White;
            SkrzydloWydajnosc.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            SkrzydloWydajnosc.Location = new Point(78, 177);
            SkrzydloWydajnosc.Name = "SkrzydloWydajnosc";
            SkrzydloWydajnosc.Size = new Size(57, 29);
            SkrzydloWydajnosc.TabIndex = 83;
            SkrzydloWydajnosc.TextAlign = HorizontalAlignment.Center;
            SkrzydloWydajnosc.TextChanged += SkrzydloWydajnosc_TextChanged;
            // 
            // KorpusWydajnosc
            // 
            KorpusWydajnosc.BackColor = Color.White;
            KorpusWydajnosc.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            KorpusWydajnosc.Location = new Point(78, 220);
            KorpusWydajnosc.Name = "KorpusWydajnosc";
            KorpusWydajnosc.Size = new Size(57, 29);
            KorpusWydajnosc.TabIndex = 84;
            KorpusWydajnosc.TextAlign = HorizontalAlignment.Center;
            KorpusWydajnosc.TextChanged += KorpusWydajnosc_TextChanged;
            // 
            // PozostaleWydajnosc
            // 
            PozostaleWydajnosc.BackColor = Color.White;
            PozostaleWydajnosc.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            PozostaleWydajnosc.Location = new Point(78, 245);
            PozostaleWydajnosc.Name = "PozostaleWydajnosc";
            PozostaleWydajnosc.Size = new Size(57, 29);
            PozostaleWydajnosc.TabIndex = 85;
            PozostaleWydajnosc.TextAlign = HorizontalAlignment.Center;
            PozostaleWydajnosc.TextChanged += PozostaleWydajnosc_TextChanged;
            // 
            // SumaWydajnosc
            // 
            SumaWydajnosc.BackColor = Color.IndianRed;
            SumaWydajnosc.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            SumaWydajnosc.Location = new Point(78, 55);
            SumaWydajnosc.Name = "SumaWydajnosc";
            SumaWydajnosc.Size = new Size(57, 32);
            SumaWydajnosc.TabIndex = 90;
            SumaWydajnosc.TextAlign = HorizontalAlignment.Center;
            // 
            // label19
            // 
            label19.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            label19.Location = new Point(-8, 55);
            label19.Name = "label19";
            label19.Size = new Size(80, 25);
            label19.TabIndex = 89;
            label19.Text = "Elementy";
            label19.TextAlign = ContentAlignment.MiddleRight;
            // 
            // SumaWartosc
            // 
            SumaWartosc.BackColor = Color.IndianRed;
            SumaWartosc.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            SumaWartosc.Location = new Point(316, 55);
            SumaWartosc.Name = "SumaWartosc";
            SumaWartosc.Size = new Size(117, 32);
            SumaWartosc.TabIndex = 88;
            SumaWartosc.TextAlign = HorizontalAlignment.Center;
            // 
            // SumaCena
            // 
            SumaCena.BackColor = Color.IndianRed;
            SumaCena.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            SumaCena.Location = new Point(253, 55);
            SumaCena.Name = "SumaCena";
            SumaCena.Size = new Size(57, 32);
            SumaCena.TabIndex = 87;
            SumaCena.TextAlign = HorizontalAlignment.Center;
            SumaCena.TextChanged += SumaCena_TextChanged;
            // 
            // SumaKG
            // 
            SumaKG.BackColor = Color.IndianRed;
            SumaKG.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            SumaKG.Location = new Point(141, 55);
            SumaKG.Name = "SumaKG";
            SumaKG.Size = new Size(106, 32);
            SumaKG.TabIndex = 86;
            SumaKG.TextAlign = HorizontalAlignment.Center;
            // 
            // RoznicaTuszkaElement
            // 
            RoznicaTuszkaElement.BackColor = Color.White;
            RoznicaTuszkaElement.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            RoznicaTuszkaElement.Location = new Point(439, 38);
            RoznicaTuszkaElement.Name = "RoznicaTuszkaElement";
            RoznicaTuszkaElement.Size = new Size(89, 32);
            RoznicaTuszkaElement.TabIndex = 91;
            RoznicaTuszkaElement.TextAlign = HorizontalAlignment.Center;
            // 
            // SumaWartosc2
            // 
            SumaWartosc2.BackColor = SystemColors.ButtonFace;
            SumaWartosc2.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            SumaWartosc2.Location = new Point(767, 141);
            SumaWartosc2.Multiline = true;
            SumaWartosc2.Name = "SumaWartosc2";
            SumaWartosc2.Size = new Size(207, 63);
            SumaWartosc2.TabIndex = 94;
            SumaWartosc2.TextAlign = HorizontalAlignment.Center;
            SumaWartosc2.TextChanged += SumaWartosc2_TextChanged;
            // 
            // label21
            // 
            label21.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            label21.ImageAlign = ContentAlignment.MiddleRight;
            label21.Location = new Point(1340, 690);
            label21.Name = "label21";
            label21.Size = new Size(66, 27);
            label21.TabIndex = 95;
            label21.Text = "Elementy";
            label21.TextAlign = ContentAlignment.MiddleRight;
            // 
            // label22
            // 
            label22.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            label22.ImageAlign = ContentAlignment.MiddleRight;
            label22.Location = new Point(1317, 726);
            label22.Name = "label22";
            label22.Size = new Size(89, 25);
            label22.TabIndex = 97;
            label22.Text = "Koszt Krojenia";
            label22.TextAlign = ContentAlignment.MiddleRight;
            // 
            // TuszkaKG2
            // 
            TuszkaKG2.BackColor = Color.LightGreen;
            TuszkaKG2.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            TuszkaKG2.Location = new Point(153, 276);
            TuszkaKG2.Multiline = true;
            TuszkaKG2.Name = "TuszkaKG2";
            TuszkaKG2.Size = new Size(206, 46);
            TuszkaKG2.TabIndex = 96;
            TuszkaKG2.TextAlign = HorizontalAlignment.Center;
            // 
            // WspolczynnikKrojenia
            // 
            WspolczynnikKrojenia.BackColor = Color.Gainsboro;
            WspolczynnikKrojenia.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            WspolczynnikKrojenia.Location = new Point(216, 328);
            WspolczynnikKrojenia.Multiline = true;
            WspolczynnikKrojenia.Name = "WspolczynnikKrojenia";
            WspolczynnikKrojenia.Size = new Size(83, 23);
            WspolczynnikKrojenia.TabIndex = 98;
            WspolczynnikKrojenia.TextAlign = HorizontalAlignment.Center;
            WspolczynnikKrojenia.TextChanged += WspolczynnikKrojenia_TextChanged;
            // 
            // WartoscKrojenia
            // 
            WartoscKrojenia.BackColor = Color.Gainsboro;
            WartoscKrojenia.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            WartoscKrojenia.Location = new Point(154, 357);
            WartoscKrojenia.Multiline = true;
            WartoscKrojenia.Name = "WartoscKrojenia";
            WartoscKrojenia.Size = new Size(205, 45);
            WartoscKrojenia.TabIndex = 99;
            WartoscKrojenia.TextAlign = HorizontalAlignment.Center;
            // 
            // label20
            // 
            label20.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold, GraphicsUnit.Point);
            label20.Location = new Point(1689, 750);
            label20.Name = "label20";
            label20.Size = new Size(89, 10);
            label20.TabIndex = 101;
            label20.Text = "Elementy po krojeniu";
            label20.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // WartoscElementowPoKrojeniu
            // 
            WartoscElementowPoKrojeniu.BackColor = Color.IndianRed;
            WartoscElementowPoKrojeniu.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            WartoscElementowPoKrojeniu.Location = new Point(1412, 690);
            WartoscElementowPoKrojeniu.Name = "WartoscElementowPoKrojeniu";
            WartoscElementowPoKrojeniu.Size = new Size(117, 32);
            WartoscElementowPoKrojeniu.TabIndex = 100;
            WartoscElementowPoKrojeniu.TextAlign = HorizontalAlignment.Center;
            // 
            // label23
            // 
            label23.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold, GraphicsUnit.Point);
            label23.ImageAlign = ContentAlignment.MiddleRight;
            label23.Location = new Point(1344, 757);
            label23.Name = "label23";
            label23.Size = new Size(117, 10);
            label23.TabIndex = 103;
            label23.Text = "Wartość Elementów po krojeniu";
            label23.TextAlign = ContentAlignment.MiddleRight;
            // 
            // WartoscElementowPoKrojeniu2
            // 
            WartoscElementowPoKrojeniu2.BackColor = SystemColors.ButtonFace;
            WartoscElementowPoKrojeniu2.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            WartoscElementowPoKrojeniu2.Location = new Point(766, 279);
            WartoscElementowPoKrojeniu2.Multiline = true;
            WartoscElementowPoKrojeniu2.Name = "WartoscElementowPoKrojeniu2";
            WartoscElementowPoKrojeniu2.Size = new Size(207, 63);
            WartoscElementowPoKrojeniu2.TabIndex = 102;
            WartoscElementowPoKrojeniu2.TextAlign = HorizontalAlignment.Center;
            // 
            // label25
            // 
            label25.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold, GraphicsUnit.Point);
            label25.ImageAlign = ContentAlignment.MiddleRight;
            label25.Location = new Point(1344, 724);
            label25.Name = "label25";
            label25.Size = new Size(117, 10);
            label25.TabIndex = 105;
            label25.Text = "Wartość Tuszki bez przerobu";
            label25.TextAlign = ContentAlignment.MiddleRight;
            // 
            // TuszkaWartosc2
            // 
            TuszkaWartosc2.BackColor = SystemColors.ButtonFace;
            TuszkaWartosc2.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            TuszkaWartosc2.Location = new Point(767, 5);
            TuszkaWartosc2.Multiline = true;
            TuszkaWartosc2.Name = "TuszkaWartosc2";
            TuszkaWartosc2.Size = new Size(207, 61);
            TuszkaWartosc2.TabIndex = 104;
            TuszkaWartosc2.TextAlign = HorizontalAlignment.Center;
            // 
            // label26
            // 
            label26.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold, GraphicsUnit.Point);
            label26.ImageAlign = ContentAlignment.MiddleRight;
            label26.Location = new Point(1344, 790);
            label26.Name = "label26";
            label26.Size = new Size(117, 10);
            label26.TabIndex = 107;
            label26.Text = "Różnica między elementami a tuszką";
            label26.TextAlign = ContentAlignment.MiddleRight;
            // 
            // RoznicaTuszkaElement2
            // 
            RoznicaTuszkaElement2.BackColor = Color.White;
            RoznicaTuszkaElement2.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            RoznicaTuszkaElement2.Location = new Point(1467, 773);
            RoznicaTuszkaElement2.Name = "RoznicaTuszkaElement2";
            RoznicaTuszkaElement2.Size = new Size(117, 32);
            RoznicaTuszkaElement2.TabIndex = 106;
            RoznicaTuszkaElement2.TextAlign = HorizontalAlignment.Center;
            // 
            // label27
            // 
            label27.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold, GraphicsUnit.Point);
            label27.Location = new Point(1376, 790);
            label27.Name = "label27";
            label27.Size = new Size(13, 27);
            label27.TabIndex = 108;
            label27.Text = "-";
            label27.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label28
            // 
            label28.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold, GraphicsUnit.Point);
            label28.Location = new Point(1513, 791);
            label28.Name = "label28";
            label28.Size = new Size(16, 27);
            label28.TabIndex = 109;
            label28.Text = "=";
            label28.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label29
            // 
            label29.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold, GraphicsUnit.Point);
            label29.Location = new Point(1513, 790);
            label29.Name = "label29";
            label29.Size = new Size(16, 27);
            label29.TabIndex = 117;
            label29.Text = "=";
            label29.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label30
            // 
            label30.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold, GraphicsUnit.Point);
            label30.Location = new Point(1376, 789);
            label30.Name = "label30";
            label30.Size = new Size(13, 27);
            label30.TabIndex = 116;
            label30.Text = "-";
            label30.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // textBox2
            // 
            textBox2.BackColor = Color.White;
            textBox2.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            textBox2.Location = new Point(1526, 872);
            textBox2.Name = "textBox2";
            textBox2.Size = new Size(117, 32);
            textBox2.TabIndex = 114;
            textBox2.TextAlign = HorizontalAlignment.Center;
            // 
            // label32
            // 
            label32.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold, GraphicsUnit.Point);
            label32.Location = new Point(1587, 799);
            label32.Name = "label32";
            label32.Size = new Size(117, 10);
            label32.TabIndex = 113;
            label32.Text = "Wartość Tuszki bez przerobu";
            label32.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // textBox3
            // 
            textBox3.BackColor = Color.Gainsboro;
            textBox3.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            textBox3.Location = new Point(1289, 794);
            textBox3.Name = "textBox3";
            textBox3.Size = new Size(117, 32);
            textBox3.TabIndex = 112;
            textBox3.TextAlign = HorizontalAlignment.Center;
            // 
            // label33
            // 
            label33.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold, GraphicsUnit.Point);
            label33.Location = new Point(1253, 759);
            label33.Name = "label33";
            label33.Size = new Size(117, 27);
            label33.TabIndex = 111;
            label33.Text = "Wartość Elementów po krojeniu";
            label33.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // textBox4
            // 
            textBox4.BackColor = Color.FromArgb(128, 64, 0);
            textBox4.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            textBox4.Location = new Point(1280, 864);
            textBox4.Name = "textBox4";
            textBox4.Size = new Size(117, 32);
            textBox4.TabIndex = 110;
            textBox4.TextAlign = HorizontalAlignment.Center;
            // 
            // textBox5
            // 
            textBox5.BackColor = Color.IndianRed;
            textBox5.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            textBox5.Location = new Point(1568, 778);
            textBox5.Name = "textBox5";
            textBox5.Size = new Size(117, 32);
            textBox5.TabIndex = 118;
            textBox5.TextAlign = HorizontalAlignment.Center;
            // 
            // pictureBox3
            // 
            pictureBox3.Image = (Image)resources.GetObject("pictureBox3.Image");
            pictureBox3.Location = new Point(11, 775);
            pictureBox3.Name = "pictureBox3";
            pictureBox3.Size = new Size(136, 61);
            pictureBox3.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox3.TabIndex = 136;
            pictureBox3.TabStop = false;
            // 
            // textBox6
            // 
            textBox6.BackColor = Color.IndianRed;
            textBox6.Font = new Font("Microsoft Sans Serif", 15.75F, FontStyle.Regular, GraphicsUnit.Point);
            textBox6.Location = new Point(1291, 820);
            textBox6.Multiline = true;
            textBox6.Name = "textBox6";
            textBox6.Size = new Size(206, 61);
            textBox6.TabIndex = 137;
            textBox6.TextAlign = HorizontalAlignment.Center;
            textBox6.TextChanged += textBox6_TextChanged;
            // 
            // label31
            // 
            label31.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold, GraphicsUnit.Point);
            label31.Location = new Point(1392, 704);
            label31.Name = "label31";
            label31.Size = new Size(130, 10);
            label31.TabIndex = 138;
            label31.Text = "Elementy po krojeniu";
            label31.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // pictureBox1
            // 
            pictureBox1.Image = (Image)resources.GetObject("pictureBox1.Image");
            pictureBox1.Location = new Point(11, 505);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(136, 61);
            pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox1.TabIndex = 139;
            pictureBox1.TabStop = false;
            // 
            // pictureBox2
            // 
            pictureBox2.Image = (Image)resources.GetObject("pictureBox2.Image");
            pictureBox2.Location = new Point(10, 638);
            pictureBox2.Name = "pictureBox2";
            pictureBox2.Size = new Size(136, 61);
            pictureBox2.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox2.TabIndex = 140;
            pictureBox2.TabStop = false;
            // 
            // pictureBox4
            // 
            pictureBox4.Image = (Image)resources.GetObject("pictureBox4.Image");
            pictureBox4.Location = new Point(11, 705);
            pictureBox4.Name = "pictureBox4";
            pictureBox4.Size = new Size(136, 61);
            pictureBox4.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox4.TabIndex = 141;
            pictureBox4.TabStop = false;
            // 
            // label34
            // 
            label34.Font = new Font("Segoe UI Light", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            label34.Location = new Point(482, 507);
            label34.Name = "label34";
            label34.Size = new Size(130, 60);
            label34.TabIndex = 144;
            label34.Text = "Za rozważanie 15 kg towaru";
            label34.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // label35
            // 
            label35.Font = new Font("Segoe UI Light", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            label35.Location = new Point(482, 574);
            label35.Name = "label35";
            label35.Size = new Size(130, 60);
            label35.TabIndex = 146;
            label35.Text = "Za folie za każde 10 kg";
            label35.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // label36
            // 
            label36.Font = new Font("Segoe UI Light", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            label36.Location = new Point(482, 708);
            label36.Name = "label36";
            label36.Size = new Size(130, 60);
            label36.TabIndex = 148;
            label36.Text = "Paleta drewniana za każde 750 kg towaru";
            label36.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // label37
            // 
            label37.Font = new Font("Segoe UI Light", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            label37.Location = new Point(482, 641);
            label37.Name = "label37";
            label37.Size = new Size(130, 60);
            label37.TabIndex = 148;
            label37.Text = "Za Sktrech za każde 3000 kg";
            label37.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // label38
            // 
            label38.Font = new Font("Segoe UI Light", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            label38.Location = new Point(482, 777);
            label38.Name = "label38";
            label38.Size = new Size(130, 60);
            label38.TabIndex = 151;
            label38.Text = "Karton za każde 10 kg towaru";
            label38.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // pictureBox6
            // 
            pictureBox6.Image = (Image)resources.GetObject("pictureBox6.Image");
            pictureBox6.Location = new Point(11, 571);
            pictureBox6.Name = "pictureBox6";
            pictureBox6.Size = new Size(136, 61);
            pictureBox6.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox6.TabIndex = 149;
            pictureBox6.TabStop = false;
            // 
            // label39
            // 
            label39.Font = new Font("Segoe UI Light", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            label39.Location = new Point(482, 844);
            label39.Name = "label39";
            label39.Size = new Size(130, 60);
            label39.TabIndex = 154;
            label39.Text = "Energia za każde 1 kg towaru";
            label39.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // PradMroznia
            // 
            PradMroznia.BackColor = Color.Cyan;
            PradMroznia.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            PradMroznia.Location = new Point(153, 841);
            PradMroznia.Multiline = true;
            PradMroznia.Name = "PradMroznia";
            PradMroznia.Size = new Size(206, 61);
            PradMroznia.TabIndex = 153;
            PradMroznia.TextAlign = HorizontalAlignment.Center;
            PradMroznia.TextChanged += PradMroznia_TextChanged;
            // 
            // pictureBox7
            // 
            pictureBox7.Image = (Image)resources.GetObject("pictureBox7.Image");
            pictureBox7.Location = new Point(11, 841);
            pictureBox7.Name = "pictureBox7";
            pictureBox7.Size = new Size(136, 61);
            pictureBox7.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox7.TabIndex = 152;
            pictureBox7.TabStop = false;
            // 
            // SumaKosztowMrozenia
            // 
            SumaKosztowMrozenia.BackColor = Color.Cyan;
            SumaKosztowMrozenia.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            SumaKosztowMrozenia.Location = new Point(153, 909);
            SumaKosztowMrozenia.Multiline = true;
            SumaKosztowMrozenia.Name = "SumaKosztowMrozenia";
            SumaKosztowMrozenia.Size = new Size(206, 61);
            SumaKosztowMrozenia.TabIndex = 156;
            SumaKosztowMrozenia.TextAlign = HorizontalAlignment.Center;
            SumaKosztowMrozenia.TextChanged += SumaKosztowMrozenia_TextChanged;
            // 
            // label40
            // 
            label40.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            label40.Location = new Point(365, 909);
            label40.Name = "label40";
            label40.Size = new Size(130, 60);
            label40.TabIndex = 157;
            label40.Text = "Koszt Mrożenia";
            label40.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // label41
            // 
            label41.Font = new Font("Segoe UI Light", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            label41.Location = new Point(482, 439);
            label41.Name = "label41";
            label41.Size = new Size(130, 60);
            label41.TabIndex = 160;
            label41.Text = "Iloś kilogramów do zagospodarowania an mroźnie";
            label41.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // KilogramyDoZamrozenia
            // 
            KilogramyDoZamrozenia.BackColor = Color.LightGreen;
            KilogramyDoZamrozenia.Font = new Font("Microsoft Sans Serif", 15.75F, FontStyle.Regular, GraphicsUnit.Point);
            KilogramyDoZamrozenia.Location = new Point(152, 436);
            KilogramyDoZamrozenia.Multiline = true;
            KilogramyDoZamrozenia.Name = "KilogramyDoZamrozenia";
            KilogramyDoZamrozenia.Size = new Size(206, 61);
            KilogramyDoZamrozenia.TabIndex = 159;
            KilogramyDoZamrozenia.TextAlign = HorizontalAlignment.Center;
            KilogramyDoZamrozenia.TextChanged += KilogramyDoZamrozenia_TextChanged;
            // 
            // SktrechNaPaleteCena
            // 
            SktrechNaPaleteCena.BackColor = Color.Cyan;
            SktrechNaPaleteCena.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            SktrechNaPaleteCena.Location = new Point(365, 640);
            SktrechNaPaleteCena.Multiline = true;
            SktrechNaPaleteCena.Name = "SktrechNaPaleteCena";
            SktrechNaPaleteCena.Size = new Size(111, 61);
            SktrechNaPaleteCena.TabIndex = 162;
            SktrechNaPaleteCena.TextAlign = HorizontalAlignment.Center;
            SktrechNaPaleteCena.TextChanged += SktrechNaPaleteCena_TextChanged;
            // 
            // FoliaPojemnikCena
            // 
            FoliaPojemnikCena.BackColor = Color.Cyan;
            FoliaPojemnikCena.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            FoliaPojemnikCena.Location = new Point(365, 571);
            FoliaPojemnikCena.Multiline = true;
            FoliaPojemnikCena.Name = "FoliaPojemnikCena";
            FoliaPojemnikCena.Size = new Size(111, 61);
            FoliaPojemnikCena.TabIndex = 163;
            FoliaPojemnikCena.TextAlign = HorizontalAlignment.Center;
            FoliaPojemnikCena.TextChanged += FoliaPojemnikCena_TextChanged;
            // 
            // PaletaDrewnianaCena
            // 
            PaletaDrewnianaCena.BackColor = Color.Cyan;
            PaletaDrewnianaCena.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            PaletaDrewnianaCena.Location = new Point(365, 705);
            PaletaDrewnianaCena.Multiline = true;
            PaletaDrewnianaCena.Name = "PaletaDrewnianaCena";
            PaletaDrewnianaCena.Size = new Size(111, 61);
            PaletaDrewnianaCena.TabIndex = 164;
            PaletaDrewnianaCena.TextAlign = HorizontalAlignment.Center;
            PaletaDrewnianaCena.TextChanged += PaletaDrewnianaCena_TextChanged;
            // 
            // KartonCena
            // 
            KartonCena.BackColor = Color.Cyan;
            KartonCena.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            KartonCena.Location = new Point(365, 774);
            KartonCena.Multiline = true;
            KartonCena.Name = "KartonCena";
            KartonCena.Size = new Size(111, 61);
            KartonCena.TabIndex = 165;
            KartonCena.TextAlign = HorizontalAlignment.Center;
            KartonCena.TextChanged += KartonCena_TextChanged;
            // 
            // PradMrozniaCena
            // 
            PradMrozniaCena.BackColor = Color.Cyan;
            PradMrozniaCena.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            PradMrozniaCena.Location = new Point(365, 841);
            PradMrozniaCena.Multiline = true;
            PradMrozniaCena.Name = "PradMrozniaCena";
            PradMrozniaCena.Size = new Size(111, 61);
            PradMrozniaCena.TabIndex = 166;
            PradMrozniaCena.TextAlign = HorizontalAlignment.Center;
            PradMrozniaCena.TextChanged += PradMrozniaCena_TextChanged;
            // 
            // label42
            // 
            label42.Location = new Point(365, 433);
            label42.Name = "label42";
            label42.Size = new Size(111, 64);
            label42.TabIndex = 167;
            label42.Text = "Ceny za poszczególne elementy kosztów mroźni";
            label42.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // RozwazanieTowaruCena
            // 
            RozwazanieTowaruCena.BackColor = Color.Cyan;
            RozwazanieTowaruCena.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            RozwazanieTowaruCena.Location = new Point(365, 505);
            RozwazanieTowaruCena.Multiline = true;
            RozwazanieTowaruCena.Name = "RozwazanieTowaruCena";
            RozwazanieTowaruCena.Size = new Size(111, 61);
            RozwazanieTowaruCena.TabIndex = 168;
            RozwazanieTowaruCena.TextAlign = HorizontalAlignment.Center;
            RozwazanieTowaruCena.TextChanged += RozwazanieTowaruCena_TextChanged;
            // 
            // RozwazanieTowaru
            // 
            RozwazanieTowaru.BackColor = Color.Cyan;
            RozwazanieTowaru.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            RozwazanieTowaru.Location = new Point(153, 505);
            RozwazanieTowaru.Multiline = true;
            RozwazanieTowaru.Name = "RozwazanieTowaru";
            RozwazanieTowaru.Size = new Size(206, 61);
            RozwazanieTowaru.TabIndex = 169;
            RozwazanieTowaru.TextAlign = HorizontalAlignment.Center;
            RozwazanieTowaru.TextChanged += RozwazanieTowaru_TextChanged;
            // 
            // FoliaPojemnik
            // 
            FoliaPojemnik.BackColor = Color.Cyan;
            FoliaPojemnik.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            FoliaPojemnik.Location = new Point(153, 574);
            FoliaPojemnik.Multiline = true;
            FoliaPojemnik.Name = "FoliaPojemnik";
            FoliaPojemnik.Size = new Size(206, 61);
            FoliaPojemnik.TabIndex = 170;
            FoliaPojemnik.TextAlign = HorizontalAlignment.Center;
            FoliaPojemnik.TextChanged += FoliaPojemnik_TextChanged;
            // 
            // SktrechNaPalete
            // 
            SktrechNaPalete.BackColor = Color.Cyan;
            SktrechNaPalete.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            SktrechNaPalete.Location = new Point(153, 639);
            SktrechNaPalete.Multiline = true;
            SktrechNaPalete.Name = "SktrechNaPalete";
            SktrechNaPalete.Size = new Size(206, 61);
            SktrechNaPalete.TabIndex = 171;
            SktrechNaPalete.TextAlign = HorizontalAlignment.Center;
            SktrechNaPalete.TextChanged += SktrechNaPalete_TextChanged;
            // 
            // PaletaDrewniana
            // 
            PaletaDrewniana.BackColor = Color.Cyan;
            PaletaDrewniana.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            PaletaDrewniana.Location = new Point(153, 705);
            PaletaDrewniana.Multiline = true;
            PaletaDrewniana.Name = "PaletaDrewniana";
            PaletaDrewniana.Size = new Size(206, 61);
            PaletaDrewniana.TabIndex = 172;
            PaletaDrewniana.TextAlign = HorizontalAlignment.Center;
            PaletaDrewniana.TextChanged += PaletaDrewniana_TextChanged;
            // 
            // Karton
            // 
            Karton.BackColor = Color.Cyan;
            Karton.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            Karton.Location = new Point(153, 774);
            Karton.Multiline = true;
            Karton.Name = "Karton";
            Karton.Size = new Size(206, 61);
            Karton.TabIndex = 173;
            Karton.TextAlign = HorizontalAlignment.Center;
            Karton.TextChanged += Karton_TextChanged;
            // 
            // pictureBox8
            // 
            pictureBox8.Image = (Image)resources.GetObject("pictureBox8.Image");
            pictureBox8.Location = new Point(1660, 683);
            pictureBox8.Name = "pictureBox8";
            pictureBox8.Size = new Size(103, 10);
            pictureBox8.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox8.TabIndex = 174;
            pictureBox8.TabStop = false;
            // 
            // pictureBox10
            // 
            pictureBox10.Image = (Image)resources.GetObject("pictureBox10.Image");
            pictureBox10.Location = new Point(12, 276);
            pictureBox10.Name = "pictureBox10";
            pictureBox10.Size = new Size(137, 75);
            pictureBox10.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox10.TabIndex = 176;
            pictureBox10.TabStop = false;
            // 
            // pictureBox11
            // 
            pictureBox11.Image = (Image)resources.GetObject("pictureBox11.Image");
            pictureBox11.Location = new Point(11, 357);
            pictureBox11.Name = "pictureBox11";
            pictureBox11.Size = new Size(135, 45);
            pictureBox11.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox11.TabIndex = 178;
            pictureBox11.TabStop = false;
            // 
            // label24
            // 
            label24.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            label24.Location = new Point(10, 405);
            label24.Name = "label24";
            label24.Size = new Size(348, 28);
            label24.TabIndex = 180;
            label24.Text = "Obliczanie kosztu mrożenia";
            label24.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label43
            // 
            label43.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            label43.Location = new Point(1236, 648);
            label43.Name = "label43";
            label43.Size = new Size(348, 34);
            label43.TabIndex = 181;
            label43.Text = "Obliczanie kosztu krojenia";
            label43.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label44
            // 
            label44.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            label44.Location = new Point(366, 357);
            label44.Name = "label44";
            label44.Size = new Size(130, 45);
            label44.TabIndex = 182;
            label44.Text = "Koszt Krojenia";
            label44.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // textBox7
            // 
            textBox7.BackColor = Color.Gainsboro;
            textBox7.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            textBox7.Location = new Point(1412, 724);
            textBox7.Multiline = true;
            textBox7.Name = "textBox7";
            textBox7.Size = new Size(117, 27);
            textBox7.TabIndex = 183;
            textBox7.TextAlign = HorizontalAlignment.Center;
            // 
            // pictureBox9
            // 
            pictureBox9.Image = (Image)resources.GetObject("pictureBox9.Image");
            pictureBox9.Location = new Point(623, 5);
            pictureBox9.Name = "pictureBox9";
            pictureBox9.Size = new Size(138, 61);
            pictureBox9.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox9.TabIndex = 184;
            pictureBox9.TabStop = false;
            // 
            // pictureBox5
            // 
            pictureBox5.Image = (Image)resources.GetObject("pictureBox5.Image");
            pictureBox5.Location = new Point(623, 210);
            pictureBox5.Name = "pictureBox5";
            pictureBox5.Size = new Size(137, 63);
            pictureBox5.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox5.TabIndex = 186;
            pictureBox5.TabStop = false;
            // 
            // WartoscKrojenia2
            // 
            WartoscKrojenia2.BackColor = SystemColors.ButtonFace;
            WartoscKrojenia2.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            WartoscKrojenia2.ForeColor = Color.Red;
            WartoscKrojenia2.Location = new Point(766, 210);
            WartoscKrojenia2.Multiline = true;
            WartoscKrojenia2.Name = "WartoscKrojenia2";
            WartoscKrojenia2.Size = new Size(207, 63);
            WartoscKrojenia2.TabIndex = 187;
            WartoscKrojenia2.TextAlign = HorizontalAlignment.Center;
            // 
            // pictureBox13
            // 
            pictureBox13.Image = (Image)resources.GetObject("pictureBox13.Image");
            pictureBox13.Location = new Point(624, 543);
            pictureBox13.Name = "pictureBox13";
            pictureBox13.Size = new Size(138, 63);
            pictureBox13.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox13.TabIndex = 189;
            pictureBox13.TabStop = false;
            // 
            // WartoscElementowZamrozonych
            // 
            WartoscElementowZamrozonych.BackColor = SystemColors.ButtonFace;
            WartoscElementowZamrozonych.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            WartoscElementowZamrozonych.Location = new Point(768, 543);
            WartoscElementowZamrozonych.Multiline = true;
            WartoscElementowZamrozonych.Name = "WartoscElementowZamrozonych";
            WartoscElementowZamrozonych.Size = new Size(207, 63);
            WartoscElementowZamrozonych.TabIndex = 188;
            WartoscElementowZamrozonych.TextAlign = HorizontalAlignment.Center;
            // 
            // pictureBox14
            // 
            pictureBox14.Image = (Image)resources.GetObject("pictureBox14.Image");
            pictureBox14.Location = new Point(623, 474);
            pictureBox14.Name = "pictureBox14";
            pictureBox14.Size = new Size(138, 63);
            pictureBox14.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox14.TabIndex = 191;
            pictureBox14.TabStop = false;
            // 
            // SumaKosztowMrozenia2
            // 
            SumaKosztowMrozenia2.BackColor = SystemColors.ButtonFace;
            SumaKosztowMrozenia2.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            SumaKosztowMrozenia2.ForeColor = Color.Red;
            SumaKosztowMrozenia2.Location = new Point(767, 474);
            SumaKosztowMrozenia2.Multiline = true;
            SumaKosztowMrozenia2.Name = "SumaKosztowMrozenia2";
            SumaKosztowMrozenia2.Size = new Size(207, 63);
            SumaKosztowMrozenia2.TabIndex = 190;
            SumaKosztowMrozenia2.TextAlign = HorizontalAlignment.Center;
            // 
            // label45
            // 
            label45.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            label45.ForeColor = Color.Red;
            label45.Location = new Point(980, 474);
            label45.Name = "label45";
            label45.Size = new Size(215, 63);
            label45.TabIndex = 192;
            label45.Text = " Koszt Mrożenia";
            label45.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // label46
            // 
            label46.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            label46.ForeColor = Color.Red;
            label46.Location = new Point(979, 210);
            label46.Name = "label46";
            label46.Size = new Size(215, 63);
            label46.TabIndex = 193;
            label46.Text = "Koszt Krojenia";
            label46.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // pictureBox15
            // 
            pictureBox15.Image = (Image)resources.GetObject("pictureBox15.Image");
            pictureBox15.Location = new Point(11, 434);
            pictureBox15.Name = "pictureBox15";
            pictureBox15.Size = new Size(138, 63);
            pictureBox15.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox15.TabIndex = 194;
            pictureBox15.TabStop = false;
            // 
            // pictureBox12
            // 
            pictureBox12.Image = (Image)resources.GetObject("pictureBox12.Image");
            pictureBox12.Location = new Point(10, 909);
            pictureBox12.Name = "pictureBox12";
            pictureBox12.Size = new Size(137, 60);
            pictureBox12.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox12.TabIndex = 179;
            pictureBox12.TabStop = false;
            // 
            // label47
            // 
            label47.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            label47.Location = new Point(979, 279);
            label47.Name = "label47";
            label47.Size = new Size(215, 63);
            label47.TabIndex = 195;
            label47.Text = "Wartość Elementów po kosztach krojenia";
            label47.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // label48
            // 
            label48.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            label48.Location = new Point(980, 5);
            label48.Name = "label48";
            label48.Size = new Size(214, 61);
            label48.TabIndex = 196;
            label48.Text = "Wartość Tuszki";
            label48.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // label49
            // 
            label49.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            label49.Location = new Point(981, 543);
            label49.Name = "label49";
            label49.Size = new Size(214, 63);
            label49.TabIndex = 197;
            label49.Text = "Wartość Elementów Mrożonych";
            label49.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // label50
            // 
            label50.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            label50.ForeColor = Color.Red;
            label50.Location = new Point(980, 612);
            label50.Name = "label50";
            label50.Size = new Size(215, 63);
            label50.TabIndex = 200;
            label50.Text = "Koszt zaniżenia ceny";
            label50.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // pictureBox16
            // 
            pictureBox16.Image = (Image)resources.GetObject("pictureBox16.Image");
            pictureBox16.Location = new Point(623, 612);
            pictureBox16.Name = "pictureBox16";
            pictureBox16.Size = new Size(138, 63);
            pictureBox16.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox16.TabIndex = 199;
            pictureBox16.TabStop = false;
            // 
            // KosztZanizeniaCeny
            // 
            KosztZanizeniaCeny.BackColor = SystemColors.ButtonFace;
            KosztZanizeniaCeny.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            KosztZanizeniaCeny.ForeColor = Color.Red;
            KosztZanizeniaCeny.Location = new Point(767, 612);
            KosztZanizeniaCeny.Multiline = true;
            KosztZanizeniaCeny.Name = "KosztZanizeniaCeny";
            KosztZanizeniaCeny.Size = new Size(207, 63);
            KosztZanizeniaCeny.TabIndex = 198;
            KosztZanizeniaCeny.TextAlign = HorizontalAlignment.Center;
            // 
            // label51
            // 
            label51.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            label51.Location = new Point(980, 681);
            label51.Name = "label51";
            label51.Size = new Size(215, 63);
            label51.TabIndex = 203;
            label51.Text = "Wartość Elementów Mrożonych po zniżce";
            label51.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // pictureBox17
            // 
            pictureBox17.Image = (Image)resources.GetObject("pictureBox17.Image");
            pictureBox17.Location = new Point(623, 681);
            pictureBox17.Name = "pictureBox17";
            pictureBox17.Size = new Size(138, 63);
            pictureBox17.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox17.TabIndex = 202;
            pictureBox17.TabStop = false;
            // 
            // WartoscElementowMrozonychPoObnizce
            // 
            WartoscElementowMrozonychPoObnizce.BackColor = SystemColors.ButtonFace;
            WartoscElementowMrozonychPoObnizce.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            WartoscElementowMrozonychPoObnizce.Location = new Point(767, 681);
            WartoscElementowMrozonychPoObnizce.Multiline = true;
            WartoscElementowMrozonychPoObnizce.Name = "WartoscElementowMrozonychPoObnizce";
            WartoscElementowMrozonychPoObnizce.Size = new Size(207, 63);
            WartoscElementowMrozonychPoObnizce.TabIndex = 201;
            WartoscElementowMrozonychPoObnizce.TextAlign = HorizontalAlignment.Center;
            // 
            // label52
            // 
            label52.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            label52.Location = new Point(980, 788);
            label52.Name = "label52";
            label52.Size = new Size(215, 61);
            label52.TabIndex = 206;
            label52.Text = "Wartość Tuszki";
            label52.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // pictureBox18
            // 
            pictureBox18.Image = (Image)resources.GetObject("pictureBox18.Image");
            pictureBox18.Location = new Point(623, 788);
            pictureBox18.Name = "pictureBox18";
            pictureBox18.Size = new Size(138, 61);
            pictureBox18.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox18.TabIndex = 205;
            pictureBox18.TabStop = false;
            // 
            // TuszkaWartosc3
            // 
            TuszkaWartosc3.BackColor = Color.LightGreen;
            TuszkaWartosc3.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            TuszkaWartosc3.Location = new Point(767, 788);
            TuszkaWartosc3.Multiline = true;
            TuszkaWartosc3.Name = "TuszkaWartosc3";
            TuszkaWartosc3.Size = new Size(207, 61);
            TuszkaWartosc3.TabIndex = 204;
            TuszkaWartosc3.TextAlign = HorizontalAlignment.Center;
            // 
            // label53
            // 
            label53.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            label53.Location = new Point(980, 853);
            label53.Name = "label53";
            label53.Size = new Size(215, 63);
            label53.TabIndex = 209;
            label53.Text = "Wartość Elementów Mrożonych po zniżce";
            label53.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // pictureBox19
            // 
            pictureBox19.Image = (Image)resources.GetObject("pictureBox19.Image");
            pictureBox19.Location = new Point(623, 853);
            pictureBox19.Name = "pictureBox19";
            pictureBox19.Size = new Size(138, 63);
            pictureBox19.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox19.TabIndex = 208;
            pictureBox19.TabStop = false;
            // 
            // WartoscElementowMrozonychPoObnizce2
            // 
            WartoscElementowMrozonychPoObnizce2.BackColor = SystemColors.ActiveCaption;
            WartoscElementowMrozonychPoObnizce2.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            WartoscElementowMrozonychPoObnizce2.Location = new Point(767, 853);
            WartoscElementowMrozonychPoObnizce2.Multiline = true;
            WartoscElementowMrozonychPoObnizce2.Name = "WartoscElementowMrozonychPoObnizce2";
            WartoscElementowMrozonychPoObnizce2.Size = new Size(207, 63);
            WartoscElementowMrozonychPoObnizce2.TabIndex = 207;
            WartoscElementowMrozonychPoObnizce2.TextAlign = HorizontalAlignment.Center;
            // 
            // label54
            // 
            label54.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            label54.Location = new Point(980, 922);
            label54.Name = "label54";
            label54.Size = new Size(215, 63);
            label54.TabIndex = 212;
            label54.Text = "Strata";
            label54.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // Strata
            // 
            Strata.BackColor = SystemColors.ButtonFace;
            Strata.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            Strata.ForeColor = Color.Red;
            Strata.Location = new Point(767, 922);
            Strata.Multiline = true;
            Strata.Name = "Strata";
            Strata.Size = new Size(207, 63);
            Strata.TabIndex = 210;
            Strata.TextAlign = HorizontalAlignment.Center;
            // 
            // pictureBox20
            // 
            pictureBox20.Image = (Image)resources.GetObject("pictureBox20.Image");
            pictureBox20.Location = new Point(623, 922);
            pictureBox20.Name = "pictureBox20";
            pictureBox20.Size = new Size(138, 63);
            pictureBox20.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox20.TabIndex = 213;
            pictureBox20.TabStop = false;
            // 
            // pictureBox29
            // 
            pictureBox29.Image = (Image)resources.GetObject("pictureBox29.Image");
            pictureBox29.Location = new Point(622, 279);
            pictureBox29.Name = "pictureBox29";
            pictureBox29.Size = new Size(138, 63);
            pictureBox29.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox29.TabIndex = 185;
            pictureBox29.TabStop = false;
            // 
            // pictureBox21
            // 
            pictureBox21.Image = (Image)resources.GetObject("pictureBox21.Image");
            pictureBox21.Location = new Point(623, 72);
            pictureBox21.Name = "pictureBox21";
            pictureBox21.Size = new Size(138, 63);
            pictureBox21.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox21.TabIndex = 214;
            pictureBox21.TabStop = false;
            // 
            // label55
            // 
            label55.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            label55.Location = new Point(980, 72);
            label55.Name = "label55";
            label55.Size = new Size(214, 61);
            label55.TabIndex = 215;
            label55.Text = "Zysk na elementach nie wliczając kosztów krojenia";
            label55.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // label56
            // 
            label56.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            label56.Location = new Point(980, 141);
            label56.Name = "label56";
            label56.Size = new Size(214, 61);
            label56.TabIndex = 218;
            label56.Text = "Wartość Elementów";
            label56.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // pictureBox22
            // 
            pictureBox22.Image = (Image)resources.GetObject("pictureBox22.Image");
            pictureBox22.Location = new Point(623, 141);
            pictureBox22.Name = "pictureBox22";
            pictureBox22.Size = new Size(138, 63);
            pictureBox22.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox22.TabIndex = 217;
            pictureBox22.TabStop = false;
            // 
            // RoznicaTuszkaElement3
            // 
            RoznicaTuszkaElement3.BackColor = SystemColors.ButtonFace;
            RoznicaTuszkaElement3.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            RoznicaTuszkaElement3.ForeColor = Color.FromArgb(128, 255, 128);
            RoznicaTuszkaElement3.Location = new Point(766, 72);
            RoznicaTuszkaElement3.Multiline = true;
            RoznicaTuszkaElement3.Name = "RoznicaTuszkaElement3";
            RoznicaTuszkaElement3.Size = new Size(207, 63);
            RoznicaTuszkaElement3.TabIndex = 216;
            RoznicaTuszkaElement3.TextAlign = HorizontalAlignment.Center;
            // 
            // textBox8
            // 
            textBox8.BackColor = SystemColors.ControlLight;
            textBox8.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            textBox8.Location = new Point(1503, 376);
            textBox8.Multiline = true;
            textBox8.Name = "textBox8";
            textBox8.Size = new Size(206, 61);
            textBox8.TabIndex = 242;
            textBox8.TextAlign = HorizontalAlignment.Center;
            // 
            // textBox9
            // 
            textBox9.BackColor = SystemColors.ControlLight;
            textBox9.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            textBox9.Location = new Point(1503, 307);
            textBox9.Multiline = true;
            textBox9.Name = "textBox9";
            textBox9.Size = new Size(206, 61);
            textBox9.TabIndex = 241;
            textBox9.TextAlign = HorizontalAlignment.Center;
            // 
            // textBox10
            // 
            textBox10.BackColor = SystemColors.ControlLight;
            textBox10.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            textBox10.Location = new Point(1503, 241);
            textBox10.Multiline = true;
            textBox10.Name = "textBox10";
            textBox10.Size = new Size(206, 61);
            textBox10.TabIndex = 240;
            textBox10.TextAlign = HorizontalAlignment.Center;
            // 
            // textBox11
            // 
            textBox11.BackColor = SystemColors.ControlLight;
            textBox11.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            textBox11.Location = new Point(1503, 173);
            textBox11.Multiline = true;
            textBox11.Name = "textBox11";
            textBox11.Size = new Size(206, 61);
            textBox11.TabIndex = 239;
            textBox11.TextAlign = HorizontalAlignment.Center;
            // 
            // textBox12
            // 
            textBox12.BackColor = SystemColors.ControlLight;
            textBox12.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            textBox12.Location = new Point(1503, 106);
            textBox12.Multiline = true;
            textBox12.Name = "textBox12";
            textBox12.Size = new Size(206, 61);
            textBox12.TabIndex = 238;
            textBox12.TextAlign = HorizontalAlignment.Center;
            // 
            // textBox13
            // 
            textBox13.BackColor = SystemColors.ControlLight;
            textBox13.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            textBox13.Location = new Point(1715, 107);
            textBox13.Multiline = true;
            textBox13.Name = "textBox13";
            textBox13.Size = new Size(111, 61);
            textBox13.TabIndex = 237;
            textBox13.TextAlign = HorizontalAlignment.Center;
            // 
            // textBox14
            // 
            textBox14.BackColor = SystemColors.ControlLight;
            textBox14.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            textBox14.Location = new Point(1715, 443);
            textBox14.Multiline = true;
            textBox14.Name = "textBox14";
            textBox14.Size = new Size(111, 61);
            textBox14.TabIndex = 236;
            textBox14.TextAlign = HorizontalAlignment.Center;
            // 
            // textBox15
            // 
            textBox15.BackColor = SystemColors.ControlLight;
            textBox15.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            textBox15.Location = new Point(1715, 376);
            textBox15.Multiline = true;
            textBox15.Name = "textBox15";
            textBox15.Size = new Size(111, 61);
            textBox15.TabIndex = 235;
            textBox15.TextAlign = HorizontalAlignment.Center;
            // 
            // textBox16
            // 
            textBox16.BackColor = SystemColors.ControlLight;
            textBox16.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            textBox16.Location = new Point(1715, 307);
            textBox16.Multiline = true;
            textBox16.Name = "textBox16";
            textBox16.Size = new Size(111, 61);
            textBox16.TabIndex = 234;
            textBox16.TextAlign = HorizontalAlignment.Center;
            // 
            // textBox17
            // 
            textBox17.BackColor = SystemColors.ControlLight;
            textBox17.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            textBox17.Location = new Point(1715, 173);
            textBox17.Multiline = true;
            textBox17.Name = "textBox17";
            textBox17.Size = new Size(111, 61);
            textBox17.TabIndex = 233;
            textBox17.TextAlign = HorizontalAlignment.Center;
            // 
            // textBox18
            // 
            textBox18.BackColor = SystemColors.ControlLight;
            textBox18.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            textBox18.Location = new Point(1715, 242);
            textBox18.Multiline = true;
            textBox18.Name = "textBox18";
            textBox18.Size = new Size(111, 61);
            textBox18.TabIndex = 232;
            textBox18.TextAlign = HorizontalAlignment.Center;
            // 
            // label57
            // 
            label57.Font = new Font("Segoe UI Light", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            label57.Location = new Point(1832, 446);
            label57.Name = "label57";
            label57.Size = new Size(130, 60);
            label57.TabIndex = 231;
            label57.Text = "Oczyszczanie fileta";
            label57.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // textBox19
            // 
            textBox19.BackColor = SystemColors.ControlLight;
            textBox19.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            textBox19.Location = new Point(1503, 443);
            textBox19.Multiline = true;
            textBox19.Name = "textBox19";
            textBox19.Size = new Size(206, 61);
            textBox19.TabIndex = 230;
            textBox19.TextAlign = HorizontalAlignment.Center;
            // 
            // pictureBox23
            // 
            pictureBox23.Image = (Image)resources.GetObject("pictureBox23.Image");
            pictureBox23.Location = new Point(1361, 584);
            pictureBox23.Name = "pictureBox23";
            pictureBox23.Size = new Size(136, 61);
            pictureBox23.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox23.TabIndex = 229;
            pictureBox23.TabStop = false;
            // 
            // label58
            // 
            label58.Font = new Font("Segoe UI Light", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            label58.Location = new Point(1832, 379);
            label58.Name = "label58";
            label58.Size = new Size(130, 60);
            label58.TabIndex = 228;
            label58.Text = "Odkrojenie tub";
            label58.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // pictureBox24
            // 
            pictureBox24.Image = (Image)resources.GetObject("pictureBox24.Image");
            pictureBox24.Location = new Point(1361, 308);
            pictureBox24.Name = "pictureBox24";
            pictureBox24.Size = new Size(136, 61);
            pictureBox24.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox24.TabIndex = 227;
            pictureBox24.TabStop = false;
            // 
            // label59
            // 
            label59.Font = new Font("Segoe UI Light", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            label59.Location = new Point(1832, 243);
            label59.Name = "label59";
            label59.Size = new Size(130, 60);
            label59.TabIndex = 226;
            label59.Text = "Oczyszczanie skrzydełek";
            label59.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // label60
            // 
            label60.Font = new Font("Segoe UI Light", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            label60.Location = new Point(1832, 310);
            label60.Name = "label60";
            label60.Size = new Size(130, 60);
            label60.TabIndex = 225;
            label60.Text = "Zawieszanie tub";
            label60.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // label61
            // 
            label61.Font = new Font("Segoe UI Light", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            label61.Location = new Point(1832, 176);
            label61.Name = "label61";
            label61.Size = new Size(130, 60);
            label61.TabIndex = 224;
            label61.Text = "Segregowanie skrzydeł i ćwiartek";
            label61.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // label62
            // 
            label62.Font = new Font("Segoe UI Light", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            label62.Location = new Point(1832, 109);
            label62.Name = "label62";
            label62.Size = new Size(130, 60);
            label62.TabIndex = 223;
            label62.Text = "Zawieszanie tuszki na dzielarke";
            label62.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // pictureBox25
            // 
            pictureBox25.Image = (Image)resources.GetObject("pictureBox25.Image");
            pictureBox25.Location = new Point(1361, 443);
            pictureBox25.Name = "pictureBox25";
            pictureBox25.Size = new Size(136, 61);
            pictureBox25.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox25.TabIndex = 222;
            pictureBox25.TabStop = false;
            // 
            // pictureBox26
            // 
            pictureBox26.Image = (Image)resources.GetObject("pictureBox26.Image");
            pictureBox26.Location = new Point(1361, 375);
            pictureBox26.Name = "pictureBox26";
            pictureBox26.Size = new Size(136, 61);
            pictureBox26.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox26.TabIndex = 221;
            pictureBox26.TabStop = false;
            // 
            // pictureBox27
            // 
            pictureBox27.Image = (Image)resources.GetObject("pictureBox27.Image");
            pictureBox27.Location = new Point(1361, 107);
            pictureBox27.Name = "pictureBox27";
            pictureBox27.Size = new Size(136, 61);
            pictureBox27.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox27.TabIndex = 220;
            pictureBox27.TabStop = false;
            // 
            // pictureBox28
            // 
            pictureBox28.Image = (Image)resources.GetObject("pictureBox28.Image");
            pictureBox28.Location = new Point(1361, 512);
            pictureBox28.Name = "pictureBox28";
            pictureBox28.Size = new Size(136, 61);
            pictureBox28.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox28.TabIndex = 219;
            pictureBox28.TabStop = false;
            // 
            // textBox20
            // 
            textBox20.BackColor = SystemColors.ControlLight;
            textBox20.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            textBox20.Location = new Point(1715, 512);
            textBox20.Multiline = true;
            textBox20.Name = "textBox20";
            textBox20.Size = new Size(111, 61);
            textBox20.TabIndex = 246;
            textBox20.TextAlign = HorizontalAlignment.Center;
            // 
            // label63
            // 
            label63.Font = new Font("Segoe UI Light", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            label63.Location = new Point(1832, 515);
            label63.Name = "label63";
            label63.Size = new Size(130, 60);
            label63.TabIndex = 245;
            label63.Text = "Ważenie elementów";
            label63.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // textBox21
            // 
            textBox21.BackColor = SystemColors.ControlLight;
            textBox21.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            textBox21.Location = new Point(1503, 512);
            textBox21.Multiline = true;
            textBox21.Name = "textBox21";
            textBox21.Size = new Size(206, 61);
            textBox21.TabIndex = 244;
            textBox21.TextAlign = HorizontalAlignment.Center;
            // 
            // pictureBox30
            // 
            pictureBox30.Image = (Image)resources.GetObject("pictureBox30.Image");
            pictureBox30.Location = new Point(1361, 173);
            pictureBox30.Name = "pictureBox30";
            pictureBox30.Size = new Size(136, 61);
            pictureBox30.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox30.TabIndex = 243;
            pictureBox30.TabStop = false;
            // 
            // textBox22
            // 
            textBox22.BackColor = SystemColors.ControlLight;
            textBox22.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            textBox22.Location = new Point(1715, 581);
            textBox22.Multiline = true;
            textBox22.Name = "textBox22";
            textBox22.Size = new Size(111, 61);
            textBox22.TabIndex = 250;
            textBox22.TextAlign = HorizontalAlignment.Center;
            // 
            // label64
            // 
            label64.Font = new Font("Segoe UI Light", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            label64.Location = new Point(1832, 584);
            label64.Name = "label64";
            label64.Size = new Size(130, 60);
            label64.TabIndex = 249;
            label64.Text = "Zużycie energi dzielarki";
            label64.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // textBox23
            // 
            textBox23.BackColor = SystemColors.ControlLight;
            textBox23.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            textBox23.Location = new Point(1503, 581);
            textBox23.Multiline = true;
            textBox23.Name = "textBox23";
            textBox23.Size = new Size(206, 61);
            textBox23.TabIndex = 248;
            textBox23.TextAlign = HorizontalAlignment.Center;
            // 
            // pictureBox31
            // 
            pictureBox31.Image = (Image)resources.GetObject("pictureBox31.Image");
            pictureBox31.Location = new Point(1361, 241);
            pictureBox31.Name = "pictureBox31";
            pictureBox31.Size = new Size(136, 61);
            pictureBox31.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox31.TabIndex = 247;
            pictureBox31.TabStop = false;
            // 
            // label65
            // 
            label65.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            label65.Location = new Point(1361, 70);
            label65.Name = "label65";
            label65.Size = new Size(348, 34);
            label65.TabIndex = 251;
            label65.Text = "Obliczanie kosztu krojenia";
            label65.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // CenaTuszka1
            // 
            CenaTuszka1.BackColor = Color.White;
            CenaTuszka1.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            CenaTuszka1.Location = new Point(665, 39);
            CenaTuszka1.Name = "CenaTuszka1";
            CenaTuszka1.Size = new Size(59, 32);
            CenaTuszka1.TabIndex = 252;
            CenaTuszka1.TextAlign = HorizontalAlignment.Center;
            // 
            // CenaElementyKrojenie1
            // 
            CenaElementyKrojenie1.BackColor = Color.White;
            CenaElementyKrojenie1.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            CenaElementyKrojenie1.Location = new Point(665, 313);
            CenaElementyKrojenie1.Name = "CenaElementyKrojenie1";
            CenaElementyKrojenie1.Size = new Size(59, 32);
            CenaElementyKrojenie1.TabIndex = 253;
            CenaElementyKrojenie1.TextAlign = HorizontalAlignment.Center;
            // 
            // CenaElementy1
            // 
            CenaElementy1.BackColor = Color.White;
            CenaElementy1.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            CenaElementy1.Location = new Point(665, 175);
            CenaElementy1.Name = "CenaElementy1";
            CenaElementy1.Size = new Size(59, 32);
            CenaElementy1.TabIndex = 254;
            CenaElementy1.TextAlign = HorizontalAlignment.Center;
            // 
            // CenaElementyPoMrozeniu1
            // 
            CenaElementyPoMrozeniu1.BackColor = Color.White;
            CenaElementyPoMrozeniu1.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            CenaElementyPoMrozeniu1.Location = new Point(666, 575);
            CenaElementyPoMrozeniu1.Name = "CenaElementyPoMrozeniu1";
            CenaElementyPoMrozeniu1.Size = new Size(59, 32);
            CenaElementyPoMrozeniu1.TabIndex = 255;
            CenaElementyPoMrozeniu1.TextAlign = HorizontalAlignment.Center;
            // 
            // CenaTuszka2
            // 
            CenaTuszka2.BackColor = Color.White;
            CenaTuszka2.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            CenaTuszka2.Location = new Point(666, 822);
            CenaTuszka2.Name = "CenaTuszka2";
            CenaTuszka2.Size = new Size(59, 32);
            CenaTuszka2.TabIndex = 256;
            CenaTuszka2.TextAlign = HorizontalAlignment.Center;
            // 
            // CenyElementowPoObnizeniuMroz2
            // 
            CenyElementowPoObnizeniuMroz2.BackColor = Color.White;
            CenyElementowPoObnizeniuMroz2.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            CenyElementowPoObnizeniuMroz2.Location = new Point(666, 885);
            CenyElementowPoObnizeniuMroz2.Name = "CenyElementowPoObnizeniuMroz2";
            CenyElementowPoObnizeniuMroz2.Size = new Size(59, 32);
            CenyElementowPoObnizeniuMroz2.TabIndex = 257;
            CenyElementowPoObnizeniuMroz2.TextAlign = HorizontalAlignment.Center;
            // 
            // CenaStrataSuma1
            // 
            CenaStrataSuma1.BackColor = Color.White;
            CenaStrataSuma1.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            CenaStrataSuma1.Location = new Point(666, 954);
            CenaStrataSuma1.Name = "CenaStrataSuma1";
            CenaStrataSuma1.Size = new Size(59, 32);
            CenaStrataSuma1.TabIndex = 258;
            CenaStrataSuma1.TextAlign = HorizontalAlignment.Center;
            // 
            // CenyElementowPoObnizeniuMroz1
            // 
            CenyElementowPoObnizeniuMroz1.BackColor = Color.White;
            CenyElementowPoObnizeniuMroz1.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            CenyElementowPoObnizeniuMroz1.Location = new Point(666, 713);
            CenyElementowPoObnizeniuMroz1.Name = "CenyElementowPoObnizeniuMroz1";
            CenyElementowPoObnizeniuMroz1.Size = new Size(59, 32);
            CenyElementowPoObnizeniuMroz1.TabIndex = 259;
            CenyElementowPoObnizeniuMroz1.TextAlign = HorizontalAlignment.Center;
            // 
            // Filet2Wydajnosc
            // 
            Filet2Wydajnosc.BackColor = Color.White;
            Filet2Wydajnosc.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            Filet2Wydajnosc.Location = new Point(78, 104);
            Filet2Wydajnosc.Name = "Filet2Wydajnosc";
            Filet2Wydajnosc.Size = new Size(57, 29);
            Filet2Wydajnosc.TabIndex = 264;
            Filet2Wydajnosc.TextAlign = HorizontalAlignment.Center;
            Filet2Wydajnosc.TextChanged += Filet2Wydajnosc_TextChanged;
            // 
            // label66
            // 
            label66.Location = new Point(1, 104);
            label66.Name = "label66";
            label66.Size = new Size(71, 25);
            label66.TabIndex = 263;
            label66.Text = "Filet II";
            label66.TextAlign = ContentAlignment.MiddleRight;
            // 
            // Filet2Wartosc
            // 
            Filet2Wartosc.BackColor = Color.White;
            Filet2Wartosc.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            Filet2Wartosc.Location = new Point(316, 104);
            Filet2Wartosc.Name = "Filet2Wartosc";
            Filet2Wartosc.Size = new Size(117, 29);
            Filet2Wartosc.TabIndex = 262;
            Filet2Wartosc.TextAlign = HorizontalAlignment.Center;
            Filet2Wartosc.TextChanged += Filet2Wartosc_TextChanged;
            // 
            // Filet2Cena
            // 
            Filet2Cena.BackColor = Color.White;
            Filet2Cena.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            Filet2Cena.Location = new Point(253, 104);
            Filet2Cena.Name = "Filet2Cena";
            Filet2Cena.Size = new Size(57, 29);
            Filet2Cena.TabIndex = 261;
            Filet2Cena.TextAlign = HorizontalAlignment.Center;
            Filet2Cena.TextChanged += Filet2Cena_TextChanged;
            // 
            // Filet2KG
            // 
            Filet2KG.BackColor = Color.White;
            Filet2KG.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            Filet2KG.Location = new Point(141, 104);
            Filet2KG.Name = "Filet2KG";
            Filet2KG.Size = new Size(106, 29);
            Filet2KG.TabIndex = 260;
            Filet2KG.TextAlign = HorizontalAlignment.Center;
            Filet2KG.TextChanged += Filet2KG_TextChanged;
            // 
            // Cwiartka2Wydajnosc
            // 
            Cwiartka2Wydajnosc.BackColor = Color.White;
            Cwiartka2Wydajnosc.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            Cwiartka2Wydajnosc.Location = new Point(78, 152);
            Cwiartka2Wydajnosc.Name = "Cwiartka2Wydajnosc";
            Cwiartka2Wydajnosc.Size = new Size(57, 29);
            Cwiartka2Wydajnosc.TabIndex = 269;
            Cwiartka2Wydajnosc.TextAlign = HorizontalAlignment.Center;
            Cwiartka2Wydajnosc.TextChanged += Cwiartka2Wydajnosc_TextChanged;
            // 
            // label67
            // 
            label67.Location = new Point(1, 152);
            label67.Name = "label67";
            label67.Size = new Size(71, 25);
            label67.TabIndex = 268;
            label67.Text = "Ćwiartka II";
            label67.TextAlign = ContentAlignment.MiddleRight;
            // 
            // Cwiartka2Wartosc
            // 
            Cwiartka2Wartosc.BackColor = Color.White;
            Cwiartka2Wartosc.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            Cwiartka2Wartosc.Location = new Point(316, 152);
            Cwiartka2Wartosc.Name = "Cwiartka2Wartosc";
            Cwiartka2Wartosc.Size = new Size(117, 29);
            Cwiartka2Wartosc.TabIndex = 267;
            Cwiartka2Wartosc.TextAlign = HorizontalAlignment.Center;
            Cwiartka2Wartosc.TextChanged += Cwiartka2Wartosc_TextChanged;
            // 
            // Cwiartka2Cena
            // 
            Cwiartka2Cena.BackColor = Color.White;
            Cwiartka2Cena.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            Cwiartka2Cena.Location = new Point(253, 152);
            Cwiartka2Cena.Name = "Cwiartka2Cena";
            Cwiartka2Cena.Size = new Size(57, 29);
            Cwiartka2Cena.TabIndex = 266;
            Cwiartka2Cena.TextAlign = HorizontalAlignment.Center;
            Cwiartka2Cena.TextChanged += Cwiartka2Cena_TextChanged;
            // 
            // Cwiartka2KG
            // 
            Cwiartka2KG.BackColor = Color.White;
            Cwiartka2KG.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            Cwiartka2KG.Location = new Point(141, 152);
            Cwiartka2KG.Name = "Cwiartka2KG";
            Cwiartka2KG.Size = new Size(106, 29);
            Cwiartka2KG.TabIndex = 265;
            Cwiartka2KG.TextAlign = HorizontalAlignment.Center;
            Cwiartka2KG.TextChanged += Cwiartka2KG_TextChanged;
            // 
            // Skrzydlo2Wydajnosc
            // 
            Skrzydlo2Wydajnosc.BackColor = Color.White;
            Skrzydlo2Wydajnosc.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            Skrzydlo2Wydajnosc.Location = new Point(78, 197);
            Skrzydlo2Wydajnosc.Name = "Skrzydlo2Wydajnosc";
            Skrzydlo2Wydajnosc.Size = new Size(57, 29);
            Skrzydlo2Wydajnosc.TabIndex = 274;
            Skrzydlo2Wydajnosc.TextAlign = HorizontalAlignment.Center;
            Skrzydlo2Wydajnosc.TextChanged += Skrzydlo2Wydajnosc_TextChanged;
            // 
            // label68
            // 
            label68.Location = new Point(1, 197);
            label68.Name = "label68";
            label68.Size = new Size(71, 25);
            label68.TabIndex = 273;
            label68.Text = "Skrzydlo II";
            label68.TextAlign = ContentAlignment.MiddleRight;
            // 
            // Skrzydlo2Wartosc
            // 
            Skrzydlo2Wartosc.BackColor = Color.White;
            Skrzydlo2Wartosc.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            Skrzydlo2Wartosc.Location = new Point(316, 197);
            Skrzydlo2Wartosc.Name = "Skrzydlo2Wartosc";
            Skrzydlo2Wartosc.Size = new Size(117, 29);
            Skrzydlo2Wartosc.TabIndex = 272;
            Skrzydlo2Wartosc.TextAlign = HorizontalAlignment.Center;
            // 
            // Skrzydlo2Cena
            // 
            Skrzydlo2Cena.BackColor = Color.White;
            Skrzydlo2Cena.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            Skrzydlo2Cena.Location = new Point(253, 197);
            Skrzydlo2Cena.Name = "Skrzydlo2Cena";
            Skrzydlo2Cena.Size = new Size(57, 29);
            Skrzydlo2Cena.TabIndex = 271;
            Skrzydlo2Cena.TextAlign = HorizontalAlignment.Center;
            Skrzydlo2Cena.TextChanged += Skrzydlo2Cena_TextChanged;
            // 
            // Skrzydlo2KG
            // 
            Skrzydlo2KG.BackColor = Color.White;
            Skrzydlo2KG.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            Skrzydlo2KG.Location = new Point(141, 197);
            Skrzydlo2KG.Name = "Skrzydlo2KG";
            Skrzydlo2KG.Size = new Size(106, 29);
            Skrzydlo2KG.TabIndex = 270;
            Skrzydlo2KG.TextAlign = HorizontalAlignment.Center;
            Skrzydlo2KG.TextChanged += Skrzydlo2KG_TextChanged;
            // 
            // CenaRoznicaTuszkaElementy
            // 
            CenaRoznicaTuszkaElementy.BackColor = Color.White;
            CenaRoznicaTuszkaElementy.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            CenaRoznicaTuszkaElementy.Location = new Point(665, 108);
            CenaRoznicaTuszkaElementy.Name = "CenaRoznicaTuszkaElementy";
            CenaRoznicaTuszkaElementy.Size = new Size(59, 32);
            CenaRoznicaTuszkaElementy.TabIndex = 275;
            CenaRoznicaTuszkaElementy.TextAlign = HorizontalAlignment.Center;
            // 
            // CenaKrojenia
            // 
            CenaKrojenia.BackColor = Color.White;
            CenaKrojenia.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            CenaKrojenia.Location = new Point(665, 246);
            CenaKrojenia.Name = "CenaKrojenia";
            CenaKrojenia.Size = new Size(59, 32);
            CenaKrojenia.TabIndex = 276;
            CenaKrojenia.TextAlign = HorizontalAlignment.Center;
            // 
            // CenaMrozenia
            // 
            CenaMrozenia.BackColor = Color.White;
            CenaMrozenia.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            CenaMrozenia.Location = new Point(666, 510);
            CenaMrozenia.Name = "CenaMrozenia";
            CenaMrozenia.Size = new Size(59, 32);
            CenaMrozenia.TabIndex = 277;
            CenaMrozenia.TextAlign = HorizontalAlignment.Center;
            // 
            // RoznicaObnizeniaCeny
            // 
            RoznicaObnizeniaCeny.BackColor = Color.White;
            RoznicaObnizeniaCeny.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            RoznicaObnizeniaCeny.Location = new Point(666, 648);
            RoznicaObnizeniaCeny.Name = "RoznicaObnizeniaCeny";
            RoznicaObnizeniaCeny.Size = new Size(59, 32);
            RoznicaObnizeniaCeny.TabIndex = 278;
            RoznicaObnizeniaCeny.TextAlign = HorizontalAlignment.Center;
            // 
            // CenaRoznicaTuszkaElementyPokrojone
            // 
            CenaRoznicaTuszkaElementyPokrojone.BackColor = Color.White;
            CenaRoznicaTuszkaElementyPokrojone.Font = new Font("Segoe UI", 11.25F, FontStyle.Bold, GraphicsUnit.Point);
            CenaRoznicaTuszkaElementyPokrojone.Location = new Point(665, 384);
            CenaRoznicaTuszkaElementyPokrojone.Name = "CenaRoznicaTuszkaElementyPokrojone";
            CenaRoznicaTuszkaElementyPokrojone.Size = new Size(59, 32);
            CenaRoznicaTuszkaElementyPokrojone.TabIndex = 282;
            CenaRoznicaTuszkaElementyPokrojone.TextAlign = HorizontalAlignment.Center;
            // 
            // label69
            // 
            label69.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            label69.ForeColor = Color.Red;
            label69.Location = new Point(979, 348);
            label69.Name = "label69";
            label69.Size = new Size(215, 63);
            label69.TabIndex = 281;
            label69.Text = "Roznica miedzy Tuszką a Elementami po kosztach";
            label69.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // KosztSprzedazyPokrojonego
            // 
            KosztSprzedazyPokrojonego.BackColor = SystemColors.ButtonFace;
            KosztSprzedazyPokrojonego.Font = new Font("Microsoft Sans Serif", 20.25F, FontStyle.Regular, GraphicsUnit.Point);
            KosztSprzedazyPokrojonego.ForeColor = Color.Red;
            KosztSprzedazyPokrojonego.Location = new Point(766, 348);
            KosztSprzedazyPokrojonego.Multiline = true;
            KosztSprzedazyPokrojonego.Name = "KosztSprzedazyPokrojonego";
            KosztSprzedazyPokrojonego.Size = new Size(207, 63);
            KosztSprzedazyPokrojonego.TabIndex = 280;
            KosztSprzedazyPokrojonego.TextAlign = HorizontalAlignment.Center;
            // 
            // pictureBox32
            // 
            pictureBox32.Image = (Image)resources.GetObject("pictureBox32.Image");
            pictureBox32.Location = new Point(623, 348);
            pictureBox32.Name = "pictureBox32";
            pictureBox32.Size = new Size(137, 63);
            pictureBox32.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox32.TabIndex = 279;
            pictureBox32.TabStop = false;
            // 
            // pictureBox33
            // 
            pictureBox33.Image = (Image)resources.GetObject("pictureBox33.Image");
            pictureBox33.Location = new Point(439, 1);
            pictureBox33.Name = "pictureBox33";
            pictureBox33.Size = new Size(89, 35);
            pictureBox33.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox33.TabIndex = 283;
            pictureBox33.TabStop = false;
            // 
            // OdswiezButton
            // 
            OdswiezButton.Location = new Point(439, 70);
            OdswiezButton.Name = "OdswiezButton";
            OdswiezButton.Size = new Size(75, 23);
            OdswiezButton.TabIndex = 286;
            OdswiezButton.Text = "Ceny";
            OdswiezButton.UseVisualStyleBackColor = true;
            OdswiezButton.Click += OdswiezButton_Click;
            // 
            // dateTimePicker2
            // 
            dateTimePicker2.CalendarFont = new Font("Segoe UI Semibold", 15.75F, FontStyle.Bold, GraphicsUnit.Point);
            dateTimePicker2.Location = new Point(1356, 5);
            dateTimePicker2.Name = "dateTimePicker2";
            dateTimePicker2.Size = new Size(228, 26);
            dateTimePicker2.TabIndex = 285;
            // 
            // dateTimePicker1
            // 
            dateTimePicker1.CalendarFont = new Font("Segoe UI Semibold", 15.75F, FontStyle.Bold, GraphicsUnit.Point);
            dateTimePicker1.Location = new Point(1122, 5);
            dateTimePicker1.Name = "dateTimePicker1";
            dateTimePicker1.Size = new Size(228, 26);
            dateTimePicker1.TabIndex = 284;
            // 
            // PokazMroznie
            // 
            PokazMroznie.Location = new Point(1174, 301);
            PokazMroznie.Name = "PokazMroznie";
            PokazMroznie.Size = new Size(75, 23);
            PokazMroznie.TabIndex = 287;
            PokazMroznie.Text = "Mroźnia";
            PokazMroznie.UseVisualStyleBackColor = true;
            PokazMroznie.Click += PokazMroznie_Click;
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Location = new Point(1174, 328);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowHeadersWidth = 51;
            dataGridView1.RowTemplate.Height = 25;
            dataGridView1.Size = new Size(561, 416);
            dataGridView1.TabIndex = 288;
            // 
            // PokazKrojenieMrozenie
            // 
            ClientSize = new Size(1896, 993);
            Controls.Add(dataGridView1);
            Controls.Add(PokazMroznie);
            Controls.Add(OdswiezButton);
            Controls.Add(dateTimePicker2);
            Controls.Add(dateTimePicker1);
            Controls.Add(pictureBox33);
            Controls.Add(CenaRoznicaTuszkaElementyPokrojone);
            Controls.Add(label69);
            Controls.Add(KosztSprzedazyPokrojonego);
            Controls.Add(pictureBox32);
            Controls.Add(RoznicaObnizeniaCeny);
            Controls.Add(CenaMrozenia);
            Controls.Add(CenaKrojenia);
            Controls.Add(CenaRoznicaTuszkaElementy);
            Controls.Add(Skrzydlo2Wydajnosc);
            Controls.Add(label68);
            Controls.Add(Skrzydlo2Wartosc);
            Controls.Add(Skrzydlo2Cena);
            Controls.Add(Skrzydlo2KG);
            Controls.Add(Cwiartka2Wydajnosc);
            Controls.Add(label67);
            Controls.Add(Cwiartka2Wartosc);
            Controls.Add(Cwiartka2Cena);
            Controls.Add(Cwiartka2KG);
            Controls.Add(Filet2Wydajnosc);
            Controls.Add(label66);
            Controls.Add(Filet2Wartosc);
            Controls.Add(Filet2Cena);
            Controls.Add(Filet2KG);
            Controls.Add(CenyElementowPoObnizeniuMroz1);
            Controls.Add(CenaStrataSuma1);
            Controls.Add(CenyElementowPoObnizeniuMroz2);
            Controls.Add(CenaTuszka2);
            Controls.Add(CenaElementyPoMrozeniu1);
            Controls.Add(CenaElementy1);
            Controls.Add(CenaElementyKrojenie1);
            Controls.Add(CenaTuszka1);
            Controls.Add(label65);
            Controls.Add(textBox22);
            Controls.Add(label64);
            Controls.Add(textBox23);
            Controls.Add(pictureBox31);
            Controls.Add(textBox20);
            Controls.Add(label63);
            Controls.Add(textBox21);
            Controls.Add(pictureBox30);
            Controls.Add(textBox8);
            Controls.Add(textBox9);
            Controls.Add(textBox10);
            Controls.Add(textBox11);
            Controls.Add(textBox12);
            Controls.Add(textBox13);
            Controls.Add(textBox14);
            Controls.Add(textBox15);
            Controls.Add(textBox16);
            Controls.Add(textBox17);
            Controls.Add(textBox18);
            Controls.Add(label57);
            Controls.Add(textBox19);
            Controls.Add(pictureBox23);
            Controls.Add(label58);
            Controls.Add(pictureBox24);
            Controls.Add(label59);
            Controls.Add(label60);
            Controls.Add(label61);
            Controls.Add(label62);
            Controls.Add(pictureBox25);
            Controls.Add(pictureBox26);
            Controls.Add(pictureBox27);
            Controls.Add(pictureBox28);
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
            Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
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
            ((System.ComponentModel.ISupportInitialize)pictureBox23).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox24).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox25).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox26).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox27).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox28).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox30).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox31).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox32).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox33).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
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
                double cwiartka2Wydajnosc = PobierzWartosc(Cwiartka2Wydajnosc.Text) / 100;
                double filetWydajnosc = PobierzWartosc(FiletWydajnosc.Text) / 100;
                double filet2Wydajnosc = PobierzWartosc(Filet2Wydajnosc.Text) / 100;
                double korpusWydajnosc = PobierzWartosc(KorpusWydajnosc.Text) / 100;
                double pozostaleWydajnosc = PobierzWartosc(PozostaleWydajnosc.Text) / 100;
                double skrzydloWydajnosc = PobierzWartosc(SkrzydloWydajnosc.Text) / 100;
                double skrzydlo2Wydajnosc = PobierzWartosc(Skrzydlo2Wydajnosc.Text) / 100;

                // Obliczenia dla poszczególnych elementów (wydajność i cena)
                double cwiartkaKG = tuszkaKG * cwiartkaWydajnosc;
                double cwiartka2KG = tuszkaKG * cwiartka2Wydajnosc;
                double filetKG = tuszkaKG * filetWydajnosc;
                double filet2KG = tuszkaKG * filet2Wydajnosc;
                double korpusKG = tuszkaKG * korpusWydajnosc;
                double pozostaleKG = tuszkaKG * pozostaleWydajnosc;
                double skrzydloKG = tuszkaKG * skrzydloWydajnosc;
                double skrzydlo2KG = tuszkaKG * skrzydlo2Wydajnosc;

                double cwiartkaCena = PobierzWartosc(CwiartkaCena.Text);
                double cwiartka2Cena = PobierzWartosc(Cwiartka2Cena.Text);
                double filetCena = PobierzWartosc(FiletCena.Text);
                double filet2Cena = PobierzWartosc(Filet2Cena.Text);
                double korpusCena = PobierzWartosc(KorpusCena.Text);
                double pozostaleCena = PobierzWartosc(PozostaleCena.Text);
                double skrzydloCena = PobierzWartosc(SkrzydloCena.Text);
                double skrzydlo2Cena = PobierzWartosc(Skrzydlo2Cena.Text);


                // Obliczenia wartości każdego elementu
                double cwiartkaWartosc = cwiartkaKG * cwiartkaCena;
                double cwiartka2Wartosc = cwiartka2KG * cwiartka2Cena;
                double filetWartosc = filetKG * filetCena;
                double filet2Wartosc = filet2KG * filet2Cena;
                double korpusWartosc = korpusKG * korpusCena;
                double pozostaleWartosc = pozostaleKG * pozostaleCena;
                double skrzydloWartosc = skrzydloKG * skrzydloCena;
                double skrzydlo2Wartosc = skrzydlo2KG * skrzydlo2Cena;
                double tuszkaWartosc = tuszkaKG * tuszkaCena;

                // Ustawienie wartości i kilogramów w TextBoxach z separatorem tysięcy i 2 miejscami po przecinku
                CwiartkaKG.Text = cwiartkaKG.ToString("N2");
                Cwiartka2KG.Text = cwiartka2KG.ToString("N2");
                FiletKG.Text = filetKG.ToString("N2");
                Filet2KG.Text = filet2KG.ToString("N2");
                KorpusKG.Text = korpusKG.ToString("N2");
                PozostaleKG.Text = pozostaleKG.ToString("N2");
                SkrzydloKG.Text = skrzydloKG.ToString("N2");
                Skrzydlo2KG.Text = skrzydlo2KG.ToString("N2");
                TuszkaKG2.Text = tuszkaKG.ToString("N2");

                CwiartkaWartosc.Text = cwiartkaWartosc.ToString("N2");
                Cwiartka2Wartosc.Text = cwiartka2Wartosc.ToString("N2");
                FiletWartosc.Text = filetWartosc.ToString("N2");
                Filet2Wartosc.Text = filet2Wartosc.ToString("N2");
                KorpusWartosc.Text = korpusWartosc.ToString("N2");
                PozostaleWartosc.Text = pozostaleWartosc.ToString("N2");
                SkrzydloWartosc.Text = skrzydloWartosc.ToString("N2");
                Skrzydlo2Wartosc.Text = skrzydlo2Wartosc.ToString("N2");

                // Suma wartości wszystkich elementów
                double sumaWartosciElementow = cwiartkaWartosc + cwiartka2Wartosc + filetWartosc + filet2Wartosc + korpusWartosc + pozostaleWartosc + skrzydloWartosc + skrzydlo2Wartosc;

                // Oblicz sumy kilogramów i wartości
                double sumaKg = cwiartkaKG + cwiartka2KG + filetKG + filet2KG + korpusKG + pozostaleKG + skrzydloKG + skrzydlo2KG;

                // Suma % wydajnosci
                double sumaWydajnosciElementow = (cwiartkaWydajnosc + cwiartka2Wydajnosc + filetWydajnosc + filet2Wydajnosc + korpusWydajnosc + pozostaleWydajnosc + skrzydloWydajnosc + skrzydlo2Wydajnosc) * 100;

                // Sredniua cena
                double sredniaCenaElementow = sumaWartosciElementow / sumaKg;



                // Ustawienie sumy kilogramów i wartości w TextBoxach z separatorem tysięcy i 2 miejscami po przecinku
                SumaKG.Text = sumaKg.ToString("N2");
                SumaWartosc.Text = sumaWartosciElementow.ToString("N2");
                SumaWartosc2.Text = sumaWartosciElementow.ToString("N2");
                SumaWydajnosc.Text = sumaWydajnosciElementow.ToString("N1") + " %";
                SumaCena.Text = sredniaCenaElementow.ToString("N2");

                double cenaElementow = sumaWartosciElementow / sumaKg;
                CenaElementy1.Text = cenaElementow.ToString("N2");
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


                double cenaTuszki = tuszkaWartosc / tuszkaKG;
                CenaTuszka1.Text = cenaTuszki.ToString("N2") + " zł";
                CenaTuszka2.Text = cenaTuszki.ToString("N2") + " zł";

                double cenaRoznicaTuszkaElementy = cenaElementow - cenaTuszki;
                CenaRoznicaTuszkaElementy.Text = cenaRoznicaTuszkaElementy.ToString("N2") + " zł";

                // Zmienianie koloru czcionki w zależności od wartości
                if (cenaRoznicaTuszkaElementy > 0)
                {
                    CenaRoznicaTuszkaElementy.ForeColor = System.Drawing.Color.Green;  // Zielona czcionka
                    CenaRoznicaTuszkaElementy.ForeColor = System.Drawing.Color.Green;  // Zielona czcionka
                }
                else if (cenaRoznicaTuszkaElementy < 0)
                {
                    CenaRoznicaTuszkaElementy.ForeColor = System.Drawing.Color.Red;  // Czerwona czcionka
                    CenaRoznicaTuszkaElementy.ForeColor = System.Drawing.Color.Red;  // Czerwona czcionka
                }
                else
                {
                    CenaRoznicaTuszkaElementy.ForeColor = System.Drawing.Color.Black;  // Czarna czcionka, jeśli wynik to 0
                    CenaRoznicaTuszkaElementy.ForeColor = System.Drawing.Color.Black;  // Czarna czcionka, jeśli wynik to 0
                }

                double przelicznikKrojenia = PobierzWartosc(WspolczynnikKrojenia.Text);
                double wynikKrojenia = tuszkaKG / przelicznikKrojenia;

                WartoscKrojenia.Text = wynikKrojenia.ToString("N2");
                WartoscKrojenia2.Text = wynikKrojenia.ToString("N2") + " zł";





                double wartoscElementowPoKrojeniu = sumaWartosciElementow - wynikKrojenia;
                WartoscElementowPoKrojeniu.Text = wartoscElementowPoKrojeniu.ToString("N2");
                WartoscElementowPoKrojeniu2.Text = wartoscElementowPoKrojeniu.ToString("N2");

                double cenaElementowPoKrojeniu = wartoscElementowPoKrojeniu / tuszkaKG;
                CenaElementyKrojenie1.Text = cenaElementowPoKrojeniu.ToString("N2");


                double roznicaTuszkaElementPoKrojeniu = wartoscElementowPoKrojeniu - tuszkaWartosc;
                RoznicaTuszkaElement2.Text = roznicaTuszkaElement.ToString("N2") + " zł";
                RoznicaTuszkaElement3.Text = roznicaTuszkaElement.ToString("N2") + " zł";

                // Zmienianie koloru czcionki w zależności od wartości
                if (roznicaTuszkaElement > 0)
                {
                    RoznicaTuszkaElement2.ForeColor = System.Drawing.Color.Green;  // Zielona czcionka
                    RoznicaTuszkaElement3.ForeColor = System.Drawing.Color.Green;  // Zielona czcionka
                }
                else if (roznicaTuszkaElement < 0)
                {
                    RoznicaTuszkaElement2.ForeColor = System.Drawing.Color.Red;  // Czerwona czcionka
                    RoznicaTuszkaElement3.ForeColor = System.Drawing.Color.Red;  // Czerwona czcionka
                }
                else
                {
                    RoznicaTuszkaElement2.ForeColor = System.Drawing.Color.Black;  // Czarna czcionka, jeśli wynik to 0
                    RoznicaTuszkaElement3.ForeColor = System.Drawing.Color.Black;  // Czarna czcionka, jeśli wynik to 0
                }

                double cenaKrojenia = cenaElementowPoKrojeniu - cenaElementow;
                CenaKrojenia.Text = cenaKrojenia.ToString("N2") + " zł";

                // Zmienianie koloru czcionki w zależności od wartości
                if (cenaKrojenia > 0)
                {
                    CenaKrojenia.ForeColor = System.Drawing.Color.Green;  // Zielona czcionka
                    CenaKrojenia.ForeColor = System.Drawing.Color.Green;  // Zielona czcionka
                }
                else if (cenaKrojenia < 0)
                {
                    CenaKrojenia.ForeColor = System.Drawing.Color.Red;  // Czerwona czcionka
                    CenaKrojenia.ForeColor = System.Drawing.Color.Red;  // Czerwona czcionka
                }
                else
                {
                    CenaKrojenia.ForeColor = System.Drawing.Color.Black;  // Czarna czcionka, jeśli wynik to 0
                    CenaKrojenia.ForeColor = System.Drawing.Color.Black;  // Czarna czcionka, jeśli wynik to 0
                }


                double kosztSprzedaniaElementow = tuszkaWartosc - wartoscElementowPoKrojeniu;
                KosztSprzedazyPokrojonego.Text = kosztSprzedaniaElementow.ToString("N2");

                double cenaRoznicyMiedzyTuszkaElementamiPokrojonymi = cenaTuszki - cenaElementowPoKrojeniu;
                CenaRoznicaTuszkaElementyPokrojone.Text = cenaRoznicyMiedzyTuszkaElementamiPokrojonymi.ToString("N2") + " zł";

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
                double tuszkaKG = PobierzWartosc(TuszkaKG.Text);
                double tuszkaCena = PobierzWartosc(TuszkaCena.Text);
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

                double cenaElementowPoKosztachMrozenia = ElementyPoKosztach / tuszkaKG;
                CenaElementyPoMrozeniu1.Text = cenaElementowPoKosztachMrozenia.ToString("N2") + " zł";

                double cenaElementy = Elementy / tuszkaKG;

                double roznicaMrozenia = cenaElementowPoKosztachMrozenia - cenaElementy;
                CenaMrozenia.Text = roznicaMrozenia.ToString("N2") + " zł";

                double obnizenieTowaruMrozonego = ElementyPoKosztach * 0.82;

                double roznicaWoBnizce = ElementyPoKosztach - obnizenieTowaruMrozonego;
                KosztZanizeniaCeny.Text = roznicaWoBnizce.ToString("N2") + " zł";

                // Zmienianie koloru czcionki w zależności od wartości





                WartoscElementowMrozonychPoObnizce.Text = obnizenieTowaruMrozonego.ToString("N2") + " zł";
                WartoscElementowMrozonychPoObnizce2.Text = obnizenieTowaruMrozonego.ToString("N2") + " zł";

                double cenaPoKosztachObnizeniaCeny = obnizenieTowaruMrozonego / tuszkaKG;
                CenyElementowPoObnizeniuMroz1.Text = cenaPoKosztachObnizeniaCeny.ToString("N2") + " zł";
                CenyElementowPoObnizeniuMroz2.Text = cenaPoKosztachObnizeniaCeny.ToString("N2") + " zł";


                double roznicaObnizeniaCeny = cenaPoKosztachObnizeniaCeny - cenaElementowPoKosztachMrozenia;
                RoznicaObnizeniaCeny.Text = roznicaObnizeniaCeny.ToString("N2") + " zł";

                // Zmienianie koloru czcionki w zależności od wartości
                if (roznicaObnizeniaCeny > 0)
                {
                    RoznicaObnizeniaCeny.ForeColor = System.Drawing.Color.Green;  // Zielona czcionka

                }
                else if (roznicaObnizeniaCeny < 0)
                {
                    RoznicaObnizeniaCeny.ForeColor = System.Drawing.Color.Red;  // Czerwona czcionka

                }
                else
                {
                    RoznicaObnizeniaCeny.ForeColor = System.Drawing.Color.Black;  // Czarna czcionka, jeśli wynik to 0

                }

                double tuszkaWartosc = PobierzWartosc(TuszkaWartosc.Text);
                double wynikStraty = tuszkaWartosc - obnizenieTowaruMrozonego;



                Strata.Text = wynikStraty.ToString("N2") + " zł";

                double strataCena = cenaPoKosztachObnizeniaCeny - tuszkaCena;
                CenaStrataSuma1.Text = strataCena.ToString("N2") + " zł";

                // Zmienianie koloru czcionki w zależności od wartości
                if (strataCena > 0)
                {
                    CenaStrataSuma1.ForeColor = System.Drawing.Color.Green;  // Zielona czcionka

                }
                else if (strataCena < 0)
                {
                    CenaStrataSuma1.ForeColor = System.Drawing.Color.Red;  // Czerwona czcionka

                }
                else
                {
                    CenaStrataSuma1.ForeColor = System.Drawing.Color.Black;  // Czarna czcionka, jeśli wynik to 0

                }

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

        private void Filet2Wydajnosc_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void Filet2KG_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void Filet2Cena_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void Filet2Wartosc_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void Cwiartka2Wydajnosc_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void Cwiartka2KG_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void Cwiartka2Cena_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void CwiartkaWartosc_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void Cwiartka2Wartosc_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void Skrzydlo2Wydajnosc_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void Skrzydlo2KG_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void Skrzydlo2Cena_TextChanged(object sender, EventArgs e)
        {
            ObliczWszystko();
        }

        private void OdswiezButton_Click(object sender, EventArgs e)
        {
            // Pobierz daty z DateTimePicker
            string dateFrom = dateTimePicker1.Value.ToString("yyyy-MM-dd");
            string dateTo = dateTimePicker2.Value.ToString("yyyy-MM-dd");

            // Connection string (dostosuj do swojej bazy danych)
            string connectionString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

            // Zapytanie SQL
            string query = @"
        SELECT DP.[kod], 
               SUM(DP.[wartnetto]) / NULLIF(SUM(DP.[ilosc]), 0) AS SredniaCena
        FROM [HANDEL].[HM].[DP] DP 
        INNER JOIN [HANDEL].[HM].[TW] TW ON DP.[idtw] = TW.[id]
        INNER JOIN [HANDEL].[HM].[DK] DK ON DP.[super] = DK.[id]
        WHERE DP.[data] >= @DateFrom 
          AND DP.[data] < DATEADD(DAY, 1, @DateTo)
          AND TW.[katalog] = 67095
        GROUP BY DP.[kod]
        ORDER BY SredniaCena DESC;";

            // Otwórz połączenie z bazą danych i wykonaj zapytanie
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        // Parametry zapytania SQL
                        cmd.Parameters.AddWithValue("@DateFrom", dateFrom);
                        cmd.Parameters.AddWithValue("@DateTo", dateTo);

                        // Wykonaj zapytanie i odczytaj wyniki
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string kod = reader["kod"].ToString();
                                double cena = reader.GetDouble(reader.GetOrdinal("SredniaCena")); // Poprawka: użycie GetDouble

                                // Przypisz wyniki do odpowiednich TextBox
                                switch (kod)
                                {
                                    case "Filet A":
                                        FiletCena.Text = cena.ToString("F2");
                                        break;
                                    case "Filet II":
                                        Filet2Cena.Text = cena.ToString("F2");
                                        break;
                                    case "Ćwiartka":
                                        CwiartkaCena.Text = cena.ToString("F2");
                                        break;
                                    case "Ćwiartka II":
                                        Cwiartka2Cena.Text = cena.ToString("F2");
                                        break;
                                    case "Skrzydło I":
                                        SkrzydloCena.Text = cena.ToString("F2");
                                        break;
                                    case "Skrzydło II":
                                        Skrzydlo2Cena.Text = cena.ToString("F2");
                                        break;
                                    case "Korpus":
                                        KorpusCena.Text = cena.ToString("F2");
                                        break;
                                    case "Kurczak A":
                                        TuszkaCena.Text = cena.ToString("F2");
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd: {ex.Message}");
                }
                ObliczWszystko();
            }
        }

        private void PokazMroznie_Click(object sender, EventArgs e)
        {
            string dateFrom = dateTimePicker1.Value.ToString("yyyy-MM-dd");
            string dateTo = dateTimePicker2.Value.ToString("yyyy-MM-dd");

            string connectionString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

            string query = @"
    WITH 
    ScalonaIlosc AS (
        SELECT
            CASE 
                WHEN MZ.kod LIKE 'Kurczak A%' THEN 'Kurczak A'
                WHEN MZ.kod LIKE 'Korpus%' THEN 'Korpus'
                WHEN MZ.kod LIKE 'Ćwiartka%' THEN 'Ćwiartka'
                WHEN MZ.kod LIKE 'Filet II%' THEN 'Filet II'
                WHEN MZ.kod LIKE 'Filet %' THEN 'Filet A'
                WHEN MZ.kod LIKE 'Skrzydło I%' THEN 'Skrzydło I'
                WHEN MZ.kod LIKE 'Trybowane bez skóry%' THEN 'Trybowane bez skóry'
                WHEN MZ.kod LIKE 'Trybowane ze skórą%' THEN 'Trybowane ze skórą'
                ELSE MZ.kod
            END AS ScalonyKod,
            ABS(SUM(CASE WHEN MZ.ilosc < 0 THEN MZ.ilosc ELSE 0 END)) AS Ilosc
        FROM [HANDEL].[HM].[MG]
        JOIN [HANDEL].[HM].[MZ] ON MG.ID = MZ.super
        WHERE MG.magazyn = 65552
          AND MG.seria IN ('sMM+', 'sMM-', 'sMK-', 'sMK+')
          AND MG.[Data] BETWEEN @DateFrom AND @DateTo
        GROUP BY 
            CASE 
                WHEN MZ.kod LIKE 'Kurczak A%' THEN 'Kurczak A'
                WHEN MZ.kod LIKE 'Korpus%' THEN 'Korpus'
                WHEN MZ.kod LIKE 'Ćwiartka%' THEN 'Ćwiartka'
                WHEN MZ.kod LIKE 'Filet II%' THEN 'Filet II'
                WHEN MZ.kod LIKE 'Filet %' THEN 'Filet A'
                WHEN MZ.kod LIKE 'Skrzydło I%' THEN 'Skrzydło I'
                WHEN MZ.kod LIKE 'Trybowane bez skóry%' THEN 'Trybowane bez skóry'
                WHEN MZ.kod LIKE 'Trybowane ze skórą%' THEN 'Trybowane ze skórą'
                ELSE MZ.kod
            END
    ),
    CenyTowarow AS (
        SELECT 
            DP.kod AS KodTowaru,
            ROUND(SUM(DP.wartnetto) / NULLIF(SUM(DP.ilosc), 0), 2) AS SredniaCena
        FROM [HANDEL].[HM].[DP] DP 
        INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.id
        INNER JOIN [HANDEL].[HM].[DK] DK ON DP.super = DK.id
        WHERE DP.data >= @DateFrom 
          AND DP.data < DATEADD(DAY, 1, @DateTo)
          AND TW.katalog = 67095
        GROUP BY DP.kod
    )
    SELECT 
        SI.ScalonyKod,
        SI.Ilosc,
        ROUND(COALESCE(CT.SredniaCena, 0), 2) AS Cena,
        ROUND(SI.Ilosc * COALESCE(CT.SredniaCena, 0), 2) AS Wartosc,
        ROUND(SI.Ilosc * COALESCE(CT.SredniaCena, 0) * 0.82, 2) AS [18%],
        ROUND((SI.Ilosc * COALESCE(CT.SredniaCena, 0) * 0.82) - (SI.Ilosc * COALESCE(CT.SredniaCena, 0)), 2) AS Strata
    FROM ScalonaIlosc SI
    LEFT JOIN CenyTowarow CT ON SI.ScalonyKod = CT.KodTowaru
    ORDER BY Wartosc DESC;
    ";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DateFrom", dateFrom);
                        cmd.Parameters.AddWithValue("@DateTo", dateTo);

                        SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                        DataTable dataTable = new DataTable();
                        adapter.Fill(dataTable);

                        // Usuń wiersze z Wartosc = 0
                        for (int i = dataTable.Rows.Count - 1; i >= 0; i--)
                        {
                            if (decimal.TryParse(dataTable.Rows[i]["Wartosc"].ToString(), out decimal val) && val == 0)
                            {
                                dataTable.Rows.RemoveAt(i);
                            }
                        }

                        // Oblicz sumy
                        decimal sumaIlosc = Convert.ToDecimal(dataTable.Compute("SUM(Ilosc)", string.Empty));
                        decimal sumaWartosc = Convert.ToDecimal(dataTable.Compute("SUM(Wartosc)", string.Empty));
                        decimal suma18 = Convert.ToDecimal(dataTable.Compute("SUM([18%])", string.Empty));
                        decimal sumaStrata = Convert.ToDecimal(dataTable.Compute("SUM(Strata)", string.Empty));
                        decimal sumaCena = sumaIlosc != 0 ? sumaWartosc / sumaIlosc : 0;

                        // Dodaj SUMA jako pierwszy wiersz
                        DataRow summaryRow = dataTable.NewRow();
                        summaryRow["ScalonyKod"] = "SUMA";
                        summaryRow["Ilosc"] = sumaIlosc;
                        summaryRow["Cena"] = sumaCena;
                        summaryRow["Wartosc"] = sumaWartosc;
                        summaryRow["18%"] = suma18;
                        summaryRow["Strata"] = sumaStrata;
                        dataTable.Rows.InsertAt(summaryRow, 0);

                        // Tworzymy tabelę do wyświetlania
                        DataTable formattedTable = new DataTable();
                        formattedTable.Columns.Add("ScalonyKod");
                        formattedTable.Columns.Add("Ilosc");
                        formattedTable.Columns.Add("Cena");
                        formattedTable.Columns.Add("Wartosc");
                        formattedTable.Columns.Add("18%");
                        formattedTable.Columns.Add("Strata");

                        foreach (DataRow row in dataTable.Rows)
                        {
                            string kod = row["ScalonyKod"].ToString();

                            decimal.TryParse(row["Ilosc"]?.ToString(), out decimal ilosc);
                            decimal.TryParse(row["Cena"]?.ToString(), out decimal cena);
                            decimal.TryParse(row["Wartosc"]?.ToString(), out decimal wartosc);
                            decimal.TryParse(row["18%"]?.ToString(), out decimal osiemnascie);
                            decimal.TryParse(row["Strata"]?.ToString(), out decimal strata);

                            formattedTable.Rows.Add(
                                kod,
                                $"{ilosc:N0} kg",
                                $"{cena:N2} zł",
                                $"{wartosc:N2} zł",
                                $"{osiemnascie:N2} zł",
                                $"{strata:N2} zł"
                            );
                        }

                        dataGridView1.DataSource = formattedTable;

                        // Styl siatki
                        dataGridView1.RowHeadersVisible = false;
                        dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                        dataGridView1.DefaultCellStyle.Font = new Font("Arial", 9);
                        dataGridView1.RowTemplate.Height = 16;

                        // Formatowanie wyglądu komórek
                        foreach (DataGridViewRow row in dataGridView1.Rows)
                        {
                            string scalonyKod = row.Cells["ScalonyKod"]?.Value?.ToString();

                            if (scalonyKod == "SUMA")
                            {
                                row.DefaultCellStyle.Font = new Font("Arial", 9, FontStyle.Bold);

                                // Sprawdź wartość Strata w wierszu SUMA i zaznacz na czerwono jeśli ujemna
                                if (row.Cells["Strata"]?.Value != null)
                                {
                                    string raw = row.Cells["Strata"].Value.ToString().Replace(" zł", "").Replace(" ", "").Replace(",", ".");
                                    row.Cells["Strata"].Style.ForeColor = Color.Red;
                                    if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal strataSuma) && strataSuma < 0)
                                    {
                                        row.Cells["Strata"].Style.ForeColor = Color.Red;
                                    }
                                }
                            }
                        }

                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SumaWartosc2_TextChanged(object sender, EventArgs e)
        {

        }
    }
}


