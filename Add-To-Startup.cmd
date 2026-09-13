@echo off
set TARGET=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\NowPlaying-Toast.cmd
echo @echo off> "%TARGET%"
echo start "" "%~dp0NowPlayingToast.exe">> "%TARGET%"
echo Added to Startup. Toast waits quietly until Apple Music opens.
pause
