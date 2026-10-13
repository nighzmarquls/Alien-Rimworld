using RimWorld;

using Verse;


namespace Xenomorphtype
{
    internal class RitualRoleXenomorph : RitualRoleColonist
    {
        public override bool AppliesToPawn(Pawn pawn, out string reason, TargetInfo selectedTarget, LordJob_Ritual ritual = null, RitualRoleAssignments assignments = null, Precept_Ritual precept = null, bool skipReason = false)
        {
            bool baseApply = base.AppliesToPawn(pawn, out reason, selectedTarget, ritual, assignments, precept, skipReason);

            if (XMTUtility.IsXenomorph(pawn) && baseApply)
            {
           
                if (XMTUtility.IsQueen(pawn))
                {
                    reason = "Already a queen.";
                    return false;
                }
       
                return true;
            }
            else
            {

                reason = "Cannot biologically ascend.";
                return false;
            }
        }
  
    }
}
