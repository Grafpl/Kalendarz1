# Analiza pracy handlowca Maja — instrukcja

3 pliki w `BAZA_WIEDZY/SELECTY/`:

| Plik                          | Gdzie uruchomić                 | Co liczy                                |
|-------------------------------|----------------------------------|-----------------------------------------|
| `analiza_maja_HANDEL.sql`     | **192.168.0.112** / `Handel`     | Faktury, ceny, marża, klienci, płatności (A, B, C, D, E, F, I + K1) |
| `analiza_maja_LIBRANET.sql`   | **192.168.0.109** / `LibraNet`   | Zamówienia, reklamacje, CRM (G, H, J + K2) |
| `analiza_maja_LINKED.sql`     | tylko gdy masz linked server     | Wariant referencyjny (linked nie działa w obu kierunkach — nie używaj) |

---

## 1. Procedura (kolejność)

### Krok 1 — uruchom HANDEL

1. SSMS → połącz z `192.168.0.112` (user `sa`, baza `Handel`)
2. Otwórz `analiza_maja_HANDEL.sql`
3. **Najpierw zaznacz tylko raport 0.0** (linie ~50–65) → F5 → zweryfikuj jak Maja jest zapisana. Jeśli nie 'Maja', zmień `@HandlowiecMaja` na linii 35.
4. F5 — uruchom całość. Wyniki: 16 resultsetów (0.0 do K1).
5. **WAŻNE: z raportu `0.2b` skopiuj wartość komórki `KlienciMaiCSV`** (lista ID przecinkami). Zachowaj w schowku — będzie potrzebna w Kroku 2.
6. Każdy resultset zapisz do CSV: prawym → Save Results As → CSV.

### Krok 2 — uruchom LibraNet

1. SSMS → nowe okno → połącz z `192.168.0.109` (user `pronova`, baza `LibraNet`)
2. Otwórz `analiza_maja_LIBRANET.sql`
3. **W linii 47 wklej listę ze schowka** w miejsce `NULL`:
   ```sql
   DECLARE @KlienciMaiCSV NVARCHAR(MAX) = N'12345,12346,12351,12399,...';
   ```
   Jeśli pominiesz — sekcja H.1 pokaże WSZYSTKIE reklamacje z okresu (więcej szumu, ale dane są).
4. F5 — uruchom całość. Wyniki: ~10 resultsetów (0.0, G.1–G.3, H.1–H.3, J.1–J.3, K2).
5. Zapisz każdy resultset do CSV.

### Krok 3 — wklej do Claude w przeglądarce

Sugerowany prompt:

> Oto twarde dane sprzedażowe handlowca Maja z mojego systemu ZPSP. Maja
> zaczęła pracę 10.2025, dziś żąda podwyżki z 7000 do 10000 zł albo
> odchodzi. Drugi handlowiec (Teresa) też pójdzie na 10000 (parytet).
>
> Przeanalizuj 11 wymiarów i odpowiedz:
> 1. Czy dane uzasadniają jej żądanie?
> 2. Co mówi scorecard K1 + K2 vs inni handlowcy?
> 3. Gdzie traci/zarabia marżę (D.3 vs D.4)? Sumaryczne manco lub zysk (D.2)?
> 4. Red flagi: klienci uciekają (C.2, F.1), reklamacje (H.2), płatności (I.2)?
> 5. 5 KPI do ustawienia na Q4 / pierwszy kwartał.
> 6. Czy parytet z Teresą jest fair w świetle danych?

W pierwszej kolejności wklej **K1 + K2** (scorecardy) — to esencja.

---

## 2. Lista raportów

### Z HANDEL (`analiza_maja_HANDEL.sql`)

| Raport | Opis                                                      | Pytanie                |
|--------|-----------------------------------------------------------|------------------------|
| 0.0    | Lista kandydatów na "Maję" w ContractorClassification     | Czy mam dobry filtr?   |
| 0.2    | Lista klientów Mai (kontrahent_id + nazwy)                | Kto kupuje od Mai?     |
| **0.2b** | **CSV z ID klientów Mai — KOPIUJ to**                   | Do skryptu LibraNet    |
| A.1    | Wolumen miesięczny per handlowiec                         | Czy Maja rośnie?       |
| A.2    | Maja vs średnia firmy per miesiąc                         | Jaki ma udział?        |
| B.1    | Top 100 klientów Mai                                      | Kto kupuje?            |
| B.2    | Top 5 klientów + udział skumulowany                       | Uzależnienie od top?   |
| B.3    | HHI koncentracji portfela (wszyscy handlowcy)             | Zdrowy portfel?        |
| C.1    | Klienci Mai: NOWI / PRZEJĘCI / KONTYNUACJA                | Pozyskała sama?        |
| C.2    | Klienci UTRACENI                                          | Kto uciekł?            |
| D.1    | Ceny Mai vs średnia firmy per towar/miesiąc               | Gdzie dumpuje?         |
| **D.2** | **Suma marży vs benchmark — manco/zysk**                 | **Globalna marża**     |
| D.3    | Top 10 strat marży                                        | Co tnie?               |
| D.4    | Top 10 zysków marży                                       | Co świetne?            |
| E.1    | Top 20 towarów Mai                                        | Czym handluje?         |
| E.2    | Mix świeże/mrożone — Maja vs inni                         | Premium mix?           |
| F.1    | Frekwencja per klient (sygnał 30/60/90 dni)               | Klient ucieka?         |
| F.2    | % aktywnej bazy per miesiąc                               | Baza się kurczy?       |
| I.1    | Stan należności klientów Mai                              | Trudni płatnicy?       |
| I.2    | Średnia jakość płatności per handlowiec                   | Sprzedaje ryzykownie?  |
| **K1** | **SCORECARD FAKTUROWY (wszyscy obok siebie)**             | **Główny wynik 1/2**   |

### Z LibraNet (`analiza_maja_LIBRANET.sql`)

| Raport | Opis                                                      | Pytanie                |
|--------|-----------------------------------------------------------|------------------------|
| 0.0    | Kandydaci na Maję w ZamowieniaMieso                       | Spójna nazwa z HANDEL? |
| G.1    | Zamówienia Mai per miesiąc                                | Co planuje?            |
| G.2    | Zamówienia per handlowiec (benchmark)                     | Kto anuluje?           |
| G.3    | Średni czas zamówienie → odbiór                           | Planuje na daleko?     |
| H.1    | Reklamacje (filtruje po klientach Mai jeśli wkleisz CSV)  | Z czego reklamują?     |
| H.2    | Reklamacje per UserID zgłaszający (+ mapowanie handlowca) | Maja vs inni           |
| H.3    | Typy reklamacji (jakość vs auto-import korekt)            | Realne reklamacje?     |
| J.1    | Aktywność NotatkiUzycia per handlowiec                    | CRM aktywność          |
| J.2    | CallReminderConfig (jeśli Maja używa CRM)                 | Przypomnienia          |
| J.3    | UserID Mai (do crossów)                                   | Diagnostyka            |
| **K2** | **SCORECARD ZAMÓWIENIOWY (zamówienia + reklamacje)**      | **Główny wynik 2/2**   |

---

## 3. Ryzyka interpretacyjne (przekaż Claude wraz z danymi)

1. **ContractorClassification = stan AKTUALNY, nie historyczny.** Jeśli klient niedawno przepisany na Maję (przejęcie po Paulinie), jego historyczne faktury retro-przejdą pod Maję. Porównaj raporty 0.0 (HANDEL) ↔ 0.0 (LibraNet) — rozjazd = sygnał.

2. **Jola robi ~60% wolumenu firmy** (Damak, Trzepałka). Jej średnia cena/kg jest sztucznie niska bo wielkie wolumeny. **Porównuj Maję głównie z Anią, Radkiem, Teresą.**

3. **Paulina przechodzi z części sprzedaży na żywiec.** Jej mix może być nietypowy — traktuj jako bench warunkowy.

4. **Sekcja H (reklamacje):** zgodnie z `BAZA_WIEDZY/08_Sprzedaz_ceny.md` **75% reklamacji to auto-import faktur korygujących z Symfonii** (FKS, FKSB, FWK). To NIE są reklamacje jakościowe. Raport H.3 (typy) pomoże oddzielić — jeśli dominuje "Korekta" lub pusto, zignoruj liczby ogólne.

5. **Sekcja I (płatności)** to stan na DZIŚ. Klient z 30+ dni przeterminowania to niekoniecznie wina Mai. Patrz raczej: średnia dni przeterm per handlowiec (I.2) — sygnał "sprzedaje trudnym".

6. **Sekcja D (ceny vs benchmark):** zakłada „benchmark = średnia wszystkich innych handlowców na ten sam towar w tym samym miesiącu". Towary które sprzedaje TYLKO Maja → benchmark NULL → wyłączone z sumy. Uczciwe, ale traci się ~5% pozycji.

---

## 4. Diagnostyka błędów

| Błąd                                                          | Rozwiązanie                                              |
|---------------------------------------------------------------|----------------------------------------------------------|
| `Could not find server '192.168.0.112'`                       | Używasz starego pliku `analiza_maja_LINKED.sql` — **przełącz na `_HANDEL.sql` + `_LIBRANET.sql`** (jak wyżej) |
| `Database 'LibraNet' does not exist`                          | Uruchomiłeś LibraNet-owy skrypt na serwerze HANDEL. Zmień połączenie |
| Raport 0.0 zwraca 0 wierszy                                   | Maja w bazie ma inną nazwę. Zmień LIKE na `%a%` w sekcji 0.0 i znajdź właściwą |
| Sekcja H.1 zwraca dużo niezwiązanych reklamacji               | Wklej KlienciMaiCSV z raportu 0.2b skryptu HANDEL do `@KlienciMaiCSV` w skrypcie LibraNet |
| `Invalid object name 'NotatkiUzycia'`                         | Tabela nie istnieje — sekcja J.1 sama to wykrywa i pomija. Nic nie rób |
| `STRING_SPLIT` nie istnieje                                   | LibraNet to SQL 2008 R2 — funkcja niedostępna. Wklej IDki ręcznie do `#KlienciMai` przez INSERT |
