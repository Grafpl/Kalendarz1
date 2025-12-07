using Microsoft.Data.SqlClient;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Kalendarz1.CRM
{
    public partial class HistoriaHandlowcaWindow : Window
    {
        private readonly string connectionString;
        private readonly string operatorId;
        private readonly string operatorName;
        private readonly int pozycja;
        private readonly int suma;
        private readonly int proby;
        private readonly int nawiazano;
        private readonly int zgoda;
        private readonly int oferty;
        private readonly int nieZainteresowany;
        private readonly bool wszystkieDni;

        public HistoriaHandlowcaWindow(string connStr, DataRowView rankingRow, bool wszystkie = false)
        {
            InitializeComponent();
            connectionString = connStr;
            wszystkieDni = wszystkie;

            // Pobierz dane z wiersza rankingu
            operatorName = rankingRow["Operator"]?.ToString() ?? "Nieznany";
            pozycja = Convert.ToInt32(rankingRow["Pozycja"]);
            suma = Convert.ToInt32(rankingRow["Suma"]);
            proby = Convert.ToInt32(rankingRow["Proby"]);
            nawiazano = Convert.ToInt32(rankingRow["Nawiazano"]);
            zgoda = Convert.ToInt32(rankingRow["Zgoda"]);
            oferty = Convert.ToInt32(rankingRow["Oferty"]);
            nieZainteresowany = Convert.ToInt32(rankingRow["NieZainteresowany"]);

            // Wyciągnij ID operatora
            if (operatorName.StartsWith("ID: "))
                operatorId = operatorName.Substring(4);
            else
                operatorId = PobierzIdOperatora(operatorName);

            UstawDane();
            WczytajHistorie();
            WczytajWykres();
        }

        private string PobierzIdOperatora(string nazwa)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("SELECT TOP 1 ID FROM operators WHERE Name = @nazwa", conn);
                    cmd.Parameters.AddWithValue("@nazwa", nazwa);
                    var result = cmd.ExecuteScalar();
                    return result?.ToString() ?? "";
                }
            }
            catch { return ""; }
        }

        private void UstawDane()
        {
            Title = $"Historia: {operatorName}";

            // Inicjały
            var parts = operatorName.Split(' ');
            if (parts.Length >= 2)
                txtInitials.Text = $"{parts[0][0]}{parts[1][0]}".ToUpper();
            else if (operatorName.Length >= 2)
                txtInitials.Text = operatorName.Substring(0, 2).ToUpper();

            txtNazwaHandlowca.Text = operatorName;
            txtPozycja.Text = $"#{pozycja}";
            txtOkres.Text = wszystkieDni ? "Wszystkie dni" : "Ostatnie 30 dni";
            txtSuma.Text = suma.ToString();

            // Średnia dzienna
            int dni = wszystkieDni ? 365 : 30;
            double srednia = Math.Round((double)suma / dni, 1);
            txtSrednia.Text = srednia.ToString("0.0");

            // Skuteczność (Zgody + Oferty) / Wszystkie
            if (suma > 0)
            {
                double skutecznosc = ((double)(zgoda + oferty) / suma) * 100;
                txtSkutecznosc.Text = $"{skutecznosc:0}%";
            }

            // Statystyki
            txtStatProby.Text = proby.ToString();
            txtStatNawiazano.Text = nawiazano.ToString();
            txtStatZgoda.Text = zgoda.ToString();
            txtStatOferty.Text = oferty.ToString();
            txtStatNieZaint.Text = nieZainteresowany.ToString();
        }

        private void WczytajWykres()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Grupuj po miesiącach - ostatnie 12 miesięcy
                    var cmd = new SqlCommand(@"
                        SELECT FORMAT(h.DataZmiany, 'MMM yy', 'pl-PL') as Okres,
                               YEAR(h.DataZmiany) as Rok, MONTH(h.DataZmiany) as Miesiac,
                               COUNT(*) as Liczba
                        FROM HistoriaZmianCRM h
                        WHERE h.KtoWykonal = @opId
                          AND h.DataZmiany > DATEADD(month, -12, GETDATE())
                          AND h.WartoscNowa NOT IN ('Do zadzwonienia', 'Nowy')
                        GROUP BY YEAR(h.DataZmiany), MONTH(h.DataZmiany), FORMAT(h.DataZmiany, 'MMM yy', 'pl-PL')
                        ORDER BY Rok, Miesiac", conn);

                    cmd.Parameters.AddWithValue("@opId", operatorId);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    if (dt.Rows.Count == 0) return;

                    int maxWartosc = dt.AsEnumerable().Max(r => Convert.ToInt32(r["Liczba"]));
                    double maxWysokosc = 300; // Duży wykres

                    var dane = new ObservableCollection<WykresSlupek>();
                    foreach (DataRow row in dt.Rows)
                    {
                        int liczba = Convert.ToInt32(row["Liczba"]);
                        double wysokosc = maxWartosc > 0 ? (double)liczba / maxWartosc * maxWysokosc : 8;

                        dane.Add(new WykresSlupek
                        {
                            Etykieta = row["Okres"].ToString(),
                            Wartosc = liczba.ToString(),
                            Wysokosc = Math.Max(8, wysokosc),
                            Kolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A")),
                            Tooltip = $"{row["Okres"]}: {liczba} akcji"
                        });
                    }

                    wykresAktywnosci.ItemsSource = dane;
                }
            }
            catch { }
        }

        private void WczytajHistorie()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string whereDate = wszystkieDni ? "" : "AND h.DataZmiany > DATEADD(day, -30, GETDATE())";

                    var cmd = new SqlCommand($@"
                        SELECT h.DataZmiany, h.WartoscNowa, h.TypZmiany,
                               o.Nazwa as NazwaKlienta, o.MIASTO as Miasto, o.Telefon_K as Telefon
                        FROM HistoriaZmianCRM h
                        LEFT JOIN OdbiorcyCRM o ON h.IDOdbiorcy = o.ID
                        WHERE h.KtoWykonal = @opId {whereDate}
                          AND h.WartoscNowa NOT IN ('Do zadzwonienia', 'Nowy')
                        ORDER BY h.DataZmiany DESC", conn);

                    cmd.Parameters.AddWithValue("@opId", operatorId);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    var lista = new ObservableCollection<HistoriaAkcji>();
                    foreach (DataRow row in dt.Rows)
                    {
                        var akcja = new HistoriaAkcji
                        {
                            DataZmiany = row["DataZmiany"] != DBNull.Value ? (DateTime)row["DataZmiany"] : DateTime.MinValue,
                            WartoscNowa = row["WartoscNowa"]?.ToString() ?? "",
                            NazwaKlienta = row["NazwaKlienta"]?.ToString() ?? "-",
                            Miasto = row["Miasto"]?.ToString() ?? "-",
                            Telefon = row["Telefon"]?.ToString() ?? "-"
                        };

                        UstawKoloryStatusu(akcja);
                        lista.Add(akcja);
                    }

                    dgHistoria.ItemsSource = lista;
                    txtLiczbaAkcji.Text = $"{lista.Count} rekordów";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania historii: {ex.Message}");
            }
        }

        private void UstawKoloryStatusu(HistoriaAkcji akcja)
        {
            // Określ typ akcji (telefon/mail)
            bool jestOferta = akcja.WartoscNowa == "Do wysłania oferta";
            akcja.TypIkona = jestOferta ? "📧" : "📞";
            akcja.TypOpis = jestOferta ? "Mail / Oferta" : "Telefon";

            switch (akcja.WartoscNowa)
            {
                case "Próba kontaktu":
                    akcja.StatusIkona = "⏳";
                    akcja.StatusKolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEDD5"));
                    akcja.StatusTekstKolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9A3412"));
                    break;
                case "Nawiązano kontakt":
                    akcja.StatusIkona = "✅";
                    akcja.StatusKolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7"));
                    akcja.StatusTekstKolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#166534"));
                    break;
                case "Zgoda na dalszy kontakt":
                    akcja.StatusIkona = "🤝";
                    akcja.StatusKolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCFBF1"));
                    akcja.StatusTekstKolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D9488"));
                    break;
                case "Do wysłania oferta":
                    akcja.StatusIkona = "📄";
                    akcja.StatusKolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DBEAFE"));
                    akcja.StatusTekstKolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E40AF"));
                    break;
                case "Nie zainteresowany":
                    akcja.StatusIkona = "❌";
                    akcja.StatusKolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
                    akcja.StatusTekstKolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B"));
                    break;
                default:
                    akcja.StatusIkona = "📋";
                    akcja.StatusKolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6"));
                    akcja.StatusTekstKolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
                    break;
            }
        }
    }

    public class HistoriaAkcji
    {
        public DateTime DataZmiany { get; set; }
        public string WartoscNowa { get; set; }
        public string NazwaKlienta { get; set; }
        public string Miasto { get; set; }
        public string Telefon { get; set; }
        public string StatusIkona { get; set; }
        public SolidColorBrush StatusKolor { get; set; }
        public SolidColorBrush StatusTekstKolor { get; set; }
        public string TypIkona { get; set; }
        public string TypOpis { get; set; }
    }

    public class WykresSlupek
    {
        public string Etykieta { get; set; }
        public string Wartosc { get; set; }
        public double Wysokosc { get; set; }
        public SolidColorBrush Kolor { get; set; }
        public string Tooltip { get; set; }
    }
}
