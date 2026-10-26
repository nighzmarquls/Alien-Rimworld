using RimWorld;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public interface IHorrorPregnancy
    {
        bool IsHorror { get; }
        bool Discovered { get; set; }
    }

    public static class HorrorPregnancyUtility
    {
        private static readonly System.Reflection.FieldInfo notificationCountdown =
            AccessTools.Field(typeof(HediffComp_MessageAfterTicks), "ticksUntilMessage");

        public static void TransferNotificationState(Hediff source, Hediff replacement)
        {
            var previous = source?.TryGetComp<HediffComp_MessageAfterTicks>();
            var next = replacement?.TryGetComp<HediffComp_MessageAfterTicks>();
            if (previous != null && next != null)
                notificationCountdown.SetValue(next, notificationCountdown.GetValue(previous));
        }
        public static bool Enabled => XMTSettings.HorrorPregnancy;
        public static HorrorPregnancyDef Tuning => DefDatabase<HorrorPregnancyDef>.GetNamed("XMT_HorrorPregnancy");

        // Intentional non-inlined Harmony extension points. Core never references optional mod types.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool TryHandleNativePregnancy(Pawn recipient, Pawn donor, float essence, bool existingOnly,
            out bool result) { result = false; return false; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool ShouldDeferMotherTransformation(Pawn pawn) => false;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool UseVanillaGeneInspection(Pawn pawn) => true;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static float AdditionalEssence(Pawn recipient) => CryptimorphicInfluence.Contamination(recipient);
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ApplyReproductiveMutation(Pawn recipient, Pawn partner) { }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool TryAdvanceNativePregnancy(Hediff pregnancy, float progress) => false;

        public static float AdvancedProgress(float current, float additional) =>
            current >= 1f ? current : Mathf.Min(1f, current + Mathf.Max(0f, additional));

        public static void AdvancePregnancies(Pawn pawn, float progress)
        {
            if (pawn?.health == null || pawn.Dead || !(progress > 0f) || float.IsNaN(progress)) return;
            // Each hediff owns one gestation, irrespective of its fetus/litter count or womb references.
            foreach (Hediff pregnancy in pawn.health.hediffSet.hediffs.ToList())
            {
                if (TryAdvanceNativePregnancy(pregnancy, progress)) continue;
                if (pregnancy is Hediff_Pregnant || pregnancy.TryGetComp<HediffComp_HorrorPregnant>() != null)
                    pregnancy.Severity = AdvancedProgress(pregnancy.Severity, progress);
            }
        }

        public static float EssenceLoad(Pawn recipient, Pawn donor = null, float extra = 0f) =>
            (recipient == null ? 0f : BioUtility.GetXenomorphInfluence(recipient)) +
            AdditionalEssence(recipient) + Mathf.Max(0f, extra);

        public static string Reveal(Pawn patient)
        {
            var findings = new HashSet<string>();
            foreach (Hediff tissue in patient.health.hediffSet.hediffs)
            {
                IHorrorPregnancy pregnancy = MutationTissueAssessment.HorrorPregnancy(tissue);
                if (pregnancy == null || pregnancy.Discovered) continue;
                pregnancy.Discovered = true;
                tissue.SetVisible();
                var metadata = tissue.def.GetModExtension<MutationTissueExtension>();
                string key = metadata?.inspectionFindingKey ?? TissueAssessmentDef.Current.defaultInspectionFindingKey;
                if (!key.NullOrEmpty()) findings.Add(key.Translate(patient.Named("PAWN"), tissue.LabelCap.Named("MUTATION")).ToString());
            }
            return string.Join("\n\n", findings);
        }

        public static HediffDef PregnancyFor(ThingDef race) =>
            race?.GetModExtension<AnimalMutateForms>()?.horrorPregnancy ?? XenoGeneDefOf.XMT_HorrorPregnant;

        public static int LitterSize(ThingDef race)
        {
            if (race?.comps?.Any(c => c is CompProperties_EggLayer) == true) return 1;
            return Mathf.Max(1, race?.race?.litterSizeCurve == null ? 1 :
                Mathf.RoundToInt(Rand.ByCurve(race.race.litterSizeCurve)));
        }

        public static GeneSet CopyGenes(GeneSet source)
        {
            GeneSet copy = new GeneSet();
            if (source != null) foreach (GeneDef gene in source.GenesListForReading) copy.AddGene(gene);
            return copy;
        }

        public static bool TryApply(Pawn recipient, Pawn donor = null, float extraEssence = 0f,
            bool existingOnly = false, bool force = false, HediffDef pregnancyDef = null)
        {
            if (recipient?.health == null || recipient.Dead || !XMTSettings.HorrorPregnancy) return false;
            float essence = EssenceLoad(recipient, donor, extraEssence);
            if (TryHandleNativePregnancy(recipient, donor, essence, existingOnly, out bool handled)) return handled;
            Hediff_Pregnant source = recipient.health.hediffSet.GetFirstHediff<Hediff_Pregnant>();
            if (recipient.health.hediffSet.GetHediffComps<HediffComp_HorrorPregnant>().Any()) return true;
            if (source == null && (existingOnly || !BioUtility.CanGiveHorrorBirth(recipient))) return false;
            if (!force && !Rand.Chance(Tuning.Chance(essence, source != null))) return false;
            Hediff replacement = HediffMaker.MakeHediff(pregnancyDef ?? PregnancyFor(recipient.def), recipient, source?.Part);
            HediffComp_HorrorPregnant comp = replacement.TryGetComp<HediffComp_HorrorPregnant>();
            if (comp == null) return false;
            Pawn father = source != null ? source.Father : donor;
            GeneSet genes = source != null ? CopyGenes(source.geneSet) : PregnancyUtility.GetInheritedGeneSet(father, recipient);
            comp.Initialize(father, genes, essence, source == null ? LitterSize(recipient.def) : 1);
            if (!comp.Initialized) return false;
            if (replacement is HediffWithParents parents) parents.SetParents(recipient, father, genes);
            recipient.health.AddHediff(replacement, source?.Part);
            if (source != null)
            {
                TransferNotificationState(source, replacement);
                replacement.Severity = Mathf.Clamp01(source.GestationProgress);
                recipient.health.RemoveHediff(source);
            }
            return true;
        }

        public static Pawn MakeChild(PawnKindDef kind, Pawn mother, Pawn father, GeneSet genes)
        {
            PawnGenerationRequest request = new PawnGenerationRequest(kind, mother.Faction);
            request.FixedBiologicalAge = 0f;
            Pawn child = PawnGenerator.GeneratePawn(request);
            BioUtility.TryApplyHorrorGenePayload(child, new HorrorGenePayload(genes?.GenesListForReading));
            if (child.relations != null)
            {
                child.relations.AddDirectRelation(PawnRelationDefOf.ParentBirth, mother);
                child.relations.AddDirectRelation(PawnRelationDefOf.Parent, mother);
                if (father != null && father != mother) child.relations.AddDirectRelation(PawnRelationDefOf.Parent, father);
            }
            CompLarvalGenes larva = child.GetComp<CompLarvalGenes>();
            if (larva != null) { larva.mother = mother; larva.father = father; }
            return child;
        }
    }


}
