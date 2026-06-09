using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kalendarz1.AnalitykaPelna.Models
{
    public enum GranulacjaCzasu
    {
        Dzien,
        Tydzien,
        Miesiac
    }

    /// <summary>Pozycja na liście wyboru towarów (lewa kolumna, checkboxy).</summary>
    public class TowarPickerItem : INotifyPropertyChanged
    {
        public int IdHandel { get; set; }
        public string Kod { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public string Katalog { get; set; } = "";
        public decimal SumaPrzychoduPLN { get; set; }
        public decimal SumaIloscKg { get; set; }

        private bool _zaznaczony;
        public bool Zaznaczony
        {
            get => _zaznaczony;
            set
            {
                if (_zaznaczony == value) return;
                _zaznaczony = value;
                OnPropertyChanged();
            }
        }

        public string DisplayLabel => $"{Kod}  {Nazwa}";
        public string PrzychodFormatted => SumaPrzychoduPLN.ToString("N0") + " zł";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? ""));
    }

    /// <summary>Surowy wiersz przychodu z bazy: jeden towar × jeden okres (dzień).</summary>
    public class PrzychodPerOkresDay
    {
        public int IdHandel { get; set; }
        public DateTime Data { get; set; }
        public decimal Wartosc { get; set; }
        public decimal Ilosc { get; set; }
    }

    /// <summary>Punkt na osi czasu po agregacji wg granulacji (D/T/M).</summary>
    public class PunktCzasu
    {
        public DateTime PoczatekOkresu { get; set; }
        public string EtykietaKrotka { get; set; } = "";
        public string EtykietaPelna { get; set; } = "";
    }

    /// <summary>Wynik dla pojedynczego towaru po agregacji — % udziału w przychodzie per okres.</summary>
    public class TowarUdzialSeria
    {
        public int IdHandel { get; set; }
        public string Kod { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public string Kolor { get; set; } = "#7C3AED";

        // Klucz = PoczatekOkresu (ten sam co PunktCzasu.PoczatekOkresu)
        public Dictionary<DateTime, decimal> WartoscPerOkres { get; set; } = new();
        public Dictionary<DateTime, decimal> UdzialProcPerOkres { get; set; } = new();

        public decimal SumaWartosci { get; set; }
        public decimal SredniUdzialProc { get; set; }
    }

    /// <summary>Komplet danych do wykresu i tabeli.</summary>
    public class UdzialPrzychoduDataSet
    {
        public List<PunktCzasu> OsCzasu { get; set; } = new();

        /// <summary>Suma przychodu (PLN) wszystkich towarów per okres — mianownik %.</summary>
        public Dictionary<DateTime, decimal> SumaCalkowitaPerOkres { get; set; } = new();

        public List<TowarUdzialSeria> Serie { get; set; } = new();

        public decimal SumaCalkowitaPLN { get; set; }
        public decimal SumaZaznaczonychPLN { get; set; }
        public decimal UdzialZaznaczonychProc => SumaCalkowitaPLN > 0
            ? SumaZaznaczonychPLN * 100m / SumaCalkowitaPLN : 0m;

        public GranulacjaCzasu Granulacja { get; set; }
        public DateTime DataOd { get; set; }
        public DateTime DataDo { get; set; }
    }
}
