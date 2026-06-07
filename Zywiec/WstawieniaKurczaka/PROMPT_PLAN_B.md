# PROMPT DLA CLAUDE WEB — PLAN B + DODATKI

> Jeśli pierwszy prompt (`PROMPT_DO_CLAUDE_WEB.md`) jest zbyt długi lub Claude Web odetnie odpowiedź — masz tu kompresowaną wersję + plan jak iść dalej iteracyjnie.

---

## 📋 WERSJA SKRÓCONA (jeśli pełna za długa)

Skopiuj tylko to:

```
Jesteś senior UX/UI designer aplikacji biznesowych WPF .NET 8 dla ubojni drobiu (Piórkowscy, 258M obrotu, 200t/dzień).

Pracownik docelowy: "Teresa" — zakupowiec żywca, 42 lata, codziennie 7-17, monitor 1920×1080, kontaktuje się z 200 hodowcami.

Aplikacja "Cykle Wstawień" ma 3 okna:
1. PANEL GŁÓWNY (4 panele: Lista wstawień / Przypomnienia / Nadchodzące do potwierdzenia / Historia kontaktów) — załączam screenshot
2. MODYFIKACJA WSTAWIENIA (formularz + tabela Szablon Dostaw) — załączam screenshot
3. SZCZEGÓŁY WSTAWIENIA (dialog z danymi z ważenia palet LibraNet)

Paleta firmowa: #5C8A3A zielony, #3498DB niebieski, #E74C3C czerwony, #F39C12 żółty, fontu Segoe UI. Czechcoholike.

Stack: WPF .NET 8 code-behind (nie MVVM), DevExpress, LiveCharts, ClosedXML dostępne.

Już zaimplementowane (zachowaj):
- 8 wariantów SMS-ów + skróty klawiszowe S/F/R/Enter
- ⭐ ikona stałego klienta
- Auto-snooze 3 dni po SMS o potwierdzenie
- Auto-wpis do ContactHistory po SMS
- Status bar dolny z licznikami
- Menu kontekstowe z 15+ akcjami

CHCĘ od Ciebie:
- 10 koncepcji wizualnych dla PANELU GŁÓWNEGO
- 10 dla OKNA MODYFIKACJI WSTAWIENIA
- 10 dla DIALOGU SZCZEGÓŁY

Każda koncepcja: nazwa, ASCII mockup, mikrocopy po polsku, nowe elementy WPF, paleta HEX, mikrointerakcje, dlaczego działa dla Teresy, zagrożenia.

Na końcu: REKOMENDACJA top 3 z każdej + roadmapa wdrożenia.

Pisz po polsku, konkretnie (nie "nowoczesny", "intuicyjny"), z polskimi danymi testowymi ("Słąbkowska Agnieszka 50 000 szt 28.06.2026").

Po prostu zacznij. Nie pytaj o pozwolenie. Nie skracaj. Jeśli zabraknie miejsca w 1 wiadomości — kontynuuj w kolejnej.
```

---

## 🎯 FOLLOW-UP PYTANIA DO CLAUDE WEB

Po wstępnej odpowiedzi możesz pogłębić któryś z aspektów. Skopiuj któreś:

### Jeśli koncepcje są ogólnikowe
> "Wybierz koncepcję A3 i rozwiń ją do pełnej specyfikacji: dokładne wymiary px wszystkich elementów, każdy kolor HEX, każdy font-size, każdy padding/margin, animacje (czas trwania ms, easing function), wszystkie stany interaktywne (default/hover/focus/active/disabled). Stwórz tabele 'element → właściwość → wartość'. Zachowaj się jak design system."

### Jeśli chcesz głębszy przykład
> "Dla koncepcji A1 (Command Center 2026) — wygeneruj dokładny XAML WPF dla głównego layoutu (Grid, kolumny, Border, Style w Window.Resources). Zachowaj zasady projektu: code-behind nie MVVM, polskie znaki, paleta firmowa. Dam to bezpośrednio do projektu."

### Jeśli brakuje empatii do persona
> "Wciel się w Teresę. Opisz JEJ dzień (7:00-17:00) godzina po godzinie, używając Twojego nowego designu zamiast obecnego. Pokaż gdzie obecna aplikacja ją frustrowała i jak Twój redesign to rozwiązuje. Konkretne sytuacje (telefon dzwoni, hodowca pyta o cenę, etc.)."

### Jeśli potrzebne więcej akcji
> "Dodaj sekcję D: 10 koncepcji NOWYCH okien które obecnie nie istnieją ale powinny: Dashboard analityka tygodnia / Mapa hodowców / Kalendarz miesiąca / Centrum komunikacji / Workspace AI assistant / itd. Każde z ASCII mockup."

### Jeśli chcesz konkretu pod implementację
> "Dla koncepcji B5 (twoja Top 1 dla Modyfikacji) — wygeneruj kompletny XAML + minimalne code-behind. Zachowaj zgodność z `WstawienieWindow.xaml.cs` (interface właściwości UserID, Dostawca, LpWstawienia, DataWstawienia, SztWstawienia, Modyfikacja). Mam to skopiować bezpośrednio do projektu."

### Jeśli chcesz wybrać między 2 koncepcjami
> "Porównaj koncepcję A1 vs A7. Tabela: kryterium / A1 / A7 / wygrana. Kryteria: czas nauki Teresy, kliknięcia na 1 akcję, czytelność w stresie, koszt implementacji, ryzyko regresji, długoterminowa skalowalność. Wskaż wygraną i 3 elementy A7 do zaszczepienia w A1."

---

## 🏁 CO ZROBIĆ Z WYNIKIEM

### Krok 1: Skopiuj odpowiedź Claude Web do mnie
Jak wrócisz tutaj (Claude Code), wklej **całą odpowiedź** z claude.ai jako tekst. Bez skracania.

### Krok 2: Wskaż co wybierasz
Napisz coś typu:
- *"Podoba mi się A3 (Calm Workspace) + B7 (Conversational UI) + C2 (Storytelling)"*
- Lub: *"Daj mi mockupy A1, A5, A8 razem na 1 obrazku do porównania"*

### Krok 3: Iteracyjna implementacja
Implementację dzielimy na 3 etapy:

**Etap 1 — Panel Główny (5-8h pracy)**
- Refaktor `WidokWstawienia.xaml` na nowy layout
- Migracja istniejących funkcji (SMS-y, skróty, status bar)
- Backward-compat z obecnym SQL-em

**Etap 2 — Modyfikacja Wstawienia (3-5h)**
- Refaktor `WstawienieWindow.xaml`
- Zachowanie istniejących właściwości

**Etap 3 — Szczegóły + drobne (2-3h)**
- Nowy dialog
- Polishing

### Krok 4: Git branch
Przed implementacją utworzę nowy branch `feature/redesign-cycle-wstawien` żeby nie ruszać main.

---

## 🛡️ JAKBY CLAUDE WEB ODMÓWIŁ / SKRÓCIŁ

**Problem:** Claude Web po wygenerowaniu ~5000 słów może powiedzieć *"to za długie"* lub uciąć w połowie.

**Rozwiązanie:** Podziel zamówienie na 3 osobne wiadomości:

**Wiadomość 1:**
```
Daj mi 10 KONCEPCJI dla PANELU GŁÓWNEGO (sekcja A z mojego promptu).
Każda: nazwa, ASCII mockup, paleta, mikrocopy, dlaczego.
Min. 400 słów per koncepcja.
```

**Wiadomość 2 (po Etapie 1):**
```
Świetnie. Teraz daj mi 10 KONCEPCJI dla OKNA MODYFIKACJI WSTAWIENIA (sekcja B).
Pamiętaj kontekst Teresy z poprzedniej wiadomości.
```

**Wiadomość 3:**
```
Teraz 10 KONCEPCJI dla DIALOGU SZCZEGÓŁY (sekcja C) + REKOMENDACJA top 3 z każdej sekcji.
```

---

## 📤 PLIKI DO ZAŁĄCZENIA W CZACIE (priorytet od góry)

1. ✅ `screenshot_panel_glowny.png` (must-have)
2. ✅ `screenshot_modyfikacja_wstawienia.png` (must-have)
3. ✅ `WidokWstawienia.xaml` (480 linii, mieści się jako tekst)
4. ✅ `WstawienieWindow.xaml`
5. ⏳ `CLAUDE.md` (kontekst firmy)
6. ⏳ Wybrany fragment `WidokWstawienia.xaml.cs` (najważniejsze metody Setup*Columns)

**Tip:** Jeśli claude.ai limituje upload — wklej XAML jako tekst w wiadomości w bloku kodu (\`\`\`xml ... \`\`\`).

---

## 🧪 TESTOWY PIERWSZY PROMPT (sanity check)

Najpierw zapytaj Claude Web krótko żeby sprawdzić czy ma kontekst:

```
Mam aplikację WPF dla ubojni drobiu — chcę zrobić redesign 3 okien.
Załączam screenshoty i XAML.
Czy rozumiesz konwencje branżowe (wstawienie, hodowca, dostawca, sztuki, kurczak, dostawa)?
Powiedz w 2-3 zdaniach co rozumiesz przed dużym zadaniem.
```

Jeśli odpowie sensownie — wklejaj główny prompt. Jeśli się myli — popraw kontekst pierwszą wiadomością.
