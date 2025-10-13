using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kalendarz1.PrognozyUboju
{
    /// <summary>
    /// Model danych dla pojedynczego towaru z rozbiciem na dni tygodnia
    /// </summary>
    public class TowarPrognozyModel : INotifyPropertyChanged
    {
        public int TowarId { get; set; }
        public string KodTowaru { get; set; }
        public string NazwaTowaru { get; set; }

        public decimal Poniedzialek { get; set; }
        public decimal Wtorek { get; set; }
        public decimal Sroda { get; set; }
        public decimal Czwartek { get; set; }
        public decimal Piatek { get; set; }
        public decimal Sobota { get; set; }
        public decimal Niedziela { get; set; }

        public decimal SumaTydzien => Poniedzialek + Wtorek + Sroda + Czwartek + Piatek + Sobota + Niedziela;
        public decimal SredniaDzienna => SumaTydzien / 7;

        public int LiczbaTygodni { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Model danych dla odbiorcy z towarem
    /// </summary>
    public class OdbiorcaTowarModel : INotifyPropertyChanged
    {
        public int KontrahentId { get; set; }
        public string NazwaKontrahenta { get; set; }
        public string KodTowaru { get; set; }
        public string NazwaTowaru { get; set; }
        public string Handlowiec { get; set; } // NOWA WŁAŚCIWOŚĆ

        public decimal Pon { get; set; }
        public decimal Wt { get; set; }
        public decimal Sr { get; set; }
        public decimal Czw { get; set; }
        public decimal Pt { get; set; }
        public decimal Sob { get; set; }
        public decimal Ndz { get; set; }

        public decimal SumaTydzien => Pon + Wt + Sr + Czw + Pt + Sob + Ndz;
        public string FormatSuma => $"{SumaTydzien:N0} kg";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// NOWY MODEL: Model danych dla handlowca z rozbiciem na dni
    /// </summary>
    public class HandlowiecPrognozyModel : INotifyPropertyChanged
    {
        public string NazwaHandlowca { get; set; }
        public decimal Poniedzialek { get; set; }
        public decimal Wtorek { get; set; }
        public decimal Sroda { get; set; }
        public decimal Czwartek { get; set; }
        public decimal Piatek { get; set; }
        public decimal Sobota { get; set; }
        public decimal Niedziela { get; set; }
        public decimal SumaTydzien => Poniedzialek + Wtorek + Sroda + Czwartek + Piatek + Sobota + Niedziela;
        public int LiczbaTygodni { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Parametry filtrowania i analizy
    /// </summary>
    public class FiltryPrognozy
    {
        public int LiczbaTygodni { get; set; } = 8;
        public decimal MinimalnaIlosc { get; set; } = 0;
    }
}