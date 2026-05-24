# 🎨 Splash aktualizacji ZPSP — pełna konfiguracja

Splash launchera jest **w 100% konfigurowalny** przez plik `theme.json` na QNAP.
**Zmieniasz JSON → następna aktualizacja u pracownika widzi nowe ustawienia. BEZ rekompilacji.**

---

## 📂 STRUKTURA NA QNAP

```
\\192.168.0.170\Install\Kalendarz1L\Launcher\Assets\
├── theme.json              ← KONFIG (rozmiary, kolory, teksty)
├── logo.png                ← Twoje logo (auto fallback gdy brak)
└── memes\                  ← Folder z memami (carousel co 5s)
    ├── mem01.jpg
    ├── mem02.png
    ├── mem03.gif
    └── ... (dowolnie ile)
```

---

## 🚀 PIERWSZE WGRYWANIE (10 min)

### Krok 1 — Stwórz strukturę
Otwórz w Eksploratorze:
```
\\192.168.0.170\Install\Kalendarz1L\Launcher\
```

Stwórz:
- Folder `Assets`
- W nim folder `memes`

### Krok 2 — Wgraj `theme.json`
Skopiuj plik:
```
C:\Users\PC\source\repos\Grafpl\Kalendarz1\DEPLOY\theme.json.example
```
na:
```
\\192.168.0.170\Install\Kalendarz1L\Launcher\Assets\theme.json
```
(zmień nazwę z `.example` na samo `.json`)

### Krok 3 — Wgraj logo
- Skopiuj `C:\Users\PC\source\repos\Grafpl\Kalendarz1\logo-2-green.png`
- Wklej do `\\192.168.0.170\Install\Kalendarz1L\Launcher\Assets\`
- **Zmień nazwę na `logo.png`** (małymi literami!)

**Jeśli nie wgrasz logo** — launcher wygeneruje **fallback** (zielone kółko z literą "P") — splash nadal działa.

### Krok 4 — Wgraj memy
Wrzuć dowolne pliki .jpg/.png/.gif do `Assets\memes\`:
- Minimum 2 sztuki (jeden się nie zmienia)
- Format 16:9 najlepszy (1280×720)
- Maks 10 MB na plik (większe są pomijane z logiem)

---

## ⚙️ EDYCJA `theme.json` — WSZYSTKIE PARAMETRY

Wszystko jest **z polskimi komentarzami** w `theme.json.example`. Otwórz Notepad++ albo Visual Studio Code i edytuj na QNAP bezpośrednio.

### Najczęstsze zmiany:

#### Większe okno
```json
"WindowWidth": 1200,
"WindowHeight": 900
```

#### Większe logo
```json
"LogoSize": 140,
"LogoMargin": 32
```

#### Inny tytuł
```json
"Title": "AKTUALIZACJA KALENDARZ",
"Subtitle": "Najlepszy program na swiecie"
```

#### Niebieskie zamiast zielone
```json
"TopBarGradientStart": "#3B82F6",
"TopBarGradientEnd": "#1E40AF",
"DotActiveColor": "#3B82F6",
"ProgressBarFgColor": "#3B82F6",
"ProgressBarFgColorTop": "#60A5FA"
```

#### Memy co 3 sekundy (zamiast 5)
```json
"MemeRotationSeconds": 3
```

#### Wyłącz ETA "Pozostalo XX sek"
```json
"ProgressBarShowEta": false
```

#### Wyłącz click na mem
```json
"MemeClickToSkip": false
```

### Sekcje (po `_section_*`)
1. **OKNO** — rozmiar + tło + obramowanie
2. **TOPBAR** — pasek u góry z logo + tytułem
3. **LOGO** — logo Piórkowscy
4. **TEKSTY** — tytuł + podtytuł
5. **MEMY** — carousel
6. **KROPKI** — wskaźnik aktywnego mema
7. **OSTRZEŻENIE** — "NIE KLIKAJ NICZEGO"
8. **PROGRESS** — pasek postępu
9. **DÓŁ** — licznik plików + nazwa pliku
10. **STOPKA** — info na dole

---

## 🎨 FUNKCJE SPLASH

### ✨ Smooth progress bar
Pasek **animuje się płynnie** (60 FPS interpolation) do nowej wartości — zamiast skakać.
Wyłącz: `"ProgressBarSmooth": false`

### ⏱️ ETA "Pozostalo XX sek"
Po 5+ skopiowanych plikach automatycznie liczy ile zostało.
Wyłącz: `"ProgressBarShowEta": false`

### 👆 Click-to-skip
Klik na mem = następny natychmiast (easter egg).
Wyłącz: `"MemeClickToSkip": false`

### 🖼️ Fallback logo
Jeśli `logo.png` nie istnieje — launcher narysuje sam (zielone kółko z "P").

### 📝 Log błędów
Każde uruchomienie launchera dopisuje do:
```
%LOCALAPPDATA%\ZPSP\launcher.log
```
Możesz tam sprawdzić co się działo (np. które memy zostały pominięte).

---

## 🛡️ BULLETPROOF — co już się NIE zepsuje

Launcher jest odporny na:

| Problem | Co robi launcher |
|---|---|
| Brak `theme.json` | Używa wbudowanych defaults |
| Uszkodzony `theme.json` | Loguje warn, używa defaults |
| Brak logo.png | Generuje fallback z literą "P" |
| Brak folderu memes\ | Pokazuje placeholder z instrukcją |
| Uszkodzony mem | Loguje i pomija, ładuje pozostałe |
| Mem > 10 MB | Loguje warn i pomija |
| Brak QNAP | Tryb offline — odpala lokalną kopię |
| Plik zablokowany | Pomija, kopiuje resztę |
| Niewłaściwy kolor (#XYZ) | Używa fallback (np. czarny) |
| Crash przy zmianie mema | Loguje i jedzie dalej |

---

## 📋 PRZYKŁADY MEMÓW DO WGRANIA

### Branżowe drobiarskie:
- Kurczak biegający (GIF)
- "Polska drób > Mercosur" (z flagą)
- Wykres "200 ton dziennie"
- "Linia ubojowa 7500 szt/h"

### Firmowe:
- Zdjęcia zespołu
- Hala z lotu ptaka
- Logo Piórkowscy w stylu rocznicowym
- Marcin w masarni Zgierz

### Motywacyjne:
- "Trzymaj sie, juz prawie!"
- "Kurczak bylby dumny!"
- "Im wieksza paleta tym wiekszy zysk"

**Tip:** Po prostu zapisz memy z Facebook / Reddit / WhatsApp i wrzuć do folderu — launcher wszystko obsłuży.

---

## 🔄 JAK SZYBKO ZOBACZYĆ ZMIANY (TY)

1. Zmień `theme.json` na QNAP (Notepad++ → zapisz)
2. U siebie: usuń `%LOCALAPPDATA%\ZPSP\Assets\theme.json` żeby wymusić ponowne ściągnięcie
3. Odpal launcher (skrót ZPSP) → splash z nowymi ustawieniami

LUB prościej: poczekaj na następną aktualizację ZPSP — Assets synchronizują się automatycznie.

---

## 🚨 TROUBLESHOOTING

### "Nie widać zmian w theme.json"
- Launcher cache'uje lokalnie — sprawdź `%LOCALAPPDATA%\ZPSP\Assets\theme.json`
- Usuń cache i odpal launcher ponownie

### "Splash crashuje przy memie"
- Sprawdź `%LOCALAPPDATA%\ZPSP\launcher.log`
- Mem może być uszkodzony — usuń z QNAP

### "Logo nie wyświetla się"
- Sprawdź czy plik nazywa się **dokładnie** `logo.png` (małe litery)
- Sprawdź format — PNG działa najlepiej

### "Kolory wyglądają dziwnie"
- Format hex MUSI być `#RRGGBB` lub `#AARRGGBB`
- Bez `#` lub w innej długości — fallback

### "Carousel nie rotuje"
- Trzeba minimum 2 memy
- Sprawdź `MemeRotationSeconds` w theme.json (musi być >= 2)

---

## 📞 W razie problemów

Sprawdź `%LOCALAPPDATA%\ZPSP\launcher.log` — pierwsze 10 ostatnich linii zwykle wystarczy żeby zlokalizować problem.

**Powodzenia!** 🐔
