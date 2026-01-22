using System;
using System.ComponentModel;

namespace Kalendarz1.Avilog.Models
{
    /// <summary>
    /// Model pojedynczego kursu transportowego dla rozliczeń Avilog
    /// </summary>
    public class AvilogKursModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // === IDENTYFIKATORY ===
        public int ID { get; set; }
        public int LP { get; set; }
        public DateTime CalcDate { get; set; }
        public int CarLp { get; set; }

        // === POJAZDY I KIEROWCA ===
        public string CarID { get; set; }
        public string TrailerID { get; set; }
        public int? DriverGID { get; set; }
        public string KierowcaNazwa { get; set; }

        // === DOSTAWCA ===
        public string CustomerGID { get; set; }
        public string CustomerRealGID { get; set; }
        public string HodowcaNazwa { get; set; }

        // === SZTUKI ===
        public int SztukiZadeklarowane { get; set; }
        public int SztukiLumel { get; set; }
        public int SztukiPadle { get; set; }

        /// <summary>
        /// Suma sztuk = LUMEL + Padłe
        /// </summary>
        public int SztukiRazem => SztukiLumel + SztukiPadle;

        // === WAGI HODOWCY ===
        public decimal BruttoHodowcy { get; set; }
        public decimal TaraHodowcy { get; set; }
        public decimal NettoHodowcy { get; set; }

        // === WAGI UBOJNI ===
        public decimal BruttoUbojni { get; set; }
        public decimal TaraUbojni { get; set; }
        public decimal NettoUbojni { get; set; }

        // === KILOMETRY ===
        public int StartKM { get; set; }
        public int StopKM { get; set; }

        /// <summary>
        /// Dystans = StopKM - StartKM
        /// </summary>
        public int DystansKM => StopKM - StartKM;

        // === CZASY TRANSPORTU ===
        public DateTime? PoczatekUslugi { get; set; }
        public DateTime? Wyjazd { get; set; }
        public DateTime? DojazdHodowca { get; set; }
        public DateTime? Zaladunek { get; set; }
        public DateTime? ZaladunekKoniec { get; set; }
        public DateTime? WyjazdHodowca { get; set; }
        public DateTime? Przyjazd { get; set; }
        public DateTime? KoniecUslugi { get; set; }

        /// <summary>
        /// Czas usługi w godzinach
        /// </summary>
        public decimal CzasUslugiGodziny
        {
            get
            {
                if (PoczatekUslugi.HasValue && KoniecUslugi.HasValue)
                {
                    return (decimal)(KoniecUslugi.Value - PoczatekUslugi.Value).TotalHours;
                }
                return 0;
            }
        }

        // === OBLICZENIA AVILOG ===

        /// <summary>
        /// Średnia waga kurczaka = Netto / Suma sztuk
        /// </summary>
        public decimal SredniaWagaKurczaka
        {
            get
            {
                if (SztukiRazem > 0)
                    return Math.Round(NettoHodowcy / SztukiRazem, 3);
                return 0;
            }
        }

        /// <summary>
        /// Upadki w kg = Sztuki padłe × Średnia waga kurczaka
        /// </summary>
        public decimal UpadkiKg
        {
            get
            {
                if (SztukiRazem > 0)
                    return Math.Round(SztukiPadle * (NettoHodowcy / SztukiRazem), 0);
                return 0;
            }
        }

        /// <summary>
        /// Różnica kg = Netto hodowcy - Upadki kg (to idzie do rozliczenia)
        /// </summary>
        public decimal RoznicaKg => NettoHodowcy - UpadkiKg;

        /// <summary>
        /// Różnica wag między hodowcą a ubojnią w %
        /// </summary>
        public decimal RoznicaWagProcent
        {
            get
            {
                if (NettoHodowcy > 0)
                    return Math.Round(((NettoHodowcy - NettoUbojni) / NettoHodowcy) * 100, 2);
                return 0;
            }
        }

        // === WALIDACJA I OSTRZEŻENIA ===

        /// <summary>
        /// Czy brakuje danych o kilometrach
        /// </summary>
        public bool BrakKilometrow => StartKM == 0 || StopKM == 0;

        /// <summary>
        /// Czy brakuje danych o godzinach
        /// </summary>
        public bool BrakGodzin => !PoczatekUslugi.HasValue || !KoniecUslugi.HasValue;

        /// <summary>
        /// Czy jest duża różnica wag (> 2%)
        /// </summary>
        public bool DuzaRoznicaWag => Math.Abs(RoznicaWagProcent) > 2;

        /// <summary>
        /// Czy są jakieś ostrzeżenia
        /// </summary>
        public bool MaOstrzezenia => BrakKilometrow || BrakGodzin || DuzaRoznicaWag;

        /// <summary>
        /// Tekst ostrzeżenia dla tooltipa
        /// </summary>
        public string OstrzezenieText
        {
            get
            {
                var warnings = new System.Collections.Generic.List<string>();
                if (BrakKilometrow) warnings.Add("Brak danych o kilometrach");
                if (BrakGodzin) warnings.Add("Brak danych o godzinach");
                if (DuzaRoznicaWag) warnings.Add($"Duża różnica wag: {RoznicaWagProcent:N2}%");
                return string.Join("\n", warnings);
            }
        }

        // === FORMATOWANIE ===

        public string DataFormatowana => CalcDate.ToString("dd.MM.yyyy");
        public string DzienTygodnia => CalcDate.ToString("dddd", new System.Globalization.CultureInfo("pl-PL"));
        public string ZestawText => $"{CarID} / {TrailerID}";

        public string PoczatekUslugiFormatowany => PoczatekUslugi?.ToString("HH:mm") ?? "-";
        public string KoniecUslugiFormatowany => KoniecUslugi?.ToString("HH:mm") ?? "-";
        public string CzasUslugiFormatowany => CzasUslugiGodziny > 0 ? $"{CzasUslugiGodziny:N2} h" : "-";
    }
}
