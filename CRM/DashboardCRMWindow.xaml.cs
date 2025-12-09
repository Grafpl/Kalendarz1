using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1.CRM
{
    public partial class DashboardCRMWindow : Window
    {
        private readonly string connectionString;
        private int okresDni = 7;

        public DashboardCRMWindow(string connString)
        {
            connectionString = connString;
            InitializeComponent();
            Loaded += (s, e) => WczytajDane();
        }

        private void CmbOkres_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbOkres.SelectedIndex < 0) return;

            switch (cmbOkres.SelectedIndex)
            {
                case 0: okresDni = 7; break;
                case 1: okresDni = 14; break;
                case 2: okresDni = 30; break;
                case 3: okresDni = DateTime.Now.Day; break; // Ten miesiąc
                case 4: okresDni = DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month - 1); break; // Poprzedni
            }
            WczytajDane();
        }

        private void BtnOdswiez_Click(object sender, RoutedEventArgs e) => WczytajDane();

        private void WczytajDane()
        {
            try
            {
                WczytajKPI();
                WczytajWykresAktywnosci();
                WczytajRozkladStatusow();
                WczytajPorownanieMiesiecy();
                WczytajTopHandlowcow();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WczytajKPI()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Wszystkie kontakty
                var cmdAll = new SqlCommand("SELECT COUNT(*) FROM OdbiorcyCRM WHERE ISNULL(Status, '') NOT IN ('Poprosił o usunięcie', 'Błędny rekord (do raportu)')", conn);
                int wszystkie = (int)cmdAll.ExecuteScalar();
                txtKpiWszystkie.Text = wszystkie.ToString("N0");

                // Nowe w okresie
                var cmdNowe = new SqlCommand($@"SELECT COUNT(*) FROM HistoriaZmianCRM
                    WHERE TypZmiany = 'Utworzenie kontaktu' AND DataZmiany > DATEADD(day, -{okresDni}, GETDATE())", conn);
                int nowe = (int)cmdNowe.ExecuteScalar();
                txtKpiWszystkieTrend.Text = $"+{nowe} w tym okresie";

                // Wykonane akcje w okresie
                var cmdAkcje = new SqlCommand($@"SELECT COUNT(*) FROM HistoriaZmianCRM
                    WHERE DataZmiany > DATEADD(day, -{okresDni}, GETDATE())", conn);
                int akcje = (int)cmdAkcje.ExecuteScalar();
                txtKpiAkcje.Text = akcje.ToString("N0");
                double srednia = okresDni > 0 ? (double)akcje / okresDni : 0;
                txtKpiAkcjeSrednia.Text = $"śr. {srednia:F1}/dzień";

                // Skuteczność (pozytywne statusy / wszystkie zmiany statusów)
                var cmdSkut = new SqlCommand($@"
                    SELECT
                        SUM(CASE WHEN WartoscNowa IN ('Nawiązano kontakt', 'Zgoda na dalszy kontakt', 'Do wysłania oferta') THEN 1 ELSE 0 END) as Pozytywne,
                        COUNT(*) as Wszystkie
                    FROM HistoriaZmianCRM
                    WHERE TypZmiany = 'Zmiana statusu' AND DataZmiany > DATEADD(day, -{okresDni}, GETDATE())", conn);
                using (var reader = cmdSkut.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        int pozytywne = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                        int wszystkieZmiany = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        double skutecznosc = wszystkieZmiany > 0 ? (double)pozytywne / wszystkieZmiany * 100 : 0;
                        txtKpiSkutecznosc.Text = $"{skutecznosc:F0}%";
                    }
                }

                // Otwarte oferty
                var cmdOferty = new SqlCommand("SELECT COUNT(*) FROM OdbiorcyCRM WHERE Status = 'Do wysłania oferta'", conn);
                int oferty = (int)cmdOferty.ExecuteScalar();
                txtKpiOferty.Text = oferty.ToString();

                // Zaniedbane (>30 dni bez aktywności)
                var cmdZaniedbane = new SqlCommand(@"
                    SELECT COUNT(*) FROM OdbiorcyCRM o
                    WHERE ISNULL(o.Status, '') NOT IN ('Poprosił o usunięcie', 'Błędny rekord (do raportu)', 'Nie zainteresowany')
                    AND NOT EXISTS (
                        SELECT 1 FROM HistoriaZmianCRM h
                        WHERE h.IDOdbiorcy = o.ID AND h.DataZmiany > DATEADD(day, -30, GETDATE())
                    )", conn);
                int zaniedbane = (int)cmdZaniedbane.ExecuteScalar();
                txtKpiZaniedbane.Text = zaniedbane.ToString();
            }
        }

        private void WczytajWykresAktywnosci()
        {
            var dane = new List<DzienAktywnosci>();
            double maxWartosc = 1;

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand($@"
                    SELECT CAST(DataZmiany AS DATE) as Dzien, COUNT(*) as Liczba
                    FROM HistoriaZmianCRM
                    WHERE DataZmiany > DATEADD(day, -{okresDni}, GETDATE())
                    GROUP BY CAST(DataZmiany AS DATE)
                    ORDER BY Dzien", conn);

                var wyniki = new Dictionary<DateTime, int>();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        wyniki[reader.GetDateTime(0)] = reader.GetInt32(1);
                    }
                }

                // Uzupełnij wszystkie dni
                for (int i = okresDni - 1; i >= 0; i--)
                {
                    var dzien = DateTime.Today.AddDays(-i);
                    int wartosc = wyniki.ContainsKey(dzien) ? wyniki[dzien] : 0;
                    if (wartosc > maxWartosc) maxWartosc = wartosc;
                    dane.Add(new DzienAktywnosci
                    {
                        Data = dzien.ToString("dd.MM"),
                        Etykieta = dzien.ToString("ddd").Substring(0, 2),
                        Wartosc = wartosc,
                        Tooltip = $"Akcji: {wartosc}"
                    });
                }
            }

            // Oblicz wysokości słupków (max 150px)
            foreach (var d in dane)
            {
                d.Wysokosc = maxWartosc > 0 ? (d.Wartosc / maxWartosc) * 150 : 0;
                d.Kolor = d.Wartosc > 0
                    ? new SolidColorBrush(Color.FromRgb(22, 163, 74))
                    : new SolidColorBrush(Color.FromRgb(226, 232, 240));
            }

            wykresAktywnosci.ItemsSource = dane;
        }

        private void WczytajRozkladStatusow()
        {
            var statusy = new List<StatusInfo>();
            int suma = 0;

            var definicje = new Dictionary<string, (string ikona, string kolor)>
            {
                { "Do zadzwonienia", ("📞", "#64748B") },
                { "Próba kontaktu", ("⏳", "#F97316") },
                { "Nawiązano kontakt", ("✅", "#16A34A") },
                { "Zgoda na dalszy kontakt", ("🤝", "#14B8A6") },
                { "Do wysłania oferta", ("📄", "#2563EB") },
                { "Nie zainteresowany", ("❌", "#DC2626") }
            };

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT ISNULL(Status, 'Do zadzwonienia') as Status, COUNT(*) as Liczba
                    FROM OdbiorcyCRM
                    WHERE ISNULL(Status, '') NOT IN ('Poprosił o usunięcie', 'Błędny rekord (do raportu)')
                    GROUP BY Status
                    ORDER BY COUNT(*) DESC", conn);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string status = reader.GetString(0);
                        int liczba = reader.GetInt32(1);
                        suma += liczba;

                        string ikona = "📋";
                        string kolor = "#94A3B8";
                        if (definicje.ContainsKey(status))
                        {
                            ikona = definicje[status].Item1;
                            kolor = definicje[status].Item2;
                        }

                        statusy.Add(new StatusInfo
                        {
                            Nazwa = status,
                            Ikona = ikona,
                            Liczba = liczba,
                            KolorPaska = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kolor))
                        });
                    }
                }
            }

            // Oblicz procenty i szerokości
            foreach (var s in statusy)
            {
                s.Procent = suma > 0 ? (int)Math.Round((double)s.Liczba / suma * 100) : 0;
                s.SzerokoscPaska = suma > 0 ? (double)s.Liczba / suma * 200 : 0; // max 200px
            }

            listaStatusow.ItemsSource = statusy;
        }

        private void WczytajPorownanieMiesiecy()
        {
            var miesiace = new List<MiesiacInfo>();
            int poprzednia = 0;

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT YEAR(DataZmiany) as Rok, MONTH(DataZmiany) as Miesiac, COUNT(*) as Liczba
                    FROM HistoriaZmianCRM
                    WHERE DataZmiany > DATEADD(month, -6, GETDATE())
                    GROUP BY YEAR(DataZmiany), MONTH(DataZmiany)
                    ORDER BY Rok, Miesiac", conn);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int rok = reader.GetInt32(0);
                        int miesiac = reader.GetInt32(1);
                        int liczba = reader.GetInt32(2);

                        int zmiana = poprzednia > 0 ? (int)Math.Round((double)(liczba - poprzednia) / poprzednia * 100) : 0;
                        string trend = zmiana >= 0 ? $"+{zmiana}%" : $"{zmiana}%";

                        miesiace.Add(new MiesiacInfo
                        {
                            NazwaMiesiaca = new DateTime(rok, miesiac, 1).ToString("MMM"),
                            LiczbaAkcji = liczba,
                            Trend = poprzednia == 0 ? "-" : trend,
                            KolorTrendu = zmiana >= 0
                                ? new SolidColorBrush(Color.FromRgb(22, 163, 74))
                                : new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                            TloKarty = DateTime.Now.Month == miesiac && DateTime.Now.Year == rok
                                ? new SolidColorBrush(Color.FromRgb(220, 252, 231))
                                : new SolidColorBrush(Color.FromRgb(248, 250, 252))
                        });

                        poprzednia = liczba;
                    }
                }
            }

            listaMiesiace.ItemsSource = miesiace;
        }

        private void WczytajTopHandlowcow()
        {
            var handlowcy = new List<HandlowiecInfo>();
            var koloryPozycji = new[] { "#FFD700", "#C0C0C0", "#CD7F32", "#16A34A", "#16A34A" };

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand($@"
                    SELECT TOP 5
                        ISNULL(o.Name, h.KtoWykonal) as Handlowiec,
                        COUNT(*) as LiczbaAkcji,
                        SUM(CASE WHEN WartoscNowa IN ('Nawiązano kontakt', 'Zgoda na dalszy kontakt', 'Do wysłania oferta') THEN 1 ELSE 0 END) as Pozytywne
                    FROM HistoriaZmianCRM h
                    LEFT JOIN operators o ON h.KtoWykonal = CAST(o.ID AS NVARCHAR)
                    WHERE h.DataZmiany > DATEADD(day, -{okresDni}, GETDATE())
                    GROUP BY h.KtoWykonal, o.Name
                    ORDER BY COUNT(*) DESC", conn);

                int pozycja = 1;
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string nazwa = reader.IsDBNull(0) ? "Nieznany" : reader.GetString(0);
                        int akcje = reader.GetInt32(1);
                        int pozytywne = reader.GetInt32(2);
                        int skutecznosc = akcje > 0 ? (int)Math.Round((double)pozytywne / akcje * 100) : 0;

                        handlowcy.Add(new HandlowiecInfo
                        {
                            Pozycja = pozycja,
                            Nazwa = nazwa,
                            LiczbaAkcji = akcje,
                            Skutecznosc = skutecznosc,
                            KolorPozycji = new SolidColorBrush((Color)ColorConverter.ConvertFromString(koloryPozycji[Math.Min(pozycja - 1, 4)]))
                        });
                        pozycja++;
                    }
                }
            }

            listaTopHandlowcy.ItemsSource = handlowcy;
        }
    }

    // Klasy pomocnicze
    public class DzienAktywnosci
    {
        public string Data { get; set; }
        public string Etykieta { get; set; }
        public int Wartosc { get; set; }
        public double Wysokosc { get; set; }
        public string Tooltip { get; set; }
        public SolidColorBrush Kolor { get; set; }
    }

    public class StatusInfo
    {
        public string Nazwa { get; set; }
        public string Ikona { get; set; }
        public int Liczba { get; set; }
        public int Procent { get; set; }
        public double SzerokoscPaska { get; set; }
        public SolidColorBrush KolorPaska { get; set; }
    }

    public class MiesiacInfo
    {
        public string NazwaMiesiaca { get; set; }
        public int LiczbaAkcji { get; set; }
        public string Trend { get; set; }
        public SolidColorBrush KolorTrendu { get; set; }
        public SolidColorBrush TloKarty { get; set; }
    }

    public class HandlowiecInfo
    {
        public int Pozycja { get; set; }
        public string Nazwa { get; set; }
        public int LiczbaAkcji { get; set; }
        public int Skutecznosc { get; set; }
        public SolidColorBrush KolorPozycji { get; set; }
    }
}
