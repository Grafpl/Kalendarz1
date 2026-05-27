// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/Controls/KursBarControl.xaml.cs — pasek kursu na osi czasu.
// Kolory ustawiane w kodzie (bez StaticResource — kontrolka dodawana do Canvas).
// 4-stanowa kolorystyka (priorytet): pusty / przeładowany / brak przydziału / OK.
// ════════════════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kalendarz1.Transport.WPF.Models;

namespace Kalendarz1.Transport.WPF.Controls
{
    public partial class KursBarControl : UserControl
    {
        public KursBar Kurs { get; private set; } = null!;

        public KursBarControl() { InitializeComponent(); }

        public void Bind(KursBar k)
        {
            Kurs = k;
            TxtTrasa.Text = k.Trasa;
            TtTrasa.Text = $"Kurs #{k.KursID}: {k.Trasa}";
            TtCzas.Text = $"{k.Wyjazd:hh\\:mm}–{k.Powrot:hh\\:mm}" + (k.BrakGodzin ? "  (godziny domyślne)" : "");
            TtKP.Text = $"{(string.IsNullOrEmpty(k.KierowcaNazwa) ? "— brak kierowcy" : k.KierowcaNazwa)} · " +
                        $"{(string.IsNullOrEmpty(k.PojazdRej) ? "— brak pojazdu" : k.PojazdRej)}";
            TtMetryki.Text = $"{k.LiczbaLadunkow} ład. · {k.Poj} poj · {k.WypProc}% wypełnienia";
            TtUtw.Text = string.IsNullOrEmpty(k.UtworzylName) ? "" : $"Utworzył: {k.UtworzylName} · {k.UtworzylData}";

            var (bg, fg, accent) = Kolory(k.Stan);
            Pasek.Background = new SolidColorBrush(bg);
            Pasek.BorderBrush = new SolidColorBrush(accent);
            TxtTrasa.Foreground = new SolidColorBrush(fg);
            BadgeWypelnienie.Background = new SolidColorBrush(accent);
            TxtBadge.Text = $"{k.WypProc}%";

            if (k.Konflikt) OznaczKonflikt();
        }

        public void OznaczKonflikt()
        {
            Pasek.BorderThickness = new Thickness(3, 1.6, 1.6, 1.6);
            Pasek.BorderBrush = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
            TtKonflikt.Text = $"⚠ Konflikt: kierowca ma nakładający się kurs";
            TtKonflikt.Visibility = Visibility.Visible;
        }

        private static (Color bg, Color fg, Color accent) Kolory(int stan) => stan switch
        {
            0 => (Color.FromRgb(0xFD, 0xEC, 0xEC), Color.FromRgb(0xC6, 0x28, 0x28), Color.FromRgb(0xC6, 0x28, 0x28)), // pusty
            1 => (Color.FromRgb(0xFD, 0xEC, 0xEC), Color.FromRgb(0xC6, 0x28, 0x28), Color.FromRgb(0xC6, 0x28, 0x28)), // przeładowany
            2 => (Color.FromRgb(0xFF, 0xF3, 0xDC), Color.FromRgb(0xB2, 0x6A, 0x00), Color.FromRgb(0xB2, 0x6A, 0x00)), // brak przydziału
            _ => (Color.FromRgb(0xE7, 0xF4, 0xE8), Color.FromRgb(0x2E, 0x7D, 0x32), Color.FromRgb(0x2E, 0x7D, 0x32)), // OK
        };
    }
}
