# Instrukcja integracji kreatora importu specyfikacji z Excel/LibreOffice

## 1. Wymagane pakiety NuGet

Dodaj następujące pakiety do projektu:

```xml
<PackageReference Include="ClosedXML" Version="0.102.2" />
```

Lub przez Package Manager Console:
```
Install-Package ClosedXML
```

## 2. Pliki do dodania

Skopiuj następujące pliki do folderu `Zywiec/WidokSpecyfikacji/`:
- `ImportSpecyfikacjeWizard.xaml`
- `ImportSpecyfikacjeWizard.xaml.cs`

## 3. Integracja z WidokSpecyfikacje.xaml

### 3.1. Dodaj przycisk "Import" w pasku narzędzi

Znajdź w pliku `WidokSpecyfikacje.xaml` miejsce gdzie są przyciski (np. przy przycisku "Dodaj specyfikację") 
i dodaj nowy przycisk:

```xml
<!-- Przycisk Import z Excel -->
<Button x:Name="btnImport" 
        Content="📥 Import z Excel" 
        Padding="15,8"
        Margin="0,0,10,0"
        Background="#FF5722"
        Foreground="White"
        FontWeight="SemiBold"
        BorderThickness="0"
        Cursor="Hand"
        Click="BtnImport_Click"
        ToolTip="Importuj specyfikacje z pliku Excel/LibreOffice">
    <Button.Template>
        <ControlTemplate TargetType="Button">
            <Border Background="{TemplateBinding Background}" 
                    CornerRadius="6" 
                    Padding="{TemplateBinding Padding}">
                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Border>
        </ControlTemplate>
    </Button.Template>
    <Button.Style>
        <Style TargetType="Button">
            <Setter Property="Background" Value="#FF5722"/>
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter Property="Background" Value="#E64A19"/>
                </Trigger>
            </Style.Triggers>
        </Style>
    </Button.Style>
</Button>
```

### 3.2. Dodaj obsługę kliknięcia w WidokSpecyfikacje.xaml.cs

Dodaj metodę obsługi kliknięcia:

```csharp
/// <summary>
/// Otwiera kreator importu specyfikacji z pliku Excel/LibreOffice
/// </summary>
private void BtnImport_Click(object sender, RoutedEventArgs e)
{
    try
    {
        var wizard = new Kalendarz1.Zywiec.WidokSpecyfikacji.ImportSpecyfikacjeWizard(connectionString);
        
        // Callback do odświeżenia danych po imporcie
        wizard.OnImportCompleted = () =>
        {
            Dispatcher.Invoke(() =>
            {
                // Odśwież dane w DataGrid
                if (dateTimePicker1.SelectedDate.HasValue)
                {
                    LoadData(dateTimePicker1.SelectedDate.Value);
                }
            });
        };
        
        wizard.Owner = this;
        wizard.ShowDialog();
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Błąd otwierania kreatora importu:\n{ex.Message}",
            "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

## 4. Dostosowanie do struktury bazy danych

### 4.1. Sprawdź nazwy kolumn w tabeli FarmerCalc

Kreator zakłada następującą strukturę tabeli `dbo.FarmerCalc`:

| Kolumna | Typ | Opis |
|---------|-----|------|
| ID | INT | Klucz główny (auto) |
| CalcDate | DATE | Data uboju |
| CarLp | INT | Numer auta |
| CustomerGID | VARCHAR | ID dostawcy |
| DeclI1 | INT | Sztuki deklarowane |
| DeclI2 | INT | Padłe |
| DeclI3 | INT | Chore (CH) |
| DeclI4 | INT | Niedowaga (NW) |
| DeclI5 | INT | Zmiażdżone (ZM) |
| LumQnt | INT | Sztuki LUMEL |
| ProdQnt | INT | Sztuki produkcja |
| ProdWgt | DECIMAL | Kilogramy produkcja |
| Price | DECIMAL | Cena |
| Addition | DECIMAL | Dodatek do ceny |
| Loss | DECIMAL | Ubytek (%) |
| IncDeadConf | BIT | Czy odliczać PiK |
| NettoWeight | DECIMAL | Waga netto |
| PriceTypeID | INT | ID typu ceny |
| IncPiK | BIT | Flaga PiK |
| FarmerBrutto | DECIMAL | Brutto hodowcy |
| FarmerTara | DECIMAL | Tara hodowcy |
| SlaughterBrutto | DECIMAL | Brutto ubojni |
| SlaughterTara | DECIMAL | Tara ubojni |

### 4.2. Jeśli brakuje kolumn wag, dodaj je:

```sql
-- Dodanie kolumn wag jeśli nie istnieją
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.FarmerCalc') AND name = 'FarmerBrutto')
BEGIN
    ALTER TABLE dbo.FarmerCalc ADD FarmerBrutto DECIMAL(18,2) DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.FarmerCalc') AND name = 'FarmerTara')
BEGIN
    ALTER TABLE dbo.FarmerCalc ADD FarmerTara DECIMAL(18,2) DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.FarmerCalc') AND name = 'SlaughterBrutto')
BEGIN
    ALTER TABLE dbo.FarmerCalc ADD SlaughterBrutto DECIMAL(18,2) DEFAULT 0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.FarmerCalc') AND name = 'SlaughterTara')
BEGIN
    ALTER TABLE dbo.FarmerCalc ADD SlaughterTara DECIMAL(18,2) DEFAULT 0;
END
```

## 5. Mapowanie kolumn Excel → Baza

Kreator odczytuje dane z arkusza "Wpisywałka" w następujący sposób:

| Kolumna Excel | Litera | Pole w bazie |
|---------------|--------|--------------|
| Nr kolejności | A | CarLp |
| Nr specyfikacji | B | (informacyjnie) |
| Dostawca | C | CustomerGID (przez mapowanie) |
| Sztuki dek | D | DeclI1 |
| Padłe | E | DeclI2 |
| CH | F | DeclI3 |
| NW | G | DeclI4 |
| ZM | H | DeclI5 |
| Hodowca Brutto | I | FarmerBrutto |
| Hodowca Tara | J | FarmerTara |
| Ubojnia Brutto | K | SlaughterBrutto |
| Ubojnia Tara | L | SlaughterTara |
| LUMEL | M | LumQnt |
| Sztuki Produkcja | N | ProdQnt |
| KG Produkcja | O | ProdWgt |
| Typ Ceny | P | PriceTypeID |
| Cena 1 | Q | Price (lub średnia z Q i T) |
| Typ 1 (łączona) | R | (do ustalenia typu) |
| Typ 2 (łączona) | S | (do ustalenia typu) |
| Cena 2 | T | (do średniej) |
| Dodatek | U | Addition |
| PiK | V | IncPiK, IncDeadConf |
| Ubytek | W | Loss |
| Data uboju | B21 lub wiersz z "Data" | CalcDate |

## 6. Klasa DostawcaItem

Jeśli w projekcie istnieje już klasa `DostawcaItem`, usuń definicję z pliku 
`ImportSpecyfikacjeWizard.xaml.cs` i użyj using do istniejącej.

Jeśli klasa jest w przestrzeni nazw `Kalendarz1`, zmień:
```csharp
// W ImportSpecyfikacjeWizard.xaml.cs dodaj using:
using Kalendarz1; // jeśli DostawcaItem jest tam zdefiniowany

// I usuń lokalną definicję klasy DostawcaItem na końcu pliku
```

## 7. Testowanie

1. Uruchom aplikację
2. Kliknij przycisk "Import z Excel"
3. Wybierz plik Excel ze specyfikacjami
4. Wybierz arkusz "Wpisywałka"
5. Sprawdź podgląd danych
6. Zmapuj dostawców (kreator próbuje automatycznie dopasować)
7. Kliknij "Importuj"
8. Sprawdź czy dane pojawiły się w głównym widoku

## 8. Obsługa błędów

Kreator loguje błędy do Debug Output. Aby zobaczyć szczegóły:
- Visual Studio: View → Output → Debug

## 9. Rozszerzenia (opcjonalne)

### 9.1. Obsługa plików ODS (LibreOffice)
Aby obsługiwać pliki .ods, można użyć biblioteki jak `NPOI` lub przekonwertować 
plik do .xlsx przez LibreOffice CLI.

### 9.2. Historia importów
Można dodać tabelę do logowania importów:

```sql
CREATE TABLE dbo.ImportHistory (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    ImportDate DATETIME DEFAULT GETDATE(),
    FileName NVARCHAR(500),
    CalcDate DATE,
    RowCount INT,
    ImportedBy NVARCHAR(100)
);
```

---

## Wsparcie

W razie problemów sprawdź:
1. Czy pakiet ClosedXML jest zainstalowany
2. Czy connection string jest poprawny
3. Czy struktura tabeli FarmerCalc zgadza się z oczekiwaną
4. Czy użytkownik ma uprawnienia do INSERT/DELETE w tabeli
