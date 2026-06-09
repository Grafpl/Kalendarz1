using System.Collections.Generic;
using System.Windows;
using Kalendarz1.SkrzynkaZakupu.Models;

namespace Kalendarz1.SkrzynkaZakupu.Views
{
    public partial class WyborFolderuDialog : Window
    {
        public MailFolderModel? Wybrany { get; private set; }

        public WyborFolderuDialog(IEnumerable<MailFolderModel> foldery)
        {
            InitializeComponent();
            LstFoldery.ItemsSource = foldery;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e) => Zatwierdz();
        private void LstFoldery_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => Zatwierdz();

        private void Zatwierdz()
        {
            Wybrany = LstFoldery.SelectedItem as MailFolderModel;
            if (Wybrany == null) return;
            DialogResult = true;
            Close();
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
