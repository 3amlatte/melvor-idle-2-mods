[CmdletBinding()]
param(
    # Override Modpack\version.txt.
    [string] $Version,

    # Where the game is installed. Default: GameDir from Directory.Build.props.
    [string] $GameDir
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'zip-archive.ps1')

$root = Split-Path -Parent $PSScriptRoot
$distDir = Join-Path $root 'dist'
$modpackDir = Join-Path $root 'Modpack'
$stageRoot = Join-Path $distDir 'modpack-stage'

if (-not $Version) {
    $versionFile = Join-Path $modpackDir 'version.txt'
    if (-not (Test-Path $versionFile)) { throw "Version file not found: $versionFile" }
    $Version = (Get-Content $versionFile -Raw).Trim()
}
if ([string]::IsNullOrWhiteSpace($Version)) { throw 'Modpack version is empty.' }

$optionalPath = Join-Path $modpackDir 'optional.txt'
$optional = @()
if (Test-Path $optionalPath) {
    $optional = @(Get-Content $optionalPath |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ -and -not $_.StartsWith('#') })
}

$excludePath = Join-Path $modpackDir 'exclude.txt'
$excluded = @()
if (Test-Path $excludePath) {
    $excluded = @(Get-Content $excludePath |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ -and -not $_.StartsWith('#') })
}

$allProjects = @(Get-ChildItem -Path $root -Directory |
    Where-Object { Test-Path (Join-Path $_.FullName ($_.Name + '.csproj')) } |
    ForEach-Object { $_.Name } |
    Sort-Object)

if ($allProjects.Count -eq 0) { throw "No mod projects found under $root." }

$unknownOptional = @($optional | Where-Object { $allProjects -notcontains $_ })
if ($unknownOptional.Count -gt 0) {
    throw ("Modpack\optional.txt lists unknown projects: " + ($unknownOptional -join ', '))
}

$unknownExcluded = @($excluded | Where-Object { $allProjects -notcontains $_ })
if ($unknownExcluded.Count -gt 0) {
    throw ("Modpack\exclude.txt lists unknown projects: " + ($unknownExcluded -join ', '))
}

$projects = @($allProjects | Where-Object { $excluded -notcontains $_ })
if ($projects.Count -eq 0) { throw "Every project is excluded from the modpack." }

$defaultMods = @($projects | Where-Object { $optional -notcontains $_ })

function Get-ModVersion([string] $mod) {
    $project = Join-Path $root ($mod + '\' + $mod + '.csproj')
    [xml] $xml = Get-Content -Path $project
    $versionNode = $xml.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1
    if ($versionNode) { [string] $versionNode.Version } else { '0.0.0' }
}

Write-Host ("Packaging {0} mod(s): {1} default, {2} optional, {3} excluded ..." -f $projects.Count, $defaultMods.Count, $optional.Count, $excluded.Count)

$packageArgs = @{ Mods = $projects }
if ($GameDir) { $packageArgs.GameDir = $GameDir }
& (Join-Path $PSScriptRoot 'package.ps1') @packageArgs

if (Test-Path $stageRoot) { Remove-Item $stageRoot -Recurse -Force }
$modsStage = Join-Path $stageRoot 'mods'
$optionalStage = Join-Path $stageRoot 'optional-mods'
New-Item -ItemType Directory -Force -Path $modsStage | Out-Null
New-Item -ItemType Directory -Force -Path $optionalStage | Out-Null

function Add-ModFromZip([string] $mod, [string] $targetRoot) {
    $version = Get-ModVersion $mod
    $zip = Join-Path $distDir ($mod + '-' + $version + '.zip')
    if (-not (Test-Path $zip)) { throw "Package not found: $zip" }

    $temp = Join-Path ([IO.Path]::GetTempPath()) ('modpack-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $temp | Out-Null
    try {
        Expand-Archive -LiteralPath $zip -DestinationPath $temp -Force
        $source = Join-Path $temp ('mods\' + $mod)
        if (-not (Test-Path $source)) { throw "Package $zip does not contain mods\$mod" }
        Move-Item -LiteralPath $source -Destination (Join-Path $targetRoot $mod)
    }
    finally {
        Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

foreach ($mod in $defaultMods) {
    Add-ModFromZip $mod $modsStage
    Write-Host ("  mods\{0} {1}" -f $mod, (Get-ModVersion $mod))
}
foreach ($mod in $optional) {
    Add-ModFromZip $mod $optionalStage
    Write-Host ("  optional-mods\{0} {1}" -f $mod, (Get-ModVersion $mod))
}

Copy-Item -LiteralPath (Join-Path $modpackDir 'README.txt') -Destination (Join-Path $stageRoot '3am-Modpack-README.txt')

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("3AM MODPACK $Version")
$lines.Add('')
$lines.Add('Installed in mods:')
foreach ($mod in $defaultMods) {
    $lines.Add(('  {0} {1}' -f $mod, (Get-ModVersion $mod)))
}
$lines.Add('')
$lines.Add('Optional in optional-mods (not installed):')
foreach ($mod in $optional) {
    $lines.Add(('  {0} {1}' -f $mod, (Get-ModVersion $mod)))
}
Set-Content -LiteralPath (Join-Path $stageRoot '3am-Modpack-versions.txt') -Value $lines -Encoding UTF8

$zip = Join-Path $distDir ('3am-Modpack-' + $Version + '.zip')
New-CleanZip -SourceDir $stageRoot -ZipPath $zip

$rootFiles = @('3am-Modpack-README.txt', '3am-Modpack-versions.txt')
$entries = @(Assert-ZipLayout -ZipPath $zip -AllowedPrefixes @('mods/', 'optional-mods/') -AllowedRootFiles $rootFiles)
foreach ($mod in $defaultMods) {
    $dllEntry = 'mods/' + $mod + '/' + $mod + '.dll'
    if ($entries -notcontains $dllEntry) { throw "Modpack $zip is missing $dllEntry." }
}
foreach ($mod in $optional) {
    $dllEntry = 'optional-mods/' + $mod + '/' + $mod + '.dll'
    if ($entries -notcontains $dllEntry) { throw "Modpack $zip is missing $dllEntry." }
}

$hash = (Get-FileHash -Path $zip -Algorithm SHA256).Hash
("{0}  {1}" -f $hash, (Split-Path -Leaf $zip)) |
    Set-Content -Path ($zip + '.sha256') -Encoding Ascii

$sizeKb = (Get-Item $zip).Length / 1KB
Remove-Item $stageRoot -Recurse -Force

Write-Host ''
Write-Host ("Modpack {0} -> {1} ({2:N1} KB)" -f $Version, $zip, $sizeKb)
Write-Host ("sha256 {0}" -f $hash)
Write-Host ("layout: {0} file(s) under mods/ and optional-mods/" -f $entries.Count)
