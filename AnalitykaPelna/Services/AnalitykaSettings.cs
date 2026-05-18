using System;
using System.IO;
using Newtonsoft.Json;

namespace Kalendarz1.AnalitykaPelna.Services
{
    /// <summary>
    /// Persystencja stanu Analityki: ostatnia zakładka, ostatnie filtry, folder eksportu.
    /// Plik: %LOCALAPPDATA%\Kalendarz1\analityka.json
    /// </summary>
    public static class AnalitykaSettings
    {
        public static int OstatniaZakladka { get; set; } = 2;  // domyślnie Bilans
        public static DateTime? OstatniaDataOd { get; set; }
        public static DateTime? OstatniaDataDo { get; set; }
        public static int OstatniLiczbaTygodniPrognozy { get; set; } = 8;
        public static string? OstatniFolderEksportu { get; set; }
        public static bool LiveAktywneNaStarcie { get; set; }

        private static bool _zaladowano;
        private static readonly object _lock = new();

        private static string Sciezka =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Kalendarz1", "analityka.json");

        public static void Zaladuj()
        {
            if (_zaladowano) return;
            lock (_lock)
            {
                if (_zaladowano) return;
                try
                {
                    if (File.Exists(Sciezka))
                    {
                        var json = File.ReadAllText(Sciezka);
                        var snap = JsonConvert.DeserializeObject<Snapshot>(json);
                        if (snap != null)
                        {
                            OstatniaZakladka = snap.OstatniaZakladka;
                            OstatniaDataOd = snap.OstatniaDataOd;
                            OstatniaDataDo = snap.OstatniaDataDo;
                            OstatniLiczbaTygodniPrognozy = snap.OstatniLiczbaTygodniPrognozy > 0
                                ? snap.OstatniLiczbaTygodniPrognozy : 8;
                            OstatniFolderEksportu = snap.OstatniFolderEksportu;
                            LiveAktywneNaStarcie = snap.LiveAktywneNaStarcie;
                        }
                    }
                }
                catch
                {
                    // Plik mógł zostać uszkodzony — używamy defaultów, nie psujemy aplikacji
                }
                _zaladowano = true;
            }
        }

        public static void Zapisz()
        {
            try
            {
                var dir = Path.GetDirectoryName(Sciezka);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var snap = new Snapshot
                {
                    OstatniaZakladka = OstatniaZakladka,
                    OstatniaDataOd = OstatniaDataOd,
                    OstatniaDataDo = OstatniaDataDo,
                    OstatniLiczbaTygodniPrognozy = OstatniLiczbaTygodniPrognozy,
                    OstatniFolderEksportu = OstatniFolderEksportu,
                    LiveAktywneNaStarcie = LiveAktywneNaStarcie
                };
                File.WriteAllText(Sciezka, JsonConvert.SerializeObject(snap, Formatting.Indented));
            }
            catch
            {
                // Brak dostępu do dysku nie powinien zatrzymać aplikacji
            }
        }

        private class Snapshot
        {
            public int OstatniaZakladka { get; set; }
            public DateTime? OstatniaDataOd { get; set; }
            public DateTime? OstatniaDataDo { get; set; }
            public int OstatniLiczbaTygodniPrognozy { get; set; }
            public string? OstatniFolderEksportu { get; set; }
            public bool LiveAktywneNaStarcie { get; set; }
        }
    }
}
