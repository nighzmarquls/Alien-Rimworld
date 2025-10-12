
using RimWorld.QuestGen;

namespace Xenomorphtype
{
    internal class QuestNode_RequiresQueenNotPlayer : QuestNode
    {
        protected override void RunInt()
        {

        }

        protected override bool TestRunInt(Slate slate)
        {
            return !XMTUtility.QueenIsPlayer();
        }
    }
}
