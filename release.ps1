param(
    [string]$Version = '0.1.0'
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$modId = 'lushanlan.shipping_bin'

# 1) 真实构建（不做 release 专用编译，保证与开发包同一份代码）
& (Join-Path $root 'build.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

$releaseRoot = Join-Path $root 'build\release'
$stage = Join-Path $releaseRoot 'ShippingBin'
$zipPath = Join-Path $releaseRoot ("ProbablyStolen-ShippingBin-{0}.zip" -f $Version)

if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null

Copy-Item -LiteralPath (Join-Path $root 'build\ShippingBin.dll') -Destination $stage -Force
Copy-Item -LiteralPath (Join-Path $root 'packaging\manifest.xml') -Destination $stage -Force
Copy-Item -LiteralPath (Join-Path $root 'packaging\Localization') -Destination $stage -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root 'packaging\Textures') -Destination $stage -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $stage -Force
Copy-Item -LiteralPath (Join-Path $root 'packaging\release\README.md') -Destination $stage -Force
Copy-Item -LiteralPath (Join-Path $root 'packaging\release\CHANGELOG.md') -Destination $stage -Force

# 2) 版本一致性：manifest ModVersion 必须与发布版本一致
[xml]$manifest = Get-Content -LiteralPath (Join-Path $stage 'manifest.xml') -Raw
if ($manifest.Manifest.ID -ne $modId) { throw "manifest ID mismatch: $($manifest.Manifest.ID)" }
if ([string]$manifest.Manifest.ModVersion -ne $Version) {
    throw "manifest ModVersion '$($manifest.Manifest.ModVersion)' != release version '$Version'"
}

# 3) 内容白名单校验：发行包只能出现下列文件/后缀
$allowed = @(
    'manifest.xml',
    'ShippingBin.dll',
    'LICENSE',
    'README.md',
    'CHANGELOG.md'
)
$allowedPrefixes = @('Localization\', 'Textures\')
$forbiddenNames = @('Assembly-CSharp.dll', '0Harmony.dll', 'UnityEngine.CoreModule.dll', 'netstandard.dll')
$violations = New-Object System.Collections.Generic.List[string]
Get-ChildItem -LiteralPath $stage -Recurse -File | ForEach-Object {
    $rel = $_.FullName.Substring($stage.Length + 1)
    $ok = ($allowed -contains $rel)
    if (-not $ok) {
        foreach ($p in $allowedPrefixes) { if ($rel.StartsWith($p)) { $ok = $true } }
    }
    if (-not $ok) { $violations.Add("unexpected file: $rel") }
    if ($forbiddenNames -contains $_.Name) { $violations.Add("forbidden assembly: $rel") }
    if ($_.Name -like '*.pdb' -or $_.Name -like '*.cs' -or $_.Name -like '*.zip' -or $_.Name -like '*.log') {
        $violations.Add("forbidden artifact: $rel")
    }
}
if ($violations.Count -gt 0) {
    $violations | ForEach-Object { Write-Host "  $_" }
    throw "release package contains $($violations.Count) forbidden file(s)"
}

# 4) 打包 zip（内容根为 mod 目录本身，解压后可直接放进 Mods\）
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zipPath -CompressionLevel Optimal

# 5) 官方 manifest parser + ModScanner 对发行目录重扫
#    必须在子进程执行：scan.ps1 会注册 AppDomain.AssemblyResolve 处理器，
#    同进程调用会让后续 cmdlet（如 Set-Content）在解析卫星资源程序集时递归失败。
$scanScript = Join-Path $root 'tools\scan.ps1'
& pwsh -NoProfile -File $scanScript -Package $stage
if ($LASTEXITCODE -ne 0) { throw 'scan.ps1 failed' }

# 6) 哈希清单（发行目录内所有文件 + zip 本身）
$sums = New-Object System.Collections.Generic.List[string]
Get-ChildItem -LiteralPath $stage -Recurse -File | Sort-Object FullName | ForEach-Object {
    $rel = $_.FullName.Substring($stage.Length + 1)
    $sums.Add(("{0}  {1}" -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash, $rel))
}
$sums.Add(("{0}  {1}" -f (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash, (Split-Path $zipPath -Leaf)))
$sumsPath = Join-Path $releaseRoot 'SHA256SUMS.txt'
[System.IO.File]::WriteAllLines($sumsPath, $sums, (New-Object System.Text.UTF8Encoding($false)))

Write-Host ''
Write-Host "Release candidate: $stage"
Write-Host "Zip: $zipPath"
Get-ChildItem -LiteralPath $stage -Recurse -File | Sort-Object FullName | ForEach-Object {
    $rel = $_.FullName.Substring($stage.Length + 1)
    Write-Host ("  {0,8}  {1}" -f $_.Length, $rel)
}
Write-Host ("  {0,8}  {1}" -f (Get-Item $zipPath).Length, (Split-Path $zipPath -Leaf))
Write-Host "SHA256SUMS: $(Join-Path $releaseRoot 'SHA256SUMS.txt')"
