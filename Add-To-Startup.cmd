@echo off
setlocal DisableDelayedExpansion
if not exist "%~dp0NowPlayingToast.exe" (
  echo NowPlayingToast.exe not found. Extract the release ZIP or run Publish.cmd first.
  pause
  exit /b 1
)

rem Pass the folder as data so spaces, apostrophes, and shell characters stay literal.
set "NOWPLAYING_TOAST_HOME=%~dp0"
powershell.exe -NoLogo -NoProfile -NonInteractive -Command ^
  "$ErrorActionPreference = 'Stop';" ^
  "$exe = Join-Path $env:NOWPLAYING_TOAST_HOME 'NowPlayingToast.exe';" ^
  "$startup = [Environment]::GetFolderPath('Startup');" ^
  "if ([string]::IsNullOrWhiteSpace($startup)) { throw 'Windows did not return a Startup folder.' };" ^
  "$null = New-Item -ItemType Directory -Path $startup -Force;" ^
  "$link = Join-Path $startup 'NowPlaying-Toast.lnk';" ^
  "$shell = New-Object -ComObject WScript.Shell;" ^
  "$shortcut = $shell.CreateShortcut($link);" ^
  "$shortcut.TargetPath = $exe;" ^
  "$shortcut.WorkingDirectory = $env:NOWPLAYING_TOAST_HOME;" ^
  "$shortcut.Description = 'Apple Music track notifications';" ^
  "$shortcut.Save();" ^
  "if (-not (Test-Path -LiteralPath $link -PathType Leaf)) { throw 'The Startup shortcut was not created.' };" ^
  "$legacy = Join-Path $startup 'NowPlaying-Toast.cmd';" ^
  "if (Test-Path -LiteralPath $legacy) { Remove-Item -LiteralPath $legacy -Force };"
if errorlevel 1 (
  echo Could not finish Startup setup. Review the error above and try again.
  pause
  exit /b 1
)

echo Added to Startup. Toast waits quietly until Apple Music opens.
echo Keep the application in this folder, or rerun this script after moving it.
pause
exit /b 0
