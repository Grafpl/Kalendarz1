using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace Kalendarz1.Zywiec.WidokSpecyfikacji
{
    /// <summary>
    /// Prosty czytnik plików ODS (LibreOffice Calc / OpenDocument Spreadsheet)
    /// </summary>
    public class OdsReader : IDisposable
    {
        private ZipArchive _archive;
        private XDocument _contentDoc;
        private XNamespace _tableNs = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
        private XNamespace _textNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        private XNamespace _officeNs = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
        
        private List<OdsWorksheet> _worksheets = new List<OdsWorksheet>();

        public IReadOnlyList<OdsWorksheet> Worksheets => _worksheets;

        public OdsReader(string filePath)
        {
            LoadFile(filePath);
        }

        public OdsReader(Stream stream)
        {
            LoadStream(stream);
        }

        private void LoadFile(string filePath)
        {
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            LoadStream(fileStream);
        }

        private void LoadStream(Stream stream)
        {
            _archive = new ZipArchive(stream, ZipArchiveMode.Read);
            
            var contentEntry = _archive.GetEntry("content.xml");
            if (contentEntry == null)
                throw new InvalidOperationException("Plik ODS nie zawiera content.xml");

            using (var contentStream = contentEntry.Open())
            {
                _contentDoc = XDocument.Load(contentStream);
            }

            ParseWorksheets();
        }

        private void ParseWorksheets()
        {
            var tables = _contentDoc.Descendants(_tableNs + "table");
            
            foreach (var table in tables)
            {
                string sheetName = table.Attribute(_tableNs + "name")?.Value ?? "Arkusz";
                var worksheet = new OdsWorksheet(sheetName, table, _tableNs, _textNs, _officeNs);
                _worksheets.Add(worksheet);
            }
        }

        public OdsWorksheet Worksheet(string name)
        {
            return _worksheets.FirstOrDefault(w => w.Name == name)
                ?? throw new ArgumentException($"Arkusz '{name}' nie istnieje");
        }

        public OdsWorksheet Worksheet(int index)
        {
            if (index < 0 || index >= _worksheets.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _worksheets[index];
        }

        public void Dispose()
        {
            _archive?.Dispose();
        }
    }

    /// <summary>
    /// Reprezentuje arkusz w pliku ODS
    /// </summary>
    public class OdsWorksheet
    {
        public string Name { get; }
        
        private XElement _tableElement;
        private XNamespace _tableNs;
        private XNamespace _textNs;
        private XNamespace _officeNs;
        
        private List<List<OdsCell>> _rows = new List<List<OdsCell>>();

        public OdsWorksheet(string name, XElement tableElement, XNamespace tableNs, XNamespace textNs, XNamespace officeNs)
        {
            Name = name;
            _tableElement = tableElement;
            _tableNs = tableNs;
            _textNs = textNs;
            _officeNs = officeNs;
            
            ParseRows();
        }

        private void ParseRows()
        {
            var rowElements = _tableElement.Elements(_tableNs + "table-row");
            
            foreach (var rowElement in rowElements)
            {
                // Sprawdź czy wiersz jest powtórzony
                int rowRepeat = 1;
                var repeatAttr = rowElement.Attribute(_tableNs + "number-rows-repeated");
                if (repeatAttr != null && int.TryParse(repeatAttr.Value, out int rep))
                {
                    rowRepeat = Math.Min(rep, 100); // Ogranicz do 100 powtórzeń
                }

                var cells = ParseCells(rowElement);
                
                for (int i = 0; i < rowRepeat; i++)
                {
                    _rows.Add(cells.Select(c => c.Clone()).ToList());
                }
            }
        }

        private List<OdsCell> ParseCells(XElement rowElement)
        {
            var cells = new List<OdsCell>();
            var cellElements = rowElement.Elements()
                .Where(e => e.Name == _tableNs + "table-cell" || e.Name == _tableNs + "covered-table-cell");

            foreach (var cellElement in cellElements)
            {
                // Sprawdź czy komórka jest powtórzona
                int colRepeat = 1;
                var repeatAttr = cellElement.Attribute(_tableNs + "number-columns-repeated");
                if (repeatAttr != null && int.TryParse(repeatAttr.Value, out int rep))
                {
                    colRepeat = Math.Min(rep, 100); // Ogranicz do 100 powtórzeń
                }

                var cell = ParseCell(cellElement);
                
                for (int i = 0; i < colRepeat; i++)
                {
                    cells.Add(cell.Clone());
                }
            }

            return cells;
        }

        private OdsCell ParseCell(XElement cellElement)
        {
            var cell = new OdsCell();

            // Typ wartości
            var valueType = cellElement.Attribute(_officeNs + "value-type")?.Value;
            cell.ValueType = valueType ?? "string";

            // Pobierz wartość w zależności od typu
            switch (cell.ValueType)
            {
                case "float":
                case "percentage":  // Procenty też mają wartość liczbową w office:value
                case "currency":    // Waluta również
                    var floatValue = cellElement.Attribute(_officeNs + "value")?.Value;
                    if (floatValue != null && decimal.TryParse(floatValue, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal decVal))
                    {
                        cell.NumericValue = decVal;
                        cell.TextValue = decVal.ToString(CultureInfo.InvariantCulture);
                    }
                    break;

                case "date":
                    var dateValue = cellElement.Attribute(_officeNs + "date-value")?.Value;
                    if (dateValue != null && DateTime.TryParse(dateValue, out DateTime dateVal))
                    {
                        cell.DateValue = dateVal;
                        cell.TextValue = dateVal.ToString("yyyy-MM-dd");
                    }
                    break;

                case "time":
                    var timeValue = cellElement.Attribute(_officeNs + "time-value")?.Value;
                    cell.TextValue = timeValue ?? "";
                    break;

                case "boolean":
                    var boolValue = cellElement.Attribute(_officeNs + "boolean-value")?.Value;
                    cell.TextValue = boolValue ?? "";
                    break;

                default:
                    // Pobierz tekst z elementów <text:p>
                    var textElements = cellElement.Descendants(_textNs + "p");
                    cell.TextValue = string.Join("\n", textElements.Select(t => t.Value));
                    break;
            }

            // Jeśli brak wartości tekstowej, spróbuj pobrać z text:p
            if (string.IsNullOrEmpty(cell.TextValue))
            {
                var textElements = cellElement.Descendants(_textNs + "p");
                cell.TextValue = string.Join("\n", textElements.Select(t => t.Value));
            }

            return cell;
        }

        /// <summary>
        /// Pobiera komórkę (1-indexed jak w Excel/ClosedXML)
        /// </summary>
        public OdsCell Cell(int row, int column)
        {
            int rowIndex = row - 1;
            int colIndex = column - 1;

            if (rowIndex < 0 || rowIndex >= _rows.Count)
                return new OdsCell(); // Pusta komórka

            var rowCells = _rows[rowIndex];
            if (colIndex < 0 || colIndex >= rowCells.Count)
                return new OdsCell(); // Pusta komórka

            return rowCells[colIndex];
        }

        /// <summary>
        /// Pobiera komórkę po adresie (np. "A1", "B21")
        /// </summary>
        public OdsCell Cell(string address)
        {
            var (row, col) = ParseCellAddress(address);
            return Cell(row, col);
        }

        private (int row, int col) ParseCellAddress(string address)
        {
            int col = 0;
            int row = 0;
            int i = 0;

            // Parsuj litery (kolumna)
            while (i < address.Length && char.IsLetter(address[i]))
            {
                col = col * 26 + (char.ToUpper(address[i]) - 'A' + 1);
                i++;
            }

            // Parsuj cyfry (wiersz)
            while (i < address.Length && char.IsDigit(address[i]))
            {
                row = row * 10 + (address[i] - '0');
                i++;
            }

            return (row, col);
        }

        /// <summary>
        /// Zwraca liczbę wierszy z danymi
        /// </summary>
        public int RowCount => _rows.Count;
    }

    /// <summary>
    /// Reprezentuje pojedynczą komórkę ODS
    /// </summary>
    public class OdsCell
    {
        public string ValueType { get; set; } = "string";
        public string TextValue { get; set; } = "";
        public decimal? NumericValue { get; set; }
        public DateTime? DateValue { get; set; }

        public bool IsBlank => string.IsNullOrWhiteSpace(TextValue) && NumericValue == null && DateValue == null;

        public bool IsDateTime => DateValue.HasValue;
        public bool IsNumber => NumericValue.HasValue;
        public bool IsPercentage => ValueType == "percentage" && NumericValue.HasValue;

        public DateTime GetDateTime()
        {
            return DateValue ?? DateTime.MinValue;
        }

        public double GetDouble()
        {
            return (double)(NumericValue ?? 0);
        }

        public int GetInt()
        {
            return (int)(NumericValue ?? 0);
        }

        public decimal GetDecimal()
        {
            return NumericValue ?? 0;
        }

        public override string ToString()
        {
            if (DateValue.HasValue)
                return DateValue.Value.ToString("yyyy-MM-dd");
            if (NumericValue.HasValue)
                return NumericValue.Value.ToString(CultureInfo.InvariantCulture);
            return TextValue ?? "";
        }

        public OdsCell Clone()
        {
            return new OdsCell
            {
                ValueType = this.ValueType,
                TextValue = this.TextValue,
                NumericValue = this.NumericValue,
                DateValue = this.DateValue
            };
        }
    }
}
