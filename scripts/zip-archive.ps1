# Shared zip helpers.
#
# Windows PowerShell 5.1's Compress-Archive can write entry names with backslashes
# instead of the forward slashes the ZIP spec requires. Some extractors then show the
# whole path as one file name ("four loose files with the folders in the names"), which
# is exactly what players must never see. These helpers write entries ourselves, always
# with '/', and then refuse to publish an archive whose layout is wrong.

Add-Type -AssemblyName System.IO.Compression | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem | Out-Null

function ConvertTo-ZipEntryPath {
    param([Parameter(Mandatory)] [string] $RelativePath)
    ($RelativePath.TrimStart('\', '/')) -replace '\\', '/'
}

function New-CleanZip {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $SourceDir,
        [Parameter(Mandatory)] [string] $ZipPath
    )

    if (-not (Test-Path -LiteralPath $SourceDir)) { throw "Source folder not found: $SourceDir" }
    $sourceFull = (Resolve-Path -LiteralPath $SourceDir).ProviderPath.TrimEnd('\', '/')

    $files = @(Get-ChildItem -LiteralPath $sourceFull -Recurse -File | Sort-Object FullName)
    if ($files.Count -eq 0) { throw "Nothing to package in $SourceDir" }

    if (Test-Path -LiteralPath $ZipPath) { Remove-Item -LiteralPath $ZipPath -Force }

    $stream = [IO.File]::Open($ZipPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite)
    try {
        $archive = New-Object -TypeName System.IO.Compression.ZipArchive -ArgumentList @($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
        try {
            # Explicit folder entries (every ancestor, not just the leaf), like the
            # archives Nexus repacks for mod managers.
            $directories = New-Object 'System.Collections.Generic.SortedSet[string]'
            foreach ($file in $files) {
                $directory = $file.DirectoryName
                while ($directory -and $directory.Length -gt $sourceFull.Length) {
                    $relative = ConvertTo-ZipEntryPath ($directory.Substring($sourceFull.Length))
                    if ($relative) { [void] $directories.Add($relative) }
                    $directory = [IO.Path]::GetDirectoryName($directory)
                }
            }
            foreach ($relative in $directories) {
                $archive.CreateEntry($relative + '/') | Out-Null
            }

            foreach ($file in $files) {
                $relative = ConvertTo-ZipEntryPath ($file.FullName.Substring($sourceFull.Length))
                $entry = $archive.CreateEntry($relative, [IO.Compression.CompressionLevel]::Optimal)
                try { $entry.LastWriteTime = [DateTimeOffset] $file.LastWriteTime } catch { }
                $input = [IO.File]::OpenRead($file.FullName)
                try {
                    $output = $entry.Open()
                    try { $input.CopyTo($output) } finally { $output.Dispose() }
                }
                finally { $input.Dispose() }
            }
        }
        finally { $archive.Dispose() }
    }
    finally { $stream.Dispose() }
}

function Get-ZipFileEntries {
    param([Parameter(Mandatory)] [string] $ZipPath)

    $archive = [IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        return @($archive.Entries |
            Where-Object { -not $_.FullName.EndsWith('/') } |
            ForEach-Object { $_.FullName })
    }
    finally { $archive.Dispose() }
}

function Assert-ZipLayout {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $ZipPath,
        [Parameter(Mandatory)] [string[]] $AllowedPrefixes,
        [string[]] $AllowedRootFiles = @()
    )

    $entries = Get-ZipFileEntries -ZipPath $ZipPath
    if ($entries.Count -eq 0) { throw "Archive has no files: $ZipPath" }

    $prefixText = $AllowedPrefixes -join ', '
    foreach ($entry in $entries) {
        if ($entry.Contains('\')) { throw "Archive $ZipPath has a backslash in an entry name: $entry" }
        if ($AllowedRootFiles -contains $entry) { continue }

        $matched = $false
        foreach ($prefix in $AllowedPrefixes) {
            if ($entry.StartsWith($prefix, [StringComparison]::Ordinal)) { $matched = $true; break }
        }
        if (-not $matched) {
            throw "Archive $ZipPath has an entry outside $prefixText : $entry"
        }
    }

    return $entries
}
