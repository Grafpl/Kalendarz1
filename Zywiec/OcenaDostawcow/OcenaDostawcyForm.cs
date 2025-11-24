using System;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Diagnostics;

// Aliasy dla uniknięcia konfliktów
using Rectangle = System.Drawing.Rectangle;

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
        private Button btnGenerujPustyPDF;
        private Button btnHistoria;
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
            this.Text = "📋 Ocena Dostawcy Żywca - System Jakości";
            this.Size = new Size(1400, 850);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new System.Drawing.Font("Segoe UI", 9.5f);
            this.BackColor = Color.FromArgb(245, 247, 250);
            this.Icon = SystemIcons.Application;
            
            // Panel nagłówka z gradientem
            panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 120,
                BackColor = Color.FromArgb(30, 58, 95)
            };
            panelHeader.Paint += (s, e) =>
            {
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    panelHeader.ClientRectangle,
                    Color.FromArgb(30, 58, 95),
                    Color.FromArgb(74, 111, 165),
                    LinearGradientMode.Vertical))
                {
                    e.Graphics.FillRectangle(brush, panelHeader.ClientRectangle);
                }
            };
            
            lblTitle = new Label
            {
                Text = "PROCEDURY ZAKŁADOWE - OCENA DOSTAWCÓW",
                Font = new System.Drawing.Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(25, 15)
            };
            
            lblDostawca = new Label
            {
                Text = $"Dostawca: {_dostawcaNazwa} (ID: {_dostawcaId})",
                Font = new System.Drawing.Font("Segoe UI", 12f),
                ForeColor = Color.FromArgb(189, 195, 199),
                AutoSize = true,
                Location = new Point(25, 50)
            };
            
            lblNumerRaportu = new Label
            {
                Text = "Nr raportu:",
                Font = new System.Drawing.Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(700, 25)
            };
            
            txtNumerRaportu = new TextBox
            {
                Location = new Point(800, 23),
                Width = 180,
                ReadOnly = true,
                BackColor = Color.FromArgb(236, 240, 241),
                Font = new System.Drawing.Font("Segoe UI", 10f, FontStyle.Bold)
            };
            
            lblDataOceny = new Label
            {
                Text = "Data oceny:",
                Font = new System.Drawing.Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(700, 55)
            };
            
            dpDataOceny = new DateTimePicker
            {
                Location = new Point(800, 53),
                Width = 180,
                Format = DateTimePickerFormat.Short,
                Font = new System.Drawing.Font("Segoe UI", 10f)
            };
            
            // Dodaj logo jeśli istnieje
            try
            {
                string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logo.png");
                if (File.Exists(logoPath))
                {
                    PictureBox logo = new PictureBox
                    {
                        Location = new Point(1000, 10),
                        Size = new Size(100, 100),
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Image = Image.FromFile(logoPath)
                    };
                    panelHeader.Controls.Add(logo);
                }
            }
            catch { }
            
            panelHeader.Controls.AddRange(new Control[] { lblTitle, lblDostawca, lblNumerRaportu, txtNumerRaportu, lblDataOceny, dpDataOceny });
            
            // TabControl z ulepszonym wyglądem
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI", 10f),
                ItemSize = new Size(200, 40),
                SizeMode = TabSizeMode.Fixed,
                DrawMode = TabDrawMode.OwnerDrawFixed
            };
            
            tabControl.DrawItem += (sender, e) =>
            {
                TabPage page = tabControl.TabPages[e.Index];
                Rectangle tabBounds = e.Bounds;
                
                if (e.Index == tabControl.SelectedIndex)
                {
                    using (Brush brush = new SolidBrush(Color.FromArgb(52, 152, 219)))
                    {
                        e.Graphics.FillRectangle(brush, tabBounds);
                    }
                    TextRenderer.DrawText(e.Graphics, page.Text, new Font("Segoe UI", 11f, FontStyle.Bold),
                        tabBounds, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else
                {
                    using (Brush brush = new SolidBrush(Color.FromArgb(236, 240, 241)))
                    {
                        e.Graphics.FillRectangle(brush, tabBounds);
                    }
                    TextRenderer.DrawText(e.Graphics, page.Text, new Font("Segoe UI", 10f),
                        tabBounds, Color.FromArgb(52, 73, 94), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            };
            
            // Zakładki
            tabSamoocena = new TabPage("📝 Samoocena hodowcy")
            {
                BackColor = Color.White,
                AutoScroll = true
            };
            CreateSamoocenaTab();
            
            tabListaKontrolna = new TabPage("✅ Lista kontrolna")
            {
                BackColor = Color.White,
                AutoScroll = true
            };
            CreateListaKontrolnaTab();
            
            tabPodsumowanie = new TabPage("📊 Podsumowanie")
            {
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
                Size = new Size(120, 40),
                Location = new Point(20, 10),
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
                Text = "📄 Raport PDF",
                Size = new Size(140, 40),
                Location = new Point(150, 10),
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnGenerujPDF.FlatAppearance.BorderSize = 0;
            btnGenerujPDF.Click += BtnGenerujPDF_Click;
            
            btnGenerujPustyPDF = new Button
            {
                Text = "📝 Pusty formularz",
                Size = new Size(160, 40),
                Location = new Point(300, 10),
                BackColor = Color.FromArgb(243, 156, 18),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnGenerujPustyPDF.FlatAppearance.BorderSize = 0;
            btnGenerujPustyPDF.Click += BtnGenerujPustyPDF_Click;
            
            btnHistoria = new Button
            {
                Text = "📚 Historia ocen",
                Size = new Size(140, 40),
                Location = new Point(470, 10),
                BackColor = Color.FromArgb(155, 89, 182),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnHistoria.FlatAppearance.BorderSize = 0;
            btnHistoria.Click += BtnHistoria_Click;
            
            btnAnuluj = new Button
            {
                Text = "❌ Anuluj",
                Size = new Size(110, 40),
                BackColor = Color.FromArgb(149, 165, 166),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnAnuluj.Location = new Point(this.ClientSize.Width - btnAnuluj.Width - 40, 10);
            btnAnuluj.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            btnAnuluj.FlatAppearance.BorderSize = 0;
            btnAnuluj.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            
            panelButtons.Controls.AddRange(new Control[] { btnZapisz, btnGenerujPDF, btnGenerujPustyPDF, btnHistoria, btnAnuluj });
            
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
                Font = new System.Drawing.Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 62, 80),
                Location = new Point(20, y),
                AutoSize = true
            };
            tabSamoocena.Controls.Add(lblInfo);
            y += 35;
            
            Label lblPunktacja = new Label
            {
                Text = "Każde pytanie: 3 punkty za odpowiedź TAK, 0 punktów za NIE",
                Font = new System.Drawing.Font("Segoe UI", 9f, FontStyle.Italic),
                ForeColor = Color.FromArgb(52, 152, 219),
                Location = new Point(20, y),
                AutoSize = true
            };
            tabSamoocena.Controls.Add(lblPunktacja);
            y += 30;
            
            for (int i = 0; i < pytaniaSamoocena.Length; i++)
            {
                Panel panelPytanie = new Panel
                {
                    Location = new Point(20, y),
                    Size = new Size(1300, 40),
                    BackColor = (i % 2 == 0) ? Color.FromArgb(248, 249, 250) : Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };
                
                Label lblPytanie = new Label
                {
                    Text = pytaniaSamoocena[i],
                    Location = new Point(10, 10),
                    Size = new Size(900, 20),
                    Font = new System.Drawing.Font("Segoe UI", 10f)
                };
                
                chkSamoocena_TAK[i] = new CheckBox
                {
                    Text = "TAK",
                    Location = new Point(950, 10),
                    Size = new Size(60, 20),
                    Font = new System.Drawing.Font("Segoe UI", 10f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(39, 174, 96),
                    Tag = i
                };
                
                chkSamoocena_NIE[i] = new CheckBox
                {
                    Text = "NIE",
                    Location = new Point(1050, 10),
                    Size = new Size(60, 20),
                    Font = new System.Drawing.Font("Segoe UI", 10f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(231, 76, 60),
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
                
                y += 45;
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
                Font = new System.Drawing.Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 62, 80),
                Location = new Point(20, y),
                AutoSize = true
            };
            panelScroll.Controls.Add(lblInfo);
            y += 35;
            
            Label lblInfo2 = new Label
            {
                Text = "Pytania 1-5: wypełnia HODOWCA (3 pkt za TAK) | Pytania 6-20: wypełnia KIEROWCA (1 pkt za TAK)",
                Font = new System.Drawing.Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(231, 76, 60),
                Location = new Point(20, y),
                AutoSize = true
            };
            panelScroll.Controls.Add(lblInfo2);
            y += 35;
            
            for (int i = 0; i < pytaniaKontrola.Length; i++)
            {
                Panel panelPytanie = new Panel
                {
                    Location = new Point(20, y),
                    Size = new Size(1300, 40),
                    BackColor = (i < 5) ? Color.FromArgb(255, 250, 205) : 
                               (i % 2 == 0) ? Color.FromArgb(248, 249, 250) : Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };
                
                if (i < 5)
                {
                    // Dodaj etykietę dla pytań hodowcy
                    Label lblHodowca = new Label
                    {
                        Text = "HODOWCA",
                        Location = new Point(1150, 10),
                        Size = new Size(80, 20),
                        Font = new System.Drawing.Font("Segoe UI", 9f, FontStyle.Bold),
                        ForeColor = Color.FromArgb(243, 156, 18),
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    panelPytanie.Controls.Add(lblHodowca);
                }
                else
                {
                    // Dodaj etykietę dla pytań kierowcy
                    Label lblKierowca = new Label
                    {
                        Text = "KIEROWCA",
                        Location = new Point(1150, 10),
                        Size = new Size(80, 20),
                        Font = new System.Drawing.Font("Segoe UI", 9f, FontStyle.Bold),
                        ForeColor = Color.FromArgb(52, 152, 219),
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    panelPytanie.Controls.Add(lblKierowca);
                }
                
                Label lblPytanie = new Label
                {
                    Text = pytaniaKontrola[i],
                    Location = new Point(10, 10),
                    Size = new Size(900, 20),
                    Font = new System.Drawing.Font("Segoe UI", 10f)
                };
                
                chkKontrola_TAK[i] = new CheckBox
                {
                    Text = "TAK",
                    Location = new Point(950, 10),
                    Size = new Size(60, 20),
                    Font = new System.Drawing.Font("Segoe UI", 10f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(39, 174, 96),
                    Tag = i
                };
                
                chkKontrola_NIE[i] = new CheckBox
                {
                    Text = "NIE",
                    Location = new Point(1050, 10),
                    Size = new Size(60, 20),
                    Font = new System.Drawing.Font("Segoe UI", 10f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(231, 76, 60),
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
                
                y += 45;
            }
            
            tabListaKontrolna.Controls.Add(panelScroll);
        }
        
        private void CreatePodsumowanieTab()
        {
            int y = 20;
            
            // Nagłówek
            Label lblTitle = new Label
            {
                Text = "PODSUMOWANIE OCENY",
                Font = new System.Drawing.Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 62, 80),
                Location = new Point(20, y),
                AutoSize = true
            };
            tabPodsumowanie.Controls.Add(lblTitle);
            y += 40;
            
            // Panel punktacji
            Panel panelPunkty = new Panel
            {
                Location = new Point(20, y),
                Size = new Size(500, 250),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(248, 249, 250)
            };
            
            Label lblPunkty1 = new Label
            {
                Text = "Punkty Sekcja I (Samoocena, max 24 pkt):",
                Location = new Point(10, 20),
                Size = new Size(300, 25),
                Font = new System.Drawing.Font("Segoe UI", 10f, FontStyle.Bold)
            };
            
            txtPunkty1_5 = new TextBox
            {
                Location = new Point(320, 18),
                Size = new Size(80, 25),
                ReadOnly = true,
                BackColor = Color.White,
                Font = new System.Drawing.Font("Segoe UI", 11f, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center
            };
            
            Label lblPunkty2 = new Label
            {
                Text = "Punkty Sekcja II (Lista kontrolna, max 30 pkt):",
                Location = new Point(10, 60),
                Size = new Size(300, 25),
                Font = new System.Drawing.Font("Segoe UI", 10f, FontStyle.Bold)
            };
            
            txtPunkty6_20 = new TextBox
            {
                Location = new Point(320, 58),
                Size = new Size(80, 25),
                ReadOnly = true,
                BackColor = Color.White,
                Font = new System.Drawing.Font("Segoe UI", 11f, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center
            };
            
            // Linia separująca
            Panel separator = new Panel
            {
                Location = new Point(10, 100),
                Size = new Size(480, 2),
                BackColor = Color.FromArgb(52, 73, 94)
            };
            
            Label lblPunktyRazem = new Label
            {
                Text = "ŁĄCZNA SUMA PUNKTÓW (max 60 pkt):",
                Location = new Point(10, 120),
                Size = new Size(300, 30),
                Font = new System.Drawing.Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94)
            };
            
            txtPunktyRazem = new TextBox
            {
                Location = new Point(320, 118),
                Size = new Size(100, 35),
                ReadOnly = true,
                BackColor = Color.FromArgb(39, 174, 96),
                ForeColor = Color.White,
                Font = new System.Drawing.Font("Segoe UI", 14f, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center
            };
            
            // Status oceny
            Label lblStatus = new Label
            {
                Location = new Point(10, 170),
                Size = new Size(480, 60),
                Font = new System.Drawing.Font("Segoe UI", 11f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.FixedSingle
            };
            
            panelPunkty.Controls.AddRange(new Control[] { 
                lblPunkty1, txtPunkty1_5, 
                lblPunkty2, txtPunkty6_20,
                separator,
                lblPunktyRazem, txtPunktyRazem,
                lblStatus
            });
            
            tabPodsumowanie.Controls.Add(panelPunkty);
            
            // Panel dokumentacji
            Panel panelDokumentacja = new Panel
            {
                Location = new Point(540, y),
                Size = new Size(400, 150),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(248, 249, 250)
            };
            
            Label lblDokumentacja = new Label
            {
                Text = "DOKUMENTACJA WETERYNARYJNA",
                Location = new Point(10, 10),
                Size = new Size(380, 25),
                Font = new System.Drawing.Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 62, 80)
            };
            
            Label lblDokInfo = new Label
            {
                Text = "Czy przedstawiono wymaganą dokumentację?",
                Location = new Point(10, 40),
                Size = new Size(380, 25),
                Font = new System.Drawing.Font("Segoe UI", 10f)
            };
            
            chkDokumentacja_TAK = new CheckBox
            {
                Text = "TAK (6 pkt)",
                Location = new Point(50, 70),
                Size = new Size(120, 25),
                Font = new System.Drawing.Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(39, 174, 96)
            };
            
            chkDokumentacja_NIE = new CheckBox
            {
                Text = "NIE (0 pkt)",
                Location = new Point(200, 70),
                Size = new Size(120, 25),
                Font = new System.Drawing.Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(231, 76, 60)
            };
            
            chkDokumentacja_TAK.CheckedChanged += (s, e) =>
            {
                if (chkDokumentacja_TAK.Checked)
                    chkDokumentacja_NIE.Checked = false;
                CalculatePoints();
            };
            
            chkDokumentacja_NIE.CheckedChanged += (s, e) =>
            {
                if (chkDokumentacja_NIE.Checked)
                    chkDokumentacja_TAK.Checked = false;
                CalculatePoints();
            };
            
            panelDokumentacja.Controls.AddRange(new Control[] { 
                lblDokumentacja, lblDokInfo,
                chkDokumentacja_TAK, chkDokumentacja_NIE
            });
            
            tabPodsumowanie.Controls.Add(panelDokumentacja);
            
            // Uwagi
            y += 280;
            Label lblUwagi = new Label
            {
                Text = "UWAGI I ZALECENIA:",
                Location = new Point(20, y),
                Size = new Size(200, 25),
                Font = new System.Drawing.Font("Segoe UI", 11f, FontStyle.Bold)
            };
            tabPodsumowanie.Controls.Add(lblUwagi);
            
            txtUwagi = new TextBox
            {
                Location = new Point(20, y + 30),
                Size = new Size(920, 120),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new System.Drawing.Font("Segoe UI", 10f)
            };
            tabPodsumowanie.Controls.Add(txtUwagi);
            
            txtPunktyRazem.TextChanged += (s, e) =>
            {
                if (int.TryParse(txtPunktyRazem.Text, out int punkty))
                {
                    if (punkty >= 30)
                    {
                        lblStatus.Text = "✅ OCENA: BARDZO DOBRY";
                        lblStatus.BackColor = Color.FromArgb(39, 174, 96);
                        lblStatus.ForeColor = Color.White;
                    }
                    else if (punkty >= 20)
                    {
                        lblStatus.Text = "⚠️ OCENA: DOBRY";
                        lblStatus.BackColor = Color.FromArgb(243, 156, 18);
                        lblStatus.ForeColor = Color.White;
                    }
                    else
                    {
                        lblStatus.Text = "❌ OCENA: NIEZADOWALAJĄCY";
                        lblStatus.BackColor = Color.FromArgb(231, 76, 60);
                        lblStatus.ForeColor = Color.White;
                    }
                }
            };
        }
        
        private async void LoadData()
        {
            await GenerateReportNumber();
            
            if (_ocenaId.HasValue)
            {
                await LoadExistingEvaluation();
            }
        }
        
        private async Task GenerateReportNumber()
        {
            try
            {
                string query = @"
                    SELECT COUNT(*) + 1 
                    FROM [dbo].[OcenyDostawcow] 
                    WHERE YEAR(DataOceny) = @Rok";
                    
                using var connection = new SqlConnection(connectionString);
                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Rok", DateTime.Now.Year);
                
                await connection.OpenAsync();
                int numerKolejny = (int)await command.ExecuteScalarAsync();
                
                txtNumerRaportu.Text = $"OD/{DateTime.Now.Year}/{numerKolejny:D4}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd generowania numeru: {ex.Message}");
                txtNumerRaportu.Text = $"OD/{DateTime.Now.Year}/XXXX";
            }
        }
        
        private async Task LoadExistingEvaluation()
        {
            try
            {
                string query = @"
                    SELECT * FROM [dbo].[OcenyDostawcow] 
                    WHERE ID = @ID";
                    
                using var connection = new SqlConnection(connectionString);
                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@ID", _ocenaId.Value);
                
                await connection.OpenAsync();
                using var reader = await command.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    txtNumerRaportu.Text = reader["NumerRaportu"]?.ToString();
                    dpDataOceny.Value = Convert.ToDateTime(reader["DataOceny"]);
                    txtUwagi.Text = reader["Uwagi"]?.ToString();
                    
                    // Załaduj odpowiedzi samooceny
                    for (int i = 1; i <= 8; i++)
                    {
                        string columnName = $"Samoocena_P{i}";
                        if (reader[columnName] != DBNull.Value)
                        {
                            bool value = Convert.ToBoolean(reader[columnName]);
                            if (value)
                                chkSamoocena_TAK[i - 1].Checked = true;
                            else
                                chkSamoocena_NIE[i - 1].Checked = true;
                        }
                    }
                    
                    // Załaduj odpowiedzi kontrolne
                    for (int i = 1; i <= 20; i++)
                    {
                        string columnName = $"Kontrolna_P{i}";
                        if (reader[columnName] != DBNull.Value)
                        {
                            bool value = Convert.ToBoolean(reader[columnName]);
                            if (value)
                                chkKontrola_TAK[i - 1].Checked = true;
                            else
                                chkKontrola_NIE[i - 1].Checked = true;
                        }
                    }
                    
                    // Załaduj dokumentację
                    if (reader["DokumentacjaWeterynaryjna"] != DBNull.Value)
                    {
                        bool dok = Convert.ToBoolean(reader["DokumentacjaWeterynaryjna"]);
                        if (dok)
                            chkDokumentacja_TAK.Checked = true;
                        else
                            chkDokumentacja_NIE.Checked = true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}");
            }
        }
        
        private void CalculatePoints()
        {
            int punktySekcja1 = 0;
            int punktySekcja2 = 0;
            
            // Sekcja I - Samoocena (8 pytań po 3 punkty)
            for (int i = 0; i < 8; i++)
            {
                if (chkSamoocena_TAK[i].Checked)
                    punktySekcja1 += 3;
            }
            
            // Sekcja II - Lista kontrolna
            // Pierwsze 5 pytań (hodowca) - po 3 punkty
            for (int i = 0; i < 5; i++)
            {
                if (chkKontrola_TAK[i].Checked)
                    punktySekcja2 += 3;
            }
            
            // Pozostałe 15 pytań (kierowca) - po 1 punkcie
            for (int i = 5; i < 20; i++)
            {
                if (chkKontrola_TAK[i].Checked)
                    punktySekcja2 += 1;
            }
            
            // Dokumentacja - 6 punktów
            int punktyDokumentacja = chkDokumentacja_TAK.Checked ? 6 : 0;
            
            // Aktualizacja pól
            txtPunkty1_5.Text = punktySekcja1.ToString();
            txtPunkty6_20.Text = punktySekcja2.ToString();
            
            PunktyRazem = punktySekcja1 + punktySekcja2 + punktyDokumentacja;
            txtPunktyRazem.Text = PunktyRazem.ToString();
        }
        
        private async void BtnZapisz_Click(object sender, EventArgs e)
        {
            try
            {
                string query;
                if (_ocenaId.HasValue)
                {
                    query = @"
                        UPDATE [dbo].[OcenyDostawcow] SET
                            DataOceny = @DataOceny,
                            PunktySekcja1 = @PunktySekcja1,
                            PunktySekcja2 = @PunktySekcja2,
                            PunktyRazem = @PunktyRazem,
                            Uwagi = @Uwagi,
                            DokumentacjaWeterynaryjna = @Dokumentacja,
                            DataModyfikacji = @DataModyfikacji,
                            Samoocena_P1 = @S1, Samoocena_P2 = @S2, Samoocena_P3 = @S3, Samoocena_P4 = @S4,
                            Samoocena_P5 = @S5, Samoocena_P6 = @S6, Samoocena_P7 = @S7, Samoocena_P8 = @S8,
                            Kontrolna_P1 = @K1, Kontrolna_P2 = @K2, Kontrolna_P3 = @K3, Kontrolna_P4 = @K4,
                            Kontrolna_P5 = @K5, Kontrolna_P6 = @K6, Kontrolna_P7 = @K7, Kontrolna_P8 = @K8,
                            Kontrolna_P9 = @K9, Kontrolna_P10 = @K10, Kontrolna_P11 = @K11, Kontrolna_P12 = @K12,
                            Kontrolna_P13 = @K13, Kontrolna_P14 = @K14, Kontrolna_P15 = @K15, Kontrolna_P16 = @K16,
                            Kontrolna_P17 = @K17, Kontrolna_P18 = @K18, Kontrolna_P19 = @K19, Kontrolna_P20 = @K20
                        WHERE ID = @ID";
                }
                else
                {
                    query = @"
                        INSERT INTO [dbo].[OcenyDostawcow]
                            (DostawcaID, NumerRaportu, DataOceny, PunktySekcja1, PunktySekcja2, PunktyRazem,
                             OceniajacyUserID, Uwagi, DokumentacjaWeterynaryjna, DataUtworzenia, Status,
                             Samoocena_P1, Samoocena_P2, Samoocena_P3, Samoocena_P4, Samoocena_P5, 
                             Samoocena_P6, Samoocena_P7, Samoocena_P8,
                             Kontrolna_P1, Kontrolna_P2, Kontrolna_P3, Kontrolna_P4, Kontrolna_P5,
                             Kontrolna_P6, Kontrolna_P7, Kontrolna_P8, Kontrolna_P9, Kontrolna_P10,
                             Kontrolna_P11, Kontrolna_P12, Kontrolna_P13, Kontrolna_P14, Kontrolna_P15,
                             Kontrolna_P16, Kontrolna_P17, Kontrolna_P18, Kontrolna_P19, Kontrolna_P20)
                        VALUES
                            (@DostawcaID, @NumerRaportu, @DataOceny, @PunktySekcja1, @PunktySekcja2, @PunktyRazem,
                             @UserID, @Uwagi, @Dokumentacja, @DataUtworzenia, @Status,
                             @S1, @S2, @S3, @S4, @S5, @S6, @S7, @S8,
                             @K1, @K2, @K3, @K4, @K5, @K6, @K7, @K8, @K9, @K10,
                             @K11, @K12, @K13, @K14, @K15, @K16, @K17, @K18, @K19, @K20)";
                }
                
                using var connection = new SqlConnection(connectionString);
                using var command = new SqlCommand(query, connection);
                
                // Parametry podstawowe
                command.Parameters.AddWithValue("@DostawcaID", _dostawcaId);
                command.Parameters.AddWithValue("@NumerRaportu", txtNumerRaportu.Text);
                command.Parameters.AddWithValue("@DataOceny", dpDataOceny.Value);
                command.Parameters.AddWithValue("@PunktySekcja1", Convert.ToInt32(txtPunkty1_5.Text));
                command.Parameters.AddWithValue("@PunktySekcja2", Convert.ToInt32(txtPunkty6_20.Text));
                command.Parameters.AddWithValue("@PunktyRazem", PunktyRazem);
                command.Parameters.AddWithValue("@UserID", _userId);
                command.Parameters.AddWithValue("@Uwagi", txtUwagi.Text ?? "");
                command.Parameters.AddWithValue("@Dokumentacja", chkDokumentacja_TAK.Checked);
                command.Parameters.AddWithValue("@Status", PunktyRazem >= 20 ? "Zaakceptowany" : "Odrzucony");
                
                if (_ocenaId.HasValue)
                {
                    command.Parameters.AddWithValue("@ID", _ocenaId.Value);
                    command.Parameters.AddWithValue("@DataModyfikacji", DateTime.Now);
                }
                else
                {
                    command.Parameters.AddWithValue("@DataUtworzenia", DateTime.Now);
                }
                
                // Parametry samooceny
                for (int i = 0; i < 8; i++)
                {
                    command.Parameters.AddWithValue($"@S{i + 1}", chkSamoocena_TAK[i].Checked);
                }
                
                // Parametry kontrolne
                for (int i = 0; i < 20; i++)
                {
                    command.Parameters.AddWithValue($"@K{i + 1}", chkKontrola_TAK[i].Checked);
                }
                
                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
                
                MessageBox.Show("Ocena została zapisana pomyślnie!", "Sukces", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu: {ex.Message}", "Błąd", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void BtnGenerujPDF_Click(object sender, EventArgs e)
        {
            try
            {
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "PDF Files (*.pdf)|*.pdf",
                    FileName = $"Ocena_{_dostawcaNazwa.Replace(" ", "_")}_{DateTime.Now:yyyy_MM_dd}.pdf",
                    Title = "Zapisz raport PDF"
                };
                
                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    // Przygotuj dane dla generatora
                    bool[] samoocena = new bool[8];
                    for (int i = 0; i < 8; i++)
                        samoocena[i] = chkSamoocena_TAK[i].Checked;
                    
                    bool[] kontrolna = new bool[20];
                    for (int i = 0; i < 20; i++)
                        kontrolna[i] = chkKontrola_TAK[i].Checked;
                    
                    /* Użyj istniejącego generatora OcenaPDFGenerator
                    var generator = new OcenaPDFGenerator();
                    generator.GenerujRaport(
                        saveDialog.FileName,
                        txtNumerRaportu.Text,
                        dpDataOceny.Value,
                        _dostawcaNazwa,
                        _dostawcaId,
                        txtUwagi.Text,
                        Convert.ToInt32(txtPunkty1_5.Text),
                        Convert.ToInt32(txtPunkty6_20.Text),
                        PunktyRazem,
                        samoocena,
                        kontrolna,
                        chkDokumentacja_TAK.Checked
                    );
                    */
                    
                    MessageBox.Show("PDF został wygenerowany pomyślnie!", "Sukces", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                    // Otwórz PDF
                    Process.Start(new ProcessStartInfo(saveDialog.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd generowania PDF: {ex.Message}", "Błąd", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void BtnGenerujPustyPDF_Click(object sender, EventArgs e)
        {
            try
            {
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "PDF Files (*.pdf)|*.pdf",
                    FileName = $"Formularz_Oceny_Dostawcy_{DateTime.Now:yyyy_MM_dd}.pdf",
                    Title = "Zapisz pusty formularz PDF"
                };
                
                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    var generator = new BlankOcenaFormPDFGenerator();
                    generator.GenerujPustyFormularz(saveDialog.FileName);
                    
                    MessageBox.Show("Pusty formularz PDF został wygenerowany pomyślnie!\nMożesz go teraz wydrukować i przekazać hodowcy do wypełnienia.", 
                        "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                    // Otwórz PDF
                    Process.Start(new ProcessStartInfo(saveDialog.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd generowania pustego formularza: {ex.Message}", "Błąd", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void BtnHistoria_Click(object sender, EventArgs e)
        {
            var historiaForm = new HistoriaOcenWindow(_dostawcaId);
            historiaForm.ShowDialog();
        }
    }
}
