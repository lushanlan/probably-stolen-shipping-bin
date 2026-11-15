param([string]$Package = (Join-Path (Split-Path $PSScriptRoot -Parent) 'build\package-dev'))
$ErrorActionPreference = 'Stop'
$managed = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'Probably Stolen_Data\Managed'
$script:managedForResolution = $managed
[AppDomain]::CurrentDomain.add_AssemblyResolve({ param($sender, $eventArgs)
  $name = (New-Object System.Reflection.AssemblyName($eventArgs.Name)).Name
  $path = Join-Path $script:managedForResolution ($name + '.dll')
  if (Test-Path -LiteralPath $path) { return [System.Reflection.Assembly]::LoadFrom($path) }
  return $null
})
$game = [System.Reflection.Assembly]::LoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))
$loaderType = $game.GetType('ModLoader', $true)
$parser = $loaderType.GetMethod('ParseManifestForTools', [System.Reflection.BindingFlags]'Public,NonPublic,Static')
$parseArgs = [object[]]@([string]$Package, $null)
$manifest = $parser.Invoke($null, $parseArgs)
if ($null -eq $manifest) { throw "Manifest parse failed: $($parseArgs[1])" }
Write-Output ("Manifest ID={0}; Assemblies={1}; Capabilities={2}" -f $manifest.ID, ($manifest.Assemblies -join ','), ($manifest.Capabilities -join ','))
$scanner = $game.GetType('ModScanner', $true)
$method = $scanner.GetMethod('Scan', [System.Reflection.BindingFlags]'Public,NonPublic,Static')
$paths = [string[]]@((Join-Path $Package 'ShippingBin.dll'))
$mods = [string[]]@(Get-ChildItem -LiteralPath $managed -Filter '*.dll' | ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_.Name) })
try {
  $result = $method.Invoke($null, [object[]]@($paths, $mods, $Package, $false))
  $result | Format-List Refused,Error,MethodsScanned,Capabilities,Findings,HardFlags,Pending
} catch { Write-Error ($_.Exception.ToString()); exit 1 }
