using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Printing;

namespace Kalendarz1.KontrolaGodzin
{
    public partial class KartaRCPWindow : Window
    {
        private List<RejestracjaModel> _dane;
        private List<PracownikModel> _pracownicy;
        private readonly string _connectionString = @"Server=192.168.0.23\SQLEXPRESS;Database=UNISYSTEM;User Id=sa;Password=UniRCPAdmin123$;";

        public KartaRCPWindow(List<RejestracjaModel> dane, List<PracownikModel> pracownicy)
        {
            InitializeComponent();
            _dane = dane;
            _pracownicy = pracownicy ?? new List<PracownikModel>();

            // Jeśli lista pracowników jest pusta, spróbuj pobrać z danych lub z bazy
            if (!_pracownicy.Any(p => p.Id > 0))
            {
                // Najpierw spróbuj wyciągnąć z rejestracji
                var zRejestracji = _dane
                    .GroupBy(r => r.PracownikId)
                    .Select(g => new PracownikModel
                    {
                        Id = g.Key,
                        Nazwisko = g.First().Pracownik?.Split(' ').FirstOrDefault() ?? "",
                        Imie = g.First().Pracownik?.Split(' ').Skip(1).FirstOrDefault() ?? "",
                        GrupaNazwa = g.First().Grupa
                    })
                    .Where(p => p.Id > 0)
                    .OrderBy(p => p.Nazwisko)
                    .ToList();

                if (zRejestracji.Any())
                {
                    _pracownicy = zRejestracji;
                }
                else
                {
                    // Pobierz z bazy danych
                    PobierzPracownikowZBazy();
                }
            }

            // Inicjalizuj combobox
            var listaPracownikow = _pracownicy.Where(p => p.Id > 0).OrderBy(p => p.PelneNazwisko).ToList();
            cmbPracownik.ItemsSource = listaPracownikow;
            cmbPracownik.DisplayMemberPath = "PelneNazwisko";
            cmbPracownik.SelectedValuePath = "Id";

            dpData.SelectedDate = DateTime.Today;
            
            // Debug info
            Title = $"🖨️ Drukuj Kartę RCP ({listaPracownikow.Count} pracowników)";
        }

        private void PobierzPracownikowZBazy()
        {
            try
            {
                _pracownicy = new List<PracownikModel>();
                
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    
                    string sql = @"
                        SELECT 
                            RCINE_EMPLOYEE_ID,
                            RCINE_EMPLOYEE_NAME,
                            RCINE_EMPLOYEE_SURNAME,
                            RCINE_EMPLOYEE_GROUP_ID,
                            RCINE_EMPLOYEE_GROUP_NAME
                        FROM V_RCINE_EMPLOYEES
                        WHERE RCINE_EMPLOYEE_TYPE = 1
                        ORDER BY RCINE_EMPLOYEE_SURNAME, RCINE_EMPLOYEE_NAME";

                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _pracownicy.Add(new PracownikModel
                            {
                                Id = reader.GetInt32(0),
                                Imie = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                Nazwisko = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                GrupaId = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                                GrupaNazwa = reader.IsDBNull(4) ? "" : reader.GetString(4)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd pobierania pracowników: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CmbPracownik_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            OdswiezKarte();
        }

        private void DpData_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            OdswiezKarte();
        }

        private void OdswiezKarte()
        {
            if (cmbPracownik.SelectedValue is not int pracownikId || !dpData.SelectedDate.HasValue)
            {
                WyczyscKarte();
                return;
            }

            var data = dpData.SelectedDate.Value;
            var pracownik = _pracownicy.FirstOrDefault(p => p.Id == pracownikId);

            if (pracownik == null)
            {
                WyczyscKarte();
                return;
            }

            // Pobierz rejestracje
            var rejestracje = _dane
                .Where(r => r.PracownikId == pracownikId && r.DataCzas.Date == data.Date)
                .OrderBy(r => r.DataCzas)
                .ToList();

            // Wypełnij dane
            txtKartaPracownik.Text = pracownik.PelneNazwisko;
            txtKartaData.Text = $"{data:dd.MM.yyyy} ({data:dddd})";
            txtKartaDzial.Text = pracownik.GrupaNazwa ?? "-";
            txtKartaNumer.Text = rejestracje.FirstOrDefault()?.NumerKarty.ToString() ?? "-";

            // Wejście/wyjście
            var wejscia = rejestracje.Where(r => r.TypInt == 1).ToList();
            var wyjscia = rejestracje.Where(r => r.TypInt == 0).ToList();

            var pierwszeWejscie = wejscia.FirstOrDefault()?.DataCzas;
            var ostatnieWyjscie = wyjscia.LastOrDefault()?.DataCzas;

            txtKartaWejscie.Text = pierwszeWejscie?.ToString("HH:mm") ?? "--:--";
            txtKartaWyjscie.Text = ostatnieWyjscie?.ToString("HH:mm") ?? "--:--";

            // Czas pracy
            TimeSpan czasPracy = TimeSpan.Zero;
            TimeSpan czasPrzerw = TimeSpan.Zero;

            if (pierwszeWejscie.HasValue && ostatnieWyjscie.HasValue && ostatnieWyjscie > pierwszeWejscie)
            {
                czasPracy = ostatnieWyjscie.Value - pierwszeWejscie.Value;

                // Przerwy
                for (int i = 0; i < wyjscia.Count; i++)
                {
                    var nastepneWejscie = wejscia.FirstOrDefault(w => w.DataCzas > wyjscia[i].DataCzas);
                    if (nastepneWejscie != null)
                    {
                        var przerwa = nastepneWejscie.DataCzas - wyjscia[i].DataCzas;
                        if (przerwa.TotalMinutes > 2 && przerwa.TotalHours < 4)
                        {
                            czasPrzerw += przerwa;
                        }
                    }
                }
            }
            else if (pierwszeWejscie.HasValue && data == DateTime.Today)
            {
                czasPracy = DateTime.Now - pierwszeWejscie.Value;
            }

            var czasEfektywny = czasPracy - czasPrzerw;

            txtKartaCzas.Text = FormatTimeSpan(czasPracy);
            txtKartaPrzerwy.Text = FormatTimeSpan(czasPrzerw);
            txtKartaEfektywny.Text = FormatTimeSpan(czasEfektywny);

            // Lista rejestracji
            var listaRej = rejestracje.Select(r => new
            {
                Godzina = r.DataCzas.ToString("HH:mm:ss"),
                Typ = r.Typ,
                TypKolor = new SolidColorBrush(r.TypInt == 1 ? 
                    (Color)ColorConverter.ConvertFromString("#276749") : 
                    (Color)ColorConverter.ConvertFromString("#C53030")),
                Punkt = r.PunktDostepu
            }).ToList();

            listRejestracje.ItemsSource = listaRej;

            // Timestamp
            txtKartaTimestamp.Text = $"Wydruk: {DateTime.Now:dd.MM.yyyy HH:mm}";
        }

        private void WyczyscKarte()
        {
            txtKartaPracownik.Text = "-";
            txtKartaData.Text = "-";
            txtKartaDzial.Text = "-";
            txtKartaNumer.Text = "-";
            txtKartaWejscie.Text = "--:--";
            txtKartaWyjscie.Text = "--:--";
            txtKartaCzas.Text = "0:00";
            txtKartaPrzerwy.Text = "0:00";
            txtKartaEfektywny.Text = "0:00";
            listRejestracje.ItemsSource = null;
            txtKartaTimestamp.Text = "";
        }

        private string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours < 0) return "0:00";
            return $"{(int)ts.TotalHours}:{ts.Minutes:D2}";
        }

        private void BtnDrukuj_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var printDialog = new PrintDialog();
                
                if (printDialog.ShowDialog() == true)
                {
                    // Przygotuj do druku
                    kartaRCP.Measure(new Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight));
                    kartaRCP.Arrange(new Rect(new Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight)));

                    // Drukuj
                    printDialog.PrintVisual(kartaRCP, $"Karta RCP - {txtKartaPracownik.Text}");

                    MessageBox.Show("Karta została wysłana do drukarki!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd drukowania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
