using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Data;

// =================================================================
// ==== POPRAWKA: Zmiana przestrzeni nazw na spójną z resztą   ====
// =================================================================
namespace Kalendarz1.PrognozyUboju
{
    // Model dla głównego wiersza podsumowującego dzień
    public class PodsumowanieDniaModel : INotifyPropertyChanged
    {
        public DateTime Data { get; set; }
        public string DzienTygodnia => Data.ToString("dddd", new System.Globalization.CultureInfo("pl-PL"));
        public decimal IloscWyprodukowana { get; set; }
        public decimal IloscSprzedana { get; set; }
        public decimal PrognozaSprzedazy { get; set; }
        public decimal Wariancja => IloscWyprodukowana - IloscSprzedana;
        public decimal WariancjaPrognozy => IloscSprzedana - PrognozaSprzedazy;
        public decimal ProcentSprzedazy
        {
            get
            {
                if (IloscWyprodukowana == 0) return 0;
                return (IloscSprzedana / IloscWyprodukowana) * 100;
            }
        }

        public List<SzczegolSprzedazyModel> SzczegolySprzedazy { get; set; } = new List<SzczegolSprzedazyModel>();
        public List<SzczegolProdukcjiModel> SzczegolyProdukcji { get; set; } = new List<SzczegolProdukcjiModel>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
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
    public class ValueToSignConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return 0;

            if (value is decimal decimalValue)
            {
                if (decimalValue < 0) return -1;
                if (decimalValue > 0) return 1;
                return 0;
            }

            if (value is double doubleValue)
            {
                if (doubleValue < 0) return -1;
                if (doubleValue > 0) return 1;
                return 0;
            }

            if (value is int intValue)
            {
                if (intValue < 0) return -1;
                if (intValue > 0) return 1;
                return 0;
            }

            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack nie jest wspierany dla ValueToSignConverter");
        }
    }
}