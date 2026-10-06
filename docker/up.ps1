$ErrorActionPreference = "Stop"

$baseUrl = "http://localhost:8096"
$authHeader = 'MediaBrowser Client="VoBadgeDev", Device="Docker", DeviceId="vobadge-local", Version="1.0.0"'

function Invoke-Json {
    param(
        [string]$Method,
        [string]$Url,
        [object]$Body = $null,
        [hashtable]$Headers = @{}
    )

    $params = @{
        Method = $Method
        Uri = $Url
        Headers = $Headers
        ContentType = "application/json"
        UseBasicParsing = $true
    }

    if ($null -ne $Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 8 -Compress)
    }

    for ($attempt = 1; $attempt -le 8; $attempt++) {
        try {
            return Invoke-RestMethod @params
        }
        catch {
            $status = $_.Exception.Response.StatusCode.value__
            if ($status -eq 204) { return $null }
            $transient = $null -eq $_.Exception.Response -or $status -ge 500
            if ($transient -and $attempt -lt 8) {
                Start-Sleep -Seconds 3
                continue
            }
            throw
        }
    }
}

function Wait-Jellyfin {
    $deadline = (Get-Date).AddMinutes(3)
    while ((Get-Date) -lt $deadline) {
        try {
            $info = Invoke-RestMethod -Uri "$baseUrl/System/Info/Public" -UseBasicParsing
            $health = Invoke-WebRequest -Uri "$baseUrl/health" -UseBasicParsing
            if ($health.StatusCode -eq 200 -and $health.Content -match "Healthy") {
                Start-Sleep -Seconds 2
                return $info
            }
        }
        catch {
            Start-Sleep -Seconds 2
        }
    }
    throw "Jellyfin did not become ready on $baseUrl"
}

Write-Host "=== 1/5 Seed test media ==="
& (Join-Path $PSScriptRoot "seed-media.ps1")

Write-Host "=== 2/5 Build and deploy plugin ==="
& (Join-Path $PSScriptRoot "deploy.ps1")

Write-Host "=== 3/5 Start Jellyfin ==="
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    docker compose up -d --force-recreate
    if (-not $?) { throw "docker compose up failed" }
}
finally {
    Pop-Location
}

Write-Host "Waiting for Jellyfin..."
$info = Wait-Jellyfin
Write-Host "Jellyfin $($info.Version) is up"

Write-Host "=== 4/5 First-run setup ==="
$headers = @{ Authorization = $authHeader }

if (-not $info.StartupWizardCompleted) {
    Invoke-Json -Method POST -Url "$baseUrl/Startup/Configuration" -Headers $headers -Body @{
        UICulture = "fr-FR"
        MetadataCountryCode = "FR"
        PreferredMetadataLanguage = "fr"
    } | Out-Null

    Invoke-Json -Method POST -Url "$baseUrl/Startup/RemoteAccess" -Headers $headers -Body @{
        EnableRemoteAccess = $true
        EnableAutomaticPortMapping = $false
    } | Out-Null

    Invoke-Json -Method GET -Url "$baseUrl/Startup/User" -Headers $headers | Out-Null
    Invoke-Json -Method POST -Url "$baseUrl/Startup/User" -Headers $headers -Body @{
        Name = "admin"
        Password = "admin"
    } | Out-Null

    Invoke-Json -Method POST -Url "$baseUrl/Startup/Complete" -Headers $headers | Out-Null
    Write-Host "Wizard completed (admin / admin)"
}
else {
    Write-Host "Wizard already completed"
}

$auth = Invoke-Json -Method POST -Url "$baseUrl/Users/AuthenticateByName" -Headers $headers -Body @{
    Username = "admin"
    Pw = "admin"
}
$tokenHeaders = @{
    Authorization = "$authHeader, Token=`"$($auth.AccessToken)`""
}

$folders = Invoke-Json -Method GET -Url "$baseUrl/Library/VirtualFolders" -Headers $tokenHeaders
$names = @($folders | ForEach-Object { $_.Name })

if ($names -notcontains "Movies") {
    Invoke-Json -Method POST -Url "$baseUrl/Library/VirtualFolders?name=Movies&collectionType=movies&paths=/media/movies&refreshLibrary=false" -Headers $tokenHeaders -Body @{
        LibraryOptions = @{
            Enabled = $true
            EnableRealtimeMonitor = $true
        }
    } | Out-Null
    Write-Host "Created Movies library"
}

if ($names -notcontains "Shows") {
    Invoke-Json -Method POST -Url "$baseUrl/Library/VirtualFolders?name=Shows&collectionType=tvshows&paths=/media/shows&refreshLibrary=false" -Headers $tokenHeaders -Body @{
        LibraryOptions = @{
            Enabled = $true
            EnableRealtimeMonitor = $true
        }
    } | Out-Null
    Write-Host "Created Shows library"
}

Write-Host "=== 5/5 Scan library and run VO Badge task ==="
Invoke-Json -Method POST -Url "$baseUrl/Library/Refresh" -Headers $tokenHeaders | Out-Null

$deadline = (Get-Date).AddMinutes(2)
$movieCount = 0
while ((Get-Date) -lt $deadline) {
    $items = Invoke-Json -Method GET -Url "$baseUrl/Items?IncludeItemTypes=Movie&Recursive=true" -Headers $tokenHeaders
    $movieCount = [int]$items.TotalRecordCount
    if ($movieCount -ge 4) { break }
    Start-Sleep -Seconds 3
}

Write-Host "Library movies: $movieCount"
Start-Sleep -Seconds 8

$tasks = Invoke-Json -Method GET -Url "$baseUrl/ScheduledTasks" -Headers $tokenHeaders
$scan = $tasks | Where-Object { $_.Key -eq "VoBadgeScanTask" } | Select-Object -First 1
if ($null -eq $scan) {
    Write-Host "VO Badge task not found. Check Dashboard > Plugins."
}
else {
    Invoke-Json -Method POST -Url "$baseUrl/ScheduledTasks/Running/$($scan.Id)" -Headers $tokenHeaders | Out-Null
    Write-Host "Triggered scheduled task: $($scan.Name)"
}

Write-Host ""
Write-Host "Ready"
Write-Host "  URL:      $baseUrl"
Write-Host "  Login:    admin / admin"
Write-Host "  Plugin:   Dashboard > Plugins > VO Badge"
Write-Host "  Task:     Dashboard > Scheduled Tasks > VO Badge - Scan Library"
Write-Host "  Redeploy: .\docker\deploy.ps1"
