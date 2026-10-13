using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public static class SovereignAlterationUtility
    {
        private const int BaseChannelTicks = 350;

        public static bool IsRemote(Pawn queen)
        {
            return queen?.GetComp<CompQueen>()?.HasFunctionalEvolutionFeature(RoyalEvolutionDefOf.Evo_RoyalCrown) == true;
        }

        public static int Range(Pawn queen)
        {
            return MutagenicMiasmaUtility.Range(queen);
        }

        public static int ChannelTicks(Pawn queen)
        {
            float hereditaryCapacity = queen == null || XenoStatDefOf.XMT_HereditaryCapacity == null
                ? 0f
                : queen.GetStatValue(XenoStatDefOf.XMT_HereditaryCapacity);
            return (int)EvolutionScalingUtility.Scale(BaseChannelTicks, 0f, 60f, 1200f,
                EvolutionScalingCurve.Inverse, EvolutionScalingRounding.Round,
                new EvolutionScalingFactor(hereditaryCapacity, 22f));
        }

        public static int WorkTicks(Pawn queen, bool remote)
        {
            return remote ? ChannelTicks(queen) : BaseChannelTicks;
        }

        public static bool CanSelect(Pawn queen, Thing target)
        {
            if (queen == null || target == null || target.Destroyed || !target.Spawned || queen.Map == null || target.Map != queen.Map)
            {
                return false;
            }

            if (target is Pawn pawn && pawn.Dead)
            {
                return false;
            }

            if (IsRemote(queen))
            {
                return target.Position.InHorDistOf(queen.Position, Range(queen));
            }

            return queen.Map.reachability.CanReach(queen.Position, target, PathEndMode.Touch, TraverseMode.PassDoors, Danger.Unspecified);
        }

        public static AcceptanceReport CanContinue(Pawn queen, Thing target, bool remote, IntVec3 casterStartPosition, IntVec3 targetStartPosition)
        {
            if (queen == null || target == null || target.Destroyed || !target.Spawned || queen.Map == null || target.Map != queen.Map)
            {
                return "XMT_SovereignAlterationInvalidTarget".Translate();
            }

            if (target is Pawn targetPawn && targetPawn.Dead)
            {
                return "XMT_SovereignAlterationInvalidTarget".Translate();
            }

            if (!remote)
            {
                return queen.Position.AdjacentTo8WayOrInside(target)
                    ? AcceptanceReport.WasAccepted
                    : "XMT_SovereignAlterationNotAdjacent".Translate(target.LabelShort);
            }

            if (!IsRemote(queen))
            {
                return "XMT_HorrorAdvanceInvalid_NoCrown".Translate();
            }

            if (queen.Position != casterStartPosition)
            {
                return "XMT_SovereignAlterationCasterMoved".Translate();
            }

            if (target.Position != targetStartPosition)
            {
                return "XMT_SovereignAlterationTargetMoved".Translate(target.LabelShort);
            }

            int range = Range(queen);
            if (!target.Position.InHorDistOf(queen.Position, range))
            {
                return "XMT_SovereignAlterationOutOfRange".Translate(target.LabelShort, range);
            }

            return AcceptanceReport.WasAccepted;
        }
    }
}
