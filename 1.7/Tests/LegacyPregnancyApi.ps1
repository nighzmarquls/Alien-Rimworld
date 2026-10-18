# Read-only loader/JIT audit against installed upstream binaries. No game startup or graphics initialization.
# Run after building the optional projects. This is not a substitute for an in-game delivery/save test.
$ErrorActionPreference = 'Stop'
$pregRepository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$pregGame = [IO.Path]::GetFullPath((Join-Path $pregRepository '../..'))
$pregMods = [IO.Path]::GetFullPath((Join-Path $pregRepository '..'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $pregGame 'Assembly-CSharp.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $pregGame 'UnityStubs.dll'))
$pregForwarders = Get-ChildItem (Join-Path $pregRepository 'Compatibility/1.7/rjw/Assemblies') -Filter 'UnityEngine.*.dll'
foreach ($pregForwarder in $pregForwarders) { [void][Reflection.Assembly]::LoadFrom($pregForwarder.FullName) }
foreach ($pregSupport in @('0MultiplayerAPI.dll', '0PrepatcherAPI.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $pregMods ('rjw/1.6/Assemblies/' + $pregSupport)))
}
$pregProject = [xml](Get-Content (Join-Path $pregRepository '1.7/Source/XMT.csproj') -Raw)
foreach ($pregDependency in @('0Harmony', 'AlienRace', 'VEF')) {
    $pregHint = ($pregProject.Project.ItemGroup.Reference | Where-Object { $_.Include -like ($pregDependency + '*') }).HintPath
    [void][Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath((Join-Path $pregRepository ('1.7/Source/' + $pregHint))))
}
$pregRJW = [Reflection.Assembly]::LoadFrom((Join-Path $pregMods 'rjw/1.6/Assemblies/RJW.dll'))
$pregMenstruation = [Reflection.Assembly]::LoadFrom((Join-Path $pregMods 'rjw_menstruation/1.6/Assemblies/RJW_Menstruation.dll'))
$pregFlags = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'
$pregMethods = @(
    @($pregRJW, 'rjw.Hediff_BasePregnancy', @('Initialize', 'Tick', 'PostBirth')),
    @($pregRJW, 'rjw.SexPartAdder', @('MakePart')),
    @($pregRJW, 'rjw.Genital_Helper', @('has_vagina', 'get_genitalsBPR')),
    @($pregRJW, 'rjw.SexUtility', @('IngestFluids')),
    @($pregMenstruation, 'RJW_Menstruation.HediffComp_Menstruation', @('CompPostTick', 'TakeLoosePregnancy', 'Implant')),
    @($pregMenstruation, 'RJW_Menstruation.Hediff_MultiplePregnancy', @('GiveBirth', 'AddNewBaby')),
    @($pregMenstruation, 'RJW_Menstruation.MenstruationUtility', @('GetPregnancyIcon', 'GetSingleBabyImage')),
    @($pregMenstruation, 'RJW_Menstruation.PregnancyCommon', @('GetBabyInfo', 'SetupBabyXenotype', 'BabyPawnKindDecider')),
    @($pregMenstruation, 'RJW_Menstruation.Dialog_WombStatus', @('DoWindowContents'))
)
$pregMethodCount = 0
foreach ($pregItem in $pregMethods) {
    $pregType = $pregItem[0].GetType($pregItem[1], $true)
    foreach ($pregMethodName in $pregItem[2]) {
        $pregMethod = $pregType.GetMethod($pregMethodName, $pregFlags)
        if ($null -eq $pregMethod) { throw ('Missing upstream pregnancy API: ' + $pregItem[1] + '.' + $pregMethodName) }
        [Runtime.CompilerServices.RuntimeHelpers]::PrepareMethod($pregMethod.MethodHandle)
        $pregMethodCount++
    }
}
$pregForwardCount = 0
foreach ($pregForwarder in $pregForwarders) {
    $pregAssembly = [Reflection.Assembly]::LoadFrom($pregForwarder.FullName)
    foreach ($pregType in $pregAssembly.GetForwardedTypes()) {
        if ($pregType.Assembly.GetName().Name -ne 'UnityStubs') { throw ('Wrong facade: ' + $pregType.FullName) }
        $pregForwardCount++
    }
}
Write-Output ('PASS: ' + $pregMethodCount + ' upstream methods JIT; ' + $pregForwardCount + ' legacy Unity identities resolve to UnityStubs.')
