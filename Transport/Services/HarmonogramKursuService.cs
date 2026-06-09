// ════════════════════════════════════════════════════════════════════════════
// Transport/Services/HarmonogramKursuService.cs
//
// Wrapper na EtaService — pobiera dane z LibraNet (GPS klientów, czasy rozładunku,
// odcinki EstymacjeTras) i zwraca pełen harmonogram kursu:
//
//   Wyjazd 06:00 [Koziołki 40, Dmosin]
//     → 06:42 jazda 42 km (28 min) → Klient A [✓ z historii]
//     ⏸ rozładunek 95 min → odjazd 08:17
//     → 08:55 jazda 31 km (38 min) → Klient B [~ szacowany Haversine]
//     ⏸ rozładunek 30 min → odjazd 09:25
//     → 11:10 powrót do bazy (105 km)
//   Razem: 5h 10min, 178 km
//
// Reuse: EtaService.Calculate + CzasRozladunkuService + HistoriaRozladunkuService.
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Transport.Services
{
    public class HarmonogramKursuService
    {
        private const string ConnLibraDefault =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private readonly string _connLibra;
        private readonly CzasRozladunkuService _czasSvc;
        private readonly HistoriaRozladunkuService _historiaSvc;
        private readonly EtaService _etaSvc = new();

        public HarmonogramKursuService() : this(ConnLibraDefault) { }
        public HarmonogramKursuService(string connLibra)
        {
            _connLibra = connLibra;
            _czasSvc = new CzasRozladunkuService(connLibra);
            _historiaSvc = new HistoriaRozladunkuService();
        }

        /// <summary>Wejście: jeden punkt kursu (kolejność + ID zamówienia lub bezpośrednio klient).</summary>
        public class WejsciePunkt
        {
            public int Kolejnosc { get; set; }
            /// <summary>ID zamówienia LibraNet.ZamowieniaMieso.Id (gdy KlientId nieznany). Opcjonalne.</summary>
            public int? ZamowienieId { get; set; }
            /// <summary>ID klienta Sage (gdy znany bezpośrednio). Priorytet przed ZamowienieId.</summary>
            public int? KlientId { get; set; }
            /// <summary>Nazwa do wyświetlenia.</summary>
            public string Nazwa { get; set; } = "";
        }

        /// <summary>Wynik dla jednego kroku w harmonogramie — UI to bezpośrednio renderuje.</summary>
        public class Krok
        {
            public int Kolejnosc { get; set; }
            public string Nazwa { get; set; } = "";
            public int? KlientId { get; set; }
            public TimeSpan GodzPrzyjazdu { get; set; }
            public TimeSpan GodzOdjazdu { get; set; }
            public double KmOdPoprzedniego { get; set; }
            public TimeSpan CzasJazdy { get; set; }
            public int RozladunekMin { get; set; }
            /// <summary>Czy czas jazdy z historii Webfleet (true) czy szacowany Haversine (false).</summary>
            public bool CzasJazdyZHistorii { get; set; }
            /// <summary>Czy mamy GPS klienta.</summary>
            public bool MaGps { get; set; }
        }

        public class WynikHarmonogramu
        {
            public TimeSpan GodzWyjazdu { get; set; }
            public TimeSpan GodzPowrotu { get; set; }
            public double TotalKm { get; set; }
            public double PowrotKm { get; set; }
            public TimeSpan CalkowityCzas { get; set; }
            public List<Krok> Kroki { get; set; } = new();
            public List<string> Ostrzezenia { get; set; } = new();
        }

        /// <summary>
        /// Główna metoda — pobiera GPS klientów + czasy rozładunku + odcinki + liczy harmonogram.
        /// </summary>
        public async Task<WynikHarmonogramu> ObliczAsync(TimeSpan godzWyjazdu, IEnumerable<WejsciePunkt> punkty)
        {
            var wynik = new WynikHarmonogramu { GodzWyjazdu = godzWyjazdu };
            var listaPunktow = punkty.OrderBy(p => p.Kolejnosc).ToList();
            if (listaPunktow.Count == 0)
            {
                wynik.Ostrzezenia.Add("Brak punktów w kursie — dodaj klientów.");
                return wynik;
            }

            // 1) Wyciągnij KlientId dla punktów które mają tylko ZamowienieId
            var zamIdsBezKl = listaPunktow.Where(p => !p.KlientId.HasValue && p.ZamowienieId.HasValue)
                                          .Select(p => p.ZamowienieId!.Value).Distinct().ToList();
            if (zamIdsBezKl.Count > 0)
            {
                var mapping = await _czasSvc.PobierzCzasyDlaZamowienAsync(zamIdsBezKl);
                foreach (var p in listaPunktow.Where(p => !p.KlientId.HasValue && p.ZamowienieId.HasValue))
                {
                    if (mapping.TryGetValue(p.ZamowienieId!.Value, out var t)) p.KlientId = t.KlientId;
                }
            }

            // 2) Pobierz czasy rozładunku per klient (z karty / historii / default)
            var klientIds = listaPunktow.Where(p => p.KlientId.HasValue).Select(p => p.KlientId!.Value).Distinct().ToList();
            var czasyRozladunku = await _czasSvc.PobierzCzasyAsync(klientIds);

            // 3) Pobierz GPS dla klientów
            var gps = await _historiaSvc.PobierzKlientowZGpsAsync();

            // 4) Pobierz odcinki tras (Webfleet history)
            var odcinki = await _historiaSvc.PobierzWszystkieOdcinkiAsync();

            // 5) Buduj StopInput dla EtaService
            var stops = new List<EtaService.StopInput>();
            foreach (var p in listaPunktow)
            {
                bool maGps = p.KlientId.HasValue && gps.TryGetValue(p.KlientId.Value, out var gg);
                double lat = 0, lon = 0;
                if (maGps) { lat = gps[p.KlientId!.Value].Lat; lon = gps[p.KlientId!.Value].Lon; }

                int? rozladunek = p.KlientId.HasValue && czasyRozladunku.TryGetValue(p.KlientId.Value, out var r) ? r : (int?)null;

                stops.Add(new EtaService.StopInput
                {
                    Kolejnosc = p.Kolejnosc,
                    NazwaKlienta = p.Nazwa,
                    Latitude = lat,
                    Longitude = lon,
                    KlientId = p.KlientId,
                    RozladunekMin = rozladunek
                });

                if (!maGps) wynik.Ostrzezenia.Add($"#{p.Kolejnosc} {p.Nazwa} — brak GPS, używamy szacunku.");
                if (!p.KlientId.HasValue) wynik.Ostrzezenia.Add($"#{p.Kolejnosc} {p.Nazwa} — brak KlientId, używamy 30 min jazdy.");
            }

            // 6) Wywołaj EtaService
            var eta = _etaSvc.Calculate(godzWyjazdu, stops, odcinki);

            // 7) Konwertuj na nasz WynikHarmonogramu z informacją "z historii / szacowany"
            int prevLokId = EtaService.BazaLokId;
            for (int i = 0; i < eta.Stops.Count; i++)
            {
                var s = eta.Stops[i];
                var p = listaPunktow[i];
                bool zHist = p.KlientId.HasValue && odcinki.ContainsKey((prevLokId, p.KlientId.Value));
                wynik.Kroki.Add(new Krok
                {
                    Kolejnosc = s.Kolejnosc,
                    Nazwa = s.NazwaKlienta,
                    KlientId = p.KlientId,
                    GodzPrzyjazdu = s.Eta,
                    GodzOdjazdu = s.DepartureAfterUnload,
                    KmOdPoprzedniego = s.DistanceFromPrevKm,
                    CzasJazdy = s.DriveTime,
                    RozladunekMin = s.UnloadMin,
                    CzasJazdyZHistorii = zHist,
                    MaGps = s.HasCoordinates
                });
                if (p.KlientId.HasValue) prevLokId = p.KlientId.Value;
            }

            wynik.GodzPowrotu = eta.EstimatedReturnTime;
            wynik.TotalKm = eta.TotalDistanceKm + eta.ReturnDistanceKm;
            wynik.PowrotKm = eta.ReturnDistanceKm;
            wynik.CalkowityCzas = eta.TotalDuration;

            return wynik;
        }

        /// <summary>
        /// Pomocnicze formatowanie do TextBox/Label — wieloliniowy raport gotowy do wyświetlenia.
        /// </summary>
        public static string Sformatuj(WynikHarmonogramu w)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"🚛 WYJAZD {w.GodzWyjazdu:hh\\:mm}   ·   Baza Koziołki 40, 95-061 Dmosin");
            sb.AppendLine();
            for (int i = 0; i < w.Kroki.Count; i++)
            {
                var k = w.Kroki[i];
                string flag = k.CzasJazdyZHistorii ? "✓ historia" : (k.MaGps ? "~ Haversine" : "⚠ bez GPS");
                sb.AppendLine($"   ↓ jazda {k.KmOdPoprzedniego:F0} km ({(int)k.CzasJazdy.TotalMinutes} min)  [{flag}]");
                sb.AppendLine($"📍 {k.GodzPrzyjazdu:hh\\:mm}  #{k.Kolejnosc}  {k.Nazwa}");
                sb.AppendLine($"   ⏸ rozładunek {k.RozladunekMin} min  →  odjazd {k.GodzOdjazdu:hh\\:mm}");
            }
            sb.AppendLine($"   ↓ powrót do bazy ({w.PowrotKm:F0} km)");
            sb.AppendLine($"🏁 POWRÓT {w.GodzPowrotu:hh\\:mm}");
            sb.AppendLine();
            sb.AppendLine($"📊 RAZEM: {w.CalkowityCzas.TotalHours:F1} h ({(int)w.CalkowityCzas.TotalMinutes} min) · {w.TotalKm:F0} km");
            if (w.Ostrzezenia.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("⚠️ Ostrzeżenia:");
                foreach (var o in w.Ostrzezenia) sb.AppendLine($"   • {o}");
            }
            return sb.ToString();
        }
    }
}
