
using HarmonyLib;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    internal class XMTBloodfeedPatches
    {
        [HarmonyPatch(typeof(SanguophageUtility), nameof(SanguophageUtility.DoBite))]
        public static class SanguophageUtility_DoBite_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(Pawn biter, Pawn victim, float targetBloodLoss)
            {

                CompAcidBlood acidBlood = victim.GetAcidBloodComp();
                if (acidBlood != null)
                {
                    CompAcidBlood biterAcidBlood = biter.GetAcidBloodComp();

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
