using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    public class GameComponent_CorpseArtProgress : GameComponent
    {
        private Dictionary<int, float> workDoneByThingId = new Dictionary<int, float>();
        private int nextCleanupTick;

        public GameComponent_CorpseArtProgress(Game game)
        {
        }

        public float WorkDone(Thing thing)
        {
            if (thing == null)
            {
                return 0f;
            }

            return workDoneByThingId.TryGetValue(thing.thingIDNumber, out float workDone) ? workDone : 0f;
        }

        public void SetWorkDone(Thing thing, float workDone)
        {
            if (thing == null)
            {
                return;
            }

            if (workDone <= 0f)
            {
                workDoneByThingId.Remove(thing.thingIDNumber);
                return;
            }

            workDoneByThingId[thing.thingIDNumber] = workDone;
        }

        public void Clear(Thing thing)
        {
            if (thing != null)
            {
                workDoneByThingId.Remove(thing.thingIDNumber);
            }
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            int ticksGame = Find.TickManager.TicksGame;
            if (ticksGame < nextCleanupTick)
            {
                return;
            }

            nextCleanupTick = ticksGame + 2500;
            PruneMissingTargets();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref workDoneByThingId, "workDoneByThingId", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref nextCleanupTick, "nextCleanupTick", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && workDoneByThingId == null)
            {
                workDoneByThingId = new Dictionary<int, float>();
            }
        }

        private void PruneMissingTargets()
        {
            if (workDoneByThingId.Count == 0 || Find.Maps == null)
            {
                return;
            }

            HashSet<int> spawnedThingIds = new HashSet<int>();
            foreach (Map map in Find.Maps)
            {
                foreach (Thing thing in map.listerThings.AllThings)
                {
                    spawnedThingIds.Add(thing.thingIDNumber);
                }
            }

            List<int> missingThingIds = null;
            foreach (int thingId in workDoneByThingId.Keys)
            {
                if (!spawnedThingIds.Contains(thingId))
                {
                    if (missingThingIds == null)
                    {
                        missingThingIds = new List<int>();
                    }

                    missingThingIds.Add(thingId);
                }
            }

            if (missingThingIds == null)
            {
                return;
            }

            foreach (int thingId in missingThingIds)
            {
                workDoneByThingId.Remove(thingId);
            }
        }
    }
}
