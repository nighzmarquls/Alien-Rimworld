
using HarmonyLib;
using PipeSystem;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class XMTClimbPatches
    {
        private static readonly FieldInfo JobDriverToilsField = AccessTools.Field(typeof(JobDriver), "toils");
        private static readonly FieldInfo JobDriverCurToilIndexField = AccessTools.Field(typeof(JobDriver), "curToilIndex");
        private static readonly FieldInfo JobDriverWantBeginNextToilField = AccessTools.Field(typeof(JobDriver), "wantBeginNextToil");
        private static readonly MethodInfo ClosestThingReachableMethod = AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThingReachable));
        private static readonly MethodInfo ClosestThingGlobalReachableMethod = AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThing_Global_Reachable));
        private static readonly MethodInfo TraversalClosestThingReachableMethod = AccessTools.Method(typeof(TraversalReachabilityUtility), nameof(TraversalReachabilityUtility.ClosestThingReachable));
        private static readonly MethodInfo TraversalClosestThingGlobalReachableMethod = AccessTools.Method(typeof(TraversalReachabilityUtility), nameof(TraversalReachabilityUtility.ClosestThingGlobalReachable));
        private static readonly MethodInfo LowLevelCanReachMethod = AccessTools.Method(typeof(Reachability), nameof(Reachability.CanReach),
            new Type[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(TraverseParms) });
        private static readonly MethodInfo HaulDestinationCanReachMethod = AccessTools.Method(typeof(TraversalReachabilityUtility), nameof(TraversalReachabilityUtility.CanReachHaulDestination));

        private static IEnumerable<CodeInstruction> ReplaceCall(IEnumerable<CodeInstruction> instructions, MethodInfo original, MethodInfo replacement)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(original))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                }
                yield return instruction;
            }
        }

        private static Toil GetCurrentToil(JobDriver driver)
        {
            if (driver == null)
            {
                return null;
            }

            List<Toil> toils = JobDriverToilsField.GetValue(driver) as List<Toil>;
            int index = (int)JobDriverCurToilIndexField.GetValue(driver);
            return toils != null && index >= 0 && index < toils.Count ? toils[index] : null;
        }

        private static void RestoreClimbToil(JobDriver driver, List<Toil> toils, int curToilIndex, bool wantBeginNextToil, Job job)
        {
            if (wantBeginNextToil ||
                toils == null ||
                curToilIndex < 0 ||
                curToilIndex >= toils.Count)
            {
                return;
            }

            Toil currentToil = toils[curToilIndex];
            if (!ClimbUtility.HasClimbSupport(currentToil))
            {
                return;
            }

            CompClimber climber = driver?.pawn?.GetClimberComp();
            if (climber == null || job == null)
            {
                return;
            }

            // A generated toil being climb-capable does not mean traversal took ownership of it.
            // Only restore manual completion for a job that persisted active traversal ownership.
            if (climber.HasActiveClimbToilFor(job))
            {
                climber.MarkClimbToilActive(job, currentToil);
                currentToil.defaultCompleteMode = ToilCompleteMode.Never;
            }
        }

        internal static void RestoreDetachedClimbToil(JobDriver driver, Job job)
        {
            if (driver == null)
            {
                return;
            }

            List<Toil> toils = JobDriverToilsField.GetValue(driver) as List<Toil>;
            if (driver.pawn != null && toils != null)
            {
                foreach (Toil toil in toils)
                {
                    if (toil != null)
                    {
                        toil.actor = driver.pawn;
                    }
                }
            }

            RestoreClimbToil(
                driver,
                toils,
                (int)JobDriverCurToilIndexField.GetValue(driver),
                (bool)JobDriverWantBeginNextToilField.GetValue(driver),
                job);
        }

        [HarmonyPatch(typeof(JobDriver), "SetupToils")]
        public static class Patch_JobDriver_SetupToils
        {
            [HarmonyPostfix]
            public static void Postfix(JobDriver __instance, List<Toil> ___toils, int ___curToilIndex, bool ___wantBeginNextToil)
            {
                Pawn pawn = __instance?.pawn;
                Job currentJob = pawn?.jobs?.curJob;
                RestoreClimbToil(__instance, ___toils, ___curToilIndex, ___wantBeginNextToil, currentJob);
            }
        }

        [HarmonyPatch]
        public static class Patch_CompResource_PipeNet_InfiltrationCache
        {
            public static MethodBase TargetMethod()
            {
                return AccessTools.PropertySetter(typeof(CompResource), nameof(CompResource.PipeNet));
            }

            [HarmonyPrefix]
            public static void Prefix(CompResource __instance, ref PipeNet __state)
            {
                __state = __instance?.PipeNet;
            }

            [HarmonyPostfix]
            public static void Postfix(CompResource __instance, PipeNet __state)
            {
                if (__instance != null && !ReferenceEquals(__state, __instance.PipeNet))
                {
                    InfiltrationUtility.NotifyPipeTopologyChanged(__instance.parent?.Map);
                }
            }
        }

        [HarmonyPatch(typeof(ReachabilityUtility), nameof(ReachabilityUtility.CanReach), new Type[] { typeof(Pawn), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(Danger), typeof(bool), typeof(bool), typeof(TraverseMode) })]
        public static class Patch_ReachabilityUtility_CanReach
        {
            [HarmonyPrefix]
            public static bool Prefix(Pawn pawn, LocalTargetInfo dest, PathEndMode peMode, Danger maxDanger, bool canBashDoors, bool canBashFences, TraverseMode mode, ref bool __result)
            {
                if (pawn == null ||
                    !pawn.Spawned ||
                    pawn.Map == null ||
                    !dest.IsValid ||
                    !dest.Cell.InBounds(pawn.Map) ||
                    !XMTUtility.IsXenomorph(pawn) ||
                    pawn.GetClimberComp() == null)
                {
                    return true;
                }

                __result = ClimbUtility.CanReachByWalkingOrClimb(pawn, dest, peMode, maxDanger, canBashDoors, canBashFences, mode);
                return false;
            }
        }

        [HarmonyPatch(typeof(Toils_Goto), nameof(Toils_Goto.GotoThing), new Type[] { typeof(TargetIndex), typeof(PathEndMode), typeof(bool) })]
        public static class Patch_Toils_Goto_GotoThing
        {
            [HarmonyPostfix]
            public static void Postfix(TargetIndex ind, PathEndMode peMode, bool canGotoSpawnedParent, Toil __result)
            {
                ClimbUtility.AddClimbSupport(__result, ind, peMode, canGotoSpawnedParent);
            }
        }

        [HarmonyPatch]
        public static class Patch_RestUtility_ClosestThingReachable
        {
            public static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(RestUtility), nameof(RestUtility.FindBedFor),
                    new Type[] { typeof(Pawn), typeof(Pawn), typeof(bool), typeof(bool), typeof(GuestStatus?) });
                yield return AccessTools.Method(typeof(RestUtility), nameof(RestUtility.FindPatientBedFor),
                    new Type[] { typeof(Pawn) });
            }

            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return ReplaceCall(instructions, ClosestThingReachableMethod, TraversalClosestThingReachableMethod);
            }
        }

        [HarmonyPatch(typeof(JobGiver_Work), "TryIssueJobPackage")]
        public static class Patch_JobGiver_Work_ClosestThingReachable
        {
            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return ReplaceCall(
                    ReplaceCall(instructions, ClosestThingReachableMethod, TraversalClosestThingReachableMethod),
                    ClosestThingGlobalReachableMethod, TraversalClosestThingGlobalReachableMethod);
            }
        }

        [HarmonyPatch]
        public static class Patch_StoreUtility_CanReach
        {
            public static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(StoreUtility), nameof(StoreUtility.IsGoodStoreCell));
                yield return AccessTools.Method(typeof(StoreUtility), nameof(StoreUtility.TryFindBestBetterNonSlotGroupStorageFor));
            }

            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return ReplaceCall(instructions, LowLevelCanReachMethod, HaulDestinationCanReachMethod);
            }
        }

        [HarmonyPatch(typeof(Toils_Goto), nameof(Toils_Goto.GotoThing), new Type[] { typeof(TargetIndex), typeof(IntVec3) })]
        public static class Patch_Toils_Goto_GotoThingExact
        {
            [HarmonyPostfix]
            public static void Postfix(TargetIndex ind, IntVec3 exactCell, Toil __result)
            {
                ClimbUtility.AddClimbSupport(__result, ind, exactCell);
            }
        }

        [HarmonyPatch(typeof(Toils_Goto), nameof(Toils_Goto.GotoCell), new Type[] { typeof(TargetIndex), typeof(PathEndMode) })]
        public static class Patch_Toils_Goto_GotoCell
        {
            [HarmonyPostfix]
            public static void Postfix(TargetIndex ind, PathEndMode peMode, Toil __result)
            {
                ClimbUtility.AddClimbSupport(__result, ind, peMode);
            }
        }

        [HarmonyPatch(typeof(Toils_Goto), nameof(Toils_Goto.GotoCell), new Type[] { typeof(IntVec3), typeof(PathEndMode) })]
        public static class Patch_Toils_Goto_GotoCell_IntVec3
        {
            [HarmonyPostfix]
            public static void Postfix(IntVec3 cell, PathEndMode peMode, Toil __result)
            {
                ClimbUtility.AddClimbSupport(__result, cell, peMode);
            }
        }

        [HarmonyPatch(typeof(Toils_Bed), nameof(Toils_Bed.GotoBed))]
        public static class Patch_Toils_Bed_GotoBed
        {
            [HarmonyPostfix]
            public static void Postfix(TargetIndex bedIndex, Toil __result)
            {
                ClimbUtility.AddClimbSupport(__result, delegate (Pawn actor)
                {
                    Building_Bed bed = actor?.CurJob?.GetTarget(bedIndex).Thing as Building_Bed;
                    return bed == null
                        ? LocalTargetInfo.Invalid
                        : new LocalTargetInfo(RestUtility.GetBedSleepingSlotPosFor(actor, bed));
                }, PathEndMode.OnCell);
            }
        }

        [HarmonyPatch(typeof(Toils_Haul), nameof(Toils_Haul.CarryHauledThingToCell))]
        public static class Patch_Toils_Haul_CarryHauledThingToCell
        {
            [HarmonyPostfix]
            public static void Postfix(TargetIndex squareIndex, PathEndMode pathEndMode, Toil __result)
            {
                ClimbUtility.AddCarryClimbSupport(__result, squareIndex, pathEndMode);
            }
        }

        [HarmonyPatch(typeof(Toils_Haul), nameof(Toils_Haul.CarryHauledThingToContainer))]
        public static class Patch_Toils_Haul_CarryHauledThingToContainer
        {
            [HarmonyPostfix]
            public static void Postfix(Toil __result)
            {
                ClimbUtility.AddCarryClimbSupport(__result, TargetIndex.B, PathEndMode.Touch);
            }
        }

        [HarmonyPatch(typeof(FloatMenuOptionProvider_DraftedMove), "PawnCanGoto")]
        public static class Patch_FloatMenuOptionProvider_DraftedMove_PawnCanGoto_Diagnostic
        {
            [HarmonyPostfix]
            public static void Postfix(Pawn __0, IntVec3 __1, AcceptanceReport __result)
            {
                Pawn pawn = __0;
                IntVec3 destination = __1;
                if (!XMTSettings.LogClimbing || !TraversalReachabilityUtility.IsTraversalPawn(pawn))
                {
                    return;
                }

                bool normalReachable = ClimbUtility.OriginalCanReach(pawn, destination, PathEndMode.OnCell, Danger.Deadly);
                if (__result.Accepted && normalReachable)
                {
                    return;
                }

                bool traversalReachable = ClimbUtility.CanReachByWalkingOrClimb(
                    pawn, destination, PathEndMode.OnCell, Danger.Deadly);
                Log.Message("[XMT][Climbing] Drafted move gate for " + pawn + " to " + destination +
                    ": accepted=" + __result.Accepted + "; reason=" + (__result.Reason ?? "<none>") +
                    "; normalReachable=" + normalReachable + "; traversalReachable=" + traversalReachable +
                    "; " + ClimbUtility.GetClimbDecisionReport(pawn, destination, PathEndMode.OnCell));
            }
        }

        [HarmonyPatch(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.StartPath))]
        public static class Patch_PawnPathFollower_StartPath_Diagnostic
        {
            [HarmonyPrefix]
            public static void Prefix(Pawn ___pawn, LocalTargetInfo dest, PathEndMode peMode)
            {
                Toil currentToil = GetCurrentToil(___pawn?.jobs?.curDriver);
                if (!XMTSettings.LogClimbing || !TraversalReachabilityUtility.IsTraversalPawn(___pawn) ||
                    !dest.IsValid || !dest.Cell.InBounds(___pawn.Map) ||
                    ClimbUtility.HasClimbSupport(currentToil) ||
                    ClimbUtility.OriginalCanReach(___pawn, dest, peMode, ___pawn.NormalMaxDanger()) ||
                    !ClimbUtility.CanReachByWalkingOrClimb(___pawn, dest, peMode, ___pawn.NormalMaxDanger()))
                {
                    return;
                }

                int warningKey = ___pawn.thingIDNumber * 397 ^ dest.Cell.GetHashCode() ^ (___pawn.CurJob?.GetHashCode() ?? 0);
                Log.WarningOnce("[XMT][Climbing] " + ___pawn + " started an unwrapped vanilla path to traversal-only destination " + dest +
                    " with " + peMode + "; job=" + ___pawn.CurJob + ", driver=" + ___pawn.jobs?.curDriver?.GetType().FullName +
                    ", toil=" + currentToil?.debugName, warningKey);
            }
        }
    }
}
