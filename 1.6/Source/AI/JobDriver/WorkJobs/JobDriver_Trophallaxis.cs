using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;
using RimWorld.Planet;
using System.Runtime.Remoting.Messaging;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace Xenomorphtype
{

    public class JobDriver_Trophallaxis : JobDriver
    {
        protected float initialFoodPercentage;

        protected bool recipientStored => recipient.Map != pawn.Map;
        protected Pawn recipient => (Pawn)base.TargetThingA;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
        
            return pawn.Reserve(pawn, job, 1, -1, null, errorOnFailed);
    
        }

        protected IEnumerable<Toil> FeedRecipient()
        {
            AddFailCondition(() => pawn.needs.food.CurCategory == HungerCategory.Starving);
            yield return Mouthfeed();
        }

        private Toil Mouthfeed()
        {
            Toil toil = ToilMaker.MakeToil("Trophallaxis");
            toil.initAction = delegate
            {
                recipient.jobs.StartJob(ChildcareUtility.MakeBabySuckleJob(pawn), JobCondition.InterruptForced);
                initialFoodPercentage = recipient.needs.food.CurLevelPercentage;
            };
            toil.tickAction = delegate
            {
                
                float nutritionWanted = recipient.needs.food.NutritionWanted;
                float gained = pawn.needs.food == null ? Mathf.Min(recipient.needs.food.MaxLevel / 1250f, nutritionWanted) : Mathf.Min(Mathf.Min(recipient.needs.food.MaxLevel / 1250f, nutritionWanted), pawn.needs.food.CurLevel);
                float lost = gained;

                recipient.needs.food.CurLevel += gained;
                if (pawn.needs.food != null)
                {
                    pawn.needs.food.CurLevel -= lost;
                }

                if (recipient.needs.food.CurLevelPercentage >= 0.75f|| pawn.needs.food.CurCategory == HungerCategory.Starving)
                {
                    ReadyForNextToil();
                }
            };
            toil.AddFinishAction(delegate
            {
                if (pawn.needs.joy != null) {
                    pawn.needs.joy.GainJoy(0.12f, InternalDefOf.Communion);
                }
                if (XMTUtility.IsXenomorph(recipient))
                {
                    if (recipient.needs.joy != null)
                    {
                        recipient.needs.joy.GainJoy(0.12f, InternalDefOf.Communion);
                        recipient.needs.joy.GainJoy(0.12f, JoyKindDefOf.Gluttonous);
                    }

                    XMTUtility.GiveInteractionMemory(recipient, HorrorMoodDefOf.RecievedTrophallaxis, pawn);
                }
                else
                {
                    if (recipient.relations != null)
                    {
                        CompPawnInfo pawnInfo = recipient.Info();
                        if (pawnInfo != null)
                        {
                            if (pawnInfo.IsObsessed() || recipient.relations.OpinionOf(pawn) > 0)
                            {
                                XMTUtility.GiveInteractionMemory(recipient, HorrorMoodDefOf.VictimTrophallaxisHappy, pawn);
                                if (recipient.needs.joy != null)
                                {
                                    recipient.needs.joy.GainJoy(0.12f, JoyKindDefOf.Gluttonous);
                                    pawnInfo.GainObsession(0.12f);
                                }
                            }
                            else
                            {
                                XMTUtility.GiveInteractionMemory(recipient, HorrorMoodDefOf.VictimTrophallaxisScared, pawn);
                            }

                        }
                        else
                        {
                            XMTUtility.GiveInteractionMemory(recipient, HorrorMoodDefOf.VictimTrophallaxisScared, pawn);
                        }
                    }
                }

                XMTUtility.GiveInteractionMemory(pawn, HorrorMoodDefOf.GaveTrophallaxis, recipient);
                

                if (recipient.CurJobDef == JobDefOf.BabySuckle)
                {
                    recipient.jobs.EndCurrentJob(JobCondition.Succeeded);
                }

            });
            toil.WithProgressBar((recipientStored) ? TargetIndex.B : TargetIndex.A, () => recipient.needs.food.CurLevelPercentage);
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.WithEffect(EffecterDefOf.Breastfeeding, (recipientStored) ? TargetIndex.B : TargetIndex.A);
            return toil;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
      
            IEnumerator<Toil> feedRecipientEnumerator = FeedRecipient().GetEnumerator();
            if (!feedRecipientEnumerator.MoveNext())
            {
                throw new InvalidOperationException("There must be at least one toil in JobDriver_Trophallaxis.");
            }

            Toil firstFeedingToil = feedRecipientEnumerator.Current;
            yield return Toils_Goto.GotoThing( (recipientStored)? TargetIndex.B : TargetIndex.A, (recipientStored) ? PathEndMode.InteractionCell : PathEndMode.OnCell).FailOnDestroyedNullOrForbidden((recipientStored) ? TargetIndex.B : TargetIndex.A).FailOnSomeonePhysicallyInteracting((recipientStored) ? TargetIndex.B : TargetIndex.A);
            yield return firstFeedingToil;
            while (feedRecipientEnumerator.MoveNext())
            {
                yield return feedRecipientEnumerator.Current;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref initialFoodPercentage, "initialFoodPercentage", 0f);
        }
    }

}
