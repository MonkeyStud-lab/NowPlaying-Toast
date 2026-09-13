# Now Playing Toast

Windows desktop toast for Apple Music (and other SMTC media apps).

When the track changes, a clean dark card pops in the bottom-right with album art, title, artist, and an **Apple Music red** outline.

## How it works

Uses Windows System Media Transport Controls (SMTC) **session events** (`CurrentSessionChanged` / `MediaPropertiesChanged`) so it only wakes when the track changes - no busy polling of Now Playing Session Manager.

Album art is decoded only on a real track change (SMTC thumbnail, with an iTunes lookup fallback).

By default the **Apple Music watcher** keeps the toast off until Apple Music is running, then starts it, and quits the toast when Apple Music closes.

## Requirements

- Windows 10/11
- .NET 6 Desktop Runtime (or build with the .NET 6 SDK)

## Build

```bat
cd src
dotnet build -c Release
```

Copy the output next to the `.cmd` launchers, or run the watcher / start scripts below.

## Run

- `Start-AppleMusic-Watcher.cmd` - recommended: toast only while Apple Music is open
- `Start-NowPlaying-Toast.cmd` - force-start the toast alone (manual)
- `Stop-NowPlaying-Toast.cmd` - quit toast and watcher
- `Add-To-Startup.cmd` - run the watcher at login (toast still only with Apple Music)

Tray icon: **Show current track** to preview. On launch it shows the current song once so you can confirm it is working.
