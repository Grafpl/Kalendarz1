using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Kalendarz1.OfertaCenowa
{
    /// <summary>
    /// Manager szablonów - obsługuje zapisywanie i wczytywanie szablonów
    /// </summary>
    public class SzablonyManager
    {
        private readonly string _folderSzablonow;
        private readonly string _plikSzablonowTowarow;
        private readonly string _plikSzablonowParametrow;

        public SzablonyManager()
        {
            _folderSzablonow = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OfertaHandlowa", "Szablony");
            Directory.CreateDirectory(_folderSzablonow);

            _plikSzablonowTowarow = Path.Combine(_folderSzablonow, "szablony_towarow.json");
            _plikSzablonowParametrow = Path.Combine(_folderSzablonow, "szablony_parametrow.json");
        }

        // =====================================================
        // SZABLONY TOWARÓW
        // =====================================================

        public List<SzablonTowarow> WczytajSzablonyTowarow()
        {
            try
            {
                if (File.Exists(_plikSzablonowTowarow))
                {
                    string json = File.ReadAllText(_plikSzablonowTowarow);
                    var szablony = JsonSerializer.Deserialize<List<SzablonTowarow>>(json);
                    return szablony ?? new List<SzablonTowarow>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd wczytywania szablonów towarów: {ex.Message}");
            }

            return new List<SzablonTowarow>();
        }

        public void ZapiszSzablonyTowarow(List<SzablonTowarow> szablony)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(szablony, options);
                File.WriteAllText(_plikSzablonowTowarow, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd zapisywania szablonów towarów: {ex.Message}");
            }
        }

        public void DodajSzablonTowarow(SzablonTowarow szablon)
        {
            var szablony = WczytajSzablonyTowarow();
            szablon.Id = szablony.Count > 0 ? szablony[^1].Id + 1 : 1;
            szablony.Add(szablon);
            ZapiszSzablonyTowarow(szablony);
        }

        public void UsunSzablonTowarow(int id)
        {
            var szablony = WczytajSzablonyTowarow();
            szablony.RemoveAll(s => s.Id == id);
            ZapiszSzablonyTowarow(szablony);
        }

        // =====================================================
        // SZABLONY PARAMETRÓW
        // =====================================================

        public List<SzablonParametrow> WczytajSzablonyParametrow()
        {
            try
            {
                if (File.Exists(_plikSzablonowParametrow))
                {
                    string json = File.ReadAllText(_plikSzablonowParametrow);
                    var szablony = JsonSerializer.Deserialize<List<SzablonParametrow>>(json);
                    return szablony ?? new List<SzablonParametrow>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd wczytywania szablonów parametrów: {ex.Message}");
            }

            return new List<SzablonParametrow>();
        }

        public void ZapiszSzablonyParametrow(List<SzablonParametrow> szablony)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(szablony, options);
                File.WriteAllText(_plikSzablonowParametrow, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd zapisywania szablonów parametrów: {ex.Message}");
            }
        }

        public void ZapiszSzablonParametrow(SzablonParametrow szablon)
        {
            var szablony = WczytajSzablonyParametrow();
            var istniejacy = szablony.FindIndex(s => s.Id == szablon.Id);
            
            if (istniejacy >= 0)
            {
                szablony[istniejacy] = szablon;
            }
            else
            {
                szablon.Id = szablony.Count > 0 ? szablony.Max(s => s.Id) + 1 : 1;
                szablony.Add(szablon);
            }
            
            ZapiszSzablonyParametrow(szablony);
        }

        public void UsunSzablonParametrow(int id)
        {
            var szablony = WczytajSzablonyParametrow();
            szablony.RemoveAll(s => s.Id == id);
            ZapiszSzablonyParametrow(szablony);
        }

        // =====================================================
        // PRZYKŁADOWE SZABLONY
        // =====================================================

        public void UtworzSzablonyPrzykladowe()
        {
            // Sprawdź czy już istnieją
            if (WczytajSzablonyParametrow().Count > 0) return;

            // Utwórz przykładowe szablony parametrów
            var szablonyParametrow = new List<SzablonParametrow>
            {
                new SzablonParametrow
                {
                    Id = 1,
                    Nazwa = "Standardowy PL",
                    TerminPlatnosci = "14 dni",
                    DniPlatnosci = 14,
                    WalutaKonta = "PLN",
                    Jezyk = JezykOferty.Polski,
                    TypLogo = TypLogo.Okragle,
                    PokazOpakowanie = true,
                    PokazCene = true,
                    PokazIlosc = false,
                    PokazTerminPlatnosci = true,
                    TransportTyp = "wlasny"
                },
                new SzablonParametrow
                {
                    Id = 2,
                    Nazwa = "Export EN",
                    TerminPlatnosci = "30 dni",
                    DniPlatnosci = 30,
                    WalutaKonta = "EUR",
                    Jezyk = JezykOferty.English,
                    TypLogo = TypLogo.Dlugie,
                    PokazOpakowanie = true,
                    PokazCene = true,
                    PokazIlosc = true,
                    PokazTerminPlatnosci = true,
                    TransportTyp = "klienta"
                }
            };

            ZapiszSzablonyParametrow(szablonyParametrow);
        }
    }
}
