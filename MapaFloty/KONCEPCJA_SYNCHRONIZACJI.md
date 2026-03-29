# Koncepcja synchronizacji systemu transportowego z Webfleet

## Spis treści
1. [Cel biznesowy](#cel)
2. [Co mamy teraz](#teraz)
3. [Problem z Orders API](#problem-orders)
4. [Trzy drogi do wyboru](#drogi)
5. [DROGA A — Bez Orders, czyste GPS](#droga-a)
6. [DROGA B — Orders jako punkty kontrolne](#droga-b)
7. [DROGA C — Pełna integracja Orders + GPS](#droga-c)
8. [Porównanie dróg](#porownanie)
9. [Moja rekomendacja](#rekomendacja)
10. [Harmonogram wdrożenia](#harmonogram)

---

## 1. Cel biznesowy <a name="cel"></a>

Logistyk tworzy **Kurs** (trasę) w systemie transportowym. Kurs zawiera **Ładunki** (zamówienia klientów) w określonej **kolejności** — kierowca ma objechać te punkty po kolei.

**Czego potrzebujesz:**
- Czy kierowca jedzie zgodnie z planem (kolejność przystanków)?
- Czy pojechał gdzieś poza trasę?
- Który przystanek jest już obsłużony, a który nie?
- Jak daleko jest do następnego klienta?
- Czy się spóźni?
- Historia: jak wyglądał przejazd vs plan?
- Kontrola w czasie rzeczywistym i historycznie

---

## 2. Co mamy teraz <a name="teraz"></a>

### System transportowy (TransportPL):
- `Kurs` — trasa na dany dzień, pojazd, kierowca, godzina wyjazdu/powrotu
- `Ladunek` — zamówienia klientów na kursie, w kolejności (`Kolejnosc`)
- `KodKlienta` = `ZAM_xxxx` — referencja do zamówienia w LibraNet

### Webfleet GPS:
- Pozycja pojazdów co ~30 sekund (`showObjectReportExtern`)
- Historia tras GPS (`showTracks`) — dokładna trasa z prędkościami
- **Orders API** — zlecenia z adresami i statusami

### Adresy klientów:
- Symfonia (Handel DB) → `STContractors` + `STPostOfficeAddresses`
- GPS z `KartotekaOdbiorcyDane` lub auto-geokodowanie Nominatim
- Cache w `KlientAdres` (TransportPL)

### Mapowania:
- `WebfleetVehicleMapping` — pojazd systemu ↔ pojazd GPS
- `WebfleetDriverMapping` — kierowca systemu ↔ kierowca GPS

---

## 3. Problem z Orders API <a name="problem-orders"></a>

**Błąd 2605** — `sendDestinationOrderExtern` wymaga terminala PRO (ekran TomTom w kabinie). Wasze pojazdy mają **LINK** (tracker GPS bez ekranu).

**`insertDestinationOrderExtern`** — tworzy zlecenie w systemie Webfleet bez wysyłania na urządzenie. ALE:
- Bez terminala PRO kierowca **nie potwierdza** przyjęcia/realizacji
- Statusy zleceń (zaakceptowane, dotarł, zakończone) **nie aktualizują się automatycznie**
- ETA jest obliczane tylko gdy zlecenie jest **przypisane do pojazdu z aktywnym GPS**
- Orders API to narzędzie do komunikacji z kierowcą — bez ekranu traci sens

**Wniosek:** Orders API ma ograniczoną wartość bez terminali PRO. Statusy trzeba zarządzać ręcznie lub automatycznie na podstawie GPS.

---

## 4. Trzy drogi do wyboru <a name="drogi"></a>

| | DROGA A | DROGA B | DROGA C |
|---|---------|---------|---------|
| **Nazwa** | Czyste GPS | Orders jako punkty kontrolne | Pełna integracja |
| **Orders API** | Nie używamy | Tylko insert (monitoring) | Insert + assign + statusy |
| **Śledzenie postępu** | Nasz system oblicza z GPS | Mieszane: GPS + Orders | Webfleet zarządza statusami |
| **Złożoność** | Średnia | Średnia-wysoka | Wysoka |
| **Wymaga PRO terminal** | NIE | NIE | TAK (dla pełnych statusów) |
| **Kontrola** | Pełna w naszym systemie | Częściowa | Webfleet ma kontrolę |

---

## 5. DROGA A — Bez Orders, czyste GPS <a name="droga-a"></a>

### Idea
Nie używamy Orders API w ogóle. Zamiast tego nasz system sam analizuje trasę GPS pojazdu i porównuje z planem kursu (kolejność przystanków).

### Jak to działa:

#### Widok "Monitor kursu" (nowe okno):
1. Logistyk wybiera kurs z dzisiaj
2. System pokazuje:
   - **Mapę** z zaplanowaną trasą (linia łącząca przystanki wg kolejności)
   - **Aktualną pozycję** pojazdu (z Webfleet GPS, odświeżanie co 30s)
   - **Przystanki** jako punkty z numerami
   - **Status każdego przystanku**: oczekujący / w drodze / dotarł / obsłużony

#### Automatyczna detekcja statusu przystanku:
```
Dla każdego przystanku (klient z adresem GPS):
  - Oblicz odległość pojazdu od przystanku
  - Jeśli < 500m i prędkość < 5 km/h → "DOTARŁ"
  - Jeśli wcześniej "DOTARŁ" i teraz > 1km → "OBSŁUŻONY" (odjechał)
  - Jeśli nigdy < 500m → "POMINIĘTY" (pojechał dalej)
```

#### Co widzi logistyk:
- Przystanek 1: ✅ Obsłużony (08:15 - 08:32, 17 min)
- Przystanek 2: ✅ Obsłużony (09:05 - 09:18, 13 min)
- Przystanek 3: 🚛 W drodze (ETA ~15 min, 12.3 km)
- Przystanek 4: ⏳ Oczekujący
- Przystanek 5: ⏳ Oczekujący

#### Wykrywanie odchyleń:
- Pojazd jedzie w kierunku innym niż następny przystanek → ostrzeżenie
- Pojazd stoi w miejscu nie będącym przystankiem > 15 min → alert "nieplanowany postój"
- Pojazd pominął przystanek (był daleko i pojechał do następnego) → "POMINIĘTY" na czerwono

#### Historycznie:
- Tabela `KursRealizacja` w TransportPL:
  - KursID, LadunekID, KodKlienta
  - CzasDotarcia, CzasOdjazdu, CzasPostoju (min)
  - OdlegloscOdPunktu (m) — jak blisko pojechał
  - Status: Obsłużony / Pominięty / NieObjechany
  - Kolejnosc planowana vs faktyczna
- Wypełniana automatycznie z analizy GPS po zakończeniu kursu

### Zalety:
- ✅ Nie wymaga Orders API ani terminali PRO
- ✅ Pełna kontrola w naszym systemie
- ✅ Działa z każdym trackerem GPS (LINK, OBD, cokolwiek)
- ✅ Historyczna analiza: plan vs realizacja
- ✅ Wykrywanie odchyleń i nieplanowanych postojów

### Wady:
- ❌ Kierowca nie widzi listy przystanków na ekranie (musi mieć kartkę/telefon)
- ❌ Trzeba samemu zbudować logikę detekcji (GPS proximity)
- ❌ Detekcja "dotarł" na podstawie 500m promienia — nie 100% precyzyjna

---

## 6. DROGA B — Orders jako punkty kontrolne <a name="droga-b"></a>

### Idea
Używamy `insertDestinationOrderExtern` do wstawienia zleceń do Webfleet jako "punkty kontrolne" do monitorowania. Nasz system nadal analizuje GPS, ale zlecenia Webfleet dają dodatkowy kontekst w panelu webowym Webfleet.

### Jak to działa:

#### Przy tworzeniu kursu:
1. Logistyk klika "Wyślij do Webfleet"
2. Dla każdego ładunku tworzymy osobne zlecenie (`insertDestinationOrderExtern`):
   - OrderID: `K{KursID}_S{Kolejnosc}` (np. K1348_S1, K1348_S2)
   - Typ: delivery (3)
   - Współrzędne klienta
   - Tekst: nazwa klienta, ilość E2, uwagi
3. Zlecenia są widoczne w panelu webowym Webfleet

#### Monitoring (nasz system):
- Taki sam jak DROGA A (analiza GPS, proximity detection)
- PLUS: w panelu Webfleet widzisz zlecenia i ich status
- PLUS: jeśli kiedyś kupicie terminale PRO, kierowcy zobaczą zlecenia

#### Przy zmianie kursu:
- `updateDestinationOrderExtern` aktualizuje zlecenia
- Usunięty ładunek → `cancelOrderExtern`
- Dodany ładunek → nowy `insertDestinationOrderExtern`

#### Historycznie:
- `showOrderReportExtern` + nasza tabela `KursRealizacja`
- Porównanie: zlecenie Webfleet vs GPS reality

### Zalety:
- ✅ Zlecenia widoczne w panelu webowym Webfleet
- ✅ Gotowe na przyszłość (terminale PRO)
- ✅ Nasza analiza GPS jako backup
- ✅ Dual source: Webfleet + nasz system

### Wady:
- ❌ Statusy zleceń nie aktualizują się automatycznie (brak PRO)
- ❌ Trzeba ręcznie zamykać zlecenia lub automatycznie z GPS
- ❌ Więcej złożoności (dwa systemy do zarządzania)
- ❌ Rate limity Webfleet API (6 req/min na showOrderReport)

---

## 7. DROGA C — Pełna integracja Orders + GPS <a name="droga-c"></a>

### Idea
Kupujecie terminale TomTom PRO (lub wymieniacie LINK na PRO). Kierowca widzi zlecenia na ekranie, potwierdza przyjęcie, nawigację, dotarcie. Pełna dwukierunkowa synchronizacja.

### Jak to działa:
- `sendDestinationOrderExtern` wysyła zlecenie na terminal
- Kierowca potwierdza: zaakceptowane → w drodze → dotarł → zakończone
- Webfleet oblicza ETA w czasie rzeczywistym
- `showOrderReportExtern` zwraca pełne statusy
- Nasz system odpytuje co 60s i aktualizuje `KursRealizacja`

### Zalety:
- ✅ Kierowca widzi listę klientów + nawigację
- ✅ Automatyczne statusy (potwierdzone przez kierowcę)
- ✅ Precyzyjne ETA z TomTom Traffic
- ✅ Dowody dostawy (ePOD)
- ✅ Profesjonalne rozwiązanie fleet management

### Wady:
- ❌ **Koszt** — terminale PRO ~1000-2000 PLN/szt × 12 pojazdów
- ❌ **Abonament** Webfleet PRO wyższy niż LINK
- ❌ Zależność od Webfleet (vendor lock-in)
- ❌ Kierowcy muszą się przyzwyczaić do obsługi terminala

---

## 8. Porównanie dróg <a name="porownanie"></a>

| Kryterium | DROGA A | DROGA B | DROGA C |
|-----------|:-------:|:-------:|:-------:|
| **Koszt wdrożenia** | 0 zł | 0 zł | 12-24k zł (terminale) |
| **Czas realizacji** | 2-3 dni | 3-4 dni | 1-2 tyg + zakup |
| **Czy kierowca widzi trasę** | Nie | Nie | Tak |
| **Auto-detekcja "dotarł"** | GPS (500m) | GPS (500m) | Kierowca potwierdza |
| **ETA** | Nasz system (dystans/prędkość) | Nasz + Webfleet | Webfleet Traffic |
| **Wykrywanie odchyleń** | Tak (GPS) | Tak (GPS) | Tak (GPS + Orders) |
| **Historia plan vs realizacja** | Tak | Tak | Tak |
| **Niezależność od Webfleet** | Pełna | Częściowa | Niska |
| **Gotowość na przyszłość** | Średnia | Wysoka | Pełna |
| **Precyzja detekcji** | ~90% | ~90% | ~99% |

---

## 9. Moja rekomendacja <a name="rekomendacja"></a>

### **DROGA A** — najpierw, od razu

**Powód:** Daje Ci 90% tego czego potrzebujesz, zero kosztów, działa z obecnym sprzętem (LINK). Można zrobić w 2-3 dni.

**Co dokładnie zbuduję:**

1. **Nowe okno "Monitor Kursów"** (dostępne z Mapy Floty i z Planowania Transportu):
   - Lewa strona: lista dzisiejszych kursów z podsumowaniem statusu
   - Prawa strona: mapa z trasą zaplanowaną + pozycja pojazdu live
   - Pod mapą: lista przystanków ze statusami w czasie rzeczywistym

2. **Tabela `KursRealizacja`** w TransportPL:
   - Automatycznie wypełniana co 30s na podstawie GPS
   - Dla każdego ładunku: czas dotarcia, czas odjazdu, status
   - Kolejność faktyczna vs planowana

3. **Automatyczna detekcja statusu** (proximity algorithm):
   - Pojazd < 500m od klienta i prędkość < 5 → "DOTARŁ" + timestamp
   - Pojazd odjechał > 1km → "OBSŁUŻONY" + timestamp
   - Pojazd pominął (następny przystanek bliżej) → "POMINIĘTY"
   - Nieplanowany postój > 15 min → alert

4. **Panel "Jak jechał"** (historia):
   - Mapa z trasą GPS + zaplanowane przystanki
   - Timeline: kiedy dotarł do każdego klienta, ile stał
   - Odchylenia: gdzie jechał poza plan
   - Ocena: % przystanków obsłużonych w kolejności

5. **Dashboard logistyka**:
   - Wszystkie dzisiejsze kursy na jednym ekranie
   - Kolory: zielony=w normie, żółty=opóźniony, czerwony=problem
   - Klik na kurs → pełny podgląd mapy

### Później (opcjonalnie): **DROGA B** jako rozszerzenie
Gdy DROGA A działa i się sprawdza, możemy dodać Orders API jako "bonus" — zlecenia w Webfleet dla dodatkowej widoczności. Ale to nie jest konieczne.

### W przyszłości: **DROGA C** przy zakupie terminali PRO
Jeśli kiedyś zdecydujesz się na terminale PRO, kod z DROGI A + B jest gotowy do rozbudowy.

---

## 10. Harmonogram wdrożenia <a name="harmonogram"></a>

### Faza 1 (DROGA A) — Monitor kursów:
- Tabela `KursRealizacja` + serwis proximity detection
- Okno "Monitor Kursów" z mapą live
- Auto-detekcja statusów przystanków
- Integracja z istniejącą Mapą Floty

### Faza 2 — Historia i raporty:
- Panel "Jak jechał" z porównaniem plan vs GPS
- Timeline przystanków
- Raport PDF: realizacja kursu
- Dashboard wszystkich kursów

### Faza 3 (opcjonalna DROGA B) — Orders Webfleet:
- Insert zleceń do Webfleet
- Synchronizacja zmian
- Status z Webfleet + nasz GPS

---

**Która drogę wybierasz? A, B, czy C?**
