@echo off
setlocal
cd /d "%~dp0"
title MapMenuPlus - make the Thunderstore zip
echo ==================================================
echo   MapMenuPlus - make the Thunderstore zip
echo ==================================================
echo.

rem The finished mod file: BUILD.bat put it into BONELAB\Mods
set "DLL="
for %%D in ("C:\steam\steamapps\common\BONELAB" "C:\Program Files (x86)\Steam\steamapps\common\BONELAB" "C:\Program Files\Steam\steamapps\common\BONELAB" "D:\SteamLibrary\steamapps\common\BONELAB" "E:\SteamLibrary\steamapps\common\BONELAB" "D:\Steam\steamapps\common\BONELAB") do (
  if exist "%%~D\Mods\MapMenuPlus.dll" if not defined DLL set "DLL=%%~D\Mods\MapMenuPlus.dll"
)
if defined DLL goto havedll

echo I could not find MapMenuPlus.dll in your BONELAB\Mods folder.
echo Type the full path of your BONELAB folder (the one that contains the Mods folder).
echo Example: C:\steam\steamapps\common\BONELAB
set "BL="
set /p "BL=Path: "
set "BL=%BL:"=%"
if exist "%BL%\Mods\MapMenuPlus.dll" set "DLL=%BL%\Mods\MapMenuPlus.dll"
if not defined DLL goto nodll

:havedll
echo Using the mod file: "%DLL%"
echo.
for %%F in (manifest.json icon.png README.md CHANGELOG.md) do (
  if not exist "%%F" (
    echo %%F is missing next to this file. Extract the whole zip again.
    goto fail
  )
)

set "STAGE=%~dp0package"
set "ZIP=%~dp0MapMenuPlus-1.0.0.zip"
if exist "%STAGE%" rmdir /s /q "%STAGE%"
mkdir "%STAGE%\Mods"
copy /y manifest.json "%STAGE%\" >nul
copy /y icon.png "%STAGE%\" >nul
copy /y README.md "%STAGE%\" >nul
copy /y CHANGELOG.md "%STAGE%\" >nul
copy /y "%DLL%" "%STAGE%\Mods\MapMenuPlus.dll" >nul
if exist "%ZIP%" del /q "%ZIP%"

rem The files go IN the zip (not a folder around them) - that is what Thunderstore checks.
powershell -NoProfile -ExecutionPolicy Bypass -Command "Add-Type -AssemblyName System.IO.Compression.FileSystem; [System.IO.Compression.ZipFile]::CreateFromDirectory($env:STAGE, $env:ZIP)"
if not exist "%ZIP%" goto zipfail
rmdir /s /q "%STAGE%"

echo ==================================================
echo   SUCCESS - MapMenuPlus-1.0.0.zip is ready.
echo   A window opens with the file selected.
echo   Upload THAT file on Thunderstore.
echo ==================================================
echo.
start "" explorer /select,"%ZIP%"
pause
exit /b 0

:nodll
echo.
echo Still no MapMenuPlus.dll there. Build the mod first: double-click BUILD.bat in the
echo MapMenuPlus folder (the one with the .cs files), then run this file again.
goto fail

:zipfail
echo.
echo Making the zip failed. Select all the text in this window (or take a screenshot) and send it to Claude.
goto fail

:fail
echo.
pause
exit /b 1
