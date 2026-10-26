using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Xenomorphtype
{
    public sealed class LordJob_NemesisHarvest : LordJob_NemesisMission
    {
        private bool repelled;
        private bool alarmed;
        private bool mapExhausted;
        private int collectionUntil = -1;
        private int nextPartyCheck;
        public override bool Successful => mapExhausted || missionSuccessCount > 0;
        public override bool CovertEnded => alarmed || withdrawing;

        public override void Begin()
        {
            base.Begin();
            EnsureIndex();
        }

        private void EnsureIndex()
        {
            if (!withdrawing) lord.Map.GetComponent<MapComponent_NemesisHarvest>().Begin(this,
                lord.ownedPawns.Where(pawn => pawn.Spawned && !pawn.Dead), EntryCell);
        }

        public override bool AllowThreatResponse(Pawn pawn, Thing aggressor)
        {
            RegisterAggression(aggressor);
            return false;
        }

        public override bool Notify_MissionDamage(Pawn pawn, DamageInfo damage)
        {
            RegisterAggression(damage.Instigator);
            alarmed = true;
            if (withdrawing) collectionUntil = Find.TickManager.TicksGame;
            return false;
        }

        private void RegisterAggression(Thing aggressor)
        {
            if (repelled || aggressor?.Faction?.IsPlayer != true) return;
            repelled = true;
            alarmed = true;
            Current.Game.GetComponent<GameComponent_Nemesis>().RecordMapEvidence(
                DefDatabase<NemesisEvidenceDef>.GetNamed("XMT_Nemesis_HarvestersRepelled"), 1f,
                lord.Map, mission, "harvesting interrupted by player aggression");
        }

        public override void Notify_Revealed(Pawn pawn)
        {
            base.Notify_Revealed(pawn);
            alarmed = true;
        }

        public override void Withdraw(string reason)
        {
            if (withdrawing) return;
            // Notifications only set flags. Base withdrawal is entered from the normal lord tick.
            collectionUntil = Find.TickManager.TicksGame + mission.workerSettings.collectionDurationTicks;
            if (XMTUtility.PlayerFactionIsCryptimorph() || !mission.MapRequirementsMet(lord.Map))
                collectionUntil = Find.TickManager.TicksGame;
            lord.Map.GetComponent<MapComponent_NemesisHarvest>().End(this);
            base.Withdraw(reason);
        }

        protected override void TickMission(List<Pawn> pawns, int tick)
        {
            foreach (Pawn pawn in pawns.Where(pawn => !pawn.Downed && pawn.InMentalState)) pawn.mindState.mentalStateHandler.Reset();
            if (withdrawing)
            {
                if (alarmed && tick >= collectionUntil)
                    foreach (Pawn pawn in pawns.Where(pawn => !pawn.Downed
                        && (pawn.CurJobDef == NemesisHarvestUtility.CollectJob || pawn.CurJobDef == JobDefOf.Ingest)))
                        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: true);
                return;
            }
            EnsureIndex();
            if (alarmed) { Withdraw("harvesters attacked, discovered or exposed to light"); return; }
            if (tick < nextPartyCheck) return;
            nextPartyCheck = tick + 120;
            MapComponent_NemesisHarvest state = lord.Map.GetComponent<MapComponent_NemesisHarvest>();
            if (state.Complete())
            {
                mapExhausted = true;
                Withdraw("map has no remaining conversion targets");
                return;
            }
            if (pawns.Where(pawn => !pawn.Downed).All(pawn => !CanAcceptMore(pawn)))
                Withdraw("party inventories and food needs are full");
        }

        internal static bool CanAcceptMore(Pawn pawn)
        {
            ThingDef jelly = pawn.GetComp<CompJellyMaker>()?.GetJellyProduct();
            return jelly != null && (pawn.inventory != null && MassUtility.Capacity(pawn)
                - MassUtility.GearAndInventoryMass(pawn) >= jelly.GetStatValueAbstract(StatDefOf.Mass)
                || CanEatMore(pawn, jelly));
        }

        private static bool CanEatMore(Pawn pawn, ThingDef jelly)
            => pawn.needs?.food != null && pawn.needs.food.MaxLevel - pawn.needs.food.CurLevel
                >= jelly.GetStatValueAbstract(StatDefOf.Nutrition) && pawn.RaceProps.CanEverEat(jelly);

        protected override Job GetOperationalJob(Pawn pawn)
        {
            NemesisMissionMember member = Member(pawn);
            if (Find.TickManager.TicksGame < member.nextCaptureTick) return Wait();
            member.nextCaptureTick = Find.TickManager.TicksGame + 120;
            Job collect = CollectJelly(pawn);
            if (collect != null) return collect;
            if (!CanAcceptMore(pawn)) return Wait();
            NemesisHarvestIndex index = lord.Map.GetComponent<MapComponent_NemesisHarvest>().Index;
            CompJellyMaker maker = pawn.GetComp<CompJellyMaker>();
            if (index == null || maker == null) return Wait();
            int nearbyPathChecks = 0;
            int fallbackPathChecks = 0;
            if (!member.routeInitialized)
            {
                member.routeIndex = lord.ownedPawns.IndexOf(pawn);
                member.routeInitialized = true;
            }
            int offset = member.routeIndex;
            foreach (IntVec3 cell in index.Candidates(pawn, offset))
            {
                if (!XMTHiveUtility.IsLightSuitableAt(cell, pawn.Map) || Claimed(cell, pawn)
                    || !FeralJobUtility.IsPlaceAvailableForJobBy(pawn, cell)) continue;
                Plant plant = cell.GetPlant(pawn.Map);
                if (plant != null ? !maker.CanMakeIntoJelly(plant)
                    : !CompJellyMaker.TerrainCellEmpty(cell, pawn.Map) || !maker.CanConvertCell(cell)) continue;
                bool nearby = System.Math.Abs(cell.x - pawn.Position.x) <= 2 && System.Math.Abs(cell.z - pawn.Position.z) <= 2;
                if (nearby) { if (nearbyPathChecks++ >= 4) continue; }
                else if (fallbackPathChecks++ >= 4) break;
                LocalTargetInfo target = plant != null ? new LocalTargetInfo(plant) : new LocalTargetInfo(cell);
                if (!ClimbUtility.CanReachByWalkingOrClimb(pawn, target, PathEndMode.Touch, Danger.Deadly)) continue;
                Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_ProduceJelly, target);
                if (!FeralJobUtility.ReservePlaceForJob(pawn, job, cell)) continue;
                if (plant != null) FeralJobUtility.ReserveThingForJob(pawn, job, plant);
                return job;
            }
            member.routeIndex++;
            return Wait();
        }

        private bool Claimed(IntVec3 cell, Pawn pawn) => lord.ownedPawns.Any(other => other != pawn
            && other.CurJobDef == XenoWorkDefOf.XMT_ProduceJelly && other.CurJob.targetA.Cell == cell);

        private Job CollectJelly(Pawn pawn)
        {
            ThingDef jelly = pawn.GetComp<CompJellyMaker>()?.GetJellyProduct();
            if (jelly == null || !CanAcceptMore(pawn)) return null;
            Thing stack = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(jelly),
                PathEndMode.Touch, TraverseParms.For(pawn, Danger.Deadly), mission.workerSettings.localSearchRadius,
                thing => FeralJobUtility.IsThingAvailableForJobBy(pawn, thing)
                    && !lord.ownedPawns.Any(other => other != pawn && other.CurJob?.targetA.Thing == thing));
            if (stack == null) return null;
            int carry = pawn.inventory == null ? 0 : MassUtility.CountToPickUpUntilOverEncumbered(pawn, stack);
            if (carry > 0)
            {
                Job job = JobMaker.MakeJob(NemesisHarvestUtility.CollectJob, stack);
                job.count = System.Math.Min(carry, stack.stackCount);
                return job;
            }
            if (!CanEatMore(pawn, jelly)) return null;
            Job eat = JobMaker.MakeJob(JobDefOf.Ingest, stack);
            eat.count = System.Math.Min(stack.stackCount, FoodUtility.StackCountForNutrition(
                pawn.needs.food.MaxLevel - pawn.needs.food.CurLevel, FoodUtility.GetNutrition(pawn, stack, jelly)));
            return eat;
        }

        protected override Job GetWithdrawalJob(Pawn pawn)
        {
            if (Find.TickManager.TicksGame < collectionUntil)
            {
                NemesisMissionMember member = Member(pawn);
                if (Find.TickManager.TicksGame >= member.nextCaptureTick)
                {
                    member.nextCaptureTick = Find.TickManager.TicksGame + 120;
                    Job collect = CollectJelly(pawn);
                    if (collect != null) return collect;
                }
                // Stay available to share output while another member is collecting it.
                if (lord.ownedPawns.Any(other => other != pawn && other.CurJobDef == NemesisHarvestUtility.CollectJob)) return Wait();
            }
            return NemesisHarvestUtility.ExitJob(pawn, EntryCell) ?? Wait();
        }

        internal void NotifyJellyUsed(int amount) => IncrementMissionSuccess(amount, "jelly consumed or carried off-map");

        public override void Notify_PawnLost(Pawn pawn, PawnLostCondition condition)
        {
            if (condition == PawnLostCondition.ExitedMap && pawn.inventory != null)
            {
                ThingDef jelly = pawn.GetComp<CompJellyMaker>()?.GetJellyProduct();
                if (jelly != null) NotifyJellyUsed(pawn.inventory.innerContainer.TotalStackCountOfDef(jelly));
            }
            base.Notify_PawnLost(pawn, condition);
        }

        public override void Cleanup()
        {
            lord.Map.GetComponent<MapComponent_NemesisHarvest>().End(this);
            base.Cleanup();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref repelled, "harvestersRepelled");
            Scribe_Values.Look(ref alarmed, "harvestAlarmed");
            Scribe_Values.Look(ref mapExhausted, "harvestMapExhausted");
            Scribe_Values.Look(ref collectionUntil, "harvestCollectionUntil", -1);
        }
    }
}
