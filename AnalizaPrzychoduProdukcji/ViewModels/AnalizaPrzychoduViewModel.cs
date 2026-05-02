using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LiveCharts;

namespace Kalendarz1.AnalizaPrzychoduProdukcji.ViewModels
{
    public class AnalizaPrzychoduViewModel : INotifyPropertyChanged
    {
        private ChartValues<double> _przychodValues = new();
        public ChartValues<double> PrzychodValues
        {
            get => _przychodValues;
            set { _przychodValues = value; OnPropertyChanged(); }
        }

        private List<string> _przychodLabels = new();
        public List<string> PrzychodLabels
        {
            get => _przychodLabels;
            set { _przychodLabels = value; OnPropertyChanged(); }
        }

        private ChartValues<double> _operatorSumaValues = new();
        public ChartValues<double> OperatorSumaValues
        {
            get => _operatorSumaValues;
            set { _operatorSumaValues = value; OnPropertyChanged(); }
        }

        private List<string> _operatorLabels = new();
        public List<string> OperatorLabels
        {
            get => _operatorLabels;
            set { _operatorLabels = value; OnPropertyChanged(); }
        }

        // Granice zmian dopasowane do realiów: dzienna 5–21, nocna 21–5
        private List<string> _zmianyLabels = new() { "Dzienna (5-21)", "Nocna (21-5)" };
        public List<string> ZmianyLabels
        {
            get => _zmianyLabels;
            set { _zmianyLabels = value; OnPropertyChanged(); }
        }

        private List<string> _dniTygodniaLabels = new() { "Pn", "Wt", "Śr", "Cz", "Pt", "So", "Nd" };
        public List<string> DniTygodniaLabels
        {
            get => _dniTygodniaLabels;
            set { _dniTygodniaLabels = value; OnPropertyChanged(); }
        }

        // Sprzedaż – top klienci
        private ChartValues<double> _sprzedazValues = new();
        public ChartValues<double> SprzedazValues
        {
            get => _sprzedazValues;
            set { _sprzedazValues = value; OnPropertyChanged(); }
        }

        private List<string> _sprzedazLabels = new();
        public List<string> SprzedazLabels
        {
            get => _sprzedazLabels;
            set { _sprzedazLabels = value; OnPropertyChanged(); }
        }

        public Func<double, string> YFormatter { get; set; } = value => value.ToString("N0") + " kg";
        public Func<double, string> ZlFormatter { get; set; } = value => value.ToString("N0") + " zł";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
