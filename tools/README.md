# Narzędzia wspierające import AVILOG

## `avilog_pdf_table_extractor.py`

Skrypt korzysta z biblioteki [pdfplumber](https://github.com/jsvine/pdfplumber),
aby wyciągnąć wiersze tabeli z planu transportu AVILOG i zapisać je do JSON
lub CSV. Podejście tabelowe daje stabilniejsze wyniki niż parsowanie surowego
tekstu.

### Instalacja zależności

```bash
pip install pdfplumber
```

### Użycie

```bash
python tools/avilog_pdf_table_extractor.py /ścieżka/do/pliku.pdf --format csv
```

Domyślnie plik wynikowy powstaje obok źródłowego (z rozszerzeniem `.json` lub
`.csv`). Możesz wskazać własną lokalizację flagą `--output`.
