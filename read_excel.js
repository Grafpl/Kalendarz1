const XLSX = require('xlsx');
const path = require('path');

const filePath = path.join(__dirname, 'Baza Hodowców Asia 2.xlsx');

console.log('Reading file:', filePath);
console.log('');

const workbook = XLSX.readFile(filePath);

// 1. List all sheet names
console.log('=== SHEET NAMES ===');
workbook.SheetNames.forEach((name, i) => {
    console.log(`  ${i + 1}. "${name}"`);
});
console.log('');

// 2. Read the first sheet
const sheetName = workbook.SheetNames[0];
console.log(`=== Reading sheet: "${sheetName}" ===`);
console.log('');

const sheet = workbook.Sheets[sheetName];

// Get range
const range = XLSX.utils.decode_range(sheet['!ref']);
console.log(`Range: ${sheet['!ref']}`);
console.log(`Rows: ${range.e.r - range.s.r + 1} (including header)`);
console.log(`Columns: ${range.e.c - range.s.c + 1}`);
console.log('');

// Convert to JSON with headers
const data = XLSX.utils.sheet_to_json(sheet, { defval: '' });

// 3. Show column headers
const headers = data.length > 0 ? Object.keys(data[0]) : [];
console.log('=== COLUMN HEADERS ===');
headers.forEach((h, i) => {
    console.log(`  ${i + 1}. "${h}"`);
});
console.log('');

// 4. Show first 5 rows
console.log('=== FIRST 5 ROWS ===');
const firstRows = data.slice(0, 5);
firstRows.forEach((row, i) => {
    console.log(`--- Row ${i + 1} ---`);
    headers.forEach(h => {
        const val = row[h];
        if (val !== '' && val !== null && val !== undefined) {
            console.log(`  ${h}: ${JSON.stringify(val)}`);
        } else {
            console.log(`  ${h}: (empty)`);
        }
    });
    console.log('');
});

// 5. Total row count
console.log(`=== TOTAL DATA ROWS (excluding header): ${data.length} ===`);
