# Centrum nagrań AI (CNA)

Moduł ZPSP do wyszukiwania zdarzeń w nagraniach CCTV po polskim opisie. Wpięty jako kafelek w głównym menu (kategoria *ADMINISTRACJA SYSTEMU*).

## Co to robi

Użytkownik wpisuje po polsku, np.:

- *„osoba bez czepka w pakowalni wczoraj między 14 a 18"*
- *„ciężarówka cofająca na rampie skupu w piątek po 22:00"*
- *„kierowca opuszczający kabinę przy bramie wyjazdowej w nocy"*
- *„anomalie na linii uboju dzisiaj rano"*

Klika **Szukaj**. Po kilkunastu sekundach widzi top-N wyników: miniatura klatki, kamera, czas, opis dlaczego AI uznała że pasuje, score 0-100, przycisk **▶ Odtwórz** (live z tej kamery).

## Architektura

```
NVR Internec (192.168.0.125, port 554 RTSP, port 80 HTTP/ONVIF)
            │
            │  RTSP main-stream  rtsp://admin:****@192.168.0.125:554/unicast/c{ch}/s0/live
            ▼
   IndexerBackgroundService                  (proces ZPSP, singleton + Timer co 10s)
      ├─ RtspFrameGrabber (FFmpeg.exe → 1 JPEG / kamera / 10s)
      └─ FrameIndex (SQLite)                  %LOCALAPPDATA%\Kalendarz1\CentrumNagranAI\index.db
            │
   ┌────────┴─────────────────────────────────────────┐
   │ tabele:                                          │
   │  camera, frame, frame_embedding, query_audit     │
   └──────────────────────────────────────────────────┘

   SearchService (wywoływany z UI gdy user klika Szukaj)
      ├─ FrameIndex.GetFrames(filtr)                  ← kandydaci
      ├─ Każda klatka → VlmClient.AnalyzeImageAsync   ← Claude Haiku 4.5 (cloud)
      │     prompt: "Czy pasuje do <query>? JSON {score, reason}"
      ├─ Sortuj po score
      ├─ Audit do query_audit + cna_search_audit.log
      └─ Zwróć top-N do UI

   CentrumNagranAIWindow (WPF)
      ├─ TextBox + Szukaj
      ├─ Lista wyników z miniaturkami i score badges
      └─ Klik ▶ → ClipPlayerWindow (LibVLCSharp + RTSP main-stream)
```

### Stos technologiczny

| Warstwa | Co używamy | Powód |
|---|---|---|
| Klatki RTSP | FFmpeg.exe (gyan.dev essentials build) | Standard, działa z każdym NVR, prosta CLI |
| Indeks | SQLite + Microsoft.Data.Sqlite | Lokalny, izolowany od MSSQL produkcyjnego, WAL = concurrent reads |
| VLM | Claude Haiku 4.5 (Anthropic API) | Najlepsze rozumienie polskiego, $1/$5 per M tokens, multimodal |
| Player | LibVLCSharp (już w ZPSP NuGet) | Wsparcie RTSP wprost, działa na każdym streamie |
| Background | Singleton + System.Timers.Timer | Wzorzec 1:1 z `MarketIntelligenceBackgroundService.cs` |

CLIP / lokalne embeddingi zaprojektowano w schema (tabela `frame_embedding` istnieje), ale w PoC używamy brute-force VLM rerank — wystarczy dla puli ~200 klatek/zapytanie. Powyżej trzeba dorobić CLIP prefilter (patrz „TODO produkcyjne").

## Konfiguracja

### Plik sekretów (poza repo Git)

`%LOCALAPPDATA%\Kalendarz1\CentrumNagranAI\secrets.json`:

```json
{
  "AnthropicApiKey": "sk-ant-api03-...",
  "Nvr": [
    {
      "Name": "NVR1-Ubojnia",
      "Host": "192.168.0.125",
      "RtspPort": 554,
      "User": "admin",
      "Password": "****",
      "Channels": [1, 2, 3, 4],
      "DefaultStreamType": 0
    }
  ]
}
```

Pola:
- `AnthropicApiKey` — klucz z https://console.anthropic.com/. Fallback do ENV `ANTHROPIC_API_KEY`.
- `Nvr[].Host` / `RtspPort` / `User` / `Password` — dane do logowania RTSP. Najlepiej dedykowany użytkownik typu `zpsp_ai` z uprawnieniami tylko *Live View + Playback*.
- `Channels` — lista numerów kanałów do indeksowania.
- `DefaultStreamType` — 0 = main (zawsze dostępny, ~2-4 Mbps), 1 = sub (lżejszy, nie każdy kanał ma).

**Ten plik nigdy nie trafia do repo.** `.gitignore` zawiera `secrets.json` na wszelki wypadek.

### Format URL RTSP — uwaga ze sprzętem Internec

NVR Internec **i6-N25232UHV** (firmware NVR-B3601) **nie używa Hikvision-standardowego** URL `/Streaming/Channels/CCSS`. Wszystkie standardowe ścieżki zwracają 401 *zanim* NVR sprawdzi path.

Format znaleziony przez ONVIF GetStreamUri:
```
rtsp://user:pass@host:554/unicast/c{channel}/s{stream}/live
```

Gdy podłączasz inny NVR, zweryfikuj URL przez ONVIF (port 80, endpoint `/onvif/Media`):
1. POST `GetProfiles` z WS-Security UsernameToken (digest)
2. Per profil POST `GetStreamUri` → dostajesz prawidłowy URL

Kod do tego jest „przepalony" w PowerShellu w git history checkpointu 2 — można go potem zamknąć w klasie `OnvifClient`.

## Jak uruchomić od zera

### 1. Zależności jednorazowe

- **.NET 8 SDK** (już masz)
- **FFmpeg** — pobiera się automatycznie do `tools/ffmpeg/` (skrypt w PowerShell, ~100 MB, gyan.dev build). Plik gitignored.
- **Klucz Anthropic** — załóż na https://console.anthropic.com/, doładuj $5+ na Billing, wygeneruj API Key, wklej do `secrets.json`.

### 2. Sieć

PC dewelopowy musi mieć dostęp do NVR (192.168.0.125 dla Piórkowscy, przez VPN „Ubojnia").

Test:
```
Test-NetConnection 192.168.0.125 -Port 554
```

### 3. Build i pierwszy test

```
dotnet build Kalendarz1.csproj -c Debug
.\bin\Debug\net8.0-windows7.0\Kalendarz1.exe --cna-test 30
```

Po 30 sekundach proces sam się zamyka. Sprawdź logi:
```
%LOCALAPPDATA%\Kalendarz1\CentrumNagranAI\audit\cna_selftest.log
```

Powinno być `COUNT(frame) PO = 12` (4 kamery × 3 cykle).

### 4. Uruchomienie z UI

Normalnie:
```
.\bin\Debug\net8.0-windows7.0\Kalendarz1.exe
```
Logujesz się do ZPSP, klikasz kafelek **🎥 Centrum nagrań AI** (kategoria ADMINISTRACJA SYSTEMU).

Skrót deweloperski (omija logowanie):
```
.\bin\Debug\net8.0-windows7.0\Kalendarz1.exe --cna-show-window
```

## Dodanie kolejnej kamery

W `secrets.json` dorzuć kanał do tablicy `Channels` lub dodaj kolejny obiekt do `Nvr[]`. CnaConfig wczyta to przy następnym starcie ZPSP, FrameIndex.Init upserta listę kamer w bazie.

Jeśli to NOWY model NVR (inny niż Internec NVR-B3601), zweryfikuj URL przez ONVIF. Jeśli inny niż `/unicast/c{ch}/s{stream}/live` — popraw `RtspFrameGrabber.ZbudujRtspUrl`.

## Logi i audit

`%LOCALAPPDATA%\Kalendarz1\CentrumNagranAI\audit\`:

| Plik | Co zawiera |
|---|---|
| `cna_indexer.log` | Każdy cykl indeksacji: kiedy, ile klatek OK/fail, czas |
| `cna_vlm.log` | Każde wywołanie Claude API: model, tokeny, koszt |
| `cna_search_audit.log` | **RODO** Każde zapytanie użytkownika: kto, co, kiedy, koszt, top hit ID |
| `cna_selftest.log` | Logi testów deweloperskich |

Tabela `query_audit` w SQLite duplikuje `cna_search_audit.log` dla łatwego SQL-a. Plain text log jest **append-only** i może być archiwizowany niezależnie.

UI: przycisk **📂 Audit** w głównym oknie otwiera ten folder w Eksploratorze.

## Dane i retencja

Klatki: `%LOCALAPPDATA%\Kalendarz1\CentrumNagranAI\frames\<cameraId>\<yyyy-MM-dd>\<unix_ts>.jpg`

PoC retencja **3 dni** (zaplanowana, ale **NIE jest jeszcze automatyczna**). Aktualnie klatki rosną w nieskończoność. Do dorobienia: nightly job kasuje katalogi `frames/*/<data>` starsze niż N dni (patrz „TODO").

Rozmiar typowy: ~27 KB / klatka × 4 kamery × 6 klatek/min × 60 × 24 × 3 = **~2.8 GB / 3 dni**.

Baza `index.db` — kilka MB nawet przy 100k klatek.

## Stop / restart serwisu indeksacji

Indeksacja chodzi jako singleton **wewnątrz procesu ZPSP**. Czyli:
- ZPSP uruchomione → indeksacja chodzi (od checkpointu 5 wpięta do `App.xaml.cs OnStartup` przez flag `--cna-test`; w produkcji wpinamy do normalnego flow startup).
- ZPSP zamknięte → indeksacja staje.

**Aktualnie w produkcyjnym flow (login → menu) indexer NIE startuje sam.** Trzeba dorobić wywołanie `IndexerBackgroundService.Instance.StartAsync()` w `App.xaml.cs` po zalogowaniu (analogicznie do `StartNotyfikacjeService()`). Patrz „TODO produkcyjne".

## Flagi deweloperskie

| Flaga | Co robi | Czas |
|---|---|---|
| `--cna-test [seconds]` | Indeksuje wszystkie kamery przez N sekund | N+0.7s |
| `--cna-test-vlm` | Wysyła ostatnią klatkę do Claude i pokazuje opis | ~3-5s |
| `--cna-test-search "<query>"` | Brute-force VLM rerank wszystkich klatek dla zapytania | zależy od liczby klatek (~6s/klatka sequential, ~14s dla 36 klatek z parallel-5) |
| `--cna-show-window` | Otwiera CentrumNagranAIWindow bezpośrednio (omija login) | natychmiast |

## Znane ograniczenia PoC

1. **Indexer nie startuje sam w normalnym flow ZPSP** — trzeba ręcznie wywołać przez `--cna-test` albo dorobić wpięcie w `App.xaml.cs`.
2. **Brak retencji klatek** — rosną w nieskończoność.
3. **Brak CLIP prefilter** — brute-force VLM dla wszystkich kandydatów. Skaluje do ~200 klatek/zapytanie ekonomicznie. Wyżej drogo.
4. **Playback klipu = live z kamery, nie ±15s wokół zdarzenia** — Hikvision `?starttime=&endtime=` na firmware Internec'a nie działa, do pełnego playback trzeba ONVIF GetReplayUri.
5. **Brak detekcji obiektów (YOLO)** — CLIP/VLM rozumieją scenę, ale nie tagują „osoba", „pojazd", „twarz" jako struktury.
6. **Brak anonimizacji twarzy** — RODO production-grade wymaga blur/mask na twarzach przed wysłaniem do cloud.
7. **AnthropicApiKey w secrets.json plain text** — lepsze byłoby Windows DPAPI (`ProtectedData.Protect`).
8. **Single user** — wszystko zapisuje user `"ser"` hardcoded. Po wpięciu w ZPSP weź z `App.UserID`.
9. **Format URL RTSP zaszyty pod Internec** — gdy dojdzie inny NVR, trzeba dorobić fallback / alternatywny URL builder.
10. **Per-user permissions w menu** — domyślnie tylko admin. Granular acl do dorobienia w `Menu.cs > LoadAllPermissions`.
11. **MapaPiętra/lokalizacji kamer** — kamery to tylko ID (`NVR1-Ubojnia-CH01`), brak nazw "Rampa skupu" / "Hala uboju" w UI. Dodać `name` field i edytor.

## TODO do produkcji

### Krótkie (~kilka godzin)
- [ ] **Wpięcie indexera w normalny startup** — `IndexerBackgroundService.Instance.StartAsync()` w `App.xaml.cs OnStartup` po zalogowaniu, wzorzec `StartNotyfikacjeService()`.
- [ ] **Retencja**: nightly Timer który kasuje katalogi `frames/<cam>/<date>` starsze niż `RetencjaDni` z `CnaConfig`. Plus `DELETE FROM frame WHERE ts < ?`.
- [ ] **App.UserID zamiast "ser"** w `SearchService.SearchAsync` audit.
- [ ] **Polepszenie promptu** — Haiku obecnie scoruje 85/100 wszystkim co metalowe. Trzeba dorobić few-shot examples + bardziej agresywne instrukcje dyskryminujące.
- [ ] **Naprawa AnalitykaPelna i revert temp commit** — patrz `git log` dla `TEMPORARY: wyklucz AnalitykaPelna`.

### Średnie (~1-2 dni)
- [ ] **CLIP prefilter** — pobrać `clip-ViT-B-32-multilingual` ONNX, ClipEmbedder.cs, `frame_embedding` populować w background po insert klatki, KNN cosine na 100k wektorach to <1s. Bez tego skala >200 klatek robi się droga.
- [ ] **Playback ±15s** — ONVIF GetReplayUri (osobny endpoint), URL z `?from=&to=` parametrami albo nasz własny FFmpeg HLS proxy.
- [ ] **Kategorie w UI** — chip-y „Hala uboju", „Pakowalnia", „Magazyn" jako filter zamiast camera ID. Wymaga metadanych camera.label.
- [ ] **Time picker** — DataPicker From/To w UI, przekazać do `SearchService.fromUtc/toUtc`.
- [ ] **Dedykowany użytkownik RTSP zamiast admin** — `zpsp_ai` z minimalnymi uprawnieniami w panelu NVR.

### Długie (>2 dni)
- [ ] **Anonimizacja twarzy** — YOLO face detect + blur przed wysłaniem do cloud.
- [ ] **Detekcja obiektów (YOLOv8)** — równoległy pipeline obok CLIP, tagi „person", „vehicle", „forklift", „cap" — pozwoli na strukturalne zapytania typu „pokaż wszystkie wjazdy ciężarówek dziś".
- [ ] **OCR tablic rejestracyjnych** — paddleocr / tesseract na snapshotach z kamer rampy.
- [ ] **Wydzielenie indexera do Windows Service** — żeby chodził niezależnie od ZPSP.
- [ ] **Multi-NVR z różnymi formatami URL** — abstrakcja nad RTSP URL building (`INvrAdapter`).
- [ ] **DPAPI dla secrets** — `ProtectedData.Protect` zamiast plain JSON.
- [ ] **Web UI** (przeglądarka, mobile) — gdyby ktoś chciał szukać z telefonu.
- [ ] **Live alerty** — co 10s nowa klatka leci przez „guard prompts" (np. „czy widać człowieka bez czepka?"), match → SMS/Slack.

## Komitki na branchu `feature/centrum-nagran-ai`

```
8cf3b97  feat(cna): checkpoint 1 - NuGety
fe92ed7  feat(cna): checkpoint 2 - klatka RTSP z 4 kamer NVR
3d39109  feat(cna): checkpoint 3 - pętla indeksacji + SQLite
4e998b0  feat(cna): checkpoint 4 - VLM hello-world (Claude Haiku 4.5)
cac0857  feat(cna): checkpoint 5 - end-to-end search z konsoli
006b087  feat(cna): checkpoint 6 - kafelek w menu + placeholder window
ba84c0d  TEMPORARY: wyklucz AnalitykaPelna z buildu (REVERT przed merge)
7f9f257  feat(cna): checkpoint 7+8 - pełne UI z miniaturkami + LibVLC player
1267966  feat(cna): checkpoint 9 - audit log RODO
+ ten commit (checkpoint 10 - dokumentacja)
```

Przed merge do `master`:
1. Naprawić `AnalitykaPelna/Views/WidokWydajnosc.xaml.cs` (brakuje TextBlock'ów `txtBmMmElementy` itp. w XAML).
2. `git revert ba84c0d` — przywróci AnalitykaPelna do build i napraw factory dla kafelka.
3. Verify `dotnet build` zielony.
4. Merge / PR.
