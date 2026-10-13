using HarmonyLib;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    [HarmonyPatch(typeof(Pawn_GeneTracker), nameof(Pawn_GeneTracker.AddGene), new[] { typeof(GeneDef), typeof(bool) })]
    internal static class XMTGenePatches
    {
        private static void Prefix(Pawn ___pawn, ref GeneDef geneDef)
        {
            geneDef = XMT_GeneRemapListDef.GetRemappedGeneFor(___pawn?.def, geneDef);
        }
    }
}
