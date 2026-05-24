// ════════════════════════════════════════════════════════════════════════════
// MigracjaKontraktowImport.cs — import umów z CSV → dbo.Kontrakty
// Część 4 audytu, Faza M
// Uruchomienie: Kalendarz1.exe --migracja-kontraktow "ścieżka\Inwentaryzacja_umow.csv"
//
// CSV: UTF-8, separator ; (lub , — auto-detect), nagłówki jak w README_migracja_z_excela.md
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kalendarz1.Kontrakty.Models;
using Kalendarz1.Kontrakty.Services;

namespace Kalendarz1.Kontrakty.Migracja
{
    public class MigracjaKontraktowImport
    {
        private readonly KontraktyService _svc = new();

        public async Task<(int ok, int blad)> ImportujAsync(string csvPath, string userId = "migracja")
        {
            if (!File.Exists(csvPath))
                throw new FileNotFoundException($"CSV nie istnieje: {csvPath}");

            var lines = await File.ReadAllLinesAsync(csvPath);
            if (lines.Length < 2)
            {
                Console.WriteLine("CSV pusty lub tylko nagłówek.");
                return (0, 0);
            }

            // Auto-detekcja separatora
            var sep = lines[0].Contains(';') ? ';' : ',';
            var headers = lines[0].Split(sep).Select(h => h.Trim()).ToArray();

            int Idx(string name) => Array.FindIndex(headers, h => h.Equals(name, StringComparison.OrdinalIgnoreCase));

            int iDostId   = Idx("DostawcaId");
            int iNazwa    = Idx("NazwaHodowcy");
            int iNip      = Idx("Nip");
            int iNrGosp   = Idx("NrGospodarstwa");
            int iAdres    = Idx("Adres");
            int iTyp      = Idx("TypKontraktu");
            int iOd       = Idx("DataOd");
            int iDo       = Idx("DataDo");
            int iUbytku   = Idx("ProcentUbytku");
            int iTypCeny  = Idx("TypCeny");
            int iCena     = Idx("Cena");
            int iTermin   = Idx("TerminPlatnosciDni");
            int iArimr    = Idx("LiczySieDoArimr");
            int iStatus   = Idx("Status");
            int iSkan     = Idx("SciezkaPdfSkan");

            if (iDostId < 0 || iNazwa < 0 || iOd < 0)
                throw new InvalidOperationException("Brak wymaganych kolumn: DostawcaId, NazwaHodowcy, DataOd.");

            int ok = 0, blad = 0;
            for (int row = 1; row < lines.Length; row++)
            {
                if (string.IsNullOrWhiteSpace(lines[row])) continue;
                var c = lines[row].Split(sep);

                try
                {
                    string Get(int i) => (i >= 0 && i < c.Length) ? c[i].Trim() : "";
                    decimal? GetDec(int i) => string.IsNullOrWhiteSpace(Get(i))
                        ? (decimal?)null
                        : decimal.Parse(Get(i).Replace(',', '.'), CultureInfo.InvariantCulture);
                    DateTime? GetDate(int i) => string.IsNullOrWhiteSpace(Get(i))
                        ? (DateTime?)null
                        : DateTime.Parse(Get(i), CultureInfo.InvariantCulture);

                    var dataOd = GetDate(iOd) ?? throw new Exception("brak DataOd");

                    var dto = new KontraktDto
                    {
                        DostawcaId = int.Parse(Get(iDostId)),
                        NazwaHodowcySnapshot = Get(iNazwa),
                        NipSnapshot = iNip >= 0 ? NullEmpty(Get(iNip)) : null,
                        NrGospodarstwaSnapshot = iNrGosp >= 0 ? NullEmpty(Get(iNrGosp)) : null,
                        AdresSnapshot = iAdres >= 0 ? NullEmpty(Get(iAdres)) : null,
                        TypKontraktu = iTyp >= 0 ? Get(iTyp) : "ARIMR_3LAT",
                        DataObowiazujeOd = dataOd,
                        DataObowiazujeDo = GetDate(iDo),
                        Rok = (short)dataOd.Year,
                        ProcentUbytku = GetDec(iUbytku) ?? 3.00m,
                        TypCeny = iTypCeny >= 0 ? Get(iTypCeny) : "wolnorynkowa",
                        Cena = GetDec(iCena),
                        TerminPlatnosciDni = iTermin >= 0 && int.TryParse(Get(iTermin), out var t) ? t : 21,
                        LiczySieDoArimr = iArimr >= 0 && Get(iArimr) == "1",
                        Status = iStatus >= 0 ? Get(iStatus) : "ACTIVE",
                        PartiaPiorkowscy = "PIORKOWSCY",
                        SciezkaPdfSkan = iSkan >= 0 ? NullEmpty(Get(iSkan)) : null
                    };

                    var id = await _svc.CreateAsync(dto, userId);
                    Console.WriteLine($"[OK] wiersz {row}: {dto.NazwaHodowcySnapshot} → {dto.NumerKontraktu} (id={id})");
                    ok++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BŁĄD] wiersz {row}: {ex.Message}");
                    blad++;
                }
            }

            Console.WriteLine($"\n═══ MIGRACJA ZAKOŃCZONA: {ok} OK, {blad} błędów ═══");
            return (ok, blad);
        }

        private static string? NullEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
    }
}

// ── Wpięcie w App.xaml.cs OnStartup: ─────────────────────────────────────────
//
//   if (e.Args.Length >= 2 && e.Args[0] == "--migracja-kontraktow")
//   {
//       AllocConsole();  // P/Invoke kernel32 jeśli chcesz widzieć Console.WriteLine
//       var imp = new Kalendarz1.Kontrakty.Migracja.MigracjaKontraktowImport();
//       await imp.ImportujAsync(e.Args[1]);
//       Shutdown(0);
//       return;
//   }
