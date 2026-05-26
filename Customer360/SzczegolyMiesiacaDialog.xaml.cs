using Kalendarz1.Customer360.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Kalendarz1.Customer360
{
    public partial class SzczegolyMiesiacaDialog : Window
    {
        public SzczegolyMiesiacaDialog(string tytul, List<FakturaDetail> faktury, List<OrderHistoryItem> zamowienia)
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }

            LblTytul.Text = tytul;
            GridFaktury.ItemsSource = faktury;
            GridZamowienia.ItemsSource = zamowienia;

            decimal fakBrutto = faktury.Sum(f => f.Brutto);
            decimal fakKg = faktury.Sum(f => f.SumaKg);
            decimal zamKg = zamowienia.Sum(z => z.SumaKg);
            decimal zamWart = zamowienia.Sum(z => z.Wartosc);

            LblFaktury.Text = $"💰 Faktury ({faktury.Count}) — {fakBrutto:N0} zł brutto · {fakKg:N0} kg";
            LblZamowienia.Text = $"🛒 Zamówienia ({zamowienia.Count}) — {zamWart:N0} zł · {zamKg:N0} kg";

            string roznica = "";
            if (zamKg > 0 || fakKg > 0)
            {
                decimal r = fakKg - zamKg;
                roznica = $" · różnica kg (faktury − zamówienia): {(r >= 0 ? "+" : "")}{r:N0} kg";
            }
            LblPodsumowanie.Text = $"Faktur: {faktury.Count} · Zamówień: {zamowienia.Count}{roznica}";
        }
    }
}
