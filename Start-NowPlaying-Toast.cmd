@echo off
setlocal DisableDelayedExpansion
if not exist "%~dp0NowPlayingToast.exe" (
  echo NowPlayingToast.exe not found. Extract the release ZIP or run Publish.cmd first.
  pause
  exit /b 1
)
start "" "%~dp0NowPlayingToast.exe"
if errorlevel 1 (
  echo Could not start Now Playing Toast.
  pause
  exit /b 1
)
exit /b 0
