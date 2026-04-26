using System.Windows;
using System.Windows.Input;

namespace Kalendarz1.Zywiec.Kalendarz
{
    /// <summary>
    /// Okno pomocy ze skrótami klawiszowymi (wywoływane przez F1 lub przycisk "?").
    /// </summary>
    public partial class SkrotyKlawiszoweWindow : Window
    {
        public SkrotyKlawiszoweWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Esc lub F1 zamyka okno
            if (e.Key == Key.Escape || e.Key == Key.F1)
            {
                this.Close();
                e.Handled = true;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
