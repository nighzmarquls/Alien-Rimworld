

using HarmonyLib;
using Verse;
using Xenomorphtype;
using RimEffect;
using VEF.Abilities;

namespace XMT_CE
{
    internal class XMTMEHediffCompPatches
    {
        [HarmonyPatch(typeof(HediffComp_NaturalBiotic), nameof(HediffComp_NaturalBiotic.CompPostMake))]
        static class Patch_HediffComp_NaturalBiotic_CompPostMake
        {

            [HarmonyPrefix]
            static bool Prefix(ref HediffComp_NaturalBiotic __instance)
            {
                if(XMTUtility.IsXenomorph(__instance.Pawn))
                {
                    if (!__instance.Pawn.health.hediffSet.HasHediff(RE_DefOf.RE_BioticAmpHediff))
                    {
                        Hediff hediff = HediffMaker.MakeHediff(RE_DefOf.RE_BioticAmpHediff, __instance.Pawn, __instance.Pawn.RaceProps.body.GetPartsWithDef(InternalDefOf.StarbeastBrain).FirstOrFallback());
                        Hediff_Abilities val = (Hediff_Abilities)(object)((hediff is Hediff_Abilities) ? hediff : null);
                        if (val != null)
                        {
                            ((Hediff)(object)val).Severity = 0f;
                            val.giveRandomAbilities = true;
                            __instance.Pawn.health.AddHediff((Hediff)(object)val);
                            ((Hediff_Level)(object)val).SetLevelTo(0);
                        }
                    }
                    return false;
                }
                return true;
            }
        }
    }
}
