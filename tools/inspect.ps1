param([Parameter(Mandatory=$true)][string]$Type, [string]$Method = '')
$managed = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'Probably Stolen_Data\Managed'
Add-Type -Path (Join-Path $managed 'Unity.Burst.Cecil.dll')
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $managed 'Assembly-CSharp.dll'))
$allTypes = New-Object 'System.Collections.Generic.List[Mono.Cecil.TypeDefinition]'
function AddTypes($type) { $allTypes.Add($type); foreach ($nested in $type.NestedTypes) { AddTypes $nested } }
foreach ($typeDefinition in $assembly.MainModule.Types) { AddTypes $typeDefinition }
$types = $allTypes | Where-Object { $_.FullName -like "*$Type*" }
foreach ($t in $types) {
    Write-Output ("TYPE {0} : {1}" -f $t.FullName, $t.BaseType)
    $t.Fields | ForEach-Object { Write-Output ("FIELD {0}" -f $_.FullName) }
    $t.Methods | Where-Object { !$Method -or $_.Name -like "*$Method*" } | ForEach-Object {
        Write-Output ("METHOD {0}" -f $_.FullName)
        if ($Method -and $_.HasBody) { $_.Body.Instructions | ForEach-Object { Write-Output ("  {0}" -f $_) } }
    }
}
