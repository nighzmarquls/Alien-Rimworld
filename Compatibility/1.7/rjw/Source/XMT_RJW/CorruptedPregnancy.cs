using RimWorld;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Xenomorphtype;

namespace XMT_RJW
{
    // Added to supported defs before pawns are created/loaded; no transient driver state.
    public class HediffComp_CorruptedPregnancy : HediffComp, IHorrorPregnancy
    {
        public bool corrupted;
        public bool discovered;
        private float essenceLoad;
        private float gestationFactor = 1f;
        private List<Pawn> converted = new List<Pawn>();
        private List<ThingDef> originalRaces = new List<ThingDef>();
        public bool IsHorror => corrupted;
        public bool Discovered { get => discovered; set => discovered = value; }
        public ThingDef OriginalRace(Pawn baby)
        {
            int index = converted.IndexOf(baby);
            return index < 0 || index >= originalRaces.Count ? null : originalRaces[index];
        }
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref corrupted, "horrorCorrupted");
            Scribe_Values.Look(ref discovered, "horrorDiscovered");
            Scribe_Values.Look(ref essenceLoad, "horrorEssenceLoad");
            Scribe_Values.Look(ref gestationFactor, "horrorGestationFactor", 1f);
            Scribe_Collections.Look(ref converted, "horrorOffspring", LookMode.Reference);
            Scribe_Collections.Look(ref originalRaces, "originalRaces", LookMode.Def);
            converted ??= new List<Pawn>();
            originalRaces ??= new List<ThingDef>();
        }

        public bool Corrupt(float essence)
        {
            IList babies = RJWApiShim.Babies(parent);
            if (babies == null || babies.Count == 0) return false;
            essenceLoad = Mathf.Max(essenceLoad, essence);
            for (int i = 0; i < babies.Count; i++)
            {
                if (babies[i] is not Pawn original || original.Spawned || original.Destroyed || converted.Contains(original)) continue;
                HorrorChild choice = HorrorPregnancyUtility.Tuning.Select(
                    HorrorPregnancyUtility.PregnancyFor(original.def), essenceLoad);
                if (choice == null) continue;
                Pawn donor = original.relations?.DirectRelations.FirstOrDefault(r =>
                    r.def == PawnRelationDefOf.Parent && r.otherPawn != Pawn)?.otherPawn ??
                    RJWApiShim.Read(parent, "father") as Pawn ?? (parent as HediffWithParents)?.Father;
                HorrorGenePayload payload = BioUtility.CaptureHorrorGenePayload(original);
                GeneSet genes = payload?.ToGeneSet() ?? new GeneSet();
                Pawn replacement = HorrorPregnancyUtility.MakeChild(choice.childKind, Pawn, donor, genes);
                if (replacement == null) continue;
                BioUtility.TryApplyHorrorGenePayload(replacement, payload);
                replacement.Name = original.Name;
                // All babies remain unspawned and deep-owned by the original pregnancy.
                babies[i] = replacement;
                RJWApiShim.ReplaceSiblingReferences(parent, original, replacement);
                converted.Add(replacement);
                originalRaces.Add(original.def);
                gestationFactor = Mathf.Min(gestationFactor, choice.gestationFactor);
                corrupted = true;
                original.Destroy(DestroyMode.Vanish);
            }
            return corrupted;
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if (!corrupted) return;
            RJWApiShim.Advance(parent, delta, gestationFactor);
            // Later fertilized eggs/twins must not escape the same corruption.
            if (Pawn.IsHashIntervalTick(250, delta)) Corrupt(essenceLoad);
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            if (!corrupted) return;
            // 1.7 SHIM: Biotech + Menstruation move the same baby list through
            // pregnancy -> labor -> pushing. Carry corruption metadata with that list.
            HediffDef next = parent is Hediff_Pregnant ? HediffDefOf.PregnancyLabor :
                parent is Hediff_Labor ? HediffDefOf.PregnancyLaborPushing : null;
            if (next == null) return;
            HediffComp_CorruptedPregnancy successor = Pawn.health.hediffSet.hediffs
                .Where(h => h.def == next).Select(h => h.TryGetComp<HediffComp_CorruptedPregnancy>())
                .LastOrDefault(c => c != null && !c.corrupted);
            if (successor == null) return;
            successor.corrupted = true;
            successor.discovered = discovered;
            successor.essenceLoad = essenceLoad;
            successor.gestationFactor = gestationFactor;
            successor.converted = converted;
            successor.originalRaces = originalRaces;
        }

        public bool Deliver()
        {
            IList babies = RJWApiShim.Babies(parent);
            if (!corrupted || babies == null) return false;
            while (babies.Count > 0)
            {
                if (babies[0] is not Pawn child) return false;
                if (!child.Spawned && XMTUtility.TrySpawnPawnFromTarget(child, Pawn) == null) return false;
                // Remove only after successful release, so save/load cannot deliver twice.
                babies.RemoveAt(0);
                int index = converted.IndexOf(child);
                if (index >= 0) { converted.RemoveAt(index); originalRaces.RemoveAt(index); }
            }
            Pawn.health.RemoveHediff(parent);
            return true;
        }
    }

    public class HediffCompProperties_CorruptedPregnancy : HediffCompProperties
    {
        public HediffCompProperties_CorruptedPregnancy() { compClass = typeof(HediffComp_CorruptedPregnancy); }
    }
}
