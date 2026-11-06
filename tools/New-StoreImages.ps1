<#
.SYNOPSIS
    Makes the Microsoft Store package's images (store/Images) and the Store listing logo and screenshots
    (store/Listing) from the DeskAuth logo and the website's screenshots.

.DESCRIPTION
    Only needed when the logo or screenshots change; the images are kept in the repository.
    Windows only (uses System.Drawing).
#>
[CmdletBinding()]
param(
    [string] $Logo,
    [string] $ScreenshotFolder,
    [string] $OutputFolder
)

$ErrorActionPreference = 'Stop'
# set here, as Windows PowerShell 5.1 has no $PSScriptRoot in parameter defaults
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
if (-not $Logo) { $Logo = Join-Path $root 'src\DeskAuth\Resources\DeskAuthLogo.png' }
if (-not $ScreenshotFolder) { $ScreenshotFolder = Join-Path $root 'docs\images\screenshots' }
if (-not $OutputFolder) { $OutputFolder = Join-Path $root 'store' }
Add-Type -AssemblyName System.Drawing

function New-Canvas([int] $Width, [int] $Height) {
    $bitmap = New-Object System.Drawing.Bitmap $Width, $Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    return $bitmap, $graphics
}

# The logo, scaled to $Size and centred on a transparent $Width x $Height image
function Save-Logo([string] $Path, [int] $Width, [int] $Height, [int] $Size) {
    $bitmap, $graphics = New-Canvas $Width $Height
    try {
        $graphics.DrawImage($script:source, [int](($Width - $Size) / 2), [int](($Height - $Size) / 2), $Size, $Size)
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$images = Join-Path $OutputFolder 'Images'
New-Item -ItemType Directory -Force $images | Out-Null
Get-ChildItem $images -Filter *.png | Remove-Item

$script:source = [System.Drawing.Image]::FromFile((Resolve-Path $Logo))
try {
    # Tiles and Start menu icons, at 100% and 200% display scaling. The Store logo and tiles have some space
    # around the logo, as Windows' tile guidelines suggest.
    foreach ($scale in 100, 200) {
        $f = $scale / 100
        Save-Logo (Join-Path $images "StoreLogo.scale-$scale.png") (50 * $f) (50 * $f) (50 * $f)
        Save-Logo (Join-Path $images "Square150x150Logo.scale-$scale.png") (150 * $f) (150 * $f) (100 * $f)
        Save-Logo (Join-Path $images "Square44x44Logo.scale-$scale.png") (44 * $f) (44 * $f) (44 * $f)
    }
    # Taskbar, title bar and file icons. "unplated" versions are shown without a coloured square behind them.
    foreach ($size in 16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256) {
        Save-Logo (Join-Path $images "Square44x44Logo.targetsize-$size.png") $size $size $size
        Save-Logo (Join-Path $images "Square44x44Logo.targetsize-${size}_altform-unplated.png") $size $size $size
    }

    # Store listing logo (1:1 box art, 300 x 300 or larger)
    $listing = Join-Path $OutputFolder 'Listing'
    New-Item -ItemType Directory -Force $listing | Out-Null
    Save-Logo (Join-Path $listing 'Logo-300.png') 300 300 300
}
finally {
    $script:source.Dispose()
}

# Store listing screenshots: the website's screenshots on a 1920 x 1080 background (the Store needs at
# least 1366 x 768)
$screens = Join-Path $OutputFolder 'Listing'
foreach ($theme in @(@{ Suffix = '' }, @{ Suffix = '-dark' })) {
    foreach ($name in 'main', 'add', 'options', 'hotkey', 'protection') {
        $file = Join-Path $ScreenshotFolder "$name$($theme.Suffix).png"
        if (-not (Test-Path $file)) { continue }
        $shot = [System.Drawing.Image]::FromFile((Resolve-Path $file))
        $bitmap, $graphics = New-Canvas 1920 1080
        try {
            # extend the screenshot's own background
            $graphics.Clear(([System.Drawing.Bitmap] $shot).GetPixel(0, 0))
            # fit within 1000 pixels high, never enlarged
            $scale = [Math]::Min(1.0, [Math]::Min(1000 / $shot.Height, 1800 / $shot.Width))
            $w = [int]($shot.Width * $scale)
            $h = [int]($shot.Height * $scale)
            $graphics.DrawImage($shot, [int]((1920 - $w) / 2), [int]((1080 - $h) / 2), $w, $h)
            $bitmap.Save((Join-Path $screens "Screenshot-$name$($theme.Suffix).png"), [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $graphics.Dispose()
            $bitmap.Dispose()
            $shot.Dispose()
        }
    }
}

Get-ChildItem $images, $screens -File | Select-Object Name, Length
