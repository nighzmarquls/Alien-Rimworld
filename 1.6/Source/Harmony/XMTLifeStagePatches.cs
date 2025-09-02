using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    internal class XMTLifeStagePatches
    {

        [HarmonyPatch(typeof(LifeStageWorker), nameof(LifeStageWorker_HumanlikeAdult.Notify_LifeStageStarted))]
        public static class Patch_LifeStageWorker_Notify_LifeStageStarted
        {
            [HarmonyPostfix]
            public static void Postfix(Pawn pawn, LifeStageDef previousLifeStage)
            {
                bool childWasXeno = XMTUtility.IsXenomorph(pawn);

                if (childWasXeno)
                {
                    return;
                }

                FactionManager manager = Find.FactionManager;

                if(manager == null)
                {
                    return;
                }

                if(pawn.Faction == null)
                {
                    return;
                }

                if(Faction.OfPlayerSilentFail == pawn.Faction && XMTUtility.PlayerXenosOnMap(pawn.MapHeld))
                {
                    CompPawnInfo info = pawn.Info();

                    if (info != null)
                    {
                        info.GainObsession(0.25f);
                    }
                }
            }
        }
    }
}
