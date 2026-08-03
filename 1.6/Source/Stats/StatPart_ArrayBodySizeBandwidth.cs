using RimWorld;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class StatPart_ArrayBodySizeBandwidth : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            Pawn pawn = req.Thing as Pawn;
            if (AppliesTo(pawn))
            {
                val += BandwidthFor(pawn);
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            Pawn pawn = req.Thing as Pawn;
            if (!AppliesTo(pawn))
            {
                return null;
            }

            int bandwidth = BandwidthFor(pawn);
            return "XMT_ArrayBodySizeBandwidth".Translate(bandwidth, pawn.BodySize.ToString("0.##"));
        }

        private static int BandwidthFor(Pawn pawn)
        {
            float bodySize = Mathf.Max(0f, pawn?.BodySize ?? 0f);
            return Mathf.FloorToInt(bodySize * bodySize / 3f);
        }

        private static bool AppliesTo(Pawn pawn)
        {
            return pawn?.mechanitor != null
                && pawn.GetComp<CompQueen>()?.HasFunctionalEvolutionFeature(RoyalEvolutionDefOf.Evo_MechanitorArray) == true;
        }
    }
}
