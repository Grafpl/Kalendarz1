# Audyt modułu "Zaopatrzenie i Zakup" + projekt modułu Kontraktów Hodowców

**Data:** 2026-05-23
**Autor:** Claude (na zlecenie Sergiusza)
**Kontekst kadrowy:** Paulina odeszła • Magda wchodzi w poniedziałek • Tereska niedługo odchodzi • Asia → strażnik kontraktów
**Kontekst biznesowy:** ARiMR (deadline IX.2026, do 10 mln PLN, warunek = 3-letnie kontrakty na ≥50% surowca) • transformacja w sp. z o.o. (deadline 01.08.2026)

---

## Struktura audytu

| Część | Plik | Status |
|---|---|---|
| **1. Audyt 16 kafelków** | [`01_KAFELKI_AUDYT.md`](01_KAFELKI_AUDYT.md) | ✅ gotowe |
| **2. Wdrożenie Magdy** | [`02_WDROZENIE_MAGDY.md`](02_WDROZENIE_MAGDY.md) | ✅ gotowe |
| **3. Centrum Asi (strażnik kontraktów)** | [`03_CENTRUM_ASI.md`](03_CENTRUM_ASI.md) | ✅ gotowe |
| **4. Spec modułu Kontrakty Hodowców** | [`04_MODUL_KONTRAKTY_SPEC.md`](04_MODUL_KONTRAKTY_SPEC.md) | ✅ gotowe |
| **5. Quick wins na weekend** | [`05_QUICK_WINS_WEEKEND.md`](05_QUICK_WINS_WEEKEND.md) | ✅ gotowe |
| **6. Komunikat dla zespołu (3 warianty)** | [`06_KOMUNIKAT_ZESPOL.md`](06_KOMUNIKAT_ZESPOL.md) | ✅ bonus |
| **7. Plan refactoru plików-monstrów** | [`07_PLAN_REFACTOR_MONSTROW.md`](07_PLAN_REFACTOR_MONSTROW.md) | ✅ bonus |
| **8. Centrum Asi — pełna spec techniczna** | [`08_CENTRUM_ASI_PELNA_SPEC.md`](08_CENTRUM_ASI_PELNA_SPEC.md) | ✅ bonus |
| **SQL — wykonywalna schema Kontraktów** | [`SQL/01_Kontrakty_v1_schema.sql`](SQL/01_Kontrakty_v1_schema.sql) | ✅ bonus |
| **Kod startowy C# — Kontrakty Hodowców** | [`KOD_STARTOWY/README.md`](KOD_STARTOWY/README.md) | ✅ bonus |
| **Szablony Word — wzór umowy + instrukcja bookmarków** | [`Szablony_Word/00_README_jak_zrobic_bookmarki.md`](Szablony_Word/00_README_jak_zrobic_bookmarki.md) | ✅ bonus |

---

## Reguły gry

- ✅ Wszystko **w osobnym folderze audytu** — żadnych zmian w kodzie produkcyjnym
- ✅ Stack: **C#/.NET 8.0/WPF/DevExpress/SQL Server** (bez migracji)
- ✅ Per kafelek: tabela SQL / nazwa widoku / mockup ekranu
- ✅ Po polsku, bullet pointy, terminy techniczne PO angielsku
- ✅ Trzymanie się tego co realnie potrzebuje Magda, Asia, ARiMR
