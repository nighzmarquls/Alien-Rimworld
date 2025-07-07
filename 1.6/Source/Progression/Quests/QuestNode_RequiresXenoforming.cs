
using RimWorld.QuestGen;

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
            return XenoformingUtility.XenoformingMeets(minimumXenoforming);
        }
    }
}
