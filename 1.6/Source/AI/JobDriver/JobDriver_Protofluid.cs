

using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace Xenomorphtype
{
    internal class JobDriver_Protofluid : JobDriver
    {
        private const TargetIndex TargetInd = TargetIndex.A;

        private const TargetIndex ItemInd = TargetIndex.B;

        private const int DurationTicks = 600;

        private Mote warmupMote;

        private Corpse targetCorpse => (Corpse)job.GetTarget(TargetIndex.A).Thing;

        private Pawn targetPawn => job.GetTarget(TargetInd).Pawn;

        private Thing Item => job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn.Reserve(targetCorpse, job, 1, -1, null, errorOnFailed))
            {
                return pawn.Reserve(Item, job, 1, -1, null, errorOnFailed);
            }

            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch).FailOnDespawnedOrNull(TargetIndex.B).FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOnDespawnedOrNull(TargetIndex.A);
            Toil toil = Toils_General.Wait(600);
            toil.WithProgressBarToilDelay(TargetIndex.A);
            toil.FailOnDespawnedOrNull(TargetIndex.A);
            toil.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            toil.tickAction = delegate
            {
                CompUsable compUsable = Item.TryGetComp<CompUsable>();
                if (compUsable != null && warmupMote == null && compUsable.Props.warmupMote != null)
                {
                    warmupMote = MoteMaker.MakeAttachedOverlay(targetCorpse, compUsable.Props.warmupMote, Vector3.zero);
                }

                warmupMote?.Maintain();
            };
            yield return toil;
            yield return Toils_General.Do(Resurrect);
        }

        private void Resurrect()
        {
            Comp_TargetEffectProtofluid compTargetEffect_Protofluid = Item.TryGetComp<Comp_TargetEffectProtofluid>();

            XMTResearch.ProgressCryptobioTech(500, pawn);

            if (targetPawn != null)
            {
                TaggedString taggedString = HealthUtility.FixWorstHealthCondition(targetPawn);
                if (PawnUtility.ShouldSendNotificationAbout(targetPawn))
                {
                    Messages.Message(taggedString, targetPawn, MessageTypeDefOf.PositiveEvent);
                }

                if (compTargetEffect_Protofluid.Props.addsHediff != null)
                {
                    targetPawn.health.AddHediff(compTargetEffect_Protofluid.Props.addsHediff);
                }
                
                Item.SplitOff(1).Destroy();
                return;
            }

            Pawn innerPawn = targetCorpse.InnerPawn;

            bool flag = true;
            if (!compTargetEffect_Protofluid.Props.withSideEffects)
            {
                if (!ResurrectionUtility.TryResurrect(innerPawn))
                {
                    flag = false;
                }
            }
            else if (!ResurrectionUtility.TryResurrectWithSideEffects(innerPawn))
            {
                flag = false;
            }

            if (flag)
            {
                SoundDefOf.MechSerumUsed.PlayOneShot(SoundInfo.InMap(innerPawn));
                Messages.Message("MessagePawnResurrected".Translate(innerPawn), innerPawn, MessageTypeDefOf.PositiveEvent);
                if (compTargetEffect_Protofluid.Props.moteDef != null)
                {
                    MoteMaker.MakeAttachedOverlay(innerPawn, compTargetEffect_Protofluid.Props.moteDef, Vector3.zero);
                }

                if (compTargetEffect_Protofluid.Props.addsHediff != null)
                {
                    innerPawn.health.AddHediff(compTargetEffect_Protofluid.Props.addsHediff);
                }
            }

            Item.SplitOff(1).Destroy();
        }
    }
}