param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$outputDirectory = $PSScriptRoot
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = [System.Collections.Generic.List[object]]::new()

function New-ScaledPoints {
    param(
        [double[]]$Coordinates,
        [double]$Scale
    )

    $points = [System.Collections.Generic.List[System.Drawing.PointF]]::new()
    for ($index = 0; $index -lt $Coordinates.Length; $index += 2) {
        $points.Add([System.Drawing.PointF]::new(
            [single]($Coordinates[$index] * $Scale),
            [single]($Coordinates[$index + 1] * $Scale)))
    }

    return ,$points.ToArray()
}

function New-IcoFrame {
    param([System.Drawing.Bitmap]$Bitmap)

    $size = $Bitmap.Width
    $stream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($stream)

    $writer.Write([int]40)
    $writer.Write([int]$size)
    $writer.Write([int]($size * 2))
    $writer.Write([int16]1)
    $writer.Write([int16]32)
    $writer.Write([int]0)
    $writer.Write([int]($size * $size * 4))
    $writer.Write([int]0)
    $writer.Write([int]0)
    $writer.Write([int]0)
    $writer.Write([int]0)

    for ($y = $size - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $size; $x++) {
            $pixel = $Bitmap.GetPixel($x, $y)
            $writer.Write([byte]$pixel.B)
            $writer.Write([byte]$pixel.G)
            $writer.Write([byte]$pixel.R)
            $writer.Write([byte]$pixel.A)
        }
    }

    $maskLength = [int]([Math]::Ceiling($size / 32.0) * 4 * $size)
    for ($index = 0; $index -lt $maskLength; $index++) {
        $writer.Write([byte]0)
    }

    $writer.Flush()
    $bytes = $stream.ToArray()
    $writer.Dispose()
    $stream.Dispose()
    return ,$bytes
}

$ink = [System.Drawing.Color]::FromArgb(255, 16, 25, 39)
$leftWall = [System.Drawing.Color]::FromArgb(255, 23, 43, 64)
$rightWall = [System.Drawing.Color]::FromArgb(255, 19, 36, 57)
$topRim = [System.Drawing.Color]::FromArgb(255, 27, 53, 76)
$cyan = [System.Drawing.Color]::FromArgb(255, 41, 217, 242)
$sky = [System.Drawing.Color]::FromArgb(255, 120, 200, 255)

foreach ($size in $sizes) {
    $scale = $size / 256.0
    $bitmap = [System.Drawing.Bitmap]::new(
        $size,
        $size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $leftBrush = [System.Drawing.SolidBrush]::new($leftWall)
    $rightBrush = [System.Drawing.SolidBrush]::new($rightWall)
    $topBrush = [System.Drawing.SolidBrush]::new($topRim)
    $openingBrush = [System.Drawing.SolidBrush]::new($ink)
    $accentBrush = [System.Drawing.SolidBrush]::new($sky)
    $strokeWidth = [single][Math]::Max(2.25, 12.64 * $scale)
    $openingStrokeWidth = [single][Math]::Max(1.6, 7.9 * $scale)
    $outline = [System.Drawing.Pen]::new($cyan, $strokeWidth)
    $openingOutline = [System.Drawing.Pen]::new($cyan, $openingStrokeWidth)

    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)

        $left = New-ScaledPoints -Scale $scale -Coordinates @(26.88, 69.54, 128, 124.84, 128, 238.6, 26.88, 183.3)
        $right = New-ScaledPoints -Scale $scale -Coordinates @(128, 124.84, 229.12, 69.54, 229.12, 183.3, 128, 238.6)
        $top = New-ScaledPoints -Scale $scale -Coordinates @(26.88, 69.54, 128, 14.24, 229.12, 69.54, 128, 124.84)
        $opening = New-ScaledPoints -Scale $scale -Coordinates @(69.54, 69.54, 128, 36.36, 186.46, 69.54, 128, 102.72)

        $graphics.FillPolygon($leftBrush, $left)
        $graphics.FillPolygon($rightBrush, $right)
        $graphics.FillPolygon($topBrush, $top)
        $graphics.FillPolygon($openingBrush, $opening)
        $outline.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $outline.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $outline.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $openingOutline.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $openingOutline.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $openingOutline.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $graphics.DrawPolygon($outline, $left)
        $graphics.DrawPolygon($outline, $right)
        $graphics.DrawPolygon($outline, $top)
        $graphics.DrawPolygon($openingOutline, $opening)
        $graphics.DrawLine(
            $outline,
            [single](128 * $scale),
            [single](124.84 * $scale),
            [single](128 * $scale),
            [single](238.6 * $scale))

        $accentDiameter = [single][Math]::Max(2.5, 14 * $scale)
        $accentX = [single]((226 * $scale) - ($accentDiameter / 2))
        $accentY = [single]((40 * $scale) - ($accentDiameter / 2))
        $graphics.FillEllipse($accentBrush, $accentX, $accentY, $accentDiameter, $accentDiameter)

        $pngPath = Join-Path $outputDirectory "SandboxStudio-$size.png"
        $bitmap.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $frames.Add([pscustomobject]@{
            Size = $size
            Bytes = New-IcoFrame -Bitmap $bitmap
        })
    }
    finally {
        $openingOutline.Dispose()
        $outline.Dispose()
        $accentBrush.Dispose()
        $openingBrush.Dispose()
        $topBrush.Dispose()
        $rightBrush.Dispose()
        $leftBrush.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$iconStream = [System.IO.MemoryStream]::new()
$iconWriter = [System.IO.BinaryWriter]::new($iconStream)
$iconWriter.Write([System.UInt16]0)
$iconWriter.Write([System.UInt16]1)
$iconWriter.Write([System.UInt16]$frames.Count)
$offset = [System.UInt32](6 + 16 * $frames.Count)

foreach ($frame in $frames) {
    $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
    $iconWriter.Write([byte]$dimension)
    $iconWriter.Write([byte]$dimension)
    $iconWriter.Write([byte]0)
    $iconWriter.Write([byte]0)
    $iconWriter.Write([System.UInt16]1)
    $iconWriter.Write([System.UInt16]32)
    $iconWriter.Write([System.UInt32]$frame.Bytes.Length)
    $iconWriter.Write($offset)
    $offset = [System.UInt32]($offset + $frame.Bytes.Length)
}

foreach ($frame in $frames) {
    $iconWriter.Write([byte[]]$frame.Bytes)
}

$iconWriter.Flush()
$iconBytes = $iconStream.ToArray()
$iconWriter.Dispose()
$iconStream.Dispose()
[System.IO.File]::WriteAllBytes((Join-Path $outputDirectory 'SandboxStudio.ico'), $iconBytes)

Write-Output "Generated PNG and multi-size ICO assets in $outputDirectory"
