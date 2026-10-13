using LudeonTK;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    public static class DebugActions_GrappleStealth
    {
        private const string Category = "Alien | Rimworld";

        [DebugAction(Category, "Report grapple check", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ReportGrappleCheck()
        {
            BeginPawnTargeting("Select grapple attacker.", delegate (Pawn attacker)
            {
                BeginPawnTargeting("Select grapple defender.", delegate (Pawn defender)
                {
                    GrappleCheckReport report = XMTUtility.GetGrappleCheckReport(attacker, defender);
                    Log.Message(report.ToLogString());
                    Messages.Message("Grapple resist chance: " + report.ResistChance.ToStringPercent(), MessageTypeDefOf.TaskCompletion, false);
                });
            });
        }

        [DebugAction(Category, "Report stealth reveal check", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ReportStealthRevealCheck()
        {
            BeginPawnTargeting("Select hidden cryptimorph.", delegate (Pawn hidden)
            {
                BeginThingTargeting("Select spotter.", delegate (Thing spotter)
                {
                    float brightness = hidden.MapHeld.glowGrid.GroundGlowAt(hidden.PositionHeld);
                    StealthDetectionReport report = XMTStealthUtility.GetDetectionReport(hidden, spotter, 4f, brightness);
                    Log.Message(report.ToLogString());
                    Messages.Message("Reveal chance: " + report.FinalChance.ToStringPercent(), MessageTypeDefOf.TaskCompletion, false);
                });
            });
        }

        [DebugAction(Category, "Report nearby stealth spotters", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ReportNearbyStealthSpotters()
        {
            BeginPawnTargeting("Select hidden cryptimorph.", delegate (Pawn hidden)
            {
                const float spotRange = 4f;
                float brightness = hidden.MapHeld.glowGrid.GroundGlowAt(hidden.PositionHeld);
                IEnumerable<StealthDetectionReport> reports = GenRadial.RadialDistinctThingsAround(hidden.PositionHeld, hidden.MapHeld, spotRange, true)
                    .Where(thing => thing != hidden && XMTUtility.IsSpotter(thing) && XMTUtility.IsThingWaryOfTarget(thing, hidden))
                    .Select(thing => XMTStealthUtility.GetDetectionReport(hidden, thing, spotRange, brightness))
                    .OrderByDescending(report => report.FinalChance);

                string output = string.Join("\n", reports.Select(report => report.Spotter + ": " + report.FinalChance.ToStringPercent() + " (visual " + report.VisualChance.ToStringPercent() + ", hearing " + report.HearingChance.ToStringPercent() + ")"));
                if (output.NullOrEmpty())
                {
                    output = "No nearby spotters found.";
                }

                Log.Message("Nearby stealth spotters for " + hidden + ":\n" + output);
                Messages.Message("Wrote nearby stealth spotters to log.", MessageTypeDefOf.TaskCompletion, false);
            });
        }

        private static void BeginPawnTargeting(string prompt, System.Action<Pawn> onSelected)
        {
            Messages.Message(prompt, MessageTypeDefOf.NeutralEvent, false);
            TargetingParameters targetingParameters = new TargetingParameters
            {
                canTargetPawns = true,
                canTargetBuildings = false,
                canTargetItems = false,
                canTargetLocations = false,
                validator = target => target.Thing is Pawn
            };

            Find.Targeter.BeginTargeting(targetingParameters, delegate (LocalTargetInfo target)
            {
                if (target.Thing is Pawn pawn)
                {
                    onSelected(pawn);
                }
            });
        }

        private static void BeginThingTargeting(string prompt, System.Action<Thing> onSelected)
        {
            Messages.Message(prompt, MessageTypeDefOf.NeutralEvent, false);
            TargetingParameters targetingParameters = new TargetingParameters
            {
                canTargetPawns = true,
                canTargetBuildings = true,
                canTargetItems = false,
                canTargetLocations = false,
                validator = target => target.Thing != null
            };

            Find.Targeter.BeginTargeting(targetingParameters, delegate (LocalTargetInfo target)
            {
                if (target.Thing != null)
                {
                    onSelected(target.Thing);
                }
            });
        }
    }
}
