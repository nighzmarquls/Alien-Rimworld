using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    // Definition-only, lazy runtime caches. Never retain Things, Pawns, maps or live corpse yields.
    public static class ResourceHelperUtility
    {
        private const int MaxDepth = 24;
        private sealed class Cache
        {
            public readonly Dictionary<(ThingDef, ThingDef, bool, bool), float> fractions = new Dictionary<(ThingDef, ThingDef, bool, bool), float>();
            public readonly Dictionary<(ThingDef, ThingDef, bool, bool), float> units = new Dictionary<(ThingDef, ThingDef, bool, bool), float>();
            public readonly Dictionary<(ThingDef, ThingDef, bool, bool), bool> possibilities = new Dictionary<(ThingDef, ThingDef, bool, bool), bool>();
            public readonly Dictionary<ThingDef, bool> corpses = new Dictionary<ThingDef, bool>();
            public readonly Dictionary<(RecipeDef, ThingDef, ThingDef, bool), float> recipeUnits = new Dictionary<(RecipeDef, ThingDef, ThingDef, bool), float>();
        }
        private static readonly Dictionary<QueenIngestibleResourceDef, Cache> caches = new Dictionary<QueenIngestibleResourceDef, Cache>();
        private static readonly Dictionary<string, Cache> unions = new Dictionary<string, Cache>();
        private static readonly Dictionary<ThingDef, List<RecipeDef>> recipes = new Dictionary<ThingDef, List<RecipeDef>>();
        private static readonly Dictionary<(ResourceTerrainDef, TerrainDef), bool> terrains = new Dictionary<(ResourceTerrainDef, TerrainDef), bool>();
        private static readonly HashSet<string> reportedTerrainConflicts = new HashSet<string>();
        private static readonly Dictionary<QueenIngestibleResourceDef, int> resourceIds = new Dictionary<QueenIngestibleResourceDef, int>();

        public static void ClearCaches()
        {
            caches.Clear(); unions.Clear(); recipes.Clear(); terrains.Clear(); resourceIds.Clear(); reportedTerrainConflicts.Clear();
        }
        private static Cache For(QueenIngestibleResourceDef resource)
        {
            if (!caches.TryGetValue(resource, out Cache cache)) caches.Add(resource, cache = new Cache());
            return cache;
        }
        private static Cache For(List<QueenIngestibleResourceDef> resources)
        {
            if (resources.Count == 1) return For(resources[0]);
            foreach (QueenIngestibleResourceDef resource in resources)
                if (!resourceIds.ContainsKey(resource)) resourceIds.Add(resource, resourceIds.Count);
            string key = string.Join(",", resources.Select(r => resourceIds[r]).OrderBy(id => id));
            if (!unions.TryGetValue(key, out Cache cache)) unions.Add(key, cache = new Cache());
            return cache;
        }
        private static List<RecipeDef> RecipesFor(ThingDef def)
        {
            if (!recipes.TryGetValue(def, out List<RecipeDef> result))
            {
                result = DefDatabase<RecipeDef>.AllDefsListForReading
                    .Where(r => r.products != null && r.products.Any(p => p.thingDef == def && p.count > 0)).ToList();
                recipes.Add(def, result);
            }
            return result;
        }
        public static bool Available(QueenIngestibleResourceDef resource, Thing consumer)
        {
            if (resource == null) return false;
            Pawn pawn = consumer as Pawn;
            bool queen = pawn != null && XMTUtility.IsQueen(pawn);
            if ((resource.queenOnly && !queen) || (resource.nonQueenOnly && queen)) return false;
            if (resource.requiredGene != null && pawn?.genes?.HasActiveGene(resource.requiredGene) != true) return false;
            if (resource.requiredEvolution != null && pawn?.GetComp<CompQueen>()?.HasActiveEvolution(resource.requiredEvolution) != true) return false;
            if (resource.prerequisiteAssimilations != null)
                foreach (QueenAssimilationDef prerequisite in resource.prerequisiteAssimilations)
                    if (pawn?.GetComp<CompQueenAssimilation>()?.HasAssimilated(prerequisite) != true) return false;
            return true;
        }
        private static bool Direct(ThingDef def, QueenIngestibleResourceDef resource, bool derived = false)
            => def != resource.product && resource.ValueFor(def, derived) > 0f;

        private static bool Possible(ThingDef def, ThingDef stuff, bool unknownStuff, QueenIngestibleResourceDef resource,
            HashSet<ThingDef> path, int depth, out bool complete)
        {
            complete = true;
            if (def == null || def.IsCorpse || def.race != null || resource.product == def) return false;
            var key = (def, stuff, unknownStuff, depth > 0);
            Cache cache = For(resource);
            if (cache.possibilities.TryGetValue(key, out bool cached)) return cached;
            if (depth >= MaxDepth || !path.Add(def)) { complete = false; return false; }
            bool result = Direct(def, resource, depth > 0);
            try
            {
                if (!result && resource.allowDerivedIngredientValue)
                    foreach (RecipeDef recipe in RecipesFor(def))
                        foreach (IngredientCount ingredient in recipe.ingredients)
                            foreach (ThingDef allowed in ingredient.filter.AllowedThingDefs)
                            {
                                if (!ingredient.IsFixedIngredient && recipe.fixedIngredientFilter != null && !recipe.fixedIngredientFilter.Allows(allowed)) continue;
                                if (IsStuffIngredient(def, ingredient) && IsStuffSlot(def, allowed))
                                {
                                    if (stuff != null && allowed != stuff) continue;
                                    if (unknownStuff) { result = true; continue; }
                                }
                                result |= Possible(allowed, null, allowed.MadeFromStuff, resource, path, depth + 1, out bool done);
                                complete &= done;
                            }
            }
            finally { path.Remove(def); }
            if (complete) cache.possibilities[key] = result;
            return result;
        }
        private static bool IsStuffSlot(ThingDef product, ThingDef ingredient)
            => product.MadeFromStuff && ingredient.IsStuff && product.stuffCategories != null
                && ingredient.stuffProps?.categories?.Any(c => product.stuffCategories.Contains(c)) == true;
        private static bool IsStuffIngredient(ThingDef product, IngredientCount ingredient)
            => product.MadeFromStuff && product.costStuffCount > 0
                && Mathf.Abs(ingredient.GetBaseCount() - product.costStuffCount) < 0.001f
                && ingredient.filter.AllowedThingDefs.All(d => IsStuffSlot(product, d));

        public static float EligibleFraction(ThingDef def, ThingDef stuff, QueenIngestibleResourceDef resource)
        {
            if (def == null || resource == null || def.IsCorpse || def.race != null) return 0f;
            return Fraction(def, stuff, false, new List<QueenIngestibleResourceDef> { resource }, new HashSet<ThingDef>(), 0, out _);
        }
        // Union is evaluated inside each recipe before choosing the least eligible alternative.
        // Unioning independently minimized resource results would lose overlapping alternatives.
        private static float Fraction(ThingDef def, ThingDef stuff, bool unknownStuff, List<QueenIngestibleResourceDef> resources,
            HashSet<ThingDef> path, int depth, out bool complete)
        {
            complete = true;
            if (def == null || def.IsCorpse || def.race != null) return 0f;
            Cache cache = For(resources);
            var key = (def, stuff, unknownStuff, depth > 0);
            if (cache.fractions.TryGetValue(key, out float cached)) return cached;
            if (depth >= MaxDepth || !path.Add(def)) { complete = false; return 0f; }
            float result = 0f;
            try
            {
                if (resources.Any(r => Direct(def, r, depth > 0))) result = 1f;
                else
                {
                    List<QueenIngestibleResourceDef> derived = resources.Where(r => r.allowDerivedIngredientValue && r.product != def).ToList();
                    // Early out: recipes are static; a non-direct thing with no producing recipe is ineligible.
                    List<RecipeDef> candidates = RecipesFor(def);
                    if (derived.Count > 0 && candidates.Count > 0)
                    {
                        result = 1f;
                        foreach (RecipeDef recipe in candidates)
                        {
                            float candidate = RecipeFraction(recipe, def, stuff, unknownStuff, derived, path, depth + 1, out bool done);
                            complete &= done;
                            result = Mathf.Min(result, candidate); // Zero alternatives count too.
                        }
                    }
                }
            }
            finally { path.Remove(def); }
            // A path-limited answer is local to this traversal, never a permanent negative cache entry.
            if (complete) cache.fractions[key] = Mathf.Clamp01(result);
            return Mathf.Clamp01(result);
        }
        private struct IngredientOption
        {
            public float mass;
            public float eligible;
        }
        private static float RecipeFraction(RecipeDef recipe, ThingDef product, ThingDef stuff, bool unknownStuff,
            List<QueenIngestibleResourceDef> resources, HashSet<ThingDef> path, int depth, out bool complete)
        {
            complete = true;
            var groups = new List<List<IngredientOption>>();
            if (recipe.ingredients == null || recipe.ingredients.Count == 0) return 0f;
            foreach (IngredientCount ingredient in recipe.ingredients)
            {
                var options = new List<IngredientOption>();
                foreach (ThingDef allowed in ingredient.filter.AllowedThingDefs)
                {
                    // Vanilla RecipeDef.IsIngredient exempts fixed slots from the recipe-wide
                    // filter. Generated non-stuff recipes leave that wider filter empty.
                    if (!ingredient.IsFixedIngredient && recipe.fixedIngredientFilter != null && !recipe.fixedIngredientFilter.Allows(allowed)) continue;
                    // Actual stuff constrains the stuff slot, not unrelated ingredient slots (components etc.).
                    if (IsStuffIngredient(product, ingredient) && IsStuffSlot(product, allowed))
                    {
                        if (stuff != null && allowed != stuff) continue;
                        if (unknownStuff)
                        {
                            float assumedMass = IngredientMass(ingredient, allowed, recipe);
                            options.Add(new IngredientOption { mass = assumedMass, eligible = assumedMass });
                            continue;
                        }
                    }
                    float fraction = Fraction(allowed, null, allowed.MadeFromStuff, resources, path, depth, out bool done);
                    complete &= done;
                    float mass = IngredientMass(ingredient, allowed, recipe);
                    options.Add(new IngredientOption { mass = mass, eligible = mass * fraction });
                }
                if (options.Count == 0) return 0f;
                groups.Add(options);
            }
            // Minimize the mass ratio over all allowed ingredient combinations without enumerating
            // their Cartesian product. Each binary-search step independently minimizes E - ratio*M.
            if (groups.Sum(g => g.Max(o => o.mass)) <= 0f) return 0f;
            float low = 0f, high = 1f;
            for (int i = 0; i < 24; i++)
            {
                float ratio = (low + high) * 0.5f;
                float difference = 0f;
                foreach (List<IngredientOption> options in groups)
                    difference += options.Min(o => o.eligible - ratio * o.mass);
                if (difference >= 0f) low = ratio; else high = ratio;
            }
            return low < 0.000001f ? 0f : high;
        }
        private static float IngredientMass(IngredientCount ingredient, ThingDef def, RecipeDef recipe)
        {
            float value = recipe.IngredientValueGetter.ValuePerUnitOf(def);
            float count = value > 0f ? ingredient.GetBaseCount() / value : 0f;
            return Mathf.Max(0f, count * def.GetStatValueAbstract(StatDefOf.Mass));
        }

        // Queen storage retains its unit-valued entries. It shares the recursive per-resource
        // cache infrastructure, while mass converters use eligible fractions of the final product.
        public static float DefinitionUnits(ThingDef def, QueenIngestibleResourceDef resource, ThingDef stuff = null)
            => Units(def, stuff, false, resource, new HashSet<ThingDef>(), 0, out _);
        private static float Units(ThingDef def, ThingDef stuff, bool unknownStuff, QueenIngestibleResourceDef resource,
            HashSet<ThingDef> path, int depth, out bool complete)
        {
            complete = true;
            if (def == null || resource == null || def.IsCorpse) return 0f;
            Cache cache = For(resource);
            var key = (def, stuff, unknownStuff, depth > 0);
            if (cache.units.TryGetValue(key, out float cached)) return cached;
            if (depth >= MaxDepth || !path.Add(def)) { complete = false; return 0f; }
            float result = 0f;
            try
            {
                float direct = resource.ValueFor(def, depth > 0);
                if (direct > 0f) result = direct;
                else
                {
                    float derived = float.PositiveInfinity;
                    if (def.CostList?.Count > 0 || def.costStuffCount > 0)
                    {
                        float cost = 0f;
                        if (def.CostList != null)
                            foreach (ThingDefCountClass entry in def.CostList)
                            {
                                cost += Units(entry.thingDef, null, entry.thingDef.MadeFromStuff, resource, path, depth + 1, out bool done) * entry.count;
                                complete &= done;
                            }
                        if (def.costStuffCount > 0)
                        {
                            // Forgive unspecified stuff on recursive recipe ingredients only.
                            if (stuff != null) cost += resource.ValueFor(stuff, true) * def.costStuffCount;
                            else if (unknownStuff) cost += def.costStuffCount;
                        }
                        derived = cost;
                    }
                    foreach (RecipeDef recipe in RecipesFor(def))
                    {
                        float candidate = RecipeUnits(recipe, resource, stuff, path, depth + 1, out bool done, def, unknownStuff);
                        complete &= done;
                        int productCount = recipe.products.Where(p => p.thingDef == def).Sum(p => p.count);
                        derived = Mathf.Min(derived, candidate / Mathf.Max(1, productCount));
                    }
                    if (float.IsPositiveInfinity(derived)) derived = 0f;
                    result = derived;
                }
            }
            finally { path.Remove(def); }
            if (complete) cache.units[key] = Mathf.Max(0f, result);
            return Mathf.Max(0f, result);
        }
        public static float RecipeUnits(RecipeDef recipe, QueenIngestibleResourceDef resource)
            => RecipeUnits(recipe, resource, null, new HashSet<ThingDef>(), 0, out _);
        private static float RecipeUnits(RecipeDef recipe, QueenIngestibleResourceDef resource, ThingDef stuff,
            HashSet<ThingDef> path, int depth, out bool complete, ThingDef product = null, bool unknownStuff = false)
        {
            complete = true;
            float result = 0f;
            if (recipe?.ingredients == null || resource == null) return 0f;
            var key = (recipe, stuff, product, unknownStuff);
            Cache cache = For(resource);
            if (cache.recipeUnits.TryGetValue(key, out float cached)) return cached;
            foreach (IngredientCount ingredient in recipe.ingredients)
            {
                float minimum = float.PositiveInfinity;
                foreach (ThingDef allowed in ingredient.filter.AllowedThingDefs)
                {
                    if (!ingredient.IsFixedIngredient && recipe.fixedIngredientFilter != null && !recipe.fixedIngredientFilter.Allows(allowed)) continue;
                    bool stuffSlot = product != null && IsStuffIngredient(product, ingredient) && IsStuffSlot(product, allowed);
                    if (stuffSlot && stuff != null && stuff != allowed) continue;
                    float perUnit = recipe.IngredientValueGetter.ValuePerUnitOf(allowed);
                    float value;
                    if (stuffSlot && unknownStuff) value = Mathf.Max(1f, resource.ValueFor(allowed, true));
                    else
                    {
                        value = Units(allowed, null, allowed.MadeFromStuff, resource, path, depth + 1, out bool done);
                        complete &= done;
                    }
                    minimum = Mathf.Min(minimum, perUnit > 0f ? value * ingredient.GetBaseCount() / perUnit : 0f);
                }
                if (!float.IsPositiveInfinity(minimum)) result += minimum;
            }
            if (complete) cache.recipeUnits[key] = result;
            return result;
        }
        public static bool CorpseAllowed(Corpse corpse, QueenIngestibleResourceDef resource)
        {
            // Rot state is live and checked before consulting race validity.
            if (corpse?.InnerPawn == null || resource == null || resource.corpseRotStages?.Contains(corpse.GetRotStage()) != true) return false;
            return CorpseRaceAllowed(corpse.InnerPawn.def, resource) && LiveFilterAllows(corpse, resource);
        }
        private static bool LiveFilterAllows(Thing input, QueenIngestibleResourceDef resource)
        {
            // Cache only def/stuff structure; hit-point, quality and special filters stay live.
            if (resource.inputFilter?.Allows(input.def) == true && !resource.inputFilter.Allows(input)) return false;
            if (input is Corpse && resource.corpseFilter?.Allows(input.def) == true && !resource.corpseFilter.Allows(input)) return false;
            return true;
        }
        public static bool CorpseRaceAllowed(ThingDef race, QueenIngestibleResourceDef resource)
        {
            if (race?.race == null || resource == null) return false;
            Cache cache = For(resource);
            if (!cache.corpses.TryGetValue(race, out bool allowed))
            {
                ThingDef corpse = race.race.corpseDef;
                allowed = resource.corpseFilter?.Allows(race) == true || (corpse != null && resource.corpseFilter?.Allows(corpse) == true)
                    || resource.ValueFor(race, false) > 0f || (corpse != null && resource.ValueFor(corpse, false) > 0f);
                cache.corpses.Add(race, allowed);
            }
            return allowed;
        }
        private static void CorpseValues(Corpse corpse, out float mass, out float nutrition)
        {
            // ButcherProducts is unsuitable for a read-only query: it can remove body parts,
            // create Things and record history. Read the same live stats without mutating the pawn.
            mass = nutrition = 0f;
            foreach (var product in LiveCorpseProducts(corpse.InnerPawn)) AddProductValue(product.def, product.count, ref mass, ref nutrition);
            if (mass <= 0f) mass = corpse.GetStatValue(StatDefOf.Mass);
            float condition = CorpseCondition(corpse);
            mass *= condition;
            nutrition *= condition;
        }
        private static IEnumerable<(ThingDef def, float count)> LiveCorpseProducts(Pawn pawn)
        {
            if (pawn.RaceProps.meatDef != null) yield return (pawn.RaceProps.meatDef, pawn.GetStatValue(StatDefOf.MeatAmount));
            if (pawn.RaceProps.leatherDef != null) yield return (pawn.RaceProps.leatherDef, pawn.GetStatValue(StatDefOf.LeatherAmount));
            if (pawn.def.butcherProducts != null)
                foreach (ThingDefCountClass product in pawn.def.butcherProducts)
                    yield return (product.thingDef, product.count * (Find.Storyteller?.difficulty?.butcherYieldFactor ?? 1f));
            BodyPartToDrop bodyPart = pawn.RaceProps.Humanlike ? null : pawn.ageTracker?.CurKindLifeStage?.butcherBodyPart;
            if (bodyPart != null && (pawn.gender == Gender.None || (pawn.gender == Gender.Male ? bodyPart.allowMale : bodyPart.allowFemale)))
                foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
                    if (part.IsInGroup(bodyPart.bodyPartGroup)) yield return (bodyPart.thing ?? part.def.spawnThingOnRemoved, 1f);
        }
        private static float CorpseCondition(Corpse corpse)
        {
            float hp = corpse.MaxHitPoints > 0 ? Mathf.Clamp01((float)corpse.HitPoints / corpse.MaxHitPoints) : 1f;
            CompRottable rot = corpse.TryGetComp<CompRottable>();
            float remaining = corpse.GetRotStage() == RotStage.Dessicated ? 0.25f
                : rot == null ? 1f : Mathf.Clamp01(1f - rot.RotProgress / Mathf.Max(1f, rot.PropsRot.TicksToDessicated));
            return hp * remaining;
        }
        private static void AddProductValue(ThingDef def, float count, ref float mass, ref float nutrition)
        {
            if (def == null || count <= 0f) return;
            mass += def.GetStatValueAbstract(StatDefOf.Mass) * count;
            nutrition += def.GetStatValueAbstract(StatDefOf.Nutrition) * count;
        }
        public static float TemperatureYield(QueenIngestibleResourceDef resource, IntVec3 cell, Map map)
        {
            ResourceTemperature temperature = resource?.temperature;
            if (temperature?.enabled != true || temperature.celsiusPerInterval == 0f || map == null || !cell.InBounds(map)) return 0f;
            return Mathf.Max(0f, (cell.GetTemperature(map) - temperature.restingCelsius) / temperature.celsiusPerInterval) * temperature.yieldPerInterval;
        }
        private static float MassFactor(QueenIngestibleResourceDef resource)
            => resource.massFactor * (resource.applyJellySettings ? XMTSettings.JellyMassEfficiency / 0.025f : 1f);
        private static float NutritionFactor(QueenIngestibleResourceDef resource)
            => resource.nutritionToMass * (resource.applyJellySettings ? XMTSettings.JellyNutritionEfficiency / 0.5f : 1f);
        private static float EquivalentMass(float mass, float nutrition, QueenIngestibleResourceDef resource)
            => Mathf.Max(mass, nutrition * NutritionFactor(resource) / Mathf.Max(0.000001f, MassFactor(resource)));

        public static float ValueFor(Thing thing, QueenIngestibleResourceDef resource, int count = -1)
        {
            if (thing == null || thing.Destroyed || resource == null) return 0f;
            if (!LiveFilterAllows(thing, resource)) return 0f;
            int quantity = count < 0 ? thing.stackCount : Mathf.Min(count, thing.stackCount);
            if (quantity <= 0) return 0f;
            if (thing is Corpse corpse)
            {
                if (!CorpseAllowed(corpse, resource)) return 0f;
                if (resource.massBased)
                {
                    CorpseValues(corpse, out float mass, out float nutrition);
                    return EquivalentMass(mass, nutrition, resource) * MassFactor(resource);
                }
                float direct = Mathf.Max(resource.ValueFor(corpse.def, false), resource.ValueFor(corpse.InnerPawn.def, false));
                float value = 0f;
                foreach (var product in LiveCorpseProducts(corpse.InnerPawn)) value += DefinitionUnits(product.def, resource) * product.count;
                RecipeDef recipe = corpse.InnerPawn.RaceProps.IsMechanoid ? QueenMechGestationUtility.RecipeForProduct(corpse.InnerPawn.def, ignoreResearch: true) : null;
                value = resource.allowDerivedIngredientValue ? Mathf.Max(value, RecipeUnits(recipe, resource)) * resource.derivedIngredientFactor : 0f;
                value = resource.allowDirectAndDerivedValue ? direct + value : Mathf.Max(direct, value);
                return value * CorpseCondition(corpse);
            }
            if (thing is Pawn || (thing.def.category != ThingCategory.Item && !(thing is Plant))) return 0f;
            if (!resource.massBased)
            {
                float direct = resource.ValueFor(thing.def, false);
                float derived = resource.allowDerivedIngredientValue && (direct <= 0f || resource.allowDirectAndDerivedValue)
                    ? DefinitionUnits(thing.def, resource, thing.Stuff) * resource.derivedIngredientFactor : 0f;
                return (resource.allowDirectAndDerivedValue ? direct + derived : Mathf.Max(direct, derived)) * quantity;
            }
            float fraction = EligibleFraction(thing.def, thing.Stuff, resource);
            // Native Nutrition already applies PlantGrowthNutritionFactor. Plant biomass uses
            // that live nutrition once, rather than a default mass or a second growth factor.
            float converted = EquivalentMass(thing is Plant ? 0f : thing.GetStatValue(StatDefOf.Mass),
                thing.GetStatValue(StatDefOf.Nutrition), resource) * fraction * quantity;
            return converted * MassFactor(resource);
        }

        public sealed class Allocation
        {
            public QueenIngestibleResourceDef resource;
            public float mass;
            public int ItemCount => CountFor(1f);
            public int CountFor(float factor) => resource.product == null || mass <= 0f || factor <= 0f ? 0
                : Mathf.Max(1, Mathf.FloorToInt(mass * factor / Mathf.Max(0.000001f, resource.product.GetStatValueAbstract(StatDefOf.Mass))));
        }
        // Immediate conversion snapshot, never serialized or cached. The caller uses the same
        // allocations for its preview and commit; worn items are paid for and removed once.
        public sealed class ConversionPlan
        {
            internal Thing input;
            internal float quantity;
            internal Pawn wearer;
            internal readonly List<(Apparel apparel, int count)> worn = new List<(Apparel apparel, int count)>();
            internal bool consumed;
            public List<Allocation> Allocations { get; internal set; } = new List<Allocation>();
            public bool TryConsume()
            {
                if (consumed || input == null || input.Destroyed || quantity <= 0f || Allocations.Count == 0) return false;
                foreach (var item in worn)
                    if (item.apparel.Destroyed || item.apparel.Wearer != wearer || item.apparel.stackCount != item.count) return false;
                consumed = true;
                // Destroy notifies the real apparel ThingOwner, which removes worn apparel
                // and sends the normal tracker notifications before the corpse can disappear.
                foreach (var item in worn)
                    if (!Consume(item.apparel, item.count) || !item.apparel.Destroyed) return false;
                return Consume(input, quantity, preserveCorpseRemainder: true);
            }
        }
        public static ConversionPlan PlanConversion(Thing input, List<ResourceConversionEntry> entries, Thing consumer, float fraction = 1f)
        {
            var plan = new ConversionPlan { input = input, quantity = ConversionQuantity(input, fraction) };
            plan.Allocations = Allocate(input, entries, consumer, plan.quantity);
            // Apparel cannot confer permission to convert an otherwise ineligible corpse.
            if (plan.Allocations.Count == 0 || !(input is Corpse corpse) || corpse.InnerPawn?.apparel == null) return plan;
            plan.wearer = corpse.InnerPawn;
            foreach (Apparel apparel in plan.wearer.apparel.WornApparel)
            {
                if (apparel.Destroyed || !apparel.def.destroyable) continue;
                List<Allocation> apparelOutput = Allocate(apparel, entries, consumer);
                if (apparelOutput.Count == 0) continue;
                plan.worn.Add((apparel, apparel.stackCount));
                plan.Allocations.AddRange(apparelOutput);
            }
            plan.Allocations = plan.Allocations.GroupBy(a => a.resource)
                .Select(g => new Allocation { resource = g.Key, mass = g.Sum(a => a.mass) }).ToList();
            return plan;
        }
        private static List<ResourceConversionEntry> Active(List<ResourceConversionEntry> entries, Thing consumer)
            => entries?.Where(e => e?.resourceDef?.product != null && e.Weight > 0f && Available(e.resourceDef, consumer))
                .GroupBy(e => e.resourceDef).Select(g => new ResourceConversionEntry { resourceDef = g.Key, allocationWeight = g.Sum(e => e.Weight) }).ToList()
                ?? new List<ResourceConversionEntry>();
        public static bool CanConvertDefinition(ThingDef def, List<ResourceConversionEntry> entries, Thing consumer)
        {
            if (def == null) return false;
            var active = Active(entries, consumer);
            if (active.Any(e => e.resourceDef.product == def)) return false;
            if (def.IsCorpse)
                return active.Any(e => CorpseRaceAllowed(def.ingestible?.sourceDef, e.resourceDef)
                    || e.resourceDef.corpseFilter?.Allows(def) == true || e.resourceDef.ValueFor(def, false) > 0f);
            active = active.Where(e => Possible(def, null, false, e.resourceDef, new HashSet<ThingDef>(), 0, out _)).ToList();
            return active.Count > 0 && Fraction(def, null, false, active.Select(e => e.resourceDef).ToList(), new HashSet<ThingDef>(), 0, out _) > 0f;
        }

        public static bool CanConvertPlant(Plant plant, List<ResourceConversionEntry> entries, Thing consumer)
            => plant != null && !plant.Destroyed && CanConvertDefinition(plant.def, entries, consumer)
                && Active(entries, consumer).Any(entry => LiveFilterAllows(plant, entry.resourceDef)
                    && Possible(plant.def, null, false, entry.resourceDef, new HashSet<ThingDef>(), 0, out _));
        public static List<Allocation> Allocate(Thing input, List<ResourceConversionEntry> entries, Thing consumer, float quantity = -1f)
        {
            var active = Active(entries, consumer);
            if (input == null || input.Destroyed || input is Pawn || (input.def.category != ThingCategory.Item && !(input is Plant)) || active.Count == 0) return new List<Allocation>();
            if (active.Any(e => e.resourceDef.product == input.def)) return new List<Allocation>();
            active = active.Where(e => input is Corpse corpse ? CorpseAllowed(corpse, e.resourceDef)
                : LiveFilterAllows(input, e.resourceDef) && Possible(input.def, input.Stuff, false, e.resourceDef, new HashSet<ThingDef>(), 0, out _)).ToList();
            if (active.Count == 0) return new List<Allocation>();
            float mass, nutrition;
            float fraction = 1f;
            if (input is Corpse body) CorpseValues(body, out mass, out nutrition);
            else
            {
                mass = input is Plant ? 0f : input.GetStatValue(StatDefOf.Mass);
                nutrition = input.GetStatValue(StatDefOf.Nutrition);
                fraction = Fraction(input.def, input.Stuff, false, active.Select(e => e.resourceDef).ToList(), new HashSet<ThingDef>(), 0, out _);
            }
            float amount = quantity < 0f ? input.stackCount : Mathf.Min(input.stackCount, quantity);
            float budget = active.Max(e => EquivalentMass(mass, nutrition, e.resourceDef)) * fraction * amount;
            return Divide(budget, active, input.PositionHeld, input.MapHeld, true);
        }
        public static List<Allocation> AllocateDefinition(ThingDef input, int count, List<ResourceConversionEntry> entries, Thing consumer)
        {
            var active = Active(entries, consumer).Where(e => Possible(input, null, false, e.resourceDef, new HashSet<ThingDef>(), 0, out _)).ToList();
            if (active.Count == 0 || count <= 0) return new List<Allocation>();
            float fraction = Fraction(input, null, false, active.Select(e => e.resourceDef).ToList(), new HashSet<ThingDef>(), 0, out _);
            float budget = active.Max(e => EquivalentMass(input.category == ThingCategory.Plant ? 0f : input.GetStatValueAbstract(StatDefOf.Mass),
                input.GetStatValueAbstract(StatDefOf.Nutrition), e.resourceDef)) * fraction * count;
            return Divide(budget, active, IntVec3.Invalid, null, true);
        }
        private static List<Allocation> Divide(float budget, List<ResourceConversionEntry> active, IntVec3 cell, Map map, bool material)
        {
            var result = new List<Allocation>();
            float weights = active.Sum(e => e.Weight);
            if (weights <= 0f) return result;
            foreach (ResourceConversionEntry entry in active)
            {
                float value = budget * entry.Weight / weights;
                if (material) value *= 1f + TemperatureYield(entry.resourceDef, cell, map);
                value *= MassFactor(entry.resourceDef);
                if (value > 0f && !float.IsNaN(value) && !float.IsInfinity(value)) result.Add(new Allocation { resource = entry.resourceDef, mass = value });
            }
            return result;
        }
        public sealed class TerrainEvaluation
        {
            public readonly Dictionary<QueenIngestibleResourceDef, float> values = new Dictionary<QueenIngestibleResourceDef, float>();
            public TerrainDef original;
            public ResourceTerrainDef depletion;
            public bool conflict;
        }
        public static TerrainEvaluation EvaluateTerrain(IEnumerable<QueenIngestibleResourceDef> resources, IntVec3 cell, Map map, bool allowTerrain = true)
        {
            if (map == null || !cell.InBounds(map)) return new TerrainEvaluation();
            return EvaluateTerrain(resources, cell.GetTerrain(map), r => TemperatureYield(r, cell, map), allowTerrain);
        }
        public static TerrainEvaluation EvaluateTerrain(IEnumerable<QueenIngestibleResourceDef> resources, TerrainDef terrain,
            Func<QueenIngestibleResourceDef, float> temperatureYield = null, bool allowTerrain = true)
        {
            var result = new TerrainEvaluation { original = terrain };
            if (terrain == null) return result;
            foreach (QueenIngestibleResourceDef resource in resources.Distinct())
            {
                if (resource == null) continue;
                float value = 0f;
                if (allowTerrain && resource.terrainProfiles != null)
                    foreach (ResourceTerrainDef profile in resource.terrainProfiles)
                    {
                        var key = (profile, result.original);
                        if (!terrains.TryGetValue(key, out bool matches)) terrains[key] = matches = profile.Matches(result.original);
                        if (!matches) continue;
                        float fertility = profile.useDriesToFertility && result.original.driesTo != null ? result.original.driesTo.fertility : result.original.fertility;
                        float yield = profile.yield * resource.terrainYieldFactor * (profile.yieldFromFertilityLoss ? Mathf.Max(0f, fertility - (profile.depletedTerrain?.fertility ?? 0f)) : 1f);
                        if (yield <= 0f) continue;
                        value += yield;
                        if (profile.depletedTerrain == null && !profile.removeTopLayer) continue;
                        if (result.depletion == null || profile.depletionPriority > result.depletion.depletionPriority)
                        { result.depletion = profile; result.conflict = false; }
                        else if (profile.depletionPriority == result.depletion.depletionPriority
                            && (profile.depletedTerrain != result.depletion.depletedTerrain || profile.removeTopLayer != result.depletion.removeTopLayer))
                            result.conflict = true;
                    }
                float temperature = temperatureYield?.Invoke(resource) ?? 0f;
                // No material: use one as the temperature basis. No free unit at rest.
                value = value > 0f ? value * (1f + temperature) : temperature;
                if (value > 0f) result.values[resource] = value * (resource.massBased ? MassFactor(resource) : 1f);
            }
            if (result.conflict)
            {
                string key = result.original.defName;
                if (reportedTerrainConflicts.Add(key)) Log.Error("[XMT] Resource terrain profiles have conflicting depletion at equal priority on " + key);
                result.values.Clear();
            }
            return result;
        }
        public static IEnumerable<string> TerrainConfigErrors(IEnumerable<ResourceTerrainDef> profiles)
        {
            List<ResourceTerrainDef> list = profiles?.Where(p => p != null).Distinct().ToList() ?? new List<ResourceTerrainDef>();
            for (int i = 0; i < list.Count; i++)
                for (int j = i + 1; j < list.Count; j++)
                {
                    ResourceTerrainDef a = list[i], b = list[j];
                    if (a.depletionPriority != b.depletionPriority || (a.depletedTerrain == b.depletedTerrain && a.removeTopLayer == b.removeTopLayer)) continue;
                    if (a.depletedTerrain == null && !a.removeTopLayer || b.depletedTerrain == null && !b.removeTopLayer) continue;
                    if (DefDatabase<TerrainDef>.AllDefsListForReading.Any(t => a.Matches(t) && b.Matches(t)))
                        yield return "Conflicting resource terrain depletion at equal priority: " + a.defName + " and " + b.defName;
                }
        }
        public static bool ApplyTerrain(TerrainEvaluation evaluation, IntVec3 cell, Map map)
        {
            if (evaluation == null || evaluation.conflict || evaluation.values.Count == 0 || map == null || !cell.InBounds(map)
                || cell.GetTerrain(map) != evaluation.original) return false;
            if (evaluation.depletion?.removeTopLayer == true) map.terrainGrid.RemoveTopLayer(cell, false);
            else if (evaluation.depletion?.depletedTerrain != null) map.terrainGrid.SetTerrain(cell, evaluation.depletion.depletedTerrain);
            return true;
        }
        public static List<Allocation> AllocateTerrain(List<ResourceConversionEntry> entries, Thing consumer, IntVec3 cell, Map map, out TerrainEvaluation evaluation)
        {
            List<ResourceConversionEntry> active = Active(entries, consumer);
            evaluation = EvaluateTerrain(active.Select(e => e.resourceDef), cell, map);
            TerrainEvaluation evaluated = evaluation;
            var eligible = active.Where(e => evaluated.values.ContainsKey(e.resourceDef)).ToList();
            // Matching profiles inside a resource stack. Outputs sharing that terrain input
            // still divide one budget, just as they do for a physical input item.
            if (eligible.Count == 0) return new List<Allocation>();
            float budget = eligible.Max(e => evaluated.values[e.resourceDef] / Mathf.Max(0.000001f, MassFactor(e.resourceDef)));
            return Divide(budget, eligible, cell, map, false);
        }
        // The quantity used to value an item is exactly the quantity committed here.
        public static float ConversionQuantity(Thing thing, float fraction)
        {
            if (thing == null || fraction <= 0f) return 0f;
            fraction = Mathf.Clamp01(fraction);
            if (thing is Corpse corpse)
            {
                if (corpse.TryGetComp<CompRottable>() != null && !corpse.IsDessicated()) return fraction;
                // Non-rotting/dessicated corpses use HP as their remaining material. Quantize
                // before valuation so partial conversion cannot pay for a different amount.
                return corpse.HitPoints > 0 ? Mathf.Clamp01((float)Mathf.CeilToInt(corpse.HitPoints * fraction) / corpse.HitPoints) : 0f;
            }
            return Mathf.Clamp(Mathf.FloorToInt(thing.stackCount * fraction), 1, thing.stackCount);
        }
        public static bool Consume(Thing thing, float quantity, bool preserveCorpseRemainder = false)
        {
            if (thing == null || thing.Destroyed || quantity <= 0f) return false;
            if (thing is Corpse corpse && preserveCorpseRemainder)
            {
                CompRottable rot = corpse.TryGetComp<CompRottable>();
                if (rot != null && !corpse.IsDessicated())
                {
                    rot.RotProgress += rot.PropsRot.TicksToDessicated * Mathf.Clamp01(quantity);
                    if (corpse.IsDessicated() && corpse.MapHeld != null) FeralJobUtility.ClearFeralJobReservationsForTarget(corpse.MapHeld, corpse);
                    return true;
                }
                int consumedHP = Mathf.Max(1, Mathf.RoundToInt(corpse.HitPoints * Mathf.Clamp01(quantity)));
                if (consumedHP > 0 && consumedHP < corpse.HitPoints)
                {
                    corpse.HitPoints -= consumedHP;
                    return true;
                }
            }
            int count = Mathf.Min(thing.stackCount, Mathf.Max(1, Mathf.FloorToInt(quantity)));
            if ((count >= thing.stackCount || thing is Plant) && thing.MapHeld != null)
                FeralJobUtility.ClearFeralJobReservationsForTarget(thing.MapHeld, thing);
            if (thing is Plant plant) plant.Kill();
            else if (count >= thing.stackCount) thing.Destroy();
            else thing.SplitOff(count).Destroy();
            return true;
        }
    }
}
