using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kalendarz1.Zywiec.Kalendarz.Models;
using Kalendarz1.Zywiec.Kalendarz.Services;

namespace Kalendarz1.Zywiec.Kalendarz.Views
{
    public partial class WszystkieDostawyWindow : Window
    {
        private static readonly string ConnectionString =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private readonly WszystkieDostawyService _service;
        private CancellationTokenSource _cts = new();
        private List<WszystkieDostawyRekord> _wszystkie = new();
        private readonly ObservableCollection<WszystkieDostawyRekord> _widoczne = new();

        public WszystkieDostawyWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _service = new WszystkieDostawyService(ConnectionString);
            dgDostawy.ItemsSource = _widoczne;

            InitFiltry();
            Loaded += async (_, _) =>
            {
                await ZaladujListyAsync();
                await OdswiezDostawyAsync();
            };
        }

        // ====== INICJALIZACJA FILTRÓW ======
        private void InitFiltry()
        {
            // Domyślnie: ostatnie 30 dni
            dpDataOd.SelectedDate = DateTime.Today.AddDays(-30);
            dpDataDo.SelectedDate = DateTime.Today.AddDays(14);

            cmbBufor.ItemsSource = new[]
            {
                "Wszystkie", "Potwierdzony", "Anulowany", "Sprzedany",
                "B.Kontr.", "B.Wolny.", "Do wykupienia", "Planowany"
            };
            cmbBufor.SelectedIndex = 0;
        }

        // ====== ŁADOWANIE LIST POMOCNICZYCH ======
        private async Task ZaladujListyAsync()
        {
            try
            {
                using var conn = new SqlConnection(ConnectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(
                    "SELECT DISTINCT Dostawca FROM dbo.HarmonogramDostaw " +
                    "WHERE Dostawca IS NOT NULL AND Dostawca <> '' ORDER BY Dostawca", conn);
                var lista = new List<string> { "" };
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) lista.Add(r.GetString(0));
                cmbDostawca.ItemsSource = lista;
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Błąd ładowania hodowców: {ex.Message}";
            }
        }

        // ====== ODŚWIEŻENIE DOSTAW ======
        private async Task OdswiezDostawyAsync()
        {
            _cts.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            txtStatus.Text = "⏳ Ładowanie...";
            txtLicznik.Text = "";

            var filtry = new WszystkieDostawyService.Filtry
            {
                DataOd = dpDataOd.SelectedDate,
                DataDo = dpDataDo.SelectedDate,
                Dostawca = string.IsNullOrWhiteSpace(cmbDostawca.Text) ? null : cmbDostawca.Text,
                Bufor = cmbBufor.SelectedItem?.ToString(),
                Szukaj = txtSzukaj.Text,
                TylkoStaliKlienci = chkTylkoStali.IsChecked == true,
                TylkoZeSmsem = chkTylkoZeSmsem.IsChecked == true,
                TylkoWymagajaAktualizacjiSms = chkWymagajaSms.IsChecked == true
            };

            try
            {
                var sw = Stopwatch.StartNew();
                _wszystkie = await _service.PobierzAsync(filtry, ct);
                sw.Stop();

                if (ct.IsCancellationRequested) return;

                _widoczne.Clear();
                foreach (var d in _wszystkie) _widoczne.Add(d);

                var kpi = _service.WyliczKpi(_wszystkie);
                txtStatus.Text = $"✓ Wczytano {_wszystkie.Count} dostaw w {sw.ElapsedMilliseconds} ms";
                // KPI scalone do paska statusu (panel kafelków usunięty na życzenie usera)
                txtLicznik.Text = $"{kpi.LiczbaDostaw} dostaw  •  {kpi.SumaAut} aut  •  {kpi.SumaSztuk:#,0} szt  •  {kpi.UnikalnychHodowcow} hodowców";
            }
            catch (OperationCanceledException) { /* nowy filtr w trakcie */ }
            catch (Exception ex)
            {
                txtStatus.Text = $"⚠ Błąd: {ex.Message}";
                MessageBox.Show($"Błąd ładowania dostaw:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ====== EVENTY UI ======
        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e) => await OdswiezDostawyAsync();
        private async void BtnZastosujFiltry_Click(object sender, RoutedEventArgs e) => await OdswiezDostawyAsync();

        private async void BtnWyczyscFiltry_Click(object sender, RoutedEventArgs e)
        {
            txtSzukaj.Text = "";
            cmbDostawca.Text = "";
            cmbBufor.SelectedIndex = 0;
            dpDataOd.SelectedDate = DateTime.Today.AddDays(-30);
            dpDataDo.SelectedDate = DateTime.Today.AddDays(14);
            chkTylkoStali.IsChecked = false;
            chkTylkoZeSmsem.IsChecked = false;
            chkWymagajaSms.IsChecked = false;
            await OdswiezDostawyAsync();
        }

        private async void TxtSzukaj_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await OdswiezDostawyAsync();
        }

        // ====== SIDE PANEL - SZCZEGÓŁY ======
        private void DgDostawy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var d = dgDostawy.SelectedItem as WszystkieDostawyRekord;
            if (d == null)
            {
                panelSzczegoly.Visibility = Visibility.Collapsed;
                txtBrakSelekcji.Visibility = Visibility.Visible;
                return;
            }
            panelSzczegoly.Visibility = Visibility.Visible;
            txtBrakSelekcji.Visibility = Visibility.Collapsed;

            txtDetDostawca.Text = (d.StalyKlient ? "★ " : "") + d.Dostawca;
            txtDetDataLp.Text = $"LP {d.LP}   •   {d.DataOdbioru:dd.MM.yyyy} ({d.DataOdbioru:dddd})";
            txtDetAuta.Text = $"🚛 Auta: {d.Auta}";
            txtDetSztuki.Text = $"🐔 Sztuki: {d.SztukiDek:#,0}";
            txtDetWaga.Text = $"⚖️ Waga: {d.WagaDek:0.00} kg";
            txtDetDoba.Text = $"📅 Doba: {d.RoznicaDni} dni" +
                (d.DataWstawienia.HasValue ? $"  (wstawienie {d.DataWstawienia:dd.MM.yyyy})" : "");
            txtDetCena.Text = $"💰 {d.TypCeny}  •  {d.Cena:0.00} zł";
            txtDetStatus.Text = $"ℹ️ Status: {d.Bufor}";

            // SMS status
            if (d.SmsWymagaAktualizacji)
                txtDetSms.Text = $"⚠️ SMS wysłany {d.SmsCreatedAt:dd.MM.yyyy HH:mm}, ale zmieniono datę lub auta — wymaga aktualizacji!";
            else if (d.BylSMS)
                txtDetSms.Text = $"📱 SMS o szczegółach wysłany {d.SmsCreatedAt:dd.MM.yyyy HH:mm} — aktualny.";
            else
                txtDetSms.Text = "Brak wysłanego SMS-a o szczegółach.";

            txtDetUwagi.Text = string.IsNullOrWhiteSpace(d.Uwagi) ? "(brak uwag)" : d.Uwagi;

            _ = LoadAuditAsync(d.LP);
        }

        // ====== HISTORIA ZMIAN (AUDIT) ======
        public sealed class AuditWiersz
        {
            public DateTime Kiedy { get; set; }
            public string Pole { get; set; } = "";
            public string Zmiana { get; set; } = "";
            public string Kto { get; set; } = "";
        }

        private async Task LoadAuditAsync(int lp)
        {
            try
            {
                var lista = new List<AuditWiersz>();
                using var conn = new SqlConnection(ConnectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    SELECT TOP 50 AL.DataZmiany, AL.PoleZmienione, AL.StaraWartosc, AL.NowaWartosc, ISNULL(O.Name, AL.UserID) AS Kto
                    FROM dbo.AuditLog AL
                    LEFT JOIN dbo.operators O ON AL.UserID = O.ID
                    WHERE AL.NazwaTabeli = 'HarmonogramDostaw' AND AL.LP = @lp
                    ORDER BY AL.DataZmiany DESC", conn) { CommandTimeout = 15 };
                cmd.Parameters.AddWithValue("@lp", lp);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    string stara = r.IsDBNull(2) ? "—" : r.GetString(2);
                    string nowa = r.IsDBNull(3) ? "—" : r.GetString(3);
                    lista.Add(new AuditWiersz
                    {
                        Kiedy = r.GetDateTime(0),
                        Pole = r.IsDBNull(1) ? "" : r.GetString(1),
                        Zmiana = $"{stara} → {nowa}",
                        Kto = r.IsDBNull(4) ? "" : r.GetString(4)
                    });
                }
                dgAudit.ItemsSource = lista;
            }
            catch
            {
                dgAudit.ItemsSource = null;
            }
        }

        // ====== QUICK ACTIONS ======
        private void DgDostawy_MouseDoubleClick(object sender, MouseButtonEventArgs e)
            => MenuPokazWKalendarzu_Click(sender, null!);

        private void MenuPokazWKalendarzu_Click(object sender, RoutedEventArgs e)
        {
            var d = dgDostawy.SelectedItem as WszystkieDostawyRekord;
            if (d == null) return;
            // Sygnał do głównego okna kalendarza — minimalna integracja przez Application.Current
            // Wskazujemy datę dostawy + LP, główne okno może zareagować (ale nie wymagamy)
            try
            {
                var glowne = Application.Current.Windows.OfType<WidokKalendarzaWPF>().FirstOrDefault();
                if (glowne != null)
                {
                    glowne.Activate();
                    MessageBox.Show($"Przeskocz do dostawy LP {d.LP} z dnia {d.DataOdbioru:dd.MM.yyyy}.\n\n" +
                        "(Funkcja przeniesie Cię ręcznie — kliknij wybraną datę w kalendarzu)",
                        "Otwórz w kalendarzu",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch { }
        }

        private void MenuKopiujWiersz_Click(object sender, RoutedEventArgs e)
        {
            var d = dgDostawy.SelectedItem as WszystkieDostawyRekord;
            if (d == null) return;
            string tekst =
                $"LP {d.LP}\tDostawca: {d.Dostawca}\tData: {d.DataOdbioru:dd.MM.yyyy}\t" +
                $"Aut: {d.Auta}\tSztuk: {d.SztukiDek:#,0}\tWaga: {d.WagaDek:0.00}\t" +
                $"Cena: {d.Cena:0.00} {d.TypCeny}\tStatus: {d.Bufor}";
            try
            {
                Clipboard.SetText(tekst);
                txtStatus.Text = "✓ Wiersz skopiowany do schowka";
            }
            catch { }
        }

        private async void MenuPokazAudyt_Click(object sender, RoutedEventArgs e)
        {
            var d = dgDostawy.SelectedItem as WszystkieDostawyRekord;
            if (d == null) return;
            await LoadAuditAsync(d.LP);
            txtStatus.Text = $"✓ Załadowano historię dla LP {d.LP}";
        }

        // ====== EKSPORT ======
        private void BtnEksportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_wszystkie.Count == 0) { txtStatus.Text = "Brak danych do eksportu"; return; }
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel (*.xlsx)|*.xlsx",
                FileName = $"Dostawy_{DateTime.Now:yyyy-MM-dd_HHmm}.xlsx"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _service.EksportujExcel(_wszystkie, dlg.FileName);
                txtStatus.Text = $"✓ Excel zapisany: {dlg.FileName}";
                if (MessageBox.Show("Otworzyć teraz?", "Eksport Excel",
                        MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo { FileName = dlg.FileName, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd eksportu Excel:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnEksportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_wszystkie.Count == 0) { txtStatus.Text = "Brak danych do eksportu"; return; }
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF (*.pdf)|*.pdf",
                FileName = $"Dostawy_{DateTime.Now:yyyy-MM-dd_HHmm}.pdf"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var filtry = new WszystkieDostawyService.Filtry
                {
                    DataOd = dpDataOd.SelectedDate,
                    DataDo = dpDataDo.SelectedDate
                };
                var kpi = _service.WyliczKpi(_wszystkie);
                var pdf = _service.EksportujPdf(_wszystkie, kpi, filtry);
                File.WriteAllBytes(dlg.FileName, pdf);
                txtStatus.Text = $"✓ PDF zapisany: {dlg.FileName}";
                if (MessageBox.Show("Otworzyć teraz?", "Eksport PDF",
                        MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo { FileName = dlg.FileName, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd eksportu PDF:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
