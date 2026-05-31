using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace Kalendarz1.Zywiec.WidokSpecyfikacji
{
    /// <summary>
    /// Debugger importu specyfikacji — pokazuje DOKŁADNIE:
    ///  1) jak dopasowano hodowców z Excela do bazy (metoda + pewność, niepewne podświetlone),
    ///  2) co odczytano z poszczególnych komórek + podgląd wyliczeń (netto/śr.waga/Do zapł./Wartość)
    ///     wg tej samej formuły co PDF — żeby sprawdzić zgodność PRZED importem.
    /// Czysto diagnostyczne okno (read-only).
    /// </summary>
    public partial class ImportDebugWindow : Window
    {
        private readonly ImportDebugMeta _meta;
        private readonly List<ImportDebugRow> _parseRows;
        private readonly List<ImportDebugMatch> _matchRows;

        public ImportDebugWindow(ImportDebugMeta meta, List<ImportDebugRow> parseRows, List<ImportDebugMatch> matchRows)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            _meta = meta;
            _parseRows = parseRows ?? new List<ImportDebugRow>();
            _matchRows = matchRows ?? new List<ImportDebugMatch>();

            dataGridMatch.ItemsSource = _matchRows;
            dataGridParse.ItemsSource = _parseRows;

            WypelnijNaglowek();
        }

        private void WypelnijNaglowek()
        {
            lblPlik.Text = $"{_meta.Plik}  ·  {_meta.TypPliku}";
            lblSciezka.Text = _meta.SciezkaPelna;
            lblArkusz.Text = _meta.Arkusz;
            lblData.Text = _meta.DataUboju.ToString("dd.MM.yyyy");
            lblWierszy.Text = _meta.LiczbaWierszy.ToString();
            lblDostawcow.Text = _meta.LiczbaDostawcow.ToString();

            int wysoka = _matchRows.Count(m => m.Rank == 3);
            int srednia = _matchRows.Count(m => m.Rank == 2);
            int niska = _matchRows.Count(m => m.Rank == 1);
            int brak = _matchRows.Count(m => m.Rank == 0);

            lblWysoka.Text = wysoka.ToString();
            lblSrednia.Text = srednia.ToString();
            lblNiska.Text = niska.ToString();
            lblBrak.Text = brak.ToString();

            int problemy = _parseRows.Count(r => r.MaProblem);
            lblProblemy.Text = problemy.ToString();
            chipProblemy.Visibility = problemy > 0 ? Visibility.Visible : Visibility.Collapsed;

            // Tytuły zakładek z licznikami
            tabMatch.Header = $"🔗 Dopasowanie hodowców ({_matchRows.Count})";
            tabParse.Header = $"📋 Parsowanie komórek ({_parseRows.Count})";

            // Podpowiedź gdy są niedopasowani / niepewni
            if (brak > 0 || niska > 0)
            {
                lblHint.Text = $"⚠ {brak + niska} dostawców wymaga kontroli (brak lub niska pewność) — sprawdź zakładkę dopasowania i popraw ręcznie w kroku „Mapowanie\".";
                lblHint.Visibility = Visibility.Visible;
            }
            else
            {
                lblHint.Text = "✓ Wszystkie pozycje dopasowane ze średnią lub wysoką pewnością.";
                lblHint.Visibility = Visibility.Visible;
            }
        }

        private void BtnKopiuj_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(BudujRaport());
                MessageBox.Show("Raport skopiowany do schowka.", "Debugger importu",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się skopiować: {ex.Message}", "Debugger importu",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e) => Close();

        private string BudujRaport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════ DEBUG IMPORTU SPECYFIKACJI ═══════");
            sb.AppendLine($"Plik:    {_meta.Plik}  ({_meta.TypPliku})");
            sb.AppendLine($"Ścieżka: {_meta.SciezkaPelna}");
            sb.AppendLine($"Arkusz:  {_meta.Arkusz}   |   Data uboju: {_meta.DataUboju:dd.MM.yyyy}");
            sb.AppendLine($"Wierszy: {_meta.LiczbaWierszy}   |   Dostawców: {_meta.LiczbaDostawcow}");
            sb.AppendLine($"Dopasowanie pewności → Wysoka: {_matchRows.Count(m => m.Rank == 3)} | " +
                          $"Średnia: {_matchRows.Count(m => m.Rank == 2)} | " +
                          $"Niska: {_matchRows.Count(m => m.Rank == 1)} | " +
                          $"Brak: {_matchRows.Count(m => m.Rank == 0)}");

            sb.AppendLine();
            sb.AppendLine("─────── DOPASOWANIE HODOWCÓW ───────");
            foreach (var m in _matchRows)
            {
                string znak = m.Rank switch { 3 => "✓✓", 2 => "✓", 1 => "≈", _ => "✗" };
                sb.AppendLine($"  [{znak} {m.MatchPewnosc,-7}] {m.DostawcaExcel}  ({m.IloscWierszy} w.)");
                sb.AppendLine($"        → {m.DopasowanoDo}{(string.IsNullOrEmpty(m.AnimNo) ? "" : $" (ARiMR {m.AnimNo})")}   [{m.Metoda}]");
                if (m.Harmonogram != "—")
                    sb.AppendLine($"        Harmonogram: {m.Harmonogram}");
            }

            sb.AppendLine();
            sb.AppendLine("─────── PARSOWANIE + PODGLĄD WYLICZEŃ ───────");
            sb.AppendLine("  Spec | Dostawca(Excel) | netto | śr.waga | Do zapł. | Wartość | Flagi");
            foreach (var r in _parseRows)
            {
                sb.AppendLine($"  {r.NrSpecyfikacji,4} | {Skroc(r.DostawcaExcel, 24),-24} | " +
                              $"{r.Netto,8:N0} | {r.SrWaga,6:N2} | {r.DoZaplaty,8:N0} | {r.Wartosc,12:N2} | {r.Flagi}");
            }

            int problemy = _parseRows.Count(r => r.MaProblem);
            if (problemy > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"⚠ Wierszy z ostrzeżeniami: {problemy}");
            }
            return sb.ToString();
        }

        private static string Skroc(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }
    }

    #region Modele widoku debuggera

    public class ImportDebugMeta
    {
        public string Plik { get; set; } = "";
        public string SciezkaPelna { get; set; } = "";
        public string TypPliku { get; set; } = "";
        public string Arkusz { get; set; } = "";
        public DateTime DataUboju { get; set; }
        public int LiczbaWierszy { get; set; }
        public int LiczbaDostawcow { get; set; }
    }

    public class ImportDebugMatch
    {
        public string DostawcaExcel { get; set; } = "";
        public int IloscWierszy { get; set; }
        public string Metoda { get; set; } = "";
        public int Rank { get; set; }
        public string DopasowanoDo { get; set; } = "";
        public string AnimNo { get; set; } = "";
        public string IdBazy { get; set; } = "";
        public string Harmonogram { get; set; } = "";

        public string MatchPewnosc => Rank switch
        {
            3 => "Wysoka",
            2 => "Średnia",
            1 => "Niska",
            _ => "Brak"
        };

        // Tło wiersza wg pewności
        public Brush RowBrush => Rank switch
        {
            3 => new SolidColorBrush(Color.FromRgb(0xE8, 0xF5, 0xE9)), // zielonkawe
            2 => new SolidColorBrush(Color.FromRgb(0xFF, 0xF8, 0xE1)), // jasny bursztyn
            1 => new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0xB2)), // bursztyn
            _ => new SolidColorBrush(Color.FromRgb(0xFF, 0xCD, 0xD2)), // czerwonawe
        };

        public Brush PewnoscBrush => Rank switch
        {
            3 => new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)),
            2 => new SolidColorBrush(Color.FromRgb(0xF5, 0x7C, 0x00)),
            1 => new SolidColorBrush(Color.FromRgb(0xE6, 0x51, 0x00)),
            _ => new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)),
        };
    }

    public class ImportDebugRow
    {
        public int NrAuta { get; set; }
        public int NrSpecyfikacji { get; set; }
        public string DostawcaExcel { get; set; } = "";
        public int SztukiDek { get; set; }
        public int Padle { get; set; }
        public int CH { get; set; }
        public int NW { get; set; }
        public int ZM { get; set; }
        public decimal BruttoHodowcy { get; set; }
        public decimal TaraHodowcy { get; set; }
        public decimal BruttoUbojni { get; set; }
        public decimal TaraUbojni { get; set; }
        public int LUMEL { get; set; }
        public string TypCeny { get; set; } = "";
        public decimal Cena { get; set; }
        public decimal Dodatek { get; set; }
        public bool PiK { get; set; }
        public decimal Ubytek { get; set; }

        // Podgląd wyliczeń (kanoniczna formuła)
        public decimal Netto { get; set; }
        public decimal SrWaga { get; set; }
        public decimal DoZaplaty { get; set; }
        public decimal Wartosc { get; set; }
        public string Flagi { get; set; } = "";

        public bool MaProblem => Flagi != null && Flagi.Contains("⚠");

        public Brush RowBrush => MaProblem
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0xEB, 0xEE))
            : Brushes.Transparent;
    }

    #endregion
}
