

using RimWorld;
using Verse;

namespace Xenomorphtype
{
    public class PawnMesoSkeletonDrawer : PawnScarDrawer
    {
        protected override string ScarTexturePath => "Things/Pawn/Overlays/MesoSkeleton/MesoSkeletonOverlay";

        protected override float ScaleFactor => 0.5f;

        public PawnMesoSkeletonDrawer(Pawn pawn)
            : base(pawn)
        {
        }
    }
}
