using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Kalendarz1.KontrolaGodzin
{
    public partial class ZarzadzanieKartamiWindow : Window
    {
        private readonly string _connectionString = @"Server=192.168.0.23\SQLEXPRESS;Database=UNISYSTEM;User Id=sa;Password=UniRCPAdmin123$;";
        
        private List<KartaViewModel> _karty = new List<KartaViewModel>();
        private List<PracownikKartaViewModel> _pracownicyKarty = new List<PracownikKartaViewModel>();
        private List<HistoriaKartyViewModel> _historia = new List<HistoriaKartyViewModel>();

        // Stany kart z bazy UNICARD
        private const int STAN_AKTYWNA = 135270402;
        private const int STAN_NIEAKTYWNA = 135270403;
        private const int STAN_ZABLOKOWANA = 135270401;

        public ZarzadzanieKartamiWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            Loaded += (s, e) => LoadData();
        }

        private void LoadData()
        {
            try
            {
                LoadKarty();
                LoadPracownicyZKartami();
                LoadHistoria();
                UpdateStatystyki();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadKarty()
        {
            _karty.Clear();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                // Pobierz karty ze szczegółami i aktualnym przypisaniem
                string sql = @"
                    SELECT 
                        c.KDCAD_CARD_ID,
                        c.KDCAD_CARD_NUMBER,
                        c.KDCAD_CARD_STATE,
                        ec.KDINEC_EMPLOYEE_ID,
                        ec.KDINEC_EMPLOYEE_NAME,
                        ec.KDINEC_EMPLOYEE_SURNAME,
                        ec.KDINEC_DATETIME_FROM,
                        ec.KDINEC_DATETIME_TO
                    FROM V_KDCAD_CARDS_DETAILS c
                    LEFT JOIN V_KDINEC_EMPLOYEES_CARDS ec 
                        ON c.KDCAD_CARD_NUMBER = ec.KDINEC_CARD_NUMBER 
                        AND ec.KDINEC_DATETIME_TO IS NULL
                    ORDER BY c.KDCAD_CARD_NUMBER";

                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var karta = new KartaViewModel
                        {
                            KartaId = reader.GetInt32(0),
                            NumerKarty = reader.GetInt64(1),
                            Stan = reader.GetInt32(2),
                            PracownikId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3),
                            PracownikImie = reader.IsDBNull(4) ? null : reader.GetString(4),
                            PracownikNazwisko = reader.IsDBNull(5) ? null : reader.GetString(5),
                            PrzypisanaOd = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6),
                            PrzypisanaDo = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7)
                        };

                        // Określ opis stanu
                        karta.StanOpis = karta.Stan switch
                        {
                            STAN_AKTYWNA => "✅ Aktywna",
                            STAN_NIEAKTYWNA => "⛔ Nieaktywna",
                            STAN_ZABLOKOWANA => "🔒 Zablokowana",
                            _ => $"❓ {karta.Stan}"
                        };

                        // Status przypisania
                        if (karta.PracownikId.HasValue)
                        {
                            karta.Status = "👤 Przypisana";
                            karta.Uwagi = "";
                        }
                        else
                        {
                            karta.Status = "📭 Wolna";
                            karta.Uwagi = "Dostępna do przypisania";
                        }

                        _karty.Add(karta);
                    }
                }
            }
        }

        private void LoadPracownicyZKartami()
        {
            _pracownicyKarty.Clear();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                string sql = @"
                    SELECT 
                        ec.KDINEC_EMPLOYEE_ID,
                        ec.KDINEC_EMPLOYEE_NAME,
                        ec.KDINEC_EMPLOYEE_SURNAME,
                        ec.KDINEC_CARD_NUMBER,
                        ec.KDINEC_DATETIME_FROM,
                        ec.KDINEC_DATETIME_TO,
                        e.RCINE_EMPLOYEE_GROUP_NAME
                    FROM V_KDINEC_EMPLOYEES_CARDS ec
                    LEFT JOIN V_RCINE_EMPLOYEES e ON ec.KDINEC_EMPLOYEE_ID = e.RCINE_EMPLOYEE_ID
                    WHERE ec.KDINEC_DATETIME_TO IS NULL
                    ORDER BY ec.KDINEC_EMPLOYEE_SURNAME, ec.KDINEC_EMPLOYEE_NAME";

                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        _pracownicyKarty.Add(new PracownikKartaViewModel
                        {
                            PracownikId = reader.GetInt32(0),
                            Imie = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            Nazwisko = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            NumerKarty = reader.GetInt64(3),
                            PrzypisanaOd = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4),
                            Dzial = reader.IsDBNull(6) ? "" : reader.GetString(6),
                            StatusKarty = "✅ Aktywna"
                        });
                    }
                }
            }
        }

        private void LoadHistoria()
        {
            _historia.Clear();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                string sql = @"
                    SELECT 
                        KDINEC_CARD_NUMBER,
                        KDINEC_EMPLOYEE_NAME,
                        KDINEC_EMPLOYEE_SURNAME,
                        KDINEC_DATETIME_FROM,
                        KDINEC_DATETIME_TO
                    FROM V_KDINEC_EMPLOYEES_CARDS
                    ORDER BY KDINEC_DATETIME_FROM DESC";

                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var hist = new HistoriaKartyViewModel
                        {
                            NumerKarty = reader.GetInt64(0),
                            Imie = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            Nazwisko = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            DataOd = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                            DataDo = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4)
                        };

                        // Oblicz czas użytkowania
                        if (hist.DataOd.HasValue)
                        {
                            var koniec = hist.DataDo ?? DateTime.Now;
                            var czas = koniec - hist.DataOd.Value;
                            hist.CzasUzytkowania = $"{(int)czas.TotalDays} dni";
                        }

                        hist.Status = hist.DataDo.HasValue ? "📤 Zwrócona" : "✅ Aktywna";

                        _historia.Add(hist);
                    }
                }
            }
        }

        private void UpdateStatystyki()
        {
            txtWszystkieKarty.Text = _karty.Count.ToString();
            txtAktywne.Text = _karty.Count(k => k.Stan == STAN_AKTYWNA).ToString();
            txtNieaktywne.Text = _karty.Count(k => k.Stan != STAN_AKTYWNA).ToString();
            txtPrzypisane.Text = _karty.Count(k => k.PracownikId.HasValue).ToString();
            txtWolne.Text = _karty.Count(k => !k.PracownikId.HasValue && k.Stan == STAN_AKTYWNA).ToString();
        }

        private void ApplyFilter()
        {
            var widok = cmbWidok.SelectedIndex;
            var szukaj = txtSzukaj.Text?.Trim().ToLower() ?? "";

            gridKarty.Visibility = widok == 0 ? Visibility.Visible : Visibility.Collapsed;
            gridPracownicy.Visibility = widok == 1 ? Visibility.Visible : Visibility.Collapsed;
            gridHistoria.Visibility = widok == 2 ? Visibility.Visible : Visibility.Collapsed;

            if (widok == 0)
            {
                // Karty
                var filtered = _karty.AsEnumerable();

                if (rbAktywne.IsChecked == true)
                    filtered = filtered.Where(k => k.Stan == STAN_AKTYWNA);
                else if (rbPrzypisane.IsChecked == true)
                    filtered = filtered.Where(k => k.PracownikId.HasValue);
                else if (rbWolne.IsChecked == true)
                    filtered = filtered.Where(k => !k.PracownikId.HasValue && k.Stan == STAN_AKTYWNA);

                if (!string.IsNullOrEmpty(szukaj))
                {
                    filtered = filtered.Where(k =>
                        k.NumerKarty.ToString().Contains(szukaj) ||
                        (k.Pracownik?.ToLower().Contains(szukaj) ?? false));
                }

                var lista = filtered.ToList();
                gridKarty.ItemsSource = lista;
                txtInfo.Text = $"Wyświetlono {lista.Count} z {_karty.Count} kart";
            }
            else if (widok == 1)
            {
                // Pracownicy
                var filtered = _pracownicyKarty.AsEnumerable();

                if (!string.IsNullOrEmpty(szukaj))
                {
                    filtered = filtered.Where(p =>
                        p.Pracownik.ToLower().Contains(szukaj) ||
                        p.NumerKarty.ToString().Contains(szukaj) ||
                        (p.Dzial?.ToLower().Contains(szukaj) ?? false));
                }

                var lista = filtered.ToList();
                gridPracownicy.ItemsSource = lista;
                txtInfo.Text = $"Wyświetlono {lista.Count} pracowników z kartami";
            }
            else
            {
                // Historia
                var filtered = _historia.AsEnumerable();

                if (!string.IsNullOrEmpty(szukaj))
                {
                    filtered = filtered.Where(h =>
                        h.Pracownik.ToLower().Contains(szukaj) ||
                        h.NumerKarty.ToString().Contains(szukaj));
                }

                var lista = filtered.ToList();
                gridHistoria.ItemsSource = lista;
                txtInfo.Text = $"Wyświetlono {lista.Count} wpisów historii";
            }
        }

        #region Event Handlers

        private void BtnOdswiez_Click(object sender, RoutedEventArgs e) => LoadData();

        private void BtnPrzypisKarte_Click(object sender, RoutedEventArgs e)
        {
            var window = new PrzypisKartyWindow();
            window.Owner = this;
            window.ShowDialog();

            // Odśwież dane jeśli były zmiany
            if (window.ZmianyZapisane)
            {
                LoadData();
            }
        }

        private void Filtr_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) ApplyFilter();
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded) ApplyFilter();
        }

        private void CmbWidok_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) ApplyFilter();
        }

        private void GridKarty_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (gridKarty.SelectedItem is KartaViewModel karta)
            {
                // Pokaż szczegóły karty
                var sb = new StringBuilder();
                sb.AppendLine($"=== KARTA #{karta.NumerKarty} ===\n");
                sb.AppendLine($"ID: {karta.KartaId}");
                sb.AppendLine($"Numer: {karta.NumerKarty}");
                sb.AppendLine($"Stan: {karta.StanOpis}");
                sb.AppendLine($"\n--- Przypisanie ---");
                sb.AppendLine($"Pracownik: {karta.Pracownik ?? "brak"}");
                sb.AppendLine($"Od: {karta.PrzypisanaOd?.ToString("dd.MM.yyyy HH:mm") ?? "-"}");
                sb.AppendLine($"Do: {karta.PrzypisanaDo?.ToString("dd.MM.yyyy HH:mm") ?? "-"}");

                // Historia karty
                var historiaKarty = _historia.Where(h => h.NumerKarty == karta.NumerKarty).ToList();
                if (historiaKarty.Any())
                {
                    sb.AppendLine($"\n--- Historia ({historiaKarty.Count} przypisań) ---");
                    foreach (var h in historiaKarty.Take(5))
                    {
                        sb.AppendLine($"• {h.Pracownik}: {h.DataOd:dd.MM.yyyy} - {h.DataDo?.ToString("dd.MM.yyyy") ?? "teraz"}");
                    }
                }

                MessageBox.Show(sb.ToString(), $"Szczegóły karty {karta.NumerKarty}", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void GridPracownicy_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (gridPracownicy.SelectedItem is PracownikKartaViewModel prac)
            {
                // Pokaż historię kart pracownika
                var historiaOsoby = _historia.Where(h => 
                    h.Imie == prac.Imie && h.Nazwisko == prac.Nazwisko).ToList();

                var sb = new StringBuilder();
                sb.AppendLine($"=== {prac.Pracownik} ===\n");
                sb.AppendLine($"Dział: {prac.Dzial}");
                sb.AppendLine($"Aktualna karta: {prac.NumerKarty}");
                sb.AppendLine($"\n--- Historia kart ({historiaOsoby.Count}) ---");
                
                foreach (var h in historiaOsoby)
                {
                    sb.AppendLine($"• Karta {h.NumerKarty}: {h.DataOd:dd.MM.yyyy} - {h.DataDo?.ToString("dd.MM.yyyy") ?? "teraz"} ({h.CzasUzytkowania})");
                }

                MessageBox.Show(sb.ToString(), $"Historia kart - {prac.Pracownik}", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv",
                    FileName = $"karty_rcp_{DateTime.Now:yyyyMMdd}.csv"
                };

                if (dialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Numer karty;Stan;Pracownik;Przypisana od;Przypisana do;Status");

                    foreach (var k in _karty)
                    {
                        sb.AppendLine($"{k.NumerKarty};{k.StanOpis};{k.Pracownik ?? ""};{k.PrzypisanaOd?.ToString("dd.MM.yyyy HH:mm") ?? ""};{k.PrzypisanaDo?.ToString("dd.MM.yyyy HH:mm") ?? ""};{k.Status}");
                    }

                    File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show("Wyeksportowano!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd eksportu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e) => Close();

        #endregion
    }

    #region View Models

    public class KartaViewModel
    {
        public int KartaId { get; set; }
        public long NumerKarty { get; set; }
        public int Stan { get; set; }
        public string StanOpis { get; set; }
        public int? PracownikId { get; set; }
        public string PracownikImie { get; set; }
        public string PracownikNazwisko { get; set; }
        public string Pracownik => PracownikId.HasValue ? $"{PracownikNazwisko} {PracownikImie}".Trim() : null;
        public DateTime? PrzypisanaOd { get; set; }
        public DateTime? PrzypisanaDo { get; set; }
        public string Status { get; set; }
        public string Uwagi { get; set; }
    }

    public class PracownikKartaViewModel
    {
        public int PracownikId { get; set; }
        public string Imie { get; set; }
        public string Nazwisko { get; set; }
        public string Pracownik => $"{Nazwisko} {Imie}".Trim();
        public long NumerKarty { get; set; }
        public DateTime? PrzypisanaOd { get; set; }
        public string Dzial { get; set; }
        public string StatusKarty { get; set; }
        public DateTime? OstatniaRejestracja { get; set; }
    }

    public class HistoriaKartyViewModel
    {
        public long NumerKarty { get; set; }
        public string Imie { get; set; }
        public string Nazwisko { get; set; }
        public string Pracownik => $"{Nazwisko} {Imie}".Trim();
        public DateTime? DataOd { get; set; }
        public DateTime? DataDo { get; set; }
        public string CzasUzytkowania { get; set; }
        public string Status { get; set; }
    }

    #endregion
}
