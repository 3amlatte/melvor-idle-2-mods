# Melvor Idle 2 save helper (Windows).
#
# Steam Cloud sometimes fails to upload the .sav on exit (Steam logs
# "failed to issue" to steamcloud-sydney.storage.googleapis.com) and the next
# launch can look like a sync conflict. This script keeps local copies and can
# put one back.
#
#   .\melvor-saves.ps1 -Status      Show local saves vs the cloud cache (default)
#   .\melvor-saves.ps1 -Backup      Copy saves to Documents\Melvor Idle 2 Backups
#   .\melvor-saves.ps1 -Restore     Restore the newest backup (game must be closed)
#   .\melvor-saves.ps1 -CleanTemp   Delete .tmp files left by a killed game
#   .\melvor-saves.ps1 -Launch      Start the game through Steam
#
# Saves live in:
#   <Steam>\userdata\<account>\3218350\remote\saves

[CmdletBinding()]
param(
    [switch] $Status,
    [switch] $Backup,
    [switch] $Restore,
    [switch] $CleanTemp,
    [switch] $Launch,
    [string] $From
)

$ErrorActionPreference = 'Stop'
$AppId = '3218350'
$GameName = 'Melvor Idle 2'

function Get-SteamPath {
    $value = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
    if ($value) { return $value.Replace('/', '\') }
    foreach ($candidate in @('C:\Program Files (x86)\Steam', 'C:\Program Files\Steam')) {
        if (Test-Path $candidate) { return $candidate }
    }
    throw 'Steam folder not found.'
}

$steam = Get-SteamPath
$remote = $null
foreach ($account in Get-ChildItem (Join-Path $steam 'userdata') -Directory -ErrorAction SilentlyContinue) {
    $candidate = Join-Path $account.FullName (Join-Path $AppId 'remote')
    if (Test-Path $candidate) { $remote = $candidate; break }
}
if (-not $remote) { throw "No $GameName save folder found under $steam\userdata." }

$saves = Join-Path $remote 'saves'
$backupRoot = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Melvor Idle 2 Backups'

function Write-Head([string] $text) {
    Write-Host ''
    Write-Host $text -ForegroundColor DarkYellow
    Write-Host ('-' * $text.Length) -ForegroundColor DarkGray
}

function Show-Status {
    Write-Host "$GameName saves: $saves"
    $cache = Join-Path (Split-Path $remote -Parent) 'remotecache.vdf'
    if (Test-Path $cache) {
        $change = (Select-String -Path $cache -Pattern '"ChangeNumber"\s+"(\d+)"' |
            Select-Object -First 1).Matches.Groups[1].Value
        Write-Host "Steam cloud cache change number: $change"
    }

    foreach ($file in Get-ChildItem $saves -File -ErrorAction SilentlyContinue | Sort-Object Name) {
        $state = 'unknown'
        if (Test-Path $cache) {
            $key = 'saves/' + $file.Name
            $block = [regex]::Match((Get-Content $cache -Raw),
                [regex]::Escape($key) + '"\s*\{(?<body>.*?)\}', 'Singleline')
            if ($block.Success) {
                $local = [regex]::Match($block.Groups['body'].Value, '"localtime"\s+"(\d+)"')
                $cloud = [regex]::Match($block.Groups['body'].Value, '"time"\s+"(\d+)"')
                if ($local.Success -and $cloud.Success) {
                    $state = if ([long]$local.Groups[1].Value -gt [long]$cloud.Groups[1].Value) {
                        'local newer - upload pending'
                    } else { 'in sync' }
                }
            }
        }
        Write-Host ("  {0,-52} {1,10} bytes  {2:yyyy-MM-dd HH:mm}  {3}" -f
            $file.Name, $file.Length, $file.LastWriteTime, $state)
    }

    $temps = @(Get-ChildItem $saves -Filter '*.tmp' -ErrorAction SilentlyContinue)
    if ($temps.Count -gt 0) {
        Write-Host ("  {0} leftover .tmp file(s); run -CleanTemp" -f $temps.Count) -ForegroundColor Yellow
    }

    if (Test-Path $backupRoot) {
        $latest = Get-ChildItem $backupRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1
        if ($latest) { Write-Host "Newest local backup: $($latest.FullName)" }
    }
}

function Invoke-Backup {
    $stamp = Get-Date -Format 'yyyy-MM-dd_HH-mm-ss'
    $target = Join-Path $backupRoot $stamp
    New-Item -ItemType Directory -Force -Path (Join-Path $target 'saves') | Out-Null
    Copy-Item (Join-Path $saves '*') -Destination (Join-Path $target 'saves') -Force -ErrorAction SilentlyContinue
    Copy-Item (Join-Path $remote 'userSettings.json') -Destination $target -Force -ErrorAction SilentlyContinue

    # Keep the newest 20 backups.
    Get-ChildItem $backupRoot -Directory | Sort-Object Name -Descending |
        Select-Object -Skip 20 | ForEach-Object { Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }

    Write-Host "Backed up to $target" -ForegroundColor Green
}

function Invoke-Restore {
    if (-not $From) {
        $From = (Get-ChildItem $backupRoot -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending | Select-Object -First 1).FullName
    }
    if (-not $From -or -not (Test-Path $From)) { throw 'No backup folder found. Use -From <folder>.' }

    $source = if (Test-Path (Join-Path $From 'saves')) { Join-Path $From 'saves' } else { $From }
    $files = @(Get-ChildItem $source -Include '*.sav', '*.bak' -File -ErrorAction SilentlyContinue)
    if ($files.Count -eq 0) { throw "No save files in $source." }

    if (Get-Process -Name Melvor2 -ErrorAction SilentlyContinue) {
        throw 'Close Melvor Idle 2 before restoring a save.'
    }

    foreach ($file in $files) {
        Copy-Item $file.FullName -Destination $saves -Force
    }
    Write-Host "Restored $($files.Count) file(s) from $source" -ForegroundColor Green
    Write-Host 'If Steam asks about cloud files on the next launch, choose "Upload local files".'
}

function Invoke-CleanTemp {
    $removed = 0
    foreach ($file in Get-ChildItem $saves -Filter '*.tmp' -ErrorAction SilentlyContinue) {
        Remove-Item $file.FullName -Force -ErrorAction SilentlyContinue
        $removed++
    }
    Write-Host "Removed $removed leftover .tmp file(s)." -ForegroundColor Green
}

if (-not ($Status -or $Backup -or $Restore -or $CleanTemp -or $Launch)) { $Status = $true }

if ($Status) { Show-Status }
if ($Backup) { Invoke-Backup }
if ($Restore) { Invoke-Restore }
if ($CleanTemp) { Invoke-CleanTemp }
if ($Launch) { Start-Process "steam://rungameid/$AppId"; Write-Host "Starting $GameName through Steam..." }
