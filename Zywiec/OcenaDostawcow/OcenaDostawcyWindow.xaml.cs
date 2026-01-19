using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1
{
    /// <summary>
    /// Okno do oceny dostawców/hodowców kurczaków
    /// </summary>
    public partial class OcenaDostawcyWindow : Window
    {
        // STAŁE
        private const string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // ZMIENNE
        private string _dostawcaId;
        private string _userId;
        private int? _ocenaId;

        public OcenaDostawcyWindow(string dostawcaId, string userId, int? ocenaId = null)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            _dostawcaId = dostawcaId;
            _userId = userId;
            _ocenaId = ocenaId;

            dpDataOceny.SelectedDate = DateTime.Now;

            LoadDostawcaInfo();
            GenerateReportNumber();
            SetupEventHandlers();

            if (_ocenaId.HasValue)
            {
                LoadExistingOcena();
            }
        }

        private async void LoadDostawcaInfo()
        {
            try
            {
                string zapytanie = @"
                    SELECT Name, ShortName, Address, City, PostalCode, Phone1, Phone2, Email
                    FROM [dbo].[Dostawcy]
                    WHERE ID = @DostawcaID";

                using var polaczenie = new SqlConnection(connectionString);
                using var komenda = new SqlCommand(zapytanie, polaczenie);
                komenda.Parameters.AddWithValue("@DostawcaID", _dostawcaId);

                await polaczenie.OpenAsync();
                using var czytnik = await komenda.ExecuteReaderAsync();

                if (await czytnik.ReadAsync())
                {
                    string nazwa = czytnik["Name"]?.ToString() ?? "Nieznany";
                    string skrot = czytnik["ShortName"]?.ToString() ?? "";
                    string adres = $"{czytnik["PostalCode"]} {czytnik["City"]}, {czytnik["Address"]}";

                    txtNazwaDostawcy.Text = $"{nazwa} ({skrot}) - {adres}";
                }
            }
            catch (Exception blad)
            {
                MessageBox.Show($"Nie mogę załadować danych hodowcy: {blad.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtNazwaDostawcy.Text = "Błąd ładowania danych";
            }
        }

        private void GenerateReportNumber()
        {
            try
            {
                int rok = DateTime.Now.Year;

                string zapytanie = @"
                    SELECT ISNULL(MAX(CAST(SUBSTRING(NumerRaportu, 9, 2) AS INT)), 0) + 1
                    FROM [dbo].[OcenyDostawcow] 
                    WHERE NumerRaportu LIKE '%/' + @Year";

                using var polaczenie = new SqlConnection(connectionString);
                using var komenda = new SqlCommand(zapytanie, polaczenie);
                komenda.Parameters.AddWithValue("@Year", rok.ToString());

                polaczenie.Open();
                object wynik = komenda.ExecuteScalar();
                int numerKolejny = wynik != DBNull.Value ? Convert.ToInt32(wynik) : 1;

                txtNumerRaportu.Text = $"PZ-Z-10-{numerKolejny:00}/{rok}";
            }
            catch
            {
                txtNumerRaportu.Text = $"PZ-Z-10-XX/{DateTime.Now.Year}";
            }
        }

        // ==========================================
        // OBSŁUGA ZDARZEŃ (CHECKBOXY)
        // ==========================================
        private void SetupEventHandlers()
        {
            // Pytanie 1 - PIW
            chkPIW_TAK.Checked += (s, e) => { chkPIW_NIE.IsChecked = false; CalculatePoints(); };
            chkPIW_NIE.Checked += (s, e) => { chkPIW_TAK.IsChecked = false; CalculatePoints(); };

            // Pytanie 2 - Środki dezynfekcyjne
            chkDezynf_TAK.Checked += (s, e) => { chkDezynf_NIE.IsChecked = false; CalculatePoints(); };
            chkDezynf_NIE.Checked += (s, e) => { chkDezynf_TAK.IsChecked = false; CalculatePoints(); };

            // Pytanie 3 - Obornik
            chkObornik_TAK.Checked += (s, e) => { chkObornik_NIE.IsChecked = false; CalculatePoints(); };
            chkObornik_NIE.Checked += (s, e) => { chkObornik_TAK.IsChecked = false; CalculatePoints(); };

            // Pytanie 4 - Weterynaria
            chkWeterynaria_TAK.Checked += (s, e) => { chkWeterynaria_NIE.IsChecked = false; CalculatePoints(); };
            chkWeterynaria_NIE.Checked += (s, e) => { chkWeterynaria_TAK.IsChecked = false; CalculatePoints(); };

            // Pytanie 5 - Teren
            chkTeren_TAK.Checked += (s, e) => { chkTeren_NIE.IsChecked = false; CalculatePoints(); };
            chkTeren_NIE.Checked += (s, e) => { chkTeren_TAK.IsChecked = false; CalculatePoints(); };

            // Pytanie 6 - Odzież
            chkOdziez_TAK.Checked += (s, e) => { chkOdziez_NIE.IsChecked = false; CalculatePoints(); };
            chkOdziez_NIE.Checked += (s, e) => { chkOdziez_TAK.IsChecked = false; CalculatePoints(); };

            // Pytanie 7 - Maty
            chkMaty_TAK.Checked += (s, e) => { chkMaty_NIE.IsChecked = false; CalculatePoints(); };
            chkMaty_NIE.Checked += (s, e) => { chkMaty_TAK.IsChecked = false; CalculatePoints(); };

            // Pytanie 8 - Środki dezynfekcyjne doraźne
            chkSrodkiDez_TAK.Checked += (s, e) => { chkSrodkiDez_NIE.IsChecked = false; CalculatePoints(); };
            chkSrodkiDez_NIE.Checked += (s, e) => { chkSrodkiDez_TAK.IsChecked = false; CalculatePoints(); };

            // Pytania 1-20 (Lista kontrolna)
            for (int i = 1; i <= 20; i++)
            {
                var takCheckBox = this.FindName($"chkQ{i}_TAK") as CheckBox;
                var nieCheckBox = this.FindName($"chkQ{i}_NIE") as CheckBox;

                if (takCheckBox != null && nieCheckBox != null)
                {
                    int numer = i;

                    takCheckBox.Checked += (s, e) => {
                        var nie = this.FindName($"chkQ{numer}_NIE") as CheckBox;
                        if (nie != null) nie.IsChecked = false;
                        CalculatePoints();
                    };

                    nieCheckBox.Checked += (s, e) => {
                        var tak = this.FindName($"chkQ{numer}_TAK") as CheckBox;
                        if (tak != null) tak.IsChecked = false;
                        CalculatePoints();
                    };
                }
            }

            // Pytanie 21 - Dokumentacja
            chkQ21_TAK.Checked += (s, e) => { chkQ21_NIE.IsChecked = false; };
            chkQ21_NIE.Checked += (s, e) => { chkQ21_TAK.IsChecked = false; };
        }

        private void CalculatePoints()
        {
            int punkty1_5 = 0;   // Punkty za pytania 1-5
            int punkty6_20 = 0;  // Punkty za pytania 6-20

            // PYTANIA 1-5: każde TAK = 3 punkty
            if (chkQ1_TAK?.IsChecked == true) punkty1_5 += 3;
            if (chkQ2_TAK?.IsChecked == true) punkty1_5 += 3;
            if (chkQ3_TAK?.IsChecked == true) punkty1_5 += 3;
            if (chkQ4_TAK?.IsChecked == true) punkty1_5 += 3;
            if (chkQ5_TAK?.IsChecked == true) punkty1_5 += 3;

            // PYTANIA 6-20: każde TAK = 1 punkt
            for (int i = 6; i <= 20; i++)
            {
                var chk = this.FindName($"chkQ{i}_TAK") as CheckBox;
                if (chk?.IsChecked == true) punkty6_20 += 1;
            }

            // Wyświetl wyniki
            txtPunkty1_5.Text = punkty1_5.ToString();
            txtPunkty6_20.Text = punkty6_20.ToString();
            txtPunktyRazem.Text = (punkty1_5 + punkty6_20).ToString();

            // Kolorowanie wyniku
            int razem = punkty1_5 + punkty6_20;
            if (razem >= 30)
                txtPunktyRazem.Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x8B, 0x57)); // Zielony
            else if (razem >= 20)
                txtPunktyRazem.Background = new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37)); // Złoty
            else
                txtPunktyRazem.Background = new SolidColorBrush(Color.FromRgb(0xDC, 0x14, 0x3C)); // Czerwony

            txtPunktyRazem.Foreground = Brushes.White;
        }

        // ==========================================
        // ZAPIS DO BAZY
        // ==========================================
        private async void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (!dpDataOceny.SelectedDate.HasValue)
            {
                MessageBox.Show("Proszę wybrać datę oceny!", "Brak daty",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string zapytanie = @"
                    INSERT INTO [dbo].[OcenyDostawcow]
                    (DostawcaID, DataOceny, NumerRaportu, NrProcedury,
                     CzyPIW, MiejsceSrodkowDezynfekcyjnych, CzyWywozObornika,
                     MiejsceWeterynarii, TerenUporzadkowany, ObuwieOchronne,
                     OdziezOchronna, MatyDezynfekcyjne, SrodkiDezynfekcyjneDorazne,
                     PosiadaWNI, FermaOpieka, StadoWolneOdSalmonelli,
                     KurnikMytyDezynfekowany, PadleUsuwane, ZaladunekZgodnyZPlanem,
                     DostepDoPlanu, WjazdWybetowany, WjazdOswietlony,
                     PodjazdOswietlony, PodjazdWybetowany, KurnikDostosowyDoZaladunku,
                     ZapewnionaIdentyfikowalnosc, PoczesaWylapywaniaBrojlerowOswietlenie,
                     SciolkaSucha, KuryCzyste, KurySuche, PodczasZaladunkuPuste,
                     TechnikaLapania, IloscOsobDoZaladunku,
                     PunktySekcja1_5, PunktySekcja6_20, PunktyRazem,
                     Uwagi, OceniajacyUserID, DataUtworzenia, Status)
                    VALUES
                    (@DostawcaID, @DataOceny, @NumerRaportu, '04',
                     @CzyPIW, @MiejsceDezynf, @Obornik,
                     @Weterynaria, @Teren, @Obuwie,
                     @Odziez, @Maty, @SrodkiDez,
                     @Q1, @Q2, @Q3, @Q4, @Q5, @Q6, @Q7, @Q8, @Q9, @Q10,
                     @Q11, @Q12, @Q13, @Q14, @Q15, @Q16, @Q17, @Q18, @Q19, @Q20,
                     @Punkty1_5, @Punkty6_20, @PunktyRazem,
                     @Uwagi, @UserID, GETDATE(), 'Aktywna')";

                using var polaczenie = new SqlConnection(connectionString);
                using var komenda = new SqlCommand(zapytanie, polaczenie);

                komenda.Parameters.AddWithValue("@DostawcaID", _dostawcaId);
                komenda.Parameters.AddWithValue("@DataOceny", dpDataOceny.SelectedDate.Value);
                komenda.Parameters.AddWithValue("@NumerRaportu", txtNumerRaportu.Text);

                // Samoocena
                komenda.Parameters.AddWithValue("@CzyPIW", chkPIW_TAK.IsChecked ?? false);
                komenda.Parameters.AddWithValue("@MiejsceDezynf", chkDezynf_TAK.IsChecked ?? false);
                komenda.Parameters.AddWithValue("@Obornik", chkObornik_TAK.IsChecked ?? false);
                komenda.Parameters.AddWithValue("@Weterynaria", chkWeterynaria_TAK.IsChecked ?? false);
                komenda.Parameters.AddWithValue("@Teren", chkTeren_TAK.IsChecked ?? false);
                komenda.Parameters.AddWithValue("@Obuwie", chkOdziez_TAK.IsChecked ?? false);
                komenda.Parameters.AddWithValue("@Odziez", chkOdziez_TAK.IsChecked ?? false);
                komenda.Parameters.AddWithValue("@Maty", chkMaty_TAK.IsChecked ?? false);
                komenda.Parameters.AddWithValue("@SrodkiDez", chkSrodkiDez_TAK.IsChecked ?? false);

                // Lista kontrolna
                for (int i = 1; i <= 20; i++)
                {
                    var checkbox = this.FindName($"chkQ{i}_TAK") as CheckBox;
                    komenda.Parameters.AddWithValue($"@Q{i}", checkbox?.IsChecked ?? false);
                }

                // Punkty
                komenda.Parameters.AddWithValue("@Punkty1_5", int.Parse(txtPunkty1_5.Text ?? "0"));
                komenda.Parameters.AddWithValue("@Punkty6_20", int.Parse(txtPunkty6_20.Text ?? "0"));
                komenda.Parameters.AddWithValue("@PunktyRazem", int.Parse(txtPunktyRazem.Text ?? "0"));

                komenda.Parameters.AddWithValue("@Uwagi", txtUwagi.Text ?? "");
                komenda.Parameters.AddWithValue("@UserID", _userId);

                await polaczenie.OpenAsync();
                await komenda.ExecuteNonQueryAsync();

                MessageBox.Show(
                    "✅ Ocena została zapisana pomyślnie!", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception blad)
            {
                MessageBox.Show($"❌ Nie udało się zapisać oceny!\n\n{blad.Message}",
                    "Błąd zapisu", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==========================================
        // GENEROWANIE PDF
        // ==========================================

        /// <summary>
        /// Przycisk "Generuj PDF" (Wypełniony raport)
        /// </summary>
        private void BtnGenerujPDF_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Czy wygenerować wypełniony raport PDF?", "Generowanie PDF",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                GenerujWypelnionyPDF();
            }
        }

        /// <summary>
        /// PRZYCISK "DRUKUJ PUSTY" - generuje pusty formularz
        /// </summary>
        private void BtnDrukujPusty_Click(object sender, RoutedEventArgs e)
        {
            // Pytanie o punktację
            var result = MessageBox.Show(
                "Czy wyświetlić punktację na formularzu?\n\n" +
                "TAK - pokaże wartości punktów\n" +
                "NIE - formularz bez punktacji",
                "Opcje formularza",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.Yes);

            bool showPoints = (result == MessageBoxResult.Yes);

            // Ścieżka pliku na pulpicie
            string nazwaPliku = $"Formularz_Oceny_PUSTY_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            string sciezka = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), nazwaPliku);

            try
            {
                // GENEROWANIE PDF
                var generator = new BlankOcenaFormPDFGenerator();
                generator.GenerujPustyFormularz(sciezka, showPoints);

                // Sprawdzenie czy plik istnieje i ma rozmiar > 0
                if (File.Exists(sciezka))
                {
                    var info = new FileInfo(sciezka);
                    if (info.Length > 0)
                    {
                        // Otwórz PDF
                        Process.Start(new ProcessStartInfo(sciezka) { UseShellExecute = true });
                        
                        MessageBox.Show(
                            $"✅ Formularz wygenerowany!\n\n" +
                            $"Plik: {nazwaPliku}\n" +
                            $"Rozmiar: {info.Length:N0} bajtów\n" +
                            $"Lokalizacja: Pulpit",
                            "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(
                            $"❌ Plik PDF ma rozmiar 0 KB!\n\n" +
                            $"Ścieżka: {sciezka}",
                            "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show(
                        $"❌ Plik PDF nie został utworzony!\n\n" +
                        $"Ścieżka: {sciezka}",
                        "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"❌ BŁĄD:\n\n{ex.Message}\n\nTyp: {ex.GetType().Name}",
                    "Błąd generowania PDF", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Generuje wypełniony raport PDF z aktualnymi danymi
        /// </summary>
        private void GenerujWypelnionyPDF()
        {
            try
            {
                // 1. Zbieranie danych z formularza
                bool[] samoocena = new bool[8];
                samoocena[0] = chkPIW_TAK.IsChecked == true;
                samoocena[1] = chkDezynf_TAK.IsChecked == true;
                samoocena[2] = chkObornik_TAK.IsChecked == true;
                samoocena[3] = chkWeterynaria_TAK.IsChecked == true;
                samoocena[4] = chkTeren_TAK.IsChecked == true;
                samoocena[5] = chkOdziez_TAK.IsChecked == true;
                samoocena[6] = chkMaty_TAK.IsChecked == true;
                samoocena[7] = chkSrodkiDez_TAK.IsChecked == true;

                bool[] listaKontrolna = new bool[20];
                for (int i = 0; i < 20; i++)
                {
                    var chk = this.FindName($"chkQ{i + 1}_TAK") as CheckBox;
                    listaKontrolna[i] = chk?.IsChecked == true;
                }

                bool dokumentacja = chkQ21_TAK.IsChecked == true;

                int.TryParse(txtPunkty1_5.Text, out int p1_5);
                int.TryParse(txtPunkty6_20.Text, out int p6_20);
                int.TryParse(txtPunktyRazem.Text, out int pRazem);

                // 2. Ścieżka pliku
                string numerBezZnakow = txtNumerRaportu.Text.Replace("/", "_").Replace("-", "_");
                string nazwaPliku = $"Ocena_{numerBezZnakow}.pdf";
                string sciezka = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), nazwaPliku);

                // 3. Generowanie przez OcenaPDFGenerator
                var generator = new OcenaPDFGenerator();
                generator.GenerujPdf(
                    sciezka,
                    txtNumerRaportu.Text,
                    dpDataOceny.SelectedDate ?? DateTime.Now,
                    txtNazwaDostawcy.Text,
                    _dostawcaId,
                    samoocena,
                    listaKontrolna,
                    dokumentacja,
                    p1_5, p6_20, pRazem,
                    txtUwagi.Text,
                    false  // czyPusty = false
                );

                // 4. Otwarcie pliku
                Process.Start(new ProcessStartInfo(sciezka) { UseShellExecute = true });
                MessageBox.Show($"✅ PDF wygenerowany!\n{nazwaPliku}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd PDF: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Otwiera okno historii ocen dla bieżącego dostawcy
        /// </summary>
        private void BtnHistoria_Click(object sender, RoutedEventArgs e)
        {
            var oknoHistorii = new HistoriaOcenWindow(_dostawcaId);
            oknoHistorii.ShowDialog();
        }

        /// <summary>
        /// Anuluje i zamyka okno
        /// </summary>
        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            var wynik = MessageBox.Show(
                "Czy na pewno chcesz anulować?\n\nNiezapisane dane zostaną utracone!",
                "Potwierdzenie",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (wynik == MessageBoxResult.Yes)
            {
                DialogResult = false;
                Close();
            }
        }

        /// <summary>
        /// Ładuje istniejącą ocenę do edycji
        /// </summary>
        private async void LoadExistingOcena()
        {
            // Miejsce na logikę ładowania istniejącej oceny do edycji
        }
    }
}
