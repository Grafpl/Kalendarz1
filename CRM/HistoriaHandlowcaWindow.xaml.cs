using Microsoft.Data.SqlClient;
using System;
using System.Collections.ObjectModel;
using System.Data;
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
        private readonly int doZadzwonienia;
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
            doZadzwonienia = Convert.ToInt32(rankingRow["DoZadzwonienia"]);
            proby = Convert.ToInt32(rankingRow["Proby"]);
            nawiazano = Convert.ToInt32(rankingRow["Nawiazano"]);
            zgoda = Convert.ToInt32(rankingRow["Zgoda"]);
            oferty = Convert.ToInt32(rankingRow["Oferty"]);
            nieZainteresowany = Convert.ToInt32(rankingRow["NieZainteresowany"]);

            // Wyciągnij ID operatora z nazwy (jeśli jest w formacie "ID: XXX")
            if (operatorName.StartsWith("ID: "))
                operatorId = operatorName.Substring(4);
            else
            {
                // Pobierz ID z bazy po nazwie
                operatorId = PobierzIdOperatora(operatorName);
            }

            UstawDane();
            WczytajHistorie();
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

            // Statystyki
            txtStatDoZadzw.Text = doZadzwonienia.ToString();
            txtStatProby.Text = proby.ToString();
            txtStatNawiazano.Text = nawiazano.ToString();
            txtStatZgoda.Text = zgoda.ToString();
            txtStatOferty.Text = oferty.ToString();
            txtStatNieZaint.Text = nieZainteresowany.ToString();
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

                        // Ustaw kolory na podstawie statusu
                        switch (akcja.WartoscNowa)
                        {
                            case "Do zadzwonienia":
                            case "Nowy":
                                akcja.StatusIkona = "📞";
                                akcja.StatusKolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
                                akcja.StatusTekstKolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
                                break;
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

                        lista.Add(akcja);
                    }

                    dgHistoria.ItemsSource = lista;
                    txtLiczbaAkcji.Text = $" ({lista.Count} rekordów)";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania historii: {ex.Message}");
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
    }
}
