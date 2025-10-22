# 🔧 ZMIANY W WIDOKKALENDARZA.CS

## ⚠️ NIE ZMIENIAJ NAMESPACE WidokKalendarza!

Dodaj tylko **NA GÓRZE PLIKU** (w sekcji using):

```csharp
using Kalendarz1.AnkietyHodowcow;
```

---

## 📝 ZMIEŃ TYLKO METODĘ (linia ~3719):

### ❌ STARA WERSJA (usuń to):
```csharp
private async Task PokazHistorieZaznaczonegoAsync()
{
    if (datagridRanking.CurrentRow == null) return;
    var dostawca = Convert.ToString(datagridRanking.CurrentRow.Cells["Dostawca"].Value);
    if (string.IsNullOrWhiteSpace(dostawca)) return;
    var window = new YourNamespace.HistoriaHodowcyWindow(connectionPermission, dostawca);
    window.ShowDialog();
    await Task.CompletedTask;
}
```

### ✅ NOWA WERSJA (wklej to):
```csharp
private async Task PokazHistorieZaznaczonegoAsync()
{
    if (datagridRanking.CurrentRow == null) return;
    var dostawca = Convert.ToString(datagridRanking.CurrentRow.Cells["Dostawca"].Value);
    if (string.IsNullOrWhiteSpace(dostawca)) return;
    
    // Użyj PREMIUM Window
    var window = new HistoriaHodowcyWindowPremium(connectionPermission, dostawca);
    
    // Ustaw owner dla lepszego centrowania
    var helper = new System.Windows.Interop.WindowInteropHelper(window);
    helper.Owner = this.Handle;
    
    window.ShowDialog();
    await Task.CompletedTask;
}
```

---

## ✅ TO WSZYSTKO!

**NIE** zmieniaj nic innego w WidokKalendarza.cs!

Namespace pozostaje: `namespace Kalendarz1` (lub jakkolwiek masz teraz)
