using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    public class HorrorChild
    {
        public PawnKindDef childKind;
        public float gestationFactor;
        public float probability;
        public float essenceMinimum;
        public float essenceMaximum = float.MaxValue;
    }

    public class HediffComp_HorrorPregnant : HediffComp_SeverityModifierBase, IHorrorPregnancy
    {
        private PawnKindDef childKind;
        private float gestationFactor = 1f;
        private bool birthed;
        private Pawn father;
        private GeneSet genes;
        private int remaining = -1;
        private Pawn pendingChild;
        private bool discovered;
        public bool IsHorror => true;
        public bool Discovered { get => discovered; set => discovered = value; }
        public bool Initialized => childKind != null && remaining > 0;

        public void Initialize(Pawn donor, GeneSet inherited, float essence, int count)
        {
            if (remaining >= 0) return;
            HorrorChild selected = HorrorPregnancyUtility.Tuning.Select(parent.def, essence);
            if (selected == null) return;
            childKind = selected.childKind;
            gestationFactor = selected.gestationFactor;
            father = donor;
            genes = HorrorPregnancyUtility.CopyGenes(inherited);
            remaining = count;
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            if (remaining >= 0) return;
            Hediff_Pregnant source = Pawn.health.hediffSet.GetFirstHediff<Hediff_Pregnant>();
            father = source?.Father;
            GeneSet inherited = source?.geneSet ?? PregnancyUtility.GetInheritedGeneSet(father, Pawn);
            if (parent is HediffWithParents parents) parents.SetParents(Pawn, father, HorrorPregnancyUtility.CopyGenes(inherited));
            Initialize(father, inherited, HorrorPregnancyUtility.EssenceLoad(Pawn, father),
                source == null ? HorrorPregnancyUtility.LitterSize(Pawn.def) : 1);
            if (source != null && Initialized)
            {
                parent.Severity = source.GestationProgress;
                Pawn.health.RemoveHediff(source);
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Defs.Look(ref childKind, "childKind");
            Scribe_Values.Look(ref gestationFactor, "gestationFactor", 1f);
            Scribe_Values.Look(ref birthed, "birthed");
            Scribe_References.Look(ref father, "father");
            Scribe_Deep.Look(ref genes, "inheritedGenes");
            Scribe_Values.Look(ref remaining, "remainingOffspring", -1);
            Scribe_Deep.Look(ref pendingChild, "pendingChild");
            Scribe_Values.Look(ref discovered, "discovered");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && remaining < 0)
            {
                remaining = birthed ? 0 : 1;
                genes ??= new HorrorGenePayload(BioUtility.GetCanonicalPawnGenes(Pawn)).ToGeneSet();
            }
        }

        public override bool CompShouldRemove => birthed;
        public override float SeverityChangePerDay() => childKind == null ? 0f :
            1f / UnityEngine.Mathf.Max(0.01f, Pawn.RaceProps.gestationPeriodDays * gestationFactor);

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (birthed || remaining <= 0 || childKind == null || parent.Severity < 1f) return;
            while (remaining > 0)
            {
                pendingChild ??= HorrorPregnancyUtility.MakeChild(childKind, Pawn, father, genes);
                if (pendingChild == null) return;
                if (!pendingChild.Spawned && XMTUtility.TrySpawnPawnFromTarget(pendingChild, Pawn) == null) return;
                Pawn delivered = pendingChild;
                pendingChild = null;
                remaining--;
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(XenoPreceptDefOf.XMT_Parasite_Birth,
                    Pawn.Named(HistoryEventArgsNames.Doer), delivered.Named(HistoryEventArgsNames.Victim)), true);
            }
            birthed = true;
        }
    }

    public class HediffCompProperties_Comp_HorrorPregnant : HediffCompProperties
    {
        // Legacy and third-party XML fallback; shipped templates live in HorrorPregnancyDef.
        public List<HorrorChild> babies;
        public HediffCompProperties_Comp_HorrorPregnant() { compClass = typeof(HediffComp_HorrorPregnant); }
    }
}
