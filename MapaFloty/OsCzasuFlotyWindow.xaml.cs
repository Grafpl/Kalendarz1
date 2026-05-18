using System.Windows;

namespace Kalendarz1.MapaFloty
{
    /// <summary>
    /// Shim window dla MapaFloty.OsCzasuFlotyView (Faza 5.1).
    /// Cała logika w UserControl — Window jest tylko hostem (accessMap[63] compatibility).
    /// W TabControl używa się OsCzasuFlotyView bezpośrednio.
    /// </summary>
    public partial class OsCzasuFlotyWindow : Window
    {
        public OsCzasuFlotyWindow()
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
        }
    }
}
