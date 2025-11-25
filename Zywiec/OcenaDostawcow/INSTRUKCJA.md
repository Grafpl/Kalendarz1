# INSTRUKCJA - Naprawa generatora PDF

## ❗ KROK 1: USUŃ STARE PLIKI

W folderze `C:\Users\sergi\source\repos\Grafpl\Kalendarz1\Zywiec\OcenaDostawcow\`

**USUŃ te pliki (jeśli istnieją):**
- `BlankOcenaFormPDFGenerator_v2.cs`
- stary `BlankOcenaFormPDFGenerator.cs`

---

## ❗ KROK 2: SKOPIUJ NOWE PLIKI

Skopiuj do tego samego folderu:

1. **BlankOcenaFormPDFGenerator.cs** - NOWY generator (przepisany od zera)
2. **OcenaDostawcyWindow.xaml.cs** - poprawione wywołanie generatora
3. **HistoriaOcenWindow.xaml** 
4. **HistoriaOcenWindow.xaml.cs**

---

## ❗ KROK 3: REBUILD

W Visual Studio:
1. Build → Clean Solution
2. Build → Rebuild Solution (Ctrl+Shift+B)

---

## Co zostało naprawione:

### BlankOcenaFormPDFGenerator.cs:
- ✅ Przepisany OD ZERA bazując na działającym OcenaPDFGenerator_v3
- ✅ Usunięte `using System.Drawing` i `using System.Windows.Forms` (powodowały konflikty!)
- ✅ Używa TYLKO QuestPDF (jak działający generator)
- ✅ Prosta struktura: nagłówek → treść → stopka

### OcenaDostawcyWindow.xaml.cs:
- ✅ Przycisk "Drukuj Pusty" wywołuje `BlankOcenaFormPDFGenerator`
- ✅ Przycisk "Generuj PDF" wywołuje `OcenaPDFGenerator` (wypełniony)

---

## Struktura nowego generatora:

```
BlankOcenaFormPDFGenerator
├── GenerujPustyFormularz(sciezka, showPoints)
├── ComposeHeader()      - nagłówek z polami na dane
├── ComposeContent()     - wszystkie sekcje
│   ├── Sekcja I - Samoocena (8 pytań, 3 pkt każde)
│   ├── Sekcja II - Hodowca (5 pytań, 3 pkt każde)
│   ├── Sekcja II - Kierowca (15 pytań, 1 pkt każde)
│   ├── Sekcja III - Dokumentacja
│   ├── Podsumowanie punktacji
│   └── Podpisy
└── ComposeFooter()      - stopka z datą i stroną
```

---

## Jeśli nadal nie działa:

1. Sprawdź czy masz zainstalowany **QuestPDF** w projekcie
2. W NuGet Package Manager: `Install-Package QuestPDF`
3. Upewnij się że nie ma duplikatów klas w projekcie

---

**Po wykonaniu kroków 1-3 przycisk "Drukuj Pusty" powinien generować poprawny PDF!**
