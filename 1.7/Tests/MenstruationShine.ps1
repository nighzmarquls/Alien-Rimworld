# Read-only installed-binary audit in an isolated PowerShell process, no game UI.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'LegacyPregnancyApi.ps1')
[void][Reflection.Assembly]::LoadFrom((Join-Path $pregRepository '1.7/Assemblies/XMT.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $pregRepository 'Compatibility/1.7/rjw/Assemblies/XMT_RJW.dll'))
$shineAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $pregRepository 'Compatibility/1.7/rjw_menstruation/Assemblies/XMT_Menstruation.dll'))
$shinePatch = $shineAssembly.GetType('XMT_Menstruation.CryptimorphShine+DrawHighlights', $true)
$shineFlags = [Reflection.BindingFlags]'Static,NonPublic'
$shineTargets = @($shinePatch.GetMethod('TargetMethods', $shineFlags).Invoke($null, @()))
$shineTranspiler = $shinePatch.GetMethod('Transpiler', $shineFlags)
$shineReader = [HarmonyLib.PatchProcessor].GetMethods([Reflection.BindingFlags]'Public,Static') |
    Where-Object { $_.Name -eq 'GetOriginalInstructions' -and -not $_.GetParameters()[1].ParameterType.IsByRef }
$shineCalls = 0
foreach ($shineTarget in $shineTargets) {
    $shineInstructions = $shineReader.Invoke($null, [object[]]@($shineTarget, $null))
    $shineExpected = @($shineInstructions | Where-Object {
        $_.operand -is [Reflection.MethodInfo] -and (
            ($_.operand.DeclaringType -eq [UnityEngine.GUI] -and $_.operand.Name -eq 'DrawTexture') -or
            ($_.operand.DeclaringType -eq [Verse.Widgets] -and $_.operand.Name -eq 'DrawTextureFitted'))
    }).Count
    $shineArguments = [object[]]::new(1)
    $shineArguments[0] = $shineInstructions
    $shineRewritten = @($shineTranspiler.Invoke($null, $shineArguments))
    $shineActual = @($shineRewritten | Where-Object {
        $_.operand -is [Reflection.MethodInfo] -and $_.operand.DeclaringType.FullName -eq 'XMT_Menstruation.CryptimorphShine'
    }).Count
    if ($shineExpected -eq 0 -or $shineActual -ne $shineExpected) {
        throw ('Incomplete highlight draw coverage: ' + $shineTarget.Name + ' ' + $shineActual + '/' + $shineExpected)
    }
    $shineCalls += $shineActual
}
$shineHarmony = [HarmonyLib.Harmony]::new('XMT.ShinePatchAudit')
try {
    $shinePatched = @($shineHarmony.CreateClassProcessor($shinePatch).Patch())
    if ($shinePatched.Count -ne 5) { throw 'Expected five shine drawing patches' }
    Write-Output ('PASS: all ' + $shineCalls + ' drawing calls in five upstream methods use matching shine wrappers; Harmony patches apply.')
} finally { $shineHarmony.UnpatchAll($shineHarmony.Id) }
