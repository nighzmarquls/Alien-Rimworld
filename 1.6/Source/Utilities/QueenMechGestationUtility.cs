using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Xenomorphtype
{
    public static class QueenMechGestationUtility
    {
        private const int FallbackCycleTicks = 120000;
        private const float CompressionFactor = 0.025f;
        private const float ReferenceLightBodySize = 0.7f;
        private const float OvothroneAssistedMaxBodySize = 3.6f;
        private const int MinGestationTicks = 1500;
        private const int MaxGestationTicks = 20000;

        public static QueenIngestibleResourceDef MechMaterialResource => DefDatabase<QueenIngestibleResourceDef>.GetNamedSilentFail("XMT_MechanoidMaterial");

        public static IEnumerable<RecipeDef> EligibleRecipes(Pawn queen, bool ovothroneAssisted = false)
        {
            if (queen == null)
            {
                yield break;
            }

            foreach (RecipeDef recipe in AllMechGestationRecipes())
            {
                if (RecipeAllowedForQueen(queen, recipe, ovothroneAssisted))
                {
                    yield return recipe;
                }
            }
        }

        private static IEnumerable<RecipeDef> AllMechGestationRecipes()
        {
            HashSet<RecipeDef> yielded = new HashSet<RecipeDef>();
            foreach (string gestatorDefName in new[] { "MechGestator", "LargeMechGestator" })
            {
                ThingDef gestator = DefDatabase<ThingDef>.GetNamedSilentFail(gestatorDefName);
                if (gestator?.recipes == null)
                {
                    continue;
                }

                foreach (RecipeDef recipe in gestator.recipes)
                {
                    if (recipe != null && yielded.Add(recipe))
                    {
                        yield return recipe;
                    }
                }
            }
        }

        private static bool RecipeAllowedForQueen(Pawn queen, RecipeDef recipe, bool ovothroneAssisted)
        {
            if (!RecipeAvailableByResearch(recipe))
            {
                return false;
            }

            ThingDef product = ProductFor(recipe);
            if (product?.race == null || !product.race.IsMechanoid)
            {
                return false;
            }

            

            bool mechanoidSynthesis = false;
            bool integratedEggSack = false;
            float maxMechSize = queen.BodySize / 2;
            if (queen.GetComp<CompQueen>() is CompQueen compQueen)
            {
                mechanoidSynthesis = compQueen.HasActiveEvolution(RoyalEvolutionDefOf.Evo_MechanoidSynthesis);
                integratedEggSack = compQueen.HasActiveEvolution(RoyalEvolutionDefOf.Evo_IntegratedEggSac);
                
            }

            if (ovothroneAssisted)
            {
                maxMechSize = OvothroneAssistedMaxBodySize;
            }

            if (integratedEggSack)
            {
                maxMechSize = queen.BodySize + 0.6f;
            }

            if (mechanoidSynthesis && product.race.baseBodySize <= maxMechSize)
            {
                return true;
            }

            return IsLightMech(product, recipe);
        }

        public static RecipeDef RecipeForProduct(ThingDef product, bool ignoreResearch = false)
        {
            if (product == null)
            {
                return null;
            }

            foreach (RecipeDef recipe in AllMechGestationRecipes())
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

        public static bool CanGestate(Pawn queen, RecipeDef recipe, out string reason, bool ovothroneAssisted = false)
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

            if (recipe == null)
            {
                reason = "XMT_MechGestationUnavailable".Translate();
                return false;
            }

            if (!RecipeAvailableByResearch(recipe))
            {
                reason = recipe.researchPrerequisite == null
                    ? "XMT_MechGestationUnavailable".Translate()
                    : "XMT_MechGestationResearchMissing".Translate(recipe.researchPrerequisite.LabelCap);
                return false;
            }

            if (!RecipeAllowedForQueen(queen, recipe, ovothroneAssisted))
            {
                reason = "XMT_MechGestationUnavailable".Translate();
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

        public static bool TryFinishGestation(Pawn queen, RecipeDef recipe, IntVec3 cell, Map map, out Pawn mech, out string reason, bool ovothroneAssisted = false)
        {
            mech = null;
            reason = null;
            CompQueenAssimilation comp = queen?.GetComp<CompQueenAssimilation>();
            QueenIngestibleResourceDef resource = MechMaterialResource;
            if (queen == null || comp == null || resource == null || map == null)
            {
                reason = "XMT_MechGestationUnavailable".Translate();
                return false;
            }

            if (!CanGestate(queen, recipe, out reason, ovothroneAssisted))
            {
                return false;
            }

            ThingDef product = ProductFor(recipe);
            PawnKindDef kind = PawnKindFor(product);
            if (kind == null)
            {
                reason = "XMT_MechGestationUnavailable".Translate();
                return false;
            }

            PawnGenerationRequest request = new PawnGenerationRequest(kind, queen.Faction);
            request.FixedBiologicalAge = 0;
            request.FixedChronologicalAge = 0;
            mech = PawnGenerator.GeneratePawn(request);
            if (queen.mechanitor == null || !queen.mechanitor.CanOverseeSubject(mech))
            {
                reason = "XMT_MechGestationNoBandwidth".Translate();
                return false;
            }

            float cost = MaterialCost(queen, recipe);
            if (!comp.TrySpendResource(resource, cost))
            {
                reason = "XMT_MechGestationNeedsMaterial".Translate(Mathf.CeilToInt(cost), resource.LabelCap);
                return false;
            }

            GenSpawn.Spawn(mech, cell, map, WipeMode.Vanish);
            queen.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech);
            queen.mechanitor.AssignPawnControlGroup(mech, MechWorkModeDefOf.Work);
            queen.mechanitor.Notify_BandwidthChanged();
            SoundDefOf.CocoonDestroyed.PlayOneShot(new TargetInfo(cell, map));
            MakeGestationFilth(cell, map);
            Messages.Message("XMT_MechGestationComplete".Translate(queen.Named("PAWN"), mech.Named("MECH")).Resolve(), mech, MessageTypeDefOf.PositiveEvent, false);
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

        private static void MakeGestationFilth(IntVec3 center, Map map)
        {
            FilthMaker.TryMakeFilth(center, map, InternalDefOf.Starbeast_Filth_Resin, count: 8);
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, 1.5f, false))
            {
                if (cell.InBounds(map) && Rand.Chance(0.45f))
                {
                    FilthMaker.TryMakeFilth(cell, map, InternalDefOf.Starbeast_Filth_Resin);
                }
            }
        }
    }
}
