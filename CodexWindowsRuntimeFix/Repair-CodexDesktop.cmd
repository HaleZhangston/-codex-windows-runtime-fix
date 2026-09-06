@echo off
REM Launch the PowerShell repair script from the same directory.
REM -NoProfile avoids user profile side effects.
REM -ExecutionPolicy Bypass applies only to this one process.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Repair-CodexDesktop.ps1"
