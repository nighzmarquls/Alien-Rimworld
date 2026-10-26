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

        private IntVec3 plannedDestCell;

        private IntVec3 lastUnresolvedDestinationLogged = IntVec3.Invalid;

        public bool strictDestination;

        private float flightDistance;

        private bool pawnWasDrafted;

        private bool pawnCanFireAtWill = true;

        protected int ticksFlightTime = 120;

        protected int ticksClimbing;

        private Job detachedJob;

        private JobDriver detachedDriver;

        private bool preservingDepartureReservations;
        private bool preservingLandingReservations;

        internal Job DetachedJob => detachedJob;
        internal bool PreservingDepartureReservations => preservingDepartureReservations;
        internal bool PreservesEnrouteReservations(Pawn pawn) => preservingDepartureReservations ||
            preservingLandingReservations || pawn.CurJob?.def == JobDefOf.Carried;

        internal static PawnClimber ReservationOwnerFor(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed)
            {
                return null;
            }

            PawnClimber owner = pawn.ParentHolder as PawnClimber ?? pawn.GetClimberComp()?.pawnClimber;
            return owner != null && !owner.Destroyed && owner.detachedJob != null &&
                owner.detachedDriver?.pawn == pawn ? owner : null;
        }

        internal bool CancelDetachedJob(JobCondition condition)
        {
            Job job = detachedJob;
            JobDriver driver = detachedDriver;
            if (job == null)
            {
                return false;
            }

            // Remove ownership before cleanup so reservation patches allow release.
            detachedJob = null;
            detachedDriver = null;
            Pawn pawn = driver?.pawn ?? ClimbingPawn;
            pawn?.ClearReservationsForJob(job);
            if (driver != null)
            {
                driver.ended = true;
                driver.Cleanup(condition);
            }
            pawn?.GetClimberComp()?.ClearClimberData();
            if (XMTSettings.LogClimbing)
            {
                Log.Message("[XMT][Climbing] " + pawn + " cancelled suspended traversal job " + job +
                    " with " + condition + "; released its reservations.");
            }
            return true;
        }

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
            Pawn pawn = detachedDriver?.pawn ?? ClimbingPawn;
            CancelDetachedJob(JobCondition.InterruptForced);
            if (pawn != null && jobQueue != null)
            {
                jobQueue.Clear(pawn, canReturnToPool: true);
                jobQueue = null;
            }
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
            else
            {
                RestoreClimberCompLink();
            }
        }

        private void RestoreClimberCompLink()
        {
            Pawn pawn = ClimbingPawn;
            CompClimber climber = pawn?.GetClimberComp();
            if (climber != null && climber.pawnClimber != this)
            {
                climber.RestoreLoadedClimber(this, detachedJob);
            }
        }

        private void RestoreDetachedDriverLinks()
        {
            if (detachedDriver == null)
            {
                return;
            }

            detachedDriver.pawn = ClimbingPawn;
            detachedDriver.job = detachedJob;
            XMTClimbPatches.RestoreDetachedClimbToil(detachedDriver, detachedJob);
        }

        protected virtual void RespawnPawn()
        {
            Thing flyingThing = ClimbingThing;
            Pawn pawn = flyingThing as Pawn;
            if (carriedThing != null && pawn != null)
            {
                // Keep the exact job target out of the map's stack-merging path.
                if (!innerContainer.TryTransferToContainer(carriedThing,
                    pawn.carryTracker.GetDirectlyHeldThings(), canMergeWithExistingStacks: false))
                {
                    Log.ErrorOnce("Could not return traversal cargo " + carriedThing.ToStringSafe() +
                        " to " + pawn + "; retaining the pawn and cargo in the climber.", thingIDNumber);
                    return;
                }
                if (XMTSettings.LogClimbing)
                {
                    Log.Message("[XMT][Climbing] " + pawn + " restored traversal cargo " + carriedThing +
                        " directly to the carry tracker; current cargo=" + pawn.carryTracker.CarriedThing);
                }
                carriedThing = null;
            }
            LandingEffects();
            bool droppedPawn = innerContainer.TryDrop(flyingThing, destCell, flyingThing.MapHeld, ThingPlaceMode.Direct, out var lastResultingThing, null, null, playDropSound: false);
            if (XMTSettings.LogClimbing)
            {
                Log.Message("[XMT][Climbing] " + pawn + " completed climb flyer drop; originally planned landing=" + plannedDestCell +
                    "; resolved landing=" + destCell + "; drop succeeded=" + droppedPawn +
                    "; actual position=" + (lastResultingThing?.PositionHeld ?? IntVec3.Invalid) +
                    "; spawned=" + (lastResultingThing?.Spawned ?? false));
            }
            if (!droppedPawn)
            {
                return;
            }
            ClimbUtility.NotifyTraversalProgress(pawn);
            if (pawn?.drafter != null)
            {
                pawn.drafter.Drafted = pawnWasDrafted;
                pawn.drafter.FireAtWill = pawnCanFireAtWill;
            }

            flyingThing.Rotation = base.Rotation;
            if (pawn == null)
            {
                return;
            }

            CompClimber climber = pawn.GetClimberComp();
            if (climber != null && XMTSettings.LogClimbing)
            {
                Log.Message("[XMT][Climbing] " + pawn + " restoring detached climb job after landing; detached job=" + detachedJob +
                    "; detached driver=" + detachedDriver + "; temporary job=" + pawn.jobs?.curJob +
                    "; captured queue=" + (jobQueue != null) + "; final target=" + climber.climbParameters.FinalGoalTarget +
                    "; route registered=" + climber.climbParameters.ClimbCellsRegistered);
            }

            preservingLandingReservations = true;
            try
            {
                if (pawn.jobs.curJob != null && pawn.jobs.curJob != detachedJob)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false, canReturnToPool: true);
                }

                if (pawn.jobs.jobQueue != null && pawn.jobs.jobQueue != jobQueue)
                {
                    pawn.jobs.jobQueue.Clear(pawn, canReturnToPool: true);
                }
            }
            finally
            {
                preservingLandingReservations = false;
            }

            pawn.jobs.jobQueue = jobQueue ?? new JobQueue();
            jobQueue = null;

            // Hauling's initial reservations remain valid at pickup and delivery.
            // Recheck them quietly for stolen/destroyed targets and older mid-flight saves.
            // Other drivers may consume or repurpose initial targets; do not rerun them.
            if (detachedJob?.def == JobDefOf.HaulToCell &&
                detachedDriver?.GetType() == typeof(JobDriver_HaulToCell) &&
                !detachedDriver.TryMakePreToilReservations(errorOnFailed: false))
            {
                CancelDetachedJob(JobCondition.Incompletable);
            }

            if (detachedJob != null && detachedDriver != null)
            {
                pawn.jobs.curJob = detachedJob;
                pawn.jobs.curDriver = detachedDriver;
                detachedDriver.pawn = pawn;
                detachedDriver.job = detachedJob;
                XMTClimbPatches.RestoreDetachedClimbToil(detachedDriver, detachedJob);
            }
            else
            {
                pawn.jobs.curJob = null;
                pawn.jobs.curDriver = null;
                pawn.jobs.CheckForJobOverride(0f, ignoreQueue: false);
            }

            detachedJob = null;
            detachedDriver = null;

            if (climber != null && XMTSettings.LogClimbing)
            {
                Log.Message("[XMT][Climbing] " + pawn + " restored detached climb job after landing; current job=" + pawn.jobs?.curJob +
                    "; driver=" + pawn.jobs?.curDriver);
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
        }

        protected override void Tick()
        {
            RestoreClimberCompLink();

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
                RespawnPawn();
                if (ClimbingPawn == null)
                {
                    Destroy();
                }
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

            if (strictDestination)
            {
                if (XMTSettings.LogClimbing && lastUnresolvedDestinationLogged != destCell)
                {
                    lastUnresolvedDestinationLogged = destCell;
                    Log.Message("[XMT][Climbing] " + ClimbingPawn + " has an invalid strict infiltration landing at " + destCell +
                        "; radial climb landing correction is disabled for this traversal.");
                }
                return;
            }

            IntVec3 rejectedDestination = destCell;
            int num = GenRadial.NumCellsInRadius(3.9f);
            for (int i = 0; i < num; i++)
            {
                IntVec3 cell = destCell + GenRadial.RadialPattern[i];
                if (JumpUtility.ValidJumpTarget(ClimbingPawn, base.Map, cell))
                {
                    destCell = cell;
                    lastUnresolvedDestinationLogged = IntVec3.Invalid;
                    if (XMTSettings.LogClimbing)
                    {
                        Log.Message("[XMT][Climbing] " + ClimbingPawn + " adjusted climb landing from rejected cell " + rejectedDestination + " to " + destCell +
                            "; originally planned landing=" + plannedDestCell + "; flyer position=" + Position);
                    }
                    return;
                }
            }

            if (XMTSettings.LogClimbing && lastUnresolvedDestinationLogged != rejectedDestination)
            {
                lastUnresolvedDestinationLogged = rejectedDestination;
                Log.Message("[XMT][Climbing] " + ClimbingPawn + " could not find a valid replacement climb landing within 3.9 cells of " + rejectedDestination +
                    "; originally planned landing=" + plannedDestCell + "; flyer position=" + Position);
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
            if (!underground && CarriedThing != null && ClimbingPawn != null)
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
            pawnClimber.plannedDestCell = destCell;
            pawnClimber.pawnWasDrafted = pawn.Drafted;
            pawnClimber.climbEffectorDef = flightEffecterDef;
            pawnClimber.soundLanding = landingSound;
            pawnClimber.triggeringAbility = triggeringAbility?.def;
            pawnClimber.target = target;
            
            if (pawn.drafter != null)
            {
                pawnClimber.pawnCanFireAtWill = pawn.drafter.FireAtWill;
            }

            pawnClimber.detachedJob = pawn.jobs.curJob;
            pawnClimber.detachedDriver = pawn.jobs.curDriver;
            // Expose the owner before despawn invokes reservation cleanup.
            pawn.GetClimberComp().pawnClimber = pawnClimber;
            pawnClimber.jobQueue = pawn.jobs.jobQueue;
            pawn.jobs.curJob = null;
            pawn.jobs.curDriver = null;
            pawn.jobs.jobQueue = new JobQueue();

            if (pawn.Spawned)
            {
                pawnClimber.preservingDepartureReservations = true;
                try
                {
                    pawn.DeSpawn(DestroyMode.WillReplace);
                }
                finally
                {
                    pawnClimber.preservingDepartureReservations = false;
                }
            }

            if (!pawnClimber.innerContainer.TryAdd(pawn))
            {
                Log.Error("Could not add " + pawn.ToStringSafe() + " to a climber.");
                pawn.Destroy();
            }

            // Insert the pawn first: ClimbingThing relies on it being the first entry.
            Thing cargo = flyWithCarriedThing ? pawn.carryTracker.CarriedThing : null;
            if (cargo != null && pawn.carryTracker.GetDirectlyHeldThings().TryTransferToContainer(cargo,
                pawnClimber.innerContainer, canMergeWithExistingStacks: false))
            {
                pawnClimber.carriedThing = cargo;
                if (XMTSettings.LogClimbing)
                {
                    Log.Message("[XMT][Climbing] " + pawn + " transferred traversal cargo " + cargo +
                        " directly into the climber; detached job=" + pawnClimber.detachedJob);
                }
            }
            else if (cargo != null)
            {
                // A failed transfer leaves the item in the held pawn's carry container.
                Log.Error("Could not transfer traversal cargo " + cargo.ToStringSafe() +
                    " into the climber; retaining it in " + pawn + "'s carry tracker.");
            }

            return pawnClimber;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref startVec, "startVec");
            Scribe_Values.Look(ref destCell, "destCell");
            Scribe_Values.Look(ref plannedDestCell, "plannedDestCell", IntVec3.Invalid);
            Scribe_Values.Look(ref strictDestination, "strictDestination", false);
            Scribe_Values.Look(ref flightDistance, "flightDistance", 0f);
            Scribe_Values.Look(ref underground, "underground", false);
            Scribe_Values.Look(ref pawnWasDrafted, "pawnWasDrafted", defaultValue: false);
            Scribe_Values.Look(ref pawnCanFireAtWill, "pawnCanFireAtWill", defaultValue: true);
            Scribe_Values.Look(ref ticksFlightTime, "ticksFlightTime", 0);
            Scribe_Values.Look(ref ticksClimbing, "ticksFlying", 0);
            Scribe_Defs.Look(ref climbEffectorDef, "flightEffecterDef");
            Scribe_Defs.Look(ref soundLanding, "soundLanding");
            Scribe_Defs.Look(ref triggeringAbility, "triggeringAbility");
            Scribe_References.Look(ref carriedThing, "carriedThing");
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_Deep.Look(ref detachedJob, "detachedJob");
            Scribe_Deep.Look(ref detachedDriver, "detachedDriver");
            Scribe_Deep.Look(ref jobQueue, "jobQueue");
            Scribe_TargetInfo.Look(ref target, "target");

            // JobDriver.ExposeData runs SetupToils during PostLoadInit. A detached driver is
            // outside Pawn_JobTracker ownership, so restore the links while loading variables
            // before its own PostLoadInit is allowed to run.
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                RestoreDetachedDriverLinks();
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (!plannedDestCell.IsValid)
                {
                    plannedDestCell = destCell;
                }

                RestoreDetachedDriverLinks();
            }
        }
    }
}
