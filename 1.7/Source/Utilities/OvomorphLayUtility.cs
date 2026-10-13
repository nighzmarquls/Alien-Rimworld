using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace Xenomorphtype
{
    public static class OvomorphLayUtility
    {
        public static float GetAdjustedFoodCost(Pawn layer, float baseFoodCost, bool useCostModifiers = true)
        {
            float adjustedCost = baseFoodCost;

            if (useCostModifiers)
            {
                adjustedCost *= XMT_OvomorphLayCostModifierListDef.CostFactorFor(layer);
            }

            if (layer?.needs?.food != null)
            {
                adjustedCost = Mathf.Min(layer.needs.food.MaxLevel, adjustedCost);
            }

            return adjustedCost;
        }

        public static bool CanAffordFoodCost(Pawn layer, float baseFoodCost, bool useCostModifiers = true)
        {
            float foodCost = GetAdjustedFoodCost(layer, baseFoodCost, useCostModifiers);
            return layer?.needs?.food == null || layer.needs.food.CurLevel > foodCost;
        }

        public static bool CanAffordResourceCost(Pawn layer, QueenIngestibleResourceDef resourceDef, float resourceCost)
        {
            if (resourceDef == null || resourceCost <= 0f)
            {
                return true;
            }

            CompQueenAssimilation assimilation = layer?.GetComp<CompQueenAssimilation>();
            return assimilation != null && assimilation.ResourceUnlocked(resourceDef) && assimilation.GetResourceAmount(resourceDef) >= resourceCost;
        }

        public static bool CanLayAt(Pawn layer, IntVec3 loc, ThingDef ovomorphDef, out string reason)
        {
            return CanLayAt(layer, loc, layer, ovomorphDef, out reason);
        }

        public static bool CanLayAt(Pawn layer, IntVec3 loc, Thing positionSource, ThingDef ovomorphDef, out string reason)
        {
            return CanLayAt(layer, loc, positionSource, ovomorphDef, out reason, requireReachability: true, allowDeadLayer: false);
        }

        public static bool CanLayAt(Pawn layer, IntVec3 loc, Thing positionSource, ThingDef ovomorphDef, out string reason, bool requireReachability, bool allowDeadLayer)
        {
            reason = null;

            if (layer == null || layer.Destroyed || (!allowDeadLayer && layer.Dead))
            {
                reason = "XMT_MessageMustDesignateAlive".Translate();
                return false;
            }

            positionSource ??= layer;
            if (positionSource.Destroyed || !positionSource.Spawned || positionSource.MapHeld == null)
            {
                reason = "CannotGenericWorkCustom".Translate("NotSpawned".Translate());
                return false;
            }

            Map map = positionSource.MapHeld;
            if (!loc.InBounds(map))
            {
                reason = "OutOfBounds".Translate();
                return false;
            }

            if (ovomorphDef == null)
            {
                reason = "NoThing".Translate();
                return false;
            }

            if (loc.GetEdifice(map) != null)
            {
                reason = "SpaceAlreadyOccupied".Translate();
                return false;
            }

            if (requireReachability && !map.reachability.CanReach(positionSource.PositionHeld, loc, PathEndMode.Touch, TraverseMode.PassDoors, Danger.Deadly))
            {
                reason = "NoPath".Translate();
                return false;
            }

            return true;
        }

        public static Thing TryLayOvomorph(Pawn layer, IntVec3 loc, ThingDef ovomorphDef, float baseFoodCost, bool useCostModifiers = true, float initialProgress = 0f, QueenIngestibleResourceDef resourceDef = null, KnowledgeProfileDef knowledgeProfile = null)
        {
            float foodCost = GetAdjustedFoodCost(layer, baseFoodCost, useCostModifiers);
            float resourceCost = 0;
            return TryLayOvomorphWithCost(layer, loc, ovomorphDef, foodCost, initialProgress, resourceDef, resourceCost, knowledgeProfile);
        }

        public static Thing TryLayOvomorphWithCost(Pawn layer, IntVec3 loc, ThingDef ovomorphDef, float foodCost, float initialProgress = 0f, QueenIngestibleResourceDef resourceDef = null, float resourceCost = 0, KnowledgeProfileDef knowledgeProfile = null)
        {
            return TryLayOvomorphWithCost(layer, loc, layer, ovomorphDef, foodCost, initialProgress, resourceDef, resourceCost, knowledgeProfile);
        }

        public static Thing TryLayOvomorphWithCost(Pawn layer, IntVec3 loc, Thing positionSource, ThingDef ovomorphDef, float foodCost, float initialProgress = 0f, QueenIngestibleResourceDef resourceDef = null, float resourceCost = 0, KnowledgeProfileDef knowledgeProfile = null, bool chargeFood = true, bool playSound = true, bool makeFilth = true, bool recordEvent = true, bool witness = true, bool requireReachability = true, bool allowDeadLayer = false)
        {
            if (!CanLayAt(layer, loc, positionSource, ovomorphDef, out string _, requireReachability, allowDeadLayer))
            {
                return null;
            }

            if (chargeFood && layer.needs?.food != null && layer.needs.food.CurLevel <= foodCost)
            {
                return null;
            }

            if (!CanAffordResourceCost(layer, resourceDef, resourceCost))
            {
                return null;
            }

            Map map = positionSource.MapHeld;
            Thing laidThing = GenSpawn.Spawn(ovomorphDef, loc, map, WipeMode.Vanish);

            if (resourceDef != null && resourceCost > 0f)
            {
                CompQueenAssimilation assimilation = layer.GetComp<CompQueenAssimilation>();
                if (assimilation == null || !assimilation.TrySpendResource(resourceDef, resourceCost))
                {
                    laidThing.Destroy();
                    return null;
                }
            }

            if (laidThing is Ovomorph ovomorph)
            {
                ovomorph.LayEgg(layer, layer);
                ovomorph.ForceProgress(initialProgress);
                layer.GetComp<CompOvomorphLayer>()?.TryApplyBroodLineage(ovomorph, ovomorphDef);
            }

            if (chargeFood && layer.needs?.food != null)
            {
                layer.needs.food.CurLevel -= foodCost;
            }

            if (witness)
            {
                XMTUtility.WitnessOvomorph(loc, map, 0.1f, knowledgeProfile: knowledgeProfile);
            }
            if (recordEvent)
            {
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(XenoPreceptDefOf.XMT_Ovomorph_Laid, layer.Named(HistoryEventArgsNames.Doer)));
            }
            if (playSound)
            {
                SoundDefOf.CocoonDestroyed.PlayOneShot(new TargetInfo(loc, map));
            }
            if (makeFilth)
            {
                FilthMaker.TryMakeFilth(loc, map, InternalDefOf.Starbeast_Filth_Resin, count: 8);
            }

            return laidThing;
        }
    }
}
