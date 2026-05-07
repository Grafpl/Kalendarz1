#requires -RunAsAdministrator
# Instalacja Kalendarz1.CnaService jako usługa Windows.
# Wymaga uprawnień administratora.

param(
    [string]$ExePath = "$PSScriptRoot\bin\Release\net8.0-windows7.0\Kalendarz1.CnaService.exe",
    [string]$ServiceName = "ZPSP-CNA",
    [string]$DisplayName = "ZPSP - Centrum nagrań AI",
    [string]$Description = "Indeksacja klatek CCTV i backfill embedingów dla Centrum nagrań AI"
)

if (-not (Test-Path $ExePath)) {
    Write-Error "Nie znaleziono exe: $ExePath. Najpierw zbuduj: dotnet build CnaService\CnaService.csproj -c Release"
    exit 1
}

# Sprawdź czy usługa już istnieje
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Usługa '$ServiceName' już istnieje. Zatrzymuję i usuwam..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "Instaluję usługę '$ServiceName'..."
sc.exe create $ServiceName binPath= "`"$ExePath`"" start= auto DisplayName= "`"$DisplayName`"" | Out-Null
sc.exe description $ServiceName "$Description" | Out-Null

Write-Host "Uruchamiam usługę..."
Start-Service -Name $ServiceName

Write-Host "✓ Usługa '$ServiceName' zainstalowana i uruchomiona." -ForegroundColor Green
Write-Host ""
Write-Host "Zarządzanie:"
Write-Host "  Start:     Start-Service -Name $ServiceName"
Write-Host "  Stop:      Stop-Service -Name $ServiceName"
Write-Host "  Status:    Get-Service -Name $ServiceName"
Write-Host "  Restart:   Restart-Service -Name $ServiceName"
Write-Host "  Logi:      Get-EventLog -LogName Application -Source $ServiceName -Newest 50"
Write-Host "  Odinstaluj: sc.exe delete $ServiceName"
