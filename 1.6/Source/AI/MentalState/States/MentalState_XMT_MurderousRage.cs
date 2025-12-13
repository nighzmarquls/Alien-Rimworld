
using RimWorld;
using Verse.AI;
using Verse;

namespace Xenomorphtype
{
    internal class MentalState_XMT_MurderousRage : MentalState
    {
        public Thing target;

        private const int NoLongerValidTargetCheckInterval = 120;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref target, "target");
        }

        public override RandomSocialMode SocialModeMax()
        {
            return RandomSocialMode.Off;
        }

        public override void PreStart()
        {
            base.PreStart();
            TryFindNewTarget();
        }

        public override void MentalStateTick(int delta)
        {
            if (target == null || ( target is Pawn alive && alive.Dead) || target.Destroyed)
            {
                RecoverFromState();
            }

            if (pawn.IsHashIntervalTick(120) && !IsTargetStillValidAndReachable())
            {
                if (!TryFindNewTarget())
                {
                    RecoverFromState();
                    return;
                }
                Messages.Message("MessageMurderousRageChangedTarget".Translate(pawn.NameShortColored, target.Label, pawn.Named("PAWN"), target.Named("TARGET")).Resolve().AdjustedFor(pawn), pawn, MessageTypeDefOf.NegativeEvent);
                base.MentalStateTick(delta);
            }
        }
        public override TaggedString GetBeginLetterText()
        {
            if (target == null)
            {
                Log.Error("No target. This should have been checked in this mental state's worker.");
                return "";
            }
            return def.beginLetter.Formatted(pawn.NameShortColored, target.Label, pawn.Named("PAWN"), target.Named("TARGET")).AdjustedFor(pawn).Resolve()
            .CapitalizeFirst();
        }
        private bool TryFindNewTarget()
        {
            if (XMTUtility.IsXenomorph(pawn) || pawn.Info().IsObsessed())
            {
                target = XMTMentalStateUtility.FindXenoEnemyToKill(pawn);
                return target != null;
            }
            else
            {
                target = XMTMentalStateUtility.FindXenoToKill(pawn);
                return target != null;
            }
        }

        public bool IsTargetStillValidAndReachable()
        {
            if (target != null && target.SpawnedParentOrMe != null && (!(target.SpawnedParentOrMe is Pawn) || target.SpawnedParentOrMe == target))
            {
                return pawn.CanReach(target.SpawnedParentOrMe, PathEndMode.Touch, Danger.Deadly, canBashDoors: true);
            }

            return false;
        }
    }
}