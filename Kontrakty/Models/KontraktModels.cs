using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kalendarz1.Kontrakty.Models
{
    /// <summary>
    /// Wiersz listy kontraktów — odpowiada widokowi dbo.v_KontraktyAktualne
    /// (nagłówek dbo.Kontrakty + AKTUALNA wersja dbo.KontraktyWersje).
    /// Status / okres / warunki pochodzą z aktualnej wersji (mogą być NULL, gdy kontrakt nie ma jeszcze wersji).
    /// </summary>
    public class KontraktListItem : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string NumerKontraktu { get; set; } = "";
        public string DostawcaId { get; set; } = "";
        public string Hodowca { get; set; } = "";
        public string HodowcaNip { get; set; } = "";
        public string HodowcaGospodarstwo { get; set; } = "";
        public string TypKontraktu { get; set; } = "";
        public bool LiczySieDoArimr { get; set; }
        public string Podmiot { get; set; } = "";

        public int? WersjaId { get; set; }
        public int? NrWersji { get; set; }
        public string? Status { get; set; }
        public DateTime? DataPodpisania { get; set; }
        public DateTime? ObowiazujeOd { get; set; }
        public DateTime? ObowiazujeDo { get; set; }
        public decimal? ProcentUbytku { get; set; }
        public string? TypCeny { get; set; }
        public decimal? Cena { get; set; }
        public int? TerminPlatnosciDni { get; set; }
        public string? SciezkaWord { get; set; }
        public string? SciezkaPdfSkan { get; set; }
        public int? DniDoWygasniecia { get; set; }

        // ── Etykiety do UI (computed, nie ViewModel) ──────────────────────────
        public string TypLabel => TypKontraktu switch
        {
            "ARIMR_3LAT" => "3-letni",
            "ROCZNY" => "Roczny",
            "SEZONOWY" => "Sezonowy",
            "WIECZNY" => "Wieczny",
            "SPOT" => "Spot",
            _ => TypKontraktu
        };

        public string StatusLabel => (Status ?? "BRAK") switch
        {
            "DRAFT" => "Szkic",
            "NEGOCJACJE" => "W negocjacji",
            "SENT" => "Wysłany",
            "SIGNED" => "Podpisany",
            "ACTIVE" => "Aktywny",
            "EXPIRING" => "Wygasa",
            "EXPIRED" => "Wygasły",
            "TERMINATED" => "Wypowiedziany",
            "SUPERSEDED" => "Zastąpiony",
            _ => "—"
        };

        public string ArimrLabel => LiczySieDoArimr ? "ARiMR" : "";

        /// <summary>Druga linia komórki HODOWCA: „NIP 123… · gosp. 045…" (puste znika).</summary>
        public string HodowcaPodtytul
        {
            get
            {
                var parts = new List<string>(2);
                if (!string.IsNullOrWhiteSpace(HodowcaNip)) parts.Add("NIP " + HodowcaNip);
                if (!string.IsNullOrWhiteSpace(HodowcaGospodarstwo)) parts.Add("gosp. " + HodowcaGospodarstwo);
                return parts.Count > 0 ? string.Join("  ·  ", parts) : "ID " + DostawcaId;
            }
        }

        public string OkresLabel
        {
            get
            {
                if (ObowiazujeOd is null) return "—";
                string od = ObowiazujeOd.Value.ToString("dd.MM.yy");
                string doTxt = ObowiazujeDo is null ? "bezterm." : ObowiazujeDo.Value.ToString("dd.MM.yy");
                return $"{od} → {doTxt}";
            }
        }

        public string WygasaLabel
        {
            get
            {
                if (ObowiazujeDo is null) return "wieczny";
                if (DniDoWygasniecia is null) return "—";
                int d = DniDoWygasniecia.Value;
                return d < 0 ? $"{-d} dni temu" : $"za {d} dni";
            }
        }

        public string WersjaLabel => NrWersji is null ? "—" : $"v{NrWersji}";

        /// <summary>Czy w grze (wymaga uwagi terminowej).</summary>
        public bool JestAktywny => Status is "ACTIVE" or "EXPIRING" or "SIGNED";

        /// <summary>Pilność terminowa do kolorowania UI: CRIT / WARN / SOON / OK / NONE.</summary>
        public string Pilnosc =>
            (Status is "ACTIVE" or "EXPIRING") && DniDoWygasniecia is int d
                ? (d < 0 ? "CRIT" : d <= 30 ? "WARN" : d <= 90 ? "SOON" : "OK")
                : "NONE";

        public bool HasPlik => !string.IsNullOrWhiteSpace(SciezkaPdfSkan) || !string.IsNullOrWhiteSpace(SciezkaWord);

        // ── Twórca (avatar + nazwa + data) — jak w starym programie ──────────
        public string UtworzylUserId { get; set; } = "";
        public string UtworzylNazwa { get; set; } = "";
        public DateTime? UtworzylKiedy { get; set; }
        public string UtworzylKiedyLabel => UtworzylKiedy?.ToString("dd.MM.yyyy") ?? "";

        private BitmapSource? _avatarUtw;
        /// <summary>Avatar twórcy — ładowany progresywnie w tle (notify dla odświeżenia komórki).</summary>
        public BitmapSource? AvatarUtw
        {
            get => _avatarUtw;
            set { _avatarUtw = value; OnChanged(nameof(AvatarUtw)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    /// <summary>Sugestie warunków handlowych z historii dostaw (FarmerCalc, 12 mies.).</summary>
    public class WarunkiSugestia
    {
        public int Dostaw { get; set; }
        public decimal? CenaSrednia { get; set; }   // zł/kg
        public decimal? CenaOstatnia { get; set; }   // zł/kg
        public decimal? UbytekSredniProc { get; set; } // już w % (Loss*100)
        public decimal? WagaSrednia { get; set; }    // kg
        public DateTime? OstatniaDostawa { get; set; }
        public bool MaDane => Dostaw > 0;

        public string Opis
        {
            get
            {
                var p = new List<string>();
                if (CenaSrednia is { } c) p.Add($"cena śr. {c:0.00} zł/kg" + (CenaOstatnia is { } co ? $" (ost. {co:0.00})" : ""));
                if (UbytekSredniProc is { } u) p.Add($"ubytek śr. {u:0.0}%");
                if (WagaSrednia is { } w) p.Add($"waga śr. {w:0.00} kg");
                string baza = p.Count > 0 ? string.Join("  ·  ", p) : "brak danych liczbowych";
                return $"💡 Z {Dostaw} dostaw (12 mies.): {baza}";
            }
        }
    }

    /// <summary>Pojedyncza dostawa do sugerowania warunków (ostatnie N z FarmerCalc).</summary>
    public class DostawaSugestia
    {
        public DateTime Data { get; set; }
        public decimal? Cena { get; set; }            // zł/kg (FarmerCalc.Price)
        public decimal? Dodatek { get; set; }         // zł/kg (FarmerCalc.Addition)
        public decimal? UbytekProc { get; set; }      // % (Loss*100)
        public string TypCeny { get; set; } = "";     // z PriceType.Name (Wolnorynkowa/Rolnicza/…)
        public string CzyjaWaga { get; set; } = "";   // „Hodowca" / „Ubojnia" — heurystyka z Ubytek

        public string DataLabel => Data.ToString("dd.MM");
        public string CenaLabel => Cena is { } c ? $"{c:0.00} zł" : "—";
        public string DodatekLabel => Dodatek is { } d ? $"{d:0.00} zł" : "—";
        public string UbytekLabel => UbytekProc is { } u ? $"{u:0.0}%" : "—";
        public string TypCenyLabel => string.IsNullOrWhiteSpace(TypCeny) ? "—" : TypCeny;
        public string CzyjaWagaLabel => string.IsNullOrWhiteSpace(CzyjaWaga) ? "—" : CzyjaWaga;
    }

    /// <summary>Konfiguracja numeracji kontraktów (dbo.KontraktyNumeracjaConfig).</summary>
    public class NumeracjaConfig
    {
        public int Id { get; set; }
        public string FormatSzablon { get; set; } = "K/{ROK}/{NNNN}";
        public bool ResetRoczny { get; set; } = true;
        public short Rok { get; set; } = (short)DateTime.Now.Year;
        public int NastepnyNumer { get; set; } = 1;
    }

    /// <summary>Snapshot compliance ARiMR — z widoku dbo.v_ArimrCompliance.</summary>
    public class ArimrCompliance
    {
        public decimal SurowiecCaloscKg { get; set; }
        public decimal SurowiecArimrKg { get; set; }
        public int HodowcowOgolem { get; set; }
        public int HodowcowArimr { get; set; }
        public decimal ProcentArimr { get; set; }
        public string Status { get; set; } = "BRAK_DANYCH"; // OK / WARN / CRIT / BRAK_DANYCH
        public DateTime WyliczonoKiedy { get; set; }

        public bool CzyAlarm => Status is "WARN" or "CRIT";
        public decimal MarginesPp => ProcentArimr - 50m;
    }

    /// <summary>Wpis dziennika audytu (dbo.KontraktyAuditLog).</summary>
    public class AuditWpis
    {
        public long Id { get; set; }
        public int? KontraktId { get; set; }
        public int? WersjaId { get; set; }
        public string Tabela { get; set; } = "";
        public string Operacja { get; set; } = "";
        public string? Pole { get; set; }
        public string? WartoscPrzed { get; set; }
        public string? WartoscPo { get; set; }
        public string? UserId { get; set; }
        public DateTime Kiedy { get; set; }

        public string KiedyLabel => Kiedy.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
        public string OpisLabel => Operacja switch
        {
            "UTWORZENIE" => "Utworzenie kontraktu",
            "PRZEDLUZENIE" => "Przedłużenie (nowa wersja)",
            "EDYCJA" => "Edycja wersji",
            "AKTYWACJA" => "Aktywacja wersji",
            "ZMIANA_STATUSU" => "Zmiana statusu",
            "TRANSFORMACJA" => "Decyzja transformacji",
            _ => Operacja
        };
        public string SzczegolLabel =>
            string.IsNullOrWhiteSpace(Pole) ? "" :
            $"{Pole}: {(string.IsNullOrWhiteSpace(WartoscPrzed) ? "—" : WartoscPrzed)} → {(string.IsNullOrWhiteSpace(WartoscPo) ? "—" : WartoscPo)}";
    }

    /// <summary>Decyzja transformacji JDG→sp. z o.o. (dbo.KontraktyTransformacja).</summary>
    public class TransformacjaDecyzja
    {
        public int KontraktId { get; set; }
        public string Decyzja { get; set; } = "NIEOKRESLONE"; // APORT / DZIERZAWA / NIEOKRESLONE
        public DateTime? DataDecyzji { get; set; }
        public string? UserId { get; set; }
        public string? Uzasadnienie { get; set; }

        public string DecyzjaLabel => Decyzja switch
        {
            "APORT" => "Aport do sp. z o.o.",
            "DZIERZAWA" => "Dzierżawa / cesja",
            _ => "Nieokreślona"
        };
    }

    /// <summary>Punkt trendu zgodności ARiMR (dbo.KontraktyComplianceSnapshot).</summary>
    public class ComplianceTrendPunkt
    {
        public DateTime Data { get; set; }
        public decimal Procent { get; set; }
    }

    /// <summary>Liczniki inwentarza kontraktów (kafelki dashboardu / pasek listy).</summary>
    public class KontraktyInwentarz
    {
        public int Aktywne { get; set; }
        public int Wygasajace90 { get; set; }
        public int Wygasle { get; set; }
        public int Robocze { get; set; }
        public int Razem { get; set; }
    }

    /// <summary>Alert wygasania/braku — z dbo.KontraktyAlerty (+ numer/hodowca z JOIN).</summary>
    public class KontraktAlertItem
    {
        public int Id { get; set; }
        public int KontraktId { get; set; }
        public string TypAlertu { get; set; } = "";
        public string Severity { get; set; } = "INFO"; // INFO/WARN/CRIT
        public string Wiadomosc { get; set; } = "";
        public DateTime DataWygenerowania { get; set; }
        public bool Przeczytany { get; set; }
        public bool AkcjaPodjeta { get; set; }
        public string NumerKontraktu { get; set; } = "";
        public string Hodowca { get; set; } = "";

        public string SeverityLabel => Severity switch { "CRIT" => "Krytyczny", "HIGH" => "Pilny", "WARN" => "Ważny", _ => "Info" };
        public bool Eskalowany { get; set; }
        public string DataLabel => DataWygenerowania.ToString("dd.MM.yyyy HH:mm");
        public string TypLabel => TypAlertu switch
        {
            "WYGASNAL" => "Wygasł",
            "WYGASA_30" => "Wygasa ≤30 dni",
            "WYGASA_60" => "Wygasa ≤60 dni",
            "WYGASA_90" => "Wygasa ≤90 dni",
            "MARTWA_UMOWA" => "Martwa umowa",
            "BRAK_SKANU" => "Brak skanu",
            _ => TypAlertu
        };
    }

    /// <summary>Pozycja rankingu hodowców wg wolumenu (12 mies.) z flagą kontraktu/ARiMR.</summary>
    public class RankingHodowca
    {
        public int Pozycja { get; set; }
        public string DostawcaId { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public decimal WagaKg12m { get; set; }
        public int LiczbaDostaw { get; set; }
        public DateTime? OstatniaDostawa { get; set; }
        public bool MaKontrakt { get; set; }
        public bool MaArimr { get; set; }

        public string WagaTonyLabel => $"{WagaKg12m / 1000m:N0} t";
        public string OstatniaLabel => OstatniaDostawa?.ToString("dd.MM.yy") ?? "—";
        public string StatusLabel => MaArimr ? "🎯 ARiMR" : MaKontrakt ? "✓ kontrakt" : "✗ brak umowy";
        public Brush StatusKolor => Kresk(MaArimr ? "#166534" : MaKontrakt ? "#1E40AF" : "#92400E");
        public Brush StatusTlo  => Kresk(MaArimr ? "#DCFCE7" : MaKontrakt ? "#E0E7FF" : "#FEF3C7");
        private static Brush Kresk(string hex)
        { var b = (Brush)new BrushConverter().ConvertFromString(hex)!; b.Freeze(); return b; }
    }

    /// <summary>Hodowca z dostawami (12 mies.) bez aktywnego kontraktu — z dbo.v_HodowcyBezKontraktu.</summary>
    public class HodowcaBezKontraktu
    {
        public string DostawcaId { get; set; } = "";
        public string Hodowca { get; set; } = "";
        public int LiczbaDostaw { get; set; }
        public decimal WagaKg12m { get; set; }
        public DateTime? OstatniaDostawa { get; set; }

        public string WagaTonyLabel => $"{WagaKg12m / 1000m:N0} t";
        public string OstatniaLabel => OstatniaDostawa?.ToString("dd.MM.yy") ?? "—";
    }

    /// <summary>Hodowca do wyboru w edytorze (z dbo.Dostawcy).</summary>
    public class HodowcaPicker
    {
        public string DostawcaId { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public string Nip { get; set; } = "";
        public string Pesel { get; set; } = "";
        public string Regon { get; set; } = "";
        public string NrDowodu { get; set; } = "";
        public string Telefon { get; set; } = "";
        public string Email { get; set; } = "";
        public string NrGospodarstwa { get; set; } = "";
        public string Adres { get; set; } = "";
        public string Display => string.IsNullOrWhiteSpace(Nip) ? Nazwa : $"{Nazwa}  ·  NIP {Nip}";
        /// <summary>Inicjał do awatara-kółka na liście wyboru.</summary>
        public string Inicjal => string.IsNullOrWhiteSpace(Nazwa) ? "?" : Nazwa.TrimStart().Substring(0, 1).ToUpper();
        public string Meta
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(Nip)) parts.Add("NIP " + Nip);
                if (!string.IsNullOrWhiteSpace(NrGospodarstwa)) parts.Add("gosp. " + NrGospodarstwa);
                if (!string.IsNullOrWhiteSpace(Adres)) parts.Add(Adres);
                return parts.Count == 0 ? "— brak danych —" : string.Join("  ·  ", parts);
            }
        }
    }

    /// <summary>Nagłówek kontraktu (dbo.Kontrakty) — tożsamość relacji.</summary>
    public class KontraktDetail
    {
        public int Id { get; set; }
        public string NumerKontraktu { get; set; } = "";
        public short Rok { get; set; }
        public int LpRoku { get; set; }
        public string DostawcaId { get; set; } = "";
        public string TypKontraktu { get; set; } = "ARIMR_3LAT";
        public bool LiczySieDoArimr { get; set; }
        public string Podmiot { get; set; } = "PIORKOWSCY_SC";
        public string? NazwaHodowcySnapshot { get; set; }
        public string? NipSnapshot { get; set; }
        public string? NrGospodarstwaSnapshot { get; set; }
        public string? AdresSnapshot { get; set; }
        public int? PoprzedniKontraktId { get; set; }
        public string? EmailRODO { get; set; }
        public string? PeselSnapshot { get; set; }
        public string? RegonSnapshot { get; set; }
        public string? NrDowoduSnapshot { get; set; }
        public string? TelefonSnapshot { get; set; }
        public string UtworzylUserId { get; set; } = "";
        public DateTime UtworzylKiedy { get; set; } = DateTime.Now;
        public DateTime? ZamknietyKiedy { get; set; }
        public string? PowodZamkniecia { get; set; }

        public string TypLabel => KontraktStatus.TypLabel(TypKontraktu);
        public string PodmiotLabel => Podmiot == "PIORKOWSCY_SPZOO" ? "Piórkowscy sp. z o.o." : "Piórkowscy s.c.";
    }

    /// <summary>Wersja warunków (dbo.KontraktyWersje) — snapshot + okres ważności.</summary>
    public class KontraktWersja
    {
        public int Id { get; set; }
        public int KontraktId { get; set; }
        public int NrWersji { get; set; }
        public bool IsAktualna { get; set; }
        public string Status { get; set; } = "DRAFT";

        public DateTime? DataPodpisania { get; set; }
        public DateTime ObowiazujeOd { get; set; } = DateTime.Today;
        public DateTime? ObowiazujeDo { get; set; }
        public int OkresWypowiedzeniaDni { get; set; } = 90;

        public decimal? ProcentUbytku { get; set; } = 3.00m;
        public string TypCeny { get; set; } = "wolnorynkowa";
        public decimal? Cena { get; set; }
        public decimal? DodatekZl { get; set; }
        public int TerminPlatnosciDni { get; set; } = 21;
        public string RozliczanaWaga { get; set; } = "NETTO_HODOWCY";
        public int? MinimalnaIloscSzt { get; set; }
        public bool Ekskluzywnosc { get; set; }
        public string? KlauzuleSzczegolne { get; set; }

        // Rozszerzone warunki (część 3 — wymaga 03_Kontrakty_wersje_rozszerzenia.sql)
        public decimal? CenaMin { get; set; }
        public decimal? CenaMax { get; set; }
        public string? Indeksacja { get; set; }
        public string? CzestotliwoscDostaw { get; set; }
        public int? MaxIloscSzt { get; set; }
        public string? TransportCzyj { get; set; }
        public bool PaszaOdNas { get; set; }
        public bool PisklakiOdNas { get; set; }
        public decimal? KaraUmownaZl { get; set; }
        public bool AutoOdnowienie { get; set; }
        public bool PrawoPierwokupu { get; set; }
        public string? OsobaKontaktowa { get; set; }
        public string? TelefonKontaktowy { get; set; }

        // Kontraktacja (część 4)
        public string? DostawcaPaszyNazwa { get; set; }
        public string? DostawcaPisklatNazwa { get; set; }
        public string? BonusOpis { get; set; }
        /// <summary>true = konfiskaty/padłe potrącane od hodowcy (zazwyczaj); false = pokrywa ubojnia.</summary>
        public bool KonfiskatyHodowca { get; set; } = true;

        public string? SciezkaWord { get; set; }
        public string? SciezkaPdfSkan { get; set; }
        public int? SzablonId { get; set; }

        public string? PowodZmiany { get; set; }
        public string UtworzylUserId { get; set; } = "";
        public string UtworzylNazwa { get; set; } = "";
        public DateTime UtworzylKiedy { get; set; } = DateTime.Now;

        // ── Etykiety ──
        public string NrLabel => $"v{NrVersjiSafe}";
        private int NrVersjiSafe => NrWersji <= 0 ? 1 : NrWersji;
        public string StatusLabel => KontraktStatus.Label(Status);
        public string OkresLabel
        {
            get
            {
                string od = ObowiazujeOd.ToString("dd.MM.yyyy");
                string doTxt = ObowiazujeDo is null ? "bezterminowo" : ObowiazujeDo.Value.ToString("dd.MM.yyyy");
                return $"{od} → {doTxt}";
            }
        }
        public string WarunkiLabel
        {
            get
            {
                var ub = ProcentUbytku.HasValue ? $"ubytek {ProcentUbytku:0.0}%" : "ubytek —";
                var cena = Cena.HasValue ? $"{Cena:0.00} zł/kg" : "cena wg cennika";
                var dod = DodatekZl.HasValue && DodatekZl.Value != 0 ? $" +{DodatekZl:0.00}" : "";
                return $"{TypCeny}: {cena}{dod} · {ub} · płatność {TerminPlatnosciDni} dni";
            }
        }
        public string AutorLabel => $"{(string.IsNullOrWhiteSpace(UtworzylNazwa) ? UtworzylUserId : UtworzylNazwa)} · {UtworzylKiedy:dd.MM.yyyy}";
        public bool Edytowalna => Status is "DRAFT" or "NEGOCJACJE";
    }

    /// <summary>Etykiety statusów / typów — wspólne dla widoków.</summary>
    public static class KontraktStatus
    {
        public static string Label(string? s) => (s ?? "BRAK") switch
        {
            "DRAFT" => "Szkic",
            "NEGOCJACJE" => "W negocjacji",
            "SENT" => "Wysłany",
            "SIGNED" => "Podpisany",
            "ACTIVE" => "Aktywny",
            "EXPIRING" => "Wygasa",
            "EXPIRED" => "Wygasły",
            "TERMINATED" => "Wypowiedziany",
            "SUPERSEDED" => "Zastąpiony",
            _ => "—"
        };

        public static string TypLabel(string t) => t switch
        {
            "ARIMR_3LAT" => "3-letni",
            "ROCZNY" => "Roczny",
            "SEZONOWY" => "Sezonowy",
            "WIECZNY" => "Wieczny",
            "SPOT" => "Spot",
            _ => t
        };
    }

    /// <summary>Pojedynczy cykl harmonogramu (dbo.KontraktyHarmonogram) — per wersja kontraktu.</summary>
    public class HarmonogramCykl
    {
        public int Id { get; set; }
        public int KontraktId { get; set; }
        public int WersjaId { get; set; }
        public int NrCyklu { get; set; }
        public DateTime? DataWstawienia { get; set; }
        public int? IloscWstawiona { get; set; }
        public int? IloscUbiorki { get; set; }
        public int? DzienUbiorki { get; set; }
        public DateTime? DataUbojuKoncowego { get; set; }
        public int? IloscUboju { get; set; }
        public string Status { get; set; } = "PLANOWANY";

        public string DataWstawieniaLabel => DataWstawienia?.ToString("dd.MM.yyyy") ?? "—";
        public string DataUbojuLabel => DataUbojuKoncowego?.ToString("dd.MM.yyyy") ?? "—";
        /// <summary>Auto-data ubiórki = data wstawienia + DzienUbiorki (domyślnie 33). Tylko do wyświetlenia.</summary>
        public string DataUbiorkaLabel => DataWstawienia is { } d ? d.AddDays(DzienUbiorki ?? 33).ToString("dd.MM.yyyy") : "—";
        public string IloscWstawionaLabel => IloscWstawiona is { } v && v > 0 ? $"{v:N0} szt." : "—";
        public string IloscUbojuLabel => IloscUboju is { } v && v > 0 ? $"{v:N0} szt." : "—";
        public string DzienUbiorkiLabel => DzienUbiorki is { } d && d > 0 ? $"{d}. dzień" : "—";
        /// <summary>Czytelny tytuł kafelka, np. „Cykl 1".</summary>
        public string TytulCyklu => $"Cykl {NrCyklu}";
        /// <summary>Skrót okresu cyklu: „12.03 → 23.04" (wstawienie → ubój).</summary>
        public string OkresCyklu =>
            (DataWstawienia?.ToString("dd.MM") ?? "—") + " → " + (DataUbojuKoncowego?.ToString("dd.MM") ?? "?");
    }

    /// <summary>Załącznik kontraktu (dbo.KontraktyZalaczniki) — skan PDF / aneks.</summary>
    public class KontraktZalacznik
    {
        public int Id { get; set; }
        public int KontraktId { get; set; }
        public int? WersjaId { get; set; }
        public string TypZalacznika { get; set; } = "SKAN_PODPISANY";
        public string NazwaPliku { get; set; } = "";
        public string SciezkaUnc { get; set; } = "";
        public string DodalUserId { get; set; } = "";
        public DateTime DodanyKiedy { get; set; }
        public string? Opis { get; set; }

        public string TypLabel => TypZalacznika switch
        {
            "SKAN_PODPISANY" => "Skan podpisany",
            "ANEKS" => "Aneks",
            "OSWIADCZENIE" => "Oświadczenie",
            "KORESPONDENCJA" => "Korespondencja",
            _ => "Inny"
        };
        public string DodanyLabel => DodanyKiedy.ToString("dd.MM.yyyy HH:mm");
    }
}
