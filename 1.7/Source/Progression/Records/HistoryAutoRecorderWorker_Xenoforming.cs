using RimWorld;

namespace Xenomorphtype
{
    internal class HistoryAutoRecorderWorker_Xenoforming : HistoryAutoRecorderWorker
    {
        public override float PullRecord()
        {
            float num = XenoformingUtility.GetXenoforming();

            return num;
        }
    }
}
