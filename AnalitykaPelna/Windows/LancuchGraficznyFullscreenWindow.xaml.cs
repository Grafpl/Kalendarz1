using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Kalendarz1.AnalitykaPelna.Models;

namespace Kalendarz1.AnalitykaPelna.Windows
{
    /// <summary>
    /// Pełnoekranowe okno z widokiem łańcucha graficznego — WindowStyle=None + Topmost
    /// żeby zakryć taskbar. Esc/F11 = wyjście.
    /// </summary>
    public partial class LancuchGraficznyFullscreenWindow : Window
    {
        public LancuchGraficznyFullscreenWindow()
        {
            InitializeComponent();
            widokGraficzny.UkryjPrzyciskFullscreen = true;

            // True fullscreen — pokrywa cały ekran łącznie z taskbarem
            Loaded += (s, e) =>
            {
                WindowState = WindowState.Normal;
                Left = 0;
                Top = 0;
                Width = SystemParameters.PrimaryScreenWidth;
                Height = SystemParameters.PrimaryScreenHeight;
            };
        }

        public Task ZastosujFiltryAsync(FiltryAnaliz f) => widokGraficzny.ZastosujFiltryAsync(f);

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.F11)
            {
                Close();
                e.Handled = true;
            }
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e) => Close();
    }
}
