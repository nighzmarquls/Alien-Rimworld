using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;
using Xenomorphtype;

namespace XMT_RJW
{
    public class HediffComp_ReproductiveTissue : HediffComp
    {
        private bool matured;
        public override void CompExposeData() { Scribe_Values.Look(ref matured, "matured"); }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if (matured) return;
            severityAdjustment += RJWReproductionDef.Current.maturationPerDay * delta / GenDate.TicksPerDay;
            if (parent.Severity < 1f) return;
            BodyPartRecord site = parent.Part ?? RJWApiShim.Genitals(Pawn);
            if (site == null || Pawn.health.hediffSet.PartIsMissing(site)) return;
            HediffDef target = RJWReproductionDef.Current.femalePart;
            var originals = RJWApiShim.FemaleParts(Pawn).Where(h => h.Part == site && h.def != target).ToList();
            foreach (Hediff old in originals)
            {
                Hediff replacement = RJWApiShim.MakeFemalePart(Pawn, site);
                if (replacement == null) return;
                RJWReproduction.TransferWomb(old, replacement);
                Pawn.health.AddHediff(replacement, site);
                Pawn.health.RemoveHediff(old);
            }
            if (originals.Count == 0 && !Pawn.health.hediffSet.hediffs.Any(h => h.def == target && h.Part == site))
            {
                Hediff created = RJWApiShim.MakeFemalePart(Pawn, site);
                if (created == null) return;
                Pawn.health.AddHediff(created, site);
            }
            matured = true;
        }
    }

    public class HediffCompProperties_ReproductiveTissue : HediffCompProperties
    {
        public HediffCompProperties_ReproductiveTissue() { compClass = typeof(HediffComp_ReproductiveTissue); }
    }

    public class Hediff_FluidEssence : HediffWithComps
    {
        public int expiresAt;
        // Exposure is integration bookkeeping, not a visible pregnancy/health condition.
        public override bool Visible => false;
        public override void ExposeData() { base.ExposeData(); Scribe_Values.Look(ref expiresAt, "expiresAt"); }
        public override bool ShouldRemove => Find.TickManager.TicksGame >= expiresAt;
    }
}
