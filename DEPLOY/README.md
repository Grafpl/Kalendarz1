# 🚀 ZPSP — Dystrybucja i Auto-Update

System dystrybucji: **Sergiusz buduje Release lokalnie → wgrywa 1 klikiem na QNAP → userzy automatycznie dostają update przy następnym starcie**.

---

## 📂 Struktura na QNAP (`\\192.168.0.170\Install\Kalendarz1\`)

```
\\192.168.0.170\Install\Kalendarz1\
├── Release\              ← aktualna wersja produkcyjna ZPSP (binarki)
│   ├── Kalendarz1.exe
│   ├── *.dll
│   ├── Assets\, Resources\
│   ├── appsettings.json
│   └── VERSION.txt       ← znacznik wersji
│
├── Launcher\             ← launcher dla maszyn userów
│   ├── ZPSP.exe          ← mały (~10 MB) auto-update launcher
│   └── install-launcher.bat ← instalacja na stacji
│
└── Backup\               ← historia wersji (rollback w razie awarii)
    ├── 2026-05-11_14h30\
    ├── 2026-05-09_09h00\
    └── ...
```

**Czego NIE ma na QNAP** (zostaje lokalnie u Sergiusza):
- ❌ Source code `*.cs`, `*.xaml`
- ❌ `.git/`
- ❌ `BAZA_WIEDZY/`
- ❌ `CLAUDE.md`
- ❌ `SQL/`
- ❌ `Kalendarz1.csproj`

---

## 🎯 SERGIUSZ — JAK WDRAŻAĆ NOWĄ WERSJĘ

### Aktualizacja głównej aplikacji (codziennie/co tydzień):

1. Zrób zmiany w kodzie lokalnie w Visual Studio
2. Sprawdź czy działa lokalnie (F5 z Debug — bez problemu)
3. **Zamknij uruchomione lokalnie ZPSP**
4. **Kliknij dwukrotnie:** `DEPLOY\deploy.bat`

Skrypt zrobi:
- ✅ Backup obecnej wersji na QNAP do `Backup\<timestamp>\`
- ✅ Build Release (~2-3 min)
- ✅ Wyczyści wrażliwe pliki (PDB, BAZA_WIEDZY, SQL, …)
- ✅ Upload binariek na `\\192.168.0.170\Install\Kalendarz1\Release\`
- ✅ Zapis znacznika wersji w `VERSION.txt`

**Czas: 2-3 minuty.** Userzy widzą nową wersję przy następnym uruchomieniu (auto-update).

### Aktualizacja launchera (rzadko, tylko gdy zmieniasz kod ZPSP.Launcher\):

1. **Kliknij dwukrotnie:** `DEPLOY\build-launcher.bat`

Skrypt zrobi build single-file (~10 MB) i wgra na QNAP.
Potem na każdej stacji userów trzeba **raz** odpalić `install-launcher.bat`.

---

## 🖥️ INSTALACJA NA STACJI PRACOWNIKA (jednorazowo)

### Krok 1: Na komputerze pracownika otwórz w eksploratorze:
```
\\192.168.0.170\Install\Kalendarz1\Launcher\
```

### Krok 2: Kliknij dwukrotnie `install-launcher.bat`

Skrypt:
- ✅ Kopiuje `ZPSP.exe` (launcher) do `%LOCALAPPDATA%\ZPSP\`
- ✅ Tworzy skrót `ZPSP` na pulpicie pracownika
- ✅ Ikona skrótu prowadzi do lokalnego launchera

### Krok 3: Klik w skrót na pulpicie

Launcher:
1. Sprawdza wersję na QNAP vs lokalna kopia
2. Jeśli inna → kopiuje binarki z QNAP do `%LOCALAPPDATA%\ZPSP\`
3. Pokazuje splash "Aktualizowanie ZPSP..." podczas kopiowania
4. Odpala lokalną kopię

**Pierwsze uruchomienie:** ~30-60 sekund (kopia całości z QNAP).
**Kolejne (bez aktualizacji):** ~1 sekunda.
**Z aktualizacją:** ~10-20 sekund.

---

## 🛡️ TRYBY DZIAŁANIA LAUNCHERA

### ✅ Normalny start
QNAP dostępny, lokalna wersja aktualna → odpala lokalnie

### 🔄 Z aktualizacją
QNAP dostępny, na QNAP nowsza wersja → kopiuje + odpala

### 📴 Tryb offline
QNAP niedostępny, lokalna kopia istnieje → pyta usera "Uruchomić offline?" → odpala starą wersję

### ❌ Brak wszystkiego
QNAP niedostępny + brak lokalnej kopii → komunikat błędu, kontakt Sergiusz

### ⛔ ZPSP już uruchomiona
Próba aktualizacji gdy plik jest zablokowany → "Zamknij ZPSP i spróbuj ponownie"

---

## 🔄 ROLLBACK W RAZIE AWARII

Jeśli wgrałeś bugowaną wersję i userzy mają problemy:

```bash
# Na komputerze Sergiusza w cmd:
xcopy /E /I /Y "\\192.168.0.170\Install\Kalendarz1\Backup\2026-05-11_14h30\*" "\\192.168.0.170\Install\Kalendarz1\Release\"
```

Następne uruchomienie userów = poprzednia działająca wersja.

---

## 🔒 WHITELIST MASZYN (anti-theft)

W pliku `App.xaml.cs` jest stała:
```csharp
private const bool WhitelistEnabled = false;
```

**Domyślnie wyłączona** (żeby nie zablokować Cię przy testach).

### Włączenie:
1. Otwórz `App.xaml.cs`
2. Zmień `WhitelistEnabled = true`
3. W tablicy `AllowedMachines` wpisz nazwy komputerów które mają mieć dostęp:
   ```csharp
   private static readonly string[] AllowedMachines = new[]
   {
       "PC-SERGIUSZ", "PC-JOLA", "PC-MAJA", "PC-MARCIN",
       "PC-JUSTYNA", "PC-MAGAZYN-1", "PC-PORTIERNIA"
   };
   ```
4. Sprawdź nazwę komputera komendą `hostname` w cmd.
5. Rebuild + deploy

**Efekt:** Jeśli ktoś ukradnie folder `%LOCALAPPDATA%\ZPSP\` z `Kalendarz1.exe` i odpali w domu → pojawi się "Brak uprawnień stanowiska", apka się zamknie.

⚠ **NIE jest 100% security** — zaawansowany atakujący przeczyta kod i obejdzie. Ale podnosi barierę dla 95% przypadków.

---

## ⚙️ DLACZEGO TAK?

| Aspekt | Stara wersja | Nowa wersja |
|---|---|---|
| **Source code na QNAP** | TAK ❌ (kradzież IP) | NIE ✅ (tylko binarki) |
| **Hasło SA w plikach** | TAK ❌ | TAK ⚠ (DPAPI w TIER 2) |
| **Debug vs Release** | Debug ❌ (większy, łatwy do decompile) | Release ✅ (optymalizowany) |
| **Update mechanism** | Ręczny copy/paste | Automatyczny przez launcher ✅ |
| **Versioning/rollback** | Brak ❌ | Backup\<timestamp>\ ✅ |
| **Uruchamianie z sieci** | TAK (locks na exe) | NIE — kopia lokalna ✅ |
| **Performance startu** | Wolne (ładowanie z sieci) | Szybkie (lokalne) ✅ |
| **Działa bez sieci** | NIE | TAK (offline mode) ✅ |

---

## 🐛 DEBUG / TROUBLESHOOTING

### "User nie widzi nowej wersji po deploy"
- Sprawdź `VERSION.txt` na `\\192.168.0.170\Install\Kalendarz1\Release\`
- Sprawdź czy launcher u usera kopiuje (uruchom Task Manager → ZPSP.exe → wstrzymane?)
- Wymuszenie aktualizacji: usuń ręcznie `%LOCALAPPDATA%\ZPSP\Kalendarz1.exe` → następny start zrobi pełną kopię

### "Launcher mówi: ZPSP jest uruchomiona"
- User ma otwartą starą instancję — niech zamknie wszystkie okna ZPSP
- Sprawdź Task Manager → nawet jeśli okno zamknięte, proces mógł zostać

### "Brak połączenia z QNAP"
- Sprawdź czy user jest w sieci firmowej
- Test: czy `\\192.168.0.170\Install\` jest dostępne w eksploratorze
- Sprawdź uprawnienia QNAP do folderu `Install\Kalendarz1\Release\`

### "Brak ikony pulpitu"
- Skrót można dorobić ręcznie: pulpit → prawy klik → Nowy → Skrót → `%LOCALAPPDATA%\ZPSP\ZPSP.exe`

---

## 📋 CHECKLIST WDROŻENIA (jednorazowo)

- [ ] 1. Uruchom `DEPLOY\build-launcher.bat` (build launchera + upload na QNAP)
- [ ] 2. Uruchom `DEPLOY\deploy.bat` (build Release ZPSP + upload na QNAP)
- [ ] 3. **WAŻNE: Usuń ze QNAP STARE pliki** (cały folder Kalendarz1 oprócz `Release\`, `Launcher\`, `Backup\`):
      - Usuń `*.cs`, `*.xaml`, `.git/`, `BAZA_WIEDZY/`, `SQL/`, `bin/Debug/`, itd.
- [ ] 4. Na każdej stacji pracownika uruchom raz `install-launcher.bat`
- [ ] 5. Po 1-2 tygodniach (testy) — włącz `WhitelistEnabled = true` z listą stacji
- [ ] 6. Powiedz pracownikom żeby uruchamiali ZPSP z nowego skrótu na pulpicie

---

**Pytania → Sergiusz.**
