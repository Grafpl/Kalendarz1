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
        private Label label24;
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
        private TextBox textBox2;
        private TextBox SumaWartosc2;
        private Label label21;
        private Label label22;
        private TextBox TuszkaKG2;
        private TextBox WspolczynnikKrojenia;
        private TextBox WartoscKrojenia;
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

        }

        private void InitializeComponent()
        {
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
            label24 = new Label();
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
            textBox2 = new TextBox();
            SumaWartosc2 = new TextBox();
            label21 = new Label();
            label22 = new Label();
            TuszkaKG2 = new TextBox();
            WspolczynnikKrojenia = new TextBox();
            WartoscKrojenia = new TextBox();
            SuspendLayout();
            // 
            // sztuki
            // 
            sztuki.BackColor = System.Drawing.SystemColors.ControlLight;
            sztuki.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            sztuki.Location = new System.Drawing.Point(683, 644);
            sztuki.Name = "sztuki";
            sztuki.Size = new System.Drawing.Size(62, 25);
            sztuki.TabIndex = 26;
            sztuki.TextAlign = HorizontalAlignment.Center;
            // 
            // label11
            // 
            label11.Location = new System.Drawing.Point(594, 594);
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
            sztukNaSzuflade.Location = new System.Drawing.Point(513, 644);
            sztukNaSzuflade.Name = "sztukNaSzuflade";
            sztukNaSzuflade.Size = new System.Drawing.Size(57, 25);
            sztukNaSzuflade.TabIndex = 28;
            sztukNaSzuflade.TextAlign = HorizontalAlignment.Center;
            // 
            // label10
            // 
            label10.Location = new System.Drawing.Point(513, 594);
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
            label1.Location = new System.Drawing.Point(576, 649);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(12, 15);
            label1.TabIndex = 29;
            label1.Text = "*";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(662, 649);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(15, 15);
            label2.TabIndex = 30;
            label2.Text = "=";
            // 
            // textBox1
            // 
            textBox1.BackColor = System.Drawing.SystemColors.ControlLight;
            textBox1.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            textBox1.Location = new System.Drawing.Point(594, 644);
            textBox1.Name = "textBox1";
            textBox1.Size = new System.Drawing.Size(62, 25);
            textBox1.TabIndex = 31;
            textBox1.Text = "264";
            textBox1.TextAlign = HorizontalAlignment.Center;
            // 
            // label3
            // 
            label3.Location = new System.Drawing.Point(683, 591);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(62, 47);
            label3.TabIndex = 32;
            label3.Text = "Deklarowane sztuki na 1 auto";
            label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new System.Drawing.Point(751, 649);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(12, 15);
            label4.TabIndex = 33;
            label4.Text = "*";
            // 
            // obliczeniaAut
            // 
            obliczeniaAut.BackColor = System.Drawing.SystemColors.Window;
            obliczeniaAut.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            obliczeniaAut.Location = new System.Drawing.Point(769, 644);
            obliczeniaAut.Name = "obliczeniaAut";
            obliczeniaAut.Size = new System.Drawing.Size(36, 25);
            obliczeniaAut.TabIndex = 34;
            obliczeniaAut.TextAlign = HorizontalAlignment.Center;
            // 
            // label12
            // 
            label12.Location = new System.Drawing.Point(769, 597);
            label12.Name = "label12";
            label12.Size = new System.Drawing.Size(36, 35);
            label12.TabIndex = 35;
            label12.Text = "Ilość aut";
            label12.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new System.Drawing.Point(811, 649);
            label5.Name = "label5";
            label5.Size = new System.Drawing.Size(15, 15);
            label5.TabIndex = 36;
            label5.Text = "=";
            // 
            // label6
            // 
            label6.Location = new System.Drawing.Point(825, 591);
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
            sumaSztuk.Location = new System.Drawing.Point(832, 644);
            sumaSztuk.Name = "sumaSztuk";
            sumaSztuk.Size = new System.Drawing.Size(86, 25);
            sumaSztuk.TabIndex = 37;
            sumaSztuk.TextAlign = HorizontalAlignment.Center;
            // 
            // buttonZamknij
            // 
            buttonZamknij.BackColor = System.Drawing.Color.IndianRed;
            buttonZamknij.Location = new System.Drawing.Point(843, 715);
            buttonZamknij.Name = "buttonZamknij";
            buttonZamknij.Size = new System.Drawing.Size(75, 23);
            buttonZamknij.TabIndex = 40;
            buttonZamknij.Text = "Anuluj";
            buttonZamknij.UseVisualStyleBackColor = false;
            // 
            // button1
            // 
            button1.BackColor = System.Drawing.Color.Chartreuse;
            button1.Location = new System.Drawing.Point(843, 686);
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
            TuszkaKG.Location = new System.Drawing.Point(138, 43);
            TuszkaKG.Name = "TuszkaKG";
            TuszkaKG.Size = new System.Drawing.Size(106, 27);
            TuszkaKG.TabIndex = 42;
            TuszkaKG.TextAlign = HorizontalAlignment.Center;
            TuszkaKG.TextChanged += TuszkaKG_TextChanged_1;
            // 
            // label7
            // 
            label7.Location = new System.Drawing.Point(138, 13);
            label7.Name = "label7";
            label7.Size = new System.Drawing.Size(106, 27);
            label7.TabIndex = 41;
            label7.Text = "KG";
            label7.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // TuszkaCena
            // 
            TuszkaCena.BackColor = System.Drawing.Color.LightGreen;
            TuszkaCena.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            TuszkaCena.Location = new System.Drawing.Point(250, 43);
            TuszkaCena.Name = "TuszkaCena";
            TuszkaCena.Size = new System.Drawing.Size(57, 27);
            TuszkaCena.TabIndex = 44;
            TuszkaCena.TextAlign = HorizontalAlignment.Center;
            TuszkaCena.TextChanged += TuszkaCena_TextChanged_1;
            // 
            // label8
            // 
            label8.Location = new System.Drawing.Point(250, 13);
            label8.Name = "label8";
            label8.Size = new System.Drawing.Size(57, 27);
            label8.TabIndex = 43;
            label8.Text = "Cena";
            label8.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // TuszkaWartosc
            // 
            TuszkaWartosc.BackColor = System.Drawing.Color.LightGreen;
            TuszkaWartosc.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            TuszkaWartosc.Location = new System.Drawing.Point(313, 43);
            TuszkaWartosc.Name = "TuszkaWartosc";
            TuszkaWartosc.Size = new System.Drawing.Size(117, 27);
            TuszkaWartosc.TabIndex = 46;
            TuszkaWartosc.TextAlign = HorizontalAlignment.Center;
            // 
            // label9
            // 
            label9.Location = new System.Drawing.Point(313, 13);
            label9.Name = "label9";
            label9.Size = new System.Drawing.Size(117, 27);
            label9.TabIndex = 45;
            label9.Text = "Wartość";
            label9.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // FiletWartosc
            // 
            FiletWartosc.BackColor = System.Drawing.Color.White;
            FiletWartosc.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            FiletWartosc.Location = new System.Drawing.Point(313, 112);
            FiletWartosc.Name = "FiletWartosc";
            FiletWartosc.Size = new System.Drawing.Size(117, 25);
            FiletWartosc.TabIndex = 52;
            FiletWartosc.TextAlign = HorizontalAlignment.Center;
            // 
            // FiletCena
            // 
            FiletCena.BackColor = System.Drawing.Color.White;
            FiletCena.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            FiletCena.Location = new System.Drawing.Point(250, 112);
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
            FiletKG.Location = new System.Drawing.Point(138, 112);
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
            CwiartkaWartosc.Location = new System.Drawing.Point(313, 143);
            CwiartkaWartosc.Name = "CwiartkaWartosc";
            CwiartkaWartosc.Size = new System.Drawing.Size(117, 25);
            CwiartkaWartosc.TabIndex = 58;
            CwiartkaWartosc.TextAlign = HorizontalAlignment.Center;
            // 
            // CwiartkaCena
            // 
            CwiartkaCena.BackColor = System.Drawing.Color.White;
            CwiartkaCena.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            CwiartkaCena.Location = new System.Drawing.Point(250, 143);
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
            CwiartkaKG.Location = new System.Drawing.Point(138, 143);
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
            SkrzydloWartosc.Location = new System.Drawing.Point(313, 174);
            SkrzydloWartosc.Name = "SkrzydloWartosc";
            SkrzydloWartosc.Size = new System.Drawing.Size(117, 25);
            SkrzydloWartosc.TabIndex = 64;
            SkrzydloWartosc.TextAlign = HorizontalAlignment.Center;
            // 
            // SkrzydloCena
            // 
            SkrzydloCena.BackColor = System.Drawing.Color.White;
            SkrzydloCena.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            SkrzydloCena.Location = new System.Drawing.Point(250, 174);
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
            SkrzydloKG.Location = new System.Drawing.Point(138, 174);
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
            KorpusWartosc.Location = new System.Drawing.Point(313, 205);
            KorpusWartosc.Name = "KorpusWartosc";
            KorpusWartosc.Size = new System.Drawing.Size(117, 25);
            KorpusWartosc.TabIndex = 70;
            KorpusWartosc.TextAlign = HorizontalAlignment.Center;
            // 
            // KorpusCena
            // 
            KorpusCena.BackColor = System.Drawing.Color.White;
            KorpusCena.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            KorpusCena.Location = new System.Drawing.Point(250, 205);
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
            KorpusKG.Location = new System.Drawing.Point(138, 205);
            KorpusKG.Name = "KorpusKG";
            KorpusKG.Size = new System.Drawing.Size(106, 25);
            KorpusKG.TabIndex = 66;
            KorpusKG.TextAlign = HorizontalAlignment.Center;
            // 
            // label24
            // 
            label24.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label24.Location = new System.Drawing.Point(12, 43);
            label24.Name = "label24";
            label24.Size = new System.Drawing.Size(57, 25);
            label24.TabIndex = 65;
            label24.Text = "Tuszka";
            label24.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label13
            // 
            label13.Location = new System.Drawing.Point(12, 112);
            label13.Name = "label13";
            label13.Size = new System.Drawing.Size(57, 25);
            label13.TabIndex = 71;
            label13.Text = "Filet";
            label13.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label14
            // 
            label14.Location = new System.Drawing.Point(12, 143);
            label14.Name = "label14";
            label14.Size = new System.Drawing.Size(57, 25);
            label14.TabIndex = 72;
            label14.Text = "Ćwiartka";
            label14.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label15
            // 
            label15.Location = new System.Drawing.Point(12, 174);
            label15.Name = "label15";
            label15.Size = new System.Drawing.Size(57, 25);
            label15.TabIndex = 73;
            label15.Text = "Skrzydło";
            label15.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label16
            // 
            label16.Location = new System.Drawing.Point(12, 205);
            label16.Name = "label16";
            label16.Size = new System.Drawing.Size(57, 25);
            label16.TabIndex = 74;
            label16.Text = "Korpus";
            label16.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label17
            // 
            label17.Location = new System.Drawing.Point(12, 236);
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
            PozostaleWartosc.Location = new System.Drawing.Point(313, 236);
            PozostaleWartosc.Name = "PozostaleWartosc";
            PozostaleWartosc.Size = new System.Drawing.Size(117, 25);
            PozostaleWartosc.TabIndex = 77;
            PozostaleWartosc.TextAlign = HorizontalAlignment.Center;
            // 
            // PozostaleCena
            // 
            PozostaleCena.BackColor = System.Drawing.Color.White;
            PozostaleCena.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            PozostaleCena.Location = new System.Drawing.Point(250, 236);
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
            PozostaleKG.Location = new System.Drawing.Point(138, 236);
            PozostaleKG.Name = "PozostaleKG";
            PozostaleKG.Size = new System.Drawing.Size(106, 25);
            PozostaleKG.TabIndex = 75;
            PozostaleKG.TextAlign = HorizontalAlignment.Center;
            // 
            // TuszkaWydajnosc
            // 
            TuszkaWydajnosc.BackColor = System.Drawing.Color.LightGreen;
            TuszkaWydajnosc.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            TuszkaWydajnosc.Location = new System.Drawing.Point(75, 43);
            TuszkaWydajnosc.Name = "TuszkaWydajnosc";
            TuszkaWydajnosc.Size = new System.Drawing.Size(57, 27);
            TuszkaWydajnosc.TabIndex = 79;
            TuszkaWydajnosc.TextAlign = HorizontalAlignment.Center;
            // 
            // label18
            // 
            label18.Location = new System.Drawing.Point(75, 13);
            label18.Name = "label18";
            label18.Size = new System.Drawing.Size(57, 27);
            label18.TabIndex = 80;
            label18.Text = "%";
            label18.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // FiletWydajnosc
            // 
            FiletWydajnosc.BackColor = System.Drawing.Color.White;
            FiletWydajnosc.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            FiletWydajnosc.Location = new System.Drawing.Point(75, 112);
            FiletWydajnosc.Name = "FiletWydajnosc";
            FiletWydajnosc.Size = new System.Drawing.Size(57, 25);
            FiletWydajnosc.TabIndex = 81;
            FiletWydajnosc.TextAlign = HorizontalAlignment.Center;
            // 
            // CwiartkaWydajnosc
            // 
            CwiartkaWydajnosc.BackColor = System.Drawing.Color.White;
            CwiartkaWydajnosc.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            CwiartkaWydajnosc.Location = new System.Drawing.Point(75, 143);
            CwiartkaWydajnosc.Name = "CwiartkaWydajnosc";
            CwiartkaWydajnosc.Size = new System.Drawing.Size(57, 25);
            CwiartkaWydajnosc.TabIndex = 82;
            CwiartkaWydajnosc.TextAlign = HorizontalAlignment.Center;
            // 
            // SkrzydloWydajnosc
            // 
            SkrzydloWydajnosc.BackColor = System.Drawing.Color.White;
            SkrzydloWydajnosc.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            SkrzydloWydajnosc.Location = new System.Drawing.Point(75, 174);
            SkrzydloWydajnosc.Name = "SkrzydloWydajnosc";
            SkrzydloWydajnosc.Size = new System.Drawing.Size(57, 25);
            SkrzydloWydajnosc.TabIndex = 83;
            SkrzydloWydajnosc.TextAlign = HorizontalAlignment.Center;
            // 
            // KorpusWydajnosc
            // 
            KorpusWydajnosc.BackColor = System.Drawing.Color.White;
            KorpusWydajnosc.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            KorpusWydajnosc.Location = new System.Drawing.Point(75, 205);
            KorpusWydajnosc.Name = "KorpusWydajnosc";
            KorpusWydajnosc.Size = new System.Drawing.Size(57, 25);
            KorpusWydajnosc.TabIndex = 84;
            KorpusWydajnosc.TextAlign = HorizontalAlignment.Center;
            // 
            // PozostaleWydajnosc
            // 
            PozostaleWydajnosc.BackColor = System.Drawing.Color.White;
            PozostaleWydajnosc.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            PozostaleWydajnosc.Location = new System.Drawing.Point(75, 236);
            PozostaleWydajnosc.Name = "PozostaleWydajnosc";
            PozostaleWydajnosc.Size = new System.Drawing.Size(57, 25);
            PozostaleWydajnosc.TabIndex = 85;
            PozostaleWydajnosc.TextAlign = HorizontalAlignment.Center;
            // 
            // SumaWydajnosc
            // 
            SumaWydajnosc.BackColor = System.Drawing.Color.IndianRed;
            SumaWydajnosc.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            SumaWydajnosc.Location = new System.Drawing.Point(75, 79);
            SumaWydajnosc.Name = "SumaWydajnosc";
            SumaWydajnosc.Size = new System.Drawing.Size(57, 27);
            SumaWydajnosc.TabIndex = 90;
            SumaWydajnosc.TextAlign = HorizontalAlignment.Center;
            // 
            // label19
            // 
            label19.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label19.Location = new System.Drawing.Point(3, 79);
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
            SumaWartosc.Location = new System.Drawing.Point(313, 79);
            SumaWartosc.Name = "SumaWartosc";
            SumaWartosc.Size = new System.Drawing.Size(117, 27);
            SumaWartosc.TabIndex = 88;
            SumaWartosc.TextAlign = HorizontalAlignment.Center;
            // 
            // SumaCena
            // 
            SumaCena.BackColor = System.Drawing.Color.IndianRed;
            SumaCena.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            SumaCena.Location = new System.Drawing.Point(250, 79);
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
            SumaKG.Location = new System.Drawing.Point(138, 79);
            SumaKG.Name = "SumaKG";
            SumaKG.Size = new System.Drawing.Size(106, 27);
            SumaKG.TabIndex = 86;
            SumaKG.TextAlign = HorizontalAlignment.Center;
            // 
            // textBox2
            // 
            textBox2.BackColor = System.Drawing.Color.White;
            textBox2.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            textBox2.Location = new System.Drawing.Point(436, 79);
            textBox2.Name = "textBox2";
            textBox2.Size = new System.Drawing.Size(66, 27);
            textBox2.TabIndex = 91;
            textBox2.TextAlign = HorizontalAlignment.Center;
            // 
            // SumaWartosc2
            // 
            SumaWartosc2.BackColor = System.Drawing.Color.IndianRed;
            SumaWartosc2.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            SumaWartosc2.Location = new System.Drawing.Point(729, 9);
            SumaWartosc2.Name = "SumaWartosc2";
            SumaWartosc2.Size = new System.Drawing.Size(117, 27);
            SumaWartosc2.TabIndex = 94;
            SumaWartosc2.TextAlign = HorizontalAlignment.Center;
            // 
            // label21
            // 
            label21.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label21.Location = new System.Drawing.Point(658, 11);
            label21.Name = "label21";
            label21.Size = new System.Drawing.Size(66, 25);
            label21.TabIndex = 95;
            label21.Text = "Elementy";
            label21.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label22
            // 
            label22.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label22.Location = new System.Drawing.Point(499, 47);
            label22.Name = "label22";
            label22.Size = new System.Drawing.Size(89, 25);
            label22.TabIndex = 97;
            label22.Text = "Koszt Krojenia";
            label22.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // TuszkaKG2
            // 
            TuszkaKG2.BackColor = System.Drawing.Color.Gainsboro;
            TuszkaKG2.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            TuszkaKG2.Location = new System.Drawing.Point(594, 45);
            TuszkaKG2.Name = "TuszkaKG2";
            TuszkaKG2.Size = new System.Drawing.Size(62, 27);
            TuszkaKG2.TabIndex = 96;
            TuszkaKG2.TextAlign = HorizontalAlignment.Center;
            // 
            // WspolczynnikKrojenia
            // 
            WspolczynnikKrojenia.BackColor = System.Drawing.Color.Gainsboro;
            WspolczynnikKrojenia.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            WspolczynnikKrojenia.Location = new System.Drawing.Point(662, 45);
            WspolczynnikKrojenia.Name = "WspolczynnikKrojenia";
            WspolczynnikKrojenia.Size = new System.Drawing.Size(62, 27);
            WspolczynnikKrojenia.TabIndex = 98;
            WspolczynnikKrojenia.TextAlign = HorizontalAlignment.Center;
            // 
            // WartoscKrojenia
            // 
            WartoscKrojenia.BackColor = System.Drawing.Color.Gainsboro;
            WartoscKrojenia.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            WartoscKrojenia.Location = new System.Drawing.Point(729, 45);
            WartoscKrojenia.Name = "WartoscKrojenia";
            WartoscKrojenia.Size = new System.Drawing.Size(117, 27);
            WartoscKrojenia.TabIndex = 99;
            WartoscKrojenia.TextAlign = HorizontalAlignment.Center;
            // 
            // PokazKrojenieMrozenie
            // 
            ClientSize = new System.Drawing.Size(1397, 737);
            Controls.Add(WartoscKrojenia);
            Controls.Add(WspolczynnikKrojenia);
            Controls.Add(label22);
            Controls.Add(TuszkaKG2);
            Controls.Add(label21);
            Controls.Add(SumaWartosc2);
            Controls.Add(textBox2);
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
            Controls.Add(label24);
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
            Name = "PokazKrojenieMrozenie";
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

                // Ustawienie wartości tuszki w TextBoxie z separatorem tysięcy i 2 miejscami po przecinku
                TuszkaWartosc.Text = tuszkaWartosc.ToString("N2");



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
    }
}

