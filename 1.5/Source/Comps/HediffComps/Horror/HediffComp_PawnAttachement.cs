using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    internal class HediffComp_PawnAttachement : HediffComp  , IThingHolder 
    {
        ThingOwner pawnContainer;
        bool removed = false;
        public HediffCompProperties_PawnAttachement Props => (HediffCompProperties_PawnAttachement)props;

        public Pawn attachedPawn => pawnContainer != null && pawnContainer.Count > 0? pawnContainer[0] as Pawn : null;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref removed, "removed", false);
            Scribe_Deep.Look(ref pawnContainer, "pawnContainer");

        }
        public override bool CompShouldRemove
        {
            get
            {
                if (base.CompShouldRemove)
                {
                    return true;
                }
                return removed;
            }
        }

        public IThingHolder ParentHolder => parent.pawn.Map;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            pawnContainer = new ThingOwner<Thing>(this, oneStackOnly: false);
            if (dinfo != null)
            {
                Pawn attacher = dinfo.Value.Instigator as Pawn;
                if (attacher != null)
                {
                    attacher.jobs.StopAll();
                    attacher.DeSpawn();
                    if (attacher.holdingOwner != null)
                    {
                        attacher.holdingOwner.TryTransferToContainer(attacher, pawnContainer, attacher.stackCount);
                    }
                    else
                    {
                        pawnContainer.TryAdd(attacher);
                    }
                }
            }
        }
        public override void CompPostMake()
        {
            base.CompPostMake();

        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            Pawn.health.RemoveHediff(parent);
            PawnRelease();

        }

        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
        }
        public override void Notify_SurgicallyReplaced(Pawn surgeon)
        {
            base.Notify_SurgicallyReplaced(surgeon);
            PawnRelease();
        }
        public override void Notify_SurgicallyRemoved(Pawn surgeon)
        {
            base.Notify_SurgicallyRemoved(surgeon);
            PawnRelease();
        }
        public override void CompTended(float quality, float maxQuality, int batchPosition = 0)
        {
            base.CompTended(quality, maxQuality, batchPosition);

            if (quality >= Props.tendQualityToRelease)
            {
                if (Props.tendQualityToKill >= 0 && quality > Props.tendQualityToKill)
                {
                    PawnRelease(killPawn:true);
                    return;
                }
                else
                {
                    PawnRelease();
                }
            }


        }
        public void PawnRelease(bool killPawn = false)
        {
            if (removed)
            {
                return;
            }
            Pawn released = pawnContainer[0] as Pawn;
            if (released != null)
            {
                if (pawnContainer.TryDropAll(parent.pawn.PositionHeld, parent.pawn.MapHeld, ThingPlaceMode.Near))
                {
                    if (Props.attackIfRemoved)
                    {
                        released.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk, "", forced: true, forceWake: true, false);
                    }

                    if (killPawn)
                    {
                        released.Kill(new DamageInfo(DamageDefOf.SurgicalCut,10));
                    }
                }
            }
            removed = true;
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return pawnContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }
    }

    public class HediffCompProperties_PawnAttachement : HediffCompProperties
    {
        public float    tendQualityToRelease = 0.75f;
        public float    tendQualityToKill = -1f;
        public bool     attackIfRemoved = true;
        public HediffCompProperties_PawnAttachement()
        {
            compClass = typeof(HediffComp_PawnAttachement);
        }
    }
}
