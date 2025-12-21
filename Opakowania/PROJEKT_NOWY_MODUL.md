# PROJEKT: Nowy Moduł Sald Opakowań

## Cel: SZYBKOŚĆ - dane w < 1 sekundy

---

## 1. ARCHITEKTURA - Uproszczona

```
┌─────────────────────────────────────────────────────────────┐
│                        WIDOKI                                │
├─────────────────────────────────────────────────────────────┤
│  MainWindow          │  SzczegolyWindow                      │
│  - Lista kontrahentów│  - Dokumenty kontrahenta              │
│  - Filtrowanie       │  - Wykres                             │
│  - Sortowanie        │  - Eksport PDF/Email                  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                     SERWISY                                  │
├─────────────────────────────────────────────────────────────┤
│  SaldaService        │  PdfService         │ EmailService    │
│  - JEDNO zapytanie   │  - Generowanie PDF  │ - Outlook/mailto│
│  - Cache w pamięci   │  - Zachowany stary  │                 │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   BAZA DANYCH                                │
│  1 zapytanie = wszystkie dane                               │
└─────────────────────────────────────────────────────────────┘
```

---

## 2. BAZA DANYCH - Jedno Super-Zapytanie

### Zamiast skomplikowanych CTE i FULL OUTER JOIN:

```sql
-- JEDNO ZAPYTANIE - wszystkie salda wszystkich kontrahentów, wszystkie typy opakowań
SELECT
    C.id AS KontrahentId,
    C.Shortcut AS Kontrahent,
    C.Name AS Nazwa,
    ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec,

    -- Salda wszystkich typów w jednym wierszu
    SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END) AS E2,
    SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END) AS H1,
    SUM(CASE WHEN TW.nazwa = 'Paleta EURO' THEN MZ.Ilosc ELSE 0 END) AS EURO,
    SUM(CASE WHEN TW.nazwa = 'Paleta plastikowa' THEN MZ.Ilosc ELSE 0 END) AS PCV,
    SUM(CASE WHEN TW.nazwa = 'Paleta Drewniana' THEN MZ.Ilosc ELSE 0 END) AS DREW,

    -- Data ostatniego dokumentu
    MAX(MG.Data) AS OstatniDokument

FROM [HANDEL].[SSCommon].[STContractors] C
INNER JOIN [HANDEL].[HM].[MG] MG ON MG.khid = C.id
INNER JOIN [HANDEL].[HM].[MZ] MZ ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId

WHERE MG.anulowany = 0
  AND MG.magazyn = 65559
  AND MG.typ_dk IN ('MW1', 'MP')
  AND MG.data <= @DataDo
  AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO',
                   'Paleta plastikowa', 'Paleta Drewniana')

GROUP BY C.id, C.Shortcut, C.Name, WYM.CDim_Handlowiec_Val
HAVING ABS(SUM(MZ.Ilosc)) > 0
ORDER BY C.Shortcut
```

**Korzyści:**
- 1 zapytanie zamiast 3-5
- Wszystkie typy opakowań w jednym wierszu
- Brak FULL OUTER JOIN (wolny)
- Brak OUTER APPLY (wolny)

---

## 3. CACHE W PAMIĘCI

```csharp
public class SaldaCache
{
    private static List<SaldoKontrahenta> _cache;
    private static DateTime _cacheTime;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);

    public static bool IsValid =>
        _cache != null && DateTime.Now - _cacheTime < CacheLifetime;

    public static void Set(List<SaldoKontrahenta> data)
    {
        _cache = data;
        _cacheTime = DateTime.Now;
    }

    public static List<SaldoKontrahenta> Get() => _cache;

    public static void Invalidate() => _cache = null;
}
```

**Zasada:**
- Pierwsze wejście = pobierz z bazy
- Kolejne wejścia w ciągu 5 min = użyj cache
- Przycisk "Odśwież" = wymuś pobranie z bazy

---

## 4. STRUKTURA PLIKÓW (uproszczona)

```
Opakowania/
├── Models/
│   └── SaldoKontrahenta.cs      # Jeden model - wszystkie dane
│
├── Services/
│   ├── SaldaService.cs          # Pobieranie danych (1 zapytanie)
│   ├── PdfReportService.cs      # [ZACHOWANY] - generowanie PDF
│   └── EmailService.cs          # Wysyłka email
│
├── Views/
│   ├── MainWindow.xaml          # Lista kontrahentów
│   └── SzczegolyWindow.xaml     # Szczegóły + PDF
│
├── ViewModels/
│   ├── MainViewModel.cs
│   └── SzczegolyViewModel.cs
│
└── Resources/
    └── Styles.xaml
```

**Usunięte zbędne pliki:**
- Converters.cs (inline)
- Wiele modeli (jeden wystarczy)
- DodajPotwierdzenieWindow (na później)
- SaldaWszystkichOpakowanWindow (duplikat)

---

## 5. MODEL DANYCH - Jeden Prosty

```csharp
public class SaldoKontrahenta
{
    public int Id { get; set; }
    public string Kontrahent { get; set; }  // Shortcut
    public string Nazwa { get; set; }
    public string Handlowiec { get; set; }

    // Salda - wszystkie typy w jednym obiekcie
    public int E2 { get; set; }
    public int H1 { get; set; }
    public int EURO { get; set; }
    public int PCV { get; set; }
    public int DREW { get; set; }

    public DateTime? OstatniDokument { get; set; }

    // Obliczane
    public int SumaWszystkich => Math.Abs(E2) + Math.Abs(H1) +
                                  Math.Abs(EURO) + Math.Abs(PCV) + Math.Abs(DREW);
    public bool MaSaldo => SumaWszystkich > 0;

    // Formatowanie
    public string E2Tekst => FormatSaldo(E2);
    public string H1Tekst => FormatSaldo(H1);
    // itd...

    private string FormatSaldo(int s) => s == 0 ? "-" :
        (s > 0 ? $"{s} wyd." : $"{Math.Abs(s)} zwr.");
}
```

---

## 6. GŁÓWNE OKNO - Szybkie i Proste

```
┌────────────────────────────────────────────────────────────────┐
│  SALDA OPAKOWAŃ                              [Odśwież] [?]     │
├────────────────────────────────────────────────────────────────┤
│  Szukaj: [________________]   Handlowiec: [Wszyscy ▼]          │
├────────────────────────────────────────────────────────────────┤
│  Kontrahent     │  E2   │  H1  │ EURO │ PCV  │ DREW │ Ostatni  │
├─────────────────┼───────┼──────┼──────┼──────┼──────┼──────────┤
│  ABC FIRMA      │  150  │  -   │  20  │  -   │  -   │ 15.12    │
│  XYZ SP ZOO     │  -50  │  10  │  -   │  -   │  -   │ 18.12    │
│  ...            │       │      │      │      │      │          │
├────────────────────────────────────────────────────────────────┤
│  Σ Wydane: 1234    Σ Zwroty: 567    Kontrahentów: 89           │
└────────────────────────────────────────────────────────────────┘

Kolory:
- Czerwony = kontrahent winny (saldo > 0)
- Zielony = my winni (saldo < 0)
- Szary = zero

Kliknięcie wiersza → otwiera szczegóły
```

---

## 7. OKNO SZCZEGÓŁÓW

```
┌────────────────────────────────────────────────────────────────┐
│  ← ABC FIRMA                          [PDF] [PDF+Email]        │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐   │
│  │   E2    │ │   H1    │ │  EURO   │ │   PCV   │ │  DREW   │   │
│  │   150   │ │    0    │ │   20    │ │    0    │ │    0    │   │
│  │  winny  │ │    -    │ │  winny  │ │    -    │ │    -    │   │
│  └─────────┘ └─────────┘ └─────────┘ └─────────┘ └─────────┘   │
│                                                                 │
├────────────────────────────────────────────────────────────────┤
│  Okres: [01.11.2024] - [21.12.2024]   [Ten mies.] [3 mies.]    │
├────────────────────────────────────────────────────────────────┤
│  Data       │ Dokument        │  E2  │  H1  │ EURO │ PCV │DREW │
├─────────────┼─────────────────┼──────┼──────┼──────┼─────┼─────┤
│  SALDO NA 21.12               │  150 │   0  │  20  │  0  │  0  │
├─────────────┼─────────────────┼──────┼──────┼──────┼─────┼─────┤
│  20.12.2024 │ MW/123/24       │  +50 │      │      │     │     │
│  18.12.2024 │ MW/120/24       │ +100 │      │  +20 │     │     │
│  ...        │                 │      │      │      │     │     │
├─────────────┼─────────────────┼──────┼──────┼──────┼─────┼─────┤
│  SALDO NA 01.11               │    0 │   0  │   0  │  0  │  0  │
└────────────────────────────────────────────────────────────────┘
```

---

## 8. PRZEPŁYW DANYCH

```
1. Start aplikacji
   └── Pokaż puste okno z spinnerem (< 100ms)

2. Pierwsze pobranie danych
   └── 1 zapytanie SQL (~500ms)
   └── Zapisz w cache
   └── Pokaż listę

3. Filtrowanie/sortowanie
   └── Operacje na cache w pamięci (< 10ms)
   └── Bez zapytań do bazy!

4. Kliknięcie kontrahenta
   └── 1 zapytanie na dokumenty (~200ms)
   └── Otwórz okno szczegółów

5. Generowanie PDF
   └── Użyj danych z pamięci
   └── Wygeneruj PDF (zachowany stary kod)
```

---

## 9. OPTYMALIZACJE WYDAJNOŚCI

| Problem | Rozwiązanie |
|---------|-------------|
| Wiele zapytań | 1 zapytanie na wszystko |
| N+1 dla potwierdzeń | Pominięte (dodamy później) |
| Wolne FULL OUTER JOIN | Proste INNER JOIN + GROUP BY |
| OUTER APPLY | Usunięte |
| Ładowanie przy każdym wejściu | Cache 5 minut |
| Filtrowanie = nowe zapytanie | Filtrowanie w pamięci |

---

## 10. CO ZACHOWUJEMY

1. **PdfReportService.cs** - generowanie PDF (działa dobrze)
2. **Format PDF** - strona 1 podsumowanie, strona 2+ dokumenty
3. **Kolorystyka** - czerwony/zielony/szary
4. **Logika salda** - wydane/zwrot

---

## 11. CO USUWAMY

1. ❌ Potwierdzenia (na później - osobna funkcjonalność)
2. ❌ Alerty i progi (komplikacja)
3. ❌ Wykresy tygodniowe (na później)
4. ❌ Historia zmian
5. ❌ Przypomnienia
6. ❌ Statystyki dashboard
7. ❌ Wiele okien (2 wystarczą)

---

## 12. HARMONOGRAM IMPLEMENTACJI

### Faza 1: Rdzeń (TERAZ)
- [ ] Nowy SaldaService z 1 zapytaniem
- [ ] Nowy model SaldoKontrahenta
- [ ] MainWindow - lista
- [ ] SzczegolyWindow - dokumenty
- [ ] Integracja z istniejącym PDF

### Faza 2: Ulepszenia (PÓŹNIEJ)
- [ ] Cache
- [ ] Potwierdzenia
- [ ] Wykresy
- [ ] Eksport Excel

---

## Pytanie do Ciebie:

**Czy ten projekt Ci odpowiada?**

1. Czy mogę zacząć implementację od Fazy 1?
2. Czy są jakieś funkcje które MUSZĄ być od początku?
3. Czy format PDF ma pozostać bez zmian?

---

*Projekt utworzony: 2024-12-21*
