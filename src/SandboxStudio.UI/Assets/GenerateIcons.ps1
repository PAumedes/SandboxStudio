param()

# Renders the SandboxStudio mark (see SandboxStudio.svg) into every raster asset:
# the window/exe icons (SandboxStudio-<size>.png, SandboxStudio.ico) and the
# MSIX tile, logo and splash images under Package\.

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$outputDirectory = $PSScriptRoot
$packageDirectory = Join-Path $PSScriptRoot 'Package'
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = [System.Collections.Generic.List[object]]::new()

function New-ScaledPoints {
    param(
        [double[]]$Coordinates,
        [double]$Scale,
        [double]$OffsetX = 0,
        [double]$OffsetY = 0
    )

    $points = [System.Collections.Generic.List[System.Drawing.PointF]]::new()
    for ($index = 0; $index -lt $Coordinates.Length; $index += 2) {
        $points.Add([System.Drawing.PointF]::new(
            [single]($OffsetX + $Coordinates[$index] * $Scale),
            [single]($OffsetY + $Coordinates[$index + 1] * $Scale)))
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

# Draws the mark into a $Size x $Size square whose top-left corner is ($X, $Y).
function Add-Mark {
    param(
        [System.Drawing.Graphics]$Graphics,
        [double]$X,
        [double]$Y,
        [double]$Size
    )

    $scale = $Size / 256.0
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
        $left = New-ScaledPoints -Scale $scale -OffsetX $X -OffsetY $Y -Coordinates @(26.88, 69.54, 128, 124.84, 128, 238.6, 26.88, 183.3)
        $right = New-ScaledPoints -Scale $scale -OffsetX $X -OffsetY $Y -Coordinates @(128, 124.84, 229.12, 69.54, 229.12, 183.3, 128, 238.6)
        $top = New-ScaledPoints -Scale $scale -OffsetX $X -OffsetY $Y -Coordinates @(26.88, 69.54, 128, 14.24, 229.12, 69.54, 128, 124.84)
        $opening = New-ScaledPoints -Scale $scale -OffsetX $X -OffsetY $Y -Coordinates @(69.54, 69.54, 128, 36.36, 186.46, 69.54, 128, 102.72)

        $Graphics.FillPolygon($leftBrush, $left)
        $Graphics.FillPolygon($rightBrush, $right)
        $Graphics.FillPolygon($topBrush, $top)
        $Graphics.FillPolygon($openingBrush, $opening)
        foreach ($pen in $outline, $openingOutline) {
            $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
            $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
            $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        }
        $Graphics.DrawPolygon($outline, $left)
        $Graphics.DrawPolygon($outline, $right)
        $Graphics.DrawPolygon($outline, $top)
        $Graphics.DrawPolygon($openingOutline, $opening)
        $Graphics.DrawLine(
            $outline,
            [single]($X + 128 * $scale),
            [single]($Y + 124.84 * $scale),
            [single]($X + 128 * $scale),
            [single]($Y + 238.6 * $scale))

        $accentDiameter = [single][Math]::Max(2.5, 14 * $scale)
        $accentX = [single](($X + 226 * $scale) - ($accentDiameter / 2))
        $accentY = [single](($Y + 40 * $scale) - ($accentDiameter / 2))
        $Graphics.FillEllipse($accentBrush, $accentX, $accentY, $accentDiameter, $accentDiameter)
    }
    finally {
        $openingOutline.Dispose()
        $outline.Dispose()
        $accentBrush.Dispose()
        $openingBrush.Dispose()
        $topBrush.Dispose()
        $rightBrush.Dispose()
        $leftBrush.Dispose()
    }
}

# Renders the mark centred on a transparent $Width x $Height canvas, sized to
# $MarkRatio of the shorter side (the rest is the platform-recommended padding).
function New-MarkBitmap {
    param(
        [int]$Width,
        [int]$Height,
        [double]$MarkRatio = 1.0
    )

    $bitmap = [System.Drawing.Bitmap]::new(
        $Width,
        $Height,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $markSize = [Math]::Min($Width, $Height) * $MarkRatio
        Add-Mark -Graphics $graphics -X (($Width - $markSize) / 2) -Y (($Height - $markSize) / 2) -Size $markSize
    }
    finally {
        $graphics.Dispose()
    }

    return $bitmap
}

foreach ($size in $sizes) {
    $bitmap = New-MarkBitmap -Width $size -Height $size
    try {
        $bitmap.Save((Join-Path $outputDirectory "SandboxStudio-$size.png"), [System.Drawing.Imaging.ImageFormat]::Png)
        $frames.Add([pscustomobject]@{
            Size = $size
            Bytes = New-IcoFrame -Bitmap $bitmap
        })
    }
    finally {
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

# MSIX visual assets. Scale variants follow the 100/125/150/200/400 plateaus;
# target-size variants feed the taskbar, Start list and Explorer, and the
# altform-unplated copies keep the mark on a transparent background there.
New-Item -ItemType Directory -Force -Path $packageDirectory | Out-Null
$packageAssets = [System.Collections.Generic.List[object]]::new()
foreach ($scale in 100, 125, 150, 200, 400) {
    $factor = $scale / 100.0
    $packageAssets.Add(@("Square44x44Logo.scale-$scale.png", [int](44 * $factor), [int](44 * $factor), 1.0))
    $packageAssets.Add(@("Square71x71Logo.scale-$scale.png", [int](71 * $factor), [int](71 * $factor), 0.66))
    $packageAssets.Add(@("Square150x150Logo.scale-$scale.png", [int](150 * $factor), [int](150 * $factor), 0.66))
    $packageAssets.Add(@("Wide310x150Logo.scale-$scale.png", [int](310 * $factor), [int](150 * $factor), 0.66))
    $packageAssets.Add(@("Square310x310Logo.scale-$scale.png", [int](310 * $factor), [int](310 * $factor), 0.5))
    $packageAssets.Add(@("StoreLogo.scale-$scale.png", [int](50 * $factor), [int](50 * $factor), 1.0))
    $packageAssets.Add(@("SplashScreen.scale-$scale.png", [int](620 * $factor), [int](300 * $factor), 0.5))
}
foreach ($target in 16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256) {
    foreach ($suffix in '', '_altform-unplated', '_altform-lightunplated') {
        $packageAssets.Add(@("Square44x44Logo.targetsize-$target$suffix.png", $target, $target, 1.0))
    }
}

foreach ($asset in $packageAssets) {
    $bitmap = New-MarkBitmap -Width $asset[1] -Height $asset[2] -MarkRatio $asset[3]
    try {
        $bitmap.Save((Join-Path $packageDirectory $asset[0]), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }
}

Write-Output "Generated PNG, multi-size ICO and $($packageAssets.Count) MSIX assets in $outputDirectory"
