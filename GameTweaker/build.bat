@echo off
dotnet build -c Release
if errorlevel 1 (
    echo.
    echo Build failed!
    pause
    exit /b 1
)

set "SOURCE=bin\Release\netstandard2.1\GameTweaker.dll"
set "DEST=..\BepInEx\plugins\GameTweaker.dll"

if not exist "..\BepInEx\plugins" mkdir "..\BepInEx\plugins"
copy /Y "%SOURCE%" "%DEST%" >nul

echo Copied to: %DEST%
pause
