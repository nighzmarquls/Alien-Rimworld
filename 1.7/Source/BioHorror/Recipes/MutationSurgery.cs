using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    // Reuse vanilla amputation's failure, tale, violation, and thought path. Only its
    // actual removal step differs for an adjunct, local tissue, or neural lesion.
    public class Recipe_RemoveMutation : Recipe_RemoveBodyPart
    {
        private bool removed; // Synchronous ApplyOnPawn/DamagePart call only; never saved job state.
        protected override bool SpawnPartsWhenRemoved => false;
        public override string GetLabelWhenUsedOn(Pawn pawn, BodyPartRecord part) =>
            "XMT_ExciseForeignTissue".Translate();
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            bool distributed = recipe.GetModExtension<MutationSurgeryExtension>()?.scatteredCuts > 0;
            return pawn.health.hediffSet.hediffs.Where(h => h.def == recipe.removesHediff && h.Visible && (h.Part != null || distributed))
                .Select(h => h.Part ?? pawn.RaceProps.body.corePart).Where(p => !pawn.health.hediffSet.PartIsMissing(p)).Distinct();
        }
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            return base.AvailableOnNow(thing, part) && thing is Pawn pawn &&
                GetPartsToApplyOn(pawn, recipe).Any(p => part == null || p == part);
        }
        private Hediff Target(Pawn pawn, BodyPartRecord part) => pawn.health.hediffSet.hediffs.FirstOrDefault(h =>
            h.def == recipe.removesHediff && h.Visible && (h.Part == part || (h.Part == null && part.IsCorePart &&
                recipe.GetModExtension<MutationSurgeryExtension>()?.scatteredCuts > 0)));
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn surgeon, List<Thing> ingredients, Bill bill)
        {
            if (part == null) return;
            Hediff target = Target(pawn, part);
            if (target == null || part == null || pawn.health.hediffSet.PartIsMissing(part)) return;
            var tuning = recipe.GetModExtension<MutationSurgeryExtension>();
            bool complete = target.Severity >= tuning.completeSeverity;
            float removedSeverity = target.Severity;
            removed = false;
            base.ApplyOnPawn(pawn, part, surgeon, ingredients, bill);
            if (!removed || !pawn.Spawned || tuning.neurological) return;
            if (tuning.releasesAcid)
            {
                if (KnowledgeUtility.GetAcidRiskKnowledge(surgeon) >= KnowledgeDefOf.XMT_Knowledge_Acid.practicalThreshold)
                    GenPlace.TryPlaceThing(ThingMaker.MakeThing(InternalDefOf.XMT_Acid), pawn.Position, pawn.Map, ThingPlaceMode.Near);
                else AcidUtility.TryRemovedTissueAcidSpill(pawn, surgeon, removedSeverity);
            }
            ThingDef product = complete && tuning.harvestedPart != null ? tuning.harvestedPart :
                DefDatabase<ThingDef>.GetNamed("Starbeast_Flesh_Meat");
            Thing item = ThingMaker.MakeThing(product);
            item.stackCount = product == tuning.harvestedPart ? 1 : tuning.meatYield;
            GenPlace.TryPlaceThing(item, pawn.Position, pawn.Map, ThingPlaceMode.Near);
        }
        public override void DamagePart(Pawn pawn, BodyPartRecord part)
        {
            var tuning = recipe.GetModExtension<MutationSurgeryExtension>();
            if (tuning.amputate) base.DamagePart(pawn, part);
            else
            {
                Hediff target = Target(pawn, part);
                if (target == null) return;
                pawn.health.RemoveHediff(target);
                if (tuning.scatteredCuts > 0)
                {
                    var sites = pawn.health.hediffSet.GetNotMissingParts(depth: BodyPartDepth.Outside).ToList();
                    sites.Shuffle();
                    foreach (BodyPartRecord site in sites.Take(tuning.scatteredCuts))
                        pawn.TakeDamage(new DamageInfo(DamageDefOf.SurgicalCut, 1f, hitPart: site));
                }
                if (tuning.neurological)
                {
                    Hediff scar = HediffMaker.MakeHediff(HediffDefOf.SurgicalCut, pawn, part);
                    pawn.health.AddHediff(scar, part);
                    scar.Severity = 1f;
                    var permanent = scar.TryGetComp<HediffComp_GetsPermanent>();
                    if (permanent != null) permanent.IsPermanent = true;
                }
            }
            removed = true;
        }
    }

    public class Recipe_InstallMutation : Recipe_InstallImplant
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            return pawn.health.hediffSet.GetNotMissingParts().Where(p => recipe.appliedOnFixedBodyParts.Contains(p.def) &&
                !pawn.health.hediffSet.hediffs.Any(h => h.Part == p && h.def == recipe.addsHediff &&
                    (h.def.defName != "XMT_AcidbloodMarrow" || h.Severity >= h.def.maxSeverity)));
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn surgeon, List<Thing> ingredients, Bill bill)
        {
            if (!GetPartsToApplyOn(pawn, recipe).Contains(part)) return;
            if (CheckSurgeryFail(surgeon, pawn, ingredients, part, bill)) return;
            TaleRecorder.RecordTale(TaleDefOf.DidSurgery, surgeon, pawn);
            Hediff mutation = pawn.health.hediffSet.hediffs.FirstOrDefault(h => h.def == recipe.addsHediff && h.Part == part);
            if (mutation != null) mutation.Severity = Mathf.Min(mutation.def.maxSeverity, mutation.Severity + 1f);
            else
            {
                mutation = pawn.health.AddHediff(recipe.addsHediff, part);
                mutation.Severity = Mathf.Min(1f, mutation.def.maxSeverity);
            }
        }
    }
    public class Recipe_InstallMutatedOrgan : Recipe_InstallArtificialBodyPart
    {
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn surgeon, List<Thing> ingredients, Bill bill)
        {
            if (!GetPartsToApplyOn(pawn, recipe).Contains(part)) return;
            var before = pawn.health.hediffSet.hediffs.ToList();
            base.ApplyOnPawn(pawn, part, surgeon, ingredients, bill);
            Hediff installed = pawn.health.hediffSet.hediffs.FirstOrDefault(h => h.def == recipe.addsHediff && h.Part == part && !before.Contains(h));
            if (installed != null) installed.Severity = 1f;
        }
    }
    public class Recipe_ClearCryptimorphicExposure : Recipe_Surgery
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null) =>
            base.AvailableOnNow(thing, part) && thing is Pawn pawn && CryptimorphicInfluence.Exposure(pawn)?.discovered == true;
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn surgeon, List<Thing> ingredients, Bill bill)
        {
            var exposure = CryptimorphicInfluence.Exposure(pawn);
            if (exposure == null || !exposure.discovered || CheckSurgeryFail(surgeon, pawn, ingredients, part, bill)) return;
            pawn.health.RemoveHediff(exposure);
            TaleRecorder.RecordTale(TaleDefOf.DidSurgery, surgeon, pawn);
        }
    }
}
