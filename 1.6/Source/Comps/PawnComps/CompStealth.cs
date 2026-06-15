using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Jobs;
using Verse;
using Verse.AI;
using Verse.Noise;

namespace Xenomorphtype
{ 
    public class CompStealth : ThingComp
    {
        CompStealthProperties Props => props as CompStealthProperties;
        private Pawn Parent => (Pawn)parent;
        bool wasLastFriendly = false;

        public int becomeInvisibleTick = 0;

        private bool IsFriendly => Parent.Faction == null ? Faction.OfPlayer.def == InternalDefOf.XMT_PlayerHive ? true : false
            : Parent.Faction.IsPlayer;
        [Unsaved(false)]
        private HediffComp_Invisibility _invisibility;

        public HediffComp_Invisibility Invisibility
        {
            get
            {
                if (wasLastFriendly == IsFriendly)
                {
                    if (_invisibility != null)
                    {
                        return _invisibility;
                    }
                }
                else
                {
                    if (_invisibility != null)
                    {
                        _invisibility.BecomeVisible();
                    }
                }

                wasLastFriendly = IsFriendly;
                Hediff hediff = Parent.health.hediffSet.GetFirstHediffOfDef(InternalDefOf.StarbeastStealthHostile);

                if (IsFriendly)
                {
                    hediff = Parent.health.hediffSet.GetFirstHediffOfDef(InternalDefOf.StarbeastStealthFriendly);
                    if (hediff == null)
                    {
                        hediff = Parent.health.AddHediff(InternalDefOf.StarbeastStealthFriendly);
                    }

                }
                else
                {
                    if (hediff == null)
                    {
                        hediff = Parent.health.AddHediff(InternalDefOf.StarbeastStealthHostile);
                    }
                }
                _invisibility = hediff?.TryGetComp<HediffComp_Invisibility>();
                return _invisibility;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref becomeInvisibleTick, "becomeInvisibleTick", 0);

        }

        public void StealthCalm()
        {
            if (Parent.Faction == null || !Parent.Faction.IsPlayer)
            {
                if (Parent.mindState.mentalStateHandler.InMentalState)
                {
                    if (Parent.mindState.mentalStateHandler.CurState?.def == MentalStateDefOf.Manhunter)
                    {
                        Parent.mindState.mentalStateHandler.Reset();
                    }
                }
            }
        }
        public void RevelationShock(Thing Discoverer = null)
        {
            if (!Parent.IsPsychologicallyInvisible())
            {
                return;
            }

            if (Parent.Faction == null || !Parent.Faction.IsPlayer)
            {
                if (Discoverer != null)
                {
                    if(Discoverer.def == ThingDefOf.Fire)
                    {
                        return;
                    }

                    Job attackJob = JobMaker.MakeJob(JobDefOf.AttackMelee, Discoverer);

                    Parent.jobs.StartJob(attackJob, JobCondition.InterruptForced);
                    Parent.Map.attackTargetsCache.UpdateTarget(Parent);

                }
                else
                {
                    IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(Parent.PositionHeld, Props.spotRange, false);

                    foreach (IntVec3 cell in cells)
                    {
                        List<Thing> things = cell.GetThingList(Parent.MapHeld);
                        bool foundTarget = false;
                        foreach (Thing thing in things)
                        {
                            if (thing.def == ThingDefOf.Fire)
                            {
                                continue;
                            }

                            CompGlower glower = thing.TryGetComp<CompGlower>();
                            if(glower != null)
                            {
                                Job attackJob = JobMaker.MakeJob(JobDefOf.AttackMelee, thing);

                                Parent.jobs.StartJob(attackJob, JobCondition.InterruptForced);
                                Parent.Map.attackTargetsCache.UpdateTarget(Parent);
                                foundTarget = true;
                                break;
                               
                            }
                        }
                        if (foundTarget)
                        {
                            break;
                        }
                    }
                }
            }

            if (Parent.def == InternalDefOf.XMT_Starbeast_AlienRace || Parent.def == InternalDefOf.XMT_Royal_AlienRace)
            {
                XMTUtility.WitnessHorror(Parent.PositionHeld, Parent.Map, 0.25f);
            }
            else if(Parent.def == InternalDefOf.XMT_Larva)
            {
                XMTUtility.WitnessLarva(Parent.PositionHeld, Parent.Map, 0.25f);
            }
            
        }
        public void TryHide()
        {
            if(!Invisibility.PsychologicallyVisible)
            {
                return;
            }
            if (Invisibility != null)
            {
                StealthCalm();
                Invisibility.BecomeInvisible();
                becomeInvisibleTick = int.MaxValue;
            }
        }

        public void ForceVisible()
        {
            if (Invisibility != null)
            {
                Invisibility.BecomeVisible(instant: true);
                becomeInvisibleTick = Find.TickManager.TicksGame + 600;
                Parent.Drawer?.renderer?.SetAllGraphicsDirty();
            }
        }

        protected void TryVisible(bool instant = false)
        {
            if (Invisibility.PsychologicallyVisible)
            {
                return;
            }
            if (Invisibility != null)
            {
                Invisibility.BecomeVisible(instant);
                becomeInvisibleTick = Find.TickManager.TicksGame + 600;
                Parent.Drawer?.renderer?.SetAllGraphicsDirty();
            }
        }

        public override void CompTickInterval(int delta)
        {
            if (Parent.Spawned)
            {
                if (parent.IsHashIntervalTick(60))
                {
                    if(!ModsConfig.AnomalyActive && !ModsConfig.RoyaltyActive)
                    {
                        return;
                    }

                    int tick = Find.TickManager.TicksGame;
                    if (Invisibility != null && Invisibility.PsychologicallyVisible && tick > becomeInvisibleTick)
                    {
                        if (Parent.Map.gameConditionManager.MapBrightness <= Props.hideBrightness || HiddenByBed())
                        {
                            TryHide();
                        }
                    }

                    if (tick < Parent.LastAttackTargetTick + 300)
                    {
                        RevelationShock(Parent.LastAttackedTarget.Thing);
                        TryVisible();
                        return;
                    }

                    CheckIfDetected();
                }
                
            }
        }

        private bool HiddenByBed()
        {
            if(Parent.InBed())
            {
                if (Parent.CurrentBed().def == XenoBuildingDefOf.HiveSleepingCocoon || Parent.CurrentBed().def == XenoBuildingDefOf.HiveHidingSpot)
                {
                    return true;
                }
            }
            return false;
        }

        private void CheckIfDetected()
        {
            if (HiddenByBed())
            {
                TryHide();
                return;
            }

            float brightness = Parent.MapHeld.glowGrid.GroundGlowAt(Parent.Position);

            if(DarklightUtility.IsDarklightAt(Parent.Position,Parent.MapHeld))
            {
                brightness *= 0.5f;
            }

            if (brightness > Props.hideBrightness)
            {
                if (Parent.Map.skyManager.CurSkyGlow < brightness)
                {
                    RevelationShock();
                }
                TryVisible();
                return;
            }

            if (brightness < Props.minVisibleBrightness)
            {
                TryHide();
            }

            IEnumerable<Thing> PossibleSpotters = GenRadial.RadialDistinctThingsAround(Parent.Position, Parent.Map, Props.spotRange, true)
                    .Where(x => x != Parent && XMTUtility.IsSpotter(x) && XMTUtility.IsThingWaryOfTarget(x,Parent));

            if (PossibleSpotters.Any())
            {
                foreach (Thing thing in PossibleSpotters)
                {
                    StealthDetectionReport report = XMTStealthUtility.GetDetectionReport(Parent, thing, Props.spotRange, brightness);
                    if (report.CanDetect && Rand.Chance(report.FinalChance))
                    {
                        RevelationShock(thing);
                        TryVisible();
                        break;
                    }
                }
            }

        }
    }

    public class CompStealthProperties : CompProperties
    {
        public float spotRange = 4f;
        public float hideBrightness = 0.45f;
        public float minVisibleBrightness = 0.1f;
        public CompStealthProperties()
        {
            this.compClass = typeof(CompStealth);
        }

    }
}
