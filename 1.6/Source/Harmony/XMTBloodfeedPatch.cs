
using HarmonyLib;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    internal class XMTBloodfeedPatch
    {
        [HarmonyPatch(typeof(SanguophageUtility), nameof(SanguophageUtility.DoBite))]
        public static class SanguophageUtility_DoBite_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(Pawn biter, Pawn victim, float targetBloodLoss)
            {

                CompAcidBlood acidBlood = victim.GetComp<CompAcidBlood>();
                if (acidBlood != null)
                {
                    CompAcidBlood biterAcidBlood = biter.GetComp<CompAcidBlood>();

                    if(biterAcidBlood != null)
                    {
                        return true;
                    }

                    acidBlood.DamageBiter(biter);
                    acidBlood.TrySplashAcid(acidBlood.GetBloodFullness()+targetBloodLoss);
                    return false;
                }
                return true;
            }
        }
    }
}
