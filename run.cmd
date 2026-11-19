@echo off
setlocal enabledelayedexpansion

rem ============================================================================
rem AiteBar - Script to build and launch the application for testing
rem ============================================================================

cd /d "%~dp0"

set "CONFIG=Debug"
set "NO_BUILD=0"

rem Parse command-line arguments
:parse_args
if "%~1"=="" goto after_args
if /i "%~1"=="Release" (
    set "CONFIG=Release"
    shift
    goto parse_args
)
if /i "%~1"=="Debug" (
    set "CONFIG=Debug"
    shift
    goto parse_args
)
if /i "%~1"=="--no-build" (
    set "NO_BUILD=1"
    shift
    goto parse_args
)
if /i "%~1"=="--quick" (
    set "NO_BUILD=1"
    shift
    goto parse_args
)
if /i "%~1"=="-h" goto show_help
if /i "%~1"=="--help" goto show_help
if /i "%~1"=="/?" goto show_help
shift
goto parse_args

:after_args
echo ========================================================
echo  AiteBar Test Launcher [Configuration: %CONFIG%]
echo ========================================================

rem 1. Check for dotnet SDK
where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] dotnet CLI was not found in PATH.
    echo Please install .NET SDK 10 or add it to PATH.
    pause
    exit /b 1
)

rem 2. Build project if needed
if "%NO_BUILD%"=="1" (
    echo [*] Skipping build -- existing binary will be used.
) else (
    echo [*] Building AiteBar [%CONFIG%]...
    dotnet build ".\AiteBar\AiteBar.csproj" -c %CONFIG% -m:1 -p:UseSharedCompilation=false
    if errorlevel 1 (
        echo.
        echo [ERROR] Build failed!
        echo If AiteBar is already running, please exit the app from the tray and try again.
        pause
        exit /b 1
    )
)

rem 3. Check executable path
set "EXE_PATH=.\AiteBar\bin\%CONFIG%\net10.0-windows\win-x64\AiteBar.exe"
if not exist "%EXE_PATH%" (
    echo.
    echo [ERROR] Executable not found at %EXE_PATH%
    echo Run without --no-build to compile first.
    pause
    exit /b 1
)

rem 4. Launch application
echo [*] Launching %EXE_PATH%...
start "" "%EXE_PATH%"
echo [OK] AiteBar launched successfully.
exit /b 0

:show_help
echo Usage: run.cmd [Debug^|Release] [--no-build^|--quick]
echo.
echo Options:
echo   Debug       Build and run in Debug configuration (default).
echo   Release     Build and run in Release configuration.
echo   --no-build  Launch existing executable without rebuilding.
echo   --quick     Alias for --no-build.
echo.
exit /b 0
