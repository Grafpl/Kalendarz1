# 17 — Słownik skrótów i pojęć

## Skróty firmowe

| Skrót | Znaczenie | Kontekst |
|---|---|---|
| **ZPSP** | "Zajebisty Program Sergiusza Piórkowskiego" = `Kalendarz1.csproj` | Autorski system Sergiusza |
| **PZ** | Przyjęcie zewnętrzne | Kupowanie żywca, paszy z zewnątrz |
| **WZ** | Wydanie zewnętrzne | Wydanie towaru do klienta |
| **RW** | Rozchód wewnętrzny | Przesunięcie między magazynami |
| **PW** | Przyjęcie wewnętrzne | Przyjęcie z produkcji |
| **MM** | Przesunięcie międzymagazynowe | Wewnętrzny ruch |

---

## Series dokumentów Symfonia / ZPSP

> Pełna mapa z opisami i magazynami → `BAZA_WIEDZY/23_HANDEL_Schema_Sage_Symfonia.md` sekcja 3.

### PRZYCHODY (zwiększają stan)
| Series | Pełna nazwa | Co znaczy | Magazyn |
|---|---|---|---|
| **sPZ / PZ** | Przyjęcie zewnętrzne | Zakup żywca / paszy od dostawcy | 65550 / 65556 |
| **sPZK / PZK** | Przyjęcie korekta | Korekta PZ | dowolny |
| **PZH** | Przyjęcie z handlu | Zwrot od klienta | 65556 |
| **sPWU / PWU** | Przyjęcie wewn. ubojnia | Tuszki + podroby z linii uboju | **65555 (M.UBOJ)** |
| **sPWP / PWP** | Przychód wewn. produkcja | Elementy po krojeniu | **65554 (M.PROD)** |
| **sPPM / PPM** | Przychód masarnia | Wędliny | 65562 (M.MASAR) |
| **sPPK / PPK** | Przychód karma | Karma dla zwierząt | 65547 (KARMA) |
| **sPKM / PKM** | Korekta magazynowa | Inwentarz | dowolny |
| **sMM+ / MM+** | Przesunięcie międzymag. + | Wpływ z innego magazynu | docelowy |
| **sMP / MP** | Przyjęcie opakowań | Folie, taśmy, etykiety | 65559 |

### ROZCHODY (zmniejszają stan)
| Series | Pełna nazwa | Co znaczy | Magazyn |
|---|---|---|---|
| **sWZ / WZ** | Wydanie zewnętrzne | Sprzedaż klientowi | 65556 (M.DYST) |
| **sWZ-W / WZ-W** | Wydanie wewnętrzne | Pracownik / wewn. zużycie | dowolny |
| **sWZK / WZK** | Wydanie korekta | Zwrot towaru | dowolny |
| **sRWU / RWU** | Rozchód wewn. ubój | Żywiec → ubój | 65555 |
| **sRWP / RWP** | Rozchód wewn. produkcja | Tuszka → krojenie | 65554 |
| **sRPM / RPM** | Rozchód wewn. masarnia | Surowiec do wędlin | 65562 |
| **sRPK / RPK** | Rozchód produkcja karmy | Składniki do karmy | 65547 |
| **sMM- / MM-** | Przesunięcie międzymag. − | Wyjście do innego magazynu | źródłowy |
| **sMW / MW** | Wydanie opakowań | Folie do produkcji | 65559 |

### Faktury
| Series | Pełna nazwa | Co znaczy |
|---|---|---|
| **FVS** | Faktura sprzedaży | Standardowa |
| **FKS** | Faktura korygująca | Korekta (zwrot/cena) |
| **FKSB** | Faktura korygująca B | Wariant B |
| **FWK** | Faktura wewn. korygująca | Korekta wewnętrzna |

**Reguła**: literka `s` na początku = nowa generacja Symfonii (po 2021), bez `s` = stara. Funkcjonalnie identyczne.

**Zasada FKS/FKSB/FWK**: auto-importują się do ZPSP jako reklamacje (= 75% rekordów reklamacji).

---

## Magazyny HANDEL (Symfonia) — real names z 2026-05-09

> ⚠️ **Wcześniejsze mapowanie było mylne**. Pełna lista + flow → `24_Magazyny_i_Lancuch_Produkcji.md`.

### Łańcuch produkcji
| ID | Skrót | Nazwa pełna |
|---:|---|---|
| **65555** | M. UBOJ | Magazyn ubojni (sPWU) |
| **65554** | M. PROD | Magazyn produkcji / krojenia |
| **65556** | M. DYST | Magazyn dystrybucji (sWZ) |

### Odgałęzienia z PRODUKCJI
| ID | Skrót | Nazwa |
|---:|---|---|
| **65552** | M. MROŹ | Mroźnia |
| **65562** | M. MASAR | Masarnia |
| **65547** | KARMA | Magazyn produkcji karmy |
| **65551** | M. ODPA | Magazyn odpadów |
| **65564** | M. ROZCH | Magazyn rozchodu |

### Pomocnicze
| ID | Skrót | Nazwa |
|---:|---|---|
| **65559** | Mag. opak. | Magazyn opakowań |
| **65550** | Mag. faktur | Magazyn faktur (sPZ od hodowców) |
| **65543** | Mag. 65543 | TASOMIX-specific |
| **65566** | Mag. 65566 | Samol/Ekoplon |

### Kategorie towarów (NIE magazyny — `TW.katalog`)
| ID | Co |
|---:|---|
| 65882 | Żywiec (kurczak żywy 7-12) |
| 67094 | Odpady |
| 67095 | Mięso (świeże) — Tuszka A/B, podroby, filet, korpus |
| 67104 | Mięso (inne) |
| 67153 | Mrożone |
| 65883 | Pasze |

---

## Stref i pomieszczeń

| Skrót | Znaczenie |
|---|---|
| **Brudna** | Strefa uboju + patroszenia (Łukasz Collins) |
| **Czysta** | Strefa po chłodzeniu (klasyfikacja A/B + rozbiór) |
| **Hala** | Cały budynek produkcyjny (brudna + czysta + magazyn dystrybucji + wydawka) |
| **Wydawka** | 2 rampy załadunkowe |
| **Magazyn dystrybucji** | 65554, palety na podłodze (bez regałów) |
| **Szokówka** | Komora szybkiego zamrażania (24h przed mroźnią) |
| **Mroźnia** | 3 komory długoterminowe (-18°C, cel -20°C) |
| **Chłodnia** | +2°C do +4°C (dla świeżego towaru) |

---

## Klasy wagowe (WAGO selektywna)

**Definicja:** Klasa wagowa = **liczba sztuk w pojemniku 15 kg netto**. **Mniejszy numer = większy ptak.**

| Klasa | Sztuk w 15kg | Ciężar 1 sztuki | Komentarz |
|---|---|---|---|
| **5** | 5 sztuk | ~3.00 kg | Bardzo duży |
| **6** | 6 sztuk | ~2.50 kg | **Idealna** |
| **7** | 7 sztuk | ~2.14 kg | **Idealna** |
| **8** | 8 sztuk | ~1.875 kg | Średni |
| **9** | 9 sztuk | ~1.67 kg | Średni |
| **10** | 10 sztuk | ~1.50 kg | Mniejszy |
| **11** | 11 sztuk | ~1.36 kg | Mały |
| **12** | 12 sztuk | ~1.25 kg | Najmniejszy w użyciu |
| **0** | (brak / mix) | — | Zapomnienie operatora lub mix klas w pojemniku |

**UWAGA:** To NIE jest waga w kg. To **liczba sztuk** w pojemniku 15 kg.

**Pole DB:** `LibraNet.dbo.In0E.QntInCont` (int, tylko dla `ArticleID = '40'` = Kurczak A).

---

## Klasy A / B (klasyfikacja wzrokowa)

| Klasa | Co | Co dalej |
|---|---|---|
| **A** | Tuszka bez wad | Pojemnik 15 kg → magazyn świeżych |
| **B** | Tuszka z wadą (krwiak/złamanie/żółć/oparzenie/otwarta rana) | **Maszyna rozbierająca** → filet+korpus rozdzielone |

---

## Współczynniki uzysku (z modułu Krojenie 14A)

| Element | Uzysk z tuszki |
|---|---|
| **Filet** | **29.5%** |
| **Ćwiartka** | **33.4%** |
| **Korpus** | **22.7%** |
| **Skrzydło** | **8.7%** |
| Pozostałe | ~5.7% |

**Przelicznik żywiec → tuszka:** ~78%.

---

## Skróty kontekstu biznesowego

| Skrót | Znaczenie |
|---|---|
| **HACCP** | Hazard Analysis and Critical Control Points |
| **CCP** | Critical Control Point |
| **BRC** | British Retail Consortium (certyfikacja jakości) |
| **IFS** | International Featured Standards (certyfikacja jakości) |
| **HPAI** | Highly Pathogenic Avian Influenza (ptasia grypa) |
| **BCC** | Better Chicken Commitment (etyczne hodowle) |
| **CRM** | Customer Relationship Management |
| **CAPA** | Corrective Action Preventive Action (działania korygujące) |
| **VRP** | Vehicle Routing Problem (algorytm tras) |
| **MES** | Manufacturing Execution System |
| **WMS** | Warehouse Management System |
| **TMS** | Transport Management System |
| **RCP** | Rejestrator Czasu Pracy (UNICARD) |
| **KSeF** | Krajowy System e-Faktur |
| **IRZplus** | Identyfikacja i Rejestracja Zwierząt |
| **ARiMR** | Agencja Restrukturyzacji i Modernizacji Rolnictwa |

---

## Skróty produktowe (rozbiór)

| Skrót | Pełna nazwa |
|---|---|
| **Tuszka** | Cały kurczak po patroszeniu i chłodzeniu |
| **Filet** | Mięso piersiowe (29.5% tuszki) |
| **Polędwiczka** | Mała wewnętrzna część fileta |
| **Ćwiartka** | Udo + podudzie razem (33.4% tuszki) |
| **Korpus** | Pozostała tuszka po wyjęciu fileta i ćwiartek (22.7%) |
| **Skrzydło** | Skrzydło w 3 częściach (8.7%) |
| **Mielone** | Mielone mięso (z fileta lub ćwiartki) |
| **Tuba** | Forma sprzedaży (np. mielone w tubie) |
| **Skórki** | Odpad — skórka kurczaka (do utylizacji lub na karmę) |
| **Kości** | Odpad — szkielet (do utylizacji) |

---

## Pojemniki i opakowania

| Pojęcie | Wymiary / cechy |
|---|---|
| **E2** | Plastikowy pojemnik 15 kg netto (towar) — standard branżowy |
| **H1** | Paleta — standard 36-40 E2 / paleta |
| **Polibloк** | Karton + worek foliowy z 10 kg zamrożonego towaru |
| **Karton** | Detaliczne opakowanie |

---

## Dokumenty i referencje

| Dokument | Co |
|---|---|
| **WZ papierowa** | Magazynier wpisuje numery partii ręcznie (brak skanerów!) |
| **Etykieta** | Wydruk z wagi RADWAG (waga + data + nr partii) |
| **Awizacja** | Klient deklaruje datę + godzinę odbioru |
| **Plomba** | Zabezpieczenie auta po załadunku |

---

## Personelowe / organizacyjne

| Skrót | Znaczenie |
|---|---|
| **Zmiana A** | Główna zmiana (5:00-13:00) |
| **Zmiana B** | Druga zmiana (14:00-21:00) |
| **CTO** | Chief Technology Officer (Sergiusz pełni nieformalnie) |
| **COO** | Chief Operating Officer (Sergiusz pełni nieformalnie) |

---

## Skróty Sergiusza w komunikacji

| Skrót | Znaczenie |
|---|---|
| **"Można spróbować"** | "OK, idź do przodu" |
| **"Najwyżej poprawimy"** | "Akceptuję ryzyko, ucz się przez iterację" |
| **"Daruj sobie"** | "Zmień podejście, nie dyskutuj" |
| **"Cofnij to"** | DOSŁOWNIE — `git checkout --` na zmianach |

---

## Dane kontaktowe firmy

- **Adres:** Ubojnia Koziołki 40, 95-061 Dmosin
- **Współrzędne GPS:** 51.9148, 19.8089
- **Województwo:** Łódzkie
- **Gmina:** Brzeziny
- **Druga lokalizacja:** Zgierz (masarnia Marcina Piórkowskiego)
- **Email Sergiusza:** sergiusz.piorko@gmail.com

---

## TypCeny (LibraNet.HarmonogramDostaw) — klasyfikacja Kontrakt/Wolny

> Pełny opis w `27_WidokCenWszystkich_modul.md` §5.

| Wartość `TypCeny` | Klasyfikacja | Cena/kg | Co znaczy |
|---|---|---|---|
| `wolnyrynek` | **Wolny rynek** | ~4.00 zł | Hodowca bez umowy, cena bieżąca dnia |
| `wolnorynkowa` | **Wolny rynek** | ~4.00 zł | Alias `wolnyrynek` |
| `rolnicza` | **Kontrakt** | ~4.40 zł | Umowa kontraktowa, cena rolnicza |
| `ministerialna` | **Kontrakt** | ~5.23 zł | Umowa kontraktowa, cena ministerialna |
| `łączona` | **Kontrakt** | między | Umowa kontraktowa, cena mieszana |

**SQL klasyfikacji:**
```sql
CASE WHEN LOWER(TypCeny) IN ('wolnyrynek','wolnorynkowa')
     THEN 'Wolny' ELSE 'Kontrakt' END AS Kategoria
```

**Cel firmowy:** 50/50 kontrakt/wolny (procedury 01).

---

## Pojęcia analityczne — ceny żywca

| Pojęcie | Definicja |
|---|---|
| **Przebitka** | Marża = sprzedaż − zakup (per kg). Główne 2 serie: **Zrzeszenie − Rolnicza** (sprzedaż zrzeszeniowa vs cena rolnicza), **Nasza Tuszka − Wolnorynkowa** (nasza cena vs wolny rynek). 3. (opcjonalna): **Nasza Tuszka − Średnia wszystkich potwierdzonych**. |
| **Średnia ważona cena** | `Σ(Cena × Sztuki) / Σ(Sztuki)` — używana w module Kontrakty (per okres). |
| **Wolumen kg** | `SztukiDek × WagaDek` — przelicznik z liczby sztuk i wagi średniej na kg żywca. |
| **YoY** | Year-over-Year — porównanie tego samego okresu roku do roku. W ZPSP 4 tryby: Day/Week/Month/Quarter. |
| **Imputacja Wolnorynkowa** | Brak ceny dnia → interpolacja liniowa z dni sąsiednich. Wykres znaczy je niebieskimi rombami. |

---

## Przelicznik zł/kg (orientacyjne)

| Co | Cena |
|---|---|
| Żywiec wolny rynek | 4.00 zł/kg |
| Żywiec rolnicza | 4.40 zł/kg |
| Żywiec ministerialna | 5.23 zł/kg |
| Tuszka Symfonia 2026 | 7.30-8.50 zł/kg |
| Tuszka rynkowa | 7.10-7.55 zł/kg |
| Filet PL premium | 15-17 zł/kg |
| Filet BR (Mercosur) | ~13 zł/kg ⚠️ |
| Spread żywiec→produkt cel | 2.50 zł/kg |
| Strata mrożenia | -18% wartości |
| Stawka km kierowcy | 0.69 zł |
| Paliwo | 3.71 zł/km (70% kosztu kursu) |
