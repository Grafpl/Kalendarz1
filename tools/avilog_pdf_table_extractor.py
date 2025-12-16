"""
Lightweight helper for pulling AVILOG transport plans out of PDF files.

The script uses pdfplumber's table extraction to capture rows instead of
relying on raw text parsing. It keeps the columns as they appear in the AVILOG
layout and emits JSON or CSV for downstream validation/import.
"""

from __future__ import annotations

import argparse
import csv
import json
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Iterable, List, Sequence

import pdfplumber


@dataclass
class AvilogRow:
    page: int
    kierowca: str | None
    hodowca: str | None
    ciagnik: str | None
    naczepa: str | None
    ilosc_sztuk: str | None
    wymiary: str | None
    wyjazd_zaklad: str | None
    przyjazd_hodowca: str | None
    poczatek_zaladunku: str | None
    koniec_zaladunku: str | None
    wyjazd_hodowca: str | None
    powrot_zaklad: str | None
    obserwacje: str | None
    raw: List[str]


TABLE_SETTINGS = {
    "vertical_strategy": "lines",
    "horizontal_strategy": "lines",
    "intersection_x_tolerance": 5,
    "intersection_y_tolerance": 5,
    "snap_tolerance": 3,
}


def normalize_cell(value: str | None) -> str | None:
    if value is None:
        return None
    cleaned = " ".join(value.replace("\n", " ").split())
    return cleaned or None


def parse_table(table: Sequence[Sequence[str | None]], page_number: int) -> Iterable[AvilogRow]:
    headers, *rows = table
    header_map = [normalize_cell(h) for h in headers]

    def get(row: Sequence[str | None], label: str) -> str | None:
        try:
            idx = header_map.index(label)
        except ValueError:
            return None
        return normalize_cell(row[idx]) if idx < len(row) else None

    for raw_row in rows:
        normalized_raw = [normalize_cell(cell) or "" for cell in raw_row]
        yield AvilogRow(
            page=page_number,
            kierowca=get(raw_row, "KIEROWCA"),
            hodowca=get(raw_row, "HODOWCA"),
            ciagnik=get(raw_row, "CIĄGNIK"),
            naczepa=get(raw_row, "NACZEPA"),
            ilosc_sztuk=get(raw_row, "ILOŚĆ SZTUK"),
            wymiary=get(raw_row, "WYMIAR SKRZYŃ"),
            wyjazd_zaklad=get(raw_row, "WYJAZD Z ZAKŁADU"),
            przyjazd_hodowca=get(raw_row, "PRZYJAZD NA ZAKŁAD"),
            poczatek_zaladunku=get(raw_row, "POCZĄTEK ZAŁADUNKU"),
            koniec_zaladunku=get(raw_row, "KONIEC ZAŁADUNKU"),
            wyjazd_hodowca=get(raw_row, "WYJAZD OD HODOWCY"),
            powrot_zaklad=get(raw_row, "POWRÓT NA ZAKŁAD"),
            obserwacje=get(raw_row, "OBSERWACJE"),
            raw=normalized_raw,
        )


def extract_rows(pdf_path: Path) -> List[AvilogRow]:
    rows: List[AvilogRow] = []
    with pdfplumber.open(pdf_path) as pdf:
        for page_number, page in enumerate(pdf.pages, start=1):
            for table in page.extract_tables(table_settings=TABLE_SETTINGS):
                if len(table) < 2:
                    continue
                rows.extend(parse_table(table, page_number))
    return rows


def write_output(rows: List[AvilogRow], output: Path, fmt: str) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    if fmt == "json":
        data = [asdict(row) for row in rows]
        output.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    elif fmt == "csv":
        fieldnames = list(asdict(rows[0]).keys()) if rows else []
        with output.open("w", newline="", encoding="utf-8") as csvfile:
            writer = csv.DictWriter(csvfile, fieldnames=fieldnames)
            writer.writeheader()
            for row in rows:
                writer.writerow(asdict(row))
    else:
        raise ValueError(f"Unsupported format: {fmt}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Extract AVILOG transport plan tables from PDF")
    parser.add_argument("pdf", type=Path, help="Path to AVILOG PDF file")
    parser.add_argument("--output", "-o", type=Path, help="Where to save the parsed data")
    parser.add_argument("--format", "-f", choices=["json", "csv"], default="json", help="Output format")
    args = parser.parse_args()

    rows = extract_rows(args.pdf)
    if not rows:
        raise SystemExit("No rows extracted. Check if the PDF layout matches the expected AVILOG table format.")

    output_path = args.output or args.pdf.with_suffix(f".{args.format}")
    write_output(rows, output_path, args.format)
    print(f"Parsed {len(rows)} rows from {args.pdf} -> {output_path}")


if __name__ == "__main__":
    main()
