using AlienRace;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static UnityEngine.GraphicsBuffer;



namespace Xenomorphtype
{
    public class BioUtility
    {
        private static float maxEssence = 4;
        private const float MetabolicLossSeverity = 0.45f;

        private const float MinMetabolicLossSeverity = 0.45f;

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

                        if (XMTSettings.LogBiohorror)
                        {
                            Log.Message(host + " has extra genes " + hostGenes);
                        }
                    }
                }
            }
            return hostGenes;
        }

        public static void TryMutatingPawn(ref Pawn pawn, XMT_MutationsHealthSet customSet = null, float bonusEssence = 0)
        {
            if (pawn.health == null)
            {
                return;
            }

            if (pawn.GetMorphComp() != null)
            {
                return;
            }

            if (XMTSettings.LogBiohorror)
            {
                Log.Message(pawn + " being mutated");
            }
           
            float essence = GetXenomorphInfluence(pawn)+bonusEssence;

            if(pawn.health.hediffSet.HasPregnancyHediff())
            {
                ApplyHorrorPregnancy(pawn);
            }

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
                    if (TryApplyMutation(pawn, health, out Hediff _, bonusEssence, false))
                    {
                        return;
                    }
                }

            }
        }

        public static XMT_MutationsHealthSet GetFallbackMutationSet(Pawn target)
        {
            if (target != null)
            {
                switch (target.Info().StrongestPheromone)
                {
                    case CompPawnInfo.PheromoneType.Lover:
                        return XenoGeneDefOf.XMT_LovinMutationSet;
                    case CompPawnInfo.PheromoneType.Friend:
                        return XenoGeneDefOf.XMT_AscendanceMutationSet;
                    case CompPawnInfo.PheromoneType.Threat:
                        return XenoGeneDefOf.XMT_HostMeatMutationSet;
                }
            }

            return XenoGeneDefOf.XMT_MutationsSet;
        }

        public static IEnumerable<MutationHealth> AllMutationsForSet(XMT_MutationsHealthSet set)
        {
            if (set?.mutations == null)
            {
                yield break;
            }

            foreach (MutationHealth mutation in set.mutations)
            {
                if (mutation?.horror != null)
                {
                    yield return mutation;
                }
            }
        }

        public static string LabelForMutationSet(XMT_MutationsHealthSet set)
        {
            if (set == null)
            {
                return "";
            }

            if (!set.uiLabel.NullOrEmpty())
            {
                return set.uiLabel;
            }

            if (!set.label.NullOrEmpty())
            {
                return set.LabelCap.ToString();
            }

            return set.defName;
        }

        public static string LabelForMutation(MutationHealth mutation)
        {
            if (mutation == null)
            {
                return "";
            }

            if (!mutation.uiLabel.NullOrEmpty())
            {
                return mutation.uiLabel;
            }

            if (mutation.horror != null)
            {
                if (!mutation.horror.label.NullOrEmpty())
                {
                    return mutation.horror.LabelCap.ToString();
                }

                return mutation.horror.defName;
            }

            return "";
        }

        public static MutationHealth MutationForDef(XMT_MutationsHealthSet set, HediffDef mutationDef)
        {
            if (set?.mutations == null || mutationDef == null)
            {
                return null;
            }

            return set.mutations.FirstOrDefault(mutation => mutation?.horror == mutationDef);
        }

        public static AcceptanceReport CanApplyMutation(Pawn target, MutationHealth mutation, float bonusEssence = 0f, bool requireExistingMutations = false)
        {
            if (target == null)
            {
                return "XMT_MutationInvalid_Target".Translate();
            }

            if (target.Dead)
            {
                return "XMT_MutationInvalid_TargetDead".Translate(target.LabelShort);
            }

            if (target.health == null)
            {
                return "XMT_MutationInvalid_NoHealth".Translate(target.LabelShort);
            }

            if (target.GetMorphComp() != null)
            {
                return "XMT_MutationInvalid_Xenomorph".Translate(target.LabelShort);
            }

            if (mutation?.horror == null)
            {
                return "XMT_MutationInvalid_NoMutation".Translate();
            }

            float essence = GetXenomorphInfluence(target) + bonusEssence;
            if (requireExistingMutations && essence <= 0f)
            {
                return "XMT_MutationInvalid_NoEssence".Translate(essence.ToString("0.##"));
            }

            if (mutation.essenceMinimum > 0 && mutation.essenceMinimum > essence)
            {
                return "XMT_MutationInvalid_EssenceLow".Translate(essence.ToString("0.##"), mutation.essenceMinimum.ToString("0.##"));
            }

            if (mutation.essenceMaximum > 0 && mutation.essenceMaximum < essence)
            {
                return "XMT_MutationInvalid_EssenceHigh".Translate(essence.ToString("0.##"), mutation.essenceMaximum.ToString("0.##"));
            }

            BodyPartRecord specificPart = SpecificMutationPart(target, mutation);
            if (mutation.specificBodyPart != null && specificPart == null)
            {
                return "XMT_MutationInvalid_MissingBodyPart".Translate(mutation.specificBodyPart.label);
            }

            if (mutation.randomBodypart && mutation.specificBodyPart == null && !target.health.hediffSet.GetNotMissingParts().Any())
            {
                return "XMT_MutationInvalid_NoBodyPart".Translate();
            }

            Hediff existing = ExistingMutationHediff(target, mutation.horror, specificPart);
            if (existing != null && existing.Severity >= mutation.horror.maxSeverity)
            {
                return "XMT_MutationInvalid_MaxSeverity".Translate(LabelForMutation(mutation));
            }

            return true;
        }

        public static AcceptanceReport CanRemoveMutation(Pawn target, HediffDef mutationDef)
        {
            if (target == null)
            {
                return "XMT_MutationInvalid_Target".Translate();
            }

            if (target.Dead)
            {
                return "XMT_MutationInvalid_TargetDead".Translate(target.LabelShort);
            }

            if (target.health == null)
            {
                return "XMT_MutationInvalid_NoHealth".Translate(target.LabelShort);
            }

            if (mutationDef == null)
            {
                return "XMT_MutationInvalid_NoMutation".Translate();
            }

            if (!target.health.hediffSet.hediffs.Any(hediff => hediff.def == mutationDef))
            {
                return "XMT_MutationInvalid_NotPresent".Translate(mutationDef.LabelCap);
            }

            return true;
        }

        public static bool TryApplyMutation(Pawn target, MutationHealth mutation, out Hediff changedHediff, float bonusEssence = 0f, bool feedback = true, bool requireExistingMutations = false)
        {
            changedHediff = null;
            AcceptanceReport report = CanApplyMutation(target, mutation, bonusEssence, requireExistingMutations);
            if (!report.Accepted)
            {
                return false;
            }

            BodyPartRecord specificPart = SpecificMutationPart(target, mutation);
            Hediff existing = ExistingMutationHediff(target, mutation.horror, specificPart);
            bool intensified = existing != null;

            if (existing != null)
            {
                existing.Severity = Mathf.Min(existing.Severity + mutation.horror.initialSeverity, mutation.horror.maxSeverity);
                changedHediff = existing;
            }
            else if (specificPart != null)
            {
                changedHediff = target.health.AddHediff(mutation.horror, specificPart);
            }
            else if (mutation.randomBodypart)
            {
                changedHediff = target.health.AddHediff(mutation.horror, target.health.hediffSet.GetNotMissingParts().RandomElement());
            }
            else
            {
                changedHediff = target.health.AddHediff(mutation.horror);
            }

            RevealMutation(changedHediff);
            if (feedback)
            {
                ThrowMutationMote(target, intensified ? "XMT_MutationMote_Intensified".Translate(LabelForMutation(mutation)) : "XMT_MutationMote_Added".Translate(LabelForMutation(mutation)));
            }

            return changedHediff != null;
        }

        public static bool TryRemoveMutation(Pawn target, HediffDef mutationDef, out Hediff changedHediff, bool feedback = true)
        {
            changedHediff = null;
            AcceptanceReport report = CanRemoveMutation(target, mutationDef);
            if (!report.Accepted)
            {
                return false;
            }

            Hediff hediff = target.health.hediffSet.hediffs.FirstOrDefault(candidate => candidate.def == mutationDef);
            if (hediff == null)
            {
                return false;
            }

            float reduction = mutationDef.initialSeverity > 0 ? mutationDef.initialSeverity : 1f;
            if (hediff.Severity > reduction)
            {
                hediff.Severity -= reduction;
                changedHediff = hediff;
                RevealMutation(changedHediff);
                if (feedback)
                {
                    ThrowMutationMote(target, "XMT_MutationMote_Reduced".Translate(mutationDef.LabelCap));
                }
            }
            else
            {
                target.health.RemoveHediff(hediff);
                if (feedback)
                {
                    ThrowMutationMote(target, "XMT_MutationMote_Removed".Translate(mutationDef.LabelCap));
                }
            }

            return true;
        }

        private static BodyPartRecord SpecificMutationPart(Pawn target, MutationHealth mutation)
        {
            if (target?.health?.hediffSet == null || mutation?.specificBodyPart == null)
            {
                return null;
            }

            return target.health.hediffSet.GetBodyPartRecord(mutation.specificBodyPart);
        }

        private static Hediff ExistingMutationHediff(Pawn target, HediffDef mutationDef, BodyPartRecord specificPart)
        {
            if (target?.health?.hediffSet == null || mutationDef == null)
            {
                return null;
            }

            foreach (Hediff hediff in target.health.hediffSet.hediffs)
            {
                if (hediff.def != mutationDef)
                {
                    continue;
                }

                if (specificPart != null && hediff.Part != specificPart)
                {
                    continue;
                }

                return hediff;
            }

            return null;
        }

        private static void RevealMutation(Hediff hediff)
        {
            hediff?.SetVisible();
        }

        private static void ThrowMutationMote(Pawn target, string text)
        {
            if (target?.MapHeld == null || text.NullOrEmpty())
            {
                return;
            }

            MoteMaker.ThrowText(target.DrawPos, target.MapHeld, text, 3.65f);
        }

        public static void InsertGenesetToPawn(GeneSet geneset, ref Pawn pawn, bool xenogene = false, bool forbidUnknown = true)
        {
            if (XMTSettings.LogBiohorror)
            {
                Log.Message("Verifying Humanlike " + pawn);
            }

            if(pawn.IsAnimal)
            {
                return;
            }

            if (XMTSettings.LogBiohorror)
            {
                Log.Message("assigning geneset to " + pawn);
            }

            if (pawn.genes != null)
            {
                
                foreach (GeneDef gene in geneset.GenesListForReading)
                {
                    GeneDef remappedGene = XMT_GeneRemapListDef.GetRemappedGeneFor(pawn.def, gene);
                    if (forbidUnknown && remappedGene.geneClass == typeof(UnknownGene))
                    {
                        continue;
                    }

                    if (XMTSettings.LogBiohorror)
                    {
                        Log.Warning("Adding Gene " + remappedGene);
                    }
                    pawn.genes.AddGene(remappedGene, xenogene);
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

        public static int GeneComplexityTotal(IEnumerable<GeneDef> genes)
        {
            if (genes == null)
            {
                return 0;
            }

            int total = 0;
            foreach (GeneDef gene in genes)
            {
                if (gene != null)
                {
                    total += gene.biostatCpx;
                }
            }

            return total;
        }

        public static int GetHereditaryCapacity(Pawn pawn, int fallback = 12)
        {
            if (pawn == null)
            {
                return fallback;
            }

            return Mathf.Max(0, Mathf.RoundToInt(pawn.GetStatValue(XenoStatDefOf.XMT_HereditaryCapacity)));
        }

        public static List<GeneDef> FilterGenesWithinComplexity(IEnumerable<GeneDef> genes, int capacity)
        {
            List<GeneDef> filteredGenes = new List<GeneDef>();

            if (genes == null || capacity <= 0)
            {
                return filteredGenes;
            }

            int currentComplexity = 0;
            foreach (GeneDef gene in genes)
            {
                if (gene == null)
                {
                    continue;
                }

                if (currentComplexity >= capacity)
                {
                    break;
                }

                int geneComplexity = gene.biostatCpx;
                if (currentComplexity + geneComplexity > capacity)
                {
                    continue;
                }

                filteredGenes.Add(gene);
                currentComplexity += geneComplexity;
            }

            return filteredGenes;
        }

        public static List<GeneDef> GetCryptimorphInheritableGenes(Pawn pawn)
        {
            List<GeneDef> genes = new List<GeneDef>();

            if (pawn == null)
            {
                return genes;
            }

            genes.AddRange(GetExtraHostGenes(pawn));

            if (pawn.genes != null)
            {
                genes.AddRange(GetCryptimorphInheritableGenes(pawn.genes.GenesListForReading));
            }

            return genes;
        }

        public static List<GeneDef> GetCryptimorphInheritableGenes(IEnumerable<GeneDef> genes)
        {
            List<GeneDef> inheritableGenes = new List<GeneDef>();

            if (genes == null)
            {
                return inheritableGenes;
            }

            List<GeneDef> blacklistGenes = InternalDefOf.XMT_Starbeast_AlienRace.alienRace.raceRestriction.blackGeneList;
            List<String> blacklistTags = InternalDefOf.XMT_Starbeast_AlienRace.alienRace.raceRestriction.blackGeneTags;

            foreach (GeneDef gene in genes)
            {
                if (gene == null)
                {
                    continue;
                }

                GeneDef remappedGene = XMT_GeneRemapListDef.GetRemappedGeneFor(InternalDefOf.XMT_Starbeast_AlienRace, gene);

                if (blacklistGenes.Contains(remappedGene))
                {
                    continue;
                }

                bool blacklistedByTag = false;
                foreach (string tag in blacklistTags)
                {
                    if (remappedGene.exclusionTags != null && remappedGene.exclusionTags.Contains(tag))
                    {
                        blacklistedByTag = true;
                        break;
                    }
                }

                if (blacklistedByTag)
                {
                    continue;
                }

                inheritableGenes.Add(remappedGene);
            }

            return inheritableGenes;
        }

        public static List<GeneDef> GetCryptimorphInheritableGenes(IEnumerable<Gene> genes)
        {
            if (genes == null)
            {
                return new List<GeneDef>();
            }

            return GetCryptimorphInheritableGenes(genes.Select(x => x.def));
        }

        public static void ExtractGenesWithinComplexityToGeneset(ref GeneSet geneset, IEnumerable<GeneDef> genes, int capacity)
        {
            if (geneset == null || genes == null)
            {
                Log.Warning("Invalid arguments on ExtractGenesWithinComplexityToGeneset");
                return;
            }

            ExtractGenesToGeneset(ref geneset, FilterGenesWithinComplexity(genes, capacity));
        }

        public static void ExtractCryptimorphGenesToGeneset(ref GeneSet geneset, List<GeneDef> genes)
        {
            if (geneset == null || genes == null)
            {
                Log.Warning("Invalid arguments on ExtractGenestoGeneset");
                return;
            }

            foreach (GeneDef gene in GetCryptimorphInheritableGenes(genes))
            {
                geneset.AddGene(gene);
            }
        }
        public static void ExtractCryptimorphGenesToGeneset(ref GeneSet geneset, List<Gene> genes)
        {
            if (geneset == null || genes == null)
            {
                Log.Warning("Invalid arguments on ExtractGenestoGeneset");
                return;
            }

            foreach (GeneDef gene in GetCryptimorphInheritableGenes(genes))
            {
                geneset.AddGene(gene);
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
                CompPawnInfo info = second.Info();
                if (info != null)
                {
                    info.ApplyLoverPheromone(first);
                    info.GainObsession(0.12f);
                }

                AddLovinMutation(second);
                if(CanGiveHorrorBirth(second) && XMTSettings.HorrorPregnancy)
                {
                    ApplyHorrorPregnancy(second);
                    
                }
                return;
            }

            if ((secondIsXenomorph) && !firstIsXenomorph)
            {
                CompPawnInfo info = first.Info();
                if (info != null)
                {
                    info.ApplyLoverPheromone(second);
                    info.GainObsession(0.12f);
                }
                AddLovinMutation(first);
                if (CanGiveHorrorBirth(first) && XMTSettings.HorrorPregnancy)
                {
                    ApplyHorrorPregnancy(first);
                }
                return;
            }

            float firstXenomorphEssence = GetXenomorphInfluence(first);
            float secondXenomorphEssence = GetXenomorphInfluence(second);

            if (Rand.Chance( (firstXenomorphEssence + secondXenomorphEssence)/2))
            {
                if (CanGiveHorrorBirth(first) && XMTSettings.HorrorPregnancy)
                {
                    ApplyHorrorPregnancy(first);
                }
                if (CanGiveHorrorBirth(second) && XMTSettings.HorrorPregnancy)
                {
                    ApplyHorrorPregnancy(second);
                }
            }
        }


        private static HediffDef GetHorrorPregnancyForPawn(Pawn target)
        {

            HediffDef pregnancy = XenoGeneDefOf.XMT_HorrorPregnant;
            if (target != null)
            {
                if (target.def.modExtensions != null)
                {
                    foreach (DefModExtension modExt in target.def.modExtensions)
                    {
                        AnimalMutateForms animalMutateForms = modExt as AnimalMutateForms;
                        if (animalMutateForms != null)
                        {
                            if (animalMutateForms.horrorPregnancy != null)
                            {
                                pregnancy = animalMutateForms.horrorPregnancy;
                            }
                            break;
                        }
                    }
                }
            }
            return pregnancy;
        }
        private static void ApplyHorrorPregnancy(Pawn target)
        {
            if(target == null)
            {
                return;
            }

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
            if(mapHeld == null)
            {
                return;
            }

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

                        if(!RaceRestrictionSettings.CanHaveGene(gene.def, InternalDefOf.XMT_Starbeast_AlienRace, false))
                        {
                            continue;
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

        public static void AssignAlteredGeneExpression(ref Pawn pawn, List<GeneDef> genes, string xenotypeName = "")
        {

            if (pawn != null)
            {

                if (pawn.genes != null)
                {
                    if (XMTSettings.LogBiohorror)
                    {
                        Log.Message("confirmed has genes " + pawn);
                    }

                    List<Gene> genesToRemove = pawn.genes.GenesListForReading.ToList();

                    if (XMTSettings.LogBiohorror)
                    {
                        Log.Message("removing original genes on " + pawn);
                    }

                    foreach (Gene gene in genesToRemove)
                    {
                        pawn.genes.RemoveGene(gene);
                    }

                    if (XMTSettings.LogBiohorror)
                    {
                        Log.Message("generating new geneset " + pawn);
                    }

                    GeneSet geneSet = new GeneSet();

                    ExtractGenesToGeneset(ref geneSet, genes);
                    InsertGenesetToPawn(geneSet, ref pawn);

                    if (!xenotypeName.NullOrEmpty())
                    {
                        pawn.genes.xenotypeName = xenotypeName;
                    }
                }
            }
        }
        public static void AssignAlteredGeneExpression(ref Thing target, List<GeneDef> genes, string xenotypeName = "")
        {
            if (XMTSettings.LogBiohorror)
            {
                Log.Message("assigning Altered Gene Expression to " + target);
            }


            if (target is Pawn pawn)
            {
                AssignAlteredGeneExpression(ref pawn, genes);
                return;
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
                if (!xenotypeName.NullOrEmpty())
                {
                   geneHolder.templateName = xenotypeName;
                }
            }

            
        }

        internal static List<GeneDef> GetAllHiveGenes(Map map)
        {
            List<GeneDef> genes = new List<GeneDef>();

            if (XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_NovelGenes))
            {
                foreach (GeneDef gene in XenoGeneDefOf.XMT_NovelGenes.genes)
                {
                    genes.AddDistinct(gene);
                }
            }

            List<Thing> GeneCarriers = XMTHiveUtility.GetAllGeneCarriers(map);

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

        public static bool HasMutations(Pawn pawn, bool checkXeno = true)
        {
            if(pawn == null)
            {
                return false;
            }

            if(XMTUtility.IsXenomorph(pawn) && checkXeno)
            {
                return false;
            }
            return GetXenomorphInfluence(pawn) > 0;
        }
        internal static bool HasAlterableGenes(Thing thing)
        {
            if (thing is Pawn pawn)
            {
                if (XMTUtility.IsXenomorph(thing))
                {
                    return true;
                }
                else if (XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_MutantExpression))
                {
                    if(HasMutations(pawn, false))
                    {
                        return true;
                    }
                }
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

        internal static void AlterGenes(ref Pawn targetPawn, List<GeneDef> selectedGenes, List<GeneDef> originalGenes, String xenotypeName)
        {
            int differences = 0;
            foreach (GeneDef gene in selectedGenes)
            {
                if (originalGenes.Contains(gene))
                {
                    continue;
                }
                differences++;
            }

            foreach (GeneDef gene in originalGenes)
            {
                if (!selectedGenes.Contains(gene))
                {
                    differences++;
                }
            }

            if (differences > 0)
            {
                Hediff geneIntegration = HediffMaker.MakeHediff(XenoGeneDefOf.XMT_GeneIntegration, targetPawn);

                geneIntegration.Severity = (1.0f * differences) / 24;

                targetPawn.health.AddHediff(geneIntegration);

                if (targetPawn.genes != null)
                {
                    targetPawn.genes.xenotypeName = xenotypeName;
                }
                AssignAlteredGeneExpression(ref targetPawn, selectedGenes);

                AlienPartGenerator.AlienComp testComp = targetPawn.GetComp<AlienPartGenerator.AlienComp>();
                if (testComp != null)
                {
                    Log.Message("Found Alien Comp on " + targetPawn);
                    testComp.RegenerateAddonsForced();
                }
                else
                {
                    Log.Message("Did not find Alien Comp on " + targetPawn);
                }
               

                if (XMTSettings.LogBiohorror)
                {
                    Log.Message("applied altered genes to " + targetPawn);
                }

            }
        }
        internal static void AlterGenes(ref Thing target, List<GeneDef> selectedGenes, List<GeneDef> originalGenes, String xenotypeName)
        {
            if (target is Pawn targetPawn)
            {
                AlterGenes(ref targetPawn, selectedGenes, originalGenes, xenotypeName);
                return;
            }

            int differences = 0;
            foreach (GeneDef gene in selectedGenes)
            {
                if (originalGenes.Contains(gene))
                {
                    continue;
                }
                differences++;
            }

            foreach (GeneDef gene in originalGenes)
            {
                if (!selectedGenes.Contains(gene))
                {
                    differences++;
                }
            }

            if (differences > 0)
            {

                AssignAlteredGeneExpression(ref target, selectedGenes, xenotypeName);

                if (XMTSettings.LogBiohorror)
                {
                    Log.Message("applied altered genes to " + target);
                }
            }
        }

        public static void ExtractMetabolicCostFromPawn(Pawn pawn, bool useFood = true)
        {
            if (pawn.needs != null && pawn.needs.food != null && useFood)
            {
                pawn.needs.food.CurLevel -= MetabolicLossSeverity;
            }
            else
            {
                Hediff hediff = HediffMaker.MakeHediff(HediffDefOf.BloodLoss, pawn);
                hediff.Severity = MetabolicLossSeverity;
                pawn.health.AddHediff(hediff);
            }
        }
        internal static bool PawnHasEnoughForExtraction(Pawn pawn, bool useFood = true)
        {
            if (pawn.needs != null && pawn.needs.food != null && useFood)
            {
                return pawn.needs.food.CurLevel > MinMetabolicLossSeverity;
            }
            Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (firstHediffOfDef != null)
            {
                return firstHediffOfDef.Severity < MinMetabolicLossSeverity;
            }

            return true;
        }

        internal static bool PerformBioconstructionCost(Pawn pawn)
        {
            if (pawn?.needs?.food != null)
            {
                pawn.needs.food.CurLevel = pawn.needs.food.CurLevel - XMTHiveUtility.HiveHungerCostPerTick;

                if (pawn.needs.food.Starving)
                {
                    Hediff Malnutrition = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Malnutrition);

                    if (Malnutrition != null)
                    {
                        Malnutrition.Severity += 0.001f;
                        pawn.mindState.mentalStateHandler.TryStartMentalState(XenoMentalStateDefOf.XMT_MurderousRage, "", forced: true, forceWake: true, causedByMood: false, transitionSilently: true);
                        return false;
                    }

                }
            }
            return true;
        }

        public static XenotypeDef GetWorldAppropriateXenotype(Faction faction = null)
        {
            XenotypeDef returnType = XenotypeDefOf.Baseliner;
            return returnType;
        }

        internal static void InheritNonGenes(Pawn source, ref Pawn child)
        {
            if (source.story != null && source.story.traits != null)
            {
                foreach (Trait trait in source.story.traits.allTraits)
                {
                    if (trait.def == ExternalDefOf.Nimble)
                    {
                        child.story.traits.GainTrait(new Trait(ExternalDefOf.Nimble));
                        continue;
                    }
                    if (trait.def == ExternalDefOf.Tough)
                    {
                        child.story.traits.GainTrait(new Trait(ExternalDefOf.Tough));
                        continue;
                    }
                    if (trait.def == ExternalDefOf.Beauty)
                    {
                        child.story.traits.GainTrait(new Trait(ExternalDefOf.Beauty, trait.Degree));
                        continue;
                    }

                    if (trait.def == ExternalDefOf.PsychicSensitivity)
                    {
                        if (trait.Degree >= 0)
                        {
                            child.story.traits.GainTrait(new Trait(ExternalDefOf.PsychicSensitivity, trait.Degree));
                        }
                        continue;
                    }
                }
            }

            if (ModsConfig.IsActive("RimEffectRenegade.AsariReapers"))
            {
                Hediff_Level sourceNaturalHediff = source.health.hediffSet.GetFirstHediffOfDef(ExternalDefOf.RE_BioticAmpHediff) as Hediff_Level;
                Log.Message(sourceNaturalHediff + " is available to inherit");

                if (sourceNaturalHediff != null) {
                    int bioticLevel = sourceNaturalHediff.level;

                    Hediff_Level firstHediffOfDef = child.health.hediffSet.GetFirstHediffOfDef(ExternalDefOf.RE_BioticAmpHediff) as Hediff_Level;
                    Log.Message(firstHediffOfDef + " is available to recieve");
                    if (firstHediffOfDef == null)
                    {
                        Log.Message("child def is null, adding Hediff");
                        firstHediffOfDef = child.health.AddHediff(ExternalDefOf.RE_BioticAmpHediff, child.health.hediffSet.GetBodyPartRecord(InternalDefOf.StarbeastBrain)) as Hediff_Level;
                    }

                    if (firstHediffOfDef != null)
                    {
                        for (int i = 0; i < bioticLevel; i++)
                        {
                            firstHediffOfDef.ChangeLevel(1);
                        }
                    }
                }
            }
            if (ModsConfig.RoyaltyActive)
            {
                int inheritLevel = source.GetPsylinkLevel();
                if (inheritLevel > 0)
                {
                    child.ChangePsylinkLevel(inheritLevel);
                }
            }
        }

        internal static void ClearGenes(ref Pawn pawn)
        {
            if (pawn.genes != null)
            {
                foreach (Gene gene in pawn.genes.GenesListForReading)
                {
                    pawn.genes.RemoveGene(gene);
                }
            }
        }

        internal static List<GeneDef> GetXenomorphGenes()
        {
            List<GeneDef> genes = new List<GeneDef>();

            foreach( AlienChanceEntry<GeneDef> geneChance in InternalDefOf.XMT_Starbeast_AlienRace.alienRace.generalSettings.raceGenes)
            {
                if (XMTSettings.LogBiohorror)
                {
                    Log.Message("returning base gene: " + geneChance.entry);
                }
                genes.Add(geneChance.entry);
            }

            return genes;
        }

        internal static PawnKindDef GetChildKindOfHost(Pawn host)
        {
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
                                if(animalHostGenes.customChildKind != null)
                                {
                                    return animalHostGenes.customChildKind;
                                }
                            }
                        }

                    }
                }
            }
            return XenoPawnKindDefOf.XMT_StarbeastKind;
        }
    }
}
