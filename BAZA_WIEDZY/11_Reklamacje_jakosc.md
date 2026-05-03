# 11 — Reklamacje i jakość

## Workflow reklamacji (PROCEDURY_07_JAKOSC)

1. **Klient zgłasza** (przez handlowca lub bezpośrednio) → **numer reklamacji**
2. **Klasyfikacja:**
   - **Jakościowa** (np. krwiak, złamanie, żółć)
   - **Ilościowa** (mniej kg niż na fakturze)
   - **Transportowa** (uszkodzenia w drodze)
3. **Badanie** (hala, dokumenty, kamery, logi ZPSP)
4. **Konsultacja** Dyrektor + Zarząd
5. **Decyzja:** pełne / częściowe / odrzucenie
6. **Odpowiedź klientowi do 15:00 tego dnia**
7. **Zamknięcie 48h**
8. **Działania korygujące (CAPA):** co/kto/do kiedy
9. **Archiwizacja**

---

## SLA

| Etap | Czas |
|---|---|
| Odpowiedź klientowi | **do 15:00 tego dnia** |
| Zamknięcie reklamacji | **48h** od zgłoszenia |
| Działania korygujące | wpisane w CAPA register |

---

## Statusy reklamacji w ZPSP

**Plik:** `Reklamacje/Models/ReklamacjeModels.cs`

```csharp
public const string ZGLOSZONA = "ZGLOSZONA";
public const string W_ANALIZIE = "W_ANALIZIE";
public const string ZASADNA = "ZASADNA";
public const string POWIAZANA = "POWIAZANA";
public const string ZAMKNIETA = "ZAMKNIETA";
public const string ODRZUCONA = "ODRZUCONA";
```

---

## ⚠️ KRYTYCZNE: 75% reklamacji to AUTO-IMPORT

**75% rekordów w bazie reklamacji** to **AUTO-IMPORT faktur korygujących z Symfonii** — NIE są to faktyczne reklamacje jakościowe!

**Typy faktur korygujących z Symfonii:**
- **FKS** — faktura korygująca sprzedaży
- **FKSB** — faktura korygująca sprzedaży B
- **FWK** — faktura wewnętrzna korygująca

**Skąd biorą się?**
- Klient zwrócił część towaru (najczęstsze)
- Korekta ceny (rabat zatwierdzony po fakcie)
- Pomyłka ilościowa (przeliczenie po załadunku)
- Błąd faktury (literówka, zła nazwa)

**Implikacja dla statystyk:**
- Faktyczna liczba reklamacji jakościowych jest **~25% z bazy**
- Statystyki "ile reklamacji w miesiącu" są **zawyżone**
- W dashboardach **MUSI być filtr** "tylko prawdziwe reklamacje" (`Status != AUTO_IMPORT_KOREKTA` lub podobny)

---

## Faktyczne reklamacje jakościowe — typowe powody

| Powód | Częstotliwość | Skąd się bierze |
|---|---|---|
| **Krwiak / czerwony filet** | Częste | Hodowla — kurczak źle złapany, wybroczyny mięśnia |
| **Złamanie** | Częste | Hodowla — kurczak źle złapany |
| **Żółć (nieusunięta)** | Średnio | Patroszenie — torebka żółciowa nieusunięta |
| **Oparzenia skóry** | Rzadko | Parzenie — temperatura za wysoka |
| **Otwarte rany** | Rzadko | Hodowla / transport |
| **Termometr (towar ciepły)** | Rzadko | Transport — chłodnia wadliwa |

---

## Moduł Reklamacje w ZPSP

**Folder:** `Reklamacje/`

**Główne pliki:**
- `FormPanelReklamacjiWindow.xaml.cs` — panel główny (lista reklamacji)
- `FormReklamacjaWindow.xaml.cs` — pojedyncza reklamacja (powiązana z fakturą bazową + opcjonalnie z korektą)
- `FormSzczegolyReklamacjiWindow.xaml.cs` — szczegóły
- `UzupelnijReklamacjeWindow.xaml.cs` — uzupełnianie info do auto-importowanych korekt
- `Analityka/StatystykiReklamacjiWindow.xaml.cs` — statystyki

**Sercu modułu — `FormReklamacjaWindow` konstruktor:**

```csharp
public FormReklamacjaWindow(string connStringHandel, int dokId, int kontrId,
    string nrDok, string nazwaKontr, string user,
    string connStringLibraNet = null)

// Lub konstruktor "przypiety do korekty":
public FormReklamacjaWindow(string connStringHandel, int idFakturyBazowej,
    int kontrId, string nrFakturyBazowej, string nazwaKontr, string user,
    string connStringLibraNet, int idKorekty, string nrKorekty,
    DateTime? dataKorekty, decimal? wartoscKorekty, decimal? kgKorekty)
```

**Connection strings:**
- `Handel` — `Reklamacje.ReklamacjeConnectionStrings.Handel` = `192.168.0.112`
- `LibraNet` — `Reklamacje.ReklamacjeConnectionStrings.LibraNet` = `192.168.0.109`

**Service:** `ReklamacjeEmailService.cs` — wysyłka maili
**PDF:** `ReklamacjePDFGenerator.cs` — generowanie protokołów

**SQL skrypty:**
- `Reklamacje_CreateTables.sql` — tworzenie tabel
- `Reklamacje_AlterTables.sql` — modyfikacje schematu
- `Reklamacje_Ustawienia.sql` — konfiguracja

---

## Kontrola jakości — Justyna Chrostowska

**Rola:** Specjalista ds. Jakości (formalnie) + de facto Dyrektor Zakładu (oczekiwanie Sergiusza).

**Limit decyzyjny:** **1000 zł** (większe → Sergiusz).

**Harmonogram (5 obchodów dziennie):**
- 7:00, 9:00, 11:00, 13:00, 15:00

**Co kontroluje:**
- Temperatury chłodni / mroźni / hal
- Czystość po nocnej zmianie
- Etykiety, kategorie, kamery (Hikvision)
- Krojenie (oznakowanie)
- II zmianę (popołudnie)

**Pain point Sergiusza:**
> *"Justyna nie chodzi po hali wystarczająco. Zerka tylko w kamery, ale powinna obserwować, chodzić, usprawniać procesy, sprawdzać kierowników. Nie do końca to robi."*

**Nowa rola w planie:** Klaudia Osińska jako Asystent ds. Jakości (formalizacja).

---

## Dział kontroli jakości — pain points

> *"Kontrola jakości — brak procedur, niejasne jak to robią."* (Sergiusz, scena 3)

**Brak procedur:** Sergiusz przyznaje że dział jakości nie ma jasno spisanych procedur kontroli klasyfikacji A/B (mimo PROCEDURY_07_JAKOSC). To prawdopodobnie znaczy że **procedury są na papierze, ale nie są stosowane w praktyce**.

---

## BRC / IFS (cele 2026)

**Status:** W przygotowaniu (Justyna prowadzi projekt).

**Wymagania krytyczne:**
- Onboarding pracowników agencyjnych — szkolenie stanowiskowe + podpis
- Weryfikacja po 1 tygodniu i 1 miesiącu (BRC/IFS wymóg)
- Bez szkolenia + podpisu → NIE wchodzi na halę
- Procedury HACCP (Critical Control Points)
- Audyty wewnętrzne

---

## HACCP

**Tabela w ZPSP:** `Haccp` (LibraNet) — pomiary temperatur, krytyczne punkty kontroli.

**Moduł:** `Partie/Services/PartiaService.cs` — `GetHaccpAsync(string partia)`.

**Tabela `partii.HaccpModel`** (uproszczone, sprawdzić w `PartiaModels.cs`).

---

## Reklamacje od hodowców (do nas)

**Hodowca dostaje feedback:**
- Gdy partia ma dużo klasy B
- Gdy reklamacje od klientów
- Drugi raz źle → **obcinamy ilość** dostawy

**Specyfikacja drobiu** ma rubrykę **"klasa B"** — tam można zobaczyć dane historyczne hodowcy.

**Pomysł Sergiusza (z PYTANIA_PRODUKCJA):**
> *"Fajnie by było aby przy odebraniu hodowcy można było sprawdzać jak często jego partia jest reklamowana, ile klasy B i A ma."*

**Akcja:** Ranking hodowców per partia + alert "Stróżewski 3 reklamacje — rozmowa".

---

## Pomysł integracji 4 okien Reklamacje

**Problem:** Są 4 osobne okna (`FormReklamacjaWindow`, `FormPanelReklamacjiWindow`, `FormSzczegolyReklamacjiWindow`, `UzupelnijReklamacjeWindow`) — duplikaty + niejasne jak są ze sobą powiązane.

**Pomysł audytu (Task #27 completed):**
- `FormPanelReklamacjiWindow` = główny panel (lista)
- `FormReklamacjaWindow` = pojedyncza reklamacja (CRUD)
- `FormSzczegolyReklamacjiWindow` = read-only szczegóły
- `UzupelnijReklamacjeWindow` = uzupełnianie auto-importowanych korekt

**Status:** zostawić jak jest póki Sergiusz nie poda priorytetu konsolidacji.
