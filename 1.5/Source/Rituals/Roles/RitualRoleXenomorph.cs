using RimWorld;

using Verse;


namespace Xenomorphtype
{
    internal class RitualRoleXenomorph : RitualRoleColonist
    {
        public override bool AppliesToPawn(Pawn pawn, out string reason, TargetInfo selectedTarget, LordJob_Ritual ritual = null, RitualRoleAssignments assignments = null, Precept_Ritual precept = null, bool skipReason = false)
        {
            bool baseApply = base.AppliesToPawn(pawn, out reason, selectedTarget, ritual, assignments, precept, skipReason);

            if (XMTSettings.LogRituals)
            {
                Log.Message("checking " + pawn + " for role of xenomorph");
            }
            if (XMTUtility.IsXenomorph(pawn) && baseApply)
            {
                if (XMTSettings.LogRituals)
                {
                    Log.Message(pawn + " is a xenomorph");
                }
                if (XMTUtility.IsQueen(pawn))
                {
                    reason = "Already a queen.";
                    return false;
                }
                if (XMTSettings.LogRituals)
                {
                    Log.Message(pawn + " is not a queen");
                }
                return true;
            }
            else
            {
                if (XMTSettings.LogRituals)
                {
                    Log.Message(pawn + " is not a xenomorph");
                }
                reason = "Cannot biologically ascend.";
                return false;
            }
        }
  
    }
}
