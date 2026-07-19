using LudeonTK;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    internal static class DebugActions_HorrorAdvancement
    {
        [DebugAction("Xenomorph", "Log horror advancement data", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LogAdvancementData()
        {
            Find.Targeter.BeginTargeting(DebugTargetParameters(), target =>
            {
                Thing thing = target.Thing;
                XMT_HorrorPawnExtension extension = HorrorAdvancementUtility.GetExtension(thing);
                List<HorrorAdvancementOption> promotions = HorrorAdvancementUtility.GetOptions(thing, HorrorAdvancementDirection.Promote);
                List<HorrorAdvancementOption> demotions = HorrorAdvancementUtility.GetOptions(thing, HorrorAdvancementDirection.Demote);
                Log.Message("Horror advancement data for " + thing +
                    ": tier=" + (extension?.tier.ToString() ?? (thing is Pawn pawn && HorrorAdvancementUtility.IsEffectiveTierZero(pawn) ? "effective 0" : "none")) +
                    ", bodySize=" + HorrorAdvancementUtility.EffectiveBodySize(thing).ToString("0.###") +
                    ", promotions=[" + string.Join(", ", promotions.Select(option => OptionDescription(thing, option))) + "]" +
                    ", demotions=[" + string.Join(", ", demotions.Select(option => OptionDescription(thing, option))) + "]");
            });
        }

        [DebugAction("Xenomorph", "Execute Crown horror advancement", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ExecuteAdvancement()
        {
            Find.Targeter.BeginTargeting(new TargetingParameters
            {
                canTargetPawns = true,
                canTargetBuildings = false,
                canTargetItems = false,
                validator = target => target.Thing is Pawn pawn && pawn.GetComp<CompGeneManipulator>() != null
            }, queenTarget =>
            {
                Pawn queen = queenTarget.Thing as Pawn;
                Find.Targeter.BeginTargeting(DebugTargetParameters(), target => OpenOptionMenu(queen, target.Thing));
            });
        }

        private static void OpenOptionMenu(Pawn queen, Thing target)
        {
            List<FloatMenuOption> menuOptions = new List<FloatMenuOption>();
            foreach (HorrorAdvancementDirection direction in new[] { HorrorAdvancementDirection.Promote, HorrorAdvancementDirection.Demote })
            {
                foreach (HorrorAdvancementOption option in HorrorAdvancementUtility.GetOptions(target, direction))
                {
                    HorrorAdvancementOption selected = option;
                    float cost = HorrorAdvancementUtility.FoodCost(queen, target, selected);
                    FloatMenuOption menuOption = new FloatMenuOption(direction + ": " + selected.Label + " (food " + cost.ToString("0.##") + ")", delegate
                    {
                        bool success = HorrorAdvancementUtility.TryExecute(queen, target, selected, out Thing result);
                        Log.Message("Crown advancement " + (success ? "succeeded" : "failed") + ": " + target + " -> " + result);
                    });
                    AcceptanceReport report = HorrorAdvancementUtility.CanExecute(queen, target, selected);
                    if (!report.Accepted)
                    {
                        menuOption.Disabled = true;
                        menuOption.tooltip = report.Reason;
                    }
                    menuOptions.Add(menuOption);
                }
            }

            if (menuOptions.NullOrEmpty())
            {
                Messages.Message("No horror advancement options for " + target.LabelShort + ".", MessageTypeDefOf.RejectInput, false);
                return;
            }

            Find.WindowStack.Add(new FloatMenu(menuOptions));
        }

        private static TargetingParameters DebugTargetParameters()
        {
            return new TargetingParameters
            {
                canTargetPawns = true,
                canTargetBuildings = true,
                canTargetItems = false,
                validator = target => target.Thing != null && HorrorAdvancementUtility.HasAnyOptions(target.Thing)
            };
        }

        private static string OptionDescription(Thing source, HorrorAdvancementOption option)
        {
            return option.Label + " (size " + HorrorAdvancementUtility.DestinationBodySize(source, option).ToString("0.###") + ")";
        }
    }
}
