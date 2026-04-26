
using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class WorkGiver_ExtractAcid: WorkGiver_EntityOnPlatform
    {
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }

            return GetEntity(t) != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (GetEntity(t) == null)
            {
                return null;
            }

            return JobMaker.MakeJob(XenoWorkDefOf.XMT_ExtractAcid, t);
        }

        protected override Pawn GetEntity(Thing potentialPlatform)
        {
            if (potentialPlatform is Building_HoldingPlatform { HeldPawn: var heldPawn })
            {
                if (heldPawn == null)
                {
                    return null;
                }

                if (!heldPawn.Info().extractAcid)
                {
                    return null;
                }

                if (!XMTUtility.IsXenomorph(heldPawn))
                {
                    return null;
                }

                if(!BioUtility.PawnHasEnoughForExtraction(heldPawn, false))
                {
                    return null;
                }
                
                return heldPawn;
            }

            return null;
        }
    }
}
