# ZPSP.Sales - Dziennik Zmian

Wszystkie istotne zmiany w module sprzedazy beda dokumentowane w tym pliku.

Format oparty na [Keep a Changelog](https://keepachangelog.com/pl/1.0.0/).

## [2.0.0] - 2026-01-16

### Dodano
- **Architektura MVVM** - pelna separacja warstw (Models, ViewModels, Views, Services, Repositories)
- **Dependency Injection** z Microsoft.Extensions.DependencyInjection
- **Scentralizowane zapytania SQL** w klasie `SqlQueries.cs`
- **Batch loading** - eliminacja problemu N+1 przy ladowaniu pozycji zamowien
- **Centralny CacheService** z TTL dla kontrahentow, produktow i wydan
- **BaseViewModel** z obsluga INotifyPropertyChanged, ladowania i bledow
- **RelayCommand / AsyncRelayCommand** dla komend w UI
- **Interfejsy serwisow i repozytoriow** dla testowalnosci
- **Dokumentacja** - README.md, ARCHITECTURE.md, DATABASE.md

### Zmieniono
- **OrderRepository** - uzywa scentralizowanych zapytan zamiast inline SQL
- **Parametryzacja zapytan** - wszystkie zapytania sa parametryzowane (bezpieczenstwo)
- **Async/await** - konsekwentne uzycie w calym kodzie
- **Connection strings** - przeniesione do centralnej klasy `DatabaseConnections`

### Usunieto
- Inline SQL rozproszone po kodzie (przeniesione do SqlQueries.cs)
- Bezposrednie tworzenie SqlConnection w ViewModels

### Bezpieczenstwo
- Eliminacja ryzyka SQL injection przez parametryzacje
- Usuniecie hardkodowanych hasel z kodu (przeniesione do konfiguracji)

---

## [1.x.x] - Historia przed refaktoryzacja

### Funkcjonalnosci istniejace przed refaktoryzacja:
- Lista zamowien na wybrany dzien
- Filtrowanie po odbiorcy i produkcie
- Anulowanie i przywracanie zamowien
- Duplikowanie zamowien
- Agregacje produktow (bilansowanie)
- Dashboard z KPI
- Integracja z transportem
- Historia zmian

---

## Planowane

### [2.1.0]
- [ ] Testy jednostkowe dla serwisow (xUnit + Moq)
- [ ] Dodanie Serilog do logowania
- [ ] Eksport raportow do Excel/PDF

### [2.2.0]
- [ ] Optymalizacja widokow SQL
- [ ] Indeksy dla czestych zapytan
- [ ] Stored procedures dla zlozonych operacji

### [3.0.0]
- [ ] Migracja do .NET MAUI (cross-platform)
- [ ] API REST dla klientow mobilnych
