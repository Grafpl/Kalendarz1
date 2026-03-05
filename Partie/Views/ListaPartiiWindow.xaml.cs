using System.Windows;

namespace Kalendarz1.Partie.Views
{
    public partial class ListaPartiiWindow : Window
    {
        public ListaPartiiWindow()
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
        }
    }
}
