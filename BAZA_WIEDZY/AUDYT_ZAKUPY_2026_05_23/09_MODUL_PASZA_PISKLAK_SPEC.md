# Część 9 (rozszerzenie) — Spec modułu Zakup Paszy i Piskląt

> Wskrzeszenie martwego kafelka `ZakupPaszyPisklak` (Menu.cs:1537, FormFactory=null). Z Części 1 audytu: kafelek wisi w UI i nic nie robi (na weekend ukrywamy — QW1; tu pełna spec docelowa Q3 2026).

---

## 0. Po co ten moduł

**Dziś:** faktury pasz (TASOMIX/De Heus/Ekoplon) i piskląt (wylęgarnie) wpisuje się **ręcznie w Symfonii**, a rozliczenie z hodowcami robi Asia w Excelu (instrukcja Magdy #9).

**Problem:** **druga oś surowca (pasza + pisklęta) nie jest śledzona w ZPSP** — Ser nie widzi:
- Ile wydaliśmy na paszę per hodowca
- Czy koszt paszy/piskląt jest prawidłowo potrącany z należności hodowcy
- Trendów cenowych pasz (kiedy TASOMIX podniósł cenę)

**Cel modułu:** ewidencja zakupów pasz/piskląt + automatyczne rozliczenie per hodowca + integracja z kontraktami.

---

## 1. Powiązanie biznesowe

```
Wylęgarnia → pisklęta → Hodowca (wstawienie, dzień 0)
                              ↓
Dostawca paszy → pasza → Hodowca (przez cykl 35-42 dni)
                              ↓
                       Hodowca tuczy
                              ↓
                Żywiec → Piórkowscy (odbiór)
                              ↓
        Rozliczenie: cena żywca − koszt paszy − koszt piskląt = wypłata hodowcy
```

**Kluczowa logika:** w niektórych modelach **Piórkowscy finansują paszę i pisklęta** (kontrakt zintegrowany), potem potrącają z należności za żywiec. To trzeba śledzić.

---

## 2. SQL Schema

```sql
-- ════════════════════════════════════════════════════════════════════════════
-- ZAKUP PASZY I PISKLĄT v1 (Q3 2026)
-- ════════════════════════════════════════════════════════════════════════════

-- Dostawcy pasz / wylęgarnie
CREATE TABLE dbo.DostawcyPaszPisklat (
    Id           INT IDENTITY PRIMARY KEY,
    Nazwa        NVARCHAR(200) NOT NULL,     -- 'TASOMIX', 'De Heus', 'Ekoplon', 'Wylęgarnia X'
    Typ          VARCHAR(20) NOT NULL,       -- 'PASZA' / 'PISKLETA'
    Nip          VARCHAR(15) NULL,
    Aktywny      BIT NOT NULL DEFAULT 1,
    CONSTRAINT CK_DostPasz_Typ CHECK (Typ IN ('PASZA','PISKLETA'))
);
GO

-- Faktury / dostawy paszy i piskląt
CREATE TABLE dbo.ZakupyPaszPisklat (
    Id              INT IDENTITY PRIMARY KEY,
    DostawcaPaszId  INT NOT NULL,            -- FK → DostawcyPaszPisklat
    Typ             VARCHAR(20) NOT NULL,    -- PASZA / PISKLETA
    NumerFaktury    VARCHAR(50) NULL,        -- nr faktury z Symfonii / dokumentu
    DataFaktury     DATE NOT NULL,
    DataDostawy     DATE NULL,

    -- Komu dostarczone (hodowca docelowy)
    HodowcaId       INT NULL,                -- FK → DOSTAWCY (NULL = magazyn własny)
    WstawienieLp    INT NULL,                -- powiązanie z konkretnym cyklem (WstawieniaKurczakow.Lp)

    -- Pozycje
    Produkt         NVARCHAR(100) NOT NULL,  -- 'Pasza Starter/Grower/Finisher' / 'Pisklęta Cobb/Ross'
    Ilosc           DECIMAL(12,2) NOT NULL,  -- kg dla paszy, sztuki dla piskląt
    JednostkaMiary  VARCHAR(10) NOT NULL,    -- 'kg' / 'szt'
    CenaJednostkowa DECIMAL(10,4) NOT NULL,  -- netto
    WartoscNetto    AS (Ilosc * CenaJednostkowa) PERSISTED,
    StawkaVat       DECIMAL(5,2) NOT NULL DEFAULT 8.00,

    -- Rozliczenie z hodowcą
    DoPotraceniaodHodowcy BIT NOT NULL DEFAULT 0,  -- czy potrącamy z należności
    Rozliczone      BIT NOT NULL DEFAULT 0,
    RozliczoneKiedy DATETIME2 NULL,

    -- Audyt
    WprowadzilUserId VARCHAR(20) NOT NULL,
    WprowadzonyKiedy DATETIME2 NOT NULL DEFAULT GETDATE(),
    Notatka         NVARCHAR(500) NULL,

    CONSTRAINT FK_ZakupyPP_Dostawca FOREIGN KEY (DostawcaPaszId) REFERENCES dbo.DostawcyPaszPisklat(Id),
    CONSTRAINT FK_ZakupyPP_Hodowca FOREIGN KEY (HodowcaId) REFERENCES dbo.DOSTAWCY(ID),
    CONSTRAINT CK_ZakupyPP_Typ CHECK (Typ IN ('PASZA','PISKLETA'))
);
GO
CREATE INDEX IX_ZakupyPP_Hodowca ON dbo.ZakupyPaszPisklat(HodowcaId, Rozliczone);
CREATE INDEX IX_ZakupyPP_Wstawienie ON dbo.ZakupyPaszPisklat(WstawienieLp);
CREATE INDEX IX_ZakupyPP_Data ON dbo.ZakupyPaszPisklat(DataFaktury);
GO

-- Słownik produktów (do ComboBox)
CREATE TABLE dbo.ProduktyPaszPisklat (
    Id          INT IDENTITY PRIMARY KEY,
    Nazwa       NVARCHAR(100) NOT NULL,
    Typ         VARCHAR(20) NOT NULL,        -- PASZA / PISKLETA
    StawkaVat   DECIMAL(5,2) NOT NULL DEFAULT 8.00,
    Aktywny     BIT NOT NULL DEFAULT 1
);
GO
INSERT INTO dbo.ProduktyPaszPisklat (Nazwa, Typ, StawkaVat) VALUES
  ('Pasza Starter', 'PASZA', 8.00),
  ('Pasza Grower', 'PASZA', 8.00),
  ('Pasza Finisher', 'PASZA', 8.00),
  ('Pisklęta Cobb 500', 'PISKLETA', 8.00),
  ('Pisklęta Ross 308', 'PISKLETA', 8.00);
GO
```

---

## 3. Widok główny + UI

### 3.1 `ZakupPaszWindow.xaml` (mockup)

```
┌══════════════════════════════════════════════════════════════════════════╗
║  🌾 Zakup Paszy i Piskląt                                                ║
╠══════════════════════════════════════════════════════════════════════════╣
║  [➕ Nowa faktura] [📊 Rozliczenia per hodowca] [📈 Trendy cen]          ║
║                                                                            ║
║  🔍 [____] [Typ: Wszystkie ▼] [Hodowca: Wszyscy ▼] [Okres: maj 2026 ▼]  ║
║                                                                            ║
║  ┌──────────────────────────────────────────────────────────────────┐   ║
║  │ Data    Dostawca   Typ     Produkt        Ilość  Cena  Wartość Hod│   ║
║  │ 20.05   TASOMIX    Pasza   Finisher      5000kg  2.50  12500  KOWA│   ║
║  │ 18.05   Wylęgarnia Pisklę  Ross 308     30000sz  2.80  84000  KOWA│   ║
║  │ ...                                                                │   ║
║  └──────────────────────────────────────────────────────────────────┘   ║
║                                                                            ║
║  Suma maj: pasza 145 000 zł | pisklęta 320 000 zł | do potrącenia 280k  ║
╚══════════════════════════════════════════════════════════════════════════╝
```

### 3.2 Główne ekrany
1. **Lista faktur** (DataGrid + filtry) — typ, hodowca, okres
2. **Nowa faktura** (formularz — dostawca, produkt, ilość, cena, hodowca docelowy, czy potrącać)
3. **Rozliczenia per hodowca** — ile pasz/piskląt poszło do hodowcy X, ile do potrącenia z żywca
4. **Trendy cen** — wykres ceny TASOMIX/De Heus w czasie

---

## 4. Integracje

### 4.1 Z Wstawieniami
- Faktura piskląt **łączona z konkretnym wstawieniem** (`WstawienieLp`)
- Z karty wstawienia widać: "pisklęta od Wylęgarnia X, 30000 szt, 84000 zł"

### 4.2 Z Rozliczeniami hodowców
- Przy rozliczeniu żywca system pokazuje: "do potrącenia: pasza 12500 + pisklęta 84000 = 96500 zł"
- Wypłata hodowcy = wartość żywca − potrącenia

### 4.3 Z Symfonią
- **Faza 1:** ręczne wpisanie w obu (Symfonia + ZPSP) — Magda wpisuje 2×
- **Faza 2 (opcjonalnie):** sync — faktura w ZPSP generuje zapis w Symfonii (jak ContractorClassification triggery)

### 4.4 Z Kontraktami
- Kontrakt może mieć flagę "model zintegrowany" (finansujemy paszę/pisklęta)
- Wtedy automatycznie `DoPotraceniaodHodowcy = 1`

---

## 5. Wpięcie do menu (wskrzeszenie kafelka)

W `Menu.cs:1537` — **odkomentuj** (jeśli QW1 zakomentował) i podmień `null` na factory:

```csharp
new MenuItemConfig("ZakupPaszyPisklak", "Zakup Paszy i Piskląt",
    "Ewidencja zakupów pasz (TASOMIX/De Heus/Ekoplon) i piskląt + rozliczenie per hodowca",
    Color.FromArgb(27, 94, 32),
    () => new Kalendarz1.PaszaPisklak.ZakupPaszWindow(), "🌾", "Pasza"),  // ← było: null
```

`accessMap[01]` już istnieje — nie ruszać.

---

## 6. Plan wdrożenia

| Faza | Co | Effort |
|---|---|---|
| **F1** | SQL schema (3 tabele + słowniki) | 1 dzień |
| **F2** | `ZakupPaszService` (CRUD) + lista UI | 2 dni |
| **F3** | Formularz nowej faktury + walidacje | 1 dzień |
| **F4** | Rozliczenia per hodowca (widok + logika potrąceń) | 2 dni |
| **F5** | Trendy cen (wykres LiveCharts) | 1 dzień |
| **F6** | Integracja z Wstawieniami + Rozliczeniami | 2 dni |
| **F7** (opc.) | Sync z Symfonią | 3 dni |
| **Razem** | | **9-12 dni** Sera (Q3 2026) |

---

## 7. Wartość biznesowa

| Korzyść | Dla kogo |
|---|---|
| Ser widzi koszt paszy/piskląt per hodowca | Ser (kontrola marży) |
| Automatyczne potrącenia z należności | Asia (mniej Excela) |
| Magda wpisuje raz zamiast w Excelu Asi + Symfonii | Magda (mniej klikania — obietnica Sera) |
| Trendy cen pasz (kiedy podnieść cenę żywca hodowcom) | Ser (strategia) |
| Pełen obraz kosztów surowca (żywiec + pasza + pisklęta) | Ser (rentowność) |

---

## 8. Czego NIE robić w tym module

- ❌ Nie duplikować pełnej księgowości Symfonii — to **ewidencja operacyjna**, nie system finansowo-księgowy
- ❌ Nie wdrażać przed Magdą / przed Kontraktami — to Q3, niższy priorytet niż ARiMR
- ❌ Nie komplikować integracją Symfonia w Fazie 1 — najpierw ręczne 2×, potem sync jeśli się sprawdzi

---

## 📌 Podsumowanie

| Co | Wartość |
|---|---|
| **Status dziś** | Martwy kafelek (null factory) |
| **Weekend (QW1)** | Ukryć z UI |
| **Q3 2026** | Pełen moduł — 9-12 dni Sera |
| **Priorytet** | Niższy niż Kontrakty/Centrum Asi (po ARiMR) |
| **Główna wartość** | Druga oś surowca pod kontrolą + mniej klikania dla Magdy |

---

*Wersja 1.0 • 24.05.2026 • Wskrzeszenie martwego kafelka. Domyka audyt kafelków (Część 1).*
