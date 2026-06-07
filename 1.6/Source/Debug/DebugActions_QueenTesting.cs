using LudeonTK;
using RimWorld;
using System.Collections.Generic;
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
