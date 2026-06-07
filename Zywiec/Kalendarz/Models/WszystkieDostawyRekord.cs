using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kalendarz1.Zywiec.Kalendarz.Models
{
    // Model wiersza w nowym widoku "Wszystkie dostawy" (WPF).
    // Wzbogacony o flagi z innych modułów: SMS snapshot, stały klient, status produkcji.
    // INotifyPropertyChanged żeby aktualizacje (np. po edycji) były widoczne natychmiast.
    public class WszystkieDostawyRekord : INotifyPropertyChanged
    {
        // === KLUCZE / IDENTYFIKACJA ===
        public int LP { get; set; }
        public int? LpW { get; set; }
        public string Dostawca { get; set; } = "";
        public string DostawcaID { get; set; } = "";

        // === GŁÓWNE DANE DOSTAWY ===
        public DateTime DataOdbioru { get; set; }
        public DateTime? DataWstawienia { get; set; }
        public int RoznicaDni => DataWstawienia.HasValue
            ? (int)(DataOdbioru.Date - DataWstawienia.Value.Date).TotalDays
            : 0;

        private int _auta;
        public int Auta { get => _auta; set { _auta = value; OnChanged(); } }

        private int _sztukiDek;
        public int SztukiDek { get => _sztukiDek; set { _sztukiDek = value; OnChanged(); } }

        private decimal _wagaDek;
        public decimal WagaDek { get => _wagaDek; set { _wagaDek = value; OnChanged(); } }

        public string TypUmowy { get; set; } = "";

        private string _typCeny = "";
        public string TypCeny { get => _typCeny; set { _typCeny = value; OnChanged(); } }

        private decimal _cena;
        public decimal Cena { get => _cena; set { _cena = value; OnChanged(); } }

        private string _bufor = "";
        public string Bufor { get => _bufor; set { _bufor = value; OnChanged(); OnPropertyChanged(nameof(StatusKolor)); } }

        public int Ubytek { get; set; }
        public string Uwagi { get; set; } = "";

        // === METADANE ===
        public DateTime? DataUtw { get; set; }
        public string KtoStwo { get; set; } = "";
        public DateTime? DataMod { get; set; }
        public string KtoMod { get; set; } = "";

        // === SMS SNAPSHOT (z poprzedniej iteracji) ===
        public bool BylSMS { get; set; }
        public bool SmsWymagaAktualizacji { get; set; }
        public DateTime? SmsCreatedAt { get; set; }

        // === STAŁY KLIENT (4+ dostaw w 12 m-cach z Bufor='Potwierdzony') ===
        public bool StalyKlient { get; set; }

        // === DISPLAY HELPERS (na potrzeby kolumn DataGrid) ===
        public string SmsStatus => SmsWymagaAktualizacji ? "⚠️" : (BylSMS ? "📱" : "");
        public string StalyKlientIkona => StalyKlient ? "★" : "";

        // Kolor wiersza po Bufor — używamy DataTrigger w XAML, ale eksponujemy też jako string
        public string StatusKolor => (Bufor ?? "").ToLower() switch
        {
            "anulowany" => "#FEE2E2",
            "potwierdzony" => "#DCFCE7",
            "sprzedany" => "#DBEAFE",
            "b.kontr." => "#FEF3C7",
            "b.wolny." => "#FEF3C7",
            "b.wolny" => "#FEF3C7",
            "do wykupienia" => "#FCE7F3",
            _ => "#FFFFFF"
        };

        // === INotifyPropertyChanged ===
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? ""));
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
