using System.Windows;

namespace Kalendarz1.Transport
{
    /// <summary>
    /// Shim window dla Transport.TransportZmianyView (Faza 7-A).
    /// Cała logika w UserControl — Window jest tylko hostem (accessMap[59] compatibility).
    /// W TransportHub'ie używa się TransportZmianyView bezpośrednio.
    /// </summary>
    public partial class TransportZmianyWindow : Window
    {
        public TransportZmianyWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
        }
    }
}
