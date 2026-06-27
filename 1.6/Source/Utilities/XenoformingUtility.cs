using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    [StaticConstructorOnStartup]
    internal class XenoformingUtility
    {

        private static GameComponent_Xenomorph gameComponent => Current.Game.GetComponent<GameComponent_Xenomorph>();

        private static readonly Texture2D InvestigateTex = ContentFinder<Texture2D>.Get("UI/Commands/OfferGifts");

        public static Command InvestigateCommand(Caravan caravan, Settlement settlement)
        {
            return new Command_Action
            {
                defaultLabel = "XMT_CommandInvestigateSite".Translate(),
                defaultDesc = "XMT_CommandInvestigateSiteDesc".Translate(),
                icon = InvestigateTex,
                action = delegate
                {
                    if (!settlement.HasMap)
                    {
                        LongEventHandler.QueueLongEvent(delegate
                        {
                            InvestigateSettlement(settlement, caravan);
                        }, "GeneratingMapForNewEncounter", doAsynchronously: false, null);
                    }
                    else
                    {
                        InvestigateSettlement(settlement, caravan);
                    }

                }
            };
        }

        public static void SettlementCounterAttack(Settlement settlement, Caravan caravan)
        {
            SurfaceTile tile = Find.WorldGrid[settlement.Tile.tileId];
            
            bool GeneratedMap = !settlement.HasMap;
            Map orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(settlement.Tile, null);
            TaggedString letterLabel = "XMT_LetterLabelCaravanAttackedByBase".Translate();
            TaggedString letterText = "XMT_LetterCaravanAttackedByBase".Translate(caravan.Label, settlement.Label.ApplyTag(TagType.Settlement, settlement.Faction.GetUniqueLoadID())).CapitalizeFirst();

            if (GeneratedMap)
            {
                Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
            }

            if (settlement.Faction != null)
            {
                FactionRelationKind playerRelationKind = settlement.Faction.PlayerRelationKind;
                Faction.OfPlayer.TryAffectGoodwillWith(settlement.Faction, Faction.OfPlayer.GoodwillToMakeHostile(settlement.Faction), canSendMessage: false, canSendHostilityLetter: false, HistoryEventDefOf.AttackedSettlement);
                settlement.Faction.TryAppendRelationKindChangedInfo(ref letterText, playerRelationKind, settlement.Faction.PlayerRelationKind);
            }
            Find.LetterStack.ReceiveLetter(letterLabel, letterText, LetterDefOf.NegativeEvent, caravan.PawnsListForReading, settlement.Faction);
            CaravanEnterMapUtility.Enter(caravan, orGenerateMap, CaravanEnterMode.Center, CaravanDropInventoryMode.DoNotDrop, draftColonists: true);
        }

        public static void InvestigateSettlement(Settlement settlement, Caravan caravan)
        {
            SurfaceTile tile = Find.WorldGrid[settlement.Tile.tileId];
            XenoformingComp comp = null;
            bool questInvitedInvestigation = settlement.TryGetComponent(out comp) && comp.DistressQuestSent;

            if (!tile.Mutators.Contains(XenoMapDefOf.XMT_SettlementAftermath))
            {
                tile.AddMutator(XenoMapDefOf.XMT_SettlementAftermath);
            }

            bool GeneratedMap = !settlement.HasMap;
            Map orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(settlement.Tile, null);
            TaggedString letterLabel = "XMT_LetterLabelCaravanEnteredAftermathBase".Translate();
            TaggedString letterText = "XMT_LetterCaravanEnteredAftermathBase".Translate(caravan.Label, settlement.Label.ApplyTag(TagType.Settlement, settlement.Faction.GetUniqueLoadID())).CapitalizeFirst();

            if (GeneratedMap)
            {
                Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
            }

            Find.LetterStack.ReceiveLetter(letterLabel, letterText, LetterDefOf.NeutralEvent, caravan.PawnsListForReading, settlement.Faction);
            CaravanEnterMapUtility.Enter(caravan, orGenerateMap, CaravanEnterMode.Edge, CaravanDropInventoryMode.DoNotDrop, draftColonists: true);

            if (questInvitedInvestigation)
            {
                if (!comp.DistressSignalTag.NullOrEmpty())
                {
                    QuestUtility.SendQuestTargetSignals(new List<string> { comp.DistressSignalTag }, "Investigated");
                }

                AwardDistressInvestigationGoodwill(settlement);
                gameComponent.RemoveCachedDistressSignal(settlement, comp.DistressQuestId, comp.DistressSignalTag);
                comp.Notify_Investigated();
            }
        }

        public static bool CellIsFertile(IntVec3 cell, Map map)
        {
            TerrainDef foundTerrain = cell.GetTerrain(map);
            
            if(foundTerrain.affordances.Contains(InternalDefOf.Resin))
            {
                return false;
            }

            return foundTerrain.fertility > 0 || (foundTerrain.driesTo != null && foundTerrain.driesTo.fertility > 0);
        }
        protected static TerrainDef DegradeTerrain(TerrainDef terrainDef)
        {
            if (terrainDef.fertility > 1.0 || (terrainDef.driesTo != null && terrainDef.driesTo.fertility == 1.0))
            {
                return TerrainDefOf.Soil;
            }

            if (terrainDef.fertility == 1.0 || (terrainDef.driesTo != null && terrainDef.driesTo.fertility == 1.0))
            {
                return TerrainDefOf.Gravel;
            }

            if (terrainDef.fertility > 0.5 || (terrainDef.driesTo != null && terrainDef.driesTo.fertility > 0.5))
            {
                return TerrainDefOf.Sand;
            }

            return InternalDefOf.BarrenDust;

        }

        public static float ValueOfTerrainOnCell(Map map,IntVec3 cell, out TerrainDef degraded, TerrainDef startingTerrain = null)
        {
            if (startingTerrain == null)
            {
                startingTerrain = map.terrainGrid.TerrainAt(cell);
            }
            degraded = DegradeTerrain(startingTerrain);
            return startingTerrain.fertility - degraded.fertility;
        }
        public static float DegradeTerrainOnCell(Map map, IntVec3 cell, TerrainDef startingTerrain = null)
        {
            if (startingTerrain == InternalDefOf.HiveFloor)
            {
                map.terrainGrid.RemoveTopLayer(cell);
                return 0.5f;
            }
            else if (startingTerrain == InternalDefOf.HeavyHiveFloor)
            {
                map.terrainGrid.RemoveTopLayer(cell);
                return 1;
            }

            float drainedFertility = ValueOfTerrainOnCell(map, cell, out TerrainDef degraded ,startingTerrain);
            map.terrainGrid.SetTerrain(cell, degraded);
            return drainedFertility;
        }

        public static bool XenoformingMeets(float minXenoforming)
        {
            return gameComponent.Xenoforming >= minXenoforming;
        }

        public static void ReleaseEmbryoOnWorld(Pawn pawn)
        {
            gameComponent.ReleaseEmbryoOnWorld(pawn);
        }
        public static void HandleXenoformingImpact(Pawn pawn)
        {
            if (XMTSettings.LogWorld)
            {
                Log.Message(pawn + " is being evaluated for xenoforming impact");
            }
            if (pawn != null)
            {
                if(CaravanUtility.IsCaravanMember(pawn))
                {
                    if (XMTSettings.LogWorld)
                    {
                        Log.Message(pawn + " is in a caravan. Skipping.");
                    }
                    return;
                }
                if (XMTUtility.IsXenomorph(pawn))
                {
                    if (XMTSettings.LogWorld)
                    {
                        Log.Message(pawn + " is a xenomorph");
                    }
                    gameComponent.ReleaseXenomorphOnWorld(pawn);
                }
                else
                {
                    if(XMTUtility.HasEmbryo(pawn))
                    {
                        if (XMTSettings.LogWorld)
                        {
                            Log.Message(pawn + " has an embryo");
                        }
                        gameComponent.ReleaseEmbryoOnWorld(pawn);
                    }
                    float essence = BioUtility.GetXenomorphInfluence(pawn);
                    if (essence > 0)
                    {
                        if (XMTSettings.LogWorld)
                        {
                            Log.Message(pawn + " has mutations");
                        }
                        gameComponent.ReleaseMutagenOnWorld(essence);
                    }
                }

                ThingOwner carriedThings = pawn.inventory.GetDirectlyHeldThings();

                if (XMTSettings.LogWorld)
                {
                    Log.Message("checking inventory for " + pawn);
                }
                if (carriedThings != null)
                {
                    foreach (Thing carriedThing in carriedThings)
                    {
                        HandleXenoformingImpact(carriedThing);
                    }
                }

            }
        }

        public static void HandleXenoformingImpact(HibernationCocoon hibernationCocoon)
        {
            if (hibernationCocoon != null)
            {
                if (XMTSettings.LogWorld)
                {
                    Log.Message(hibernationCocoon + " is a hibernation cocoon");
                }
                HandleXenoformingImpact(hibernationCocoon.ContainedThing as Pawn);
            }


        }
        public static void HandleXenoformingImpact(Ovomorph Ovomorph)
        {

            if (Ovomorph != null)
            {
                if (Ovomorph.Unhatched)
                {
                    if (XMTSettings.LogWorld)
                    {
                        Log.Message(Ovomorph + " is viable Ovomorph");
                    }
                    gameComponent.ReleaseOvomorphOnWorld(Ovomorph);
                    return;
                }
            }
        }

        public static float GetXenoforming()
        {
            return gameComponent.Xenoforming;
        }
        public static void HandleXenoformingImpact(Thing thing)
        {
            if(thing == null)
            {
                if (XMTSettings.LogWorld)
                {
                    Log.Message("thing is null");
                }
                return;
            }

            if (XMTSettings.LogWorld)
            {
                Log.Message(thing + " is being evaluated for xenoforming impact");
            }

            if(thing is StarbeastCorpse corpse)
            {
                if(corpse.NotActuallyDead)
                {
                    if (XMTSettings.LogWorld)
                    {
                        Log.Message(thing + " is a not actually dead corpse!");
                    }
                    HandleXenoformingImpact(corpse.InnerPawn);
                    return;
                }
            }

            MinifiedThing minifiedThing = thing as MinifiedThing;

            if (minifiedThing != null)
            {
                if (XMTSettings.LogWorld)
                {
                    Log.Message(thing + " is a minified thing");
                }
                HandleXenoformingImpact(minifiedThing.InnerThing);
                return;
            }

            Ovomorph Ovomorph = thing as Ovomorph;

            if (Ovomorph != null)
            {
                if (Ovomorph.Unhatched)
                {
                    if (XMTSettings.LogWorld)
                    {
                        Log.Message(thing + " is viable Ovomorph");
                    }
                    gameComponent.ReleaseOvomorphOnWorld(Ovomorph);
                    return;
                }
            }

            HibernationCocoon hibernationCocoon = thing as HibernationCocoon;

            if(hibernationCocoon != null)
            {
                if (XMTSettings.LogWorld)
                {
                    Log.Message(thing + " is a hibernation cocoon");
                }
                HandleXenoformingImpact(hibernationCocoon.ContainedThing as Pawn);
            }
            
            XMTGenePack genePack = thing as XMTGenePack;

            if (genePack != null)
            {
                if (XMTSettings.LogWorld)
                {
                    Log.Message(thing + " is a genepack");
                }
                gameComponent.ReleaseMutagenOnWorld(genePack.Potency*genePack.stackCount);
                return;
            }

            Pawn pawn = thing as Pawn;
            if (pawn != null)
            {
                HandleXenoformingImpact(pawn);
            }
        }

        internal static void HandleMatureMorphDeath(Pawn deadMorph)
        {
            if(deadMorph.def == InternalDefOf.XMT_Larva)
            {
                return;
            }

            gameComponent.HandleMatureMorphDeath(deadMorph);
        }

        internal static float ChanceByXenoforming(float chance)
        {
            return chance * (gameComponent.Xenoforming / 100);
        }

        internal static void SetXenoforming(float v)
        {
            gameComponent.Xenoforming = v;
        }

        internal static void IncreaseXenoforming(float v)
        {
            gameComponent.Xenoforming += v;
            Messages.Message("DEBUG: Xenoforming Increased to: " + gameComponent.Xenoforming, MessageTypeDefOf.NeutralEvent);

        }

        internal static void DecreaseXenoforming(float v)
        {
            gameComponent.Xenoforming -= v;
            Messages.Message("DEBUG: Xenoforming Decreased to: " + gameComponent.Xenoforming, MessageTypeDefOf.NeutralEvent);

        }

        internal static bool QueenCalledForAid(Pawn aggressor = null)
        {
            if(XMTUtility.QueenPresent())
            {
                return gameComponent.TryQueenCallForAid(aggressor);
            }
            return false;
        }

        internal static bool QueenCalledForAid(Pawn queen, Pawn aggressor)
        {
            if (queen == null)
            {
                return QueenCalledForAid(aggressor);
            }

            return gameComponent.TryQueenCallForAid(queen, aggressor);
        }

        public static Pawn GenerateFeralXenomorph()
        {
            PawnGenerationRequest request = new PawnGenerationRequest(
                               InternalDefOf.XMT_FeralStarbeastKind, faction: null, PawnGenerationContext.PlayerStarter, -1, true, false, true, false, false, 0, false, true, false, false, false, false, false, false, true, 0, 0, null, 0, null, null, null, null, 0, fixedGender: Gender.Female);

            request.ForceNoIdeo = true;
            request.ForceNoBackstory = true;
            request.ForceNoGear = true;
            request.ForceBaselinerChance = 100;

            Pawn pawn = PawnGenerator.GeneratePawn(request);
            BioUtility.ClearGenes(ref pawn);

            GeneSet genes = new GeneSet();
            BioUtility.ExtractCryptimorphGenesToGeneset(ref genes, BioUtility.GetWorldAppropriateXenotype().genes);
            BioUtility.ExtractGenesToGeneset(ref genes, InternalDefOf.XMT_Starbeast_AlienRace.alienRace.raceRestriction.geneList);
            BioUtility.InsertGenesetToPawn(genes, ref pawn);
            return pawn;
        }

        public static Pawn GetWorldOrGeneratedFeralXenomorphForSite(bool allowPlayerPioneers = true)
        {
            return gameComponent.GetWorldOrGeneratedCryptimorphForSite(allowPlayerPioneers);
        }

        public static bool IsQueenAidDefender(Pawn pawn)
        {
            return gameComponent.IsQueenAidDefender(pawn);
        }

        public static Pawn GenerateFeralQueen(RoyalEvolutionSet advancementSet = null)
        {
            PawnGenerationRequest request = new PawnGenerationRequest(
                                   InternalDefOf.XMT_RoyaltyKind, faction: null, PawnGenerationContext.PlayerStarter, -1, true, false, true, false, false, 0, false, true, false, false, false, false, false, false, true, 0, 0, null, 0, null, null, null, null, 0, fixedGender: Gender.Female);

            request.ForceNoIdeo = true;
            request.ForceNoBackstory = true;
            request.ForceNoGear = true;
            request.ForceBaselinerChance = 100;
            request.Faction = null;
            Pawn newQueen = PawnGenerator.GeneratePawn(request);


            BioUtility.ClearGenes(ref newQueen);
            newQueen.genes.SetXenotype(XenotypeDefOf.Baseliner);

            GeneSet genes = new GeneSet();
            BioUtility.ExtractGenesToGeneset(ref genes, BioUtility.GetWorldAppropriateXenotype().genes);
            BioUtility.ExtractGenesToGeneset(ref genes, InternalDefOf.XMT_Starbeast_AlienRace.alienRace.raceRestriction.geneList);
            BioUtility.InsertGenesetToPawn(genes, ref newQueen);

            float Advancements = 1;

            Advancements += Mathf.Max(0,Mathf.Floor(GetXenoforming()-10));

            if (advancementSet == null)
            {
                advancementSet = RoyalEvolutionDefOf.BaseQueenSet;
            }

            if(newQueen.GetComp<CompQueen>() is CompQueen comp)
            {
                comp.RecieveProgress(Advancements);

                foreach(RoyalEvolutionDef evo in advancementSet.evolutions)
                {
                    if (comp.AvailableEvoPoints >= evo.evoPointCost)
                    {
                        comp.AddEvolution(evo);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return newQueen;
        }

        internal static void PrepareQueenForOvoThrone(Pawn queen)
        {
            CompQueen compQueen = queen?.GetComp<CompQueen>();
            if (compQueen == null)
            {
                return;
            }

            if (RoyalEvolutionDefOf.BaseQueenSet?.evolutions == null)
            {
                return;
            }

            compQueen.RecieveProgress(10);
            foreach (RoyalEvolutionDef evolution in RoyalEvolutionDefOf.BaseQueenSet.evolutions)
            {
                if (evolution != null && !compQueen.ChosenEvolutions.Contains(evolution))
                {
                    compQueen.AddEvolution(evolution);
                }
            }
        }

        internal static void EnsureQueenHasOvoThrone(Pawn queen)
        {
            CompQueen compQueen = queen?.GetComp<CompQueen>();
            if (compQueen == null || RoyalEvolutionDefOf.Evo_OvoThrone == null)
            {
                return;
            }

            if (!compQueen.ChosenEvolutions.Contains(RoyalEvolutionDefOf.Evo_OvoThrone))
            {
                compQueen.AddEvolution(RoyalEvolutionDefOf.Evo_OvoThrone);
            }
        }

        internal static void AddReprisal(Faction faction, float points)
        {
            gameComponent.CacheReprisal(faction, points);
        }

        internal static void AddDistressSignal(Settlement settlement, int settlementDestroyedTick)
        {
            if (settlement == null || settlement.Destroyed || XenoMapDefOf.XMT_SettlementDistressSignal == null)
            {
                return;
            }

            if (!settlement.TryGetComponent(out XenoformingComp comp))
            {
                return;
            }

            string signalTag = comp.DistressSignalTag;
            if (signalTag.NullOrEmpty())
            {
                signalTag = "XMT_DistressSignal_" + settlement.ID + "_" + settlementDestroyedTick;
            }
            QuestUtility.AddQuestTag(settlement, signalTag);

            Slate slate = new Slate();
            slate.Set("settlement", settlement);
            slate.Set("faction", settlement.Faction);
            slate.Set("settlementLabel", settlement.Label);
            slate.Set("settlement_label", settlement.Label);
            slate.Set("timeoutTicks", Mathf.Max(1, settlementDestroyedTick - Find.TickManager.TicksGame));
            slate.Set("distressSignalTag", signalTag);
            slate.Set("investigatedSignal", signalTag + ".Investigated");
            slate.Set("expiredSignal", signalTag + ".Expired");

            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(XenoMapDefOf.XMT_SettlementDistressSignal, slate);
            int questId = quest?.id ?? -1;
            comp.Notify_DistressSignalSent(questId, signalTag, settlementDestroyedTick);
            gameComponent.CacheDistressSignal(settlement, questId, signalTag, settlementDestroyedTick);
        }

        internal static void ConfirmDestroyedSettlement(Settlement settlement)
        {
            if (settlement == null || settlement.Destroyed)
            {
                return;
            }

            XenoformingComp comp = null;
            settlement.TryGetComponent(out comp);
            if (comp != null)
            {
                if (!comp.DistressSignalTag.NullOrEmpty())
                {
                    QuestUtility.SendQuestTargetSignals(new List<string> { comp.DistressSignalTag }, "Expired");
                }

                EndDistressQuest(comp.DistressQuestId, QuestEndOutcome.Fail);
                gameComponent.RemoveCachedDistressSignal(settlement, comp.DistressQuestId, comp.DistressSignalTag);
            }

            if (settlement.HasMap)
            {
                comp?.Notify_RemoveWhenMapClears();
                return;
            }

            RemoveDestroyedSettlement(settlement, comp);
        }

        internal static void RemoveDestroyedSettlementIfReady(Settlement settlement)
        {
            if (settlement == null || settlement.Destroyed)
            {
                return;
            }

            if (!settlement.TryGetComponent(out XenoformingComp comp) || !comp.RemoveWhenMapClears || settlement.HasMap)
            {
                return;
            }

            RemoveDestroyedSettlement(settlement, comp);
        }

        private static void RemoveDestroyedSettlement(Settlement settlement, XenoformingComp comp)
        {
            if (settlement == null || settlement.Destroyed)
            {
                return;
            }

            // TODO: Consider replacing removed settlements with an Odyssey-style ruined settlement site.
            comp?.Notify_DestroyedConfirmed();
            settlement.Destroy();
        }

        private static void EndDistressQuest(int questId, QuestEndOutcome outcome)
        {
            if (questId < 0)
            {
                return;
            }

            Quest quest = Find.QuestManager.ActiveQuestsListForReading.FirstOrDefault(activeQuest => activeQuest != null && activeQuest.id == questId);
            quest?.End(outcome, sendLetter: false, playSound: false);
        }

        private static void AwardDistressInvestigationGoodwill(Settlement settlement)
        {
            Faction faction = settlement?.Faction;
            if (faction == null || faction == Faction.OfPlayer || faction.HostileTo(Faction.OfPlayer) || faction.temporary)
            {
                return;
            }

            int goodwill = faction.PlayerRelationKind == FactionRelationKind.Ally ? 10 : 5;
            Faction.OfPlayer.TryAffectGoodwillWith(faction, goodwill, canSendMessage: true, canSendHostilityLetter: false);
        }
    }
}
