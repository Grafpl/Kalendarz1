# 08 — Sprzedaż, ceny, marża, bilans

## Polityka cenowa (PROCEDURY_01_HANDLOWCY)

| Produkt | Kto ustala cenę | Tryb |
|---|---|---|
| **Świeże** | Handlowiec na podstawie rynku | Obowiązkowe raportowanie Zarządowi |
| **Mrożone** | **WYŁĄCZNIE Zarząd** (Sergiusz) | Przed wpisaniem zamówienia |
| **Rabaty** | **WYŁĄCZNIE Zarząd** | Na uzasadniony wniosek handlowca |

---

## Bilans dnia

**Wzór:**
```
BILANS = PRZYCHÓD + STANY - ZAMÓWIENIA
```

**Dla każdego produktu (filet, ćwiartka, korpus, skrzydło, tuszka):**
- Przychód = ile dziś wyprodukowane
- Stany = co zostało z wczoraj w magazynie świeżych
- Zamówienia = co klienci zamówili

**Idealny stan:** Bilans dodatni o **5-6 ton** (bufor) — rezerwa na późne zamówienia, problemy.

---

## Bufor (5-6 ton)

**Cel:** Bufor 5-6 ton ZAWSZE zachowany (rezerwa).

**Wyjątek krytyczny:** **Piątek — bufor MUSI być sprzedany do 0** (towar nie przeżyje weekendu).

Co z buforem niesprzedanym w piątek:
- Tuszka → krojenie (poniedziałek)
- Pokrojone elementy → mroźnia (Janek, sobota rano)

**Nadwyżki w ciągu tygodnia (ponad bufor):**
- → **CHŁODNIA** (NIE mroźnia) — mrożenie tylko ostateczność
- Mrożenie wymaga **pisemnej zgody Zarządu**

---

## Proporcjonalne ucinanie (gdy produkcja < zamówień)

**Zasada:** Każdy klient dostaje **TEN SAM PROCENT** (np. 80%).
- NIE faworyzowanie, NIE VIP
- Wyjątki: WYŁĄCZNIE pisemna zgoda Zarządu

**Algorytm Sergiusza w ZPSP:**
- **<5% odchyłki** → zespół handlowy ucina sam
- **5-20% odchyłki** → zespół + zatwierdzenie Sergiusza
- **>20% odchyłki** → tylko Sergiusz decyduje

**Realne przypadki:**
- *Trzepałka* — Jola obiecała 7.5t mając 5t → Radrob nic
- *Radrob* — skumulowany niedobór 12.7t

**Krytyczny scenariusz nierównowagi (z PROCEDURY_01):**

> Produkcja daje **50% mały / 50% duży**, a zamówienia są **80% duży / 20% mały**:
> - Handlowcy muszą **PROAKTYWNIE proponować klientom miks 50/50**
> - Solidarność: nie zostawiać kolegom mniej pożądanego
> - **Ranni klienci dostają to co zamówili, popołudniowi dostają resztki** ← problem do rozwiązania w architekturze

---

## Marża (Sergiusz: top-down approach)

**Problem z liczeniem od dołu:** `DP.kosztAproksymowany` w HANDEL jest **niewiarygodny** — czasem równy `ilosc`, czasem 0. Nie używać.

**Sergiusz przyjmuje top-down:**
```
Marża = (cena_sprzedaży × ilość) − (cena_żywca × ilość / uzysk_%)
```

Gdzie:
- `cena_sprzedaży` = z faktury sprzedaży
- `cena_żywca` = średnia cena dnia uboju (4.00 / 4.40 / 5.23)
- `uzysk_%` = wg elementu (Filet 29.5%, Ćwiartka 33.4%, Korpus 22.7%, Skrzydło 8.7%)

**Spread żywiec→produkt (cel):** ~2.50 PLN/kg, **cel: zwiększyć**.

---

## Strata mrożenia (-18%)

Każdy kg który trafia do mroźni traci **~18% wartości** vs cena świeżej.

**Implikacja:** Lepiej sprzedać świeżą po niższej cenie niż mrozić.

---

## Limity kredytowe

- Ustala **ubezpieczyciel** (zewnętrzny)
- Klient może wysłać sprawozdania finansowe bezpośrednio
- **ZPSP blokuje zamówienie** po przekroczeniu limitu lub przeterminowaniu

**Eskalacja windykacji:**
- 1-3 dni przeterminowania → **przypomnienie**
- 7 dni → **ostrzeżenie** (e-mail + telefon)
- 14 dni → **wstrzymanie sprzedaży**

**Sygnał problemów:** **6 towarzystw odmówiło polisy** — sygnał deficytów klientów. Branża drobiarska niskomarżowa, ubezpieczyciele widzą ryzyko.

---

## CRM — statusy leadów

1. **Nowy lead** (kontakt w 48h)
2. **Skontaktowany** (rozmowa odbyta)
3. **Zainteresowany** (próbka/oferta wysłana)
4. **Próbne zamówienie** (pierwsze małe zamówienie)
5. **Stały klient**
6. **Odrzucony** (powtórzyć kontakt za 3 mies.)

**Spadek częstotliwości stałego klienta = SYGNAŁ ALARMOWY** → handlowiec dzwoni.

**Utrata stałego klienta = obowiązek raportu Zarządowi w 48h.**

---

## Spotkania (oficjalnie)

| Spotkanie | Częstotliwość | Uczestnicy | Status |
|---|---|---|---|
| Operacyjne | 9:00-10:00, 2-3x/tydz | Sergiusz + handlowcy | Aktywne |
| Decyzja krojenie/mrożenie | **13:00 codziennie** | Plan: Teresa+Justyna | **NIE ODBYWA SIĘ** (cel) |
| Rozszerzone (myjka, opakowania, finanse) | wtorek | Sergiusz + dyrektorzy | Aktywne |

**Pain point:** Sergiusz chce żeby spotkania 13:00 odbywały się BEZ niego (Teresa + Justyna prowadzą). *"Nie chcę mikro decyzji podejmować."*

---

## Komunikacja

**Aktualne kanały:**
- **WhatsApp grupy:** Handlowa, Produkcyjna, Jakość
- Anulacja → wpis do ZPSP **+** info na grupę
- Brak towaru → grupa Handlowa **+** Dyrektor + Koordynator Logistyki

**Plan migracji:** WhatsApp → **Microsoft Teams** (#sprzedaz, #produkcja, #logistyka, #jakosc, #zarzad).

**Problemy z WhatsApp:**
- Pani Jola NIE czyta — Ania pośredniczy
- Wiadomości giną w długich wątkach
- Brak struktury (mailbox vs kanały)

---

## Reklamacje

**75% reklamacji w bazie ZPSP** to **AUTO-IMPORT faktur korygujących z Symfonii** (FKS, FKSB, FWK).
- To NIE są faktyczne reklamacje jakościowe
- Zawyża statystyki

**Faktyczne reklamacje (typowe):**
- Filet czerwony (krwiak)
- Złamania
- Żółć (nieusunięta)
- Oparzenia
- Otwarte rany

**SLA:**
- Odpowiedź klientowi do **15:00 tego dnia**
- Zamknięcie **48h**

---

## Awaryjne procedury (PROCEDURY_01)

**Przy awarii ZPSP:**
- Handlowiec MUSI kontynuować przyjmowanie zamówień (kartka, Excel, telefon)
- **Po przywróceniu** — wszystkie zamówienia do ZPSP w trybie pilnym
- Tłumaczenie *"system nie działał"* = niedopuszczalne

**Co WOLNO i NIE WOLNO fakturzystkom (PROCEDURY_02):**
- ✅ Wystawić fakturę gdy załadunek potwierdzony i zgadza się z ZPSP
- ❌ Karteczka zamiast ZPSP (zawsze: *"Wprowadzę do ZPSP, proszę podać dane"*)
- ❌ Faktura przed załadunkiem (czeka aż magazyn potwierdzi)
- ❌ Zmiana zamówienia bez zgody handlowca
- ❌ Decydować komu dać towar gdy brakuje (eskalacja Dyrektor)

---

## Pain points sprzedaży (Sergiusz)

> *"Wkurza mnie to, że ciężko przewidzieć ile ostatecznie będzie towaru na koniec dnia."*

> *"Pani Jola monopolistyczny most do Damak/Trzepałka — używa karteczek."*

> *"Co 2 dni anulacja — szukamy innego klienta który od nas rzadziej bierze."*

---

## Pomysły do ZPSP (z PYTANIA_PRODUKCJA)

1. **Marża top-down w Dashboardzie Sprzedaży** — algorytm jak wyżej (Task #30, pending)
2. **Alert „Niesprzedane na piątek"** — bufor musi spaść do 0 (Task #31, pending)
3. **Awansowanie Teresy → Dyrektor Handlowy** — workflow w ZPSP dla niej (do rozmowy)
