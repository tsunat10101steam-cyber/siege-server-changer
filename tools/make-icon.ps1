# Generates src/SiegeServerChanger.App/app.ico (globe on a blue rounded square).
# Usage: powershell -ExecutionPolicy Bypass -File tools/make-icon.ps1
Add-Type -AssemblyName System.Drawing

$out = Join-Path $PSScriptRoot '..\src\SiegeServerChanger.App\app.ico'
$sizes = 16, 24, 32, 48, 64, 128, 256

function New-IconPng([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    # Rounded square background
    $r = [Math]::Max(2, $s * 0.22)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $w = $s - 1
    $path.AddArc(0, 0, $r * 2, $r * 2, 180, 90)
    $path.AddArc($w - $r * 2, 0, $r * 2, $r * 2, 270, 90)
    $path.AddArc($w - $r * 2, $w - $r * 2, $r * 2, $r * 2, 0, 90)
    $path.AddArc(0, $w - $r * 2, $r * 2, $r * 2, 90, 90)
    $path.CloseFigure()
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Point 0, 0), (New-Object System.Drawing.Point $s, $s), ([System.Drawing.Color]::FromArgb(255, 77, 150, 255)), ([System.Drawing.Color]::FromArgb(255, 30, 80, 190))
    $g.FillPath($bg, $path)

    # Globe
    $m = $s * 0.2; $d = $s - 2 * $m
    $penW = [Math]::Max(1.0, $s / 16.0)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), $penW
    $g.DrawEllipse($pen, $m, $m, $d, $d)
    $g.DrawEllipse($pen, $s / 2 - $d * 0.22, $m, $d * 0.44, $d)
    $g.DrawLine($pen, $m, $s / 2, $m + $d, $s / 2)
    if ($s -ge 32) {
        $thin = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(200, 255, 255, 255)), ($penW * 0.7)
        $y1 = $m + $d * 0.25; $y2 = $m + $d * 0.75; $dx = $d * 0.067
        $g.DrawLine($thin, $m + $dx, $y1, $m + $d - $dx, $y1)
        $g.DrawLine($thin, $m + $dx, $y2, $m + $d - $dx, $y2)
    }

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    return , $ms.ToArray()
}

$images = foreach ($s in $sizes) { , (New-IconPng $s) }

# ICO container with PNG-compressed entries
$fs = [System.IO.File]::Create($out)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $images[$i].Length
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32); $bw.Write([UInt32]$len); $bw.Write([UInt32]$offset)
    $offset += $len
}
foreach ($img in $images) { $bw.Write($img) }
$bw.Close()
"Wrote $out"
