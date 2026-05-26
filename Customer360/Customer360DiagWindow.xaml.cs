using Kalendarz1.Customer360.Services;
using System;
using System.Windows;

namespace Kalendarz1.Customer360
{
    public partial class Customer360DiagWindow : Window
    {
        private readonly Customer360Service _service = new();
        private readonly int _klientId;

        public Customer360DiagWindow(int klientId)
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
            _klientId = klientId;
            Loaded += async (s, e) => await RunAsync();
        }

        private async System.Threading.Tasks.Task RunAsync()
        {
            LblStan.Text = "⏳ Zbieram dane…";
            BtnRerun.IsEnabled = false;
            BtnCopy.IsEnabled = false;
            TxtOut.Text = "Uruchamiam diagnostykę dla klienta " + _klientId + "…";
            try
            {
                var report = await _service.BuildDiagnosticReportAsync(_klientId);
                TxtOut.Text = report;
                LblStan.Text = $"✅ Gotowe ({report.Length} znaków)";
            }
            catch (Exception ex)
            {
                TxtOut.Text = "❌ KRYTYCZNY BŁĄD DIAGNOSTYKI:\n\n" + ex;
                LblStan.Text = "❌ Błąd";
            }
            finally
            {
                BtnRerun.IsEnabled = true;
                BtnCopy.IsEnabled = true;
            }
        }

        private async void BtnRerun_Click(object sender, RoutedEventArgs e) => await RunAsync();

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(TxtOut.Text);
                LblStan.Text = "📋 Skopiowano do schowka!";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Nie udało się skopiować: " + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
                string path = System.IO.Path.Combine(dir, $"C360_diag_klient{_klientId}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                System.IO.File.WriteAllText(path, TxtOut.Text, System.Text.Encoding.UTF8);
                LblStan.Text = "💾 Zapisano: " + path;
                // Otwórz w Notatniku
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Nie udało się zapisać: " + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
