using Kalendarz1.Transport.Repozytorium;
using Kalendarz1.Zywiec.Kalendarz.Dialogs;
using Kalendarz1.Zywiec.Kalendarz.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1.Transport.Services
{
    // Wysyłka SMS-ów o kursach do kierowców — pojedynczo lub grupowo.
    // Most między WinForms (Transport) a WPF (WyslijSmsDialog).
    // Używa istniejącej infrastruktury: MacroDroidClient (/sms) + WyslijSmsDialog (preview UI).
    public static class TransportSmsService
    {
        // Wysyła SMS o pojedynczym kursie. Otwiera WyslijSmsDialog (WPF) z podglądem.
        public static async Task<bool> WyslijSmsPojedynczyAsync(
            TransportRepozytorium repo,
            Kurs kurs,
            Kierowca? kierowca,
            string connectionString,
            string? userId)
        {
            if (kurs == null) { ShowInfo("Brak danych kursu."); return false; }
            if (kierowca == null) { ShowInfo("Kurs nie ma przypisanego kierowcy."); return false; }
            if (string.IsNullOrWhiteSpace(kierowca.Telefon))
            {
                ShowInfo($"Kierowca {kierowca.PelneImie} nie ma telefonu w bazie.\n\n" +
                         "Dodaj numer w zakładce Kierowcy.");
                return false;
            }

            var ladunki = await repo.PobierzLadunkiAsync(kurs.KursID);
            var podsumowanie = TransportSmsBuilder.PrzygotujPodsumowanie(kurs, ladunki);
            string tresc = TransportSmsBuilder.ZbudujTresc(podsumowanie);

            string info = $"📦 Kurs {kurs.DataKursu:dd.MM.yyyy}  •  " +
                          $"👤 Kierowca: {kierowca.PelneImie}  •  " +
                          $"🚛 Auto: {kurs.PojazdRejestracja ?? "(brak)"}";

            // Otwórz dialog WPF (z preview, edycja treści, wybór schowek/telefon)
            bool sukces = false;
            await UruchomNaWpfDispatcher(() =>
            {
                var dlg = new WyslijSmsDialog(
                    info, kierowca.PelneImie, kierowca.Telefon, tresc, connectionString, userId);
                bool? wynik = dlg.ShowDialog();
                sukces = wynik == true && (dlg.SmsWyslanyPrzezTelefon || dlg.TylkoSchowek);
            });
            return sukces;
        }

        // Wysyła SMS grupowo — po kolei do wszystkich kierowców z dziennego harmonogramu.
        // Bez dialogu per-kierowca (zbyt pracochłonne). Pokazuje podsumowanie i potwierdza wysyłkę całej grupy.
        public static async Task<(int wyslane, int pominiete, int bledy)> WyslijSmsGrupoweAsync(
            TransportRepozytorium repo,
            DateTime dataKursu,
            string connectionString,
            string? userId)
        {
            var kursy = await repo.PobierzKursyPoDacieAsync(dataKursu);
            if (kursy.Count == 0)
            {
                ShowInfo($"Brak kursów na {dataKursu:dd.MM.yyyy}.");
                return (0, 0, 0);
            }

            // Pobierz wszystkich kierowców i pojazdy (do mapowania)
            var kierowcy = await repo.PobierzKierowcowAsync();
            var slownikKierowcow = kierowcy.ToDictionary(k => k.KierowcaID);

            // Filtruj kursy które mają przypisanego kierowcę z telefonem
            var kursyZKierowca = new List<(Kurs kurs, Kierowca kierowca)>();
            int bezKierowcy = 0, bezTelefonu = 0;
            foreach (var k in kursy)
            {
                if (!k.KierowcaID.HasValue) { bezKierowcy++; continue; }
                if (!slownikKierowcow.TryGetValue(k.KierowcaID.Value, out var kierowca))
                {
                    bezKierowcy++;
                    continue;
                }
                if (string.IsNullOrWhiteSpace(kierowca.Telefon)) { bezTelefonu++; continue; }
                kursyZKierowca.Add((k, kierowca));
            }

            if (kursyZKierowca.Count == 0)
            {
                ShowInfo($"Brak kursów z kierowcami z telefonem na {dataKursu:dd.MM.yyyy}.\n\n" +
                         $"Pominięto: {bezKierowcy} bez kierowcy, {bezTelefonu} bez telefonu.");
                return (0, bezKierowcy + bezTelefonu, 0);
            }

            // Potwierdzenie grupowej wysyłki
            string podsumLista = string.Join("\n",
                kursyZKierowca.Take(10).Select(p => $"  • {p.kierowca.PelneImie} ({p.kurs.PojazdRejestracja ?? "brak auta"})"));
            if (kursyZKierowca.Count > 10) podsumLista += $"\n  ... i {kursyZKierowca.Count - 10} więcej";

            var pytanie = MessageBox.Show(
                $"Wysłać SMS o kursie do {kursyZKierowca.Count} kierowców na {dataKursu:dd.MM.yyyy}?\n\n" +
                podsumLista +
                $"\n\nWysyłka po kolei z opóźnieniem 5s (ok. {kursyZKierowca.Count * 5}s razem).\n" +
                (bezKierowcy > 0 || bezTelefonu > 0
                    ? $"\n⚠ Pominięto: {bezKierowcy} bez kierowcy, {bezTelefonu} bez telefonu"
                    : ""),
                "📱 Grupowy SMS o kursie",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (pytanie != DialogResult.OK) return (0, 0, 0);

            int wyslane = 0, bledy = 0;
            foreach (var (kurs, kierowca) in kursyZKierowca)
            {
                var ladunki = await repo.PobierzLadunkiAsync(kurs.KursID);
                var podsumowanie = TransportSmsBuilder.PrzygotujPodsumowanie(kurs, ladunki);
                string tresc = TransportSmsBuilder.ZbudujTresc(podsumowanie);

                var wynik = await MacroDroidClient.WyslijSmsAsync(userId, kierowca.Telefon, tresc);
                if (wynik.Sukces) wyslane++;
                else bledy++;

                // Opóźnienie 5s żeby Android nie przeciążyć
                if (kursyZKierowca.IndexOf((kurs, kierowca)) < kursyZKierowca.Count - 1)
                    await Task.Delay(5000);
            }

            int pominiete = bezKierowcy + bezTelefonu;
            MessageBox.Show(
                $"📊 Wysyłka grupowa zakończona:\n\n" +
                $"✅ Wysłano: {wyslane}\n" +
                $"❌ Błędy: {bledy}\n" +
                $"⏭ Pominięto: {pominiete} (bez kierowcy/telefonu)",
                "Wynik grupowej wysyłki",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return (wyslane, pominiete, bledy);
        }

        // Uruchamia akcję na wątku WPF Dispatcher (potrzebne dla WyslijSmsDialog)
        private static Task UruchomNaWpfDispatcher(Action akcja)
        {
            var tcs = new TaskCompletionSource<bool>();
            var app = System.Windows.Application.Current;
            if (app != null)
            {
                app.Dispatcher.Invoke(() => { try { akcja(); } finally { tcs.SetResult(true); } });
            }
            else
            {
                // Brak WPF Application — sklamruj okno samego dialogu na threadzie głównym
                akcja();
                tcs.SetResult(true);
            }
            return tcs.Task;
        }

        private static void ShowInfo(string msg) =>
            MessageBox.Show(msg, "📱 SMS", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
