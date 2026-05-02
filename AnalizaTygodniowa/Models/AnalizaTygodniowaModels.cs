using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Data;

namespace Kalendarz1.AnalizaTygodniowa.Models
{
    public class PodsumowanieDniaModel : INotifyPropertyChanged
    {
        public DateTime Data { get; set; }
        public string DzienTygodnia => Data.ToString("dddd", new CultureInfo("pl-PL"));
        public decimal IloscWyprodukowana { get; set; }
        public decimal IloscSprzedana { get; set; }
        public decimal PrognozaSprzedazy { get; set; }
        public decimal Wariancja => IloscWyprodukowana - IloscSprzedana;
        public decimal WariancjaPrognozy => IloscSprzedana - PrognozaSprzedazy;
        public decimal WartoscSprzedazy { get; set; }
        public decimal ProcentSprzedazy => IloscWyprodukowana == 0 ? 0 : IloscSprzedana / IloscWyprodukowana * 100;

        // |sprzedaż - prognoza| / prognoza * 100
        public decimal Mape => PrognozaSprzedazy <= 0 ? 0
            : Math.Abs(IloscSprzedana - PrognozaSprzedazy) / PrognozaSprzedazy * 100;

        // Wyznaczane statystycznie: |Δ - mean| > 2σ
        public bool Anomalia { get; set; }
        public string AnomaliaIcon => Anomalia ? "⚡" : "";

        public List<SzczegolSprzedazyModel> SzczegolySprzedazy { get; set; } = new();
        public List<SzczegolProdukcjiModel> SzczegolyProdukcji { get; set; } = new();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Wpis w panelu Top-N (ranking odbiorców / handlowców / produktów)
    public class RankingItem
    {
        public int Pozycja { get; set; }
        public string Klucz { get; set; }     // identyfikator (kod/id/nazwa)
        public string Nazwa { get; set; }     // display
        public decimal Ilosc { get; set; }    // kg
        public decimal Wartosc { get; set; }  // zł
        public decimal ProcentOgolu { get; set; } // udział % w łącznej sprzedaży
    }

    // Heatmapa: jeden wiersz = jeden towar, kolumny = dni, wartość = sprzedaż dnia
    public class HeatmapaWiersz
    {
        public string KodTowaru { get; set; }
        public string NazwaTowaru { get; set; }
        public Dictionary<DateTime, decimal> Sprzedaz { get; set; } = new();
        public decimal SumaSprzedazy { get; set; }
        public decimal SumaProdukcji { get; set; }
    }

    public class SzczegolSprzedazyModel
    {
        public string NazwaKontrahenta { get; set; }
        public string Handlowiec { get; set; }
        public string KodTowaru { get; set; }
        public string NazwaTowaru { get; set; }
        public decimal Ilosc { get; set; }
        public decimal Cena { get; set; }
        public decimal Wartosc => Ilosc * Cena;
        public string NumerDokumentu { get; set; }
    }

    public class SzczegolProdukcjiModel
    {
        public string NumerDokumentu { get; set; }
        public string KodTowaru { get; set; }
        public string NazwaTowaru { get; set; }
        public decimal Ilosc { get; set; }
    }

    public class SuroweDaneSQl
    {
        public DateTime Data { get; set; }
        public string TypOperacji { get; set; }
        public string KodTowaru { get; set; }
        public string NazwaTowaru { get; set; }
        public decimal Ilosc { get; set; }
        public decimal Cena { get; set; }
        public string NazwaKontrahenta { get; set; }
        public string Handlowiec { get; set; }
        public string NumerDokumentu { get; set; }
    }

    public class TowarComboItem
    {
        public int Id { get; set; }
        public string Kod { get; set; }
        public string Nazwa { get; set; }
        public string DisplayText => Id == 0 ? Kod : $"{Kod}  {Nazwa}";
    }

    public class AnalizaTygodniowaFilter
    {
        public DateTime DataOd { get; set; }
        public DateTime DataDo { get; set; }
        public int? TowarId { get; set; }
        public List<string> Handlowcy { get; set; } = new();
        public List<int> OdbiorcyIds { get; set; } = new();
        public bool UkryjKorekty { get; set; }
    }

    public class ValueToSignConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return 0;
            if (value is decimal d) return d < 0 ? -1 : (d > 0 ? 1 : 0);
            if (value is double dd) return dd < 0 ? -1 : (dd > 0 ? 1 : 0);
            if (value is int i) return i < 0 ? -1 : (i > 0 ? 1 : 0);
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
