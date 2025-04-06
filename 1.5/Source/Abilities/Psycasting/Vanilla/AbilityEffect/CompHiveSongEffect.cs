using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Xenomorphtype
{
    public class CompHiveSongEffect : CompAbilityEffect_GiveHediff
    {
        Pawn caster => parent.pawn;
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if(!XMTUtility.IsXenomorph(caster))
            {
                CompPawnInfo info = caster.GetComp<CompPawnInfo>();
                if (info != null)
                {
                    info.WitnessPsychicHorror(0.1f);
                    info.GainObsession(0.05f);
                    if(info.IsObsessed())
                    {
                        caster.needs.joy.GainJoy(0.12f, InternalDefOf.Communion); 
                    }
                }
            }

            if (target.HasThing)
            {
                if (target.Thing is Pawn subject)
                {
                    if (XMTUtility.IsXenomorph(subject))
                    {
                        base.Apply(target, dest);
                        subject.needs.joy.GainJoy(0.5f, InternalDefOf.Communion);
                    }
                    else
                    {
                        CompPawnInfo info = subject.GetComp<CompPawnInfo>();
                        if (info != null)
                        {
                            info.WitnessPsychicHorror(0.1f);
                            info.GainObsession(0.05f);
                        
                            if (info.IsObsessed())
                            {
                                base.Apply(target, dest);
                                subject.needs.joy.GainJoy(0.5f, InternalDefOf.Communion);
                                XMTUtility.GiveMemory(subject, HorrorMoodDefOf.XMT_CommuneWithQueen);
                            }
                        }
                    }
                }
            }
        }
    }
}
