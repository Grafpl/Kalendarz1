using System;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.Diagnostics;

// DODAJ TEN ALIAS:
using Rectangle = System.Drawing.Rectangle;
using iTextRectangle = iTextSharp.text.Rectangle;

namespace Kalendarz1
{
    public partial class OcenaDostawcyForm : Form
    {
        private const string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string _dostawcaId;
        private string _dostawcaNazwa;
        private string _userId;
        private int? _ocenaId;
        
        // Właściwości publiczne
        public int PunktyRazem { get; private set; }
        
        // Kontrolki formularza
        private Panel panelHeader;
        private Label lblTitle;
        private Label lblDostawca;
        private Label lblNumerRaportu;
        private TextBox txtNumerRaportu;
        private Label lblDataOceny;
        private DateTimePicker dpDataOceny;
        
        private TabControl tabControl;
        private TabPage tabSamoocena;
        private TabPage tabListaKontrolna;
        private TabPage tabPodsumowanie;
        
        // Checkboxy dla samooceny (pytania 1-8)
        private CheckBox[] chkSamoocena_TAK = new CheckBox[8];
        private CheckBox[] chkSamoocena_NIE = new CheckBox[8];
        
        // Checkboxy dla listy kontrolnej (pytania 1-20)
        private CheckBox[] chkKontrola_TAK = new CheckBox[20];
        private CheckBox[] chkKontrola_NIE = new CheckBox[20];
        
        // Checkboxy dla dokumentacji
        private CheckBox chkDokumentacja_TAK;
        private CheckBox chkDokumentacja_NIE;
        
        // Podsumowanie
        private TextBox txtPunkty1_5;
        private TextBox txtPunkty6_20;
        private TextBox txtPunktyRazem;
        private TextBox txtUwagi;
        
        // Przyciski
        private Button btnZapisz;
        private Button btnGenerujPDF;
        private Button btnAnuluj;
        
        public OcenaDostawcyForm(string dostawcaId, string dostawcaNazwa, string userId, int? ocenaId = null)
        {
            _dostawcaId = dostawcaId;
            _dostawcaNazwa = dostawcaNazwa;
            _userId = userId;
            _ocenaId = ocenaId;
            
            InitializeComponent();
            LoadData();
        }
        
        private void InitializeComponent()
        {
            this.Text = "📋 Ocena Dostawcy Żywca";
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new System.Drawing.Font("Segoe UI", 9.5f);
            this.BackColor = Color.FromArgb(245, 247, 250);
            
            // Panel nagłówka
            panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                BackColor = Color.FromArgb(52, 73, 94),
                Padding = new Padding(20)
            };
            
            lblTitle = new Label
            {
                Text = "PROCEDURY ZAKŁADOWE - OCENA DOSTAWCÓW",
                Font = new System.Drawing.Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 10)
            };
            
            lblDostawca = new Label
            {
                Text = $"Dostawca: {_dostawcaNazwa} (ID: {_dostawcaId})",
                Font = new System.Drawing.Font("Segoe UI", 11f),
                ForeColor = Color.FromArgb(189, 195, 199),
                AutoSize = true,
                Location = new Point(20, 45)
            };
            
            lblNumerRaportu = new Label
            {
                Text = "Nr raportu:",
                Font = new System.Drawing.Font("Segoe UI", 10f),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(600, 20)
            };
            
            txtNumerRaportu = new TextBox
            {
                Location = new Point(700, 18),
                Width = 150,
                ReadOnly = true,
                BackColor = Color.FromArgb(236, 240, 241)
            };
            
            lblDataOceny = new Label
            {
                Text = "Data oceny:",
                Font = new System.Drawing.Font("Segoe UI", 10f),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(600, 50)
            };
            
            dpDataOceny = new DateTimePicker
            {
                Location = new Point(700, 48),
                Width = 150,
                Format = DateTimePickerFormat.Short
            };
            
            panelHeader.Controls.AddRange(new Control[] { lblTitle, lblDostawca, lblNumerRaportu, txtNumerRaportu, lblDataOceny, dpDataOceny });
            
            // TabControl
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI", 10f)
            };
            
            // Tab 1: Samoocena
            tabSamoocena = new TabPage
            {
                Text = "📝 Samoocena Dostawcy",
                BackColor = Color.White,
                AutoScroll = true
            };
            
            CreateSamoocenaTab();
            
            // Tab 2: Lista kontrolna
            tabListaKontrolna = new TabPage
            {
                Text = "🚚 Lista Kontrolna Audytu",
                BackColor = Color.White,
                AutoScroll = true
            };
            
            CreateListaKontrolnaTab();
            
            // Tab 3: Podsumowanie
            tabPodsumowanie = new TabPage
            {
                Text = "📊 Podsumowanie",
                BackColor = Color.White
            };
            
            CreatePodsumowanieTab();
            
            tabControl.TabPages.AddRange(new TabPage[] { tabSamoocena, tabListaKontrolna, tabPodsumowanie });
            
            // Panel przycisków
            Panel panelButtons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.FromArgb(236, 240, 241),
                Padding = new Padding(20, 10, 20, 10)
            };
            
            btnZapisz = new Button
            {
                Text = "💾 Zapisz",
                Size = new Size(120, 35),
                Location = new Point(20, 12),
                BackColor = Color.FromArgb(39, 174, 96),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnZapisz.FlatAppearance.BorderSize = 0;
            btnZapisz.Click += BtnZapisz_Click;
            
            btnGenerujPDF = new Button
            {
                Text = "📄 Generuj PDF",
                Size = new Size(130, 35),
                Location = new Point(150, 12),
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnGenerujPDF.FlatAppearance.BorderSize = 0;
            btnGenerujPDF.Click += BtnGenerujPDF_Click;
            
            btnAnuluj = new Button
            {
                Text = "❌ Anuluj",
                Size = new Size(110, 35),
                BackColor = Color.FromArgb(149, 165, 166),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnAnuluj.Location = new Point(this.ClientSize.Width - btnAnuluj.Width - 40, 12);
            btnAnuluj.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            btnAnuluj.FlatAppearance.BorderSize = 0;
            btnAnuluj.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            
            panelButtons.Controls.AddRange(new Control[] { btnZapisz, btnGenerujPDF, btnAnuluj });
            
            // Dodaj kontrolki do formularza
            this.Controls.Add(tabControl);
            this.Controls.Add(panelButtons);
            this.Controls.Add(panelHeader);
        }
        
        private void CreateSamoocenaTab()
        {
            string[] pytaniaSamoocena = new string[]
            {
                "1. Czy gospodarstwo jest zgłoszone w PIW?",
                "2. Czy w gospodarstwie znajduje się wydzielone miejsce do składowania środków dezynfekcyjnych?",
                "3. Czy obornik jest wywożony z gospodarstwa?",
                "4. Czy w gospodarstwie znajdują się miejsca zapewniające właściwe warunki przechowywania produktów leczniczych?",
                "5. Czy teren wokół fermy jest uporządkowany oraz zabezpieczony przed dostępem innych zwierząt?",
                "6. Czy w gospodarstwie znajduje się odzież i obuwie lub ochraniacze tylko do użycia w gospodarstwie?",
                "7. Czy w gospodarstwie znajdują się maty dezynfekcyjne?",
                "8. Czy gospodarstwo posiada środki dezynfekcyjne w ilości niezbędnej do przeprowadzenia doraźnej dezynfekcji?"
            };
            
            int y = 20;
            
            Label lblInfo = new Label
            {
                Text = "SAMOOCENA DOSTAWCY - HODOWCY (wypełnia hodowca)",
                Font = new System.Drawing.Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 62, 80),
                Location = new Point(20, y),
                AutoSize = true
            };
            tabSamoocena.Controls.Add(lblInfo);
            y += 40;
            
            for (int i = 0; i < pytaniaSamoocena.Length; i++)
            {
                Panel panelPytanie = new Panel
                {
                    Location = new Point(20, y),
                    Size = new Size(1100, 35),
                    BackColor = (i % 2 == 0) ? Color.FromArgb(248, 249, 250) : Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };
                
                Label lblPytanie = new Label
                {
                    Text = pytaniaSamoocena[i],
                    Location = new Point(10, 8),
                    Size = new Size(800, 20),
                    Font = new System.Drawing.Font("Segoe UI", 9.5f)
                };
                
                chkSamoocena_TAK[i] = new CheckBox
                {
                    Text = "TAK",
                    Location = new Point(850, 7),
                    Size = new Size(60, 20),
                    Tag = i
                };
                
                chkSamoocena_NIE[i] = new CheckBox
                {
                    Text = "NIE",
                    Location = new Point(920, 7),
                    Size = new Size(60, 20),
                    Tag = i
                };
                
                int index = i;
                chkSamoocena_TAK[i].CheckedChanged += (s, e) => 
                {
                    if (chkSamoocena_TAK[index].Checked)
                        chkSamoocena_NIE[index].Checked = false;
                    CalculatePoints();
                };
                
                chkSamoocena_NIE[i].CheckedChanged += (s, e) =>
                {
                    if (chkSamoocena_NIE[index].Checked)
                        chkSamoocena_TAK[index].Checked = false;
                    CalculatePoints();
                };
                
                panelPytanie.Controls.AddRange(new Control[] { lblPytanie, chkSamoocena_TAK[i], chkSamoocena_NIE[i] });
                tabSamoocena.Controls.Add(panelPytanie);
                
                y += 40;
            }
        }
        
        private void CreateListaKontrolnaTab()
        {
            string[] pytaniaKontrola = new string[]
            {
                "1. Czy gospodarstwo posiada numer WNI?",
                "2. Czy ferma objęta jest opieką weterynaryjną?",
                "3. Czy stado jest wolne od salmonelli?",
                "4. Czy kurnik jest myty i dezynfekowany przed wstawieniem piskląt?",
                "5. Czy padłe ptaki usuwane są codziennie? Czy ferma posiada chłodnię magazyn na sztuki padłe?",
                "6. Czy godzina przyjazdu na fermę/wagę jest zgodna z planową?",
                "7. Czy załadunek rozpoczął się o planowanej godzinie?",
                "8. Czy wjazd na fermę jest wybetonowany/utwardzony?",
                "9. Czy wjazd na fermę jest oświetlony?",
                "10. Czy podjazd pod kurnik jest oświetlony?",
                "11. Czy podjazd pod kurnik jest wybetonowany?",
                "12. Czy kurnik jest dostosowany do załadunku wózkiem?",
                "13. Czy zapewniona jest identyfikowalność? Kurniki są oznaczone?",
                "14. Czy podczas wyłapywania brojlerów zapewniono niebieskie oświetlenie na kurniku?",
                "15. Czy ściółka jest sucha?",
                "16. Czy kury są czyste?",
                "17. Czy kury są suche?",
                "18. Czy podczas załadunku kurniki są puste?",
                "19. Czy technika łapania i ładowania kurczat jest odpowiednia?",
                "20. Czy ilość osób do załadunku jest odpowiednia?"
            };
            
            Panel panelScroll = new Panel
            {
                AutoScroll = true,
                Dock = DockStyle.Fill
            };
            
            int y = 20;
            
            Label lblInfo = new Label
            {
                Text = "LISTA KONTROLNA AUDYTU DOSTAWCY ŻYWCA",
                Font = new System.Drawing.Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 62, 80),
                Location = new Point(20, y),
                AutoSize = true
            };
            panelScroll.Controls.Add(lblInfo);
            y += 30;
            
            Label lblInfo2 = new Label
            {
                Text = "Pytania 1-5: wypełnia HODOWCA (3 pkt za TAK) | Pytania 6-20: wypełnia KIEROWCA (1 pkt za TAK)",
                Font = new System.Drawing.Font("Segoe UI", 9f, FontStyle.Italic),
                ForeColor = Color.FromArgb(231, 76, 60),
                Location = new Point(20, y),
                AutoSize = true
            };
            panelScroll.Controls.Add(lblInfo2);
            y += 30;
            
            for (int i = 0; i < pytaniaKontrola.Length; i++)
            {
                Panel panelPytanie = new Panel
                {
                    Location = new Point(20, y),
                    Size = new Size(1100, 35),
                    BackColor = (i < 5) ? Color.FromArgb(255, 250, 205) : 
                               (i % 2 == 0) ? Color.FromArgb(248, 249, 250) : Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };
                
                Label lblPytanie = new Label
                {
                    Text = pytaniaKontrola[i],
                    Location = new Point(10, 8),
                    Size = new Size(800, 20),
                    Font = new System.Drawing.Font("Segoe UI", 9.5f)
                };
                
                chkKontrola_TAK[i] = new CheckBox
                {
                    Text = "TAK",
                    Location = new Point(850, 7),
                    Size = new Size(60, 20),
                    Tag = i
                };
                
                chkKontrola_NIE[i] = new CheckBox
                {
                    Text = "NIE",
                    Location = new Point(920, 7),
                    Size = new Size(60, 20),
                    Tag = i
                };
                
                int index = i;
                chkKontrola_TAK[i].CheckedChanged += (s, e) =>
                {
                    if (chkKontrola_TAK[index].Checked)
                        chkKontrola_NIE[index].Checked = false;
                    CalculatePoints();
                };
                
                chkKontrola_NIE[i].CheckedChanged += (s, e) =>
                {
                    if (chkKontrola_NIE[index].Checked)
                        chkKontrola_TAK[index].Checked = false;
                    CalculatePoints();
                };
                
                panelPytanie.Controls.AddRange(new Control[] { lblPytanie, chkKontrola_TAK[i], chkKontrola_NIE[i] });
                panelScroll.Controls.Add(panelPytanie);
                
                y += 40;
            }
            
            // Pytanie o dokumentację
            y += 20;
            Panel panelDokumentacja = new Panel
            {
                Location = new Point(20, y),
                Size = new Size(1100, 35),
                BackColor = Color.FromArgb(230, 230, 250),
                BorderStyle = BorderStyle.FixedSingle
            };
            
            Label lblDokumentacja = new Label
            {
                Text = "21. Czy do dostawy dostarczono świadectwo zdrowia?",
                Location = new Point(10, 8),
                Size = new Size(800, 20),
                Font = new System.Drawing.Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            
            chkDokumentacja_TAK = new CheckBox
            {
                Text = "TAK",
                Location = new Point(850, 7),
                Size = new Size(60, 20)
            };
            
            chkDokumentacja_NIE = new CheckBox
            {
                Text = "NIE",
                Location = new Point(920, 7),
                Size = new Size(60, 20)
            };
            
            chkDokumentacja_TAK.CheckedChanged += (s, e) =>
            {
                if (chkDokumentacja_TAK.Checked)
                    chkDokumentacja_NIE.Checked = false;
            };
            
            chkDokumentacja_NIE.CheckedChanged += (s, e) =>
            {
                if (chkDokumentacja_NIE.Checked)
                    chkDokumentacja_TAK.Checked = false;
            };
            
            panelDokumentacja.Controls.AddRange(new Control[] { lblDokumentacja, chkDokumentacja_TAK, chkDokumentacja_NIE });
            panelScroll.Controls.Add(panelDokumentacja);
            
            tabListaKontrolna.Controls.Add(panelScroll);
        }
        
        private void CreatePodsumowanieTab()
        {
            // Panel punktów
            Panel panelPunkty = new Panel
            {
                Location = new Point(50, 50),
                Size = new Size(500, 200),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(232, 245, 233)
            };
            
            Label lblTytulPunkty = new Label
            {
                Text = "📊 PODSUMOWANIE PUNKTÓW",
                Location = new Point(20, 20),
                Size = new Size(460, 30),
                Font = new System.Drawing.Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(27, 94, 32),
                TextAlign = ContentAlignment.MiddleCenter
            };
            
            Label lblPunkty1_5 = new Label
            {
                Text = "Punkty za pytania 1-5:",
                Location = new Point(20, 70),
                Size = new Size(200, 25),
                Font = new System.Drawing.Font("Segoe UI", 11f)
            };
            
            txtPunkty1_5 = new TextBox
            {
                Location = new Point(250, 70),
                Size = new Size(100, 25),
                ReadOnly = true,
                Font = new System.Drawing.Font("Segoe UI", 11f, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center,
                BackColor = Color.White
            };
            
            Label lblPunkty6_20 = new Label
            {
                Text = "Punkty za pytania 6-20:",
                Location = new Point(20, 105),
                Size = new Size(200, 25),
                Font = new System.Drawing.Font("Segoe UI", 11f)
            };
            
            txtPunkty6_20 = new TextBox
            {
                Location = new Point(250, 105),
                Size = new Size(100, 25),
                ReadOnly = true,
                Font = new System.Drawing.Font("Segoe UI", 11f, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center,
                BackColor = Color.White
            };
            
            Label lblPunktyRazem = new Label
            {
                Text = "SUMA PUNKTÓW:",
                Location = new Point(20, 145),
                Size = new Size(200, 30),
                Font = new System.Drawing.Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.FromArgb(27, 94, 32)
            };
            
            txtPunktyRazem = new TextBox
            {
                Location = new Point(250, 145),
                Size = new Size(100, 30),
                ReadOnly = true,
                Font = new System.Drawing.Font("Segoe UI", 14f, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center,
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White
            };
            
            panelPunkty.Controls.AddRange(new Control[] { 
                lblTytulPunkty, lblPunkty1_5, txtPunkty1_5, 
                lblPunkty6_20, txtPunkty6_20, lblPunktyRazem, txtPunktyRazem 
            });
            
            // Panel skali oceny
            Panel panelSkala = new Panel
            {
                Location = new Point(600, 50),
                Size = new Size(400, 200),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(255, 249, 196)
            };
            
            Label lblSkala = new Label
            {
                Text = "📋 SKALA OCENY",
                Location = new Point(20, 20),
                Size = new Size(360, 30),
                Font = new System.Drawing.Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(245, 127, 23),
                TextAlign = ContentAlignment.MiddleCenter
            };
            
            Label lblSkala1 = new Label
            {
                Text = "🟢 30+ punktów - BARDZO DOBRA",
                Location = new Point(20, 70),
                Size = new Size(360, 25),
                Font = new System.Drawing.Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(27, 94, 32)
            };
            
            Label lblSkala2 = new Label
            {
                Text = "🟡 20-29 punktów - DOBRA",
                Location = new Point(20, 100),
                Size = new Size(360, 25),
                Font = new System.Drawing.Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(245, 127, 23)
            };
            
            Label lblSkala3 = new Label
            {
                Text = "🔴 Poniżej 20 punktów - NIEZADOWALAJĄCA",
                Location = new Point(20, 130),
                Size = new Size(360, 25),
                Font = new System.Drawing.Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(198, 40, 40)
            };
            
            panelSkala.Controls.AddRange(new Control[] { lblSkala, lblSkala1, lblSkala2, lblSkala3 });
            
            // Panel uwag
            Label lblUwagi = new Label
            {
                Text = "📝 UWAGI:",
                Location = new Point(50, 270),
                Size = new Size(100, 25),
                Font = new System.Drawing.Font("Segoe UI", 11f, FontStyle.Bold)
            };
            
            txtUwagi = new TextBox
            {
                Location = new Point(50, 300),
                Size = new Size(950, 150),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new System.Drawing.Font("Segoe UI", 10f)
            };
            
            tabPodsumowanie.Controls.AddRange(new Control[] { panelPunkty, panelSkala, lblUwagi, txtUwagi });
        }
        
        private void CalculatePoints()
        {
            int punkty1_5 = 0;
            int punkty6_20 = 0;
            
            // Punkty za pytania 1-5 (lista kontrolna)
            for (int i = 0; i < 5; i++)
            {
                if (chkKontrola_TAK[i].Checked)
                    punkty1_5 += 3;
            }
            
            // Punkty za pytania 6-20 (lista kontrolna)
            for (int i = 5; i < 20; i++)
            {
                if (chkKontrola_TAK[i].Checked)
                    punkty6_20 += 1;
            }
            
            int suma = punkty1_5 + punkty6_20;
            
            if (txtPunkty1_5 != null)
                txtPunkty1_5.Text = punkty1_5.ToString();
            if (txtPunkty6_20 != null)
                txtPunkty6_20.Text = punkty6_20.ToString();
            if (txtPunktyRazem != null)
            {
                txtPunktyRazem.Text = suma.ToString();
                PunktyRazem = suma;
                
                // Kolorowanie wyniku
                if (suma >= 30)
                {
                    txtPunktyRazem.BackColor = Color.FromArgb(76, 175, 80);
                    txtPunktyRazem.ForeColor = Color.White;
                }
                else if (suma >= 20)
                {
                    txtPunktyRazem.BackColor = Color.FromArgb(255, 193, 7);
                    txtPunktyRazem.ForeColor = Color.Black;
                }
                else
                {
                    txtPunktyRazem.BackColor = Color.FromArgb(244, 67, 54);
                    txtPunktyRazem.ForeColor = Color.White;
                }
            }
        }
        
        private void LoadData()
        {
            GenerateReportNumber();
            dpDataOceny.Value = DateTime.Now;
            
            if (_ocenaId.HasValue)
            {
                // Załaduj istniejącą ocenę
                LoadExistingEvaluation();
            }
        }
        
        private void GenerateReportNumber()
        {
            try
            {
                int rok = DateTime.Now.Year;
                string query = @"
                    SELECT ISNULL(MAX(CAST(SUBSTRING(NumerRaportu, 9, 2) AS INT)), 0) + 1
                    FROM [LibraNet].[dbo].[OcenyDostawcow] 
                    WHERE NumerRaportu LIKE '%/' + @Year";
                    
                using var connection = new SqlConnection(connectionString);
                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Year", rok.ToString());
                
                connection.Open();
                object result = command.ExecuteScalar();
                int numerKolejny = result != DBNull.Value ? Convert.ToInt32(result) : 1;
                
                txtNumerRaportu.Text = $"PZ-Z-10-{numerKolejny:00}/{rok}";
            }
            catch
            {
                txtNumerRaportu.Text = $"PZ-Z-10-XX/{DateTime.Now.Year}";
            }
        }
        
        private void LoadExistingEvaluation()
        {
            // Implementacja ładowania istniejącej oceny
        }

        private async void BtnZapisz_Click(object sender, EventArgs e)
        {
            try
            {
                if (MessageBox.Show("Czy na pewno chcesz zapisać ocenę?", "Potwierdzenie",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                string query = @"
            INSERT INTO [LibraNet].[dbo].[OcenyDostawcow]
            (DostawcaID, DataOceny, NumerRaportu, WersjaFormularza,
             CzyPIW, MiejsceSrodkowDezynfekcyjnych, CzyWywozObornika,
             MiejsceWeterynarii, TerenUporzadkowany, ObuwieOchronne,
             OdziezOchronna, MatyDezynfekcyjne, SrodkiDezynfekcyjneDorazne,
             PosiadaWNI, FermaOpieka, StadoWolneOdSalmonelli,
             KurnikMytyDezynfekowany, PadleUsuwane, ZaladunekZgodnyZPlanem,
             DostepDoPlanu, WjazdWybetowany, WjazdOswietlony,
             PodjazdOswietlony, PodjazdWybetowany, KurnikDostosowyDoZaladunku,
             ZapewnionaIdentyfikowalnosc, PoczesaWylapywaniaBrojlerowOswietlenie,
             SciolkaSucha, KuryCzyste, KurySuche, PodczasZaladunkuPuste,
             TechnikaLapania, IloscOsobDoZaladunku,
             PunktySekcja1_5, PunktySekcja6_20, PunktyRazem,
             Uwagi, OceniajacyUserID, DataUtworzenia, Status)
            VALUES
            (@DostawcaID, @DataOceny, @NumerRaportu, '04',
             @CzyPIW, @MiejsceDezynf, @Obornik, @Weterynaria, @Teren, 
             @Obuwie, @Odziez, @Maty, @SrodkiDez,
             @Q1, @Q2, @Q3, @Q4, @Q5, @Q6, @Q7, @Q8, @Q9, @Q10,
             @Q11, @Q12, @Q13, @Q14, @Q15, @Q16, @Q17, @Q18, @Q19, @Q20,
             @Punkty1_5, @Punkty6_20, @PunktyRazem,
             @Uwagi, @UserID, GETDATE(), 'Aktywna')";

                using var connection = new SqlConnection(connectionString);
                using var command = new SqlCommand(query, connection);

                // Parametry podstawowe
                command.Parameters.AddWithValue("@DostawcaID", _dostawcaId);
                command.Parameters.AddWithValue("@DataOceny", dpDataOceny.Value);
                command.Parameters.AddWithValue("@NumerRaportu", txtNumerRaportu.Text);

                // Samoocena - POPRAWIONE!
                command.Parameters.AddWithValue("@CzyPIW", chkSamoocena_TAK[0].Checked);
                command.Parameters.AddWithValue("@MiejsceDezynf", chkSamoocena_TAK[1].Checked);
                command.Parameters.AddWithValue("@Obornik", chkSamoocena_TAK[2].Checked);
                command.Parameters.AddWithValue("@Weterynaria", chkSamoocena_TAK[3].Checked);
                command.Parameters.AddWithValue("@Teren", chkSamoocena_TAK[4].Checked);
                command.Parameters.AddWithValue("@Obuwie", chkSamoocena_TAK[5].Checked);
                command.Parameters.AddWithValue("@Odziez", chkSamoocena_TAK[6].Checked);
                command.Parameters.AddWithValue("@Maty", chkSamoocena_TAK[7].Checked);
                command.Parameters.AddWithValue("@SrodkiDez", chkSamoocena_TAK[7].Checked);

                // Lista kontrolna (pytania 1-20)
                for (int i = 0; i < 20; i++)
                {
                    command.Parameters.AddWithValue($"@Q{i + 1}", chkKontrola_TAK[i].Checked);
                }

                // Punkty
                command.Parameters.AddWithValue("@Punkty1_5", int.Parse(txtPunkty1_5.Text ?? "0"));
                command.Parameters.AddWithValue("@Punkty6_20", int.Parse(txtPunkty6_20.Text ?? "0"));
                command.Parameters.AddWithValue("@PunktyRazem", int.Parse(txtPunktyRazem.Text ?? "0"));

                // Dodatkowe
                command.Parameters.AddWithValue("@Uwagi", txtUwagi.Text ?? "");
                command.Parameters.AddWithValue("@UserID", _userId);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();

                MessageBox.Show("✅ Ocena została zapisana pomyślnie!", "Sukces",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd zapisu: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnGenerujPDF_Click(object sender, EventArgs e)
        {
            try
            {
                string fileName = $"Ocena_{txtNumerRaportu.Text.Replace("/", "_").Replace("-", "_")}.pdf";
                string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
                
                Document document = new Document(PageSize.A4, 25, 25, 30, 30);
                PdfWriter writer = PdfWriter.GetInstance(document, new FileStream(filePath, FileMode.Create));
                
                document.Open();
                
                // Czcionki
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);
                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                var smallFont = FontFactory.GetFont(FontFactory.HELVETICA, 8);
                
                // Nagłówek
                document.Add(new Paragraph("PROCEDURY ZAKŁADOWE", titleFont) { Alignment = Element.ALIGN_CENTER });
                document.Add(new Paragraph("OCENA DOSTAWCÓW", titleFont) { Alignment = Element.ALIGN_CENTER });
                document.Add(new Paragraph("\n"));
                
                // Informacje podstawowe
                document.Add(new Paragraph($"Data: {dpDataOceny.Value:dd.MM.yyyy}", normalFont));
                document.Add(new Paragraph($"Nr raportu: {txtNumerRaportu.Text}", normalFont));
                document.Add(new Paragraph($"Dostawca: {_dostawcaNazwa} (ID: {_dostawcaId})", headerFont));
                document.Add(new Paragraph("\n"));
                
                // Tabela samooceny
                document.Add(new Paragraph("SAMOOCENA DOSTAWCY:", headerFont));
                PdfPTable tableSamoocena = new PdfPTable(3);
                tableSamoocena.WidthPercentage = 100;
                tableSamoocena.SetWidths(new float[] { 70f, 15f, 15f });
                
                tableSamoocena.AddCell(new PdfPCell(new Phrase("Pytanie", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY });
                tableSamoocena.AddCell(new PdfPCell(new Phrase("TAK", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, HorizontalAlignment = Element.ALIGN_CENTER });
                tableSamoocena.AddCell(new PdfPCell(new Phrase("NIE", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, HorizontalAlignment = Element.ALIGN_CENTER });
                
                string[] pytaniaSamoocenaPDF = new string[]
                {
                    "1. Czy gospodarstwo jest zgłoszone w PIW?",
                    "2. Czy znajduje się miejsce do składowania środków dezynfekcyjnych?",
                    "3. Czy obornik jest wywożony z gospodarstwa?",
                    "4. Czy znajdują się miejsca dla produktów leczniczych?",
                    "5. Czy teren jest uporządkowany?",
                    "6. Czy znajduje się odzież ochronna?",
                    "7. Czy znajdują się maty dezynfekcyjne?",
                    "8. Czy posiada środki dezynfekcyjne?"
                };
                
                for (int i = 0; i < 8; i++)
                {
                    tableSamoocena.AddCell(new PdfPCell(new Phrase(pytaniaSamoocenaPDF[i], smallFont)));
                    tableSamoocena.AddCell(new PdfPCell(new Phrase(chkSamoocena_TAK[i].Checked ? "X" : "", normalFont)) 
                        { HorizontalAlignment = Element.ALIGN_CENTER });
                    tableSamoocena.AddCell(new PdfPCell(new Phrase(chkSamoocena_NIE[i].Checked ? "X" : "", normalFont)) 
                        { HorizontalAlignment = Element.ALIGN_CENTER });
                }
                
                document.Add(tableSamoocena);
                document.Add(new Paragraph("\n"));
                
                // Podsumowanie punktów
                document.Add(new Paragraph($"SUMA PUNKTÓW: {txtPunktyRazem.Text}", titleFont));
                document.Add(new Paragraph($"Punkty 1-5: {txtPunkty1_5.Text}", normalFont));
                document.Add(new Paragraph($"Punkty 6-20: {txtPunkty6_20.Text}", normalFont));
                
                // Uwagi
                if (!string.IsNullOrWhiteSpace(txtUwagi.Text))
                {
                    document.Add(new Paragraph("\nUWAGI:", headerFont));
                    document.Add(new Paragraph(txtUwagi.Text, normalFont));
                }

                // Podpisy
                document.Add(new Paragraph("\n\n\n"));
                PdfPTable tableSignatures = new PdfPTable(2);
                tableSignatures.WidthPercentage = 100;

                tableSignatures.AddCell(new PdfPCell(new Phrase("_______________________", normalFont))
                { Border = iTextRectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_CENTER });

                tableSignatures.AddCell(new PdfPCell(new Phrase("_______________________", normalFont))
                { Border = iTextRectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_CENTER });

                tableSignatures.AddCell(new PdfPCell(new Phrase("PODPIS HODOWCY", headerFont))
                { Border = iTextRectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_CENTER });

                tableSignatures.AddCell(new PdfPCell(new Phrase("PODPIS KIEROWCY", headerFont))
                { Border = iTextRectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_CENTER });

                document.Add(tableSignatures);

                document.Close();
                
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                
                MessageBox.Show($"✅ PDF został wygenerowany!\nLokalizacja: {filePath}", "Sukces", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd generowania PDF: {ex.Message}", "Błąd", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
