# NowPlaying-Toast — popup when Windows media session track changes (Apple Music, etc.)
$ErrorActionPreference = 'SilentlyContinue'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Runtime.WindowsRuntime

$asTaskMethod = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
  $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and
  $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
} | Select-Object -First 1

function Wait-Op($op, [Type]$t) {
  $m = $asTaskMethod.MakeGenericMethod($t)
  $task = $m.Invoke($null, @($op))
  return $task.GetAwaiter().GetResult()
}

$null = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows.Media.Control, ContentType = WindowsRuntime]
$null = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties, Windows.Media.Control, ContentType = WindowsRuntime]
$null = [Windows.Storage.Streams.DataReader, Windows.Storage.Streams, ContentType = WindowsRuntime]
$null = [Windows.Storage.Streams.IRandomAccessStreamWithContentType, Windows.Storage.Streams, ContentType = WindowsRuntime]

function Get-NowPlaying {
  try {
    $mgr = Wait-Op ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager]::RequestAsync()) ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager])
    $session = $mgr.GetCurrentSession()
    if (-not $session) { return $null }

    $props = Wait-Op ($session.TryGetMediaPropertiesAsync()) ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties])
    if (-not $props) { return $null }

    $title = ([string]$props.Title).Trim()
    $artist = ([string]$props.Artist).Trim()
    $album = ([string]$props.AlbumTitle).Trim()
    $app = ''
    try { $app = [string]$session.SourceAppUserModelId } catch {}
    if ([string]::IsNullOrWhiteSpace($title)) { return $null }

    $thumbBytes = $null
    try {
      if ($props.Thumbnail) {
        $stream = Wait-Op ($props.Thumbnail.OpenReadAsync()) ([Windows.Storage.Streams.IRandomAccessStreamWithContentType])
        $size = [int]$stream.Size
        if ($size -gt 64 -and $size -lt 4MB) {
          $reader = [Windows.Storage.Streams.DataReader]::CreateDataReader($stream)
          $load = $reader.LoadAsync([uint32]$size)
          # LoadAsync is IAsyncOperation<uint> 
          $loadType = [uint32]
          try {
            $m = $asTaskMethod.MakeGenericMethod($loadType)
            $m.Invoke($null, @($load)).GetAwaiter().GetResult() | Out-Null
          } catch {
            $sw = [Diagnostics.Stopwatch]::StartNew()
            while ([int]$load.Status -ne 1 -and $sw.ElapsedMilliseconds -lt 2000) { Start-Sleep -Milliseconds 20 }
          }
          $thumbBytes = New-Object byte[] $size
          $reader.ReadBytes($thumbBytes)
          $reader.Dispose()
        }
        $stream.Dispose()
      }
    } catch {}

    [pscustomobject]@{
      Title  = $title
      Artist = $artist
      Album  = $album
      App    = $app
      Thumb  = $thumbBytes
      Key    = "$title|$artist|$album"
    }
  } catch { return $null }
}

$form = New-Object System.Windows.Forms.Form
$form.FormBorderStyle = 'None'
$form.StartPosition = 'Manual'
$form.ShowInTaskbar = $false
$form.TopMost = $true
$form.BackColor = [System.Drawing.Color]::FromArgb(28, 28, 30)
$form.Size = New-Object System.Drawing.Size(360, 92)
$form.Opacity = 0.97

$panel = New-Object System.Windows.Forms.Panel
$panel.Dock = 'Fill'
$panel.BackColor = $form.BackColor
$form.Controls.Add($panel)

$pic = New-Object System.Windows.Forms.PictureBox
$pic.Size = New-Object System.Drawing.Size(68, 68)
$pic.Location = New-Object System.Drawing.Point(12, 12)
$pic.SizeMode = 'Zoom'
$pic.BackColor = [System.Drawing.Color]::FromArgb(44, 44, 46)
$panel.Controls.Add($pic)

$lblTitle = New-Object System.Windows.Forms.Label
$lblTitle.ForeColor = [System.Drawing.Color]::White
$lblTitle.Font = New-Object System.Drawing.Font('Segoe UI Semibold', 11)
$lblTitle.Location = New-Object System.Drawing.Point(92, 14)
$lblTitle.Size = New-Object System.Drawing.Size(250, 24)
$lblTitle.AutoEllipsis = $true
$panel.Controls.Add($lblTitle)

$lblArtist = New-Object System.Windows.Forms.Label
$lblArtist.ForeColor = [System.Drawing.Color]::FromArgb(180, 180, 185)
$lblArtist.Font = New-Object System.Drawing.Font('Segoe UI', 9.5)
$lblArtist.Location = New-Object System.Drawing.Point(92, 40)
$lblArtist.Size = New-Object System.Drawing.Size(250, 20)
$lblArtist.AutoEllipsis = $true
$panel.Controls.Add($lblArtist)

$lblApp = New-Object System.Windows.Forms.Label
$lblApp.ForeColor = [System.Drawing.Color]::FromArgb(120, 120, 128)
$lblApp.Font = New-Object System.Drawing.Font('Segoe UI', 8)
$lblApp.Location = New-Object System.Drawing.Point(92, 62)
$lblApp.Size = New-Object System.Drawing.Size(250, 16)
$lblApp.AutoEllipsis = $true
$panel.Controls.Add($lblApp)

function Place-Form {
  $wa = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
  $form.Left = $wa.Right - $form.Width - 18
  $form.Top = $wa.Bottom - $form.Height - 18
}

function Show-Toast($np) {
  $lblTitle.Text = $np.Title
  $lblArtist.Text = $(if ($np.Artist) { $np.Artist } elseif ($np.Album) { $np.Album } else { ' ' })
  $appNice = $np.App
  if ($appNice -match 'AppleMusic|AppleInc\.AppleMusic') { $appNice = 'Apple Music' }
  elseif ($appNice -match 'Spotify') { $appNice = 'Spotify' }
  elseif ($appNice -match 'Chrome|Edge|Firefox|Brave') { $appNice = 'Browser' }
  elseif ($appNice) {
    $base = ($appNice -split '!')[0]
    if ($base -match '\\') { $appNice = [IO.Path]::GetFileNameWithoutExtension($base) }
    else { $appNice = $base }
  }
  $lblApp.Text = $appNice

  if ($pic.Image) { $old = $pic.Image; $pic.Image = $null; $old.Dispose() }
  if ($np.Thumb -and $np.Thumb.Length -gt 0) {
    try {
      $ms = New-Object System.IO.MemoryStream(,$np.Thumb)
      $img = [System.Drawing.Image]::FromStream($ms)
      $bmp = New-Object System.Drawing.Bitmap $img
      $img.Dispose(); $ms.Dispose()
      $pic.Image = $bmp
    } catch {}
  }

  Place-Form
  if (-not $form.Visible) { $form.Show() }
  $form.BringToFront()
  $script:hideAt = [datetime]::UtcNow.AddSeconds(4.5)
}

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 900
$script:lastKey = ''
$script:hideAt = [datetime]::MinValue
$script:primed = $false

$timer.Add_Tick({
  try {
    $np = Get-NowPlaying
    if ($np) {
      if (-not $script:primed) {
        $script:lastKey = $np.Key
        $script:primed = $true
      } elseif ($np.Key -ne $script:lastKey) {
        $script:lastKey = $np.Key
        Show-Toast $np
      }
    } else {
      $script:primed = $true
    }
    if ($form.Visible -and [datetime]::UtcNow -ge $script:hideAt) { $form.Hide() }
  } catch {}
})

$tray = New-Object System.Windows.Forms.NotifyIcon
$tray.Text = 'Now Playing Toast'
$tray.Visible = $true
try { $tray.Icon = [System.Drawing.SystemIcons]::Application } catch {}
$menu = New-Object System.Windows.Forms.ContextMenuStrip
$miTest = $menu.Items.Add('Show current track')
$miExit = $menu.Items.Add('Exit')
$tray.ContextMenuStrip = $menu
$miTest.Add_Click({
  $np = Get-NowPlaying
  if ($np) { Show-Toast $np }
  else { [System.Windows.Forms.MessageBox]::Show('Nothing playing (or no media session). Start a song in Apple Music.','Now Playing Toast') }
})
$miExit.Add_Click({
  $tray.Visible = $false
  $timer.Stop()
  $form.Close()
  [System.Windows.Forms.Application]::Exit()
})

Place-Form
$timer.Start()
[System.Windows.Forms.Application]::Run()
$tray.Dispose()
if ($pic.Image) { $pic.Image.Dispose() }
