param([switch]$ApplyHarmonyPatches)
$ErrorActionPreference = 'Stop'
$modRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sourceRoot = Join-Path $modRoot '1.7/Source'
[xml]$project = Get-Content -LiteralPath (Join-Path $sourceRoot 'XMT.csproj') -Raw
foreach ($reference in $project.Project.ItemGroup.Reference) {
    if ($reference.HintPath) {
        [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath((Join-Path $sourceRoot $reference.HintPath))) > $null
    }
}
$modAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $modRoot '1.7/Assemblies/XMT.dll'))
$assemblies = @([AppDomain]::CurrentDomain.GetAssemblies())
$flags = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static'
$script:checks = 0
function Check($condition, [string]$message) {
    if (!$condition) { throw $message }
    $script:checks++
}
function Resolve-Type([string]$name) {
    foreach ($candidate in @($name, "Verse.$name", "Verse.AI.$name", "RimWorld.$name")) {
        foreach ($assembly in $assemblies) {
            $type = $assembly.GetType($candidate, $false)
            if ($type) { return $type }
        }
    }
    return $null
}
function Find-Field([Type]$type, [string]$name) {
    while ($type) {
        $field = $type.GetField($name, $flags)
        if ($field) { return $field }
        $type = $type.BaseType
    }
    return $null
}
$defBase = Resolve-Type 'Def'
$defIndex = @{}
$parents = @{}
$documents = [Collections.Generic.List[xml]]::new()
$gameRoot = [IO.Path]::GetFullPath((Join-Path $modRoot '../..'))
$workshopRoot = [IO.Path]::GetFullPath((Join-Path $gameRoot '../../workshop/content/294100'))
$defRoots = @((Join-Path $gameRoot 'Data'), (Join-Path $modRoot 'Common/Defs'), (Join-Path $modRoot '1.7/Defs'))
foreach ($integration in @('VRE_Android','Yautja')) { $defRoots += Join-Path $modRoot "Compatibility/1.7/$integration/Defs" }
# These are XML-only optional dependencies, not build references. Read their routed 1.7 content.
foreach ($id in @('1814383360','1814987817','2975771801','3536839586')) {
    $dependencyRoot = Join-Path $workshopRoot $id
    $folder = if (Test-Path (Join-Path $dependencyRoot '1.7/Defs')) { '1.7/Defs' } else { '1.6/Defs' }
    if (Test-Path (Join-Path $dependencyRoot $folder)) { $defRoots += Join-Path $dependencyRoot $folder }
}
foreach ($root in $defRoots) {
    if (!(Test-Path -LiteralPath $root)) { continue }
    foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -Filter '*.xml') {
        [xml]$document = Get-Content -LiteralPath $file.FullName -Raw
        $documents.Add($document)
        foreach ($node in $document.SelectNodes('/Defs/*')) {
            if ($node.defName) { $defIndex[[string]$node.defName] = $node }
            if ($node.HasAttribute('Name')) { $parents[$node.GetAttribute('Name')] = $node }
        }
    }
}
function Validate-Object($node, [Type]$type, [string]$location) {
    if ($node.HasAttribute('Class')) {
        $concrete = Resolve-Type $node.GetAttribute('Class')
        Check ($null -ne $concrete) "$location has unknown XML Class $($node.GetAttribute('Class'))"
        Check ($type.IsAssignableFrom($concrete)) "$location has incompatible XML Class $concrete for $type"
        $type = $concrete
    }
    if ([Nullable]::GetUnderlyingType($type)) { $type = [Nullable]::GetUnderlyingType($type) }
    $children = @($node.ChildNodes | Where-Object NodeType -eq Element)
    if ($type.FullName -eq 'Xenomorphtype.XMT_TraitAssignment' -and $defIndex.ContainsKey([string]$node.trait)) {
        $traitDefinition = $defIndex[[string]$node.trait]
        $traitDegrees = @($traitDefinition.SelectNodes('degreeDatas/li') | ForEach-Object { [int]$_.degree })
        if ($traitDegrees.Count -gt 0) {
            Check ($traitDegrees -contains [int]$node.degree) "$location assigns undefined degree $([int]$node.degree) to trait $($node.trait)"
        }
    }
    if ($type.Name -eq 'ThingDefCountClass') {
        Check ($children.Count -eq 0) "$location must use RimWorld's custom def/count XML format"
        $reference = if ($node.LocalName -eq 'li') { $node.InnerText.Trim() } else { $node.LocalName }
        Check ($defIndex.ContainsKey($reference)) "$location references missing def $reference"
        if ($node.LocalName -ne 'li') {
            $countValue = 0
            Check ([int]::TryParse($node.InnerText, [ref]$countValue) -and $countValue -gt 0) "$location has an invalid item count"
        }
        return
    }
    if ($type -eq [Type]) {
        Check ($null -ne (Resolve-Type $node.InnerText)) "$location has unknown worker type $($node.InnerText)"
        return
    }
    if ($type.IsPrimitive -or $type.IsEnum -or $type -eq [string] -or $type.Name -in @('IntRange','FloatRange','NameTriple')) { return }
    if ($defBase.IsAssignableFrom($type) -and $children.Count -eq 0 -and !$node.HasAttribute('Abstract')) {
        if ($node.InnerText.Trim()) {
            $optionalGuard = $node.SelectSingleNode('ancestor-or-self::*[@MayRequire or @MayRequireAnyOf]')
            Check ($defIndex.ContainsKey($node.InnerText.Trim()) -or $null -ne $optionalGuard) "$location references missing def $($node.InnerText)"
        }
        return
    }
    if ($type.IsGenericType -and $type.GetGenericTypeDefinition() -eq [Collections.Generic.List``1]) {
        foreach ($child in $children) {
            # Vanilla's custom backstory/trait loaders use named XML elements instead of li.
            if ($child.LocalName -eq 'li' -or $type.GenericTypeArguments[0].Name -eq 'ThingDefCountClass') {
                Validate-Object $child $type.GenericTypeArguments[0] "$location/$($child.LocalName)"
            }
        }
        return
    }
    if ($type.Namespace -notmatch '^(Xenomorphtype|AlienRace|Verse|RimWorld)' -or $type.Name -match 'Dictionary|List') { return }
    foreach ($child in $children) {
        $field = Find-Field $type $child.LocalName
        if (!$field -and $child.LocalName -in @('title','titleShort','forcedTraits','skillGains','requiredWorkTags')) { continue }
        Check ($null -ne $field) "$location/$($child.LocalName) is not a field on $type"
        Validate-Object $child $field.FieldType "$location/$($child.LocalName)"
    }
}
$changedXml = @(
    '1.7/Defs/HediffDefs/Hediffs_Part_Cosmetic.xml',
    '1.7/Defs/TraitDefs/Traits_Singular.xml','1.7/Defs/Knowledge/KnowledgeDefs.xml',
    '1.7/Defs/ThoughtDefs/Thoughts_Victims.xml','1.7/Defs/MentalStateDefs/MentalStates_Mood.xml',
    '1.7/Defs/BackstoryDefs/BackstoryCompanionDefs.xml','1.7/Defs/BackstoryDefs/Human_Bios.xml',
    '1.7/Defs/PawnRelationDefs/CompanionRelationDefs.xml','1.7/Defs/ThoughtDefs/Thoughts_CompanionRelations.xml',
    '1.7/Defs/DutyDefs/Duties_CompanionRecovery.xml','1.7/Defs/JobDefs/Jobs_CompanionRecovery.xml',
    '1.7/Defs/BackstoryDefs/Human_Child_Story.xml','1.7/Defs/BackstoryDefs/Human_Adult_Story.xml',
    'Compatibility/1.7/VRE_Android/Defs/Hediffs_Converters.xml','Compatibility/1.7/Yautja/Defs/Hediffs_Dutch.xml'
)
$changedXml += @(Get-ChildItem -LiteralPath (Join-Path $modRoot '1.7/Defs/WorldActivities') -Filter '*.xml' |
    ForEach-Object { '1.7/Defs/WorldActivities/' + $_.Name })
foreach ($relative in $changedXml) {
    [xml]$document = Get-Content -LiteralPath (Join-Path $modRoot $relative) -Raw
    foreach ($node in $document.SelectNodes('/Defs/*')) {
        # The existing maid setup contains genes generated from vanilla templates at load time.
        # Keep this feature's semantic checks scoped to its relocated setup defs.
        if ($relative -eq '1.7/Defs/HediffDefs/Hediffs_Part_Cosmetic.xml' -and
            $node.GetAttribute('Name') -ne 'XMT_PawnSetupBase' -and [string]$node.defName -notlike 'XMT_Setup*') { continue }
        $type = Resolve-Type $node.LocalName
        Check ($null -ne $type) "$relative has unknown def type $($node.LocalName)"
        if ($node.HasAttribute('ParentName')) { Check ($parents.ContainsKey($node.GetAttribute('ParentName'))) "$relative has missing XML parent $($node.GetAttribute('ParentName'))" }
        Validate-Object $node $type "$relative/$($node.defName)"
    }
}
# Regression: BodyPurist is a separate degree-zero trait, not the negative end of Transhumanist.
[xml]$invalidTraitFixture = '<li><trait>BodyPurist</trait><degree>-1</degree></li>'
$invalidTraitRejected = $false
try { Validate-Object $invalidTraitFixture.DocumentElement (Resolve-Type 'Xenomorphtype.XMT_TraitAssignment') 'InvalidTraitFixture' }
catch {
    if ($_.Exception.Message -notlike '*assigns undefined degree -1 to trait BodyPurist*') { throw }
    $invalidTraitRejected = $true
}
Check $invalidTraitRejected 'Trait-degree validation did not reject the invalid Kavalier trait regression'
Write-Output "PASS XML structure, worker types, trait degrees and referenced defs ($checks checks)"
foreach ($patchCase in @(
    @('1.7/Patches/SlayerRage.xml', 'Data/Core/Defs/ThinkTreeDefs/SubTrees_Misc.xml', 'ThinkNode'),
    @('Compatibility/1.7/Yautja/Patches/Dutch.xml', '1.7/Defs/BackstoryDefs/Human_Bios.xml', 'HediffDef')
)) {
    [xml]$patchDocument = Get-Content -LiteralPath (Join-Path $modRoot $patchCase[0]) -Raw
    $targetPath = if ($patchCase[1].StartsWith('Data/')) { Join-Path $gameRoot $patchCase[1] } else { Join-Path $modRoot $patchCase[1] }
    [xml]$targetDocument = Get-Content -LiteralPath $targetPath -Raw
    Check ($targetDocument.SelectNodes($patchDocument.Patch.Operation.xpath).Count -eq 1) "Patch target missing or ambiguous: $($patchCase[0])"
    foreach ($value in $patchDocument.Patch.Operation.value.ChildNodes | Where-Object NodeType -eq Element) {
        Validate-Object $value (Resolve-Type $patchCase[2]) $patchCase[0]
    }
}
foreach ($familyPair in @(@('XMT_AdoptiveParent','Parent'), @('XMT_AdoptiveChild','Child'), @('XMT_AdoptiveSibling','Sibling'))) {
    foreach ($field in @('diedThought','diedThoughtFemale','lostThought','lostThoughtFemale','killedThought','killedThoughtFemale','opinionOffset')) {
        Check ([string]$defIndex[$familyPair[0]].$field -eq [string]$defIndex[$familyPair[1]].$field) "$($familyPair[0]) differs from vanilla family field $field"
    }
}
foreach ($relative in $changedXml | Where-Object { $_.StartsWith('1.7/') }) {
    [xml]$baseOnly = Get-Content -LiteralPath (Join-Path $modRoot $relative) -Raw
    foreach ($optional in @($baseOnly.SelectNodes('//*[@MayRequire]'))) { $optional.ParentNode.RemoveChild($optional) > $null }
    $optionalLeaks = @($baseOnly.SelectNodes('//*[not(*)]') | Where-Object { $_.InnerText -match '^(VREA_|VAE_Apparel_|VWE_Gun_|ABYautja|Gun_Incinerator$|Apparel_ShirtRuffle$|Apparel_VestRoyal$|XMT_ConvertHumanTo)' })
    Check ($optionalLeaks.Count -eq 0) "$relative leaks optional content into its base-game fallback"
}
$keyIndex = @{}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $modRoot '1.7/Languages/English/Keyed') -Filter '*.xml') {
    [xml]$keys = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($key in $keys.SelectNodes('/LanguageData/*')) { $keyIndex[$key.LocalName] = $true }
}
foreach ($source in Get-ChildItem -LiteralPath (Join-Path $sourceRoot 'WorldActivities') -Filter '*.cs') {
    foreach ($key in [regex]::Matches((Get-Content -LiteralPath $source.FullName -Raw),'"(XMT_\w+)"\s*\.Translate\(')) {
        Check ($keyIndex.ContainsKey($key.Groups[1].Value)) "Missing keyed translation $($key.Groups[1].Value)"
    }
}
Write-Output 'PASS patch targets, family mood parity, base-game fallbacks and translation keys'
$patchTypes = [Collections.Generic.List[Type]]::new()
foreach ($source in Get-ChildItem -LiteralPath (Join-Path $sourceRoot 'WorldActivities') -Filter '*.cs') {
    $sourceText = Get-Content -LiteralPath $source.FullName -Raw
    foreach ($match in [regex]::Matches($sourceText,'\bclass\s+(XMT_\w+Patch)\b')) {
        $type = $modAssembly.GetType("Xenomorphtype.$($match.Groups[1].Value)", $true)
        $attributes = @($type.GetCustomAttributes([HarmonyLib.HarmonyPatch], $false))
        if (!$attributes.Count) { continue }
        $info = $attributes[0].info
        $methods = @($info.declaringType.GetMethods($flags) | Where-Object Name -eq $info.methodName)
        if ($null -ne $info.argumentTypes) {
            $methods = @($methods | Where-Object {
                $parameters = $_.GetParameters()
                if ($parameters.Count -ne $info.argumentTypes.Count) { return $false }
                for ($i = 0; $i -lt $parameters.Count; $i++) { if ($parameters[$i].ParameterType -ne $info.argumentTypes[$i]) { return $false } }
                return $true
            })
        }
        Check ($methods.Count -eq 1) "$type must identify exactly one original method, found $($methods.Count)"
        $original = $methods[0]
        foreach ($patch in $type.GetMethods($flags) | Where-Object Name -in @('Prefix','Postfix')) {
            foreach ($parameter in $patch.GetParameters()) {
                $name = $parameter.Name
                if ($name -match '^___(.+)$') { Check ($null -ne (Find-Field $info.declaringType $Matches[1])) "$type binds a missing private field $name" }
                elseif ($name -match '^__(\d+)$') { Check ([int]$Matches[1] -lt $original.GetParameters().Count) "$type binds a missing argument $name" }
                elseif ($name -notmatch '^__') { Check ($original.GetParameters().Name -contains $name) "$type binds unknown argument $name" }
            }
        }
        $patchTypes.Add($type)
    }
}
Write-Output "PASS $($patchTypes.Count) Harmony targets and parameter bindings"
if ($ApplyHarmonyPatches) {
    $harmony = [HarmonyLib.Harmony]::new('XMT.Tests.WorldActivities')
    try {
        foreach ($type in $patchTypes) { $harmony.CreateClassProcessor($type).Patch() > $null }
        Write-Output "PASS all new Harmony patches applied in isolated validation process"
    }
    finally { $harmony.UnpatchAll('XMT.Tests.WorldActivities') }
}
Write-Output "PASS $checks assertions. Map/job execution and real Scribe round-trips still require in-game validation."
