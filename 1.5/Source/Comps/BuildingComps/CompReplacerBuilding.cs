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
                    if (Props.replacedStuff != null)
                    {
                        Thing thing = ThingMaker.MakeThing(Props.replacedWith, Props.replacedStuff);
                        thing.SetFactionDirect(faction);
                        GenSpawn.Spawn(thing, position, map, parent.Rotation, WipeMode.VanishOrMoveAside);
                    }
                    else
                    {
                        Thing spawned = GenSpawn.Spawn(Props.replacedWith, position, map, WipeMode.VanishOrMoveAside);
                        if (spawned != null)
                        {
                            if (faction != null)
                            {
                                spawned.SetFaction(faction);
                            }
                        }
                    }
                  
                }

            }
        }
      
    }

    public class CompReplacerBuildingProperties : CompProperties
    {
        public ThingDef replacedWith = null;
        public ThingDef replacedStuff = null;

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
