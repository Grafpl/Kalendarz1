namespace Kalendarz1
{
    partial class Platnosci
    {
        private System.ComponentModel.IContainer components = null;

        // === Deklaracje kontrolek (NOWE + ISTNIEJĄCE) ===
        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Panel panelHeader;
        private System.Windows.Forms.Panel panelFilters;
        private System.Windows.Forms.Panel panelSummary;
        private System.Windows.Forms.Panel panelMain;

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPageHodowcy;
        private System.Windows.Forms.TabPage tabPageUbojnia;

        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.DataGridView dataGridView2;

        private System.Windows.Forms.Button refreshButton;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button buttonExportExcel;

        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.CheckBox showAllCheckBox;
        private System.Windows.Forms.ComboBox comboBoxFiltr;

        // CHECKBOX do karty Ubojnia – Pokaż podatek rolniczy (0–800)
        private System.Windows.Forms.CheckBox chkPokazPodatekRolniczy;

        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.TextBox textBoxPrzeterminowane;
        private System.Windows.Forms.TextBox textBoxZblizajace;   // suma zbliżających się terminów
        private System.Windows.Forms.TextBox textBoxWTerminie;
        private System.Windows.Forms.TextBox textBoxSumaUbojnia; // pomocnicze

        private System.Windows.Forms.Label lblTytul;
        private System.Windows.Forms.Label label3;   // SUMA
        private System.Windows.Forms.Label label4;   // Przeterminowane
        private System.Windows.Forms.Label label7;   // Zbliżające (≤7 dni)
        private System.Windows.Forms.Label label5;   // W terminie
        private System.Windows.Forms.Label label6;   // Szukaj
        private System.Windows.Forms.Label lblStatus;

        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.ProgressBar progressBar1;

        private System.Windows.Forms.ListView listViewTop3;
        private System.Windows.Forms.ColumnHeader colLp;
        private System.Windows.Forms.ColumnHeader colKontrahent;
        private System.Windows.Forms.ColumnHeader colDoZaplacenia;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Platnosci));
            this.panelTop = new System.Windows.Forms.Panel();
            this.panelHeader = new System.Windows.Forms.Panel();
            this.lblTytul = new System.Windows.Forms.Label();
            this.refreshButton = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.buttonExportExcel = new System.Windows.Forms.Button();
            this.panelFilters = new System.Windows.Forms.Panel();
            this.label6 = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.comboBoxFiltr = new System.Windows.Forms.ComboBox();
            this.showAllCheckBox = new System.Windows.Forms.CheckBox();
            this.chkPokazPodatekRolniczy = new System.Windows.Forms.CheckBox();

            this.panelMain = new System.Windows.Forms.Panel();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPageHodowcy = new System.Windows.Forms.TabPage();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.tabPageUbojnia = new System.Windows.Forms.TabPage();
            this.dataGridView2 = new System.Windows.Forms.DataGridView();

            this.panelSummary = new System.Windows.Forms.Panel();
            this.label3 = new System.Windows.Forms.Label();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.textBoxPrzeterminowane = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.textBoxZblizajace = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.textBoxWTerminie = new System.Windows.Forms.TextBox();
            this.textBoxSumaUbojnia = new System.Windows.Forms.TextBox();
            this.lblStatus = new System.Windows.Forms.Label();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();

            this.listViewTop3 = new System.Windows.Forms.ListView();
            this.colLp = new System.Windows.Forms.ColumnHeader();
            this.colKontrahent = new System.Windows.Forms.ColumnHeader();
            this.colDoZaplacenia = new System.Windows.Forms.ColumnHeader();

            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);

            // panelTop
            this.panelTop.BackColor = System.Drawing.Color.White;
            this.panelTop.Controls.Add(this.panelHeader);
            this.panelTop.Controls.Add(this.panelFilters);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(0, 0);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(1200, 150);
            this.panelTop.TabIndex = 0;

            // panelHeader
            this.panelHeader.BackColor = System.Drawing.Color.FromArgb(37, 99, 235);
            this.panelHeader.Controls.Add(this.lblTytul);
            this.panelHeader.Controls.Add(this.refreshButton);
            this.panelHeader.Controls.Add(this.button1);
            this.panelHeader.Controls.Add(this.buttonExportExcel);
            this.panelHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelHeader.Location = new System.Drawing.Point(0, 0);
            this.panelHeader.Name = "panelHeader";
            this.panelHeader.Size = new System.Drawing.Size(1200, 70);
            this.panelHeader.TabIndex = 0;

            // lblTytul
            this.lblTytul.AutoSize = true;
            this.lblTytul.Font = new System.Drawing.Font("Segoe UI", 20F, System.Drawing.FontStyle.Bold);
            this.lblTytul.ForeColor = System.Drawing.Color.White;
            this.lblTytul.Location = new System.Drawing.Point(20, 15);
            this.lblTytul.Name = "lblTytul";
            this.lblTytul.Size = new System.Drawing.Size(250, 37);
            this.lblTytul.Text = "System Płatności";

            // refreshButton
            this.refreshButton.Anchor = (System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right);
            this.refreshButton.BackColor = System.Drawing.Color.FromArgb(34, 197, 94);
            this.refreshButton.FlatAppearance.BorderSize = 0;
            this.refreshButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.refreshButton.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.refreshButton.ForeColor = System.Drawing.Color.White;
            this.refreshButton.Location = new System.Drawing.Point(850, 15);
            this.refreshButton.Name = "refreshButton";
            this.refreshButton.Size = new System.Drawing.Size(100, 40);
            this.refreshButton.Text = "Odśwież";
            this.refreshButton.UseVisualStyleBackColor = false;
            this.refreshButton.Click += new System.EventHandler(this.refreshButton_Click);

            // button1 (Raport)
            this.button1.Anchor = (System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right);
            this.button1.BackColor = System.Drawing.Color.FromArgb(59, 130, 246);
            this.button1.FlatAppearance.BorderSize = 0;
            this.button1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button1.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.button1.ForeColor = System.Drawing.Color.White;
            this.button1.Location = new System.Drawing.Point(960, 15);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(100, 40);
            this.button1.Text = "Raport";
            this.button1.UseVisualStyleBackColor = false;
            this.button1.Click += new System.EventHandler(this.button1_Click_1);

            // buttonExportExcel
            this.buttonExportExcel.Anchor = (System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right);
            this.buttonExportExcel.BackColor = System.Drawing.Color.FromArgb(16, 185, 129);
            this.buttonExportExcel.FlatAppearance.BorderSize = 0;
            this.buttonExportExcel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonExportExcel.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.buttonExportExcel.ForeColor = System.Drawing.Color.White;
            this.buttonExportExcel.Location = new System.Drawing.Point(1070, 15);
            this.buttonExportExcel.Name = "buttonExportExcel";
            this.buttonExportExcel.Size = new System.Drawing.Size(100, 40);
            this.buttonExportExcel.Text = "Excel";
            this.buttonExportExcel.UseVisualStyleBackColor = false;
            this.buttonExportExcel.Click += new System.EventHandler(this.buttonExportExcel_Click);

            // panelFilters
            this.panelFilters.BackColor = System.Drawing.Color.FromArgb(248, 250, 252);
            this.panelFilters.Controls.Add(this.label6);
            this.panelFilters.Controls.Add(this.textBox1);
            this.panelFilters.Controls.Add(this.comboBoxFiltr);
            this.panelFilters.Controls.Add(this.showAllCheckBox);
            this.panelFilters.Controls.Add(this.chkPokazPodatekRolniczy);
            this.panelFilters.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelFilters.Location = new System.Drawing.Point(0, 70);
            this.panelFilters.Name = "panelFilters";
            this.panelFilters.Size = new System.Drawing.Size(1200, 80);

            // label6
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.label6.Location = new System.Drawing.Point(20, 27);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(108, 19);
            this.label6.Text = "Szukaj hodowcy:";

            // textBox1
            this.textBox1.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.textBox1.Location = new System.Drawing.Point(134, 23);
            this.textBox1.Name = "textBox1";
            this.textBox1.PlaceholderText = "Wpisz nazwę...";
            this.textBox1.Size = new System.Drawing.Size(250, 27);

            // comboBoxFiltr
            this.comboBoxFiltr.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxFiltr.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.comboBoxFiltr.FormattingEnabled = true;
            this.comboBoxFiltr.Items.AddRange(new object[] {
                "Wszystkie",
                "Przeterminowane",
                "Zbliżające (≤7 dni)",
                "W terminie"});
            this.comboBoxFiltr.Location = new System.Drawing.Point(400, 23);
            this.comboBoxFiltr.Name = "comboBoxFiltr";
            this.comboBoxFiltr.Size = new System.Drawing.Size(190, 28);
            this.comboBoxFiltr.SelectedIndexChanged += new System.EventHandler(this.comboBoxFiltr_SelectedIndexChanged);

            // showAllCheckBox
            this.showAllCheckBox.AutoSize = true;
            this.showAllCheckBox.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.showAllCheckBox.Location = new System.Drawing.Point(610, 26);
            this.showAllCheckBox.Name = "showAllCheckBox";
            this.showAllCheckBox.Size = new System.Drawing.Size(169, 23);
            this.showAllCheckBox.Text = "Pokaż wszystkie kolumny";
            this.showAllCheckBox.UseVisualStyleBackColor = true;
            //this.showAllCheckBox.CheckedChanged += new System.EventHandler(this.ShowAllCheckBox_CheckedChanged);

            // chkPokazPodatekRolniczy (widoczny tylko dla karty Ubojnia)
            this.chkPokazPodatekRolniczy.AutoSize = true;
            this.chkPokazPodatekRolniczy.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.chkPokazPodatekRolniczy.Location = new System.Drawing.Point(820, 26);
            this.chkPokazPodatekRolniczy.Name = "chkPokazPodatekRolniczy";
            this.chkPokazPodatekRolniczy.Size = new System.Drawing.Size(238, 23);
            this.chkPokazPodatekRolniczy.Text = "Pokaż podatek rolniczy (0–800 zł)";
            this.chkPokazPodatekRolniczy.UseVisualStyleBackColor = true;
            this.chkPokazPodatekRolniczy.Visible = false; // pokażemy tylko w zakładce Ubojnia
            this.chkPokazPodatekRolniczy.CheckedChanged += new System.EventHandler(this.chkPokazPodatekRolniczy_CheckedChanged);

            // panelMain
            this.panelMain.Controls.Add(this.tabControl1);
            this.panelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelMain.Location = new System.Drawing.Point(0, 150);
            this.panelMain.Name = "panelMain";
            this.panelMain.Padding = new System.Windows.Forms.Padding(10);
            this.panelMain.Size = new System.Drawing.Size(1200, 500);

            // tabControl1
            this.tabControl1.Controls.Add(this.tabPageHodowcy);
            this.tabControl1.Controls.Add(this.tabPageUbojnia);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.tabControl1.ItemSize = new System.Drawing.Size(200, 40);
            this.tabControl1.Location = new System.Drawing.Point(10, 10);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(1180, 480);
            this.tabControl1.SizeMode = System.Windows.Forms.TabSizeMode.Fixed;
            this.tabControl1.SelectedIndexChanged += new System.EventHandler(this.tabControl1_SelectedIndexChanged);

            // tabPageHodowcy
            this.tabPageHodowcy.Controls.Add(this.dataGridView1);
            this.tabPageHodowcy.Location = new System.Drawing.Point(4, 44);
            this.tabPageHodowcy.Name = "tabPageHodowcy";
            this.tabPageHodowcy.Padding = new System.Windows.Forms.Padding(10);
            this.tabPageHodowcy.Size = new System.Drawing.Size(1172, 432);
            this.tabPageHodowcy.Text = "Płatności DO Hodowców";
            this.tabPageHodowcy.UseVisualStyleBackColor = true;

            // tabPageUbojnia
            this.tabPageUbojnia.Controls.Add(this.dataGridView2);
            this.tabPageUbojnia.Location = new System.Drawing.Point(4, 44);
            this.tabPageUbojnia.Name = "tabPageUbojnia";
            this.tabPageUbojnia.Padding = new System.Windows.Forms.Padding(10);
            this.tabPageUbojnia.Size = new System.Drawing.Size(1172, 432);
            this.tabPageUbojnia.Text = "Płatności OD Ubojni";
            this.tabPageUbojnia.UseVisualStyleBackColor = true;

            // dataGridView1
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(10, 10);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.RowTemplate.Height = 25;
            this.dataGridView1.Size = new System.Drawing.Size(1152, 412);

            // dataGridView2
            this.dataGridView2.AllowUserToAddRows = false;
            this.dataGridView2.AllowUserToDeleteRows = false;
            this.dataGridView2.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dataGridView2.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView2.Location = new System.Drawing.Point(10, 10);
            this.dataGridView2.Name = "dataGridView2";
            this.dataGridView2.ReadOnly = true;
            this.dataGridView2.RowTemplate.Height = 25;
            this.dataGridView2.Size = new System.Drawing.Size(1152, 412);

            // panelSummary (ODSTĘPY MIĘDZY SUMAMI – powiększony rozstaw)
            this.panelSummary.BackColor = System.Drawing.Color.FromArgb(30, 41, 59);
            this.panelSummary.Controls.Add(this.listViewTop3);

            this.panelSummary.Controls.Add(this.label3);
            this.panelSummary.Controls.Add(this.textBox2);

            this.panelSummary.Controls.Add(this.label4);
            this.panelSummary.Controls.Add(this.textBoxPrzeterminowane);

            this.panelSummary.Controls.Add(this.label7);
            this.panelSummary.Controls.Add(this.textBoxZblizajace);

            this.panelSummary.Controls.Add(this.label5);
            this.panelSummary.Controls.Add(this.textBoxWTerminie);

            this.panelSummary.Controls.Add(this.textBoxSumaUbojnia);
            this.panelSummary.Controls.Add(this.lblStatus);
            this.panelSummary.Controls.Add(this.progressBar1);
            this.panelSummary.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelSummary.Location = new System.Drawing.Point(0, 650);
            this.panelSummary.Name = "panelSummary";
            this.panelSummary.Size = new System.Drawing.Size(1200, 100);

            // SUMA – większy odstęp i wyrównanie
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.label3.ForeColor = System.Drawing.Color.White;
            this.label3.Location = new System.Drawing.Point(20, 15);
            this.label3.Text = "SUMA:";

            this.textBox2.BackColor = System.Drawing.Color.FromArgb(254, 240, 138);
            this.textBox2.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox2.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.textBox2.ForeColor = System.Drawing.Color.FromArgb(161, 98, 7);
            this.textBox2.Location = new System.Drawing.Point(85, 10);
            this.textBox2.ReadOnly = true;
            this.textBox2.Size = new System.Drawing.Size(240, 29);
            this.textBox2.Text = "0.00 zł";
            this.textBox2.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            // PRZETERMINOWANE – przesunięte niżej dla odstępów
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.label4.ForeColor = System.Drawing.Color.FromArgb(248, 113, 113);
            this.label4.Location = new System.Drawing.Point(20, 58);
            this.label4.Text = "Przeterminowane:";

            this.textBoxPrzeterminowane.BackColor = System.Drawing.Color.FromArgb(254, 202, 202);
            this.textBoxPrzeterminowane.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBoxPrzeterminowane.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.textBoxPrzeterminowane.ForeColor = System.Drawing.Color.FromArgb(127, 29, 29);
            this.textBoxPrzeterminowane.Location = new System.Drawing.Point(135, 56);
            this.textBoxPrzeterminowane.ReadOnly = true;
            this.textBoxPrzeterminowane.Size = new System.Drawing.Size(190, 18);
            this.textBoxPrzeterminowane.Text = "0.00 zł (0)";
            this.textBoxPrzeterminowane.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            // ZBLIŻAJĄCE – osobna suma (była; wyeksponowana)
            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.label7.ForeColor = System.Drawing.Color.FromArgb(250, 204, 21);
            this.label7.Location = new System.Drawing.Point(350, 18);
            this.label7.Text = "Zbliżające (≤7 dni):";

            this.textBoxZblizajace.BackColor = System.Drawing.Color.FromArgb(254, 249, 195);
            this.textBoxZblizajace.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBoxZblizajace.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.textBoxZblizajace.ForeColor = System.Drawing.Color.FromArgb(120, 53, 15);
            this.textBoxZblizajace.Location = new System.Drawing.Point(465, 16);
            this.textBoxZblizajace.ReadOnly = true;
            this.textBoxZblizajace.Size = new System.Drawing.Size(190, 18);
            this.textBoxZblizajace.Text = "0.00 zł (0)";
            this.textBoxZblizajace.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            // W TERMINIE – większy odstęp w bok
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.label5.ForeColor = System.Drawing.Color.FromArgb(134, 239, 172);
            this.label5.Location = new System.Drawing.Point(350, 58);
            this.label5.Text = "W terminie:";

            this.textBoxWTerminie.BackColor = System.Drawing.Color.FromArgb(220, 252, 231);
            this.textBoxWTerminie.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBoxWTerminie.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.textBoxWTerminie.ForeColor = System.Drawing.Color.FromArgb(22, 101, 52);
            this.textBoxWTerminie.Location = new System.Drawing.Point(420, 56);
            this.textBoxWTerminie.ReadOnly = true;
            this.textBoxWTerminie.Size = new System.Drawing.Size(190, 18);
            this.textBoxWTerminie.Text = "0.00 zł (0)";
            this.textBoxWTerminie.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            // ukryte pomocnicze
            this.textBoxSumaUbojnia.Visible = false;
            this.textBoxSumaUbojnia.Location = new System.Drawing.Point(670, 16);
            this.textBoxSumaUbojnia.Size = new System.Drawing.Size(10, 23);

            // TOP3 – po prawej
            this.listViewTop3.Anchor = (System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right);
            this.listViewTop3.Location = new System.Drawing.Point(700, 10);
            this.listViewTop3.Name = "listViewTop3";
            this.listViewTop3.Size = new System.Drawing.Size(340, 78);
            this.listViewTop3.View = System.Windows.Forms.View.Details;
            this.listViewTop3.FullRowSelect = true;
            this.listViewTop3.GridLines = true;
            this.listViewTop3.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
                this.colLp, this.colKontrahent, this.colDoZaplacenia
            });
            this.colLp.Text = "Lp."; this.colLp.Width = 40;
            this.colKontrahent.Text = "Kontrahent"; this.colKontrahent.Width = 170;
            this.colDoZaplacenia.Text = "Do zapłaty"; this.colDoZaplacenia.Width = 110;

            // lblStatus
            this.lblStatus.Anchor = (System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right);
            this.lblStatus.AutoSize = true;
            this.lblStatus.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblStatus.ForeColor = System.Drawing.Color.FromArgb(156, 163, 175);
            this.lblStatus.Location = new System.Drawing.Point(20, 82);
            this.lblStatus.Text = "Gotowy";

            // progressBar1
            this.progressBar1.Anchor = (System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right);
            this.progressBar1.Location = new System.Drawing.Point(120, 86);
            this.progressBar1.Size = new System.Drawing.Size(540, 6);
            this.progressBar1.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.progressBar1.Visible = false;

            // Form
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1200, 750);
            this.Controls.Add(this.panelMain);
            this.Controls.Add(this.panelSummary);
            this.Controls.Add(this.panelTop);
            this.Name = "Platnosci";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "System Płatności - Kalendarz1";

            // Resume layouts
            this.panelTop.ResumeLayout(false);
            this.panelHeader.ResumeLayout(false);
            this.panelHeader.PerformLayout();
            this.panelFilters.ResumeLayout(false);
            this.panelFilters.PerformLayout();
            this.panelSummary.ResumeLayout(false);
            this.panelSummary.PerformLayout();
            this.panelMain.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabPageHodowcy.ResumeLayout(false);
            this.tabPageUbojnia.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView2)).EndInit();
        }
        #endregion
    }
}
