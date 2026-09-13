@echo off
set TARGET=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\NowPlaying-Toast.cmd
echo @echo off> "%TARGET%"
echo cd /d "%~dp0">> "%TARGET%"
echo call "%~dp0Start-AppleMusic-Watcher.cmd">> "%TARGET%"
echo Added to Startup: watcher starts the toast only when Apple Music is running.
pause
