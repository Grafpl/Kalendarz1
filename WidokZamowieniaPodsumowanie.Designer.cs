#nullable disable
namespace Kalendarz1
{
    partial class WidokZamowieniaPodsumowanie
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
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.panelNawigacja = new System.Windows.Forms.Panel();
            this.btnAnuluj = new System.Windows.Forms.Button();
            this.lblZakresDat = new System.Windows.Forms.Label();
            this.btnTydzienNext = new System.Windows.Forms.Button();
            this.btnTydzienPrev = new System.Windows.Forms.Button();
            this.btnNoweZamowienie = new System.Windows.Forms.Button();
            this.btnModyfikuj = new System.Windows.Forms.Button();
            this.btnOdswiez = new System.Windows.Forms.Button();
            this.panelDni = new System.Windows.Forms.FlowLayoutPanel();
            this.btnPon = new System.Windows.Forms.Button();
            this.btnWt = new System.Windows.Forms.Button();
            this.btnSr = new System.Windows.Forms.Button();
            this.btnCzw = new System.Windows.Forms.Button();
            this.btnPt = new System.Windows.Forms.Button();
            this.btnSo = new System.Windows.Forms.Button();
            this.btnNd = new System.Windows.Forms.Button();
            this.panelGlowny = new System.Windows.Forms.TableLayoutPanel();
            this.panelMaster = new System.Windows.Forms.Panel();
            this.dgvZamowienia = new System.Windows.Forms.DataGridView();
            this.panelFiltry = new System.Windows.Forms.Panel();
            this.cbFiltrujHandlowca = new System.Windows.Forms.ComboBox();
            this.txtFiltrujOdbiorce = new System.Windows.Forms.TextBox();
            this.panelPodsumowanie = new System.Windows.Forms.Panel();
            this.lblPodsumowanie = new System.Windows.Forms.Label();
            this.panelDetail = new System.Windows.Forms.TableLayoutPanel();
            this.dgvAgregacja = new System.Windows.Forms.DataGridView();
            this.label2 = new System.Windows.Forms.Label();
            this.panelSzczegolyTop = new System.Windows.Forms.TableLayoutPanel();
            this.panelNotatki = new System.Windows.Forms.Panel();
            this.dgvSzczegoly = new System.Windows.Forms.DataGridView();
            this.txtNotatki = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.panelPrzychody = new System.Windows.Forms.Panel();
            this.dgvPrzychody = new System.Windows.Forms.DataGridView();
            this.label1 = new System.Windows.Forms.Label();
            this.cbFiltrujTowar = new System.Windows.Forms.ComboBox();
            this.tableLayoutPanel1.SuspendLayout();
            this.panelNawigacja.SuspendLayout();
            this.panelDni.SuspendLayout();
            this.panelGlowny.SuspendLayout();
            this.panelMaster.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvZamowienia)).BeginInit();
            this.panelFiltry.SuspendLayout();
            this.panelPodsumowanie.SuspendLayout();
            this.panelDetail.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvAgregacja)).BeginInit();
            this.panelSzczegolyTop.SuspendLayout();
            this.panelNotatki.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvSzczegoly)).BeginInit();
            this.panelPrzychody.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvPrzychody)).BeginInit();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.panelNawigacja, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.panelGlowny, 0, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 70F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1264, 681);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // panelNawigacja
            // 
            this.panelNawigacja.Controls.Add(this.btnAnuluj);
            this.panelNawigacja.Controls.Add(this.lblZakresDat);
            this.panelNawigacja.Controls.Add(this.btnTydzienNext);
            this.panelNawigacja.Controls.Add(this.btnTydzienPrev);
            this.panelNawigacja.Controls.Add(this.btnNoweZamowienie);
            this.panelNawigacja.Controls.Add(this.btnModyfikuj);
            this.panelNawigacja.Controls.Add(this.btnOdswiez);
            this.panelNawigacja.Controls.Add(this.panelDni);
            this.panelNawigacja.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelNawigacja.Location = new System.Drawing.Point(3, 3);
            this.panelNawigacja.Name = "panelNawigacja";
            this.panelNawigacja.Size = new System.Drawing.Size(1258, 64);
            this.panelNawigacja.TabIndex = 0;
            // 
            // btnAnuluj
            // 
            this.btnAnuluj.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAnuluj.BackColor = System.Drawing.Color.IndianRed;
            this.btnAnuluj.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.btnAnuluj.ForeColor = System.Drawing.Color.White;
            this.btnAnuluj.Location = new System.Drawing.Point(1020, 12);
            this.btnAnuluj.Name = "btnAnuluj";
            this.btnAnuluj.Size = new System.Drawing.Size(110, 40);
            this.btnAnuluj.TabIndex = 7;
            this.btnAnuluj.Text = "Anuluj";
            this.btnAnuluj.UseVisualStyleBackColor = false;
            this.btnAnuluj.Click += new System.EventHandler(this.btnAnuluj_Click);
            // 
            // lblZakresDat
            // 
            this.lblZakresDat.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblZakresDat.Location = new System.Drawing.Point(82, 9);
            this.lblZakresDat.Name = "lblZakresDat";
            this.lblZakresDat.Size = new System.Drawing.Size(200, 49);
            this.lblZakresDat.TabIndex = 6;
            this.lblZakresDat.Text = "dd.MM.yyyy - dd.MM.yyyy";
            this.lblZakresDat.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btnTydzienNext
            // 
            this.btnTydzienNext.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.btnTydzienNext.Location = new System.Drawing.Point(288, 12);
            this.btnTydzienNext.Name = "btnTydzienNext";
            this.btnTydzienNext.Size = new System.Drawing.Size(40, 40);
            this.btnTydzienNext.TabIndex = 5;
            this.btnTydzienNext.Text = ">";
            this.btnTydzienNext.UseVisualStyleBackColor = true;
            this.btnTydzienNext.Click += new System.EventHandler(this.btnTydzienNext_Click);
            // 
            // btnTydzienPrev
            // 
            this.btnTydzienPrev.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.btnTydzienPrev.Location = new System.Drawing.Point(36, 12);
            this.btnTydzienPrev.Name = "btnTydzienPrev";
            this.btnTydzienPrev.Size = new System.Drawing.Size(40, 40);
            this.btnTydzienPrev.TabIndex = 4;
            this.btnTydzienPrev.Text = "<";
            this.btnTydzienPrev.UseVisualStyleBackColor = true;
            this.btnTydzienPrev.Click += new System.EventHandler(this.btnTydzienPrev_Click);
            // 
            // btnNoweZamowienie
            // 
            this.btnNoweZamowienie.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnNoweZamowienie.BackColor = System.Drawing.Color.SeaGreen;
            this.btnNoweZamowienie.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.btnNoweZamowienie.ForeColor = System.Drawing.Color.White;
            this.btnNoweZamowienie.Location = new System.Drawing.Point(788, 12);
            this.btnNoweZamowienie.Name = "btnNoweZamowienie";
            this.btnNoweZamowienie.Size = new System.Drawing.Size(110, 40);
            this.btnNoweZamowienie.TabIndex = 3;
            this.btnNoweZamowienie.Text = "Nowe";
            this.btnNoweZamowienie.UseVisualStyleBackColor = false;
            this.btnNoweZamowienie.Click += new System.EventHandler(this.btnNoweZamowienie_Click);
            // 
            // btnModyfikuj
            // 
            this.btnModyfikuj.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnModyfikuj.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.btnModyfikuj.Location = new System.Drawing.Point(904, 12);
            this.btnModyfikuj.Name = "btnModyfikuj";
            this.btnModyfikuj.Size = new System.Drawing.Size(110, 40);
            this.btnModyfikuj.TabIndex = 2;
            this.btnModyfikuj.Text = "Modyfikuj";
            this.btnModyfikuj.UseVisualStyleBackColor = true;
            this.btnModyfikuj.Click += new System.EventHandler(this.btnModyfikuj_Click);
            // 
            // btnOdswiez
            // 
            this.btnOdswiez.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOdswiez.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.btnOdswiez.Location = new System.Drawing.Point(1136, 12);
            this.btnOdswiez.Name = "btnOdswiez";
            this.btnOdswiez.Size = new System.Drawing.Size(110, 40);
            this.btnOdswiez.TabIndex = 1;
            this.btnOdswiez.Text = "Odśwież";
            this.btnOdswiez.UseVisualStyleBackColor = true;
            this.btnOdswiez.Click += new System.EventHandler(this.btnOdswiez_Click);
            // 
            // panelDni
            // 
            this.panelDni.Controls.Add(this.btnPon);
            this.panelDni.Controls.Add(this.btnWt);
            this.panelDni.Controls.Add(this.btnSr);
            this.panelDni.Controls.Add(this.btnCzw);
            this.panelDni.Controls.Add(this.btnPt);
            this.panelDni.Controls.Add(this.btnSo);
            this.panelDni.Controls.Add(this.btnNd);
            this.panelDni.Location = new System.Drawing.Point(344, 6);
            this.panelDni.Name = "panelDni";
            this.panelDni.Size = new System.Drawing.Size(540, 52);
            this.panelDni.TabIndex = 0;
            // 
            // btnPon
            // 
            this.btnPon.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.btnPon.Location = new System.Drawing.Point(3, 3);
            this.btnPon.Name = "btnPon";
            this.btnPon.Size = new System.Drawing.Size(70, 45);
            this.btnPon.TabIndex = 0;
            this.btnPon.Text = "Pon";
            this.btnPon.UseVisualStyleBackColor = true;
            // 
            // btnWt
            // 
            this.btnWt.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.btnWt.Location = new System.Drawing.Point(79, 3);
            this.btnWt.Name = "btnWt";
            this.btnWt.Size = new System.Drawing.Size(70, 45);
            this.btnWt.TabIndex = 1;
            this.btnWt.Text = "Wt";
            this.btnWt.UseVisualStyleBackColor = true;
            // 
            // btnSr
            // 
            this.btnSr.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.btnSr.Location = new System.Drawing.Point(155, 3);
            this.btnSr.Name = "btnSr";
            this.btnSr.Size = new System.Drawing.Size(70, 45);
            this.btnSr.TabIndex = 2;
            this.btnSr.Text = "Śr";
            this.btnSr.UseVisualStyleBackColor = true;
            // 
            // btnCzw
            // 
            this.btnCzw.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.btnCzw.Location = new System.Drawing.Point(231, 3);
            this.btnCzw.Name = "btnCzw";
            this.btnCzw.Size = new System.Drawing.Size(70, 45);
            this.btnCzw.TabIndex = 3;
            this.btnCzw.Text = "Czw";
            this.btnCzw.UseVisualStyleBackColor = true;
            // 
            // btnPt
            // 
            this.btnPt.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.btnPt.Location = new System.Drawing.Point(307, 3);
            this.btnPt.Name = "btnPt";
            this.btnPt.Size = new System.Drawing.Size(70, 45);
            this.btnPt.TabIndex = 4;
            this.btnPt.Text = "Pt";
            this.btnPt.UseVisualStyleBackColor = true;
            // 
            // btnSo
            // 
            this.btnSo.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.btnSo.Location = new System.Drawing.Point(383, 3);
            this.btnSo.Name = "btnSo";
            this.btnSo.Size = new System.Drawing.Size(70, 45);
            this.btnSo.TabIndex = 5;
            this.btnSo.Text = "So";
            this.btnSo.UseVisualStyleBackColor = true;
            // 
            // btnNd
            // 
            this.btnNd.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.btnNd.Location = new System.Drawing.Point(459, 3);
            this.btnNd.Name = "btnNd";
            this.btnNd.Size = new System.Drawing.Size(70, 45);
            this.btnNd.TabIndex = 6;
            this.btnNd.Text = "Nd";
            this.btnNd.UseVisualStyleBackColor = true;
            // 
            // panelGlowny
            // 
            this.panelGlowny.ColumnCount = 2;
            this.panelGlowny.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 55F));
            this.panelGlowny.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 45F));
            this.panelGlowny.Controls.Add(this.panelMaster, 0, 0);
            this.panelGlowny.Controls.Add(this.panelDetail, 1, 0);
            this.panelGlowny.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelGlowny.Location = new System.Drawing.Point(3, 73);
            this.panelGlowny.Name = "panelGlowny";
            this.panelGlowny.RowCount = 1;
            this.panelGlowny.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.panelGlowny.Size = new System.Drawing.Size(1258, 605);
            this.panelGlowny.TabIndex = 1;
            // 
            // panelMaster
            // 
            this.panelMaster.Controls.Add(this.dgvZamowienia);
            this.panelMaster.Controls.Add(this.panelFiltry);
            this.panelMaster.Controls.Add(this.panelPodsumowanie);
            this.panelMaster.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelMaster.Location = new System.Drawing.Point(3, 3);
            this.panelMaster.Name = "panelMaster";
            this.panelMaster.Size = new System.Drawing.Size(685, 599);
            this.panelMaster.TabIndex = 0;
            // 
            // dgvZamowienia
            // 
            this.dgvZamowienia.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvZamowienia.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvZamowienia.Location = new System.Drawing.Point(0, 45);
            this.dgvZamowienia.Name = "dgvZamowienia";
            this.dgvZamowienia.Size = new System.Drawing.Size(685, 514);
            this.dgvZamowienia.TabIndex = 1;
            this.dgvZamowienia.SelectionChanged += new System.EventHandler(this.dgvZamowienia_SelectionChanged);
            this.dgvZamowienia.RowPrePaint += new System.Windows.Forms.DataGridViewRowPrePaintEventHandler(this.dgvZamowienia_RowPrePaint);
            // 
            // panelFiltry
            // 
            this.panelFiltry.Controls.Add(this.cbFiltrujTowar);
            this.panelFiltry.Controls.Add(this.cbFiltrujHandlowca);
            this.panelFiltry.Controls.Add(this.txtFiltrujOdbiorce);
            this.panelFiltry.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelFiltry.Location = new System.Drawing.Point(0, 0);
            this.panelFiltry.Name = "panelFiltry";
            this.panelFiltry.Padding = new System.Windows.Forms.Padding(5);
            this.panelFiltry.Size = new System.Drawing.Size(685, 45);
            this.panelFiltry.TabIndex = 2;
            // 
            // cbFiltrujHandlowca
            // 
            this.cbFiltrujHandlowca.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbFiltrujHandlowca.FormattingEnabled = true;
            this.cbFiltrujHandlowca.Location = new System.Drawing.Point(267, 11);
            this.cbFiltrujHandlowca.Name = "cbFiltrujHandlowca";
            this.cbFiltrujHandlowca.Size = new System.Drawing.Size(180, 23);
            this.cbFiltrujHandlowca.TabIndex = 1;
            this.cbFiltrujHandlowca.SelectedIndexChanged += new System.EventHandler(this.Filtry_Changed);
            // 
            // txtFiltrujOdbiorce
            // 
            this.txtFiltrujOdbiorce.Location = new System.Drawing.Point(12, 11);
            this.txtFiltrujOdbiorce.Name = "txtFiltrujOdbiorce";
            this.txtFiltrujOdbiorce.PlaceholderText = "Filtruj po nazwie odbiorcy...";
            this.txtFiltrujOdbiorce.Size = new System.Drawing.Size(249, 23);
            this.txtFiltrujOdbiorce.TabIndex = 0;
            this.txtFiltrujOdbiorce.TextChanged += new System.EventHandler(this.Filtry_Changed);
            // 
            // panelPodsumowanie
            // 
            this.panelPodsumowanie.BackColor = System.Drawing.SystemColors.Control;
            this.panelPodsumowanie.Controls.Add(this.lblPodsumowanie);
            this.panelPodsumowanie.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelPodsumowanie.Location = new System.Drawing.Point(0, 559);
            this.panelPodsumowanie.Name = "panelPodsumowanie";
            this.panelPodsumowanie.Size = new System.Drawing.Size(685, 40);
            this.panelPodsumowanie.TabIndex = 0;
            // 
            // lblPodsumowanie
            // 
            this.lblPodsumowanie.AutoSize = true;
            this.lblPodsumowanie.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblPodsumowanie.Location = new System.Drawing.Point(12, 11);
            this.lblPodsumowanie.Name = "lblPodsumowanie";
            this.lblPodsumowanie.Size = new System.Drawing.Size(13, 17);
            this.lblPodsumowanie.TabIndex = 0;
            this.lblPodsumowanie.Text = "-";
            // 
            // panelDetail
            // 
            this.panelDetail.ColumnCount = 1;
            this.panelDetail.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.panelDetail.Controls.Add(this.dgvAgregacja, 0, 2);
            this.panelDetail.Controls.Add(this.label2, 0, 1);
            this.panelDetail.Controls.Add(this.panelSzczegolyTop, 0, 0);
            this.panelDetail.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelDetail.Location = new System.Drawing.Point(694, 3);
            this.panelDetail.Name = "panelDetail";
            this.panelDetail.RowCount = 3;
            this.panelDetail.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 40F));
            this.panelDetail.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.panelDetail.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 60F));
            this.panelDetail.Size = new System.Drawing.Size(561, 599);
            this.panelDetail.TabIndex = 1;
            // 
            // dgvAgregacja
            // 
            this.dgvAgregacja.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvAgregacja.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvAgregacja.Location = new System.Drawing.Point(3, 317);
            this.dgvAgregacja.Name = "dgvAgregacja";
            this.dgvAgregacja.Size = new System.Drawing.Size(555, 279);
            this.dgvAgregacja.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label2.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label2.Location = new System.Drawing.Point(3, 284);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(555, 30);
            this.label2.TabIndex = 3;
            this.label2.Text = "Podsumowanie produktów dla wybranego dnia";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // panelSzczegolyTop
            // 
            this.panelSzczegolyTop.ColumnCount = 2;
            this.panelSzczegolyTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.panelSzczegolyTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.panelSzczegolyTop.Controls.Add(this.panelNotatki, 0, 0);
            this.panelSzczegolyTop.Controls.Add(this.panelPrzychody, 1, 0);
            this.panelSzczegolyTop.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelSzczegolyTop.Location = new System.Drawing.Point(3, 3);
            this.panelSzczegolyTop.Name = "panelSzczegolyTop";
            this.panelSzczegolyTop.RowCount = 1;
            this.panelSzczegolyTop.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.panelSzczegolyTop.Size = new System.Drawing.Size(555, 278);
            this.panelSzczegolyTop.TabIndex = 4;
            // 
            // panelNotatki
            // 
            this.panelNotatki.Controls.Add(this.dgvSzczegoly);
            this.panelNotatki.Controls.Add(this.txtNotatki);
            this.panelNotatki.Controls.Add(this.label3);
            this.panelNotatki.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelNotatki.Location = new System.Drawing.Point(3, 3);
            this.panelNotatki.Name = "panelNotatki";
            this.panelNotatki.Size = new System.Drawing.Size(271, 272);
            this.panelNotatki.TabIndex = 0;
            // 
            // dgvSzczegoly
            // 
            this.dgvSzczegoly.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvSzczegoly.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvSzczegoly.Location = new System.Drawing.Point(0, 23);
            this.dgvSzczegoly.Name = "dgvSzczegoly";
            this.dgvSzczegoly.Size = new System.Drawing.Size(271, 149);
            this.dgvSzczegoly.TabIndex = 2;
            // 
            // txtNotatki
            // 
            this.txtNotatki.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.txtNotatki.Location = new System.Drawing.Point(0, 172);
            this.txtNotatki.Multiline = true;
            this.txtNotatki.Name = "txtNotatki";
            this.txtNotatki.ReadOnly = true;
            this.txtNotatki.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtNotatki.Size = new System.Drawing.Size(271, 100);
            this.txtNotatki.TabIndex = 1;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Dock = System.Windows.Forms.DockStyle.Top;
            this.label3.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label3.Location = new System.Drawing.Point(0, 0);
            this.label3.Name = "label3";
            this.label3.Padding = new System.Windows.Forms.Padding(0, 0, 0, 6);
            this.label3.Size = new System.Drawing.Size(208, 23);
            this.label3.TabIndex = 0;
            this.label3.Text = "Szczegóły / Notatki zamówienia:";
            // 
            // panelPrzychody
            // 
            this.panelPrzychody.Controls.Add(this.dgvPrzychody);
            this.panelPrzychody.Controls.Add(this.label1);
            this.panelPrzychody.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelPrzychody.Location = new System.Drawing.Point(280, 3);
            this.panelPrzychody.Name = "panelPrzychody";
            this.panelPrzychody.Size = new System.Drawing.Size(272, 272);
            this.panelPrzychody.TabIndex = 1;
            // 
            // dgvPrzychody
            // 
            this.dgvPrzychody.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvPrzychody.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvPrzychody.Location = new System.Drawing.Point(0, 23);
            this.dgvPrzychody.Name = "dgvPrzychody";
            this.dgvPrzychody.Size = new System.Drawing.Size(272, 249);
            this.dgvPrzychody.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Top;
            this.label1.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Name = "label1";
            this.label1.Padding = new System.Windows.Forms.Padding(0, 0, 0, 6);
            this.label1.Size = new System.Drawing.Size(122, 23);
            this.label1.TabIndex = 0;
            this.label1.Text = "Przychody towaru:";
            // 
            // cbFiltrujTowar
            // 
            this.cbFiltrujTowar.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbFiltrujTowar.FormattingEnabled = true;
            this.cbFiltrujTowar.Location = new System.Drawing.Point(453, 11);
            this.cbFiltrujTowar.Name = "cbFiltrujTowar";
            this.cbFiltrujTowar.Size = new System.Drawing.Size(220, 23);
            this.cbFiltrujTowar.TabIndex = 2;
            this.cbFiltrujTowar.SelectedIndexChanged += new System.EventHandler(this.Filtry_Changed);
            // 
            // WidokZamowieniaPodsumowanie
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1264, 681);
            this.Controls.Add(this.tableLayoutPanel1);
            this.MinimumSize = new System.Drawing.Size(1280, 720);
            this.Name = "WidokZamowieniaPodsumowanie";
            this.Text = "Podsumowanie Tygodniowe Zamówień";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.tableLayoutPanel1.ResumeLayout(false);
            this.panelNawigacja.ResumeLayout(false);
            this.panelNawigacja.PerformLayout();
            this.panelDni.ResumeLayout(false);
            this.panelGlowny.ResumeLayout(false);
            this.panelMaster.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvZamowienia)).EndInit();
            this.panelFiltry.ResumeLayout(false);
            this.panelFiltry.PerformLayout();
            this.panelPodsumowanie.ResumeLayout(false);
            this.panelPodsumowanie.PerformLayout();
            this.panelDetail.ResumeLayout(false);
            this.panelDetail.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvAgregacja)).EndInit();
            this.panelSzczegolyTop.ResumeLayout(false);
            this.panelNotatki.ResumeLayout(false);
            this.panelNotatki.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvSzczegoly)).EndInit();
            this.panelPrzychody.ResumeLayout(false);
            this.panelPrzychody.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvPrzychody)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Panel panelNawigacja;
        private System.Windows.Forms.FlowLayoutPanel panelDni;
        private System.Windows.Forms.Button btnPon;
        private System.Windows.Forms.Button btnWt;
        private System.Windows.Forms.Button btnSr;
        private System.Windows.Forms.Button btnCzw;
        private System.Windows.Forms.Button btnPt;
        private System.Windows.Forms.Button btnOdswiez;
        private System.Windows.Forms.Button btnNoweZamowienie;
        private System.Windows.Forms.Button btnModyfikuj;
        private System.Windows.Forms.TableLayoutPanel panelGlowny;
        private System.Windows.Forms.Panel panelMaster;
        private System.Windows.Forms.DataGridView dgvZamowienia;
        private System.Windows.Forms.Panel panelPodsumowanie;
        private System.Windows.Forms.Label lblPodsumowanie;
        private System.Windows.Forms.TableLayoutPanel panelDetail;
        private System.Windows.Forms.DataGridView dgvSzczegoly;
        private System.Windows.Forms.DataGridView dgvAgregacja;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Panel panelFiltry;
        private System.Windows.Forms.ComboBox cbFiltrujHandlowca;
        private System.Windows.Forms.TextBox txtFiltrujOdbiorce;
        private System.Windows.Forms.TableLayoutPanel panelSzczegolyTop;
        private System.Windows.Forms.Panel panelNotatki;
        private System.Windows.Forms.TextBox txtNotatki;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Panel panelPrzychody;
        private System.Windows.Forms.DataGridView dgvPrzychody;
        private System.Windows.Forms.Button btnSo;
        private System.Windows.Forms.Button btnNd;
        private System.Windows.Forms.Button btnTydzienNext;
        private System.Windows.Forms.Button btnTydzienPrev;
        private System.Windows.Forms.Label lblZakresDat;
        private System.Windows.Forms.Button btnAnuluj;
        private System.Windows.Forms.ComboBox cbFiltrujTowar;
    }
}

