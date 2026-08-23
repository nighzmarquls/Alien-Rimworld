using LudeonTK;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    public static class DebugActions_TraversalValidation
    {
        private const string Category = "Alien | Rimworld";

        [DebugActionYielder]
        private static IEnumerable<DebugActionNode> TraversalValidationNodes()
        {
            yield return new DebugActionNode("Traversal validation", DebugActionType.Action, null)
            {
                category = Category,
                childGetter = delegate
                {
                    return new List<DebugActionNode>
                    {
                        DebugActions_ClimbNavigation.MakeRootNode(),
                        DebugActions_InfiltrationNavigation.MakeRootNode(),
                        DebugActions_InfiltrationNavigation.MakeCombinedRootNode(),
                        MakeCacheDiagnosticsNode()
                    };
                }
            };
        }

        private static DebugActionNode MakeCacheDiagnosticsNode()
        {
            return new DebugActionNode("Traversal cache diagnostics", DebugActionType.Action, null)
            {
                childGetter = delegate
                {
                    return new List<DebugActionNode>
                    {
                        new DebugActionNode("Report both caches", DebugActionType.Action, ReportCaches),
                        new DebugActionNode("Clear climb cache", DebugActionType.Action, ClearClimbCache),
                        new DebugActionNode("Clear infiltration cache", DebugActionType.Action, ClearInfiltrationCache),
                        new DebugActionNode("Clear both caches", DebugActionType.Action, ClearBothCaches),
                        new DebugActionNode("Verify roof invalidation isolation", DebugActionType.Action, VerifyRoofIsolation),
                        new DebugActionNode("Verify infiltration invalidation isolation", DebugActionType.Action, VerifyInfiltrationIsolation),
                        new DebugActionNode("Verify region invalidation", DebugActionType.Action, VerifyRegionInvalidation)
                    };
                }
            };
        }

        private static void ReportCaches()
        {
            Map map = Find.CurrentMap;
            string report = "Climb: " + ClimbUtility.TopologyCacheReport(map) + "\nInfiltration: " + InfiltrationUtility.CacheReport(map);
            Log.Message("[Alien | Rimworld] Traversal topology caches\n" + report);
            Messages.Message("Traversal cache report written to the log.", MessageTypeDefOf.TaskCompletion, false);
        }

        private static void ClearClimbCache()
        {
            TraversalTopologyMapComponent.For(Find.CurrentMap)?.ClimbCache.Clear();
            Messages.Message("Cleared the current map's climb cache.", MessageTypeDefOf.TaskCompletion, false);
        }

        private static void ClearInfiltrationCache()
        {
            InfiltrationUtility.ClearCache(Find.CurrentMap);
            Messages.Message("Cleared the current map's infiltration cache.", MessageTypeDefOf.TaskCompletion, false);
        }

        private static void ClearBothCaches()
        {
            TraversalTopologyMapComponent.For(Find.CurrentMap)?.ClearCaches();
            Messages.Message("Cleared both traversal caches on the current map.", MessageTypeDefOf.TaskCompletion, false);
        }

        private static void VerifyRoofIsolation()
        {
            TraversalTopologyMapComponent component = TraversalTopologyMapComponent.For(Find.CurrentMap);
            if (component == null)
            {
                ReportResult("Roof invalidation isolation", false, "no current-map topology component");
                return;
            }
            int climbBefore = component.ClimbCache.RoofRevision;
            int infiltrationBefore = component.InfiltrationCache.ComponentsRevision;
            Map map = Find.CurrentMap;
            ClimbUtility.NotifyRoofChanged(map, new IntVec3(map.Size.x / 2, 0, map.Size.z / 2));
            bool passed = component.ClimbCache.RoofRevision == climbBefore + 1 &&
                component.InfiltrationCache.ComponentsRevision == infiltrationBefore;
            ReportResult("Roof invalidation isolation", passed,
                "climb " + climbBefore + " -> " + component.ClimbCache.RoofRevision +
                ", infiltration components " + infiltrationBefore + " -> " + component.InfiltrationCache.ComponentsRevision);
        }

        private static void VerifyInfiltrationIsolation()
        {
            TraversalTopologyMapComponent component = TraversalTopologyMapComponent.For(Find.CurrentMap);
            if (component == null)
            {
                ReportResult("Infiltration invalidation isolation", false, "no current-map topology component");
                return;
            }
            int climbBefore = component.ClimbCache.RoofRevision;
            int infiltrationRegionBefore = component.InfiltrationCache.RegionRevision;
            InfiltrationUtility.NotifyPipeTopologyChanged(Find.CurrentMap);
            bool passed = component.ClimbCache.RoofRevision == climbBefore &&
                component.InfiltrationCache.RegionRevision > infiltrationRegionBefore;
            ReportResult("Infiltration invalidation isolation", passed,
                "climb=" + climbBefore + ", infiltration region " + infiltrationRegionBefore + " -> " + component.InfiltrationCache.RegionRevision);
        }

        private static void VerifyRegionInvalidation()
        {
            TraversalTopologyMapComponent component = TraversalTopologyMapComponent.For(Find.CurrentMap);
            if (component == null)
            {
                ReportResult("Region invalidation", false, "no current-map topology component");
                return;
            }
            int climbBefore = component.ClimbCache.RegionRevision;
            int infiltrationBefore = component.InfiltrationCache.RegionRevision;
            int componentsBefore = component.InfiltrationCache.ComponentsRevision;
            ClimbUtility.NotifyRegionsRoomsChanged(Find.CurrentMap);
            InfiltrationUtility.NotifyRegionsRoomsChanged(Find.CurrentMap);
            bool passed = component.ClimbCache.RegionRevision == climbBefore + 1 &&
                component.InfiltrationCache.RegionRevision == infiltrationBefore + 1 &&
                component.InfiltrationCache.ComponentsRevision == componentsBefore;
            ReportResult("Region invalidation", passed,
                "climb " + climbBefore + " -> " + component.ClimbCache.RegionRevision +
                ", infiltration " + infiltrationBefore + " -> " + component.InfiltrationCache.RegionRevision +
                ", components=" + componentsBefore);
        }

        private static void ReportResult(string name, bool passed, string detail)
        {
            string report = name + " " + (passed ? "passed" : "FAILED") + ": " + detail;
            Log.Message("[Alien | Rimworld] " + report);
            Messages.Message(report, passed ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.RejectInput, false);
        }
    }
}
