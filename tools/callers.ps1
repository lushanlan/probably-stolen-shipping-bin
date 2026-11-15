param([Parameter(Mandatory=$true)][string]$Needle)
$managed = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'Probably Stolen_Data\Managed'
Add-Type -Path (Join-Path $managed 'Unity.Burst.Cecil.dll')
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $managed 'Assembly-CSharp.dll'))
function Visit($type) {
  foreach ($method in $type.Methods) {
    if ($method.HasBody) {
      foreach ($instruction in $method.Body.Instructions) {
        if ($null -ne $instruction.Operand -and $instruction.Operand.ToString().Contains($Needle)) { Write-Output ("{0}: {1}" -f $method.FullName, $instruction) }
      }
    }
  }
  foreach ($nested in $type.NestedTypes) { Visit $nested }
}
foreach ($type in $assembly.MainModule.Types) { Visit $type }
