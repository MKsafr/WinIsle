# media.ps1 - dipakai VorsWinIsland. Hanya karakter ASCII di file ini (supaya aman di PowerShell 5.1).
#
# Mode 1 (default): berjalan terus, mencetak 1 baris JSON per detik berisi info media yang sedang diputar.
#                   Berhenti sendiri kalau proses VorsWinIsland (-ParentPid) sudah tidak ada.
# Mode 2 (-SeekTo detik): pindahkan posisi lagu lalu keluar.
param([double]$SeekTo = -1, [int]$ParentPid = 0)

$ErrorActionPreference = 'SilentlyContinue'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false
$inv = [System.Globalization.CultureInfo]::InvariantCulture

Add-Type -AssemblyName System.Runtime.WindowsRuntime
$MgrType   = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager,Windows.Media.Control,ContentType=WindowsRuntime]
$PropsType = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties,Windows.Media.Control,ContentType=WindowsRuntime]

$asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
})[0]

function Await($op, $type) {
    $task = $asTask.MakeGenericMethod($type).Invoke($null, @($op))
    $task.Wait(-1) | Out-Null
    $task.Result
}

$mgr = Await ($MgrType::RequestAsync()) $MgrType

# ---------- Mode seek ----------
if ($SeekTo -ge 0) {
    $s = $mgr.GetCurrentSession()
    if ($s) {
        $ticks = [long]($SeekTo * 10000000)
        Await ($s.TryChangePlaybackPositionAsync($ticks)) ([bool]) | Out-Null
    }
    exit
}

# ---------- Mode pemantau ----------
while ($true) {
    if ($ParentPid -gt 0 -and -not (Get-Process -Id $ParentPid -ErrorAction SilentlyContinue)) { break }

    $line = '{}'
    try {
        $s = $mgr.GetCurrentSession()
        if ($s) {
            $p = $null
            try { $p = Await ($s.TryGetMediaPropertiesAsync()) $PropsType } catch {}
            $playing = ("$($s.GetPlaybackInfo().PlaybackStatus)" -eq 'Playing')

            $tl = $null
            try { $tl = $s.GetTimelineProperties() } catch {}
            $pos = -1.0
            $dur = 0.0
            if ($tl) {
                $pos = $tl.Position.TotalSeconds
                $dur = $tl.EndTime.TotalSeconds - $tl.StartTime.TotalSeconds
                if ($playing) { $pos += ([DateTimeOffset]::Now - $tl.LastUpdatedTime).TotalSeconds }
            }

            $line = @{
                title   = "$($p.Title)"
                artist  = "$($p.Artist)"
                album   = "$($p.AlbumTitle)"
                playing = $playing
                pos     = ([math]::Round($pos, 2)).ToString('0.00', $inv)
                dur     = ([math]::Round($dur, 2)).ToString('0.00', $inv)
            } | ConvertTo-Json -Compress
        }
    } catch {}

    [Console]::Out.WriteLine($line)
    [Console]::Out.Flush()
    Start-Sleep -Milliseconds 1000
}
