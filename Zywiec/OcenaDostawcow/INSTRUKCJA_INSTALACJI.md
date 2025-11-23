# 📦 INSTRUKCJA INSTALACJI I KONFIGURACJI
## OcenaPDFGenerator - Wersja 2.0 Professional

---

## 🎯 KROK 1: Dodaj nowy plik do projektu

### 1.1 Usuń stary plik (opcjonalnie - zachowaj backup!)
```
1. W Solution Explorer znajdź: OcenaPDFGenerator.cs
2. Kliknij prawym przyciskiem myszy
3. Wybierz "Exclude from Project" (lub usuń jeśli masz backup)
```

### 1.2 Dodaj nowy plik
```
1. Kliknij prawym na projekt "Kalendarz1"
2. Add → Existing Item...
3. Wybierz nowy plik: OcenaPDFGenerator.cs
4. Kliknij "Add"
```

---

## 🖼️ KROK 2: Dodaj plik Logo.png

### 2.1 Przygotuj logo
- Format: PNG (zalecane), JPG, BMP
- Rozmiar: Szerokość ~300-500px (automatyczne skalowanie)
- Przezroczyste tło: zalecane (dla PNG)
- Nazwa pliku: **Logo.png** (dokładnie tak!)

### 2.2 Skopiuj do projektu
```
1. Skopiuj plik Logo.png
2. Wklej go do głównego folderu projektu (tam gdzie .csproj)
   Przykład: C:\Users\PC\source\repos\Grafpl\Kalendarz1\Logo.png
```

### 2.3 Ustaw właściwości pliku w Visual Studio
```
1. Kliknij prawym na Logo.png w Solution Explorer
2. Properties
3. Ustaw:
   - Build Action: Content
   - Copy to Output Directory: Copy if newer
```

**WAŻNE:** Logo będzie kopiowane do folderu bin\Debug (lub bin\Release) przy każdym buildzie!

### 2.4 Alternatywnie: Umieść logo bezpośrednio w bin
```
Jeśli nie chcesz go dodawać do projektu, po prostu skopiuj Logo.png do:
- bin\Debug\Logo.png (podczas developmentu)
- bin\Release\Logo.png (w wersji produkcyjnej)
```

---

## 📚 KROK 3: Sprawdź biblioteki NuGet

### 3.1 Otwórz NuGet Package Manager
```
Tools → NuGet Package Manager → Manage NuGet Packages for Solution...
```

### 3.2 Sprawdź czy masz zainstalowane:
- ✅ **QuestPDF** (wersja 2022.12.0 lub nowsza)
- ✅ **QuestPDF.Helpers**
- ✅ **QuestPDF.Infrastructure**

### 3.3 Jeśli brakuje, zainstaluj:
```
1. Przejdź do zakładki "Browse"
2. Wyszukaj: "QuestPDF"
3. Wybierz: QuestPDF
4. Kliknij "Install" dla projektu Kalendarz1
5. Zaakceptuj licencję (Community License)
```

### 3.4 Lub użyj Package Manager Console:
```powershell
Install-Package QuestPDF
```

---

## 🔧 KROK 4: Rebuild projektu

### 4.1 Wyczyść i przebuduj
```
1. Build → Clean Solution
2. Build → Rebuild Solution
```

### 4.2 Sprawdź błędy kompilacji
- Powinno być: **0 Errors**
- Jeśli są błędy, sprawdź czy:
  - QuestPDF jest zainstalowany
  - Plik OcenaPDFGenerator.cs jest w namespace Kalendarz1
  - Wszystkie using są na miejscu

---

## ✅ KROK 5: Test podstawowy

### 5.1 Dodaj kod testowy (np. w Program.cs lub w Button_Click)

```csharp
using Kalendarz1;
using System;
using System.IO;

// Test pustego formularza
var generator = new OcenaPDFGenerator();
string sciezkaTestowa = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
    "TEST_Formularz.pdf"
);

generator.GenerujPdf(
    sciezkaDoPliku: sciezkaTestowa,
    numerRaportu: "",
    dataOceny: DateTime.Now,
    dostawcaNazwa: "TEST - Jan Kowalski",
    dostawcaId: "DOW-001",
    samoocena: null,
    listaKontrolna: null,
    dokumentacja: false,
    p1_5: 0,
    p6_20: 0,
    pRazem: 0,
    uwagi: "",
    czyPustyFormularz: true
);

// Otwórz wygenerowany PDF
System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
{
    FileName = sciezkaTestowa,
    UseShellExecute = true
});
```

### 5.2 Uruchom test
```
1. Naciśnij F5 (lub Start)
2. Wykonaj akcję która wywołuje kod testowy
3. Sprawdź czy PDF pojawił się na pulpicie
4. Otwórz PDF i sprawdź wygląd
```

---

## 🎨 KROK 6: Dostosowanie do istniejącego systemu

### 6.1 Integracja z OcenaDostawcyWindow.xaml.cs

**Znajdź metodę generowania PDF** (prawdopodobnie w ButtonGenerujPDF_Click):

```csharp
// STARY KOD (usuń lub zakomentuj):
// var generator = new OcenaPDFGenerator();
// generator.GenerujStaryPdf(...);

// NOWY KOD:
var generator = new OcenaPDFGenerator();

// Pobierz dane z kontrolek WPF
string numerRaportu = txtNumerRaportu.Text;
DateTime dataOceny = dpDataOceny.SelectedDate ?? DateTime.Now;
string dostawcaNazwa = txtNazwaDostawcy.Text;
string dostawcaId = txtDostawcaId.Text;
string uwagi = txtUwagi.Text;

// Zbierz odpowiedzi z checkboxów (przykład)
bool[] samoocena = new bool[]
{
    chkPytanie1.IsChecked == true,
    chkPytanie2.IsChecked == true,
    chkPytanie3.IsChecked == true,
    chkPytanie4.IsChecked == true,
    chkPytanie5.IsChecked == true
};

bool[] listaKontrolna = new bool[]
{
    chkPytanie6.IsChecked == true,
    chkPytanie7.IsChecked == true,
    // ... itd. dla wszystkich 25 pytań (6-30)
};

bool dokumentacja = chkDokumentacja.IsChecked == true;

// Oblicz punkty
int punkty1_5 = samoocena.Count(x => x) * 3;  // 3 pkt za każde TAK
int punkty6_30 = listaKontrolna.Count(x => x) * 1;  // 1 pkt za każde TAK
int punktyRazem = punkty1_5 + punkty6_30;

// Generuj PDF
string sciezka = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
    "Raporty",
    $"Ocena_{dostawcaId}_{DateTime.Now:yyyy-MM-dd}.pdf"
);

Directory.CreateDirectory(Path.GetDirectoryName(sciezka));

generator.GenerujPdf(
    sciezkaDoPliku: sciezka,
    numerRaportu: numerRaportu,
    dataOceny: dataOceny,
    dostawcaNazwa: dostawcaNazwa,
    dostawcaId: dostawcaId,
    samoocena: samoocena,
    listaKontrolna: listaKontrolna,
    dokumentacja: dokumentacja,
    p1_5: punkty1_5,
    p6_20: punkty6_30,
    pRazem: punktyRazem,
    uwagi: uwagi,
    czyPustyFormularz: false
);

// Informacja dla użytkownika
MessageBox.Show($"Raport zapisany:\n{sciezka}", 
    "Sukces", 
    MessageBoxButton.OK, 
    MessageBoxImage.Information);

// Opcjonalnie: otwórz PDF
if (MessageBox.Show("Czy otworzyć raport?", "Pytanie", 
    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
{
    Process.Start(new ProcessStartInfo { FileName = sciezka, UseShellExecute = true });
}
```

### 6.2 Dodaj przycisk "Generuj pusty formularz"

W XAML:
```xaml
<Button x:Name="btnGenerujPusty" 
        Content="Drukuj pusty formularz" 
        Click="BtnGenerujPusty_Click" 
        Margin="5"/>
```

W Code-behind:
```csharp
private void BtnGenerujPusty_Click(object sender, RoutedEventArgs e)
{
    var generator = new OcenaPDFGenerator();
    
    string dostawcaNazwa = txtNazwaDostawcy.Text;
    string dostawcaId = txtDostawcaId.Text;
    
    if (string.IsNullOrWhiteSpace(dostawcaNazwa))
    {
        MessageBox.Show("Wybierz dostawcę!", "Błąd", 
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
    }
    
    string sciezka = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        $"Formularz_{dostawcaId}.pdf"
    );
    
    generator.GenerujPdf(
        sciezkaDoPliku: sciezka,
        numerRaportu: "",
        dataOceny: DateTime.Now,
        dostawcaNazwa: dostawcaNazwa,
        dostawcaId: dostawcaId,
        samoocena: null,
        listaKontrolna: null,
        dokumentacja: false,
        p1_5: 0,
        p6_20: 0,
        pRazem: 0,
        uwagi: "",
        czyPustyFormularz: true
    );
    
    MessageBox.Show($"Pusty formularz zapisany na pulpicie:\n{sciezka}", 
        "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
    
    Process.Start(new ProcessStartInfo { FileName = sciezka, UseShellExecute = true });
}
```

---

## 🐛 KROK 7: Rozwiązywanie problemów

### Problem 1: "Nie można znaleźć pliku Logo.png"
**Rozwiązanie:**
- Sprawdź czy Logo.png jest w folderze bin\Debug lub bin\Release
- Sprawdź właściwości pliku w VS: Copy to Output Directory = Copy if newer
- Alternatywnie: umieść logo bezpośrednio w bin

### Problem 2: "QuestPDF license required"
**Rozwiązanie:**
- Kod już zawiera: `QuestPDF.Settings.License = LicenseType.Community;`
- Jest to darmowa licencja Community (do użytku niekomercyjnego)
- Jeśli potrzebujesz licencji komercyjnej, odwiedź: https://www.questpdf.com/

### Problem 3: "Nie można zapisać pliku"
**Rozwiązanie:**
- Sprawdź czy plik nie jest otwarty w Adobe Reader
- Upewnij się że folder istnieje: `Directory.CreateDirectory(...)`
- Sprawdź uprawnienia do zapisu

### Problem 4: "Błąd kompilacji - brak typu OcenaPDFGenerator"
**Rozwiązanie:**
- Upewnij się że namespace jest `Kalendarz1`
- Rebuild Solution
- Sprawdź czy plik jest w projekcie (not excluded)

### Problem 5: "Czcionka nie działa"
**Rozwiązanie:**
- Kod używa Fonts.Calibri (systemowa czcionka Windows)
- Powinna działać bez problemu
- Alternatywa: zmień na `FontFamily("Arial")`

---

## 📊 KROK 8: Weryfikacja działania

### Checklist końcowy:
- [ ] Projekt się kompiluje bez błędów
- [ ] Logo.png jest widoczne w PDF
- [ ] Pusty formularz generuje się poprawnie
- [ ] Wypełniony raport pokazuje dane
- [ ] Checkboxy są wyraźne
- [ ] Kolory są odpowiednie (zielony główny)
- [ ] Podsumowanie pokazuje prawidłowe punkty
- [ ] PDF można wydrukować (Ctrl+P w Adobe Reader)
- [ ] Wszystkie sekcje są widoczne
- [ ] Podpisy mają miejsce do wpisania

---

## 🎉 GOTOWE!

Twój system oceny dostawców jest teraz w pełni profesjonalny!

**Co dalej?**
- Przetestuj z prawdziwymi danymi
- Wydrukuj kilka formularzy dla hodowców
- Wygeneruj przykładowe raporty
- Zobacz plik PRZYKLADY_UZYCIA.cs dla więcej scenariuszy

**Potrzebujesz pomocy?**
- Sprawdź ZMIANY_OcenaPDFGenerator.md (pełna lista zmian)
- Zobacz PRZYKLADY_UZYCIA.cs (7 przykładów użycia)

---

## 📞 WSPARCIE

Jeśli napotkasz problemy:
1. Sprawdź najpierw sekcję "Rozwiązywanie problemów" powyżej
2. Przejrzyj przykłady w PRZYKLADY_UZYCIA.cs
3. Zrób debug - ustaw breakpoint i sprawdź wartości zmiennych

**Powodzenia! 🚀**
