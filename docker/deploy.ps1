$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$pluginDir = Join-Path $PSScriptRoot "config\plugins\VoBadge"
$buildDir = Join-Path $root "src\VoBadge\bin\Release\net10.0"

Write-Host "Building plugin (Release)..."
dotnet build (Join-Path $root "src\VoBadge.sln") -c Release --nologo
if (-not $?) { throw "Build failed" }

New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Get-ChildItem -LiteralPath $pluginDir -File -ErrorAction SilentlyContinue | Remove-Item -Force

Copy-Item -LiteralPath (Join-Path $buildDir "Jellyfin.Plugin.VoBadge.dll") -Destination $pluginDir -Force
$pdb = Join-Path $buildDir "Jellyfin.Plugin.VoBadge.pdb"
if (Test-Path -LiteralPath $pdb) {
    Copy-Item -LiteralPath $pdb -Destination $pluginDir -Force
}

Copy-Item -LiteralPath (Join-Path $PSScriptRoot "meta.json") -Destination $pluginDir -Force

Write-Host "Plugin copied to $pluginDir"

$running = docker ps --filter "name=vobadge-jellyfin" --format "{{.Names}}"
if ($running -eq "vobadge-jellyfin") {
    Write-Host "Restarting Jellyfin to reload the plugin..."
    docker restart vobadge-jellyfin | Out-Null
}
