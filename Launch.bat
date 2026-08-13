@echo off
setlocal
cd /d "%~dp0"
REM Modern QoL entry: Albeoris inject + auto-run. Movies enabled (no -M).
REM Pair with dgVoodoo2 DDraw drop-ins in the game root for Win11 windowed/OBS.
".\Launcher\Septerra.exe" run . -r
endlocal
