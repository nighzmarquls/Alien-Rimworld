using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace Xenomorphtype
{
    public class CompReinforcing : ThingComp
    {
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

                foreach(Thing thing in adjacentThings)
                {
                    CompReinforceable comp = thing.TryGetComp<CompReinforceable>();
                    if (comp != null)
                    {
                        comp.GainReinforcement(this);
                    }
                }
            }
        }
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            foreach (IntVec3 direction in GenAdj.AdjacentCells)
            {
                IntVec3 c = parent.Position + direction;

                if (!c.InBounds(previousMap))
                {
                    continue;
                }

                List<Thing> adjacentThings = c.GetThingList(previousMap);
                foreach (Thing thing in adjacentThings)
                {
                    CompReinforceable comp = thing.TryGetComp<CompReinforceable>();
                    if (comp != null)
                    {
                        comp.LoseReinforcement(this);
                    }
                }
            }
            base.PostDestroy(mode, previousMap);
        }
    }
    public class CompReinforcingProperties : CompProperties
    {

        public CompReinforcingProperties()
        {
            this.compClass = typeof(CompReinforcing);
        }

        public CompReinforcingProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
