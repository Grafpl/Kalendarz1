using Kalendarz1.Transport.Formularze;
using Kalendarz1.Transport.Repozytorium;
using System.Windows;

namespace Kalendarz1.Transport
{
    /// <summary>
    /// TransportHubWindow (Faza 7) — jeden hub dla operacji transportowych.
    /// 3 zakładki: Planowanie | Zmiany do akceptacji | Mapa Floty LIVE.
    ///
    /// WPF defery realizację Visual tree dla non-selected TabItem'ów, więc
    /// Loaded handlers w sub-views (np. TransportZmianyView.View_Loaded,
    /// MapaFlotyView.InitializeWebView) fires dopiero gdy user kliknie zakładkę.
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

        private void BtnOtworzPlanowanie_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var connTransport = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                var connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
                var repo = new TransportRepozytorium(connTransport, connHandel);
                using var f = new TransportMainFormImproved(repo, App.UserID ?? "system");
                f.ShowDialog();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Błąd otwierania edytora:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
