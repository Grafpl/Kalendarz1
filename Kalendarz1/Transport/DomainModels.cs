using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Kalendarz1.Transport
{
    /// <summary>
    /// Kierowca – uproszczony model domenowy u¿ywany w warstwie UI / logice.
    /// </summary>
    public sealed class Driver
    {
        public int GID { get; init; }
        public string Name { get; init; } = string.Empty; // Imiê i nazwisko razem – tak jak w tabeli dbo.Driver
        public bool Active { get; init; }
    }

    /// <summary>
    /// Pojazd – spójny z tabel¹ CarTrailer (Kind 3 = samochód, 4 = naczepa). Tu u¿ywamy tylko samochodów (3).
    /// </summary>
    public sealed class Vehicle
    {
        public string ID { get; init; } = string.Empty;           // Rejestracja
        public string? Brand { get; init; }
        public string? Model { get; init; }
        public decimal? CapacityKg { get; init; }                 // Kolumna Capacity
        public int? PalletSlotsH1 { get; init; }                  // (Opcjonalne – jeœli dodasz kolumnê w DB)
    }

    public enum TripStatus { Planned, InProgress, Completed, Canceled }

    /// <summary>
    /// Kurs / transport – uproszczony model niezale¿ny od aktualnej tabeli dbo.TransportTrip.
    /// </summary>
    public sealed class Trip
    {
        public long TripID { get; set; }
        public DateTime TripDate { get; set; }
        public TripStatus Status { get; set; } = TripStatus.Planned;
        public int? DriverGID { get; set; }
        public string? VehicleID { get; set; }
        public string? TrailerID { get; set; }
        public TimeSpan? PlannedDeparture { get; set; }
        public string? RouteName { get; set; }
        public string? Notes { get; set; }
        public List<TripLoad> Loads { get; } = new();
    }

    /// <summary>
    /// £adunek przypisany do kursu – sumarycznie dla klienta lub punktu.
    /// </summary>
    public sealed class TripLoad
    {
        public long? TripLoadID { get; set; }
        public long TripID { get; set; }
        public int SequenceNo { get; set; }
        public string? CustomerCode { get; set; }
        public decimal MeatKg { get; set; }
        public int CarcassCount { get; set; }
        public int PalletsH1 { get; set; }
        public int ContainersE2 { get; set; }
        public string? Comment { get; set; }
    }

    internal static class TransportUi
    {
        private static readonly Color Back = Color.FromArgb(246, 249, 252);
        private static readonly Color PanelBack = Color.FromArgb(235, 240, 246);
        private static readonly Color Accent = Color.FromArgb(0, 99, 177);
        private static readonly Color AccentText = Color.White;
        private static readonly Color GridHeaderBack = Color.FromArgb(222, 228, 235);
        private static readonly Font BaseFont = new("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        private static readonly Font ButtonFont = new("Segoe UI Semibold", 10F, FontStyle.Regular, GraphicsUnit.Point);

        public static void ApplyTheme(Form f)
        {
            f.BackColor = Back;
            f.Font = BaseFont;
            f.Padding = new Padding(2);
            foreach (Control c in f.Controls) Apply(c);
        }

        private static void Apply(Control c)
        {
            if (c is Button b)
            {
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderColor = Accent;
                b.FlatAppearance.BorderSize = 1;
                b.BackColor = Accent;
                b.ForeColor = AccentText;
                b.Font = ButtonFont;
                b.Height = 40; b.MinimumSize = new Size(90, 40);
                b.Margin = new Padding(6, 8, 0, 8);
                b.Padding = new Padding(10, 6, 10, 6);
            }
            else if (c is FlowLayoutPanel or TableLayoutPanel or Panel)
            {
                c.BackColor = PanelBack;
            }
            else if (c is StatusStrip ss)
            {
                ss.BackColor = PanelBack; ss.SizingGrip = false; ss.Font = BaseFont;
            }
            else if (c is SplitContainer sc)
            {
                sc.BackColor = PanelBack; Apply(sc.Panel1); Apply(sc.Panel2);
            }
            else if (c is GroupBox gb)
            {
                gb.BackColor = PanelBack; gb.Font = new Font(BaseFont, FontStyle.Bold);
            }
            if (c.HasChildren)
                foreach (Control child in c.Controls) Apply(child);
        }

        public static void StyleGrid(DataGridView g)
        {
            g.BackgroundColor = Color.White;
            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersDefaultCellStyle.BackColor = GridHeaderBack;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
            g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Regular);
            g.DefaultCellStyle.Font = new Font("Segoe UI", 10F);
            g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 251, 253);
            g.BorderStyle = BorderStyle.None;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.RowHeadersVisible = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.RowTemplate.Height = 30;
        }
    }
}
