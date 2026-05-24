# 7. Wystawianie specyfikacji żywca

**Kiedy tego używasz:** przed każdą dostawą żywca — żeby hodowca dostał mailem **specyfikację** (parametry surowca, cena, zakres wagi, termin). To dokument towarzyszący umowie zakupu.
**Ile czasu zajmuje:** 3-5 minut (gdy kopiujesz z poprzedniej) / 10 min (gdy nowa od zera).
**Wideo:** [link — Ser dorzuci nagranie]

> Magda — okno specyfikacji jest **bardzo duże i wygląda groźnie** (16 tysięcy linii kodu — Ser sam mówi). Większość pól jest dla Sera/Asi do analiz. **Ty użyjesz max 8 pól.** Ucz się tylko ich.

## Co musisz mieć przed startem
- [ ] **Hodowca** w bazie + jego email
- [ ] **Ustalenia cenowe** (cena/kg, typ ceny) — z umowy zakupu (#6)
- [ ] **Zakres wagi** kurczaka — typowo 1.6-2.4 kg
- [ ] **Termin dostawy** (data lub okres)

---

## Kroki

**1. W menu głównym kategoria ZAOPATRZENIE I ZAKUPY → kafelek "📋 Specyfikacja Surowca".**
[SCREEN: menu główne z kafelkiem "Specyfikacja Surowca" zaznaczonym]
Otworzy się duże pełnoekranowe okno "Specyfikacje Dostaw — System Piórkowscy".

**2. **Najpierw sprawdź czy NIE ma już specyfikacji dla tego hodowcy.** W liście DataGrid wyszukaj nazwisko (filtr po hodowcy). Jeśli jest poprzednia z tym samym hodowcą — **najprostsza droga to ją skopiować i edytować daty**.**
[SCREEN: główny DataGrid Specyfikacji z listą wcześniejszych specyfikacji i podświetloną jedną dla "Kowalski"]

**3. **Wariant A — kopiuj z poprzedniej (PREFEROWANY, 80% przypadków):**
- Zaznacz wcześniejszą specyfikację tego hodowcy.
- Kliknij prawym → **"Kopiuj jako nowa"** (jeśli widoczne) lub w toolbar przycisk **"Duplikuj"**.
- Otworzy się formularz z wypełnionymi polami z poprzedniej.
- Zmień tylko: **datę**, **cenę** (jeśli się zmieniła), **zakres dostaw** (jeśli inny).
- **Skacz do kroku 6.**

**4. **Wariant B — nowa od zera:**
- W górnym pasku kliknij **"Nowa specyfikacja"** (zielony przycisk).
[SCREEN: górny pasek z przyciskiem "Nowa specyfikacja" zaznaczonym strzałką]
- Otworzy się formularz `NowaSpecyfikacjaWindow`.

**5. **Wypełnij wymagane pola (Magda używa maksymalnie tych 8):**
- **Hodowca** (ComboBox, wybierz z listy)
- **Data od / Data do** (zakres ważności specyfikacji)
- **Sztuki** (przewidywana liczba)
- **Waga deklarowana** (Farmer, kg)
- **Szt/Poj** (ile kurczaków na 1 paletę, typowo **264** sztuki duży kurczak, mały więcej)
- **Cena** (zł/kg)
- **Ubytek %** (z umowy, typowo 3%)
- **Status** (zwykle "Aktywna")
[SCREEN: formularz nowej specyfikacji z 8 polami wypełnionymi przykładowymi danymi]

**Pozostałe pola** w formularzu są dla Sera/Asi — **nie tykaj ich** bez instrukcji. Wystarczą domyślne wartości.

**6. **Kliknij **"Zapisz"** (zielony przycisk dolny prawy).** Wpis pojawia się w głównym DataGrid.

**7. **Wysyłka emailem do hodowcy:**
- Zaznacz wiersz Twojej nowej specyfikacji.
- Kliknij **"Wyślij Email"** (przycisk w toolbar — szukaj ikony 📧 albo tekstu "Email").
[SCREEN: przycisk "Wyślij Email" zaznaczony w toolbar]
- Otworzy się okno `EmailSpecyfikacjaWindow` z gotowym mailem (adres hodowcy + PDF specyfikacji w załączniku).
- Sprawdź treść, popraw temat jeśli trzeba.
- Kliknij **"Wyślij"** w oknie maila.
[SCREEN: okno EmailSpecyfikacjaWindow z polami "Do", "Temat", "Treść" + załącznik PDF widoczny]

**8. **Po wysłaniu mail pojawia się w Outlooku Sent. Specyfikacja w ZPSP zostaje oznaczona jako "Wysłana".**

---

## ⚠️ Najczęstsze problemy

- **"Hodowca nie ma maila"** → Sprawdź w kartotece Hodowcy. Jeśli brak, dzwoń do Tereski/Asi po email. Bez maila nie wyślesz — drukuj PDF i daj kierowcy do hodowcy.
- **"Okno specyfikacji jest gigantyczne i nie wiem co kliknąć"** → To **normalne** (16k linii kodu). Trzymaj się 8 pól z kroku 5, reszta dla Sera.
- **"Wybrałam Wyślij Email ale Outlook nie otwiera się"** → ZPSP używa **Microsoft Outlook** do wysyłki. Jeśli nie jest zainstalowany / zalogowany → dzwoń do Edyty (IT).
- **"PDF w załączniku wygląda dziwnie / brak danych"** → Sprawdź czy wypełniłaś wszystkie 8 pól. Jeśli któreś puste, PDF wyświetli puste pole.
- **"Hodowca odpowiada że cena niezgodna z umową"** → Zatrzymaj, **dzwoń do Asi**. Specyfikacja musi się **zgadzać z aktywną umową** (#6).
- **"Zapomniałam zaznaczyć Wariantu A i wpisałam nową od zera 30 minut"** → Następnym razem kopiuj. Tej nie martw się, zapisz.

---

## 📞 Do kogo dzwonić

| Problem | Osoba |
|---|---|
| Nie wiem jaką cenę wpisać | **Asia** (zgodność z umową) lub **Ser** |
| Hodowca nie ma maila | **Tereska** / **Asia** |
| Outlook nie chce się otworzyć | **Edyta** (IT) |
| PDF wygląda dziwnie / nie generuje się | **Ser** (problem z iTextSharp) |
| Hodowca odbija mail "Nie znam tego adresu" | Zmień email hodowcy → **Asia** zatwierdza wniosek zmiany |
| Specyfikacja niezgodna z umową | **Asia** — koniecznie |

---

## ✅ Skąd wiesz że zrobiłaś dobrze

- Specyfikacja **pojawiła się w głównym DataGrid** z dzisiejszą datą i Twoim imieniem (avatar).
- **Outlook Sent pokazuje wysłany mail** z PDF w załączniku.
- Hodowca **nie skarży się** że cena niezgodna z umową.
- W kalendarzu dostaw (instrukcja #4 / kafelek Kalendarz) widać że dostawa jest **zaplanowana zgodnie ze specyfikacją**.

---

## 🔧 Mała ściągawka — pola Magdy

| Pole | Co wpisać | Typowo |
|---|---|---|
| Hodowca | z listy | wybór |
| Data od / Data do | zakres ważności | tydzień / 2 tygodnie |
| Sztuki | przewidywana liczba | 25-40 tys |
| Waga deklarowana | kg surowca | hodowca podaje |
| Szt/Poj | sztuki na paletę | **264** (duży) / więcej (mały) |
| Cena | zł/kg | z umowy |
| Ubytek % | z umowy | 3% |
| Status | Aktywna | tak |

---

## 🔧 Czego ZPSP NIE obsługuje (dziś)

> **[BRAK W ZPSP — DO DODANIA]** Brak przycisku **"📋 Skopiuj z poprzedniej dla tego hodowcy"** (jednym klikiem clone+edit). Dziś trzeba zaznaczyć w DataGrid, prawy klik, szukać "Kopiuj jako nowa" albo robić od nowa.
>
> *Workaround na teraz:* w DataGrid głównym wyszukać poprzednią + prawy klik (sprawdź czy jest opcja kopiowania).
>
> *Planowane:* duży przycisk **"📋 Skopiuj z poprzedniej"** w toolbar (Część 2 audytu, RANK A1 — priorytet 1 dzień pracy Sera). Magda 80% przypadków = jeden klik + edycja dat.
