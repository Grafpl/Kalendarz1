using System.Windows;

namespace Kalendarz1.MarketIntelligence.Views
{
    /// <summary>
    /// Pełny przewodnik po module Briefing AI — otwierany z przycisku ❓ Jak to działa.
    /// Statyczna treść, brak load logic.
    /// </summary>
    public partial class BriefingHelpWindow : Window
    {
        public BriefingHelpWindow()
        {
            InitializeComponent();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
