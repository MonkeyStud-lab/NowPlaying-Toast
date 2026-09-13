# Now Playing Toast

Windows desktop toast for Apple Music (and other SMTC media apps).

When the track changes, a clean dark card pops in the bottom-right with album art, title, artist, and an **Apple Music red** outline.

## How it works

Uses Windows System Media Transport Controls (SMTC) **session events** (`CurrentSessionChanged` / `MediaPropertiesChanged`) so it only wakes when the track changes - no busy polling of Now Playing Session Manager.

Album art is decoded only on a real track change (SMTC thumbnail, with an iTunes lookup fallback).

The app can sit in the tray waiting. It turns on media tracking only while **Apple Music** is running, and goes idle again when Apple Music closes (no separate watcher process).

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

- `Start-NowPlaying-Toast.cmd` - start (waits for Apple Music if it is not open yet)
- `Stop-NowPlaying-Toast.cmd` - quit
- `Add-To-Startup.cmd` - launch with Windows

Tray icon: **Show current track** to preview. When Apple Music opens it shows the current song once so you can confirm it is working.
