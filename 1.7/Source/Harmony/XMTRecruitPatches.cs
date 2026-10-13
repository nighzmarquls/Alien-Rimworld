using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class XMTRecruitPatches
    {

        [HarmonyPatch(typeof(RecruitUtility), nameof(RecruitUtility.Recruit))]
        public static class Patch_RecruitUtility_Recruit
        {
            [HarmonyPostfix]
            public static void Postfix(Pawn pawn, Faction faction)
            {
                if (faction == null)
                {
                    return;
                }

                bool wasXeno = XMTUtility.IsXenomorph(pawn);

                if (wasXeno)
                {
                    return;
                }

                if (!faction.IsPlayer)
                {
                    return;
                }

                if(pawn.Info() is CompPawnInfo info)
                {
                    float awareness = KnowledgeUtility.GetTotalEffectiveKnowledge(pawn);
                    if (KnowledgeUtility.IsObsessed(pawn) || awareness > 0)
                    {
                        ResearchUtility.ProgressCryptobioTech(10 + Mathf.FloorToInt(awareness*500), pawn);
                    }
                }
            }
        }
    }
}
