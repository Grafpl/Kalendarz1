const XLSX = require('xlsx');
const path = require('path');

const filePath = path.join(__dirname, 'Baza Hodowców Asia 2.xlsx');
const workbook = XLSX.readFile(filePath);

// Check how many rows actually have meaningful data (non-empty Dostawca)
const sheet = workbook.Sheets['Baza Hodowców'];
const data = XLSX.utils.sheet_to_json(sheet, { defval: '' });

let nonEmptyCount = 0;
data.forEach(row => {
    if (row['Dostawca'] && row['Dostawca'].toString().trim() !== '') {
        nonEmptyCount++;
    }
});
console.log(`Rows with non-empty "Dostawca": ${nonEmptyCount}`);
console.log(`Total rows parsed: ${data.length}`);

// Quick peek at sheets 2-4
['Dane', 'Arkusz1', 'Arkusz2'].forEach(name => {
    const s = workbook.Sheets[name];
    if (s && s['!ref']) {
        const d = XLSX.utils.sheet_to_json(s, { defval: '' });
        console.log(`\nSheet "${name}": ${d.length} rows, ref=${s['!ref']}`);
        if (d.length > 0) {
            console.log('  Headers:', Object.keys(d[0]).join(', '));
            console.log('  First row:', JSON.stringify(d[0]));
        }
    } else {
        console.log(`\nSheet "${name}": empty or no ref`);
    }
});
