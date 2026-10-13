using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{
    public static class HarmfulAbilityUtility
    {
        public static void RegisterFactionHarm(Pawn caster, Thing target, HashSet<Faction> affectedFactions,
            int? fixedGoodwillChange = null, bool sendHostilityLetter = false)
        {
            Faction casterFaction = caster?.Faction;
            Faction targetFaction = target?.Faction;
            if (casterFaction == null || targetFaction == null || casterFaction == targetFaction
                || target.HostileTo(caster) || affectedFactions == null || !affectedFactions.Add(targetFaction))
            {
                return;
            }

            int goodwillChange = fixedGoodwillChange ?? targetFaction.GoodwillToMakeHostile(casterFaction);
            targetFaction.TryAffectGoodwillWith(casterFaction, goodwillChange, canSendMessage: true,
                canSendHostilityLetter: sendHostilityLetter, HistoryEventDefOf.UsedHarmfulAbility);
        }
    }
}
