using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1.OfertaCenowa
{
    /// <summary>
    /// Wiersz mapowania świeży-mrożony
    /// </summary>
    public class MapowanieWiersz : INotifyPropertyChanged
    {
        private TowarOferta? _wybranyMrozony;

        public int Lp { get; set; }
        public int IdSwiezy { get; set; }
        public string KodSwiezy { get; set; } = "";
        public string NazwaSwiezy { get; set; } = "";

        public TowarOferta? WybranyMrozony
        {
            get => _wybranyMrozony;
            set
            {
                _wybranyMrozony = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusBackground));
                OnPropertyChanged(nameof(StatusForeground));
            }
        }

        public string StatusText => WybranyMrozony != null ? "✓" : "—";
        public SolidColorBrush StatusBackground => WybranyMrozony != null 
            ? new SolidColorBrush(Color.FromRgb(220, 252, 231)) 
            : new SolidColorBrush(Color.FromRgb(243, 244, 246));
        public SolidColorBrush StatusForeground => WybranyMrozony != null 
            ? new SolidColorBrush(Color.FromRgb(22, 163, 74)) 
            : new SolidColorBrush(Color.FromRgb(156, 163, 175));

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Model danych mapowania do zapisu JSON
    /// </summary>
    public class MapowanieSwiezyMrozony
    {
        public int IdSwiezy { get; set; }
        public string KodSwiezy { get; set; } = "";
        public int IdMrozony { get; set; }
        public string KodMrozony { get; set; } = "";
    }

    /// <summary>
    /// Okno mapowania produktów świeży-mrożony
    /// </summary>
    public partial class MapowanieSwiezyMrozonyWindow : Window, INotifyPropertyChanged
    {
        private readonly string _plikMapowan;
        private List<MapowanieWiersz> _wszystkieMapowania = new();
        private List<MapowanieWiersz> _filtrowaneLista = new();

        public ObservableCollection<TowarOferta> TowaryMrozone { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public MapowanieSwiezyMrozonyWindow(
            IEnumerable<TowarOferta> towarySwiezy, 
            IEnumerable<TowarOferta> towaryMrozone)
        {
            InitializeComponent();
            DataContext = this;

            _plikMapowan = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OfertaHandlowa", "mapowania_swiezy_mrozony.json");

            // Wypełnij listę mrożonych
            foreach (var t in towaryMrozone.OrderBy(t => t.Kod))
            {
                TowaryMrozone.Add(t);
            }

            // Wczytaj istniejące mapowania
            var zapisaneMapowania = WczytajMapowania();

            // Utwórz wiersze dla każdego produktu świeżego
            int lp = 1;
            foreach (var swiezy in towarySwiezy.OrderBy(t => t.Kod))
            {
                var wiersz = new MapowanieWiersz
                {
                    Lp = lp++,
                    IdSwiezy = swiezy.Id,
                    KodSwiezy = swiezy.Kod,
                    NazwaSwiezy = swiezy.Nazwa
                };

                // Sprawdź czy jest zapisane mapowanie
                var mapowanie = zapisaneMapowania.FirstOrDefault(m => m.IdSwiezy == swiezy.Id);
                if (mapowanie != null)
                {
                    wiersz.WybranyMrozony = TowaryMrozone.FirstOrDefault(t => t.Id == mapowanie.IdMrozony);
                }

                _wszystkieMapowania.Add(wiersz);
            }

            OdswiezListe();
        }

        private void OdswiezListe()
        {
            string szukaj = txtSzukaj?.Text?.ToLower() ?? "";
            bool tylkoBez = chkTylkoBezMapowania?.IsChecked == true;

            _filtrowaneLista = _wszystkieMapowania
                .Where(m =>
                    (string.IsNullOrEmpty(szukaj) ||
                     m.KodSwiezy.ToLower().Contains(szukaj) ||
                     m.NazwaSwiezy.ToLower().Contains(szukaj)) &&
                    (!tylkoBez || m.WybranyMrozony == null))
                .ToList();

            // Przenumeruj
            int lp = 1;
            foreach (var m in _filtrowaneLista)
                m.Lp = lp++;

            icMapowania.ItemsSource = null;
            icMapowania.ItemsSource = _filtrowaneLista;

            // Statystyki
            int zmapowane = _wszystkieMapowania.Count(m => m.WybranyMrozony != null);
            int wszystkie = _wszystkieMapowania.Count;
            txtStatystyki.Text = $"Zmapowano: {zmapowane} / {wszystkie}";
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e) => OdswiezListe();
        private void ChkTylkoBezMapowania_Changed(object sender, RoutedEventArgs e) => OdswiezListe();

        private List<MapowanieSwiezyMrozony> WczytajMapowania()
        {
            try
            {
                if (File.Exists(_plikMapowan))
                {
                    string json = File.ReadAllText(_plikMapowan);
                    return JsonSerializer.Deserialize<List<MapowanieSwiezyMrozony>>(json) ?? new();
                }
            }
            catch { }
            return new();
        }

        private void ZapiszMapowania()
        {
            try
            {
                var folder = Path.GetDirectoryName(_plikMapowan);
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);

                var mapowania = _wszystkieMapowania
                    .Where(m => m.WybranyMrozony != null)
                    .Select(m => new MapowanieSwiezyMrozony
                    {
                        IdSwiezy = m.IdSwiezy,
                        KodSwiezy = m.KodSwiezy,
                        IdMrozony = m.WybranyMrozony!.Id,
                        KodMrozony = m.WybranyMrozony.Kod
                    })
                    .ToList();

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(mapowania, options);
                File.WriteAllText(_plikMapowan, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            ZapiszMapowania();
            int zmapowane = _wszystkieMapowania.Count(m => m.WybranyMrozony != null);
            MessageBox.Show($"Zapisano {zmapowane} mapowań.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Statyczna metoda do pobrania odpowiednika mrożonego
        /// </summary>
        public static int? PobierzIdMrozonego(int idSwiezego)
        {
            try
            {
                string plik = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "OfertaHandlowa", "mapowania_swiezy_mrozony.json");

                if (File.Exists(plik))
                {
                    string json = File.ReadAllText(plik);
                    var mapowania = JsonSerializer.Deserialize<List<MapowanieSwiezyMrozony>>(json);
                    return mapowania?.FirstOrDefault(m => m.IdSwiezy == idSwiezego)?.IdMrozony;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Statyczna metoda do pobrania odpowiednika świeżego
        /// </summary>
        public static int? PobierzIdSwiezego(int idMrozonego)
        {
            try
            {
                string plik = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "OfertaHandlowa", "mapowania_swiezy_mrozony.json");

                if (File.Exists(plik))
                {
                    string json = File.ReadAllText(plik);
                    var mapowania = JsonSerializer.Deserialize<List<MapowanieSwiezyMrozony>>(json);
                    return mapowania?.FirstOrDefault(m => m.IdMrozony == idMrozonego)?.IdSwiezy;
                }
            }
            catch { }
            return null;
        }
    }
}
