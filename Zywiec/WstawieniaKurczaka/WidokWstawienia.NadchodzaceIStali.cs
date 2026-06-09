using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Kalendarz1
{
    // ============================================================================
    // Stali klienci — wyróżnienie ⭐ + bold + zielony border
    //
    // HashSet hodowców którzy mieli kiedykolwiek dostawę z Bufor='Potwierdzony'.
    // Budowany RAZ z istniejącego _deliveryCache (PreloadDeliveryCache),
    // bez dodatkowego SQL. Lookup w LoadingRow = O(1).
    //
    // Metody LoadDoPotwierdzenia / SetupDoPotwierdzeniaColumns są w głównym pliku
    // WidokWstawienia.xaml.cs — tam dodano: ⭐ kolumnę, ZaDni, formatowanie wierszy.
    // ============================================================================

    public partial class WidokWstawienia
    {
        // Cache hodowców-stałych — case-insensitive
        private HashSet<string> _staliKlienci = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Wywoływane po LoadWstawienia (DataView dostępne)
        // + PreloadDeliveryCache (mapa LpW→DostawaInfo z Bufor)
        //
        // Logika: hodowcy często rezerwują wstawienia na CAŁY ROK z góry — te dalekie wstawienia
        // jeszcze nie mają dostaw (bo nie nadszedł termin), więc nie informują o "stałości".
        // Bierzemy zatem tylko wstawienia, których data <= dziś + 20 dni (czyli odbyte
        // lub bardzo bliskie). Z tej puli ostatnie 4 i sprawdzamy czy w którymś była
        // dostawa z Bufor='Potwierdzony'. Jeśli tak → ⭐ "ostatnio coś nam dał".
        private const int StaliKlienciOstatnichWstawien = 4;
        private const int StaliKlienciHorizonDniWPrzod = 20;

        // Status dostawy = "coś od hodowcy faktycznie weszło / jest twardo zaplanowane".
        // Skopiowane z OstatnieWstawieniaHodowcyWindow.JestPotwierdzony — bo tam jest jeden
        // wspólny słownik wartości pola Bufor. Sam "Potwierdzony" to za wąsko (mieliśmy hodowców
        // którzy zdają nam co miesiąc ale mają status "Sprzedany" lub "B.Kontr.").
        private static bool JestZrealizowanaDostawa(string? bufor)
        {
            if (string.IsNullOrEmpty(bufor)) return false;
            return bufor.Equals("Potwierdzony", StringComparison.OrdinalIgnoreCase)
                || bufor.Equals("Sprzedany", StringComparison.OrdinalIgnoreCase)
                || bufor.Equals("B.Wolny.", StringComparison.OrdinalIgnoreCase)
                || bufor.Equals("B.Wolny", StringComparison.OrdinalIgnoreCase)
                || bufor.Equals("B.Kontr.", StringComparison.OrdinalIgnoreCase);
        }

        internal void ZbudujCacheStalychKlientow()
        {
            _staliKlienci.Clear();
            if (_deliveryCache == null || _deliveryCache.Count == 0) return;

            DateTime granica = DateTime.Today.AddDays(StaliKlienciHorizonDniWPrzod);

            // Źródło: _deliveryCache zawiera CAŁĄ historię (FULL JOIN, nie TOP 100), więc znajdziemy
            // też hodowców których ostatnie wstawienia są dalej niż widoczna lista na lewej kolumnie.
            // Grupowanie wstawień per hodowca — pomijamy wstawienia >20 dni w przyszłość
            var perHodowca = new Dictionary<string, List<(DateTime data, int lp)>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in _deliveryCache)
            {
                int lp = kv.Key;
                var dostawy = kv.Value;
                if (dostawy == null || dostawy.Count == 0) continue;

                // Dostawca i DataWstawienia są takie same dla wszystkich dostaw tego LP
                string d = dostawy[0].Dostawca ?? "";
                DateTime data = dostawy[0].DataWstawienia;
                if (string.IsNullOrWhiteSpace(d)) continue;
                if (data == DateTime.MinValue) continue;
                if (data > granica) continue; // odcinamy roczne rezerwacje

                if (!perHodowca.TryGetValue(d, out var lista))
                {
                    lista = new List<(DateTime, int)>();
                    perHodowca[d] = lista;
                }
                lista.Add((data, lp));
            }

            foreach (var kv in perHodowca)
            {
                var ostatnie = kv.Value.OrderByDescending(x => x.data).Take(StaliKlienciOstatnichWstawien);
                foreach (var (_, lp) in ostatnie)
                {
                    if (!_deliveryCache.TryGetValue(lp, out var dostawy)) continue;
                    if (dostawy.Any(d => JestZrealizowanaDostawa(d.Bufor)))
                    {
                        _staliKlienci.Add(kv.Key);
                        break;
                    }
                }
            }
        }

        // O(1) lookup używany w LoadingRow handlerach + konwerterze
        internal bool CzyStalyKlient(string dostawca)
        {
            if (string.IsNullOrEmpty(dostawca)) return false;
            return _staliKlienci.Contains(dostawca);
        }

        // Buduje kolumnę "Hodowca" z żółtą ★ przed nazwą dla stałych klientów.
        // ★ jest osobnym TextBlockiem w gold/żółtym kolorze (emoji ⭐ ignoruje Foreground bo to color font).
        internal DataGridTemplateColumn BudujKolumneHodowcaZGwiazdka(DataGridLength width)
        {
            var col = new DataGridTemplateColumn
            {
                Header = "Hodowca",
                SortMemberPath = "Dostawca",
                Width = width
            };

            var tpl = new DataTemplate();
            var sp = new System.Windows.FrameworkElementFactory(typeof(StackPanel));
            sp.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            // ★ — tylko gdy stały klient (converter zwraca "★" lub "")
            var star = new System.Windows.FrameworkElementFactory(typeof(TextBlock));
            star.SetBinding(TextBlock.TextProperty, new Binding("Dostawca") { Converter = new StalyKlientIkonkaConverter(this) });
            star.SetValue(TextBlock.ForegroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xC1, 0x07))); // amber/żółty
            star.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            star.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 4, 0));
            star.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            sp.AppendChild(star);

            var name = new System.Windows.FrameworkElementFactory(typeof(TextBlock));
            name.SetBinding(TextBlock.TextProperty, new Binding("Dostawca"));
            name.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            sp.AppendChild(name);

            tpl.VisualTree = sp;
            col.CellTemplate = tpl;
            return col;
        }

        // ====== SMS O POTWIERDZENIE TERMINU (Nadchodzące wstawienia) ======

        private string PrzygotujTrescPotwierdzenia(DataRowView row, string wariant)
        {
            string ilosc = row["IloscWstawienia"] != DBNull.Value
                ? Convert.ToInt32(row["IloscWstawienia"]).ToString("# ##0") : "?";
            string data = row["DataWstawienia"] != DBNull.Value
                ? Convert.ToDateTime(row["DataWstawienia"]).ToString("dd.MM.yyyy") : "?";
            string dzien = row["DataWstawienia"] != DBNull.Value
                ? Convert.ToDateTime(row["DataWstawienia"]).ToString("dddd", new System.Globalization.CultureInfo("pl-PL"))
                : "?";
            int zaDni = row["ZaDni"] != DBNull.Value ? Convert.ToInt32(row["ZaDni"]) : 0;
            string zaDniTekst = zaDni switch
            {
                0 => "dzis",
                1 => "jutro",
                -1 => "wczoraj",
                < 0 => $"{-zaDni} dni temu",
                _ => $"za {zaDni} dni"
            };

            const string podpis = "Pozdrawiamy, Ubojnia Drobiu \"Piórkowscy\".";

            return wariant switch
            {
                "pelny" =>
                    $"Dzień dobry! Piszemy w sprawie zaplanowanego wstawienia kurczaków: " +
                    $"{data} ({dzien}, {zaDniTekst}), {ilosc} szt. " +
                    $"Czy termin pozostaje aktualny? " +
                    $"Prosimy o krótką odpowiedź: \"TAK\" jeśli wszystko gra, " +
                    $"lub podanie nowej daty jeśli się zmieniło. " +
                    $"{podpis}",

                "krotki" =>
                    $"Dzień dobry! Czy wstawienie {data} ({zaDniTekst}), {ilosc} szt jest aktualne? " +
                    $"Prosimy odpisać \"TAK\" lub podać nową datę. " +
                    $"{podpis}",

                _ => PrzygotujTrescPotwierdzenia(row, "pelny")
            };
        }

        private void MenuSmsPotwierdzenie_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridDoPotwierdzenia.SelectedItem is not DataRowView row)
            {
                MessageBox.Show("Wybierz wstawienie z listy.", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string wariant = (sender is MenuItem mi && mi.Tag != null) ? mi.Tag.ToString() ?? "pelny" : "pelny";
            string dostawca = row["Dostawca"] != DBNull.Value ? Convert.ToString(row["Dostawca"]) ?? "" : "";
            string telefon = row["Telefon"] != DBNull.Value ? Convert.ToString(row["Telefon"])?.Trim() ?? "" : "";
            string tresc = PrzygotujTrescPotwierdzenia(row, wariant);

            string nazwaWariantu = wariant == "pelny"
                ? "1️⃣ Pełne potwierdzenie"
                : "2️⃣ Krótkie potwierdzenie";

            // Wysyłka przez telefon (dialog) LUB schowek (legacy) — wspólny helper
            if (!WyslijSmsLubPokaSchowek(dostawca, telefon, tresc, "Potwierdzenie terminu — " + nazwaWariantu, out _,
                $"SMS potwierdzający — wariant {(wariant == "pelny" ? "1" : "2")}")) return;

            // #2 Auto-wpis do ContactHistory + snooze 3 dni (wiersz znika z Nadchodzących)
            int? lpWst = row["LP"] != DBNull.Value ? Convert.ToInt32(row["LP"]) : (int?)null;
            ZapiszContactSmsAutomatycznie(lpWst, dostawca, "Potwierdzenie terminu — " + nazwaWariantu, snoozeDays: 3);

            LoadHistoria();
            LoadDoPotwierdzenia();  // wiersz znika z listy (snooze 3 dni)
            OdswiezStatusBar();
        }
    }

    // Konwerter — zwraca monochromatyczny ★ (U+2605) jeśli stały klient, inaczej "".
    // ★ jest jednokolorowa — Foreground=Gold zadziała (emoji ⭐ używa color font i ignoruje Foreground).
    public class StalyKlientIkonkaConverter : IValueConverter
    {
        private readonly WidokWstawienia _widok;
        public StalyKlientIkonkaConverter(WidokWstawienia widok) { _widok = widok; }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null || value == DBNull.Value) return "";
            string dostawca = value.ToString() ?? "";
            return _widok.CzyStalyKlient(dostawca) ? "★" : "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotImplementedException();
    }

    // Konwerter — łączy ⭐ + nazwę hodowcy w jednej komórce
    // ("Kowalski" → "⭐ Kowalski" jeśli stały, inaczej "Kowalski")
    public class DostawcaZGwiazdkaConverter : IValueConverter
    {
        private readonly WidokWstawienia _widok;
        public DostawcaZGwiazdkaConverter(WidokWstawienia widok) { _widok = widok; }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null || value == DBNull.Value) return "";
            string dostawca = value.ToString() ?? "";
            return _widok.CzyStalyKlient(dostawca) ? "⭐ " + dostawca : dostawca;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotImplementedException();
    }
}
