using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    public enum XMT_ResponseResult { Completed, Deferred, Failed }

    public class XMT_WatcherDef : Def
    {
        public Type workerClass;
        public int intervalTicks = 250;
        private XMT_WatcherWorker worker;
        public XMT_WatcherWorker Worker
        {
            get
            {
                if (worker == null) { worker = (XMT_WatcherWorker)Activator.CreateInstance(workerClass); worker.Definition = this; }
                return worker;
            }
        }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (workerClass == null || workerClass.IsAbstract || !typeof(XMT_WatcherWorker).IsAssignableFrom(workerClass))
                yield return defName + ": invalid watcher worker.";
            if (intervalTicks < 250) yield return defName + ": interval must be at least 250 ticks.";
        }
    }

    public abstract class XMT_WatcherWorker
    {
        public XMT_WatcherDef Definition { get; internal set; }
        public abstract void Tick(GameComponent_XMTWorldState state, int tick);
    }

    public class XMT_ResponseDef : Def
    {
        public Type responseWorkerClass;
        public XMT_WatcherDef watcher;
        public IntRange delayDays = new IntRange(1, 3);
        public float selectionWeight = 1f;
        private XMT_ResponseWorker responseWorker;
        public XMT_ResponseWorker ResponseWorker => responseWorker ??= (XMT_ResponseWorker)Activator.CreateInstance(responseWorkerClass);
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (responseWorkerClass != null && (responseWorkerClass.IsAbstract || !typeof(XMT_ResponseWorker).IsAssignableFrom(responseWorkerClass)))
                yield return defName + ": invalid response worker.";
            if (delayDays.min < 0 || delayDays.max < delayDays.min) yield return defName + ": invalid response delay.";
            if (selectionWeight < 0f) yield return defName + ": negative response selection weight.";
        }
    }

    public abstract class XMT_ResponseWorker { }

    public abstract class XMT_CompanionResponseWorker : XMT_ResponseWorker
    {
        public abstract XMT_ResponseResult Execute(GameComponent_XMTWorldState state, XMT_CompanionLink link, Map map);
    }

    public class XMT_CompanionDef : Def
    {
        public string role = "caretaker";
        public XMT_CompanionSource source;
        public XMT_WatcherDef watcher;
        public XMT_ResponseDef friendlyResponse;
        public XMT_ResponseDef recoveryResponse;
        public PawnRelationDef ownerToCompanion;
        public PawnRelationDef companionToOwner;
        public Type relationWorkerClass = typeof(XMT_CompanionRelationWorker);
        public Type withdrawalWorkerClass = typeof(XMT_CompanionWithdrawal_Injury);
        public Type corpseOutcomeWorkerClass = typeof(XMT_CorpseOutcomeWorker);
        public IntRange? friendlyArrivalDelayDays;
        public IntRange? recoveryDelayDays;
        public IntRange FriendlyDelay => friendlyArrivalDelayDays ?? friendlyResponse?.delayDays ?? new IntRange(1, 120);
        public IntRange RecoveryDelay => recoveryDelayDays ?? recoveryResponse?.delayDays ?? new IntRange(1, 3);
        public IntRange retryAfterWithdrawalDays = new IntRange(1, 3);
        public int retryCap = -1;
        public bool allowDropPods;
        public Type supportWorkerClass;
        public float supportPointsFactor = 1f;
        public FloatRange biologicalAge = new FloatRange(25f, 45f);
        public FloatRange chronologicalAge;
        public float yearsAfterOwnerBirth = -1f;
        public List<HediffDef> setupHediffs = new List<HediffDef>();
        private XMT_CompanionWithdrawalWorker withdrawalWorker;
        public XMT_CompanionWithdrawalWorker WithdrawalWorker => withdrawalWorker ??=
            (XMT_CompanionWithdrawalWorker)Activator.CreateInstance(withdrawalWorkerClass);
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (source == null) yield return defName + ": companion source is required.";
            else foreach (string error in source.ConfigErrors()) yield return defName + ": " + error;
            if (role.NullOrEmpty() || watcher == null || friendlyResponse?.responseWorkerClass == null || recoveryResponse?.responseWorkerClass == null)
                yield return defName + ": role, watcher and executable responses are required.";
            else if (!typeof(XMT_CompanionResponseWorker).IsAssignableFrom(friendlyResponse.responseWorkerClass)
                || !typeof(XMT_CompanionResponseWorker).IsAssignableFrom(recoveryResponse.responseWorkerClass))
                yield return defName + ": responses must use companion response workers.";
            if (retryCap < -1 || supportPointsFactor < 0f) yield return defName + ": invalid companion limits.";
            foreach (var worker in new[] { (relationWorkerClass, typeof(XMT_CompanionRelationWorker)),
                (withdrawalWorkerClass, typeof(XMT_CompanionWithdrawalWorker)), (corpseOutcomeWorkerClass, typeof(XMT_CorpseOutcomeWorker)) })
                if (worker.Item1 == null || worker.Item1.IsAbstract || !worker.Item2.IsAssignableFrom(worker.Item1))
                    yield return defName + ": invalid companion worker (expected " + worker.Item2.Name + ").";
            if (supportWorkerClass != null && (supportWorkerClass.IsAbstract || !typeof(XMT_CompanionSupportWorker).IsAssignableFrom(supportWorkerClass)))
                yield return defName + ": invalid support worker.";
            if (biologicalAge.min < 0f || biologicalAge.max < biologicalAge.min || chronologicalAge.min < 0f || chronologicalAge.max < chronologicalAge.min)
                yield return defName + ": invalid age range.";
            foreach (IntRange range in new[] { FriendlyDelay, RecoveryDelay, retryAfterWithdrawalDays })
                if (range.min < 0 || range.max < range.min) yield return defName + ": invalid companion delay.";
        }
    }

    public class XMT_BackstoryCompanionDef : Def
    {
        public BackstoryDef backstory;
        public XMT_CompanionDef companion;
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (backstory == null || companion == null) yield return defName + ": backstory and companion are required.";
        }
    }

    public abstract class XMT_CompanionWithdrawalWorker
    {
        public abstract bool ShouldWithdraw(Pawn pawn);
    }
    public sealed class XMT_CompanionWithdrawal_Injury : XMT_CompanionWithdrawalWorker
    {
        public override bool ShouldWithdraw(Pawn pawn) => pawn.health.hediffSet.BleedRateTotal > 0.35f
            || pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) < 0.4f
            || pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving) < 0.4f;
    }
    public sealed class XMT_CompanionWithdrawal_Never : XMT_CompanionWithdrawalWorker
    {
        public override bool ShouldWithdraw(Pawn pawn) => false;
    }
    // Extension point only: actual serum-backed resurrection is intentionally not implemented.
    public class XMT_CorpseOutcomeWorker
    {
        public virtual void Recovered(GameComponent_XMTWorldState state, XMT_CompanionLink link, Corpse corpse) { }
    }
}
