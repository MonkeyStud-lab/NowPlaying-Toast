@echo off
setlocal DisableDelayedExpansion
powershell.exe -NoLogo -NoProfile -NonInteractive -Command ^
  "$ErrorActionPreference = 'Stop';" ^
  "$startup = [Environment]::GetFolderPath('Startup');" ^
  "if ([string]::IsNullOrWhiteSpace($startup)) { throw 'Windows did not return a Startup folder.' };" ^
  "foreach ($name in @('NowPlaying-Toast.lnk', 'NowPlaying-Toast.cmd')) {" ^
  "  $path = Join-Path $startup $name;" ^
  "  if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force };" ^
  "};"
if errorlevel 1 (
  echo Could not remove the Startup entry. Review the error above and try again.
  pause
  exit /b 1
)

echo Removed from Startup. Use Stop-NowPlaying-Toast.cmd to close a running instance.
pause
exit /b 0
