<#
.SYNOPSIS
    Builds the Microsoft Store package of DeskAuth: an .msixupload file to upload in Partner Center.

.DESCRIPTION
    Publishes DeskAuth (including .NET) for each processor type with -p:DeskAuthStore=true, makes an MSIX
    package of each from store/AppxManifest.xml and store/Images, and bundles them. The package isn't signed:
    the Microsoft Store signs it.

    Needs the Windows SDK (makeappx.exe and makepri.exe), which GitHub's Windows runners and Visual Studio
    include.

    To try the package on this PC before uploading it, use -Install. It registers the x64 build without
    signing it, which needs Windows' Developer Mode (Settings, System, For developers).

.EXAMPLE
    tools/Build-StorePackage.ps1 -Version 4.3.0 -IdentityName 12345DeskAuth.DeskAuth `
        -Publisher 'CN=01234567-89AB-CDEF-0123-456789ABCDEF' -PublisherDisplayName 'Nick Morphis'
#>
[CmdletBinding()]
param(
    # Release version, such as 4.3.0. The package version is 4.3.0.0.
    [Parameter(Mandatory)] [string] $Version,

    # From Partner Center: Product management, Product identity
    [string] $IdentityName = 'DeskAuth.Test',
    [string] $Publisher = 'CN=DeskAuth Test',
    [string] $PublisherDisplayName = 'DeskAuth',

    [string[]] $Architectures = @('x64', 'arm64', 'x86'),
    [string] $OutputFolder = 'store-package',

    # Register the x64 layout on this PC (Developer Mode), for testing
    [switch] $Install
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

# 4.3.0 or v4.3.0-beta.1 -> 4.3.0.0. The Store needs the last part to be 0.
$core = ($Version -replace '^[vV]', '') -replace '[-+].*$', ''
if ($core -notmatch '^\d+\.\d+\.\d+$') { throw "'$Version' is not a version such as 4.3.0" }
$packageVersion = "$core.0"

function Find-SdkTool([string] $Name) {
    $kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    $tool = Get-ChildItem $kits -Directory -Filter '10.*' -ErrorAction SilentlyContinue |
        Sort-Object { [Version] $_.Name } -Descending |
        ForEach-Object { Join-Path $_.FullName "x64\$Name" } |
        Where-Object { Test-Path $_ } |
        Select-Object -First 1
    if (-not $tool) { throw "$Name was not found. Install the Windows SDK." }
    return $tool
}

function Invoke-Tool([string] $Tool, [string[]] $Arguments) {
    & $Tool @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$(Split-Path -Leaf $Tool) failed with exit code $LASTEXITCODE" }
}

$makeappx = Find-SdkTool 'makeappx.exe'
$makepri = Find-SdkTool 'makepri.exe'

if ($Install) {
    # remove the copy registered by an earlier run, as its files are about to be replaced
    Get-AppxPackage -Name $IdentityName | Remove-AppxPackage
}

$output = New-Item -ItemType Directory -Force (Join-Path $root $OutputFolder)
$work = Join-Path $output.FullName 'work'
if (Test-Path $work) { Remove-Item -Recurse -Force $work }
New-Item -ItemType Directory $work | Out-Null

$template = Get-Content -Raw (Join-Path $root 'store\AppxManifest.xml')
$packages = New-Item -ItemType Directory (Join-Path $work 'packages')

foreach ($arch in $Architectures) {
    Write-Host "Building $arch"
    $layout = Join-Path $work "layout-$arch"

    dotnet publish (Join-Path $root 'src\DeskAuth') -c Release -r "win-$arch" --self-contained true `
        -p:DeskAuthStore=true -p:Version=$core -p:DebugType=none -p:SatelliteResourceLanguages=en -o $layout
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $arch" }

    Copy-Item -Recurse (Join-Path $root 'store\Images') (Join-Path $layout 'Images')

    $manifest = $template.
        Replace('$IDENTITY_NAME$', [Security.SecurityElement]::Escape($IdentityName)).
        Replace('$PUBLISHER$', [Security.SecurityElement]::Escape($Publisher)).
        Replace('$PUBLISHER_DISPLAY_NAME$', [Security.SecurityElement]::Escape($PublisherDisplayName)).
        Replace('$VERSION$', $packageVersion).
        Replace('$ARCHITECTURE$', $arch)
    [IO.File]::WriteAllText((Join-Path $layout 'AppxManifest.xml'), $manifest, (New-Object Text.UTF8Encoding $false))

    # resources.pri lets Windows pick the right image size (the .scale-NNN and .targetsize-NN files)
    $priConfig = Join-Path $work 'priconfig.xml'
    if (-not (Test-Path $priConfig)) {
        Invoke-Tool $makepri @('createconfig', '/cf', $priConfig, '/dq', 'en-US', '/pv', '10.0.0', '/o')
    }
    # index only the images and manifest, not the program files
    $priRoot = Join-Path $work "pri-$arch"
    New-Item -ItemType Directory $priRoot | Out-Null
    Copy-Item -Recurse (Join-Path $layout 'Images') (Join-Path $priRoot 'Images')
    Copy-Item (Join-Path $layout 'AppxManifest.xml') $priRoot
    Invoke-Tool $makepri @('new', '/pr', $priRoot, '/cf', $priConfig, '/mn', (Join-Path $priRoot 'AppxManifest.xml'),
        '/of', (Join-Path $layout 'resources.pri'), '/o')

    if ($Install -and $arch -eq 'x64') {
        Write-Host 'Registering the x64 layout on this PC'
        Add-AppxPackage -Register (Join-Path $layout 'AppxManifest.xml')
    }

    Invoke-Tool $makeappx @('pack', '/d', $layout, '/p', (Join-Path $packages.FullName "DeskAuth_${packageVersion}_$arch.msix"), '/o')
}

# One bundle with every processor type, wrapped in the .msixupload file that Partner Center takes
$bundle = Join-Path $work "DeskAuth_$packageVersion.msixbundle"
Invoke-Tool $makeappx @('bundle', '/d', $packages.FullName, '/p', $bundle, '/bv', $packageVersion, '/o')

$upload = Join-Path $output.FullName "DeskAuth_$packageVersion.msixupload"
if (Test-Path $upload) { Remove-Item $upload }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::Open($upload, 'Create')
try {
    [void] [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $bundle, (Split-Path -Leaf $bundle))
}
finally {
    $zip.Dispose()
}

Write-Host "Made $upload"
Get-Item $upload
