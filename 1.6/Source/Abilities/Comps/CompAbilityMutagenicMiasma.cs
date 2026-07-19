using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class Verb_CastMutagenicMiasma : Verb_CastAbility
    {
        public override void DrawHighlight(LocalTargetInfo target)
        {
            if (CasterPawn != null)
            {
                GenDraw.DrawRadiusRing(CasterPawn.Position, MutagenicMiasmaUtility.Range(CasterPawn));
            }

            Ability?.DrawEffectPreviews(target);
        }
    }

    public class Ability_MutagenicMiasma : Ability
    {
        public Ability_MutagenicMiasma()
        {
        }

        public Ability_MutagenicMiasma(Pawn pawn, AbilityDef def) : base(pawn, def)
        {
        }

        public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            bool activated = base.Activate(target, dest);
            if (activated)
            {
                ResetCooldown();
                StartCooldown(MutagenicMiasmaUtility.CooldownTicks(pawn));
            }

            return activated;
        }

        public override LocalTargetInfo AIGetAOETarget()
        {
            CompAbilityMutagenicMiasma comp = CompOfType<CompAbilityMutagenicMiasma>();
            if (comp == null || pawn?.Map == null)
            {
                return LocalTargetInfo.Invalid;
            }

            LocalTargetInfo bestTarget = LocalTargetInfo.Invalid;
            float bestScore = 0f;
            float range = MutagenicMiasmaUtility.Range(pawn);
            foreach (Pawn candidate in pawn.Map.mapPawns.AllPawnsSpawned)
            {
                if (candidate == pawn || !candidate.HostileTo(pawn) || !MutagenicMiasmaUtility.CanMutate(candidate)
                    || !candidate.Position.InHorDistOf(pawn.Position, range))
                {
                    continue;
                }

                float score = comp.ScoreTarget(candidate.Position);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = new LocalTargetInfo(candidate.Position);
                }
            }

            return bestTarget;
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            CompAbilityMutagenicMiasma comp = CompOfType<CompAbilityMutagenicMiasma>();
            return base.AICanTargetNow(target) && comp != null && comp.ScoreTarget(target.Cell) > 0f;
        }

        public override string Tooltip
        {
            get
            {
                string baseTooltip = base.Tooltip;
                if (pawn == null)
                {
                    return baseTooltip;
                }

                return baseTooltip + "\n\n" + "XMT_MutagenicMiasmaStats".Translate(
                    MutagenicMiasmaUtility.Range(pawn),
                    MutagenicMiasmaUtility.Angle(pawn),
                    MutagenicMiasmaUtility.CooldownTicks(pawn).ToStringTicksToPeriod());
            }
        }
    }

    public class CompAbilityMutagenicMiasma : CompAbilityEffect
    {
        private static readonly Color PreviewColor = new Color(0.18f, 0.19f, 0.21f, 0.75f);

        private new CompProperties_AbilityMutagenicMiasma Props => (CompProperties_AbilityMutagenicMiasma)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Map map = caster?.Map;
            if (map == null || !target.Cell.IsValid)
            {
                return;
            }

            HashSet<Pawn> affectedPawns = new HashSet<Pawn>();
            HashSet<Faction> angeredFactions = new HashSet<Faction>();
            foreach (IntVec3 cell in AffectedCells(target.Cell))
            {
                FilthMaker.TryMakeFilth(cell, map, Props.filthDef ?? InternalDefOf.Starbeast_Filth_Resin);
                ThrowMiasmaFleck(cell, map);

                foreach (Pawn pawn in cell.GetThingList(map).OfType<Pawn>().ToList())
                {
                    if (pawn == caster || !affectedPawns.Add(pawn) || !MutagenicMiasmaUtility.CanMutate(pawn))
                    {
                        continue;
                    }

                    RegisterHostileAct(caster, pawn, angeredFactions);
                    float protectionChance = MutagenicMiasmaUtility.ProtectionChance(pawn);
                    if (Rand.Chance(protectionChance))
                    {
                        MoteMaker.ThrowText(pawn.DrawPos, map, "XMT_MutagenicMiasmaProtected".Translate(protectionChance.ToStringPercent()), 1.9f);
                        continue;
                    }

                    Pawn mutationTarget = pawn;
                    BioUtility.TryMutatingPawn(ref mutationTarget, XenoGeneDefOf.XMT_HostMeatMutationSet);
                    ApplyFlu(pawn);
                }
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !target.Cell.IsValid || target.Cell == caster.Position)
            {
                return false;
            }

            float range = MutagenicMiasmaUtility.Range(caster);
            if (!target.Cell.InHorDistOf(caster.Position, range))
            {
                if (throwMessages)
                {
                    Messages.Message("XMT_MutagenicMiasmaOutOfRange".Translate(range), caster, MessageTypeDefOf.RejectInput, false);
                }

                return false;
            }

            return base.Valid(target, throwMessages);
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return target.Cell.IsValid && ScoreTarget(target.Cell) > 0f;
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            if (target.Cell.IsValid)
            {
                GenDraw.DrawFieldEdges(AffectedCells(target.Cell), PreviewColor);
            }
        }

        public float ScoreTarget(IntVec3 targetCell)
        {
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !targetCell.IsValid || targetCell == caster.Position
                || !targetCell.InHorDistOf(caster.Position, MutagenicMiasmaUtility.Range(caster)))
            {
                return float.MinValue;
            }

            float score = 0f;
            HashSet<Pawn> scored = new HashSet<Pawn>();
            foreach (IntVec3 cell in AffectedCells(targetCell))
            {
                foreach (Pawn pawn in cell.GetThingList(caster.Map).OfType<Pawn>())
                {
                    if (pawn == caster || !scored.Add(pawn) || !MutagenicMiasmaUtility.CanMutate(pawn))
                    {
                        continue;
                    }

                    float vulnerability = 1f - MutagenicMiasmaUtility.ProtectionChance(pawn);
                    if (pawn.HostileTo(caster))
                    {
                        score += vulnerability;
                    }
                    else
                    {
                        score -= 1.5f + vulnerability;
                    }
                }
            }

            return score;
        }

        private List<IntVec3> AffectedCells(IntVec3 targetCell)
        {
            List<IntVec3> cells = new List<IntVec3>();
            Pawn caster = parent.pawn;
            Map map = caster?.Map;
            if (map == null || !targetCell.IsValid || targetCell == caster.Position)
            {
                return cells;
            }

            float range = MutagenicMiasmaUtility.Range(caster);
            float halfAngle = MutagenicMiasmaUtility.Angle(caster) * 0.5f;
            Vector3 forward = (targetCell - caster.Position).ToVector3();
            forward.y = 0f;
            HashSet<IntVec3> uniqueCells = new HashSet<IntVec3>();

            if (halfAngle > 0f)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(caster.Position, range, false))
                {
                    Vector3 offset = (cell - caster.Position).ToVector3();
                    offset.y = 0f;
                    if (Vector3.Angle(forward, offset) <= halfAngle + 0.01f)
                    {
                        TryAddCell(cell, range, map, uniqueCells, cells);
                    }
                }
            }

            IntVec3 lineEnd = caster.Position + new IntVec3(
                Mathf.RoundToInt(forward.normalized.x * range),
                0,
                Mathf.RoundToInt(forward.normalized.z * range));
            foreach (IntVec3 cell in GenSight.PointsOnLineOfSight(caster.Position, lineEnd))
            {
                if (cell != caster.Position)
                {
                    TryAddCell(cell, range, map, uniqueCells, cells);
                }
            }

            return cells;
        }

        private void TryAddCell(IntVec3 cell, float range, Map map, HashSet<IntVec3> uniqueCells, List<IntVec3> cells)
        {
            Pawn caster = parent.pawn;
            if (!cell.InBounds(map) || !cell.InHorDistOf(caster.Position, range) || uniqueCells.Contains(cell))
            {
                return;
            }

            if (cell.Filled(map) && !Props.canHitFilledCells)
            {
                return;
            }

            if (!parent.verb.TryFindShootLineFromTo(caster.Position, new LocalTargetInfo(cell), out ShootLine _, true))
            {
                return;
            }

            uniqueCells.Add(cell);
            cells.Add(cell);
        }

        private void ThrowMiasmaFleck(IntVec3 cell, Map map)
        {
            FleckDef fleckDef = Props.fleckDef ?? InternalDefOf.XMT_MutagenicMiasmaSmoke;
            if (fleckDef == null)
            {
                return;
            }

            Vector3 position = cell.ToVector3Shifted() + Gen.RandomHorizontalVector(0.25f);
            FleckCreationData data = FleckMaker.GetDataStatic(position, map, fleckDef, Rand.Range(0.85f, 1.15f));
            data.rotation = Rand.Range(0f, 360f);
            data.rotationRate = Rand.Range(-30f, 30f);
            data.velocityAngle = Rand.Range(30f, 40f);
            data.velocitySpeed = Rand.Range(0.25f, 0.45f);
            map.flecks.CreateFleck(data);
        }

        private void ApplyFlu(Pawn pawn)
        {
            HediffDef fluDef = Props.fluHediff ?? InternalDefOf.XMT_Flu;
            if (pawn?.health == null || fluDef == null)
            {
                return;
            }

            Hediff flu = pawn.health.hediffSet.GetFirstHediffOfDef(fluDef);
            if (flu == null)
            {
                flu = HediffMaker.MakeHediff(fluDef, pawn);
                pawn.health.AddHediff(flu);
                flu.Severity = 0.5f;
            }

            if (flu is HediffWithComps fluWithComps)
            {
                fluWithComps.TryGetComp<HediffComp_MiasmaMinimumDuration>()?.Refresh(Props.minimumFluDurationTicks);
            }
        }

        private static void RegisterHostileAct(Pawn caster, Pawn target, HashSet<Faction> angeredFactions)
        {
            Faction casterFaction = caster?.Faction;
            Faction targetFaction = target?.Faction;
            if (casterFaction == null || targetFaction == null || casterFaction == targetFaction || target.HostileTo(caster)
                || !angeredFactions.Add(targetFaction))
            {
                return;
            }

            int goodwillChange = targetFaction.GoodwillToMakeHostile(casterFaction);
            targetFaction.TryAffectGoodwillWith(casterFaction, goodwillChange, canSendMessage: true,
                canSendHostilityLetter: true, HistoryEventDefOf.UsedHarmfulAbility);
        }
    }

    public class CompProperties_AbilityMutagenicMiasma : CompProperties_AbilityEffect
    {
        public ThingDef filthDef;
        public FleckDef fleckDef;
        public HediffDef fluHediff;
        public int minimumFluDurationTicks = 120000;
        public bool canHitFilledCells = true;

        public CompProperties_AbilityMutagenicMiasma()
        {
            compClass = typeof(CompAbilityMutagenicMiasma);
        }
    }

    public static class MutagenicMiasmaUtility
    {
        private const float BaselinePower = 66f;
        private const float SovereignPower = 114f;

        private static StatDef vacuumResistance;
        private static StatDef decompressionResistance;
        private static StatDef toxicEnvironmentResistance;

        public static float Power(Pawn pawn)
        {
            if (pawn == null || XenoStatDefOf.XMT_HereditaryCapacity == null)
            {
                return 0f;
            }

            return pawn.BodySize * pawn.GetStatValue(XenoStatDefOf.XMT_HereditaryCapacity);
        }

        public static int Range(Pawn pawn)
        {
            return Mathf.Clamp(Mathf.RoundToInt(3f + (Power(pawn) - BaselinePower) / 9.6f), 1, 12);
        }

        public static float Angle(Pawn pawn)
        {
            return Mathf.Clamp((Power(pawn) - BaselinePower) * 90f / (SovereignPower - BaselinePower), 0f, 180f);
        }

        public static int CooldownTicks(Pawn pawn)
        {
            return Mathf.Max(Mathf.RoundToInt(1250f * SovereignPower / Mathf.Max(Power(pawn), 1f)), 60);
        }

        public static bool CanMutate(Pawn pawn)
        {
            XMT_MutationsHealthSet set = XenoGeneDefOf.XMT_HostMeatMutationSet;
            return pawn != null && set?.mutations != null
                && set.mutations.Any(mutation => BioUtility.CanApplyMutation(pawn, mutation).Accepted);
        }

        public static float ProtectionChance(Pawn pawn)
        {
            float apparelProtection = Mathf.Min(ApparelCoverage(pawn), 0.5f);
            float vacuum = Mathf.Max(GetStatValue(pawn, VacuumResistance), GetStatValue(pawn, DecompressionResistance));
            float resistanceMitigation = Mathf.Clamp01(0.5f * vacuum + 0.5f * GetStatValue(pawn, ToxicEnvironmentResistance));
            return Mathf.Clamp01(apparelProtection + (1f - apparelProtection) * resistanceMitigation);
        }

        public static float ApparelCoverage(Pawn pawn)
        {
            if (pawn?.apparel?.WornApparel == null || pawn.health?.hediffSet == null)
            {
                return 0f;
            }

            float totalCoverage = 0f;
            float covered = 0f;
            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts().Where(part => part.depth == BodyPartDepth.Outside))
            {
                float coverage = Mathf.Max(part.coverageAbs, 0f);
                totalCoverage += coverage;
                if (pawn.apparel.WornApparel.Any(apparel => apparel.def.apparel?.CoversBodyPart(part) == true))
                {
                    covered += coverage;
                }
            }

            return totalCoverage > 0f ? Mathf.Clamp01(covered / totalCoverage) : 0f;
        }

        private static StatDef VacuumResistance => vacuumResistance ??= DefDatabase<StatDef>.GetNamedSilentFail("VacuumResistance");
        private static StatDef DecompressionResistance => decompressionResistance ??= DefDatabase<StatDef>.GetNamedSilentFail("DecompressionResistance");
        private static StatDef ToxicEnvironmentResistance => toxicEnvironmentResistance ??= DefDatabase<StatDef>.GetNamedSilentFail("ToxicEnvironmentResistance");

        private static float GetStatValue(Pawn pawn, StatDef stat)
        {
            return pawn != null && stat != null ? Mathf.Clamp01(pawn.GetStatValue(stat)) : 0f;
        }
    }

    public class HediffComp_MiasmaMinimumDuration : HediffComp
    {
        private int minimumUntilTick = -1;

        public void Refresh(int durationTicks)
        {
            minimumUntilTick = Mathf.Max(minimumUntilTick, Find.TickManager.TicksGame + Mathf.Max(durationTicks, 0));
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (minimumUntilTick <= Find.TickManager.TicksGame)
            {
                return;
            }

            float minimumSeverity = Mathf.Max(parent.def.minSeverity + 0.0001f, 0.001f);
            if (parent.Severity + severityAdjustment < minimumSeverity)
            {
                severityAdjustment = minimumSeverity - parent.Severity;
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref minimumUntilTick, "minimumUntilTick", -1);
        }
    }

    public class HediffCompProperties_MiasmaMinimumDuration : HediffCompProperties
    {
        public HediffCompProperties_MiasmaMinimumDuration()
        {
            compClass = typeof(HediffComp_MiasmaMinimumDuration);
        }
    }
}
