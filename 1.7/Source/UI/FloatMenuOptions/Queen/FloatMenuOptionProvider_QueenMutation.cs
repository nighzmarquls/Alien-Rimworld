using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class FloatMenuOptionProvider_QueenMutation : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;
        protected override bool RequiresManipulation => true;

        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {
            Pawn pawn = context.FirstSelectedPawn;
            Pawn target = clickedThing as Pawn;
            CompGeneManipulator manipulator = pawn?.GetComp<CompGeneManipulator>();
            if (pawn == null || target == null || manipulator == null || pawn.Faction != Faction.OfPlayer || !manipulator.HasAvailableMutationOptions())
            {
                return null;
            }

            if (target == pawn || target.Dead || target.health == null || XMTUtility.IsXenomorph(target) || !BioUtility.HasMutations(target, false))
            {
                return null;
            }

            FloatMenuOption induce = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("XMT_FMO_InduceMutation".Translate(target.LabelShort), delegate
            {
                manipulator.OpenMutationMenu(target, QueenMutationOperation.Add);
            }, MenuOptionPriority.High), pawn, target);

            FloatMenuOption remove = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("XMT_FMO_RemoveMutation".Translate(target.LabelShort), delegate
            {
                manipulator.OpenMutationMenu(target, QueenMutationOperation.Remove);
            }, MenuOptionPriority.High), pawn, target);

            if (!pawn.CanReserveAndReach(target, PathEndMode.Touch, Danger.Deadly))
            {
                induce.Disabled = true;
                induce.tooltip = "CannotReach".Translate(target.LabelShort);
                remove.Disabled = true;
                remove.tooltip = "CannotReach".Translate(target.LabelShort);
            }

            return new FloatMenuOption("XMT_FMO_MutationActions".Translate(target.LabelShort), delegate
            {
                Find.WindowStack.Add(new FloatMenu(new System.Collections.Generic.List<FloatMenuOption>
                {
                    induce,
                    remove
                }));
            }, MenuOptionPriority.High);
        }
    }
}
