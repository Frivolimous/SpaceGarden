@echo off
rem Space Garden prototype: one menu for the everyday commands. Double-click it, or run "sg" from this folder.
rem "sg 2" runs menu option 2 straight away.
setlocal EnableExtensions
cd /d "%~dp0"
if "%GODOT%"=="" set "GODOT=C:\Tools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe"

if not "%~1"=="" (
  set "PICK=%~1"
  call :run
  exit /b
)

:menu
cls
echo.
echo   Space Garden (Godot prototype)
echo   ------------------------------
echo    1  Play: your garden (fast growth, 60x)
echo    2  Play: your garden at the real pace (1x)
echo    3  Play: a new garden, very fast (3600x, its own save)
echo    4  Play: pretend a day has passed (real pace)
echo    5  Open in the Godot editor
echo.
echo    0  Quit
echo.
set "PICK="
set /p "PICK=  Choose: "
if "%PICK%"=="0" exit /b
if /i "%PICK%"=="q" exit /b
call :run
goto menu

:run
if not exist "%GODOT%" (
  echo   Godot not found at %GODOT% ^(set GODOT to its path^)
  pause
  exit /b 1
)
echo   Building...
dotnet build -v q -nologo || (pause & exit /b 1)
if "%PICK%"=="1" start "Space Garden" "%GODOT%" --path game
if "%PICK%"=="2" start "Space Garden" "%GODOT%" --path game -- --grow=1
if "%PICK%"=="3" start "Space Garden" "%GODOT%" --path game -- --fresh --grow=3600 --profile=fast
if "%PICK%"=="4" start "Space Garden" "%GODOT%" --path game -- --grow=1 --away=24
if "%PICK%"=="5" start "Space Garden" "%GODOT%" --editor --path game
exit /b
