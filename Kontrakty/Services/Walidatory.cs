using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Kalendarz1.Kontrakty.Models;

namespace Kalendarz1.Kontrakty.Services
{
    /// <summary>Walidacje pól kontraktu (suma kontrolna NIP/PESEL, nr ARiMR, sekwencja harmonogramu).</summary>
    public static class Walidatory
    {
        private static string Cyfry(string? s) => new string((s ?? "").Where(char.IsDigit).ToArray());

        /// <summary>NIP — 10 cyfr, suma kontrolna (wagi 6,5,7,2,3,4,5,6,7).</summary>
        public static bool NipPoprawny(string? nip)
        {
            var d = Cyfry(nip);
            if (d.Length != 10) return false;
            int[] w = { 6, 5, 7, 2, 3, 4, 5, 6, 7 };
            int suma = 0;
            for (int i = 0; i < 9; i++) suma += (d[i] - '0') * w[i];
            int kontrolna = suma % 11;
            if (kontrolna == 10) return false;
            return kontrolna == (d[9] - '0');
        }

        /// <summary>PESEL — 11 cyfr, suma kontrolna (wagi 1,3,7,9,1,3,7,9,1,3).</summary>
        public static bool PeselPoprawny(string? pesel)
        {
            var d = Cyfry(pesel);
            if (d.Length != 11) return false;
            int[] w = { 1, 3, 7, 9, 1, 3, 7, 9, 1, 3 };
            int suma = 0;
            for (int i = 0; i < 10; i++) suma += (d[i] - '0') * w[i];
            int kontrolna = (10 - (suma % 10)) % 10;
            return kontrolna == (d[10] - '0');
        }

        /// <summary>Numer gospodarstwa ARiMR: PL + 9 cyfr (akceptuje też same 9 cyfr — legacy import).</summary>
        public static bool ArimrPoprawny(string? nr)
        {
            var s = (nr ?? "").Trim().ToUpperInvariant().Replace(" ", "");
            return Regex.IsMatch(s, @"^PL\d{9}$") || Regex.IsMatch(s, @"^\d{9}$");
        }

        /// <summary>
        /// Harmonogram: daty wstawień rosnące + odstęp 50-70 dni. Zwraca listę komunikatów
        /// (BŁĄD: ... blokuje; UWAGA: ... tylko ostrzega). Pusta lista = OK.
        /// </summary>
        public static List<string> WalidujHarmonogram(IEnumerable<HarmonogramCykl> cykle)
        {
            var bledy = new List<string>();
            var z = cykle.Where(c => c.DataWstawienia.HasValue)
                         .OrderBy(c => c.NrCyklu).ToList();
            for (int i = 1; i < z.Count; i++)
            {
                var poprz = z[i - 1].DataWstawienia!.Value;
                var biez = z[i].DataWstawienia!.Value;
                if (biez <= poprz)
                {
                    bledy.Add($"BŁĄD: cykl {z[i].NrCyklu} ma datę wstawienia nie późniejszą niż cykl {z[i - 1].NrCyklu}.");
                    continue;
                }
                int dni = (biez - poprz).Days;
                if (dni < 50 || dni > 70)
                    bledy.Add($"UWAGA: odstęp cykl {z[i - 1].NrCyklu}→{z[i].NrCyklu} = {dni} dni (poza 50-70).");
            }
            return bledy;
        }

        public static bool MaBlad(IEnumerable<string> komunikaty) => komunikaty.Any(k => k.StartsWith("BŁĄD"));
    }
}
