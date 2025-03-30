using RimWorld;

using Verse;


namespace Xenomorphtype
{ 
    internal class RitualRoleQueen : RitualRoleColonist
    {

        public override bool AppliesToPawn(Pawn pawn, out string reason, TargetInfo selectedTarget, LordJob_Ritual ritual = null, RitualRoleAssignments assignments = null, Precept_Ritual precept = null, bool skipReason = false)
        {
            bool baseApply = base.AppliesToPawn(pawn, out reason, selectedTarget, ritual, assignments, precept, skipReason);

            if (XMTUtility.IsXenomorph(pawn) && baseApply)
            {
                if (XMTSettings.LogRituals)
                {
                    Log.Message(pawn + " is a xenomorph!");
                }
                if (XMTUtility.IsQueen(pawn))
                {
                    if (XMTSettings.LogRituals)
                    {
                        Log.Message(pawn + " is a queen!");
                    }
                    return true;
                }
                reason = "Not a queen.";
                return false;
            }
            else
            {
                reason = "Cannot biologically ascend.";
                return false;
            }
        }
    }
}
