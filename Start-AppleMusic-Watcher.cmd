@echo off
cd /d "%~dp0"
REM Single instance: skip if watcher already running
powershell -NoProfile -WindowStyle Hidden -Command ^
  "if (Get-CimInstance Win32_Process -Filter \"Name='powershell.exe'\" | Where-Object { $_.CommandLine -match 'Watch-AppleMusic\.ps1' }) { exit 0 }; Start-Process -FilePath powershell.exe -ArgumentList '-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"\"%~dp0Watch-AppleMusic.ps1\"\"' -WindowStyle Hidden"
