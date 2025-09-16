namespace Kalendarz1
{
    partial class Platnosci
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        // Deklaracje kontrolek
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
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.TextBox textBoxPrzeterminowane;
        private System.Windows.Forms.TextBox textBoxWTerminie;
        private System.Windows.Forms.TextBox textBoxSumaUbojnia;
        private System.Windows.Forms.Label lblTytul;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.ProgressBar progressBar1;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Platnosci));

            // Główne kontrolki
            this.panelTop = new System.Windows.Forms.Panel();
            this.panelHeader = new System.Windows.Forms.Panel();
            this.panelFilters = new System.Windows.Forms.Panel();
            this.panelSummary = new System.Windows.Forms.Panel();
            this.panelMain = new System.Windows.Forms.Panel();

            // Tab Control
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPageHodowcy = new System.Windows.Forms.TabPage();
            this.tabPageUbojnia = new System.Windows.Forms.TabPage();

            // DataGridViews
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.dataGridView2 = new System.Windows.Forms.DataGridView();

            // Przyciski
            this.refreshButton = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.buttonExportExcel = new System.Windows.Forms.Button();

            // Filtry i wyszukiwanie
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.showAllCheckBox = new System.Windows.Forms.CheckBox();
            this.comboBoxFiltr = new System.Windows.Forms.ComboBox();

            // Podsumowanie
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.textBoxPrzeterminowane = new System.Windows.Forms.TextBox();
            this.textBoxWTerminie = new System.Windows.Forms.TextBox();
            this.textBoxSumaUbojnia = new System.Windows.Forms.TextBox();

            // Etykiety
            this.lblTytul = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.lblStatus = new System.Windows.Forms.Label();

            // Inne
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.progressBar1 = new System.Windows.Forms.ProgressBar();

            // SuspendLayout
            this.panelTop.SuspendLayout();
            this.panelHeader.SuspendLayout();
            this.panelFilters.SuspendLayout();
            this.panelSummary.SuspendLayout();
            this.panelMain.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabPageHodowcy.SuspendLayout();
            this.tabPageUbojnia.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView2)).BeginInit();
            this.SuspendLayout();

            // 
            // panelTop
            // 
            this.panelTop.BackColor = System.Drawing.Color.White;
            this.panelTop.Controls.Add(this.panelHeader);
            this.panelTop.Controls.Add(this.panelFilters);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(0, 0);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(1200, 140);
            this.panelTop.TabIndex = 0;

            // 
            // panelHeader
            // 
            this.panelHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(37)))), ((int)(((byte)(99)))), ((int)(((byte)(235)))));
            this.panelHeader.Controls.Add(this.lblTytul);
            this.panelHeader.Controls.Add(this.refreshButton);
            this.panelHeader.Controls.Add(this.button1);
            this.panelHeader.Controls.Add(this.buttonExportExcel);
            this.panelHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelHeader.Location = new System.Drawing.Point(0, 0);
            this.panelHeader.Name = "panelHeader";
            this.panelHeader.Size = new System.Drawing.Size(1200, 70);
            this.panelHeader.TabIndex = 0;

            // 
            // lblTytul
            // 
            this.lblTytul.AutoSize = true;
            this.lblTytul.Font = new System.Drawing.Font("Segoe UI", 20F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblTytul.ForeColor = System.Drawing.Color.White;
            this.lblTytul.Location = new System.Drawing.Point(20, 15);
            this.lblTytul.Name = "lblTytul";
            this.lblTytul.Size = new System.Drawing.Size(250, 37);
            this.lblTytul.TabIndex = 0;
            this.lblTytul.Text = "System Płatności";

            // 
            // refreshButton
            // 
            this.refreshButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.refreshButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(34)))), ((int)(((byte)(197)))), ((int)(((byte)(94)))));
            this.refreshButton.FlatAppearance.BorderSize = 0;
            this.refreshButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.refreshButton.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.refreshButton.ForeColor = System.Drawing.Color.White;
            this.refreshButton.Location = new System.Drawing.Point(850, 15);
            this.refreshButton.Name = "refreshButton";
            this.refreshButton.Size = new System.Drawing.Size(100, 40);
            this.refreshButton.TabIndex = 1;
            this.refreshButton.Text = "Odśwież";
            this.refreshButton.UseVisualStyleBackColor = false;
            this.refreshButton.Click += new System.EventHandler(this.refreshButton_Click);

            // 
            // button1
            // 
            this.button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.button1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(59)))), ((int)(((byte)(130)))), ((int)(((byte)(246)))));
            this.button1.FlatAppearance.BorderSize = 0;
            this.button1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button1.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.button1.ForeColor = System.Drawing.Color.White;
            this.button1.Location = new System.Drawing.Point(960, 15);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(100, 40);
            this.button1.TabIndex = 2;
            this.button1.Text = "Raport";
            this.button1.UseVisualStyleBackColor = false;
            this.button1.Click += new System.EventHandler(this.button1_Click_1);

            // 
            // buttonExportExcel
            // 
            this.buttonExportExcel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonExportExcel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(16)))), ((int)(((byte)(185)))), ((int)(((byte)(129)))));
            this.buttonExportExcel.FlatAppearance.BorderSize = 0;
            this.buttonExportExcel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonExportExcel.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.buttonExportExcel.ForeColor = System.Drawing.Color.White;
            this.buttonExportExcel.Location = new System.Drawing.Point(1070, 15);
            this.buttonExportExcel.Name = "buttonExportExcel";
            this.buttonExportExcel.Size = new System.Drawing.Size(100, 40);
            this.buttonExportExcel.TabIndex = 3;
            this.buttonExportExcel.Text = "Excel";
            this.buttonExportExcel.UseVisualStyleBackColor = false;
            this.buttonExportExcel.Click += new System.EventHandler(this.buttonExportExcel_Click);

            // 
            // panelFilters
            // 
            this.panelFilters.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this.panelFilters.Controls.Add(this.label6);
            this.panelFilters.Controls.Add(this.textBox1);
            this.panelFilters.Controls.Add(this.comboBoxFiltr);
            this.panelFilters.Controls.Add(this.showAllCheckBox);
            this.panelFilters.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelFilters.Location = new System.Drawing.Point(0, 70);
            this.panelFilters.Name = "panelFilters";
            this.panelFilters.Size = new System.Drawing.Size(1200, 70);
            this.panelFilters.TabIndex = 1;

            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label6.Location = new System.Drawing.Point(20, 25);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(108, 19);
            this.label6.TabIndex = 0;
            this.label6.Text = "Szukaj hodowcy:";

            // 
            // textBox1
            // 
            this.textBox1.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.textBox1.Location = new System.Drawing.Point(134, 21);
            this.textBox1.Name = "textBox1";
            this.textBox1.PlaceholderText = "Wpisz nazwę...";
            this.textBox1.Size = new System.Drawing.Size(250, 27);
            this.textBox1.TabIndex = 1;

            // 
            // comboBoxFiltr
            // 
            this.comboBoxFiltr.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxFiltr.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.comboBoxFiltr.FormattingEnabled = true;
            this.comboBoxFiltr.Items.AddRange(new object[] {
            "Wszystkie",
            "Przeterminowane",
            "Do 7 dni",
            "W terminie"});
            this.comboBoxFiltr.Location = new System.Drawing.Point(400, 21);
            this.comboBoxFiltr.Name = "comboBoxFiltr";
            this.comboBoxFiltr.Size = new System.Drawing.Size(180, 28);
            this.comboBoxFiltr.TabIndex = 2;
            this.comboBoxFiltr.SelectedIndexChanged += new System.EventHandler(this.comboBoxFiltr_SelectedIndexChanged);

            // 
            // showAllCheckBox
            // 
            this.showAllCheckBox.AutoSize = true;
            this.showAllCheckBox.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.showAllCheckBox.Location = new System.Drawing.Point(600, 24);
            this.showAllCheckBox.Name = "showAllCheckBox";
            this.showAllCheckBox.Size = new System.Drawing.Size(169, 23);
            this.showAllCheckBox.TabIndex = 3;
            this.showAllCheckBox.Text = "Pokaż wszystkie kolumny";
            this.showAllCheckBox.UseVisualStyleBackColor = true;

            // 
            // panelMain
            // 
            this.panelMain.Controls.Add(this.tabControl1);
            this.panelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelMain.Location = new System.Drawing.Point(0, 140);
            this.panelMain.Name = "panelMain";
            this.panelMain.Padding = new System.Windows.Forms.Padding(10);
            this.panelMain.Size = new System.Drawing.Size(1200, 500);
            this.panelMain.TabIndex = 1;

            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPageHodowcy);
            this.tabControl1.Controls.Add(this.tabPageUbojnia);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.tabControl1.ItemSize = new System.Drawing.Size(200, 40);
            this.tabControl1.Location = new System.Drawing.Point(10, 10);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(1180, 480);
            this.tabControl1.SizeMode = System.Windows.Forms.TabSizeMode.Fixed;
            this.tabControl1.TabIndex = 0;
            this.tabControl1.SelectedIndexChanged += new System.EventHandler(this.tabControl1_SelectedIndexChanged);

            // 
            // tabPageHodowcy
            // 
            this.tabPageHodowcy.Controls.Add(this.dataGridView1);
            this.tabPageHodowcy.Location = new System.Drawing.Point(4, 44);
            this.tabPageHodowcy.Name = "tabPageHodowcy";
            this.tabPageHodowcy.Padding = new System.Windows.Forms.Padding(10);
            this.tabPageHodowcy.Size = new System.Drawing.Size(1172, 432);
            this.tabPageHodowcy.TabIndex = 0;
            this.tabPageHodowcy.Text = "Płatności DO Hodowców";
            this.tabPageHodowcy.UseVisualStyleBackColor = true;

            // 
            // tabPageUbojnia
            // 
            this.tabPageUbojnia.Controls.Add(this.dataGridView2);
            this.tabPageUbojnia.Location = new System.Drawing.Point(4, 44);
            this.tabPageUbojnia.Name = "tabPageUbojnia";
            this.tabPageUbojnia.Padding = new System.Windows.Forms.Padding(10);
            this.tabPageUbojnia.Size = new System.Drawing.Size(1172, 432);
            this.tabPageUbojnia.TabIndex = 1;
            this.tabPageUbojnia.Text = "Płatności OD Ubojni";
            this.tabPageUbojnia.UseVisualStyleBackColor = true;

            // 
            // dataGridView1
            // 
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
            this.dataGridView1.TabIndex = 0;

            // 
            // dataGridView2
            // 
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
            this.dataGridView2.TabIndex = 0;

            // 
            // panelSummary
            // 
            this.panelSummary.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(41)))), ((int)(((byte)(59)))));
            this.panelSummary.Controls.Add(this.label3);
            this.panelSummary.Controls.Add(this.textBox2);
            this.panelSummary.Controls.Add(this.label4);
            this.panelSummary.Controls.Add(this.textBoxPrzeterminowane);
            this.panelSummary.Controls.Add(this.label5);
            this.panelSummary.Controls.Add(this.textBoxWTerminie);
            this.panelSummary.Controls.Add(this.textBoxSumaUbojnia);
            this.panelSummary.Controls.Add(this.lblStatus);
            this.panelSummary.Controls.Add(this.progressBar1);
            this.panelSummary.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelSummary.Location = new System.Drawing.Point(0, 640);
            this.panelSummary.Name = "panelSummary";
            this.panelSummary.Size = new System.Drawing.Size(1200, 100);
            this.panelSummary.TabIndex = 2;

            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label3.ForeColor = System.Drawing.Color.White;
            this.label3.Location = new System.Drawing.Point(20, 20);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(52, 19);
            this.label3.TabIndex = 0;
            this.label3.Text = "SUMA:";

            // 
            // textBox2
            // 
            this.textBox2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(254)))), ((int)(((byte)(240)))), ((int)(((byte)(138)))));
            this.textBox2.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox2.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.textBox2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(161)))), ((int)(((byte)(98)))), ((int)(((byte)(7)))));
            this.textBox2.Location = new System.Drawing.Point(80, 15);
            this.textBox2.Name = "textBox2";
            this.textBox2.ReadOnly = true;
            this.textBox2.Size = new System.Drawing.Size(200, 29);
            this.textBox2.TabIndex = 1;
            this.textBox2.Text = "0.00 zł";
            this.textBox2.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label4.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(113)))), ((int)(((byte)(113)))));
            this.label4.Location = new System.Drawing.Point(20, 55);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(95, 15);
            this.label4.TabIndex = 2;
            this.label4.Text = "Przeterminowane:";

            // 
            // textBoxPrzeterminowane
            // 
            this.textBoxPrzeterminowane.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(254)))), ((int)(((byte)(202)))), ((int)(((byte)(202)))));
            this.textBoxPrzeterminowane.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBoxPrzeterminowane.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.textBoxPrzeterminowane.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(127)))), ((int)(((byte)(29)))), ((int)(((byte)(29)))));
            this.textBoxPrzeterminowane.Location = new System.Drawing.Point(120, 52);
            this.textBoxPrzeterminowane.Name = "textBoxPrzeterminowane";
            this.textBoxPrzeterminowane.ReadOnly = true;
            this.textBoxPrzeterminowane.Size = new System.Drawing.Size(160, 18);
            this.textBoxPrzeterminowane.TabIndex = 3;
            this.textBoxPrzeterminowane.Text = "0.00 zł (0)";
            this.textBoxPrzeterminowane.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label5.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(134)))), ((int)(((byte)(239)))), ((int)(((byte)(172)))));
            this.label5.Location = new System.Drawing.Point(300, 20);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(66, 15);
            this.label5.TabIndex = 4;
            this.label5.Text = "W terminie:";

            // 
            // textBoxWTerminie
            // 
            this.textBoxWTerminie.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(252)))), ((int)(((byte)(231)))));
            this.textBoxWTerminie.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBoxWTerminie.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.textBoxWTerminie.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(22)))), ((int)(((byte)(101)))), ((int)(((byte)(52)))));
            this.textBoxWTerminie.Location = new System.Drawing.Point(370, 17);
            this.textBoxWTerminie.Name = "textBoxWTerminie";
            this.textBoxWTerminie.ReadOnly = true;
            this.textBoxWTerminie.Size = new System.Drawing.Size(160, 18);
            this.textBoxWTerminie.TabIndex = 5;
            this.textBoxWTerminie.Text = "0.00 zł (0)";
            this.textBoxWTerminie.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            // 
            // textBoxSumaUbojnia
            // 
            this.textBoxSumaUbojnia.Visible = false;
            this.textBoxSumaUbojnia.Location = new System.Drawing.Point(550, 17);
            this.textBoxSumaUbojnia.Name = "textBoxSumaUbojnia";
            this.textBoxSumaUbojnia.Size = new System.Drawing.Size(10, 23);
            this.textBoxSumaUbojnia.TabIndex = 6;

            // 
            // lblStatus
            // 
            this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.lblStatus.AutoSize = true;
            this.lblStatus.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(156)))), ((int)(((byte)(163)))), ((int)(((byte)(175)))));
            this.lblStatus.Location = new System.Drawing.Point(900, 70);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(280, 15);
            this.lblStatus.TabIndex = 7;
            this.lblStatus.Text = "Gotowy";
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

            // 
            // progressBar1
            // 
            this.progressBar1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar1.Location = new System.Drawing.Point(20, 75);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(870, 10);
            this.progressBar1.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.progressBar1.TabIndex = 8;
            this.progressBar1.Visible = false;

            // 
            // Platnosci
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1200, 740);
            this.Controls.Add(this.panelMain);
            this.Controls.Add(this.panelSummary);
            this.Controls.Add(this.panelTop);
            this.Name = "Platnosci";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "System Płatności - Kalendarz1";
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
            this.ResumeLayout(false);

        }

        #endregion
    }
}