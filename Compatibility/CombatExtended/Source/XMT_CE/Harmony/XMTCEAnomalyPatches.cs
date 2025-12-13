

using CombatExtended;
using HarmonyLib;
using Verse;
using Xenomorphtype;

namespace XMT_CE
{
    internal class XMTCEAnomalyPatches
    {

        [HarmonyPatch(typeof(ITab_Inventory), "IsVisible", MethodType.Getter)]
        static class Patch_ITab_Inventory_IsVisible
        {

            [HarmonyPostfix]
            static void Postfix(ITab_Inventory __instance, ref bool __result)
            {
                if (__result)
                {
                    return;
                }

                Thing selected = Find.Selector.SingleSelectedThing;

                Pawn selectedPawn = selected as Pawn;

                if(selectedPawn == null)
                {
                    if(selected is Corpse corpse)
                    {
                        selectedPawn = corpse.InnerPawn;
                    }
                }

                if (selectedPawn == null)
                {
                    return;
                }

                if(!XMTUtility.IsXenomorph(selectedPawn))
                {
                    return;
                }

                if (selectedPawn.apparel == null)
                {
                    if (selectedPawn.equipment == null)
                    {
                        return;
                    }
                }

                __result = true;
            }
        }
    }
}
