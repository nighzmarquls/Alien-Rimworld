using System;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    // Own failed self-containment transfers before attempting placement. References alone cannot save an orphan pawn.
    public sealed class GameComponent_SelfOccupantRecovery : GameComponent, IThingHolder, ISuspendableThingHolder
    {
        private const int RetryInterval = 600;
        private ThingOwner<Pawn> contents;
        private List<RecoveryLocation> locations = new List<RecoveryLocation>();
        private int nextRetryTick;

        public GameComponent_SelfOccupantRecovery(Game game)
        {
            contents = new ThingOwner<Pawn>(this) { dontTickContents = true };
        }

        public IThingHolder ParentHolder => null;
        public bool IsContentsSuspended => true;
        public ThingOwner GetDirectlyHeldThings() => contents;
        public void GetChildHolders(List<IThingHolder> outChildren) => ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, contents);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref contents, "selfOccupantRecoveryContents", this);
            Scribe_Collections.Look(ref locations, "selfOccupantRecoveryLocations", LookMode.Deep);
            Scribe_Values.Look(ref nextRetryTick, "selfOccupantRecoveryTick", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                contents ??= new ThingOwner<Pawn>(this);
                contents.dontTickContents = true;
                locations ??= new List<RecoveryLocation>();
            }
        }

        public void Recover(Pawn pawn, Map map, IntVec3 cell)
        {
            if (pawn == null || pawn.Destroyed || pawn.Spawned || pawn.holdingOwner != null)
            {
                return;
            }

            if (!contents.TryAdd(pawn, canMergeWithExistingStacks: false))
            {
                Log.Error("[XMT] Could not secure failed self-occupant " + pawn + " for recovery.");
                return;
            }

            RecoveryLocation location = new RecoveryLocation { pawn = pawn, map = map, cell = cell };
            locations.Add(location);
            if (locations.Count == 1)
            {
                nextRetryTick = Find.TickManager.TicksGame + RetryInterval;
            }
            if (TryRestore(location))
            {
                locations.Remove(location);
                Log.Warning("[XMT] Restored " + pawn + " after failed self-containment.");
            }
            else
            {
                Log.Warning("[XMT] Preserving " + pawn + " in saved emergency custody after failed self-containment; placement will retry every " + RetryInterval + " ticks.");
            }
        }

        public override void GameComponentTick()
        {
            if (locations.Count == 0 || Find.TickManager.TicksGame < nextRetryTick)
            {
                return;
            }

            nextRetryTick = Find.TickManager.TicksGame + RetryInterval;
            for (int i = locations.Count - 1; i >= 0; i--)
            {
                if (TryRestore(locations[i]))
                {
                    Log.Warning("[XMT] Completed emergency self-occupant recovery for " + locations[i].pawn + ".");
                    locations.RemoveAt(i);
                }
            }
        }

        private bool TryRestore(RecoveryLocation location)
        {
            Pawn pawn = location.pawn;
            if (pawn == null || pawn.Destroyed || pawn.Spawned || pawn.holdingOwner != contents)
            {
                return true;
            }

            Map map = location.map;
            // Do not silently relocate a pawn to a different map if its original map was removed.
            if (map == null || !Find.Maps.Contains(map))
            {
                return false;
            }

            try
            {
                IntVec3 origin = location.cell.InBounds(map) ? location.cell : map.Center;
                contents.TryDrop(pawn, origin, map, ThingPlaceMode.Near, out _, playDropSound: false);
                if (!pawn.Spawned)
                {
                    // One bounded, wider fallback per retry; no map scan or per-tick placement search.
                    contents.TryDrop(pawn, CellFinder.RandomCell(map), map, ThingPlaceMode.Near, out _, playDropSound: false);
                }
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[XMT] Exception restoring self-occupant " + pawn + ": " + exception, pawn.thingIDNumber ^ 0x51F0CC);
            }
            finally
            {
                // SpawnSetup can throw after GenSpawn removes the pawn from its owner.
                if (!pawn.Spawned && !pawn.Destroyed && pawn.holdingOwner == null)
                {
                    contents.TryAdd(pawn, canMergeWithExistingStacks: false);
                }
            }

            return pawn.Spawned;
        }

        private sealed class RecoveryLocation : IExposable
        {
            public RecoveryLocation() { }

            public Pawn pawn;
            public Map map;
            public IntVec3 cell;

            public void ExposeData()
            {
                Scribe_References.Look(ref pawn, "pawn");
                Scribe_References.Look(ref map, "map");
                Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            }
        }
    }
}
