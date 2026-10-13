using LudeonTK;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    public static class DebugActions_QueenTesting
    {
        private const string Category = "Alien | Rimworld";

        [DebugAction(Category, "Spawn feral queen", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnFeralQueen()
        {
            BeginCellTargeting("Select queen spawn cell.", delegate (IntVec3 cell, Map map)
            {
                Pawn queen = XenoformingUtility.GenerateFeralQueen();
                if (queen == null)
                {
                    Messages.Message("Failed to generate feral queen.", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                GenSpawn.Spawn(queen, cell, map, WipeMode.VanishOrMoveAside);
                XMTUtility.DeclareQueen(queen);
                Messages.Message("Spawned feral queen.", MessageTypeDefOf.TaskCompletion, false);
            });
        }

        [DebugAction(Category, "Spawn feral queen in throne", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnFeralQueenInThrone()
        {
            BeginCellTargeting("Select ovothrone spawn cell.", delegate (IntVec3 cell, Map map)
            {
                EggSack throne = GenSpawn.Spawn(XenoBuildingDefOf.XMT_Ovothrone, cell, map, Rot4.Random, WipeMode.VanishOrMoveAside) as EggSack;
                if (throne == null)
                {
                    Messages.Message("Failed to spawn ovothrone.", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                Find.Selector.Select(throne, playSound: false, forceDesignatorDeselect: false);
                Messages.Message("Spawned ovothrone; it will generate its own queen if it remains empty.", MessageTypeDefOf.TaskCompletion, false);
            });
        }

        [DebugActionYielder]
        private static IEnumerable<DebugActionNode> XenoformingSetters()
        {
            DebugActionNode root = new DebugActionNode("Set xenoforming", DebugActionType.Action, null);
            root.category = Category;
            root.childGetter = delegate
            {
                List<DebugActionNode> children = new List<DebugActionNode>();
                for (int value = 0; value <= 100; value += 10)
                {
                    int selectedValue = value;
                    children.Add(new DebugActionNode(selectedValue + "%", DebugActionType.Action, delegate
                    {
                        XenoformingUtility.SetXenoforming(selectedValue);
                        Messages.Message("Set xenoforming to " + selectedValue + ".", MessageTypeDefOf.TaskCompletion, false);
                    }));
                }

                return children;
            };

            yield return root;
        }

        [DebugActionYielder]
        private static IEnumerable<DebugActionNode> MechanitorQueenTestNodes()
        {
            DebugActionNode root = new DebugActionNode("Mechanitor queen tests", DebugActionType.Action, null);
            root.category = Category;
            root.childGetter = delegate
            {
                return new List<DebugActionNode>
                {
                    AssimilateXmlItemNode(),
                    new DebugActionNode("Assimilate all XML items", DebugActionType.Action, AssimilateAllXmlItems),
                    new DebugActionNode("Fill mechanoid material", DebugActionType.Action, FillMechanoidMaterial),
                    SetOvothroneReserveEggsNode()
                };
            };

            yield return root;
        }

        private static DebugActionNode AssimilateXmlItemNode()
        {
            DebugActionNode root = new DebugActionNode("Assimilate XML item...", DebugActionType.Action, null);
            root.childGetter = delegate
            {
                return DefDatabase<QueenAssimilationDef>.AllDefsListForReading
                    .Where(def => def.thingDef != null)
                    .OrderBy(def => def.LabelCap.ToString())
                    .Select(def =>
                    {
                        QueenAssimilationDef selectedDef = def;
                        return new DebugActionNode(selectedDef.LabelCap.ToString(), DebugActionType.Action, delegate
                        {
                            BeginQueenTargeting("Select queen to receive " + selectedDef.LabelCap + ".", delegate (Pawn queen)
                            {
                                CompQueenAssimilation assimilation = queen.GetComp<CompQueenAssimilation>();
                                if (assimilation == null)
                                {
                                    Messages.Message("Selected pawn has no queen assimilation comp.", MessageTypeDefOf.RejectInput, false);
                                    return;
                                }

                                assimilation.DebugCompleteAssimilation(selectedDef);
                            });
                        });
                    })
                    .ToList();
            };

            return root;
        }

        private static void AssimilateAllXmlItems()
        {
            BeginQueenTargeting("Select queen to receive all XML-defined assimilations.", delegate (Pawn queen)
            {
                CompQueenAssimilation assimilation = queen.GetComp<CompQueenAssimilation>();
                if (assimilation == null)
                {
                    Messages.Message("Selected pawn has no queen assimilation comp.", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                foreach (QueenAssimilationDef def in DefDatabase<QueenAssimilationDef>.AllDefsListForReading.Where(def => def.thingDef != null))
                {
                    assimilation.DebugCompleteAssimilation(def, showMessage: false);
                }

                Messages.Message("Debug assimilated all XML-defined queen items for " + queen.LabelShort + ".", queen, MessageTypeDefOf.TaskCompletion, false);
            });
        }

        private static void FillMechanoidMaterial()
        {
            BeginQueenTargeting("Select queen to fill mechanoid material.", delegate (Pawn queen)
            {
                CompQueenAssimilation assimilation = queen.GetComp<CompQueenAssimilation>();
                QueenIngestibleResourceDef resource = QueenMechGestationUtility.MechMaterialResource;
                if (assimilation == null || resource == null)
                {
                    Messages.Message("Selected pawn cannot store mechanoid material.", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                assimilation.AddResource(resource, assimilation.GetResourceCapacity(resource));
                Messages.Message("Filled mechanoid material for " + queen.LabelShort + ".", queen, MessageTypeDefOf.TaskCompletion, false);
            });
        }

        private static DebugActionNode SetOvothroneReserveEggsNode()
        {
            DebugActionNode root = new DebugActionNode("Set ovothrone reserve eggs", DebugActionType.Action, null);
            root.childGetter = delegate
            {
                List<DebugActionNode> children = new List<DebugActionNode>();
                for (int value = 0; value <= EggSack.MaxBacklog; value++)
                {
                    int selectedValue = value;
                    children.Add(new DebugActionNode(selectedValue.ToString(), DebugActionType.Action, delegate
                    {
                        BeginOvothroneTargeting("Select ovothrone reserve eggs: " + selectedValue + ".", delegate (EggSack throne)
                        {
                            throne.DebugSetReserveEggs(selectedValue);
                            Messages.Message("Set ovothrone reserve eggs to " + selectedValue + ".", throne, MessageTypeDefOf.TaskCompletion, false);
                        });
                    }));
                }

                return children;
            };

            return root;
        }

        private static void BeginQueenTargeting(string prompt, System.Action<Pawn> onSelected)
        {
            BeginThingTargeting(prompt, delegate (Thing thing)
            {
                Pawn queen = thing as Pawn ?? (thing as EggSack)?.Occupant;
                if (queen == null)
                {
                    Messages.Message("No queen selected.", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                onSelected(queen);
            });
        }

        private static void BeginOvothroneTargeting(string prompt, System.Action<EggSack> onSelected)
        {
            BeginThingTargeting(prompt, delegate (Thing thing)
            {
                if (thing is EggSack throne)
                {
                    onSelected(throne);
                    return;
                }

                Messages.Message("No ovothrone selected.", MessageTypeDefOf.RejectInput, false);
            });
        }

        private static void BeginThingTargeting(string prompt, System.Action<Thing> onSelected)
        {
            Messages.Message(prompt, MessageTypeDefOf.NeutralEvent, false);
            TargetingParameters targetingParameters = new TargetingParameters
            {
                canTargetLocations = false,
                canTargetPawns = true,
                canTargetBuildings = true,
                canTargetItems = false,
                mapObjectTargetsMustBeAutoAttackable = false,
                validator = target => target.HasThing
            };

            Find.Targeter.BeginTargeting(targetingParameters, delegate (LocalTargetInfo target)
            {
                if (!target.HasThing)
                {
                    Messages.Message("No thing selected.", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                onSelected(target.Thing);
            });
        }

        private static void BeginCellTargeting(string prompt, System.Action<IntVec3, Map> onSelected)
        {
            Messages.Message(prompt, MessageTypeDefOf.NeutralEvent, false);
            TargetingParameters targetingParameters = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetPawns = false,
                canTargetBuildings = false,
                canTargetItems = false,
                validator = target => target.Cell.InBounds(Find.CurrentMap)
            };

            Find.Targeter.BeginTargeting(targetingParameters, delegate (LocalTargetInfo target)
            {
                Map map = Find.CurrentMap;
                if (map == null)
                {
                    Messages.Message("No current map found.", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                onSelected(target.Cell, map);
            });
        }

    }
}
