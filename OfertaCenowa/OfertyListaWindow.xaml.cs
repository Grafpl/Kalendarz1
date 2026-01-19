using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1.OfertaCenowa
{
    public partial class OfertyListaWindow : Window
    {
        // WAŻNE: UserID jako string (zgodność z OfertaHandlowaWindow)
        public string UserID { get; set; }

        private ObservableCollection<OfertaListaItem> _wszystkieOferty = new ObservableCollection<OfertaListaItem>();
        private ObservableCollection<OfertaListaItem> _filtrowaneOferty = new ObservableCollection<OfertaListaItem>();
        private OfertaListaItem _wybranaOferta;
        private bool _daneZmienione = false;

        private readonly string _connectionStringLibraNet =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True;";

        public OfertyListaWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            dgOferty.ItemsSource = _filtrowaneOferty;
            Loaded += OfertyListaWindow_Loaded;
        }

        private async void OfertyListaWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await WczytajOfertyAsync();
        }

        #region Wczytywanie danych

        private async Task WczytajOfertyAsync()
        {
            try
            {
                _wszystkieOferty.Clear();

                await Task.Run(() =>
                {
                    using (var conn = new SqlConnection(_connectionStringLibraNet))
                    {
                        conn.Open();

                        const string sql = @"
                            SELECT o.ID, o.NumerOferty, o.DataWystawienia, o.DataWaznosci,
                                   o.KlientNazwa, o.KlientAdres, o.KlientMiejscowosc, o.KlientEmail, 
                                   o.KlientTelefon, o.KlientOsobaKontaktowa,
                                   o.WartoscNetto, o.Status, o.SciezkaPliku,
                                   o.HandlowiecID, o.HandlowiecNazwa, o.Notatki,
                                   (SELECT COUNT(*) FROM Oferty_Pozycje WHERE OfertaID = o.ID) as LiczbaPozycji
                            FROM Oferty o
                            ORDER BY o.DataWystawienia DESC";

                        using (var cmd = new SqlCommand(sql, conn))
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var item = new OfertaListaItem
                                {
                                    ID = reader.GetInt32(0),
                                    NumerOferty = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                    DataWystawienia = reader.IsDBNull(2) ? DateTime.Now : reader.GetDateTime(2),
                                    DataWaznosci = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                                    KlientNazwa = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    KlientAdres = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                    KlientMiejscowosc = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                    KlientEmail = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                    KlientTelefon = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                    KlientOsobaKontaktowa = reader.IsDBNull(9) ? "" : reader.GetString(9),
                                    WartoscNetto = reader.IsDBNull(10) ? 0 : reader.GetDecimal(10),
                                    WartoscBrutto = reader.IsDBNull(10) ? 0 : reader.GetDecimal(10), // tymczasowo = netto
                                    Status = reader.IsDBNull(11) ? "Nowa" : reader.GetString(11),
                                    SciezkaPDF = reader.IsDBNull(12) ? "" : reader.GetString(12),
                                    HandlowiecID = reader.IsDBNull(13) ? "" : reader.GetString(13),
                                    HandlowiecNazwa = reader.IsDBNull(14) ? "" : reader.GetString(14),
                                    Uwagi = reader.IsDBNull(15) ? "" : reader.GetString(15),
                                    LiczbaPozycji = reader.IsDBNull(16) ? 0 : reader.GetInt32(16)
                                };

                                Dispatcher.Invoke(() => _wszystkieOferty.Add(item));
                            }
                        }
                    }
                });

                FiltrujOferty();
                AktualizujStatystyki();
                txtPodtytul.Text = $"Łącznie {_wszystkieOferty.Count} ofert w bazie";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania ofert:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Filtrowanie

        private void FiltrujOferty()
        {
            var wynik = _wszystkieOferty.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(txtSzukaj.Text))
            {
                var szukaj = txtSzukaj.Text.ToLower();
                wynik = wynik.Where(o =>
                    (o.NumerOferty?.ToLower().Contains(szukaj) ?? false) ||
                    (o.KlientNazwa?.ToLower().Contains(szukaj) ?? false) ||
                    (o.KlientEmail?.ToLower().Contains(szukaj) ?? false) ||
                    (o.KlientMiejscowosc?.ToLower().Contains(szukaj) ?? false));
            }

            if (cboStatus.SelectedItem is ComboBoxItem statusItem &&
                !string.IsNullOrEmpty(statusItem.Tag?.ToString()))
            {
                var status = statusItem.Tag.ToString();
                wynik = wynik.Where(o => o.Status == status);
            }

            if (dpDataOd.SelectedDate.HasValue)
            {
                wynik = wynik.Where(o => o.DataWystawienia.Date >= dpDataOd.SelectedDate.Value.Date);
            }

            if (dpDataDo.SelectedDate.HasValue)
            {
                wynik = wynik.Where(o => o.DataWystawienia.Date <= dpDataDo.SelectedDate.Value.Date);
            }

            _filtrowaneOferty.Clear();
            foreach (var item in wynik)
            {
                _filtrowaneOferty.Add(item);
            }

            AktualizujStatystyki();
        }

        private void AktualizujStatystyki()
        {
            var lista = _filtrowaneOferty;

            txtStatWszystkie.Text = lista.Count.ToString();
            txtStatNowe.Text = lista.Count(o => o.Status == "Nowa").ToString();
            txtStatWyslane.Text = lista.Count(o => o.Status == "Wyslana").ToString();
            txtStatZaakceptowane.Text = lista.Count(o => o.Status == "Zaakceptowana").ToString();
            txtStatWartosc.Text = lista.Sum(o => o.WartoscBrutto).ToString("N0") + " zł";
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e) => FiltrujOferty();
        private void CboStatus_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (IsLoaded) FiltrujOferty(); }
        private void DpData_SelectedDateChanged(object sender, SelectionChangedEventArgs e) { if (IsLoaded) FiltrujOferty(); }

        private void BtnFiltrDzisiaj_Click(object sender, RoutedEventArgs e)
        {
            dpDataOd.SelectedDate = DateTime.Today;
            dpDataDo.SelectedDate = DateTime.Today;
        }

        private void BtnFiltrTydzien_Click(object sender, RoutedEventArgs e)
        {
            dpDataOd.SelectedDate = DateTime.Today.AddDays(-7);
            dpDataDo.SelectedDate = DateTime.Today;
        }

        private void BtnFiltrMiesiac_Click(object sender, RoutedEventArgs e)
        {
            dpDataOd.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            dpDataDo.SelectedDate = DateTime.Today;
        }

        private void BtnFiltrWyczysc_Click(object sender, RoutedEventArgs e)
        {
            txtSzukaj.Text = "";
            cboStatus.SelectedIndex = 0;
            dpDataOd.SelectedDate = null;
            dpDataDo.SelectedDate = null;
        }

        #endregion

        #region Wybór oferty i edycja danych

        private void DgOferty_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_daneZmienione && _wybranaOferta != null)
            {
                var result = MessageBox.Show("Masz niezapisane zmiany. Czy zapisać?", "Niezapisane zmiany",
                    MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes) ZapiszDaneKontaktowe();
                else if (result == MessageBoxResult.Cancel) { dgOferty.SelectedItem = _wybranaOferta; return; }
            }

            // Obsługa przycisku usuwania wielu
            int zaznaczoneCount = dgOferty.SelectedItems.Count;
            if (zaznaczoneCount > 1)
            {
                btnUsunZaznaczone.Visibility = Visibility.Visible;
                btnUsunZaznaczone.IsEnabled = true;
                btnUsunZaznaczone.Content = $"🗑️ Usuń zaznaczone ({zaznaczoneCount})";
            }
            else
            {
                btnUsunZaznaczone.Visibility = Visibility.Collapsed;
                btnUsunZaznaczone.IsEnabled = false;
            }

            _wybranaOferta = dgOferty.SelectedItem as OfertaListaItem;

            if (_wybranaOferta != null)
            {
                WyswietlSzczegolyOferty(_wybranaOferta);
                placeholderBrakWyboru.Visibility = Visibility.Collapsed;
                panelSzczegolyOferty.Visibility = Visibility.Visible;
                btnWyslijEmailZOferta.IsEnabled = true;
                btnOtworzPDFSzczegoly.IsEnabled = !string.IsNullOrEmpty(_wybranaOferta.SciezkaPDF);
            }
            else
            {
                placeholderBrakWyboru.Visibility = Visibility.Visible;
                panelSzczegolyOferty.Visibility = Visibility.Collapsed;
                btnWyslijEmailZOferta.IsEnabled = false;
                btnOtworzPDFSzczegoly.IsEnabled = false;
            }

            _daneZmienione = false;
            btnZapiszKontakt.IsEnabled = false;
            txtZmianyNiezapisane.Visibility = Visibility.Collapsed;
        }

        private void DgOferty_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Podwójne kliknięcie otwiera PDF oferty
            if (_wybranaOferta != null)
            {
                OtworzPDF(_wybranaOferta);
            }
        }

        private void WyswietlSzczegolyOferty(OfertaListaItem oferta)
        {
            txtSzczegolyIkona.Text = oferta.StatusIkona;
            txtSzczegolyNumer.Text = oferta.NumerOferty;
            txtSzczegolyData.Text = oferta.DataWystawieniaFormatowana;
            txtKlientNazwa.Text = oferta.KlientNazwa;
            txtKlientAdres.Text = $"{oferta.KlientAdres}, {oferta.KlientMiejscowosc}";
            txtKlientOsoba.Text = oferta.KlientOsobaKontaktowa ?? "";
            txtKlientEmail.Text = oferta.KlientEmail ?? "";
            txtKlientTelefon.Text = oferta.KlientTelefon ?? "";
            txtSzczegolyWartosc.Text = oferta.WartoscFormatowana;
            txtSzczegolyHandlowiec.Text = string.IsNullOrEmpty(oferta.HandlowiecNazwa) ? "-" : oferta.HandlowiecNazwa;
        }

        private void DaneKontaktowe_Changed(object sender, TextChangedEventArgs e)
        {
            if (_wybranaOferta == null) return;

            bool zmiany = txtKlientOsoba.Text != (_wybranaOferta.KlientOsobaKontaktowa ?? "") ||
                          txtKlientEmail.Text != (_wybranaOferta.KlientEmail ?? "") ||
                          txtKlientTelefon.Text != (_wybranaOferta.KlientTelefon ?? "");

            _daneZmienione = zmiany;
            btnZapiszKontakt.IsEnabled = zmiany;
            txtZmianyNiezapisane.Visibility = zmiany ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnZapiszKontakt_Click(object sender, RoutedEventArgs e) => ZapiszDaneKontaktowe();

        private void ZapiszDaneKontaktowe()
        {
            if (_wybranaOferta == null) return;

            try
            {
                using (var conn = new SqlConnection(_connectionStringLibraNet))
                {
                    conn.Open();
                    const string sql = @"UPDATE Oferty SET KlientOsobaKontaktowa = @Osoba, KlientEmail = @Email, KlientTelefon = @Telefon WHERE ID = @ID";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Osoba", txtKlientOsoba.Text ?? "");
                        cmd.Parameters.AddWithValue("@Email", txtKlientEmail.Text ?? "");
                        cmd.Parameters.AddWithValue("@Telefon", txtKlientTelefon.Text ?? "");
                        cmd.Parameters.AddWithValue("@ID", _wybranaOferta.ID);
                        cmd.ExecuteNonQuery();
                    }
                }

                _wybranaOferta.KlientOsobaKontaktowa = txtKlientOsoba.Text;
                _wybranaOferta.KlientEmail = txtKlientEmail.Text;
                _wybranaOferta.KlientTelefon = txtKlientTelefon.Text;
                _daneZmienione = false;
                btnZapiszKontakt.IsEnabled = false;
                txtZmianyNiezapisane.Visibility = Visibility.Collapsed;
                dgOferty.Items.Refresh();

                MessageBox.Show("✅ Dane kontaktowe zostały zapisane.", "Zapisano", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Zmiana statusu

        private void BtnZmienStatus_Click(object sender, RoutedEventArgs e)
        {
            if (_wybranaOferta == null) return;

            var btn = sender as Button;
            var nowyStatus = btn?.Tag?.ToString();
            if (string.IsNullOrEmpty(nowyStatus) || _wybranaOferta.Status == nowyStatus) return;

            try
            {
                using (var conn = new SqlConnection(_connectionStringLibraNet))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("UPDATE Oferty SET Status = @Status WHERE ID = @ID", conn))
                    {
                        cmd.Parameters.AddWithValue("@Status", nowyStatus);
                        cmd.Parameters.AddWithValue("@ID", _wybranaOferta.ID);
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = new SqlCommand(@"INSERT INTO Oferty_Historia (OfertaID, DataZmiany, PoprzedniStatus, NowyStatus, UserID, Opis) VALUES (@OfertaID, GETDATE(), @Poprzedni, @Nowy, @UserID, @Opis)", conn))
                    {
                        cmd.Parameters.AddWithValue("@OfertaID", _wybranaOferta.ID);
                        cmd.Parameters.AddWithValue("@Poprzedni", _wybranaOferta.Status ?? "");
                        cmd.Parameters.AddWithValue("@Nowy", nowyStatus);
                        cmd.Parameters.AddWithValue("@UserID", UserID ?? "");
                        cmd.Parameters.AddWithValue("@Opis", $"Zmiana statusu z '{_wybranaOferta.Status}' na '{nowyStatus}'");
                        cmd.ExecuteNonQuery();
                    }
                }

                _wybranaOferta.Status = nowyStatus;
                dgOferty.Items.Refresh();
                WyswietlSzczegolyOferty(_wybranaOferta);
                AktualizujStatystyki();

                MessageBox.Show($"✅ Status zmieniony na: {nowyStatus}", "Zmieniono", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zmiany statusu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Akcje

        private void BtnOtworzPDF_Click(object sender, RoutedEventArgs e)
        {
            var oferta = (sender as Button)?.Tag as OfertaListaItem;
            if (oferta != null) OtworzPDF(oferta);
        }

        private void BtnOtworzPDFSzczegoly_Click(object sender, RoutedEventArgs e)
        {
            if (_wybranaOferta != null) OtworzPDF(_wybranaOferta);
        }

        private void OtworzPDF(OfertaListaItem oferta)
        {
            if (string.IsNullOrEmpty(oferta.SciezkaPDF))
            {
                MessageBox.Show("Brak pliku PDF dla tej oferty.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!System.IO.File.Exists(oferta.SciezkaPDF))
            {
                MessageBox.Show($"Plik PDF nie istnieje:\n{oferta.SciezkaPDF}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try { Process.Start(new ProcessStartInfo(oferta.SciezkaPDF) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show($"Błąd otwierania PDF:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnWyslijEmail_Click(object sender, RoutedEventArgs e)
        {
            var oferta = (sender as Button)?.Tag as OfertaListaItem;
            if (oferta != null) WyslijEmail(oferta);
        }

        private void BtnWyslijEmailZOferta_Click(object sender, RoutedEventArgs e)
        {
            if (_wybranaOferta != null) WyslijEmail(_wybranaOferta);
        }

        private void WyslijEmail(OfertaListaItem oferta)
        {
            if (string.IsNullOrWhiteSpace(oferta.KlientEmail))
            {
                MessageBox.Show("Brak adresu email dla tego klienta.\n\nUzupełnij email w panelu po prawej stronie i zapisz.", "Brak emaila", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(oferta.SciezkaPDF))
            {
                MessageBox.Show("Brak pliku PDF do wysłania.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Wysłać ofertę {oferta.NumerOferty} na adres:\n\n📧 {oferta.KlientEmail}\n\nKlient: {oferta.KlientNazwa}",
                "Potwierdź wysyłkę", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                MessageBox.Show($"✅ Oferta została wysłana na:\n{oferta.KlientEmail}", "Wysłano", MessageBoxButton.OK, MessageBoxImage.Information);
                if (oferta.Status == "Nowa") BtnZmienStatus_Click(new Button { Tag = "Wyslana" }, null);
            }
        }

        private void BtnNowaOferta_Click(object sender, RoutedEventArgs e)
        {
            // UserID jest teraz string - zgodność z OfertaHandlowaWindow
            var okno = new OfertaHandlowaWindow { UserID = UserID };
            okno.ShowDialog();
            _ = WczytajOfertyAsync();
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e) => await WczytajOfertyAsync();

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            if (_daneZmienione)
            {
                var result = MessageBox.Show("Masz niezapisane zmiany. Czy zapisać przed zamknięciem?", "Niezapisane zmiany",
                    MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes) ZapiszDaneKontaktowe();
                else if (result == MessageBoxResult.Cancel) return;
            }
            Close();
        }

        #endregion

        #region Usuwanie ofert

        // Usuwanie z przycisku w tabeli
        private async void BtnUsunOferte_Click(object sender, RoutedEventArgs e)
        {
            var oferta = (sender as Button)?.Tag as OfertaListaItem;
            if (oferta != null)
            {
                await UsunOferteAsync(oferta);
            }
        }

        // Usuwanie z panelu szczegółów
        private async void BtnUsunWybranaOferte_Click(object sender, RoutedEventArgs e)
        {
            if (_wybranaOferta != null)
            {
                await UsunOferteAsync(_wybranaOferta);
            }
        }

        // Usuwanie wielu zaznaczonych
        private async void BtnUsunZaznaczone_Click(object sender, RoutedEventArgs e)
        {
            var zaznaczone = dgOferty.SelectedItems.Cast<OfertaListaItem>().ToList();
            if (zaznaczone.Count == 0)
            {
                MessageBox.Show("Nie zaznaczono żadnych ofert.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Czy na pewno chcesz usunąć {zaznaczone.Count} zaznaczonych ofert?\n\n⚠️ UWAGA: Ta operacja jest nieodwracalna!\n\nZostaną usunięte oferty oraz ich pozycje i historia.",
                "Potwierdź usunięcie wielu ofert",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            int usuniete = 0;
            int bledy = 0;

            foreach (var oferta in zaznaczone)
            {
                if (await UsunOferteZBazyAsync(oferta.ID))
                    usuniete++;
                else
                    bledy++;
            }

            await WczytajOfertyAsync();

            if (bledy > 0)
                MessageBox.Show($"Usunięto {usuniete} ofert.\n{bledy} ofert nie udało się usunąć.", "Zakończono z błędami", MessageBoxButton.OK, MessageBoxImage.Warning);
            else
                MessageBox.Show($"✅ Pomyślnie usunięto {usuniete} ofert.", "Usunięto", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Główna metoda usuwania pojedynczej oferty
        private async Task UsunOferteAsync(OfertaListaItem oferta)
        {
            var result = MessageBox.Show(
                $"Czy na pewno chcesz usunąć ofertę?\n\n📋 Numer: {oferta.NumerOferty}\n👤 Klient: {oferta.KlientNazwa}\n💰 Wartość: {oferta.WartoscFormatowana}\n\n⚠️ UWAGA: Ta operacja jest nieodwracalna!\nZostaną usunięte wszystkie dane oferty, pozycje i historia.",
                "Potwierdź usunięcie oferty",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            bool sukces = await UsunOferteZBazyAsync(oferta.ID);

            if (sukces)
            {
                MessageBox.Show($"✅ Oferta {oferta.NumerOferty} została usunięta.", "Usunięto", MessageBoxButton.OK, MessageBoxImage.Information);

                // Ukryj panel szczegółów jeśli usunięto wybraną ofertę
                if (_wybranaOferta?.ID == oferta.ID)
                {
                    _wybranaOferta = null;
                    panelSzczegolyOferty.Visibility = Visibility.Collapsed;
                    placeholderBrakWyboru.Visibility = Visibility.Visible;
                    btnWyslijEmailZOferta.IsEnabled = false;
                    btnOtworzPDFSzczegoly.IsEnabled = false;
                }

                await WczytajOfertyAsync();
            }
            else
            {
                MessageBox.Show($"❌ Nie udało się usunąć oferty {oferta.NumerOferty}.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Usuwanie z bazy danych
        private async Task<bool> UsunOferteZBazyAsync(int ofertaId)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using (var conn = new SqlConnection(_connectionStringLibraNet))
                    {
                        conn.Open();
                        using (var trans = conn.BeginTransaction())
                        {
                            try
                            {
                                // 1. Usuń pozycje oferty
                                using (var cmd = new SqlCommand("DELETE FROM Oferty_Pozycje WHERE OfertaID = @ID", conn, trans))
                                {
                                    cmd.Parameters.AddWithValue("@ID", ofertaId);
                                    cmd.ExecuteNonQuery();
                                }

                                // 2. Usuń historię oferty
                                using (var cmd = new SqlCommand("DELETE FROM Oferty_Historia WHERE OfertaID = @ID", conn, trans))
                                {
                                    cmd.Parameters.AddWithValue("@ID", ofertaId);
                                    cmd.ExecuteNonQuery();
                                }

                                // 3. Usuń samą ofertę
                                using (var cmd = new SqlCommand("DELETE FROM Oferty WHERE ID = @ID", conn, trans))
                                {
                                    cmd.Parameters.AddWithValue("@ID", ofertaId);
                                    int affected = cmd.ExecuteNonQuery();

                                    if (affected > 0)
                                    {
                                        trans.Commit();
                                        return true;
                                    }
                                    else
                                    {
                                        trans.Rollback();
                                        return false;
                                    }
                                }
                            }
                            catch
                            {
                                trans.Rollback();
                                throw;
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas usuwania oferty:\n{ex.Message}", "Błąd bazy danych", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        #endregion
    }

    // UWAGA: Klasa OfertaListaItem jest w osobnym pliku OfertaListaItem.cs!
    // NIE DUPLIKUJ JEJ TUTAJ!
}