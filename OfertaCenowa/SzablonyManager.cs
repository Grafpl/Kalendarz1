using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Kalendarz1.OfertaCenowa
{
    /// <summary>
    /// Manager do zarządzania szablonami towarów i parametrów
    /// </summary>
    public class SzablonyManager
    {
        private static readonly string _folderSzablonow = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Kalendarz1",
            "Szablony"
        );

        private static readonly string _plikSzablonowTowarow = Path.Combine(_folderSzablonow, "szablony_towarow.json");
        private static readonly string _plikSzablonowParametrow = Path.Combine(_folderSzablonow, "szablony_parametrow.json");

        public SzablonyManager()
        {
            if (!Directory.Exists(_folderSzablonow))
            {
                Directory.CreateDirectory(_folderSzablonow);
            }
        }

        #region Szablony Towarów

        /// <summary>
        /// Zapisuje szablon towarów
        /// </summary>
        public void ZapiszSzablonTowarow(SzablonTowarow szablon)
        {
            var szablony = WczytajSzablonyTowarow();
            
            // Jeśli szablon o tym ID już istnieje, zastąp go
            var istniejacy = szablony.FirstOrDefault(s => s.Id == szablon.Id);
            if (istniejacy != null)
            {
                szablony.Remove(istniejacy);
            }
            else
            {
                // Dla nowego szablonu, ustaw nowe ID
                szablon.Id = szablony.Any() ? szablony.Max(s => s.Id) + 1 : 1;
            }

            szablony.Add(szablon);
            ZapiszDoPliku(_plikSzablonowTowarow, szablony);
        }

        /// <summary>
        /// Wczytuje wszystkie szablony towarów
        /// </summary>
        public List<SzablonTowarow> WczytajSzablonyTowarow()
        {
            return WczytajZPliku<SzablonTowarow>(_plikSzablonowTowarow);
        }

        /// <summary>
        /// Usuwa szablon towarów
        /// </summary>
        public void UsunSzablonTowarow(int id)
        {
            var szablony = WczytajSzablonyTowarow();
            szablony.RemoveAll(s => s.Id == id);
            ZapiszDoPliku(_plikSzablonowTowarow, szablony);
        }

        #endregion

        #region Szablony Parametrów

        /// <summary>
        /// Zapisuje szablon parametrów
        /// </summary>
        public void ZapiszSzablonParametrow(SzablonParametrow szablon)
        {
            var szablony = WczytajSzablonyParametrow();
            
            var istniejacy = szablony.FirstOrDefault(s => s.Id == szablon.Id);
            if (istniejacy != null)
            {
                szablony.Remove(istniejacy);
            }
            else
            {
                szablon.Id = szablony.Any() ? szablony.Max(s => s.Id) + 1 : 1;
            }

            szablony.Add(szablon);
            ZapiszDoPliku(_plikSzablonowParametrow, szablony);
        }

        /// <summary>
        /// Wczytuje wszystkie szablony parametrów
        /// </summary>
        public List<SzablonParametrow> WczytajSzablonyParametrow()
        {
            return WczytajZPliku<SzablonParametrow>(_plikSzablonowParametrow);
        }

        /// <summary>
        /// Usuwa szablon parametrów
        /// </summary>
        public void UsunSzablonParametrow(int id)
        {
            var szablony = WczytajSzablonyParametrow();
            szablony.RemoveAll(s => s.Id == id);
            ZapiszDoPliku(_plikSzablonowParametrow, szablony);
        }

        #endregion

        #region Metody pomocnicze

        private void ZapiszDoPliku<T>(string sciezka, List<T> dane)
        {
            var opcje = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string json = JsonSerializer.Serialize(dane, opcje);
            File.WriteAllText(sciezka, json);
        }

        private List<T> WczytajZPliku<T>(string sciezka)
        {
            if (!File.Exists(sciezka))
            {
                return new List<T>();
            }

            try
            {
                string json = File.ReadAllText(sciezka);
                return JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>();
            }
            catch
            {
                return new List<T>();
            }
        }

        #endregion

        #region Szablony domyślne

        /// <summary>
        /// Tworzy przykładowe szablony dla nowych użytkowników
        /// </summary>
        public void UtworzSzablonyPrzykladowe()
        {
            // Sprawdź czy już istnieją jakieś szablony
            if (WczytajSzablonyTowarow().Any() || WczytajSzablonyParametrow().Any())
            {
                return; // Już są szablony, nie nadpisuj
            }

            // Szablon parametrów: "Standardowa oferta PL"
            var szablonStandardowy = new SzablonParametrow
            {
                Nazwa = "Standardowa oferta PL",
                TerminPlatnosci = "7 dni",
                DniPlatnosci = 7,
                WalutaKonta = "PLN",
                Jezyk = JezykOferty.Polski,
                TypLogo = TypLogo.Okragle,
                PokazOpakowanie = false,
                PokazCene = true,
                PokazIlosc = false,
                PokazTerminPlatnosci = true,
                TransportTyp = "wlasny",
                DodajNotkeOCenach = true,
                NotatkaCustom = ""
            };
            ZapiszSzablonParametrow(szablonStandardowy);

            // Szablon parametrów: "Oferta eksportowa EN"
            var szablonEksport = new SzablonParametrow
            {
                Nazwa = "Oferta eksportowa EN",
                TerminPlatnosci = "14 dni",
                DniPlatnosci = 14,
                WalutaKonta = "EUR",
                Jezyk = JezykOferty.English,
                TypLogo = TypLogo.Dlugie,
                PokazOpakowanie = true,
                PokazCene = true,
                PokazIlosc = true,
                PokazTerminPlatnosci = true,
                TransportTyp = "klienta",
                DodajNotkeOCenach = false,
                NotatkaCustom = "Price validity: 7 days"
            };
            ZapiszSzablonParametrow(szablonEksport);

            // Szablon parametrów: "Tylko ceny (bez ilości)"
            var szablonCenyOnly = new SzablonParametrow
            {
                Nazwa = "Tylko ceny (bez ilości)",
                TerminPlatnosci = "7 dni",
                DniPlatnosci = 7,
                WalutaKonta = "PLN",
                Jezyk = JezykOferty.Polski,
                TypLogo = TypLogo.Okragle,
                PokazOpakowanie = false,
                PokazCene = true,
                PokazIlosc = false,
                PokazTerminPlatnosci = false,
                TransportTyp = "wlasny",
                DodajNotkeOCenach = true,
                NotatkaCustom = "Ceny orientacyjne. Ostateczna cena zależy od zamówionej ilości."
            };
            ZapiszSzablonParametrow(szablonCenyOnly);
        }

        #endregion
    }
}
