# 29. ⭐ PDF Knowledge → AI Chat (RAG) — PEŁNY PORADNIK

## Co to jest RAG
**RAG = Retrieval-Augmented Generation**

W skrócie: AI **nie zgaduje** odpowiedzi. Najpierw szuka w Twoich dokumentach, znajduje cytaty, potem na podstawie cytatów odpowiada. **Zawsze z podaniem źródła**.

## Problem dziś
- Nowy pracownik widzi "zielone udo" → pyta kolegi → kolega nie pamięta → dzwoni do Ciebie
- Procedura PROCEDURY_05.docx ma 45 stron — nikt nie pamięta
- Wiedza w głowach 3-4 ludzi → urlop = chaos
- BRC v9 wymaga **dokumentacji + dostępu do niej** dla każdego pracownika

## Wartość biznesowa
- **Onboarding nowego pracownika** z 4 tyg na 1 tydzień
- **Mniej telefonów do Ciebie** (5-10/dzień → 1-2/dzień)
- **Wiedza zinstytucjonalizowana** — przeżyje rotację pracowników
- **BRC v9 compliance** — formalna procedura "Jak pracownik zdobywa wiedzę?"
- **Wartość: ~80-150 tys zł/rok** (oszczędzony czas Twój + szybsze onboarding + mniej błędów)

---

## ŹRÓDŁA WIEDZY DO ZAINDEKSOWANIA

### Już macie:
1. **BAZA_WIEDZY/Drobiarstwo/Broiler Meat Signals.pdf** (190 str) ✓
2. **BAZA_WIEDZY/ZSRIR - Dokumentacja API_1.0.pdf** ✓
3. **Dokumenty ogólnikowe/PROCEDURY_01-08.docx** (~8 plików) ✓
4. **BAZA_WIEDZY/*.md** (~30 plików dokumentacji ZPSP) ✓
5. **HACCP plan** (jeśli macie w docs)
6. **Instrukcje obsługi maszyn** (Marel skubarki, parzelnik, chłodnia)

### Można dodać:
7. **Wszystkie maile od dyrekcji** (komunikaty wewnętrzne)
8. **Notatki z meetingów** (Fireflies transkrypcje)
9. **EU 178/2002, 2073/2005** (oficjalne dokumenty)
10. **BRC v9 issue 9** standard
11. **Best practices Marel/CSB** (publicznie dostępne whitepapers)

---

## ARCHITEKTURA RAG

```
┌─────────────────────────────────────────────────────┐
│  PIPELINE WIEDZY                                    │
├─────────────────────────────────────────────────────┤
│                                                     │
│  [PDFy + DOCXy + MD]                                │
│         │                                           │
│         │ 1. Ekstrakcja tekstu (pdftotext, docx2txt)│
│         ▼                                           │
│  [Czysty tekst per dokument]                        │
│         │                                           │
│         │ 2. Chunking (200-500 tokenów chunk)       │
│         ▼                                           │
│  [Chunki z metadanymi (źródło, strona)]             │
│         │                                           │
│         │ 3. Embeddings (OpenAI text-embedding-3,   │
│         │     Voyage-3, lub Cohere)                 │
│         ▼                                           │
│  [Vector DB: Qdrant lub Postgres+pgvector]          │
│                                                     │
└─────────────────────────────────────────────────────┘
                          │
                          │
┌─────────────────────────────────────────────────────┐
│  CHAT PIPELINE                                      │
├─────────────────────────────────────────────────────┤
│                                                     │
│  [User: "Co to wooden breast?"]                     │
│         │                                           │
│         │ 1. Embed pytanie                          │
│         ▼                                           │
│  [Embedding pytania]                                │
│         │                                           │
│         │ 2. Vector search w bazie                  │
│         ▼                                           │
│  [TOP 5 najbliższych chunków]                       │
│         │                                           │
│         │ 3. Reranking (Cohere rerank lub Claude)   │
│         ▼                                           │
│  [TOP 3 najtrafniejszych chunków]                   │
│         │                                           │
│         │ 4. Prompt Claude: pytanie + chunki        │
│         ▼                                           │
│  [Claude generuje odpowiedź z cytowaniem źródeł]    │
│                                                     │
└─────────────────────────────────────────────────────┘
```

---

## STACK TECHNOLOGICZNY

### Opcja 1: ALL-IN-ONE z istniejących narzędzi (najszybciej)
- **Claude API** (Anthropic)
- **PostgreSQL z pgvector extension** (możesz dorzucić do istniejącego SQL Server jako osobny PG)
- **Voyage AI embeddings** (Anthropic poleca, ~$0.05/1M tokens)
- Czas: ~80h

### Opcja 2: Qdrant (dedykowana vector DB)
- **Qdrant** (open source, Docker, free do 1M vectors)
- **Voyage embeddings**
- **Claude API**
- Czas: ~60h
- Lepsza wydajność search

### Opcja 3: LlamaIndex / LangChain (framework)
- **LlamaIndex** lub **LangChain** — gotowe orchestrationa
- Sporo wbudowanego, ale dodatkowa abstrakcja
- Czas: ~40h dla MVP, ale trudniejszy debug
- **Tylko Python** (rest API), więc dla Was sub-optymalne

### Opcja 4: Anthropic Files API (proste, hosted)
- **Files API od Anthropic** (wybrane modele)
- Upload plików → AI sama indeksuje + szuka
- Brak własnej infrastruktury
- Czas: ~20h
- Limit rozmiaru/liczby plików

**Moja rekomendacja: Opcja 1 (pgvector)** — używasz już SQL Server, dorzucasz mały PostgreSQL na boku, prosty stack, pełna kontrola.

---

## DATABASE SCHEMA (PostgreSQL + pgvector)

```sql
-- Postgres + pgvector
CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE rag_dokumenty (
    id SERIAL PRIMARY KEY,
    nazwa VARCHAR(500) NOT NULL,
    sciezka_zrodlowa VARCHAR(1000) NOT NULL,
    typ_dokumentu VARCHAR(50) NOT NULL,  -- 'PDF', 'DOCX', 'MD', 'EMAIL'
    kategoria VARCHAR(100) NULL,  -- 'PROCEDURA', 'PODRECZNIK', 'PRZEPIS_PRAWNY'
    data_dodania TIMESTAMP NOT NULL DEFAULT NOW(),
    data_modyfikacji TIMESTAMP NULL,
    hash_sha256 VARCHAR(64) NULL,
    liczba_chunkow INTEGER NULL,
    aktywny BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE rag_chunki (
    id SERIAL PRIMARY KEY,
    dokument_id INTEGER NOT NULL REFERENCES rag_dokumenty(id) ON DELETE CASCADE,
    numer_chunku INTEGER NOT NULL,
    tekst TEXT NOT NULL,
    strona_pdf INTEGER NULL,
    rozdzial VARCHAR(200) NULL,
    embedding vector(1024),  -- dla Voyage-3-large lub OpenAI text-embedding-3-large
    tokeny INTEGER NULL
);

CREATE INDEX rag_chunki_embedding_idx 
    ON rag_chunki USING ivfflat (embedding vector_cosine_ops) 
    WITH (lists = 100);

CREATE TABLE rag_zapytania_log (
    id SERIAL PRIMARY KEY,
    data_zapytania TIMESTAMP NOT NULL DEFAULT NOW(),
    user_id VARCHAR(100) NULL,
    pytanie TEXT NOT NULL,
    odpowiedz TEXT NULL,
    chunki_uzyte JSON NULL,  -- IDs chunków + scores
    czas_odpowiedzi_ms INTEGER NULL,
    koszt_usd DECIMAL(10,6) NULL,
    ocena_uzytkownika INTEGER NULL,  -- 1-5 like/dislike
    feedback_text TEXT NULL
);
```

---

## INDEKSACJA — pipeline

### Krok 1: Ekstrakcja tekstu z PDF/DOCX

```csharp
public class DocumentExtractor
{
    public async Task<List<string>> ExtractAsync(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        return extension switch
        {
            ".pdf" => await ExtractPdfAsync(filePath),
            ".docx" => await ExtractDocxAsync(filePath),
            ".md" => new List<string> { await File.ReadAllTextAsync(filePath) },
            _ => throw new NotSupportedException($"Format {extension} nieobsługiwany")
        };
    }

    private async Task<List<string>> ExtractPdfAsync(string path)
    {
        // pdftotext -layout per page
        var pages = new List<string>();
        using var doc = UglyToad.PdfPig.PdfDocument.Open(path);  // PdfPig - free
        foreach (var page in doc.GetPages())
        {
            var text = string.Join(" ", page.GetWords().Select(w => w.Text));
            pages.Add(text);
        }
        return pages;
    }

    private async Task<List<string>> ExtractDocxAsync(string path)
    {
        using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(path, false);
        var body = doc.MainDocumentPart.Document.Body;
        var paragraphs = body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
            .Select(p => p.InnerText)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();
        return paragraphs;
    }
}
```

### Krok 2: Chunking

```csharp
public class TextChunker
{
    private const int CHUNK_SIZE_TOKENS = 400;
    private const int OVERLAP_TOKENS = 50;

    public List<Chunk> ChunkPages(List<string> pages, int dokumentId)
    {
        var chunks = new List<Chunk>();
        int chunkNumber = 0;
        
        for (int pageIdx = 0; pageIdx < pages.Count; pageIdx++)
        {
            var pageText = pages[pageIdx];
            var pageChunks = SplitIntoTokenChunks(pageText, CHUNK_SIZE_TOKENS, OVERLAP_TOKENS);
            foreach (var c in pageChunks)
            {
                chunks.Add(new Chunk
                {
                    DokumentId = dokumentId,
                    NumerChunku = chunkNumber++,
                    Tekst = c,
                    StronaPdf = pageIdx + 1,
                    Tokeny = EstimateTokens(c)
                });
            }
        }
        return chunks;
    }

    private List<string> SplitIntoTokenChunks(string text, int chunkSize, int overlap)
    {
        // Prosty: dzielenie po zdaniach + okno
        var sentences = SplitIntoSentences(text);
        var chunks = new List<string>();
        var current = new StringBuilder();
        int currentTokens = 0;

        foreach (var sentence in sentences)
        {
            var tokens = EstimateTokens(sentence);
            if (currentTokens + tokens > chunkSize && current.Length > 0)
            {
                chunks.Add(current.ToString().Trim());
                // Overlap: zostaw ostatnie zdanie
                current = new StringBuilder(sentence);
                currentTokens = tokens;
            }
            else
            {
                current.Append(" ").Append(sentence);
                currentTokens += tokens;
            }
        }
        if (current.Length > 0)
            chunks.Add(current.ToString().Trim());
        return chunks;
    }

    private int EstimateTokens(string text) => text.Length / 4;  // przybliżenie
}
```

### Krok 3: Embeddings (Voyage AI)

```csharp
public class VoyageEmbedder
{
    private readonly HttpClient _http;
    private const string ENDPOINT = "https://api.voyageai.com/v1/embeddings";
    private const string MODEL = "voyage-3-large";  // 1024 wymiary

    public async Task<float[]> EmbedAsync(string text)
    {
        var request = new
        {
            input = text,
            model = MODEL,
            input_type = "document"  // 'document' dla indeksowania, 'query' dla pytania
        };

        var response = await _http.PostAsJsonAsync(ENDPOINT, request);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var embedding = json.GetProperty("data")[0].GetProperty("embedding")
            .EnumerateArray().Select(e => e.GetSingle()).ToArray();
        return embedding;
    }

    public async Task<List<float[]>> EmbedBatchAsync(List<string> texts)
    {
        // Voyage obsługuje batch
        var request = new
        {
            input = texts,
            model = MODEL,
            input_type = "document"
        };
        var response = await _http.PostAsJsonAsync(ENDPOINT, request);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("data")
            .EnumerateArray()
            .Select(d => d.GetProperty("embedding").EnumerateArray()
                .Select(e => e.GetSingle()).ToArray())
            .ToList();
    }
}
```

### Koszt embeddings
- Voyage-3-large: $0.18/1M tokens
- BAZA_WIEDZY ZPSP ~500k tokens = **$0.09 jednorazowo**
- Nawet PDFy z 1000 stron = ~500k tokens × 5 = 2.5M tokens = **$0.45**
- **TOTAL indeksacja**: ~5-10 zł jednorazowo

---

## ZAPYTANIA — Chat Pipeline

```csharp
public class RagChatService
{
    private readonly VoyageEmbedder _embedder;
    private readonly AnthropicClient _claude;
    private readonly NpgsqlConnection _pg;

    public async Task<RagAnswer> AskAsync(string question, string? userId = null)
    {
        var sw = Stopwatch.StartNew();

        // 1. Embed pytanie
        var queryEmbedding = await _embedder.EmbedAsync(question);

        // 2. Vector search w pgvector
        var topChunks = await SearchSimilarChunks(queryEmbedding, topK: 10);

        // 3. Rerank (opcjonalnie) — Claude lub Cohere
        var rerankedChunks = await RerankChunks(question, topChunks, topK: 3);

        // 4. Build prompt
        var systemPrompt = @"Jesteś asystentem firmy Piórkowscy — zakładu drobiarskiego.
Odpowiadasz na pytania pracowników o procedury, jakość, produkcję, BRC, HACCP.

WAŻNE ZASADY:
1. ZAWSZE bazuj odpowiedź NA DOSTARCZONYCH FRAGMENTACH (kontekst).
2. Jeśli kontekst nie zawiera odpowiedzi - powiedz: 'Nie znalazłem tej informacji w dokumentacji firmowej. Skontaktuj się z [QM/produkcja].'
3. ZAWSZE cytuj źródła: [Źródło: nazwa dokumentu, str X].
4. Odpowiadaj zwięźle, po polsku.
5. Jeśli pytanie dotyczy procedury - podaj kroki numerowane.
6. Jeśli pytanie krytyczne dla bezpieczeństwa (np. pozytyw Salmonella) - oznacz '🚨 WAŻNE' i powiedz natychmiast zadzwonić do QM.";

        var contextText = string.Join("\n\n---\n\n", 
            rerankedChunks.Select((c, i) => 
                $"[Źródło {i+1}: {c.NazwaDokumentu}, str. {c.StronaPdf}]\n{c.Tekst}"));

        var userPrompt = $"KONTEKST:\n{contextText}\n\nPYTANIE PRACOWNIKA: {question}";

        // 5. Call Claude
        var request = new MessageParameters
        {
            Model = "claude-haiku-4-5-20251001",
            MaxTokens = 1000,
            Temperature = 0.1m,
            System = new[] { new SystemMessage { Type = "text", Text = systemPrompt } },
            Messages = new List<Message>
            {
                new Message { Role = RoleType.User, Content = new List<ContentBase> 
                    { new TextContent { Text = userPrompt } } 
                }
            }
        };

        var response = await _claude.Messages.GetClaudeMessageAsync(request);
        var answer = response.Content[0].Text;

        sw.Stop();
        var koszt = CalcCost(response);

        // 6. Log
        await LogQuery(question, answer, rerankedChunks, sw.ElapsedMilliseconds, koszt, userId);

        return new RagAnswer
        {
            Answer = answer,
            Sources = rerankedChunks,
            ResponseTimeMs = sw.ElapsedMilliseconds,
            CostUSD = koszt
        };
    }

    private async Task<List<ChunkResult>> SearchSimilarChunks(float[] embedding, int topK)
    {
        const string sql = @"
            SELECT c.id, c.tekst, c.strona_pdf, c.dokument_id, d.nazwa,
                   1 - (c.embedding <=> @embedding::vector) AS similarity
            FROM rag_chunki c
            JOIN rag_dokumenty d ON d.id = c.dokument_id
            WHERE d.aktywny = TRUE
            ORDER BY c.embedding <=> @embedding::vector
            LIMIT @topK;";
        // ... wykonanie i mapowanie
    }
}
```

### Koszt per zapytanie
- Embedding pytania: ~10 tokens × $0.18/1M = **$0.0000018**
- Vector search: 0 (w bazie)
- Claude Haiku z context ~2000 tokens + odpowiedź 200 tokens:
  - Input: 2000 × $1/1M = $0.002
  - Output: 200 × $5/1M = $0.001
- **TOTAL: ~$0.003/zapytanie = 1.5 grosza**

Przy 50 zapytań/dzień × 250 dni = 12500/rok × $0.003 = **$37.50/rok ≈ 150 zł/rok**

---

## INTERFEJS UŻYTKOWNIKA

### Opcja A: Embedded w ZPSP (WPF)
- Nowe okno `RAGChatWindow.xaml`
- Pole tekstowe + przycisk WYŚLIJ
- Historia rozmowy
- Sidebar z linkami do źródeł

### Opcja B: Web app (Blazor) — POLECAM
- Dostęp z tabletu / telefonu w hali
- Mobile-friendly
- Cały zespół ma dostęp przez przeglądarkę
- URL: `http://serwer/zpsp-chat`

### Opcja C: Bot na Slacku / Teamsach
- Pracownik pisze @ZpspBot "co to wooden breast?"
- Bot odpowiada w wątku
- Wszyscy widzą Q&A → wspólne uczenie

**Rekomendacja**: Opcja B (Blazor) + Opcja C (Slack/Teams) jeśli używacie

### Mockup UI (Blazor)
```
┌────────────────────────────────────────────────────────────┐
│  🤖 ZPSP CHAT — Asystent firmowy                          │
├────────────────────────────────────────────────────────────┤
│                                                            │
│  Ty (Janek): Co to jest wooden breast?                     │
│                                                            │
│  🤖 ZPSP:                                                  │
│  Wooden breast (WB) to schorzenie mięśni piersiowych       │
│  charakteryzujące się stwardnieniem włókien mięśniowych.   │
│                                                            │
│  Objawy:                                                   │
│  • Twardy, jędrny filet                                    │
│  • Wilgotna powierzchnia z drobnymi krwiakami              │
│  • Czasem występuje razem z white striping                 │
│                                                            │
│  Wpływ:                                                    │
│  • Drip loss wzrasta z 1.0% do 1.15%                       │
│  • Cooking loss z 21.5% do 30.9%                           │
│  • Spadek wartości handlowej fileta                        │
│                                                            │
│  Przyczyny: szybko rosnące rasy, niedotlenienie mięśni     │
│  z powodu zbyt dużych włókien.                             │
│                                                            │
│  📚 Źródła:                                                │
│  • Broiler Meat Signals str. 140-141                       │
│  • PROCEDURY_05.docx str. 23                               │
│                                                            │
│  ────────────────────────────────────────────────         │
│  Ty: Co robić jak go znajdę?                               │
│                                                            │
│  🤖 ZPSP:                                                  │
│  1. Klasyfikuj tuszkę jako klasa B                         │
│  2. Wpisz do systemu typ wady: 'WOODEN_BREAST'             │
│  3. Lekkie WB można sprzedać do przerobu (nuggetsy)        │
│  4. Ciężkie WB → utylizacja                                │
│  5. Powiadom QM jeśli wzrost > 5% partii                   │
│                                                            │
│  📚 Źródła:                                                │
│  • PROCEDURY_05.docx str. 25                               │
│  • PROCEDURY_07.docx str. 12                               │
│                                                            │
│  ────────────────────────────────────────────────         │
│  [Wpisz pytanie...]                              [WYŚLIJ]  │
│                                                            │
└────────────────────────────────────────────────────────────┘
```

---

## CYKL ŻYCIA WIEDZY

### Aktualizacja dokumentów
- **Procedura zmieniona** → upload nowej wersji → reindex
- **Nowy dokument** → drag&drop w panelu admin → auto-index
- **Stare dokumenty** → oznaczone jako "aktywny=false", AI nie używa

### Panel admin (`/admin/rag`)
- Lista wszystkich dokumentów
- Status indeksacji
- Liczba chunków
- Liczba zapytań do dokumentu
- Przycisk: REINDEX, USUŃ, DEZAKTYWUJ

### Monitoring jakości
- Co tydzień: 10 losowych Q&A → QM ocenia (1-5)
- Niskie oceny → analiza:
  - Czy chunki były dobre?
  - Czy reranking poszedł źle?
  - Czy potrzeba dodatkowych dokumentów?

---

## KONKRETNE USE CASES

### Use Case 1: Nowy pracownik
**Stary sposób**: 2 tygodnie szkolenia + 2 tygodnie pytania kolegów
**Nowy sposób**: Tablet w hali, chat pyta cokolwiek, odpowiedzi 24/7

### Use Case 2: Pracownik nocnej zmiany
**Stary sposób**: budzi QM o 02:00 z pytaniem
**Nowy sposób**: pyta bota, dostaje odpowiedź, działa

### Use Case 3: Procedura przy alercie
**Stary sposób**: panika, dzwoni gdzie, nikt nie odbiera
**Nowy sposób**: "Pozytyw Salmonella partia 1247, co robić?"
→ Bot odpowiada krok po kroku z playbook'a + numer do QM jeśli stress critical

### Use Case 4: Sprawdzenie wymogu prawnego
**Stary sposób**: szukasz w 200-stronnicowym BRC
**Nowy sposób**: "Co BRC v9 mówi o temperaturze chłodzenia?" → cytat + str

### Use Case 5: Audyt
**Auditor**: "Pokaż jak pracownik dowiaduje się o procedurach"
**Ty**: "Mamy AI chat" → demo → auditor zachwycony

---

## INTEGRACJA Z EXISTING ZPSP

### Z #19 (CCP) — bot rozpoznaje incydent
Pracownik: "Chłodnia pokazuje 5°C, co robić?"
Bot rozpoznaje: pytanie o CCP → odpowiada playbook'em + auto-link do CCP_Dashboard

### Z #22 (Recall) — bot pomaga w recall
QM: "Jak inicjować recall dla salmonella?"
Bot: krok po kroku z playbook'a + przycisk "Otwórz panel Recall w ZPSP"

### Z #28 (Photo+AI) — multi-modal
Pracownik wysyła zdjęcie tuszki + pyta "co to za wada?"
Bot widzi zdjęcie (Claude VLM) + szuka w PDF → odpowiada

---

## CZAS IMPLEMENTACJI

| Etap | Czas | Koszt |
|---|---|---|
| Setup PostgreSQL + pgvector | 8h | hosting ~50zł/mies |
| Document extractor (PDF, DOCX, MD) | 12h | — |
| Chunker | 8h | — |
| Voyage embeddings + batch upload | 12h | ~10 zł indeksacja |
| RagChatService (search + Claude + log) | 20h | — |
| UI Blazor app | 32h | — |
| Auth (kto może używać) | 8h | — |
| Panel admin (upload, reindex) | 16h | — |
| Slack/Teams bot (opcjonalnie) | 16h | — |
| Tuning prompts + testowanie | 16h | $5 testów |
| Pilot 2 tyg z 5 pracownikami | — | — |
| **RAZEM** | **~150h** | **~1500 zł/rok** |

---

## STRATEGIA ROZWOJU

### Faza 1 (MVP) — 1 miesiąc
- Indeksacja: Broiler Meat Signals + 8 procedur
- Chat web (Blazor)
- Auth: tylko zalogowani pracownicy
- 5 pilotów

### Faza 2 — 2 miesiące
- Dodaj wszystkie BAZA_WIEDZY/*.md
- Slack/Teams bot
- Multimodal (zdjęcia)

### Faza 3 — pół roku
- Auto-indeksacja maili dyrekcji (z opt-in)
- Personalizacja per dział (QC widzi inne odpowiedzi niż transport)
- "Sugerowane pytania" na podstawie aktualnych incydentów

### Faza 4 — rok
- Voice (wpisuj głosem na tablecie)
- Multilingual (angielski dla audytorów BRC)
- Predictive ("Wczoraj był incydent X, dziś sprawdź Y" — proaktywnie)

---

## RYZYKA

⚠️ **Halucynacje AI** — bot wymyśla. Mitygacja: dobry prompt + temperature=0.1 + zawsze cytuj źródła
⚠️ **Stare dokumenty w bazie** — bot odpowiada przestarzałą procedurą. Mitygacja: data_modyfikacji + okresowy audyt
⚠️ **Pracownicy nie używają** — mitygacja: szkolenie + przyklejenie wartości w hali ("100+ pytań/tydzień!")
⚠️ **Wrażliwe dane w odpowiedzi** — bot może ujawnić rzeczy nie dla wszystkich. Mitygacja: dokumenty tagowane "publiczne/poufne", auth per dział
⚠️ **Koszt AI rośnie** — monitoring + budget alert. Jak >500 zł/mies → optymalizacja
