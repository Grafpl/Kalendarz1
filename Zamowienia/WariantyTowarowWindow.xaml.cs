using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using Kalendarz1.Zamowienia.Services;

namespace Kalendarz1.Zamowienia
{
    /// <summary>
    /// Ekran admina: definiowanie wariantów wewnętrznych towaru (np. Filet A → Pojedynczy/Podwójny).
    /// Zapisuje do dbo.TowarWarianty (przez TowarWariantyService). W Symfonii nic się nie zmienia.
    /// </summary>
    public partial class WariantyTowarowWindow : Window
    {
        private const string ConnHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private const string ConnLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private readonly TowarWariantyService _svc = new(ConnLibra);
        private List<TowarRow> _wszystkie = new();
        private Dictionary<int, List<TowarWariantyService.Wariant>> _mapa = new();
        private int? _wybranyId;
        private readonly ObservableCollection<WariantRow> _warianty = new();

        public WariantyTowarowWindow()
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
            dgWarianty.ItemsSource = _warianty;
            Loaded += async (_, _) => await LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            try
            {
                txtStatus.Text = "Ładowanie…";
                _mapa = await _svc.GetMapaAsync();

                var lista = new List<TowarRow>();
                await using (var cn = new SqlConnection(ConnHandel))
                {
                    await cn.OpenAsync();
                    // Mięso świeże + mrożone (te same katalogi co okno zamówienia)
                    const string sql = "SELECT Id, Kod FROM [HANDEL].[HM].[TW] WHERE katalog IN ('67095','67153') ORDER BY Kod";
                    await using var cmd = new SqlCommand(sql, cn);
                    await using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        int id = rd.GetInt32(0);
                        lista.Add(new TowarRow
                        {
                            Id = id,
                            Kod = rd.IsDBNull(1) ? "" : rd.GetString(1),
                            LiczbaWariantow = _mapa.TryGetValue(id, out var wl) ? wl.Count : 0
                        });
                    }
                }
                _wszystkie = lista;
                Render("");
                txtStatus.Text = $"{_wszystkie.Count} towarów · {_mapa.Count} z wariantami";
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Błąd ładowania";
                MessageBox.Show($"Nie udało się załadować:\n{ex.Message}", "Warianty", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Render(string filtr)
        {
            filtr = (filtr ?? "").Trim();
            IEnumerable<TowarRow> baza = _wszystkie;
            if (filtr.Length > 0)
                baza = baza.Where(t => t.Kod.Contains(filtr, StringComparison.OrdinalIgnoreCase) || t.Id.ToString() == filtr);
            // Towary z wariantami na górze
            dgTowary.ItemsSource = baza.OrderByDescending(t => t.LiczbaWariantow).ThenBy(t => t.Kod).ToList();
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e) => Render(txtSzukaj.Text);

        private void DgTowary_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgTowary.SelectedItem is not TowarRow t) return;
            _wybranyId = t.Id;
            txtWybranyTowar.Text = $"🔀 Warianty dla: {t.Kod}  (Id {t.Id})";

            _warianty.Clear();
            if (_mapa.TryGetValue(t.Id, out var wl))
                foreach (var w in wl)
                    _warianty.Add(new WariantRow { Kod = w.Kod, Nazwa = w.Nazwa });
        }

        private async void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (!_wybranyId.HasValue)
            {
                MessageBox.Show("Najpierw wybierz towar z lewej listy.", "Warianty", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                var warianty = _warianty
                    .Where(w => !string.IsNullOrWhiteSpace(w.Kod) || !string.IsNullOrWhiteSpace(w.Nazwa))
                    .Select(w => new TowarWariantyService.Wariant
                    {
                        KodTowaru = _wybranyId.Value,
                        Kod = (w.Kod ?? "").Trim().ToUpperInvariant().Replace(" ", "_"),
                        Nazwa = (w.Nazwa ?? "").Trim()
                    })
                    .Where(w => w.Kod.Length > 0 && w.Nazwa.Length > 0)
                    .ToList();

                await _svc.SetWariantyAsync(_wybranyId.Value, warianty);
                _mapa = await _svc.GetMapaAsync();

                // Odśwież licznik na liście
                var row = _wszystkie.FirstOrDefault(x => x.Id == _wybranyId.Value);
                if (row != null) row.LiczbaWariantow = warianty.Count;
                Render(txtSzukaj.Text);

                txtStatus.Text = $"Zapisano {warianty.Count} wariantów dla towaru {_wybranyId.Value}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu:\n{ex.Message}", "Warianty", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e) => Close();

        public class TowarRow
        {
            public int Id { get; set; }
            public string Kod { get; set; } = "";
            public int LiczbaWariantow { get; set; }
        }

        public class WariantRow
        {
            public string Kod { get; set; } = "";
            public string Nazwa { get; set; } = "";
        }
    }
}
