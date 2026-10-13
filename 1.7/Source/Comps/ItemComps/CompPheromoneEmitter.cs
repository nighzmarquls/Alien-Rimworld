using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class CompPheromoneEmitter : CompApparelReloadable
    {
        private int nextUseTick;

        private CompProperties_PheromoneEmitter EmitterProps => (CompProperties_PheromoneEmitter)props;
        private int CooldownTicksRemaining => Mathf.Max(0, nextUseTick - (Find.TickManager?.TicksGame ?? 0));

        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            Command_Action command = new Command_Action
            {
                defaultLabel = "XMT_PheromoneEmitter_ApplyLabel".Translate(),
                defaultDesc = "XMT_PheromoneEmitter_ApplyDescription".Translate(),
                defaultDescPostfix = "\n\n" + "XMT_PheromoneEmitter_Charges".Translate(RemainingCharges, MaxCharges),
                icon = parent.def.uiIcon,
                action = OpenPheromoneMenu
            };

            if (CooldownTicksRemaining > 0)
            {
                command.Disable("XMT_PheromoneEmitter_Cooldown".Translate(CooldownTicksRemaining.ToStringTicksToPeriod()));
            }
            else if (!CanBeUsed(out string reason))
            {
                command.Disable(reason);
            }

            yield return command;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextUseTick, "nextPheromoneEmitterUseTick", 0);
        }

        public override string CompInspectStringExtra()
        {
            string output = base.CompInspectStringExtra();
            if (!output.NullOrEmpty()) output += "\n";
            output += "XMT_PheromoneEmitter_Charges".Translate(RemainingCharges, MaxCharges);
            if (CooldownTicksRemaining > 0)
            {
                output += "\n";
                output += "XMT_PheromoneEmitter_CooldownInspect".Translate(CooldownTicksRemaining.ToStringTicksToPeriod());
            }
            return output;
        }

        private void OpenPheromoneMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>
            {
                new FloatMenuOption("XMT_Pheromone_Aggregation".Translate(), () => TryActivate(PheromoneProductionType.Aggregation)),
                new FloatMenuOption("XMT_Pheromone_Reproductive".Translate(), () => TryActivate(PheromoneProductionType.Reproductive)),
                new FloatMenuOption("XMT_Pheromone_Alarm".Translate(), () => TryActivate(PheromoneProductionType.Alarm))
            };
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void TryActivate(PheromoneProductionType type)
        {
            Pawn wearer = Wearer;
            if (wearer == null || CooldownTicksRemaining > 0 || !CanBeUsed(out _))
            {
                return;
            }

            float aggregation = type == PheromoneProductionType.Aggregation ? EmitterProps.dose : 0f;
            float reproductive = type == PheromoneProductionType.Reproductive ? EmitterProps.dose : 0f;
            float alarm = type == PheromoneProductionType.Alarm ? EmitterProps.dose : 0f;
            if (!PheromoneUtility.TryApplyArtificial(wearer, aggregation, reproductive, alarm))
            {
                Messages.Message("XMT_PheromoneApplicationFailed".Translate(wearer.Named("PAWN")), wearer, MessageTypeDefOf.RejectInput, false);
                return;
            }

            UsedOnce();
            nextUseTick = Find.TickManager.TicksGame + EmitterProps.cooldownTicks;
        }
    }

    public class CompProperties_PheromoneEmitter : CompProperties_ApparelReloadable
    {
        public int cooldownTicks = 1250;
        public float dose = 1f;

        public CompProperties_PheromoneEmitter()
        {
            compClass = typeof(CompPheromoneEmitter);
        }
    }
}
