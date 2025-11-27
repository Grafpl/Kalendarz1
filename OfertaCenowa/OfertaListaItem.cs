using System;
using System.ComponentModel;
using System.Windows.Media;

namespace Kalendarz1.OfertaCenowa
{
    /// <summary>
    /// Model danych dla listy ofert - JEDYNA definicja tej klasy w projekcie!
    /// Zawiera wszystkie pola używane przez OfertyListaWindow i OfertaRepository.
    /// </summary>
    public class OfertaListaItem : INotifyPropertyChanged
    {
        // === PODSTAWOWE ===
        public int ID { get; set; }
        public string NumerOferty { get; set; } = "";
        public DateTime DataWystawienia { get; set; }
        public DateTime? DataWaznosci { get; set; }
        
        // === KLIENT ===
        public string KlientNazwa { get; set; } = "";
        public string KlientNIP { get; set; } = "";
        public string KlientAdres { get; set; } = "";
        public string KlientMiejscowosc { get; set; } = "";
        public string KlientEmail { get; set; } = "";
        public string KlientTelefon { get; set; } = "";
        public string KlientOsobaKontaktowa { get; set; } = "";
        
        // === HANDLOWIEC ===
        public string HandlowiecID { get; set; } = "";
        public string HandlowiecNazwa { get; set; } = "";
        
        // === WARTOŚCI ===
        public decimal WartoscNetto { get; set; }
        public decimal WartoscBrutto { get; set; }
        public string Waluta { get; set; } = "PLN";
        public int LiczbaPozycji { get; set; }
        
        // === STATUS (z INotifyPropertyChanged) ===
        private string _status = "";
        public string Status 
        { 
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(StatusIkona));
                OnPropertyChanged(nameof(StatusBackground));
                OnPropertyChanged(nameof(StatusForeground));
            }
        }
        
        // === PLIKI ===
        public string SciezkaPDF { get; set; } = "";
        public string NazwaPliku { get; set; } = "";
        
        // === DODATKOWE ===
        public int UserID { get; set; }
        public string Uwagi { get; set; } = "";
        public bool CzyPrzeterminowana { get; set; }
        public bool CzyMaZamowienie { get; set; }
        public bool CzyMaFakture { get; set; }

        // === WŁAŚCIWOŚCI OBLICZANE - FORMATOWANIE ===
        public string DataWystawieniaFormatowana => DataWystawienia.ToString("dd.MM.yyyy");
        public string DataWaznosciFormatowana => DataWaznosci?.ToString("dd.MM.yyyy") ?? "-";
        public string WartoscFormatowana => $"{WartoscBrutto:N2} zł";
        public string WartoscNettoFormatowana => $"{WartoscNetto:N2} {Waluta}";
        
        // === IKONA STATUSU ===
        public string StatusIkona
        {
            get
            {
                return Status switch
                {
                    "Nowa" => "📝",
                    "Wyslana" => "📧",
                    "Zaakceptowana" => "✅",
                    "Odrzucona" => "❌",
                    "Anulowana" => "🚫",
                    "Wygasla" => "⏰",
                    _ => "📋"
                };
            }
        }
        
        // === KOLOR TŁA STATUSU ===
        public Brush StatusBackground
        {
            get
            {
                return Status switch
                {
                    "Nowa" => new SolidColorBrush(Color.FromRgb(239, 246, 255)),        // Blue light
                    "Wyslana" => new SolidColorBrush(Color.FromRgb(245, 243, 255)),    // Purple light
                    "Zaakceptowana" => new SolidColorBrush(Color.FromRgb(236, 253, 245)), // Green light
                    "Odrzucona" => new SolidColorBrush(Color.FromRgb(254, 242, 242)),  // Red light
                    "Anulowana" => new SolidColorBrush(Color.FromRgb(243, 244, 246)),  // Gray light
                    "Wygasla" => new SolidColorBrush(Color.FromRgb(255, 251, 235)),    // Yellow light
                    _ => new SolidColorBrush(Color.FromRgb(243, 244, 246))
                };
            }
        }
        
        // === KOLOR TEKSTU STATUSU ===
        public Brush StatusForeground
        {
            get
            {
                return Status switch
                {
                    "Nowa" => new SolidColorBrush(Color.FromRgb(59, 130, 246)),         // Blue
                    "Wyslana" => new SolidColorBrush(Color.FromRgb(139, 92, 246)),      // Purple
                    "Zaakceptowana" => new SolidColorBrush(Color.FromRgb(34, 197, 94)), // Green
                    "Odrzucona" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),     // Red
                    "Anulowana" => new SolidColorBrush(Color.FromRgb(107, 114, 128)),   // Gray
                    "Wygasla" => new SolidColorBrush(Color.FromRgb(217, 119, 6)),       // Yellow/Orange
                    _ => new SolidColorBrush(Color.FromRgb(107, 114, 128))
                };
            }
        }

        // === IMPLEMENTACJA INotifyPropertyChanged ===
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
