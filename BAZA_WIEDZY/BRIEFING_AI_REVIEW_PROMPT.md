# PROMPT DO PRZEGLĄDU: Moduł "Briefing AI / Centrum Wiadomości AI"

> **Instrukcja dla mnie (użytkownika):** skopiuj CAŁY ten dokument do Claude.ai (web).
> Na końcu jest sekcja „CZEGO OD CIEBIE OCZEKUJĘ" z pytaniami do recenzenta.

---

## 0. Rola dla Ciebie, Claude

Jesteś starszym architektem oprogramowania i specjalistą od systemów AI/RAG oraz aplikacji
.NET/WPF. Poniżej masz pełny opis modułu, który zbudowałem w mojej firmowej aplikacji.
**Twoje zadanie: ocenić czy jest dobrze zaprojektowany i zaproponować konkretne zmiany.**
Bądź krytyczny, rzeczowy, wskazuj realne problemy (architektura, koszty, niezawodność,
bezpieczeństwo, jakość danych, UX). Nie chwal na siłę.

---

## 1. Kontekst biznesowy

- **Firma:** Ubojnia Drobiu „Piórkowscy" (ZPSP) — zakład drobiarski, ~258 mln zł obrotu,
  ~200 t/dzień, lokalizacja Brzeziny (woj. łódzkie).
- **Aplikacja:** „Kalendarz1" (wewnętrznie ZPSP) — wewnętrzny system produkcyjno-handlowy.
  WPF .NET 8.0 (target `net8.0-windows7.0`), wzorzec **code-behind** (bez MVVM) w całej apce
  **z wyjątkiem tego modułu** (Briefing używa MVVM — jedyny taki w repo).
- **Autor:** właściciel firmy, samouk, programuje w C#/.NET ~5 lat. Klient = developer.
- **Cel modułu Briefing:** osobisty agent AI śledzący wiadomości z branży drobiarskiej i mięsnej.
  Codziennie ma skanować polskie (i trochę zagranicznych) źródła, streszczać je, dawać opinię
  „co to znaczy dla nas" z perspektywy CEO/Sales/Buyer, i pozwalać dopytywać przez chat AI,
  który zna kontekst firmy i historię newsów.

---

## 2. Stack technologiczny

| Warstwa | Technologia |
|---|---|
| UI | WPF (.NET 8.0), C# 12, nullable reference types ON |
| Wzorzec | MVVM (ViewModel + ObservableCollection + RelayCommand) |
| Bazy danych | SQL Server (2 instancje, patrz niżej) |
| Dostęp do DB | `Microsoft.Data.SqlClient`, surowy ADO.NET (bez ORM/EF) |
| AI — analiza | **Anthropic Claude API** |
| AI — wyszukiwanie | **Perplexity Sonar API** |
| RSS | `System.ServiceModel.Syndication` (SyndicationFeed) |
| HTML scraping | `HttpClient` + parsowanie ręczne/regex |
| Sekrety | plik `secrets.json` w `%LOCALAPPDATA%\Kalendarz1\MarketIntelligence\` (poza repo) |

### Modele AI (ważne — zmieniane w trakcie życia projektu)
- **Filtr trafności + tłumaczenia EN→PL:** `claude-haiku-4-5-20251001` (Haiku 4.5) — tani.
- **Analiza artykułów + dzienne streszczenie + chat:** `claude-sonnet-4-6` (Sonnet 4.6).
  - Uwaga: wcześniej do analizy używaliśmy Opus 4.7, ale **odrzucony** — zbyt wolny, ucinał
    output przy ~2500 tokenach i nie wspierał `temperature` (HTTP 400). Sonnet 4.6 = 3× szybszy,
    5× tańszy, `max_tokens=8000`, `temperature=0.2` dla determinizmu.
- **Prompt caching:** system prompt cache'owany `cache_control: ephemeral` (TTL 5 min).

---

## 3. Bazy danych i tabele

### Instancja 1: LibraNet (192.168.0.109, SQL Server 2022)
Tu żyją wszystkie tabele modułu (prefix `intel_`) oraz źródłowe zamówienia mięsa.

| Tabela | Rola | Przybl. liczność |
|---|---|---|
| `intel_Articles` | pobrane + przeanalizowane artykuły (tytuł, treść, kategoria, severity, RelevanceScore, CeoAnalysis/SalesAnalysis/BuyerAnalysis, EducationalContent, FetchedAt, PublishDate, UrlHash) | ~377 |
| `intel_HpaiAlerts` | alerty ptasiej grypy (scraper GLW) | **0 (scraper 404 — nie działa)** |
| `intel_FetchLog` | historia pobrań (RSS/scrap/relevant/analyzed/HPAI/czas/sukces/błąd) | ~15 |
| `intel_Prices` | ceny (drób, zboża) — scrapery zwracają 0 | ~29 (stare) |
| `intel_Sources` | **własne źródła użytkownika** (URL, RSS/WebScraping, kategoria, język, priorytet, tematy) | 1 (user) |
| `intel_DailySummary` | dzienne streszczenie (headline, CeoSummary/SalesSummary/BuyerSummary, MarketMood, TopAlerts JSON, ActionItems JSON, liczniki severity) | ~2 |
| `intel_UserQueries` | **własne tematy/zapytania użytkownika** do Perplexity (QueryText, Category, Priority, Enabled, RecencyFilter) | 3 |
| `intel_ArticleFeedback` | feedback 👍/👎 per artykuł (ArticleId, UserId, Vote, Category, SourceName) | nowa |

Dodatkowo czyta: `dbo.ZamowieniaMieso` + `ZamowieniaMiesoTowar` (zamówienia mięsa, do BusinessContext).

### Instancja 2: HANDEL (192.168.0.112, SQL Server 2019, Sage Symfonia)
Używane przez `ContextBuilderService` do zbudowania kontekstu biznesowego dla AI
(TOP klienci, dostawcy, ceny, konkurenci). Tabele Sage: `HM.DK` (dokumenty/faktury),
`HM.MG`, `SSCommon.STContractors` (kontrahenci — pole `Name`, NIE `Name1`).

### Sekrety / connection stringi
- Klucze API i connection stringi w `secrets.json` (poza repo), z fallbackiem do zmiennych
  środowiskowych i (legacy) hardcoded w `MarketIntelligenceConfig.cs`.
- **⚠ ZNANY DŁUG:** w pewnym momencie wklejono na sztywno w kod connection string do HANDEL
  z loginem `sa` i hasłem (świadoma decyzja właściciela, ale to ryzyko — trafiło do historii git).

---

## 4. Architektura plików (folder `MarketIntelligence/`)

```
MarketIntelligence/
├── MarketIntelligenceConfig.cs          # connection stringi + ścieżki sekretów
├── Views/
│   ├── PorannyBriefingWindow.xaml(.cs)   # GŁÓWNE OKNO (tytuł "Centrum Wiadomości AI")
│   ├── BriefingOnePagerWindow.xaml(.cs)  # Podsumowanie dnia + chat AI + feedback 👍/👎
│   ├── SourcesManagementWindow.xaml(.cs) # "Moje źródła" — CRUD intel_Sources + auto-detect RSS
│   ├── UserQueriesManagementWindow.xaml(.cs) # "Moje tematy" — CRUD intel_UserQueries
│   ├── BriefingDiagnosticsWindow.xaml(.cs)   # Diagnostyka (5 zakładek: status/historia/log/zapytania/źródła)
│   └── BriefingHelpWindow.xaml(.cs)      # Przewodnik ("Jak to działa")
├── ViewModels/
│   └── PorannyBriefingViewModel.cs       # ~380 linii (po refaktorze; było 2190)
├── Services/
│   ├── NewsFetchOrchestrator.cs          # PIPELINE pobierania (serce modułu)
│   ├── BriefingDataLoaderService.cs      # most ViewModel ↔ orchestrator (singleton)
│   ├── SourcesService.cs                 # intel_Sources + auto-detect RSS (9 ścieżek)
│   ├── UserQueriesService.cs             # intel_UserQueries CRUD
│   ├── FeedbackService.cs                # intel_ArticleFeedback (👍/👎)
│   ├── BriefingChatContextBuilder.cs     # buduje system prompt dla chatu
│   ├── DiagnosticsReportGenerator.cs     # raport diagnostyczny
│   ├── AI/
│   │   ├── ClaudeAnalysisService.cs      # wszystkie calle do Claude (Haiku+Sonnet)
│   │   ├── ContextBuilderService.cs      # kontekst biznesowy z HANDEL+LibraNet
│   │   └── ContentEnrichmentService.cs   # dociąganie pełnej treści artykułu
│   └── DataSources/
│       ├── RssFeedService.cs             # 45 źródeł RSS + hardcoded NewsSourceConfig
│       ├── WebScraperService.cs          # 6-7 źródeł HTML
│       ├── PerplexitySearchService.cs    # Perplexity Sonar (16 hardcoded zapytań + user)
│       └── ContentFilterService.cs       # scoring trafności + wykluczenia + dedup
├── Models/
│   ├── BriefingModels.cs                 # BriefingArticle, MonthlyStats, PriceIndicator...
│   ├── IntelArticle.cs
│   └── (kilka modeli intel_*)
├── Database/
│   └── DatabaseSetup.cs                  # tworzenie tabel intel_*
└── SQL/
    └── CreateTables.sql
```

---

## 5. Jak działa pipeline pobierania (`NewsFetchOrchestrator.FetchAndAnalyzeAsync`)

Każdy etap ma własny **timeout** (RunWithTimeoutAsync) i loguje czas. Kolejność:

1. **RSS** (40 źródeł, limit 90s) — `SyndicationFeed`. Zwraca ~280-300 surowych artykułów.
2. **Scraping HTML** (7 źródeł, limit 120s) — GLW/PIORiN/KOWR/KRIR/WIR. **Prawie zawsze 0 artykułów**
   (struktura stron się zmienia, parser nie łapie).
3. **Perplexity** (limit 240s) — 16 hardcoded zapytań „Critical PL" (HPAI, ceny żywca, Cedrob,
   import Brazylia/Ukraina, KSeF…) + zapytania użytkownika z `intel_UserQueries`.
   **Limit budżetu: 30 zapytań/dzień** (persistent counter). Po wyczerpaniu zwraca 0.
4. **Tłumaczenie** (10 EN, Haiku, limit 120s) — tłumaczy angielskie tytuły/streszczenia na PL.
5. **Filtr lokalny** (limit 30s) — `ContentFilterService`: scoring słów-kluczy
   (drób=10, HPAI=15, kurczak=10, cedrob=8…), wzorce wykluczeń (ciągniki, kursy walut, polityka,
   reklamy → score ujemny), deduplikacja po znormalizowanym tytule/URL hash.
   Z ~300 zostaje ~86-166 „relevant".
6. **AI Filter Haiku** (limit 90s) — Haiku ocenia trafność w paczkach po 10 (JSON: relevant/category/priority).
7. **Content Enrichment** (limit 180s) — dociąga pełną treść top artykułów (web scraping treści).
   Często wzbogaca tylko 0-1 z 15 (sporo URL-i nie daje się sparsować).
8. **BusinessContext** (HANDEL+LibraNet, limit 30s) — TOP klienci/dostawcy/ceny/konkurenci.
9. **AI Analysis Sonnet** (top N, limit 360s) — Sonnet generuje dla każdego artykułu:
   Streszczenie + KimJest (edukacja) + CoToZnaczyDlaPiórkowscy (analiza CEO/Sales/Buyer) +
   ZalecaneDziałania. To najdroższy i najwolniejszy etap.
10. **Daily Summary Sonnet** (limit 90s) — jedno dzienne streszczenie + mood + action items.
11. **Save Daily Summary** → `intel_DailySummary`.
12. **HPAI Alerts (GLW)** — scraper `wetgiw.gov.pl` → **404, zawsze 0 alertów**.
13. **Poultry Prices (MRiRW)** — scraper → 0.
14. **Commodity Prices (MATIF)** — scraper → 0.
15. **Save to DB** → `intel_Articles` (upsert po UrlHash).
16. **Log fetch** → `intel_FetchLog`.
17. **Auto-cleanup** — retencja 30 dni (raz dziennie).

**Auto-fetch:** timer sprawdza co 60s; o 06:00-06:15 odpala pełne pobranie raz dziennie
(flaga per-dzień w JSON w LocalAppData). Po fetchu auto-eksport One-pagera do pliku MD.

---

## 6. Interfejs użytkownika (po przebudowie „chat-first")

### Główne okno (`PorannyBriefingWindow` — „Centrum Wiadomości AI")
- **Nagłówek (1 wiersz):** tytuł + 3 REALNE liczniki (liczba newsów / krytycznych / HPAI,
  liczone z faktycznie załadowanych artykułów) + toolbar:
  `🔄 Pobierz nowe` · `📋 Podsumowanie` · `💬 Zapytaj AI` · `⚙ Więcej ▾`
  (menu: Moje źródła / Moje tematy / Diagnostyka / Pomoc).
- **Lewo (60%, główny obszar):** lista newsów z `intel_Articles`. Filtry kategorii
  (Wszystkie/HPAI/Ceny/Konkurencja/Klienci) + szukajka. Karta: kategoria · źródło · badge
  „🔴 WAŻNE" (gdy critical) · badge „🤖 AI" (gdy artykuł ma analizę) · tytuł · zajawka ·
  **data dodania do bazy** („dziś 14:30" / „wczoraj" / „3 dni temu"). Klik = rozwija pełną analizę
  (sekcje AI pokazują się tylko gdy istnieją).
- **Prawo (40%, chowany):** stały panel chatu AI (toggle „💬 Zapytaj AI", domyślnie schowany).
  Szybkie pytania (HPAI? / Ceny żywca / Cedrob / Esencja) + pole tekstowe.
- **Sortowanie:** lista po dacie dodania do bazy (`FetchedAt`) malejąco (najnowsze na górze).
- **Zakres:** ostatnie 3 dni (auto-load z bazy po otwarciu okna).

### One-pager (`BriefingOnePagerWindow`)
Esencja dnia z `intel_DailySummary`: TOP 3 krytyczne + 5 action items + streszczenia
CEO/Sales/Buyer + prognoza tygodniowa + **chat AI** + przyciski 👍/👎 przy artykułach.

### Chat AI (kontekst)
`BriefingChatContextBuilder` buduje system prompt zawierający: profil firmy + ostatnie 7 dni
briefingów + TOP 25 artykułów z 30 dni + aktywne tematy użytkownika + preferencje z feedbacku.
System prompt cache'owany (ephemeral) → kolejne pytania w sesji tańsze i szybsze.

---

## 7. Co zostało zrobione w tym module (historia)

1. **Rozbudowa w agenta (Faza 1-3):**
   - „Moje źródła" — user dodaje dowolny URL, auto-detekcja RSS (sprawdza 9 ścieżek), fallback HTML scraping.
   - „Moje tematy" — edytowalne zapytania do Perplexity (8 szablonów pod firmę).
   - Feedback 👍/👎 — tabela `intel_ArticleFeedback`, agregacja preferencji per (kategoria, źródło).
   - Chat AI — w One-pagerze i głównym oknie, z pełnym kontekstem firmy + newsów.
   - Okno pomocy.

2. **Audyt + radykalna przebudowa (opcja „spal i odbuduj"):**
   - **Odkrycie:** ~80% zawartości głównego okna było HARDCODED MOCKIEM z lutego 2026
     (ceny żywca, Biedronka 22.99, Cedrob, dotacje ARiMR, EU benchmark, mapa konkurencji z SWOT,
     MATIF, 10 zakładek). ViewModel miał 2190 linii, z czego ~1700 to `LoadSeedData()` z fałszywymi danymi.
   - **Usunięto ~3000 linii** (17 mock-kolekcji, 10-tabowy boczny panel, role switcher, tasks panel,
     pasek wskaźników z 7 fałszywymi liczbami, rysowanie sparkline). ViewModel: 2190 → 380 linii.
   - Nowy układ chat-first (lewo newsy / prawo chat). Tylko REALNE dane.
   - Nazwa „Poranny Briefing" → „Centrum Wiadomości AI".

3. **Uproszczenia UI** (na prośbę „za dużo guzików"): 7 → 3 przyciski + menu, chat domyślnie schowany,
   sekcje AI tylko gdy są, data dodania do bazy na kartach.

4. **Dopasowanie pod preferencje** (po serii pytań): sortowanie wg daty dodania, zakres 3 dni,
   analiza AI 15 artykułów, auto-seed Farmer.pl + tematy (Mercosur, rzekomy pomór/Newcastle, hodowcy broiler),
   wyróżnik „🔴 WAŻNE", głównie PL + trochę zagranicy.

5. **Naprawy wydajności z logów produkcyjnych** (ostatnia tura):
   - Filtr Haiku był **sekwencyjny** (16 paczek × 7s = 112s > limit 90s → HARD TIMEOUT).
     → zrównoleglone (`Task.WhenAll`).
   - Analiza Sonnet 15 art. = **309 s** (użytkownik ręcznie anulował). → równoległość 3 → 5.
   - Analiza szła na **zboża/suszę/dopłaty/nawozy** zamiast drobiu (top-N po samym RelevanceScore,
     a generic agri dominuje wolumenowo). → dodano `PoultryTopicRank`
     (HPAI 100 > drób 90 > ceny 80 > konkurencja 70 > klienci 60 > import 50 > kat. źródła 40 > pasze 20).
   - Tematy użytkownika (Mercosur/Newcastle) **nigdy nie startowały** — budżet Perplexity 30/30
     padał na 16 hardcoded zanim do nich doszło. → user queries idą PIERWSZE.

---

## 8. Znane problemy (otwarte) — proszę oceń wagę i zaproponuj rozwiązania

1. **GLW HPAI scraper zwraca 404** — dedykowane alerty ptasiej grypy nie działają w ogóle
   (`intel_HpaiAlerts` = 0). HPAI wchodzi tylko ubocznie przez RSS/Perplexity jako artykuły.
2. **Scrapery cen (MRiRW, MATIF) i HTML źródła zwracają 0** — parsery nie nadążają za zmianami stron.
   Cały moduł cenowy faktycznie martwy.
3. **Content Enrichment prawie nie działa** — wzbogaca 0-1 z 15 artykułów; raz wciągnął
   **binarny śmieć** (nieczytelne dane) z Farmer.pl, który Sonnet musiał „analizować" (strata kasy).
4. **Budżet Perplexity 30/dzień** wyczerpuje się szybko przy wielu ręcznych fetchach
   (po wyczerpaniu zostaje tylko RSS).
5. **Filtr trafności jest hojny dla rolnictwa ogólnego** — dużo zbóż/nawozów/dopłat/suszy
   przechodzi jako „relevant" (bo pasze/zboża wpływają na koszt hodowcy), co rozmywa drobiarski fokus.
6. **Koszt** ~9 zł/dzień przy 2 fetchach (Sonnet analiza = lwia część). Auto-fetch + ręczne = więcej.
7. **Bezpieczeństwo:** hardcoded `sa` + hasło HANDEL w historii git (świadomy dług właściciela).
8. **Brak testów** — projekt nie ma test runnera (decyzja właściciela), więc zero testów jednostkowych
   na pipeline/filtry/parsowanie JSON z Claude.
9. **Parsowanie JSON z Claude bywa kruche** — Haiku czasem zwraca malformed JSON (jest fallback,
   ale to wskazuje na ryzyko).

---

## 9. Liczby / koszty (z raportu diagnostycznego)

- Środowisko: Windows 11, .NET 8.0.22, SQL Server 2022 (LibraNet) + 2019 (HANDEL).
- Źródła: 45 RSS + 6-7 HTML scraping + Perplexity (16 hardcoded zapytań).
- Koszt dzienny (2 fetche): Haiku ~1,15 zł + Sonnet ~7,44 zł + Perplexity ~0,60 zł = **~9,20 zł**.
- Cennik Anthropic: Haiku $1/$5 (in/out mln tok.), Sonnet $3/$15 + cache 10× taniej; przelicznik 4 PLN/USD.
- Prompt caching działa (widać `cache_read` w logach).

---

## 10. CZEGO OD CIEBIE OCZEKUJĘ (recenzja)

Proszę o szczerą, konkretną ocenę. Odpowiedz na:

1. **Architektura** — czy pipeline 17-etapowy z timeoutami to dobry wzorzec, czy over-engineering?
   Czy MVVM tu pasuje? Czy podział na serwisy jest sensowny? Co byś rozdzielił/scalił?
2. **Niezawodność** — etapy które zwracają 0 (scrapery cen, GLW, enrichment) — wyrzucić, naprawić,
   czy zastąpić innym podejściem (np. API zamiast scrapingu)?
3. **Jakość danych** — jak lepiej trzymać drobiarski fokus? Czy filtr keyword + Haiku to dobre 2 stopnie,
   czy lepiej embeddingi/reranking? Czy `PoultryTopicRank` (hardcoded regexy) to dobre rozwiązanie,
   czy obejście?
4. **Koszty** — czy ~9 zł/dzień to rozsądnie? Gdzie przepalam? Czy 15 artykułów analizowanych przez Sonnet
   ma sens, skoro większość to generic agri? Jak zoptymalizować (mniej Sonnet, więcej Haiku, batch API)?
5. **Wydajność** — pełny fetch trwa ~5-6 minut. Da się to zrównoleglić bardziej / zrobić strumieniowo /
   w tle bez blokowania?
6. **Perplexity vs RSS** — czy 16 hardcoded zapytań + 30/dzień budżet to dobry model? Alternatywy?
7. **Chat AI / RAG** — kontekst budowany jako „wrzuć 25 artykułów + 7 briefingów w system prompt".
   Czy to skaluje się? Czy potrzebna wektorowa baza / RAG, czy przy tej skali (377 artykułów) to przerost?
8. **Bezpieczeństwo** — jak pilnie naprawić hardcoded `sa`/hasło? Najprostsza realna ścieżka dla
   jednoosobowego developera?
9. **Czego brakuje** — jakie 3-5 funkcji dodałbyś, żeby to był realnie użyteczny agent dla CEO ubojni?
10. **Najważniejsze 3 rzeczy** do zrobienia w następnej kolejności (priorytetyzacja).

Możesz odpowiadać po polsku. Jeśli czegoś nie wiesz z opisu — wypisz pytania, a uzupełnię.
```
