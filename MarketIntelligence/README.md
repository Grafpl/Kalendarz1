# MarketIntelligence / Briefing AI — README

Moduł osobistego agenta wiadomości branżowych (drób + mięso) dla ubojni Piórkowscy.
WPF .NET 8, MVVM (jedyny moduł z MVVM w repo), surowy ADO.NET (bez EF/ORM).

## Bazy danych

- **LibraNet** (192.168.0.109) — tabele `intel_*` + zamówienia mięsa.
- **HANDEL** (192.168.0.112, Sage Symfonia) — kontekst biznesowy (klienci/dostawcy/ceny).

## Sekrety (`secrets.json`)

Plik POZA repo: `%LOCALAPPDATA%\Kalendarz1\MarketIntelligence\secrets.json`

```json
{
  "ANTHROPIC_API_KEY": "sk-ant-...",
  "PERPLEXITY_API_KEY": "pplx-...",
  "PERPLEXITY_DAILY_LIMIT": "30",
  "LIBRANET_CONNECTION_STRING": "Server=192.168.0.109;Database=LibraNet;User Id=...;Password=...;TrustServerCertificate=True",
  "HANDEL_CONNECTION_STRING": "Server=192.168.0.112;Database=Handel;User Id=zpsp_intel_reader;Password=...;TrustServerCertificate=True"
}
```

Kolejność rozwiązywania connection stringów: **zmienna środowiskowa → secrets.json → hardcoded fallback**.

## ⚠ Bezpieczeństwo — zejście z hardcoded `sa`

Obecnie, jeśli brak `HANDEL_CONNECTION_STRING` w env/secrets, kod używa **hardcoded loginu `sa`**
(ostatni fallback) i wypisuje ostrzeżenie w logach przy starcie. Żeby to naprawić:

### 1. Utwórz dedykowanego read-only usera na serwerze HANDEL (192.168.0.112)

```sql
-- Na instancji HANDEL (SQL Server 2019), w bazie master:
USE master;
CREATE LOGIN zpsp_intel_reader WITH PASSWORD = 'WYBIERZ_MOCNE_HASLO', CHECK_POLICY = ON;

USE Handel;
CREATE USER zpsp_intel_reader FOR LOGIN zpsp_intel_reader;

-- Minimalne uprawnienia: tylko SELECT na schemacie używanym przez ContextBuilderService.
GRANT SELECT ON SCHEMA::HM TO zpsp_intel_reader;          -- dokumenty/faktury (HM.DK, HM.MG)
GRANT SELECT ON SCHEMA::SSCommon TO zpsp_intel_reader;    -- kontrahenci (SSCommon.STContractors)
-- (NIE nadawaj db_owner ani write. Moduł tylko czyta z HANDEL.)
```

### 2. Wpisz connection string do secrets.json

```json
"HANDEL_CONNECTION_STRING": "Server=192.168.0.112;Database=Handel;User Id=zpsp_intel_reader;Password=WYBIERZ_MOCNE_HASLO;TrustServerCertificate=True"
```

### 3. Zweryfikuj

Po restarcie aplikacji ostrzeżenie „⚠ SECURITY: hardcoded `sa`" w logach **ma zniknąć**.
Jeśli nadal jest — klucz w secrets.json jest źle nazwany lub pusty.

> Uwaga: hardcoded `sa` jest też w historii git. Tego dokument nie usuwa (świadoma decyzja).
> Realnie warto z czasem zrotować hasło `sa` na serwerze HANDEL.

## Flagi funkcji (`BriefingFeatureFlags.cs`)

Martwe etapy pipeline'u są domyślnie WYŁĄCZONE (zwracały 0):
- `EnableScrapingSources` (scrapery HTML PIORiN/KOWR/KRIR/WIR) — OFF
- `EnableHpaiScraper` (GLW wetgiw.gov.pl, zwraca 404) — OFF
- `EnablePriceScrapers` (MRiRW + MATIF) — OFF
- `EnableContentEnrichment` — ON, ale tylko whitelista domen (`EnrichmentDomainWhitelist`)

Kod scraperów zostaje w repo. Aby włączyć z powrotem — ustaw flagę na `true`.
