# Instrukcja Podłączenia Wagi RHEWA 82c-1

## Twój sprzęt

### Waga: RHEWA 82c-1
- **Producent:** RHEWA-WAAGENFABRIK (Niemcy)
- **Max:** 60 000 kg
- **Min:** 400 kg
- **Działka:** 20 kg
- **Nr fabryczny:** 212043
- **Firmware:** >= 3.02

### Drukarka: PICCO-2SU
- **Szerokość:** 58mm
- **Interfejs:** USB, RS232
- **Producent:** CompArt International

## Podłączenie wagi RHEWA

### Krok 1: Sprawdź kabel

Waga RHEWA 82c posiada złącze RS-232 (port szeregowy). Potrzebujesz:
- Kabla RS-232 (9-pin lub 25-pin, zależnie od modelu)
- Konwertera USB-RS232 (jeśli komputer nie ma portu COM)

### Krok 2: Znajdź port COM

1. Podłącz kabel do komputera
2. Otwórz **Menedżer urządzeń** (kliknij prawym na "Ten komputer" → Zarządzaj)
3. Rozwiń **Porty (COM i LPT)**
4. Znajdź numer portu (np. COM3, COM4)

### Krok 3: Sprawdź ustawienia w wadze

W menu wagi RHEWA (przycisk "i" lub menu serwisowe):

1. Znajdź **Interface** lub **RS232**
2. Zanotuj ustawienia:
   - **Baud Rate:** zazwyczaj 9600
   - **Data Bits:** 8
   - **Parity:** None lub Even
   - **Stop Bits:** 1

### Krok 4: Połącz w Panelu Portiera

1. Kliknij **"Połącz"** w sekcji statusu wagi
2. Wybierz port COM (np. COM3)
3. Wybierz prędkość (np. 9600)
4. Kliknij **"Połącz"**

## Używanie wagi

### Odczyt automatyczny
1. Wjedź autem na wagę
2. Poczekaj aż waga się ustabilizuje (symbol >0< na wyświetlaczu)
3. Kliknij przycisk **⚖️** przy polu BRUTTO
4. Waga zostanie automatycznie wczytana

### Odczyt tary
1. Po rozładunku wjedź pustym autem
2. Kliknij przycisk **⚖️** przy polu TARA
3. NETTO obliczy się automatycznie

## Komendy wagi RHEWA

Program wysyła komendę **"S"** (stable - odczyt stabilny). Jeśli nie działa, skontaktuj się z serwisem aby sprawdzić protokół komunikacji.

Inne możliwe komendy:
- `W` - weight (waga)
- `G` - gross (brutto)
- `N` - net (netto)
- `T` - tare (tara)

## Rozwiązywanie problemów

### "Nie udało się odczytać wagi"
- Sprawdź czy waga jest stabilna (symbol >0< musi być widoczny)
- Sprawdź czy auto nie jest w ruchu
- Poczekaj kilka sekund i spróbuj ponownie

### "Błąd połączenia"
- Sprawdź czy kabel jest podłączony
- Sprawdź numer portu COM
- Sprawdź prędkość transmisji (BaudRate)
- Spróbuj innej parzystości (None/Even)

### Waga pokazuje "US"
- Waga niestabilna (Unstable)
- Poczekaj aż auto się zatrzyma

### Waga pokazuje "OL"
- Przeciążenie (Overload)
- Waga przekracza 60 000 kg

## Kontakt z serwisem

**MULTIWAG** - Autoryzowany serwis RHEWA
- www.multiwag.pl
- Tel: **504 335 604**

Przy kontakcie podaj:
- Model: RHEWA 82c-1
- Nr fabryczny: 212043

## Drukarka PICCO-2SU

Drukarka PICCO powinna być automatycznie rozpoznana przez Windows. Jeśli nie:

1. Podłącz drukarkę przez USB
2. Windows powinien zainstalować sterowniki automatycznie
3. Sprawdź w **Ustawienia → Drukarki** czy PICCO jest widoczna
4. Ustaw jako domyślną drukarkę

### Test drukarki
1. Wybierz dostawę z zapisaną wagą
2. Kliknij przycisk **🖨️**
3. Wybierz drukarkę PICCO
4. Kliknij **Drukuj**

Kwit wagowy wydrukuje się na papierze 58mm.
