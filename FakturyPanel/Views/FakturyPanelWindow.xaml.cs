using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kalendarz1.FakturyPanel.ViewModels;
using Kalendarz1.FakturyPanel.Models;
using Kalendarz1.FakturyPanel.Services;
using Kalendarz1.WPF;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.FakturyPanel.Views
{
    /// <summary>
    /// Panel dla fakturzystek - widok zamówień handlowców z historią zmian
    /// </summary>
    public partial class FakturyPanelWindow : Window
    {
        private readonly FakturyPanelViewModel _viewModel;
        private System.Windows.Threading.DispatcherTimer _searchTimer;
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private readonly string _connTransport = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public FakturyPanelWindow()
        {
            InitializeComponent();

            _viewModel = new FakturyPanelViewModel();
            DataContext = _viewModel;

            // Timer do opóźnionego wyszukiwania
            _searchTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _searchTimer.Tick += SearchTimer_Tick;

            Loaded += FakturyPanelWindow_Loaded;
        }

        private async void FakturyPanelWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await _viewModel.InitializeAsync();

                // Ustaw domyślny handlowiec
                if (cbHandlowiec.Items.Count > 0)
                    cbHandlowiec.SelectedIndex = 0;

                // Ustaw focus na pole wyszukiwania
                txtSearch.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd inicjalizacji: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Nawigacja tygodnia

        private void BtnPreviousWeek_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.PoprzedniTydzienCommand.Execute(null);
        }

        private void BtnNextWeek_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.NastepnyTydzienCommand.Execute(null);
        }

        private void BtnToday_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.DzisCommand.Execute(null);
        }

        #endregion

        #region Filtry

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Restart timera przy każdej zmianie tekstu
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private async void SearchTimer_Tick(object sender, EventArgs e)
        {
            _searchTimer.Stop();
            await _viewModel.SzukajAsync(txtSearch.Text);
        }

        private async void CbHandlowiec_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbHandlowiec.SelectedItem != null)
            {
                await _viewModel.FiltrujPoHandlowcuAsync(cbHandlowiec.SelectedItem.ToString());
            }
        }

        private void ChkShowCanceled_Changed(object sender, RoutedEventArgs e)
        {
            _viewModel.OdswiezCommand.Execute(null);
        }

        private void ChkOnlyNotInvoiced_Changed(object sender, RoutedEventArgs e)
        {
            _viewModel.OdswiezCommand.Execute(null);
        }

        private void BtnClearFilters_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = "";
            _viewModel.WyczyscFiltryCommand.Execute(null);
        }

        #endregion

        #region Przyciski akcji

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.OdswiezCommand.Execute(null);
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ExportToExcel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd eksportu: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToExcel()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Plik Excel (*.xlsx)|*.xlsx",
                FileName = $"Zamowienia_Faktury_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                DefaultExt = ".xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // Prosty eksport do CSV (można rozszerzyć o pełny Excel)
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("ID;Odbiorca;Handlowiec;Data odbioru;Godzina;Transport;kg;Pojemniki;Palety;Notatka;Status");

                    foreach (var z in _viewModel.Zamowienia)
                    {
                        sb.AppendLine($"{z.Id};\"{z.Odbiorca}\";\"{z.Handlowiec}\";{z.DataOdbioru:yyyy-MM-dd};{z.GodzinaOdbioru};\"{z.TransportTekst}\";{z.SumaKg};{z.SumaPojemnikow};{z.SumaPalet};\"{z.Notatka?.Replace("\n", " ")}\";\"{z.StatusWyswietlany}\"");
                    }

                    var csvPath = dialog.FileName.Replace(".xlsx", ".csv");
                    System.IO.File.WriteAllText(csvPath, sb.ToString(), System.Text.Encoding.UTF8);

                    MessageBox.Show($"Wyeksportowano {_viewModel.Zamowienia.Count} zamówień do:\n{csvPath}",
                        "Eksport zakończony", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas eksportu: {ex.Message}", "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region DataGrid

        private void DgOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Obsługiwane przez binding
        }

        #endregion

        #region Skróty klawiszowe

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.F5)
            {
                _viewModel.OdswiezCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (!string.IsNullOrEmpty(txtSearch.Text))
                {
                    txtSearch.Text = "";
                    e.Handled = true;
                }
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.F:
                        txtSearch.Focus();
                        txtSearch.SelectAll();
                        e.Handled = true;
                        break;
                    case Key.Left:
                        _viewModel.PoprzedniTydzienCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.Right:
                        _viewModel.NastepnyTydzienCommand.Execute(null);
                        e.Handled = true;
                        break;
                }
            }
        }

        #endregion

        #region Menu kontekstowe - DataGrid

        private void DgOrders_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Podwójne kliknięcie otwiera okno modyfikacji
            if (_viewModel.WybraneZamowienie != null)
            {
                MenuModyfikuj_Click(sender, null);
            }
        }

        private void DgOrders_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Aktualizuj widoczność opcji menu w zależności od stanu zamówienia
            if (_viewModel.WybraneZamowienie != null)
            {
                var zamowienie = _viewModel.WybraneZamowienie;
                menuAnuluj.Visibility = zamowienie.JestAnulowane ? Visibility.Collapsed : Visibility.Visible;
                menuPrzywroc.Visibility = zamowienie.JestAnulowane ? Visibility.Visible : Visibility.Collapsed;
                menuUsun.Visibility = zamowienie.JestAnulowane ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void MenuDuplikuj_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.WybraneZamowienie == null) return;

            var id = _viewModel.WybraneZamowienie.Id;
            if (id <= 0)
            {
                MessageBox.Show("Nie można zduplikować tego zamówienia.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var dlg = new MultipleDatePickerWindow("Wybierz dni dla duplikatu zamówienia");
                if (dlg.ShowDialog() == true && dlg.SelectedDates.Count > 0)
                {
                    _ = DuplikujZamowienieAsync(id, dlg.SelectedDates);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DuplikujZamowienieAsync(int sourceId, System.Collections.Generic.List<DateTime> dates)
        {
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                foreach (var date in dates)
                {
                    // Duplikuj zamówienie
                    var sql = @"
                        INSERT INTO ZamowieniaMieso (OdbiorcaId, DataOdbioru, DataProdukcji, Godzina, Notatka,
                            Handlowiec, WlasnyOdbior, DataDodania, DodanyPrzez, Status)
                        SELECT OdbiorcaId, @NowaData, DATEADD(day, -1, @NowaData), Godzina, Notatka,
                            Handlowiec, WlasnyOdbior, GETDATE(), @Uzytkownik, 'Nowe'
                        FROM ZamowieniaMieso WHERE ID = @SourceId;
                        SELECT SCOPE_IDENTITY();";

                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@SourceId", sourceId);
                    cmd.Parameters.AddWithValue("@NowaData", date);
                    cmd.Parameters.AddWithValue("@Uzytkownik", App.UserID ?? "System");

                    var newId = await cmd.ExecuteScalarAsync();
                    if (newId != null && newId != DBNull.Value)
                    {
                        // Kopiuj pozycje zamówienia
                        var sqlPoz = @"
                            INSERT INTO ZamowieniaMiesoPozycje (ZamowienieId, ProduktId, Ilosc, Cena, Palety, Pojemniki)
                            SELECT @NewId, ProduktId, Ilosc, Cena, Palety, Pojemniki
                            FROM ZamowieniaMiesoPozycje WHERE ZamowienieId = @SourceId";

                        await using var cmdPoz = new SqlCommand(sqlPoz, cn);
                        cmdPoz.Parameters.AddWithValue("@NewId", Convert.ToInt32(newId));
                        cmdPoz.Parameters.AddWithValue("@SourceId", sourceId);
                        await cmdPoz.ExecuteNonQueryAsync();
                    }
                }

                MessageBox.Show($"Utworzono {dates.Count} duplikat(ów) zamówienia.", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                _viewModel.OdswiezCommand.Execute(null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas duplikowania zamówienia: {ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuCykliczne_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.WybraneZamowienie == null) return;

            MessageBox.Show("Funkcja zamówień cyklicznych jest w trakcie rozwoju.",
                "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuModyfikuj_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.WybraneZamowienie == null) return;

            var id = _viewModel.WybraneZamowienie.Id;
            if (id <= 0)
            {
                MessageBox.Show("Nie można modyfikować tego zamówienia.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var widokZamowienia = new Kalendarz1.WidokZamowienia(App.UserID, id);
                if (widokZamowienia.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _viewModel.OdswiezCommand.Execute(null);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas otwierania zamówienia: {ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuNotatka_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.WybraneZamowienie == null) return;

            // Włącz tryb edycji notatki
            _viewModel.EdytujNotatkeCommand.Execute(null);
        }

        private void MenuSzczegolyZamowienia_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.WybraneZamowienie == null) return;

            var z = _viewModel.WybraneZamowienie;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"📦 ZAMÓWIENIE #{z.Id}");
            sb.AppendLine($"═══════════════════════════════════════");
            sb.AppendLine($"📋 Odbiorca: {z.Odbiorca}");
            sb.AppendLine($"👤 Handlowiec: {z.Handlowiec}");
            sb.AppendLine($"📅 Data odbioru: {z.DataOdbioru:dd.MM.yyyy} {z.GodzinaOdbioru}");
            sb.AppendLine($"🏭 Data produkcji: {z.DataProdukcji:dd.MM.yyyy}");
            sb.AppendLine($"🚚 Transport: {z.TransportTekst}");
            sb.AppendLine();
            sb.AppendLine($"⚖️ Ilość: {z.SumaKg:N0} kg");
            sb.AppendLine($"📦 Pojemniki: {z.SumaPojemnikow}");
            sb.AppendLine($"🎨 Palety: {z.SumaPalet}");
            sb.AppendLine($"💰 Wartość: {z.WartoscTekst}");
            sb.AppendLine();
            sb.AppendLine($"📌 Status: {z.StatusWyswietlany}");
            sb.AppendLine($"📝 Notatka: {z.Notatka ?? "(brak)"}");

            MessageBox.Show(sb.ToString(), "Szczegóły zamówienia",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuSzczegolyPlatnosci_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.WybraneZamowienie == null) return;

            var z = _viewModel.WybraneZamowienie;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"💳 PŁATNOŚCI - {z.Odbiorca}");
            sb.AppendLine($"═══════════════════════════════════════");
            sb.AppendLine($"NIP: {z.NIPWyswietlany}");
            sb.AppendLine($"Limit kredytowy: {z.LimitInfo}");
            sb.AppendLine($"Dni płatności: {z.DniPlatnosciTekst}");
            sb.AppendLine();
            sb.AppendLine($"📞 Telefon: {z.TelefonWyswietlany}");
            sb.AppendLine($"📧 Email: {z.EmailWyswietlany}");
            sb.AppendLine($"📍 Adres: {z.PelnyAdres}");

            MessageBox.Show(sb.ToString(), "Płatności kontrahenta",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void MenuHistoriaZamowien_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.WybraneZamowienie == null) return;

            try
            {
                var odbiorcaId = _viewModel.WybraneZamowienie.OdbiorcaId;
                var odbiorca = _viewModel.WybraneZamowienie.Odbiorca;

                if (odbiorcaId <= 0)
                {
                    MessageBox.Show("Brak informacji o kliencie.", "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var historia = new System.Text.StringBuilder();
                historia.AppendLine($"📋 HISTORIA ZAMÓWIEŃ - {odbiorca}");
                historia.AppendLine(new string('━', 60));
                historia.AppendLine();

                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();

                    string sql = @"
                        SELECT TOP 20
                            zm.Id,
                            zm.DataOdbioru,
                            zm.Status,
                            zm.Anulowane,
                            SUM(ISNULL(zmp.Ilosc, 0)) as IloscCalkowita
                        FROM ZamowieniaMieso zm
                        LEFT JOIN ZamowieniaMiesoPozycje zmp ON zm.Id = zmp.ZamowienieId
                        WHERE zm.OdbiorcaId = @ClientId
                            AND zm.DataOdbioru >= DATEADD(MONTH, -6, GETDATE())
                        GROUP BY zm.Id, zm.DataOdbioru, zm.Status, zm.Anulowane
                        ORDER BY zm.DataOdbioru DESC";

                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@ClientId", odbiorcaId);

                    await using var reader = await cmd.ExecuteReaderAsync();

                    decimal sumaKg = 0;
                    int liczbaZamowien = 0;

                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        DateTime data = reader.GetDateTime(1);
                        string statusZam = reader.IsDBNull(2) ? "Brak" : reader.GetString(2);
                        bool anulowane = !reader.IsDBNull(3) && reader.GetBoolean(3);
                        decimal ilosc = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4);

                        string statusDisplay = anulowane ? "❌ Anulowane" : statusZam;
                        historia.AppendLine($"#{id} | {data:dd.MM.yyyy} | {statusDisplay,-14} | {ilosc,7:N0} kg");

                        if (!anulowane)
                        {
                            sumaKg += ilosc;
                            liczbaZamowien++;
                        }
                    }

                    historia.AppendLine();
                    historia.AppendLine(new string('━', 60));
                    historia.AppendLine($"Razem (ostatnie 6 m-cy): {liczbaZamowien} zamówień | {sumaKg:N0} kg");

                    if (liczbaZamowien > 0)
                    {
                        decimal srednia = sumaKg / liczbaZamowien;
                        historia.AppendLine($"Średnia na zamówienie: {srednia:N0} kg");
                    }
                }

                MessageBox.Show(historia.ToString(), "Historia zamówień",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MenuTransportInfo_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.WybraneZamowienie == null) return;

            var id = _viewModel.WybraneZamowienie.Id;

            try
            {
                // Pobierz TransportKursID z zamówienia
                int? transportKursId = null;
                await using (var cn = new SqlConnection(_connHandel))
                {
                    await cn.OpenAsync();
                    var sql = "SELECT TransportKursID FROM dbo.ZamowieniaMieso WHERE Id = @Id";
                    using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Id", id);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        transportKursId = Convert.ToInt32(result);
                    }
                }

                if (!transportKursId.HasValue)
                {
                    MessageBox.Show("To zamówienie nie jest przypisane do żadnego transportu.",
                        "Brak transportu", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                MessageBox.Show($"Zamówienie jest przypisane do kursu transportowego #{transportKursId.Value}.\n\n" +
                    "Aby zobaczyć szczegóły, użyj modułu Transport.",
                    "Informacje o transporcie", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas pobierania informacji o transporcie: {ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuOdswiez_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.OdswiezCommand.Execute(null);
        }

        private async void MenuAnuluj_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.WybraneZamowienie == null) return;

            var z = _viewModel.WybraneZamowienie;
            if (z.JestAnulowane)
            {
                MessageBox.Show("To zamówienie jest już anulowane.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Czy na pewno chcesz anulować to zamówienie?\n\n" +
                $"📦 Odbiorca: {z.Odbiorca}\n" +
                $"⚖️ Ilość: {z.SumaKg:N0} kg\n\n" +
                $"⚠️ Zamówienie można później przywrócić.",
                "Potwierdź anulowanie",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await using var cn = new SqlConnection(_connHandel);
                    await cn.OpenAsync();
                    await using var cmd = new SqlCommand(
                        "UPDATE dbo.ZamowieniaMieso SET Anulowane = 1 WHERE Id = @Id", cn);
                    cmd.Parameters.AddWithValue("@Id", z.Id);
                    await cmd.ExecuteNonQueryAsync();

                    // Logowanie historii zmian
                    await HistoriaZmianService.LogujAnulowanie(z.Id, App.UserID, App.UserFullName,
                        $"Anulowano zamówienie dla odbiorcy: {z.Odbiorca}, ilość: {z.SumaKg:N0} kg");

                    MessageBox.Show("Zamówienie zostało anulowane.", "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    _viewModel.OdswiezCommand.Execute(null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Wystąpił błąd podczas anulowania zamówienia: {ex.Message}",
                        "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void MenuPrzywroc_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.WybraneZamowienie == null) return;

            var z = _viewModel.WybraneZamowienie;
            if (!z.JestAnulowane)
            {
                MessageBox.Show("To zamówienie nie jest anulowane.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Czy na pewno chcesz przywrócić to zamówienie?\n\n" +
                $"📦 Odbiorca: {z.Odbiorca}\n" +
                $"⚖️ Ilość: {z.SumaKg:N0} kg\n\n" +
                $"✅ Zamówienie zostanie ponownie aktywowane.",
                "Potwierdź przywrócenie",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await using var cn = new SqlConnection(_connHandel);
                    await cn.OpenAsync();
                    await using var cmd = new SqlCommand(
                        "UPDATE dbo.ZamowieniaMieso SET Anulowane = 0 WHERE Id = @Id", cn);
                    cmd.Parameters.AddWithValue("@Id", z.Id);
                    await cmd.ExecuteNonQueryAsync();

                    // Logowanie historii zmian
                    await HistoriaZmianService.LogujPrzywrocenie(z.Id, App.UserID, App.UserFullName);

                    MessageBox.Show("Zamówienie zostało przywrócone.", "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    _viewModel.OdswiezCommand.Execute(null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Wystąpił błąd podczas przywracania zamówienia: {ex.Message}",
                        "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void MenuUsun_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.WybraneZamowienie == null) return;

            var z = _viewModel.WybraneZamowienie;

            var result = MessageBox.Show(
                $"⚠️ UWAGA! Usunięcie jest NIEODWRACALNE!\n\n" +
                $"Czy na pewno chcesz TRWALE usunąć to zamówienie?\n\n" +
                $"📦 Odbiorca: {z.Odbiorca}\n" +
                $"⚖️ Ilość: {z.SumaKg:N0} kg\n\n" +
                $"🗑️ Ta operacja nie może zostać cofnięta!",
                "POTWIERDŹ USUNIĘCIE",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);

            if (result == MessageBoxResult.Yes)
            {
                // Drugie potwierdzenie
                var result2 = MessageBox.Show(
                    "Czy jesteś absolutnie pewien?\n\nDane zostaną trwale usunięte.",
                    "OSTATNIE OSTRZEŻENIE",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result2 == MessageBoxResult.Yes)
                {
                    try
                    {
                        await using var cn = new SqlConnection(_connHandel);
                        await cn.OpenAsync();

                        // Najpierw usuń pozycje
                        await using var cmdPoz = new SqlCommand(
                            "DELETE FROM ZamowieniaMiesoPozycje WHERE ZamowienieId = @Id", cn);
                        cmdPoz.Parameters.AddWithValue("@Id", z.Id);
                        await cmdPoz.ExecuteNonQueryAsync();

                        // Potem usuń zamówienie
                        await using var cmd = new SqlCommand(
                            "DELETE FROM dbo.ZamowieniaMieso WHERE Id = @Id", cn);
                        cmd.Parameters.AddWithValue("@Id", z.Id);
                        await cmd.ExecuteNonQueryAsync();

                        // Logowanie historii zmian
                        await HistoriaZmianService.LogujUsuniecie(z.Id, App.UserID, App.UserFullName);

                        MessageBox.Show("Zamówienie zostało trwale usunięte.", "Sukces",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        _viewModel.OdswiezCommand.Execute(null);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Wystąpił błąd podczas usuwania zamówienia: {ex.Message}",
                            "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        #endregion
    }
}
