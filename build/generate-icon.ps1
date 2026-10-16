<#
.SYNOPSIS
    Gera assets/icon.ico e assets/icon-256.png. Arte original desenhada em código, com a mesma
    geometria de Theme.DrawMark (src/AnyPS5Conversor/Theme.cs). Requer Windows (System.Drawing).
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-Rounded([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure(); $p
}

function New-Mark([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $s = [float]($size - 1)
    $accent = [System.Drawing.Color]::FromArgb(0x3B, 0x82, 0xF6)
    $g.FillPath((New-Object System.Drawing.SolidBrush $accent), (New-Rounded 0 0 $s $s ($s * 0.22)))

    $white = [System.Drawing.Brushes]::White
    $g.FillEllipse($white, $s * 0.13, $s * 0.41, $s * 0.34, $s * 0.38)
    $g.FillEllipse($white, $s * 0.53, $s * 0.41, $s * 0.34, $s * 0.38)
    $g.FillPath($white, (New-Rounded ($s * 0.12) ($s * 0.30) ($s * 0.76) ($s * 0.32) ($s * 0.14)))

    $hole = New-Object System.Drawing.SolidBrush $accent
    $g.FillRectangle($hole, $s * 0.235, $s * 0.445, $s * 0.15, $s * 0.05)
    $g.FillRectangle($hole, $s * 0.285, $s * 0.395, $s * 0.05, $s * 0.15)
    $g.FillEllipse($hole, $s * 0.615, $s * 0.475, $s * 0.07, $s * 0.07)
    $g.FillEllipse($hole, $s * 0.695, $s * 0.395, $s * 0.07, $s * 0.07)

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    , $ms.ToArray()
}

$assets = Join-Path (Split-Path -Parent $PSScriptRoot) 'assets'
$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($size in $sizes) { , (New-Mark $size) }

$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $b = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32); $w.Write([UInt32]$pngs[$i].Length); $w.Write([UInt32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($png in $pngs) { $w.Write($png) }
[IO.File]::WriteAllBytes((Join-Path $assets 'icon.ico'), $out.ToArray())
[IO.File]::WriteAllBytes((Join-Path $assets 'icon-256.png'), $pngs[-1])
Write-Host "icon written to $assets"
