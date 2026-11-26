[CmdletBinding()]
param(
    # Mod folder names. Default: every project with a matching .csproj, plus Modpack.
    [string[]] $Mods = @(),

    [string] $DistDir
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'zip-archive.ps1')

$root = Split-Path -Parent $PSScriptRoot
if (-not $DistDir) { $DistDir = Join-Path $root 'dist' }
$macDir = Join-Path $DistDir 'macos'
$stageRoot = Join-Path $DistDir '_macstage'

$packageAll = $Mods.Count -eq 0
if ($packageAll) {
    $Mods = @(Get-ChildItem -Path $root -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName ($_.Name + '.csproj')) } |
        ForEach-Object { $_.Name })
}

if (Test-Path $stageRoot) { Remove-Item $stageRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $macDir, $stageRoot | Out-Null

function Get-ProjectVersion([string] $project) {
    [xml] $xml = Get-Content -LiteralPath $project
    $node = $xml.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1
    if ($node) { [string] $node.Version } else { '0.0.0' }
}

function New-MacReadme([string] $mod, [string] $version, [string] $extra) {
    @"
$mod $version for macOS
$('=' * ($mod.Length + $version.Length + 12))

This is the macOS package of $mod for Melvor Idle 2. The mod is a net10.0 class
library and the DLL is the same one the Windows package uses; no separate
per-architecture build is needed.

INSTALL
1. Install MacOS Mod Loader for macOS first (the loader package with the
   Install Melvor Mod Loader.app and the data_MelvorModLoader_macos_* folders).
2. Extract this zip into:
       ~/Library/Application Support/MelvorModLoader
   The zip contains a mods folder, so it ends up in:
       ~/Library/Application Support/MelvorModLoader/mods/$mod
3. Restart Melvor Idle 2. Open the loader's Mods menu (top bar, or F1) to find
   the mod and its key.

$extra
UNINSTALL
Delete:
    ~/Library/Application Support/MelvorModLoader/mods/$mod
Nothing is written to your save.
"@
}

$packages = @()

foreach ($mod in $Mods) {
    $project = Join-Path $root (Join-Path $mod ($mod + '.csproj'))
    if (-not (Test-Path $project)) {
        Write-Warning "Skipping $mod (no .csproj)"
        continue
    }

    $version = Get-ProjectVersion $project
    $baseZip = Join-Path $DistDir ("{0}-{1}.zip" -f $mod, $version)
    if (-not (Test-Path $baseZip)) {
        Write-Host "Building $mod $version first ..."
        & (Join-Path $PSScriptRoot 'package.ps1') -Mods $mod
        if ($LASTEXITCODE -ne 0) { throw "package.ps1 failed for $mod" }
    }
    if (-not (Test-Path $baseZip)) { throw "Base zip not found: $baseZip" }

    $stage = Join-Path $stageRoot $mod
    New-Item -ItemType Directory -Force -Path $stage | Out-Null
    [IO.Compression.ZipFile]::ExtractToDirectory($baseZip, $stage)

    $dll = Join-Path $stage ("mods\{0}\{0}.dll" -f $mod)
    if (-not (Test-Path $dll)) { throw "Base zip does not contain mods\$mod\$mod.dll: $baseZip" }

    New-MacReadme -mod $mod -version $version -extra '' |
        Set-Content -LiteralPath (Join-Path $stage 'README-MACOS.txt') -Encoding UTF8

    $zip = Join-Path $macDir ("{0}-{1}-macos.zip" -f $mod, $version)
    New-CleanZip -SourceDir $stage -ZipPath $zip

    $entries = @(Assert-ZipLayout -ZipPath $zip -AllowedPrefixes @("mods/$mod/") `
        -AllowedRootFiles @('README-MACOS.txt'))
    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    ("{0}  {1}" -f $hash, (Split-Path -Leaf $zip)) |
        Set-Content -LiteralPath ($zip + '.sha256') -Encoding ASCII

    Write-Host ("  {0} -> {1} ({2:N1} KB, {3} files)" -f $mod, (Split-Path -Leaf $zip),
        ((Get-Item -LiteralPath $zip).Length / 1KB), $entries.Count)
    $packages += [pscustomobject]@{ Mod = $mod; Version = $version; Zip = $zip; Sha256 = $hash }
}

# The modpack has its own version and one big mods/ tree.
$modpackVersionFile = Join-Path (Join-Path $root 'Modpack') 'version.txt'
if ((Test-Path $modpackVersionFile) -and ($packageAll -or $Mods -contains 'Modpack')) {
    $version = (Get-Content -LiteralPath $modpackVersionFile -Raw).Trim()
    $baseZip = Join-Path $DistDir ("3am-Modpack-{0}.zip" -f $version)
    if (-not (Test-Path $baseZip)) { throw "Base modpack zip not found: $baseZip" }

    $stage = Join-Path $stageRoot 'Modpack'
    New-Item -ItemType Directory -Force -Path $stage | Out-Null
    [IO.Compression.ZipFile]::ExtractToDirectory($baseZip, $stage)

    New-MacReadme -mod '3am Modpack' -version $version -extra @"
This pack contains every 3am mod. Seed Return Lite and Max are optional
alternatives; install only one Seed Return version at a time.
"@ | Set-Content -LiteralPath (Join-Path $stage 'README-MACOS.txt') -Encoding UTF8

    $zip = Join-Path $macDir ("3am-Modpack-{0}-macos.zip" -f $version)
    New-CleanZip -SourceDir $stage -ZipPath $zip
    $entries = @(Assert-ZipLayout -ZipPath $zip -AllowedPrefixes @('mods/', 'optional-mods/') `
        -AllowedRootFiles @('README-MACOS.txt', '3am-Modpack-README.txt', '3am-Modpack-versions.txt'))
    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    ("{0}  {1}" -f $hash, (Split-Path -Leaf $zip)) |
        Set-Content -LiteralPath ($zip + '.sha256') -Encoding ASCII

    Write-Host ("  Modpack -> {0} ({1:N1} KB, {2} files)" -f (Split-Path -Leaf $zip),
        ((Get-Item -LiteralPath $zip).Length / 1KB), $entries.Count)
    $packages += [pscustomobject]@{ Mod = 'Modpack'; Version = $version; Zip = $zip; Sha256 = $hash }
}

Remove-Item $stageRoot -Recurse -Force

Write-Host ""
Write-Host ("Packaged {0} macOS mod(s) into {1}" -f $packages.Count, $macDir)
$packages | Format-Table Mod, Version, Sha256 -AutoSize
