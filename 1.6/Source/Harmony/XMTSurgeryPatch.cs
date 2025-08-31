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

                CompAcidBlood acidBlood = pawn.GetComp<CompAcidBlood>();


                if (___recipe.Worker is Recipe_Surgery recipe_Surgery)
                {
                    bool insufficientMedicine = true;


                    foreach (ThingDef medicine in ___consumedMedicine.Keys)
                    {
                        if (medicine == InternalDefOf.Starbeast_Jelly)
                        {
                            BioUtility.TryMutatingPawn(ref pawn);
                        }
                        float potency = medicine.statBases.GetStatValueFromList(StatDefOf.MedicalPotency, 0);
                        if (potency > 1.5)
                        {
                            insufficientMedicine = false;
                        }
                    }

                    if (___recipe.Worker is Recipe_ExtractHemogen)
                    {
                        insufficientMedicine = true;
                    }

                    if(___recipe.Worker is Recipe_ExtractJelly || ___recipe.Worker is Recipe_ExtractResin)
                    {
                        insufficientMedicine = false;
                    }

                    if (insufficientMedicine)
                    {
                        if (acidBlood != null)
                        {
                            pawn.health.AddHediff(HediffDefOf.SurgicalCut, dinfo: new DamageInfo(DamageDefOf.SurgicalCut, amount: 1, instigator: billDoer));
                            if (acidBlood.TrySplashAcidThing(1, billDoer))
                            {
                                billDoer.ClearAllReservations();
                                billDoer.jobs.StopAll();
                            }

                            acidBlood.TrySplashAcid(acidBlood.GetBloodFullness());
                            return;
                        }
                    }
                }
            }
        }
    }
}
