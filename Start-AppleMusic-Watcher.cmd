@echo off
cd /d "%~dp0"
powershell -NoProfile -WindowStyle Hidden -Command "if (Get-CimInstance Win32_Process -Filter \"Name='powershell.exe'\" | Where-Object { $_.CommandLine -like '*Watch-AppleMusic.ps1*' }) { exit 0 }; Start-Process powershell -ArgumentList '-NoProfile','-WindowStyle','Hidden','-ExecutionPolicy','Bypass','-File','\"%~dp0Watch-AppleMusic.ps1\"' -WindowStyle Hidden"
