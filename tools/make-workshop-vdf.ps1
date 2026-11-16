param(
    [string]$Version = '0.1.0',
    [string]$PublishedFileId = '3840748573',
    [int]$Visibility = 0
)
# 生成 SteamCMD Workshop 上传用 VDF（只生成文件，不登录、不上传）。
# 用法：pwsh -File .\tools\make-workshop-vdf.ps1
# 首次上传 publishedfileid=0、visibility=2（隐藏）；正式公开时 visibility=0 并复用 SteamCMD 回写的 id。
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$content = Join-Path $root 'build\release\ShippingBin'
if (-not (Test-Path -LiteralPath (Join-Path $content 'manifest.xml'))) {
    throw "Release content not found: $content. Run 'pwsh -File .\release.ps1' first."
}

$outDir = Join-Path $root 'build\release\workshop'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# 1) 封面：复用已有正视图（不新出图），仅做 512x512 适配与透明背景
$source = Join-Path $root 'art\wooden-shipping-bin-front.png'
$preview = Join-Path $outDir 'preview.png'
if (-not (Test-Path -LiteralPath $source)) { throw "Cover source not found: $source" }
Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile($source)
try {
    $w = $img.Width; $h = $img.Height
    $bmpSrc = New-Object System.Drawing.Bitmap($img)
    # alpha 包围盒
    $minX = $w; $minY = $h; $maxX = -1; $maxY = -1
    $step = 2
    for ($y = 0; $y -lt $h; $y += $step) {
        for ($x = 0; $x -lt $w; $x += $step) {
            if ($bmpSrc.GetPixel($x, $y).A -gt 16) {
                if ($x -lt $minX) { $minX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    if ($maxX -lt 0) { throw 'Cover source is fully transparent' }
    $cropW = $maxX - $minX + 1
    $cropH = $maxY - $minY + 1
    $size = 512
    $scale = [Math]::Min(($size - 32) / $cropW, ($size - 32) / $cropH)
    $drawW = [int][Math]::Round($cropW * $scale)
    $drawH = [int][Math]::Round($cropH * $scale)
    $dst = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($dst)
    try {
        $g.Clear([System.Drawing.Color]::Transparent)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        $destRect = New-Object System.Drawing.Rectangle(
            [int](($size - $drawW) / 2), [int](($size - $drawH) / 2), $drawW, $drawH)
        $srcRect = New-Object System.Drawing.Rectangle($minX, $minY, $cropW, $cropH)
        $g.DrawImage($bmpSrc, $destRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
    } finally { $g.Dispose() }
    $dst.Save($preview, [System.Drawing.Imaging.ImageFormat]::Png)
    $dst.Dispose()
    $bmpSrc.Dispose()
} finally { $img.Dispose() }

# 2) 描述：中英合并（VDF 需要单行，换行转义为 \n）
function Read-Text([string]$name) {
    $p = Join-Path $root "packaging\workshop\$name"
    if (-not (Test-Path -LiteralPath $p)) { throw "Missing workshop text: $p" }
    return (Get-Content -LiteralPath $p -Raw).Trim()
}
$description = (Read-Text 'description.zh.txt') + "`n`n---`n`n" + (Read-Text 'description.en.txt')
$title = '可能是星露谷：出货箱 | Shipping Bin'
$changenote = "v$Version 首个发行候选：夜间统一结算、四档容量 100/170/340/960、材料拖拽升级与进度提示、赃物/违禁/假贴标规则、自动化料斗保护、早间报告销售额。"
$template = Get-Content -LiteralPath (Join-Path $root 'packaging\workshop\workshop.vdf.template') -Raw

function ConvertTo-VdfString([string]$s) {
    $t = $s -replace '\\', '\\'
    $t = $t -replace '"', '\"'
    $t = $t -replace "`r`n", '\n'
    $t = $t -replace "`n", '\n'
    return $t
}

$vdf = $template
$vdf = $vdf.Replace('__PUBLISHEDFILEID__', $PublishedFileId)
$vdf = $vdf.Replace('__CONTENTFOLDER__', $content)
$vdf = $vdf.Replace('__PREVIEWFILE__', $preview)
$vdf = $vdf.Replace('__VISIBILITY__', [string]$Visibility)
$vdf = $vdf.Replace('__TITLE__', (ConvertTo-VdfString $title))
$vdf = $vdf.Replace('__DESCRIPTION__', (ConvertTo-VdfString $description))
$vdf = $vdf.Replace('__CHANGENOTE__', (ConvertTo-VdfString $changenote))

$vdfPath = Join-Path $outDir 'workshop_item.vdf'
[System.IO.File]::WriteAllText($vdfPath, $vdf, (New-Object System.Text.UTF8Encoding($false)))
if ((Get-Item $preview).Length -gt 1MB) { throw "preview.png exceeds 1 MB Workshop limit: $preview" }

Write-Host "Workshop VDF: $vdfPath"
Write-Host "  contentfolder: $content"
Write-Host "  previewfile  : $preview ($((Get-Item $preview).Length) bytes)"
Write-Host "  appid 4348910, publishedfileid $PublishedFileId, visibility $Visibility (2 = hidden)"
Write-Host '  未登录、未上传。上传由用户执行：steamcmd +login <account> +workshop_build_item "<abs path to workshop_item.vdf>" +quit'
