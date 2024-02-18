using System;

namespace Kalendarz1
{
    partial class WidokKalendarza
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

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
            HarmonogramDnia = new System.Windows.Forms.ListBox();
            MyCalendar = new System.Windows.Forms.MonthCalendar();
            Data = new System.Windows.Forms.DateTimePicker();
            dataGridView1 = new System.Windows.Forms.DataGridView();
            Datao = new System.Windows.Forms.DataGridViewTextBoxColumn();
            Dostawca = new System.Windows.Forms.DataGridViewTextBoxColumn();
            Auta = new System.Windows.Forms.DataGridViewTextBoxColumn();
            Sztuki = new System.Windows.Forms.DataGridViewTextBoxColumn();
            Waga = new System.Windows.Forms.DataGridViewTextBoxColumn();
            Status = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            SuspendLayout();
            // 
            // HarmonogramDnia
            // 
            HarmonogramDnia.FormattingEnabled = true;
            HarmonogramDnia.ItemHeight = 15;
            HarmonogramDnia.Location = new System.Drawing.Point(889, 328);
            HarmonogramDnia.Name = "HarmonogramDnia";
            HarmonogramDnia.Size = new System.Drawing.Size(731, 94);
            HarmonogramDnia.TabIndex = 0;
            // 
            // MyCalendar
            // 
            MyCalendar.Location = new System.Drawing.Point(794, 21);
            MyCalendar.Name = "MyCalendar";
            MyCalendar.TabIndex = 1;
            MyCalendar.DateChanged += MyCalendar_DateChanged_1;
            // 
            // Data
            // 
            Data.CustomFormat = "yyyy-MM-dd";
            Data.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            Data.Location = new System.Drawing.Point(1361, 60);
            Data.Name = "Data";
            Data.Size = new System.Drawing.Size(89, 23);
            Data.TabIndex = 18;
            Data.Value = new DateTime(2024, 2, 12, 0, 0, 0, 0);
            Data.ValueChanged += Data_ValueChanged_1;
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { Datao, Dostawca, Auta, Sztuki, Waga, Status });
            dataGridView1.Location = new System.Drawing.Point(5, 21);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowTemplate.Height = 25;
            dataGridView1.Size = new System.Drawing.Size(777, 871);
            dataGridView1.TabIndex = 19;
            // 
            // Datao
            // 
            Datao.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            Datao.FillWeight = 400F;
            Datao.HeaderText = "Data";
            Datao.Name = "Datao";
            Datao.Width = 56;
            // 
            // Dostawca
            // 
            Dostawca.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            Dostawca.FillWeight = 400F;
            Dostawca.HeaderText = "Dostawca";
            Dostawca.Name = "Dostawca";
            Dostawca.Width = 83;
            // 
            // Auta
            // 
            Auta.FillWeight = 30F;
            Auta.HeaderText = "Auta";
            Auta.Name = "Auta";
            Auta.Width = 30;
            // 
            // Sztuki
            // 
            Sztuki.FillWeight = 70F;
            Sztuki.HeaderText = "Sztuki";
            Sztuki.Name = "Sztuki";
            Sztuki.Width = 40;
            // 
            // Waga
            // 
            Waga.FillWeight = 70F;
            Waga.HeaderText = "Waga";
            Waga.Name = "Waga";
            Waga.Width = 40;
            // 
            // Status
            // 
            Status.HeaderText = "Status";
            Status.Name = "Status";
            Status.Width = 150;
            // 
            // WidokKalendarza
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1518, 914);
            Controls.Add(dataGridView1);
            Controls.Add(Data);
            Controls.Add(MyCalendar);
            Controls.Add(HarmonogramDnia);
            Name = "WidokKalendarza";
            Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ResumeLayout(false);
        }


        #endregion

        private System.Windows.Forms.ListBox HarmonogramDnia;
        private System.Windows.Forms.MonthCalendar MyCalendar;
        private System.Windows.Forms.DateTimePicker Data;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.DataGridViewTextBoxColumn Datao;
        private System.Windows.Forms.DataGridViewTextBoxColumn Dostawca;
        private System.Windows.Forms.DataGridViewTextBoxColumn Auta;
        private System.Windows.Forms.DataGridViewTextBoxColumn Sztuki;
        private System.Windows.Forms.DataGridViewTextBoxColumn Waga;
        private System.Windows.Forms.DataGridViewTextBoxColumn Status;
    }
}