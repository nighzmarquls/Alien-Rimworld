using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class RitualObligationTargetWorker_MatureChrysalis : RitualObligationTargetFilter
    {
        private static readonly HashSet<Pawn> blocked = new HashSet<Pawn>();
        public RitualObligationTargetWorker_MatureChrysalis()
        {
        }

        public RitualObligationTargetWorker_MatureChrysalis(RitualObligationTargetFilterDef def)
            : base(def)
        {
        }
        public override IEnumerable<string> GetTargetInfos(RitualObligation obligation)
        {
            yield return "Fully prepared";
        }
        public override IEnumerable<TargetInfo> GetTargets(RitualObligation obligation, Map map)
        {
            IEnumerable<FillableChrysalis> candidates = map.listerBuildings.AllBuildingsColonistOfClass<FillableChrysalis>();
            foreach (FillableChrysalis candidate in candidates)
            {
                if (candidate.Filled)
                {
                    yield return candidate;
                }
            }
        }

        protected override RitualTargetUseReport CanUseTargetInternal(TargetInfo target, RitualObligation obligation)
        {

            FillableChrysalis targetChrysalis = target.Thing as FillableChrysalis;
            if (targetChrysalis == null)
            {
                return false;
            }

            if (!targetChrysalis.Filled)
            {
                return false;
            }

            return true;
        }

        public override bool ShouldGrayOut(Pawn pawn, ILordJobAssignmentsManager<RitualRole> assignments, out TaggedString reason)
        {
            if (blocked.Contains(pawn) && assignments.PawnParticipating(pawn))
            {
                reason = "XMT_RitualTargetJellyInfoWarning".Translate(pawn.Named("PAWN"));
                return true;
            }

            reason = TaggedString.Empty;
            return false;
        }
        public override IEnumerable<string> GetBlockingIssues(TargetInfo target, RitualRoleAssignments assignments)
        {
            Dictionary<Thing, int> dictionary = new Dictionary<Thing, int>();
            List<Thing> jellyList = target.Map.listerThings.ThingsOfDef(InternalDefOf.Starbeast_Jelly);
            bool needMoreJelly = true;
            int num = 0;
            blocked.Clear();
            List<Pawn> participants = assignments.SpectatorsForReading;
            foreach (Pawn participant in participants)
            {
                int neededJelly = Math.Max(def.woodPerParticipant - participant.inventory.Count(InternalDefOf.Starbeast_Jelly), 0);
                bool haveJelly = false;
                for (int i = 0; i < jellyList.Count; i++)
                {
                    Thing jelly = jellyList[i];
                    if (jelly.IsForbidden(participant) || !participant.CanReserveAndReach(jelly, PathEndMode.Touch, participant.NormalMaxDanger()))
                    {
                        continue;
                    }

                    int stackCount = jelly.stackCount;
                    if (dictionary.TryGetValue(jelly, out var value))
                    {
                        stackCount = Math.Max(stackCount - value, 0);
                    }

                    if (stackCount >= neededJelly)
                    {
                        if (dictionary.ContainsKey(jelly))
                        {
                            dictionary[jelly] += neededJelly;
                        }
                        else
                        {
                            dictionary[jelly] = neededJelly;
                        }

                        haveJelly = true;
                        break;
                    }
                }

                if (!haveJelly)
                {
                    blocked.Add(participant);
                    needMoreJelly = false;
                    num++;
                }
            }

            if (!needMoreJelly)
            {
                TaggedString taggedString = "XMT_RitualTargetJellyInfo".Translate(def.woodPerParticipant, def.woodPerParticipant * participants.Count());
                if (num == 1)
                {
                    taggedString += string.Format(" {0}", "XMT_RitualTargetJellyInfoAppend".Translate(num));
                }
                else
                {
                    taggedString += string.Format(" {0}", "XMT_RitualTargetJellyInfoAppendMultiple".Translate(num));
                }

                yield return taggedString;
            }
        }
    }
}
