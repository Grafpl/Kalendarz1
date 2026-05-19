using System.Windows;

namespace Kalendarz1.Transport
{
    /// <summary>
    /// TransportHubWindow (Faza 7+ — pełen WPF planowanie) — jeden hub dla operacji transportowych.
    /// 3 zakładki: Planowanie (TransportPlanowanieView) | Zmiany | Mapa Floty LIVE.
    ///
    /// WPF defery realizację Visual tree dla non-selected TabItem'ów, więc
    /// Loaded handlers w sub-views fires dopiero gdy user kliknie zakładkę.
    /// </summary>
    public partial class TransportHubWindow : Window
    {
        public const int TabPlanowanie = 0;
        public const int TabZmiany = 1;
        public const int TabMapaLive = 2;

        public TransportHubWindow()
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
        }

        public TransportHubWindow(int startTab) : this()
        {
            if (startTab >= 0 && startTab < HubTabs.Items.Count)
                HubTabs.SelectedIndex = startTab;
        }
    }
}
