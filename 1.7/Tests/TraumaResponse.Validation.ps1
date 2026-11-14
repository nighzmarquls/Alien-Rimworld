# Executes the production response methods with deterministic random rolls and small engine substitutes.
# Does not simulate native job scheduling or Scribe; verify those in-game.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$source = Get-Content -Raw "$root/Source/Utilities/XMTUtility.cs"
$start = $source.IndexOf('        internal static bool IsTraumaCoward(')
$end = $source.IndexOf('        internal static void ThreatResponse(', $start)
if ($start -lt 0 -or $end -lt 0) { throw 'Production trauma methods not found' }
$methods = $source.Substring($start, $end - $start)
$interactionSource = Get-Content -Raw "$root/Source/Harmony/XMTInteractionPatches.cs"
$interactionStart = $interactionSource.IndexOf('        public static class Pawn_InteractionsTracker_TryInteractWith')
$interactionEnd = $interactionSource.IndexOf('        [HarmonyPatch(typeof(InteractionWorker_RomanceAttempt)', $interactionStart)
if ($interactionStart -lt 0 -or $interactionEnd -lt 0) { throw 'Production social interaction patch not found' }
$interaction = $interactionSource.Substring($interactionStart, $interactionEnd - $interactionStart) -replace '\[HarmonyPostfix\]', ''
$workers = Get-Content -Raw "$root/Source/AI/MentalBreak/Workers/MentalBreakWorker_Traumatized.cs"
$driver = Get-Content -Raw "$root/Source/AI/JobDriver/HumanJobs/JobDriver_TraumaCower.cs"
$substitutes = @'
using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
namespace UnityEngine {
    public static class Mathf { public static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value)); }
}
namespace Verse {
    [Flags] public enum WorkTags { Violent = 1 }
    public class Faction { }
    public class Map { }
    public class Thing { public bool Destroyed; public Map MapHeld; public Faction Faction; }
    public class JobDef { }
    public class MentalStateDef { }
    public class TraitDef { }
    public class Traits { public bool wimp; public bool HasTrait(TraitDef def) => wimp; }
    public class Story { public Traits traits = new Traits(); }
    public class Race { public bool Humanlike = true; public float manhunterOnDamageChance = 0.5f; }
    public class Mood { public float CurLevelPercentage = 0.5f; }
    public class Needs { public Mood mood = new Mood(); }
    public class WeaponDef { public bool IsRangedWeapon; }
    public class Weapon { public WeaponDef def = new WeaponDef(); }
    public class Equipment { public Weapon Primary; }
    public class Pawn : Thing {
        public bool Spawned = true, Dead, Downed, InMentalState, Drafted, awake = true, disabled, xeno, obsessed;
        public float trauma = 4f, triggerFactor = 1f, acidKnowledge;
        public Equipment equipment = new Equipment();
        public Story story = new Story(); public Race RaceProps = new Race(); public Needs needs = new Needs();
        public Map Map => MapHeld;
        public Pawn_JobTracker jobs = new Pawn_JobTracker(); public MindState mindState = new MindState();
        public Job CurJob => jobs.curJob; public JobDef CurJobDef => CurJob?.def;
        public bool Awake() => awake; public bool WorkTagIsDisabled(WorkTags tag) => disabled;
        public Xenomorphtype.CompPawnInfo info;
    }
    public class MindState { public MentalHandler mentalStateHandler = new MentalHandler(); }
    public class MentalHandler {
        public MentalStateDef started;
        public void TryStartMentalState(MentalStateDef def, string reason, bool forced, bool forceWake, bool other) { started = def; }
    }
    public static class Rand {
        public static Queue<bool> results = new Queue<bool>(); public static List<float> chances = new List<float>();
        public static bool Chance(float chance) { chances.Add(chance); return results.Dequeue(); }
    }
    public class TickManager { public int TicksGame = 1000; }
    public static class Find { public static TickManager TickManager = new TickManager(); }
    public static class Translations { public static string Translate(this string key) => key; }
}
namespace RimWorld {
    public static class TraitDefOf { public static TraitDef Wimp = new TraitDef(); }
    public static class JobDefOf { public static JobDef AttackStatic = new JobDef(); }
    public static class MentalStateDefOf {
        public static MentalStateDef PanicFlee = new MentalStateDef(), Wander_OwnRoom = new MentalStateDef();
    }
}
namespace Verse.AI {
    public enum JobCondition { InterruptForced }
    public class Job { public JobDef def; public Thing target; }
    public static class JobMaker { public static Job MakeJob(JobDef def, Thing target = null) => new Job { def = def, target = target }; }
    public class Pawn_JobTracker {
        public Job curJob; public int starts;
        public void StartJob(Job job, JobCondition condition) { curJob = job; starts++; }
    }
    public class Toil { public int duration; }
    public abstract class JobDriver {
        protected Pawn pawn;
        public abstract bool TryMakePreToilReservations(bool errorOnFailed);
        public abstract string GetReport();
        protected abstract IEnumerable<Toil> MakeNewToils();
    }
    public static class DriverExtensions { public static void FailOn(this JobDriver driver, Func<bool> fail) { } }
    public static class Toils_General { public static Toil Wait(int duration) => new Toil { duration = duration }; }
    public class MentalBreakWorker { public virtual bool BreakCanOccur(Pawn pawn) => true; }
}
namespace Xenomorphtype {
    public class CompPawnInfo {
        public Thing parent; public int NextTraumaCowerTick; public float scent;
        public float XenomorphPheromoneValue() => scent;
    }
    public static class PawnInfoExtension { public static CompPawnInfo Info(this Pawn pawn) => pawn.info; }
    public static class XMT_SlayerUtility { public static bool rage; public static bool TryRage(Pawn pawn, Thing horror) => rage; }
    public static class KnowledgeUtility {
        public static void TryShareKnowledge(Pawn teacher, Pawn student) { }
        public static float GetAcidRiskKnowledge(Pawn pawn) => pawn.acidKnowledge;
        public static KnowledgeAssessment GetAssessment(Pawn pawn) => new KnowledgeAssessment { traumaTriggerFactor = pawn.triggerFactor };
        public static bool IsObsessed(Pawn pawn) => pawn.obsessed;
        public static bool IsObsessed(Pawn pawn, out float trauma) { trauma = pawn.trauma; return pawn.obsessed; }
        public static float GetTrauma(Pawn pawn) => pawn.trauma;
    }
    public class KnowledgeAssessment { public float traumaTriggerFactor; }
    public static class XMTHiveUtility { public static bool XenosOnMap(Map map) => true; }
    public static class XenoWorkDefOf { public static JobDef XMT_TraumaCower = new JobDef(); }
    public static class XenoMentalStateDefOf { public static MentalStateDef XMT_MurderousRage = new MentalStateDef(); }
    public partial class XMTUtility { public static bool IsXenomorph(Pawn pawn) => pawn.xeno; }
    public static class TraumaRegression {
        private static int checks;
        private static void Check(bool result, string message) { checks++; if (!result) throw new Exception(message); }
        private static Pawn Pawn() {
            var pawn = new Pawn { MapHeld = new Map(), Faction = new Faction() };
            pawn.info = new CompPawnInfo { parent = pawn }; return pawn;
        }
        private static Thing Horror(Pawn pawn) => new Thing { MapHeld = pawn.Map, Faction = new Faction() };
        private static void Roll(bool violent, bool trigger = true) {
            Rand.results.Clear(); Rand.chances.Clear(); Rand.results.Enqueue(violent); Rand.results.Enqueue(trigger);
        }
        public static string Run() {
            var pawn = Pawn(); pawn.Drafted = true; var horror = Horror(pawn);
            Roll(true); XMTUtility.TraumaResponse(horror, pawn.info);
            Check(pawn.CurJobDef == JobDefOf.AttackStatic && pawn.CurJob.target == horror, "Violent result must force attack against the horror");
            Check(Rand.chances[0] == 0.5f && Rand.chances[1] == 0.5f, "Existing manhunter and trauma probability must survive");
            Roll(false); XMTUtility.TraumaResponse(horror, pawn.info);
            Check(pawn.CurJobDef == XenoWorkDefOf.XMT_TraumaCower && pawn.Drafted, "Cower must interrupt attack without undrafting");
            Check(pawn.info.NextTraumaCowerTick == 1600, "Cower must establish bounded recovery");
            int starts = pawn.jobs.starts;
            Roll(true); XMTUtility.TraumaResponse(horror, pawn.info);
            Check(pawn.jobs.starts == starts && Rand.chances.Count == 0, "Active cower must not restart or be replaced by repeated exposure");
            pawn.jobs.curJob = null; Find.TickManager.TicksGame = 1180;
            Roll(false); XMTUtility.TraumaResponse(horror, pawn.info);
            Check(pawn.CurJob == null, "Recovery must suppress a second cower");
            Roll(true); XMTUtility.TraumaResponse(horror, pawn.info);
            Check(pawn.CurJobDef == JobDefOf.AttackStatic, "Cower recovery must preserve violent responses");
            Find.TickManager.TicksGame = 1600;
            Roll(false); XMTUtility.TraumaResponse(horror, pawn.info);
            Check(pawn.CurJobDef == XenoWorkDefOf.XMT_TraumaCower, "Cower must become available at the recovery boundary");
            foreach (bool wimp in new[] { false, true }) {
                pawn = Pawn(); pawn.story.traits.wimp = wimp; pawn.disabled = !wimp;
                Roll(true); XMTUtility.TraumaResponse(Horror(pawn), pawn.info);
                Check(pawn.mindState.mentalStateHandler.started == MentalStateDefOf.PanicFlee && pawn.CurJob == null,
                    "Wimp and violence-incapable pawns must panic even on a violent roll");
                Check(new MentalBreakWorker_TraumatizedCoward().BreakCanOccur(pawn), "Cowards must retain the map-exit mood break");
            }
            pawn = Pawn();
            Check(!new MentalBreakWorker_TraumatizedCoward().BreakCanOccur(pawn), "Other pawns must not receive the map-exit mood break");
            Check(new MentalBreakWorker_Traumatized().BreakCanOccur(pawn), "Other trauma mood breaks must remain eligible");
            Roll(false, false); XMTUtility.TraumaResponse(Horror(pawn), pawn.info);
            Check(pawn.CurJob == null && pawn.mindState.mentalStateHandler.started == null, "Failed trauma roll must do nothing");
            pawn.RaceProps.Humanlike = false;
            Roll(false); XMTUtility.TraumaResponse(Horror(pawn), pawn.info);
            Check(pawn.mindState.mentalStateHandler.started == MentalStateDefOf.PanicFlee, "Animal panic must remain unchanged");
            foreach (string exemption in new[] { "mental", "downed", "asleep", "unspawned", "obsessed", "xeno", "slayer" }) {
                pawn = Pawn(); pawn.InMentalState = exemption == "mental"; pawn.Downed = exemption == "downed";
                pawn.awake = exemption != "asleep"; pawn.Spawned = exemption != "unspawned";
                pawn.obsessed = exemption == "obsessed"; pawn.xeno = exemption == "xeno";
                XMT_SlayerUtility.rage = exemption == "slayer";
                Roll(false); XMTUtility.TraumaResponse(Horror(pawn), pawn.info);
                Check(pawn.CurJob == null && pawn.mindState.mentalStateHandler.started == null, "Exemption failed: " + exemption);
            }
            XMT_SlayerUtility.rage = false;
            pawn = Pawn(); horror = Horror(pawn); horror.MapHeld = new Map();
            Roll(false); XMTUtility.TraumaResponse(horror, pawn.info);
            Check(pawn.CurJob == null && Rand.chances.Count == 0, "Invalid-map horror must be rejected before rolling");
            foreach (float trauma in new[] { 0f, 0.5f, 1f, 2f, 4f, 8f }) {
                pawn = Pawn(); pawn.trauma = trauma;
                Roll(false, false); XMTUtility.TraumaResponse(Horror(pawn), pawn.info, fromInterruption: true);
                Check(trauma == 0f ? Rand.chances.Count == 0 : Rand.chances[1] == Math.Min(1f, trauma * 0.25f),
                    "Interruption probability must scale with trauma and cap at one: " + trauma);
                Check(pawn.CurJob == null && pawn.mindState.mentalStateHandler.started == null,
                    "Failed interruption roll must leave the current job intact");
            }
            foreach (float factor in new[] { 0f, 0.5f, 2f }) {
                pawn = Pawn(); pawn.trauma = 1f; pawn.triggerFactor = factor;
                Roll(false, false); XMTUtility.TraumaResponse(Horror(pawn), pawn.info, fromInterruption: true);
                Check(Rand.chances[1] == 0.25f * factor, "Interruption must respect trauma-trigger modifiers");
            }
            pawn = Pawn(); horror = Horror(pawn);
            Roll(true); XMTUtility.TraumaResponse(horror, pawn.info, fromInterruption: true);
            Check(pawn.CurJobDef == JobDefOf.AttackStatic && pawn.CurJob.target == horror,
                "Interruption fight must target the cryptimorph");
            pawn = Pawn(); pawn.story.traits.wimp = true;
            Roll(true); XMTUtility.TraumaResponse(Horror(pawn), pawn.info, fromInterruption: true);
            Check(pawn.mindState.mentalStateHandler.started == MentalStateDefOf.PanicFlee,
                "Interruption must preserve coward flight behavior");
            pawn = Pawn(); pawn.obsessed = true;
            Roll(true); XMTUtility.TraumaResponse(Horror(pawn), pawn.info, fromInterruption: true);
            Check(Rand.chances.Count == 0 && pawn.CurJob == null, "Obsessed targets must not roll interruption trauma");
            foreach (bool ranged in new[] { false, true }) {
                foreach (float acidKnowledge in new[] { 0f, 0.01f, 1f }) {
                    pawn = Pawn(); pawn.acidKnowledge = acidKnowledge;
                    pawn.equipment.Primary = new Weapon { def = new WeaponDef { IsRangedWeapon = ranged } };
                    var cryptimorph = Pawn(); cryptimorph.xeno = true; cryptimorph.MapHeld = pawn.Map;
                    Roll(true); XMTUtility.TraumaResponse(cryptimorph, pawn.info, fromInterruption: true);
                    Check(pawn.CurJobDef == (acidKnowledge > 0f && !ranged ? XenoWorkDefOf.XMT_TraumaCower : JobDefOf.AttackStatic),
                        "Acid knowledge must block melee, preserve ranged violence, and allow ignorance: " + ranged + "/" + acidKnowledge);
                }
            }
            pawn = Pawn(); pawn.acidKnowledge = 1f; pawn.equipment = null;
            var acidHorror = Pawn(); acidHorror.xeno = true; acidHorror.MapHeld = pawn.Map;
            Roll(true); XMTUtility.TraumaResponse(acidHorror, pawn.info, fromInterruption: true);
            Check(pawn.CurJobDef == XenoWorkDefOf.XMT_TraumaCower, "Acid-aware unarmed pawns must not attack");
            pawn = Pawn(); pawn.acidKnowledge = 1f;
            Roll(true); XMTUtility.TraumaResponse(Horror(pawn), pawn.info, fromInterruption: true);
            Check(pawn.CurJobDef == JobDefOf.AttackStatic, "Acid knowledge must not block attacks on unrelated horrors");
            foreach (bool cryptimorphInitiates in new[] { false, true }) {
                foreach (float scent in new[] { -0.5f, 0f, 0.5f }) {
                    pawn = Pawn(); pawn.trauma = 2f; pawn.info.scent = scent;
                    var cryptimorph = Pawn(); cryptimorph.xeno = true; cryptimorph.MapHeld = pawn.Map;
                    Roll(true);
                    XMTInteractionPatches.Pawn_InteractionsTracker_TryInteractWith.Postfix(
                        cryptimorphInitiates ? pawn : cryptimorph, cryptimorphInitiates ? cryptimorph : pawn, true);
                    Check(pawn.CurJobDef == JobDefOf.AttackStatic && pawn.CurJob.target == cryptimorph,
                        "Social trauma must target the cryptimorph in either interaction direction regardless of scent");
                    Check(Rand.chances.Count == 2 && Rand.chances[1] == 0.5f,
                        "Social interaction must use exactly one trauma roll with established scaling");
                    Check((cryptimorph.mindState.mentalStateHandler.started == XenoMentalStateDefOf.XMT_MurderousRage) == (scent <= -0.5f),
                        "Existing cryptimorph scent rage threshold must survive");
                }
            }
            foreach (bool bothCryptimorphs in new[] { false, true }) {
                pawn = Pawn(); pawn.xeno = bothCryptimorphs;
                var other = Pawn(); other.xeno = bothCryptimorphs; other.MapHeld = pawn.Map;
                Roll(true); XMTInteractionPatches.Pawn_InteractionsTracker_TryInteractWith.Postfix(pawn, other, true);
                Check(Rand.chances.Count == 0, "Same-kind interactions must not roll cross-species trauma");
            }
            pawn = Pawn(); pawn.info.scent = -1f;
            var failedCryptimorph = Pawn(); failedCryptimorph.xeno = true; failedCryptimorph.MapHeld = pawn.Map;
            Roll(true); XMTInteractionPatches.Pawn_InteractionsTracker_TryInteractWith.Postfix(pawn, failedCryptimorph, false);
            Check(Rand.chances.Count == 0 && failedCryptimorph.mindState.mentalStateHandler.started == null,
                "Failed interactions must not trigger trauma or scent rage");
            XMTInteractionPatches.Pawn_InteractionsTracker_TryInteractWith.Postfix(null, failedCryptimorph, true);
            XMTInteractionPatches.Pawn_InteractionsTracker_TryInteractWith.Postfix(pawn, null, true);
            Check(Rand.chances.Count == 0, "Null participants must not trigger responses");
            return checks + " trauma response checks passed";
        }
    }
}
'@
$production = "namespace Xenomorphtype { public partial class XMTUtility {`n$methods`n} public class XMTInteractionPatches {`n$interaction`n} }"
$code = $substitutes + "`n" + $production + "`n" + ($workers -replace '(?m)^using [^\r\n]+;\r?\n', '') + "`n" + ($driver -replace '(?m)^using [^\r\n]+;\r?\n', '')
$outDir = Join-Path $root 'Source/obj/TraumaResponse'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$output = Join-Path $outDir ('TraumaResponse.' + [Guid]::NewGuid().ToString('N') + '.dll')
Add-Type -TypeDefinition $code -OutputAssembly $output
Add-Type -Path $output
[Xenomorphtype.TraumaRegression]::Run()

[xml]$jobs = Get-Content -Raw "$root/Defs/JobDefs/Jobs_Humans.xml"
$cower = @($jobs.Defs.JobDef | Where-Object defName -eq XMT_TraumaCower)
if ($cower.Count -ne 1 -or $cower.playerInterruptible -ne 'false' -or $cower.casualInterruptible -ne 'false' -or
    $cower.suspendable -ne 'false' -or $cower.neverFleeFromEnemies -ne 'true' -or $cower.checkOverrideOnDamage -ne 'Never') {
    throw 'Cower job interruption settings are invalid'
}
[xml]$breaks = Get-Content -Raw "$root/Defs/MentalStateDefs/MentalStates_Mood.xml"
$exitBreak = $breaks.Defs.MentalBreakDef | Where-Object defName -eq XMT_GiveUpExit
if ($exitBreak.workerClass -ne 'Xenomorphtype.MentalBreakWorker_TraumatizedCoward') { throw 'Wrong map-exit eligibility worker' }
if (@($breaks.Defs.MentalBreakDef | Where-Object workerClass -eq Xenomorphtype.MentalBreakWorker_TraumatizedCoward).Count -ne 1) {
    throw 'Coward restriction leaked to other trauma breaks'
}
'Trauma XML wiring checks passed'
