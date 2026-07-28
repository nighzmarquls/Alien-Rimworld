using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    public static class XMT_IFFUtility
    {
        public static bool IsAutomaticTurretAggressionAppropriate(Thing turret, Pawn target)
        {
            if (turret == null || target == null || target.def != InternalDefOf.XMT_Starbeast_AlienRace)
            {
                return true;
            }

            Pawn_ApparelTracker apparelTracker = target.apparel;
            if (apparelTracker == null)
            {
                return true;
            }

            bool wearingIFFCollar = false;
            List<Apparel> wornApparel = apparelTracker.WornApparel;
            for (int i = 0; i < wornApparel.Count; i++)
            {
                if (wornApparel[i].def == InternalDefOf.XMT_IFFCollar)
                {
                    wearingIFFCollar = true;
                    break;
                }
            }

            if (!wearingIFFCollar || turret.Faction == null || target.Faction == null)
            {
                return true;
            }

            return turret.Faction != target.Faction;
        }
    }
}
