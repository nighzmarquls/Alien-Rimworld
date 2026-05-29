using RimWorld;
using Verse;

namespace Xenomorphtype
{
    public class CompAbilityLayOvomorph : CompAbilityEffect
    {
        public new CompProperties_AbilityLayOvomorph Props => (CompProperties_AbilityLayOvomorph)props;

        public override bool CanCast => base.CanCast && OvomorphLayUtility.CanAffordFoodCost(parent.pawn, Props.baseFoodCost, Props.useCostModifiers);

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (!OvomorphLayUtility.CanLayAt(parent.pawn, target.Cell, Props.ovomorphDef, out string reason))
            {
                if (!reason.NullOrEmpty())
                {
                    Messages.Message(reason, parent.pawn, MessageTypeDefOf.RejectInput, historical: false);
                }
                return;
            }

            Thing laidThing = OvomorphLayUtility.TryLayOvomorph(parent.pawn, target.Cell, Props.ovomorphDef, Props.baseFoodCost, Props.useCostModifiers, Props.initialProgress);
            if (laidThing == null)
            {
                return;
            }

            if (Props.openGeneDialog && laidThing is GeneOvomorph geneOvomorph)
            {
                Find.WindowStack.Add(new Dialogue_GeneExpression(geneOvomorph));
            }

            base.Apply(target, dest);
        }
    }

    public class CompProperties_AbilityLayOvomorph : CompProperties_AbilityEffect
    {
        public ThingDef ovomorphDef;
        public float baseFoodCost = 2f;
        public bool useCostModifiers = true;
        public float initialProgress;
        public bool openGeneDialog;

        public CompProperties_AbilityLayOvomorph()
        {
            compClass = typeof(CompAbilityLayOvomorph);
        }
    }
}
