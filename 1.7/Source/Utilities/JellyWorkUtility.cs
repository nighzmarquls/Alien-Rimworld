using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal static class JellyWorkUtility
    {
        internal const int FailedSearchRetryTicks = 600;

        internal static void Trace(Pawn pawn, string reason)
        {
            if (XMTSettings.LogJobGiver)
                Log.Message("[XMT][JobGiver][Jelly] " + pawn + ": " + reason);
        }

        internal static bool CookingEnabled(Pawn pawn)
            => pawn?.workSettings != null && !pawn.WorkTypeIsDisabled(XenoWorkDefOf.Cooking)
                && pawn.workSettings.WorkIsActive(XenoWorkDefOf.Cooking);

        internal static bool IsStoredInput(Pawn pawn, Thing item)
        {
            if (item == null || !item.Spawned || item.Map != pawn.Map) return false;
            SlotGroup storage = pawn.Map.haulDestinationManager.SlotGroupAt(item.Position);
            return storage != null
                && (storage.parent is Zone_Stockpile || storage.parent is Thing building && building.Faction == pawn.Faction)
                && storage.Settings.AllowedToAccept(item);
        }

        private static bool UsesDesignations(CompJellyMaker maker)
            => XMTUtility.QueenIsPlayer() && maker.GetJellyProduct() == InternalDefOf.Starbeast_Jelly;

        private static bool DesignationEligible(Pawn pawn, CompJellyMaker maker, IntVec3 cell, bool requireLightLevel)
        {
            if (!FeralJobUtility.IsPlaceAvailableForJobBy(pawn, cell)
                || requireLightLevel && !CompJellyMaker.IsProperLightLevelForFeralJelly(cell, pawn.Map)
                || !maker.CanConvertCell(cell)) return false;
            Thing ingredient = maker.FindIngredient(cell, pawn.Map);
            return ingredient == null || FeralJobUtility.IsThingAvailableForJobBy(pawn, ingredient);
        }

        // Intent only: never run path queries or a region traversal from the boredom gate.
        internal static bool HasDeliberateWork(Pawn pawn, CompJellyMaker maker)
        {
            if (pawn?.Spawned != true || pawn.Faction?.IsPlayer != true || maker == null) return false;
            if (!CookingEnabled(pawn))
            {
                Trace(pawn, "boredom override blocked: Cooking disabled or unassigned.");
                return false;
            }
            if (UsesDesignations(maker))
                foreach (Designation designation in pawn.Map.designationManager.SpawnedDesignationsOfDef(XenoWorkDefOf.XMT_MakeJelly))
                    if (DesignationEligible(pawn, maker, designation.target.Cell, false))
                    {
                        Trace(pawn, "boredom override: eligible jelly designation.");
                        return true;
                    }
            return HasStoredInput(pawn, maker);
        }

        internal static bool HasStoredInput(Pawn pawn, CompJellyMaker maker)
        {
            if (pawn?.Spawned != true || pawn.Faction?.IsPlayer != true || maker == null || !CookingEnabled(pawn)) return false;
            int rejected = 0, unavailable = 0;
            foreach (ThingDef def in maker.Inputs.GetStoreSettings().filter.AllowedThingDefs)
            {
                if (def.category != ThingCategory.Item) continue;
                foreach (Thing item in pawn.Map.listerThings.ThingsOfDef(def))
                {
                    if (!IsStoredInput(pawn, item) || !maker.CanMakeIntoJelly(item)) { rejected++; continue; }
                    if (!FeralJobUtility.IsThingAvailableForJobBy(pawn, item)) { unavailable++; continue; }
                    Trace(pawn, "boredom override: eligible stored ingredient " + item + ".");
                    return true;
                }
            }
            Trace(pawn, "no deliberate jelly work: no eligible designation/stored input; storage/filter/conversion rejects="
                + rejected + ", forbidden/area/reservation rejects=" + unavailable + ".");
            return false;
        }

        internal static bool TryFindDesignatedCell(Pawn pawn, CompJellyMaker maker, bool requireLightLevel, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (!UsesDesignations(maker)) return false;
            foreach (Designation designation in pawn.Map.designationManager.SpawnedDesignationsOfDef(XenoWorkDefOf.XMT_MakeJelly))
            {
                IntVec3 candidate = designation.target.Cell;
                if (!DesignationEligible(pawn, maker, candidate, requireLightLevel)) continue;
                if (!ClimbUtility.CanReachByWalkingOrClimb(pawn, candidate, PathEndMode.Touch, Danger.Deadly))
                {
                    Trace(pawn, "skipping unreachable jelly designation at " + candidate + ".");
                    continue;
                }
                cell = candidate;
                return true;
            }
            return false;
        }
    }
}
