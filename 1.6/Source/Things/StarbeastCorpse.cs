using AlienRace;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class StarbeastCorpse : Corpse
    {
        bool _initialized = false;
        bool _notActuallyDead = true;
        bool _playDeadReported;
        bool _confirmedDeathReported;
        DamageInfo? _pendingNemesisDamage;

        [Unsaved]
        bool _resurrectionInProgress;

        bool _notfixedSkinColor = true;
        const int reviveInterval = 2500*24;

        int nextRevivalTick = -1;

        public bool NotActuallyDead
        {
            get
            {
                if (!_notActuallyDead)
                {
                    return false;
                }

                if (_initialized)
                {
                    if (this.IsDessicated())
                    {
                        ConfirmActualDeath("dessicated");
                    }
                    return _notActuallyDead;
                }

                _notActuallyDead = EvaluateRevivalViability(out string failureReason);
                _initialized = true;
                if (!_notActuallyDead)
                {
                    ConfirmActualDeath(failureReason);
                }

                return _notActuallyDead;
            }
        }

        internal bool IsViableLivingCorpseForObservation => EvaluateRevivalViability(out _);

        private bool EvaluateRevivalViability(out string failureReason)
        {
            failureReason = null;
            if(InnerPawn == null)
            {
                failureReason = "missing inner pawn";
                return false;
            }

            if(InnerPawn.GetMorphComp() == null)
            {
                failureReason = "not a mature cryptimorph";
                return false;
            }

            if(!_notActuallyDead)
            {
                failureReason = "already confirmed dead";
                return false;
            }

            if (this.IsDessicated())
            {
                failureReason = "dessicated";
                return false;
            }

            if(InnerPawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss) is Hediff bloodloss)
            {
                if(bloodloss.Severity > 0.5f)
                {
                    failureReason = "fatal blood loss";
                    return false;
                }
            }

            IEnumerable <BodyPartRecord> Parts  = InnerPawn.health.hediffSet.GetNotMissingParts();

            int foundparts = 0;
            foreach (BodyPartRecord part in Parts)
            {
                if( part.def == InternalDefOf.StarbeastBrain ||
                    part.def == InternalDefOf.StarbeastHeart ||
                    part.def == BodyPartDefOf.Torso
                   )
                {
                    float health = InnerPawn.health.hediffSet.GetPartHealth(part);
                    float maxHealth = part.def.GetMaxHealth(InnerPawn);
                    if (health / maxHealth > 0.5f)
                    {
                        foundparts++;
                    }
                }
            }

            if(foundparts < 3)
            {
                failureReason = "fatal organ damage";
                return false;
            }
            return true;

        }

        protected bool TryRevive(CompPawnInfo aggressor = null)
        {
            Pawn reference = InnerPawn;
            bool revived;
            _resurrectionInProgress = true;
            try
            {
                revived = ResurrectionUtility.TryResurrect(InnerPawn,
                    new ResurrectionParams { restoreMissingParts = false, gettingScarsChance = 0.75f });
            }
            finally
            {
                _resurrectionInProgress = false;
            }

            if (!revived)
            {
                ConfirmActualDeath("revival failed");
                return false;
            }

            if (aggressor != null)
            {
                aggressor.ApplyThreatPheromone(reference);
            }
            return true;
        }

        protected override void PrePostIngested(Pawn ingester)
        {
            base.PrePostIngested(ingester);
            if (NotActuallyDead)
            {
                _notActuallyDead = EvaluateRevivalViability(out string failureReason);
                if (!_notActuallyDead)
                {
                    ConfirmActualDeath(failureReason);
                }

                if (NotActuallyDead)
                {
                    CompPawnInfo info = ingester.Info();
                    TryRevive(info);
                }
            }
        }
        public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            _pendingNemesisDamage = dinfo;
            try
            {
                base.PostApplyDamage(dinfo, totalDamageDealt);
                if (NotActuallyDead && nextRevivalTick > 0)
                {
                    if (HitPoints > MaxHitPoints / 2)
                    {
                        CompPawnInfo info = null;
                        if (dinfo.Instigator is Pawn pawn)
                        {

                            info = pawn.Info();
                        }

                        TryRevive(info);
                    }
                    else
                    {
                        ConfirmActualDeath("corpse destroyed", dinfo);
                    }
                }
            }
            finally
            {
                _pendingNemesisDamage = null;
            }
        }

        public override void TickRare()
        {
            if(NotActuallyDead)
            {
                int tick = Find.TickManager.TicksGame;
                if (nextRevivalTick < 0)
                {
                    nextRevivalTick = tick + reviveInterval;
                }

                if(nextRevivalTick < tick)
                {
                    TryRevive();
                    return;
                }
            }
            base.TickRare();

            if(_notfixedSkinColor && this.GetRotStage() == RotStage.Dessicated)
            {
                if(InnerPawn != null)
                {
                    if(InnerPawn.story != null)
                    {
                        InnerPawn.story.skinColorOverride = Color.white;
                    }
                }
                _notfixedSkinColor = false;
            }
        }

       

        public override bool IngestibleNow {
            get
            {
                if(base.IngestibleNow)
                {
                    return !NotActuallyDead;
                }
                return false;
            }
        }
        public override IEnumerable<Thing> ButcherProducts(Pawn butcher, float efficiency)
        {
            if(NotActuallyDead)
            {
                CompPawnInfo info = butcher.Info();

                if (TryRevive(info))
                {
                    yield break;
                }
            }
            foreach (Thing item in InnerPawn.ButcherProducts(butcher, efficiency))
            {
                if (InnerPawn.ageTracker.Adult)
                {
                    yield return item;
                }
                else
                {
                    if(item.def != InnerPawn.RaceProps.leatherDef)
                    {
                        yield return item;
                    }
                    else
                    {
                        item.Discard();
                    }
                }
            }

            if (InnerPawn.health != null)
            {
                if(InnerPawn.health.hediffSet.TryGetHediff(XenoGeneDefOf.XMT_ThrumboHorn, out Hediff horn))
                {
                    yield return ThingMaker.MakeThing(ExternalDefOf.ThrumboHorn);
                }
            }

            FilthMaker.TryMakeFilth(butcher.Position, butcher.Map, InternalDefOf.Starbeast_Filth_Resin, InnerPawn.LabelIndefinite());
            
            if (InnerPawn.RaceProps.Humanlike)
            {
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.ButcheredHuman, new SignalArgs(butcher.Named(HistoryEventArgsNames.Doer), InnerPawn.Named(HistoryEventArgsNames.Victim))));
                TaleRecorder.RecordTale(TaleDefOf.ButcheredHumanlikeCorpse, butcher);
            }

            NemesisEvidenceReporter.ReportButchery(InnerPawn, butcher);
            ResearchUtility.ProgressCryptobioTech(10, butcher);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (respawningAfterLoad)
            {
                InitializeLoadedCorpseWithoutHistoricalEvidence();
            }
            else
            {
                NemesisEvidenceReporter.ResolveCryptimorphCorpse(this);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _initialized, "xmtLivingCorpseInitialized", false);
            Scribe_Values.Look(ref _notActuallyDead, "xmtNotActuallyDead", true);
            Scribe_Values.Look(ref _playDeadReported, "xmtPlayDeadReported", false);
            Scribe_Values.Look(ref _confirmedDeathReported, "xmtConfirmedDeathReported", false);
            Scribe_Values.Look(ref nextRevivalTick, "xmtNextRevivalTick", -1);
        }

        internal void ResolveInitialNemesisDeath(DamageInfo? damage)
        {
            _pendingNemesisDamage = damage;
            if (NotActuallyDead)
            {
                if (!_playDeadReported)
                {
                    _playDeadReported = true;
                    NemesisEvidenceReporter.ReportCryptimorphPlayedDead(InnerPawn, damage);
                }
            }
            else if (!_confirmedDeathReported)
            {
                ConfirmActualDeath("initial lethal damage", damage);
            }
            _pendingNemesisDamage = null;
        }

        private void ConfirmActualDeath(string reason, DamageInfo? damage = null)
        {
            _notActuallyDead = false;
            if (_confirmedDeathReported || InnerPawn == null)
            {
                return;
            }

            _confirmedDeathReported = true;
            DamageInfo? effectiveDamage = damage ?? _pendingNemesisDamage;
            XenoformingUtility.HandleMatureMorphDeath(InnerPawn);
            NemesisEvidenceReporter.ReportCryptimorphConfirmedDeath(InnerPawn, effectiveDamage, reason);
        }

        private void InitializeLoadedCorpseWithoutHistoricalEvidence()
        {
            if (_initialized)
            {
                return;
            }

            _notActuallyDead = EvaluateRevivalViability(out _);
            _initialized = true;
            if (_notActuallyDead)
            {
                _playDeadReported = true;
            }
            else
            {
                _confirmedDeathReported = true;
            }
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if(!Spawned)
            {
                return;
            }

            if (!_resurrectionInProgress && !_confirmedDeathReported && InnerPawn != null)
            {
                ConfirmActualDeath("corpse destroyed");
            }

            base.Destroy(mode);
        }
    }
}
