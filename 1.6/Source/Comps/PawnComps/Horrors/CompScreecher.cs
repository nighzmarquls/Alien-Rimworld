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
    public class CompScreecher : ThingComp
    {
        CompScreecherProperties Props => props as CompScreecherProperties;
        int nextCryTick = -1;
        
        Pawn Parent => parent as Pawn;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextCryTick, "nextCryTick", -1);
        }
        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);
            if (Parent != null)
            {
                IEnumerable<Pawn> listeners = GenRadial.RadialDistinctThingsAround(Parent.Position, prevMap, Props.deathShriekRadius, true).OfType<Pawn>();
                if (ModsConfig.RoyaltyActive)
                {
                    FleckMaker.Static(Parent.Position, Parent.Map, FleckDefOf.PsycastAreaEffect, 10f);
                }
                foreach (Pawn listener in listeners)
                {
                    if(listener.health.capacities.CapableOf(PawnCapacityDefOf.Hearing))
                    {
                        XMTUtility.GiveMemory(listener, HorrorMoodDefOf.DeathShriek);
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
                    if (Find.TickManager.TicksGame > nextCryTick)
                    {
                        nextCryTick = currentTick + Mathf.CeilToInt(Props.cryIntervalHours * 2500);

                        if (Parent.mindState.mentalStateHandler.InMentalState)
                        {
                            return;
                        }
                        Parent.mindState.mentalStateHandler.TryStartMentalState(ExternalDefOf.Crying, "living", forced: true, forceWake: true, false);
                    
                    }
                }
            }
        }
    }

    public class CompScreecherProperties : CompProperties
    {
        public float cryIntervalHours = 1;
        public float deathShriekRadius = 5;
        public CompScreecherProperties()
        {
            this.compClass = typeof(CompScreecher);
        }
        public CompScreecherProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
