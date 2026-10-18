using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    [HarmonyPatch(typeof(MainTabWindow_Inspect), "CurTabs", MethodType.Getter)]
    public static class HiveInputSelectionPatch
    {
        public static void Postfix(ref IEnumerable<InspectTabBase> __result)
        {
            List<object> selection = Find.Selector.SelectedObjects;
            if (selection.Count < 2 || !XMTUtility.QueenIsPlayer() || Find.ScreenshotModeHandler.Active)
                return;

            bool resin = (selection[0] as ThingWithComps)?.GetComp<CompAcidPit>() != null;
            if (!HiveInputSelection.TryGetProcessors(selection, resin, out _))
                return;

            InspectTabBase tab = InspectTabManager.GetSharedInstance(resin ? typeof(ITab_AcidPitInputs) : typeof(ITab_HiveInputs));
            // Add only the shared filter tab; other tabs retain vanilla's selection rules.
            __result = (__result ?? Enumerable.Empty<InspectTabBase>())
                .Where(existing => existing is not ITab_HiveInputs).Append(tab).ToList();
        }
    }
}
