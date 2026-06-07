using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Kalendarz1
{
    // ============================================================================
    // Pomysłowe usprawnienia: #3 inline edit telefonu, #6 skróty klawiszowe
    // ============================================================================

    public partial class WidokWstawienia
    {
        // === #2 AUTO-WPIS DO ContactHistory PO WYSŁANIU SMS ===
        // Wywoływane z wszystkich miejsc gdzie wysyłany jest SMS.
        // Dzięki temu w "Historia kontaktów" (prawa tabela) widać kto wysłał, kiedy, jaki wariant.
        // Bez tego operator musiałby ręcznie wpisywać każdy SMS.
        // snoozeDays > 0 ustawia SnoozedUntil = today+snoozeDays — wiersz znika z "Nadchodzących"
        // na ten okres (filtr WHERE NOT EXISTS ... SnoozedUntil > today w LoadDoPotwierdzenia).
        internal void ZapiszContactSmsAutomatycznie(int? lpWstawienia, string dostawca, string nazwaWariantu, int snoozeDays = 0)
        {
            if (string.IsNullOrWhiteSpace(dostawca)) return;
            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();
                using var cmd = new SqlCommand(@"
                    INSERT INTO dbo.ContactHistory (LpWstawienia, Dostawca, UserID, Reason, CreatedAt, ContactDate, SnoozedUntil)
                    VALUES (@lp, @d, @u, @r, GETDATE(), CAST(GETDATE() AS date), @snooze)", conn);
                cmd.Parameters.AddWithValue("@lp", (object?)lpWstawienia ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@d", dostawca);
                cmd.Parameters.AddWithValue("@u", App.UserID ?? "");
                cmd.Parameters.AddWithValue("@r", "Auto SMS: " + nazwaWariantu);
                cmd.Parameters.AddWithValue("@snooze",
                    snoozeDays > 0 ? (object)DateTime.Today.AddDays(snoozeDays) : DBNull.Value);
                cmd.ExecuteNonQuery();
            }
            catch { /* nie blokujemy SMS jeśli log padnie */ }
        }

        // === #3 INLINE EDIT TELEFONU — wpięcie PreviewMouseDoubleClick ===
        // Wywoływane raz z InitializeData. Dwuklik na komórce "Tel" → otwiera dialog
        // (zatrzymuje default double-click który by otwierał coś innego).
        internal void PodepnijDwuklikTelefonu()
        {
            dataGridPrzypomnienia.PreviewMouseDoubleClick += (s, ev) => HandleTelDoubleClick(ev, dataGridPrzypomnienia);
            dataGridDoPotwierdzenia.PreviewMouseDoubleClick += (s, ev) => HandleTelDoubleClick(ev, dataGridDoPotwierdzenia);
        }

        private void HandleTelDoubleClick(MouseButtonEventArgs ev, DataGrid grid)
        {
            DependencyObject? dep = ev.OriginalSource as DependencyObject;
            while (dep != null && dep is not DataGridCell)
                dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);

            if (dep is DataGridCell cell && cell.Column?.Header?.ToString() == "Tel")
            {
                if (cell.DataContext is DataRowView row)
                {
                    ev.Handled = true;
                    EdytujTelefonInline(row);
                }
            }
        }

        // === #6 SKRÓTY KLAWISZOWE + #4 Hot-keys F/T/R ===
        // S      = SMS wariant 2 (krótki)
        // Shift+S = SMS wariant 1 (oficjalny pełny)
        // T      = to samo co S (T jak "Text") — alias
        // F      = potwierdź wstawienie (Przypomnienia + Nadchodzące + Wstawienia)
        // R      = "Nie odebrał" +3 dni (tylko Przypomnienia)
        internal void PodepnijSkrotySms()
        {
            dataGridWstawienia.KeyDown += (s, e) => HandleHotkey(s, e, "wstawienia");
            dataGridPrzypomnienia.KeyDown += (s, e) => HandleHotkey(s, e, "przypomnienia");
            dataGridDoPotwierdzenia.KeyDown += (s, e) => HandleHotkey(s, e, "potwierdz");
        }

        private void HandleHotkey(object sender, KeyEventArgs e, string zrodlo)
        {
            // Ignoruj jeśli user pisze w polu tekstowym (textboxFilter itp.)
            if (e.OriginalSource is TextBox) return;

            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            if (ctrl) return;  // nie kradnij Ctrl+...

            switch (e.Key)
            {
                case Key.S:
                case Key.T:
                    {
                        int wariant = shift ? 1 : 2;
                        e.Handled = true;
                        switch (zrodlo)
                        {
                            case "wstawienia": WyslijSmsZWstawien(wariant); break;
                            case "przypomnienia": WyslijSmsWariant(wariant); break;
                            case "potwierdz": WyslijSmsPotwierdzenieSkrot(shift ? "pelny" : "krotki"); break;
                        }
                    }
                    break;

                case Key.F:
                    // F = Potwierdź wstawienie
                    {
                        e.Handled = true;
                        switch (zrodlo)
                        {
                            case "potwierdz":
                                MenuPotwierdzWstawienie_Click(null!, null!);
                                break;
                            case "wstawienia":
                                MenuPotwierdzWstawienieGrid_Click(null!, null!);
                                break;
                            case "przypomnienia":
                                // Brak bezpośredniego "potwierdź" w przypomnieniach,
                                // ale można symulować potwierdzenie najnowszego wstawienia hodowcy
                                // → najprościej: wywołaj edycję + user kliknie potwierdź ręcznie
                                MenuEdytuj_Click(null!, null!);
                                break;
                        }
                    }
                    break;

                case Key.R:
                    // R = "Nie odebrał" + 3 dni (tylko Przypomnienia)
                    if (zrodlo == "przypomnienia")
                    {
                        e.Handled = true;
                        MenuNieOdebral_Click(null!, null!);
                    }
                    break;
            }
        }

        private void WyslijSmsPotwierdzenieSkrot(string wariant)
        {
            if (dataGridDoPotwierdzenia.SelectedItem is not DataRowView row) return;
            string dostawca = row["Dostawca"] != DBNull.Value ? Convert.ToString(row["Dostawca"]) ?? "" : "";
            string telefon = row["Telefon"] != DBNull.Value ? Convert.ToString(row["Telefon"])?.Trim() ?? "" : "";
            if (telefon == "-") telefon = "";
            string tresc = PrzygotujTrescPotwierdzenia(row, wariant);

            Clipboard.SetText(tresc);
            int smsCount = string.IsNullOrEmpty(tresc) ? 0
                : (tresc.Length <= 70 ? 1 : 1 + (int)Math.Ceiling((tresc.Length - 70) / 67.0));

            // #2 Auto-wpis do ContactHistory + snooze 3 dni
            int? lpWst = row["LP"] != DBNull.Value ? Convert.ToInt32(row["LP"]) : (int?)null;
            string nazwaWariantu = wariant == "pelny" ? "Pełne potwierdzenie" : "Krótkie potwierdzenie";
            ZapiszContactSmsAutomatycznie(lpWst, dostawca, "Potwierdzenie (skrót) — " + nazwaWariantu, snoozeDays: 3);

            string info = $"✅ Skopiowano (skrót S) — {nazwaWariantu}\n\n";
            info += $"Hodowca: {dostawca}\n";
            info += string.IsNullOrEmpty(telefon) ? "Telefon: ⚠️ BRAK NUMERU!\n" : $"Telefon: {telefon}\n";
            info += $"Długość: {tresc.Length} znaków  ({smsCount} SMS w UCS-2)\n";
            info += $"📝 Zapisano w Historia kontaktów\n";
            info += $"\n— TREŚĆ SMS —\n{tresc}";

            MessageBox.Show(info, "SMS potwierdzający skopiowany",
                MessageBoxButton.OK, MessageBoxImage.Information);

            LoadHistoria();
            LoadDoPotwierdzenia();
            OdswiezStatusBar();
        }

        // === #3 INLINE EDIT TELEFONU — implementacja dialogu ===
        // Dwuklik na komórce Tel w przypomnieniach → otwiera dialog do wpisania numeru.
        // Logika podobna do MenuDodajNumer_Click ale wywoływana od razu z double-click.
        internal void EdytujTelefonInline(DataRowView row)
        {
            if (row == null || row["Dostawca"] == DBNull.Value) return;
            string dostawca = Convert.ToString(row["Dostawca"]) ?? "";

            // Pobierz aktualne numery
            string phone1 = "", phone2 = "", phone3 = "";
            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();
                using var cmd = new SqlCommand(
                    "SELECT ISNULL(Phone1,''), ISNULL(Phone2,''), ISNULL(Phone3,'') FROM dbo.Dostawcy WHERE ShortName = @d", conn);
                cmd.Parameters.AddWithValue("@d", dostawca);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    phone1 = r.GetString(0);
                    phone2 = r.GetString(1);
                    phone3 = r.GetString(2);
                }
            }
            catch { }

            var dialog = new OknoDodaniaNumeruDialog(dostawca, phone1, phone2, phone3);
            if (dialog.ShowDialog() != true) return;

            int rows = ZapiszNumeryHodowcy(dostawca, dialog.NumerTelefonu, dialog.NumerTelefonu2, dialog.NumerTelefonu3);
            if (rows <= 0) return; // diagnostyka i komunikat są w środku

            // Odśwież listy żeby nowy numer pokazał się w Tel kolumnie
            LoadPrzypomnienia();
            LoadDoPotwierdzenia();
        }

        // ====== WSPÓLNA METODA ZAPISU NUMERÓW HODOWCY (3 miejsca używają tego samego UPDATE) ======
        //
        // UPDATE dbo.Dostawcy SET Phone1/2/3 WHERE ShortName = @d. Gdy 0 wierszy zmienionych —
        // pokazuje diagnostykę: szuka podobnych ShortName i informuje co się stało.
        // Zwraca liczbę zmienionych wierszy (0 = nie udało się, >0 = sukces).
        internal int ZapiszNumeryHodowcy(string dostawca, string? p1, string? p2, string? p3)
        {
            if (string.IsNullOrWhiteSpace(dostawca))
            {
                MessageBox.Show("⚠️ Brak nazwy hodowcy.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return 0;
            }

            int rows;
            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();
                using var cmd = new SqlCommand(
                    "UPDATE dbo.Dostawcy SET Phone1=@p1, Phone2=@p2, Phone3=@p3 WHERE ShortName=@d", conn);
                cmd.Parameters.AddWithValue("@p1", (object?)p1 ?? "");
                cmd.Parameters.AddWithValue("@p2", (object?)p2 ?? "");
                cmd.Parameters.AddWithValue("@p3", (object?)p3 ?? "");
                cmd.Parameters.AddWithValue("@d", dostawca);
                rows = cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd zapisu numeru: " + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return 0;
            }

            if (rows > 0)
            {
                var lista = new List<string>();
                if (!string.IsNullOrEmpty(p1)) lista.Add(p1!);
                if (!string.IsNullOrEmpty(p2)) lista.Add(p2!);
                if (!string.IsNullOrEmpty(p3)) lista.Add(p3!);
                MessageBox.Show($"✅ Zapisano numery dla {dostawca}:\n{string.Join("\n", lista)}",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                return rows;
            }

            // ROWS == 0 — UPDATE nie znalazł rekordu. Diagnoza: pokaż podobnych hodowców.
            DiagnozujBrakHodowcy(dostawca);
            return 0;
        }

        private void DiagnozujBrakHodowcy(string dostawca)
        {
            string trimmed = (dostawca ?? "").Trim();
            string preFraza = trimmed.Length >= 3 ? trimmed.Substring(0, 3) : trimmed;
            var podobni = new List<(string ShortName, string LongName)>();

            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();
                using var cmd = new SqlCommand(@"
                    SELECT TOP 10 ShortName, ISNULL(LongName,'')
                    FROM dbo.Dostawcy
                    WHERE ShortName LIKE @pre + '%' OR LongName LIKE '%' + @pre + '%'
                    ORDER BY ShortName", conn);
                cmd.Parameters.AddWithValue("@pre", preFraza);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    podobni.Add((r.GetString(0), r.GetString(1)));
            }
            catch { }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"⚠️ Nie udało się zapisać numerów dla hodowcy:");
            sb.AppendLine($"   \"{dostawca}\"");
            sb.AppendLine();
            sb.AppendLine($"Przyczyna: w tabeli dbo.Dostawcy (LibraNet) nie ma rekordu o ShortName = \"{trimmed}\".");
            sb.AppendLine();
            sb.AppendLine("Możliwe powody:");
            sb.AppendLine("• Hodowca jest tylko w WstawieniaKurczakow, nie ma w słowniku Dostawcy");
            sb.AppendLine("• Spacja/literówka w nazwie (akcent, kreska, podwójna spacja)");
            sb.AppendLine("• Hodowca dodawany w Libra/Raporty.exe innym ShortName");
            sb.AppendLine();
            if (podobni.Count > 0)
            {
                sb.AppendLine($"Podobni hodowcy w bazie (zaczynający się na \"{preFraza}\"):");
                foreach (var p in podobni.Take(10))
                    sb.AppendLine($"  • {p.ShortName}" + (string.IsNullOrEmpty(p.LongName) ? "" : $"  ({p.LongName})"));
            }
            else
            {
                sb.AppendLine($"⛔ Nie znaleziono żadnych hodowców pasujących do \"{preFraza}\".");
                sb.AppendLine("Hodowca musi najpierw zostać dodany do tabeli Dostawcy (Libra/Raporty.exe).");
            }

            MessageBox.Show(sb.ToString(), "Brak rekordu w Dostawcy",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
