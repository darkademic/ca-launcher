@echo off
echo Building Combined Arms Launcher...
dotnet build
if %ERRORLEVEL% neq 0 (
    echo Build failed!
    pause
    exit /b %ERRORLEVEL%
)

echo Build successful!
echo.
echo Starting launcher...
dotnet run
