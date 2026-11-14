using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class FloatMenuOptionProvider_CryptimorphMarkFriend : FloatMenuOptionProvider_AdultCryptimorph
    {
        protected override bool AppliesInt(FloatMenuContext context)
        {
            return base.AppliesInt(context) && context.FirstSelectedPawn.Faction == Faction.OfPlayer &&
                   XMTUtility.QueenIsPlayer();
        }

        protected override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
        {
            Pawn pawn = context.FirstSelectedPawn;
            if (!IsValidTarget(pawn, clickedPawn))
            {
                return null;
            }

            FloatMenuOption option = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(
                "XMT_CommandMarkFriend".Translate(), delegate
                {
                    if (!XMTUtility.QueenIsPlayer() || pawn.Faction != Faction.OfPlayer ||
                        !IsValidTarget(pawn, clickedPawn) || XMTUtility.IsXenomorphFriendly(clickedPawn) ||
                        !FeralJobUtility.IsThingAvailableForJobBy(pawn, clickedPawn) ||
                        !pawn.CanReach(clickedPawn, PathEndMode.Touch, Danger.Deadly))
                    {
                        return;
                    }

                    Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_Snuggle, clickedPawn);
                    FeralJobUtility.ReserveThingForJob(pawn, job, clickedPawn);
                    pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                }, MenuOptionPriority.Default), pawn, clickedPawn);

            if (XMTUtility.IsXenomorphFriendly(clickedPawn))
            {
                option.Disabled = true;
                option.tooltip = "XMT_FMO_AlreadyFriend".Translate(clickedPawn.LabelShort);
            }
            else if (!FeralJobUtility.IsThingAvailableForJobBy(pawn, clickedPawn))
            {
                option.Disabled = true;
                option.tooltip = "XMT_FMO_FriendUnavailable".Translate(clickedPawn.LabelShort);
            }
            else if (!pawn.CanReach(clickedPawn, PathEndMode.Touch, Danger.Deadly))
            {
                option.Disabled = true;
                option.tooltip = "CannotReach".Translate(clickedPawn.LabelShort);
            }

            return option;
        }

        private static bool IsValidTarget(Pawn pawn, Pawn target)
        {
            return pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed &&
                   target != null && target != pawn && target.Spawned && !target.Dead &&
                   target.Map == pawn.Map && !XMTUtility.IsXenomorph(target);
        }
    }
}
