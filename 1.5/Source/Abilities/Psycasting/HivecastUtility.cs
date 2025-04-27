using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{ 
    internal class HivecastUtility
    {
        public static bool PsychicConnectionTest(Pawn subject, Pawn caster)
        {
            float subjectSensitivity = subject.GetStatValue(StatDefOf.PsychicSensitivity);
            float casterSensitivity = caster.GetStatValue(StatDefOf.PsychicSensitivity);

            int subjectLevel = subject.GetPsylinkLevel();
            int casterLevel = caster.GetPsylinkLevel();

            float cumulativeSensitvity = (casterSensitivity * subjectSensitivity) / 2;

            if (Rand.Chance(cumulativeSensitvity))
            {
                return true;
            }

            return false;
        }

        public static bool PsychicChallengeTest(Pawn subject, Pawn caster)
        {
            float subjectSensitivity = subject.GetStatValue(StatDefOf.PsychicSensitivity);
            float casterSensitivity = caster.GetStatValue(StatDefOf.PsychicSensitivity);

            int subjectLevel = subject.GetPsylinkLevel()+1;
            int casterLevel = caster.GetPsylinkLevel()+1;

            float difference = (subjectLevel*subjectSensitivity) - (casterLevel*casterSensitivity);

            if (difference > casterSensitivity)
            {
                float cumulativeSensitvity = (casterSensitivity * subjectSensitivity) / 2;
                if (Rand.Chance(cumulativeSensitvity))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
