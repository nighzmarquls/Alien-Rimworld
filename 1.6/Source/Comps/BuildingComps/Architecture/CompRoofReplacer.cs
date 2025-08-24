using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class CompRoofReplacer : ThingComp
    {
        struct RoofReplacingFrame
        {
            public CompRoofReplacerProperties properties;
            public Frame Frame;
        }
        CompRoofReplacerProperties Props => props as CompRoofReplacerProperties;
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (respawningAfterLoad)
            {
                return;
            }
            if(Props.replaceRoof == null)
            {
                return;
            }

            foreach (IntVec3 item in GenRadial.RadialCellsAround(parent.Position, Props.radius, useCenter: true))
            {
                if (!item.InBounds(parent.Map))
                {
                    continue;
                }
                RoofDef roofAt = parent.Map.roofGrid.RoofAt(item);

                if (roofAt != null)
                {
                    if(Props.targetRoof != null)
                    {
                        if(roofAt == Props.targetRoof)
                        {
                            parent.Map.roofGrid.SetRoof(item,Props.replaceRoof);
                        }
                    }
 
                }
            }
        }
    }

    public class CompRoofReplacerProperties : CompProperties
    {
        public RoofDef targetRoof = null;
        public RoofDef replaceRoof = null;

        public float radius = 1;

        public CompRoofReplacerProperties()
        {
            this.compClass = typeof(CompRoofReplacer);
        }

        public CompRoofReplacerProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
