using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace Xenomorphtype
{
    public class PawnClimber : Thing, IThingHolder
    {
        private ThingOwner<Thing> innerContainer;
        
        public bool underground = false;

        protected Vector3 startVec;

        private IntVec3 destCell;

        private float flightDistance;

        private bool pawnWasDrafted;

        private bool pawnCanFireAtWill = true;

        protected int ticksFlightTime = 120;

        protected int ticksClimbing;

        private JobQueue jobQueue;

        protected EffecterDef climbEffectorDef;

        protected SoundDef soundLanding;

        private Thing carriedThing;

        private LocalTargetInfo target;

        private AbilityDef triggeringAbility;

        private Effecter climbEffector;

        private int positionLastComputedTick = -1;

        private Vector3 groundPos;

        private Vector3 effectivePos;

        private float effectiveHeight;
        private Job preClimbJob;
        private JobDriver preJobDriver;

        protected Thing ClimbingThing
        {
            get
            {
                if (innerContainer.InnerListForReading.Count <= 0)
                {
                    return null;
                }

                return innerContainer.InnerListForReading[0];
            }
        }

        public Pawn ClimbingPawn => ClimbingThing as Pawn;

        public Thing CarriedThing => carriedThing;

        public override Vector3 DrawPos
        {
            get
            {
                RecomputePosition();
                return effectivePos;
            }
        }

        public Vector3 DestinationPos
        {
            get
            {
                Thing flyingThing = ClimbingThing;
                return GenThing.TrueCenter(destCell, flyingThing.Rotation, flyingThing.def.size, flyingThing.def.Altitude);
            }
        }

       
        private void RecomputePosition()
        {
            if (positionLastComputedTick != ticksClimbing && ClimbingThing != null)
            {
                positionLastComputedTick = ticksClimbing;
                float totalTime = (float)ticksClimbing / (float)20;
                float t = (float)ticksClimbing / (float)ticksFlightTime;
                float t2 = def.pawnFlyer.Worker.AdjustedProgress(t);
                float height = def.pawnFlyer.Worker.GetHeight(t2);
                groundPos = Vector3.Lerp(startVec, DestinationPos, t2);

                if (height < 0.985f)
                {

                    effectiveHeight = underground? -1: height;
                    //groundPos += new Vector3(Mathf.Sin(totalTime), 0, 0);
                }
                else
                {
                    effectiveHeight = -100;
                    groundPos += new Vector3(Mathf.Sin(groundPos.x)*0.5f, 0, Mathf.Cos(groundPos.z)*0.5f);
                }

                
                Vector3 vector = Altitudes.AltIncVect * effectiveHeight;
                Vector3 vector2 = Vector3.forward * (def.pawnFlyer.heightFactor * effectiveHeight);
                effectivePos = groundPos + vector + vector2;
                base.Position = groundPos.ToIntVec3();
            }
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        public PawnClimber()
        {
            innerContainer = new ThingOwner<Thing>(this);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            climbEffector?.Cleanup();
            base.Destroy(mode);
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                float a = Mathf.Max(flightDistance, 1f) / def.pawnFlyer.flightSpeed;
                a = Mathf.Max(a, def.pawnFlyer.flightDurationMin);
                ticksFlightTime = a.SecondsToTicks();
                ticksClimbing = 0;
            }
        }

        protected virtual void RespawnPawn()
        {
            Thing flyingThing = ClimbingThing;
            LandingEffects();
            innerContainer.TryDrop(flyingThing, destCell, flyingThing.MapHeld, ThingPlaceMode.Direct, out var lastResultingThing, null, null, playDropSound: false);
            Pawn pawn = flyingThing as Pawn;
            if (pawn?.drafter != null)
            {
                pawn.drafter.Drafted = pawnWasDrafted;
                pawn.drafter.FireAtWill = pawnCanFireAtWill;
            }

            flyingThing.Rotation = base.Rotation;
            if (carriedThing != null && innerContainer.TryDrop(carriedThing, destCell, flyingThing.MapHeld, ThingPlaceMode.Direct, out lastResultingThing, null, null, playDropSound: false) && pawn != null)
            {
                carriedThing.DeSpawn();
                if (!pawn.carryTracker.TryStartCarry(carriedThing))
                {
                    Log.Error("Could not carry " + carriedThing.ToStringSafe() + " after respawning flyer pawn.");
                }
            }

            if (pawn == null)
            {
                return;
            }

            if (preClimbJob != null)
            {
                pawn.jobs.curJob = preClimbJob;
                pawn.jobs.curDriver = preJobDriver;
            }
            else
            {
                pawn.jobs.CheckForJobOverride();
            }

            if (def.pawnFlyer.stunDurationTicksRange != IntRange.Zero)
            {
                pawn.stances.stunner.StunFor(def.pawnFlyer.stunDurationTicksRange.RandomInRange, null, addBattleLog: false, showMote: false);
            }

            if (triggeringAbility == null)
            {
                return;
            }

            Ability ability = pawn.abilities.GetAbility(triggeringAbility);
            if (ability?.comps == null)
            {
                return;
            }

            foreach (AbilityComp comp in ability.comps)
            {
                if (comp is ICompAbilityEffectOnJumpCompleted compAbilityEffectOnJumpCompleted)
                {
                    compAbilityEffectOnJumpCompleted.OnJumpCompleted(startVec.ToIntVec3(), target);
                }
            }
        }

        private void LandingEffects()
        {
            soundLanding?.PlayOneShot(new TargetInfo(Position, Map));
            FleckMaker.ThrowDustPuff(DestinationPos + Gen.RandomHorizontalVector(0.5f), Map, 2f);

            if(!underground)
            {
               if(Map.roofGrid.RoofAt(Position) == RoofDefOf.RoofRockThick)
               {
                    Map.roofGrid.SetRoof(Position, RoofDefOf.RoofRockThin);
               }

               RoofCollapserImmediate.DropRoofInCells(Position, Map);
            }
        }

        protected override void Tick()
        {
            if (climbEffector == null && climbEffectorDef != null)
            {
                climbEffector = climbEffectorDef.Spawn();
                climbEffector.Trigger(this, TargetInfo.Invalid);
            }
            else if(climbEffector != null)
            {
                climbEffector?.EffectTick(this, TargetInfo.Invalid);
            }

            if (ticksClimbing >= ticksFlightTime)
            {
                Debug.Log("Arrived!");
                RespawnPawn();
                Destroy();
            }
            else
            {
                if (ticksClimbing % 5 == 0)
                {
                    CheckDestination();
                }

            }

            ticksClimbing++;
        }

        private void CheckDestination()
        {
            if (JumpUtility.ValidJumpTarget(ClimbingPawn, base.Map, destCell))
            {
                return;
            }

            int num = GenRadial.NumCellsInRadius(3.9f);
            for (int i = 0; i < num; i++)
            {
                IntVec3 cell = destCell + GenRadial.RadialPattern[i];
                if (JumpUtility.ValidJumpTarget(ClimbingPawn, base.Map, cell))
                {
                    destCell = cell;
                    break;
                }
            }
        }

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            RecomputePosition();

            if (underground)
            {
                base.DynamicDrawPhaseAt(phase, drawLoc, flip);
                return;
            }

            if (ClimbingPawn != null)
            {
                ClimbingPawn.DynamicDrawPhaseAt(phase, effectivePos);
            }
            else
            {
                ClimbingThing?.DynamicDrawPhaseAt(phase, effectivePos);
            }

            base.DynamicDrawPhaseAt(phase, drawLoc, flip);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            DrawShadow(groundPos, effectiveHeight);
            if (CarriedThing != null && ClimbingPawn != null)
            {
                PawnRenderUtility.DrawCarriedThing(ClimbingPawn, effectivePos, CarriedThing);
            }
        }

        private void DrawShadow(Vector3 drawLoc, float height)
        {
            if(underground)
            {
                return;
            }

            Material shadowMaterial = def.pawnFlyer.ShadowMaterial;
            if (!(shadowMaterial == null))
            {
                float num = Mathf.Lerp(1f, 0.6f, height);
                Vector3 s = new Vector3(num, 1f, num);
                Matrix4x4 matrix = default(Matrix4x4);
                matrix.SetTRS(drawLoc, Quaternion.identity, s);
                Graphics.DrawMesh(MeshPool.plane10, matrix, shadowMaterial, 0);
            }
        }

        public static PawnClimber MakeClimber(ThingDef flyingDef, Pawn pawn, IntVec3 destCell, EffecterDef flightEffecterDef, SoundDef landingSound, bool flyWithCarriedThing = false, Vector3? overrideStartVec = null, Ability triggeringAbility = null, LocalTargetInfo target = default(LocalTargetInfo))
        {
            PawnClimber pawnClimber = (PawnClimber)ThingMaker.MakeThing(flyingDef);
            pawnClimber.startVec = overrideStartVec ?? pawn.TrueCenter();
            pawnClimber.Rotation = pawn.Rotation;
            pawnClimber.flightDistance = pawn.Position.DistanceTo(destCell);
            pawnClimber.destCell = destCell;
            pawnClimber.pawnWasDrafted = pawn.Drafted;
            pawnClimber.climbEffectorDef = flightEffecterDef;
            pawnClimber.soundLanding = landingSound;
            pawnClimber.triggeringAbility = triggeringAbility?.def;
            pawnClimber.target = target;
            
            if (pawn.drafter != null)
            {
                pawnClimber.pawnCanFireAtWill = pawn.drafter.FireAtWill;
            }

            if (pawn.CurJob != null)
            {
                Log.Message(pawn + " has job " + pawn.CurJob);
                pawnClimber.preClimbJob = pawn.CurJob;
                pawnClimber.preJobDriver = pawn.jobs.curDriver;
                
                pawn.jobs.curJob = null;
                pawn.jobs.curDriver = null;
            }

            pawnClimber.jobQueue = pawn.jobs.CaptureAndClearJobQueue();
            if (flyWithCarriedThing && pawn.carryTracker.CarriedThing != null && pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Direct, out pawnClimber.carriedThing))
            {
                if (pawnClimber.carriedThing.holdingOwner != null)
                {
                    pawnClimber.carriedThing.holdingOwner.Remove(pawnClimber.carriedThing);
                }

                pawnClimber.carriedThing.DeSpawn();
            }

            if (pawn.Spawned)
            {
                pawn.DeSpawn(DestroyMode.WillReplace);
            }

            if (!pawnClimber.innerContainer.TryAdd(pawn))
            {
                Log.Error("Could not add " + pawn.ToStringSafe() + " to a climber.");
                pawn.Destroy();
            }

            if (pawnClimber.carriedThing != null && !pawnClimber.innerContainer.TryAdd(pawnClimber.carriedThing))
            {
                Log.Error("Could not add " + pawnClimber.carriedThing.ToStringSafe() + " to a climber.");
            }

            return pawnClimber;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref startVec, "startVec");
            Scribe_Values.Look(ref destCell, "destCell");
            Scribe_Values.Look(ref flightDistance, "flightDistance", 0f);
            Scribe_Values.Look(ref pawnWasDrafted, "pawnWasDrafted", defaultValue: false);
            Scribe_Values.Look(ref pawnCanFireAtWill, "pawnCanFireAtWill", defaultValue: true);
            Scribe_Values.Look(ref ticksFlightTime, "ticksFlightTime", 0);
            Scribe_Values.Look(ref ticksClimbing, "ticksFlying", 0);
            Scribe_Defs.Look(ref climbEffectorDef, "flightEffecterDef");
            Scribe_Defs.Look(ref soundLanding, "soundLanding");
            Scribe_Defs.Look(ref triggeringAbility, "triggeringAbility");
            Scribe_References.Look(ref carriedThing, "carriedThing");
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_Deep.Look(ref jobQueue, "jobQueue");
            Scribe_TargetInfo.Look(ref target, "target");
        }
    }
}
