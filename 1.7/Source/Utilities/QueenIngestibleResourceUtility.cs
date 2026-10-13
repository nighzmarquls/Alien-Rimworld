using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public struct QueenResourceGain
    {
        public QueenIngestibleResourceDef resource;
        public float amount;

        public QueenResourceGain(QueenIngestibleResourceDef resource, float amount)
        {
            this.resource = resource;
            this.amount = amount;
        }
    }

    public static class QueenIngestibleResourceUtility
    {
        private const int MaxFlattenDepth = 4;

        public static float CapacityFor(Pawn pawn, QueenIngestibleResourceDef resource)
        {
            if (pawn == null || resource == null)
            {
                return 0f;
            }

            float bodySize = Mathf.Max(0.1f, pawn.BodySize);
            float capacity = resource.baseCapacity * Mathf.Pow(bodySize, resource.bodySizeFactor);
            if (ModsConfig.BiotechActive && pawn.genes != null)
            {
                capacity *= 1f + Mathf.Max(0f, MetabolismFor(pawn)) * resource.metabolismFactor;
            }

            if (resource.capacityModifiers != null)
            {
                foreach (QueenIngestibleResourceCapacityModifier modifier in resource.capacityModifiers)
                {
                    if (modifier?.hediff == null)
                    {
                        continue;
                    }

                    foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
                    {
                        if (hediff.def == modifier.hediff)
                        {
                            capacity += hediff.Severity * modifier.addCapacityPerSeverity;
                            capacity *= 1f + hediff.Severity * modifier.capacityFactorPerSeverity;
                        }
                    }
                }
            }

            return Mathf.Max(0f, capacity);
        }

        private static int MetabolismFor(Pawn pawn)
        {
            int metabolism = 0;
            foreach (Gene gene in pawn.genes.GenesListForReading)
            {
                if (gene.Active)
                {
                    metabolism += gene.def.biostatMet;
                }
            }

            return metabolism;
        }

        public static List<QueenResourceGain> GetResourceGains(Thing thing, Pawn queen, CompQueenAssimilation comp)
        {
            List<QueenResourceGain> gains = new List<QueenResourceGain>();
            if (thing == null || queen == null || comp == null)
            {
                return gains;
            }

            foreach (QueenIngestibleResourceDef resource in DefDatabase<QueenIngestibleResourceDef>.AllDefsListForReading)
            {
                if (!comp.ResourceUnlocked(resource) || comp.GetResourceSpace(resource) <= 0f)
                {
                    continue;
                }

                float direct = DirectValue(thing.def, resource) * thing.stackCount;
                float derived = 0f;
                if (resource.allowDerivedIngredientValue && (direct <= 0f || resource.allowDirectAndDerivedValue))
                {
                    derived = FlattenedThingValue(thing, resource) * resource.derivedIngredientFactor;
                }

                float amount = resource.allowDirectAndDerivedValue ? direct + derived : Mathf.Max(direct, derived);
                amount = Mathf.Min(amount, comp.GetResourceSpace(resource));
                if (amount > 0f)
                {
                    gains.Add(new QueenResourceGain(resource, amount));
                }
            }

            return gains;
        }

        public static float DirectValue(ThingDef thingDef, QueenIngestibleResourceDef resource, bool derived = false)
        {
            return resource?.ValueFor(thingDef, derived) ?? 0f;
        }

        public static float FlattenedThingDefValue(ThingDef thingDef, QueenIngestibleResourceDef resource)
        {
            return FlattenedThingDefValue(thingDef, resource, new HashSet<ThingDef>(), 0);
        }

        private static float FlattenedThingValue(Thing thing, QueenIngestibleResourceDef resource)
        {
            float value = FlattenedThingDefValue(thing.def, resource, new HashSet<ThingDef>(), 0) * thing.stackCount;
            if (thing.Stuff != null)
            {
                value += DirectValue(thing.Stuff, resource, derived: true) * thing.def.GetStatValueAbstract(StatDefOf.Mass, thing.Stuff);
            }

            if (thing is Corpse corpse && corpse.InnerPawn != null && corpse.InnerPawn.RaceProps.IsMechanoid)
            {
                value += Mathf.Max(FlattenedMechanoidCorpseValue(corpse, resource), FlattenedButcherValue(corpse.InnerPawn.def, resource));
            }

            return value;
        }

        private static float FlattenedThingDefValue(ThingDef thingDef, QueenIngestibleResourceDef resource, HashSet<ThingDef> seen, int depth)
        {
            if (thingDef == null || resource == null || depth > MaxFlattenDepth || !seen.Add(thingDef))
            {
                return 0f;
            }

            float direct = DirectValue(thingDef, resource, derived: true);
            if (direct > 0f)
            {
                return direct;
            }

            float costValue = 0f;
            if (thingDef.CostList != null)
            {
                foreach (ThingDefCountClass cost in thingDef.CostList)
                {
                    costValue += FlattenedThingDefValue(cost.thingDef, resource, seen, depth + 1) * cost.count;
                }
            }

            float recipeValue = 0f;
            foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                if (recipe.products == null || !recipe.products.Any(product => product.thingDef == thingDef))
                {
                    continue;
                }

                float candidate = FlattenedRecipeValue(recipe, resource, depth + 1);
                if (candidate > 0f && (recipeValue <= 0f || candidate < recipeValue))
                {
                    recipeValue = candidate;
                }
            }

            if (costValue > 0f && recipeValue > 0f)
            {
                return Mathf.Min(costValue, recipeValue);
            }

            return Mathf.Max(costValue, recipeValue);
        }

        public static float FlattenedRecipeValue(RecipeDef recipe, QueenIngestibleResourceDef resource)
        {
            return FlattenedRecipeValue(recipe, resource, 0);
        }

        private static float FlattenedRecipeValue(RecipeDef recipe, QueenIngestibleResourceDef resource, int depth)
        {
            if (recipe?.ingredients == null || depth > MaxFlattenDepth)
            {
                return 0f;
            }

            float value = 0f;
            foreach (IngredientCount ingredient in recipe.ingredients)
            {
                float ingredientValue = 0f;
                foreach (ThingDef allowedDef in ingredient.filter.AllowedThingDefs)
                {
                    float candidate = FlattenedThingDefValue(allowedDef, resource, new HashSet<ThingDef>(), depth + 1);
                    if (candidate > 0f && (ingredientValue <= 0f || candidate < ingredientValue))
                    {
                        ingredientValue = candidate;
                    }
                }

                value += ingredientValue * ingredient.GetBaseCount();
            }

            return value;
        }

        private static float FlattenedButcherValue(ThingDef pawnDef, QueenIngestibleResourceDef resource)
        {
            float value = 0f;
            if (pawnDef?.butcherProducts != null)
            {
                foreach (ThingDefCountClass product in pawnDef.butcherProducts)
                {
                    value += FlattenedThingDefValue(product.thingDef, resource, new HashSet<ThingDef>(), 0) * product.count;
                }
            }

            return value;
        }

        private static float FlattenedMechanoidCorpseValue(Corpse corpse, QueenIngestibleResourceDef resource)
        {
            RecipeDef recipe = QueenMechGestationUtility.RecipeForProduct(corpse?.InnerPawn?.def, ignoreResearch: true);
            if (recipe == null)
            {
                return 0f;
            }

            float hitPointPercent = corpse.MaxHitPoints <= 0 ? 1f : Mathf.Clamp01((float)corpse.HitPoints / corpse.MaxHitPoints);
            return FlattenedRecipeValue(recipe, resource) * hitPointPercent;
        }

        public static string GainsLabel(List<QueenResourceGain> gains)
        {
            if (gains == null || gains.Count == 0)
            {
                return "";
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < gains.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append("+");
                builder.Append(Mathf.FloorToInt(gains[i].amount));
                builder.Append(" ");
                builder.Append(gains[i].resource.LabelCap);
            }

            return builder.ToString();
        }
    }
}
