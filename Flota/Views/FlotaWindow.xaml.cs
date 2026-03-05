using System.Windows;

namespace Kalendarz1.Flota.Views
{
    public partial class FlotaWindow : Window
    {
        public FlotaWindow()
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
        }
    }
}
