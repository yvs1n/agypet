# Builds bin\AgyPet.exe using Windows' built-in .NET Framework 4.x C# compiler (zero dependencies).
# Usage:
#   .\build.ps1           -> builds bin\AgyPet.exe
#   .\build.ps1 -Test     -> builds and runs unit tests
#   .\build.ps1 -Showcase -> renders docs\showcase-classic.png & docs\showcase-dark.png
param([switch]$Test, [switch]$Showcase)

$ErrorActionPreference = 'Stop'

$fw  = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
$wpf = "$fw\WPF"
$out = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force $out | Out-Null

$refs = @(
    "$wpf\PresentationFramework.dll",
    "$wpf\PresentationCore.dll",
    "$wpf\WindowsBase.dll",
    "$wpf\UIAutomationClient.dll",
    "$wpf\UIAutomationTypes.dll",
    "$fw\System.Xaml.dll",
    "$fw\System.Core.dll",
    "$fw\System.Drawing.dll",
    "$fw\System.Windows.Forms.dll",
    "$fw\System.Web.Extensions.dll"
) | ForEach-Object { "/r:$_" }

$sources = @(Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter *.cs -Recurse | ForEach-Object FullName)

if ($Test) {
    $tests = @(Get-ChildItem (Join-Path $PSScriptRoot 'tests') -Filter *.cs | ForEach-Object FullName)
    & "$fw\csc.exe" /nologo /codepage:65001 /target:exe /main:AgyPet.Tests.AgyPetTests "/out:$out\AgyPet.Tests.exe" @refs @sources @tests
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed' }
    & "$out\AgyPet.Tests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
    return
}

if ($Showcase) {
    $docs = Join-Path $PSScriptRoot 'docs'
    New-Item -ItemType Directory -Force $docs | Out-Null
    & "$fw\csc.exe" /nologo /codepage:65001 /target:exe /main:AgyPet.Tools.Showcase "/out:$out\AgyPet.Showcase.exe" @refs @sources (Join-Path $PSScriptRoot 'tools\Showcase.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Showcase build failed' }
    & "$out\AgyPet.Showcase.exe" $docs
    if ($LASTEXITCODE -ne 0) { throw 'Showcase render failed' }
    return
}

# Generate multi-size ICO in official Antigravity colors
Add-Type -AssemblyName System.Drawing
$archMask = @(
    '......####......',
    '.....######.....',
    '....########....',
    '....########....',
    '...##########...',
    '...##########...',
    '..############..',
    '..############..',
    '..###......###..',
    '..##........##..',
    '.###........###.',
    '.##..........##.',
    '##............##',
    '#..............#'
)
$pngs = foreach ($size in 16, 24, 32, 48, 64, 128, 256) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    $px = [Math]::Max(1, [Math]::Floor($size / 16))
    $ox = [Math]::Floor(($size - 16 * $px) / 2); $oy = [Math]::Floor(($size - 14 * $px) / 2)
    for ($r = 0; $r -lt $archMask.Count; $r++) {
        $row = $archMask[$r]
        for ($c = 0; $c -lt 16; $c++) {
            if ($row[$c] -ne '#') { continue }
            $hex = if ($r -le 2) {
                if ($c -lt 7) { 0xF8C618 } elseif ($c -lt 10) { 0xFA7420 } else { 0xEF443A }
            } elseif ($r -le 6 -and $c -lt 7) { 0x4EC446 }
            elseif ($r -le 6 -and $c -ge 9) { 0x7B61EC }
            else { 0x1E78FF }
            $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb((0xFF000000 -bor $hex)))
            $g.FillRectangle($brush, [int]($ox + $c * $px), [int]($oy + $r * $px), [int]$px, [int]$px)
            $brush.Dispose()
        }
    }
    $eye = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0xFF, 0x0B, 0x12, 0x20))
    $g.FillRectangle($eye, [int]($ox + 5 * $px), [int]($oy + 5 * $px), [int]$px, [int](2 * $px))
    $g.FillRectangle($eye, [int]($ox + 10 * $px), [int]($oy + 5 * $px), [int]$px, [int](2 * $px))
    $eye.Dispose()
    $mem = New-Object IO.MemoryStream
    $bmp.Save($mem, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    , @($size, $mem.ToArray())
}
$icon = Join-Path $out 'AgyPet.ico'
$w = New-Object IO.BinaryWriter ([IO.File]::Create($icon))
$w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$pngs.Count)
$offset = 6 + 16 * $pngs.Count
foreach ($p in $pngs) {
    $b = if ($p[0] -ge 256) { [byte]0 } else { [byte]$p[0] }
    $w.Write($b); $w.Write($b); $w.Write([byte]0); $w.Write([byte]0); $w.Write([int16]1); $w.Write([int16]32)
    $w.Write([int]$p[1].Length); $w.Write([int]$offset)
    $offset += $p[1].Length
}
foreach ($p in $pngs) { $w.Write([byte[]]$p[1]) }
$w.Close()

& "$fw\csc.exe" /nologo /codepage:65001 /target:winexe /optimize "/win32icon:$icon" "/out:$out\AgyPet.exe" @refs @sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Host "Built $out\AgyPet.exe"
