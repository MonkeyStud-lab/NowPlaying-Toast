# Now Playing Toast

Windows tray toast for **Apple Music** on Windows 10/11.

When the track changes, a clean card pops up with album art, title, artist, and an Apple Music red outline. Click the toast to focus Apple Music.

## Features

- Event-based SMTC (no busy polling)
- Apple Music only - other media sessions are ignored even if Apple Music is open
- Single-instance tray app
- Settings: hold duration, corner, size, theme (Dark/Light), monitor, sound, activate toast, update check
- Click toast to bring Apple Music to the foreground
- DPI-aware multi-monitor placement
- Optional GitHub update notification on startup

## Requirements

- Windows 10/11
- For development: .NET 6 SDK
- Published builds are self-contained (no runtime install needed)

## Build (dev)

```bat
cd src
dotnet build -c Release
```

## Publish (single-file)

```bat
Publish.cmd
```

This runs:

```bat
dotnet publish src -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Copy `publish\NowPlayingToast.exe` to the project folder (or use the copy step in Publish.cmd).

## Run

- `Start-NowPlaying-Toast.cmd` - start (waits quietly until Apple Music opens)
- `Stop-NowPlaying-Toast.cmd` - quit
- `Add-To-Startup.cmd` - launch with Windows

Tray menu: **Show current track**, **Settings**, **About**, **Exit**.

## Settings

Stored at `%AppData%\NowPlayingToast\settings.json`:

| Setting | Default | Notes |
| --- | --- | --- |
| Hold duration | 4.2 s | How long the toast stays visible |
| Corner | BottomRight | BottomRight / BottomLeft / TopRight / TopLeft |
| Size | Normal | Small / Normal / Large |
| Theme | Dark | Dark / Light (red accent kept) |
| Monitor | Primary | Primary or a specific screen index |
| Show on Apple Music activate | true | Toast once when Music opens |
| Play sound on toast | false | Short system sound |
| Check for updates on startup | true | GitHub Releases notify only |

Changes apply without a full restart where practical.

## Version

1.1.0 - https://github.com/MonkeyStud-lab/NowPlaying-Toast
