# 02 — Firma: Ubojnia Drobiu Piórkowscy — skala i struktura

## Skala (z dokumentu Zakres_Obowiazkow_MEGA, marzec 2026)

| Wskaźnik | Wartość |
|---|---|
| **Obroty roczne** | **258 mln PLN** |
| **Dzienny przerób** | **200 ton / 70 000 sztuk** |
| **Linia ubojowa** | 7 500 szt/h |
| **Klienci** | 400+ (B2B głównie) |
| **Hodowcy** | 140+ rejestrowanych (40-70 aktywnych) |
| **Flota** | 12 pojazdów + 13 kierowców |
| **Pracownicy** | 100+ (etat + agencja + Nepalczycy) |
| **Magazyny chłodnicze** | 3 mroźnie + chłodnia + szokówka |
| **Spread żywiec→produkt** | 2.50 PLN/kg (cel: zwiększyć) |

**Założenie firmy:** 1996 przez Jerzego Piórkowskiego (dziadek Sergiusza).

**Lokalizacja główna:** Koziołki 40, 95-061 Dmosin, gmina Brzeziny, woj. łódzkie (51.9148, 19.8089).

**Druga lokalizacja:** Zgierz (masarnia Marcina Piórkowskiego — wspólnik).

---

## Struktura organizacyjna (z procedur 03-08, marzec 2026)

**Tylko jedna droga wydawania poleceń. Handlowcy NIE wydają poleceń operacyjnych. Wszystko przez Dyrektora Zakładu.**

```
                Zarząd (Sergiusz + Marcin)
                          │
                Dyrektor Zakładu  ← Justyna Chrostowska (oczekiwania > realizacja)
                          │
        ┌─────────────────┼──────────────────┬─────────────┬───────────────┐
        │                 │                  │             │               │
  Kierownik Uboju   Kierownik Rozbioru  Kierownik II  Kierownik       Koordynator
  (Łukasz Collins)  (Czysta strefa)     Zmiany        Magazynu        Logistyki
  zmiana A 5-13     6-14, cel filety    14-21         (świeże + WZ)   (transport)
                    ~30%
        │                 │                  │             │
  brudna strefa     czysta + krojenie   sprzątanie    Magazynier 1
  (~3:30 start)                         + załadunki   Magazynier 2
                                        popołud.

           Specjalista ds. Jakości    Kierownik Mroźni    Fakturzystki    Myjka
           (Justyna, limit 1000 zł)   (Janek Matusiak)    (Teresa, ...)   pojemników
                  │
           Asystent ds. Jakości
           (docelowo Klaudia Osińska?)

           Sprzedaż (handlowcy) ← podlega Zarządowi (NIE dyrektorowi zakładu)
           Jola, Maja, Radek, Teresa, Ania, Paulina (zakupy)
```

**Krytyczny wakat:** Dyrektor Zakładu — Justyna pełni rolę Specjalisty ds. Jakości, ale Sergiusz oczekiwał że "wejdzie wyżej" w operacje. Pain point: "Justyna nie chodzi po hali wystarczająco, zerka tylko w kamery".

---

## Magazyny w HANDEL (Symfonia)

| Symbol | Nazwa | Co tam idzie |
|---|---|---|
| **65554** | Świeże po uboju | sPWU, PWP, RWP, sPZ żywca |
| **65556** | Wydania | sWZ, sWZ-W, sWZK |
| **65552** | Drugi magazyn produkcji | (rzadziej używany) |
| **65547** | Paczkowane | sPPK |
| **65562** | Mrożonki / półprodukty | sPPM |
| **65559** | Magazyn pomocniczy | różne |

**Workflow towaru:**
1. **65554** ← przyjęcie z uboju (sPWU)
2. **65554** → krojenie → wraca jako elementy (RWP+PWP)
3. **65554** → wydanie do klienta (sWZ przez 65556)
4. **65554** → mrożenie → **65562** (jeśli się nie sprzedało)
5. **65562** → po kilku miesiącach taniej do klienta (sWZ z 65556)

---

## Pasze i hodowla (cykl 35-42 dni)

**Dostawcy paszy:** TASOMIX, De Heus, Ekoplon
**Rodzaje:** Brojler ALFA, Grower 1/2, Finiszer, Starter (kategoria HANDEL: 65883, jednostka: tona)
**Sergiusz dostarcza paszę hodowcom kontraktowym** (proporcja 50/50 kontrakt vs wolny rynek).

**Cykl hodowli:**
- **0 dzień** — pisklęta wstawiane (np. 1 stycznia 2026)
- **35 dzień** — ubój **20% wstawionych sztuk** (rozluźnienie kurnika — kurczaki muszą mieć dostęp do wodopoju i paszy)
- **42 dzień** — ubój **reszty** (pełna dostawa)

**Numer partii w ZPSP:**
```
[3 cyfry: CustomerID hodowcy] + [8 cyfr: RR-DDD-AAA]
                                 RR  = rok (2 cyfry)
                                 DDD = dzień w roku (3 cyfry, 001-366)
                                 AAA = numer auta od tego hodowcy w danym dniu (3 cyfry)
```
**Przykład:** `390-26119004` = hodowca CustomerID 390 (Szymczak Dariusz), rok 2026, 119-ty dzień roku (29 kwietnia), 4-te auto.

Pełniejszy opis: `BAZA_WIEDZY/18_Analiza_przychodu_szczegoly.md`.

---

## Kluczowe liczby finansowe

| Wskaźnik | Wartość |
|---|---|
| **Cena żywca** | 4.00 zł/kg wolny rynek, 4.40 rolnicza, 5.23 ministerialna |
| **Cena tuszki** | 7.30-8.50 zł/kg (Symfonia 2026), rynkowa 7.10-7.55 |
| **Spread żywiec→produkt** | 2.50 PLN/kg (cel: zwiększyć) |
| **Strata mrożenia** | -18% wartości (mrożone tańsze) |
| **Norma straty mroźni** | ≤2% wagi (in→out) |
| **Stawka km kierowcy** | 0.69 zł (od maja 2026, łączy delegacje) |
| **Paliwo** | 3.71 zł/km = 70% kosztu kursu |
| **Kryzys luty 2026** | spadek z 25M do 15M/mies, straty ~2M |

---

## Kluczowe systemy używane w firmie

| System | Funkcja | API/dostęp |
|---|---|---|
| **ZPSP** (Kalendarz1) | Autorski system Sergiusza, łączy 4 bazy | Pełny |
| **Sage Symfonia Handel** | ERP — faktury, magazyny, kontrahenci | SQL (HANDEL DB) |
| **Symfonia Production** | Kupiony, nigdy nie wdrożony | Brak (87 tabel pustych) |
| **WAGO selektywna** | Klasyfikacja A/B + klasy wagowe (6/7/8/9/10/11) | **BRAK** (Sergiusz zabiega o dostęp) |
| **RADWAG** | Wagi platformowe + paletowe | **BRAK API** |
| **AVILOG** | Planowanie odbioru żywca od hodowców | Wewnętrzny do firmy |
| **UNICARD** | RCP — godziny pracowników | SQL (UNISYSTEM DB) |
| **WebFleet** | Tracking floty (TomTom) | API tak |
| **Hikvision** | Kamery przemysłowe (Justyna sprawdza) | RTSP |
| **Fireflies** | Nagrywanie spotkań + transkrypcje | MCP integration |
| **Power BI** | Analizy post-factum (.pbix) | Lokalne pliki |

---

## Lokalizacje fizyczne firmy (Koziołki)

```
Hala produkcyjna:
├── Brudna strefa (Łukasz Collins, ubój + patroszenie)
├── Chłodzenie (między brudną a czystą)
├── Czysta strefa (klasyfikacja A/B + rozbiór + filet)
├── Pomieszczenie krojenia (waga platformowa + etykiety)
├── Magazyn dystrybucji (świeże tuszki + elementy, FIFO)
├── Wydawka (2 rampy załadunkowe)
└── Mroźnie (3 komory + szokówka)
```

**Bez regałów** w magazynie świeżych — palety stoją bezpośrednio. Pracownicy znają układ na pamięć. Sergiusz nie zna dokładnej liczby palet jakie się mieszczą (do sprawdzenia).

**Wydawka** — 2 rampy załadunkowe, magazynier z magazynu dystrybucji przesuwa towar na wydawkę, a stamtąd ładowane do auta.

---

## Cele 2026

| Cel | Status |
|---|---|
| **IFS / BRC certyfikacja** | W przygotowaniu |
| **Eksport bezpośredni** (omijanie pośredników) | Strategia |
| **Patroszarka Meyn Maestro** | IX 2026 (grant ARiMR) — zastąpi Meyn Mountaineer 2015 |
| **Magazyn energii 250 tys.** | Oszczędność 180 tys./rok |
| **Fotowoltaika 150 kW** | W planie |
| **Microsoft 365 + Teams** | Migracja z WhatsApp |
| **Klaudia Osińska** → asystent ds. jakości (formalnie) | W planie |
| **Teresa Jachymczak** → dyrektor handlowy | Awans w planie |
