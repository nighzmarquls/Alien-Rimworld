using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace Xenomorphtype
{
    internal sealed class XenoformingSpreadState : IExposable
    {
        internal List<PlanetTile> frontier = new List<PlanetTile>();
        internal List<PlanetTile> deferred = new List<PlanetTile>();
        internal List<PlanetTile> seeds = new List<PlanetTile>();
        internal List<PlanetTile> hillSeeds = new List<PlanetTile>();
        internal int scanCursor;
        internal int convertedTiles;
        internal int eligibleLandTiles;
        private int initializationVersion = 1;
        internal bool initialized;
        internal bool cycleRequested;
        internal int conversionsRemaining;
        internal int attemptsRemaining;
        internal int deferredToReactivate;
        internal bool collectingSites;
        internal int siteCursor;
        internal bool initialScatterCompleted;
        internal int initialScatterCount;
        internal bool queenIncidentChecked;
        internal bool graphicsDirty;
        internal int nextGraphicsTick;

        // Rebuilt in bounded batches after loading; never serialized as a second frontier.
        [Unsaved(false)] internal HashSet<PlanetTile> membership = new HashSet<PlanetTile>();
        [Unsaved(false)] internal int membershipCursor;
        [Unsaved(false)] internal bool membershipReady;
        [Unsaved(false)] internal List<PlanetTile> neighbors = new List<PlanetTile>();

        public void ExposeData()
        {
            Scribe_Collections.Look(ref frontier, "frontier");
            Scribe_Collections.Look(ref deferred, "deferred");
            Scribe_Collections.Look(ref seeds, "seeds");
            Scribe_Collections.Look(ref hillSeeds, "hillSeeds");
            Scribe_Values.Look(ref scanCursor, "scanCursor");
            Scribe_Values.Look(ref convertedTiles, "convertedTiles");
            Scribe_Values.Look(ref eligibleLandTiles, "eligibleLandTiles");
            Scribe_Values.Look(ref initializationVersion, "initializationVersion", 0);
            Scribe_Values.Look(ref initialized, "initialized");
            Scribe_Values.Look(ref cycleRequested, "cycleRequested");
            Scribe_Values.Look(ref conversionsRemaining, "conversionsRemaining");
            Scribe_Values.Look(ref attemptsRemaining, "attemptsRemaining");
            Scribe_Values.Look(ref deferredToReactivate, "deferredToReactivate");
            Scribe_Values.Look(ref collectingSites, "collectingSites");
            Scribe_Values.Look(ref siteCursor, "siteCursor");
            Scribe_Values.Look(ref initialScatterCompleted, "initialScatterCompleted");
            Scribe_Values.Look(ref initialScatterCount, "initialScatterCount");
            Scribe_Values.Look(ref queenIncidentChecked, "queenIncidentChecked");
            Scribe_Values.Look(ref graphicsDirty, "graphicsDirty");
            Scribe_Values.Look(ref nextGraphicsTick, "nextGraphicsTick");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                frontier ??= new List<PlanetTile>();
                deferred ??= new List<PlanetTile>();
                seeds ??= new List<PlanetTile>();
                hillSeeds ??= new List<PlanetTile>();
                membership = new HashSet<PlanetTile>();
                membershipCursor = 0;
                membershipReady = false;
                neighbors = new List<PlanetTile>();
                if (initializationVersion < 1)
                {
                    // Saves predating world-relative budgets need one bounded land recount.
                    initialized = false;
                    scanCursor = 0;
                    convertedTiles = 0;
                    eligibleLandTiles = 0;
                    cycleRequested = true;
                    initializationVersion = 1;
                }
            }
        }
    }

    public sealed class WorldComponent_Xenoforming : WorldComponent
    {
        internal XenoformingSpreadState biomeSpread = new XenoformingSpreadState();
        internal bool conquestStarted;
        internal bool conquestCompleted;
        internal bool victoryShown;
        internal List<Settlement> conquestSettlements = new List<Settlement>();
        internal List<PlanetTile> conquestHillTiles = new List<PlanetTile>();
        internal bool migratedGameState;
        [Unsaved(false)] internal float timeLeft = -1f;

        public WorldComponent_Xenoforming(World world) : base(world) { }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            Current.Game.GetComponent<GameComponent_Xenomorph>().MigrateWorldState(this);
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();
            GameComponent_Xenomorph game = Current.Game.GetComponent<GameComponent_Xenomorph>();
            XenoformingUtility.BiomeSpreadTick(game);
        }

        public override void WorldComponentUpdate()
        {
            base.WorldComponentUpdate();
            Current.Game.GetComponent<GameComponent_Xenomorph>().UpdateWorldXenoforming();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref biomeSpread, "biomeSpread");
            Scribe_Values.Look(ref conquestStarted, "conquestStarted");
            Scribe_Values.Look(ref conquestCompleted, "conquestCompleted");
            Scribe_Values.Look(ref victoryShown, "victoryShown");
            Scribe_Collections.Look(ref conquestSettlements, "conquestSettlements", LookMode.Reference);
            Scribe_Collections.Look(ref conquestHillTiles, "conquestHillTiles");
            Scribe_Values.Look(ref migratedGameState, "migratedGameState");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                biomeSpread ??= new XenoformingSpreadState();
                conquestSettlements ??= new List<Settlement>();
                conquestHillTiles ??= new List<PlanetTile>();
                timeLeft = -1f;
            }
        }
    }
}
