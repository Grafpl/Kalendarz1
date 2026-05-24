# 29. ZSRIR — integracja API z Ministerstwem Rolnictwa

> Utworzony 2026-05-19 na podstawie research'u 3 oficjalnych PDF-ów MRiRW (Instrukcja API, Instrukcja użytkownika v1.0.3, Metodologia rynku drobiu).
> System operatora: PWPW S.A. (wdrożenie lipiec 2024), Departament Rynków Rolnych MRiRW.

---

## 1. Podstawa prawna i obowiązek

- **Ustawa** z 30 marca 2001 o rolniczych badaniach rynkowych (Dz.U. 2015 poz. 1160)
- **Rozporządzenie MRiRW** z 8 marca 2021 (Dz.U. 2021 poz. 589) — co i kto raportuje
- **Rozp. MRiRW** z 6 lipca 2022 (Dz.U. 2022 poz. 1506) — kontrole
- **Podmiot kontrolujący:** IJHARS (drób = kontrola raz na 3 lata)

**Próg obowiązku dla drobiu rzeźnego** (§ 13 ust. 4 rozp. 2021/589):
- > 100 000 brojlerów kurzych / rok, LUB
- > 20 000 indyczych, LUB
- > 500 kaczych/gęsich
- ORAZ otrzymanie pisemnego zawiadomienia ministra

**Piórkowscy:** 200 t/dzień ≈ **12-14 mln szt./rok** → na pewno podlegają.

## 2. Co się raportuje (rynek drobiu)

10 kategorii towarowych:
- Kurczęta brojler (główna kategoria dla Piórkowskich)
- Indory, indyczki
- Brojlery kacze/gęsie
- Gęsi tuczone
- 4 typy kur (nioski/mięsne)

**Per kategoria:**
- **Cena skupu netto** w **zł/TONĘ** (UWAGA: nie zł/kg — konwersja ×1000!) — 2 miejsca po przecinku
- **Wielkość obrotu** w **TONACH** netto

**Wzór:** `Cena = Wartość_netto / Ilość_ton` (per § II.4 metodologii)

**Wyłączenia:** netto **bez VAT**, **bez transportu**, **bez palet**, **bez ubezpieczenia**

## 3. Częstotliwość i deadline

- **Okres:** tydzień **pn–niedz**
- **Deadline:** **wtorek 12:00** po zakończeniu okresu
- Archiwizacja dokumentów: min. **2 lata** (na potrzeby IJHARS)
- Jeśli brak skupu w okresie → wysłać **formularz zerowy** (`AddFormZero`)

## 4. API techniczne

**Operator infrastrukturny:** PWPW S.A.
**Baza URL:** `https://zsrir.minrol.gov.pl/api`
**Portal WWW** (Angular SPA): `https://zsrir.minrol.gov.pl`

### 4.1. Autoryzacja

```
POST /api/Auth/GetApiAccessToken
Content-Type: application/json

{"username": "...", "password": "..."}
```
→ Response: JWT Bearer token, **TTL 60 minut**. Refresh przed wygaśnięciem.

⚠️ **API używa tych samych poświadczeń co WWW, ale BEZ 2FA**. 2FA (kod e-mail TTL 6 min) działa tylko przy logowaniu przez WWW. Dla API → osobne **service account**, hasło rotowane wg polityki MRiRW (60 dni).

### 4.2. Endpointy (kontroler `DataSupplierFormApi`)

| Metoda | Endpoint | Cel |
|---|---|---|
| GET | `/GetDataSuppliers` | Lista dostawców pod kontem |
| GET | `/GetForms?dataSupplierId=X` | Formularze dostawcy |
| GET | `/GetReportingPeriods?formId=X` | Okresy + `dateTimeEnd` (deadline) |
| GET | `/GetFormConfiguration?formId=X` | Definicja pól (typy: Price/Amount/Count/Percent/Description/Option, min/max, required) |
| POST | `/AddForm` | Wysłanie raportu |
| POST | `/AddFormZero` | Formularz zerowy (brak skupu) |

### 4.3. Body `POST /AddForm`

```json
{
  "formReportingPeriodId": 12345,
  "dataSupplierId": 678,
  "forms": [
    {
      "commodityGroupId": 1,
      "formFieldsValues": {
        "Price": 5100.00,
        "Amount": 46.050
      }
    }
  ]
}
```

- `Price` w **zł/tona**
- `Amount` w **tonach**
- Klucze pól z `GetFormConfiguration` (mogą się różnić — runtime lookup)

### 4.4. Format wymiany
- JSON over HTTPS
- Header: `Authorization: Bearer <token>` + `Content-Type: application/json; charset=utf8`
- Błędy: 400/401 z opisowym `message` w body

## 5. Co NIE jest obsługiwane

- ❌ Import z Excel/CSV (jest tylko **eksport** zaraportowanych danych)
- ❌ Logowanie ePUAP / Profil Zaufany — tylko login+hasło+email-code
- ❌ Webhooks/push z systemu — tylko pull od strony klienta
- ❌ Dedykowany konektor w Sage Symfonia / Comarch / ECOD — **nikt nie zbudował**. Piórkowscy są pierwsi.

## 6. Implementacja w ZPSP

### 6.1. Struktura plików

```
ZSRIR/
├── SQL/
│   └── CreateZsrirTables.sql       # Tabela ZsrirSubmissions w LibraNet
├── Models/
│   └── ZsrirModels.cs              # POCO dla API
├── Services/
│   ├── ZsrirSecrets.cs             # Hasło z %LOCALAPPDATA% (poza repo)
│   ├── ZsrirApiClient.cs           # HttpClient + token cache
│   ├── ZsrirSubmissionsRepo.cs     # SQL CRUD historii
│   └── ZsrirDataBuilder.cs         # Agregacja z HANDEL/LibraNet
└── Views/
    ├── ZsrirWindow.xaml(.cs)       # Historia + Wyślij ręcznie
    └── ZsrirSettingsDialog.xaml(.cs)  # Login/hasło/konfiguracja
```

### 6.2. Secrets

`%LOCALAPPDATA%\Kalendarz1\Zsrir\secrets.json`:
```json
{
  "Username": "service_account@piorkowscy.com.pl",
  "Password": "...",
  "DataSupplierId": 678,
  "FormId": 42
}
```
Plik **poza repo** (analogicznie do `CentrumNagranAI/secrets.json`).

### 6.3. Tabela `ZsrirSubmissions` (LibraNet)

| Kolumna | Typ | Opis |
|---|---|---|
| Id | int PK identity | |
| OkresOd | date | Pon zakresu |
| OkresDo | date | Niedz zakresu |
| KategoriaTowaru | varchar(50) | np. "Brojler kurzy" |
| CommodityGroupId | int | ID z API |
| KgRazem | decimal(18,2) | suma kg |
| TonyRazem | decimal(18,3) | kg/1000 |
| WartoscNetto | decimal(18,2) | zł |
| CenaZlTona | decimal(18,2) | wartosc/tony |
| FormReportingPeriodId | int | ID okresu z API |
| DataSupplierId | int | ID dostawcy z API |
| Status | varchar(20) | Pending/Sent/Failed/Zero |
| ApiResponse | nvarchar(max) | JSON odpowiedzi/błędu |
| WyslanyPrzez | int | UserID (operators.ID) |
| WyslanyDataCzas | datetime | |
| CreatedAt | datetime DEFAULT GETDATE() | |

Patrz `ZSRIR/SQL/CreateZsrirTables.sql`.

## 7. Workflow tygodniowy

1. **Poniedziałek wieczór** (po zakończeniu niedzielnego okresu):
   - User otwiera Menu → ZAOPATRZENIE I ZAKUPY → 📡 ZSRIR
   - Auto-podgląd danych za poprzedni tydzień (z `SprawozdaniaWindow` logiki: HANDEL + LibraNet + Specyfikacja FarmerCalc)
2. **Wtorek do 12:00:**
   - User weryfikuje (Wartość netto, kg, cena) i klika "📤 Wyślij do ZSRIR"
   - System: token → AddForm → zapisuje response do `ZsrirSubmissions`
   - Jeśli skup = 0 → AddFormZero
3. **Historia:** lista poprzednich wysyłek (status, odpowiedź API, użytkownik, timestamp)
4. **Alert na pasku menu** gdy brak wysyłki po poniedziałku 23:59 (analogiczne do `_transportPendingBadge`)

## 8. Kontakt MRiRW

- **Departament Rynków Rolnych** — kontakt metodologiczny
- **Małgorzata Czeczko:** malgorzata.czeczko@minrol.gov.pl, tel. 22 623-16-06
- Wniosek o konto API + rola "Użytkownik dostawcy danych"

## 9. Źródła

- [Strona programu ZSRIR](https://www.gov.pl/web/rolnictwo/program-zsrir)
- [Instrukcja API (PDF)](https://www.gov.pl/attachment/58f41a08-4230-47a6-9c63-5b0beb83efdb) — kluczowa, 15 stron
- [Instrukcja użytkownika (PDF v1.0.3, 2025-05-13)](https://www.gov.pl/attachment/d4f0a7d1-eb56-4261-a7ae-e7505307b8fd)
- [Metodologia rynku drobiu (PDF)](https://www.gov.pl/attachment/739ed1c0-578e-42d7-acfb-f584d14b2fe8)
- [Portal ZSRIR](https://zsrir.minrol.gov.pl/)
- [Kontrola IJHARS](https://www.gov.pl/web/ijhars/kontrola-raportowania-danych-rynkowych)

---

**Aktualizacja:** 2026-05-19 — utworzone razem z modułem ZSRIR/ w ZPSP.
**Powiązane:** `SprawozdaniaWindow` (źródło danych), `23_HANDEL_Schema_Sage_Symfonia.md`, `13_Bazy_danych.md`.
