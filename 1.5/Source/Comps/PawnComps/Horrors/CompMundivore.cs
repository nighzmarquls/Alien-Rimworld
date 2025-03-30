using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class CompMundivore : ThingComp
    {
        int hungerTick;

        float swallowedmass;
        Pawn Parent => parent as Pawn;

        CompMundivoreProperties Props => props as CompMundivoreProperties;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref hungerTick, "hungerTick", -1);
            Scribe_Values.Look(ref swallowedmass, "swallowedmass", 0);
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
                        if (Find.TickManager.TicksGame > hungerTick)
                        {
                            int currentTick = Find.TickManager.TicksGame;

                            hungerTick = currentTick + Mathf.CeilToInt(Props.hungerIntervalHours * 2500);
                            ConsumeSomething();
                        }
                    }
                }
            }
        }

        protected void ConsumeSomething() {

          
            IEnumerable<Thing> things = from x in GenRadial.RadialDistinctThingsAround(Parent.PositionHeld, Parent.MapHeld, Props.consumptionRadius, false)
                                        where !(x is Mote) && !(x is Filth) && !(x is Building)
                                        select x;

            bool foundNothingToEat = true;

            if (things.Any())
            {
                Thing target = things.First();

                Pawn targetPawn = target as Pawn;
                Parent.jobs.StopAll();

                if (targetPawn != null)
                {
                    if (Parent.needs.food.Starving)
                    {
                        Job job = JobMaker.MakeJob(JobDefOf.PredatorHunt, target);

                        Parent.jobs.StartJob(job, JobCondition.InterruptForced);
                    }
                }
                else
                {
                    if (target.IngestibleNow)
                    {
                        float eating = Parent.needs?.food?.NutritionWanted ?? (target.GetStatValue(StatDefOf.Nutrition) * (float)target.stackCount);

                        eating = Mathf.Max(eating, 0.75f);

                        float nutrition = target.Ingested(Parent, eating);
                        if (!Parent.Dead && Parent.needs?.food != null)
                        {

                            Parent.needs.food.CurLevel += nutrition;
                            Parent.records.AddTo(RecordDefOf.NutritionEaten, nutrition);

                        }
                    }
                    else
                    {
                        swallowedmass += target.GetStatValue(StatDefOf.Mass)*target.stackCount;

                        Log.Message(parent + " swallowed " + target + " swallowedmass: " + swallowedmass);
                        target.Destroy();
                    }
                }

                foundNothingToEat = false;
            }
            
            
            if(swallowedmass >= Props.maximumInedibleMass)
            {
                int stackTotal = Mathf.CeilToInt(swallowedmass / Props.vomitThing.BaseMass);
                swallowedmass = 0;
                XMTUtility.DropAmountThing(Props.vomitThing, stackTotal, parent.PositionHeld, parent.MapHeld, ThingDefOf.Filth_Vomit);
            }

            if(foundNothingToEat && Parent.needs.food.Starving)
            {
                Parent.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Manhunter, "", forced: true, forceWake: true, false);
            }
        }
    }

    public class CompMundivoreProperties : CompProperties
    {
        public float hungerIntervalHours = 1;
        public float consumptionRadius = 2.5f;
        public float maximumInedibleMass = 100;
        public ThingDef vomitThing;
        public CompMundivoreProperties()
        {
            this.compClass = typeof(CompMundivore);
        }
        public CompMundivoreProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
