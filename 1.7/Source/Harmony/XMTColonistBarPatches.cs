using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    public static class HiveColonistBarUtility
    {
        private static readonly Predicate<Pawn> hidden = ShouldHide;
        public static bool ShouldHide(Pawn pawn)
        {
            if (pawn == null || pawn.Spawned)
                return false;
            for (IThingHolder holder = pawn.ParentHolder; holder != null; holder = holder.ParentHolder)
            {
                // The actual throne occupant remains an independently controlled queen.
                if (holder is EggSack throne && throne.ContainedThing == pawn)
                    return false;
                if (holder is HibernationCocoon || holder is AmbushTrap || holder is CompPawnPreservation)
                    return true;
            }

            return false;
        }

        public static void Filter(List<Pawn> pawns) => pawns.RemoveAll(hidden);
        public static void FilterAndSort(List<Pawn> pawns)
        {
            Filter(pawns);
            PlayerPawnsDisplayOrderUtility.Sort(pawns);
        }

        public static void NotifyContentsChanged()
        {
            if (Find.UIRoot is UIRoot_Play ui)
                ui.mapUI?.colonistBar?.MarkColonistsDirty();
        }
    }

    [HarmonyPatch(typeof(ColonistBar), "CheckRecacheEntries")]
    public static class XMTColonistBarPatches
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo sort = AccessTools.Method(typeof(PlayerPawnsDisplayOrderUtility), nameof(PlayerPawnsDisplayOrderUtility.Sort));
            MethodInfo filteredSort = AccessTools.Method(typeof(HiveColonistBarUtility), nameof(HiveColonistBarUtility.FilterAndSort));
            // Both map and caravan candidates pass through this call before entries/layout are built.
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(sort))
                    instruction.operand = filteredSort;
                yield return instruction;
            }
        }
    }
}
