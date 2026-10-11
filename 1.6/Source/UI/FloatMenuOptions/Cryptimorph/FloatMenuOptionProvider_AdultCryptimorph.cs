using RimWorld;
using Verse;

namespace Xenomorphtype
{
    internal abstract class FloatMenuOptionProvider_AdultCryptimorph : FloatMenuOptionProvider
    {
        protected override bool Drafted => false;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;
        protected override bool RequiresManipulation => true;

        protected override bool AppliesInt(FloatMenuContext context)
        {
            Pawn pawn = context.FirstSelectedPawn;
            return pawn != null && pawn.Spawned && !pawn.Downed &&
                   XMTUtility.IsXenomorph(pawn) && pawn.DevelopmentalStage.Adult();
        }

        protected static bool IsEmptyStandableCell(Pawn pawn, IntVec3 cell)
        {
            return pawn?.Map != null && cell.InBounds(pawn.Map) && cell.Standable(pawn.Map) &&
                   cell.GetEdifice(pawn.Map) == null && cell.GetFirstPawn(pawn.Map) == null;
        }
    }
}
