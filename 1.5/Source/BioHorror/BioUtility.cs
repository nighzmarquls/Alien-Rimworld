﻿using AlienRace;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;


namespace Xenomorphtype
{
    public class BioUtility
    {
        private static float maxEssence = 4;

        private static bool CheckTransformation(TransformationHorror candidate, float Essence, ref PawnKindDef pawnForm, ref ThingDef thingForm)
        {
            if (candidate.essenceMinimum > 0)
            {
                if (Essence < candidate.essenceMinimum)
                {
                    return false;
                }
            }

            if (candidate.essenceMaximum >= 0)
            {
                if (Essence > candidate.essenceMaximum)
                {
                    return false;
                }
            }
            if (Rand.Chance(candidate.probability))
            {
                pawnForm = candidate.pawnTransformationTarget;
                thingForm = candidate.thingTransformationTarget;
                return true;
            }

            return false;
        }
        public static bool TryGetMutationForm(Pawn target, out ThingDef thingForm, out PawnKindDef pawnForm)
        {
            thingForm = null;
            pawnForm = null;

            if(target != null)
            {
                float essence = GetXenomorphInfluence(target);

                if (XMTSettings.LogBiohorror)
                {
                    Log.Message(target + " has essence of " + essence);
                }

                if (target.def != null)
                {
                    if(target.def.modExtensions != null)
                    {
                        foreach (DefModExtension modExt in target.def.modExtensions)
                        {
                            AnimalMutateForms animalMutateForms = modExt as AnimalMutateForms;
                            if (animalMutateForms != null)
                            {
                               
                                foreach(TransformationHorror candidate in animalMutateForms.resultCandidates)
                                {
                                    if(CheckTransformation(candidate,essence,ref pawnForm,ref thingForm))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
                
                foreach (TransformationHorror horror in XenoGeneDefOf.XMT_DefaultTransformationSet.transformations)
                {
                    if (CheckTransformation(horror, essence, ref pawnForm, ref thingForm))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        public static List<GeneDef> GetExtraHostGenes(Pawn host)
        {
            List<GeneDef> hostGenes = new List<GeneDef>();

            if (host != null)
            {
                if (host.def != null)
                {
                    if (host.def.modExtensions != null)
                    {
                        foreach (DefModExtension modExt in host.def.modExtensions)
                        {
                            AnimalHostGenes animalHostGenes = modExt as AnimalHostGenes;
                            if (animalHostGenes != null)
                            {
                                foreach (GeneDef def in animalHostGenes.genes)
                                {
                                    hostGenes.Add(def);
                                }
                            }
                        }
                    }
                }
            }
            return hostGenes;
        }

        public static void TryMutatingPawn(ref Pawn pawn, XMT_MutationsHealthSet customSet = null)
        {
            if (XMTUtility.IsXenomorph(pawn))
            {
                return;
            }
            
            if(pawn.health == null)
            {
                return;
            }

            if (XMTSettings.LogBiohorror)
            {
                Log.Message(pawn + " being mutated");
            }
           

            float essence = GetXenomorphInfluence(pawn);

            XMT_MutationsHealthSet mutations = customSet != null ? customSet : XenoGeneDefOf.XMT_MutationsSet;
            foreach (MutationHealth health in mutations.mutations)
            {
                if (XMTSettings.LogBiohorror)
                {
                    Log.Message("testing essence: " + essence);
                }
               
                if (health.essenceMinimum > 0 && health.essenceMinimum > essence)
                {
                    continue;
                }

                if(health.essenceMaximum > 0 && health.essenceMaximum < essence)
                {
                    continue;
                }

                if (XMTSettings.LogBiohorror)
                {
                    Log.Message("probability:  " + health.probability);
                }
                
                if (Rand.Chance(health.probability))
                {
                    if (health.randomBodypart)
                    {
                        IEnumerable<BodyPartRecord> parts = pawn.health.hediffSet.GetNotMissingParts();
                        if (parts.Any())
                        {
                            pawn.health.AddHediff(health.horror, parts.RandomElement());
                            return;
                        }
                    }
                    else
                    {
                        if(health.specificBodyPart != null)
                        {
                            BodyPartRecord part = pawn.health.hediffSet.GetBodyPartRecord(health.specificBodyPart);
                            if (part != null)
                            {
                                pawn.health.AddHediff(health.horror, part);
                                return;
                            }

                        }
                        else
                        {
                            pawn.health.AddHediff(health.horror);
                            return;
                        }
                    }
                }

            }
        }

        public static void InsertGenesetToPawn(GeneSet geneset, ref Pawn pawn, bool xenogene = false, bool forbidUnknown = true)
        {
            if (XMTSettings.LogBiohorror)
            {
                Log.Message("Verifying Humanlike " + pawn);
            }

            if(pawn.IsNonMutantAnimal)
            {
                return;
            }

            if (XMTSettings.LogBiohorror)
            {
                Log.Message("assigning geneset to " + pawn);
            }

            if (pawn.genes != null)
            {
                List<GeneDef> blacklistGenes = (pawn.def as ThingDef_AlienRace)?.alienRace?.raceRestriction?.blackGeneList;
                List<String> blacklistTags = (pawn.def as ThingDef_AlienRace)?.alienRace?.raceRestriction?.blackGeneTags;

                foreach (GeneDef gene in geneset.GenesListForReading)
                {
                    if (forbidUnknown && gene.geneClass == typeof(UnknownGene))
                    {
                        continue;
                    }

                    if (blacklistGenes.Contains(gene))
                    {
                        continue;
                    }

                    foreach (String tag in blacklistTags)
                    {
                        if (gene.exclusionTags != null)
                        {
                            if (gene.exclusionTags.Contains(tag))
                            {
                                continue;
                            }
                        }
                    }

                    pawn.genes.AddGene(gene, xenogene);
                }
            }
        }
        public static void ExtractGenesToGeneset(ref GeneSet geneset, List<GeneDef> genes)
        {
            if (geneset == null || genes == null)
            {
                Log.Warning("Invalid arguments on ExtractGenestoGeneset");
                return;
            }

            
            foreach (var gene in genes)
            {
                geneset.AddGene(gene);
            }
        }
        public static void ExtractGenesToGeneset(ref GeneSet geneset, List<Gene> genes)
        {
            if (geneset == null || genes == null)
            {
                Log.Warning("Invalid arguments on ExtractGenestoGeneset");
                return;
            }

            List<GeneDef> blacklistGenes = InternalDefOf.XMT_Starbeast_AlienRace.alienRace.raceRestriction.blackGeneList;
            List<String> blacklistTags = InternalDefOf.XMT_Starbeast_AlienRace.alienRace.raceRestriction.blackGeneTags;

            foreach (var gene in genes)
            {
                if(gene is UnknownGene)
                {
                    continue;
                }

                if (blacklistGenes.Contains(gene.def))
                {
                    continue;
                }
                foreach (String tag in blacklistTags)
                {
                    if (gene.def.exclusionTags != null)
                    {
                        if (gene.def.exclusionTags.Contains(tag))
                        {
                            continue;
                        }
                    }
                }

                geneset.AddGene(gene.def);
            }
        }

        public static GeneDef GetRandomHybridGene()
        {
            GeneDef hybridGene = XenoGeneDefOf.XMT_HybridGenes.genes.RandomElement();

            if (hybridGene == null)
            {
                if (XMTSettings.LogBiohorror)
                {
                    Log.Message("Hybrid Gene was Null!");
                }

                hybridGene = XenoGeneDefOf.XMT_Libido;
            }

            if (XMTSettings.LogBiohorror)
            {
                Log.Message("Giving Hybrid Gene " + hybridGene);
            }
            return hybridGene;
        }
        public static float GetXenomorphInfluence(Pawn pawn)
        {
            float essence = 0;

            CompPerfectOrganism compPerfectOrganism = pawn.GetComp<CompPerfectOrganism>();
            if (compPerfectOrganism != null)
            {
                return maxEssence;
            }

            if(pawn.genes != null)
            {
                foreach(Gene gene in pawn.genes.GenesListForReading)
                {
                    if (gene.def.geneClass == typeof(XenomorphGene))
                    {
                        essence += 0.1f;
                    }
                    if (essence >= maxEssence)
                    {
                        return maxEssence;
                    }
                }
            }

            if(pawn.health != null)
            {
                foreach(Hediff hediff in pawn.health.hediffSet.hediffs)
                {
                    for (int i = 0; i < XenoGeneDefOf.XMT_InfluencesSet.influences.Count; i++)
                    {
                        if(hediff.def == XenoGeneDefOf.XMT_InfluencesSet.influences[i].hediff)
                        {
                            essence += XenoGeneDefOf.XMT_InfluencesSet.influences[i].influence;
                        }
                        if(essence >= maxEssence)
                        {
                            return maxEssence;
                        }
                    }
                }
            }

            return essence;
        }
        public static bool CanGiveHorrorBirth(Pawn pawn)
        {
            bool birth = false;

            if(pawn.gender == Gender.Female)
            {
                birth = true;
            }

            //TODO: RJW support.

            return birth;
        }
        public static void ApplySexEffects(Pawn first, Pawn second )
        {

            bool firstIsXenomorph = XMTUtility.IsXenomorph(first);
            bool secondIsXenomorph = XMTUtility.IsXenomorph(second);

            if(firstIsXenomorph && secondIsXenomorph)
            {
                return;
            }

            if ((firstIsXenomorph) && !secondIsXenomorph && second != null)
            {
                CompPawnInfo info = second.GetComp<CompPawnInfo>();
                if (info != null)
                {
                    info.ApplyLoverPheromone(first);
                    info.GainObsession(0.12f);
                }

                AddLovinMutation(second);
                if(CanGiveHorrorBirth(second))
                {
                    ApplyHorrorPregnancy(second);
                    
                }
                return;
            }

            if ((secondIsXenomorph) && !firstIsXenomorph)
            {
                CompPawnInfo info = first.GetComp<CompPawnInfo>();
                if (info != null)
                {
                    info.ApplyLoverPheromone(second);
                    info.GainObsession(0.12f);
                }
                AddLovinMutation(first);
                if (CanGiveHorrorBirth(first))
                {
                    ApplyHorrorPregnancy(first);
                }
                return;
            }

            float firstXenomorphEssence = GetXenomorphInfluence(first);
            float secondXenomorphEssence = GetXenomorphInfluence(second);

            if (Rand.Chance( (firstXenomorphEssence + secondXenomorphEssence)/2))
            {
                if (CanGiveHorrorBirth(first))
                {
                    ApplyHorrorPregnancy(first);
                }
                if (CanGiveHorrorBirth(second))
                {
                    ApplyHorrorPregnancy(second);
                }
            }
        }


        private static HediffDef GetHorrorPregnancyForPawn(Pawn target)
        {
            HediffDef pregnancy = XenoGeneDefOf.XMT_HorrorPregnant;
            foreach (DefModExtension modExt in target.def.modExtensions)
            {
                AnimalMutateForms animalMutateForms = modExt as AnimalMutateForms;
                if (animalMutateForms != null)
                {
                    if(animalMutateForms.horrorPregnancy != null)
                    {
                        pregnancy = animalMutateForms.horrorPregnancy;
                    }
                    break;
                }
            }
            return pregnancy;
        }
        private static void ApplyHorrorPregnancy(Pawn target)
        {
            target.health.AddHediff(GetHorrorPregnancyForPawn(target));
        }

        public static void AddLovinMutation(Pawn target)
        {
            if (XMTUtility.IsInorganic(target))
            {
                return;
            }

            TryMutatingPawn(ref target, XenoGeneDefOf.XMT_LovinMutationSet);
        }
        public static void AddHybridGene(Pawn target)
        {
            if(XMTUtility.IsInorganic(target))
            {
                return;
            }    

            if (target.genes != null)
            {
                GeneDef HybridGene = GetRandomHybridGene();
                if (!target.genes.HasEndogene(HybridGene))
                {
                    target.genes.AddGene(HybridGene, false);
                }
            }
        }

        public static void SpawnJellyHorror(IntVec3 positionHeld, Map mapHeld, float jellyPotency)
        {
            if(XenoGeneDefOf.XMT_GooGenericSet != null)
            {
                GooHorror horror = XenoGeneDefOf.XMT_GooGenericSet.horrors.RandomElement();

                if (horror != null)
                {
                    PawnGenerationRequest request = new PawnGenerationRequest(horror.childKind, null);
                    request.FixedBiologicalAge = 0;
                    Pawn spawn = PawnGenerator.GeneratePawn(request);
                    GenSpawn.Spawn(spawn, positionHeld, mapHeld);
                }
            }
        }

        internal static List<GeneDef> GetGeneForForConsumptionList(Thing target, bool forbidUnknown = true)
        {
            List<GeneDef> genes = new List<GeneDef>();

            if (XMTSettings.LogBiohorror)
            {
                Log.Message("Getting Genes from consumption of " + target);
            }
            Corpse corpse = target as Corpse;
            if (corpse != null)
            {
                if (XMTSettings.LogBiohorror)
                {
                    Log.Message(target + " identified as corpse");
                }

                if (corpse.IsNotFresh())
                {
                    return genes;
                }

                if (XMTSettings.LogBiohorror)
                {
                    Log.Message("confirmed fresh " + target);
                }

                if (XMTUtility.IsInorganic(corpse.InnerPawn))
                {
                    return genes;
                }

                if (XMTSettings.LogBiohorror)
                {
                    Log.Message("confirmed organic " + target);
                }
                genes.AddRange(GetGeneForExpressionList(corpse.InnerPawn));

                if (XMTSettings.LogBiohorror)
                {
                    Log.Message("total genes extracted " + genes.Count);
                }
                return genes;
            }

            Genepack genepack = target as Genepack;

            if (genepack != null)
            {
                return genepack.GeneSet.GenesListForReading;
            }

            Xenogerm xenogerm = target as Xenogerm;

            if (xenogerm != null)
            {
                return xenogerm.GeneSet.GenesListForReading;
            }

            return genes;
        }
        internal static List<GeneDef> GetGeneForExpressionList(Thing target, bool forbidUnknown = true)
        {
            List<GeneDef> genes = new List<GeneDef>();
            List<GeneDef> blacklistGenes = InternalDefOf.XMT_Starbeast_AlienRace.alienRace.raceRestriction.blackGeneList;
            List<String> blacklistTags = InternalDefOf.XMT_Starbeast_AlienRace.alienRace.raceRestriction.blackGeneTags;

            CompHiveGeneHolder hiveGeneHolder = target.TryGetComp<CompHiveGeneHolder>();
            
            if (hiveGeneHolder != null)
            {
                if(hiveGeneHolder.genes != null)
                {
                    if (hiveGeneHolder.genes.GenesListForReading.Count > 0)
                    {
                        foreach (GeneDef gene in hiveGeneHolder.genes.GenesListForReading)
                        {
                            if (genes.Contains(gene))
                            {
                                continue;
                            }

                            genes.Add(gene);

                        }
                    }
                }
            }

            Pawn pawn = target as Pawn;

            if (pawn != null)
            {
                genes.AddRange(GetExtraHostGenes(pawn));

                if (pawn.genes != null)
                {
                   
                    foreach (Gene gene in pawn.genes.GenesListForReading)
                    {
                        if (forbidUnknown && gene.def.geneClass == typeof(UnknownGene))
                        {
                            continue;
                        }

                        if (blacklistGenes.Contains(gene.def))
                        {
                            continue;
                        }

                        foreach (String tag in blacklistTags)
                        {
                            if (gene.def.exclusionTags != null)
                            {
                                if (gene.def.exclusionTags.Contains(tag))
                                {
                                    continue;
                                }
                            }
                        }

                        if (genes.Contains(gene.def))
                        {
                            continue;
                        }

                        genes.Add(gene.def);
                    }
                }
            }

            return genes;
        }

        public static void AssignAlteredGeneExpression(ref Thing target, List<GeneDef> genes)
        {
            if (XMTSettings.LogBiohorror)
            {
                Log.Message("assigning Altered Gene Expression to " + target);
            }
            CompHiveGeneHolder geneHolder = target.TryGetComp<CompHiveGeneHolder>();
            if (geneHolder != null)
            {
                if (XMTSettings.LogBiohorror)
                {
                    Log.Message("confirmed Gene Holder " + target);
                }
                geneHolder.genes = new GeneSet();
                ExtractGenesToGeneset(ref geneHolder.genes, genes);
            }

            Pawn pawn = target as Pawn;

            if (pawn != null)
            {
                if (XMTSettings.LogBiohorror)
                {
                    Log.Message("confirmed as pawn " + target);
                }
                if (pawn.genes != null)
                {
                    if (XMTSettings.LogBiohorror)
                    {
                        Log.Message("confirmed has genes " + target);
                    }

                    List<Gene> genesToRemove = pawn.genes.GenesListForReading.ToList();

                    if (XMTSettings.LogBiohorror)
                    {
                        Log.Message("removing original genes on " + target);
                    }

                    foreach (Gene gene in genesToRemove)
                    {
                        pawn.genes.RemoveGene(gene);
                    }

                    if (XMTSettings.LogBiohorror)
                    {
                        Log.Message("generating new geneset " + target);
                    }

                    GeneSet geneSet = new GeneSet();
                    ExtractGenesToGeneset(ref geneSet, genes);
                    InsertGenesetToPawn(geneSet, ref pawn);
                }
            }
        }

        internal static List<GeneDef> GetAllHiveGenes(Map map)
        {
            List<GeneDef> genes = new List<GeneDef>();

            List<Thing> GeneCarriers = HiveUtility.GetAllGeneCarriers(map);

            foreach (Thing thing in GeneCarriers)
            {
                List<GeneDef> thingGenes = GetGeneForExpressionList(thing);

                foreach (GeneDef gene in thingGenes)
                {
                    genes.AddDistinct(gene);
                }
            }

            return genes;
        }

        internal static bool HasConsumableGenes(Thing thing)
        {
            Corpse corpse = thing as Corpse;
            if (corpse != null)
            {
                if(corpse.IsNotFresh())
                {
                    return false;
                }

                if(!XMTUtility.IsInorganic(corpse.InnerPawn))
                {
                    return true;
                }
            }

            Genepack genepack = thing as Genepack;

            if (genepack != null)
            {
                return true;
            }

            Xenogerm xenogerm = thing as Xenogerm;

            if (xenogerm != null)
            {
                return true;
            }

            return false;
        }
        internal static bool HasAlterableGenes(Thing thing)
        {
            if(XMTUtility.IsXenomorph(thing))
            {
                return true;
            }

            CompHiveGeneHolder geneHolder = thing.TryGetComp<CompHiveGeneHolder>();
            if(geneHolder != null)
            {
                return true;
            }

            return false;
        }

        internal static Hediff MakeEmbryoPregnancy(Pawn pawn, HediffDef embryoHediff = null)
        {
            if(embryoHediff == null)
            {
                embryoHediff = InternalDefOf.XMT_Embryo;
            }
            IEnumerable<BodyPartRecord> source = from x in pawn.health.hediffSet.GetNotMissingParts()
                                                 where
                                                (x.IsInGroup(BodyPartGroupDefOf.Torso))
                                                && x.depth == BodyPartDepth.Inside && x.def != ExternalDefOf.Anus
                                                 select x;

            if (!source.Any())
            {
                source = from x in pawn.health.hediffSet.GetNotMissingParts()
                         where
                            x.depth == BodyPartDepth.Inside
                         select x;
            }

            BodyPartRecord bodyPartRecord = source.RandomElement<BodyPartRecord>();
            return HediffMaker.MakeHediff(embryoHediff, pawn, bodyPartRecord);
            
        }
    }
}
