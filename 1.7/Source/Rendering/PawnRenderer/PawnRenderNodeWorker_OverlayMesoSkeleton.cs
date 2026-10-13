using RimWorld;
using Verse;

namespace Xenomorphtype
{
    public class PawnRenderNodeWorker_OverlayMesoSkeleton : PawnRenderNodeWorker_Overlay
    {
        protected override PawnOverlayDrawer OverlayDrawer(Pawn pawn)
        {
            return pawn.MesoSkeletonDrawer();
        }

        public override bool ShouldListOnGraph(PawnRenderNode node, PawnDrawParms parms)
        {
            return true;
        }

        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            return true;
        }
    }
}
