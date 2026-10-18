using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class FloatMenuOptionProvider_HiveStructures : FloatMenuOptionProvider
    {
        protected override bool Drafted => false;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;

        protected override bool AppliesInt(FloatMenuContext context) => context.FirstSelectedPawn?.Faction == Faction.OfPlayer;
        public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing target, FloatMenuContext context)
        {
            Pawn actor = context.FirstSelectedPawn;
            foreach (HiveStructureUnlockDef def in HiveStructureUtility.Available.Where(d => d.floatMenu && d.Worker.ActorAllowed(d, actor) && d.Worker.TargetAllowed(d, target)))
            {
                var option = new FloatMenuOption(def.LabelCap, () => HiveStructureUtility.StartOrder(def, actor, target), def.Icon, Color.white);
                yield return FloatMenuUtility.DecoratePrioritizedTask(option, actor, target);
            }

            CompHivePrunable prune = target?.TryGetComp<CompHivePrunable>();
            if (prune?.CanPrune == true && actor.DevelopmentalStage.Adult() && !actor.Downed && XMTUtility.IsXenomorph(actor))
                yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("XMT_PruneStructure".Translate(target.LabelShortCap), () => actor.jobs.TryTakeOrderedJob(JobMaker.MakeJob(XenoWorkDefOf.XMT_PruneLarder, target), JobTag.Misc)), actor, target);
        }
    }
}
