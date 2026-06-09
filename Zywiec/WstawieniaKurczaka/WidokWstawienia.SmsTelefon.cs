using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.Zywiec.Kalendarz.Dialogs;
using Kalendarz1.Zywiec.Kalendarz.Services;

namespace Kalendarz1
{
    // Integracja z wysyłką SMS przez telefon (MacroDroid webhook).
    // Wspólny helper dla 3 miejsc wysyłki w Cykle Wstawień:
    //  - WyslijSmsZWstawien (Lista wstawień, 8 wariantów)
    //  - WyslijSmsWariant (Przypomnienia, 8 wariantów)
    //  - MenuSmsPotwierdzenie_Click (Nadchodzące, 2 warianty potwierdzenia)
    //
    // Każda metoda wywołuje WyslijSmsLubPokaSchowek(...) PRZED swoim zapisem do
    // ContactHistory — jeśli user anulował dialog, zwraca false, kod wywołujący
    // pomija zapis (nie zapamiętujemy fałszywej "wysyłki").
    public partial class WidokWstawienia
    {
        // Zwraca:
        //   true  → SMS wysłany przez telefon LUB skopiowany do schowka (legacy)
        //           → kod wywołujący POWINIEN zapisać do ContactHistory i odświeżyć historię
        //   false → user anulował dialog → NIE zapisuj
        internal bool WyslijSmsLubPokaSchowek(
            string dostawca,
            string telefon,
            string tresc,
            string nazwaWariantu,
            out string finalnyTekst,
            string? tytulMessageBox = null)
        {
            finalnyTekst = tresc;

            // Brak numeru → zaproponuj dodanie teraz (po dodaniu kontynuuj z nowym numerem)
            if ((string.IsNullOrWhiteSpace(telefon) || telefon == "-") && !string.IsNullOrWhiteSpace(dostawca))
            {
                // Synchroniczne wywołanie async helpera — używamy GetAwaiter().GetResult()
                // bo cała ta metoda jest sync. To OK na wątku UI bo dialog jest modalny.
                var task = DodanieNumeruHelper.ZaproponujDodanieAsync(this, connectionString, dostawca);
                task.GetAwaiter().GetResult();
                if (!string.IsNullOrWhiteSpace(task.Result))
                {
                    telefon = task.Result!;
                }
                // Jeśli user anulował — kontynuujemy z pustym, user może wpisać ręcznie w dialogu SMS
            }

            // Ścieżka NOWA — gdy telefon (MacroDroid) skonfigurowany dla zalogowanego usera
            if (WyslijSmsDialog.CzyTelefonSkonfigurowany(App.UserID))
            {
                string dostawcaInfo = $"📝 {nazwaWariantu}\n👤 Hodowca: {dostawca}" +
                    (string.IsNullOrEmpty(telefon) ? "\n⚠️ Brak numeru w bazie hodowcy" : "");
                var dlg = new WyslijSmsDialog(
                    dostawcaInfo, dostawca, telefon, tresc, connectionString, App.UserID) { Owner = this };
                bool? wynik = dlg.ShowDialog();
                if (wynik != true) return false;
                finalnyTekst = string.IsNullOrEmpty(dlg.FinalTresc) ? tresc : dlg.FinalTresc;
                return true;
            }

            // Ścieżka STARA — schowek + MessageBox (legacy gdy nikt nie skonfigurował telefonu)
            try { Clipboard.SetText(tresc); } catch { }
            string info = $"✅ Skopiowano SMS do schowka — {nazwaWariantu}\n\n";
            info += $"Hodowca: {dostawca}\n";
            info += string.IsNullOrEmpty(telefon) || telefon == "-"
                ? "Telefon: ⚠️ BRAK NUMERU!\n"
                : $"Telefon: {telefon}\n";
            info += $"Długość: {tresc.Length} znaków\n\n— TREŚĆ SMS —\n{tresc}";
            MessageBox.Show(info,
                tytulMessageBox ?? $"SMS — {nazwaWariantu}",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return true;
        }

        // ===== 📞 ZADZWOŃ DO HODOWCY (3 menu kontekstowe) =====
        // Lista wstawień
        private async void MenuZadzwonHodowcyWst_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridWstawienia.SelectedItem is not DataRowView row)
            {
                MessageBox.Show("Wybierz wstawienie z listy.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string dostawca = row["Dostawca"] != DBNull.Value ? Convert.ToString(row["Dostawca"]) ?? "" : "";
            string? telefonRekord = row.Row.Table.Columns.Contains("Telefon") && row["Telefon"] != DBNull.Value
                ? Convert.ToString(row["Telefon"]) : null;
            await ZadzwonZUI(dostawca, telefonRekord);
        }

        // Przypomnienia
        private async void MenuZadzwonHodowcyPrz_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridPrzypomnienia.SelectedItem is not DataRowView row)
            {
                MessageBox.Show("Wybierz przypomnienie z listy.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string dostawca = Convert.ToString(row["Dostawca"]) ?? "";
            string? telefonRekord = PobierzNumerTelefonu(row);
            await ZadzwonZUI(dostawca, telefonRekord);
        }

        // Nadchodzące wstawienia (do potwierdzenia)
        private async void MenuZadzwonHodowcyDoPotwierdzenia_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridDoPotwierdzenia.SelectedItem is not DataRowView row)
            {
                MessageBox.Show("Wybierz wstawienie z listy.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string dostawca = row["Dostawca"] != DBNull.Value ? Convert.ToString(row["Dostawca"]) ?? "" : "";
            string? telefonRekord = row.Row.Table.Columns.Contains("Telefon") && row["Telefon"] != DBNull.Value
                ? Convert.ToString(row["Telefon"]) : null;
            await ZadzwonZUI(dostawca, telefonRekord);
        }

        // Wspólna logika UI po wywołaniu ZadzwonHelper
        private async System.Threading.Tasks.Task ZadzwonZUI(string dostawca, string? telefonZRekordu)
        {
            var wynik = await ZadzwonHelper.ZadzwonDoDostawcyAsync(
                this, connectionString, App.UserID, dostawca, telefonZRekordu, zrodlo: "Cykle Wstawien");

            if (wynik.UserAnulowal) return;

            if (wynik.Sukces)
            {
                // Toast jeśli istnieje (z polish UI), inaczej MessageBox
                try { ShowToast(wynik.Komunikat); }
                catch { MessageBox.Show(wynik.Komunikat, "Połączenie", MessageBoxButton.OK, MessageBoxImage.Information); }
            }
            else if (wynik.BrakNumeru)
            {
                MessageBox.Show(wynik.Komunikat, "Brak numeru telefonu",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else if (wynik.TelefonNieSkonfigurowany)
            {
                MessageBox.Show(wynik.Komunikat, "Telefon nieskonfigurowany",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    $"Telefon nieosiągalny — sprawdź MacroDroid / IP / sieć.\n\nSzczegóły:\n{wynik.Komunikat}",
                    "Błąd połączenia", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Pomocniczy toast — używa istniejącego mechanizmu z innych partial files, jeśli istnieje.
        // W razie braku — pokaże MessageBox. Sprawdzamy przez try/catch w wywoływaczu.
        private void ShowToast(string komunikat)
        {
            // Wstaw do paska statusu jeśli jest pole statusWstawienia
            try
            {
                if (FindName("statusWstawienia") is TextBlock tb) tb.Text = komunikat;
            }
            catch { }
        }
    }
}
