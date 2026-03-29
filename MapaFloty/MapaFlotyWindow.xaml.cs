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
    }
}
