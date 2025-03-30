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
    public class HediffComp_Mutator : HediffComp
    {
        public int mutateTick = -1;
        HediffCompProperties_Mutator Props => props as HediffCompProperties_Mutator;
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            int currentTick = Find.TickManager.TicksGame;
            if (currentTick > mutateTick)
            {
                mutateTick = currentTick + Mathf.CeilToInt(Props.mutateHourInterval * 2500);
                if (Rand.Chance(Props.probability))
                {
                    if(Props.onlyOneMutation)
                    {
                        if (Props.customMutationSet != null)
                        {
                            foreach (MutationHealth mutation in Props.customMutationSet.mutations)
                            {
                                if(parent.pawn.health.hediffSet.HasHediff(mutation.horror))
                                {
                                    return;
                                }
                            }
                        }
                    }
                    BioUtility.TryMutatingPawn(ref parent.pawn, Props.customMutationSet);
                }

                

            }
        }
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref mutateTick, "mutateTick", -1);
        }
    }

    public class HediffCompProperties_Mutator : HediffCompProperties
    {
        public bool  onlyOneMutation = false;
        public float mutateHourInterval = 1;
        public float probability = 0.1f;
        public XMT_MutationsHealthSet customMutationSet = null;
        public HediffCompProperties_Mutator()
        {
            compClass = typeof(HediffComp_Mutator);
        }
    }
}
