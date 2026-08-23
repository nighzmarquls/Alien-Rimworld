using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;


namespace Xenomorphtype
{
    internal class XMTSurgeryPatch
    {
        [HarmonyPatch(typeof(Bill_Medical), nameof(Bill_Medical.Notify_BillWorkStarted))]
        public static class Bill_Medical_Notify_BillWorkStarted_Patch
        {
            [HarmonyPostfix]
            public static void PostFix(Pawn billDoer, BillStack ___billStack, Dictionary<ThingDef, int> ___consumedMedicine, RecipeDef ___recipe)
            {
                Pawn pawn = ___billStack.billGiver as Pawn;
                if (___billStack.billGiver is Corpse corpse)
                {
                    pawn = corpse.InnerPawn;
                }

                if (pawn == null || pawn.Dead || billDoer == null || ___recipe?.Worker is not Recipe_Surgery)
                {
                    return;
                }

                CompAcidBlood acidBlood = pawn.GetAcidBloodComp();
                bool insufficientMedicine = true;

                foreach (ThingDef medicine in ___consumedMedicine.Keys)
                {
                    if (medicine == InternalDefOf.Starbeast_Jelly)
                    {
                        BioUtility.TryMutatingPawn(ref pawn);
                    }
                    float potency = medicine.statBases.GetStatValueFromList(StatDefOf.MedicalPotency, 0);
                    if (potency > 1.5f)
                    {
                        insufficientMedicine = false;
                    }
                }

                if (acidBlood == null)
                {
                    return;
                }

                if (___recipe.Worker is Recipe_ExtractHemogen)
                {
                    insufficientMedicine = true;
                }

                if (___recipe.Worker is Recipe_ExtractJelly ||
                    ___recipe.Worker is Recipe_ExtractResin ||
                    ___recipe.Worker is Recipe_ExtractAcid  ||
                    ___recipe.Worker is Recipe_ExtractPheromone)
                {
                    insufficientMedicine = false;
                }

                float acidKnowledge = KnowledgeUtility.GetAcidRiskKnowledge(billDoer);
                if (insufficientMedicine || Rand.Chance(1f - acidKnowledge))
                {
                    AcidUtility.TrySurgicalAcidSpill(pawn, billDoer);
                    billDoer.ClearAllReservations();
                    billDoer.jobs.StopAll();
                }
            }
        }
    }
}
