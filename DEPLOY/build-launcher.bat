@echo off
chcp 65001 >nul
title Build ZPSP Launcher

REM ============================================================
REM  Build launchera + upload na QNAP (jedno klikniecie)
REM  Uruchamiaj tylko gdy zmienia sie kod w ZPSP.Launcher\
REM ============================================================

set REPO=C:\Users\PC\source\repos\Grafpl\Kalendarz1
set LAUNCHER_DIR=%REPO%\ZPSP.Launcher
set OUTPUT=C:\TEMP\ZPSP-Launcher-Release
set QNAP_LAUNCHER=\\192.168.0.170\Install\Kalendarz1L\Launcher

echo ============================================================
echo           Build ZPSP Launcher + upload na QNAP
echo ============================================================
echo.

REM [1/3] Czyszczenie
if exist "%OUTPUT%" rmdir /S /Q "%OUTPUT%"
mkdir "%OUTPUT%"

REM [2/3] Build single-file
echo [1/3] Publish single-file...
cd /D "%LAUNCHER_DIR%"
dotnet publish ZPSP.Launcher.csproj -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true -o "%OUTPUT%" -nologo -v:q
if errorlevel 1 (
    echo BLAD BUILDU
    pause
    exit /b 1
)
echo   OK
echo.

REM [3/3] Upload na QNAP
echo [2/3] Upload launchera na QNAP...
if not exist "%QNAP_LAUNCHER%" mkdir "%QNAP_LAUNCHER%"
del /Q "%QNAP_LAUNCHER%\*.*" 2>nul
copy /Y "%OUTPUT%\ZPSP.exe" "%QNAP_LAUNCHER%\ZPSP.exe" >nul
if exist "%OUTPUT%\ZPSP.pdb" del /Q "%OUTPUT%\ZPSP.pdb"
if errorlevel 1 (
    echo BLAD UPLOADU
    pause
    exit /b 1
)
echo   OK
echo.

REM Skopiuj rowniez install-launcher.bat na QNAP (do dystrybucji na stacje)
copy /Y "%REPO%\DEPLOY\install-launcher.bat" "%QNAP_LAUNCHER%\install-launcher.bat" >nul

echo ============================================================
echo                   LAUNCHER WGRANY
echo ============================================================
echo  Pliki w: %QNAP_LAUNCHER%
echo   - ZPSP.exe                  (launcher dla userow)
echo   - install-launcher.bat      (instalacja na stacjach)
echo.
echo  Aby zainstalowac na stacji pracownika:
echo  1. Otworz folder: %QNAP_LAUNCHER%
echo  2. Kliknij dwukrotnie install-launcher.bat
echo ============================================================
echo.
pause
