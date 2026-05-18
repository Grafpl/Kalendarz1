# 📋 INSTRUKCJA DLA INFORMATYKA — Wdrożenie launchera ZPSP

> **Firma:** Ubojnia Drobiu Piórkowscy, Koziołki 40
> **System:** ZPSP (Kalendarz1) — autorski system zarządzania
> **Cel wizyty:** Zainstalować nowy launcher z auto-update na wszystkich stacjach pracowników
> **Czas:** ~5 minut na stację × liczba stacji

---

## 🎯 CO DOKŁADNIE TRZEBA ZROBIĆ

Na każdym komputerze pracownika:

1. **Zainstalować nowy launcher** ZPSP (jednorazowo)
2. **Sprawdzić** że skrót działa i ZPSP się uruchamia
3. **Usunąć stary skrót** (do `\\PC\source\repos\Grafpl\Kalendarz1\bin\Debug\...\Kalendarz1.exe`)
4. **Pokazać pracownikowi** nowy skrót i wyjaśnić zmianę

---

## 📦 CO MASZ DO DYSPOZYCJI

**Dysk sieciowy QNAP** (wymagany dostęp przez sieć firmową lub VPN):
```
\\192.168.0.170\Install\Kalendarz1L\
```

**Instalator znajduje się tutaj:**
```
\\192.168.0.170\Install\Kalendarz1L\Launcher\install-launcher.bat
```

---

## ✅ PRZED WIZYTĄ — TEST POŁĄCZENIA

Zanim pójdziesz do firmy:

1. Sprawdź czy masz dostęp do sieci firmowej (LAN lub VPN do Piórkowscy)
2. Otwórz w Eksploratorze Windows: `\\192.168.0.170\Install\Kalendarz1L\`
3. Powinieneś zobaczyć 3 foldery: `Release\`, `Launcher\`, `Backup\`
4. Jeśli nie widzisz — zadzwoń do Sergiusza (`sergiusz.piorko@gmail.com`)

---

## 🖥️ PROCEDURA NA KAŻDEJ STACJI

### KROK 1 — Zaloguj się na komputerze pracownika
- Konto pracownika (NIE administrator), chyba że stacja wymaga uprawnień podwyższonych
- Sprawdź czy ma dostęp do `\\192.168.0.170\Install\` (otwórz w eksploratorze)

### KROK 2 — Zamknij stary ZPSP (jeśli uruchomiony)
- `Ctrl+Shift+Esc` → Task Manager → zakładka **Szczegóły**
- Znajdź `Kalendarz1.exe` → prawy klik → **Zakończ zadanie**
- Znajdź `ZPSP.exe` (jeśli jest) → prawy klik → **Zakończ zadanie**

### KROK 3 — Uruchom instalator launchera
1. Otwórz **Eksplorator Windows**
2. W pasku adresu wklej:
   ```
   \\192.168.0.170\Install\Kalendarz1L\Launcher\
   ```
3. Naciśnij Enter
4. Powinieneś zobaczyć 2 pliki:
   - `ZPSP.exe` (launcher, ~10 MB)
   - `install-launcher.bat` (skrypt instalacyjny)
5. **Kliknij dwukrotnie** `install-launcher.bat`
6. Otworzy się czarne okno cmd z komunikatami:
   ```
   ZPSP — Instalacja Launchera
   [1/3] Test polaczenia z QNAP...   OK
   [2/3] Instalowanie launchera lokalnie...   OK
   [3/3] Tworzenie skrotu na pulpicie...   OK

                  INSTALACJA UDANA
   ```
7. Naciśnij dowolny klawisz aby zamknąć okno

### KROK 4 — Sprawdź czy skrót działa
1. Przejdź na **pulpit pracownika**
2. Powinien być nowy skrót: **"ZPSP"**
3. Kliknij dwukrotnie nowy skrót
4. **Co powinno się stać:**
   - Pojawi się **niebieski splash** z tekstem "Aktualizowanie ZPSP..."
   - Pierwszy raz potrwa ~1-2 minuty (kopiowanie 1.1 GB z QNAP)
   - Splash zniknie
   - Otworzy się **okno logowania ZPSP** (zielone logo Piórkowscy)
5. **NIE LOGUJ SIĘ** — to test, zamknij okno (X w rogu)

### KROK 5 — Usuń stary skrót
1. Na pulpicie znajdź **stary skrót** ZPSP (prowadzi do `\\PC\source\repos\Grafpl\Kalendarz1\bin\Debug\...`)
   - Prawy klik → **Właściwości** → zakładka **Skrót** → sprawdź pole **"Element docelowy"**
   - Jeśli zawiera `bin\Debug` lub `source\repos` → to **stary skrót, usuń**
2. **Prawy klik** → **Usuń**
3. Opróżnij kosz (opcjonalnie)

### KROK 6 — Pokaż pracownikowi
Powiedz pracownikowi:
> "Od teraz uruchamiasz ZPSP **TYLKO** klikając ten nowy skrót **'ZPSP'** na pulpicie. Aktualizacje pobiorą się automatycznie. Jeśli będziesz miał problem — daj znać Sergiuszowi."

---

## 📊 LISTA STACJI DO OBSŁUŻENIA

| ✅ | Nazwa stacji / Pokój | Pracownik | Uwagi |
|---|---|---|---|
| ☐ | _____________________ | Sergiusz (właściciel) | |
| ☐ | _____________________ | Marcin (Zgierz - masarnia) | przez VPN |
| ☐ | _____________________ | Justyna Chrostowska (jakość) | |
| ☐ | _____________________ | Jola (sprzedaż) | |
| ☐ | _____________________ | Maja (sprzedaż) | |
| ☐ | _____________________ | Radek (sprzedaż) | |
| ☐ | _____________________ | Teresa (sprzedaż/fakturzystka) | |
| ☐ | _____________________ | Ania (sprzedaż) | |
| ☐ | _____________________ | Paulina (zakupy) | |
| ☐ | _____________________ | Łukasz Collins (kierownik uboju) | |
| ☐ | _____________________ | Janek Matusiak (mroźnia) | |
| ☐ | _____________________ | Magazynier 1 | |
| ☐ | _____________________ | Magazynier 2 | |
| ☐ | _____________________ | Portiernia | |
| ☐ | _____________________ | Lekarz wet. | |
| ☐ | _____________________ | Inni: ___________________ | |

---

## 🚨 ROZWIĄZYWANIE PROBLEMÓW

### Problem: "Nie mozna polaczyc sie z QNAP"
**Przyczyna:** brak dostępu do sieci `\\192.168.0.170`

**Rozwiązanie:**
1. Sprawdź czy stacja jest podłączona do sieci firmowej
2. Otwórz cmd → `ping 192.168.0.170` — czy odpowiada?
3. W eksploratorze otwórz `\\192.168.0.170\Install\` — czy się otwiera?
4. Jeśli nie — sprawdź ustawienia sieci stacji lub VPN
5. Zadzwoń do Sergiusza

### Problem: install-launcher.bat NIE TWORZY skrótu
**Przyczyna:** brak uprawnień PowerShell lub blokada polityki

**Rozwiązanie ręczne:**
1. Otwórz cmd:
   ```cmd
   cd %LOCALAPPDATA%\ZPSP
   dir
   ```
2. Powinieneś widzieć `ZPSP.exe`
3. Prawym kliknij `ZPSP.exe` → **Wyślij do** → **Pulpit (utwórz skrót)**
4. Zmień nazwę skrótu na "ZPSP"

### Problem: ZPSP.exe uruchamia się, ale natychmiast się zamyka
**Przyczyna:** brak .NET 8 Desktop Runtime na stacji

**Rozwiązanie:**
1. Pobierz: https://dotnet.microsoft.com/download/dotnet/8.0
2. Wybierz: **".NET Desktop Runtime 8.0 x64"**
3. Zainstaluj → restart stacji
4. Spróbuj ponownie

### Problem: "ZPSP jest obecnie uruchomiona (PID: 12345)"
**Przyczyna:** stary proces wisi w tle

**Rozwiązanie:**
1. Task Manager (`Ctrl+Shift+Esc`)
2. Zakładka **Szczegóły**
3. Znajdź `Kalendarz1.exe` z podanym PID
4. Prawy klik → **Zakończ zadanie**
5. Spróbuj launcher ponownie

### Problem: Pojawia się komunikat Windows SmartScreen "Niezweryfikowany wydawca"
**Przyczyna:** launcher nie ma certyfikatu code-signing (na razie)

**Rozwiązanie:**
1. Kliknij **"Więcej informacji"**
2. Kliknij **"Uruchom mimo to"**
3. Komunikat pojawi się tylko **przy pierwszym uruchomieniu** — Windows zapamiętuje decyzję

### Problem: Splash "Aktualizowanie ZPSP..." wisi >5 minut
**Przyczyna:** wolne połączenie VPN lub przerywająca sieć

**Rozwiązanie:**
1. Zamknij splash (X)
2. Otwórz cmd:
   ```cmd
   rmdir /S /Q %LOCALAPPDATA%\ZPSP
   ```
3. Sprawdź połączenie VPN
4. Spróbuj launcher ponownie (zacznie kopiowanie od nowa)

---

## 📞 KONTAKT W RAZIE PROBLEMÓW

**Sergiusz Piórkowski (właściciel + autor systemu)**
- Email: sergiusz.piorko@gmail.com
- Telefon: _________________ (uzupełnij)

W przypadku nieprzewidzianych problemów — zadzwoń przed kontynuowaniem.

---

## 📝 PO ZAKOŃCZENIU PRACY

Wypełnij i wyślij Sergiuszowi:

**Liczba stacji obsłużonych:** ____ / ____

**Stacje gdzie wystąpiły problemy:**
```
1. _________________________ | Problem: ____________________
2. _________________________ | Problem: ____________________
3. _________________________ | Problem: ____________________
```

**Łączny czas pracy:** _______ godzin

**Data wykonania:** _______________

**Podpis informatyka:** _______________

---

## 🔍 DODATKOWE INFORMACJE TECHNICZNE

### Co robi launcher (dla zrozumienia)?
1. Sprawdza wersję `Kalendarz1.exe` na QNAP vs lokalna kopia w `%LOCALAPPDATA%\ZPSP\`
2. Jeśli na QNAP jest nowsza wersja — kopiuje całość do lokalnej kopii
3. Uruchamia lokalną kopię (NIE z QNAP — to ważne dla wydajności i niezależności)

### Struktura plików po instalacji u pracownika:
```
C:\Users\[USER]\AppData\Local\ZPSP\
├── ZPSP.exe              (launcher, ~10 MB)
├── Kalendarz1.exe        (aplikacja główna)
├── *.dll                 (~50 plików)
├── Assets\
├── Resources\
└── ...
```

### Skrót na pulpicie:
```
Cel: C:\Users\[USER]\AppData\Local\ZPSP\ZPSP.exe
Folder roboczy: C:\Users\[USER]\AppData\Local\ZPSP\
```

### Co robić gdy pracownik mówi "nie działa"?
1. Niech opisze konkretnie: "co kliknął" + "co zobaczył" + "treść komunikatu błędu"
2. Sprawdź czy nowy skrót istnieje na pulpicie
3. Sprawdź czy `%LOCALAPPDATA%\ZPSP\ZPSP.exe` istnieje
4. Spróbuj uruchomić launcher ponownie
5. Jeśli problem trwa — zadzwoń do Sergiusza z opisem problemu

---

**Powodzenia! 🚀**
