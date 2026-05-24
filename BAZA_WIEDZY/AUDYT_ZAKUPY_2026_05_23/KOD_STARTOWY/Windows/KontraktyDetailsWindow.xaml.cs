// ════════════════════════════════════════════════════════════════════════════
// KontraktyDetailsWindow.xaml.cs — Faza 2 (read-only + 4 zakładki + dodaj skan)
// Target: Kontrakty/Windows/KontraktyDetailsWindow.xaml.cs
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using Kalendarz1.Kontrakty.Models;
using Kalendarz1.Kontrakty.Services;

namespace Kalendarz1.Kontrakty.Windows
{
    public partial class KontraktyDetailsWindow : Window
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private const string UMOWY_ROOT = @"\\192.168.0.170\Install\UmowyZakupu";

        private readonly KontraktyService _svc = new();
        private readonly int _kontraktId;
        private KontraktDto? _model;

        public KontraktyDetailsWindow(int kontraktId)
        {
            InitializeComponent();
            _kontraktId = kontraktId;
            Loaded += async (_, _) => await LoadAllAsync();
        }

        private async Task LoadAllAsync()
        {
            _model = await _svc.GetByIdAsync(_kontraktId);
            if (_model == null)
            {
                MessageBox.Show("Kontrakt nie istnieje.", "Błąd");
                Close();
                return;
            }

            txtHeader.Text = $"{_model.NumerKontraktu} — {_model.NazwaHodowcySnapshot}";
            txtSub.Text = $"{_model.TypKontraktu} | {_model.Status} | " +
                          $"{_model.DataObowiazujeOd:dd.MM.yyyy} – {(_model.DataObowiazujeDo?.ToString("dd.MM.yyyy") ?? "bezterminowo")}";

            BudujPodstawowe();
            await Task.WhenAll(LoadZalacznikiAsync(), LoadAuditAsync(), LoadDostawyAsync());
        }

        // ── ZAKŁADKA 1 ───────────────────────────────────────────────────────
        private void BudujPodstawowe()
        {
            void Wiersz(string label, string? val)
            {
                var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var l = new TextBlock { Text = label, Foreground = System.Windows.Media.Brushes.Gray, FontSize = 12 };
                var v = new TextBlock { Text = val ?? "—", FontSize = 12, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
                Grid.SetColumn(v, 1);
                g.Children.Add(l); g.Children.Add(v);
                panelPodstawowe.Children.Add(g);
            }

            var m = _model!;
            Wiersz("Numer kontraktu", m.NumerKontraktu);
            Wiersz("Status", m.Status);
            Wiersz("Typ", m.TypKontraktu);
            Wiersz("Liczy się do ARiMR", m.LiczySieDoArimr ? "TAK (pod dotację)" : "nie");
            Wiersz("Hodowca", m.NazwaHodowcySnapshot);
            Wiersz("NIP", m.NipSnapshot);
            Wiersz("Nr gospodarstwa", m.NrGospodarstwaSnapshot);
            Wiersz("Adres", m.AdresSnapshot);
            Wiersz("Nasz podmiot", m.PartiaPiorkowscy);
            Wiersz("Obowiązuje od", m.DataObowiazujeOd.ToString("dd.MM.yyyy"));
            Wiersz("Obowiązuje do", m.DataObowiazujeDo?.ToString("dd.MM.yyyy") ?? "na czas nieokreślony");
            Wiersz("Dni do wygaśnięcia", m.DniDoWygasniecia?.ToString() ?? "—");
            Wiersz("Okres wypowiedzenia", $"{m.OkresWypowiedzeniaDni} dni");
            Wiersz("% ubytku", m.ProcentUbytku.ToString("F2"));
            Wiersz("Typ ceny", m.TypCeny);
            Wiersz("Cena", m.Cena?.ToString("F2") + " zł/kg" ?? "wg cennika dnia");
            Wiersz("Termin płatności", $"{m.TerminPlatnosciDni} dni");
            Wiersz("Rozliczana waga", m.RozliczanaWaga);
            Wiersz("Utworzył", $"{m.UtworzylUserId} ({m.UtworzylKiedy:dd.MM.yyyy HH:mm})");
            Wiersz("Plik Word", m.SciezkaWord);
            Wiersz("Skan PDF", m.SciezkaPdfSkan);
        }

        // ── ZAKŁADKA 2: załączniki ───────────────────────────────────────────
        private async Task LoadZalacznikiAsync()
        {
            const string sql = @"
SELECT Id, KontraktId, TypZalacznika, NazwaPliku, SciezkaUnc, DodalUserId, DodanyKiedy, Opis
FROM dbo.KontraktyZalaczniki WHERE KontraktId = @K ORDER BY DodanyKiedy DESC;";
            var list = new ObservableCollection<KontraktZalacznikDto>();
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@K", _kontraktId);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                list.Add(new KontraktZalacznikDto
                {
                    Id = (int)rdr["Id"],
                    KontraktId = (int)rdr["KontraktId"],
                    TypZalacznika = (string)rdr["TypZalacznika"],
                    NazwaPliku = (string)rdr["NazwaPliku"],
                    SciezkaUnc = (string)rdr["SciezkaUnc"],
                    DodalUserId = (string)rdr["DodalUserId"],
                    DodanyKiedy = (DateTime)rdr["DodanyKiedy"],
                    Opis = rdr["Opis"] as string
                });
            }
            dgZalaczniki.ItemsSource = list;
        }

        private void DgZalaczniki_DoubleClick(object sender, RoutedEventArgs e)
        {
            if (dgZalaczniki.SelectedItem is KontraktZalacznikDto z && File.Exists(z.SciezkaUnc))
                Process.Start("explorer", $"\"{z.SciezkaUnc}\"");
            else
                MessageBox.Show("Plik niedostępny (sieć / przeniesiony).", "Uwaga");
        }

        // ── ZAKŁADKA 3: audit ────────────────────────────────────────────────
        private async Task LoadAuditAsync()
        {
            const string sql = @"
SELECT Kiedy, UserId, Akcja, PoleZmienione, StaraWartosc, NowaWartosc
FROM dbo.KontraktyAudit WHERE KontraktId = @K ORDER BY Kiedy DESC;";
            var dt = new DataTable();
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@K", _kontraktId);
            using var rdr = await cmd.ExecuteReaderAsync();
            var rows = new List<object>();
            while (await rdr.ReadAsync())
            {
                rows.Add(new
                {
                    Kiedy = (DateTime)rdr["Kiedy"],
                    UserId = rdr["UserId"] as string,
                    Akcja = rdr["Akcja"] as string,
                    PoleZmienione = rdr["PoleZmienione"] as string,
                    Zmiana = $"{rdr["StaraWartosc"] as string ?? "—"} → {rdr["NowaWartosc"] as string ?? "—"}"
                });
            }
            dgAudit.ItemsSource = rows;
        }

        // ── ZAKŁADKA 4: dostawy w okresie ────────────────────────────────────
        private async Task LoadDostawyAsync()
        {
            if (_model == null) return;
            // HarmonogramDostaw dla hodowcy w okresie kontraktu
            const string sql = @"
SELECT DataOdbioru, SztukiDek AS Sztuki, WagaDek AS Waga, Auta
FROM dbo.HarmonogramDostaw
WHERE Dostawca = @Dostawca
  AND DataOdbioru >= @Od
  AND (@Do IS NULL OR DataOdbioru <= @Do)
ORDER BY DataOdbioru DESC;";
            var rows = new List<object>();
            try
            {
                using var conn = new SqlConnection(ConnLibra);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@Dostawca", _model.NazwaHodowcySnapshot ?? "");
                cmd.Parameters.AddWithValue("@Od", _model.DataObowiazujeOd);
                cmd.Parameters.AddWithValue("@Do", (object?)_model.DataObowiazujeDo ?? DBNull.Value);
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    rows.Add(new
                    {
                        DataOdbioru = rdr["DataOdbioru"] as DateTime?,
                        Sztuki = rdr["Sztuki"]?.ToString(),
                        Waga = rdr["Waga"]?.ToString(),
                        Auta = rdr["Auta"]?.ToString()
                    });
                }
            }
            catch
            {
                // HarmonogramDostaw może mieć inną strukturę kolumn — defensywnie pomijamy
            }
            dgDostawy.ItemsSource = rows;
        }

        // ── PRZYCISKI ────────────────────────────────────────────────────────
        private async void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_model == null) return;
            var dlg = new KontraktyEditorWindow(_model) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                panelPodstawowe.Children.Clear();
                await LoadAllAsync();
            }
        }

        private async void BtnAddScan_Click(object sender, RoutedEventArgs e)
        {
            if (_model == null) return;
            var ofd = new OpenFileDialog
            {
                Title = "Wybierz skan podpisanej umowy (PDF)",
                Filter = "PDF|*.pdf|Obrazy|*.png;*.jpg;*.jpeg|Wszystkie|*.*"
            };
            if (ofd.ShowDialog() != true) return;

            try
            {
                var rokDir = Path.Combine(UMOWY_ROOT, _model.Rok.ToString());
                Directory.CreateDirectory(rokDir);
                var ext = Path.GetExtension(ofd.FileName);
                var nazwiskoSan = (_model.NazwaHodowcySnapshot ?? "Hodowca").Replace(' ', '_').Replace('/', '_');
                var destName = $"Umowa_{nazwiskoSan}_{_model.NumerKontraktu.Replace('/', '_')}_signed{ext}";
                var dest = Path.Combine(rokDir, destName);
                File.Copy(ofd.FileName, dest, overwrite: true);

                // Wpis do KontraktyZalaczniki + status SIGNED
                const string sql = @"
INSERT INTO dbo.KontraktyZalaczniki (KontraktId, TypZalacznika, NazwaPliku, SciezkaUnc, DodalUserId, Opis)
VALUES (@K, 'SKAN_PODPISANY', @N, @S, @U, 'Skan podpisanej umowy');";
                using (var conn = new SqlConnection(ConnLibra))
                {
                    await conn.OpenAsync();
                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@K", _kontraktId);
                    cmd.Parameters.AddWithValue("@N", destName);
                    cmd.Parameters.AddWithValue("@S", dest);
                    cmd.Parameters.AddWithValue("@U", App.UserID ?? "?");
                    await cmd.ExecuteNonQueryAsync();
                }

                _model.SciezkaPdfSkan = dest;
                if (_model.Status is "SENT" or "PRINTED" or "DRAFT")
                    await _svc.ChangeStatusAsync(_kontraktId, "SIGNED", App.UserID ?? "?");
                await _svc.UpdateAsync(_model, App.UserID ?? "?");

                MessageBox.Show($"Skan dodany:\n{dest}\nStatus → SIGNED.", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadZalacznikiAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd dodawania skanu:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
