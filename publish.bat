@echo off
echo Creating release build of Combined Arms Launcher...

set OUTPUT_DIR=publish\win-x64

if exist %OUTPUT_DIR% (
    echo Cleaning previous build...
    rmdir /s /q %OUTPUT_DIR%
)

echo Publishing application...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o %OUTPUT_DIR%

if %ERRORLEVEL% neq 0 (
    echo Publish failed!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Publish successful!
echo Output directory: %OUTPUT_DIR%
echo Executable: %OUTPUT_DIR%\CALauncher.exe
pause
