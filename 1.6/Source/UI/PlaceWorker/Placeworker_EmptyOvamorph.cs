
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    internal class Placeworker_EmptyOvamorph : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            List<Thing> thingList = loc.GetThingList(map);

            for (int i = 0; i < thingList.Count; i++)
            {
                if(thingList[i] is Ovamorph ova)
                {
                    if(!ova.Unhatched)
                    {
                        return true;
                    }
                }
               
            }
            return "MustPlaceOnEmptyOvamorph".Translate();
        }
    }
}
