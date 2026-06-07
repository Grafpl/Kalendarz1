using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Kalendarz1.Reklamacje.Services
{
    // Czyta partie produkcyjne z pliku parametry.ini zapisywanego przez raport AMBASIC
    // dokvat01_F.sc (formula pod faktura w Symfonii Handel).
    //
    // Klucz w INI = ID dokumentu Symfonii (HM.MG.id). Dla zwyklej reklamacji uzywamy
    // IdDokumentu reklamacji; dla korekt z Symfonii — IdFakturyOryginalnej (partie sa
    // na fakturze bazowej, nie na korekcie).
    //
    // Format INI:
    //   [914005]
    //   nr_partii1=98024268004
    //   nr_partii2=
    //   ...
    //   nr_samochodu=PO12345
    //   data_prod=2026-05-28
    //   data_przyd_kurczak=2026-06-04
    //   data_przyd_elementy=2026-06-03
    //   data_przyd_podroby=2026-06-01
    //   poj_zdane=156
    //   pal_zdane_d=1
    //   pal_zdane_h1=3
    //   pal_wydane_euro=0
    //   pal_wydane_plastikowe=12
    public static class SymfoniaPartieReader
    {
        private static readonly string[] PathCandidates = new[]
        {
            @"\\192.168.0.170\Public\SymfoniaINI\parametry.ini",
            @"\\192.168.0.171\Public\SymfoniaINI\parametry.ini",
            // Lokalny dev-fallback (kopia robocza usera)
            @"C:\Users\PC\source\repos\Grafpl\Kalendarz1\Symfonia\parametry.ini"
        };

        public sealed class PartieData
        {
            public bool Found;
            public string ZrodloPliku; // ktora sciezka zadzialala
            public List<string> Partie = new List<string>(); // tylko niepuste
            public string NrSamochodu;
            public DateTime? DataProdukcji;
            public DateTime? DataPrzyd_Tuszka;
            public DateTime? DataPrzyd_Elementy;
            public DateTime? DataPrzyd_Podroby;
            public int? Pojemniki;
            public int? Pal_H1;
            public int? Pal_Drewn;
            public int? Pal_Euro;
            public int? Pal_Plast;
        }

        // Cache plikowy z mtime check — przy kazdym wywolaniu sprawdzamy czy plik sie zmienil.
        // Parsujemy CALY plik raz, potem trzymamy mape: section → dict(key→value).
        // Reuse miedzy panelami, miedzy reklamacjami — duzy zysk.
        private static readonly object _cacheLock = new object();
        private static Dictionary<string, Dictionary<string, string>> _cache;
        private static DateTime _cacheLoadedAt;
        private static string _cacheSource;
        private static DateTime _cacheFileMtime;
        private static readonly TimeSpan FRESHNESS = TimeSpan.FromSeconds(60); // re-check co 60s

        public static PartieData GetForDocument(int idDokumentu)
        {
            if (idDokumentu <= 0) return new PartieData { Found = false };
            try
            {
                var sections = GetCachedSections();
                if (sections == null) return new PartieData { Found = false };

                string key = idDokumentu.ToString(CultureInfo.InvariantCulture);
                if (!sections.TryGetValue(key, out var dict)) return new PartieData { Found = false };

                var d = new PartieData { Found = true, ZrodloPliku = _cacheSource };
                for (int i = 1; i <= 12; i++)
                {
                    if (dict.TryGetValue("nr_partii" + i, out string val) && !string.IsNullOrWhiteSpace(val))
                        d.Partie.Add(val.Trim());
                }
                d.NrSamochodu = TryGet(dict, "nr_samochodu");
                d.DataProdukcji = TryGetDate(dict, "data_prod");
                d.DataPrzyd_Tuszka = TryGetDate(dict, "data_przyd_kurczak");
                d.DataPrzyd_Elementy = TryGetDate(dict, "data_przyd_elementy");
                d.DataPrzyd_Podroby = TryGetDate(dict, "data_przyd_podroby");
                d.Pojemniki = TryGetInt(dict, "poj_zdane");
                d.Pal_H1 = TryGetInt(dict, "pal_zdane_h1");
                d.Pal_Drewn = TryGetInt(dict, "pal_zdane_d");
                d.Pal_Euro = TryGetInt(dict, "pal_wydane_euro");
                d.Pal_Plast = TryGetInt(dict, "pal_wydane_plastikowe");
                return d;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SymfoniaPartieReader.GetForDocument: " + ex.Message);
                return new PartieData { Found = false };
            }
        }

        public static void InvalidateCache()
        {
            lock (_cacheLock)
            {
                _cache = null;
                _cacheLoadedAt = DateTime.MinValue;
            }
        }

        // Rozbija pelny numer partii (jak wpisany na fakturze) na CustomerID + PartiaDB.
        // Format ZPSP: [CustomerID 3 cyfry][Partia 8 cyfr] = 11 znaków cyfrowych.
        // Przyklad: "98024268004" → CustomerID="980", PartiaDB="24268004"
        // Partia: YY(2) + DDD(3, dzien roku) + NNN(3, kolejny numer auta tego dnia)
        // CustomerID: 3 cyfry z prefix-zerem jak trzeba (np. "057", "032").
        //
        // Fallback: nieznany format → CustomerID=null, PartiaDB = oryginal (tak zachowuje
        // sie kompatybilnosc ze starymi formatami lub recznym wpisem 8-cyfrowym).
        public static (string CustomerID, string PartiaDB) RozbijNumerPartii(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return (null, raw);
            raw = raw.Trim();
            if (raw.Length == 11)
            {
                bool wszystkieCyfry = true;
                for (int i = 0; i < raw.Length; i++)
                    if (raw[i] < '0' || raw[i] > '9') { wszystkieCyfry = false; break; }
                if (wszystkieCyfry)
                    return (raw.Substring(0, 3), raw.Substring(3));
            }
            // Stary format / inny → potraktuj caly jako Partia
            return (null, raw);
        }

        // Reverse lookup: dla podanego numeru partii zwraca liste ID dokumentow Symfonii
        // ktore ja zawieraja. Sluzy do "kto jeszcze dostal te partie" — JOIN do HM.MG potem.
        // Skanuje caly cache INI; ~10ms dla typowego rozmiaru pliku.
        public static List<int> GetDocumentsForPartia(string nrPartii)
        {
            var wynik = new List<int>();
            if (string.IsNullOrWhiteSpace(nrPartii)) return wynik;
            try
            {
                var sections = GetCachedSections();
                if (sections == null) return wynik;
                string target = nrPartii.Trim();
                foreach (var kv in sections)
                {
                    if (!int.TryParse(kv.Key, out int idDok)) continue;
                    var dict = kv.Value;
                    for (int i = 1; i <= 12; i++)
                    {
                        if (dict.TryGetValue("nr_partii" + i, out string val) && string.Equals(val?.Trim(), target, StringComparison.OrdinalIgnoreCase))
                        {
                            wynik.Add(idDok);
                            break; // ten dokument ma te partie — nie sprawdzaj kolejnych slotow
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("GetDocumentsForPartia: " + ex.Message); }
            return wynik;
        }

        // ----- prywatne -----

        private static Dictionary<string, Dictionary<string, string>> GetCachedSections()
        {
            lock (_cacheLock)
            {
                // Freshness check — jezeli cache < 60s i plik nie zmieniony, reuse
                if (_cache != null && (DateTime.Now - _cacheLoadedAt) < FRESHNESS)
                    return _cache;

                // Spróbuj kandydatów po kolei
                string sourcePath = null;
                DateTime sourceMtime = DateTime.MinValue;
                foreach (var p in PathCandidates)
                {
                    try
                    {
                        if (File.Exists(p))
                        {
                            sourcePath = p;
                            sourceMtime = File.GetLastWriteTime(p);
                            break;
                        }
                    }
                    catch { /* timeout / no permission — sprobuj kolejny */ }
                }
                if (sourcePath == null)
                {
                    // Nie ma zadnego zrodla — zachowujemy stary cache jezeli istnieje
                    return _cache;
                }

                // Jezeli plik nie zmienil sie i cache istnieje — odswiez tylko loaded-at
                if (_cache != null && _cacheSource == sourcePath && _cacheFileMtime == sourceMtime)
                {
                    _cacheLoadedAt = DateTime.Now;
                    return _cache;
                }

                // Parsuj plik (otwierany jako read-only, share read+write zeby Symfonia mogla pisac)
                var parsed = ParseFile(sourcePath);
                _cache = parsed;
                _cacheSource = sourcePath;
                _cacheFileMtime = sourceMtime;
                _cacheLoadedAt = DateTime.Now;
                return parsed;
            }
        }

        private static Dictionary<string, Dictionary<string, string>> ParseFile(string path)
        {
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            // FileShare ReadWrite — Symfonia rownolegle moze zapisywac nowe wpisy
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, DetectEncoding(path)))
            {
                string currentSection = null;
                Dictionary<string, string> currentDict = null;
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = line.Substring(1, line.Length - 2).Trim();
                        currentDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        result[currentSection] = currentDict;
                        continue;
                    }
                    if (currentDict == null) continue;

                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();
                    currentDict[k] = v;
                }
            }
            return result;
        }

        private static Encoding DetectEncoding(string path)
        {
            // AMBASIC zapisuje INI w Windows-1250 lub UTF-8 z BOM. Detect via BOM, default 1250.
            try
            {
                byte[] preamble = new byte[3];
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int n = fs.Read(preamble, 0, 3);
                    if (n >= 3 && preamble[0] == 0xEF && preamble[1] == 0xBB && preamble[2] == 0xBF) return Encoding.UTF8;
                    if (n >= 2 && preamble[0] == 0xFF && preamble[1] == 0xFE) return Encoding.Unicode;
                }
            }
            catch { }
            // Default — Windows-1250 (polskie znaki w AMBASIC)
            try { return Encoding.GetEncoding(1250); } catch { return Encoding.UTF8; }
        }

        private static string TryGet(Dictionary<string, string> d, string k)
        {
            return d.TryGetValue(k, out string v) && !string.IsNullOrWhiteSpace(v) && v != "Brak auta" ? v.Trim() : null;
        }

        private static DateTime? TryGetDate(Dictionary<string, string> d, string k)
        {
            string s = TryGet(d, k);
            if (string.IsNullOrEmpty(s)) return null;
            // Format YYYY-MM-DD lub odrzucamy placeholdery typu "Brak ..."
            if (s.StartsWith("Brak", StringComparison.OrdinalIgnoreCase)) return null;
            if (DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt)) return dt;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) return dt;
            return null;
        }

        private static int? TryGetInt(Dictionary<string, string> d, string k)
        {
            string s = TryGet(d, k);
            if (string.IsNullOrEmpty(s) || s == "0") return null;
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) return n;
            return null;
        }
    }
}
