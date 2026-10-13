using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using static RimWorld.FleshTypeDef;

namespace Xenomorphtype
{
    public class CompRepairer : CompShearable
    {
        int repairTick;


        Pawn Parent => parent as Pawn;
        CompRepairerProperties Props => props as CompRepairerProperties;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref repairTick, "repairTick", -1);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned)
            {
                return;
            }
            if (Parent != null)
            {
                if (Parent.Awake())
                {
                    if (Parent?.jobs?.curJob?.def != JobDefOf.Ingest)
                    {
                        if (Find.TickManager.TicksGame > repairTick)
                        {
                            int currentTick = Find.TickManager.TicksGame;

                            repairTick = currentTick + Mathf.CeilToInt(Props.repairIntervalHours * 2500);

                            if (fullness >= 1)
                            {
                                RepairSomething();
                            }
                        }
                    }
                }
            }
        }

        protected void RepairSomething() {

          
            IEnumerable<Thing> things = from x in GenRadial.RadialDistinctThingsAround(Parent.PositionHeld, Parent.MapHeld, Props.repairRadius, false)
                                        where !(x is Mote) && !(x is Filth) && !(x is Building)
                                        select x;

            bool foundNothingToRepair = true;

            if (things.Any())
            {
                Thing target = null;
                Pawn targetPawn = null;

                foreach (Thing candidate in things)
                {

                    if(candidate is Pawn pawnCandidate)
                    {
                       if(pawnCandidate.apparel != null)
                       {
                            bool found = false;
                            foreach(Apparel apparelItem in pawnCandidate.apparel.GetDirectlyHeldThings())
                            {
                                if(Props.repairStuffFilter.Allows(apparelItem.Stuff))
                                {
                                    found = true;
                                    break;
                                }
                            }

                            if (found)
                            {
                                targetPawn = pawnCandidate;
                                break;
                            }
                       }
                    }
                    if (Props.repairStuffFilter.Allows(candidate.Stuff))
                    {
                        int damage = candidate.MaxHitPoints - candidate.HitPoints;
                        if (damage > 0)
                        {
                            target = candidate;
                            break;
                        }
                    }
                    if (Props.repairStuffFilter.Allows(candidate))
                    {
                        target = candidate;
                        break;
                    }
                    
                }

                
                //Parent.jobs.StopAll();

                if (targetPawn != null)
                {
                    foreach (Apparel apparelItem in targetPawn.apparel.GetDirectlyHeldThings())
                    {
                        if(fullness <= 0)
                        {
                            fullness = 0;
                            break;
                        }
                        if (Props.repairStuffFilter.Allows(apparelItem.Stuff))
                        {
                            int damage = apparelItem.MaxHitPoints - apparelItem.HitPoints;
                            if (damage > 0)
                            {
                                float missingMass = apparelItem.GetStatValue(StatDefOf.Mass)*(damage/apparelItem.MaxHitPoints);
                                float woolMassTotal = Props.woolDef.GetStatValueAbstract(StatDefOf.Mass) * ResourceAmount;
                                float repairCost = missingMass / woolMassTotal;

                                apparelItem.HitPoints = apparelItem.MaxHitPoints;
                                apparelItem.SetStuffDirect(Props.woolDef);
                                fullness -= repairCost;
                            }
                        }
                    }
                }
                else if(target != null)
                {
                    if (Props.repairStuffFilter.Allows(target.Stuff))
                    {
                        int damage = target.MaxHitPoints - target.HitPoints;
                        float missingMass = target.GetStatValue(StatDefOf.Mass) * (damage / target.MaxHitPoints);
                        float woolMassTotal = Props.woolDef.GetStatValueAbstract(StatDefOf.Mass) * ResourceAmount;
                        float repairCost = missingMass / woolMassTotal;

                        target.HitPoints = target.MaxHitPoints;
                        target.SetStuffDirect(Props.woolDef);
                        fullness -= repairCost;
                        
                        return;
                    }
                    else
                    {
                        int stackTotal = target.stackCount;
                        target.Destroy();

                        fullness -= ((float)stackTotal) / ((float)ResourceAmount);
                        XMTUtility.DropAmountThing(Props.woolDef, stackTotal, parent.PositionHeld, parent.MapHeld);
                    }

                    if(fullness < 0)
                    {
                        fullness = 0;
                    }
                    
                }
            }
        }
    }

    public class CompRepairerProperties : CompProperties_Shearable
    {
        public float repairIntervalHours = 1;
        public float repairRadius = 1.5f;
        public ThingFilter repairStuffFilter;
        public CompRepairerProperties()
        {
            compClass = typeof(CompRepairer);
        }

        public override void ResolveReferences(ThingDef parentDef)
        {
            base.ResolveReferences(parentDef);
            repairStuffFilter.ResolveReferences();
        }
    }
}
