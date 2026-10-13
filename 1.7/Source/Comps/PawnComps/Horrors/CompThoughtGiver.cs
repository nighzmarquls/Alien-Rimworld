using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class CompThoughtGiver : ThingComp
    {
        CompThoughtGiverProperties Props => props as CompThoughtGiverProperties;
        int nextIntervalTick = -1;

        Pawn Parent => parent as Pawn;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextIntervalTick, "nextIntervalTick", -1);
        }
        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);
            if (Parent != null)
            {
                IEnumerable<Pawn> listeners = GenRadial.RadialDistinctThingsAround(Parent.PositionHeld, prevMap, Props.deathThoughtRadius, true).OfType<Pawn>();
                if (ModsConfig.RoyaltyActive)
                {
                    FleckMaker.Static(Parent.PositionHeld, Parent.MapHeld, FleckDefOf.PsycastAreaEffect, 10f);
                }
                foreach (Pawn listener in listeners)
                {
                    if (listener.health.capacities.CapableOf(PawnCapacityDefOf.Hearing))
                    {
                        XMTUtility.GiveMemory(listener, Props.deathThought);
                    }
                }
            }
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
                    int currentTick = Find.TickManager.TicksGame;
                    if (Find.TickManager.TicksGame > nextIntervalTick)
                    {
                        nextIntervalTick = currentTick + Mathf.CeilToInt(Props.thoughtIntervalHours * 2500);

                        if (Parent.mindState.mentalStateHandler.InMentalState)
                        {
                            return;
                        }

                        IEnumerable<Pawn> listeners = GenRadial.RadialDistinctThingsAround(Parent.PositionHeld, Parent.MapHeld, Props.intervalThoughtRadius, true).OfType<Pawn>();
                        if (ModsConfig.RoyaltyActive)
                        {
                            FleckMaker.Static(Parent.Position, Parent.Map, FleckDefOf.PsycastAreaEffect, 10f);
                        }
                        foreach (Pawn listener in listeners)
                        {
                            if (listener.health.capacities.CapableOf(PawnCapacityDefOf.Hearing))
                            {
                                XMTUtility.GiveMemory(listener, Props.intervalThought);
                            }
                        }
                    }
                }
            }
        }
    }

    public class CompThoughtGiverProperties : CompProperties
    {
        public float intervalThoughtRadius = 3;
        public float thoughtIntervalHours = 1;
        public ThoughtDef intervalThought;
        public float deathThoughtRadius = 5;
        public ThoughtDef deathThought;
        public CompThoughtGiverProperties()
        {
            this.compClass = typeof(CompThoughtGiver);
        }
        public CompThoughtGiverProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
