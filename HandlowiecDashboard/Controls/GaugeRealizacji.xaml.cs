using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1.HandlowiecDashboard.Controls
{
    /// <summary>
    /// Kontrolka Gauge do wyświetlania realizacji celu handlowca
    /// </summary>
    public partial class GaugeRealizacji : UserControl
    {
        public GaugeRealizacji()
        {
            InitializeComponent();
            LabelFormatter = val => $"{val:F0}%";
        }

        // Dependency Properties

        public static readonly DependencyProperty TytulProperty =
            DependencyProperty.Register(nameof(Tytul), typeof(string), typeof(GaugeRealizacji),
                new PropertyMetadata("Realizacja celu"));

        public string Tytul
        {
            get => (string)GetValue(TytulProperty);
            set => SetValue(TytulProperty, value);
        }

        public static readonly DependencyProperty WartoscProperty =
            DependencyProperty.Register(nameof(Wartosc), typeof(double), typeof(GaugeRealizacji),
                new PropertyMetadata(0.0, OnWartoscChanged));

        public double Wartosc
        {
            get => (double)GetValue(WartoscProperty);
            set => SetValue(WartoscProperty, value);
        }

        public static readonly DependencyProperty WartoscTekstProperty =
            DependencyProperty.Register(nameof(WartoscTekst), typeof(string), typeof(GaugeRealizacji),
                new PropertyMetadata("0 / 0 zl"));

        public string WartoscTekst
        {
            get => (string)GetValue(WartoscTekstProperty);
            set => SetValue(WartoscTekstProperty, value);
        }

        public static readonly DependencyProperty PrognozaTekstProperty =
            DependencyProperty.Register(nameof(PrognozaTekst), typeof(string), typeof(GaugeRealizacji),
                new PropertyMetadata(""));

        public string PrognozaTekst
        {
            get => (string)GetValue(PrognozaTekstProperty);
            set => SetValue(PrognozaTekstProperty, value);
        }

        public static readonly DependencyProperty KolorProperty =
            DependencyProperty.Register(nameof(Kolor), typeof(string), typeof(GaugeRealizacji),
                new PropertyMetadata("#27AE60", OnKolorChanged));

        public string Kolor
        {
            get => (string)GetValue(KolorProperty);
            set => SetValue(KolorProperty, value);
        }

        // Brush dla LiveCharts Gauge
        private Brush _kolorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));
        public Brush KolorBrush => _kolorBrush;

        // Formatter dla etykiety Gauge
        public Func<double, string> LabelFormatter { get; set; }

        private static void OnWartoscChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (GaugeRealizacji)d;
            var wartosc = (double)e.NewValue;

            // Automatycznie ustaw kolor na podstawie wartości
            control.Kolor = wartosc switch
            {
                >= 100 => "#27AE60",  // Zielony - cel osiągnięty
                >= 80 => "#F39C12",   // Żółty - blisko
                >= 50 => "#E67E22",   // Pomarańczowy - połowa
                _ => "#E74C3C"        // Czerwony - poniżej 50%
            };
        }

        private static void OnKolorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (GaugeRealizacji)d;
            try
            {
                control._kolorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString((string)e.NewValue));
            }
            catch
            {
                control._kolorBrush = new SolidColorBrush(Colors.Gray);
            }
        }
    }
}
