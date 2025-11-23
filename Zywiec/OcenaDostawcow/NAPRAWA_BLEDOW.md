# 🔧 NAPRAWA BŁĘDÓW KOMPILACJI
## OcenaPDFGenerator.cs - Poprawka dla QuestPDF

---

## ❌ PROBLEM

Wystąpiło 5 błędów kompilacji:
```
CS1503: Argument 2: cannot convert from 'method group' to 'QuestPDF.Elements.IDynamicElement'
```

Linie: 509, 512, 516, 519, 522

---

## 🔍 PRZYCZYNA

Problem był w metodzie `ComposeDokumentacja()` gdzie metoda `BodyCell` była używana jako "method group" (bez lambdy), ale ma opcjonalny parametr `isEvenRow`:

```csharp
// BŁĘDNA SKŁADNIA:
table.Cell().Element(BodyCell).AlignCenter()

// Metoda ma opcjonalny parametr:
private IContainer BodyCell(IContainer container, bool isEvenRow = false)
```

W QuestPDF, gdy metoda ma opcjonalne parametry, **nie można** użyć jej bezpośrednio jako method group - trzeba opakować w lambdę.

---

## ✅ ROZWIĄZANIE

Wszystkie 5 wywołań zostały poprawione poprzez dodanie lambda expressions:

### Przed (BŁĄD):
```csharp
table.Cell().Element(BodyCell).AlignCenter()
```

### Po (POPRAWNIE):
```csharp
table.Cell().Element(c => BodyCell(c)).AlignCenter()
```

---

## 📝 SZCZEGÓŁY NAPRAWY

### Linia 509:
```csharp
// PRZED:
table.Cell().Element(BodyCell).AlignCenter()

// PO:
table.Cell().Element(c => BodyCell(c)).AlignCenter()
```

### Linia 512:
```csharp
// PRZED:
table.Cell().Element(BodyCell).PaddingLeft(5)

// PO:
table.Cell().Element(c => BodyCell(c)).PaddingLeft(5)
```

### Linia 516:
```csharp
// PRZED:
table.Cell().Element(BodyCell).AlignCenter().AlignMiddle()

// PO:
table.Cell().Element(c => BodyCell(c)).AlignCenter().AlignMiddle()
```

### Linia 519:
```csharp
// PRZED:
table.Cell().Element(BodyCell).AlignCenter().AlignMiddle()

// PO:
table.Cell().Element(c => BodyCell(c)).AlignCenter().AlignMiddle()
```

### Linia 522:
```csharp
// PRZED:
table.Cell().Element(BodyCell).AlignCenter().AlignMiddle()

// PO:
table.Cell().Element(c => BodyCell(c)).AlignCenter().AlignMiddle()
```

---

## ✅ STATUS

- **5/5 błędów naprawionych**
- **Plik kompiluje się bez błędów**
- **Funkcjonalność nie zmieniła się**

---

## 📚 DODATKOWE INFORMACJE

### Dlaczego HeaderCell działa bez lambdy?

```csharp
// HeaderCell NIE MA opcjonalnych parametrów:
private IContainer HeaderCell(IContainer container)

// Dlatego MOŻNA użyć bezpośrednio:
header.Cell().Element(HeaderCell).Text("...")  // ✅ OK!
```

### Dlaczego BodyCell potrzebuje lambdy?

```csharp
// BodyCell MA opcjonalny parametr:
private IContainer BodyCell(IContainer container, bool isEvenRow = false)

// Dlatego TRZEBA użyć lambdy:
table.Cell().Element(c => BodyCell(c))         // ✅ OK!
table.Cell().Element(BodyCell)                  // ❌ BŁĄD!
```

---

## 🎯 WNIOSKI

### Zasada dla QuestPDF:
- ✅ **Metoda bez parametrów opcjonalnych** → można użyć jako method group
- ❌ **Metoda z parametrami opcjonalnymi** → TRZEBA użyć lambdy

### Prawidłowe wzorce:
```csharp
// 1. Method group (gdy metoda ma tylko wymagane parametry)
.Element(MyMethod)

// 2. Lambda (zawsze działa, zalecane dla metod z opcjonalnymi parametrami)
.Element(c => MyMethod(c))

// 3. Lambda z parametrami
.Element(c => MyMethod(c, param1, param2))
```

---

**Status:** ✅ Naprawione  
**Data:** 23 listopada 2024  
**Pliki:** OcenaPDFGenerator.cs
