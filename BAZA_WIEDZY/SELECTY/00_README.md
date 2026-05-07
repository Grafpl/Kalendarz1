# SELECTY do uruchomienia w SSMS — instrukcja

## ✅ Status

| Runda | Plik SQL | Plik wyników | Status |
|---|---|---|---|
| **1** LibraNet | `EKSPLORACJA_LIBRANET_FULL.sql` | `WYNIKI_RAW.txt` | ✅ GOTOWE (9115 linii) |
| **1** analiza | — | `WYNIKI_ANALIZA_RUNDA1.md` | ✅ Synteza |
| **2** LibraNet głębiej | `EKSPLORACJA_LIBRANET_2.sql` | `WYNIKI_RAW_2.txt` | ✅ GOTOWE (2865 linii) |
| **2** analiza | — | `WYNIKI_ANALIZA_RUNDA2.md` | ✅ Synteza |
| **3** LibraNet najgłębiej | `EKSPLORACJA_LIBRANET_3.sql` | `WYNIKI_LIBRANET_3.txt` | ⏳ DO URUCHOMIENIA (32 bloki) |
| **3** Symfonia | `EKSPLORACJA_HANDEL_FULL.sql` | `WYNIKI_HANDEL.txt` | ⏳ DO URUCHOMIENIA (17 bloków) |
| **3** TransportPL | `EKSPLORACJA_TRANSPORTPL_FULL.sql` | `WYNIKI_TRANSPORTPL.txt` | ⏳ DO URUCHOMIENIA (10 bloków) |
| **3** UNICARD RCP | `EKSPLORACJA_UNISYSTEM_FULL.sql` | `WYNIKI_UNISYSTEM.txt` | ✅ GOTOWE |
| **4** Zależności (z audytu kodu) | `EKSPLORACJA_ZALEZNOSCI.sql` | `WYNIKI_ZALEZNOSCI.txt` | ✅ GOTOWE (analiza w `WYNIKI_ANALIZA_RUNDA4.md`) |
| Audyt kodu | — | `BAZA_WIEDZY/AUDYT_KODU_SQL.md` | ✅ GOTOWE (wzorce SELECT/INSERT/UPDATE z kodu) |
| **5** LibraNet najgłębiej | `EKSPLORACJA_LIBRANET_5.sql` | `WYNIKI_LIBRANET_5.txt` | ✅ GOTOWE (960 linii, analiza w `WYNIKI_ANALIZA_RUNDA5.md`) |
| **5** HANDEL głębiej | `EKSPLORACJA_HANDEL_2.sql` | `WYNIKI_HANDEL_2.txt` | ✅ GOTOWE (730 linii, analiza w `WYNIKI_ANALIZA_RUNDA5.md`) |
| **5** Cross-DB | `EKSPLORACJA_CROSS_DB.sql` | `WYNIKI_CROSS_DB.txt` | ✅ GOTOWE (304 linii, analiza w `WYNIKI_ANALIZA_RUNDA5.md`) |
| **5** Synteza | — | `WYNIKI_ANALIZA_RUNDA5.md` | ✅ Synteza wszystkich 3 plików |

## ⚠️ Login do Symfonii (HANDEL, 192.168.0.112)

**INNE niż LibraNet!** Connection string z kodu ZPSP:
- **Server:** `192.168.0.112`
- **Database:** `Handel`
- **User:** `sa`
- **Password:** `?cs_'Y6,n5#Xd'Yd`



**Lokalizacja serwera:** `192.168.0.109`
**Baza:** `LibraNet`
**Login:** `pronova` / `pronova`

---

## ⭐ TRYB ZALECANY (RAZ I KONIEC)

**Jeden plik, jedno uruchomienie, jeden zapis:**

1. Otwórz w SSMS plik **`EKSPLORACJA_LIBRANET_FULL.sql`**
2. **Ctrl+T** (tryb tekstowy — wszystkie wyniki idą do jednego okna)
3. **F5** (uruchom całość, trwa ~30 sek - 2 min)
4. **Ctrl+A** w panelu wyników → **Ctrl+C**
5. Wklej do pliku **`WYNIKI_RAW.txt`** (między znaczniki `===WYNIKI POCZATEK===` i `===WYNIKI KONIEC===`)
6. Zapisz `WYNIKI_RAW.txt`
7. **Daj plik agentowi w następnej rozmowie** — sam dojdzie do wszystkiego po pierwszej kolumnie `__SEKCJA` i komentarzach.

**Każdy SELECT ma pierwszą kolumnę `__SEKCJA`** = identyfikator bloku (np. `'04_F_in0e_operatorzy_30dni'`). Agent wie po niej do czego należy każdy wynik.

**Tip dla większej szerokości tekstu w SSMS:**
- Tools → Options → Query Results → SQL Server → Results To Text → **Maximum number of characters: 8192**
- Tools → Options → Query Results → SQL Server → Results To Text → **Tab delimited** (najlepiej do parsowania)

---

## Jak używać (stary tryb — pliki osobne)

1. Otwórz SSMS, połącz się z `192.168.0.109` (login pronova/pronova)
2. Otwórz plik `.sql` z tego folderu (`File → Open → File...`)
3. F5 żeby uruchomić cały plik **albo** zaznacz fragment myszą i F5
4. Wyniki kopiuj do `WYNIKI.md` pod odpowiednią sekcję

## Pliki

| Plik | Co bada |
|---|---|
| `01_lista_tabel.sql` | Lista wszystkich tabel + liczba wierszy + rozmiar w MB |
| `02_views_procedury.sql` | Lista widoków i stored procedures |
| `03_wersja_serwera.sql` | `@@VERSION` — jaki SQL Server (2008/2012/2017+) |
| `04_listapartii.sql` | Master tabela partii — struktura, sample, statystyki |
| `05_in0e.sql` | Rdzeń ważeń produkcji — struktura, klasy, operatorzy |
| `06_article.sql` | Słownik towarów + tolerancje empiryczne |
| `07_partiadostawca.sql` | Hodowcy + dekoder partii |
| `08_harmonogram_farmercalc.sql` | Plan dostaw + rozliczenia |
| `09_zamowieniamieso.sql` | Zamówienia od klientów |
| `10_kartoteka_odbiorcy.sql` | CRM klientów |
| `11_relacje_klucze.sql` | Foreign keys + indeksy |
| `12_triggery.sql` | Triggery i procedury używające tabel kluczowych |
| `13_quirki_typy.sql` | Typy kolumn `Data`/`Godzina` w różnych tabelach |
| `14_extensions_zpsp.sql` | Tabele rozszerzeń ZPSP (PartiaStatus, QC_*, etc.) |
| `15_dostawcy_cr.sql` | DostawcyCR + workflow akceptacji |
| `16_avilog_wstawienia.sql` | Avilog mapping + wstawienia kurczaka |
| `17_kursy_ladunki.sql` | Kursy + ładunki transportu w LibraNet |
| `18_sms_komunikacja.sql` | SmsHistory + ContactHistory |
| `19_dashboard_appsettings.sql` | DashboardWidoki + AppSettings |
| `20_haccp_jakosc.sql` | Haccp + QC_Normy + QC_Zdjecia |

## Co robić z wynikami

Wszystko wklejaj do `WYNIKI.md` w odpowiednie miejsca. Tam są przygotowane bloki kodu z `text` na każdą sekcję — wklej między `[wklej tutaj]` znaczniki.

## Jeśli coś nie działa

- **`STRING_AGG`** wymaga SQL 2017+. Jeśli LibraNet jest starszy — pomiń ten fragment, reszta zadziała.
- **`TRY_CONVERT`** może nie istnieć — pliki używają `CONVERT(varchar(10), ..., 120)`.
- Niektóre tabele mogą nie istnieć (rozszerzenia ZPSP) — komentarz `-- pomiń jeśli nie istnieje` jest gdzie potrzeba.

## Po skończeniu

Zwróć mi `WYNIKI.md` (wklej zawartość do nowej rozmowy) — będę miał wtedy pełen obraz schematu i mogę pisać kod / propozycje z konkretną wiedzą o typach, danych i relacjach.
