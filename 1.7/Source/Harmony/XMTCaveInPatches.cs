using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    internal class XMTCaveInPatches
    {
        [HarmonyPatch(typeof(RoofCollapserImmediate), "DropRoofInCellPhaseOne")]
        static class Patch_RoofCollapserImmediate_DropRoofInCellPhaseOne
        {

            [HarmonyPrefix]
            static bool Prefix(IntVec3 c, Map map, List<Thing> outCrushedThings)
            {
                Log.Message("Roof Collapse Patch Reached");
                outCrushedThings = null;
                RoofDef roofDef = map.roofGrid.RoofAt(c);
                if (roofDef == null)
                {
                    return false;
                }

                List<Thing> thingList = c.GetThingList(map).ListFullCopy();

                foreach (Thing thing in thingList)
                {
                    if(XMTUtility.IsXenomorph(thing))
                    {
                        foreach( IntVec3 escapeCell in GenRadial.RadialCellsAround(c,1.5f,false))
                        {
                            if(escapeCell.Standable(map))
                            {
                                thing.Position = escapeCell;
                                break;
                            }
                        }
                    }
                }

                return true;
            }
        }
    }
}
