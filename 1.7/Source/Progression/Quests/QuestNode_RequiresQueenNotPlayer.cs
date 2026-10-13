
using RimWorld.QuestGen;
using Verse;

namespace Xenomorphtype
{
    internal class QuestNode_RequiresQueenNotPlayer : QuestNode
    {
        protected override void RunInt()
        {

        }

        protected override bool TestRunInt(Slate slate)
        {
            bool hasQueen = XMTUtility.QueenIsPlayer();
            if (XMTSettings.LogWorld)
            {
                Log.Message("[XMT][World] checking if player has queen: " + hasQueen);
            }

            return !hasQueen;
        }
    }
}
