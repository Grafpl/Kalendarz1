using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;

namespace Kalendarz1.AnalitykaPelna.Services
{
    /// <summary>
    /// Słownik magazynów Sage Symfonia (HM.MZ.magazyn → nazwa).
    ///
    /// Sage NIE trzyma nazw magazynów w osobnej tabeli słownikowej w bazie HANDEL.
    /// Nazwy są zapisane w UI Symfonii i jako sufiks w kodzie dokumentów MM+/MM-:
    ///   - "0001/22/MM-/M. PROD" → magazyn źródłowy = M. PROD
    ///   - "0001/22/MM+/M. DYST" → magazyn docelowy = M. DYST
    ///
    /// Strategia:
    ///   1. Defaulty wyciągnięte z analizy 1000 dokumentów z HM.MG (real names z Symfonii).
    ///   2. Dynamiczny refresh: LoadFromDatabaseAsync() parsuje kod dokumentów MM+
    ///      (gdzie magazyn = destination, sufiks kodu = real nazwa) i zastępuje defaulty.
    ///   3. Override przez appsettings.json sekcja "MagazynyNazwy" — najwyższy priorytet.
    ///
    /// Real nazwy potwierdzone w danych HM.MG (Pióroskovscy/HANDEL):
    ///   65547 → KARMA       (Magazyn produkcji karmy)
    ///   65551 → M. ODPA     (Magazyn odpadów)
    ///   65552 → M. MROŹ     (Mroźnia)
    ///   65554 → M. PROD     (Magazyn produkcji / krojenie)
    ///   65555 → M. UBOJ     (Magazyn ubojni - tuszki po sPWU)
    ///   65556 → M. DYST     (Magazyn dystrybucji - sWZ do klientów)
    ///   65559 → Mag. opak.  (Magazyn opakowań - sMW)
    ///   65562 → M. MASAR    (Masarnia - sPPM/sRPM)
    ///   65564 → M. ROZCH    (Magazyn rozchodu)
    /// </summary>
    public static class MagazynyHelper
    {
        // Defaulty bazują na analizie kod-suffiksów MM+/MM- w HM.MG (real Sage names).
        // Te wartości są nadpisywane przez LoadFromDatabaseAsync() i appsettings.json.
        private static readonly Dictionary<int, MagazynInfo> _defaults = new()
        {
            { 65547, new MagazynInfo("KARMA",       "Magazyn produkcji karmy",       "#F97316") },
            { 65550, new MagazynInfo("Mag. faktur", "Magazyn faktur (sPZ/sWZ-W)",    "#1E40AF") },
            { 65551, new MagazynInfo("M. ODPA",     "Magazyn odpadów",               "#7C3AED") },
            { 65552, new MagazynInfo("M. MROŹ",     "Mroźnia",                       "#0EA5E9") },
            { 65554, new MagazynInfo("M. PROD",     "Magazyn produkcji / krojenia",  "#059669") },
            { 65555, new MagazynInfo("M. UBOJ",     "Magazyn ubojni",                "#DC2626") },
            { 65556, new MagazynInfo("M. DYST",     "Magazyn dystrybucji",           "#2563EB") },
            { 65559, new MagazynInfo("Mag. opak.",  "Magazyn opakowań",              "#64748B") },
            { 65562, new MagazynInfo("M. MASAR",    "Masarnia",                      "#9A3412") },
            { 65564, new MagazynInfo("M. ROZCH",    "Magazyn rozchodu",              "#0891B2") },
            { 65543, new MagazynInfo("Mag. 65543",  "Magazyn 65543 (small Tasomix)", "#6366F1") },
            { 65566, new MagazynInfo("Mag. 65566",  "Magazyn 65566 (Samol/Ekoplon)", "#8B5CF6") },
            // Kategorie towarów (nie magazyny - dla bezpieczeństwa gdyby trafiły jako magazyn)
            { 65882, new MagazynInfo("Kat. Żywiec", "Kategoria: kurczak żywy",       "#DC2626") },
            { 65883, new MagazynInfo("Kat. Pasze",  "Kategoria: pasze",              "#CA8A04") }
        };

        // Aktywny słownik (defaults + DB refresh + appsettings overrides)
        private static Dictionary<int, MagazynInfo>? _active;
        private static bool _dbLoaded = false;

        private static Dictionary<int, MagazynInfo> Active
        {
            get
            {
                if (_active == null)
                {
                    _active = new Dictionary<int, MagazynInfo>(_defaults);
                    LoadOverridesFromAppSettings();
                }
                return _active;
            }
        }

        /// <summary>
        /// Wczytuje nazwy magazynów BEZPOŚREDNIO z bazy HANDEL — analizuje sufiksy
        /// kod-ów dokumentów MM+ (gdzie magazyn = destination, sufiks = nazwa).
        /// Wywoływane raz przy starcie modułu Analityki Pełnej.
        ///
        /// Algorytm:
        ///   1. Dla każdego ID magazynu znajdź najczęstszy sufiks po "MM+/" w kod dokumentów
        ///      (ostatnie 365 dni, anulowany=0).
        ///   2. Sufiks = nazwa magazynu (np. "M. PROD", "M. DYST", "KARMA").
        ///   3. Jeśli nie ma dokumentów MM+ dla magazynu, próbuj MM- (sufiks też = źródłowy).
        ///   4. Pełną nazwę bierzemy z opisu typu dokumentu (HM.MG.nazwa) — np. "Wydanie z magazynu opakowań".
        /// </summary>
        public static async Task LoadFromDatabaseAsync(string connHandel)
        {
            if (_dbLoaded) return;
            try
            {
                if (_active == null)
                {
                    _active = new Dictionary<int, MagazynInfo>(_defaults);
                }

                // Wyciągnij sufiks po "MM+/" lub "MM-/" — to jest real nazwa magazynu w Symfonii
                const string sql = @"
                    WITH PerMagazynKod AS (
                        SELECT
                            MZ.[magazyn]                                AS MagazynID,
                            CASE
                                WHEN MG.[seria] LIKE '%MM+%' AND CHARINDEX('/', MG.[kod], CHARINDEX('MM+/', MG.[kod])) > 0
                                    THEN LTRIM(RTRIM(SUBSTRING(MG.[kod], CHARINDEX('MM+/', MG.[kod]) + 4, 50)))
                                WHEN MG.[seria] LIKE '%MM-%' AND CHARINDEX('/', MG.[kod], CHARINDEX('MM-/', MG.[kod])) > 0
                                    THEN LTRIM(RTRIM(SUBSTRING(MG.[kod], CHARINDEX('MM-/', MG.[kod]) + 4, 50)))
                                ELSE NULL
                            END                                          AS NazwaWyciagnieta,
                            MG.[seria],
                            COUNT(*)                                     AS Liczba
                        FROM [HM].[MZ] MZ
                        INNER JOIN [HM].[MG] MG ON MG.[id] = MZ.[super]
                        WHERE MG.[anulowany] = 0
                          AND MZ.[magazyn] IS NOT NULL
                          AND MG.[seria] IN ('MM+', 'sMM+', 'MM-', 'sMM-')
                          AND MG.[data] >= DATEADD(YEAR, -2, GETDATE())
                        GROUP BY MZ.[magazyn], MG.[seria], MG.[kod]
                    ),
                    Ranked AS (
                        SELECT MagazynID, NazwaWyciagnieta, Seria, SUM(Liczba) AS LiczbaCalk,
                               ROW_NUMBER() OVER (
                                   PARTITION BY MagazynID
                                   ORDER BY
                                       CASE WHEN Seria LIKE '%MM+%' THEN 1 ELSE 2 END,
                                       SUM(Liczba) DESC
                               ) AS rn
                        FROM PerMagazynKod
                        WHERE NazwaWyciagnieta IS NOT NULL AND NazwaWyciagnieta <> ''
                        GROUP BY MagazynID, NazwaWyciagnieta, Seria
                    )
                    SELECT MagazynID, NazwaWyciagnieta, Seria, LiczbaCalk
                    FROM Ranked
                    WHERE rn = 1
                    ORDER BY MagazynID;";

                using var conn = new SqlConnection(connHandel);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int magId = reader.GetInt32(0);
                    string nazwa = reader.IsDBNull(1) ? "" : reader.GetString(1).Trim();
                    if (string.IsNullOrWhiteSpace(nazwa)) continue;

                    // Skrót czyścimy z dziwnych znaków, zachowujemy podstawowe formy "M. PROD"
                    string skrot = CzyscNazwe(nazwa);
                    if (skrot.Length == 0) continue;

                    // Pełna nazwa = czytelne rozwinięcie skrótu (best-effort)
                    string pelna = RozwinSkrot(skrot, magId);
                    string kolor = _defaults.TryGetValue(magId, out var def) ? def.KolorHex : LosujKolor(magId);

                    _active![magId] = new MagazynInfo(skrot, pelna, kolor);
                }

                _dbLoaded = true;

                // Po DB refresh wciąż respektujemy override z appsettings.json (najwyższy priorytet)
                LoadOverridesFromAppSettings();
            }
            catch
            {
                // Jeśli DB padnie, zostają defaulty + appsettings — ignorujemy (best-effort)
            }
        }

        /// <summary>Czyści sufiks ze śmieci po SUBSTRING (max 50 znaków, ucinamy na pierwszej spacji-podwojnej lub końcu).</summary>
        private static string CzyscNazwe(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var trim = raw.Trim();
            // Ucinamy wszystko po znakach kontrolnych / nadmiarowych spacjach
            var match = Regex.Match(trim, @"^([\p{L}\p{N}\.\s\-]{1,30})");
            return match.Success ? match.Value.Trim() : trim;
        }

        /// <summary>Rozwija skrót typu "M. PROD" w czytelną nazwę "Magazyn produkcji".</summary>
        private static string RozwinSkrot(string skrot, int magId)
        {
            string s = skrot.ToUpperInvariant().Replace(" ", "").Replace(".", "");
            return s switch
            {
                "MPROD"   => "Magazyn produkcji / krojenia",
                "MUBOJ"   => "Magazyn ubojni",
                "MDYST"   => "Magazyn dystrybucji",
                "MODPA"   => "Magazyn odpadów",
                "MMROZ" or "MMROŹ" => "Mroźnia",
                "MMASAR"  => "Masarnia",
                "MROZCH"  => "Magazyn rozchodu",
                "KARMA"   => "Magazyn produkcji karmy",
                _         => $"{skrot} (ID {magId})"
            };
        }

        private static string LosujKolor(int magId)
        {
            var palette = new[] { "#059669", "#2563EB", "#DC2626", "#7C3AED", "#0EA5E9",
                                  "#9A3412", "#0891B2", "#F97316", "#1E40AF", "#6366F1" };
            return palette[Math.Abs(magId) % palette.Length];
        }

        /// <summary>Czyta appsettings.json sekcję "MagazynyNazwy" i nadpisuje aktualne wpisy.
        /// Format JSON: "MagazynyNazwy": { "65554": { "Skrot": "M. PROD", "Pelna": "...", "KolorHex": "#059669" } }
        /// </summary>
        public static void LoadOverridesFromAppSettings()
        {
            try
            {
                var dir = AppDomain.CurrentDomain.BaseDirectory;
                var path = Path.Combine(dir, "appsettings.json");
                if (!File.Exists(path)) return;
                var json = JObject.Parse(File.ReadAllText(path));
                var sec = json["MagazynyNazwy"] as JObject;
                if (sec == null) return;
                foreach (var kvp in sec)
                {
                    if (!int.TryParse(kvp.Key, out int id)) continue;
                    if (kvp.Value is not JObject obj) continue;
                    string skrot = obj["Skrot"]?.Value<string>() ?? "";
                    string pelna = obj["Pelna"]?.Value<string>() ?? skrot;
                    string kolor = obj["KolorHex"]?.Value<string>() ?? "#94A3B8";
                    if (!string.IsNullOrEmpty(skrot))
                        _active![id] = new MagazynInfo(skrot, pelna, kolor);
                }
            }
            catch { /* ignore — defaults wystarczą */ }
        }

        public static string Skrot(int? id)
        {
            if (!id.HasValue) return "—";
            return Active.TryGetValue(id.Value, out var info)
                ? info.Skrot
                : $"Mag. {id.Value}";
        }

        public static string PelnaNazwa(int? id)
        {
            if (!id.HasValue) return "—";
            return Active.TryGetValue(id.Value, out var info)
                ? info.PelnaNazwa
                : $"Magazyn {id.Value} (nieznany)";
        }

        public static Color Kolor(int? id)
        {
            if (id.HasValue && Active.TryGetValue(id.Value, out var info))
                return ParseHex(info.KolorHex);
            return Color.FromRgb(0x94, 0xA3, 0xB8);
        }

        public static IReadOnlyDictionary<int, MagazynInfo> Wszystkie => Active;

        private static Color ParseHex(string hex)
        {
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(hex);
                return c;
            }
            catch { return Color.FromRgb(0x94, 0xA3, 0xB8); }
        }
    }

    /// <summary>Pełen opis magazynu — skrót (chip), pełna nazwa (tooltip), kolor (badge).</summary>
    public class MagazynInfo
    {
        public string Skrot { get; }
        public string PelnaNazwa { get; }
        public string KolorHex { get; }

        public MagazynInfo(string skrot, string pelna, string kolorHex)
        {
            Skrot = skrot;
            PelnaNazwa = pelna;
            KolorHex = kolorHex;
        }
    }
}
