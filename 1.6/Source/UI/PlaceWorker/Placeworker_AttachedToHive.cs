using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    internal class Placeworker_AttachedToHive : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            List<Thing> thingList = loc.GetThingList(map);

            for (int i = 0; i < thingList.Count; i++)
            {
                Thing thing2 = thingList[i];
                ThingDef thingDef = GenConstruct.BuiltDefOf(thing2.def) as ThingDef;
                if (thingDef?.building != null)
                {
                    if (thingDef.Fillage == FillCategory.Full)
                    {
                        return false;
                    }

                    if (thingDef.building.isAttachment && thing2.Rotation == rot)
                    {
                        return "SomethingPlacedOnThisWall".Translate();
                    }
                }
            }

            foreach (IntVec3 direction in GenAdj.CardinalDirections)
            {
                IntVec3 c = loc + direction;

                if (!c.InBounds(map))
                {
                    continue;
                }

                thingList = c.GetThingList(map);
                if (ValidHiveWall(thingList))
                {
                    return true;
                }
                
            }
            
            return "MustPlaceOnWall".Translate();
        }

        private bool ValidHiveWall(List<Thing> thingList)
        {
            for (int j = 0; j < thingList.Count; j++)
            {
                if (thingList[j].def is ThingDef wall && wall.building != null)
                {
                    if (wall.building.supportsWallAttachments)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
