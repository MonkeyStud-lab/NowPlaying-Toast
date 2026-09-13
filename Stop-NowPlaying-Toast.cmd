@echo off
taskkill /IM NowPlayingToast.exe /F >nul 2>&1
powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \"Name='powershell.exe'\" | Where-Object { $_.CommandLine -match 'Watch-AppleMusic\.ps1|NowPlaying-Toast' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }"
echo Stopped toast and Apple Music watcher.
pause
