using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.KontrolaGodzin
{
    public partial class WnioskiUrlopWindow : Window
    {
        private readonly string _unicardConn = @"Server=192.168.0.23\SQLEXPRESS;Database=UNISYSTEM;User Id=sa;Password=UniRCPAdmin123$;TrustServerCertificate=true;";
        private readonly string _zpspConn = @"Server=192.168.0.23\SQLEXPRESS;Database=UNISYSTEM;User Id=sa;Password=UniRCPAdmin123$;TrustServerCertificate=true;";

        private List<PracownikInfo> _pracownicy = new List<PracownikInfo>();
        private List<WniosekUrlopowy> _wnioski = new List<WniosekUrlopowy>();
        private List<TypNieobecnosci> _typyNieobecnosci = new List<TypNieobecnosci>();

        private DateTime _aktualnyMiesiac;
        private int? _filtrPracownikId = null;

        public WnioskiUrlopWindow()
        {
            try
            {
                InitializeComponent();
                WindowIconHelper.SetIcon(this);

                _aktualnyMiesiac = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

                dpWnioskOd.SelectedDate = DateTime.Today;
                dpWnioskDo.SelectedDate = DateTime.Today;

                // Pokaż okno natychmiast, dane załaduj w tle
                Loaded += async (s, e) => await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd inicjalizacji WnioskiUrlopWindow:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Data Loading

        private async Task LoadDataAsync()
        {
            // Ładuj dane z bazy równolegle w tle
            var pracownicyTask = Task.Run(() => LoadPracownicyFromDb());
            var typyTask = Task.Run(() => LoadTypyNieobecnosciFromDb());
            var wniosekTask = Task.Run(() => LoadWnioskiFromDb());

            await Task.WhenAll(pracownicyTask, typyTask, wniosekTask);

            _pracownicy = pracownicyTask.Result;
            _typyNieobecnosci = typyTask.Result;
            _wnioski = wniosekTask.Result;

            // Aktualizuj UI na głównym wątku
            PopulatePracownicyComboBoxes();
            PopulateTypyComboBox();
            RefreshWnioskiList();
            RenderCalendar();
            UpdateBilans();
        }

        private void LoadData()
        {
            _pracownicy = LoadPracownicyFromDb();
            _typyNieobecnosci = LoadTypyNieobecnosciFromDb();
            _wnioski = LoadWnioskiFromDb();
            PopulatePracownicyComboBoxes();
            PopulateTypyComboBox();
            RefreshWnioskiList();
            RenderCalendar();
            UpdateBilans();
        }

        private List<PracownikInfo> LoadPracownicyFromDb()
        {
            var result = new List<PracownikInfo>();
            try
            {
                using (var conn = new SqlConnection(_unicardConn))
                {
                    conn.Open();
                    string sql = @"SELECT RCINE_EMPLOYEE_ID, RCINE_EMPLOYEE_NAME, RCINE_EMPLOYEE_SURNAME,
                                          RCINE_EMPLOYEE_GROUP_ID, RCINE_EMPLOYEE_GROUP_NAME
                                   FROM V_RCINE_EMPLOYEES
                                   WHERE RCINE_EMPLOYEE_TYPE = 1
                                   ORDER BY RCINE_EMPLOYEE_SURNAME, RCINE_EMPLOYEE_NAME";

                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(new PracownikInfo
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
                Dispatcher.Invoke(() =>
                    MessageBox.Show($"Błąd ładowania pracowników: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error));
            }
            return result;
        }

        private void PopulatePracownicyComboBoxes()
        {
            var filtrLista = new List<PracownikInfo>();
            filtrLista.Add(new PracownikInfo { Id = 0, Imie = "-- Wszyscy", Nazwisko = "pracownicy --" });
            filtrLista.AddRange(_pracownicy);

            cmbFiltrPracownik.ItemsSource = filtrLista;
            cmbFiltrPracownik.DisplayMemberPath = "PelneNazwisko";
            cmbFiltrPracownik.SelectedValuePath = "Id";
            cmbFiltrPracownik.SelectedIndex = 0;

            var wniosekLista = new List<PracownikInfo>();
            wniosekLista.Add(new PracownikInfo { Id = 0, Imie = "-- Wybierz", Nazwisko = "pracownika --" });
            wniosekLista.AddRange(_pracownicy);

            cmbWnioskPracownik.ItemsSource = wniosekLista;
            cmbWnioskPracownik.DisplayMemberPath = "PelneNazwisko";
            cmbWnioskPracownik.SelectedValuePath = "Id";
            cmbWnioskPracownik.SelectedIndex = 0;

            // Zastępca - ta sama lista
            var zastLista = new List<PracownikInfo>();
            zastLista.Add(new PracownikInfo { Id = 0, Imie = "-- Brak", Nazwisko = "zastępcy --" });
            zastLista.AddRange(_pracownicy);

            cmbWnioskZastepca.ItemsSource = zastLista;
            cmbWnioskZastepca.DisplayMemberPath = "PelneNazwisko";
            cmbWnioskZastepca.SelectedValuePath = "Id";
            cmbWnioskZastepca.SelectedIndex = 0;
        }

        private List<TypNieobecnosci> LoadTypyNieobecnosciFromDb()
        {
            var result = new List<TypNieobecnosci>();
            try
            {
                using (var conn = new SqlConnection(_zpspConn))
                {
                    conn.Open();
                    string sql = "SELECT Id, Kod, Nazwa, Platne, WymagaZatwierdzenia, LimitDniRoczny, Kolor FROM HR_TypyNieobecnosci WHERE Aktywny = 1 ORDER BY Kolejnosc";
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(new TypNieobecnosci
                            {
                                Id = reader.GetInt32(0),
                                Kod = reader.GetString(1),
                                Nazwa = reader.GetString(2),
                                Platne = reader.GetBoolean(3),
                                WymagaZatwierdzenia = reader.GetBoolean(4),
                                LimitDniRoczny = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                                Kolor = reader.GetString(6)
                            });
                        }
                    }
                }
            }
            catch
            {
                // Fallback - hardcoded types if DB not yet created
                result = new List<TypNieobecnosci>
                {
                    new TypNieobecnosci { Id = 1, Kod = "UW", Nazwa = "Urlop wypoczynkowy", Platne = true, WymagaZatwierdzenia = true, LimitDniRoczny = 26, Kolor = "#38A169" },
                    new TypNieobecnosci { Id = 2, Kod = "UZ", Nazwa = "Urlop na żądanie", Platne = true, WymagaZatwierdzenia = true, LimitDniRoczny = 4, Kolor = "#DD6B20" },
                    new TypNieobecnosci { Id = 3, Kod = "L4", Nazwa = "Zwolnienie lekarskie (L4)", Platne = true, WymagaZatwierdzenia = false, LimitDniRoczny = null, Kolor = "#E53E3E" },
                    new TypNieobecnosci { Id = 4, Kod = "OK", Nazwa = "Urlop okolicznościowy", Platne = true, WymagaZatwierdzenia = true, LimitDniRoczny = null, Kolor = "#805AD5" },
                    new TypNieobecnosci { Id = 5, Kod = "OP", Nazwa = "Opieka nad dzieckiem", Platne = true, WymagaZatwierdzenia = true, LimitDniRoczny = 2, Kolor = "#D69E2E" },
                    new TypNieobecnosci { Id = 6, Kod = "UB", Nazwa = "Urlop bezpłatny", Platne = false, WymagaZatwierdzenia = true, LimitDniRoczny = null, Kolor = "#718096" },
                    new TypNieobecnosci { Id = 7, Kod = "NN", Nazwa = "Nieobecność nieusprawiedliwiona", Platne = false, WymagaZatwierdzenia = false, LimitDniRoczny = null, Kolor = "#C53030" },
                    new TypNieobecnosci { Id = 8, Kod = "IN", Nazwa = "Inna nieobecność", Platne = true, WymagaZatwierdzenia = true, LimitDniRoczny = null, Kolor = "#4A5568" }
                };
            }
            return result;
        }

        private void PopulateTypyComboBox()
        {
            cmbWnioskTyp.ItemsSource = _typyNieobecnosci;
            cmbWnioskTyp.DisplayMemberPath = "Nazwa";
            cmbWnioskTyp.SelectedValuePath = "Id";
            if (_typyNieobecnosci.Count > 0) cmbWnioskTyp.SelectedIndex = 0;
        }

        private List<WniosekUrlopowy> LoadWnioskiFromDb()
        {
            var result = new List<WniosekUrlopowy>();
            try
            {
                using (var conn = new SqlConnection(_zpspConn))
                {
                    conn.Open();
                    string sql = @"SELECT n.Id, n.PracownikId, n.PracownikImie, n.PracownikNazwisko, n.PracownikDzial,
                                          n.TypNieobecnosciId, t.Nazwa AS TypNazwa, t.Kolor AS TypKolor,
                                          n.DataOd, n.DataDo, n.IloscDni, n.Uwagi, n.Status,
                                          n.DataZgloszenia, n.ZatwierdzilNazwa, n.DataZatwierdzenia, n.PowodOdrzucenia
                                   FROM HR_Nieobecnosci n
                                   INNER JOIN HR_TypyNieobecnosci t ON n.TypNieobecnosciId = t.Id
                                   ORDER BY n.DataZgloszenia DESC";

                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(new WniosekUrlopowy
                            {
                                Id = reader.GetInt32(0),
                                PracownikId = reader.GetInt32(1),
                                PracownikImie = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                PracownikNazwisko = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                PracownikDzial = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                TypNieobecnosciId = reader.GetInt32(5),
                                TypNazwa = reader.GetString(6),
                                TypKolorHex = reader.GetString(7),
                                DataOd = reader.GetDateTime(8),
                                DataDo = reader.GetDateTime(9),
                                IloscDni = reader.GetInt32(10),
                                Uwagi = reader.IsDBNull(11) ? "" : reader.GetString(11),
                                Status = reader.GetString(12),
                                DataZgloszenia = reader.GetDateTime(13),
                                ZatwierdzilNazwa = reader.IsDBNull(14) ? "" : reader.GetString(14),
                                DataZatwierdzenia = reader.IsDBNull(15) ? (DateTime?)null : reader.GetDateTime(15),
                                PowodOdrzucenia = reader.IsDBNull(16) ? "" : reader.GetString(16)
                            });
                        }
                    }
                }
            }
            catch
            {
                // DB not yet created - start with empty list
            }
            return result;
        }

        private void ReloadWnioski()
        {
            _wnioski = LoadWnioskiFromDb();
            RefreshWnioskiList();
        }

        private void RefreshWnioskiList()
        {
            var filtered = _wnioski.AsEnumerable();

            // Filtr pracownika
            if (_filtrPracownikId.HasValue && _filtrPracownikId.Value > 0)
                filtered = filtered.Where(w => w.PracownikId == _filtrPracownikId.Value);

            // Filtr statusu
            var statusItem = cmbFiltrStatus.SelectedItem as ComboBoxItem;
            string statusFiltr = statusItem?.Content?.ToString() ?? "Wszystkie";
            if (statusFiltr == "Oczekujące")
                filtered = filtered.Where(w => w.Status == "Oczekuje");
            else if (statusFiltr == "Zatwierdzone")
                filtered = filtered.Where(w => w.Status == "Zatwierdzona");
            else if (statusFiltr == "Odrzucone")
                filtered = filtered.Where(w => w.Status == "Odrzucona");

            listWnioski.ItemsSource = filtered.ToList();
        }

        #endregion

        #region Calendar Rendering

        private void RenderCalendar()
        {
            calendarGrid.Children.Clear();

            var rok = _aktualnyMiesiac.Year;
            var miesiac = _aktualnyMiesiac.Month;
            var dniWMiesiacu = DateTime.DaysInMonth(rok, miesiac);
            var pierwszyDzien = new DateTime(rok, miesiac, 1);

            // DayOfWeek: Sunday=0, Monday=1... We want Monday=0
            int offsetDni = ((int)pierwszyDzien.DayOfWeek + 6) % 7;

            txtMiesiacRok.Text = pierwszyDzien.ToString("MMMM yyyy", new CultureInfo("pl-PL")).ToUpper();
            txtBilansRok.Text = $" — {rok}";

            // Wnioski widoczne w tym miesiącu
            var wniosekMiesiac = _wnioski.Where(w =>
                w.DataOd <= new DateTime(rok, miesiac, dniWMiesiacu) &&
                w.DataDo >= pierwszyDzien &&
                w.Status != "Odrzucona")
                .ToList();

            if (_filtrPracownikId.HasValue && _filtrPracownikId.Value > 0)
                wniosekMiesiac = wniosekMiesiac.Where(w => w.PracownikId == _filtrPracownikId.Value).ToList();

            // Puste komórki przed 1-szym
            for (int i = 0; i < offsetDni; i++)
            {
                var empty = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7FAFC")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
                    BorderThickness = new Thickness(0.5),
                    MinHeight = 80
                };
                calendarGrid.Children.Add(empty);
            }

            // Dni miesiąca
            for (int dzien = 1; dzien <= dniWMiesiacu; dzien++)
            {
                var data = new DateTime(rok, miesiac, dzien);
                var wnioskiDnia = wniosekMiesiac.Where(w => data >= w.DataOd && data <= w.DataDo).ToList();
                bool isWeekend = data.DayOfWeek == DayOfWeek.Saturday || data.DayOfWeek == DayOfWeek.Sunday;
                bool isToday = data == DateTime.Today;

                var cell = new StackPanel { Margin = new Thickness(2) };

                // Numer dnia
                var dayHeader = new TextBlock
                {
                    Text = dzien.ToString(),
                    FontSize = 13,
                    FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = new SolidColorBrush(isToday
                        ? (Color)ColorConverter.ConvertFromString("#3182CE")
                        : isWeekend
                            ? (Color)ColorConverter.ConvertFromString("#C53030")
                            : (Color)ColorConverter.ConvertFromString("#2D3748")),
                    Margin = new Thickness(4, 2, 0, 2)
                };
                cell.Children.Add(dayHeader);

                // Urlopy tego dnia (max 3 wyświetlane)
                int shown = 0;
                foreach (var w in wnioskiDnia.Take(3))
                {
                    var tag = new Border
                    {
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(4, 1, 4, 1),
                        Margin = new Thickness(2, 0, 2, 1),
                        Background = new SolidColorBrush(ParseColor(w.TypKolorHex)),
                        Opacity = w.Status == "Oczekuje" ? 0.7 : 1.0
                    };

                    string displayName = w.PracownikNazwisko.Length > 10
                        ? w.PracownikNazwisko.Substring(0, 10) + "."
                        : w.PracownikNazwisko;

                    tag.Child = new TextBlock
                    {
                        Text = displayName,
                        FontSize = 9,
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.SemiBold,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };

                    // Dashed border for pending
                    if (w.Status == "Oczekuje")
                    {
                        tag.BorderBrush = new SolidColorBrush(ParseColor(w.TypKolorHex));
                        tag.BorderThickness = new Thickness(1);
                        tag.Background = new SolidColorBrush(ParseColor(w.TypKolorHex)) { Opacity = 0.3 };
                        ((TextBlock)tag.Child).Foreground = new SolidColorBrush(ParseColor(w.TypKolorHex));
                    }

                    cell.Children.Add(tag);
                    shown++;
                }

                if (wnioskiDnia.Count > 3)
                {
                    cell.Children.Add(new TextBlock
                    {
                        Text = $"+{wnioskiDnia.Count - 3} więcej",
                        FontSize = 8,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#718096")),
                        Margin = new Thickness(4, 0, 0, 0)
                    });
                }

                // Kolizja - więcej niż 2 osoby z tego samego działu
                var grupyDzial = wnioskiDnia.GroupBy(w => w.PracownikDzial).Where(g => g.Count() >= 3).ToList();
                if (grupyDzial.Any())
                {
                    cell.Children.Add(new TextBlock
                    {
                        Text = "⚠️",
                        FontSize = 10,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(0, 0, 4, 0),
                        ToolTip = $"Kolizja: {grupyDzial.First().Count()} osób z {grupyDzial.First().Key}"
                    });
                }

                var btn = new Button { Style = (Style)FindResource("CalendarDayBtn") };

                if (isToday)
                    btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EBF8FF"));
                else if (isWeekend)
                    btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF5F5"));

                var capturedDate = data;
                btn.Click += (s, e) =>
                {
                    dpWnioskOd.SelectedDate = capturedDate;
                    dpWnioskDo.SelectedDate = capturedDate;
                };

                btn.Content = cell;
                calendarGrid.Children.Add(btn);
            }

            // Puste komórki po ostatnim dniu (do wypełnienia siatki)
            int totalCells = offsetDni + dniWMiesiacu;
            int remaining = (7 - (totalCells % 7)) % 7;
            for (int i = 0; i < remaining; i++)
            {
                var empty = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7FAFC")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
                    BorderThickness = new Thickness(0.5),
                    MinHeight = 80
                };
                calendarGrid.Children.Add(empty);
            }

            // Set proper number of rows
            int totalRows = (int)Math.Ceiling((offsetDni + dniWMiesiacu) / 7.0);
            calendarGrid.Rows = totalRows;
        }

        private Color ParseColor(string hex)
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(hex);
            }
            catch
            {
                return Colors.Gray;
            }
        }

        #endregion

        #region Bilans

        private void UpdateBilans()
        {
            int pracownikId = 0;
            if (cmbFiltrPracownik.SelectedItem is PracownikInfo p && p.Id > 0)
                pracownikId = p.Id;

            int rok = _aktualnyMiesiac.Year;

            if (pracownikId == 0)
            {
                txtBilansInfo.Text = "Bilans urlopowy";
                txtBilansPrzyslugujeVal.Text = "-";
                txtBilansWykorzystane.Text = "-";
                txtBilansZaplanowane.Text = "-";
                txtBilansPozostalo.Text = "-";
                txtBilansNaZadanie.Text = "-";
                return;
            }

            var pracownik = _pracownicy.FirstOrDefault(pr => pr.Id == pracownikId);
            if (pracownik != null)
                txtBilansInfo.Text = $"Bilans: {pracownik.PelneNazwisko}";

            // Urlop wypoczynkowy (UW) - przysługuje 26 dni
            int limitUW = 26;
            var wnUW = _wnioski.Where(w => w.PracownikId == pracownikId &&
                w.DataOd.Year == rok &&
                (w.TypNazwa.Contains("wypoczynkowy") || w.TypNazwa.Contains("żądanie"))).ToList();

            int wykorzystaneUW = wnUW.Where(w => w.Status == "Zatwierdzona" && w.DataDo < DateTime.Today).Sum(w => w.IloscDni);
            int zaplanowaneUW = wnUW.Where(w => w.Status == "Zatwierdzona" && w.DataOd >= DateTime.Today).Sum(w => w.IloscDni);
            int oczekujaceUW = wnUW.Where(w => w.Status == "Oczekuje").Sum(w => w.IloscDni);
            int pozostaloUW = limitUW - wykorzystaneUW - zaplanowaneUW;

            txtBilansPrzyslugujeVal.Text = $"{limitUW} dni";
            txtBilansWykorzystane.Text = $"{wykorzystaneUW} dni";
            txtBilansZaplanowane.Text = $"{zaplanowaneUW + oczekujaceUW} dni";
            txtBilansPozostalo.Text = $"{pozostaloUW} dni";

            // Urlop na żądanie (UZ) - limit 4 z puli 26
            int limitUZ = 4;
            int wykorzystaneUZ = _wnioski.Where(w => w.PracownikId == pracownikId &&
                w.DataOd.Year == rok &&
                w.TypNazwa.Contains("żądanie") &&
                (w.Status == "Zatwierdzona" || w.Status == "Oczekuje")).Sum(w => w.IloscDni);

            txtBilansNaZadanie.Text = $"{wykorzystaneUZ} / {limitUZ}";
        }

        #endregion

        #region Submit Leave Request

        private void BtnZlozWniosek_Click(object sender, RoutedEventArgs e)
        {
            // Walidacja
            var pracownik = cmbWnioskPracownik.SelectedItem as PracownikInfo;
            if (pracownik == null || pracownik.Id == 0)
            {
                MessageBox.Show("Wybierz pracownika.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var typ = cmbWnioskTyp.SelectedItem as TypNieobecnosci;
            if (typ == null)
            {
                MessageBox.Show("Wybierz typ nieobecności.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!dpWnioskOd.SelectedDate.HasValue || !dpWnioskDo.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz daty.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dataOd = dpWnioskOd.SelectedDate.Value;
            var dataDo = dpWnioskDo.SelectedDate.Value;

            if (dataDo < dataOd)
            {
                MessageBox.Show("Data 'do' nie może być wcześniejsza niż data 'od'.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int dniRobocze = LiczDniRobocze(dataOd, dataDo);
            if (dniRobocze == 0)
            {
                MessageBox.Show("Wybrany zakres nie zawiera dni roboczych.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Sprawdź kolizje
            var kolizje = SprawdzKolizje(pracownik.Id, dataOd, dataDo);
            if (kolizje.Any())
            {
                var msg = "Wykryto nakładające się wnioski:\n" + string.Join("\n", kolizje.Select(k =>
                    $"  - {k.TypNazwa}: {k.DataOd:dd.MM} - {k.DataDo:dd.MM.yyyy} ({k.Status})"));
                msg += "\n\nCzy mimo to chcesz złożyć wniosek?";

                if (MessageBox.Show(msg, "Kolizja dat", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;
            }

            // Sprawdź limit
            if (typ.LimitDniRoczny.HasValue)
            {
                int wykorzystane = _wnioski.Where(w =>
                    w.PracownikId == pracownik.Id &&
                    w.DataOd.Year == dataOd.Year &&
                    w.TypNieobecnosciId == typ.Id &&
                    w.Status != "Odrzucona").Sum(w => w.IloscDni);

                if (wykorzystane + dniRobocze > typ.LimitDniRoczny.Value)
                {
                    var msg = $"Przekroczenie limitu!\n\nLimit roczny: {typ.LimitDniRoczny} dni\nWykorzystane/zaplanowane: {wykorzystane} dni\nTen wniosek: {dniRobocze} dni\n\nCzy mimo to chcesz złożyć wniosek?";
                    if (MessageBox.Show(msg, "Przekroczenie limitu", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        return;
                }
            }

            // Sprawdź kolizje działowe (>2 osoby z działu na urlopie)
            var kolizjeDzialowe = SprawdzKolizjeDzialowe(pracownik.GrupaNazwa, pracownik.Id, dataOd, dataDo);
            if (kolizjeDzialowe.Count > 0)
            {
                var msg = $"W dziale {pracownik.GrupaNazwa} w tym terminie na urlopie są już:\n" +
                    string.Join("\n", kolizjeDzialowe.Select(k => $"  - {k}"));
                msg += "\n\nCzy mimo to chcesz złożyć wniosek?";
                if (MessageBox.Show(msg, "Kolizja w dziale", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
            }

            // Zapisz do bazy
            try
            {
                using (var conn = new SqlConnection(_zpspConn))
                {
                    conn.Open();
                    string sql = @"INSERT INTO HR_Nieobecnosci
                        (PracownikId, PracownikImie, PracownikNazwisko, PracownikDzial,
                         TypNieobecnosciId, DataOd, DataDo, IloscDni, Uwagi, Status, DataZgloszenia)
                        VALUES (@pid, @imie, @nazw, @dzial, @tid, @od, @do, @dni, @uwagi, @status, GETDATE())";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@pid", pracownik.Id);
                        cmd.Parameters.AddWithValue("@imie", pracownik.Imie);
                        cmd.Parameters.AddWithValue("@nazw", pracownik.Nazwisko);
                        cmd.Parameters.AddWithValue("@dzial", pracownik.GrupaNazwa);
                        cmd.Parameters.AddWithValue("@tid", typ.Id);
                        cmd.Parameters.AddWithValue("@od", dataOd);
                        cmd.Parameters.AddWithValue("@do", dataDo);
                        cmd.Parameters.AddWithValue("@dni", dniRobocze);
                        cmd.Parameters.AddWithValue("@uwagi", string.IsNullOrEmpty(txtWnioskUwagi.Text) ? (object)DBNull.Value : txtWnioskUwagi.Text);
                        cmd.Parameters.AddWithValue("@status", typ.WymagaZatwierdzenia ? "Oczekuje" : "Zatwierdzona");
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show(
                    typ.WymagaZatwierdzenia
                        ? $"Wniosek urlopowy złożony pomyślnie.\nStatus: Oczekuje na zatwierdzenie.\nDni roboczych: {dniRobocze}"
                        : $"Nieobecność zarejestrowana.\nDni roboczych: {dniRobocze}",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

                // Reset formularza
                txtWnioskUwagi.Text = "";

                // Przeładuj
                ReloadWnioski();
                RenderCalendar();
                UpdateBilans();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu do bazy: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnZatwierdzWniosek_Click(object sender, RoutedEventArgs e)
        {
            var selected = listWnioski.SelectedItem as WniosekUrlopowy;
            if (selected == null)
            {
                MessageBox.Show("Wybierz wniosek do zatwierdzenia.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (selected.Status != "Oczekuje")
            {
                MessageBox.Show("Można zatwierdzić tylko wnioski o statusie 'Oczekuje'.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var conn = new SqlConnection(_zpspConn))
                {
                    conn.Open();
                    string sql = @"UPDATE HR_Nieobecnosci SET Status = 'Zatwierdzona',
                                   ZatwierdzilNazwa = @nazwa, DataZatwierdzenia = GETDATE()
                                   WHERE Id = @id";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", selected.Id);
                        cmd.Parameters.AddWithValue("@nazwa", App.UserFullName ?? App.UserID ?? "Kierownik");
                        cmd.ExecuteNonQuery();
                    }
                }

                ReloadWnioski();
                RenderCalendar();
                UpdateBilans();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnOdrzucWniosek_Click(object sender, RoutedEventArgs e)
        {
            var selected = listWnioski.SelectedItem as WniosekUrlopowy;
            if (selected == null)
            {
                MessageBox.Show("Wybierz wniosek do odrzucenia.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (selected.Status != "Oczekuje")
            {
                MessageBox.Show("Można odrzucić tylko wnioski o statusie 'Oczekuje'.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new PowodOdrzuceniaDialog();
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using (var conn = new SqlConnection(_zpspConn))
                    {
                        conn.Open();
                        string sql = @"UPDATE HR_Nieobecnosci SET Status = 'Odrzucona',
                                       ZatwierdzilNazwa = @nazwa, DataZatwierdzenia = GETDATE(),
                                       PowodOdrzucenia = @powod
                                       WHERE Id = @id";
                        using (var cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", selected.Id);
                            cmd.Parameters.AddWithValue("@nazwa", App.UserFullName ?? App.UserID ?? "Kierownik");
                            cmd.Parameters.AddWithValue("@powod", dialog.Powod);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    ReloadWnioski();
                    RenderCalendar();
                    UpdateBilans();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnUsunWniosek_Click(object sender, RoutedEventArgs e)
        {
            var selected = listWnioski.SelectedItem as WniosekUrlopowy;
            if (selected == null)
            {
                MessageBox.Show("Wybierz wniosek do usunięcia.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Czy na pewno usunąć wniosek {selected.PracownikNazwa} ({selected.DataOd:dd.MM} - {selected.DataDo:dd.MM.yyyy})?",
                "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                using (var conn = new SqlConnection(_zpspConn))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("DELETE FROM HR_Nieobecnosci WHERE Id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", selected.Id);
                        cmd.ExecuteNonQuery();
                    }
                }

                ReloadWnioski();
                RenderCalendar();
                UpdateBilans();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Collision Detection

        private List<WniosekUrlopowy> SprawdzKolizje(int pracownikId, DateTime od, DateTime doDate)
        {
            return _wnioski.Where(w =>
                w.PracownikId == pracownikId &&
                w.Status != "Odrzucona" &&
                w.DataOd <= doDate &&
                w.DataDo >= od).ToList();
        }

        private List<string> SprawdzKolizjeDzialowe(string dzial, int excludePracownikId, DateTime od, DateTime doDate)
        {
            if (string.IsNullOrEmpty(dzial)) return new List<string>();

            return _wnioski
                .Where(w => w.PracownikDzial == dzial &&
                    w.PracownikId != excludePracownikId &&
                    w.Status != "Odrzucona" &&
                    w.DataOd <= doDate &&
                    w.DataDo >= od)
                .Select(w => $"{w.PracownikNazwisko} {w.PracownikImie} ({w.DataOd:dd.MM} - {w.DataDo:dd.MM})")
                .Distinct()
                .ToList();
        }

        private int LiczDniRobocze(DateTime od, DateTime doDate)
        {
            int count = 0;
            for (var d = od; d <= doDate; d = d.AddDays(1))
            {
                if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                    count++;
            }
            return count;
        }

        #endregion

        #region Event Handlers

        private void BtnPoprzedniMiesiac_Click(object sender, RoutedEventArgs e)
        {
            _aktualnyMiesiac = _aktualnyMiesiac.AddMonths(-1);
            RenderCalendar();
        }

        private void BtnNastepnyMiesiac_Click(object sender, RoutedEventArgs e)
        {
            _aktualnyMiesiac = _aktualnyMiesiac.AddMonths(1);
            RenderCalendar();
        }

        private void BtnDzisiaj_Click(object sender, RoutedEventArgs e)
        {
            _aktualnyMiesiac = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            RenderCalendar();
        }

        private void CmbFiltrPracownik_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            var p = cmbFiltrPracownik.SelectedItem as PracownikInfo;
            _filtrPracownikId = p?.Id > 0 ? p.Id : (int?)null;
            RenderCalendar();
            RefreshWnioskiList();
            UpdateBilans();
        }

        private void CmbFiltrStatus_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            RefreshWnioskiList();
        }

        private void CmbWnioskTyp_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Update limit info in borderDniInfo
            UpdateDniInfo();
        }

        private void DpWnioskDaty_Changed(object sender, SelectionChangedEventArgs e)
        {
            UpdateDniInfo();
        }

        private void UpdateDniInfo()
        {
            if (!IsLoaded) return;
            if (!dpWnioskOd.SelectedDate.HasValue || !dpWnioskDo.SelectedDate.HasValue) return;

            var od = dpWnioskOd.SelectedDate.Value;
            var doDate = dpWnioskDo.SelectedDate.Value;

            if (doDate < od)
            {
                txtWnioskDni.Text = "Data 'do' musi być >= data 'od'";
                txtWnioskDni.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C53030"));
                return;
            }

            int dniRobocze = LiczDniRobocze(od, doDate);
            txtWnioskDni.Text = $"Dni roboczych: {dniRobocze}";
            txtWnioskDni.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2B6CB0"));

            // Kolizja info
            var pracownik = cmbWnioskPracownik.SelectedItem as PracownikInfo;
            if (pracownik != null && pracownik.Id > 0)
            {
                var kolizje = SprawdzKolizje(pracownik.Id, od, doDate);
                if (kolizje.Any())
                {
                    txtWnioskKolizja.Text = $"Nakładające się wnioski: {kolizje.Count}";
                    borderDniInfo.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF5F5"));
                }
                else
                {
                    txtWnioskKolizja.Text = "";
                    borderDniInfo.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EBF8FF"));
                }

                // Kolizje działowe
                var dzialowe = SprawdzKolizjeDzialowe(pracownik.GrupaNazwa, pracownik.Id, od, doDate);
                if (dzialowe.Count > 0)
                {
                    txtWnioskKolizja.Text += (string.IsNullOrEmpty(txtWnioskKolizja.Text) ? "" : "\n") +
                        $"W dziale {pracownik.GrupaNazwa}: {dzialowe.Count} os. na urlopie";
                }
            }
            else
            {
                txtWnioskKolizja.Text = "";
                borderDniInfo.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EBF8FF"));
            }
        }

        private void ListWnioski_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Could highlight selected leave on calendar
        }

        #endregion

        #region Models

        public class PracownikInfo
        {
            public int Id { get; set; }
            public string Imie { get; set; }
            public string Nazwisko { get; set; }
            public int GrupaId { get; set; }
            public string GrupaNazwa { get; set; }
            public string PelneNazwisko => $"{Nazwisko} {Imie}".Trim();
        }

        public class TypNieobecnosci
        {
            public int Id { get; set; }
            public string Kod { get; set; }
            public string Nazwa { get; set; }
            public bool Platne { get; set; }
            public bool WymagaZatwierdzenia { get; set; }
            public int? LimitDniRoczny { get; set; }
            public string Kolor { get; set; }
        }

        public class WniosekUrlopowy
        {
            public int Id { get; set; }
            public int PracownikId { get; set; }
            public string PracownikImie { get; set; }
            public string PracownikNazwisko { get; set; }
            public string PracownikDzial { get; set; }
            public int TypNieobecnosciId { get; set; }
            public string TypNazwa { get; set; }
            public string TypKolorHex { get; set; }
            public DateTime DataOd { get; set; }
            public DateTime DataDo { get; set; }
            public int IloscDni { get; set; }
            public string Uwagi { get; set; }
            public string Status { get; set; }
            public DateTime DataZgloszenia { get; set; }
            public string ZatwierdzilNazwa { get; set; }
            public DateTime? DataZatwierdzenia { get; set; }
            public string PowodOdrzucenia { get; set; }

            // Display properties
            public string PracownikNazwa => $"{PracownikNazwisko} {PracownikImie}".Trim();
            public string StatusKolor => Status == "Zatwierdzona" ? "#38A169" : (Status == "Odrzucona" ? "#E53E3E" : "#DD6B20");
            public string TypKolor => TypKolorHex ?? "#718096";
            public string TloKolor => Status == "Oczekuje" ? "#FFFAF0" : (Status == "Odrzucona" ? "#FFF5F5" : "#F0FFF4");
            public string OkresDisplay => $"{DataOd:dd.MM} - {DataDo:dd.MM.yyyy} ({IloscDni} dni)";
            public Visibility UwagiVisibility => string.IsNullOrEmpty(Uwagi) ? Visibility.Collapsed : Visibility.Visible;
        }

        #endregion
    }

    #region Dialogs

    public class PowodOdrzuceniaDialog : Window
    {
        private TextBox txtPowod;
        public string Powod { get; private set; }

        public PowodOdrzuceniaDialog()
        {
            Title = "Powód odrzucenia";
            Width = 400;
            Height = 220;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F7FA"));
            ResizeMode = ResizeMode.NoResize;

            var sp = new StackPanel { Margin = new Thickness(20) };
            sp.Children.Add(new TextBlock
            {
                Text = "Podaj powód odrzucenia wniosku:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            txtPowod = new TextBox
            {
                Height = 80,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Padding = new Thickness(8, 6, 8, 6)
            };
            sp.Children.Add(txtPowod);

            var btns = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };

            var btnOk = new Button { Content = "Odrzuć wniosek", Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(0, 0, 10, 0) };
            btnOk.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E53E3E"));
            btnOk.Foreground = Brushes.White;
            btnOk.Click += (s, e) =>
            {
                Powod = txtPowod.Text;
                DialogResult = true;
                Close();
            };

            var btnAnuluj = new Button { Content = "Anuluj", Padding = new Thickness(16, 8, 16, 8) };
            btnAnuluj.Click += (s, e) => { DialogResult = false; Close(); };

            btns.Children.Add(btnOk);
            btns.Children.Add(btnAnuluj);
            sp.Children.Add(btns);

            Content = sp;
        }
    }

    #endregion
}
