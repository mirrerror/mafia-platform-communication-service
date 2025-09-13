@echo off
setlocal

:: ==============================================================================
:: Launch Script for Mafia Communication Service (Windows)
:: ------------------------------------------------------------------------------
:: This script builds and runs the .NET application.
:: It should be executed from the root directory of the project by double-clicking
:: or running from a command prompt.
:: ==============================================================================

:: Define the relative path to your main project directory
set "PROJECT_DIR=MafiaCommunicationService"

:: Check if the project directory exists
if not exist "%PROJECT_DIR%" (
    echo Error: Project directory '%PROJECT_DIR%' not found.
    echo Please run this script from the root of your solution.
    pause
    exit /b 1
)

:: Print a message to the console
echo "Launching Mafia Communication Service..."

:: Navigate into the project directory and run the app.
:: The 'dotnet run' command will automatically restore, build, and run the project.
:: All arguments passed to this script are forwarded to 'dotnet run'.
pushd "%PROJECT_DIR%"
dotnet run %*
popd

echo "Application has been shut down."
pause