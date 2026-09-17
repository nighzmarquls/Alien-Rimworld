using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public sealed class LordJob_NemesisPowerSabotage : LordJob_NemesisMission
    {
        private List<Thing> targetProviders = new List<Thing>();
        private List<Thing> targetConsumers = new List<Thing>();
        private List<Thing> cachedTargets = new List<Thing>();
        private List<Thing> inaccessibleTargets = new List<Thing>();
        private int targetCursor;
        private int connectivityCheckTick = -1;
        private float requiredConsumerDisableFraction = -1f;

        public override bool Successful => missionSuccessCount > 0;

        public override void Begin()
        {
            base.Begin();
            if (requiredConsumerDisableFraction < 0f)
                requiredConsumerDisableFraction = Rand.Range(0.5f, 0.75f);
            if (targetProviders.Count == 0 && targetConsumers.Count == 0 && cachedTargets.Count == 0)
            {
                XMTPowerSabotageUtility.Snapshot(lord.Map, out targetProviders, out targetConsumers, out cachedTargets);
                NemesisLog.Detail("Mission", "Power sabotage snapshot providers="
                    + string.Join(", ", targetProviders.Select(provider => provider.LabelShort + "@" + provider.Position))
                    + " consumers=" + string.Join(", ", targetConsumers.Select(consumer => consumer.LabelShort + "@" + consumer.Position))
                    + " requiredDisabled=" + requiredConsumerDisableFraction.ToStringPercent()
                    + " providerTargets=" + cachedTargets.Count(targetProviders.Contains)
                    + " conduitTargets=" + cachedTargets.Count(target => !targetProviders.Contains(target)));
            }
        }

        public override bool AllowThreatResponse(Pawn pawn, Thing aggressor)
        {
            Withdraw("combat engaged");
            return false;
        }

        public override bool Notify_MissionDamage(Pawn pawn, DamageInfo damage)
        {
            Notify_Revealed(pawn);
            Withdraw("combat engaged");
            return false;
        }

        public override void Notify_SabotageTargetResolved(Pawn attacker, Thing target)
        {
            connectivityCheckTick = Find.TickManager.TicksGame + 30;
        }

        public override void Notify_SabotageTargetInaccessible(Pawn attacker, Thing target)
        {
            if (target == null || inaccessibleTargets.Contains(target)) return;
            inaccessibleTargets.Add(target);
            NemesisLog.Detail("Mission", "Power sabotage refusing inaccessible target=" + target.LabelShort + "@"
                + target.Position + " pawn=" + attacker);
        }

        public override void Notify_SabotageAccessChanged(Pawn attacker, IntVec3 goalCell)
        {
            inaccessibleTargets.RemoveAll(target => target != null && targetProviders.Contains(target)
                && target.Position == goalCell);
        }

        protected override void TickMission(List<Pawn> pawns, int tick)
        {
            if (connectivityCheckTick >= 0 && tick >= connectivityCheckTick)
            {
                connectivityCheckTick = -1;
                if (SuccessReached(out int disabled, out int total))
                {
                    IncrementMissionSuccess(1, disabled + "/" + total + " targeted turret/light consumers disabled; required "
                        + requiredConsumerDisableFraction.ToStringPercent());
                    Withdraw("power grid disrupted");
                    ReportOutcome();
                }
            }

            if (!withdrawing)
                foreach (Pawn pawn in pawns.Where(pawn => !pawn.Downed
                    && (pawn.CurJobDef == JobDefOf.Wait || pawn.CurJobDef == JobDefOf.Wait_MaintainPosture)))
                {
                    Job replacement = GetMissionJob(pawn);
                    if (replacement?.def != null && replacement.def != JobDefOf.Wait)
                        pawn.jobs.StartJob(replacement, JobCondition.InterruptForced);
                }
        }

        protected override Job GetOperationalJob(Pawn pawn)
        {
            // A completed sabotage schedules exactly one connectivity traversal. Do not escalate to a provider
            // while that cable action may already have completed the mission.
            if (connectivityCheckTick >= 0) return Wait();

            if (cachedTargets.Count == 0)
            {
                if (SuccessReached(out int disabled, out int total))
                {
                    if (missionSuccessCount == 0) IncrementMissionSuccess(1, disabled + "/" + total
                        + " targeted turret/light consumers disabled; required " + requiredConsumerDisableFraction.ToStringPercent());
                    Withdraw("power grid disrupted");
                    ReportOutcome();
                }
                else
                {
                    Withdraw("cached sabotage targets exhausted");
                    ReportOutcome();
                }
                return Wait();
            }

            // Conduits are the cheap, covert objective. Providers are considered only after no cached conduit
            // can currently be acted on, and structural recovery is the final provider-only fallback.
            Thing selected = FindCachedTarget(pawn, providersOnly: false)
                ?? FindCachedTarget(pawn, providersOnly: true);
            if (selected != null)
                return SabotageJob(selected);

            return FindProviderRecoveryJob(pawn) ?? Wait();
        }

        private static Job SabotageJob(Thing target)
        {
            Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_Sabotage, target);
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            return job;
        }

        private Job FindProviderRecoveryJob(Pawn pawn)
        {
            CompMatureMorph morph = pawn.GetMorphComp();
            if (morph == null) return null;

            int remainingToExamine = cachedTargets.Count;
            while (remainingToExamine > 0 && cachedTargets.Count > 0)
            {
                if (targetCursor >= cachedTargets.Count) targetCursor = 0;
                Thing target = cachedTargets[targetCursor];
                if (XMTPowerSabotageUtility.PermanentlyInvalid(target, targetProviders))
                {
                    cachedTargets.RemoveAt(targetCursor);
                    remainingToExamine--;
                    continue;
                }
                targetCursor = (targetCursor + 1) % cachedTargets.Count;
                remainingToExamine--;
                if (!targetProviders.Contains(target)
                    || !XMTPowerSabotageUtility.TemporarilyEligible(pawn, target, requireDarkness: false)) continue;
                if (!inaccessibleTargets.Contains(target)
                    && pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly)) continue;

                Job intendedSabotage = SabotageJob(target);
                morph.NotifyPathFailure(new LocalTargetInfo(target), intendedSabotage);
                if (!morph.TryGetPathRecoveryJob(out Job recovery)) continue;
                recovery.locomotionUrgency = LocomotionUrgency.Sprint;
                NemesisLog.Detail("Mission", "Power sabotage breaching toward inaccessible provider="
                    + target.LabelShort + "@" + target.Position + " pawn=" + pawn + " recovery=" + recovery.def);
                return recovery;
            }
            return null;
        }

        private Thing FindCachedTarget(Pawn pawn, bool providersOnly)
        {
            int remainingToExamine = cachedTargets.Count;
            while (remainingToExamine > 0 && cachedTargets.Count > 0)
            {
                if (targetCursor >= cachedTargets.Count) targetCursor = 0;
                Thing target = cachedTargets[targetCursor];
                if (XMTPowerSabotageUtility.PermanentlyInvalid(target, targetProviders))
                {
                    cachedTargets.RemoveAt(targetCursor);
                    remainingToExamine--;
                    continue;
                }
                targetCursor = (targetCursor + 1) % cachedTargets.Count;
                remainingToExamine--;
                if (targetProviders.Contains(target) != providersOnly) continue;
                if (inaccessibleTargets.Contains(target)) continue;
                if (!XMTPowerSabotageUtility.TemporarilyAvailable(pawn, target, requireDarkness: !providersOnly)) continue;
                return target;
            }
            return null;
        }

        private bool SuccessReached(out int disabled, out int total)
            => XMTPowerSabotageUtility.MeetsSuccessThreshold(targetProviders, targetConsumers,
                requiredConsumerDisableFraction, out disabled, out total);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref targetProviders, "targetProviders", LookMode.Reference);
            Scribe_Collections.Look(ref targetConsumers, "targetConsumers", LookMode.Reference);
            Scribe_Collections.Look(ref cachedTargets, "cachedTargets", LookMode.Reference);
            Scribe_Collections.Look(ref inaccessibleTargets, "inaccessibleTargets", LookMode.Reference);
            Scribe_Values.Look(ref targetCursor, "targetCursor");
            Scribe_Values.Look(ref connectivityCheckTick, "connectivityCheckTick", -1);
            Scribe_Values.Look(ref requiredConsumerDisableFraction, "requiredConsumerDisableFraction", -1f);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                targetProviders ??= new List<Thing>();
                targetConsumers ??= new List<Thing>();
                cachedTargets ??= new List<Thing>();
                inaccessibleTargets ??= new List<Thing>();
                if (requiredConsumerDisableFraction < 0f) requiredConsumerDisableFraction = 0.625f;
            }
        }
    }
}
