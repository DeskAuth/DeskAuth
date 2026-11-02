<#
.SYNOPSIS
Sets the DeskAuth version shown and linked on the website (docs/*.html).

.DESCRIPTION
Updates, in every page of the website:
  * download links:  .../releases/download/v4.1.0/DeskAuth-4.1.0-x64.exe
  * release links:   .../releases/tag/v4.1.0
  * version text:    any element with a data-version attribute, such as <span data-version>4.1.0</span>

Run by the Website version workflow after each release, with the latest published release.

.EXAMPLE
./tools/Update-WebsiteVersion.ps1 -Version 4.2.0
#>
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [string]$Docs = (Join-Path (Split-Path $PSScriptRoot -Parent) 'docs')
)

$ErrorActionPreference = 'Stop'
$ver = '\d+\.\d+\.\d+'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$changed = @()

foreach ($file in Get-ChildItem $Docs -Filter *.html -File) {
    $text = [IO.File]::ReadAllText($file.FullName)
    $new = $text
    $new = $new -replace "(github\.com/DeskAuth/DeskAuth/releases/(?:download|tag)/v)$ver", "`${1}$Version"
    $new = $new -replace "(?<![\w.-])(DeskAuth-)$ver(-[A-Za-z0-9-]+\.exe)", "`${1}$Version`${2}"
    $new = $new -replace "(<[a-z]+\b[^>]*\bdata-version\b[^>]*>)[^<]*(</)", "`${1}$Version`${2}"
    if ($new -cne $text) {
        [IO.File]::WriteAllText($file.FullName, $new, $utf8)
        $changed += $file.Name
    }
}

if ($changed) {
    "Set the website to DeskAuth $Version in: $($changed -join ', ')"
} else {
    "The website already shows DeskAuth $Version"
}
