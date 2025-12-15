using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;

namespace Xenomorphtype
{
    public class JobDriver_StealthHunt : JobDriver
    {
        private bool notifiedPlayerAttacked;

        private bool firstHit = true;

        public const TargetIndex PreyInd = TargetIndex.A;

        private const TargetIndex CorpseInd = TargetIndex.A;

        private const int MaxHuntTicks = 5000;

        public Pawn Prey
        {
            get
            {
                Corpse corpse = Corpse;
                if (corpse != null)
                {
                    return corpse.InnerPawn;
                }

                return (Pawn)job.GetTarget(TargetIndex.A).Thing;
            }
        }

        private Corpse Corpse => job.GetTarget(TargetIndex.A).Thing as Corpse;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref firstHit, "firstHit", defaultValue: false);
        }

        public override string GetReport()
        {
            if (Corpse != null)
            {
                return ReportStringProcessed(JobDefOf.Ingest.reportString);
            }

            return base.GetReport();
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFinishAction(delegate
            {
                base.Map.attackTargetsCache.UpdateTarget(pawn);
            });

            Toil prepareToEatCorpse = ToilMaker.MakeToil("MakeNewToils");
            prepareToEatCorpse.initAction = delegate
            {
                Pawn actor = prepareToEatCorpse.actor;
                Corpse corpse = Corpse;
                if (corpse == null)
                {
                    Pawn prey2 = Prey;
                    if (prey2 == null)
                    {
                        actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                        return;
                    }

                    corpse = prey2.Corpse;
                    if (corpse == null || !corpse.Spawned)
                    {
                        actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                        return;
                    }
                }

                if (actor.Faction == Faction.OfPlayer)
                {
                    corpse.SetForbidden(value: false, warnOnFail: false);
                }
                else
                {
                    corpse.SetForbidden(value: true, warnOnFail: false);
                }

                actor.CurJob.SetTarget(TargetIndex.A, corpse);
            };
            yield return Toils_General.DoAtomic(delegate
            {
                base.Map.attackTargetsCache.UpdateTarget(pawn);
            });
            Action hitAction = delegate
            {
                Pawn prey = Prey;
                bool surpriseAttack = firstHit && !prey.IsColonist;
                if (pawn.needs.joy != null)
                {
                    pawn.needs.joy.GainJoy(0.125f, InternalDefOf.HuntingPrey);
                    pawn.needs.joy.GainJoy(0.125f, ExternalDefOf.Gaming_Dexterity);
                }
                if (prey.Awake() && !prey.Downed)
                {
                    if (pawn.meleeVerbs.TryMeleeAttack(prey, job.verbToUse, surpriseAttack))
                    {
                        if (!notifiedPlayerAttacked && PawnUtility.ShouldSendNotificationAbout(prey))
                        {
                            notifiedPlayerAttacked = true;
                            Messages.Message("MessageAttackedByPredator".Translate(prey.LabelShort, pawn.LabelIndefinite(), prey.Named("PREY"), pawn.Named("PREDATOR")).CapitalizeFirst(), prey, MessageTypeDefOf.ThreatSmall);
                        }


                        base.Map.attackTargetsCache.UpdateTarget(pawn);
                        firstHit = false;
                    }
                }
                else
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
                }
            };

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil toil = Toils_Combat.FollowAndMeleeAttack(TargetIndex.A, hitAction).JumpIfDespawnedOrNull(TargetIndex.A, prepareToEatCorpse).JumpIf(() => Corpse != null, prepareToEatCorpse)
                .FailOn(() => Find.TickManager.TicksGame > startTick + 5000 && (float)(job.GetTarget(TargetIndex.A).Cell - pawn.Position).LengthHorizontalSquared > 4f);
            yield return toil;
            yield return prepareToEatCorpse;
            Toil gotoCorpse = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return gotoCorpse;
            float durationMultiplier = 1f / pawn.GetStatValue(StatDefOf.EatingSpeed);
            yield return Toils_Ingest.ChewIngestible(pawn, durationMultiplier, TargetIndex.A).FailOnDespawnedOrNull(TargetIndex.A).FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Ingest.FinalizeIngest(pawn, TargetIndex.A);
            yield return Toils_Jump.JumpIf(gotoCorpse, () => pawn.needs.food.CurLevelPercentage < 0.9f);
        }

        public override void Notify_DamageTaken(DamageInfo dinfo)
        {
            base.Notify_DamageTaken(dinfo);
            if (dinfo.Def.ExternalViolenceFor(pawn) && dinfo.Def.isRanged && dinfo.Instigator != null && dinfo.Instigator != Prey && !pawn.InMentalState && !pawn.Downed)
            {
                pawn.mindState.StartFleeingBecauseOfPawnAction(dinfo.Instigator);
            }
        }
       
    }
}
