param(
    [Parameter(Mandatory = $true)][string]$ZipPath,
    [Parameter(Mandatory = $true)][string]$InstallDirectory,
    [Parameter(Mandatory = $true)][int]$ParentPid
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Windows.Forms

$stage = Join-Path $env:TEMP ("WinIsle-update-" + [guid]::NewGuid().ToString("N"))
$backup = Join-Path $stage "backup"
$payload = Join-Path $stage "payload"
$names = @("WinIsle.exe", "media.ps1", "kana.ps1", "README.md", "LICENSE", "WinIsleUpdater.ps1")
$required = @("WinIsle.exe", "media.ps1", "kana.ps1", "WinIsleUpdater.ps1")
$replaced = New-Object System.Collections.Generic.List[string]

try {
    if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) {
        throw "File ZIP update tidak ditemukan."
    }
    if (-not (Test-Path -LiteralPath $InstallDirectory -PathType Container)) {
        throw "Folder WinIsle tidak ditemukan."
    }

    $parent = Get-Process -Id $ParentPid -ErrorAction SilentlyContinue
    if ($parent) {
        if (-not $parent.WaitForExit(60000)) {
            throw "WinIsle belum tertutup setelah 60 detik."
        }
    }

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

    foreach ($name in $names) {
        $source = Join-Path $payload $name
        if (Test-Path -LiteralPath $source -PathType Leaf) {
            $destination = Join-Path $InstallDirectory $name
            if (Test-Path -LiteralPath $destination -PathType Leaf) {
                Copy-Item -LiteralPath $destination -Destination (Join-Path $backup $name)
            }
            $replaced.Add($name)
            Copy-Item -LiteralPath $source -Destination $destination -Force
        }
    }

    Start-Process -FilePath (Join-Path $InstallDirectory "WinIsle.exe")
}
catch {
    $failure = $_.Exception.Message
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
        }
    }

    [System.Windows.Forms.MessageBox]::Show(
        "Pembaruan WinIsle gagal. File lama dipulihkan jika memungkinkan.`n`n$failure",
        "Pembaruan WinIsle",
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null

    $oldExe = Join-Path $InstallDirectory "WinIsle.exe"
    if (Test-Path -LiteralPath $oldExe -PathType Leaf) {
        Start-Process -FilePath $oldExe
    }
}
finally {
    if (Test-Path -LiteralPath $stage) {
        Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
    }
}
