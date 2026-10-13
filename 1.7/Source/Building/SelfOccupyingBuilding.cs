using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public abstract class SelfOccupyingBuilding : Building_CryptosleepCasket
    {
        private const int FallbackGraceTicks = 2;

        private Pawn pendingOccupant;
        private bool hasExplicitOccupantAssignment;
        private int fallbackAttemptTick = -1;

        [Unsaved(false)]
        private bool emptyAfterLoad;

        protected virtual bool DestroyWhenEmptyAfterLoad => false;
        protected virtual bool HasFallbackOccupant => false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref pendingOccupant, "pendingOccupant");
            Scribe_Values.Look(ref hasExplicitOccupantAssignment, "hasExplicitOccupantAssignment", false);
            Scribe_Values.Look(ref fallbackAttemptTick, "fallbackAttemptTick", -1);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (ContainedThing is Pawn occupant)
            {
                pendingOccupant = null;
                hasExplicitOccupantAssignment = true;
                Notify_OccupantRegistered(occupant);
                return;
            }

            bool hadScheduledFallback = fallbackAttemptTick >= 0;
            emptyAfterLoad = respawningAfterLoad && !hadScheduledFallback;

            if (fallbackAttemptTick < 0)
            {
                fallbackAttemptTick = Find.TickManager.TicksGame + FallbackGraceTicks;
            }
        }

        public override void Open()
        {
            base.Open();
            Destroy();
        }

        public override bool Accepts(Thing thing)
        {
            return ContainedThing == null && thing is Pawn pawn && CanOccupy(pawn);
        }

        public override bool TryAcceptThing(Thing thing, bool allowSpecialEffects = true)
        {
            if (!Accepts(thing) || !base.TryAcceptThing(thing, allowSpecialEffects))
            {
                return false;
            }

            if (thing is Pawn pawn)
            {
                pendingOccupant = null;
                hasExplicitOccupantAssignment = true;
                fallbackAttemptTick = -1;
                Notify_OccupantRegistered(pawn);
            }

            return true;
        }

        public bool TryAssignOccupant(Pawn pawn)
        {
            hasExplicitOccupantAssignment = true;
            fallbackAttemptTick = -1;

            if (!CanOccupy(pawn) || (ContainedThing != null && ContainedThing != pawn))
            {
                pendingOccupant = null;
                return false;
            }

            pendingOccupant = pawn;
            return TryContainPendingOccupant();
        }

        public void Notify_ConstructionCompleted(Pawn constructor)
        {
            hasExplicitOccupantAssignment = true;
            fallbackAttemptTick = -1;
            pendingOccupant = CanOccupy(constructor) ? constructor : null;

            if (pendingOccupant == null && Prefs.DevMode)
            {
                Log.Warning(def.defName + " was completed by an invalid self-occupant: " + constructor);
            }
        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            TickOccupantAssignment();
            if (!Destroyed && ContainedThing != null)
            {
                TickSelfOccupyingBuilding(delta);
            }
        }

        protected virtual void TickSelfOccupyingBuilding(int delta)
        {
        }

        protected virtual bool CanOccupy(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && XMTUtility.IsXenomorph(pawn);
        }

        protected virtual Pawn GenerateFallbackOccupant()
        {
            return null;
        }

        protected virtual void Notify_OccupantRegistered(Pawn pawn)
        {
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (Faction == null || !Faction.IsPlayer)
            {
                yield break;
            }

            yield return new Command_Action
            {
                action = Open,
                defaultLabel = "CommandPodEject".Translate(),
                defaultDesc = "CommandPodEjectDesc".Translate(),
                hotKey = KeyBindingDefOf.Misc8,
                icon = ContentFinder<Texture2D>.Get("UI/Abilities/Starbeast_Leap")
            };
        }

        private void TickOccupantAssignment()
        {
            if (ContainedThing != null)
            {
                pendingOccupant = null;
                fallbackAttemptTick = -1;
                return;
            }

            if (pendingOccupant != null)
            {
                if (!CanOccupy(pendingOccupant))
                {
                    pendingOccupant = null;
                    DestroyEmptyAssignedBuilding();
                    return;
                }

                TryContainPendingOccupant();
                return;
            }

            if (hasExplicitOccupantAssignment)
            {
                DestroyEmptyAssignedBuilding();
                return;
            }

            if (emptyAfterLoad && DestroyWhenEmptyAfterLoad)
            {
                DestroyEmptyAssignedBuilding();
                return;
            }

            if (!HasFallbackOccupant || fallbackAttemptTick < 0 || Find.TickManager.TicksGame < fallbackAttemptTick)
            {
                return;
            }

            Pawn fallback = GenerateFallbackOccupant();
            if (fallback == null)
            {
                fallbackAttemptTick = -1;
                return;
            }

            if (Faction != null && fallback.Faction != Faction)
            {
                fallback.SetFaction(Faction);
            }

            hasExplicitOccupantAssignment = true;
            pendingOccupant = fallback;
            fallbackAttemptTick = -1;
            TryContainPendingOccupant();
        }

        private bool TryContainPendingOccupant()
        {
            Pawn pawn = pendingOccupant;
            if (!CanOccupy(pawn) || (ContainedThing != null && ContainedThing != pawn))
            {
                return false;
            }

            Map previousMap = pawn.Map;
            IntVec3 previousPosition = pawn.Position;
            bool wasSpawned = pawn.Spawned;

            if (wasSpawned && !pawn.DeSpawnOrDeselect())
            {
                return false;
            }

            if (TryAcceptThing(pawn, allowSpecialEffects: false) && ContainedThing == pawn)
            {
                Find.Selector.Select(this, playSound: false, forceDesignatorDeselect: false);
                return true;
            }

            if (!pawn.Spawned && GetDirectlyHeldThings().TryAddOrTransfer(pawn, canMergeWithExistingStacks: false))
            {
                pendingOccupant = null;
                hasExplicitOccupantAssignment = true;
                Notify_OccupantRegistered(pawn);
                Find.Selector.Select(this, playSound: false, forceDesignatorDeselect: false);
                return true;
            }

            if (wasSpawned && !pawn.Spawned && previousMap != null)
            {
                IntVec3 respawnCell = previousPosition.IsValid && previousPosition.InBounds(previousMap) && previousPosition.Standable(previousMap)
                    ? previousPosition
                    : CellFinder.RandomClosewalkCellNear(previousMap.Center, previousMap, 2);
                GenSpawn.Spawn(pawn, respawnCell, previousMap, WipeMode.VanishOrMoveAside);
            }

            return false;
        }

        private void DestroyEmptyAssignedBuilding()
        {
            if (Spawned && !Destroyed)
            {
                Destroy();
            }
        }
    }
}
