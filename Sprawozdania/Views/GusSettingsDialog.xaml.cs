using System.Windows;
using Kalendarz1.Sprawozdania.Services;

namespace Kalendarz1.Sprawozdania.Views
{
    public partial class GusSettingsDialog : Window
    {
        public GusSettingsDialog()
        {
            InitializeComponent();
            Load();
        }

        private void Load()
        {
            var s = GusSettingsManager.Load();
            txtRegon.Text = s.Regon;
            txtPkd.Text = s.Pkd;
            txtImie.Text = s.OsobaImie;
            txtNazwisko.Text = s.OsobaNazwisko;
            txtTelefon.Text = s.OsobaTelefon;
            txtEmailJedn.Text = s.EmailJednostki;
            txtEmailOsoby.Text = s.EmailOsoby;
            txtFolder.Text = s.FolderEksportu;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var s = GusSettingsManager.Load();
            s.Regon = txtRegon.Text.Trim();
            s.Pkd = txtPkd.Text.Trim();
            s.OsobaImie = txtImie.Text.Trim();
            s.OsobaNazwisko = txtNazwisko.Text.Trim();
            s.OsobaTelefon = txtTelefon.Text.Trim();
            s.EmailJednostki = txtEmailJedn.Text.Trim();
            s.EmailOsoby = txtEmailOsoby.Text.Trim();
            s.FolderEksportu = txtFolder.Text.Trim();

            try
            {
                GusSettingsManager.Save(s);
                DialogResult = true;
                Close();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Nie udało się zapisać konfiguracji:\n" + ex.Message,
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            // Prosty workaround — używamy SaveFileDialog z dummy nazwą żeby wybrać folder
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                FileName = "wybierz_folder",
                CheckFileExists = false,
                CheckPathExists = true,
                Title = "Wskaż folder eksportu (wybierz dowolny plik w docelowym folderze)"
            };
            if (!string.IsNullOrWhiteSpace(txtFolder.Text))
                dlg.InitialDirectory = txtFolder.Text;
            if (dlg.ShowDialog(this) == true)
            {
                var dir = System.IO.Path.GetDirectoryName(dlg.FileName);
                if (!string.IsNullOrWhiteSpace(dir))
                    txtFolder.Text = dir;
            }
        }
    }
}
