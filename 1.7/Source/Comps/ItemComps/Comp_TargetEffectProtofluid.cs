
using RimWorld;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class Comp_TargetEffectProtofluid : CompTargetEffect
    {
        public CompProperties_TargetEffectProtofluid Props => props as CompProperties_TargetEffectProtofluid;
        public override void DoEffectOn(Pawn user, Thing target)
        {
            if (user.IsColonistPlayerControlled)
            {
                Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_Protofluid, target, parent);
                job.count = 1;
                job.playerForced = true;
                user.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }
        }
    }

    public class CompProperties_TargetEffectProtofluid : CompProperties
    {
        public ThingDef moteDef;

        public bool withSideEffects = true;

        public HediffDef addsHediff;

        public CompProperties_TargetEffectProtofluid()
        {
            compClass = typeof(Comp_TargetEffectProtofluid);
        }
    }
}
