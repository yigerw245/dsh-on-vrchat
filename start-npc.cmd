@echo off
setlocal
cd /d "%~dp0"
set "PYTHONUTF8=1"
if exist "%~dp0.venv\Scripts\python.exe" (
  "%~dp0.venv\Scripts\python.exe" npc_bridge.py %*
) else if exist "%~dp0..\live2d_pet\.venv\Scripts\python.exe" (
  "%~dp0..\live2d_pet\.venv\Scripts\python.exe" npc_bridge.py %*
) else (
  python npc_bridge.py %*
)
