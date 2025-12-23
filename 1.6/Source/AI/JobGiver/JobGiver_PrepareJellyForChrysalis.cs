using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    internal class JobGiver_PrepareJellyForChrysalis : JobGiver_GotoAndStandSociallyActive
    {
        public ThingDef def;

        public int count = 1;

        protected override Job TryGiveJob(Pawn pawn)
        {
            IntVec3 dest = GetDest(pawn);
            Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_Ritual_PrepareJelly, dest);
            job.locomotionUrgency = locomotionUrgency;
            job.expiryInterval = expiryInterval;
            job.checkOverrideOnExpire = true;
            job.thingDefToCarry = def;
            job.count = count;
            return job;
        }

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_PrepareJellyForChrysalis obj = (JobGiver_PrepareJellyForChrysalis)base.DeepCopy(resolve);
            obj.def = def;
            obj.count = count;
            return obj;
        }
    }
}
