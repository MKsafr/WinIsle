# kana.ps1 - dipakai WinIsle untuk membaca kanji (menghasilkan hiragana) sebelum diubah ke romaji.
# Memakai Windows.Globalization.JapanesePhoneticAnalyzer (bawaan Windows). Hanya karakter ASCII di file ini.
# Satu baris masuk (-InFile, UTF-8) = satu baris keluar. Kalau analyzer tidak tersedia, baris dikembalikan apa adanya.
param([string]$InFile)

$ErrorActionPreference = 'SilentlyContinue'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false

$az = $null
try { $az = [Windows.Globalization.JapanesePhoneticAnalyzer,Windows.Globalization,ContentType=WindowsRuntime] } catch {}

$ha = [string][char]0x306F   # hiragana "ha"  (partikel -> dibaca "wa")
$he = [string][char]0x3078   # hiragana "he"  (partikel -> dibaca "e")
$wa = [string][char]0x308F
$e  = [string][char]0x3048

$lines = [System.IO.File]::ReadAllLines($InFile, (New-Object System.Text.UTF8Encoding $false))
foreach ($line in $lines) {
    $out = $line
    try {
        if ($az -and $line.Trim().Length -gt 0) {
            $words = $az::GetWords($line)
            $sb = New-Object System.Text.StringBuilder
            foreach ($w in $words) {
                if ($w.IsPhraseStart -and $sb.Length -gt 0) { [void]$sb.Append(' ') }
                $y = $w.YomiText
                if ([string]::IsNullOrEmpty($y)) { $y = $w.DisplayText }
                # kata berdiri sendiri: partikel
                if ($w.DisplayText -eq $ha) { $y = $wa }
                elseif ($w.DisplayText -eq $he) { $y = $e }
                [void]$sb.Append($y)
            }
            if ($sb.Length -gt 0) { $out = $sb.ToString() }
        }
    } catch {}
    [Console]::Out.WriteLine($out)
}
