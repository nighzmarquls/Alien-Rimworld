using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

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

        }

        public static bool CellIsFertile(IntVec3 cell, Map map)
        {
            TerrainDef foundTerrain = cell.GetTerrain(map);
            
            if(foundTerrain.affordances.Contains(InternalDefOf.Resin))
            {
                return true;
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
        public static void HandleXenoformingImpact(Ovamorph ovamorph)
        {

            if (ovamorph != null)
            {
                if (ovamorph.Unhatched)
                {
                    if (XMTSettings.LogWorld)
                    {
                        Log.Message(ovamorph + " is viable ovamorph");
                    }
                    gameComponent.ReleaseOvamorphOnWorld(ovamorph);
                    return;
                }
            }
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

            Ovamorph ovamorph = thing as Ovamorph;

            if (ovamorph != null)
            {
                if (ovamorph.Unhatched)
                {
                    if (XMTSettings.LogWorld)
                    {
                        Log.Message(thing + " is viable ovamorph");
                    }
                    gameComponent.ReleaseOvamorphOnWorld(ovamorph);
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
            gameComponent.HandleMatureMorphDeath(deadMorph);
        }

        internal static float ChanceByXenoforming(float chance)
        {
            return chance * (gameComponent.Xenoforming / 100);
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

        internal static void QueenCalledForAid()
        {
            if(XMTUtility.QueenPresent())
            {
                gameComponent.HandleQueenCallForAid();
            }
        }

       
    }
}
