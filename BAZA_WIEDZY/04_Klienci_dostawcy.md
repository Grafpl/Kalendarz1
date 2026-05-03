# 04 — Klienci i dostawcy (hodowcy)

## Klienci B2B (top wartościowo)

**Hurtownie krajowe:**
- **Damak** — top 1, kluczowy klient Pani Joli
- **Trzepałka** — top 1, drugi klient Pani Joli (monopol Joli)
- **Biesarska** — duża hurtownia
- **Destan** — duża hurtownia
- **Szubryt** — duża hurtownia
- **Bomafar** — załadunki popołudniowe
- **Polski Drób** — duży gracz
- **Publimar** — załadunki popołudniowe
- **Łyse** = **JBB Bałdyka** (duża sieć ubojni — kupują nasze nadwyżki)
- **Radrob** — średni klient (incydent z niedoborem 12.7t)
- **Ladros** — średni klient

**Klienci eksportowi (przez pośredników):**
- Mrożone (rynki DE, NL, RO)
- Obsługa: Ania
- Cel 2026: **eksport bezpośredni** (omijanie pośredników, większa marża)

---

## Liczby

- **400+ klientów** w bazie HANDEL
- **Aktywnie sprzedawane: 80-100** klientów dziennie
- **Top 10 klientów** = ~70% wolumenu (zasada Pareto)

---

## Polityka VIP (oficjalnie BRAK)

**Z procedur 01_HANDLOWCY:**
- Każdy klient dostaje **TEN SAM PROCENT** przy ucinaniu
- VIP-owanie ZAKAZANE — wyjątki tylko za pisemną zgodą Zarządu

**Realia (Sergiusz przyznaje):**
- Pani Jola obiecuje Damak/Trzepałka pełną dostawę
- Radek "na końcu łańcucha pokarmowego" — dostaje resztki
- Incydent: Trzepałka obiecano 7.5t przy 5t dostępnych → Radrob nic dostał (skumulowany niedobór 12.7t)
- **Krytyczny scenariusz:** produkcja 50/50 mały/duży, zamówienia 80/20 duży/mały — kto dostaje co?

**Algorytm Sergiusza w ZPSP:**
- < 5% odchyłki → zespół handlowy ucina sam
- 5-20% → zespół + zatwierdzenie Sergiusza
- > 20% → tylko Sergiusz decyduje

---

## Hodowcy (dostawcy żywca)

### Skala
- **140+ hodowców rejestrowanych** (wszyscy w bazie)
- **40-70 aktywnych** w danym kwartale
- **1874 hodowców z Excela** zaimportowano do tabeli `Pozyskiwanie_Hodowcy` (LibraNet)

### Model kontraktowy (50/50)
- **50% kontrakt:** Sergiusz dostarcza paszę, hodowca rośnie kurczaki, Sergiusz odbiera po 35-42 dniach po umówionej cenie
- **50% wolny rynek:** hodowca sam zarządza, sprzedaje na rynku

### Cykl
- 0 dzień: pisklęta wstawione (np. 1 stycznia)
- 35 dzień: ubój 20% (rozluźnienie kurnika)
- 42 dzień: ubój reszty
- AVILOG planuje precyzyjnie odbiór (samochód po samochodzie)

### Cena żywca
- **4.00 zł/kg** wolny rynek
- **4.40 zł/kg** rolnicza
- **5.23 zł/kg** ministerialna

### Pasze (kupowane od)
- **TASOMIX** (główny dostawca)
- **De Heus**
- **Ekoplon**

**Rodzaje paszy** (kategoria HANDEL: 65883, jednostka: tona):
- **Brojler ALFA** (główny)
- **Grower 1 / Grower 2**
- **Finiszer** (ostatnie dni)
- **Starter** (pisklęta 0-7 dni)

### Tracebility partii
- **Numer partii** = konkatenacja:
  ```
  [3 cyfry CustomerID] + [8 cyfr Partia: RR-DDD-AAA]
                          RR  = rok (np. 26 = 2026)
                          DDD = dzień w roku (001-366)
                          AAA = numer auta od tego hodowcy w danym dniu
  ```
- **Przykład:** `390-26119004` = hodowca 390 (Szymczak Dariusz), 119. dzień 2026 (29 kwietnia), 4-te auto
- Tabele: `LibraNet.dbo.PartiaDostawca` (mapowanie partia↔hodowca), `LibraNet.dbo.In0E.P1` (sama 8-cyfrowa Partia, bez CustomerID — JOIN niezbędny)
- **Mix partii:** gdy mięso z 2 transportów się miesza → tworzona NOWA partia (ten sam numer auta, inny CustomerID z przodu)
- Każda partia ma rejestrowane: hodowca, sztuki, waga, klasa A/B (na oko), data uboju
- **Brak skanerów RFID/kodów kreskowych** — magazynierzy wpisują numery partii ręcznie na WZ, ilości "na oko"
- **Wszyscy hodowcy są zewnętrzni — firma NIE ma własnych ferm**
- **Ten sam dostawca pod różnymi `CustomerID`** — np. ferma + brat. Realnie ta sama działalność. Przy raportowaniu agregować po `CustomerName` z normalizacją

Pełen dekoder + queries SQL: `BAZA_WIEDZY/18_Analiza_przychodu_szczegoly.md`.

### Feedback do hodowcy
- Hodowca dostaje informację gdy coś nie tak (krwiaki, złamania, czerwony filet)
- Dajemy drugą szansę
- Przy następnych incydentach **obcinamy ilość** dostawy
- Specyfikacja drobiu ma rubrykę "klasa B" — tam można zobaczyć

### Pain point Sergiusza (z PYTANIA_PRODUKCJA)
> *"Fajnie by było aby przy odebraniu hodowcy można było sprawdzać jak często jego partia jest reklamowana, ile klasy B i A ma."*

**Cel:** Ranking hodowców per partia + alert "Stróżewski 3 reklamacje — rozmowa".

---

## Pozyskiwanie hodowców (CRM)

**Moduł:** `Hodowcy/PozyskiwanieHodowcowWindow.xaml.cs` (`accessMap[55]`)
**Tabele:** `Pozyskiwanie_Hodowcy`, `Pozyskiwanie_Aktywnosci` (DB: LibraNet)
**Stan:** 1874 hodowców z Excela zaimportowanych

**Statusy leadów:**
1. Nowy lead (kontakt w 48h)
2. Skontaktowany
3. Zainteresowany
4. Próbne dostawy
5. Stały dostawca
6. Odrzucony (kontakt za 3 mies.)

---

## Limity kredytowe (klienci)

- Ustala **ubezpieczyciel**
- Klient może wysłać sprawozdania finansowe bezpośrednio do ubezpieczyciela
- **ZPSP blokuje zamówienie** po przekroczeniu limitu lub przeterminowaniu

**Eskalacja windykacji:**
- 1-3 dni przeterminowania → przypomnienie
- 7 dni → ostrzeżenie
- 14 dni → wstrzymanie sprzedaży

**Sygnał o problemach:** **6 towarzystw odmówiło polisy** (informacja o deficytach klientów). Co to znaczy: ubezpieczyciele widzą ryzyko w tej branży.

---

## Reklamacje od klientów

**Rzeczywiste typowe reklamacje:**
- Filet czerwony (= krwiak)
- Złamania (kurczak źle złapany w hodowli)
- Żółć (nieusunięta podczas patroszenia)
- Oparzenia (skóra przy parzeniu)
- Otwarte rany

**75% reklamacji w bazie ZPSP** to **AUTO-IMPORT faktur korygujących z Symfonii** (FKS, FKSB, FWK) — zawyża statystyki, nie wszystkie to faktyczne reklamacje jakościowe!

**SLA:**
- Odpowiedź klientowi do **15:00 tego dnia**
- Zamknięcie reklamacji w **48h**

**Workflow reklamacji** (PROCEDURY_07_JAKOSC):
1. Klient zgłasza → numer reklamacji
2. Klasyfikacja (jakościowa / ilościowa / transportowa)
3. Badanie (hala, dokumenty, kamery, logi ZPSP)
4. Konsultacja Dyrektor + Zarząd
5. Decyzja: pełne / częściowe / odrzucenie
6. Odpowiedź klientowi do 15:00
7. Zamknięcie 48h
8. Działania korygujące (CAPA): co/kto/do kiedy
9. Archiwizacja

---

## Anulacje zamówień

**Realia (Sergiusz):** *"Co 2 dni się zdarza. Po prostu szukamy innego klienta który od nas rzadziej bierze i mamy nadzieję że weźmie. Bo jak nie weźmie to stoimy z towarem."*

**Kanały informacji:**
- WhatsApp grupa Handlowa
- "Na żywo" (ustnie) między handlowcami
- ZPSP — nie zawsze wpisane od razu

**Ryzyko:** Towar zamówiony → wyprodukowany → klient anuluje → nadwyżka zalega → krojenie / mrożenie / strata wartości.
