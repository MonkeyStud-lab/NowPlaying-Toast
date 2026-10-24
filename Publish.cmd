@echo off
setlocal DisableDelayedExpansion
pushd "%~dp0"
if errorlevel 1 (
  echo Could not open the project folder.
  exit /b 1
)

echo Publishing NowPlayingToast with .NET 10...
dotnet publish src\NowPlayingToast.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
if errorlevel 1 (
  echo Publish failed. Check the output above and make sure the .NET 10 SDK is installed.
  popd
  exit /b 1
)

copy /Y "publish\NowPlayingToast.exe" "NowPlayingToast.exe" >nul
if errorlevel 1 (
  echo The build succeeded, but the executable could not be copied to the project folder.
  echo Close Now Playing Toast with Stop-NowPlaying-Toast.cmd, then run Publish.cmd again.
  popd
  exit /b 1
)

echo Published to publish\NowPlayingToast.exe
echo Copied to "%~dp0NowPlayingToast.exe"
popd
exit /b 0
