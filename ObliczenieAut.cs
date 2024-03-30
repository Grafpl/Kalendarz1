using System;
using System.Windows.Forms;
using System.Globalization;

namespace Kalendarz1
{
    public partial class ObliczenieAut : Form
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
        private Label label11;

        public ObliczenieAut(string sztukiNaSzufladeValue, string iloscAutValue, string iloscSztukValue)
        {
            InitializeComponent();

            // Przypisz wartości do właściwości
            sztukiNaSzuflade = sztukiNaSzufladeValue;
            iloscAut = iloscAutValue;
            iloscSztuk = iloscSztukValue;
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
            SuspendLayout();
            // 
            // sztuki
            // 
            sztuki.BackColor = System.Drawing.SystemColors.ControlLight;
            sztuki.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            sztuki.Location = new System.Drawing.Point(182, 62);
            sztuki.Name = "sztuki";
            sztuki.Size = new System.Drawing.Size(62, 25);
            sztuki.TabIndex = 26;
            sztuki.TextAlign = HorizontalAlignment.Center;
            // 
            // label11
            // 
            label11.Location = new System.Drawing.Point(93, 12);
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
            sztukNaSzuflade.Location = new System.Drawing.Point(12, 62);
            sztukNaSzuflade.Name = "sztukNaSzuflade";
            sztukNaSzuflade.Size = new System.Drawing.Size(57, 25);
            sztukNaSzuflade.TabIndex = 28;
            sztukNaSzuflade.TextAlign = HorizontalAlignment.Center;
            sztukNaSzuflade.TextChanged += sztukNaSzuflade_TextChanged;
            // 
            // label10
            // 
            label10.Location = new System.Drawing.Point(12, 12);
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
            label1.Location = new System.Drawing.Point(75, 67);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(12, 15);
            label1.TabIndex = 29;
            label1.Text = "*";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(161, 67);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(15, 15);
            label2.TabIndex = 30;
            label2.Text = "=";
            // 
            // textBox1
            // 
            textBox1.BackColor = System.Drawing.SystemColors.ControlLight;
            textBox1.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            textBox1.Location = new System.Drawing.Point(93, 62);
            textBox1.Name = "textBox1";
            textBox1.Size = new System.Drawing.Size(62, 25);
            textBox1.TabIndex = 31;
            textBox1.Text = "264";
            textBox1.TextAlign = HorizontalAlignment.Center;
            // 
            // label3
            // 
            label3.Location = new System.Drawing.Point(182, 9);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(62, 47);
            label3.TabIndex = 32;
            label3.Text = "Deklarowane sztuki na 1 auto";
            label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new System.Drawing.Point(250, 67);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(12, 15);
            label4.TabIndex = 33;
            label4.Text = "*";
            // 
            // obliczeniaAut
            // 
            obliczeniaAut.BackColor = System.Drawing.SystemColors.Window;
            obliczeniaAut.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            obliczeniaAut.Location = new System.Drawing.Point(268, 62);
            obliczeniaAut.Name = "obliczeniaAut";
            obliczeniaAut.Size = new System.Drawing.Size(36, 25);
            obliczeniaAut.TabIndex = 34;
            obliczeniaAut.TextAlign = HorizontalAlignment.Center;
            // 
            // label12
            // 
            label12.Location = new System.Drawing.Point(268, 15);
            label12.Name = "label12";
            label12.Size = new System.Drawing.Size(36, 35);
            label12.TabIndex = 35;
            label12.Text = "Ilość aut";
            label12.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new System.Drawing.Point(310, 67);
            label5.Name = "label5";
            label5.Size = new System.Drawing.Size(15, 15);
            label5.TabIndex = 36;
            label5.Text = "=";
            // 
            // label6
            // 
            label6.Location = new System.Drawing.Point(324, 9);
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
            sumaSztuk.Location = new System.Drawing.Point(331, 62);
            sumaSztuk.Name = "sumaSztuk";
            sumaSztuk.Size = new System.Drawing.Size(86, 25);
            sumaSztuk.TabIndex = 37;
            sumaSztuk.TextAlign = HorizontalAlignment.Center;
            // 
            // buttonZamknij
            // 
            buttonZamknij.BackColor = System.Drawing.Color.IndianRed;
            buttonZamknij.Location = new System.Drawing.Point(342, 133);
            buttonZamknij.Name = "buttonZamknij";
            buttonZamknij.Size = new System.Drawing.Size(75, 23);
            buttonZamknij.TabIndex = 40;
            buttonZamknij.Text = "Anuluj";
            buttonZamknij.UseVisualStyleBackColor = false;
            buttonZamknij.Click += buttonZamknij_Click_1;
            // 
            // button1
            // 
            button1.BackColor = System.Drawing.Color.Chartreuse;
            button1.Location = new System.Drawing.Point(342, 104);
            button1.Name = "button1";
            button1.Size = new System.Drawing.Size(75, 23);
            button1.TabIndex = 39;
            button1.Text = "Stwórz";
            button1.UseVisualStyleBackColor = false;
            // 
            // ObliczenieAut
            // 
            ClientSize = new System.Drawing.Size(429, 167);
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
            Name = "ObliczenieAut";
            Load += ObliczenieAut_Load_1;
            ResumeLayout(false);
            PerformLayout();
        }

        public string sztukiNaSzuflade { get; set; }
        public string iloscAut { get; set; }
        public string iloscSztuk { get; set; }

        private void ZamknijFormularz()
        {
            // Przypisujemy wartości z kontrolki TextBox do właściwości
            sztukiNaSzuflade = sztukNaSzuflade.Text;
            iloscAut = obliczeniaAut.Text;
            iloscSztuk = sumaSztuk.Text;
        }

        private void buttonZamknij_Click_1(object sender, EventArgs e)
        {
            ZamknijFormularz();
            this.Close(); // Zamknij bieżący formularz
        }
        private void ObliczenieAut_Load_1(object? sender, EventArgs e)
        {
            // Przypisz wartości właściwości do kontrolek TextBox
            sztukNaSzuflade.Text = sztukiNaSzuflade;
            obliczeniaAut.Text = iloscAut;
            sztuki.Text = iloscSztuk;
        }

        private void sztukNaSzuflade_TextChanged(object sender, EventArgs e)
        {
            //obliczenia.ProponowanaIloscNaSkrzynke2(sztukNaSzuflade, sztuki, obliczeniaAut);
            //obliczenia.IleautOblcizenie(obliczeniaAut,sztuki, TextBox wyliczone)
        }
    }
}

