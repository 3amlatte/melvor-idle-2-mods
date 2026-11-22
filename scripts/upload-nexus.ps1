[CmdletBinding()]
param(
    # Mod id from the site URL: /melvoridle2/mods/<ModId>
    [Parameter(Mandatory)] [string] $ModId,

    # Zip to upload.
    [Parameter(Mandatory)] [string] $Zip,

    # File version shown on the mod page.
    [Parameter(Mandatory)] [string] $Version,

    [string] $Name = 'No Negative Gear',
    [string] $FileDescription = 'Removes negative stats and bonuses from armour.',
    [string] $GameDomain = 'melvoridle2',

    # Existing mod file id: add a new version to it. Omit to create the mod's first file.
    [string] $FileId,

    # Personal API key, one line of text. Never commit this file.
    [string] $ApiKeyFile = (Join-Path $env:APPDATA 'nexus-api-key.txt'),

    # Also set the mod page's version to $Version.
    [switch] $UpdateModVersion
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$apiBase = 'https://api.nexusmods.com/v3'

if (-not (Test-Path -LiteralPath $Zip)) { throw "Zip not found: $Zip" }
if (-not (Test-Path -LiteralPath $ApiKeyFile)) { throw "API key file not found: $ApiKeyFile" }

$apiKey = (Get-Content -LiteralPath $ApiKeyFile -Raw -Encoding UTF8).Trim().Trim([char]0xFEFF)
if ([string]::IsNullOrWhiteSpace($apiKey)) { throw "API key file is empty: $ApiKeyFile" }

$headers = @{ apikey = $apiKey }

function Invoke-NexusApi {
    param(
        [Parameter(Mandatory)] [string] $Method,
        [Parameter(Mandatory)] [string] $Path,
        $Body
    )

    $params = @{
        Uri     = $apiBase + $Path
        Method  = $Method
        Headers = $headers
    }
    if ($null -ne $Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 8)
        $params.ContentType = 'application/json'
    }
    Invoke-RestMethod @params
}

# Resolve both ids: site URLs use the game-scoped id, the API also exposes a global id.
$mod = Invoke-NexusApi -Method Get -Path "/games/$GameDomain/mods/$ModId"
$gameScopedId = [string] $mod.data.game_scoped_id
$globalId = [string] $mod.data.id
Write-Host ("Mod: {0} (game-scoped {1}, global {2}, status {3})" -f $mod.data.name, $gameScopedId, $globalId, $mod.data.status)

$file = Get-Item -LiteralPath $Zip
Write-Host ("Uploading {0} ({1:N1} KB) ..." -f $file.Name, ($file.Length / 1KB))

# Multipart upload: the same flow the official Nexus upload action uses. Single-part
# presigned URLs currently reject the signature, and this path also handles large files.
$multipart = Invoke-NexusApi -Method Post -Path '/uploads/multipart' -Body @{
    size_bytes = $file.Length
    filename   = $file.Name
}
$uploadId = [string] $multipart.data.id
$partSize = [int64] $multipart.data.part_size_bytes
$partUrls = @($multipart.data.part_presigned_urls)
$completeUrl = [string] $multipart.data.complete_presigned_url
Write-Host ("Upload session: {0} ({1} part(s))" -f $uploadId, $partUrls.Count)

$etags = New-Object System.Collections.Generic.List[string]
$offset = [int64] 0
$partNumber = 1
foreach ($partUrl in $partUrls) {
    $length = [Math]::Min($partSize, $file.Length - $offset)
    $tempPart = [IO.Path]::GetTempFileName()
    try {
        $input = [IO.File]::OpenRead($file.FullName)
        try {
            $input.Seek($offset, [IO.SeekOrigin]::Begin) | Out-Null
            $output = [IO.File]::Create($tempPart)
            try {
                $buffer = New-Object byte[] 81920
                $remaining = [int64] $length
                while ($remaining -gt 0) {
                    $want = [int] [Math]::Min([int64] $buffer.Length, $remaining)
                    $read = $input.Read($buffer, 0, $want)
                    if ($read -le 0) { break }
                    $output.Write($buffer, 0, $read)
                    $remaining -= $read
                }
            }
            finally { $output.Dispose() }
        }
        finally { $input.Dispose() }

        $response = Invoke-WebRequest -Uri $partUrl -Method Put -InFile $tempPart `
            -ContentType 'application/octet-stream' -UseBasicParsing

        $etag = (@($response.Headers['ETag']) | Select-Object -First 1)
        if (-not $etag) { throw "Part $partNumber returned no ETag." }
        $etags.Add($etag.Trim('"'))
    }
    finally {
        Remove-Item -LiteralPath $tempPart -Force -ErrorAction SilentlyContinue
    }

    $offset += $length
    $partNumber++
}

$partsXml = ''
for ($i = 0; $i -lt $etags.Count; $i++) {
    $partsXml += ('<Part><PartNumber>{0}</PartNumber><ETag>{1}</ETag></Part>' -f ($i + 1), $etags[$i])
}
$completeXml = '<CompleteMultipartUpload>' + $partsXml + '</CompleteMultipartUpload>'

$xmlFile = [IO.Path]::GetTempFileName()
try {
    [IO.File]::WriteAllText($xmlFile, $completeXml, [Text.UTF8Encoding]::new($false))
    $curlOutput = & curl.exe -sS --fail-with-body -X POST $completeUrl `
        -H 'Content-Type: application/xml' --data-binary "@$xmlFile"
    if ($LASTEXITCODE -ne 0) { throw "Completing multipart upload failed (curl exit $LASTEXITCODE): $curlOutput" }
}
finally {
    Remove-Item -LiteralPath $xmlFile -Force -ErrorAction SilentlyContinue
}

Invoke-NexusApi -Method Post -Path "/uploads/$uploadId/finalise" | Out-Null

$state = $null
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Seconds 2
    $state = (Invoke-NexusApi -Method Get -Path "/uploads/$uploadId").data.state
    if ($state -eq 'available') { break }
}
if ($state -ne 'available') { throw "Upload did not become available (state: $state)." }
Write-Host "Upload is available."

if ($FileId) {
    $created = Invoke-NexusApi -Method Post -Path "/mod-files/$FileId/versions" -Body @{
        upload_id          = $uploadId
        name               = $Name
        version            = $Version
        description        = $FileDescription
        file_category      = 'main'
        update_mod_version = [bool] $UpdateModVersion
    }
    $versionId = [string] $created.data.version.id
}
else {
    $created = Invoke-NexusApi -Method Post -Path '/mod-files' -Body @{
        upload_id                    = $uploadId
        mod_id                       = $globalId   # the API wants the global id, not the URL id
        name                         = $Name
        version                      = $Version
        description                  = $FileDescription
        file_category                = 'main'
        primary_mod_manager_download = $true
        allow_mod_manager_download   = $true
        update_mod_version           = [bool] $UpdateModVersion
    }
    $versionId = [string] $created.data.id
}

Write-Host ("Created file version {0}" -f $versionId)

# Show what the page now has, including the scan state Nexus applies before publishing.
$filesResponse = Invoke-NexusApi -Method Get -Path "/mods/$globalId/files"
$modFile = @($filesResponse.data.mod_files) | Where-Object { $_.name -eq $Name } | Select-Object -First 1
if (-not $modFile) { $modFile = @($filesResponse.data.mod_files) | Select-Object -First 1 }

if ($modFile) {
    Write-Host ("File: {0} (id {1})" -f $modFile.name, $modFile.id)
    $versions = Invoke-NexusApi -Method Get -Path "/mod-files/$($modFile.id)/versions"
    @($versions.data.versions) | ForEach-Object {
        Write-Host ("  {0}  version {1}  [{2}]  scan: {3}" -f $_.uploaded_at, $_.version, $_.category, $_.security_scan_status)
    }
}
else {
    Write-Warning "The new file was not listed yet; check the mod page in a moment."
}
