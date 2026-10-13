using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    internal class CompAggressiveHunter : CompHostHunter
    {

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);


        }
        public override bool ShouldHunt()
        {
            return true;
        }

        public override bool TryResist(Pawn target)
        {
            if( base.TryResist(target))
            {
                Parent.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Manhunter, "", forced: true, forceWake: true, false);
                return true;
            }

            return false;
        }

        public override Pawn GetPreyTarget()
        {
            //Log.Message("checking for hosts around " + parent);
            IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(parent.Position, 2.5f, true);

            foreach (IntVec3 cell in cells)
            {
                Pawn candidate = cell.GetFirstPawn(parent.Map);

                Log.Message("checking if " + candidate + " is host");
                if (candidate == null)
                {
                    continue;
                }

                if (candidate == parent)
                {
                    continue;
                }

                if(parent.Faction != null && candidate.Faction == parent.Faction)
                {
                    continue;
                }

                if (XMTUtility.IsAcidBlooded(candidate))
                {
                    continue;
                }

                Log.Message("choosing " + candidate + " as host");
                return candidate;
            }

            return null;
        }
        
    }
}
