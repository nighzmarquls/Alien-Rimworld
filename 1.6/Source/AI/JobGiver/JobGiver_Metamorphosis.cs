using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;

namespace Xenomorphtype
{
    public class JobGiver_Metamorphosis : ThinkNode_JobGiver
    {
        public SoundDef soundDefMale;

        public SoundDef soundDefFemale;

        protected override Job TryGiveJob(Pawn pawn)
        {
            PawnDuty duty = pawn.mindState.duty;
            if (duty == null)
            {
                return null;
            }

            Job job = JobMaker.MakeJob(XenoWorkDefOf.Ritual_Metamorphosis, duty.focusSecond);
            job.speechSoundMale = soundDefMale ?? SoundDefOf.Speech_Leader_Male;
            job.speechSoundFemale = soundDefFemale ?? SoundDefOf.Speech_Leader_Female;
            return job;
        }

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_Metamorphosis obj = (JobGiver_Metamorphosis)base.DeepCopy(resolve);
            obj.soundDefMale = soundDefMale;
            obj.soundDefFemale = soundDefFemale;
            return obj;
        }
    }
}
