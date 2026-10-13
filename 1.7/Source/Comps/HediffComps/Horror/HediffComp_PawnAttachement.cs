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
        public bool removed = false;
        public HediffCompProperties_PawnAttachement Props => (HediffCompProperties_PawnAttachement)props;

        public Pawn attachedPawn => pawnContainer != null && pawnContainer.Count > 0? pawnContainer[0] as Pawn : null;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref removed, "removed", false);
            Scribe_Deep.Look(ref pawnContainer, "pawnContainer", this);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && pawnContainer == null)
            {
                pawnContainer = new ThingOwner<Thing>(this, oneStackOnly: false);
            }

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

        public IThingHolder ParentHolder => (IThingHolder)Pawn?.Map ?? Pawn?.ParentHolder;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            pawnContainer ??= new ThingOwner<Thing>(this, oneStackOnly: false);
            Pawn attacher = dinfo?.Instigator as Pawn;
            if (attacher == null || !AttachmentUtility.CanAttach(attacher, Pawn) || attachedPawn != null)
            {
                removed = attachedPawn == null;
                return;
            }

            Map map = attacher.Map;
            IntVec3 position = attacher.Position;
            try
            {
                attacher.jobs?.StopAll();
                if (!AttachmentUtility.CanAttach(attacher, Pawn))
                {
                    removed = true;
                    return;
                }
                attacher.DeSpawn();
                pawnContainer.TryAdd(attacher);
            }
            catch (Exception exception)
            {
                Log.Error($"[XMT][Attachment] Containment failed for {attacher} on {Pawn}: {exception}");
            }

            if (attachedPawn != attacher)
            {
                removed = true;
                if (!attacher.Spawned && attacher.holdingOwner == null && !attacher.Destroyed)
                {
                    pawnContainer.TryAdd(attacher);
                    if (pawnContainer.Contains(attacher))
                    {
                        if (!pawnContainer.TryDrop(attacher, position, map, ThingPlaceMode.Near, out Thing _))
                        {
                            PawnRelease();
                        }
                    }
                }
                if (attachedPawn != null)
                {
                    removed = false;
                    PawnRelease();
                }
                Log.Error($"[XMT][Attachment] Could not confirm containment of {attacher} on {Pawn}.");
            }
        }
        public override void CompPostMake()
        {
            base.CompPostMake();
            pawnContainer ??= new ThingOwner<Thing>(this, oneStackOnly: false);
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            Pawn.health.RemoveHediff(parent);
            PawnRelease();

        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            PawnRelease();
            if (attachedPawn != null)
            {
                Log.Error($"[XMT][Attachment] No recovery destination for {attachedPawn} after removal of {parent.def.defName} from {Pawn}. The removed hediff cannot retain save ownership.");
            }
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
        public virtual void PawnRelease(bool killPawn = false)
        {
            Pawn released = attachedPawn;
            if (released == null)
            {
                removed = true;
                return;
            }

            if (!AttachmentUtility.TryReleaseHeldPawn(pawnContainer, released, Pawn))
            {
                return;
            }
            removed = true;

            if (killPawn && !released.Dead)
            {
                released.Kill(new DamageInfo(DamageDefOf.SurgicalCut, 10));
            }
            else if (Props.attackIfRemoved && released.Spawned && !released.Dead)
            {
                released.mindState?.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk, "", forced: true, forceWake: true, false);
            }
            PostPawnReleased(released, killPawn);
        }

        protected virtual void PostPawnReleased(Pawn released, bool killPawn)
        {
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
