param([Parameter(Mandatory=$true)][string]$ImagePath)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false

try {
    if (-not (Test-Path -LiteralPath $ImagePath -PathType Leaf)) {
        throw 'Gambar untuk dipindai tidak ditemukan.'
    }

    Add-Type -AssemblyName System.Drawing
    Add-Type -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class WinIsleOcrImage
{
    public static void Enhance(string inputPath, string outputPath)
    {
        using (Bitmap source = new Bitmap(inputPath))
        {
            int scale = Math.Max(1, Math.Min(2, 10000 / Math.Max(source.Width, source.Height)));
            int width = source.Width * scale;
            int height = source.Height * scale;
            using (Bitmap image = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                using (Graphics graphics = Graphics.FromImage(image))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.DrawImage(source, new Rectangle(0, 0, width, height));
                }

                Rectangle bounds = new Rectangle(0, 0, width, height);
                BitmapData data = image.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                try
                {
                    int stride = Math.Abs(data.Stride);
                    byte[] pixels = new byte[stride * height];
                    Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

                    const int tileSize = 96;
                    for (int tileY = 0; tileY < height; tileY += tileSize)
                    {
                        int tileBottom = Math.Min(tileY + tileSize, height);
                        for (int tileX = 0; tileX < width; tileX += tileSize)
                        {
                            int tileRight = Math.Min(tileX + tileSize, width);
                            long brightness = 0;
                            long samples = 0;
                            for (int y = tileY; y < tileBottom; y += 8)
                            {
                                int row = y * stride;
                                for (int x = tileX; x < tileRight; x += 8)
                                {
                                    int pixel = row + x * 4;
                                    brightness += (pixels[pixel] * 29 + pixels[pixel + 1] * 150 + pixels[pixel + 2] * 77) >> 8;
                                    samples++;
                                }
                            }

                            bool darkBackground = samples > 0 && brightness / samples < 128;
                            for (int y = tileY; y < tileBottom; y++)
                            {
                                int row = y * stride;
                                for (int x = tileX; x < tileRight; x++)
                                {
                                    int pixel = row + x * 4;
                                    int luminance = (pixels[pixel] * 29 + pixels[pixel + 1] * 150 + pixels[pixel + 2] * 77) >> 8;
                                    int contrast = darkBackground ? 255 - luminance : luminance;
                                    byte value = (byte)Math.Min(255, contrast * 1.25);
                                    pixels[pixel] = value;
                                    pixels[pixel + 1] = value;
                                    pixels[pixel + 2] = value;
                                    pixels[pixel + 3] = 255;
                                }
                            }
                        }
                    }

                    Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                }
                finally
                {
                    image.UnlockBits(data);
                }

                image.Save(outputPath, ImageFormat.Png);
            }
        }
    }
}
'@ -ReferencedAssemblies ([System.Drawing.Bitmap].Assembly.Location)

    $enhancedImagePath = Join-Path ([System.IO.Path]::GetTempPath()) ('WinIsle-OCR-' + [guid]::NewGuid().ToString('N') + '.png')
    [WinIsleOcrImage]::Enhance($ImagePath, $enhancedImagePath)

    Add-Type -AssemblyName System.Runtime.WindowsRuntime
    $asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
        $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and
        $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
    })[0]
    if (-not $asTask) { throw 'Windows Runtime OCR tidak tersedia.' }

    function Await($operation, $type) {
        $task = $asTask.MakeGenericMethod($type).Invoke($null, @($operation))
        $task.Wait(-1) | Out-Null
        $task.Result
    }

    $engine = [Windows.Media.Ocr.OcrEngine,Windows.Media.Ocr,ContentType=WindowsRuntime]::TryCreateFromUserProfileLanguages()
    if (-not $engine) {
        throw 'OCR Windows tidak tersedia. Pasang paket bahasa dengan dukungan OCR di Pengaturan Windows.'
    }

    function RecognizeImage([string]$path, $ocrEngine) {
        $localStream = $null
        $localBitmap = $null
        try {
            $storageFile = [Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime]::GetFileFromPathAsync($path)
            $storageFile = Await $storageFile ([Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime])
            $localStream = $storageFile.OpenAsync([Windows.Storage.FileAccessMode,Windows.Storage,ContentType=WindowsRuntime]::Read)
            $localStream = Await $localStream ([Windows.Storage.Streams.IRandomAccessStream,Windows.Storage.Streams,ContentType=WindowsRuntime])
            $decoder = [Windows.Graphics.Imaging.BitmapDecoder,Windows.Graphics.Imaging,ContentType=WindowsRuntime]::CreateAsync($localStream)
            $decoder = Await $decoder ([Windows.Graphics.Imaging.BitmapDecoder,Windows.Graphics.Imaging,ContentType=WindowsRuntime])
            $localBitmap = $decoder.GetSoftwareBitmapAsync()
            $localBitmap = Await $localBitmap ([Windows.Graphics.Imaging.SoftwareBitmap,Windows.Graphics.Imaging,ContentType=WindowsRuntime])
            $recognition = $ocrEngine.RecognizeAsync($localBitmap)
            Await $recognition ([Windows.Media.Ocr.OcrResult,Windows.Media.Ocr,ContentType=WindowsRuntime])
        }
        finally {
            if ($localStream) { $localStream.Dispose() }
            if ($localBitmap) { $localBitmap.Dispose() }
        }
    }

    $result = RecognizeImage $enhancedImagePath $engine
    $recognizedText = $result.Text
    $sourceBitmap = New-Object System.Drawing.Bitmap($enhancedImagePath)
    try {
        $textSearchPosition = 0
        foreach ($line in $result.Lines) {
            $words = @($line.Words)
            if ($words.Count -lt 3) { continue }

            $lineTop = [double]::MaxValue
            $lineBottom = [double]::MinValue
            foreach ($word in $words) {
                $lineTop = [Math]::Min($lineTop, $word.BoundingRect.Y)
                $lineBottom = [Math]::Max($lineBottom, $word.BoundingRect.Y + $word.BoundingRect.Height)
            }
            # Inline code can be skipped even when OCR detects words on both sides.
            $gapThreshold = [Math]::Max(64, ($lineBottom - $lineTop) * 2)
            $lineText = $line.Text
            $lineSearchPosition = 0

            for ($wordIndex = 0; $wordIndex -lt $words.Count - 2; $wordIndex++) {
                $previousWord = $words[$wordIndex]
                $nextWord = $words[$wordIndex + 1]
                $previousRight = $previousWord.BoundingRect.X + $previousWord.BoundingRect.Width
                $gap = $nextWord.BoundingRect.X - $previousRight
                if ($gap -le $gapThreshold) { continue }

                $clusterEnd = $wordIndex + 1
                $followingWord = $words[$clusterEnd + 1]
                $cropLeft = [int][Math]::Max(0, [Math]::Floor($previousRight - 6))
                $cropTop = [int][Math]::Max(0, [Math]::Floor($lineTop - 16))
                $cropRight = [int][Math]::Min($sourceBitmap.Width, [Math]::Ceiling($followingWord.BoundingRect.X + 6))
                $cropBottom = [int][Math]::Min($sourceBitmap.Height, [Math]::Ceiling($lineBottom + 10))
                if ($cropRight -le $cropLeft -or $cropBottom -le $cropTop) { continue }

                $previousIndex = $lineText.IndexOf($previousWord.Text, $lineSearchPosition, [StringComparison]::Ordinal)
                if ($previousIndex -lt 0) { continue }
                $followingIndex = $lineText.IndexOf(
                    $followingWord.Text,
                    $previousIndex + $previousWord.Text.Length,
                    [StringComparison]::Ordinal)
                if ($followingIndex -lt $previousIndex + $previousWord.Text.Length) { continue }

                $cropPath = Join-Path ([System.IO.Path]::GetTempPath()) ('WinIsle-OCR-Gap-' + [guid]::NewGuid().ToString('N') + '.png')
                $crop = $null
                try {
                    $crop = $sourceBitmap.Clone(
                        [System.Drawing.Rectangle]::new($cropLeft, $cropTop, $cropRight - $cropLeft, $cropBottom - $cropTop),
                        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
                    $crop.Save($cropPath, [System.Drawing.Imaging.ImageFormat]::Png)
                    $gapResult = RecognizeImage $cropPath $engine
                    $recoveredText = $gapResult.Text.Trim()
                    if ($recoveredText.Length -eq 0) { continue }

                    $prefix = $lineText.Substring(0, $previousIndex + $previousWord.Text.Length).TrimEnd()
                    $suffix = $lineText.Substring($followingIndex).TrimStart()
                    $lineText = $prefix + ' ' + $recoveredText + ' ' + $suffix
                    $lineSearchPosition = $prefix.Length + $recoveredText.Length + 2
                    $wordIndex = $clusterEnd
                }
                finally {
                    if ($crop) { $crop.Dispose() }
                    if (Test-Path -LiteralPath $cropPath) { Remove-Item -LiteralPath $cropPath -Force }
                }
            }

            if ($lineText -ne $line.Text) {
                $lineIndex = $recognizedText.IndexOf($line.Text, $textSearchPosition, [StringComparison]::Ordinal)
                if ($lineIndex -ge 0) {
                    $recognizedText = $recognizedText.Remove($lineIndex, $line.Text.Length).Insert($lineIndex, $lineText)
                    $textSearchPosition = $lineIndex + $lineText.Length
                }
            }
        }
    }
    finally {
        $sourceBitmap.Dispose()
    }

    [Console]::Out.Write($recognizedText)
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
finally {
    if ($enhancedImagePath) {
        try { Remove-Item -LiteralPath $enhancedImagePath -Force } catch { }
    }
}
