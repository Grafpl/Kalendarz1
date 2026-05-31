# Kontrakty Hodowców — kontraktacja (szablon Word + wdrożenie)

## Kolejność wdrożenia SQL (LibraNet, jednorazowo)
1. `SQL/01_Kontrakty_v2_schema.sql` — tabele + widoki + sp (już uruchomione).
2. `SQL/02_Kontrakty_proc_views.sql` — proc + widoki (jeśli 01 padło na batchu).
3. `SQL/03_Kontrakty_wersje_rozszerzenia.sql` — 13 kolumn warunków.
4. `SQL/04_Kontrakty_kontraktacja.sql` — EmailRODO, DostawcaPaszy/PisklatNazwa, BonusOpis, tabela `KontraktyHarmonogram`.

Ścieżka szablonu ROCZNY ustawiona w `dbo.KontraktyTemplates` na:
`\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_Kontraktacji_2026_PROJEKT.docx`

## Szablon Word — przygotowanie (jednorazowo)
1. Wgraj `Umowa_Kontraktacji_2026_PROJEKT.docx` do `\\192.168.0.170\Install\UmowyZakupu\_SZABLON\`.
2. (opcjonalnie) sprawdź strukturę i tokeny:
   `dotnet run --project ZPSP.Tools.AddBookmark -- --inspect "\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_Kontraktacji_2026_PROJEKT.docx"`
3. Wstaw bookmark wokół tabeli harmonogramu (raz):
   `dotnet run --project ZPSP.Tools.AddBookmark -- "\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_Kontraktacji_2026_PROJEKT.docx"`
   (jeśli nagłówek tabeli inny niż domyślny — podaj go jako 2. argument w cudzysłowie).

## Tokeny w szablonie (czego oczekuje generator)

### Pola skalarne `[Pole]` (gdziekolwiek w treści/nagłówku/stopce)
| Token | Źródło |
|---|---|
| `[NumerUmowy]` | nadany numer kontraktu (np. 1/27) |
| `[Dostawca]` | snapshot nazwy hodowcy |
| `[NIP]` | snapshot NIP |
| `[NumerGospodarstwa]` | snapshot nr gospodarstwa ARiMR |
| `[AdresHodowcy]` | snapshot adresu |
| `[EmailRODO]` | e-mail RODO |
| `[PESEL]` | PESEL (snapshot, z Dostawcy.Pesel) |
| `[REGON]` | REGON (snapshot, z Dostawcy.Regon) |
| `[NrDowodu]` | nr dowodu osobistego (snapshot, z Dostawcy.IDCard) |
| `[TelefonProducenta]` | telefon (snapshot, z Dostawcy.Phone1) |
| `[Podmiot]` | Piórkowscy s.c. / sp. z o.o. |
| `[DataZawarcia]` | data podpisania |
| `[DataOd]` / `[DataDo]` | okres obowiązywania |
| `[TypCeny]` | wolnorynkowa/rolnicza/… |
| `[Cena]` | cena zł/kg |
| `[DodatekStały]` | dodatek zł/kg |
| `[Ubytek]` | % ubytku |
| `[TerminPlatnosci]` | dni |
| `[DostawcaPaszy]` / `[DostawcaPiskląt]` | nazwy |
| `[BonusJesli]` | opis bonusu warunkowego |
| `[InneUstalenia]` | klauzule szczególne |

### Tabela harmonogramu (jeden WIERSZ-WZORZEC z tokenami; generator klonuje go per cykl)
Wiersz wzorcowy w tabeli oznaczonej bookmarkiem `BMK_HARMONOGRAM`:
`[Cykl_Nr]`, `[Cykl_DataWstawienia]`, `[Cykl_IloscWstaw]`, `[Cykl_IloscUbiorki]`, `[Cykl_DzienUbiorki]`, `[Cykl_DataUboju]`, `[Cykl_IloscUboju]`

Domyślny tekst nagłówka nad tabelą: **HARMONOGRAM WSTAWIEŃ I ODBIORÓW**.

> Jeśli w Twoim szablonie nazwy pól/nagłówek są inne — albo dostosuj szablon do powyższych,
> albo zmień nazwy w `WordTemplateService.BuildKontraktacjaTokens` / `FillScheduleTable` i w `AddBookmark`.

## Użycie w aplikacji
- **Nowy kontrakt**: lista Kontrakty → „➕ Nowy kontrakt" (kreator 3-krokowy) → „Zapisz i generuj Word".
- **Istniejący**: karta kontraktu → „📄 Generuj Word" (regeneruje z bieżącej wersji + harmonogramu).
- **Ze Sprawdzalki Umów**: zaznacz dostawę → „📜 Kontrakt hodowcy" (otwiera aktywny lub kreator z prefill).
- **Skany**: karta → sekcja „Załączniki" → „📎 Dodaj plik" (kopia na `…\<rok>\Skany\…`).
