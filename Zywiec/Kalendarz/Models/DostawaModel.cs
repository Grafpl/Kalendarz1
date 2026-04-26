using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kalendarz1.Zywiec.Kalendarz
{
    public class DostawaModel : INotifyPropertyChanged
    {
        public string LP { get; set; }
        public DateTime DataOdbioru { get; set; }
        public string Dostawca { get; set; }

        private int _auta;
        public int Auta { get => _auta; set { _auta = value; OnPropertyChanged(); OnPropertyChanged(nameof(AutaDisplay)); } }

        private double _sztukiDek;
        public double SztukiDek { get => _sztukiDek; set { _sztukiDek = value; OnPropertyChanged(); OnPropertyChanged(nameof(SztukiDekDisplay)); } }

        private decimal _wagaDek;
        public decimal WagaDek { get => _wagaDek; set { _wagaDek = value; OnPropertyChanged(); OnPropertyChanged(nameof(WagaDekDisplay)); } }

        public string Bufor { get; set; }

        private string _typCeny;
        public string TypCeny { get => _typCeny; set { _typCeny = value; OnPropertyChanged(); OnPropertyChanged(nameof(TypCenyDisplay)); } }

        private decimal _cena;
        public decimal Cena { get => _cena; set { _cena = value; OnPropertyChanged(); OnPropertyChanged(nameof(CenaDisplay)); } }
        public int Distance { get; set; }
        private string _uwagi;
        public string Uwagi { get => _uwagi; set { _uwagi = value; OnPropertyChanged(); OnPropertyChanged(nameof(UwagiDisplay)); } }
        public string UwagiAutorID { get; set; }
        public string UwagiAutorName { get; set; }
        private DateTime? _dataNotatki;
        public DateTime? DataNotatki { get => _dataNotatki; set { _dataNotatki = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsVeryRecentNote)); OnPropertyChanged(nameof(IsRecentNote)); } }
        private int? _roznicaDni;
        public int? RoznicaDni { get => _roznicaDni; set { _roznicaDni = value; OnPropertyChanged(); OnPropertyChanged(nameof(RoznicaDniDisplay)); } }
        public string LpW { get; set; }
        public bool PotwWaga { get; set; }
        public bool PotwSztuki { get; set; }
        public int Ubytek { get; set; }

        private bool _isConfirmed;
        public bool IsConfirmed { get => _isConfirmed; set { _isConfirmed = value; OnPropertyChanged(); } }

        private bool _isWstawienieConfirmed;
        public bool IsWstawienieConfirmed { get => _isWstawienieConfirmed; set { _isWstawienieConfirmed = value; OnPropertyChanged(); } }

        public int SortOrder { get; set; }

        private bool _isSimulationMoved;
        public bool IsSimulationMoved { get => _isSimulationMoved; set { _isSimulationMoved = value; OnPropertyChanged(); } }

        public bool IsHeaderRow { get; set; }
        public bool IsSeparator { get; set; }
        public bool IsEmptyDay { get; set; }

        private double _sumaAuta;
        public double SumaAuta { get => _sumaAuta; set { _sumaAuta = value; OnPropertyChanged(); OnPropertyChanged(nameof(AutaDisplay)); } }

        private double _sumaSztuki;
        public double SumaSztuki { get => _sumaSztuki; set { _sumaSztuki = value; OnPropertyChanged(); OnPropertyChanged(nameof(SztukiDekDisplay)); } }

        private double _sredniaWaga;
        public double SredniaWaga { get => _sredniaWaga; set { _sredniaWaga = value; OnPropertyChanged(); OnPropertyChanged(nameof(WagaDekDisplay)); } }

        private double _sredniaCena;
        public double SredniaCena { get => _sredniaCena; set { _sredniaCena = value; OnPropertyChanged(); OnPropertyChanged(nameof(CenaDisplay)); } }

        public double SredniaKM { get; set; }
        public double SredniaDoby { get; set; }
        public int SumaUbytek { get; set; }
        public int LiczbaSprzedanych { get; set; }
        public int LiczbaAnulowanych { get; set; }
        public bool HasBothCounts => IsHeaderRow && !IsSeparator && LiczbaSprzedanych > 0 && LiczbaAnulowanych > 0;

        private static readonly string[] DniSkrot = { "niedz.", "pon.", "wt.", "śr.", "czw.", "pt.", "sob." };
        public string DostawcaDisplay => IsHeaderRow && !IsSeparator
            ? $"{DniSkrot[(int)DataOdbioru.DayOfWeek]} {DataOdbioru:dd.MM}"
            : (IsSeparator ? "" : Dostawca);

        public string SztukiDekDisplay => IsHeaderRow
            ? (SumaSztuki > 0 ? $"{SumaSztuki:#,0}" : "")
            : (SztukiDek > 0 ? $"{SztukiDek:#,0}" : "");
        public string WagaDekDisplay => IsHeaderRow
            ? (SredniaWaga > 0 ? $"{SredniaWaga:0.00}" : "")
            : (WagaDek > 0 ? $"{WagaDek:0.00}" : "");
        public string CenaDisplay => IsHeaderRow
            ? (SredniaCena > 0 ? $"{SredniaCena:0.00}" : "")
            : (Cena > 0 ? $"{Cena:0.00}" : "");
        public string KmDisplay => IsHeaderRow
            ? (SredniaKM > 0 ? $"{SredniaKM:0}" : "")
            : (Distance > 0 ? $"{Distance}" : "");
        public string RoznicaDniDisplay => IsHeaderRow
            ? (SumaUbytek > 0 ? $"Ub:{SumaUbytek}" : "")
            : (RoznicaDni.HasValue ? $"{RoznicaDni}" : "");
        public string AutaDisplay => IsHeaderRow
            ? (SumaAuta > 0 ? $"{SumaAuta:0}" : "")
            : (Auta > 0 ? Auta.ToString() : "");
        public string TypCenyDisplay => IsHeaderRow ? "" : GetTypCenyAbbrev(TypCeny);
        private static string GetTypCenyAbbrev(string typ)
        {
            if (string.IsNullOrEmpty(typ)) return "";
            var lower = typ.ToLowerInvariant();
            if (lower.Contains("wolny")) return "wol.";
            if (lower.Contains("rolnic")) return "rol.";
            if (lower.Contains("minister")) return "mini.";
            if (lower.Contains("łącz") || lower.Contains("laczo")) return "łącz.";
            return typ;
        }
        public string UwagiDisplay => IsHeaderRow && !IsSeparator
            ? BuildHeaderUwagiDisplay()
            : Uwagi;

        private string BuildHeaderUwagiDisplay()
        {
            var parts = new List<string>();
            if (LiczbaSprzedanych > 0) parts.Add($"S:{LiczbaSprzedanych}");
            if (LiczbaAnulowanych > 0) parts.Add($"A:{LiczbaAnulowanych}");
            return string.Join(" ", parts);
        }

        public bool IsVeryRecentNote => DataNotatki.HasValue && (DateTime.Now - DataNotatki.Value).TotalDays <= 1;
        public bool IsRecentNote => DataNotatki.HasValue && (DateTime.Now - DataNotatki.Value).TotalDays > 1 && (DateTime.Now - DataNotatki.Value).TotalDays <= 3;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
