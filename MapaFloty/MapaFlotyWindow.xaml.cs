using System;
using System.Windows;

namespace Kalendarz1.MapaFloty
{
    public partial class MapaFlotyWindow : Window
    {
        public MapaFlotyWindow()
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
        }

        /// <summary>
        /// Faza 4-D — otwiera okno z auto-włączoną warstwą wolnych zamówień dla podanej daty.
        /// Wywołane np. z TransportMainFormImproved.BtnMapa_Click zamiast TransportMapaWindow.
        /// </summary>
        public MapaFlotyWindow(DateTime ordersDate) : this()
        {
            FlotaView?.ShowOrdersForDate(ordersDate);
        }
    }
}
