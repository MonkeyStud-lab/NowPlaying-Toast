# Now Playing Toast

Windows desktop toast for Apple Music (and other SMTC media apps).

When the track changes, a clean dark card pops in the bottom-right with album art, title, artist, and an **Apple Music red** outline.

## Requirements

- Windows 10/11
- .NET 6 Desktop Runtime (or build with the .NET 6 SDK)

## Build

```bat
cd src
dotnet build -c Release
```

Copy the output next to the `.cmd` launchers, or run:

```bat
Start-NowPlaying-Toast.cmd
```

## Run

- `Start-NowPlaying-Toast.cmd` — start
- `Stop-NowPlaying-Toast.cmd` — quit
- `Add-To-Startup.cmd` — launch with Windows

Tray icon → **Show current track** to preview. On launch it shows the current song once so you can confirm it’s working.
