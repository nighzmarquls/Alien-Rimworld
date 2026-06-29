using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class PawnTrampler : Thing, IThingHolder
    {
        private ThingOwner<Thing> innerContainer;
        private Vector3 startVec;
        private IntVec3 destCell;
        private bool pawnWasDrafted;
        private bool pawnCanFireAtWill = true;
        private int ticksFlightTime = 90;
        private int ticksFlying;
        private int positionLastComputedTick = -1;
        private Vector3 effectivePos;
        private Vector3 groundPos;

        private Pawn TramplingPawn
        {
            get
            {
                if (innerContainer.InnerListForReading.Count <= 0)
                {
                    return null;
                }

                return innerContainer.InnerListForReading[0] as Pawn;
            }
        }

        public override Vector3 DrawPos
        {
            get
            {
                RecomputePosition();
                return effectivePos;
            }
        }

        public PawnTrampler()
        {
            innerContainer = new ThingOwner<Thing>(this);
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        protected override void Tick()
        {
            if (ticksFlying >= ticksFlightTime)
            {
                RespawnPawn();
                Destroy();
                return;
            }

            if (ticksFlying % 6 == 0)
            {
                RecomputePosition();
                FleckMaker.ThrowDustPuff(groundPos + Gen.RandomHorizontalVector(0.45f), Map, 1.6f);
            }

            ticksFlying++;
        }

        private void RespawnPawn()
        {
            Pawn pawn = TramplingPawn;
            if (pawn == null)
            {
                return;
            }

            FleckMaker.ThrowDustPuff(destCell.ToVector3Shifted() + Gen.RandomHorizontalVector(0.6f), Map, 2.5f);
            innerContainer.TryDrop(pawn, destCell, Map, ThingPlaceMode.Direct, out Thing _result, null, null, playDropSound: false);

            if (pawn.drafter != null)
            {
                pawn.drafter.Drafted = pawnWasDrafted;
                pawn.drafter.FireAtWill = pawnCanFireAtWill;
            }

            if (pawn.jobs.curJob == null)
            {
                pawn.jobs.CheckForJobOverride();
            }
        }

        private void RecomputePosition()
        {
            if (positionLastComputedTick == ticksFlying)
            {
                return;
            }

            positionLastComputedTick = ticksFlying;
            float t = ticksFlightTime <= 0 ? 1f : Mathf.Clamp01((float)ticksFlying / ticksFlightTime);
            Vector3 destVec = destCell.ToVector3ShiftedWithAltitude(AltitudeLayer.Pawn);
            groundPos = Vector3.Lerp(startVec, destVec, t);
            effectivePos = groundPos + Altitudes.AltIncVect * 0.15f;
            base.Position = groundPos.ToIntVec3();
        }

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            RecomputePosition();
            TramplingPawn?.DynamicDrawPhaseAt(phase, effectivePos);
            //base.DynamicDrawPhaseAt(phase, drawLoc, flip);
        }

        public static PawnTrampler MakeTrampler(ThingDef tramplerDef, Pawn pawn, IntVec3 destCell, int flightTicks)
        {
            PawnTrampler trampler = (PawnTrampler)ThingMaker.MakeThing(tramplerDef);
            trampler.startVec = pawn.TrueCenter();
            trampler.Rotation = pawn.Rotation;
            trampler.destCell = destCell;
            trampler.ticksFlightTime = Mathf.Max(flightTicks, 1);
            trampler.pawnWasDrafted = pawn.Drafted;

            if (pawn.drafter != null)
            {
                trampler.pawnCanFireAtWill = pawn.drafter.FireAtWill;
            }

            if (pawn.Spawned)
            {
                pawn.DeSpawn(DestroyMode.WillReplace);
            }

            if (!trampler.innerContainer.TryAdd(pawn))
            {
                Log.Error("Could not add " + pawn.ToStringSafe() + " to a queen trampler.");
                pawn.Destroy();
            }

            return trampler;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref startVec, "startVec");
            Scribe_Values.Look(ref destCell, "destCell");
            Scribe_Values.Look(ref pawnWasDrafted, "pawnWasDrafted", defaultValue: false);
            Scribe_Values.Look(ref pawnCanFireAtWill, "pawnCanFireAtWill", defaultValue: true);
            Scribe_Values.Look(ref ticksFlightTime, "ticksFlightTime", 90);
            Scribe_Values.Look(ref ticksFlying, "ticksFlying", 0);
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
        }
    }
}
