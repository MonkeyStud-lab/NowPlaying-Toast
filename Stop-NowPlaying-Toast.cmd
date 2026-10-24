@echo off
setlocal DisableDelayedExpansion
if not exist "%~dp0NowPlayingToast.exe" (
  echo NowPlayingToast.exe not found. Use Exit from the running application's tray menu.
  exit /b 1
)

start "" /wait "%~dp0NowPlayingToast.exe" --exit
if errorlevel 1 (
  echo Now Playing Toast did not stop. Use Exit from its tray menu and try again.
  exit /b 1
)
echo Stopped, or already not running.
exit /b 0
