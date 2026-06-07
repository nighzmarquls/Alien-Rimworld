using LudeonTK;
using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public static class DebugActions_Mischief
    {
        private const string Category = "Alien | Rimworld";

        [DebugActionYielder]
        private static IEnumerable<DebugActionNode> MischiefNodes()
        {
            DebugActionNode root = new DebugActionNode("Mischief", DebugActionType.Action, null);
            root.category = Category;
            root.childGetter = delegate
            {
                return new List<DebugActionNode>
                {
                    new DebugActionNode("Force random mischief", DebugActionType.Action, ForceMischief),
                    new DebugActionNode("Force offensive nest sabotage", DebugActionType.Action, ForceOffensiveNestMischief),
                    new DebugActionNode("Force door opening", DebugActionType.Action, ForceDoorOpeningMischief),
                    new DebugActionNode("Force roof breaking", DebugActionType.Action, ForceRoofBreakingMischief),
                    new DebugActionNode("Force power sabotage", DebugActionType.Action, ForcePowerSabotageMischief),
                    new DebugActionNode("Force acid blood spill", DebugActionType.Action, ForceAcidBloodMischief),
                    new DebugActionNode("Report candidates", DebugActionType.Action, ReportMischiefCandidates)
                };
            };

            yield return root;
        }

        private static void ForceMischief()
        {
            BeginCryptimorphTargeting("Select cryptimorph to force mischief.", delegate(Pawn pawn)
            {
                CompMatureMorph comp = pawn.GetMorphComp();
                comp.ClearMischiefCooldowns();
                if (comp.FindMichief(out Job job))
                {
                    string createdDefName = JobDefName(job);
                    Log.Message("[Alien|Rimworld] Forced mischief created for " + pawn + ": def=" + createdDefName + ", driver=" + JobDriverName(job) + ", job=" + JobLabel(job));
                    pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                    Log.Message("[Alien|Rimworld] Forced mischief after StartJob for " + pawn + ": createdDef=" + createdDefName + ", createdDriver=" + JobDriverName(job) + ", curDef=" + JobDefName(pawn.CurJob) + ", curDriver=" + JobDriverName(pawn.CurJob) + ", curJob=" + JobLabel(pawn.CurJob));
                    Messages.Message("Forced mischief: " + createdDefName, MessageTypeDefOf.TaskCompletion, false);
                }
                else
                {
                    Log.Message("[Alien|Rimworld] No mischief found for " + pawn + ".\n" + XMTMischiefUtility.ReportMischiefCandidates(pawn));
                    Messages.Message("No mischief candidate found; wrote report to log.", MessageTypeDefOf.RejectInput, false);
                }
            });
        }

        private static void ForceOffensiveNestMischief()
        {
            ForceSpecificMischief("offensive nest sabotage", XMTMischiefUtility.TryFindOffensiveNestMischief);
        }

        private static void ForceDoorOpeningMischief()
        {
            ForceSpecificMischief("door opening", XMTMischiefUtility.TryFindDoorMischief);
        }

        private static void ForceRoofBreakingMischief()
        {
            ForceSpecificMischief("roof breaking", XMTMischiefUtility.TryFindRoofBreakMischief);
        }

        private static void ForcePowerSabotageMischief()
        {
            ForceSpecificMischief("power sabotage", XMTMischiefUtility.TryFindPowerSabotageMischief);
        }

        private static void ForceAcidBloodMischief()
        {
            BeginCryptimorphTargeting("Select cryptimorph to force acid blood mischief.", delegate(Pawn pawn)
            {
                CompMatureMorph comp = pawn.GetMorphComp();
                if (comp.TryAcidBloodMischief(ignoreCooldown: true))
                {
                    Log.Message("[Alien|Rimworld] Forced acid blood mischief for " + pawn + ".");
                    Messages.Message("Forced acid blood mischief.", MessageTypeDefOf.TaskCompletion, false);
                }
                else
                {
                    Log.Message("[Alien|Rimworld] Acid blood mischief failed condition checks for " + pawn + ".");
                    Messages.Message("Acid blood mischief failed condition checks.", MessageTypeDefOf.RejectInput, false);
                }
            });
        }

        private static void ReportMischiefCandidates()
        {
            BeginCryptimorphTargeting("Select cryptimorph to report mischief candidates.", delegate(Pawn pawn)
            {
                string report = XMTMischiefUtility.ReportMischiefCandidates(pawn);
                Log.Message(report);
                Messages.Message("Wrote mischief candidate report to log.", MessageTypeDefOf.TaskCompletion, false);
            });
        }

        private static void ForceSpecificMischief(string label, TryFindMischiefJob tryFind)
        {
            BeginCryptimorphTargeting("Select cryptimorph to force " + label + ".", delegate(Pawn pawn)
            {
                pawn.GetMorphComp().ClearMischiefCooldowns();
                if (tryFind(pawn, out Job job, out string reason))
                {
                    string createdDefName = JobDefName(job);
                    Log.Message("[Alien|Rimworld] Forced " + label + " mischief created for " + pawn + ": def=" + createdDefName + ", driver=" + JobDriverName(job) + ", job=" + JobLabel(job) + " - " + reason);
                    pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                    Log.Message("[Alien|Rimworld] Forced " + label + " mischief after StartJob for " + pawn + ": createdDef=" + createdDefName + ", createdDriver=" + JobDriverName(job) + ", curDef=" + JobDefName(pawn.CurJob) + ", curDriver=" + JobDriverName(pawn.CurJob) + ", curJob=" + JobLabel(pawn.CurJob));
                    Messages.Message("Forced " + label + ": " + createdDefName, MessageTypeDefOf.TaskCompletion, false);
                }
                else
                {
                    Log.Message("[Alien|Rimworld] No " + label + " mischief found for " + pawn + ": " + reason);
                    Messages.Message("No " + label + " candidate found; wrote reason to log.", MessageTypeDefOf.RejectInput, false);
                }
            });
        }

        private static void BeginCryptimorphTargeting(string prompt, System.Action<Pawn> onSelected)
        {
            Messages.Message(prompt, MessageTypeDefOf.NeutralEvent, false);
            TargetingParameters targetingParameters = new TargetingParameters
            {
                canTargetPawns = true,
                canTargetBuildings = false,
                canTargetItems = false,
                canTargetLocations = false,
                validator = target => target.Thing is Pawn pawn && XMTUtility.IsXenomorph(pawn) && pawn.GetMorphComp() != null
            };

            Find.Targeter.BeginTargeting(targetingParameters, delegate(LocalTargetInfo target)
            {
                if (target.Thing is Pawn pawn && XMTUtility.IsXenomorph(pawn) && pawn.GetMorphComp() != null)
                {
                    onSelected(pawn);
                }
            });
        }

        private static string JobDefName(Job job)
        {
            return job?.def == null ? "null" : job.def.defName;
        }

        private static string JobLabel(Job job)
        {
            return job == null ? "null" : job.ToString();
        }

        private static string JobDriverName(Job job)
        {
            return job?.def?.driverClass == null ? "null" : job.def.driverClass.FullName;
        }

        private delegate bool TryFindMischiefJob(Pawn pawn, out Job job, out string reason);
    }
}
