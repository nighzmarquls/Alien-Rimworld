using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Verse;

namespace Xenomorphtype
{
    internal class XMTIncidentPatches
    {
        [HarmonyPatch(typeof(IncidentWorker_NeutralGroup), "SpawnPawns")]
        public static class Patch_IncidentWorker_NeutralGroup_SpawnPawns
        {
            [HarmonyPostfix]
            public static void Postfix(ref List<Pawn> __result)
            {
                List<Pawn> actualOut = new List<Pawn>();

                bool XenoformingSufficientForEmbryos = XenoformingUtility.XenoformingMeets(1);

                foreach (Pawn p in __result)
                {
                    if(XMTUtility.IsXenomorph(p))
                    {
                        if (XMTUtility.IsQueen(p)) // never let queen in caravans!
                        {
                            continue;
                        }
                        continue; // TODO: check Faction Ideo and Xenoforming Level.
                    }

                    if(XenoformingSufficientForEmbryos)
                    {
                        if (Rand.Chance(XenoformingUtility.ChanceByXenoforming(XMTSettings.WildEmbryoChance)))
                        {
                            Hediff embryo = BioUtility.MakeEmbryoPregnancy(p);
                            embryo.Severity += Rand.Range(0, 0.8f);
                            p.health.AddHediff(embryo);
                            if (XMTSettings.LogWorld)
                            {
                                Log.Message("spawning pawn with embryo " + p);
                            }

                        }
                    }

                    actualOut.Add(p);

                }
                __result = actualOut;
            }
        }
    }
}
