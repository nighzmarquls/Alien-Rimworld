using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace XMT_RJW
{
    public class CorruptingFluid
    {
        public string fluid;
        public float essence;
    }

    public class RJWReproductionDef : Def
    {
        public List<CorruptingFluid> fluids = new List<CorruptingFluid>();
        public HediffDef reproductiveTissue;
        public HediffDef femalePart;
        public float lovinMutationChance = 0.5f;
        public float repeatedLovinGrowth = 0.1f;
        public float maturationPerDay = 0.25f;
        public float fluidRetentionDays = 1f;
        public float maxFluidEssence = 4f;
        public static RJWReproductionDef Current => DefDatabase<RJWReproductionDef>.GetNamed("XMT_RJWReproduction");

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (reproductiveTissue == null || femalePart == null) yield return "Missing reproductive anatomy defs";
            if (maturationPerDay <= 0f || fluidRetentionDays <= 0f || maxFluidEssence < 0f)
                yield return "Invalid reproductive progression/exposure tuning";
            if (lovinMutationChance < 0f || lovinMutationChance > 1f || repeatedLovinGrowth < 0f)
                yield return "Invalid lovin mutation tuning";
        }
    }
}
