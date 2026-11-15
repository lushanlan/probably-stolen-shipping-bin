# 纯规则层测试：不加载游戏、不引用任何游戏类型。
# 覆盖数值阈值矩阵、保护判定、多箱顺序独立、嵌套去重、赃物基础金额、
# 假贴标归属、升级余点/溢出、事务提交与故障回滚。
# 这里通过只说明“规则正确”，不代表游戏内玩法已验证。
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$outDir = Join-Path $root 'build\tests'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) { throw "csc.exe not found: $csc" }

$sources = @()
$sources += Get-ChildItem -LiteralPath (Join-Path $root 'src\Core') -Filter '*.cs' | ForEach-Object { $_.FullName }
$sources += (Join-Path $root 'tests\CoreTests.cs')

$exe = Join-Path $outDir 'CoreTests.exe'
& $csc /nologo /target:exe /warnaserror- /out:$exe $sources
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }

& $exe
$exit = $LASTEXITCODE
if ($exit -ne 0) { throw "CoreTests failed with exit code $exit" }
Write-Output "PASS tools/test-rules.ps1 (game integration is NOT tested by this script)"
