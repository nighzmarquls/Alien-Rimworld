
using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class WorkGiver_ExtractAcid: WorkGiver_XMTContainedPawn
    {
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (XenoGeneDefOf.XMT_Acid_Utilization?.IsFinished != true || !pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }

            return GetEntity(t) != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (XenoGeneDefOf.XMT_Acid_Utilization?.IsFinished != true || GetEntity(t) == null)
            {
                return null;
            }

            return JobMaker.MakeJob(XenoWorkDefOf.XMT_ExtractAcid, t);
        }

        protected override Pawn GetEntity(Thing potentialPlatform)
        {
            Pawn heldPawn = XMTContainmentUtility.HeldPawn(potentialPlatform);
            if (heldPawn != null)
            {
                if (!XMTUtility.IsXenomorph(heldPawn))
                {
                    return null;
                }

                if (heldPawn.Info()?.extractAcid != true)
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
