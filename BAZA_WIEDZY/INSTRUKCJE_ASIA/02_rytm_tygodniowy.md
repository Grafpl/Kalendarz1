# Asia — rytm tygodniowy strażnika kontraktów

> Powtarzalna struktura tygodnia. Po 4-6 tygodniach robi się automatycznie. Drukowane na biurko jako "kotwica dnia".

---

## 🗓 PONIEDZIAŁEK — start tygodnia (intensywny)

### Rano (9:00–10:00) — przegląd stanu
- [ ] **Centrum Asi** (gdy wdrożone) lub manualny przegląd:
  - Wnioski o Zmiany pending (cele: zerować od poprzedniego tygodnia)
  - Wstawienia bez potwierdzenia > 48h (do Magdy do dokończenia)
  - Płatności zaległe > 7 dni (do Magdy do telefonów hodowcom)
  - Alerty wygasania kontraktów (jeśli moduł Kontrakty wdrożony)
- [ ] **5 minut z Magdą** — co Cię martwi w tym tygodniu?

### W ciągu dnia
- [ ] **Zatwierdzanie wniosków zmian** (cel: same-day)
- [ ] **Pierwsza transza przelewów** (na podstawie zaległych z poprzedniego tygodnia)

### Po południu (15:00)
- [ ] Plan tygodnia: jakie umowy są do podpisania / przedłużenia w tym tygodniu

---

## 🗓 WTOREK — kontrakty + maile

### Rano
- [ ] **Mail dnia:** odpowiedz hodowcom na maile z poprzedniego tygodnia (cel: zerowy inbox do końca dnia)

### W ciągu dnia
- [ ] **Praca z modułem Kontrakty:**
  - Generacja Word dla wniosków nowych z piątku/poniedziałku
  - Aktualizacja statusów (SENT po wysłaniu, SIGNED po skanie)
  - Wgrywanie skanów do KontraktyZalaczniki
- [ ] **Telefon do prawniczki** (jeśli są pytania) — najlepiej wt-czw, w pn za świeże, w pt już chce mieć wolne

---

## 🗓 ŚRODA — przegląd hodowców + ARiMR

### Rano
- [ ] **Dashboard ARiMR** (gdy wdrożony) — czy % compliance > 50%?
- [ ] **Lista hodowców do zakontraktowania** (spotowi z dużym wolumenem)
  - Wybierz **2-3 hodowców** na ten tydzień
  - Przekaż Teresce/Magdzie z notatką "zadzwońcie, zaproponujcie kontrakt 3-letni"

### W ciągu dnia
- [ ] **Inwentaryzacja umów papierowych** (do migracji do bazy — przed wdrożeniem modułu)
  - Cel: 20-30 umów/tydzień w Excelu
- [ ] **Spotkanie ze Serem** (15-30 min) — strategia kontraktów, decyzje cenowe

---

## 🗓 CZWARTEK — przygotowanie do ZSRIR

### Rano
- [ ] **Sprawdzenie danych za bieżący tydzień:**
  - Faktury żywca w HANDEL (od pn do śr) — czy Tereska wpisała wszystkie?
  - Harmonogram dostaw w LibraNet
  - Specyfikacje wystawione
- [ ] Jeśli brakuje → poganianie Tereski

### W ciągu dnia
- [ ] **Płatności drugiej transzy** (na podstawie faktur z bieżącego tygodnia)
- [ ] **Kontynuacja inwentaryzacji umów**

---

## 🗓 PIĄTEK — ZSRIR (główne wydarzenie tygodnia)

### Rano (9:00–13:00)
- [ ] Spokojne zakończenie tematów otwartych z tygodnia
- [ ] Magda kończy bieżące wstawienia / potwierdzenia

### 14:00 — ZSRIR z Magdą (instrukcja Magdy #15)
- [ ] Otwórz "📊 Sprawozdania"
- [ ] Wybierz tydzień (F11/F12)
- [ ] Sprawdź 3 źródła (HANDEL, LibraNet, Specyfikacje)
- [ ] Napraw rozbieżności (jeśli są)

### 16:00 — Wysyłka
- [ ] Generuj tekst maila (Ctrl+M)
- [ ] Outlook → wklej + załącz CSV
- [ ] **Wyślij na zsrir@minrol.gov.pl** (sprawdź aktualny adres!)
- [ ] Powiedz Magdzie: "Dzięki, ładnie dziś wszystko dograne. Następny piątek może już sama spróbujesz."

### 16:30 — Tygodniowy email do Sera
```
Cześć Ser,

ZSRIR poszedł 16:00, wszystko OK.
Pending na poniedziałek:
  - 3 wnioski zmian (Asia: zerwę pn rano)
  - 2 nowe kontrakty do generowania Worda
  - Compliance ARiMR: 67.4% (margines +17 pp)
  - Hodowcy do zakontraktowania (top 3): Abramowicz, Chojnacki, Dąbrowski

Magda dziś dała radę z ZSRIR samodzielnie w 30%. Tydzień 3 — pełna samodzielność.

Asia
```

---

## 📅 1. DNIA MIESIĄCA (zwykle poniedziałek)

**Pełna instrukcja:** [Magda #16](../INSTRUKCJE_MAGDA/16_pierwszy_dzien_miesiaca_checklist.md)

Twoja rola dodatkowo:
- [ ] **GUS R09 — start** (masz do 8.)
  - Otwórz `R09UWindow` (z `SprawozdaniaGusHubWindow`, kategoria PRODUKCJA)
  - Wybierz poprzedni miesiąc
  - Sprawdź 5 wartości (sztuki, waga żywa farmerska, waga poubojowa brutto, waga handlowa netto, wartość zł)
  - Drill-down — sprawdź czy nie ma anomalii (sztuki=0)
- [ ] **Compliance ARiMR za poprzedni miesiąc** — snapshot do raportu dla Sera
- [ ] **Przelewy hodowcom** — pierwsza większa transza miesiąca

---

## 📅 8. DNIA MIESIĄCA — DEADLINE GUS R09

- [ ] **NAJPÓŹNIEJ DO KOŃCA DNIA:** wysyłka R09 do Portalu Sprawozdawczego GUS
  - Login: https://raport.stat.gov.pl/
  - Upload XML wygenerowany z ZPSP
  - Po odpowiedzi → Mark as Sent w ZPSP
- [ ] Jeśli nie zdążysz → telefon do Sera + alert push w ZPSP (gdy auto-szkic wdrożony)

---

## 📅 10. DNIA MIESIĄCA — raport zamknięcia

- [ ] **Raport dla Sera** (email):
  ```
  Zamknięcie [miesiąc] [rok]:
  - Surowiec ogółem: X kg
  - Surowiec pod ARiMR: Y kg (Z%)
  - Hodowcy aktywni: N
  - Nowe kontrakty w miesiącu: M
  - Kontrakty wygasłe: K
  - Compliance trend: rośnie / spada / stabilnie
  
  Akcje na następny miesiąc:
  - [...]
  ```

---

## 🎯 KAMIENIE MILOWE (długoterminowe)

| Termin | Co |
|---|---|
| **Co czwartek** | Sprawdzenie danych do ZSRIR |
| **Co piątek 16:00** | ZSRIR wysyłka |
| **Co 1. dnia miesiąca** | Inwentaryzacja zamknięcia |
| **Co 8. dnia miesiąca** | GUS R09 wysyłka |
| **Co 10. dnia miesiąca** | Raport dla Sera |
| **Co kwartał** | Przegląd cen / typów ceny w aktywnych kontraktach |
| **01.08.2026** | Migracja kontraktów na sp. z o.o. (scenariusz S5) |
| **IX.2026** | Wniosek ARiMR — dashboard compliance + PDF audytu |

---

## ⚡ ZASADY ŻELAZNE — Asia

1. **Nigdy nie pomijaj piątkowego ZSRIR.** Brak = ryzyko kary.
2. **Nigdy nie pomijaj 8. dnia GUS R09.** To samo.
3. **Nigdy nie zmieniaj danych w Symfonii bez backupu.** Pomyłka = długie odkręcanie.
4. **Magda nie zna kontekstu hodowców** w pierwszych 3 miesiącach — odpowiadaj cierpliwie.
5. **Spór z hodowcą** = zawsze najpierw słuchaj, potem decyzje konsultuj ze Serem.
6. **Compliance ARiMR < 50%** = czerwony alert do Sera natychmiast.
7. **Co tydzień jeden lunch z Magdą** — zespół, nie tylko relacja koleżeńska/podwładna.

---

## 🛠 NARZĘDZIA POD RĘKĄ

| Co | Gdzie |
|---|---|
| Kontrakty | Kafelek **📜 Kontrakty Hodowców** (po wdrożeniu Fazy 1) |
| Wnioski o Zmiany | Kafelek **📝 Wnioski o Zmiany** |
| Płatności | Kafelek **💵 Rozliczenia z Hodowcami** |
| Statystyki hodowców | Kafelek **📊 Statystyki Hodowców** |
| ZSRIR | Kafelek **📊 Sprawozdania** |
| GUS R09 | Kafelek **📊 Sprawozdania GUS** (kategoria PRODUKCJA!) |
| Dashboard ARiMR | Kafelek **🎯 Dashboard ARiMR** (po wdrożeniu Fazy 3) |
| Foldery sieciowe | `\\192.168.0.170\Install\UmowyZakupu\` (umowy), `\Public\Potwierdzenia_Wstawien\` (dowody), `\Public\Wnioski_Zmian_Hodowcow\` (zmiany danych) |

---

*Wersja 1.0 • 24.05.2026 • Twój rytm Asia, modyfikuj jak się ułożysz w praktyce*
