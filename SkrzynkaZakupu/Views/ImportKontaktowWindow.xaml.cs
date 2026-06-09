using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Kalendarz1.SkrzynkaZakupu.Models;
using Kalendarz1.SkrzynkaZakupu.Services;

namespace Kalendarz1.SkrzynkaZakupu.Views
{
    public partial class ImportKontaktowWindow : Window
    {
        private readonly MailAccountSettings _cfg;
        private readonly MailContactsService _kontakty = new();
        private readonly ThunderbirdImportService _tb;
        private List<ThunderbirdImportService.TbFolder> _foldery = new();
        private CancellationTokenSource? _cts;

        public ImportKontaktowWindow(MailAccountSettings cfg)
        {
            InitializeComponent();
            _cfg = cfg;
            _tb = new ThunderbirdImportService(cfg);
            Loaded += async (_, _) => await ZaladujAsync();
        }

        private async Task ZaladujAsync()
        {
            int n = await _kontakty.GetCountAsync();
            TxtPodtytul.Text = $"Książka adresowa: {n} adresów";

            foreach (var p in ThunderbirdImportService.WykryjProfile())
                CmbProfile.Items.Add(p);
            if (CmbProfile.Items.Count > 0) CmbProfile.SelectedIndex = 0;
            else CmbProfile.Items.Add("(nie wykryto Thunderbirda — użyj Przeglądaj…)");
        }

        // ---------- skan IMAP ----------
        private async void BtnSkanImap_Click(object sender, RoutedEventArgs e)
        {
            BtnSkanImap.IsEnabled = false;
            var progress = new Progress<string>(s => TxtImapInfo.Text = s);
            try
            {
                TxtImapInfo.Text = "Łączę…";
                var imap = new ImapMailService(_cfg);
                try
                {
                    var adresy = await imap.GetAllAddressesAsync(progress);
                    TxtImapInfo.Text = $"Znaleziono {adresy.Count} adresów, zapisuję…";
                    int zapis = await _kontakty.UpsertManyAsync(adresy, "imap");
                    TxtImapInfo.Text = $"✓ Gotowe — {zapis} adresów w książce";
                }
                finally { await imap.DisconnectQuietAsync(); }
                await OdswiezLicznik();
            }
            catch (Exception ex)
            {
                TxtImapInfo.Text = "Błąd: " + ex.Message;
            }
            finally { BtnSkanImap.IsEnabled = true; }
        }

        // ---------- Thunderbird ----------
        private void BtnPrzegladaj_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Wskaż katalog profilu Thunderbirda (lub folder z plikami mbox)"
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (!CmbProfile.Items.Contains(dlg.SelectedPath))
                    CmbProfile.Items.Insert(0, dlg.SelectedPath);
                CmbProfile.SelectedItem = dlg.SelectedPath;
            }
        }

        private void BtnSzukajFolderow_Click(object sender, RoutedEventArgs e)
        {
            var root = CmbProfile.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(root) || !System.IO.Directory.Exists(root))
            {
                TxtStatus.Text = "Wybierz poprawny katalog profilu.";
                return;
            }
            _foldery = _tb.ZnajdzFoldery(root);
            LstFoldery.ItemsSource = _foldery;
            TxtBrakFolderow.Visibility = _foldery.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (_foldery.Count == 0) TxtBrakFolderow.Text = "Nie znaleziono plików mbox w tym katalogu.";
            BtnImportuj.IsEnabled = _foldery.Count > 0;
            TxtStatus.Text = _foldery.Count > 0 ? $"Znaleziono {_foldery.Count} folderów" : "";
        }

        private void ChkWgraj_Changed(object sender, RoutedEventArgs e)
        {
            PanelFolderCel.Visibility = ChkWgraj.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void BtnImportuj_Click(object sender, RoutedEventArgs e)
        {
            var wybrane = _foldery.Where(f => f.Wybrany).ToList();
            if (wybrane.Count == 0) { TxtStatus.Text = "Zaznacz przynajmniej jeden folder."; return; }

            bool wgraj = ChkWgraj.IsChecked == true;
            string cel = (TxtFolderCel.Text ?? "").Trim();
            if (wgraj && string.IsNullOrEmpty(cel)) { TxtStatus.Text = "Podaj folder docelowy."; return; }

            if (wgraj)
            {
                var potw = MessageBox.Show(
                    $"Wgrać stare maile z {wybrane.Count} folderów na serwer do „{cel}”?\n\n" +
                    "To operacja na wspólnej skrzynce — wiadomości zobaczą wszyscy z działu.",
                    "Potwierdź import", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (potw != MessageBoxResult.Yes) return;
            }

            _cts = new CancellationTokenSource();
            BtnImportuj.IsEnabled = false;
            BtnAnulujImport.Visibility = Visibility.Visible;
            PanelWynik.Visibility = Visibility.Collapsed;

            var progress = new Progress<string>(s => TxtStatus.Text = s);
            try
            {
                var res = await _tb.ImportujAsync(wybrane, wgraj, cel, progress, _cts.Token);

                PanelWynik.Visibility = Visibility.Visible;
                TxtWynik.Text =
                    $"✓ Zakończono.\n" +
                    $"• Foldery: {res.Folderow}\n" +
                    $"• Przeskanowane wiadomości: {res.Przeskanowano}\n" +
                    (wgraj ? $"• Wgrane na serwer: {res.Wgrano}\n" : "") +
                    $"• Adresy znalezione: {res.AdresowZnaleziono} (zapisane: {res.AdresowZapisano})" +
                    (res.Bledy.Count > 0 ? $"\n• Błędy: {res.Bledy.Count} (pierwszy: {res.Bledy[0]})" : "");
                TxtStatus.Text = "Gotowe";
                await OdswiezLicznik();
            }
            catch (OperationCanceledException)
            {
                TxtStatus.Text = "Przerwano.";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Błąd importu: " + ex.Message;
            }
            finally
            {
                BtnImportuj.IsEnabled = true;
                BtnAnulujImport.Visibility = Visibility.Collapsed;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void BtnAnulujImport_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

        private async Task OdswiezLicznik()
        {
            int n = await _kontakty.GetCountAsync();
            TxtPodtytul.Text = $"Książka adresowa: {n} adresów";
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e) => Close();
    }
}
