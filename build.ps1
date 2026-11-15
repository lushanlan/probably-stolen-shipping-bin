$ErrorActionPreference = 'Stop'
$managed = Join-Path (Split-Path $PSScriptRoot -Parent) 'Probably Stolen_Data\Managed'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Force $output | Out-Null
& $compiler /nologo /target:library /out:"$output\ShippingBin.dll" /reference:"$managed\Assembly-CSharp.dll" /reference:"$managed\netstandard.dll" /reference:"$managed\UnityEngine.CoreModule.dll" "$PSScriptRoot\src\ShippingBinMod.cs" "$PSScriptRoot\src\Rules.cs"
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $LASTEXITCODE" }
Write-Host "Built $output\ShippingBin.dll"
