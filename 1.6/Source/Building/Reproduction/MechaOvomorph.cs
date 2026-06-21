using RimWorld;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class MechaOvomorph : Ovomorph
    {
        protected override bool CanHatchNow(out string reason)
        {
            if (!base.CanHatchNow(out reason))
            {
                return false;
            }

            Pawn queen = HatchingEgg?.mother ?? XMTUtility.GetQueen();
            int bandwidthCost = HatchedBandwidthCost();
            if (bandwidthCost <= 0 || queen?.Faction != Faction.OfPlayer)
            {
                return true;
            }

            if (queen.mechanitor == null)
            {
                reason = "XMT_MechGestationNoBandwidth".Translate();
                return false;
            }

            int availableBandwidth = queen.mechanitor.TotalBandwidth - queen.mechanitor.UsedBandwidth;
            if (availableBandwidth < bandwidthCost)
            {
                reason = "XMT_MechGestationNoBandwidth".Translate();
                return false;
            }

            return true;
        }

        protected override void PostHatchPawn(Pawn pawn)
        {
            base.PostHatchPawn(pawn);
            Pawn queen = HatchingEgg?.mother ?? XMTUtility.GetQueen();
            if (pawn == null || queen?.Faction != Faction.OfPlayer || queen.mechanitor == null || pawn.OverseerSubject == null || PawnBandwidthCost(pawn) <= 0)
            {
                return;
            }

            InorganicSubversionUtility.AssignMinorMechToQueen(pawn, queen);
        }

        private int HatchedBandwidthCost()
        {
            ThingDef race = HatchingEgg?.hatchedPawnKind?.race;
            if (race == null)
            {
                return 0;
            }

            if (!ModsConfig.BiotechActive || StatDefOf.BandwidthCost == null)
            {
                return 0;
            }

            return Mathf.Max(0, Mathf.CeilToInt(race.GetStatValueAbstract(StatDefOf.BandwidthCost)));
        }

        private static int PawnBandwidthCost(Pawn pawn)
        {
            if (pawn == null || !ModsConfig.BiotechActive || StatDefOf.BandwidthCost == null)
            {
                return 0;
            }

            return Mathf.Max(0, Mathf.CeilToInt(pawn.GetStatValue(StatDefOf.BandwidthCost)));
        }
    }
}
