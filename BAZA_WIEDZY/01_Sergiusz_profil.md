# 01 — Sergiusz Piórkowski (właściciel firmy + programista ZPSP)

**Rola formalna:** Właściciel + Prezes Zarządu Ubojni Drobiu Piórkowscy (Koziołki, gmina Brzeziny, woj. łódzkie).

**Wspólnik:** Marcin Piórkowski (brat) — masarnia w Zgierzu.

**Dziadek (założyciel firmy 1996):** Jerzy Piórkowski.

**Email kontaktowy:** sergiusz.piorko@gmail.com (Fireflies organizator, Claude Code, M365 planowane).

---

## 18+ ról jednocześnie (z dokumentu Zakres_Obowiazkow_MEGA marzec 2026)

| Rola | Zakres |
|---|---|
| Właściciel / Prezes Zarządu | Decyzje strategiczne, inwestycje, akceptacja kontraktów >1000 zł |
| Dyrektor Operacyjny (COO) | Nadzór wszystkich działów (faktycznie pełni ją sam) |
| **Dyrektor Techniczny / CTO** | **Sam programuje ZPSP w C#/.NET — od ~5 lat** |
| Programista Full-Stack | WPF / WinForms / SQL Server / DevExpress |
| Dyrektor Sprzedaży (tymczasowo) | Nadzór handlowców (cel: oddać Teresie Jachymczak) |
| Dyrektor Zakupów | Negocjacje z hodowcami, kontrakty |
| Kierownik IT / Admin | M365, telefony, WebFleet, Symfonia, hostingi |
| Analityk Danych / BI | Dashboardy, KPI, raporty Power BI |
| Negocjator Kontraktów | Chłodnia 4.73 mln, ubezpieczenia 226 tys. |
| Compliance | KSeF, IRZplus, HACCP, BRC/IFS w przygotowaniu |
| HR / Kadry | Rekrutacja, procedury 200+ stron, oświadczenia |
| Mediator | Konflikty zespołowe (Jola/Justyna, Teresa/Paulina) |
| Inwestor | Chłodnia, fotowoltaika 150 kW, magazyn energii 250 tys., trafo |
| Marketing / Strategia | Pozyskiwanie hodowców (CRM 1874 hodowców z Excela) |

**Czego NIE robi sam (i chciałby delegować):**
- Codziennej pracy ze sprzedażą — cel: handlowcy bez monopolu Pani Joli
- Roli **Dyrektora Zakładu** (operacyjny szef wszystkich działów) — **kluczowe wakat**, oczekuje że Justyna Chrostowska wejdzie głębiej
- Codziennych mikro-decyzji — cel: procedury automatyzują 80% rutyny
- Spotkań handlowców 13:00 (decyzja krojenie/mrożenie) — chce żeby Teresa + Justyna prowadziły je same

---

## Stack i preferencje techniczne

**Język + framework:**
- **C# .NET 8** (target: `net8.0-windows7.0`)
- **WPF preferowane** (nowsze moduły)
- **WinForms** (legacy — Menu, niektóre starsze okna, sukcesywnie migrowane)
- **DevExpress** (licencja ~1100 USD/rok, GridControl, dxe:TextEditSettings) — rozważa Syncfusion alternatywę

**Bazy danych:**
- **SQL Server 2017+** (HANDEL na 192.168.0.112 — Sage Symfonia)
- **SQL Server starszy** (LibraNet na 192.168.0.109 — bez `TRY_CONVERT`, używać `CONVERT(... 120)`)
- **UNISYSTEM** (192.168.0.23\SQLEXPRESS — UNICARD RCP, godziny pracowników)
- **ZPSP** (DB własna, HR_* tabele, transport, partie)

**Narzędzia:**
- **Claude Code CLI** — używa codziennie do programowania
- **Visual Studio** + **GitHub Desktop**
- **Fireflies** — nagrywa większość spotkań, używa transkrypcji jako pamięci
- **M365 + Teams** — w planie migracji z WhatsApp

---

## Charakterystyka komunikacji

**Sposób pisania:**
- **Pisze dużo, dynamicznie, z przerwami** — często zaczyna jedną myśl, przerywa, wraca
- **Voice-to-text** często używa — wtedy literówki + brak interpunkcji, ale **gęste w treści** (czytać 2x)
- Lubi **długie szczegółowe odpowiedzi** gdy sam zaprasza ("opowiedz mi", "powiedz dokładnie")
- Lubi **konkrety** (file:line:konkretna funkcja) — bo czyta kod sam
- Reaguje **emocjonalnie** gdy frustracja: "Wkurza mnie to że...", "Daruj te pytania o ADHD"

**Sposób decydowania:**
- **NIE lubi propozycji 5 zmian naraz** w niezwiązanych miejscach — woli rozmowę i jedną dobrze przemyślaną
- Często używa formuły **"Można spróbować. Najwyżej poprawimy"** — co znaczy "tak, idź do przodu"
- Gdy mówi **"Cofnij to wszystko"** = serio cofnij, nie próbuj negocjować
- **"Daruj sobie"** = zmień podejście, nie dyskutuj
- Lubi **mega-dokumenty** (50+ KB plików .md z syntezą) bo daje mu to pewność że agent zrozumiał

**Charakterystyka motywacyjna:**
- **Klient-developer** — może czytać/pisać kod, więc traktuj go jak partnera technicznego, nie tylko klienta
- **Wyczerpany ilością ról** — często pracuje wieczorami nad ZPSP po dniu w firmie
- **Ambicja: 258M obrotu → eksport bezpośredni, IFS/BRC, automatyzacja**
- **Strach: HPAI, kryzys luty 2026 (-2M strat), Mercosur (Brazylia 13 zł/kg vs nasze 15-17 zł)**

---

## Język wewnętrzny (skróty Sergiusza)

- **ZPSP** = "Zajebisty Program Sergiusza Piórkowskiego" = `Kalendarz1.csproj`. Tak nazywa swój autorski system w komunikacji firmowej.
- **Hala** = budynek produkcyjny (brudna + czysta + rozbiór + magazyn dystrybucji + wydawka)
- **Brudna** = strefa uboju + patroszenia (Łukasz Collins)
- **Czysta** = strefa po chłodzeniu (klasyfikacja A/B + rozbiór + filet)
- **Tuszka** = cały kurczak po patroszeniu i chłodzeniu (78% z żywca)
- **Element** = filet, ćwiartka, korpus, skrzydło — wynik rozbioru tuszki
- **Krojenie** = rozbiór tuszki na elementy (moduł 14A z hardcoded uzysk %)
- **Awizacja** = data i godzina deklarowana przez klienta na odbiór towaru
- **Bufor** = 5-6 ton zostawiane jako rezerwa dziennie
- **Ucinanie** = proporcjonalna redukcja zamówień gdy produkcja < zamówień

---

## Dlaczego to robi sam (programuje ZPSP)

Sergiusz **nie ufa zewnętrznym dostawcom oprogramowania** dla niszowej branży drobiarskiej:
- Symfonia Handel (Sage) — dobra do faktur, ale nie do produkcji
- Symfonia Production module — kupiony, nigdy nie wdrożony (87 tabel `MF.Production` puste)
- Power BI — używa do analiz post-factum, ale nie do operacyjnego sterowania

**ZPSP rośnie jako jego własna nadbudowa nad Symfonią + LibraNet.** Łączy dane z 4 baz w jedno miejsce. Każde nowe okno to próba rozwiązania konkretnego pain point z hali / magazynu / sprzedaży.

**Lekcja dla agenta:** Sergiusz **nie chce kupować gotowych systemów**. Chce żebyś rozumiał jego firmę i pomagał mu pisać ZPSP dalej.
