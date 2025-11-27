using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Kalendarz1.OfertaCenowa
{
    /// <summary>
    /// Wiersz marży dla konkretnego towaru
    /// </summary>
    public class MarzaWiersz : INotifyPropertyChanged
    {
        private decimal _cenaSprzedazy;
        private decimal _sredniaSprzedazy;
        private decimal _cenaZakupu;
        private bool _ladowanie = false;

        public int Id { get; set; }
        public string Kod { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public decimal Ilosc { get; set; }

        public decimal CenaZakupu
        {
            get => _cenaZakupu;
            set
            {
                _cenaZakupu = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CenaZakupuStr));
                if (!_ladowanie) OnPropertyChanged(nameof(Marza));
                if (!_ladowanie) OnPropertyChanged(nameof(MarzaStr));
                if (!_ladowanie) OnPropertyChanged(nameof(MarzaKolor));
            }
        }

        public decimal SredniaSprzedazy
        {
            get => _sredniaSprzedazy;
            set
            {
                _sredniaSprzedazy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SredniaSprzedazyStr));
            }
        }

        public decimal CenaSprzedazy
        {
            get => _cenaSprzedazy;
            set
            {
                _cenaSprzedazy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CenaSprzedazyStr));
                OnPropertyChanged(nameof(Marza));
                OnPropertyChanged(nameof(MarzaStr));
                OnPropertyChanged(nameof(MarzaKolor));
            }
        }

        public string CenaSprzedazyStr
        {
            get => _cenaSprzedazy == 0 ? "" : _cenaSprzedazy.ToString("0.00");
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    CenaSprzedazy = 0;
                else if (decimal.TryParse(value.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
                    CenaSprzedazy = result;
            }
        }

        public decimal Marza => CenaZakupu > 0 ? ((CenaSprzedazy - CenaZakupu) / CenaZakupu) * 100 : 0;
        public string MarzaStr => $"{Marza:N1}%";
        public string CenaZakupuStr => CenaZakupu > 0 ? $"{CenaZakupu:N2}" : "-";
        public string SredniaSprzedazyStr => SredniaSprzedazy > 0 ? $"{SredniaSprzedazy:N2}" : "-";
        public string IloscStr => Ilosc > 0 ? $"{Ilosc:N0} kg" : "";

        public SolidColorBrush MarzaKolor
        {
            get
            {
                if (CenaZakupu == 0) return new SolidColorBrush(Color.FromRgb(156, 163, 175));
                if (Marza < 0) return new SolidColorBrush(Color.FromRgb(220, 38, 38));
                if (Marza < 5) return new SolidColorBrush(Color.FromRgb(245, 158, 11));
                if (Marza < 15) return new SolidColorBrush(Color.FromRgb(34, 197, 94));
                return new SolidColorBrush(Color.FromRgb(22, 163, 74));
            }
        }

        public void UstawLadowanie(bool ladowanie) => _ladowanie = ladowanie;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Okno zarządzania marżami - z cenami dla KONKRETNEGO towaru
    /// </summary>
    public partial class MarzeWindow : Window, INotifyPropertyChanged
    {
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        public ObservableCollection<MarzaWiersz> Wiersze { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public MarzeWindow(List<TowarOfertaWiersz> towary)
        {
            InitializeComponent();
            DataContext = this;

            foreach (var towar in towary)
            {
                var wiersz = new MarzaWiersz
                {
                    Id = towar.Id,
                    Kod = towar.Kod,
                    Nazwa = towar.Nazwa,
                    Ilosc = towar.Ilosc,
                    CenaSprzedazy = towar.CenaJednostkowa
                };
                Wiersze.Add(wiersz);
            }

            Loaded += async (s, e) => await LoadCenyAsync();
        }

        /// <summary>
        /// Pobiera cenę zakupu i średnią sprzedaży DLA KONKRETNEGO TOWARU
        /// </summary>
        private async Task LoadCenyAsync()
        {
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                foreach (var wiersz in Wiersze)
                {
                    wiersz.UstawLadowanie(true);

                    // 1. Pobierz cenę zakupu dla TEGO KONKRETNEGO towaru
                    await using (var cmdZakup = new SqlCommand(@"
                        SELECT TOP 1 Cena 
                        FROM [HANDEL].[HM].[TW_Ceny] 
                        WHERE IdTowar = @IdTowar AND IdRodzajCeny = 1
                        ORDER BY DataOd DESC", cn))
                    {
                        cmdZakup.Parameters.AddWithValue("@IdTowar", wiersz.Id);
                        var resultZakup = await cmdZakup.ExecuteScalarAsync();
                        if (resultZakup != null && resultZakup != DBNull.Value)
                        {
                            wiersz.CenaZakupu = Convert.ToDecimal(resultZakup);
                        }
                    }

                    // 2. Pobierz średnią cenę sprzedaży dla TEGO KONKRETNEGO towaru z ostatnich 30 dni
                    await using (var cmdSrednia = new SqlCommand(@"
                        SELECT AVG(dp.Cena) as SredniaCena
                        FROM [HANDEL].[HM].[DokHandlowe] d
                        JOIN [HANDEL].[HM].[DokHandlowe_Pozycje] dp ON d.Id = dp.IdDokument
                        WHERE dp.IdTowar = @IdTowar 
                        AND d.TypDokumentu IN ('WZ', 'FV')
                        AND d.DataWystawienia >= DATEADD(day, -30, GETDATE())
                        AND dp.Cena > 0", cn))
                    {
                        cmdSrednia.Parameters.AddWithValue("@IdTowar", wiersz.Id);
                        var resultSrednia = await cmdSrednia.ExecuteScalarAsync();
                        if (resultSrednia != null && resultSrednia != DBNull.Value)
                        {
                            wiersz.SredniaSprzedazy = Convert.ToDecimal(resultSrednia);
                        }
                    }

                    wiersz.UstawLadowanie(false);
                    
                    // Odśwież wyświetlanie marży
                    wiersz.CenaSprzedazy = wiersz.CenaSprzedazy;
                }

                AktualizujPodsumowanie();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd ładowania cen: {ex.Message}");
                // Spróbuj alternatywnego zapytania jeśli tabele nie istnieją
                await LoadCenyAlternatywneAsync();
            }
        }

        /// <summary>
        /// Alternatywna metoda pobierania cen (jeśli główne tabele nie istnieją)
        /// </summary>
        private async Task LoadCenyAlternatywneAsync()
        {
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                foreach (var wiersz in Wiersze)
                {
                    wiersz.UstawLadowanie(true);

                    // Spróbuj pobrać cenę z tabeli TW
                    await using (var cmd = new SqlCommand(@"
                        SELECT CenaZakupu, CenaSprzedazy 
                        FROM [HANDEL].[HM].[TW] 
                        WHERE Id = @Id", cn))
                    {
                        cmd.Parameters.AddWithValue("@Id", wiersz.Id);
                        await using var rd = await cmd.ExecuteReaderAsync();
                        if (await rd.ReadAsync())
                        {
                            if (!rd.IsDBNull(0))
                                wiersz.CenaZakupu = rd.GetDecimal(0);
                            if (!rd.IsDBNull(1))
                                wiersz.SredniaSprzedazy = rd.GetDecimal(1);
                        }
                    }

                    wiersz.UstawLadowanie(false);
                    wiersz.CenaSprzedazy = wiersz.CenaSprzedazy;
                }

                AktualizujPodsumowanie();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd alternatywnego ładowania: {ex.Message}");
            }
        }

        private void AktualizujPodsumowanie()
        {
            var wierszezCena = Wiersze.Where(w => w.CenaZakupu > 0 && w.CenaSprzedazy > 0).ToList();
            
            if (wierszezCena.Any())
            {
                decimal srednia = wierszezCena.Average(w => w.Marza);
                txtSredniaMarza.Text = $"{srednia:N1}%";
                txtSredniaMarza.Foreground = srednia < 5 
                    ? new SolidColorBrush(Color.FromRgb(245, 158, 11))
                    : new SolidColorBrush(Color.FromRgb(34, 197, 94));
            }
            else
            {
                txtSredniaMarza.Text = "-";
            }

            int bezCenyZakupu = Wiersze.Count(w => w.CenaZakupu == 0);
            txtBezCeny.Text = bezCenyZakupu > 0 ? $"({bezCenyZakupu} bez ceny zakupu)" : "";
        }

        private void TxtCena_TextChanged(object sender, TextChangedEventArgs e)
        {
            AktualizujPodsumowanie();
        }

        private void TxtCena_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c) && c != ',' && c != '.')
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        private void BtnUzyjSredniej_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MarzaWiersz wiersz)
            {
                if (wiersz.SredniaSprzedazy > 0)
                {
                    wiersz.CenaSprzedazy = wiersz.SredniaSprzedazy;
                    AktualizujPodsumowanie();
                }
            }
        }

        private void BtnDodajMarze_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MarzaWiersz wiersz)
            {
                if (wiersz.CenaZakupu > 0)
                {
                    // Dodaj 10% marży do ceny zakupu
                    wiersz.CenaSprzedazy = Math.Round(wiersz.CenaZakupu * 1.10m, 2);
                    AktualizujPodsumowanie();
                }
            }
        }

        private void BtnZastosuj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Zwraca listę towarów z nowymi cenami
        /// </summary>
        public List<TowarOfertaWiersz> PobierzTowaryZCenami()
        {
            return Wiersze.Select(w => new TowarOfertaWiersz
            {
                Id = w.Id,
                Kod = w.Kod,
                Nazwa = w.Nazwa,
                Ilosc = w.Ilosc,
                CenaJednostkowa = w.CenaSprzedazy
            }).ToList();
        }
    }
}
