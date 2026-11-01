@echo off
setlocal
cd /d "%~dp0"
title MapMenuPlus - build and install
echo ==================================================
echo   MapMenuPlus - build and install
echo ==================================================
echo.

tasklist /FI "IMAGENAME eq BONELAB_Steam_Windows64.exe" 2>nul | find /I "BONELAB_Steam_Windows64.exe" >nul
if not errorlevel 1 goto gameopen

if "%BONELAB_DIR%"=="" set "BONELAB_DIR=C:\steam\steamapps\common\BONELAB"

if exist "%BONELAB_DIR%\MelonLoader\Il2CppAssemblies\UnityEngine.CoreModule.dll" goto found

echo I could not find BONELAB at:
echo   %BONELAB_DIR%
echo.
echo Type the full path of your BONELAB folder (the one that contains the Mods and MelonLoader folders).
echo Example: C:\steam\steamapps\common\BONELAB
set "BONELAB_DIR="
set /p "BONELAB_DIR=Path: "
set "BONELAB_DIR=%BONELAB_DIR:"=%"
if exist "%BONELAB_DIR%\MelonLoader\Il2CppAssemblies\UnityEngine.CoreModule.dll" goto found

echo.
echo Still cannot find the game files there.
echo Start BONELAB once (with MelonLoader installed) so it creates MelonLoader\Il2CppAssemblies, then run this again.
goto fail

:found
echo Using BONELAB folder: %BONELAB_DIR%
echo.

if exist "%BONELAB_DIR%\Mods\BoneLib.dll" goto havebonelib
echo BoneLib.dll is missing from the Mods folder. Install BoneLib first (it is a normal BONELAB mod), then run this again.
goto fail

:havebonelib
where dotnet >nul 2>nul
if errorlevel 1 goto nodotnet

echo Building... this takes about 20 seconds.
echo.
dotnet build -c Release
if errorlevel 1 goto buildfail

if not exist "%BONELAB_DIR%\Mods\MapMenuPlus.dll" goto notcopied

echo.
echo ==================================================
echo   SUCCESS - MapMenuPlus 1.0.0 is installed.
echo   Start BONELAB, open the menu (B or Y button)
echo   and choose LEVELS.
echo   (Your other mods stay as they are.)
echo ==================================================
echo.
pause
exit /b 0

:gameopen
echo BONELAB is still running.
echo Close the game completely, then double-click BUILD.bat again.
echo (Windows cannot replace the mod file while the game is open.)
goto fail

:nodotnet
echo The .NET SDK is not installed (or this window was opened before you installed it).
echo Install the .NET 6 SDK, close this window, and double-click BUILD.bat again.
goto fail

:buildfail
echo.
echo ==================================================
echo   BUILD FAILED.
echo   Select ALL the text in this window (or take a screenshot of it)
echo   and send it to Claude.
echo ==================================================
goto fail

:notcopied
echo The build finished but MapMenuPlus.dll did not land in the Mods folder.
echo Look in the bin\Release\net6.0 folder next to this file and copy MapMenuPlus.dll into BONELAB\Mods by hand.
goto fail

:fail
echo.
pause
exit /b 1
