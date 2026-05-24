# Część 5 — Quick wins na weekend 24–25.05.2026 (przed Magdą)

**Cel:** w sobotę i niedzielę zrobić **minimum żeby Magda w poniedziałek miała łatwiej** — bez ryzykownych zmian, bez tykania plików-monstrów (Specyfikacje 16k, Kalendarz 9,6k, Wstawienia 5,3k).

**Reguła #1:** Każdy quick win ≤ 2h. **Jak coś trwa dłużej — przerwij, zostaw na czerwiec.**
**Reguła #2:** **Tylko zmiany w XAML / małe metody / SQL inserts.** Bez refactor-ów, bez przerabiania monstrów.
**Reguła #3:** **Zbuduj i przetestuj każdą zmianę osobno** (build + uruchom + kliknij). Nie pakuj 5 zmian w jedno `dotnet build`.

---

## 🎯 Lista 9 quick wins (priorytet → czas)

| # | Co | Plik | Czas | Pilność | Ryzyko |
|---|---|---|---:|---|---|
| **QW1** | Ukryć martwy kafelek "Zakup Paszy i Piskląt" | `Menu.cs:1537-1540` | 5 min | 🔴 KRYT | brak |
| **QW2** | Utworzyć folder sieciowy potwierdzeń wstawień | sieć | 5 min | 🔴 KRYT | brak |
| **QW3** | Założyć login ZPSP dla Magdy + reset hasła | SQL `operators` | 10 min | 🔴 KRYT | brak |
| **QW4** | Założyć login Symfonii dla Magdy | Sage | 15 min | 🔴 KRYT | brak |
| **QW5** | Wgrać avatar Magdy do `\\server\Avatary\` | folder sieciowy | 5 min | 🟡 ŚR | brak |
| **QW6** | Walidator sztuk wstawienia (0–250 000) | `WstawienieWindow.xaml.cs` | 1h | 🟡 ŚR | małe |
| **QW7** | Banner read-only w Płatnościach + filtr "tylko zaległe" | `Platnosci.cs` | 1h | 🟡 ŚR | małe |
| **QW8** | Banner "Klikni 'Skopiuj z poprzedniej'..." w Specyfikacjach (tylko tekst, bez przycisku!) | `WidokSpecyfikacje.xaml` (XAML only!) | 30 min | 🟢 NICE | małe |
| **QW9** | Wydruk 4 instrukcji + segregator + długopis na biurku Magdy | drukarka | 1h | 🔴 KRYT | brak |

**Razem: ~5h pracy Sera + 1h drobiazgów.**
Plus opcjonalnie 1-2h "ulubione bannery" jeśli czas pozwoli.

---

## 📋 Szczegóły każdego quick wina

### QW1 — Ukryć martwy kafelek "Zakup Paszy i Piskląt" (5 min)

**Problem:** Kafelek **wisi w UI** ale `FormFactory=null` — Magda zobaczy "Pasza" i zapyta "a co to?". Po kliknięciu **nic się nie dzieje** (Menu.cs:2242 sprawdza `if FormFactory != null`).

**Rozwiązanie:** zakomentować cały blok `MenuItemConfig` aż do implementacji w Q3.

**Plik:** `Menu.cs` linie 1537-1540
**Snippet:**
```csharp
// Menu.cs:1537-1540 — ZAKOMENTOWANE 2026-05-24 do czasu implementacji (Część 5 audytu)
// new MenuItemConfig("ZakupPaszyPisklak", "Zakup Paszy i Piskląt",
//     "Ewidencja zakupów pasz i piskląt dla hodowców kontraktowych",
//     Color.FromArgb(27, 94, 32), // Ciemny zielony #1B5E20
//     null, "🌾", "Pasza"),
```

**Uwaga:** `accessMap[01] = "ZakupPaszyPisklak"` (`Menu.cs:1303`) **NIE ruszać** — zmiana indeksów łamie permissions wszystkim. Zostawić w `_moduleAccessOrder`, tylko schować z menu.

**Walidacja:**
1. `dotnet build Kalendarz1.csproj` — bez błędów.
2. Uruchom ZPSP, zaloguj się, w kategorii ZAOPATRZENIE I ZAKUPY — **brak kafelka "Pasza"**.
3. Inne kafelki działają jak wcześniej.

**Co może pójść nie tak:** literówka w komentarzu, accidentalne wykasowanie sąsiedniego kafelka. Backup: `git diff Menu.cs` przed buildem.

---

### QW2 — Folder sieciowy potwierdzeń (5 min)

**Problem:** Instrukcja #3 mówi Magdzie żeby wrzucała screenshoty SMS-ów do `\\192.168.0.170\Public\Potwierdzenia_Wstawien\2026\`. Jeśli folderu nie ma → Magda się gubi.

**Rozwiązanie:** utwórz folder + uprawnienia + 1 plik README.

**Kroki:**
1. Otwórz Eksplorator → `\\192.168.0.170\Public\`.
2. Nowy folder: `Potwierdzenia_Wstawien`.
3. Wejdź → nowy folder `2026`.
4. W `2026` utwórz plik **`_README.txt`** z treścią:
```
FOLDER POTWIERDZEŃ WSTAWIEŃ HODOWCÓW

Nazwa pliku: NAZWA_HODOWCY_yyyy-MM-dd.png (lub .pdf)
Przykład: KOWALSKI_2026-05-24.png

Wrzucaj screenshoty SMS, kopię maila, screenshot WhatsApp.
Dla każdego wstawienia jeden plik. Asia kontroluje przy audycie ARiMR.

Pytania: Asia.
```
5. **Uprawnienia:** Edyta IT da dostęp `Modify` dla grupy ZakupZywca (Tereska, Asia, Magda, Ser).

**Walidacja:** Z komputera Magdy (po założeniu konta) otwórz `\\192.168.0.170\Public\Potwierdzenia_Wstawien\2026\` → zobacz `_README.txt` → spróbuj utworzyć testowy plik (np. `TEST.txt`) → udało się = OK, usuń test.

---

### QW3 — Login ZPSP dla Magdy (10 min)

**Problem:** Bez konta Magda nie zaloguje się w poniedziałek.

**Rozwiązanie:** wpis do `LibraNet.dbo.operators`.

**Sprawdź jakie ID są wolne:**
```sql
SELECT TOP 50 ID, Name FROM dbo.operators ORDER BY CAST(ID AS INT);
```

Wybierz wolne ID 3-5-cyfrowe (np. `4567` jeśli wolne). **Zapamiętaj — to login Magdy.**

**INSERT:**
```sql
INSERT INTO dbo.operators (ID, Name, PasswordHash, PasswordSetAt, IsAdmin)
VALUES ('4567', 'Magda Nowak', NULL, NULL, 0);
-- Name = pełne imię i nazwisko (z polskimi znakami)
-- PasswordHash = NULL → przy pierwszym logowaniu Magda ustawia hasło sama (Menu1.xaml.cs:747)
-- IsAdmin = 0 (bez uprawnień admin)
```

**Permissions (jakie kafelki Magda widzi):**
```sql
-- Sprawdź jak inni z działu zakupu mają (np. Tereska):
SELECT * FROM dbo.UserPermissions WHERE UserID = '{ID_TERESKI}';

-- Skopiuj te same permissions dla Magdy
INSERT INTO dbo.UserPermissions (UserID, ModuleIndex, HasAccess)
SELECT '4567', ModuleIndex, HasAccess
FROM dbo.UserPermissions
WHERE UserID = '{ID_TERESKI}';
```

**Walidacja:**
1. Z komputera testowego: login `4567` → "ZALOGUJ SIE" → otwiera dialog "Ustaw nowe hasło".
2. Ustaw testowe hasło `test1234` → klik "Ustaw hasło" → otwiera menu główne.
3. W menu widać te same kafelki co Tereska.
4. **W poniedziałek Magda przy Tobie zmieni hasło — wyzeruj jej hash:**
```sql
UPDATE dbo.operators SET PasswordHash = NULL, PasswordSetAt = NULL WHERE ID = '4567';
```

**Karteczka dla Magdy:**
> Login do ZPSP: **4567**
> Hasło: pierwsze logowanie sama ustawisz (system poprosi).
> Komputer: stanowisko Pauliny (na razie).

---

### QW4 — Login Symfonii dla Magdy (15 min)

**Problem:** Magda potrzebuje Symfonii do instrukcji #8 i #9.

**Rozwiązanie:** zakładasz nowego użytkownika w Symfonii HANDEL.

**Kroki (z głowy — Tereska Ci pomoże jeśli się gubisz):**
1. Otwórz Symfonię HANDEL jako admin.
2. **Administracja → Użytkownicy → Nowy**.
3. Login: `magda` lub `nowak.m` (Twoja konwencja).
4. Hasło tymczasowe: `Pierwszy_2026!` (Magda zmieni przy pierwszym logowaniu).
5. **Uprawnienia:** skopiuj z Tereski (czyta faktury Zakupów, edytuje, dodaje nowe faktury). **Nie nadawaj uprawnień admin** ani uprawnień do sprzedaży.
6. Zapisz.

**Karteczka dla Magdy:**
> Symfonia HANDEL — login: **magda** (lub jak ustawiłeś)
> Hasło tymczasowe: `Pierwszy_2026!` — zmień przy pierwszym logowaniu.
> Skrót Symfonii: pulpit Magdy.

**Walidacja:** loguj się z komputera testowego, sprawdź czy widzisz Zakupy + Kontrahentów.

---

### QW5 — Avatar Magdy (5 min)

**Problem:** W ZPSP login screen pokazuje avatary ostatnich osób (Menu1.xaml.cs:218). Bez awatara Magda zobaczy generyczne kółko z inicjałami "MN".

**Rozwiązanie:** wrzuć zdjęcie Magdy w odpowiednim formacie.

**Kroki:**
1. Poproś Magdę o zdjęcie (z jej fb / telefon) — najlepiej kwadratowe, twarz wycentrowana.
2. Przytnij do **kwadratu** (np. paint.net, IrfanView, Photopea).
3. Skala do **300×300 px** (PNG).
4. Nazwa: **`{ID_MAGDY}.png`** (np. `4567.png` jeśli login 4567).
5. Wrzuć do **`\\192.168.0.170\Install\Prace Graficzne\Avatary\`** (i backup do `\\192.168.0.171\...` jeśli synchronizowany).

**Walidacja:** zaloguj się na konto Magdy → patrz na ostatnie logowania w prawym dolnym rogu Menu1 → widać Twoje (Sera) zdjęcie + jeśli już Magda się logowała, jej zdjęcie zamiast inicjałów.

---

### QW6 — Walidator sztuk wstawienia (1h)

**Problem:** Magda może wpisać 10 milionów piskląt — system to przyjmie. Typowy kurnik to 25–40 tys.

**Rozwiązanie:** w `WstawienieWindow.xaml.cs` w handlerze `TxtSztukiWstawienia_TextChanged` (już istnieje!) dodać walidację z warningiem.

**Plik:** `Zywiec/WstawieniaKurczaka/WstawienieWindow.xaml.cs`
**Lokalizacja:** znajdź `TxtSztukiWstawienia_TextChanged` (handler już zarejestrowany w `WstawienieWindow.xaml:182`).

**Snippet (dodaj na końcu handlera):**
```csharp
// QW6 (2026-05-24) — walidacja sztuk pod Magdę
private void WalidujSztuki()
{
    if (int.TryParse(txtSztukiWstawienia.Text, out var szt))
    {
        if (szt < 1)
        {
            txtSztukiWstawienia.Background = new SolidColorBrush(Color.FromRgb(255, 235, 238)); // #FFEBEE czerwony
            txtSztukiWstawienia.ToolTip = "Liczba sztuk musi być > 0";
        }
        else if (szt > 250_000)
        {
            txtSztukiWstawienia.Background = new SolidColorBrush(Color.FromRgb(255, 243, 224)); // #FFF3E0 pomarańczowy
            txtSztukiWstawienia.ToolTip = $"Wpisałaś {szt:N0} sztuk. Typowy kurnik 25-40 tys. Sprawdź dwa razy!";
        }
        else if (szt < 5_000)
        {
            txtSztukiWstawienia.Background = new SolidColorBrush(Color.FromRgb(255, 243, 224));
            txtSztukiWstawienia.ToolTip = $"Wpisałaś {szt:N0} sztuk. Typowy kurnik 25-40 tys. Sprawdź!";
        }
        else
        {
            txtSztukiWstawienia.Background = new SolidColorBrush(Color.FromRgb(232, 245, 233)); // #E8F5E9 zielony (jak w stylu)
            txtSztukiWstawienia.ToolTip = $"OK ({szt:N0} sztuk).";
        }
    }
}
```
Wywołaj `WalidujSztuki();` w `TxtSztukiWstawienia_TextChanged` po istniejącej logice.

**Walidacja:**
1. Build → otwórz wstawienie → wpisz **100** sztuk → pomarańczowe tło + tooltip.
2. Wpisz **30000** → zielone tło.
3. Wpisz **300000** → pomarańczowe + tooltip "sprawdź dwa razy".
4. **Klucz:** Magda nadal może zapisać (nie blokujemy). To tylko warning.

**Co może pójść nie tak:** kolizja z istniejącą logiką w handlerze. Sprawdź czy nie nadpisujesz koloru w innym miejscu. Backup: `git diff WstawienieWindow.xaml.cs`.

---

### QW7 — Banner read-only w Płatnościach + filtr "tylko zaległe" (1h)

**Problem:** Magda zobaczy okno Płatności (`Platnosci.cs`) i nie będzie wiedzieć że to **read-only** (faktury idą z Symfonii). Może próbować edytować.

**Rozwiązanie:** banner u góry + chip "tylko zaległe".

**Plik:** `Platnosci.cs` (WinForms — 636 linii, legacy). Banner najprościej dodać programowo w `Platnosci_Load` (lub konstruktor) zamiast tykać Designer.

**Snippet w `Platnosci.cs` (w `Platnosci_Load` lub konstruktorze):**
```csharp
// QW7 (2026-05-24) — banner pod Magdę
var banner = new Panel
{
    Dock = DockStyle.Top,
    Height = 36,
    BackColor = System.Drawing.Color.FromArgb(255, 243, 224), // pomarańcz light
    Padding = new Padding(12, 8, 12, 8)
};
var lbl = new Label
{
    Text = "❗ TYLKO PODGLĄD — płatności idą z Symfonii. " +
           "Tu możesz tylko notować obietnice zapłaty (instrukcja Magdy #10).",
    Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold),
    ForeColor = System.Drawing.Color.FromArgb(230, 81, 0),
    AutoSize = false,
    Dock = DockStyle.Fill,
    TextAlign = System.Drawing.ContentAlignment.MiddleLeft
};
banner.Controls.Add(lbl);
this.Controls.Add(banner);
banner.BringToFront();
```

**Filtr "tylko zaległe":** jeśli starcza czasu — dodaj checkbox w nagłówku. Jeśli nie — pomiń, banner wystarczy w poniedziałek.

**Walidacja:** uruchom Płatności → widzisz pomarańczowy pasek u góry z czerwonym wykrzyknikiem.

---

### QW8 — Banner w Specyfikacjach (30 min)

**Problem:** Magda otwiera Specyfikacje (16k linii kodu!) i nie wie od czego zacząć.

**Rozwiązanie:** dodać banner XAML w samym **`WidokSpecyfikacje.xaml`** (NIE `.cs`!). Tylko tekst, bez logiki.

**Plik:** `Zywiec/WidokSpecyfikacji/WidokSpecyfikacje.xaml`
**Lokalizacja:** znajdź główny `<Grid>` (po `<Window.Resources>`) → dodaj pierwszy `<Border>` jako baner.

**Snippet (wstaw na samym początku głównego layoutu):**
```xml
<!-- QW8 (2026-05-24) — banner pod Magdę -->
<Border DockPanel.Dock="Top" Background="#E3F2FD" BorderBrush="#1976D2"
        BorderThickness="0,0,0,2" Padding="16,10">
    <TextBlock Foreground="#1976D2" FontSize="13" TextWrapping="Wrap">
        💡 <Bold>Najpierw wybierz hodowcę</Bold> z listy po lewej → zobaczysz jego poprzednie specyfikacje.
        Najszybciej: zaznacz ostatnią → prawy klik → "Kopiuj jako nowa" → zmień daty i ceny → wyślij email.
        Pełna instrukcja: <Italic>BAZA_WIEDZY\INSTRUKCJE_MAGDA\07_specyfikacja_zywca.md</Italic>
    </TextBlock>
</Border>
```

**Uwaga krytyczna:** to **zmiana w XAML, nie w `.cs`**. Plik `.cs` (16k linii) NIE TYKAĆ. XAML nie nadpisuje code-behind.

**Walidacja:** build + otwórz Specyfikacje → widzisz niebieski banner u góry. **Nic innego się nie zmieniło** (sprawdź wszystkie zakładki).

**Co może pójść nie tak:** zła pozycja w grid (DockPanel vs Grid). Sprawdź główny layout — jeśli `<Grid>` zamiast `<DockPanel>`, ustaw `Grid.Row="0"` i przesuń pozostałe wiersze.

---

### QW9 — Wydruk instrukcji + segregator (1h)

**Materiały na biurko Magdy:**

| Co | Skąd | Kopii |
|---|---|---|
| `00_INDEKS.md` (jako PDF) | `BAZA_WIEDZY/INSTRUKCJE_MAGDA/` | 1 (wpięta jako pierwsza w segregatorze) |
| `01_logowanie.md` | tam samo | 1 |
| `02_nowe_wstawienie.md` | tam samo | 1 |
| `03_potwierdzenie_wstawienia.md` | tam samo | 1 |
| `10_faq_kogo_dzwonic.md` | tam samo | 2 (1 w segregatorze, 1 na ścianę nad biurkiem) |
| Karteczka loginów (ZPSP + Symfonia + folder potwierdzeń) | napisz na ręce | 1 (w teczce-zamykanej-na-klucz!) |

**Kroki:**
1. Otwórz każdy `.md` w VSCode / Typora / wklej do Worda → eksport PDF.
2. Wydrukuj w **rozmiarze A4, dwustronnie** (papier 80g+ żeby się trzymał).
3. Segregator 2-pierścieniowy (dowolny) — dyrektor Magdy ma na biurku.
4. **Karteczka loginów** — laminowana, zamknięta w szufladzie (NIE wisi na monitorze).
5. **FAQ na ścianę:** powiesić tablica korkowa nad monitorem, FAQ przypięte pinezką.
6. Długopis + post-ity (Magda będzie notować pytania) + kubek z kawą.

**Walidacja:** w niedzielę wieczorem zobacz biurko Magdy z perspektywy świeżej osoby — czy wiesz **gdzie kliknąć** żeby się zalogować + co zrobić **najpierw**? Jak nie — popraw segregator/karteczkę.

---

## 📅 Plan godzinowy weekendu

### 🟦 Sobota 24.05.2026

| Godzina | Co | Effort |
|---|---|---:|
| 09:00–09:30 | **Kawa + plan** — przeczytać tę Część 5 + listę quick winów | 30 min |
| 09:30–10:00 | **QW2** Folder sieciowy potwierdzeń (z Edytą jeśli trzeba uprawnienia) | 30 min |
| 10:00–10:30 | **QW3** Login ZPSP dla Magdy (SQL insert + permissions) | 30 min |
| 10:30–11:00 | **QW4** Login Symfonii dla Magdy | 30 min |
| 11:00–11:30 | **QW5** Avatar (z Magdy zdjęcia) | 30 min |
| 11:30–12:30 | **QW1** Ukryć kafelek Pasza + build + test | 1h |
| 12:30–13:30 | **OBIAD** | — |
| 13:30–14:30 | **QW6** Walidator sztuk wstawienia | 1h |
| 14:30–15:30 | **QW7** Banner Płatności | 1h |
| 15:30–16:00 | **QW8** Banner Specyfikacje | 30 min |
| 16:00–16:30 | **Build pełny** + ręczne testy ostatnie | 30 min |
| 16:30+ | **Commit + push** (małymi krokami: 9 commitów osobnych) | — |

**Cel sobotni:** kod gotowy, wszystko zbudowane, login Magdy działa.

### 🟩 Niedziela 25.05.2026

| Godzina | Co | Effort |
|---|---|---:|
| 10:00–11:00 | **QW9 część 1** — eksport instrukcji do PDF + wydruk | 1h |
| 11:00–12:00 | **QW9 część 2** — segregator + karteczka + tablica korkowa | 1h |
| 12:00–13:00 | **Test świeżego oka** — usiądź przy biurku Magdy, zaloguj się jako Magda (z karteczki) → przejdź instrukcję #1 → instrukcję #2 → instrukcję #3. **Co Cię gubi? Popraw.** | 1h |
| 13:00–14:00 | **OBIAD** | — |
| 14:00–14:30 | **Komunikat dla zespołu** — wyślij na grupę (WhatsApp / Teams): "Magda przychodzi w poniedziałek. Plan: Tereska wita o 8:00, instrukcja #1 logowanie, potem #2 pierwsze wstawienie. Asia obok przez cały dzień. Pytania → mnie." | 30 min |
| 14:30+ | **Niedziela odpoczynek** ✅ | — |

---

## ⛔ Czego NIE robić w weekend (pokusy do oporu)

| Pokusa | Dlaczego NIE |
|---|---|
| "Dorobię szybko przycisk 'Skopiuj z poprzedniej' w Specyfikacjach" | 16k linii kodu. Jeden mały błąd = okno padnie. Magda zostanie bez specyfikacji w poniedziałek. **NIE dotykaj `.cs` plików > 5k linii.** |
| "Naprawię TODO drukowania PDF w Kalendarzu" | 9,6k linii. To samo. **Czerwiec.** |
| "Refactor MVVM dla Wstawień" | 5,3k linii. **Nigdy w weekend.** |
| "Zrobię od razu schema Kontraktów" | Część 4 to ~3 tygodnie pracy. Nie zaczyniaj — zniechęcisz się. |
| "Dodam Centrum Asi" | 3-4 dni roboczych. Czerwiec. |
| "Włączę SMSAPI bramkę" | Konto SMSAPI trzeba zamówić, czekać na weryfikację. Nie zdążysz w 48h. |
| "Naprawię hardcoded conn stringi" | 15 plików dotkniętych. Sobota = 100% szans regresji. **Q3.** |
| "Wpiszę 100 hodowców do tabeli Kontrakty" | Migracja to Część 4. Asia musi przygotować Excel. **Nie sam.** |

**Reguła #4:** weekend to dziewięć QUICK winów po max 1h. **Nic więcej.**

---

## ✅ Checklist niedzielny — co MUSI działać przed poniedziałkiem

- [ ] Magda ma **login ZPSP** (`4567` lub jaki założyłeś) z `PasswordHash = NULL`
- [ ] Magda ma **login Symfonii** (z hasłem tymczasowym)
- [ ] Magda ma **stanowisko** (komputer Pauliny lub nowe), monitor działa
- [ ] **Folder potwierdzeń** istnieje i ma uprawnienia
- [ ] **Avatar** Magdy w folderze sieciowym (opcjonalnie — bez nie zadziała inicjałami)
- [ ] **Kafelek "Pasza"** ZNIKNIĘTY z menu (build + test)
- [ ] **Walidator sztuk** działa (test 100, 30000, 300000)
- [ ] **Banner Płatności** widoczny (pomarańczowy)
- [ ] **Banner Specyfikacje** widoczny (niebieski)
- [ ] Wszystkie zmiany **zacommitowane + zbudowane**
- [ ] **Segregator** na biurku Magdy (5 wydruków: index, 01, 02, 03, 10)
- [ ] **FAQ** wisi nad monitorem
- [ ] **Karteczka loginów** w szufladzie (zamknięta na klucz)
- [ ] **Komunikat** poszedł do zespołu (Tereska, Asia, Justyna, Edyta wiedzą)
- [ ] **Plan dnia poniedziałkowego** napisany dla siebie (8:00 — wita Tereska, 9:00 — pierwsze logowanie z Tobą obok)

---

## 🎬 Pierwszy dzień Magdy — poniedziałek 26.05.2026 (przygotowanie z perspektywy Sera)

**Twój plan godzinowy:**

| Godzina | Co | Kto |
|---|---|---|
| 07:30 | Wejdź do biura przed wszystkimi, sprawdź biurko Magdy ostatni raz | Ty |
| 08:00 | Tereska wita Magdę przy drzwiach, prowadzi do biurka | Tereska |
| 08:15 | Magda siada, dostaje kawę, otwiera segregator | Magda + Tereska |
| 08:30 | **Pierwsze logowanie ZPSP** (z karteczki) — Ty obok | Ty + Magda |
| 08:45 | **Ustawienie hasła Magdy** (z #1) | Magda |
| 09:00 | Pokaż menu główne — kategoria ZAOPATRZENIE I ZAKUPY | Ty |
| 09:30 | Tereska wraca, zaczynają instrukcję #2 (nowe wstawienie) razem | Tereska + Magda |
| 10:00 | Ty wracasz do swojej pracy. Magda na sygnale. | Ty |
| 12:00 | **Lunch z Asią** — okazja na pierwsze pytania | Magda + Asia |
| 14:00 | Magda otwiera FAQ (#10) sama, sprawdza co jest, gdzie | Magda |
| 16:00 | **Krótkie spotkanie 15 min** — "jak Ci minął dzień, co Cię gubiło, co poprawić" | Ty + Magda |
| 16:30 | Magda zostaje albo idzie do domu | — |

**Co Magda powinna umieć po pierwszym dniu (success metric):**
- ✅ Zalogować się sama bez pomocy
- ✅ Otworzyć kafelek Cykle Wstawień + Kalendarz Dostaw
- ✅ Wiedzieć **do kogo dzwonić** gdy nie wie (FAQ czytała 2× w ciągu dnia)
- ✅ Mieć **3-4 pytania zapisane** na liście do Sera/Asi na kolejny dzień (jeśli ma — to dobrze, to znaczy że obserwuje)

**Czego Magda NIE musi umieć po pierwszym dniu:**
- ❌ Generować specyfikacji samodzielnie
- ❌ Wpisywać faktur w Symfonii
- ❌ Wysyłać planów do AviLogu
- ❌ Tworzyć umów

To wszystko **tydzień 1-2** (z checklistą w `00_INDEKS.md`).

---

## 📌 PODSUMOWANIE CZĘŚCI 5

**Total weekend effort:**
- **~5h** Sera w sobotę (kod + konta + foldery)
- **~3h** Sera w niedzielę (druki + segregator + test świeżego oka + komunikat)
- **~1h** Edyty IT (uprawnienia folder + Symfonia login)
- **~1h** Asia/Tereska (zdjęcie Magdy, podpowiedź miejsca biurka)

**Razem ~10h pracy weekendu na 3-4 osoby.**

**Po weekendzie poniedziałek 26.05.2026 to:** dzień onboardingu, nie dzień testów ZPSP. **Magda i Tereska skupione na pracy, nie na walce z bugami.**

**Niedzielnym wieczorem — odeślij na drobiarstwo:** *"Wszystko gotowe. Magda przyjdzie jutro. Spokojnej nocy."* ✅
