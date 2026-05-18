@echo off
chcp 65001 >nul
title ZPSP - Instalacja Launchera

REM ============================================================
REM  ZPSP - Instalacja Launchera na stacji uzytkownika
REM
REM  Uzycie: kliknij dwukrotnie na komputerze pracownika.
REM  Tworzy skrot na pulpicie + kopiuje launcher do %LOCALAPPDATA%.
REM ============================================================

set QNAP_LAUNCHER=\\192.168.0.170\Install\Kalendarz1L\Launcher\ZPSP.exe
set LOCAL_LAUNCHER=%LOCALAPPDATA%\ZPSP\ZPSP.exe
set SHORTCUT=%USERPROFILE%\Desktop\ZPSP.lnk

echo ============================================================
echo            ZPSP - Instalacja Launchera
echo ============================================================
echo  Stacja:    %COMPUTERNAME%
echo  Uzytkownik: %USERNAME%
echo  Source:    %QNAP_LAUNCHER%
echo  Target:    %LOCAL_LAUNCHER%
echo ============================================================
echo.

REM [1/3] Sprawdz dostepnosc QNAP
echo [1/3] Test polaczenia z QNAP...
if not exist "%QNAP_LAUNCHER%" (
    echo BLAD: Nie ma launchera na QNAP.
    echo Sprawdz czy Sergiusz wgral wersje.
    pause
    exit /b 1
)
echo   OK
echo.

REM [2/3] Skopiuj launcher do %LOCALAPPDATA%
echo [2/3] Instalowanie launchera lokalnie...
mkdir "%LOCALAPPDATA%\ZPSP" 2>nul
copy /Y "%QNAP_LAUNCHER%" "%LOCAL_LAUNCHER%" >nul
if errorlevel 1 (
    echo BLAD KOPII - przerywam
    pause
    exit /b 1
)
echo   OK
echo.

REM [3/3] Stworz skrot na pulpicie
echo [3/3] Tworzenie skrotu na pulpicie...
powershell -Command "$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('%SHORTCUT%'); $s.TargetPath = '%LOCAL_LAUNCHER%'; $s.WorkingDirectory = '%LOCALAPPDATA%\ZPSP'; $s.Description = 'ZPSP - System Piorkowscy'; $s.IconLocation = '%LOCAL_LAUNCHER%, 0'; $s.Save()"
if errorlevel 1 (
    echo OSTRZEZENIE: Nie udalo sie stworzyc skrotu (mozesz to zrobic recznie)
) else (
    echo   OK: %SHORTCUT%
)
echo.

echo ============================================================
echo                   INSTALACJA UDANA
echo ============================================================
echo  Launcher zainstalowany: %LOCAL_LAUNCHER%
echo  Skrot na pulpicie:      %SHORTCUT%
echo.
echo  Mozesz teraz uruchamiac ZPSP klikajac w skrot na pulpicie.
echo  Aktualizacje beda pobierane automatycznie z dysku sieciowego.
echo ============================================================
echo.
pause
