namespace Kalendarz1
{
    partial class CRM
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            this.mainTableLayout = new System.Windows.Forms.TableLayoutPanel();
            this.topPanel = new System.Windows.Forms.Panel();
            this.groupBoxRanking = new System.Windows.Forms.GroupBox();
            this.dataGridViewRanking = new System.Windows.Forms.DataGridView();
            this.groupBoxFiltry = new System.Windows.Forms.GroupBox();
            this.flowLayoutFilters = new System.Windows.Forms.FlowLayoutPanel();
            this.panelStatus = new System.Windows.Forms.Panel();
            this.label1 = new System.Windows.Forms.Label();
            this.comboBoxStatusFilter = new System.Windows.Forms.ComboBox();
            this.panelPowiat = new System.Windows.Forms.Panel();
            this.label2 = new System.Windows.Forms.Label();
            this.comboBoxPowiatFilter = new System.Windows.Forms.ComboBox();
            this.panelWoj = new System.Windows.Forms.Panel();
            this.label5 = new System.Windows.Forms.Label();
            this.comboBoxWoj = new System.Windows.Forms.ComboBox();
            this.panelPKD = new System.Windows.Forms.Panel();
            this.label4 = new System.Windows.Forms.Label();
            this.comboBoxPKD = new System.Windows.Forms.ComboBox();
            this.bottomPanel = new System.Windows.Forms.Panel();
            this.splitContainerMain = new System.Windows.Forms.SplitContainer();
            this.panelOdbiorcy = new System.Windows.Forms.Panel();
            this.dataGridViewOdbiorcy = new System.Windows.Forms.DataGridView();
            this.panelSearch = new System.Windows.Forms.Panel();
            this.flowLayoutButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.button3 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.textBoxSzukaj = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.panelNotatki = new System.Windows.Forms.Panel();
            this.splitContainerNotatki = new System.Windows.Forms.SplitContainer();
            this.groupBoxNotatki = new System.Windows.Forms.GroupBox();
            this.dataGridViewNotatki = new System.Windows.Forms.DataGridView();
            this.panelDodajNotatke = new System.Windows.Forms.Panel();
            this.buttonDodajNotatke = new System.Windows.Forms.Button();
            this.textBoxNotatka = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.groupBoxHistoria = new System.Windows.Forms.GroupBox();
            this.dataGridViewHistoria = new System.Windows.Forms.DataGridView();
            this.mainTableLayout.SuspendLayout();
            this.topPanel.SuspendLayout();
            this.groupBoxRanking.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewRanking)).BeginInit();
            this.groupBoxFiltry.SuspendLayout();
            this.flowLayoutFilters.SuspendLayout();
            this.panelStatus.SuspendLayout();
            this.panelPowiat.SuspendLayout();
            this.panelWoj.SuspendLayout();
            this.panelPKD.SuspendLayout();
            this.bottomPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).BeginInit();
            this.splitContainerMain.Panel1.SuspendLayout();
            this.splitContainerMain.Panel2.SuspendLayout();
            this.splitContainerMain.SuspendLayout();
            this.panelOdbiorcy.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewOdbiorcy)).BeginInit();
            this.panelSearch.SuspendLayout();
            this.flowLayoutButtons.SuspendLayout();
            this.panelNotatki.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerNotatki)).BeginInit();
            this.splitContainerNotatki.Panel1.SuspendLayout();
            this.splitContainerNotatki.Panel2.SuspendLayout();
            this.splitContainerNotatki.SuspendLayout();
            this.groupBoxNotatki.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewNotatki)).BeginInit();
            this.panelDodajNotatke.SuspendLayout();
            this.groupBoxHistoria.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewHistoria)).BeginInit();
            this.SuspendLayout();
            // 
            // mainTableLayout
            // 
            this.mainTableLayout.ColumnCount = 1;
            this.mainTableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.mainTableLayout.Controls.Add(this.topPanel, 0, 0);
            this.mainTableLayout.Controls.Add(this.bottomPanel, 0, 1);
            this.mainTableLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainTableLayout.Location = new System.Drawing.Point(0, 0);
            this.mainTableLayout.Name = "mainTableLayout";
            this.mainTableLayout.RowCount = 2;
            this.mainTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 220F));
            this.mainTableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.mainTableLayout.Size = new System.Drawing.Size(1600, 900);
            this.mainTableLayout.TabIndex = 0;
            // 
            // topPanel
            // 
            this.topPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(249)))), ((int)(((byte)(252)))));
            this.topPanel.Controls.Add(this.groupBoxRanking);
            this.topPanel.Controls.Add(this.groupBoxFiltry);
            this.topPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.topPanel.Location = new System.Drawing.Point(3, 3);
            this.topPanel.Name = "topPanel";
            this.topPanel.Padding = new System.Windows.Forms.Padding(10);
            this.topPanel.Size = new System.Drawing.Size(1594, 214);
            this.topPanel.TabIndex = 0;
            // 
            // groupBoxRanking
            // 
            this.groupBoxRanking.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxRanking.Controls.Add(this.dataGridViewRanking);
            this.groupBoxRanking.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.groupBoxRanking.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(60)))), ((int)(((byte)(90)))));
            this.groupBoxRanking.Location = new System.Drawing.Point(624, 13);
            this.groupBoxRanking.Name = "groupBoxRanking";
            this.groupBoxRanking.Padding = new System.Windows.Forms.Padding(10);
            this.groupBoxRanking.Size = new System.Drawing.Size(957, 188);
            this.groupBoxRanking.TabIndex = 1;
            this.groupBoxRanking.TabStop = false;
            this.groupBoxRanking.Text = ">> Ranking Handlowców";
            // 
            // dataGridViewRanking
            // 
            this.dataGridViewRanking.AllowUserToAddRows = false;
            this.dataGridViewRanking.AllowUserToDeleteRows = false;
            this.dataGridViewRanking.AllowUserToResizeRows = false;
            this.dataGridViewRanking.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridViewRanking.BackgroundColor = System.Drawing.Color.White;
            this.dataGridViewRanking.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dataGridViewRanking.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            dataGridViewCellStyle1.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle1.Padding = new System.Windows.Forms.Padding(5);
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridViewRanking.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.dataGridViewRanking.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewRanking.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewRanking.EnableHeadersVisualStyles = false;
            this.dataGridViewRanking.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(230)))), ((int)(((byte)(235)))));
            this.dataGridViewRanking.Location = new System.Drawing.Point(10, 28);
            this.dataGridViewRanking.Name = "dataGridViewRanking";
            this.dataGridViewRanking.ReadOnly = true;
            this.dataGridViewRanking.RowHeadersVisible = false;
            this.dataGridViewRanking.RowTemplate.Height = 32;
            this.dataGridViewRanking.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewRanking.Size = new System.Drawing.Size(937, 150);
            this.dataGridViewRanking.TabIndex = 0;
            this.dataGridViewRanking.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.DataGridViewRanking_CellFormatting);
            // 
            // groupBoxFiltry
            // 
            this.groupBoxFiltry.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)));
            this.groupBoxFiltry.Controls.Add(this.flowLayoutFilters);
            this.groupBoxFiltry.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.groupBoxFiltry.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(60)))), ((int)(((byte)(90)))));
            this.groupBoxFiltry.Location = new System.Drawing.Point(13, 13);
            this.groupBoxFiltry.Name = "groupBoxFiltry";
            this.groupBoxFiltry.Padding = new System.Windows.Forms.Padding(10);
            this.groupBoxFiltry.Size = new System.Drawing.Size(605, 188);
            this.groupBoxFiltry.TabIndex = 0;
            this.groupBoxFiltry.TabStop = false;
            this.groupBoxFiltry.Text = ">> Filtry Wyszukiwania";
            // 
            // flowLayoutFilters
            // 
            this.flowLayoutFilters.Controls.Add(this.panelStatus);
            this.flowLayoutFilters.Controls.Add(this.panelPowiat);
            this.flowLayoutFilters.Controls.Add(this.panelWoj);
            this.flowLayoutFilters.Controls.Add(this.panelPKD);
            this.flowLayoutFilters.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutFilters.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flowLayoutFilters.Location = new System.Drawing.Point(10, 28);
            this.flowLayoutFilters.Name = "flowLayoutFilters";
            this.flowLayoutFilters.Padding = new System.Windows.Forms.Padding(5);
            this.flowLayoutFilters.Size = new System.Drawing.Size(585, 150);
            this.flowLayoutFilters.TabIndex = 0;
            // 
            // panelStatus
            // 
            this.panelStatus.Controls.Add(this.label1);
            this.panelStatus.Controls.Add(this.comboBoxStatusFilter);
            this.panelStatus.Location = new System.Drawing.Point(8, 8);
            this.panelStatus.Name = "panelStatus";
            this.panelStatus.Size = new System.Drawing.Size(280, 30);
            this.panelStatus.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.label1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(70)))), ((int)(((byte)(90)))));
            this.label1.Location = new System.Drawing.Point(3, 6);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(49, 15);
            this.label1.TabIndex = 0;
            this.label1.Text = "Status:";
            // 
            // comboBoxStatusFilter
            // 
            this.comboBoxStatusFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxStatusFilter.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.comboBoxStatusFilter.FormattingEnabled = true;
            this.comboBoxStatusFilter.Location = new System.Drawing.Point(60, 3);
            this.comboBoxStatusFilter.Name = "comboBoxStatusFilter";
            this.comboBoxStatusFilter.Size = new System.Drawing.Size(210, 23);
            this.comboBoxStatusFilter.TabIndex = 1;
            this.comboBoxStatusFilter.SelectedIndexChanged += new System.EventHandler(this.comboBoxStatusFilter_SelectedIndexChanged);
            // 
            // panelPowiat
            // 
            this.panelPowiat.Controls.Add(this.label2);
            this.panelPowiat.Controls.Add(this.comboBoxPowiatFilter);
            this.panelPowiat.Location = new System.Drawing.Point(8, 44);
            this.panelPowiat.Name = "panelPowiat";
            this.panelPowiat.Size = new System.Drawing.Size(280, 30);
            this.panelPowiat.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.label2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(70)))), ((int)(((byte)(90)))));
            this.label2.Location = new System.Drawing.Point(3, 6);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(49, 15);
            this.label2.TabIndex = 0;
            this.label2.Text = "Powiat:";
            // 
            // comboBoxPowiatFilter
            // 
            this.comboBoxPowiatFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxPowiatFilter.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.comboBoxPowiatFilter.FormattingEnabled = true;
            this.comboBoxPowiatFilter.Location = new System.Drawing.Point(60, 3);
            this.comboBoxPowiatFilter.Name = "comboBoxPowiatFilter";
            this.comboBoxPowiatFilter.Size = new System.Drawing.Size(210, 23);
            this.comboBoxPowiatFilter.TabIndex = 1;
            this.comboBoxPowiatFilter.SelectedIndexChanged += new System.EventHandler(this.comboBoxPowiatFilter_SelectedIndexChanged);
            // 
            // panelWoj
            // 
            this.panelWoj.Controls.Add(this.label5);
            this.panelWoj.Controls.Add(this.comboBoxWoj);
            this.panelWoj.Location = new System.Drawing.Point(8, 80);
            this.panelWoj.Name = "panelWoj";
            this.panelWoj.Size = new System.Drawing.Size(280, 30);
            this.panelWoj.TabIndex = 2;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.label5.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(70)))), ((int)(((byte)(90)))));
            this.label5.Location = new System.Drawing.Point(3, 6);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(35, 15);
            this.label5.TabIndex = 0;
            this.label5.Text = "Woj.:";
            // 
            // comboBoxWoj
            // 
            this.comboBoxWoj.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxWoj.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.comboBoxWoj.FormattingEnabled = true;
            this.comboBoxWoj.Location = new System.Drawing.Point(60, 3);
            this.comboBoxWoj.Name = "comboBoxWoj";
            this.comboBoxWoj.Size = new System.Drawing.Size(210, 23);
            this.comboBoxWoj.TabIndex = 1;
            this.comboBoxWoj.SelectedIndexChanged += new System.EventHandler(this.ZastosujFiltry);
            // 
            // panelPKD
            // 
            this.panelPKD.Controls.Add(this.label4);
            this.panelPKD.Controls.Add(this.comboBoxPKD);
            this.panelPKD.Location = new System.Drawing.Point(294, 8);
            this.panelPKD.Name = "panelPKD";
            this.panelPKD.Size = new System.Drawing.Size(280, 66);
            this.panelPKD.TabIndex = 3;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.label4.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(70)))), ((int)(((byte)(90)))));
            this.label4.Location = new System.Drawing.Point(3, 6);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(46, 15);
            this.label4.TabIndex = 0;
            this.label4.Text = "Rodzaj:";
            // 
            // comboBoxPKD
            // 
            this.comboBoxPKD.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxPKD.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxPKD.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.comboBoxPKD.FormattingEnabled = true;
            this.comboBoxPKD.Location = new System.Drawing.Point(6, 24);
            this.comboBoxPKD.Name = "comboBoxPKD";
            this.comboBoxPKD.Size = new System.Drawing.Size(271, 23);
            this.comboBoxPKD.TabIndex = 1;
            this.comboBoxPKD.SelectedIndexChanged += new System.EventHandler(this.ZastosujFiltry);
            // 
            // bottomPanel
            // 
            this.bottomPanel.BackColor = System.Drawing.Color.White;
            this.bottomPanel.Controls.Add(this.splitContainerMain);
            this.bottomPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.bottomPanel.Location = new System.Drawing.Point(3, 223);
            this.bottomPanel.Name = "bottomPanel";
            this.bottomPanel.Padding = new System.Windows.Forms.Padding(10, 0, 10, 10);
            this.bottomPanel.Size = new System.Drawing.Size(1594, 674);
            this.bottomPanel.TabIndex = 1;
            // 
            // splitContainerMain
            // 
            this.splitContainerMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainerMain.Location = new System.Drawing.Point(10, 0);
            this.splitContainerMain.Name = "splitContainerMain";
            // 
            // splitContainerMain.Panel1
            // 
            this.splitContainerMain.Panel1.Controls.Add(this.panelOdbiorcy);
            this.splitContainerMain.Panel1MinSize = 800;
            // 
            // splitContainerMain.Panel2
            // 
            this.splitContainerMain.Panel2.Controls.Add(this.panelNotatki);
            this.splitContainerMain.Panel2MinSize = 350;
            this.splitContainerMain.Size = new System.Drawing.Size(1574, 664);
            this.splitContainerMain.SplitterDistance = 1210;
            this.splitContainerMain.SplitterWidth = 6;
            this.splitContainerMain.TabIndex = 0;
            // 
            // panelOdbiorcy
            // 
            this.panelOdbiorcy.Controls.Add(this.dataGridViewOdbiorcy);
            this.panelOdbiorcy.Controls.Add(this.panelSearch);
            this.panelOdbiorcy.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelOdbiorcy.Location = new System.Drawing.Point(0, 0);
            this.panelOdbiorcy.Name = "panelOdbiorcy";
            this.panelOdbiorcy.Size = new System.Drawing.Size(1210, 664);
            this.panelOdbiorcy.TabIndex = 0;
            // 
            // dataGridViewOdbiorcy
            // 
            this.dataGridViewOdbiorcy.AllowUserToAddRows = false;
            this.dataGridViewOdbiorcy.AllowUserToDeleteRows = false;
            this.dataGridViewOdbiorcy.AllowUserToResizeRows = false;
            this.dataGridViewOdbiorcy.BackgroundColor = System.Drawing.Color.White;
            this.dataGridViewOdbiorcy.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dataGridViewOdbiorcy.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(80)))));
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            dataGridViewCellStyle2.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle2.Padding = new System.Windows.Forms.Padding(5, 8, 5, 8);
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(62)))), ((int)(((byte)(80)))));
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridViewOdbiorcy.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            this.dataGridViewOdbiorcy.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewOdbiorcy.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewOdbiorcy.EnableHeadersVisualStyles = false;
            this.dataGridViewOdbiorcy.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(236)))), ((int)(((byte)(240)))), ((int)(((byte)(245)))));
            this.dataGridViewOdbiorcy.Location = new System.Drawing.Point(0, 52);
            this.dataGridViewOdbiorcy.Name = "dataGridViewOdbiorcy";
            this.dataGridViewOdbiorcy.RowHeadersVisible = false;
            this.dataGridViewOdbiorcy.RowTemplate.Height = 50;
            this.dataGridViewOdbiorcy.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewOdbiorcy.Size = new System.Drawing.Size(1210, 612);
            this.dataGridViewOdbiorcy.TabIndex = 1;
            this.dataGridViewOdbiorcy.CellEnter += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridViewOdbiorcy_CellEnter);
            this.dataGridViewOdbiorcy.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridViewOdbiorcy_CellValueChanged);
            this.dataGridViewOdbiorcy.CurrentCellDirtyStateChanged += new System.EventHandler(this.dataGridViewOdbiorcy_CurrentCellDirtyStateChanged);
            this.dataGridViewOdbiorcy.RowPrePaint += new System.Windows.Forms.DataGridViewRowPrePaintEventHandler(this.dataGridViewOdbiorcy_RowPrePaint);
            // 
            // panelSearch
            // 
            this.panelSearch.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(249)))), ((int)(((byte)(252)))));
            this.panelSearch.Controls.Add(this.flowLayoutButtons);
            this.panelSearch.Controls.Add(this.textBoxSzukaj);
            this.panelSearch.Controls.Add(this.label6);
            this.panelSearch.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelSearch.Location = new System.Drawing.Point(0, 0);
            this.panelSearch.Name = "panelSearch";
            this.panelSearch.Padding = new System.Windows.Forms.Padding(10);
            this.panelSearch.Size = new System.Drawing.Size(1210, 52);
            this.panelSearch.TabIndex = 0;
            // 
            // flowLayoutButtons
            // 
            this.flowLayoutButtons.AutoSize = true;
            this.flowLayoutButtons.Controls.Add(this.button3);
            this.flowLayoutButtons.Controls.Add(this.button2);
            this.flowLayoutButtons.Dock = System.Windows.Forms.DockStyle.Right;
            this.flowLayoutButtons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.flowLayoutButtons.Location = new System.Drawing.Point(494, 10);
            this.flowLayoutButtons.Name = "flowLayoutButtons";
            this.flowLayoutButtons.Padding = new System.Windows.Forms.Padding(0, 0, 5, 0);
            this.flowLayoutButtons.Size = new System.Drawing.Size(706, 32);
            this.flowLayoutButtons.TabIndex = 4;
            this.flowLayoutButtons.WrapContents = false;
            // 
            // button3
            // 
            this.button3.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(39)))), ((int)(((byte)(174)))), ((int)(((byte)(96)))));
            this.button3.FlatAppearance.BorderSize = 0;
            this.button3.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button3.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.button3.ForeColor = System.Drawing.Color.White;
            this.button3.Location = new System.Drawing.Point(593, 0);
            this.button3.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(105, 32);
            this.button3.TabIndex = 3;
            this.button3.Text = "🗺 Trasa";
            this.button3.UseVisualStyleBackColor = false;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // button2
            // 
            this.button2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(231)))), ((int)(((byte)(76)))), ((int)(((byte)(60)))));
            this.button2.FlatAppearance.BorderSize = 0;
            this.button2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button2.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.button2.ForeColor = System.Drawing.Color.White;
            this.button2.Location = new System.Drawing.Point(482, 0);
            this.button2.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(105, 32);
            this.button2.TabIndex = 2;
            this.button2.Text = "🔍 Google";
            this.button2.UseVisualStyleBackColor = false;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // textBoxSzukaj
            // 
            this.textBoxSzukaj.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBoxSzukaj.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.textBoxSzukaj.Location = new System.Drawing.Point(75, 14);
            this.textBoxSzukaj.Name = "textBoxSzukaj";
            this.textBoxSzukaj.Size = new System.Drawing.Size(250, 25);
            this.textBoxSzukaj.TabIndex = 1;
            this.textBoxSzukaj.TextChanged += new System.EventHandler(this.textBoxSzukaj_TextChanged);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.label6.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(60)))), ((int)(((byte)(90)))));
            this.label6.Location = new System.Drawing.Point(13, 16);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(56, 19);
            this.label6.TabIndex = 0;
            this.label6.Text = "Szukaj:";
            // 
            // panelNotatki
            // 
            this.panelNotatki.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(251)))), ((int)(((byte)(253)))));
            this.panelNotatki.Controls.Add(this.splitContainerNotatki);
            this.panelNotatki.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelNotatki.Location = new System.Drawing.Point(0, 0);
            this.panelNotatki.Name = "panelNotatki";
            this.panelNotatki.Padding = new System.Windows.Forms.Padding(5);
            this.panelNotatki.Size = new System.Drawing.Size(358, 664);
            this.panelNotatki.TabIndex = 0;
            // 
            // splitContainerNotatki
            // 
            this.splitContainerNotatki.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerNotatki.Location = new System.Drawing.Point(5, 5);
            this.splitContainerNotatki.Name = "splitContainerNotatki";
            this.splitContainerNotatki.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainerNotatki.Panel1
            // 
            this.splitContainerNotatki.Panel1.Controls.Add(this.groupBoxNotatki);
            this.splitContainerNotatki.Panel1.Controls.Add(this.panelDodajNotatke);
            // 
            // splitContainerNotatki.Panel2
            // 
            this.splitContainerNotatki.Panel2.Controls.Add(this.groupBoxHistoria);
            this.splitContainerNotatki.Size = new System.Drawing.Size(348, 654);
            this.splitContainerNotatki.SplitterDistance = 370;
            this.splitContainerNotatki.TabIndex = 0;
            // 
            // groupBoxNotatki
            // 
            this.groupBoxNotatki.Controls.Add(this.dataGridViewNotatki);
            this.groupBoxNotatki.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxNotatki.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.groupBoxNotatki.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(60)))), ((int)(((byte)(90)))));
            this.groupBoxNotatki.Location = new System.Drawing.Point(0, 0);
            this.groupBoxNotatki.Name = "groupBoxNotatki";
            this.groupBoxNotatki.Padding = new System.Windows.Forms.Padding(8);
            this.groupBoxNotatki.Size = new System.Drawing.Size(348, 190);
            this.groupBoxNotatki.TabIndex = 0;
            this.groupBoxNotatki.TabStop = false;
            this.groupBoxNotatki.Text = "📝 Notatki";
            // 
            // dataGridViewNotatki
            // 
            this.dataGridViewNotatki.AllowUserToAddRows = false;
            this.dataGridViewNotatki.AllowUserToDeleteRows = false;
            this.dataGridViewNotatki.AllowUserToResizeRows = false;
            this.dataGridViewNotatki.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridViewNotatki.BackgroundColor = System.Drawing.Color.White;
            this.dataGridViewNotatki.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dataGridViewNotatki.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Bold);
            dataGridViewCellStyle3.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle3.Padding = new System.Windows.Forms.Padding(3);
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridViewNotatki.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle3;
            this.dataGridViewNotatki.ColumnHeadersHeight = 28;
            this.dataGridViewNotatki.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewNotatki.EnableHeadersVisualStyles = false;
            this.dataGridViewNotatki.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(230)))), ((int)(((byte)(235)))));
            this.dataGridViewNotatki.Location = new System.Drawing.Point(8, 26);
            this.dataGridViewNotatki.Name = "dataGridViewNotatki";
            this.dataGridViewNotatki.ReadOnly = true;
            this.dataGridViewNotatki.RowHeadersVisible = false;
            this.dataGridViewNotatki.RowTemplate.Height = 35;
            this.dataGridViewNotatki.Size = new System.Drawing.Size(332, 156);
            this.dataGridViewNotatki.TabIndex = 0;
            // 
            // panelDodajNotatke
            // 
            this.panelDodajNotatke.Controls.Add(this.buttonDodajNotatke);
            this.panelDodajNotatke.Controls.Add(this.textBoxNotatka);
            this.panelDodajNotatke.Controls.Add(this.label3);
            this.panelDodajNotatke.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelDodajNotatke.Location = new System.Drawing.Point(0, 190);
            this.panelDodajNotatke.Name = "panelDodajNotatke";
            this.panelDodajNotatke.Padding = new System.Windows.Forms.Padding(8);
            this.panelDodajNotatke.Size = new System.Drawing.Size(348, 180);
            this.panelDodajNotatke.TabIndex = 1;
            // 
            // buttonDodajNotatke
            // 
            this.buttonDodajNotatke.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonDodajNotatke.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(46)))), ((int)(((byte)(204)))), ((int)(((byte)(113)))));
            this.buttonDodajNotatke.FlatAppearance.BorderSize = 0;
            this.buttonDodajNotatke.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonDodajNotatke.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.buttonDodajNotatke.ForeColor = System.Drawing.Color.White;
            this.buttonDodajNotatke.Location = new System.Drawing.Point(11, 136);
            this.buttonDodajNotatke.Name = "buttonDodajNotatke";
            this.buttonDodajNotatke.Size = new System.Drawing.Size(326, 36);
            this.buttonDodajNotatke.TabIndex = 2;
            this.buttonDodajNotatke.Text = "✅ Dodaj Notatkę";
            this.buttonDodajNotatke.UseVisualStyleBackColor = false;
            this.buttonDodajNotatke.Click += new System.EventHandler(this.buttonDodajNotatke_Click);
            // 
            // textBoxNotatka
            // 
            this.textBoxNotatka.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxNotatka.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBoxNotatka.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.textBoxNotatka.Location = new System.Drawing.Point(11, 35);
            this.textBoxNotatka.Multiline = true;
            this.textBoxNotatka.Name = "textBoxNotatka";
            this.textBoxNotatka.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBoxNotatka.Size = new System.Drawing.Size(326, 95);
            this.textBoxNotatka.TabIndex = 1;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.label3.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(60)))), ((int)(((byte)(90)))));
            this.label3.Location = new System.Drawing.Point(11, 11);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(110, 17);
            this.label3.TabIndex = 0;
            this.label3.Text = "➕ Nowa Notatka";
            // 
            // groupBoxHistoria
            // 
            this.groupBoxHistoria.Controls.Add(this.dataGridViewHistoria);
            this.groupBoxHistoria.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxHistoria.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.groupBoxHistoria.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(60)))), ((int)(((byte)(90)))));
            this.groupBoxHistoria.Location = new System.Drawing.Point(0, 0);
            this.groupBoxHistoria.Name = "groupBoxHistoria";
            this.groupBoxHistoria.Padding = new System.Windows.Forms.Padding(8);
            this.groupBoxHistoria.Size = new System.Drawing.Size(348, 280);
            this.groupBoxHistoria.TabIndex = 0;
            this.groupBoxHistoria.TabStop = false;
            this.groupBoxHistoria.Text = "📜 Historia Zmian Statusu (TOP 10)";
            // 
            // dataGridViewHistoria
            // 
            this.dataGridViewHistoria.AllowUserToAddRows = false;
            this.dataGridViewHistoria.AllowUserToDeleteRows = false;
            this.dataGridViewHistoria.AllowUserToResizeRows = false;
            this.dataGridViewHistoria.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridViewHistoria.BackgroundColor = System.Drawing.Color.White;
            this.dataGridViewHistoria.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dataGridViewHistoria.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(142)))), ((int)(((byte)(68)))), ((int)(((byte)(173)))));
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Bold);
            dataGridViewCellStyle4.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle4.Padding = new System.Windows.Forms.Padding(3);
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(142)))), ((int)(((byte)(68)))), ((int)(((byte)(173)))));
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridViewHistoria.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle4;
            this.dataGridViewHistoria.ColumnHeadersHeight = 28;
            this.dataGridViewHistoria.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewHistoria.EnableHeadersVisualStyles = false;
            this.dataGridViewHistoria.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(230)))), ((int)(((byte)(235)))));
            this.dataGridViewHistoria.Location = new System.Drawing.Point(8, 26);
            this.dataGridViewHistoria.Name = "dataGridViewHistoria";
            this.dataGridViewHistoria.ReadOnly = true;
            this.dataGridViewHistoria.RowHeadersVisible = false;
            this.dataGridViewHistoria.RowTemplate.Height = 35;
            this.dataGridViewHistoria.Size = new System.Drawing.Size(332, 246);
            this.dataGridViewHistoria.TabIndex = 0;
            // 
            // CRM
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(1600, 900);
            this.Controls.Add(this.mainTableLayout);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.MinimumSize = new System.Drawing.Size(1200, 700);
            this.Name = "CRM";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "System CRM - Zarządzanie Klientami";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.Load += new System.EventHandler(this.CRM_Load);
            this.mainTableLayout.ResumeLayout(false);
            this.topPanel.ResumeLayout(false);
            this.groupBoxRanking.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewRanking)).EndInit();
            this.groupBoxFiltry.ResumeLayout(false);
            this.flowLayoutFilters.ResumeLayout(false);
            this.panelStatus.ResumeLayout(false);
            this.panelStatus.PerformLayout();
            this.panelPowiat.ResumeLayout(false);
            this.panelPowiat.PerformLayout();
            this.panelWoj.ResumeLayout(false);
            this.panelWoj.PerformLayout();
            this.panelPKD.ResumeLayout(false);
            this.panelPKD.PerformLayout();
            this.bottomPanel.ResumeLayout(false);
            this.splitContainerMain.Panel1.ResumeLayout(false);
            this.splitContainerMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).EndInit();
            this.splitContainerMain.ResumeLayout(false);
            this.panelOdbiorcy.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewOdbiorcy)).EndInit();
            this.panelSearch.ResumeLayout(false);
            this.panelSearch.PerformLayout();
            this.flowLayoutButtons.ResumeLayout(false);
            this.panelNotatki.ResumeLayout(false);
            this.splitContainerNotatki.Panel1.ResumeLayout(false);
            this.splitContainerNotatki.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerNotatki)).EndInit();
            this.splitContainerNotatki.ResumeLayout(false);
            this.groupBoxNotatki.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewNotatki)).EndInit();
            this.panelDodajNotatke.ResumeLayout(false);
            this.panelDodajNotatke.PerformLayout();
            this.groupBoxHistoria.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewHistoria)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel mainTableLayout;
        private System.Windows.Forms.Panel topPanel;
        private System.Windows.Forms.GroupBox groupBoxFiltry;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutFilters;
        private System.Windows.Forms.Panel panelStatus;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboBoxStatusFilter;
        private System.Windows.Forms.Panel panelPowiat;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox comboBoxPowiatFilter;
        private System.Windows.Forms.Panel panelWoj;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ComboBox comboBoxWoj;
        private System.Windows.Forms.Panel panelPKD;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox comboBoxPKD;
        private System.Windows.Forms.GroupBox groupBoxRanking;
        private System.Windows.Forms.DataGridView dataGridViewRanking;
        private System.Windows.Forms.Panel bottomPanel;
        private System.Windows.Forms.SplitContainer splitContainerMain;
        private System.Windows.Forms.Panel panelOdbiorcy;
        private System.Windows.Forms.DataGridView dataGridViewOdbiorcy;
        private System.Windows.Forms.Panel panelSearch;
        private System.Windows.Forms.TextBox textBoxSzukaj;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Panel panelNotatki;
        private System.Windows.Forms.SplitContainer splitContainerNotatki;
        private System.Windows.Forms.GroupBox groupBoxNotatki;
        private System.Windows.Forms.DataGridView dataGridViewNotatki;
        private System.Windows.Forms.Panel panelDodajNotatke;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textBoxNotatka;
        private System.Windows.Forms.Button buttonDodajNotatke;
        private System.Windows.Forms.GroupBox groupBoxHistoria;
        private System.Windows.Forms.DataGridView dataGridViewHistoria;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutButtons;
    }
}
