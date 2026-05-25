@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion
title ZPSP Deploy Release

REM ============================================================
REM  ZPSP DEPLOY - build Release + upload na QNAP
REM  Uzycie: kliknij dwukrotnie. Wszystko dzieje sie automatycznie.
REM ============================================================

REM === KONFIGURACJA - ustaw swoje sciezki ===
set REPO=C:\Users\PC\source\repos\Grafpl\Kalendarz1
set RELEASE_DIR=C:\TEMP\ZPSP-Release
set QNAP_ROOT=\\192.168.0.170\Install\Kalendarz1L
set QNAP_RELEASE=%QNAP_ROOT%\Release
set QNAP_LAUNCHER=%QNAP_ROOT%\Launcher

REM Stempel czasu YYYY-MM-DD_HHMM (PowerShell - niezalezne od locale)
for /f "usebackq tokens=*" %%i in (`powershell -NoProfile -Command "Get-Date -Format 'yyyy-MM-dd_HHmm'"`) do set TS=%%i

echo ============================================================
echo            ZPSP DEPLOY RELEASE - %TS%
echo ============================================================
echo  Repo:     %REPO%
echo  Release:  %RELEASE_DIR%
echo  QNAP:     %QNAP_RELEASE%
echo ============================================================
echo.

REM === [1/6] Test polaczenia z QNAP ===
echo [1/6] Test polaczenia z QNAP...
if not exist "%QNAP_ROOT%" (
    echo BLAD: Nie mozna polaczyc sie z %QNAP_ROOT%
    echo Sprawdz czy jestes w sieci firmowej i czy QNAP jest dostepny.
    pause
    exit /b 1
)
echo   OK
echo.

REM === [2/6] Backup WYLACZONY (na zyczenie) ===
echo [2/6] Backup pominiety (wylaczony)
echo.

REM === [3/6] Czyszczenie folderu Release lokalnie ===
echo [3/6] Czyszczenie tymczasowego folderu Release...
if exist "%RELEASE_DIR%" rmdir /S /Q "%RELEASE_DIR%"
mkdir "%RELEASE_DIR%"
echo   OK
echo.

REM === [4/6] Build Release ===
REM WAZNE: uzywamy 'dotnet build' (NIE 'publish -r win-x64') zeby output byl
REM IDENTYCZNY z tym co Visual Studio produkuje w bin\Release. publish -r tworzy
REM inny layout (runtimes\, RID-specific deps.json) ktory psul DevExpress/GMap/LibVLC
REM (Avilog, Wstawienia nie ladowaly danych). Teraz launcher dostaje 1:1 to co dziala w VS.
echo [4/6] Build Release (to moze potrwac 2-3 min)...
cd /D "%REPO%"
set BIN_RELEASE=%REPO%\bin\Release\net8.0-windows7.0
dotnet build Kalendarz1.csproj -c Release -nologo -v:q
if errorlevel 1 (
    echo BLAD BUILDU - przerywam.
    pause
    exit /b 1
)
if not exist "%BIN_RELEASE%\Kalendarz1.exe" (
    echo BLAD: Nie znaleziono %BIN_RELEASE%\Kalendarz1.exe po buildzie.
    pause
    exit /b 1
)
echo   OK
echo.

REM === [5/6] Kopiowanie bin\Release do RELEASE_DIR + czyszczenie + launcher ===
echo [5/6] Kopiowanie bin\Release + czyszczenie...
REM Kopiuj DOKLADNIE to co VS produkuje (cala zawartosc bin\Release)
robocopy "%BIN_RELEASE%" "%RELEASE_DIR%" /E /MT:8 /R:2 /W:3 /NP /NFL /NDL /NJH /NJS >nul

REM Usuwamy TYLKO debug symbole + git/dev artifacts. NIE ruszamy *.xml
REM (DevExpress i inne biblioteki maja pliki .xml ktore moga byc potrzebne).
del /Q "%RELEASE_DIR%\*.pdb" 2>nul
del /Q "%RELEASE_DIR%\appsettings.Development.json" 2>nul
del /Q "%RELEASE_DIR%\.gitignore" 2>nul
del /Q "%RELEASE_DIR%\.gitattributes" 2>nul
if exist "%RELEASE_DIR%\BAZA_WIEDZY" rmdir /S /Q "%RELEASE_DIR%\BAZA_WIEDZY"
if exist "%RELEASE_DIR%\SQL" rmdir /S /Q "%RELEASE_DIR%\SQL"
if exist "%RELEASE_DIR%\DEPLOY" rmdir /S /Q "%RELEASE_DIR%\DEPLOY"
if exist "%RELEASE_DIR%\Screeny" rmdir /S /Q "%RELEASE_DIR%\Screeny"

REM Self-update launchera: kopiuj aktualnego launchera z QNAP do Release jako __ZPSP_NEW.exe
REM Stary launcher u pracownika kopiuje to do swojego folderu - wymienia sam siebie batch-em
if exist "%QNAP_LAUNCHER%\ZPSP.exe" (
    copy /Y "%QNAP_LAUNCHER%\ZPSP.exe" "%RELEASE_DIR%\__ZPSP_NEW.exe" >nul
    echo   Self-update: launcher dolaczony do Release
) else (
    echo   OSTRZEZENIE: brak launchera na QNAP - self-update pominiety
)
echo   OK
echo.

REM === [6/6] Upload na QNAP (robocopy multi-thread + progress) ===
echo [6/6] Upload na QNAP (robocopy, multi-thread)...

REM Pokaz rozmiar do wgrania
for /f "tokens=3" %%S in ('dir "%RELEASE_DIR%" /-c /s ^| findstr /C:"plik"') do set UPLOAD_SIZE=%%S
echo   Do wgrania: %UPLOAD_SIZE% bajtow

if not exist "%QNAP_RELEASE%" mkdir "%QNAP_RELEASE%"

REM /MIR = mirror (synchronizuje source na destination, usuwa zbedne)
REM /MT:8 = 8 watkow rownoleglych (2-5x szybsze niz xcopy)
REM /R:3 = 3 retry na blad sieci
REM /W:5 = 5 sek miedzy retries
REM /NP = bez procentow per plik (mniej spamu)
REM /NDL = bez listy folderow (mniej spamu)
robocopy "%RELEASE_DIR%" "%QNAP_RELEASE%" /MIR /MT:8 /R:3 /W:5 /NP /NDL
if errorlevel 8 (
    echo BLAD UPLOADU - sprawdz uprawnienia na QNAP lub polaczenie VPN
    pause
    exit /b 1
)

REM Zapisz znacznik wersji (uzyteczny dla launchera)
echo Deploy: %TS% > "%QNAP_RELEASE%\VERSION.txt"
echo Source: %COMPUTERNAME% (%USERNAME%) >> "%QNAP_RELEASE%\VERSION.txt"

echo   OK
echo.

REM === Statystyki ===
echo ============================================================
echo                   DEPLOY ZAKONCZONY POMYSLNIE
echo ============================================================
for /f "tokens=3" %%a in ('dir /-c "%QNAP_RELEASE%" ^| findstr /C:"plik"') do set SIZE=%%a
echo  Wgrana wersja: %TS%
echo  Lokalizacja:   %QNAP_RELEASE%
echo.
echo  Userzy widza nowa wersje przy nastepnym uruchomieniu.
echo ============================================================
echo.
pause
