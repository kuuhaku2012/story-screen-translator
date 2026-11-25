@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run-screen-trial.ps1" -LayoutDirectory "%~dp0artifacts\panel-trial\layout"
if errorlevel 1 pause
