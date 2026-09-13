@echo off
cd /d "%~dp0"
if not exist "%~dp0NowPlayingToast.exe" (
  echo NowPlayingToast.exe not found. Run Publish.cmd first.
  pause
  exit /b 1
)
start "" "%~dp0NowPlayingToast.exe"
