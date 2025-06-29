using HarmonyLib;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    internal class XMTExplosivePatches
    {
        [HarmonyPatch(typeof(CompExplosive), "Detonate")]
        public static class CompExplosive_Detonate
        {
            [HarmonyPrefix]
            public static void Prefix(CompProperties ___props, ThingWithComps ___parent)
            {
                Log.Message("Explosive Detonating on " + ___parent);
                CompAcidBlood acidblood = ___parent.GetComp<CompAcidBlood>();
                if (acidblood != null)
                {
                    Log.Message(___parent + " has acid blood");
                    CompProperties_Explosive Props = ___props as CompProperties_Explosive;
                    acidblood.CreateAcidExplosion(Props.explosiveRadius);
                }
            }
        }
    }
}
