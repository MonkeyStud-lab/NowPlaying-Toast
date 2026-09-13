# Now Playing Toast - launch only while Apple Music is running.
# Keep this script running (Startup); it starts/stops NowPlayingToast.exe with Apple Music.

$ErrorActionPreference = 'SilentlyContinue'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $root 'NowPlayingToast.exe'
$pollSeconds = 2

function Test-AppleMusic {
    return [bool](Get-Process -Name 'AppleMusic' -ErrorAction SilentlyContinue)
}

function Test-Toast {
    return [bool](Get-Process -Name 'NowPlayingToast' -ErrorAction SilentlyContinue)
}

function Start-Toast {
    if (-not (Test-Path -LiteralPath $exe)) { return }
    if (Test-Toast) { return }
    Start-Process -FilePath $exe -WorkingDirectory $root
}

function Stop-Toast {
    Get-Process -Name 'NowPlayingToast' -ErrorAction SilentlyContinue | Stop-Process -Force
}

while ($true) {
    try {
        $music = Test-AppleMusic
        $toast = Test-Toast
        if ($music -and -not $toast) {
            Start-Toast
        }
        elseif (-not $music -and $toast) {
            Stop-Toast
        }
    }
    catch { }
    Start-Sleep -Seconds $pollSeconds
}
