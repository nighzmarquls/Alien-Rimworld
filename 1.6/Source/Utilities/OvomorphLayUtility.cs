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

        public static bool CanLayAt(Pawn layer, IntVec3 loc, ThingDef ovomorphDef, out string reason)
        {
            return CanLayAt(layer, loc, layer, ovomorphDef, out reason);
        }

        public static bool CanLayAt(Pawn layer, IntVec3 loc, Thing positionSource, ThingDef ovomorphDef, out string reason)
        {
            reason = null;

            if (layer == null || layer.Destroyed || layer.Dead)
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

            if (!map.reachability.CanReach(positionSource.PositionHeld, loc, PathEndMode.Touch, TraverseMode.PassDoors, Danger.Deadly))
            {
                reason = "NoPath".Translate();
                return false;
            }

            return true;
        }

        public static Thing TryLayOvomorph(Pawn layer, IntVec3 loc, ThingDef ovomorphDef, float baseFoodCost, bool useCostModifiers = true, float initialProgress = 0f)
        {
            float foodCost = GetAdjustedFoodCost(layer, baseFoodCost, useCostModifiers);
            return TryLayOvomorphWithCost(layer, loc, ovomorphDef, foodCost, initialProgress);
        }

        public static Thing TryLayOvomorphWithCost(Pawn layer, IntVec3 loc, ThingDef ovomorphDef, float foodCost, float initialProgress = 0f)
        {
            return TryLayOvomorphWithCost(layer, loc, layer, ovomorphDef, foodCost, initialProgress);
        }

        public static Thing TryLayOvomorphWithCost(Pawn layer, IntVec3 loc, Thing positionSource, ThingDef ovomorphDef, float foodCost, float initialProgress = 0f)
        {
            if (!CanLayAt(layer, loc, positionSource, ovomorphDef, out string _))
            {
                return null;
            }

            if (layer.needs?.food != null && layer.needs.food.CurLevel <= foodCost)
            {
                return null;
            }

            Map map = positionSource.MapHeld;
            Thing laidThing = GenSpawn.Spawn(ovomorphDef, loc, map, WipeMode.Vanish);

            if (laidThing is Ovomorph ovomorph)
            {
                ovomorph.LayEgg(layer, layer);
                ovomorph.ForceProgress(initialProgress);
            }

            if (layer.needs?.food != null)
            {
                layer.needs.food.CurLevel -= foodCost;
            }

            XMTUtility.WitnessOvomorph(loc, map, 0.1f);
            Find.HistoryEventsManager.RecordEvent(new HistoryEvent(XenoPreceptDefOf.XMT_Ovomorph_Laid, layer.Named(HistoryEventArgsNames.Doer)));
            SoundDefOf.CocoonDestroyed.PlayOneShot(new TargetInfo(loc, map));
            FilthMaker.TryMakeFilth(loc, map, InternalDefOf.Starbeast_Filth_Resin, count: 8);

            return laidThing;
        }
    }
}
