# =============================================================================
# analiza_maja_export.ps1
# Uruchamia analiza_maja.sql i zapisuje wynik do CSV / TXT, gotowy do wklejenia.
#
# Wymagania:
#   - sqlcmd.exe w PATH (instalowany razem z SSMS)
#   - dostęp do 192.168.0.109 (LibraNet)
#   - linked server [192.168.0.112] -> Handel skonfigurowany na 192.168.0.109
#
# Użycie (w PowerShell):
#   cd "C:\Users\PC\source\repos\Grafpl\Kalendarz1\BAZA_WIEDZY\SELECTY"
#   .\analiza_maja_export.ps1
#   # → wynik w pliku analiza_maja_wynik_YYYY-MM-DD_HHmm.txt
# =============================================================================

$ErrorActionPreference = 'Stop'

$ScriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$SqlFile     = Join-Path $ScriptDir 'analiza_maja.sql'
$Timestamp   = Get-Date -Format 'yyyy-MM-dd_HHmm'
$OutTxt      = Join-Path $ScriptDir "analiza_maja_wynik_$Timestamp.txt"
$OutMd       = Join-Path $ScriptDir "analiza_maja_wynik_$Timestamp.md"

if (-not (Test-Path $SqlFile)) {
    Write-Host "❌ Nie znaleziono pliku $SqlFile" -ForegroundColor Red
    exit 1
}

# Sprawdzenie sqlcmd
$sqlcmd = Get-Command sqlcmd.exe -ErrorAction SilentlyContinue
if (-not $sqlcmd) {
    Write-Host "❌ Nie znaleziono sqlcmd.exe w PATH. Zainstaluj SQL Server Command Line Utilities lub SSMS." -ForegroundColor Red
    exit 1
}

Write-Host "▶ Uruchamiam analiza_maja.sql na 192.168.0.109/LibraNet ..." -ForegroundColor Cyan
Write-Host "  Wynik: $OutTxt"

# -W trim trailing whitespace, -s separator, -h -1 brak headeru per resultset (mamy nagłówki własne)
# -y 0 -Y 0 = unlimited column width
& sqlcmd -S '192.168.0.109' -U 'pronova' -P 'pronova' -d 'LibraNet' `
         -i $SqlFile -o $OutTxt `
         -s '|' -W -y 0 -Y 0

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ sqlcmd zwrócił kod $LASTEXITCODE — sprawdź $OutTxt" -ForegroundColor Red
    exit $LASTEXITCODE
}

$Size = (Get-Item $OutTxt).Length
Write-Host "✅ Wygenerowano $OutTxt ($([math]::Round($Size/1024,1)) KB)" -ForegroundColor Green

# Konwersja do markdown (każdy raport = osobna sekcja ## )
Write-Host "▶ Konwertuję do Markdown..." -ForegroundColor Cyan

$content = Get-Content $OutTxt -Raw
$mdLines = @()
$mdLines += "# Analiza Maja — wynik wygenerowany $Timestamp"
$mdLines += ""
$mdLines += "Plik źródłowy: ``analiza_maja.sql``"
$mdLines += ""

# Każdy resultset zaczyna się od linii "X.Y — opis" w pierwszej kolumnie pojedynczej
# (z naszego SELECT N'X.Y — opis' AS [Raport])
$rows = $content -split "`r?`n"
$inTable = $false
$first = $true

foreach ($line in $rows) {
    if ($line -match '^\s*([A-K0]\.\d[a-z]?)\s*[—-]\s*(.+?)\s*$') {
        # nagłówek raportu
        if (-not $first) { $mdLines += '' }
        $mdLines += "## $($matches[1]) — $($matches[2])"
        $mdLines += ''
        $mdLines += '```'
        $inTable = $true
        $first = $false
    }
    elseif ($line -match '^\s*Raport\s*$' -or $line -match '^-+\s*$') {
        # ignoruj nagłówki kolumn 'Raport' i separatory --- które tworzy sqlcmd
        continue
    }
    elseif ($inTable -and $line.Trim() -eq '') {
        if ($mdLines[-1] -ne '```') { $mdLines += '```' }
        $inTable = $false
    }
    elseif ($inTable) {
        $mdLines += $line
    }
}
if ($inTable -and $mdLines[-1] -ne '```') { $mdLines += '```' }

$mdLines | Out-File -FilePath $OutMd -Encoding utf8
Write-Host "✅ Wygenerowano $OutMd" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Wklej zawartość $OutMd do Claude w przeglądarce." -ForegroundColor Yellow
Write-Host "   (Priorytet: sekcja K.1 — scorecard wszystkich handlowców)" -ForegroundColor Yellow
