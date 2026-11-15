$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$destination = Join-Path $PSScriptRoot 'build\package-dev'
New-Item -ItemType Directory -Force $destination | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'build\ShippingBin.dll') -Destination $destination -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'packaging\manifest.xml') -Destination $destination -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'packaging\Localization') -Destination $destination -Recurse -Force
Write-Host "Development package only: $destination"
