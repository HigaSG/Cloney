# Gera src/Cloney/Assets/cloney.ico (dois quadrados sobrepostos = "clone").
# Uso: powershell -ExecutionPolicy Bypass -File tools\make-app-icon.ps1
Add-Type -AssemblyName System.Drawing

$out = Join-Path $PSScriptRoot '..\src\Cloney\Assets\cloney.ico'
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null

function New-RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $s = $size / 256.0

    # Quadrado de trás (cópia)
    $back = New-RoundedPath (20 * $s) (20 * $s) (150 * $s) (150 * $s) (34 * $s)
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0xF0, 0x8C, 0x00))), $back)

    # Quadrado da frente (original) com borda branca
    $front = New-RoundedPath (86 * $s) (86 * $s) (150 * $s) (150 * $s) (34 * $s)
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0xD9, 0x48, 0x0F))), $front)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([Math]::Max(1.5, 12 * $s))
    $g.DrawPath($pen, $front)

    # Sinal de "+" no quadrado da frente
    $plus = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([Math]::Max(1.5, 18 * $s))
    $plus.StartCap = 'Round'; $plus.EndCap = 'Round'
    $c = 161 * $s; $l = 34 * $s
    $g.DrawLine($plus, $c - $l, $c, $c + $l, $c)
    $g.DrawLine($plus, $c, $c - $l, $c, $c + $l)

    $g.Dispose()
    return $bmp
}

$sizes = 256, 64, 48, 32, 24, 16
$frames = foreach ($sz in $sizes) {
    $bmp = New-IconBitmap $sz
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , @($sz, $ms.ToArray())
}

$fs = [System.IO.File]::Create((Resolve-Path -LiteralPath (Split-Path $out)).Path + '\cloney.ico')
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $sz = $f[0]; $data = $f[1]
    $dim = if ($sz -ge 256) { 0 } else { $sz }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([int16]1); $w.Write([int16]32); $w.Write([int32]$data.Length); $w.Write([int32]$offset)
    $offset += $data.Length
}
foreach ($f in $frames) { $w.Write([byte[]]$f[1]) }
$w.Close()
Write-Host "Icone gerado: $out"
