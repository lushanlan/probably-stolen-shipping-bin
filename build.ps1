$ErrorActionPreference = 'Stop'
$managed = Join-Path (Split-Path $PSScriptRoot -Parent) 'Probably Stolen_Data\Managed'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Force $output | Out-Null

# 纯规则核心 + 游戏适配层 + 入口。tests\CoreTests.cs 不参与模组构建。
$sources = @(
    (Join-Path $PSScriptRoot 'src\Core\Rules.cs'),
    (Join-Path $PSScriptRoot 'src\Core\ItemFacts.cs'),
    (Join-Path $PSScriptRoot 'src\Core\Planner.cs'),
    (Join-Path $PSScriptRoot 'src\Core\Commit.cs'),
    (Join-Path $PSScriptRoot 'src\Core\GrantPolicy.cs'),
    (Join-Path $PSScriptRoot 'src\Core\Ledger.cs'),
    (Join-Path $PSScriptRoot 'src\Core\ShapeMigration.cs'),
    (Join-Path $PSScriptRoot 'src\Core\UpgradeStatus.cs'),
    (Join-Path $PSScriptRoot 'src\Game\ModIds.cs'),
    (Join-Path $PSScriptRoot 'src\Game\Loc.cs'),
    (Join-Path $PSScriptRoot 'src\Game\ModSettings.cs'),
    (Join-Path $PSScriptRoot 'src\Game\BinItem.cs'),
    (Join-Path $PSScriptRoot 'src\Game\BinWorld.cs'),
    (Join-Path $PSScriptRoot 'src\Game\FactsBuilder.cs'),
    (Join-Path $PSScriptRoot 'src\Game\NightSettlement.cs'),
    (Join-Path $PSScriptRoot 'src\Game\PendingLedger.cs'),
    (Join-Path $PSScriptRoot 'src\Game\Purchase.cs'),
    (Join-Path $PSScriptRoot 'src\Game\DebugGrant.cs'),
    (Join-Path $PSScriptRoot 'src\Game\BinOpenHook.cs'),
    (Join-Path $PSScriptRoot 'src\Game\UpgradeDragPatch.cs'),
    (Join-Path $PSScriptRoot 'src\Game\BinTooltip.cs'),
    (Join-Path $PSScriptRoot 'src\ShippingBinMod.cs')
)

$missing = @($sources | Where-Object { -not (Test-Path $_) })
if ($missing.Count -gt 0) { throw ("Missing source files: " + ($missing -join ', ')) }

# 路径含空格，参数用响应文件传递，避免 PowerShell 给原生命令加引号时的歧义。
$rsp = Join-Path $output 'csc.rsp'
$lines = @(
    '/nologo',
    '/target:library',
    ('/out:"' + (Join-Path $output 'ShippingBin.dll') + '"'),
    ('/reference:"' + (Join-Path $managed 'Assembly-CSharp.dll') + '"'),
    ('/reference:"' + (Join-Path $managed 'netstandard.dll') + '"'),
    ('/reference:"' + (Join-Path $managed 'UnityEngine.CoreModule.dll') + '"'),
    ('/reference:"' + (Join-Path $managed '0Harmony.dll') + '"')
) + @($sources | ForEach-Object { '"' + $_ + '"' })
[System.IO.File]::WriteAllLines($rsp, $lines, (New-Object System.Text.UTF8Encoding($false)))

& $compiler ('@' + $rsp)
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $LASTEXITCODE" }
Write-Host "Built $output\ShippingBin.dll"
