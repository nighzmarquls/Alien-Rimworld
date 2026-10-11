using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class FloatMenuOptionProvider_QueenAssimilation : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;
        protected override bool RequiresManipulation => true;

        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {
            Pawn pawn = context.FirstSelectedPawn;
            CompQueenAssimilation comp = pawn?.GetComp<CompQueenAssimilation>();
            if (comp == null || clickedThing == null)
            {
                return null;
            }

            AcceptanceReport report = comp.CanAssimilate(clickedThing, out QueenAssimilationDef def);
            if (!report.Accepted)
            {
                return null;
            }

            string label = "XMT_AssimilateThing".Translate(clickedThing.LabelShort);
            FloatMenuOption option = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, delegate
            {
                comp.TryStartAssimilationJob(clickedThing);
            }, MenuOptionPriority.High), pawn, clickedThing);

            if (!pawn.CanReserveAndReach(clickedThing, PathEndMode.Touch, Danger.Deadly))
            {
                option.Disabled = true;
                option.tooltip = "XMT_AssimilationCannotReach".Translate(clickedThing.LabelShort);
            }

            return option;
        }
    }
}
