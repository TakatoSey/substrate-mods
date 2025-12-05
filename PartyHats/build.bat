@echo off
dotnet build -c Release -v quiet
if errorlevel 1 (
    echo.
    echo Build failed!
    pause
    exit /b 1
)

set "SOURCE=bin\Release\netstandard2.1\PartyHatsMod.dll"
set "DEST=..\BepInEx\plugins\PartyHatsMod.dll"

if not exist "..\BepInEx\plugins\PartyHatsMod\" mkdir "..\BepInEx\plugins\PartyHatsMod.dll\"
copy /Y "%SOURCE%" "%DEST%" >nul

echo Copied to: %DEST%

pause
