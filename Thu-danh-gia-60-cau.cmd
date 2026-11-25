@echo off
cd /d "%~dp0"
powershell.exe -NoExit -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\test-qwen-expanded.ps1" -CorpusStudy
pause
