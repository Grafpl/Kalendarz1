using System;
using System.Windows;

namespace Kalendarz1.MapaFloty
{
    /// <summary>
    /// MapaFlotyHubWindow (Faza 5.3) — jeden hub zamiast 3 osobnych okien.
    /// 3 zakładki: Mapa Live (GPS + wolne zam.) | Oś czasu (gantt 24h) | Raport (KPI flota).
    ///
    /// WPF defery realizację Visual tree dla non-selected TabItem'ów,
    /// więc Loaded handlers w sub-views (np. MapaFlotyView.InitializeWebView)
    /// fires dopiero gdy user kliknie zakładkę → lazy load wbudowany.
    /// </summary>
    public partial class MapaFlotyHubWindow : Window
    {
        public const int TabLive = 0;
        public const int TabOsCzasu = 1;
        public const int TabRaport = 2;

        public MapaFlotyHubWindow()
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
        }

        /// <summary>Otwiera hub z określoną zakładką startową.</summary>
        public MapaFlotyHubWindow(int startTab) : this()
        {
            if (startTab >= 0 && startTab < HubTabs.Items.Count)
                HubTabs.SelectedIndex = startTab;
        }

        /// <summary>Otwiera hub z auto-ładowaniem wolnych zamówień dnia (zachowuje API MapaFlotyWindow z Fazy 4-D).</summary>
        public MapaFlotyHubWindow(DateTime ordersDate) : this()
        {
            FlotaView?.ShowOrdersForDate(ordersDate);
        }
    }
}
