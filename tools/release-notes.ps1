# Generates GitHub Release notes from CHANGELOG.md and appends SHA-256 + attestation verify command.
param(
    [Parameter(Mandatory = $true)][string]$Tag,
    [Parameter(Mandatory = $true)][string]$Exe,
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

$hash = (Get-FileHash $Exe -Algorithm SHA256).Hash.ToLower()
$text = @"
$notes

## Install

Download ``AgyPet.exe`` below and run it. No installer or runtime required — AgyPet automatically checks GitHub Releases for updates.

If Windows SmartScreen warns on an unsigned binary, choose **More info** -> **Run anyway**.

## Verify Checksum

``````text
$hash  AgyPet.exe
``````

Built by GitHub Actions from ``$Tag``. Credits: [Yassin Ragab](https://yassinr.me) · Built for & with [Google Antigravity](https://antigravity.google).
"@
[IO.File]::WriteAllText($Out, ($text -replace "`r`n", "`n") + "`n", (New-Object Text.UTF8Encoding($false)))
Write-Host "Wrote $Out"
