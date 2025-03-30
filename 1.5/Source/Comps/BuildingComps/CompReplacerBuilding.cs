using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.Noise;

namespace Xenomorphtype
{
    public class CompReplacerBuilding : ThingComp
    {
        CompReplacerBuildingProperties Props => props as CompReplacerBuildingProperties;

        public override void CompTick()
        {
            base.CompTick();

            if (parent != null)
            {
                IntVec3 position = parent.Position;
                Map map = parent.Map;
                Faction faction = parent.Faction;

                parent.DeSpawn();
                if (Props.replacedWith != null)
                {
                    Thing spawned = GenSpawn.Spawn(Props.replacedWith, position, map, WipeMode.Vanish);

                    if (faction != null && spawned != null)
                    {
                        spawned.SetFaction(faction);
                    }
                }

            }
        }
      
    }

    public class CompReplacerBuildingProperties : CompProperties
    {
        public ThingDef replacedWith = null;

        public CompReplacerBuildingProperties()
        {
            this.compClass = typeof(CompReplacerBuilding);
        }

        public CompReplacerBuildingProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}
