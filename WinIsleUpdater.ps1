param(
    [Parameter(Mandatory = $true)][string]$ZipPath,
    [Parameter(Mandatory = $true)][string]$InstallDirectory,
    [Parameter(Mandatory = $true)][int]$ParentPid
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Windows.Forms

$logDirectory = Join-Path $env:LOCALAPPDATA "WinIsle\Updates"
$updateLog = Join-Path $logDirectory "updater.log"
function Write-UpdateLog([string]$Message) {
    try {
        New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
        Add-Content -LiteralPath $updateLog -Value ("{0} {1}" -f (Get-Date -Format s), $Message) -Encoding UTF8
    }
    catch {
        [Console]::Error.WriteLine("Gagal menulis log updater: " + $_.Exception.Message)
    }
}

$stage = Join-Path $env:TEMP ("WinIsle-update-" + [guid]::NewGuid().ToString("N"))
$backup = Join-Path $stage "backup"
$payload = Join-Path $stage "payload"
$names = @("WinIsle.exe", "media.ps1", "kana.ps1", "scantext.ps1", "README.md", "LICENSE", "WinIsleUpdater.ps1")
$required = $names
$replaced = New-Object System.Collections.Generic.List[string]

try {
    Write-UpdateLog "Updater dimulai. ParentPid=$ParentPid; InstallDirectory=$InstallDirectory; ZipPath=$ZipPath"
    if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) {
        throw "File ZIP update tidak ditemukan."
    }
    if (-not (Test-Path -LiteralPath $InstallDirectory -PathType Container)) {
        throw "Folder WinIsle tidak ditemukan."
    }

    $parent = Get-Process -Id $ParentPid -ErrorAction SilentlyContinue
    if ($parent) {
        Write-UpdateLog "Menunggu WinIsle ditutup (PID $ParentPid)."
        if (-not $parent.WaitForExit(120000)) {
            throw "WinIsle belum tertutup setelah 120 detik."
        }
    }
    Write-UpdateLog "Proses WinIsle lama sudah berhenti."

    New-Item -ItemType Directory -Path $payload -Force | Out-Null
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    $archive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $found = @{}
        foreach ($entry in $archive.Entries) {
            if ($names -notcontains $entry.FullName) {
                throw "Isi ZIP tidak dikenali: $($entry.FullName)"
            }
            if ($found.ContainsKey($entry.FullName)) {
                throw "ZIP berisi file duplikat: $($entry.FullName)"
            }
            $found[$entry.FullName] = $true
            $target = Join-Path $payload $entry.FullName
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target)
        }
    }
    finally {
        $archive.Dispose()
    }

    foreach ($name in $required) {
        if (-not $found.ContainsKey($name) -or
            -not (Test-Path -LiteralPath (Join-Path $payload $name) -PathType Leaf)) {
            throw "Paket update tidak memiliki file wajib: $name"
        }
    }
    Write-UpdateLog "Paket ZIP tervalidasi; semua file wajib tersedia."

    foreach ($name in $names) {
        $source = Join-Path $payload $name
        if (Test-Path -LiteralPath $source -PathType Leaf) {
            $destination = Join-Path $InstallDirectory $name
            if (Test-Path -LiteralPath $destination -PathType Leaf) {
                Copy-Item -LiteralPath $destination -Destination (Join-Path $backup $name)
            }
            $replaced.Add($name)
            Copy-Item -LiteralPath $source -Destination $destination -Force
            Write-UpdateLog "File diperbarui: $name"
        }
    }

    $app = Start-Process -FilePath (Join-Path $InstallDirectory "WinIsle.exe") -PassThru
    Write-UpdateLog "Pembaruan berhasil; WinIsle dijalankan kembali (PID $($app.Id))."
}
catch {
    $failure = $_.Exception.Message
    Write-UpdateLog ("Pembaruan gagal: " + ($_ | Out-String).Trim())
    foreach ($name in $replaced) {
        $destination = Join-Path $InstallDirectory $name
        $saved = Join-Path $backup $name
        try {
            if (Test-Path -LiteralPath $saved -PathType Leaf) {
                Copy-Item -LiteralPath $saved -Destination $destination -Force
            }
            elseif (Test-Path -LiteralPath $destination -PathType Leaf) {
                Remove-Item -LiteralPath $destination -Force
            }
        }
        catch {
            $failure += "`nRollback gagal untuk $name`: $($_.Exception.Message)"
            Write-UpdateLog "Rollback gagal untuk $name`: $($_.Exception.Message)"
        }
    }

    [System.Windows.Forms.MessageBox]::Show(
        "Pembaruan WinIsle gagal. File lama dipulihkan jika memungkinkan.`n`n$failure`n`nLog: $updateLog",
        "Pembaruan WinIsle",
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null

    $oldExe = Join-Path $InstallDirectory "WinIsle.exe"
    if (Test-Path -LiteralPath $oldExe -PathType Leaf) {
        try {
            Start-Process -FilePath $oldExe
            Write-UpdateLog "WinIsle lama dijalankan kembali setelah kegagalan."
        }
        catch {
            Write-UpdateLog "WinIsle lama gagal dijalankan kembali: $($_.Exception.Message)"
        }
    }
}
finally {
    if (Test-Path -LiteralPath $stage) {
        Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
    }
}
