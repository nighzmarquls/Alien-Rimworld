using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    [DefOf]
    internal static class XMT_BountyDefOf
    {
        public static TraitDef XMT_Slayer;
        public static MentalStateDef XMT_SlayerRage;
        public static ThoughtDef XMT_SlayerKill;
        static XMT_BountyDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(XMT_BountyDefOf)); }
    }
    public static class XMT_SlayerUtility
    {
        public static bool IsSlayer(Pawn pawn) => pawn?.story?.traits?.HasTrait(XMT_BountyDefOf.XMT_Slayer) == true;
        public static bool Eligible(Pawn observer, Thing target)
        {
            if (!IsSlayer(observer) || target is not Pawn pawn || pawn == observer || pawn.Dead || pawn.MapHeld != observer.MapHeld) return false;
            return XMTUtility.IsXenomorph(pawn) || XMTUtility.IsXenomorphFriendly(pawn)
                && KnowledgeUtility.GetAssessment(pawn).obsessionSeverity >= ObsessionSeverity.Obsessed;
        }
        public static bool TryRage(Pawn pawn, Thing target)
        {
            if (pawn == null || pawn.Drafted || pawn.Downed || !pawn.Spawned || pawn.InMentalState
                || !pawn.Awake() || pawn.WorkTagIsDisabled(WorkTags.Violent) || !Eligible(pawn, target)) return false;
            return pawn.mindState.mentalStateHandler.TryStartMentalState(XMT_BountyDefOf.XMT_SlayerRage,
                forceWake: false, causedByMood: false);
        }
        public static void Killed(Pawn killed, Pawn killer)
        {
            if (killed == null || killer == null) return;
            if (killed.GetComp<CompLarvalGenes>() != null && ModsConfig.IdeologyActive)
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(XenoPreceptDefOf.XMT_Larvae_Killed,
                    killer.Named(HistoryEventArgsNames.Doer), killed.Named(HistoryEventArgsNames.Victim)), true);
            if (!IsSlayer(killer) || !XMTUtility.IsXenomorph(killed) || killed.GetComp<CompLarvalGenes>() != null) return;
            PreceptDef abhorrent = DefDatabase<PreceptDef>.GetNamedSilentFail("XMT_Biomorph_Abhorrent");
            if (ModsConfig.IdeologyActive && abhorrent != null && killer.Ideo?.HasPrecept(abhorrent) == true) return;
            ThoughtDef memory = XMT_BountyDefOf.XMT_SlayerKill;
            killer.needs?.mood?.thoughts?.memories?.RemoveMemoriesOfDef(memory);
            killer.needs?.mood?.thoughts?.memories?.TryGainMemory(memory);
        }
    }
    public sealed class MentalState_XMTSlayer : MentalState_Berserk
    {
        public override bool ForceHostileTo(Thing thing) => XMT_SlayerUtility.Eligible(pawn, thing);
        public override bool ForceHostileTo(Faction faction) => false;
    }
    public sealed class JobGiver_XMTSlayer : JobGiver_AIFightEnemies
    {
        protected override Thing FindAttackTarget(Pawn pawn)
        {
            IAttackTarget target = AttackTargetFinder.BestAttackTarget(pawn, TargetScanFlags.NeedReachableIfCantHitFromMyPos,
                t => XMT_SlayerUtility.Eligible(pawn, t));
            return target?.Thing;
        }
    }
}
