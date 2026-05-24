# Asia — ZSRIR samodzielnie (gdy Magda jeszcze nie umie)

> Po pierwszych 4-5 piątkach z Magdą — przejmiesz solo. Ta instrukcja: pełen flow w 25 minut bez pomocy. Magda obserwuje albo robi inne rzeczy.

---

## Co to ZSRIR

**ZSRIR** = Zintegrowany System Rolniczej Informacji Rynkowej (Ministerstwo Rolnictwa, zsrir.minrol.gov.pl).

**Co wysyłamy:** tygodniowe **sprawozdanie zakupu kurczaka rzeźnego** — dane o ilości, cenie, wadze, dostawcach.

**Komu wysyłamy:** `zsrir@minrol.gov.pl` (sprawdź aktualny adres przed wysłaniem!).

**Deadline:** **piątek danego tygodnia** (do końca tygodnia którego dotyczy).

**Konsekwencja braku:** kara administracyjna od IJHARS (Inspekcji Jakości Handlowej Artykułów Rolno-Spożywczych).

---

## Setup przed pierwszym samodzielnym piątkiem

- [ ] Outlook zalogowany na **Twój** mail firmowy (nie czyjś inny)
- [ ] **Aktualna lista adresów** Ministerstwa (mam: zsrir@minrol.gov.pl — sprawdź u Sergiusza czy nie ma świeższego)
- [ ] **Login do Portalu Sprawozdawczego GUS** (raport.stat.gov.pl — to inne sprawozdanie R09, ale często mylone)

---

## ⏱ FLOW — 25 minut w piątek

### 14:00 — Otwarcie

1. **Kafelek "📊 Sprawozdania"** (kategoria ZAOPATRZENIE I ZAKUPY).
2. Okno `SprawozdaniaWindow` ładuje **bieżący tydzień** automatycznie.
3. Sprawdź nagłówek: powinno być "Tydzień XX: dd.mm–dd.mm.yyyy" — bieżący kończący się dziś.

### 14:05 — Weryfikacja danych (najwięcej czasu)

System pokazuje **3 sekcje**:

#### A) HANDEL — faktury żywca
```
┌──────────────────────────────────────────┐
│ Dzień       │ Sztuki │ Waga kg │ Wart.zł│
├──────────────────────────────────────────┤
│ 18.05.2026  │  35000 │  82500  │ 619500 │
│ 19.05.2026  │  28000 │  66400  │ 498000 │
│ 20.05.2026  │  32000 │  75600  │ 567000 │
│ ...                                       │
└──────────────────────────────────────────┘
```

**Co weryfikujesz:**
- Czy są dane na każdy dzień który był aktywny (mogą być dni bez dostaw — OK)
- Czy sumy są sensowne (typowo 100-200 tys. sztuk/tydzień)

**Częsty problem:** brak faktur z czwartku/piątku — Tereska nie zdążyła wpisać. Telefon do Tereski → wpisuje → F5 w ZPSP.

#### B) LibraNet — harmonogram dostaw
```
┌──────────────────────────────────────────┐
│ Dzień       │ Sztuki  │ Sumaryczna waga │
├──────────────────────────────────────────┤
│ 18.05.2026  │  35000  │     82500       │
│ ...                                       │
└──────────────────────────────────────────┘
```

**Co weryfikujesz:** **liczby sztuk muszą się zgadzać z HANDEL**.

**Tolerancja:** różnica < 100 sztuk = OK (zaokrąglenia). Większa = błąd, sprawdzaj.

#### C) Specyfikacje wystawione w tygodniu
Lista PDF-ów które poszły do hodowców. **Tylko informacyjnie** — system nie używa do liczenia.

### 14:15 — Naprawa rozbieżności (jeśli są)

Najczęstsze:
- **Brak faktury → Tereska wpisuje w Symfonii** (3-5 min) → F5 w ZPSP
- **Sztuki HANDEL ≠ Sztuki Libra** → sprawdź ostatnią dostawę (może portier wpisał inną liczbę niż faktura)
- **Wartość zł = 0 dla dnia** → faktura nie ma ceny / inna stawka VAT → sprawdź w Symfonii

**Gdy nie da się dopasować w 15 minut:** **wyślij to co masz** + w mailu dopisz:
> Sprawozdanie zawiera niekompletne dane za [dzień]. Korekta zostanie przesłana w poniedziałek.

To akceptowane przez Ministerstwo.

### 14:20 — Generowanie maila

1. Kliknij **"Generuj tekst maila"** lub **Ctrl+M**.
2. Pole tekstowe wypełnia się gotową treścią — sprawdź czy daty/sumy się zgadzają.
3. Kliknij **"Kopiuj do schowka"** (Ctrl+C).
4. Kliknij **"Eksport CSV"** (Ctrl+S) — zapisuje plik np. `ZSRIR_2026-W21.csv` w `Dokumenty\ZSRIR\`.

### 14:23 — Wysyłka Outlook

1. Otwórz Outlook → **Nowy mail**:
   - **Do:** `zsrir@minrol.gov.pl`
   - **Cc:** Twój mail firmowy (zachowasz kopię w sent + w skrzynce odbiorczej)
   - **Temat:** `Sprawozdanie ZSRIR tydzień XX / RRRR — Piórkowscy`
2. **Treść:** Ctrl+V (wklej ze schowka).
3. **Załącznik:** załącz CSV.
4. **Wyślij**.

### 14:25 — Dokumentacja

- [ ] W Outlook **Sent** sprawdź czy mail wyszedł (status "Wysłana").
- [ ] W ZPSP klik **"Mark as Sent"** (jeśli jest taka opcja w SprawozdaniaWindow) lub zapisz w swoim Excelu/notatce: "tydzień 21 — wysłane 14:25, OK".

---

## ⚠️ Co może pójść nie tak

| Problem | Co robić |
|---|---|
| Wszystkie sumy = 0 | Tereska nie wpisała żadnych faktur w Symfonii. Pilnie telefon. |
| HANDEL pokazuje błąd połączenia | Sprawdź czy serwer 192.168.0.112 odpowiada (Edyta IT). |
| ZPSP zawiesza się przy F5 | Restart aplikacji. Jeśli powtarza się → Ser. |
| Outlook nie wysyła | Edyta IT. Tymczasowo użyj webmail / telefonu (mobile Outlook). |
| Adres Ministerstwa zmienił się | Aktualny: `zsrir@minrol.gov.pl` — sprawdź w komunikatach Ministerstwa lub u Sergiusza. |
| Magda dzwoni o pomoc | Pomóż 5 min, potem powiedz "wracam do ZSRIR, zadzwonię o 14:30". |
| Hodowca dzwoni o termin płatności | "Sprawdzę po 16:00, oddzwonię" — nie przerywaj sprawozdania. |
| Ser dzwoni | Odbieraj, ale powiedz "robię ZSRIR, mogę za 30 min?" |

---

## 🔥 Plan B — gdy nie zdążasz do 16:00

1. **15:30** — jeśli nadal jesteś w stanie zrobić → kończ.
2. **15:45** — jeśli nadal nie idzie → **wyślij niekompletne** z notą o korekcie w poniedziałek.
3. **16:00** — wyłącznie awaryjnie: poproś Sera o wysłanie w sobotę rano.
4. **Brak wysyłki przed poniedziałkiem 8:00** = ryzyko kary.

---

## ✅ Skąd wiesz że zrobiłaś dobrze

- Mail w Outlook Sent, dzisiejsza data, do `zsrir@minrol.gov.pl`, z załącznikiem CSV.
- W ciągu kolejnego tygodnia **brak pisma z Ministerstwa** = OK.
- Sergiusz w piątek wieczorem dostał krótki email "ZSRIR OK".

---

## 🎯 Po 4-5 piątkach Magda też potrafi

Wtedy:
- **Magda generuje + wysyła sama**, Ty sprawdzasz końcowy mail przed wysłaniem
- **Po 3 piątkach z samodzielną Magdą** — możesz w piątek zająć się czymś innym, Magda alarmuje Cię tylko w razie problemu
- **Twoja rola docelowa:** odbiorca cotygodniowego raportu, nie wykonawca

---

## 🔧 Czego ZPSP NIE obsługuje (dziś)

> **[BRAK W ZPSP — DO DODANIA]**
> - Brak **automatycznego wysłania maila** (Outlook ręczny)
> - Brak **alertu "piątek 14:00 — czas na ZSRIR"** (Asia/Magda pamięta)
> - Brak **archiwum wysłanych ZSRIR** w bazie (tylko w Outlook Sent)
>
> *Planowane:* w **Centrum Asi** (Część 3 audytu, D4 Tracker GUS) — auto-szkic w piątek 13:00 + alert "do wysłania" + jeden klik wysyłki.
