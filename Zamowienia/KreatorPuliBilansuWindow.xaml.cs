using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.Zamowienia.Services;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    /// <summary>
    /// Kreator "puli bilansu" — mapuje towary-składniki (np. Noga, Pałka, Udziec)
    /// na towar-rodzic (np. Ćwiartka). Zapis do LibraNet.dbo.BilansSkladniki przez
    /// BilansSkladnikiService. Wynik widoczny w "Podsumowaniu dnia" (Zamówienia Klientów).
    /// </summary>
    public partial class KreatorPuliBilansuWindow : Window
    {
        private readonly string _connLibra;
        private readonly string _connHandel;
        private readonly string _userId;
        private readonly BilansSkladnikiService _service;

        private readonly ObservableCollection<ProduktModel> _wszystkieProdukty = new();
        private readonly ObservableCollection<ProduktModel> _widoczneProdukty = new();
        private readonly ObservableCollection<SkladnikModel> _skladniki = new();
        private List<PulaModel> _pule = new();

        private int _selectedParentId;
        private string _selectedParentNazwa = "";

        public KreatorPuliBilansuWindow(string connLibra, string connHandel, string userId)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            _connLibra = connLibra;
            _connHandel = connHandel;
            _userId = userId ?? "";
            _service = new BilansSkladnikiService(connLibra);

            lbProdukty.ItemsSource = _widoczneProdukty;
            icSkladniki.ItemsSource = _skladniki;
            dgPule.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.VisibleWhenSelected;

            Loaded += async (_, _) =>
            {
                await WczytajProduktyAsync();
                await WczytajPuleAsync();
            };
        }

        // ---- Wczytywanie danych -------------------------------------------------

        private async System.Threading.Tasks.Task WczytajProduktyAsync()
        {
            try
            {
                _wszystkieProdukty.Clear();
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                const string sql = @"SELECT id, kod, nazwa FROM HM.TW WHERE aktywny = 1 ORDER BY nazwa";
                await using var cmd = new SqlCommand(sql, cn);
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    _wszystkieProdukty.Add(new ProduktModel
                    {
                        ID = rdr.GetInt32(0),
                        Kod = rdr.IsDBNull(1) ? "" : rdr.GetString(1),
                        Nazwa = rdr.IsDBNull(2) ? "" : rdr.GetString(2)
                    });
                }
                FiltrujProdukty();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania towarów z Symfonii:\n\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task WczytajPuleAsync()
        {
            try
            {
                _pule = await _service.GetWszystkieAsync();
                dgPule.ItemsSource = null;
                dgPule.ItemsSource = _pule;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania zapisanych pul:\n\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---- Wyszukiwarka -------------------------------------------------------

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e) => FiltrujProdukty();

        private void FiltrujProdukty()
        {
            string szukaj = (txtSzukaj.Text ?? "").Trim().ToLowerInvariant();
            _widoczneProdukty.Clear();

            IEnumerable<ProduktModel> zrodlo = _wszystkieProdukty;
            if (!string.IsNullOrWhiteSpace(szukaj))
            {
                zrodlo = _wszystkieProdukty.Where(p =>
                    (p.Nazwa ?? "").ToLowerInvariant().Contains(szukaj) ||
                    (p.Kod ?? "").ToLowerInvariant().Contains(szukaj));
            }

            // Ogranicz do 300 wyników żeby ListBox był responsywny
            foreach (var p in zrodlo.Take(300))
                _widoczneProdukty.Add(p);

            int total = string.IsNullOrWhiteSpace(szukaj)
                ? _wszystkieProdukty.Count
                : _wszystkieProdukty.Count(p => (p.Nazwa ?? "").ToLowerInvariant().Contains(szukaj) ||
                                                (p.Kod ?? "").ToLowerInvariant().Contains(szukaj));
            txtLicznikProduktow.Text = total > _widoczneProdukty.Count
                ? $"Pokazano {_widoczneProdukty.Count} z {total} — zawęź wyszukiwanie."
                : $"Pokazano {_widoczneProdukty.Count} towarów.";
        }

        // ---- Akcje na towarach --------------------------------------------------

        private void BtnUstawRodzica_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not ProduktModel p) return;

            // Zmiana rodzica przy niezapisanych składnikach — ostrzeż
            if (_selectedParentId != 0 && _selectedParentId != p.ID && _skladniki.Count > 0)
            {
                var r = MessageBox.Show(
                    "Zmieniasz towar nadrzędny. Niezapisane składniki bieżącej puli zostaną wyczyszczone.\n\nKontynuować?",
                    "Zmiana rodzica", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r == MessageBoxResult.No) return;
                _skladniki.Clear();
            }

            _selectedParentId = p.ID;
            _selectedParentNazwa = p.Nazwa;
            txtWybranyRodzic.Text = $"{p.Nazwa}  ({p.Kod}, id {p.ID})";
            txtStatus.Text = $"Rodzic ustawiony: {p.Nazwa}. Dodaj składniki i zapisz.";
        }

        private void BtnDodajSkladnik_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not ProduktModel p) return;

            if (_selectedParentId == 0)
            {
                MessageBox.Show("Najpierw ustaw towar nadrzędny (przycisk ➕ rodzic).",
                    "Brak rodzica", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (p.ID == _selectedParentId)
            {
                MessageBox.Show("Towar nadrzędny nie może być własnym składnikiem.",
                    "Nieprawidłowy składnik", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (_skladniki.Any(s => s.TowarID == p.ID))
            {
                MessageBox.Show("Ten składnik jest już na liście puli.",
                    "Duplikat", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _skladniki.Add(new SkladnikModel { TowarID = p.ID, Kod = p.Kod, Nazwa = p.Nazwa });
            txtStatus.Text = $"Dodano składnik: {p.Nazwa}.";
        }

        private void BtnUsunSkladnik_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is SkladnikModel s)
                _skladniki.Remove(s);
        }

        // ---- Zapisane pule ------------------------------------------------------

        private void DgPule_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => ZaladujPuleDoEdytora();

        private void BtnEdytujPule_Click(object sender, RoutedEventArgs e)
            => ZaladujPuleDoEdytora();

        private void ZaladujPuleDoEdytora()
        {
            if (dgPule.SelectedItem is not PulaModel pula) return;

            _selectedParentId = pula.ParentTowarId;
            _selectedParentNazwa = pula.ParentNazwa;
            txtWybranyRodzic.Text = $"{pula.ParentNazwa}  (id {pula.ParentTowarId})";

            _skladniki.Clear();
            foreach (var s in pula.Skladniki)
            {
                // Uzupełnij kod z katalogu jeśli mamy
                var info = _wszystkieProdukty.FirstOrDefault(p => p.ID == s.TowarID);
                _skladniki.Add(new SkladnikModel
                {
                    TowarID = s.TowarID,
                    Nazwa = string.IsNullOrEmpty(s.Nazwa) ? (info?.Nazwa ?? "") : s.Nazwa,
                    Kod = info?.Kod ?? ""
                });
            }
            txtStatus.Text = $"Wczytano pulę: {pula.ParentNazwa} ({pula.Skladniki.Count} składn.).";
        }

        private async void BtnUsunPule_Click(object sender, RoutedEventArgs e)
        {
            if (dgPule.SelectedItem is not PulaModel pula)
            {
                MessageBox.Show("Wybierz pulę z listy.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var r = MessageBox.Show(
                $"Usunąć całą pulę dla „{pula.ParentNazwa}”?\n\nLiczba składników: {pula.LiczbaSkladnikow}",
                "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;

            try
            {
                await _service.DeleteParentAsync(pula.ParentTowarId);
                await WczytajPuleAsync();
                if (_selectedParentId == pula.ParentTowarId)
                    WyczyscEdytor();
                txtStatus.Text = $"Usunięto pulę: {pula.ParentNazwa}.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd usuwania:\n\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---- Zapis / nowy / zamknięcie -----------------------------------------

        private async void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedParentId == 0)
            {
                MessageBox.Show("Ustaw towar nadrzędny (przycisk ➕ rodzic).",
                    "Brak rodzica", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_skladniki.Count == 0)
            {
                MessageBox.Show("Dodaj przynajmniej jeden składnik puli.",
                    "Brak składników", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await _service.SaveParentAsync(_selectedParentId, _selectedParentNazwa, _skladniki.ToList(), _userId);
                await WczytajPuleAsync();
                txtStatus.Text = $"✓ Zapisano pulę: {_selectedParentNazwa} ({_skladniki.Count} składn.).";
                MessageBox.Show(
                    $"✓ Pula zapisana.\n\nRodzic: {_selectedParentNazwa}\nSkładniki: {_skladniki.Count}\n\n" +
                    "Zmiany będą widoczne w „Podsumowaniu dnia” po odświeżeniu.",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu:\n\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnNowa_Click(object sender, RoutedEventArgs e)
        {
            if (_skladniki.Count > 0 || _selectedParentId != 0)
            {
                var r = MessageBox.Show("Wyczyścić edytor (niezapisane zmiany przepadną)?",
                    "Nowa pula", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r == MessageBoxResult.No) return;
            }
            WyczyscEdytor();
        }

        private void WyczyscEdytor()
        {
            _selectedParentId = 0;
            _selectedParentNazwa = "";
            _skladniki.Clear();
            txtWybranyRodzic.Text = "— nie wybrano — wyszukaj po prawej i kliknij ➕";
            dgPule.SelectedItem = null;
            txtStatus.Text = "";
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnWarianty_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var okno = new Kalendarz1.Zamowienia.WariantyTowarowWindow { Owner = this };
                okno.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się otworzyć wariantów:\n{ex.Message}",
                    "Warianty towarów", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
