# 11. Digital Inspection Sheet — tablet weterynarza

## Co to jest
Tablet zamiast papieru dla **weterynarza** wykonującego post-mortem inspection. Wpisy od razu w bazie zamiast przepisywania z kartki.

## Wartość
- **Eliminacja papier-do-Excela** (1-2h dziennie pracy biurowej)
- **Konkretne dane** zamiast szacunkowych
- **Wymóg BRC v9 sek. 3** (62% braków!)
- **Argumenty do renegocjacji** z hodowcami
- **~150-200k PLN/rok**

## Hardware
- **Samsung Galaxy Tab Active3** (8") — ~2000 zł, IP68, rękawice
- **Stojak ścienny** w rampie weterynarza — 200 zł
- **WiFi access point** w hali (jeśli słaby zasięg) — 500 zł

## Database
```sql
CREATE TABLE WetInspectionRecord (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    PartiaId INT NOT NULL,
    WetInspectorId NVARCHAR(50) NOT NULL,
    InspectionDateTime DATETIME NOT NULL,
    LiczbaSztukSprawdzonych INT NULL,
    -- Po typie wady (zgodnie z 12 typami z PDF Broiler)
    Ascites INT DEFAULT 0,
    Cellulitis INT DEFAULT 0,
    HematomyPierś INT DEFAULT 0,
    HematomyUdo INT DEFAULT 0,
    HematomyPodudzie INT DEFAULT 0,
    PopOutSkrzydlo INT DEFAULT 0,
    PopOutUdo INT DEFAULT 0,
    Zlamania INT DEFAULT 0,
    WhiteStriping INT DEFAULT 0,
    WoodenBreast INT DEFAULT 0,
    SpaghettiMeat INT DEFAULT 0,
    Inne INT DEFAULT 0,
    -- Suma
    OdrzutyTotal AS (Ascites + Cellulitis + HematomyPierś + HematomyUdo + HematomyPodudzie + 
                     PopOutSkrzydlo + PopOutUdo + Zlamania + WhiteStriping + WoodenBreast + 
                     SpaghettiMeat + Inne),
    ProcentOdrzutowOgolem AS CAST(OdrzutyTotal AS DECIMAL(10,4)) / NULLIF(LiczbaSztukSprawdzonych, 0) * 100,
    NotatkiWet NVARCHAR(2000) NULL,
    ZdjeciaPath NVARCHAR(MAX) NULL  -- JSON list
);
```

## UI (Blazor lub MAUI)
- Po jednym kliknięciu na ikonę wady — increment counter
- Foto opcjonalnie (integracja z #28)
- Auto-save co 30 sek (jeśli sieć padnie, lokalny buffer)
- Podsumowanie partii: liczby + %

## Workflow
1. Weterynarz loguje na tablet
2. Wybiera partię
3. Klika ikony co tuszka odrzucona
4. Robi zdjęcia konkretnych przypadków
5. Wysyła do bazy → koniec partii

## Integracja z #10, #12, #15, #17
- Wszystkie te pomysły bazują na tych samych danych — jedna tabela = mniej duplikacji
- Raporty per hodowca / per linia / per zmiana auto-generated

## Czas: ~40h kodu + 2500 zł hardware

## Powiązania: [10], [12], [15], [17], [28]
