
using RimWorld.QuestGen;
using Verse;

namespace Xenomorphtype
{
    internal class QuestNode_RequiresXenoforming : QuestNode
    {
        private float minimumXenoforming = 1;
        protected override void RunInt()
        {
            
        }

        protected override bool TestRunInt(Slate slate)
        {
            
            bool isGood = XenoformingUtility.XenoformingMeets(minimumXenoforming);
            if (XMTSettings.LogWorld)
            {
                Log.Message("checking Xenoforming: " + isGood);
            }
            return isGood;
        }
    }
}
