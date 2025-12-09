using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1.CRM
{
    public partial class DashboardCRMWindow : Window
    {
        private readonly string connectionString;
        private int okresDni = 0; // 0 = dzisiaj
        private DateTime dataOd;
        private DateTime dataDo;
        private bool isLoaded = false;

        public DashboardCRMWindow(string connString)
        {
            connectionString = connString;
            InitializeComponent();
            Loaded += (s, e) => { isLoaded = true; UstawOkres(); WczytajDane(); };
        }

        private void CmbOkres_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isLoaded || cmbOkres == null || cmbOkres.SelectedIndex < 0) return;
            UstawOkres();
            WczytajDane();
        }

        private void UstawOkres()
        {
            dataDo = DateTime.Today.AddDays(1); // Do jutra (włącznie z dzisiejszym dniem)
            string okresNazwa = "Dzisiaj";

            switch (cmbOkres.SelectedIndex)
            {
                case 0: // Dzisiaj
                    dataOd = DateTime.Today;
                    okresDni = 1;
                    okresNazwa = "Dzisiaj";
                    break;
                case 1: // 7 dni
                    dataOd = DateTime.Today.AddDays(-6);
                    okresDni = 7;
                    okresNazwa = "Ostatnie 7 dni";
                    break;
                case 2: // 14 dni
                    dataOd = DateTime.Today.AddDays(-13);
                    okresDni = 14;
                    okresNazwa = "Ostatnie 14 dni";
                    break;
                case 3: // 30 dni
                    dataOd = DateTime.Today.AddDays(-29);
                    okresDni = 30;
                    okresNazwa = "Ostatnie 30 dni";
                    break;
                case 4: // Ten miesiąc
                    dataOd = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    okresDni = DateTime.Today.Day;
                    okresNazwa = "Ten miesiąc";
                    break;
                case 5: // Poprzedni miesiąc
                    var prev = DateTime.Today.AddMonths(-1);
                    dataOd = new DateTime(prev.Year, prev.Month, 1);
                    dataDo = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    okresDni = DateTime.DaysInMonth(prev.Year, prev.Month);
                    okresNazwa = "Poprzedni miesiąc";
                    break;
            }

            if (txtWykresOkres != null)
                txtWykresOkres.Text = $" ({okresNazwa})";
        }

        private void BtnOdswiez_Click(object sender, RoutedEventArgs e) => WczytajDane();

        private void WczytajDane()
        {
            try
            {
                WczytajStatystykiGlobalne();
                WczytajWykresHandlowcow();
                WczytajAktywnoscPoDniach();
                WczytajTopDnia();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WczytajStatystykiGlobalne()
        {
            if (txtTelefonyTotal == null) return;

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Telefony = zmiany statusu na 'Próba kontaktu' lub 'Nawiązano kontakt'
                var cmdTelefony = new SqlCommand(@"
                    SELECT COUNT(*) FROM HistoriaZmianCRM
                    WHERE TypZmiany = 'Zmiana statusu'
                    AND WartoscNowa IN ('Próba kontaktu', 'Nawiązano kontakt')
                    AND DataZmiany >= @dataOd AND DataZmiany < @dataDo", conn);
                cmdTelefony.Parameters.AddWithValue("@dataOd", dataOd);
                cmdTelefony.Parameters.AddWithValue("@dataDo", dataDo);
                int telefony = (int)cmdTelefony.ExecuteScalar();
                txtTelefonyTotal.Text = telefony.ToString();
                if (txtTelefonyInfo != null)
                    txtTelefonyInfo.Text = okresDni == 1 ? "wykonanych połączeń" : $"śr. {(telefony / (double)okresDni):F1}/dzień";

                // Zmiany statusów (wszystkie oprócz telefonów)
                var cmdStatusy = new SqlCommand(@"
                    SELECT COUNT(*) FROM HistoriaZmianCRM
                    WHERE TypZmiany = 'Zmiana statusu'
                    AND WartoscNowa NOT IN ('Próba kontaktu', 'Nawiązano kontakt', 'Do zadzwonienia')
                    AND DataZmiany >= @dataOd AND DataZmiany < @dataDo", conn);
                cmdStatusy.Parameters.AddWithValue("@dataOd", dataOd);
                cmdStatusy.Parameters.AddWithValue("@dataDo", dataDo);
                int statusy = (int)cmdStatusy.ExecuteScalar();
                if (txtStatusyTotal != null) txtStatusyTotal.Text = statusy.ToString();
                if (txtStatusyInfo != null)
                    txtStatusyInfo.Text = okresDni == 1 ? "zmian statusów" : $"śr. {(statusy / (double)okresDni):F1}/dzień";

                // Notatki
                var cmdNotatki = new SqlCommand(@"
                    SELECT COUNT(*) FROM NotatkiCRM
                    WHERE DataUtworzenia >= @dataOd AND DataUtworzenia < @dataDo", conn);
                cmdNotatki.Parameters.AddWithValue("@dataOd", dataOd);
                cmdNotatki.Parameters.AddWithValue("@dataDo", dataDo);
                int notatki = (int)cmdNotatki.ExecuteScalar();
                if (txtNotatkiTotal != null) txtNotatkiTotal.Text = notatki.ToString();
                if (txtNotatkiInfo != null)
                    txtNotatkiInfo.Text = okresDni == 1 ? "dodanych notatek" : $"śr. {(notatki / (double)okresDni):F1}/dzień";

                // Suma
                int suma = telefony + statusy + notatki;
                if (txtAktywnoscTotal != null) txtAktywnoscTotal.Text = suma.ToString();
                if (txtAktywnoscInfo != null)
                    txtAktywnoscInfo.Text = okresDni == 1 ? "wszystkich akcji" : $"śr. {(suma / (double)okresDni):F1}/dzień";
            }
        }

        private void WczytajWykresHandlowcow()
        {
            var handlowcy = new List<HandlowiecWykres>();
            int maxSuma = 1;

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Pobierz dane per handlowiec
                var cmd = new SqlCommand(@"
                    SELECT
                        ISNULL(o.Name, h.KtoWykonal) as Handlowiec,
                        SUM(CASE WHEN h.WartoscNowa IN ('Próba kontaktu', 'Nawiązano kontakt') THEN 1 ELSE 0 END) as Telefony,
                        SUM(CASE WHEN h.WartoscNowa NOT IN ('Próba kontaktu', 'Nawiązano kontakt', 'Do zadzwonienia') THEN 1 ELSE 0 END) as Statusy
                    FROM HistoriaZmianCRM h
                    LEFT JOIN operators o ON h.KtoWykonal = CAST(o.ID AS NVARCHAR)
                    WHERE h.TypZmiany = 'Zmiana statusu'
                    AND h.DataZmiany >= @dataOd AND h.DataZmiany < @dataDo
                    GROUP BY h.KtoWykonal, o.Name
                    ORDER BY SUM(CASE WHEN h.WartoscNowa IN ('Próba kontaktu', 'Nawiązano kontakt') THEN 1 ELSE 0 END) +
                             SUM(CASE WHEN h.WartoscNowa NOT IN ('Próba kontaktu', 'Nawiązano kontakt', 'Do zadzwonienia') THEN 1 ELSE 0 END) DESC", conn);
                cmd.Parameters.AddWithValue("@dataOd", dataOd);
                cmd.Parameters.AddWithValue("@dataDo", dataDo);

                var daneHistoria = new Dictionary<string, (int telefony, int statusy)>();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string nazwa = reader.IsDBNull(0) ? "Nieznany" : reader.GetString(0);
                        int tel = reader.GetInt32(1);
                        int stat = reader.GetInt32(2);
                        daneHistoria[nazwa] = (tel, stat);
                    }
                }

                // Pobierz notatki per handlowiec
                var cmdNotatki = new SqlCommand(@"
                    SELECT
                        ISNULL(o.Name, n.KtoDodal) as Handlowiec,
                        COUNT(*) as Notatki
                    FROM NotatkiCRM n
                    LEFT JOIN operators o ON n.KtoDodal = CAST(o.ID AS NVARCHAR)
                    WHERE n.DataUtworzenia >= @dataOd AND n.DataUtworzenia < @dataDo
                    GROUP BY n.KtoDodal, o.Name", conn);
                cmdNotatki.Parameters.AddWithValue("@dataOd", dataOd);
                cmdNotatki.Parameters.AddWithValue("@dataDo", dataDo);

                var daneNotatki = new Dictionary<string, int>();
                using (var reader = cmdNotatki.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string nazwa = reader.IsDBNull(0) ? "Nieznany" : reader.GetString(0);
                        int notatki = reader.GetInt32(1);
                        daneNotatki[nazwa] = notatki;
                    }
                }

                // Połącz dane
                var wszystkieNazwy = daneHistoria.Keys.Union(daneNotatki.Keys).Distinct();
                foreach (var nazwa in wszystkieNazwy)
                {
                    int tel = daneHistoria.ContainsKey(nazwa) ? daneHistoria[nazwa].telefony : 0;
                    int stat = daneHistoria.ContainsKey(nazwa) ? daneHistoria[nazwa].statusy : 0;
                    int not = daneNotatki.ContainsKey(nazwa) ? daneNotatki[nazwa] : 0;
                    int suma = tel + stat + not;

                    if (suma > 0)
                    {
                        handlowcy.Add(new HandlowiecWykres
                        {
                            Nazwa = nazwa,
                            Telefony = tel,
                            Statusy = stat,
                            Notatki = not,
                            Suma = suma
                        });
                        if (suma > maxSuma) maxSuma = suma;
                    }
                }
            }

            // Posortuj i dodaj pozycje
            handlowcy = handlowcy.OrderByDescending(h => h.Suma).ToList();
            var koloryPozycji = new[] { "#FFD700", "#C0C0C0", "#CD7F32", "#16A34A", "#16A34A", "#64748B", "#64748B", "#64748B", "#64748B", "#64748B" };

            for (int i = 0; i < handlowcy.Count; i++)
            {
                var h = handlowcy[i];
                h.Pozycja = i + 1;
                h.KolorPozycji = new SolidColorBrush((Color)ColorConverter.ConvertFromString(koloryPozycji[Math.Min(i, koloryPozycji.Length - 1)]));

                // Oblicz szerokości słupków (max 400px łącznie)
                double skala = 400.0 / maxSuma;
                h.SzerokoscTelefony = Math.Max(h.Telefony * skala, h.Telefony > 0 ? 3 : 0);
                h.SzerokoscStatusy = Math.Max(h.Statusy * skala, h.Statusy > 0 ? 3 : 0);
                h.SzerokoscNotatki = Math.Max(h.Notatki * skala, h.Notatki > 0 ? 3 : 0);

                h.TooltipTelefony = $"📞 Telefony: {h.Telefony}";
                h.TooltipStatusy = $"🔄 Zmiany statusów: {h.Statusy}";
                h.TooltipNotatki = $"📝 Notatki: {h.Notatki}";
            }

            listaHandlowcyWykres.ItemsSource = handlowcy;
        }

        private void WczytajAktywnoscPoDniach()
        {
            var dni = new List<DzienAktywnosc>();

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Telefony i statusy po dniach
                var cmdHistoria = new SqlCommand(@"
                    SELECT
                        CAST(DataZmiany AS DATE) as Dzien,
                        SUM(CASE WHEN WartoscNowa IN ('Próba kontaktu', 'Nawiązano kontakt') THEN 1 ELSE 0 END) as Telefony,
                        SUM(CASE WHEN WartoscNowa NOT IN ('Próba kontaktu', 'Nawiązano kontakt', 'Do zadzwonienia') THEN 1 ELSE 0 END) as Statusy
                    FROM HistoriaZmianCRM
                    WHERE TypZmiany = 'Zmiana statusu'
                    AND DataZmiany >= @dataOd AND DataZmiany < @dataDo
                    GROUP BY CAST(DataZmiany AS DATE)", conn);
                cmdHistoria.Parameters.AddWithValue("@dataOd", dataOd);
                cmdHistoria.Parameters.AddWithValue("@dataDo", dataDo);

                var daneHistoria = new Dictionary<DateTime, (int tel, int stat)>();
                using (var reader = cmdHistoria.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var dzien = reader.GetDateTime(0);
                        daneHistoria[dzien] = (reader.GetInt32(1), reader.GetInt32(2));
                    }
                }

                // Notatki po dniach
                var cmdNotatki = new SqlCommand(@"
                    SELECT CAST(DataUtworzenia AS DATE) as Dzien, COUNT(*) as Notatki
                    FROM NotatkiCRM
                    WHERE DataUtworzenia >= @dataOd AND DataUtworzenia < @dataDo
                    GROUP BY CAST(DataUtworzenia AS DATE)", conn);
                cmdNotatki.Parameters.AddWithValue("@dataOd", dataOd);
                cmdNotatki.Parameters.AddWithValue("@dataDo", dataDo);

                var daneNotatki = new Dictionary<DateTime, int>();
                using (var reader = cmdNotatki.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var dzien = reader.GetDateTime(0);
                        daneNotatki[dzien] = reader.GetInt32(1);
                    }
                }

                // Uzupełnij wszystkie dni w okresie
                var culture = new CultureInfo("pl-PL");
                for (var dzien = dataOd; dzien < dataDo; dzien = dzien.AddDays(1))
                {
                    int tel = daneHistoria.ContainsKey(dzien) ? daneHistoria[dzien].tel : 0;
                    int stat = daneHistoria.ContainsKey(dzien) ? daneHistoria[dzien].stat : 0;
                    int not = daneNotatki.ContainsKey(dzien) ? daneNotatki[dzien] : 0;

                    bool czyDzisiaj = dzien == DateTime.Today;

                    dni.Add(new DzienAktywnosc
                    {
                        DzienNazwa = culture.DateTimeFormat.GetDayName(dzien.DayOfWeek),
                        Data = dzien.ToString("dd.MM.yyyy"),
                        Telefony = tel,
                        Statusy = stat,
                        Notatki = not,
                        TloKarty = czyDzisiaj
                            ? new SolidColorBrush(Color.FromRgb(220, 252, 231))
                            : new SolidColorBrush(Color.FromRgb(248, 250, 252))
                    });
                }
            }

            // Odwróć kolejność - najnowsze na górze
            dni.Reverse();
            listaAktywnoscDni.ItemsSource = dni;
        }

        private void WczytajTopDnia()
        {
            var top = new List<TopHandlowiec>();

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Tylko dzisiejsze dane dla podium
                var dzisiajOd = DateTime.Today;
                var dzisiajDo = DateTime.Today.AddDays(1);

                var cmd = new SqlCommand(@"
                    SELECT TOP 3
                        ISNULL(o.Name, h.KtoWykonal) as Handlowiec,
                        SUM(CASE WHEN h.WartoscNowa IN ('Próba kontaktu', 'Nawiązano kontakt') THEN 1 ELSE 0 END) as Telefony,
                        SUM(CASE WHEN h.WartoscNowa NOT IN ('Próba kontaktu', 'Nawiązano kontakt', 'Do zadzwonienia') THEN 1 ELSE 0 END) as Statusy
                    FROM HistoriaZmianCRM h
                    LEFT JOIN operators o ON h.KtoWykonal = CAST(o.ID AS NVARCHAR)
                    WHERE h.TypZmiany = 'Zmiana statusu'
                    AND h.DataZmiany >= @dataOd AND h.DataZmiany < @dataDo
                    GROUP BY h.KtoWykonal, o.Name
                    ORDER BY SUM(CASE WHEN h.WartoscNowa IN ('Próba kontaktu', 'Nawiązano kontakt') THEN 1 ELSE 0 END) +
                             SUM(CASE WHEN h.WartoscNowa NOT IN ('Próba kontaktu', 'Nawiązano kontakt', 'Do zadzwonienia') THEN 1 ELSE 0 END) DESC", conn);
                cmd.Parameters.AddWithValue("@dataOd", dzisiajOd);
                cmd.Parameters.AddWithValue("@dataDo", dzisiajDo);

                var daneHistoria = new Dictionary<string, (int tel, int stat)>();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string nazwa = reader.IsDBNull(0) ? "Nieznany" : reader.GetString(0);
                        daneHistoria[nazwa] = (reader.GetInt32(1), reader.GetInt32(2));
                    }
                }

                // Notatki dzisiaj
                var cmdNotatki = new SqlCommand(@"
                    SELECT ISNULL(o.Name, n.KtoDodal) as Handlowiec, COUNT(*) as Notatki
                    FROM NotatkiCRM n
                    LEFT JOIN operators o ON n.KtoDodal = CAST(o.ID AS NVARCHAR)
                    WHERE n.DataUtworzenia >= @dataOd AND n.DataUtworzenia < @dataDo
                    GROUP BY n.KtoDodal, o.Name", conn);
                cmdNotatki.Parameters.AddWithValue("@dataOd", dzisiajOd);
                cmdNotatki.Parameters.AddWithValue("@dataDo", dzisiajDo);

                var daneNotatki = new Dictionary<string, int>();
                using (var reader = cmdNotatki.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string nazwa = reader.IsDBNull(0) ? "Nieznany" : reader.GetString(0);
                        daneNotatki[nazwa] = reader.GetInt32(1);
                    }
                }

                // Połącz i posortuj
                var wszystkie = new List<TopHandlowiec>();
                var nazwy = daneHistoria.Keys.Union(daneNotatki.Keys).Distinct();
                foreach (var nazwa in nazwy)
                {
                    int tel = daneHistoria.ContainsKey(nazwa) ? daneHistoria[nazwa].tel : 0;
                    int stat = daneHistoria.ContainsKey(nazwa) ? daneHistoria[nazwa].stat : 0;
                    int not = daneNotatki.ContainsKey(nazwa) ? daneNotatki[nazwa] : 0;

                    wszystkie.Add(new TopHandlowiec
                    {
                        Nazwa = nazwa,
                        Telefony = tel,
                        Statusy = stat,
                        Notatki = not,
                        Suma = tel + stat + not
                    });
                }

                var medale = new[] { "🥇", "🥈", "🥉" };
                var koloryTla = new[] { "#FEF3C7", "#F1F5F9", "#FFEDD5" };

                top = wszystkie.OrderByDescending(t => t.Suma).Take(3).ToList();
                for (int i = 0; i < top.Count; i++)
                {
                    top[i].Medal = medale[i];
                    top[i].TloPozycji = new SolidColorBrush((Color)ColorConverter.ConvertFromString(koloryTla[i]));
                }
            }

            listaTopDnia.ItemsSource = top;
        }
    }

    // Klasy pomocnicze
    public class HandlowiecWykres
    {
        public int Pozycja { get; set; }
        public string Nazwa { get; set; }
        public int Telefony { get; set; }
        public int Statusy { get; set; }
        public int Notatki { get; set; }
        public int Suma { get; set; }
        public double SzerokoscTelefony { get; set; }
        public double SzerokoscStatusy { get; set; }
        public double SzerokoscNotatki { get; set; }
        public string TooltipTelefony { get; set; }
        public string TooltipStatusy { get; set; }
        public string TooltipNotatki { get; set; }
        public SolidColorBrush KolorPozycji { get; set; }
    }

    public class DzienAktywnosc
    {
        public string DzienNazwa { get; set; }
        public string Data { get; set; }
        public int Telefony { get; set; }
        public int Statusy { get; set; }
        public int Notatki { get; set; }
        public SolidColorBrush TloKarty { get; set; }
    }

    public class TopHandlowiec
    {
        public string Medal { get; set; }
        public string Nazwa { get; set; }
        public int Telefony { get; set; }
        public int Statusy { get; set; }
        public int Notatki { get; set; }
        public int Suma { get; set; }
        public SolidColorBrush TloPozycji { get; set; }
    }
}
