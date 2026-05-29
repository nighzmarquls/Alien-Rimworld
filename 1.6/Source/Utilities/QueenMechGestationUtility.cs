using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public static class QueenMechGestationUtility
    {
        private const int FallbackCycleTicks = 120000;
        private const float CompressionFactor = 0.025f;
        private const float ReferenceLightBodySize = 0.7f;
        private const int MinGestationTicks = 1500;
        private const int MaxGestationTicks = 20000;

        public static QueenIngestibleResourceDef MechMaterialResource => DefDatabase<QueenIngestibleResourceDef>.GetNamedSilentFail("XMT_MechanoidMaterial");

        public static IEnumerable<RecipeDef> EligibleRecipes(Pawn queen)
        {
            ThingDef lightGestator = DefDatabase<ThingDef>.GetNamedSilentFail("MechGestator");
            if (lightGestator?.recipes == null)
            {
                yield break;
            }

            foreach (RecipeDef recipe in lightGestator.recipes)
            {
                if (!RecipeAvailableByResearch(recipe))
                {
                    continue;
                }

                ThingDef product = ProductFor(recipe);
                if (product == null || product.race == null || !product.race.IsMechanoid)
                {
                    continue;
                }

                if (IsLightMech(product, recipe))
                {
                    yield return recipe;
                }
            }
        }

        public static ThingDef ProductFor(RecipeDef recipe)
        {
            if (recipe?.products == null)
            {
                return null;
            }

            foreach (ThingDefCountClass product in recipe.products)
            {
                if (product?.thingDef?.race?.IsMechanoid == true)
                {
                    return product.thingDef;
                }
            }

            return null;
        }

        public static RecipeDef RecipeForProduct(ThingDef product, bool ignoreResearch = false)
        {
            if (product == null)
            {
                return null;
            }

            ThingDef lightGestator = DefDatabase<ThingDef>.GetNamedSilentFail("MechGestator");
            if (lightGestator?.recipes == null)
            {
                return null;
            }

            foreach (RecipeDef recipe in lightGestator.recipes)
            {
                if (!ignoreResearch && !RecipeAvailableByResearch(recipe))
                {
                    continue;
                }

                if (ProductFor(recipe) == product)
                {
                    return recipe;
                }
            }

            return null;
        }

        public static bool CanGestate(Pawn queen, RecipeDef recipe, out string reason)
        {
            reason = null;
            CompQueenAssimilation comp = queen?.GetComp<CompQueenAssimilation>();
            QueenIngestibleResourceDef resource = MechMaterialResource;
            if (queen == null || comp == null || resource == null)
            {
                reason = "XMT_MechGestationUnavailable".Translate();
                return false;
            }

            if (!comp.ResourceUnlocked(resource))
            {
                reason = "XMT_MechGestationLocked".Translate();
                return false;
            }

            if (!RecipeAvailableByResearch(recipe))
            {
                reason = "XMT_MechGestationResearchMissing".Translate(recipe.researchPrerequisite.LabelCap);
                return false;
            }

            float cost = MaterialCost(queen, recipe);
            if (comp.GetResourceAmount(resource) < cost)
            {
                reason = "XMT_MechGestationNeedsMaterial".Translate(Mathf.CeilToInt(cost), resource.LabelCap);
                return false;
            }

            ThingDef product = ProductFor(recipe);
            if (product == null)
            {
                reason = "XMT_MechGestationUnavailable".Translate();
                return false;
            }

            if (queen.mechanitor == null)
            {
                reason = "XMT_MechGestationNoBandwidth".Translate();
                return false;
            }

            return true;
        }

        public static float MaterialCost(Pawn queen, RecipeDef recipe)
        {
            QueenIngestibleResourceDef resource = MechMaterialResource;
            float cost = QueenIngestibleResourceUtility.FlattenedRecipeValue(recipe, resource);
            return Mathf.Ceil(cost * EggFactor(queen));
        }

        public static int GestationTicks(Pawn queen, RecipeDef recipe)
        {
            ThingDef product = ProductFor(recipe);
            float baseTicks = recipe.formingTicks > 0 ? recipe.formingTicks : Mathf.Max(1, recipe.gestationCycles) * FallbackCycleTicks;
            float sizeFactor = product?.race == null ? 1f : Mathf.Max(0.1f, product.race.baseBodySize) / ReferenceLightBodySize;
            return Mathf.Clamp(Mathf.RoundToInt(baseTicks * CompressionFactor * sizeFactor * EggFactor(queen)), MinGestationTicks, MaxGestationTicks);
        }

        public static PawnKindDef PawnKindFor(ThingDef product)
        {
            return DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(kind => kind.race == product);
        }

        private static bool RecipeAvailableByResearch(RecipeDef recipe)
        {
            return recipe != null && (recipe.researchPrerequisite == null || recipe.researchPrerequisite.IsFinished);
        }

        private static bool IsLightMech(ThingDef product, RecipeDef recipe)
        {
            if (product.race.mechWeightClass == MechWeightClassDefOf.Light)
            {
                return true;
            }

            return product.race.mechWeightClass == null
                && product.race.baseBodySize <= 1f
                && recipe.gestationCycles <= 1;
        }

        private static float EggFactor(Pawn queen)
        {
            return XMT_OvomorphLayCostModifierListDef.CostFactorFor(queen);
        }
    }
}
