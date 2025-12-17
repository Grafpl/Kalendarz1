using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1.CRM
{
    public partial class DashboardCRMWindow : Window
    {
        private readonly string connectionString;
        private bool isLoaded = false;
        private double maxSzerokoscSlupka = 800;

        private readonly string[] kolory = new[]
        {
            "#10B981", "#3B82F6", "#F59E0B", "#EF4444", "#8B5CF6",
            "#EC4899", "#06B6D4", "#84CC16", "#F97316", "#6366F1",
            "#14B8A6", "#A855F7", "#22C55E", "#0EA5E9", "#FBBF24"
        };

        public DashboardCRMWindow(string connString)
        {
            connectionString = connString;
            InitializeComponent();
            InicjalizujKombo();
            Loaded += (s, e) => { isLoaded = true; WczytajDane(); };
            SizeChanged += (s, e) => { if (isLoaded) AktualizujSzerokoscSlupkow(); };
        }

        private void InicjalizujKombo()
        {
            int currentYear = DateTime.Today.Year;
            for (int y = currentYear; y >= currentYear - 3; y--)
                cmbRok.Items.Add(new ComboBoxItem { Content = y.ToString(), Tag = y });
            cmbRok.SelectedIndex = 0;

            cmbMiesiacOd.SelectedIndex = 0;
            cmbMiesiacDo.SelectedIndex = DateTime.Today.Month - 1;
        }

        private void CmbZakres_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isLoaded) return;
            WczytajDane();
        }

        private void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            WczytajDane();
        }

        private void WczytajDane()
        {
            try
            {
                if (cmbRok?.SelectedItem == null || cmbMiesiacOd?.SelectedItem == null || cmbMiesiacDo?.SelectedItem == null)
                    return;

                int rok = int.Parse(((ComboBoxItem)cmbRok.SelectedItem).Tag.ToString());
                int miesiacOd = int.Parse(((ComboBoxItem)cmbMiesiacOd.SelectedItem).Tag.ToString());
                int miesiacDo = int.Parse(((ComboBoxItem)cmbMiesiacDo.SelectedItem).Tag.ToString());
                if (miesiacDo < miesiacOd) miesiacDo = miesiacOd;

                var wykresOd = new DateTime(rok, miesiacOd, 1);
                var wykresDo = new DateTime(rok, miesiacDo, 1).AddMonths(1);

                var daneHandlowcow = PobierzDaneHandlowcow(wykresOd, wykresDo);
                WypelnijWykresSlupkowy(daneHandlowcow);
                WypelnijTabele(daneHandlowcow, miesiacOd, miesiacDo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Dictionary<string, DaneHandlowca> PobierzDaneHandlowcow(DateTime od, DateTime doo)
        {
            var wynik = new Dictionary<string, DaneHandlowca>();
            int liczbaMiesiecy = ((doo.Year - od.Year) * 12) + doo.Month - od.Month;
            if (liczbaMiesiecy <= 0) liczbaMiesiecy = 1;

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Pobierz zmiany statusow
                var cmd = new SqlCommand(@"
                    SELECT ISNULL(o.Name, h.KtoWykonal), MONTH(h.DataZmiany), COUNT(*)
                    FROM HistoriaZmianCRM h
                    LEFT JOIN operators o ON h.KtoWykonal = CAST(o.ID AS NVARCHAR)
                    WHERE h.TypZmiany = 'Zmiana statusu'
                    AND h.DataZmiany >= @od AND h.DataZmiany < @do
                    GROUP BY ISNULL(o.Name, h.KtoWykonal), MONTH(h.DataZmiany)", conn);
                cmd.Parameters.AddWithValue("@od", od);
                cmd.Parameters.AddWithValue("@do", doo);

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        string nazwa = r.IsDBNull(0) ? "?" : r.GetString(0);
                        int miesiac = r.GetInt32(1);
                        int cnt = r.GetInt32(2);

                        if (!wynik.ContainsKey(nazwa))
                            wynik[nazwa] = new DaneHandlowca { Nazwa = nazwa, DaneMiesieczne = new int[12] };

                        if (miesiac >= 1 && miesiac <= 12)
                            wynik[nazwa].DaneMiesieczne[miesiac - 1] += cnt;
                    }
                }

                // Pobierz notatki
                var cmdN = new SqlCommand(@"
                    SELECT ISNULL(o.Name, n.KtoDodal), MONTH(n.DataUtworzenia), COUNT(*)
                    FROM NotatkiCRM n
                    LEFT JOIN operators o ON n.KtoDodal = CAST(o.ID AS NVARCHAR)
                    WHERE n.DataUtworzenia >= @od AND n.DataUtworzenia < @do
                    GROUP BY ISNULL(o.Name, n.KtoDodal), MONTH(n.DataUtworzenia)", conn);
                cmdN.Parameters.AddWithValue("@od", od);
                cmdN.Parameters.AddWithValue("@do", doo);

                using (var r = cmdN.ExecuteReader())
                {
                    while (r.Read())
                    {
                        string nazwa = r.IsDBNull(0) ? "?" : r.GetString(0);
                        int miesiac = r.GetInt32(1);
                        int cnt = r.GetInt32(2);

                        if (!wynik.ContainsKey(nazwa))
                            wynik[nazwa] = new DaneHandlowca { Nazwa = nazwa, DaneMiesieczne = new int[12] };

                        if (miesiac >= 1 && miesiac <= 12)
                            wynik[nazwa].DaneMiesieczne[miesiac - 1] += cnt;
                    }
                }
            }

            // Oblicz sumy
            foreach (var h in wynik.Values)
                h.Suma = h.DaneMiesieczne.Sum();

            return wynik;
        }

        private void WypelnijWykresSlupkowy(Dictionary<string, DaneHandlowca> dane)
        {
            var aktywni = dane.Values.Where(x => x.Suma > 0).OrderByDescending(x => x.Suma).ToList();
            if (aktywni.Count == 0)
            {
                wykresSlupkowy.ItemsSource = null;
                return;
            }

            int maxWartosc = aktywni.Max(x => x.Suma);
            if (maxWartosc == 0) maxWartosc = 1;

            // Oblicz szerokosc slupka
            maxSzerokoscSlupka = Math.Max(200, ActualWidth - 350);

            var listaSlupkow = new List<SlupekWykresu>();
            int kolorIndex = 0;

            foreach (var h in aktywni)
            {
                string kolorHex = kolory[kolorIndex % kolory.Length];
                var kolor = (Color)ColorConverter.ConvertFromString(kolorHex);
                double szerokoscProcent = (double)h.Suma / maxWartosc;
                double szerokosc = szerokoscProcent * maxSzerokoscSlupka;
                if (szerokosc < 20) szerokosc = 20;

                listaSlupkow.Add(new SlupekWykresu
                {
                    Nazwa = h.Nazwa,
                    Suma = h.Suma,
                    SzerokoscSlupka = szerokosc,
                    Kolor = new SolidColorBrush(kolor),
                    KolorCien = kolor
                });

                kolorIndex++;
            }

            wykresSlupkowy.ItemsSource = listaSlupkow;
        }

        private void AktualizujSzerokoscSlupkow()
        {
            if (wykresSlupkowy.ItemsSource == null) return;
            var lista = wykresSlupkowy.ItemsSource as List<SlupekWykresu>;
            if (lista == null || lista.Count == 0) return;

            int maxWartosc = lista.Max(x => x.Suma);
            if (maxWartosc == 0) maxWartosc = 1;

            maxSzerokoscSlupka = Math.Max(200, ActualWidth - 350);

            foreach (var s in lista)
            {
                double szerokoscProcent = (double)s.Suma / maxWartosc;
                s.SzerokoscSlupka = Math.Max(20, szerokoscProcent * maxSzerokoscSlupka);
            }

            wykresSlupkowy.ItemsSource = null;
            wykresSlupkowy.ItemsSource = lista;
        }

        private void WypelnijTabele(Dictionary<string, DaneHandlowca> dane, int miesiacOd, int miesiacDo)
        {
            if (tabelaDane == null) return;

            var aktywni = dane.Values.Where(x => x.Suma > 0).OrderByDescending(x => x.Suma).ToList();
            var nazwyMies = new[] { "Sty", "Lut", "Mar", "Kwi", "Maj", "Cze", "Lip", "Sie", "Wrz", "Paz", "Lis", "Gru" };

            // Usun stare kolumny (oprocz pierwszej - Handlowiec)
            while (tabelaDane.Columns.Count > 1)
                tabelaDane.Columns.RemoveAt(1);

            // Dodaj kolumny dla miesiecy
            for (int m = miesiacOd - 1; m < miesiacDo; m++)
            {
                var col = new DataGridTextColumn
                {
                    Header = nazwyMies[m],
                    Binding = new System.Windows.Data.Binding($"DaneMiesieczne[{m}]"),
                    Width = 55
                };
                col.ElementStyle = new Style(typeof(TextBlock))
                {
                    Setters = {
                        new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center),
                        new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center)
                    }
                };
                tabelaDane.Columns.Add(col);
            }

            // Dodaj kolumne SUMA
            var colSuma = new DataGridTextColumn
            {
                Header = "SUMA",
                Binding = new System.Windows.Data.Binding("Suma"),
                Width = 70
            };
            colSuma.ElementStyle = new Style(typeof(TextBlock))
            {
                Setters = {
                    new Setter(TextBlock.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"))),
                    new Setter(TextBlock.FontWeightProperty, FontWeights.Bold),
                    new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center),
                    new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center)
                }
            };
            tabelaDane.Columns.Add(colSuma);

            tabelaDane.ItemsSource = aktywni;
        }
    }

    public class DaneHandlowca
    {
        public string Nazwa { get; set; }
        public int[] DaneMiesieczne { get; set; } = new int[12];
        public int Suma { get; set; }
    }

    public class SlupekWykresu
    {
        public string Nazwa { get; set; }
        public int Suma { get; set; }
        public double SzerokoscSlupka { get; set; }
        public SolidColorBrush Kolor { get; set; }
        public Color KolorCien { get; set; }
    }
}
