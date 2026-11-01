@echo off
setlocal
cd /d "%~dp0"
title MapMenuPlus - get the log files for Claude
echo ==================================================
echo   MapMenuPlus - get the log files
echo ==================================================
echo.

set "BL=C:\steam\steamapps\common\BONELAB"
if exist "%BL%\Mods" goto havepath

echo I could not find BONELAB at:
echo   %BL%
echo.
echo Type the full path of your BONELAB folder (the one that contains the Mods and MelonLoader folders).
echo Example: C:\steam\steamapps\common\BONELAB
set "BL="
set /p "BL=Path: "
set "BL=%BL:"=%"
if not exist "%BL%\Mods" goto nopath

:havepath
set "OUT=%~dp0logs_for_claude"
if not exist "%OUT%" mkdir "%OUT%"
del /q "%OUT%\*" >nul 2>nul

set "FOUND=0"
if exist "%BL%\UserData\MapMenuPlus\log.txt" (
  copy /y "%BL%\UserData\MapMenuPlus\log.txt" "%OUT%\mapmenu_log.txt" >nul
  set "FOUND=1"
)
if exist "%BL%\UserData\MapMenuPlus\modio\ids.txt" copy /y "%BL%\UserData\MapMenuPlus\modio\ids.txt" "%OUT%\mapmenu_modio_ids.txt" >nul

if exist "%BL%\MelonLoader\Latest.log" copy /y "%BL%\MelonLoader\Latest.log" "%OUT%\Latest.log" >nul

if "%FOUND%"=="0" goto nolog

echo Done. A folder opens now with the files.
echo Drag ALL the files from that folder into the chat with Claude.
echo.
start "" explorer "%OUT%"
pause
exit /b 0

:nolog
echo There is no log.txt yet.
echo Start BONELAB once with the mod installed, open the menu, choose LEVELS, click a few maps,
echo quit the game, then double-click GET_LOG.bat again.
goto fail

:nopath
echo.
echo Still cannot find the BONELAB folder there.
goto fail

:fail
echo.
pause
exit /b 1
