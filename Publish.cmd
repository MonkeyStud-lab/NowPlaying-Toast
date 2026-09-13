@echo off
cd /d "%~dp0"
echo Publishing NowPlayingToast (self-contained single-file)...
dotnet publish src -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
if errorlevel 1 (
  echo Publish failed.
  exit /b 1
)
copy /Y "publish\NowPlayingToast.exe" "%~dp0NowPlayingToast.exe" >nul
echo.
echo Published to publish\NowPlayingToast.exe
echo Copied to %~dp0NowPlayingToast.exe
