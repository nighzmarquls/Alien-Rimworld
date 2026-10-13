using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{ 
    public class CompUmbilical : CompHostHunter
    {

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            if (Parent != null)
            {
                Hediff helplessness = Parent.health.GetOrAddHediff(XenoGeneDefOf.XMT_Helpless); 
            }
            
        }
        public override bool ShouldHunt()
        {
            //Log.Message("checking hosts for " + parent);
            return true;
        }

        public override void StartHunt(Pawn prey)
        {
            //Log.Message(parent + " trying to attach to " + prey);
            TryAttachToHost(prey);
        }
        public override Pawn GetPreyTarget()
        {
            //Log.Message("checking for hosts around " + parent);
            IEnumerable<IntVec3> cells =GenRadial.RadialCellsAround(parent.Position, 1.5f, true);

            foreach (IntVec3 cell in cells)
            {
                Pawn candidate = cell.GetFirstPawn(parent.Map);

                Log.Message("checking if " + candidate + " is host");
                if (candidate == null)
                {
                    continue;
                }

                if(candidate == parent)
                {
                    continue;
                }

                if (XMTUtility.IsAcidBlooded(candidate))
                {
                    continue;
                }

                if (XMTUtility.IsInorganic(candidate))
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
