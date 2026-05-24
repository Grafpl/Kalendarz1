# Instrukcja: Panel Reklamacji — deep

> **Dla kogo**: Jola (głównie), Sergiusz, handlowcy, dział jakości.
> **Co robi**: rejestrujesz, śledzisz i zamykasz reklamacje klientów + obsługujesz auto-importowane korekty faktur z Symfonii. Workflow 6-stanowy, SLA tracking, statystyki.
> **Pliki kodu**: `Reklamacje/Views/FormPanelReklamacjiWindow.xaml`, `FormReklamacjaWindow`, `FormSzczegolyReklamacjiWindow`, `FormRozpatrzenieWindow`, `StatystykiReklamacjiWindow`, `Reklamacje/Services/ReklamacjeService.cs`.
> **Otwierane z**: menu ZPSP → **📋 Panel Reklamacji**.

---

## 1. Dwa rodzaje rekordów (KLUCZOWE)

### A. Prawdziwa reklamacja
- Klient zadzwonił, coś nie tak z produktem.
- Pełny workflow (rozpatrzenie, akceptacja/odrzucenie).

### B. Korekta faktury (auto-import z Symfonii)
- Księgowa wystawiła FKS/FKSB/FWK w Symfonii.
- System sam wciąga jako reklamację typu **Faktura korygująca**, `WymagaUzupelnienia=1`, status "Oczekuje".
- ~**75% wszystkich rekordów** to korekty (szum).

> ⚠ Filtruj **Typ ≠ Faktura korygująca** w codziennej pracy, żeby widzieć tylko prawdziwe problemy.

---

## 2. Główne okno — anatomia

`FormPanelReklamacjiWindow` (Maximized), tytuł "Panel Reklamacji — Zarządzanie".

```
┌────────────────────────────────────────────────────────────────────────┐
│ [Status ▼] [Typ ▼] [Priorytet ▼] [Handlowiec ▼] [🔍 Szukaj smart]      │
│ A: 12  P: 8  Z: 145  (mini-karty liczników)                            │
│ [DO ZROBIENIA] [W TOKU] [HISTORIA] (zakładki workflow)                 │
├────────────────────────────────────────────────────────────────────────┤
│ DataGrid 16 kolumn (czerwona ramka)                                    │
│ Data | Nr dok | Kontrahent | Źródło | Handlowiec | Typ | Korekta | Kg  │
│ | Status | Zgłaszający | Rozpatruje | Zakończył | ...                  │
│ Podświetlenia: żółte (SLA 7+ dni), czerwone (SLA 14+ dni)             │
├────────────────────────────────────────────────────────────────────────┤
│ [+ Nowa reklamacja ▼] [Excel] [Stat] [Gęstość ▼]                       │
└────────────────────────────────────────────────────────────────────────┘
```

### Mini-karty liczników (3)

| Karta | Kolor | Formuła (KategoriaZakladki) |
|---|---|---|
| **A** (DO_AKCJI) | 🔴 czerwony | UserZakonczenia NULL && status nie-finalny && OsobaRozpatrujaca NULL && status != W_ANALIZIE |
| **P** (W_TOKU) | 🟠 pomarańcz. | OsobaRozpatrujaca NOT NULL OR status = W_ANALIZIE |
| **Z** (ZAMKNIETE) | 🟢 zielony | UserZakonczenia NOT NULL OR status IN {ZAMKNIETA, ODRZUCONA, POWIAZANA, ZASADNA} |

### 3 zakładki workflow

- **DO ZROBIENIA** (czerwony #E74C3C) — KategoriaZakladki='DO_AKCJI'.
- **W TOKU** — OsobaRozpatrujaca NOT NULL OR W_ANALIZIE.
- **HISTORIA** — KategoriaZakladki='ZAMKNIETE'.

### 16 kolumn DataGrid

Data · Nr dokumentu (zielony=korekta, pomarańczowy=faktura bazowa) · Kontrahent · Źródło (ikona+label) · Handlowiec (avatar) · Typ · Korekta (🔗) · Kg (prawo) · Status (kolor) · Zgłaszający · Rozpatruje · Zakończył · + Zdjęcia inline · SLA · Historia.

### Kolory źródła

| Źródło | Tło / tekst |
|---|---|
| Handlowiec | #E3F2FD / #1565C0 |
| Kierowca | #FFF3E0 / #E65100 |
| Klient | #F3E5F5 / #6A1B9A |
| Symfonia | #E8F5E9 / #2E7D32 |
| Jakość | #FFEBEE / #C62828 |

### Kolory statusu

| Status | Tło / tekst |
|---|---|
| ZGLOSZONA (Nowa) | #FDEDEC / #C0392B |
| Oczekuje | #FFE0B2 / #E65100 |
| W_ANALIZIE | #FFF8E1 / #E67E22 |
| ZASADNA (Uznana) | #E8F5E9 / #2E7D32 |
| ODRZUCONA | #FFEBEE / #C62828 |
| POWIAZANA | #F3E5F5 / #7B1FA2 |
| ZAMKNIETA | #ECEFF1 / #546E7A |

### Podświetlenia SLA

- **7+ dni bez akcji** (JestZagrozonySLA): żółte tło #FFF8E1.
- **14+ dni** (JestKrytycznySLA): czerwone tło #FFEBEE.

---

## 3. Filtry

| Filtr | Opcje |
|---|---|
| **Status** | Wszystkie / Zgłoszona / W analizie / Zasadna / Odrzucona / Powiązana / Zamknięta |
| **Typ** | Wszystkie / 9 typów (Jakość, Ilość, Transport, Termin, Niezgodność, Temperatura, Ciała obce, **Faktura korygująca**, Inne) |
| **Priorytet** | Wszystkie / Niski 🔘 / Normalny 🔵 / Wysoki 🟠 / Krytyczny 🔴 |
| **Handlowiec** | Wszyscy / dynamicznie z HANDEL.ContractorClassification |

### 🔍 Smart Search Parser

Pole szukania rozpoznaje **komendy**:
- `moje` / `mine` → tylko moje.
- `nowe` / `new` → tylko z ostatnich 24h.
- `vip` → tylko VIP.
- `od:2026-05-01`, `od:dzisiaj`, `od:tydzień` → DataOd.
- `do:...` → DataDo.
- `partia:5891` → filtr partii.
- `kg>100`, `<50`, `kg=X` → MinKg/MaxKg.
- free text → szuka w opisach, numerach, kontrahentach.

---

## 4. Tworzenie reklamacji — 3 ścieżki

Kliknięcie **"+ NOWA REKLAMACJA"** (split button) → menu:

### Ścieżka 1: 📄 Do faktury

1. Wybierz kontrahenta → fakturę z jego listy.
2. Otwiera `FormReklamacjaWindow` z **3 panelami**.

### Ścieżka 2: ✏ Do korekty Symfonii

- Wybierz korektę (FKS/FKSB/FWK) z HANDEL.DK.
- Panel korekty (prawy) ukryty (przypisana sztywno).

### Ścieżka 3: ❓ Bez faktury

- Tylko dane reklamacji (bez towarów/partii).
- Faktura przypisana później.

### FormReklamacjaWindow — 3 panele

```
┌──────────────────┬─────────────────────┬───────────────────────┐
│ LEWY (min 350px) │ ŚRODEK (340px)      │ PRAWY (320px)         │
│                  │                     │                       │
│ TOWARY z faktury │ Dane reklamacji:    │ KOREKTY z Symfonii:   │
│ ☑ Filet, Kg✎     │ Typ [▼]             │ ○ FKS/2026/078        │
│ ☑ Skrzydła       │ Podkategoria [▼]    │ ○ FKSB/2026/045       │
│ Footer: suma     │ Priorytet (kropka)  │ ○ Brak korekty        │
│                  │ Opis * (textarea)   │                       │
│ PARTIE DOSTAWCY  │ Szablony (buttony)  │ (HANDEL.DK seria      │
│ ☑ 5891 Wojtek    │                     │  sFKS/sFKSB/sFWK,     │
│ ☐ 5895 Mazur     │ ZDJĘCIA:            │  data >= -90 dni)     │
│ Footer: zaznacz. │ drag&drop, kompres. │                       │
│                  │ 800x600, q85, podgl.│                       │
└──────────────────┴─────────────────────┴───────────────────────┘
                    [Anuluj] [ZGŁOŚ REKLAMACJĘ]
```

### Towary (lewy panel)

- DataGrid: ☑ | Nazwa | Kg ✎ (edytowalny, żółte tło) | Cena | Wartość.
- Tylko IsSelected=true trafiają do bazy.
- Waga edytowalna → porównanie Kg-Waga pokazuje deltę.
- Footer: "Zaznaczono: X | Suma: Y kg | Z zł".

### Partie dostawcy (lewy panel niżej)

- ☑ | Nr partii | Nazwa dostawcy | Data.
- Multi-select — z której partii pochodzi reklamowany towar (= który hodowca! attribution).

### Zdjęcia (środek)

- **Drag&Drop** (overlay "Upuść pliki tutaj") lub przycisk "+ Dodaj".
- **Auto-kompresja** do max 800×600px, quality 85%.
- Lewa: lista miniatur. Prawa: duży podgląd zaznaczonego.

### Korekty Symfonii (prawy)

- Widoczne tylko jeśli tryb != "Do korekty".
- Radio-button single-select.
- Query: `HANDEL.DK seria IN ('sFKS','sFKSB','sFWK') AND data >= -90 dni`.
- Check: jeśli już powiązana → skip.

### Walidacja zapisu

1. Kontrahent + Faktura wybrane.
2. ≥1 towar zaznaczony.
3. Opis nie pusty.
4. Jeśli "Do korekty" → auto-link PowiazanaReklamacjaId.

### Co zapisuje

- INSERT Reklamacje: DataZgloszenia=NOW, UserID, ZrodloZgloszenia, StatusV2='ZGLOSZONA', WymagaUzupelnienia, Handlowiec.
- INSERT ReklamacjeTowary (zaznaczone).
- INSERT ReklamacjePartie (zaznaczone partie).
- INSERT ReklamacjeZdjecia (blob compress).
- UPDATE PowiazanaReklamacjaId (jeśli korekta wybrana).

---

## 5. FormSzczegolyReklamacjiWindow

```
┌────────────────────────────────────────────────────────────────┐
│ REKLAMACJA #12  🔴 Nowa     ⏱ 2 dni od zgłoszenia             │
│ FLOW: Zgłosił 🔵JK → Przyjął 🟠SP → Zakończył ⚫               │
├──────────────────────────┬─────────────────────────────────────┤
│ LEWY (400px)             │ PRAWY (*)                           │
│ 4 mini-karty (2×2):      │ [ZDJĘCIA] [HISTORIA] [POWIĄZANE]    │
│ - Dokument               │                                     │
│ - Kontrahent             │ Historia zmian (DataGrid):          │
│ - Wartości (kg+zł)       │ Data | stary→nowy | user | komentarz│
│ - Osoby (avatary)        │                                     │
│ KATEGORYZACJA            │ Powiązane reklamacje:               │
│ OPIS PROBLEMU            │ 🔗 #ID lub nr korekty               │
│ NOTATKI JAKOŚCI (red)    │                                     │
└──────────────────────────┴─────────────────────────────────────┘
   [Rozpatrz] [Zamknij] [Edytuj]
```

- **3-Avatar Flow**: Zgłosił (blue) → Przyjął (orange) → Zakończył (green) + status outcome (✓ Zatwierdzona / ✗ Odrzucona / 🏁 Zamknięta).
- **Edytuj** dostępny tylko jeśli status = ZGLOSZONA.

---

## 6. FormRozpatrzenieWindow — 4 opcje (karty)

| Opcja | Kolor | Pola | Status po |
|---|---|---|---|
| **PRZYJĘTA** | 🟠 #F39C12 | brak (natychmiast) | OsobaRozpatrujaca=ja, W_ANALIZIE, DataAnalizy=NOW |
| **ZAAKCEPTOWANA** | 🟢 #27AE60 | Przyczyna* + Akcje naprawcze | ZASADNA, UserZakonczenia=ja, DecyzjaJakosci='ZAAKCEPTOWANA' |
| **ODRZUCONA** | 🔴 #E74C3C | Powód odrzucenia* (obowiązkowy!) | ODRZUCONA, DecyzjaJakosci='ODRZUCONA' |
| **COFNIJ** | 🔘 #95A5A6 | brak (jeśli status != Nowa) | ZGLOSZONA, OsobaRozpatrujaca=NULL, UserZakonczenia=NULL |

Walidacja: ODRZUCONA bez powodu → toast "Powód odrzucenia jest wymagany".

### Workflow pełny

```
ZGLOSZONA → (Rozpatrz/Przyjmij) → W_ANALIZIE → (Zaakceptuj/Odrzuć) →
ZASADNA / ODRZUCONA → (Zamknij) → ZAMKNIETA
```

Każde przejście zapisuje historię (kto + kiedy + stary→nowy status).

---

## 7. Statystyki (StatystykiReklamacjiWindow)

Dark mode (#0F1419 tło). Tytuł "INSPEKCJA KONTRAHENTA — KOREKTY MIESIĄCA".

### Filtry (4 panele przycisków)

- **Lata** (dynamiczne z danych + "Wszystkie").
- **Miesiące** ("Cały rok" + Sty-Gru).
- **Szybkie zakresy** (30/90 dni, pół roku, rok, 2 lata, wszystko).
- **Grupowanie** (tydzień/miesiąc/kwartał/rok).

### 6 KPI cards

| Karta | Co |
|---|---|
| **Liczba Korekt** | COUNT(distinct IdDK) |
| **Suma Straty kg** | SUM(StrataKg) |
| **Suma Straty zł** | SUM(StrataZl) red |
| **Avg Strata/Korektę** | SUM/COUNT |
| **Top Kontrahent** | max strata |
| **Top Typ** | FKS/FKSB/FWK max |

### Wykresy (LiveCharts.Wpf)

- Trend Straty (LineChart).
- Typ Korekty (PieChart — FKS/FKSB/FWK, kolory #3B82F6/#10B981/#F59E0B).
- Top Produkty (BarChart).
- DataGrid detali + export.

Strata = (Kg oryginalna - Kg po korekcie) × Cena.

---

## 8. Auto-import korekt (mechanizm)

`SyncFakturyKorygujace()` uruchamia się **raz przy Load okna** (brak widocznego schedulera, brak progress bar).

1. Pobiera z HANDEL korekty FKS/FKSB/FWK gdzie `data >= DataOdKorekt`.
2. Dla każdej: CHECK czy już w Reklamacje (po IdDokumentu + Typ='Faktura korygująca').
3. INSERT: ZrodloZgloszenia='Symfonia', StatusV2='ZGLOSZONA', WymagaUzupelnienia=1.
4. **ProbujAutoMatch** — szuka istniejącej reklamacji handlowca:
   - Na tej samej fakturze bazowej (IdFakturyOryginalnej) → UPDATE PowiazanaReklamacjaId + status POWIAZANA.
   - Fallback: ten sam khid + data w range (-14d, +3d) gdzie status ZGLOSZONA.

### Co Jola robi z każdą korektą "Oczekuje"

1. Otwórz (PPM → Edytuj lub dwuklik).
2. Dopisz opis.
3. Zmień typ z "Faktura korygująca" na właściwy (jeśli to faktyczna reklamacja).
4. Status "Oczekuje" → "Zasadna".
5. Zamknij.

---

## 9. SLA — dwa zegary

| Zegar | Limit | Od → do | Kolory |
|---|---|---|---|
| **Jakości** | 24h | DataZgloszenia → DataAnalizy | zielony OK / żółty <6h / pomarańczowy <12h / czerwony po terminie |
| **Rozwiązania** | 7 dni roboczych | DataZgloszenia → DataZakonczenia | analogicznie |

Etykiety: "✓ Xm/h" (done), "⏰ Xm/h" (remaining), "🔥 +Xh po".

---

## 10. Email + PDF (status implementacji)

### ReklamacjeEmailService — częściowy (60%)

3 szablony: nowa reklamacja, zmiana statusu, raport (z PDF). **SMTP placeholder** (_smtpUser="") — wysyłka nieaktywna dopóki nieskonfigurowane. To: reklamacje@piorkowscy.pl (internal).

### ReklamacjePDFGenerator — beta (40%)

Generuje **HTML** (nie PDF!): `~/Documents/ReklamacjeRaporty/Reklamacja_{id}_{ts}.html`. Otwiera w przeglądarce. PDF = ręcznie "drukuj do PDF" z przeglądarki.

> Workaround dla PDF: Excel eksport → otwórz w Word/LibreOffice → zapisz jako PDF.

---

## 11. Typowy dzień Joli

```
08:00  Panel Reklamacji. Mini-karty: A:12, P:8, Z:145.
08:02  Filtr: Status="Zgłoszona", Typ ≠ "Faktura korygująca". → 3 prawdziwe.
08:05  Karmar, drip loss 30% paczek. Dwuklik → szczegóły → partia 5891 (Wojtek).
08:08  Sprawdza partię 5891 (Lista Partii) → chill compliance FAIL!
08:10  "Rozpatrz" → "Zaakceptuj". Notatka: "Awaria chłodni 18.05, Karmar ma rację".
08:12  Wystawia korektę w Symfonii.
08:30  System auto-tworzy reklamację "Faktura korygująca" → Jola powiąże z oryginalną.
09:00  Lidl, pióro w paczce. Zdjęcia potwierdzają. Akceptuje, +1 QC dział pakowania.
W ciągu dnia: uzupełnianie pomarańczowych "Oczekuje" (korekty).
17:00  A: 4 zostały. Klik Stat → przegląd miesięczny.
```

---

## 12. FAQ

**P: 75% to korekty — bug?**
O: Nie. Auto-import z Symfonii. Filtruj Typ ≠ Faktura korygująca.

**P: "Oczekuje" (pomarańczowy)?**
O: Korekta bez uzupełnienia. Handlowiec musi dopisać opis i właściwy typ.

**P: Brak klientów w autosugestii?**
O: Lista z HANDEL.ContractorClassification. Pusta = problem z Symfonią.

**P: Usuwanie?**
O: Tylko Admin (UserID=11111). Zwykli zamykają.

**P: Powiązanie z partią?**
O: Closed loop hodowcy. Hodowca Scorecard (audyt NF01) to uwzględni.

**P: Email do klienta?**
O: ReklamacjeEmailService — SMTP nieskonfigurowane. Nie wpięte w UI.

**P: SLA "X dni od zgłoszenia"?**
O: Dni między DataZgloszenia a dziś. >7 dni = żółte tło (rozpatrz!).

**P: PDF raport?**
O: Generuje HTML, nie PDF. Excel → drukuj do PDF z Worda.

---

## 13. Co dalej

- **Faktury / KSeF** → `WPF/PanelFakturWindow.xaml`.
- **Partie** (attribution wad) → `03_Lista_Partii.md`.
- **Hodowca scorecard** (reklamacje jako 6. wskaźnik) → `BAZA_WIEDZY/AUDYT_BROILER_SIGNALS/` NF01.
- **Ulepszenia** (filtr "ukryj korekty", closed loop) → audyt U01.
