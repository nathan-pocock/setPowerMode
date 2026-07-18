@echo off
setlocal

if "%~1"=="" goto menu

if /i "%~1"=="low" goto low
if /i "%~1"=="balanced" goto balanced
if /i "%~1"=="performance" goto performance

echo Invalid argument: %~1
echo Usage: %~n0 [low^|balanced^|performance]
exit /b 1

:menu
echo ============================
echo   Set Power Mode
echo ============================
echo 1. Low
echo 2. Balanced
echo 3. Performance
echo 4. Exit
echo.
set "choice="
set /p "choice=Select an option (1-4): "

if "%choice%"=="1" goto low
if "%choice%"=="2" goto balanced
if "%choice%"=="3" goto performance
if "%choice%"=="4" goto end

echo Invalid choice.
echo.
goto menu

:low
powercfg.exe /setactive a1841308-3541-4fab-bc81-f71556f20b4a
echo Power mode set to Low.
goto end

:balanced
powercfg.exe /setactive 381b4222-f694-41f0-9685-ff5bb260df2e
echo Power mode set to Balanced.
goto end

:performance
powercfg.exe /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c
echo Power mode set to Performance.
goto end

:end
endlocal
