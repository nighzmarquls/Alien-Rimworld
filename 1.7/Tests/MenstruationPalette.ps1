# Installed 1.6 UI binaries with 1.7 type forwarders. Does not start a game or draw UI.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'MenstruationShine.ps1')
$paletteType = $shineAssembly.GetType('XMT_Menstruation.CryptimorphPalette+WindowColors', $true)
$paletteFlags = [Reflection.BindingFlags]'Static,NonPublic'
$paletteTargets = @($paletteType.GetMethod('TargetMethods', $paletteFlags).Invoke($null, @()))
$paletteTranspiler = $paletteType.GetMethod('Transpiler', $paletteFlags)
foreach ($paletteTarget in $paletteTargets) {
    $paletteInstructions = $shineReader.Invoke($null, [object[]]@($paletteTarget, $null))
    $paletteArguments = [object[]]::new(2)
    $paletteArguments[0] = $paletteInstructions
    $paletteArguments[1] = $paletteTarget
    $paletteRewritten = @($paletteTranspiler.Invoke($null, $paletteArguments))
    $paletteCalls = @($paletteRewritten | Where-Object {
        $_.operand -is [Reflection.MethodInfo] -and $_.operand.DeclaringType.FullName -eq 'XMT_Menstruation.CryptimorphPalette'
    })
    $paletteExpected = if ($paletteTarget.Name -eq 'DrawWomb') { 1 } else { 7 }
    if ($paletteCalls.Count -ne $paletteExpected) { throw ('Incomplete palette coverage: ' + $paletteTarget.Name + ' ' + $paletteCalls.Count + '/' + $paletteExpected) }
}
foreach ($paletteOrder in 0..1) {
    $paletteHarmony = [HarmonyLib.Harmony]::new('XMT.PalettePatchAudit.' + $paletteOrder)
    try {
        $paletteClasses = @($paletteType, $shinePatch)
        if ($paletteOrder -eq 1) { [array]::Reverse($paletteClasses) }
        foreach ($paletteClass in $paletteClasses) { [void]$paletteHarmony.CreateClassProcessor($paletteClass).Patch() }
        $paletteCycle = $shineAssembly.GetType('XMT_Menstruation.CryptimorphPalette+CycleProgressColor', $true)
        [void]$paletteHarmony.CreateClassProcessor($paletteCycle).Patch()
    } finally { $paletteHarmony.UnpatchAll($paletteHarmony.Id) }
}
$paletteWomb = $pregMenstruation.GetType('RJW_Menstruation.HediffComp_Menstruation')
foreach ($paletteProperty in @('BloodColor', 'CurrentVisibleStage', 'GetStageTexture')) {
    if ($null -eq $paletteWomb.GetProperty($paletteProperty, [Reflection.BindingFlags]'Instance,Public')) { throw ('Missing womb UI accessor: ' + $paletteProperty) }
}
Write-Output 'PASS: seven stat-bar colors, womb panel tint and cycle progress hooks resolve; palette and shine Harmony patches apply in either order.'
