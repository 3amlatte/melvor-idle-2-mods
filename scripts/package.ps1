[CmdletBinding()]
param(
    # Mod folder names to package. Default: every folder with a matching .csproj.
    [string[]] $Mods = @(),

    # Where the game is installed. Default: GameDir from Directory.Build.props.
    [string] $GameDir
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$distDir = Join-Path $root 'dist'
$stageRoot = Join-Path $distDir 'stage'

if ($Mods.Count -eq 0) {
    $Mods = @(Get-ChildItem -Path $root -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName ($_.Name + '.csproj')) } |
        ForEach-Object { $_.Name })
}

if ($Mods.Count -eq 0) { throw "No mod projects found under $root." }

if (Test-Path $stageRoot) { Remove-Item $stageRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stageRoot | Out-Null

$packages = @()

foreach ($mod in $Mods) {
    $projectDir = Join-Path $root $mod
    $project = Join-Path $projectDir ($mod + '.csproj')
    if (-not (Test-Path $project)) { throw "Project not found: $project" }

    # Version lives in the csproj so the zip name cannot drift from the assembly.
    [xml] $xml = Get-Content -Path $project
    $versionNode = $xml.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1
    $version = if ($versionNode) { $versionNode.Version } else { '0.0.0' }

    # Build straight into mods\<Mod>\ inside a staging folder, never into the live game folder:
    # packaging works while the game is running and cannot touch an installed copy.
    $stageDir = Join-Path $stageRoot $mod
    $modStage = Join-Path (Join-Path $stageDir 'mods') $mod
    New-Item -ItemType Directory -Force -Path $modStage | Out-Null

    Write-Host "Building $mod $version ..."
    $buildArgs = @(
        'build', $project, '-c', 'Release',
        ('-p:OutputPath=' + $modStage + [IO.Path]::DirectorySeparatorChar),
        '-p:AppendTargetFrameworkToOutputPath=false',
        '-p:DebugType=none',
        '-p:GenerateDependencyFile=false'
    )
    if ($GameDir) { $buildArgs += ('-p:GameDir=' + $GameDir) }

    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) { throw "Build failed for $mod (exit $LASTEXITCODE)." }

    $dll = Join-Path $modStage ($mod + '.dll')
    if (-not (Test-Path $dll)) { throw "Build did not produce $dll" }

    foreach ($file in @('README.txt', 'settings.json', '3am.mod')) {
        $from = Join-Path $projectDir $file
        if (Test-Path $from) {
            Copy-Item -Path $from -Destination (Join-Path $modStage $file)
        }
        elseif ($file -eq 'README.txt') {
            Write-Warning "$mod has no README.txt; the package will not include one."
        }
        # settings.json and 3am.mod are optional.
    }

    $zip = Join-Path $distDir ($mod + '-' + $version + '.zip')
    if (Test-Path $zip) { Remove-Item $zip -Force }

    # -Path <stage>\* keeps "mods" at the root of the zip, which is what players extract
    # into the game folder.
    Compress-Archive -Path (Join-Path $stageDir '*') -DestinationPath $zip -CompressionLevel Optimal

    $hash = (Get-FileHash -Path $zip -Algorithm SHA256).Hash
    ("{0}  {1}" -f $hash, (Split-Path -Leaf $zip)) |
        Set-Content -Path ($zip + '.sha256') -Encoding Ascii

    $sizeKb = (Get-Item $zip).Length / 1KB
    Write-Host ("  -> {0} ({1:N1} KB)" -f $zip, $sizeKb)
    Write-Host ("  sha256 {0}" -f $hash)

    $packages += [pscustomobject]@{ Mod = $mod; Version = $version; Zip = $zip; Sha256 = $hash }
}

Remove-Item $stageRoot -Recurse -Force

Write-Host ""
Write-Host ("Packaged {0} mod(s) into {1}" -f $packages.Count, $distDir)
