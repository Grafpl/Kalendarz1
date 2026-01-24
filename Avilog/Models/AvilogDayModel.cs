using System;
using System.ComponentModel;

namespace Kalendarz1.Avilog.Models
{
    /// <summary>
    /// Model podsumowania dziennego dla rozliczeń Avilog
    /// </summary>
    public class AvilogDayModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private int _lp;
        public int LP
        {
            get => _lp;
            set { _lp = value; OnPropertyChanged(nameof(LP)); }
        }

        public DateTime Data { get; set; }
        public string DzienTygodnia { get; set; }

        // === STATYSTYKI ===
        public int LiczbaKursow { get; set; }
        public int LiczbaZestawow { get; set; }

        // === SZTUKI ===
        public int SumaSztuk { get; set; }
        public int SumaUpadkowSzt { get; set; }

        // === WAGI ===
        public decimal SumaBrutto { get; set; }
        public decimal SumaTara { get; set; }
        public decimal SumaNetto { get; set; }
        public decimal SumaUpadkowKg { get; set; }

        /// <summary>
        /// Różnica kg = Netto - Upadki kg (to idzie do rozliczenia)
        /// </summary>
        public decimal SumaRoznicaKg => SumaNetto - SumaUpadkowKg;

        // === KILOMETRY I CZAS ===
        public int SumaKM { get; set; }
        public decimal SumaGodzin { get; set; }

        // === OBLICZENIA FINANSOWE ===
        private decimal _stawkaZaKg;
        public decimal StawkaZaKg
        {
            get => _stawkaZaKg;
            set { _stawkaZaKg = value; OnPropertyChanged(nameof(StawkaZaKg)); OnPropertyChanged(nameof(KosztDnia)); }
        }

        /// <summary>
        /// Koszt dnia = Różnica kg × Stawka za kg
        /// </summary>
        public decimal KosztDnia => Math.Round(SumaRoznicaKg * StawkaZaKg, 2);

        // === WALIDACJA ===
        public bool MaBrakiDanych { get; set; }
        public int LiczbaBrakowKM { get; set; }
        public int LiczbaBrakowGodzin { get; set; }

        // === FORMATOWANIE ===
        public string DataFormatowana => Data.ToString("dd.MM.yyyy");

        public string DzienTygodniaSkrocony
        {
            get
            {
                return DzienTygodnia switch
                {
                    "poniedziałek" => "Pon",
                    "wtorek" => "Wt",
                    "środa" => "Śr",
                    "czwartek" => "Czw",
                    "piątek" => "Pt",
                    "sobota" => "Sob",
                    "niedziela" => "Nd",
                    _ => DzienTygodnia?.Substring(0, Math.Min(3, DzienTygodnia?.Length ?? 0)) ?? ""
                };
            }
        }

        /// <summary>
        /// Suma godzin sformatowana jako "Xh Ymin"
        /// </summary>
        public string SumaGodzinFormatowana
        {
            get
            {
                int godziny = (int)SumaGodzin;
                int minuty = (int)((SumaGodzin - godziny) * 60);

                if (godziny > 0 && minuty > 0)
                    return $"{godziny}h {minuty}min";
                else if (godziny > 0)
                    return $"{godziny}h";
                else if (minuty > 0)
                    return $"{minuty}min";
                else
                    return "-";
            }
        }

        /// <summary>
        /// Czy to jest wiersz sumy (do formatowania)
        /// </summary>
        public bool JestSuma { get; set; }
    }

    /// <summary>
    /// Model ustawień Avilog (stawka, historia)
    /// </summary>
    public class AvilogSettingsModel
    {
        public int ID { get; set; }
        public decimal StawkaZaKg { get; set; }
        public DateTime DataOd { get; set; }
        public DateTime? DataDo { get; set; }
        public string ZmienionePrzez { get; set; }
        public DateTime DataZmiany { get; set; }
        public string Uwagi { get; set; }

        public bool JestAktywna => !DataDo.HasValue || DataDo.Value >= DateTime.Today;

        public string DataDoFormatowana => DataDo.HasValue ? DataDo.Value.ToString("dd.MM.yyyy") : "(aktywna)";
        public string StatusText => JestAktywna ? "AKTYWNA" : "nieaktywna";
    }

    /// <summary>
    /// Model podsumowania całego okresu
    /// </summary>
    public class AvilogSummaryModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public DateTime DataOd { get; set; }
        public DateTime DataDo { get; set; }

        // === STATYSTYKI ===
        public int LiczbaKursow { get; set; }
        public int LiczbaZestawow { get; set; }
        public int LiczbaDni { get; set; }

        // === SZTUKI ===
        public int SumaSztuk { get; set; }
        public int SumaUpadkowSzt { get; set; }

        // === WAGI ===
        public decimal SumaBrutto { get; set; }
        public decimal SumaTara { get; set; }
        public decimal SumaNetto { get; set; }
        public decimal SumaUpadkowKg { get; set; }
        public decimal SumaRoznicaKg { get; set; }

        // === KILOMETRY I CZAS ===
        public int SumaKM { get; set; }
        public decimal SumaGodzin { get; set; }

        // === OBLICZENIA FINANSOWE ===
        private decimal _stawkaZaKg = 0.119m;
        public decimal StawkaZaKg
        {
            get => _stawkaZaKg;
            set
            {
                _stawkaZaKg = value;
                OnPropertyChanged(nameof(StawkaZaKg));
                OnPropertyChanged(nameof(DoZaplaty));
                OnPropertyChanged(nameof(DoZaplatyFormatowane));
            }
        }

        /// <summary>
        /// DO ZAPŁATY = Różnica kg × Stawka za kg
        /// </summary>
        public decimal DoZaplaty => Math.Round(SumaRoznicaKg * StawkaZaKg, 2);

        // === FORMATOWANIE ===
        public string OkresFormatowany => $"{DataOd:dd.MM.yyyy} - {DataDo:dd.MM.yyyy}";
        public string DoZaplatyFormatowane => $"{DoZaplaty:N2} zł";

        /// <summary>
        /// Suma godzin sformatowana jako "Xh Ymin"
        /// </summary>
        public string SumaGodzinFormatowana
        {
            get
            {
                int godziny = (int)SumaGodzin;
                int minuty = (int)((SumaGodzin - godziny) * 60);

                if (godziny > 0 && minuty > 0)
                    return $"{godziny}h {minuty}min";
                else if (godziny > 0)
                    return $"{godziny}h";
                else if (minuty > 0)
                    return $"{minuty}min";
                else
                    return "-";
            }
        }

        // === ŚREDNIE ===
        public decimal SredniaWagaKurczaka => SumaSztuk > 0 ? Math.Round(SumaNetto / SumaSztuk, 3) : 0;
        public decimal SredniKMNaKurs => LiczbaKursow > 0 ? Math.Round((decimal)SumaKM / LiczbaKursow, 1) : 0;
        public decimal SredniCzasKursu => LiczbaKursow > 0 ? Math.Round(SumaGodzin / LiczbaKursow, 2) : 0;
        public decimal SrednieSztukiNaKurs => LiczbaKursow > 0 ? Math.Round((decimal)SumaSztuk / LiczbaKursow, 0) : 0;
    }
}
