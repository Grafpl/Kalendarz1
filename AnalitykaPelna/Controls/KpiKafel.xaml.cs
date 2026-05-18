using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Kalendarz1.AnalitykaPelna.Controls
{
    public partial class KpiKafel : UserControl
    {
        public static readonly DependencyProperty TytulProperty =
            DependencyProperty.Register(nameof(Tytul), typeof(string), typeof(KpiKafel),
                new PropertyMetadata(""));

        public static readonly DependencyProperty WartoscProperty =
            DependencyProperty.Register(nameof(Wartosc), typeof(string), typeof(KpiKafel),
                new PropertyMetadata("0"));

        public static readonly DependencyProperty JednostkaProperty =
            DependencyProperty.Register(nameof(Jednostka), typeof(string), typeof(KpiKafel),
                new PropertyMetadata(""));

        public static readonly DependencyProperty IkonaProperty =
            DependencyProperty.Register(nameof(Ikona), typeof(string), typeof(KpiKafel),
                new PropertyMetadata("📊"));

        public static readonly DependencyProperty KolorProperty =
            DependencyProperty.Register(nameof(Kolor), typeof(Brush), typeof(KpiKafel),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED))));

        public static readonly DependencyProperty TrendProperty =
            DependencyProperty.Register(nameof(Trend), typeof(string), typeof(KpiKafel),
                new PropertyMetadata("", OnTrendChanged));

        public string Tytul
        {
            get => (string)GetValue(TytulProperty);
            set => SetValue(TytulProperty, value);
        }

        public string Wartosc
        {
            get => (string)GetValue(WartoscProperty);
            set => SetValue(WartoscProperty, value);
        }

        public string Jednostka
        {
            get => (string)GetValue(JednostkaProperty);
            set => SetValue(JednostkaProperty, value);
        }

        public string Ikona
        {
            get => (string)GetValue(IkonaProperty);
            set => SetValue(IkonaProperty, value);
        }

        public Brush Kolor
        {
            get => (Brush)GetValue(KolorProperty);
            set => SetValue(KolorProperty, value);
        }

        public string Trend
        {
            get => (string)GetValue(TrendProperty);
            set => SetValue(TrendProperty, value);
        }

        public event MouseButtonEventHandler? KafelKlikniety;

        public KpiKafel()
        {
            InitializeComponent();
        }

        private void Border_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
            => KafelKlikniety?.Invoke(this, e);

        private static void OnTrendChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not KpiKafel k) return;
            string trend = e.NewValue as string ?? "";
            if (trend.StartsWith("+") || trend.StartsWith("▲"))
                k.txtTrend.Foreground = new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69));  // zielony
            else if (trend.StartsWith("-") || trend.StartsWith("▼"))
                k.txtTrend.Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));  // czerwony
            else
                k.txtTrend.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));  // szary
        }
    }
}
