using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Kalendarz1.Customer360.Services
{
    /// <summary>Trwała lista ostatnio otwieranych klientów (per użytkownik Windows, plik w LOCALAPPDATA).</summary>
    public static class RecentClientsStore
    {
        private const int MaxItems = 15;

        private static string PlikPath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Kalendarz1", "Customer360");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "ostatni_klienci.txt");
            }
        }

        public record RecentItem(int Id, string Nazwa, DateTime Kiedy);

        public static void Add(int id, string nazwa)
        {
            try
            {
                var lista = Get();
                lista.RemoveAll(x => x.Id == id);                    // usuń duplikat
                lista.Insert(0, new RecentItem(id, nazwa ?? "", DateTime.Now));
                var trim = lista.Take(MaxItems);
                File.WriteAllLines(PlikPath,
                    trim.Select(x => $"{x.Id}|{x.Kiedy:o}|{x.Nazwa.Replace("|", " ")}"));
            }
            catch { /* best-effort */ }
        }

        public static List<RecentItem> Get()
        {
            var wynik = new List<RecentItem>();
            try
            {
                if (!File.Exists(PlikPath)) return wynik;
                foreach (var line in File.ReadAllLines(PlikPath))
                {
                    var p = line.Split('|', 3);
                    if (p.Length >= 3 && int.TryParse(p[0], out int id))
                    {
                        DateTime.TryParse(p[1], null, System.Globalization.DateTimeStyles.RoundtripKind, out var kiedy);
                        wynik.Add(new RecentItem(id, p[2], kiedy));
                    }
                }
            }
            catch { }
            return wynik;
        }
    }
}
