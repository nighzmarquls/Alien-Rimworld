using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using UnityEngine;
using Verse.Sound;


namespace Xenomorphtype
{

    /*
     * harmony.Patch(AccessTools.Method(typeof(GenConstruct), nameof(GenConstruct.CanConstruct), [typeof(Thing), typeof(Pawn), typeof(bool), typeof(bool), typeof(JobDef)]), 
         postfix: new HarmonyMethod(patchType, nameof(CanConstructPostfix)));
     */
    internal class XMTBuildingPatches {

        [HarmonyPatch(typeof(ThingOwner), nameof(ThingOwner.TryTransferToContainer), new Type[] { typeof(Thing), typeof(ThingOwner), typeof(int), typeof(bool) })]
        public static class Patch_ThingOwner_TryTransferToContainer
        {
            [HarmonyPostfix]
            public static void Postfix(Thing item, ThingOwner otherContainer)
            {
                if(XenoGeneDefOf.XMT_Starbeast_Genetics.IsFinished)
                {
                    return;
                }

                XMTGenePack XMTpack = item as XMTGenePack;
                CompGenepackContainer container = otherContainer.Owner as CompGenepackContainer;
                if (container != null && XMTpack != null)
                {
                    List<Genepack> packs = container.ContainedGenepacks.ListFullCopy();
                    List<Genepack> corruptedPacks = new List<Genepack> { XMTpack };

                    foreach(Genepack pack in container.ContainedGenepacks)
                    {
                        Genepack corruptedPack = (Genepack)ThingMaker.MakeThing(XenoGeneDefOf.XMT_Genepack);
                        if (pack != item)
                        {
                            pack.GeneSet.AddGene(XenoGeneDefOf.XMT_UnknownGenes.genes.RandomElement());
                        }

                        corruptedPack.Initialize(pack.GeneSet.GenesListForReading);
                        corruptedPacks.Add(corruptedPack);
                    }

                    container.innerContainer.ClearAndDestroyContents();
                    container.innerContainer.TryAddRangeOrTransfer(corruptedPacks);
                }
            }
        }

        [HarmonyPatch(typeof(Building_GeneExtractor), "Finish")]
        public static class Patch_Building_GeneExtractor_Finish
        {
            private static readonly SimpleCurve GeneCountChanceCurve = new SimpleCurve
                {
                new CurvePoint(1f, 0.7f),
                new CurvePoint(2f, 0.2f),
                new CurvePoint(3f, 0.08f),
                new CurvePoint(4f, 0.02f)
            };
            public static void ExtractUnknownXenogerm(Pawn ContainedPawn, ref ThingOwner innerContainer, ref Building_GeneExtractor instance)
            {
                
                List<GeneDef> genesToAdd;
                if (ContainedPawn != null)
                {
                    Pawn containedPawn = ContainedPawn;
                    
                    genesToAdd = new List<GeneDef>();
                    Genepack genepack = (Genepack)ThingMaker.MakeThing(XenoGeneDefOf.XMT_Genepack);
                    int num = Mathf.Min((int)GeneCountChanceCurve.RandomElementByWeight((CurvePoint p) => p.y).x, containedPawn.genes.GenesListForReading.Count());

                    for (int i = 0; i < num; i++)
                    {
                        if (i < XenoGeneDefOf.XMT_UnknownGenes.genes.Count)
                        {
                            genesToAdd.Add(XenoGeneDefOf.XMT_UnknownGenes.genes[i]);
                        }
                    }

                    if (genesToAdd.Any())
                    {
                        genepack.Initialize(genesToAdd);
                    }

                    GeneUtility.ExtractXenogerm(containedPawn, Mathf.RoundToInt(60000f * GeneTuning.GeneExtractorRegrowingDurationDaysRange.RandomInRange));
                    IntVec3 intVec = (instance.def.hasInteractionCell ? instance.InteractionCell : instance.Position);
                    innerContainer.TryDropAll(intVec, instance.Map, ThingPlaceMode.Near);
                    if (!containedPawn.Dead && (containedPawn.IsPrisonerOfColony || containedPawn.IsSlaveOfColony))
                    {
                        containedPawn.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtDefOf.XenogermHarvested_Prisoner);
                    }

                    if (genesToAdd.Any())
                    {
                        GenPlace.TryPlaceThing(genepack, intVec, instance.Map, ThingPlaceMode.Near);
                    }

                    Messages.Message("GeneExtractionComplete".Translate(containedPawn.Named("PAWN")) + ": " + genesToAdd.Select((GeneDef x) => x.label).ToCommaList().CapitalizeFirst(), new LookTargets(containedPawn, genepack), MessageTypeDefOf.PositiveEvent);
                    //Rand.PopState();
                }
            }

            public static void ExtractStarbeastXenogerm(Pawn ContainedPawn, ref ThingOwner innerContainer, ref Building_GeneExtractor instance)
            {

                List<GeneDef> genesToAdd;
                if (ContainedPawn != null)
                {
                    Pawn containedPawn = ContainedPawn;

                    genesToAdd = new List<GeneDef>();
                    Genepack genepack = (Genepack)ThingMaker.MakeThing(XenoGeneDefOf.XMT_Genepack);
                    int num = Mathf.Min((int)GeneCountChanceCurve.RandomElementByWeight((CurvePoint p) => p.y).x, containedPawn.genes.GenesListForReading.Count());

                    for (int i = 0; i < num; i++)
                    {
                        genesToAdd.Add(BioUtility.GetRandomHybridGene());
                    }

                    if (genesToAdd.Any())
                    {
                        genepack.Initialize(genesToAdd);
                    }

                    GeneUtility.ExtractXenogerm(containedPawn, Mathf.RoundToInt(60000f * GeneTuning.GeneExtractorRegrowingDurationDaysRange.RandomInRange));
                    IntVec3 intVec = (instance.def.hasInteractionCell ? instance.InteractionCell : instance.Position);
                    innerContainer.TryDropAll(intVec, instance.Map, ThingPlaceMode.Near);
                    if (!containedPawn.Dead && (containedPawn.IsPrisonerOfColony || containedPawn.IsSlaveOfColony))
                    {
                        containedPawn.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtDefOf.XenogermHarvested_Prisoner);
                    }

                    if (genesToAdd.Any())
                    {
                        GenPlace.TryPlaceThing(genepack, intVec, instance.Map, ThingPlaceMode.Near);
                    }

                    Messages.Message("GeneExtractionComplete".Translate(containedPawn.Named("PAWN")) + ": " + genesToAdd.Select((GeneDef x) => x.label).ToCommaList().CapitalizeFirst(), new LookTargets(containedPawn, genepack), MessageTypeDefOf.PositiveEvent);
                    //Rand.PopState();
                }
            }

            [HarmonyPrefix]
            public static bool Prefix(Building_GeneExtractor __instance, ThingOwner ___innerContainer,ref int ___powerCutTicks, ref Sustainer ___sustainerWorking, ref Pawn ___selectedPawn, ref int ___startTick )
            {
                Pawn ContainedPawn = ___innerContainer.FirstOrDefault() as Pawn;
                if (XMTUtility.IsXenomorph(ContainedPawn))
                {
                    ___selectedPawn = null;
                    ___sustainerWorking = null;
                    ___powerCutTicks = 0;
                    if (XenoGeneDefOf.XMT_Starbeast_Genetics.IsFinished)
                    {
                        ExtractStarbeastXenogerm(ContainedPawn, ref ___innerContainer, ref __instance);
                    }
                    else
                    {
                        ExtractUnknownXenogerm(ContainedPawn, ref ___innerContainer, ref __instance);
                    }
                    ___startTick = -1;
                    return false;
                }

                //DO REGULAR GENEEXTRACTORCODE
                return true;
            }
        }
    }
}
