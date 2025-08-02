

using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    internal class XMTEnderPatch
    {
        [HarmonyPatch(typeof(ChoiceLetter_GameEnded), "Choices", MethodType.Getter)]
        static class Patch_ChoiceLetter_GameEnded_Choices
        {

            [HarmonyPostfix]
            static void Postfix(ref IEnumerable<DiaOption> __result)
            {
                bool FoundXeno = false;
                Map playermap = null;
                foreach (Map playerHomeMap in Current.Game.PlayerHomeMaps)
                {
                    if (playerHomeMap.Tile.Layer.IsRootSurface)
                    {
                        if (HiveUtility.XenosOnMap(playerHomeMap))
                        {
                            FoundXeno = true;
                            playermap = playerHomeMap;
                            break;
                        }
                    }

                }

                if (FoundXeno)
                {
                    DiaOption XenoOption =  new DiaOption("XMT_GameOverJoinXenomorphs".Translate())
                    {
                        action = delegate
                        {
                            HiveUtility.PlayerJoinXenomorphs(playermap);
                        },
                        resolveTree = true
                    };
                    List<DiaOption> diaOptions = __result.ToList();
                    diaOptions.Insert(diaOptions.Count - 2, XenoOption);
                    __result = diaOptions; 
                }
            }
        }
    }
}
