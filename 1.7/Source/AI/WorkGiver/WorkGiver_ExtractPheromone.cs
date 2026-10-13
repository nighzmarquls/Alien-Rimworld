using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class WorkGiver_ExtractPheromone : WorkGiver_XMTContainedPawn
    {
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return XenoGeneDefOf.XMT_CryptimorphicPheromones?.IsFinished == true &&
                pawn.CanReserve(t, 1, -1, null, forced) && GetEntity(t) != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return XenoGeneDefOf.XMT_CryptimorphicPheromones?.IsFinished != true || GetEntity(t) == null
                ? null
                : JobMaker.MakeJob(XenoWorkDefOf.XMT_ExtractPheromone, t);
        }

        protected override Pawn GetEntity(Thing potentialPlatform)
        {
            Pawn heldPawn = XMTContainmentUtility.HeldPawn(potentialPlatform);
            if (heldPawn?.Info()?.extractPheromone == true &&
                PheromoneUtility.CanExtractFrom(heldPawn))
            {
                return heldPawn;
            }

            return null;
        }
    }
}
