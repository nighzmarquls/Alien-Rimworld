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

        bool _notfixedSkinColor = true;
        const int reviveInterval = 2500*24;

        int nextRevivalTick = -1;

        public bool NotActuallyDead
        {
            get
            {
                if(_notActuallyDead && this.IsDessicated())
                {
                    _notActuallyDead = false;
                    XenoformingUtility.HandleMatureMorphDeath(InnerPawn);
                    return _notActuallyDead;
                }

                if(_initialized)
                {
                    return _notActuallyDead;
                }

                _notActuallyDead = NotActuallyDeadInit();
                _initialized = true;

                return _notActuallyDead;
            }
        }
        private bool NotActuallyDeadInit()
        {
            if(InnerPawn == null)
            {
                return false;
            }

            if(InnerPawn.GetMorphComp() == null)
            {
                return false;
            }

            if(!_notActuallyDead)
            {
                return false;
            }

            if(InnerPawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss) is Hediff bloodloss)
            {
                if(bloodloss.Severity > 0.5f)
                {
                    XenoformingUtility.HandleMatureMorphDeath(InnerPawn);
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
                XenoformingUtility.HandleMatureMorphDeath(InnerPawn);
                return false;
            }
            return true;

        }

        protected bool TryRevive(CompPawnInfo aggressor = null)
        {
            Pawn reference = InnerPawn;
            if (!ResurrectionUtility.TryResurrect(InnerPawn, new ResurrectionParams { restoreMissingParts = false, gettingScarsChance = 0.75f }))
            {
                _notActuallyDead = false;
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
                _notActuallyDead = NotActuallyDeadInit();

                if (NotActuallyDead)
                {
                    CompPawnInfo info = ingester.Info();
                    TryRevive(info);
                }
            }
        }
        public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
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
                    XenoformingUtility.HandleMatureMorphDeath(InnerPawn);
                    _notActuallyDead = false;
                }
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

            ResearchUtility.ProgressCryptobioTech(10, butcher);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if(!Spawned)
            {
                return;
            }

            base.Destroy(mode);
        }
    }
}
