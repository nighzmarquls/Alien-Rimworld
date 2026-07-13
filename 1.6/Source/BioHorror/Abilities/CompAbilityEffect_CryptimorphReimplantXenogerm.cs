using RimWorld;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class CompProperties_AbilityCryptimorphReimplantXenogerm : CompProperties_AbilityReimplantXenogerm
    {
        public CompProperties_AbilityCryptimorphReimplantXenogerm()
        {
            compClass = typeof(CompAbilityEffect_CryptimorphReimplantXenogerm);
        }
    }

    public class CompAbilityEffect_CryptimorphReimplantXenogerm : CompAbilityEffect_ReimplantXenogerm
    {
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn targetPawn = target.Pawn;
            if (targetPawn != null && XMTUtility.IsXenomorph(targetPawn))
            {
                if (throwMessages)
                {
                    Messages.Message("XMT_MutationInvalid_Xenomorph".Translate(targetPawn.Named("PAWN")), targetPawn, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (!ModLister.CheckBiotech("cryptimorph gene implantation"))
            {
                return;
            }

            Pawn caster = parent.pawn;
            Pawn targetPawn = target.Pawn;
            if (caster?.genes == null || targetPawn?.genes == null || XMTUtility.IsXenomorph(targetPawn))
            {
                return;
            }

            int implantCount = caster.genes.Xenogenes.Count;
            for (int i = 0; i < implantCount; i++)
            {
                GeneDef unknownGene = XenoGeneDefOf.XMT_UnknownGenes?.genes is { Count: > 0 } unknownGenes
                    ? unknownGenes.RandomElement()
                    : null;
                if (unknownGene != null)
                {
                    targetPawn.genes.AddGene(unknownGene, false);
                }
            }

            targetPawn.health?.AddHediff(HediffDefOf.XenogerminationComa);
            GeneUtility.ExtractXenogerm(caster, -1);
            GeneUtility.UpdateXenogermReplication(targetPawn);

            FleckMaker.AttachedOverlay(targetPawn, FleckDefOf.FlashHollow, new Vector3(0f, 0f, 0.26f), 1f, -1f);

            if (PawnUtility.ShouldSendNotificationAbout(caster) || PawnUtility.ShouldSendNotificationAbout(targetPawn))
            {
                int comaDuration = HediffDefOf.XenogerminationComa
                    .CompProps<HediffCompProperties_Disappears>().disappearsAfterTicks.max;
                int shockDuration = HediffDefOf.XenogermLossShock
                    .CompProps<HediffCompProperties_Disappears>().disappearsAfterTicks.max;

                Find.LetterStack.ReceiveLetter(
                    "LetterLabelGenesImplanted".Translate(),
                    "LetterTextGenesImplanted".Translate(
                        caster.Named("CASTER"),
                        targetPawn.Named("TARGET"),
                        comaDuration.ToStringTicksToPeriod().Named("COMADURATION"),
                        shockDuration.ToStringTicksToPeriod().Named("SHOCKDURATION")),
                    LetterDefOf.NeutralEvent,
                    new LookTargets(caster, targetPawn));
            }
        }
    }
}
