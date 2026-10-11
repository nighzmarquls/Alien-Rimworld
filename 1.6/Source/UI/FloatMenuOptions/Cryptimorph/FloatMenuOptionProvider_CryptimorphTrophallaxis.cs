using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class FloatMenuOptionProvider_CryptimorphTrophallaxis : FloatMenuOptionProvider_AdultCryptimorph
    {
        protected override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
        {
            if (clickedPawn == null || clickedPawn == context.FirstSelectedPawn || clickedPawn.needs?.food == null)
            {
                return null;
            }

            return MakeOption(context.FirstSelectedPawn, clickedPawn, clickedPawn);
        }

        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {
            if (clickedThing is not EggSack eggSack || eggSack.Occupant?.needs?.food == null)
            {
                return null;
            }

            return MakeOption(context.FirstSelectedPawn, eggSack.Occupant, eggSack);
        }

        private static FloatMenuOption MakeOption(Pawn pawn, Pawn recipient, Thing reserveTarget)
        {
            FloatMenuOption option = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(
                "XMT_FMO_Feed".Translate(), delegate
                {
                    FeralJobUtility.ClearFeralJobReservationsForTarget(pawn.Map, reserveTarget);
                    if (pawn.Crawling)
                    {
                        CompCrawler crawler = pawn.GetComp<CompCrawler>();
                        if (crawler != null)
                        {
                            crawler.Crawling = false;
                        }
                    }

                    Job job = reserveTarget == recipient
                        ? JobMaker.MakeJob(XenoWorkDefOf.XMT_PerformTrophallaxis, recipient)
                        : JobMaker.MakeJob(XenoWorkDefOf.XMT_PerformTrophallaxis, recipient, reserveTarget);
                    pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                }, MenuOptionPriority.Default), pawn, reserveTarget);

            if (pawn.Starving())
            {
                option.Disabled = true;
                option.tooltip = "XMT_FMO_TooHungry".Translate(pawn.LabelShort);
            }
            else if (recipient.needs.food.CurLevel >= recipient.needs.food.MaxLevel)
            {
                option.Disabled = true;
                option.tooltip = "XMT_FMO_TooStuffed".Translate(recipient.LabelShort);
            }

            return option;
        }
    }
}
