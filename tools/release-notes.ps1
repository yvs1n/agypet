# Generates GitHub Release notes from CHANGELOG.md and appends SHA-256 checksums for Windows, macOS, and Linux assets.
param(
    [Parameter(Mandatory = $true)][string]$Tag,
    [Parameter(Mandatory = $true)][string[]]$Assets,
    [Parameter(Mandatory = $true)][string]$Out
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$version = $Tag.TrimStart('v')

$section = New-Object System.Collections.Generic.List[string]
$inside = $false
foreach ($line in Get-Content (Join-Path $root 'CHANGELOG.md') -Encoding UTF8) {
    if ($line -match '^## ') {
        if ($inside) { break }
        $inside = $line -match "^## $([regex]::Escape($version))( |$)"
        continue
    }
    if ($inside) { $section.Add($line) }
}
$notes = ($section -join "`n").Trim()
if ($notes -eq '') {
    $notes = "Latest build of AgyPet ($Tag)."
}

$sums = New-Object System.Collections.Generic.List[string]
foreach ($a in $Assets) {
    if (-not (Test-Path $a)) { continue }
    $hash = (Get-FileHash $a -Algorithm SHA256).Hash.ToLower()
    $name = [IO.Path]::GetFileName($a)
    $sums.Add("$hash  $name")
}
$checksumBlock = $sums -join "`n"

$text = @"
$notes

## One-File Install & Play

- **Windows**: Download ``AgyPet.exe`` and run it (no runtime or installer needed; if SmartScreen prompts on an unsigned binary, choose **More info -> Run anyway**).
- **macOS (Apple Silicon & Intel Universal)**: Download ``AgyPet-macOS.dmg``, open it, and drag ``AgyPet.app`` to Applications or double-click to run directly.
- **Linux (`x64` / `arm64`)**: Download ``AgyPet-linux-x64`` (or ``AgyPet-linux-arm64``), make it executable (`chmod +x AgyPet-linux-x64`), and run `./AgyPet-linux-x64` (zero extra dependencies or FUSE required).

## Verify Checksums (SHA-256)

``````text
$checksumBlock
``````

Built by GitHub Actions from ``$Tag``. Credits: [Yassin Ragab](https://yassinr.me) · Built for & with [Google Antigravity](https://antigravity.google).
"@
[IO.File]::WriteAllText($Out, ($text -replace "`r`n", "`n") + "`n", (New-Object Text.UTF8Encoding($false)))
Write-Host "Wrote $Out"
