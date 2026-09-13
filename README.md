# Now Playing Toast

Windows desktop toast for Apple Music (and other SMTC media apps).

When the track changes, a Dr. Strange–style orange portal opens in the bottom-right and reveals the song title, artist, and album art.

## Requirements

- Windows 10/11
- .NET 6 Desktop Runtime (or build with the .NET 6 SDK)

## Build

```bat
cd src
dotnet build -c Release
```

Output: `src\bin\Release\net6.0-windows10.0.19041.0\NowPlayingToast.exe`

## Run

- `Start-NowPlaying-Toast.cmd` — start in background
- `Stop-NowPlaying-Toast.cmd` — quit
- `Add-To-Startup.cmd` — launch with Windows

Tray icon → **Show current track** to preview. On launch it also shows the current song once so you can confirm it’s working.

## Notes

- Reads now-playing from Windows System Media Transport Controls (works with Apple Music for Windows, Spotify, etc.)
- Album art comes from the media session thumbnail, with an iTunes Search API fallback
