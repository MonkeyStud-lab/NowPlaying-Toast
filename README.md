# Now Playing Toast

A Windows tray app for **Apple Music**. When the track changes, a card shows its album art, title, and artist with an Apple Music red outline. Click the card to bring Apple Music forward.

## Install and run

1. Download the portable ZIP from [GitHub Releases](https://github.com/MonkeyStud-lab/NowPlaying-Toast/releases/latest).
2. Extract it to a folder you intend to keep, then run `NowPlayingToast.exe` or `Start-NowPlaying-Toast.cmd`.
3. Open Apple Music and play a song. The app waits quietly in the tray when Apple Music is closed.

The published app is self-contained; you do not need to install .NET to use it. Development builds can also be downloaded from successful **Windows build** runs under the repository's **Actions** tab. Those artifacts expire after seven days and are not published releases.

### Tray and startup controls

The tray menu contains **Show current track**, **Settings**, **About**, and **Exit**. Launching a second copy shows an "Already running" notification instead of creating another instance.

- `Add-To-Startup.cmd` creates a shortcut for the current Windows user. It verifies that the executable exists and replaces the older startup script only after creating the shortcut.
- `Remove-From-Startup.cmd` removes the shortcut and any older startup script. It does not close an app that is already running.
- `Stop-NowPlaying-Toast.cmd` asks the app to exit normally and reports failure if it cannot stop. The same command is available as `NowPlayingToast.exe --exit`.

Keep the executable and scripts together. If you move the folder, rerun `Add-To-Startup.cmd` so Windows uses its new location. To remove the app, remove its startup entry, stop it, and delete the application folder.

## Features

- Track changes use Windows media-session events.
- Track details appear as soon as Windows returns them; album art loads separately. Delayed art cannot replace a newer song's art or reopen a hidden card.
- Only Apple Music sessions produce track notifications.
- Single-instance tray app with optional startup registration.
- No idle process scanning or timer polling for second launches. A five-second retry runs only if Windows' media-session manager cannot initialize.
- Album art and clickable track cards.
- Configurable duration, corner, size, dark/light theme, monitor, and sound.
- DPI-aware placement across multiple monitors.
- Optional GitHub update notification on startup; updates are downloaded manually.

## Requirements

- Windows 10 version 2004 or later, or Windows 11, on an x64 PC.
- The Apple Music desktop app for track notifications.
- For development: the **.NET 10 SDK**. `global.json` selects a stable .NET 10 SDK.

## Settings

Settings are stored at `%AppData%\NowPlayingToast\settings.json`.

| Setting | Default | Notes |
| --- | --- | --- |
| Hold duration | 4.2 s | How long the card stays visible |
| Corner | BottomRight | BottomRight / BottomLeft / TopRight / TopLeft |
| Size | Normal | Small / Normal / Large |
| Theme | Dark | Dark / Light; both keep the red accent |
| Monitor | Primary | Primary or a specific screen index |
| Show on Apple Music activate | true | Show the current track when Apple Music opens |
| Play sound on toast | false | Short system sound |
| Check for updates on startup | true | Notify about newer GitHub Releases |

Changes apply without restarting where practical.

If settings are unreadable or malformed, opening the app uses defaults and leaves the original file intact. Saving validates the values and writes a complete temporary file before replacing `settings.json`; if saving fails, the dialog reports the error and stays open.

## Build and test

From the repository root:

```bat
dotnet build src\NowPlayingToast.csproj -c Release
dotnet test tests\NowPlayingToast.Tests.csproj -c Release
```

The GitHub Actions workflow builds and tests on Windows, verifies self-contained publishing, and smoke-tests startup, single-instance behavior, and graceful shutdown. It packages the executable with the run/startup scripts and does not publish releases.

Media-session integration, tray behavior, focus, and monitor placement also require a manual Windows check with Apple Music. An automated build does not exercise a real playback session.

## Publish a portable executable

From the repository root:

```bat
Publish.cmd
```

This produces a self-contained, compressed single-file x64 application in `publish\` and copies the executable beside the helper scripts. If the old executable is running and cannot be replaced, the script reports that failure; stop the app and rerun the command.

The equivalent publish command is:

```bat
dotnet publish src\NowPlayingToast.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
```

## Troubleshooting

- **No notification:** Confirm Apple Music is playing, then use **Show current track** in the tray menu.
- **Missing executable:** Extract the release ZIP completely, or run `Publish.cmd` from a source checkout. Downloading source code alone does not include an executable.
- **Does not launch after signing in:** Run `Add-To-Startup.cmd` from the folder containing your current executable.
- **Build cannot find the SDK:** Install the .NET 10 SDK, open a new terminal, and run `dotnet --list-sdks`.
- **Cannot stop an older release:** Use **Exit** in its tray menu; older releases may not support `--exit`.
- **Persistent playback or startup errors:** Check `%LocalAppData%\NowPlayingToast\app.log` for local diagnostics. The log rotates at approximately 256 KB and keeps one previous file as `app.log.old`.

## Version and unreleased refinements

Source version: **1.1.0**. The faster track/art handling, event-driven lifecycle, safer settings, local diagnostics, .NET 10 build, automated checks, startup shortcut/removal, and graceful-stop refinements are unreleased changes. The existing v1.1.0 download may not contain them; the assembly version will be updated when a new release is prepared.
