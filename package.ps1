$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$destination = Join-Path $PSScriptRoot 'build\package-dev'
New-Item -ItemType Directory -Force $destination | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'build\ShippingBin.dll') -Destination $destination -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'packaging\manifest.xml') -Destination $destination -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'packaging\Localization') -Destination $destination -Recurse -Force
# 自定义贴图（由用户提供，本项目不生成美术资源）：
#   packaging\Textures\Items\<BinSpriteKey>.png 会被原样打进包；代码会优先使用该 sprite，
#   找不到时回退到原版 Items/items_container::eleanor_box。
$textureSource = Join-Path $PSScriptRoot 'packaging\Textures'
if (Test-Path -LiteralPath $textureSource) {
    Copy-Item -LiteralPath $textureSource -Destination $destination -Recurse -Force
    Write-Host "Included mod textures: $textureSource"
} else {
    Write-Host "No packaging\Textures yet; bin uses the vanilla container sprite."
}
Write-Host "Development package only: $destination"
