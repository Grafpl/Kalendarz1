using System.Windows;

namespace Kalendarz1.MapaFloty
{
    /// <summary>
    /// Shim window dla MapaFloty.RaportEfektywnosciView (Faza 5.1).
    /// Cała logika w UserControl — Window jest tylko hostem (accessMap[64] compatibility).
    /// W TabControl używa się RaportEfektywnosciView bezpośrednio.
    /// </summary>
    public partial class RaportEfektywnosciWindow : Window
    {
        public RaportEfektywnosciWindow()
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
        }
    }
}
