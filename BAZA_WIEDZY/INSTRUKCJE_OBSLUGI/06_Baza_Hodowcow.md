# Instrukcja: Baza Hodowców (Pozyskiwanie Hodowców) — deep

> **Dla kogo**: Maja (handlowiec, codziennie), Sergiusz (decyzje cenowe), zespół zakupów.
> **Co robi**: pełen **CRM hodowców** — lista + Karta 360° (22+ metryk) + Wizard 5-krokowy + Duplikaty (Union-Find) + Mapa GPS (WebView2/Leaflet) + Historia + KPI handlowców.
> **Pliki kodu**: `Hodowcy/PozyskiwanieHodowcowWindow.xaml`, `KartaHodowcyWindow`, `HodowcaWizardWindow`, `HodowcyDuplicateWindow`, `HodowcyMapaWindow`, `Services/HodowcaProfilService.cs`, `AnkietyHodowcow/HistoriaHodowcyWindowPremium_FINAL`.
> **Otwierane z**: menu ZPSP → **🐔 Hodowcy** (permission `accessMap[55]`). DB: LibraNet.

---

## 1. Po co — 1 zdanie

> "1874 hodowców — Maja codziennie dzwoni do 20 (status pipeline), Sergiusz widzi Kartę 360° z 22 metrykami per hodowca i decyduje o cenach."

---

## 2. 6 okien modułu

```
PozyskiwanieHodowcowWindow (CRM lista)
  ├─→ KartaHodowcyWindow (360° — klik na wierszu)
  ├─→ HodowcaWizardWindow (5-krokowy kreator — "+ Nowy")
  ├─→ HodowcyDuplicateWindow ("Duplikaty")
  ├─→ HodowcyMapaWindow ("Mapa")
  └─→ HistoriaHodowcyWindowPremium (z karty/rankingu)
```

---

## 3. PozyskiwanieHodowcowWindow — główne okno CRM

### Layout
- Lewa (70%): tabela 1874 hodowców (13 kolumn).
- Prawa (30%): panel pusty lub szczegóły wybranego.
- Górny pasek: tytuł, szukanie, licznik, przyciski.
- Pasek filtrów + szablon rozmowy + KPI handlowców + legenda statusów.

### Filtry (SQL WHERE)

| Filtr | Działanie |
|---|---|
| **Towar** | `Towar = X` — Wszystkie / **KURCZAKI** (domyślnie) / DRÓB / GĘSI / KACZKI / PERLICZKI |
| **Status** | 7 statusów pipeline |
| **Województwo** | auto-mapowane z prefixu kodu, kaskaduje powiaty |
| **Powiat** | drugi poziom (z OdbiorcyCRM) |
| **🔍 Szukaj** | `Dostawca LIKE OR Miejscowosc LIKE OR Tel1 LIKE` |

### 7 statusów pipeline

| Status | Kolor tekstu | Co |
|---|---|---|
| **Nowy** | #374151 szary | świeżo dodany |
| **Do zadzwonienia** | #78350F brąz | kolejka kontaktu |
| **Próba kontaktu** | #78350F brąz | dzwoniono, nie odebrał |
| **Nawiązano kontakt** | #14532D zielony | rozmawialiśmy, potencjał |
| **Zdaje** | #14532D zielony | aktywnie dostarcza |
| **Nie zainteresowany** | #7F1D1D czerwony | odmawia |
| **Obcy kontrakt** | #134E4A morski | ma umowę z inną ubojnią |

### 13 kolumn DataGrid

Status (badge) · Hodowca (czerwony jeśli duplikat) · Towar (kolor per towar) · Miejscowość · Województwo (#A78BFA) · Powiat (#C4B5FD) · Kod · **KM (sortowanie domyślne ASC — najbliżsi pierwsi)** · Telefon (2 numery stacked, zielony) · Ostatnia aktywność (avatar + imię + data + treść, ukryte jeśli IMPORT).

SQL: `SELECT h.*, (SELECT TOP 1 ... FROM Pozyskiwanie_Aktywnosci ORDER BY DataUtworzenia DESC) FROM Pozyskiwanie_Hodowcy WHERE Aktywny=1 ORDER BY KM ASC`.

### KPI handlowców (prawy panel, scroll poziomy)

Per użytkownik z `Pozyskiwanie_Aktywnosci`:
- **Dzis / Tydzień** (akcje).
- **n:X** (notatki), **s:Y** (zmiany statusu).
- Trend 90 dni, week-on-week.

Klik karty handlowca → modal 3-kolumnowy (alert cykli statusów A→B→A→B dla userId=11111, bar chart porównawczy, lista aktywności).

### Menu kontekstowe (PPM)

```
📞 Zadzwoń i rejestruj
🔄 Zmień status → (7 opcji)
🔍 Szukaj w Google
🗺️ Pokaż na mapie
```

### Szablon rozmowy (8 szablonów)

`_szablonyRozmow[]` hardcoded, rotacyjnie, `{nazwa}` → App.UserFullName. Np.:
> "Dzień dobry, tu {nazwa} z Ubojni Drobiu Piórkowscy. Szukamy nowych hodowców..."

Rotacja: `BtnNastepnySzablon_Click` (losowy).

### Przyciski

- **Duplikaty** (👥) → HodowcyDuplicateWindow.
- **Mapa** (🗺️) → HodowcyMapaWindow.
- **Odśwież** (↻, F5) → reload + **auto-refresh co 2 min** (zapamiętuje filtry + scroll + zaznaczenie).

### Auto-fix przy starcie

```sql
UPDATE Pozyskiwanie_Hodowcy SET Towar='KURCZAKI' WHERE Towar IS NULL OR Towar='' OR Towar='(brak)'
```
(Sprząta bulkowe importy z Excela bez Towaru.)

---

## 4. KartaHodowcyWindow — 360° (najmocniejsza część)

### Selektor okresu (header)

5 opcji — zmienia **WSZYSTKIE** KPI i wykresy (cutoff = `Today.AddDays(-OkresDni)`):
- 30 dni / **90 dni (domyślne)** / 6 miesięcy / rok / 2 lata / cały zakres.

### 4 paski KPI = 22+ metryki

#### Pasek 1 (6 — wolumen)
1. **Partii** = `Count(CreateData >= cutoff)`, sub "życie: X".
2. **Σ Żywiec** = `Sum(NettoSkup)`, sub "In0E: X kg".
3. **Wydajność śr.** = `Avg(WydajnoscProc)`, kolor zielony/żółty/czerwony.
4. **Ostatnia dostawa** = `Max(CreateData)` + "X dni temu".
5. **Śr. cykl dostaw** = mediana dni między partiami.
6. **Szacowany obrót** = `Sum(NettoSkup × CenaSkup)` + "śr. cena X zł/kg".

#### Pasek 2 (6 — ranking + klasy)
7. **Ranking** = #X / Y (vs 90d wszyscy), ocena Top 10%/25%/50%.
8. **Rynkowy udział** = moja suma / total ×100.
9. **Duży (4-7)** % (z In0E, niebieski).
10. **Mały (8-12)** % (pomarańczowy).
11. **Temp. rampy śr.**
12. **Padłe %**, kolor ≤2% zielony / ≤5% żółty / >5% czerwony.

#### Pasek 3 (5 — cykl)
13. **Śr. wiek partii** (dni od wstawienia), norma 35-42 zielony.
14. **Śr. waga sztuki** = NettoSkup / SztDekl.
15. **Straty śmiertelności** = StratySzt / SztDekl ×100, <2% zielony.
16. **Pierwsza/ostatnia dostawa** + "X lat współpracy".
17. **Partii z wstawieniem** = X / Y (% z HarmonogramLp).

#### Pasek 4 (6 — konfiskaty + transport, żółte tło)
18. **CH** (Charłaki, DeclI3).
19. **NW** (Niewykrwawione, DeclI4).
20. **ZM** (DeclI5).
21. **LUMEL** (Σ tuszek, LumQnt).
22. **Konfiskaty %** = (CH+NW+ZM)/SztDekl ×100, <1% zielony / >3% czerwony.
23. **Ubytek transp. %** = (NettoH - NettoU)/NettoH ×100, 1-3% zielony.

> 💡 Te metryki to gotowy fundament Hodowca Scorecard z audytu Broiler Signals (NF01).

### 6 zakładek

| Tab | Co |
|---|---|
| **📊 Przegląd** | Mini-stat tekstowy + 2 wykresy (Trend wydajności+klasaB, Wolumen kolumny) |
| **⚖️ Klasy wagowe** | Wykres 9 klas (Duży niebieski / Mały pomarańczowy) + tabela udziałów |
| **🏆 Ranking** | Alert pozycji + 6 kart KPI (3×2) |
| **⚠️ Anomalie** | DataGrid krańcowych partii (najlepsza/najgorsza wydajność, największa, najwięcej padłych) |
| **🌡️ Jakość** | 3 wykresy (Temp rampy, Klasa B%, Padłe per partia) |
| **💰 Rozliczenia** | 4 KPI (Σ 90d, Σ życie, max/min cena) + wykres cen + tabela miesięczna |

### Eksport CSV

- **BtnEksport** — sekcjonowany (Profil / Stat 90d / Stat życie / Liczby zbiorcze).
- **BtnEksportPartii** — szeroki (40+ kolumn per partia: LP, Partia, Data, Wiek, NettoSkup, Wydajność, CH, NW, ZM, Konfiskaty, Padłe, Cena, Wartość).

### Metody danych
`LoadProfilAsync` (Dostawcy) · `LoadHistoriaPartiiAsync` (listapartii + FarmerCalc + In0E + Out1A + vw_QC_Podsum + TemperaturyMiejsca) · `LoadHarmonogramAsync` (po NAME) · `LoadFermyAsync` (DostawcyAdresy Kind=2) · `LoadRankingAsync`.

---

## 5. HodowcaWizardWindow — 5-krokowy kreator

| Krok | Kolor | Pola |
|---|---|---|
| **1. Dane i adres** | #3B82F6 | ID, Nazwa*, Adres, Kod*, Miasto*, Województwo, Distance KM, flagi (Dostawca/Odbiorca/Rolnik/Skupowy) |
| **2. Cennik i firma** | #22C55E | Typ ceny, Dodatek, Ubytek, IncDeadConf, NIP, REGON, PESEL |
| **3. ARiMR i uwagi** | #F59E0B | AnimNo, IRZPlus, Dowód osobisty+data+wydawca, Typ osobowości, Info 1-3 |
| **4. Adresy ferm** | #A78BFA | DataGrid ferm (Kind=2): Name, Address, Zip, City, AnimNo, IRZPlus, delete |
| **5. Podsumowanie** | — | przegląd + Zapisz |

### Walidacje
- **NIP**: 10 cyfr, mod 11 (wagi 1,3,7,9,1,3,7,9,1).
- **Kod**: regex `^\d{2}-\d{3}$`, auto-mapuje województwo z prefixu.
- **AnimNo**: 12 cyfr.
- **Distance KM**: auto-calc z KodyPocztowe.

### Pola krytyczne (edit mode)
Amber lewa ramka. Zmiana → DetectChanges() → log do AdminChangeRequests. Lista: Name, Nip, Address, PostalCode, City, ProvinceID, PriceTypeID, Addition, Loss, Halt, Regon, Pesel, AnimNo, IRZPlus, IDCard*, IncDeadConf.

### Mapowanie województw (prefix kodu)
00-09=Mazowieckie · 10-14=Warmińsko-Mazurskie · 15-19=Podlaskie · 20-24=Lubelskie · 25-29=Świętokrzyskie · ... · 90-99=Łódzkie.

---

## 6. HodowcyDuplicateWindow — Union-Find

### Algorytm
1. Normalizacja telefonów (split `,;/`, extract 9-cyfrowe, prefix 48).
2. Union-Find forest (parent[id]=id).
3. Matchowanie:
   - **Telefon** (checkbox): ten sam numer → Union.
   - **Nazwa** (checkbox): równa lub substring (len≥4) → Union.
4. Grupowanie: group.Count ≥ 2 → wynik.
5. Ignorowanie: tabela `Pozyskiwanie_DuplicateIgnore`.

### 3 akcje na grupę
- **IGNORUJ** → INSERT DuplicateIgnore, usuń z wyników.
- **MERGE** → keep = min ID, reszta scala się: UPDATE `Pozyskiwanie_Aktywnosci SET HodowcaId=keep`, fill Tel2/Tel3, soft-delete reszty (Aktywny=0), log "Połączono z duplikatem".
- **DELETE** → ostrzeżenie (rzadko).

DataGrid: Grupa | Id | Dostawca | Telefon | Miejscowość | Status | Towar | MatchReason.

---

## 7. HodowcyMapaWindow — WebView2 + Leaflet

### Dane
- `KodyPocztowe` tabela: Kod | Latitude | Longitude.
- GPS per kod pocztowy.

### Pinezki
- Kolor per status (Zdaje=zielony, Nie zaint.=czerwony, Do zadzwonienia=żółty).
- Klik → popup (Nazwa, Miejscowość, Ulica, Towar, Status, Telefon).

### Filtry mapy
- Status (7 opcji), Towar, Search (live LIKE).
- Sidebar: KPI (Na mapie X, Zdaje Y, Nie zaint. Z) + lista kart (200 max) clickable → focusMarker.

### Trasa transport
Jeśli pole `Trasa` wypełnione → render route między pinezkami.

### Inicjalizacja
WebView2 → load local HTML (`%TEMP%/mapa_hodowcow_wv2.html`), Leaflet + OpenStreetMap (free), message channel JS→WPF (select marker → ScrollToHodowca).

---

## 8. HistoriaHodowcyWindowPremium — 3-kolumny

- Kolumna 1: lista dnia (Notatka/Status/Przypisanie).
- Kolumna 2: chart porównawczy weekly (słupki handlowcy).
- Kolumna 3: lista ferm/partii.

Detekcja anomalii (userId=11111): cykl A→B→A→B (n≥4), >4 zmiany statusu/tydzień, ratio statuses/unique hodowcy >3.

---

## 9. Workflow pipeline + aktywności

### Przejścia (elastyczne, user decyduje via PPM)
```
Nowy → Do zadzwonienia → Próba kontaktu → Nawiązano →
  ├─ Zdaje (aktywny)
  ├─ Nie zainteresowany
  └─ Obcy kontrakt
```

### Pozyskiwanie_Aktywnosci — co rejestrujemy

| Typ | Co |
|---|---|
| 📞 Telefon | kto, kiedy, czy odebrano, treść |
| ✉ Mail | temat, treść |
| 👁️ Wizyta | kto, kiedy, notatka |
| 🔄 Zmiana statusu | StatusPrzed → StatusPo |
| 📝 Notatka | wolny tekst |
| 👥 Przypisanie | zmiana handlowca |

Schema: `HodowcaId | UzytkownikId | UzytkownikNazwa | TypAktywnosci | Tresc | DataUtworzenia | StatusPrzed | StatusPo`.

---

## 10. Import z Excela

Bulkowe importy (np. "Baza Hodowców Asia 2.xlsx" = źródło 1874 hodowców). Wiersze z null Towar → auto KURCZAKI przy starcie. UzytkownikId="IMPORT" w aktywnościach.

---

## 11. Powiązania z modułami

```
Baza Hodowców ⟷ Kalendarz dostaw (05) ⟷ Umowy (07)
       ↓
  HarmonogramDostaw → Cykl wstawienia (01-02) → Partia (03) → Sprzedaż → Reklamacja (04)
                                                                              ↓
                                                          attribution wstecz do hodowcy
```

Metryki Karty 360° ciągnięte z: listapartii, FarmerCalc, In0E, HarmonogramDostaw, Out1A.

---

## 12. Skróty + ukryte

### Skróty
F5 (refresh) · Esc (zamknij) · Ctrl+N (nowy) · Ctrl+E (edytuj z karty) · Ctrl+F (szukaj) · Alt+→/← (kroki wizarda) · Ctrl+S (zapisz wizard).

### Ukryte
1. Avatar cache `_avatarCache` (kolor z hash).
2. Double-click avatar handlowca → ranking modal.
3. Edit mode: amber border na critical fields.
4. Auto-fix startup (Towar null → KURCZAKI).
5. RenderMap → temp HTML local (nie remote).
6. HarmonogramDostaw po NAME (case-insensitive LTRIM UPPER).
7. Anomalie auto-detekcja (extrema).
8. Mapowanie województw 16 woj. × 100 prefixów.

---

## 13. Typowy dzień Mai

```
08:00  Filtr: KURCZAKI, "Do zadzwonienia". Sort KM. 23 do dziś.
08:05  PPM Janusz (12 km) → 📞 Zadzwoń. Nie odebrał → "Próba kontaktu".
08:10  PPM Krzysiek (14 km) → Zadzwoń. Chętny → "Nawiązano kontakt". Notatka "25k ptaków, cykl 7 tyg".
08:20  8 telefonów: 3 nawiązane, 4 prób, 1 odmowa.
09:00  KPI: Maja 8/8 ✓.
09:10  Klik Wojtek → Karta 360° → Tab Ranking → #3/24. Tab Anomalie → OK.
09:15  Sergiusz: "pokaż Mazura". Ctrl+F → Mazur → Enter.
       Tab Przegląd → wydajność spada 76→68%. Tab Jakość → temp rampy rośnie.
       Diagnoza: problem z chłodnią u Mazura?
09:30  Mapa → promień 30 km → 142 hodowców. Plan wizyt czwartek.
10:00  ☕
```

---

## 14. FAQ

**P: Skąd 1874?** → Import Excel "Baza Hodowców Asia 2.xlsx".
**P: Hodowca czerwony?** → Duplikat. Otwórz "Duplikaty" → MERGE/IGNORUJ.
**P: Konfiskaty %?** → (CH+NW+ZM)/SztDekl. <1% zielony, >3% problem.
**P: AnimNo?** → Numer stada ARiMR (IRZplus). PLXXXXXXX.
**P: Zmiana handlowca?** → PPM → Przypisz. Zapis w aktywnościach.
**P: Top 12%/Top 5%?** → Pozycja w rankingu aktywnych. Bottom 25% = sygnał.
**P: Mapa zły punkt?** → Błędny kod pocztowy. Edytuj.
**P: Zerwać współpracę?** → Status "Nie zainteresowany" + notatka. Hodowca zostaje w bazie.

---

## 15. Co dalej

- **Kalendarz dostaw** → `05_Kalendarz_Dostaw_Zywca.md`.
- **Cykl wstawienia** → `01_Wstawienia_Kurczakow.md`.
- **Partia** → `03_Lista_Partii.md`.
- **Umowy** → `07_Umowy_i_Dokumenty.md`.
- **Hodowca Scorecard** (audyt) → `BAZA_WIEDZY/AUDYT_BROILER_SIGNALS/` NF01.

> ⚠ Nigdy nie usuwaj hodowcy. Status "Nie zainteresowany" + notatka. Historia zostaje.
