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

| Series | Pełna nazwa | Co znaczy |
|---|---|---|
| **sPZ** | Przyjęcie zewnętrzne | Zakup żywca / paszy |
| **sPWU** | Przyjęcie produkcji ubojowej | Tuszki świeże po uboju |
| **PWP** | Produkcja wewnętrzna przyjęcie | Z krojenia (elementy) |
| **RWP** | Rozchód wewnętrzny produkcyjny | Tuszka → krojenie |
| **sPPK** | Przyjęcie produkcji paczkowanej | Towar paczkowany |
| **sPPM** | Przyjęcie produkcji mrożone | Mrożonki |
| **sWZ** | Wydanie zewnętrzne | Sprzedaż klientowi |
| **sWZ-W** | Wydanie zewnętrzne wewnątrzwspólnotowe | Eksport UE |
| **sWZK** | Wydanie zewnętrzne korygujące | Zwrot towaru |
| **FVS** | Faktura sprzedaży | Standardowa |
| **FKS** | Faktura korygująca sprzedaży | Korekta (zwrot/cena) |
| **FKSB** | Faktura korygująca sprzedaży B | Wariant B |
| **FWK** | Faktura wewnętrzna korygująca | Korekta wewnętrzna |

**Zasada:** FKS/FKSB/FWK auto-importują się do ZPSP jako reklamacje (= 75% rekordów reklamacji).

---

## Magazyny HANDEL (Symfonia)

| Symbol | Nazwa |
|---|---|
| **65554** | Świeże po uboju |
| **65556** | Wydania |
| **65552** | Drugi magazyn produkcji |
| **65547** | Paczkowane |
| **65562** | Mrożonki / półprodukty |
| **65559** | Pomocniczy |
| **65883** | Pasze (kategoria, ton) |

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
