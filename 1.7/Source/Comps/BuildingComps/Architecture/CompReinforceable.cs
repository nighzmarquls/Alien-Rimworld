using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class CompReinforceable : ThingComp
    {
        List<Thing> reinforcers;

        CompReinforceableProperties Props => props as CompReinforceableProperties;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref reinforcers, "reinforcers", LookMode.Reference);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            foreach (IntVec3 direction in GenAdj.AdjacentCells)
            {
                IntVec3 c = parent.Position + direction;

                if (!c.InBounds(parent.Map))
                {
                    continue;
                }

                List<Thing> adjacentThings = c.GetThingList(parent.Map);

                foreach (Thing thing in adjacentThings)
                {
                    CompReinforcing comp = thing.TryGetComp<CompReinforcing>();
                    if (comp != null)
                    {
                        GainReinforcement(comp);
                    }
                }
            }
        }
        public override void CompTick()
        {
            base.CompTick();

        }

        public override float GetStatOffset(StatDef stat)
        {
            if (stat == StatDefOf.MaxHitPoints && reinforcers != null )
            {
                return base.GetStatOffset(stat) + (reinforcers.Count*Props.reinforcedHitPoints);
            }
            return base.GetStatOffset(stat);
        }
        public void GainReinforcement(CompReinforcing reinforcer)
        {
            if (reinforcers == null)
            {
                reinforcers = new List<Thing>();
            }

            if (!reinforcers.Contains(reinforcer.parent))
            {
                reinforcers.Add(reinforcer.parent);
                parent.HitPoints += Props.reinforcedHitPoints;
                StatDefOf.MaxHitPoints.Worker.ClearCacheForThing(parent);
            }
        }

        public void LoseReinforcement(CompReinforcing reinforcer)
        {

            if (reinforcers == null)
            {
                return;
            }

            if (reinforcers.Contains(reinforcer.parent))
            {
                reinforcers.Remove(reinforcer.parent);
                parent.HitPoints -= Props.reinforcedHitPoints;
                StatDefOf.MaxHitPoints.Worker.ClearCacheForThing(parent);
            }

        }
    }
    public class CompReinforceableProperties : CompProperties
    {
        public int reinforcedHitPoints = 500;


        public CompReinforceableProperties()
        {
            this.compClass = typeof(CompReinforceable);
        }

        public CompReinforceableProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
