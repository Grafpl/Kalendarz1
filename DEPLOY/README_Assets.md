# 🖼️ Memy i Logo w splashu aktualizacji ZPSP

Launcher pokazuje **carousel memów** (jak Steam) podczas aktualizacji.
Sergiusz może dodawać/zmieniać memy **bez rekompilacji** — po prostu wrzucasz pliki na QNAP.

---

## 📂 Lokalizacja na QNAP

```
\\192.168.0.170\Install\Kalendarz1L\Launcher\Assets\
├── logo.png              ← LOGO PIORKOWSCY (top-left splash)
└── memes\                ← FOLDER Z MEMAMI (rotacja co 5s)
    ├── mem01.jpg
    ├── mem02.png
    ├── kurczak.gif
    ├── jola_smieje_sie.jpg
    └── ... (dowolnie ile chcesz)
```

---

## 🚀 PIERWSZE WGRYWANIE

### Krok 1 — Stwórz strukturę folderów

Otwórz w Eksploratorze:
```
\\192.168.0.170\Install\Kalendarz1L\Launcher\
```

Stwórz nowy folder **`Assets`**, w nim folder **`memes`**.

### Krok 2 — Wrzuć logo

Logo:
- **Plik:** `logo.png` (TAK MUSI SIĘ NAZYWAĆ)
- **Lokalizacja:** `\\192.168.0.170\Install\Kalendarz1L\Launcher\Assets\logo.png`
- **Rozmiar:** najlepiej 200×200 px lub większy, format PNG z przezroczystym tłem
- **Skąd wziąć:** masz już w repo `C:\Users\PC\source\repos\Grafpl\Kalendarz1\logo-2-green.png` lub `Logo.png` — skopiuj jeden z nich, zmień nazwę na `logo.png`

### Krok 3 — Wrzuć memy

W `\\192.168.0.170\Install\Kalendarz1L\Launcher\Assets\memes\` wrzuć dowolną liczbę zdjęć:

**Akceptowane formaty:** `.jpg`, `.jpeg`, `.png`, `.gif`

**Rekomendowane rozmiary:**
- Najlepiej **16:9** (np. 1280×720, 1920×1080)
- Minimum 480×270 px (wymiar widoku w splashu)
- Maksimum sensowny: 1920×1080 (większe = wolniejsze ładowanie)

**Nazewnictwo:** dowolne — kolejność jest **losowa przy każdym uruchomieniu**

---

## 🎯 PROPOZYCJE MEMÓW (do wrzucenia)

### Branżowe drobiarskie:
- Kurczak biegający (GIF) — coolasy classic
- Mem z kurczakiem-szefem "Ja kontroluje wszystko"
- "Drogie kurczaki, dziękujemy za 30 lat służby" (logo Piórkowscy)
- "Linia ubojowa 7500 szt/h" (wykres growth)
- Wykres "Polski drób na podboj swiata"

### Firmowe / wewnętrzne:
- Zdjęcie zespołu z imprezy firmowej
- Zdjęcie hali z lotu ptaka
- Logo Piórkowscy w stylu rocznicowym
- Marcin (brat) w kuchni masarni
- Justyna kontrolujaca kurczaki

### Motywacyjne / żartobliwe:
- "Tylko spokojnie, juz prawie!"
- "Kurczak by byl dumny!"
- "Polska drób > Mercosur" (z polską flagą)
- "Im wieksza paleta tym wiekszy zysk"

**Tip:** Możesz po prostu wrzucić **screenshoty z Memów Internetowych** zapisanych z Facebooka, Reddita, 9GAG itp. — launcher wszystko obsłuży.

---

## ⚙️ JAK TO DZIAŁA

### Synchronizacja
1. **Pracownik klika skrót ZPSP**
2. Launcher startuje
3. **Najpierw kopiuje Assets** z QNAP do lokalnego cache `%LOCALAPPDATA%\ZPSP\Assets\` (~2-5 sekund, kilka MB)
4. Otwiera się splash z logo + losowym memem
5. Pasek postępu pokazuje aktualizację ZPSP
6. **Co 5 sekund** zmienia się mem (z dots indicator na dole)
7. Po skończeniu — uruchamia się ZPSP

### Co widzi pracownik:
```
+----------------------------------------------------+
|  [LOGO]   AKTUALIZACJA ZPSP                       |
|           Piorkowscy - Mistrz Drobiarstwa od 1996  |
+----------------------------------------------------+
|                                                    |
|        +-----------------------+                   |
|        |                       |                   |
|        |    [MEM #3]           |                   |
|        |    (losowy z folderu) |                   |
|        |                       |                   |
|        +-----------------------+                   |
|         o o O o o   (dots = mem 3 of 5)            |
|                                                    |
|  ⚠ PROSZE NIE KLIKAC NICZEGO - pojawi sie sam!    |
|                                                    |
|  [################----------] 56 %                 |
|                                                    |
|  Pliki: 324/567                                    |
|  Kopiuje: DevExpress.Xpf.Grid.dll                  |
+----------------------------------------------------+
```

---

## 🔄 ZMIANA MEMÓW BEZ REBOOTA APKI

Sergiusz może zmieniać memy **kiedy chce**:

1. Wrzuca/usuwa pliki w `\\192.168.0.170\Install\Kalendarz1L\Launcher\Assets\memes\`
2. **Następna aktualizacja u pracownika** → automatycznie ładuje nowe memy

**Bez ponownego deploya `deploy.bat`. Bez ponownej instalacji u pracowników. Po prostu kopiuj-wklej na QNAP.**

---

## 🛡️ SAFE MODE

Jeśli na QNAP nie ma folderu Assets (np. zapomniałeś wgrać) — splash pokaże:
- Pole z napisem "Wrzuc memy do: ..."
- Pasek postępu działa normalnie
- Logo pole będzie puste (białe)

Nic się nie zepsuje — splash jest **fail-safe**.

---

## 📋 CHECKLIST PIERWSZEGO WGRANIA

- [ ] Stworzony folder `\\192.168.0.170\Install\Kalendarz1L\Launcher\Assets\`
- [ ] Wgrane `logo.png` (z `logo-2-green.png` lub `Logo.png` z repo)
- [ ] Stworzony podfolder `\\...\Launcher\Assets\memes\`
- [ ] Wgrane min. 3-5 memów (jpg/png/gif)
- [ ] Uruchomiony `DEPLOY\build-launcher.bat` (rebuild nowego launchera z carousel)
- [ ] Test: kliknąć skrót ZPSP → splash powinien pokazać logo + memy carousel

---

## 🐛 TROUBLESHOOTING

### "Brak logo w splashu"
- Sprawdź czy `\\192.168.0.170\Install\Kalendarz1L\Launcher\Assets\logo.png` istnieje
- Nazwa **MUSI** być dokładnie `logo.png` (lowercase)
- Sprawdź czy plik nie jest uszkodzony

### "Memy nie wyświetlają się"
- Sprawdź `\\192.168.0.170\Install\Kalendarz1L\Launcher\Assets\memes\`
- Pliki muszą mieć rozszerzenie `.jpg`, `.jpeg`, `.png` lub `.gif`
- Sprawdź `%LOCALAPPDATA%\ZPSP\Assets\memes\` — czy są tam zsynchronizowane?

### "Carousel nie rotuje"
- Trzeba minimum **2 pliki** w memes/ — jeden mem nie rotuje
- Rotacja co 5s — czekaj chwilę
- Sprawdź `%LOCALAPPDATA%\ZPSP\Assets\memes\` — czy są tam wszystkie pliki?

### "Splash z opóźnieniem"
- Synchronizacja Assets z QNAP może trwać 2-5 sek przy pierwszym uruchomieniu (sieć)
- Następne uruchomienia: cache lokalny, ~instant

---

**Powodzenia!** 🐔
